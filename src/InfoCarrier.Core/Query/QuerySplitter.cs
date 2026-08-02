// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Divides a captured query into what the server executes and what the client applies to the
///     results (<c>docs/projection-split.md</c>, [ADR-010](../../../docs/decisions.md)).
/// </summary>
/// <remarks>
///     The split runs on the client, before serialization, because the allowlist rejects during
///     <em>deserialization</em>: a tree naming an anonymous type throws before the server has an
///     expression to analyze. This must therefore run <b>after</b> query parameters are
///     substituted — a surviving closure field access would name a compiler-generated display
///     class and push the boundary in for no reason.
/// </remarks>
public sealed class QuerySplitter
{
    private static readonly HashSet<string> QueryMarkers =
    [
        nameof(EntityFrameworkQueryableExtensions.AsTracking),
        nameof(EntityFrameworkQueryableExtensions.AsNoTracking),
        nameof(EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution),
        nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters),
        nameof(EntityFrameworkQueryableExtensions.IgnoreAutoIncludes),
        "AsSplitQuery",
        "AsSingleQuery",
    ];

    private readonly IModel _model;
    private readonly ServerBoundaryAnalyzer _analyzer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="QuerySplitter" /> class.
    /// </summary>
    /// <param name="model">The client model — the same entity types the server has.</param>
    /// <param name="allowlist">
    ///     The types the server accepts. Defaults to one derived from <paramref name="model" />,
    ///     which is what the server derives from its own.
    /// </param>
    public QuerySplitter(IModel model, TypeAllowlist? allowlist = null)
    {
        _model = model;
        _analyzer = new ServerBoundaryAnalyzer(allowlist ?? TypeAllowlist.ForModel(model));
    }

    /// <summary>
    ///     Splits a captured query.
    /// </summary>
    public SplitQuery Split(Expression query)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Rewrite client-typed projections into a server-side tuple plus a client-side
        // reassembly *before* looking for the boundary (§3.2). Cutting above such a projection
        // is not merely coarse — it strands navigation reads and correlated subqueries on the
        // client, and it decomposes a `GroupBy` from the aggregate that makes it translatable.
        query = ProjectionRewriter.Rewrite(query, _analyzer);

        BoundaryAnalysis analysis = _analyzer.Analyze(query);

        if (analysis.IsWhollyServerExecutable)
        {
            // Nothing to do on the client. Keep the residual an identity so callers have one
            // shape to handle, and flag it so they can skip the work entirely.
            ParameterExpression only = Expression.Parameter(query.Type, "server");
            return new SplitQuery(
                [ToServerQuery(query)],
                Expression.Lambda(only, only),
                isPassThrough: true);
        }

        if (analysis.Shippable.Count == 0)
        {
            throw new InvalidOperationException(
                $"No part of the query can be executed on the server: '{query}'. This usually means "
                    + "the query root itself names a type the server does not know.");
        }

        RejectOpenFragments(analysis);

        // Substitute each shipped subtree with a parameter the caller binds to its results.
        var parameters = new List<ParameterExpression>(analysis.Shippable.Count);
        var substitutions = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < analysis.Shippable.Count; i++)
        {
            Expression shipped = analysis.Shippable[i];

            // Bind by the interface, not by the shipped expression's exact type. A subtree ending
            // in `Include(c => c.Orders)` is typed IIncludableQueryable, which the materialized
            // EnumerableQuery does not implement — the residual then failed at invocation time
            // with a cast complaint far from the cause. Every operator the residual can apply
            // takes IQueryable<T> anyway.
            var parameter = Expression.Parameter(
                typeof(IQueryable).IsAssignableFrom(shipped.Type)
                    ? typeof(IQueryable<>).MakeGenericType(ElementTypeOf(shipped.Type))
                    : shipped.Type,
                $"server{i}");
            parameters.Add(parameter);
            substitutions[shipped] = parameter;
        }

        Expression residualBody = new SubstitutingVisitor(substitutions).Visit(query)!;
        residualBody = new MarkerStrippingVisitor().Visit(residualBody)!;

        RejectClientSideEfProperty(residualBody);

        IReadOnlyList<Expression> augmented = AugmentWithNavigations(analysis.Shippable, residualBody);

        return new SplitQuery(
            [.. augmented.Select(ToServerQuery)],
            Expression.Lambda(residualBody, parameters),
            isPassThrough: false);
    }

    private static ServerQuery ToServerQuery(Expression query)
    {
        bool sequence = typeof(IQueryable).IsAssignableFrom(query.Type);
        return new ServerQuery(query, sequence ? ElementTypeOf(query.Type) : query.Type, !sequence);
    }

    private static Type ElementTypeOf(Type queryableType)
        => (queryableType.IsGenericType && queryableType.GetGenericTypeDefinition() == typeof(IQueryable<>)
                ? queryableType
                : queryableType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>)))
            ?.GetGenericArguments()[0]
            ?? queryableType;

    private static void RejectOpenFragments(BoundaryAnalysis analysis)
    {
        if (analysis.OpenFragments.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The subquery '{analysis.OpenFragments[0]}' can only be evaluated on the server, but it "
                + "reads a value from the client-side projection that encloses it. Evaluating it on the "
                + "client would issue one query per row. Correlated subqueries under a client-side "
                + "projection are not supported yet (docs/projection-split.md §3.2, milestone M2-B).");
    }

    private static void RejectClientSideEfProperty(Expression residual)
    {
        MethodCallExpression? found = EfPropertyFinder.Find(residual);
        if (found is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"'{found}' ended up on the client side of the projection boundary, where it cannot be "
                + "evaluated: EF.Property reads state the client does not hold. Move it into the "
                + "server-side part of the query (docs/projection-split.md §3.4).");
    }

    /// <summary>
    ///     Makes sure a navigation the residual reads is actually on the wire (§3.6).
    /// </summary>
    /// <remarks>
    ///     This is the one failure mode of a plain cut that would not announce itself.
    ///     <c>Select(c =&gt; new { c.City, Count = c.Orders.Count })</c> cut at the projection ships
    ///     <c>Customer</c> entities whose <c>Orders</c> were never loaded, and the client answers
    ///     <b>0</b> — no exception, no log line, a plausible wrong number. Adding the
    ///     <c>Include</c> over-fetches; that is the accepted trade, and phase B removes the need
    ///     for it by evaluating such fragments on the server instead.
    /// </remarks>
    private IReadOnlyList<Expression> AugmentWithNavigations(IReadOnlyList<Expression> shipped, Expression residual)
    {
        List<NavigationRead> reads = NavigationReadFinder.Find(residual, _model);
        if (reads.Count == 0)
        {
            return shipped;
        }

        var result = new List<Expression>(shipped);
        foreach (NavigationRead read in reads)
        {
            bool placed = false;
            for (int i = 0; i < result.Count; i++)
            {
                if (ElementTypeOf(result[i].Type) != read.Owner.ClrType
                    || !typeof(IQueryable).IsAssignableFrom(result[i].Type))
                {
                    continue;
                }

                result[i] = Include(result[i], read.Owner.ClrType, read.Path);
                placed = true;
            }

            if (!placed)
            {
                throw new InvalidOperationException(
                    $"The client-side part of the query reads navigation '{read.Owner.DisplayName()}."
                        + $"{read.Path}', but no query sent to the server returns "
                        + $"'{read.Owner.DisplayName()}' rows that could carry it. Reading it on the "
                        + "client would silently yield an empty value "
                        + "(docs/projection-split.md §3.6).");
            }
        }

        return result;
    }

    private static Expression Include(Expression source, Type entityType, string path)
    {
        // The string overload takes the whole dotted path, so no ThenInclude chain has to be
        // reconstructed. Duplicates are harmless: EF folds identical include paths.
        MethodInfo include = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                && m.GetParameters() is [_, { ParameterType: var second }]
                && second == typeof(string))
            .MakeGenericMethod(entityType);

        return Expression.Call(include, source, Expression.Constant(path));
    }

    private sealed record NavigationRead(IEntityType Owner, string Path);

    private sealed class SubstitutingVisitor(IReadOnlyDictionary<Expression, Expression> substitutions)
        : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
            => node is not null && substitutions.TryGetValue(node, out Expression? replacement)
                ? replacement
                : base.Visit(node);
    }

    /// <summary>
    ///     Removes EF's queryable markers from the client-side remainder.
    /// </summary>
    /// <remarks>
    ///     <c>AsNoTracking</c> and friends have no <see cref="Enumerable" /> counterpart, so
    ///     <c>EnumerableQuery</c>'s rewriter fails on them. Tracking behavior has already been
    ///     read off the original tree by the time this runs.
    /// </remarks>
    private sealed class MarkerStrippingVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
            => node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
                && node.Arguments.Count == 1
                && QueryMarkers.Contains(node.Method.Name)
                    ? Visit(node.Arguments[0])
                    : base.VisitMethodCall(node);
    }

    private sealed class EfPropertyFinder : ExpressionVisitor
    {
        private MethodCallExpression? _found;

        public static MethodCallExpression? Find(Expression expression)
        {
            var finder = new EfPropertyFinder();
            finder.Visit(expression);
            return finder._found;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (_found is null
                && node.Method.DeclaringType == typeof(EF)
                && node.Method.Name == nameof(EF.Property))
            {
                _found = node;
            }

            return _found is null ? base.VisitMethodCall(node) : node;
        }
    }

    /// <summary>
    ///     Every navigation the residual reads, as a dotted path from the entity type it is
    ///     rooted at.
    /// </summary>
    /// <remarks>
    ///     Syntactic and deliberately over-approximate (§3.6): chains are followed only while
    ///     they are unbroken member accesses, and the result is keyed by the entity type read
    ///     from rather than by tracing where that entity came from. An extra <c>Include</c> costs
    ///     bandwidth; a missing one costs correctness.
    /// </remarks>
    private sealed class NavigationReadFinder(IModel model) : ExpressionVisitor
    {
        private readonly List<NavigationRead> _reads = [];

        public static List<NavigationRead> Find(Expression expression, IModel model)
        {
            var finder = new NavigationReadFinder(model);
            finder.Visit(expression);

            // `b.Author.Books` records both "Author.Books" and, on the way down, "Author".
            // Including a path loads every step of it, so the prefix is dead weight on the wire.
            return
            [
                .. finder._reads.Where(read => !finder._reads.Any(
                    other => other.Owner == read.Owner
                        && other.Path.StartsWith(read.Path + ".", StringComparison.Ordinal))),
            ];
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (TryDescribe(node, out IEntityType? owner, out string? path)
                && !_reads.Any(r => r.Owner == owner && r.Path == path))
            {
                _reads.Add(new NavigationRead(owner, path));
            }

            return base.VisitMember(node);
        }

        private bool TryDescribe(MemberExpression node, out IEntityType? owner, out string? path)
        {
            owner = null;
            path = null;

            if (node.Expression is null)
            {
                return false;
            }

            IEntityType? declaring = model.FindEntityType(node.Expression.Type);
            if (declaring?.FindNavigation(node.Member.Name) is null
                && declaring?.FindSkipNavigation(node.Member.Name) is null)
            {
                return false;
            }

            owner = declaring;
            path = node.Member.Name;

            // Extend inward through an unbroken chain: `b.Author.Books` is one path on Book, not
            // a path on Book plus an unrelated one on Author.
            if (node.Expression is MemberExpression inner
                && TryDescribe(inner, out IEntityType? innerOwner, out string? innerPath))
            {
                owner = innerOwner;
                path = $"{innerPath}.{path}";
            }

            return true;
        }
    }
}
