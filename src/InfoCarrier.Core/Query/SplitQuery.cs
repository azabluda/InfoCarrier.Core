// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;

namespace InfoCarrier.Core.Query;

/// <summary>
///     One query the client sends to the server (<c>docs/projection-split.md</c> §3.5).
/// </summary>
/// <param name="Query">The rebindable expression, rooted in at least one entity query root.</param>
/// <param name="ElementType">
///     The type each result row materializes as — the <em>boundary</em> element type, which is
///     not the type the caller asked for whenever a residual exists.
/// </param>
/// <param name="ReturnsSingleResult">
///     Whether the server returns one value rather than a sequence. Read from the shipped query,
///     never from the original: <c>…Select(c =&gt; new { … }).First()</c> ships a sequence and the
///     residual takes the first.
/// </param>
public sealed record ServerQuery(Expression Query, Type ElementType, bool ReturnsSingleResult);

/// <summary>
///     A captured query divided into the parts the server executes and the part the client
///     applies to the results ([ADR-010](../../../docs/decisions.md)).
/// </summary>
public sealed class SplitQuery
{
    private readonly LambdaExpression _residual;
    private Delegate? _compiled;

    internal SplitQuery(IReadOnlyList<ServerQuery> serverQueries, LambdaExpression residual, bool isPassThrough)
    {
        ServerQueries = serverQueries;
        _residual = residual;
        IsPassThrough = isPassThrough;
    }

    /// <summary>
    ///     The queries to send, in the order <see cref="Apply" /> expects their results.
    /// </summary>
    public IReadOnlyList<ServerQuery> ServerQueries { get; }

    /// <summary>
    ///     Whether the whole query goes to the server and the results need no further work —
    ///     the common case, which must stay as cheap as it was before the split existed.
    /// </summary>
    public bool IsPassThrough { get; }

    /// <summary>
    ///     The client-side remainder, as a lambda over one parameter per
    ///     <see cref="ServerQueries" /> entry.
    /// </summary>
    public LambdaExpression Residual => _residual;

    /// <summary>
    ///     Applies the residual to materialized server results.
    /// </summary>
    /// <remarks>
    ///     Evaluation is LINQ-to-Objects: each sequence arrives as an <c>EnumerableQuery</c>, whose
    ///     provider rewrites the residual's <see cref="Queryable" /> calls to
    ///     <see cref="Enumerable" /> ones itself. That is why the residual keeps its original
    ///     shape instead of being rewritten here.
    /// </remarks>
    public object? Apply(IReadOnlyList<object?> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (results.Count != ServerQueries.Count)
        {
            throw new ArgumentException(
                $"Expected {ServerQueries.Count} server result(s), got {results.Count}.", nameof(results));
        }

        _compiled ??= _residual.Compile();

        try
        {
            return _compiled.DynamicInvoke([.. results]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Reflection is this class's own business, never part of the contract. A caller
            // asserting InvalidOperationException from a client-side projection would otherwise
            // get TargetInvocationException. Rethrow the original with its stack intact rather
            // than `throw ex.InnerException`, which would reset it to this line.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // Unreachable; the compiler cannot see that Throw() does not return.
        }
    }
}
