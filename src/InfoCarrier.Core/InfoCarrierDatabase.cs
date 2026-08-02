// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace InfoCarrier.Core;

/// <summary>
///     The InfoCarrier client provider's <see cref="IDatabase" />: the raw-capture query entry
///     point (ADR-006). <see cref="CompileQuery{TResult}" /> intercepts the LINQ tree before
///     EF's translation pipeline, substitutes compiled-query parameters as plain constants
///     (research-findings §6), serializes the tree, and ships it to the server. SaveChanges
///     lands in Step 10.
/// </summary>
public class InfoCarrierDatabase : IDatabase
{
    private static readonly System.Reflection.MethodInfo ExecuteQueryMethod
        = typeof(InfoCarrierDatabase).GetMethod(nameof(ExecuteQuery), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    private readonly IInfoCarrierClient _client;
    private readonly IExpressionSerializer _expressionSerializer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierDatabase" /> class.
    /// </summary>
    public InfoCarrierDatabase(IDbContextOptions options, IExpressionSerializer expressionSerializer)
    {
        _client = options.Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .First()
            .InfoCarrierClient!;
        _expressionSerializer = expressionSerializer;
    }

    /// <summary>
    ///     The client used to ship operations to the server.
    /// </summary>
    protected IInfoCarrierClient Client => _client;

    /// <inheritdoc />
    public virtual Func<QueryContext, TResult> CompileQuery<TResult>(Expression query, bool async)
    {
        // Determine the element type and whether the query returns a single result.
        Type resultType = typeof(TResult);
        bool singleResult = QueryReturnsSingleResult(query);
        Type elementType = singleResult
            ? (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>)
                ? resultType.GetGenericArguments()[0]
                : resultType)
            : (resultType == typeof(IEnumerable<object>) || !resultType.IsGenericType
                ? typeof(object)
                : resultType.GetGenericArguments()[0]);

        var executeQuery = ExecuteQueryMethod
            .MakeGenericMethod(elementType)
            .CreateDelegate<Func<QueryContext, Expression, IInfoCarrierClient, IExpressionSerializer, bool, bool, object>>();

        return queryContext => (TResult)executeQuery(queryContext, query, _client, _expressionSerializer, async, singleResult);
    }

    private static object ExecuteQuery<TElement>(
        QueryContext queryContext,
        Expression query,
        IInfoCarrierClient client,
        IExpressionSerializer expressionSerializer,
        bool async,
        bool singleResult)
        => new QueryExecutor<TElement>(queryContext, query, client, expressionSerializer)
            .Execute(async, singleResult);

    private static bool QueryReturnsSingleResult(Expression query)
    {
        Type type = query.Type;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            type = type.GetGenericArguments()[0];
        }

        // A sequence query is typed IQueryable<T>; everything else is one result. Testing
        // IEnumerable instead conflated the two whenever the single result was itself a
        // sequence — `…Take(1).Select(c => c.Orders.Select(o => o.OrderID)).First()` is typed
        // IEnumerable<int> — so the async path handed EF an IAsyncEnumerable where it wanted a
        // Task<IEnumerable<int>>, and the cast failed. (`string` needed a special case under
        // the old test and needs none under this one.)
        return !typeof(IQueryable).IsAssignableFrom(type);
    }

    /// <inheritdoc />
    public virtual Expression<Func<QueryContext, TResult>> CompileQueryExpression<TResult>(Expression query, bool async)
        => throw new NotImplementedException("Precompiled queries are not supported by InfoCarrier.");

    /// <inheritdoc />
    public virtual int SaveChanges(IList<IUpdateEntry> entries)
        => SaveChangesAsync(entries).GetAwaiter().GetResult();

    /// <inheritdoc />
    public virtual async Task<int> SaveChangesAsync(
        IList<IUpdateEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var mapper = (Expressions.DynamicValueMapper)((ExpressionSerializer)_expressionSerializer).ValueMapper;
        mapper.ResetReferenceScope();

        // Relationships travel by correlation id, so every entry has to be identifiable before
        // any of them is written.
        var correlationByEntity = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < entries.Count; i++)
        {
            correlationByEntity[entries[i].ToEntityEntry().Entity] = i;
        }

        var request = new Common.SaveChangesRequest
        {
            Entries = [.. entries.Select((e, i) => ChangeEntryMapper.ToChangeEntry(e, i, mapper, correlationByEntity))],
        };

        Common.SaveChangesResult result = await _client
            .SaveChangesAsync(request, cancellationToken)
            .ConfigureAwait(false);

        ApplyGeneratedValues(entries, result, mapper);
        return result.Count;
    }

    /// <summary>
    ///     Writes store-generated values back onto the client's entries.
    /// </summary>
    /// <remarks>
    ///     Keyed by correlation id rather than by key value, because the whole point is that the
    ///     client's key was temporary and the server's is not (research-findings §9). Setting the
    ///     current value is what lets EF's own <c>AcceptAllChanges</c> replace the temporary key
    ///     and fix up everything that referenced it.
    /// </remarks>
    private static void ApplyGeneratedValues(
        IList<IUpdateEntry> entries,
        Common.SaveChangesResult result,
        Expressions.DynamicValueMapper mapper)
    {
        foreach (Common.GeneratedValues generated in result.GeneratedValues)
        {
            if (generated.CorrelationId < 0 || generated.CorrelationId >= entries.Count)
            {
                continue;
            }

            IUpdateEntry entry = entries[generated.CorrelationId];
            var entityType = (Microsoft.EntityFrameworkCore.Metadata.IEntityType)entry.EntityType;

            foreach (Expressions.DynamicPropertyValue value in ChangeEntryMapper.ReadValues(generated.SerializedValues))
            {
                if (entityType.FindProperty(value.Name) is { } property)
                {
                    entry.SetStoreGeneratedValue(property, mapper.FromPropertyValue(value, property.ClrType));
                }
            }
        }
    }
}
