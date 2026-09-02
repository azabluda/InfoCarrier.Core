// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Decides which parts of a captured query the server can execute
///     (<c>docs/projection-split.md</c> §3.1, §3.5).
/// </summary>
/// <remarks>
///     <para>
///         A node is <em>server-ok</em> when the serializer can express it and every type it puts
///         on the wire clears <see cref="TypeAllowlist" /> — the same list the server enforces on
///         deserialization, so the two sides cannot disagree about where the boundary lies
///         ([ADR-010](../../../docs/decisions.md)).
///     </para>
///     <para>
///         Server-ok alone does not make a subtree shippable. It must also contain a query root
///         (otherwise there is nothing to execute) and be <em>closed</em> — referencing no
///         parameter bound outside it. An open subtree is a correlated subquery sitting under a
///         client-side projection; see <see cref="BoundaryAnalysis.OpenFragments" />.
///     </para>
/// </remarks>
/// <remarks>
///     Initializes a new instance of the <see cref="ServerBoundaryAnalyzer" /> class.
/// </remarks>
/// <param name="allowlist">The types the server will accept. Must match the server's.</param>
/// <param name="arbitrarySqlAllowed">
///     Whether this client may send a query carrying raw SQL (#60) - set from
///     <see cref="InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution" />, and
///     <c>false</c> by default. It changes exactly one verdict, in
///     <see cref="IsSerializableKind" />: whether EF's relational raw-SQL query root has a wire
///     representation. <b>It is not a security boundary</b> - the server's own
///     <see cref="IInfoCarrierArbitrarySqlExecution" /> registration is, and it refuses a
///     raw-SQL payload however this client is configured.
/// </param>
public sealed class ServerBoundaryAnalyzer(TypeAllowlist allowlist, bool arbitrarySqlAllowed = false)
{
    private readonly TypeAllowlist _allowlist = allowlist;
    private readonly bool _arbitrarySqlAllowed = arbitrarySqlAllowed;

    /// <summary>
    ///     Analyzes a captured query tree.
    /// </summary>
    public BoundaryAnalysis Analyze(Expression root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var walker = new Walker(_allowlist, _arbitrarySqlAllowed);
        walker.Visit(root);
        return new BoundaryAnalysis(root, walker.Results);
    }

    /// <summary>
    ///     Whether the serializer has a node kind for this expression at all. Kinds outside the
    ///     minimal set of research-findings §5 (block, loop, try, goto, switch, label) have no
    ///     wire representation, so a subtree containing one can never be shipped.
    /// </summary>
    internal static bool IsSerializableKind(Expression node, bool arbitrarySqlAllowed = false)
        => node switch
        {
            // The EXACT type, not the base. A query root carrying state beyond its entity type is
            // a subclass, and `QueryRootStubNode` has no field for that state — so matching the
            // base here shipped the subclass as a plain root and DROPPED what made it different.
            // `FromSqlQueryRootExpression` (relational, `Sql` + `Argument`) is the case that found
            // it: a `FromSqlRaw` with a `WHERE` came back as the whole table, silently. Refusing
            // the subclass instead sends it down `QuerySplitter.RejectClientEvaluation`, which
            // raises EF's own `TranslationFailed` — the answer every other provider gives for a
            // construct it cannot translate. Named by shape rather than by type because
            // `FromSqlQueryRootExpression` lives in `EFCore.Relational`, which this assembly does
            // not reference (M9).
            { } root when root.GetType() == typeof(Microsoft.EntityFrameworkCore.Query.EntityQueryRootExpression)
                => true,

            // The one subclass this wire does know, and only where the application asked for it
            // (#60). `FromSqlQueryRootStubNode` carries the `Sql` and the arguments that the
            // paragraph above records as having been dropped, and `RelationalQueryRootShape`
            // names the type by shape for the same reason the comment above gives. Without the
            // option this stays `false`, so the refusal below is unchanged and every existing
            // caller sees EF's own `TranslationFailed`.
            { } root when arbitrarySqlAllowed && RelationalQueryRootShape.IsFromSqlRoot(root) => true,

            Microsoft.EntityFrameworkCore.Query.QueryRootExpression => false,
            // Any other extension node — QueryParameterExpression included, though those are
            // substituted away before the split — has no translation.
            { NodeType: ExpressionType.Extension } => false,
            ConstantExpression or ParameterExpression or MemberExpression or MethodCallExpression
                or LambdaExpression or NewExpression or NewArrayExpression or BinaryExpression
                or UnaryExpression or TypeBinaryExpression or ConditionalExpression
                or MemberInitExpression or ListInitExpression or InvocationExpression => true,
            _ => false,
        };

    internal static bool IsQueryRoot(Expression node)
        => node is Microsoft.EntityFrameworkCore.Query.QueryRootExpression
            or ConstantExpression { Value: IQueryable };

    /// <summary>
    ///     Whether a node is the caller's live <see cref="DbContext" />, which nothing can ship.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A query filter or projection that calls a method <em>on the context</em> — a
    ///         function mapped by <c>HasDbFunction</c> as an instance method is the ordinary way
    ///         to get one — funcletizes to a <see cref="ConstantExpression" /> holding the client's
    ///         own context object. It is not data, it is a live object graph with a connection, a
    ///         change tracker and a service provider hanging off it, and the server has one of its
    ///         own.
    ///     </para>
    ///     <para>
    ///         <b>The type allowlist does not catch it, and cannot be asked to.</b> R84 admits the
    ///         declaring type of every <c>HasDbFunction</c> mapping so the call can be
    ///         <em>named</em> on the wire, and for an instance mapping that type is the context.
    ///         Both outcomes were then wrong, and which one you got depended on nothing that
    ///         matters — measured with a boundary probe on two contexts:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             admitted: the whole query was judged shippable and the <b>server</b> tried to
    ///             rebuild a <c>DbContext</c> from the payload —
    ///             <c>"Cannot dynamically create an instance … no parameterless constructor"</c>.
    ///         </item>
    ///         <item>
    ///             not admitted: the query was not shippable and not refused either, so the client
    ///             fetched the whole table and ran the predicate locally.
    ///         </item>
    ///     </list>
    ///     <para>
    ///         Refusing it here makes both cases the same one: not server-ok, therefore not
    ///         shipped, therefore examined by <c>QuerySplitter.RejectClientEvaluation</c>, which
    ///         raises EF's own <c>TranslationFailed</c> — what every other provider answers for a
    ///         call it cannot translate.
    ///     </para>
    /// </remarks>
    internal static bool CarriesTheClientsContext(Expression node)
        => node is ConstantExpression { Value: DbContext };

    /// <summary>
    ///     The element type of a queryable type, or the type itself when it is not one.
    /// </summary>
    internal static Type ElementTypeOf(Type type)
        => (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>)
                ? type
                : type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>)))
            ?.GetGenericArguments()[0]
            ?? type;

    /// <summary>
    ///     The element type of any sequence type — <see cref="IQueryable{T}" /> preferred, then
    ///     <see cref="IEnumerable{T}" />. A collection navigation read inside a projection is an
    ///     <c>IEnumerable</c>, not an <c>IQueryable</c>, so the queryable-only form above cannot
    ///     see nested projections at all.
    /// </summary>
    internal static Type SequenceElementType(Type type)
    {
        if (type == typeof(string))
        {
            return type;
        }

        Type queryable = ElementTypeOf(type);
        if (queryable != type)
        {
            return queryable;
        }

        Type? enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0] ?? type;
    }

    /// <summary>
    ///     Whether a subtree is something the server could <em>run</em>, as opposed to merely
    ///     serialize.
    /// </summary>
    /// <remarks>
    ///     Containing a query root is not enough. A quoted lambda such as
    ///     <c>o =&gt; ctx.Orders</c> is server-ok, closed, and contains a root — but it is a
    ///     function, not a query. Shipping one made the server hand the lambda object back as a
    ///     result row, and the client then failed resolving the internal
    ///     <c>Expression1&lt;TDelegate&gt;</c> that a compiled lambda reports as its runtime type.
    ///     Descending into it instead finds the root inside, which is what actually executes.
    /// </remarks>
    internal static bool IsExecutableQuery(Expression node)
        => !IsClientMaterialization(node)
            && (typeof(IQueryable).IsAssignableFrom(node.Type)
                // A terminal operator (Count, Any, First, …) yields a scalar, not a queryable.
                || (node is MethodCallExpression call
                    && call.Method.DeclaringType is { } declaring
                    && (declaring == typeof(Queryable)
                        || declaring == typeof(Enumerable)
                        || declaring == typeof(EntityFrameworkQueryableExtensions))));

    /// <summary>
    ///     Operators that <em>materialize</em> rather than query.
    /// </summary>
    /// <remarks>
    ///     <c>ToList</c> is not something a provider translates — it is the point at which a
    ///     query stops being a query. Shipping a subtree that ends in one asked the server to
    ///     execute it as a terminal operator, and EF answered "could not be translated" for a
    ///     query it would happily have run one call earlier. Descending past it ships the query
    ///     and leaves the materialization on the client, where it belongs.
    /// </remarks>
    internal static bool IsClientMaterialization(Expression node)
        => node is MethodCallExpression { Method: { } method }
            && (method.DeclaringType == typeof(Enumerable) || method.DeclaringType == typeof(Queryable))
            && method.Name is nameof(Enumerable.ToList)
                or nameof(Enumerable.ToArray)
                or nameof(Enumerable.ToHashSet)
                or nameof(Enumerable.ToDictionary)
                or nameof(Enumerable.ToLookup)
                or nameof(Enumerable.AsEnumerable);

    /// <summary>
    ///     Folds each node's verdict from its direct children, post-order.
    /// </summary>
    private sealed class Walker(TypeAllowlist allowlist, bool arbitrarySqlAllowed) : ExpressionVisitor
    {
        // One entry per direct child, popped when the parent folds them.
        private readonly List<NodeFacts> _pending = [];

        public Dictionary<Expression, NodeFacts> Results { get; } = new(ReferenceEqualityComparer.Instance);

        public override Expression? Visit(Expression? node)
        {
            if (node is null)
            {
                return null;
            }

            int mark = _pending.Count;
            base.Visit(node);

            bool serverOk = IsSerializableKind(node, arbitrarySqlAllowed);
            bool hasRoot = IsQueryRoot(node);
            HashSet<ParameterExpression>? free = null;

            for (int i = mark; i < _pending.Count; i++)
            {
                NodeFacts child = _pending[i];
                serverOk &= child.ServerOk;
                hasRoot |= child.HasQueryRoot;

                if (child.Free.Count > 0)
                {
                    (free ??= new HashSet<ParameterExpression>(ReferenceEqualityComparer.Instance))
                        .UnionWith(child.Free);
                }
            }

            _pending.RemoveRange(mark, _pending.Count - mark);

            if (node is ParameterExpression parameter)
            {
                (free ??= new HashSet<ParameterExpression>(ReferenceEqualityComparer.Instance)).Add(parameter);
            }
            else if (node is LambdaExpression lambda && free is not null)
            {
                // The lambda binds its own parameters; anything left is bound further out.
                free.ExceptWith(lambda.Parameters);
            }

            if (serverOk && CarriesTheClientsContext(node))
            {
                serverOk = false;
            }

            if (serverOk)
            {
                foreach (Type type in WireTypeCollector.CollectOwn(node))
                {
                    if (!allowlist.IsAllowed(type))
                    {
                        serverOk = false;
                        break;
                    }
                }
            }

            var facts = new NodeFacts(
                serverOk,
                hasRoot,
                free is null || free.Count == 0 ? NodeFacts.NoFreeParameters : free);

            _pending.Add(facts);
            Results[node] = facts;
            return node;
        }
    }
}

/// <summary>
///     What the analysis knows about one node.
/// </summary>
/// <param name="ServerOk">The whole subtree is expressible on the wire and type-allowed.</param>
/// <param name="HasQueryRoot">The subtree contains at least one query root to execute.</param>
/// <param name="Free">Parameters referenced in the subtree but bound outside it.</param>
public readonly record struct NodeFacts(
    bool ServerOk,
    bool HasQueryRoot,
    IReadOnlySet<ParameterExpression> Free)
{
    /// <summary>
    ///     Shared empty set for the overwhelmingly common closed node.
    /// </summary>
    internal static readonly IReadOnlySet<ParameterExpression> NoFreeParameters
        = new HashSet<ParameterExpression>();

    /// <summary>
    ///     Whether the subtree references nothing bound outside it, and so can be executed alone.
    /// </summary>
    public bool IsClosed => Free.Count == 0;

    /// <summary>
    ///     Whether the subtree can be sent to the server as a query in its own right.
    /// </summary>
    public bool IsShippable => ServerOk && HasQueryRoot && IsClosed;
}

/// <summary>
///     The result of <see cref="ServerBoundaryAnalyzer.Analyze" />.
/// </summary>
public sealed class BoundaryAnalysis
{
    private readonly Dictionary<Expression, NodeFacts> _facts;

    internal BoundaryAnalysis(Expression root, Dictionary<Expression, NodeFacts> facts)
    {
        Root = root;
        _facts = facts;

        var shippable = new List<Expression>();
        var open = new List<Expression>();
        Collect(root, shippable, open);
        Shippable = shippable;
        OpenFragments = open;
    }

    /// <summary>
    ///     The analyzed tree.
    /// </summary>
    public Expression Root { get; }

    /// <summary>
    ///     The maximal subtrees the server can execute on its own — what the client ships.
    ///     More than one when a query has independent sources, as a <c>Join</c> does (§3.5).
    /// </summary>
    public IReadOnlyList<Expression> Shippable { get; }

    /// <summary>
    ///     Server-executable subtrees that contain a query root but reference a parameter bound
    ///     outside them: a correlated subquery under a client-side projection. A plain cut cannot
    ///     place these — evaluating one on the client would re-query the server once per row —
    ///     so phase A reports them rather than guessing (§3.2, §6). The projection rewrite of
    ///     phase B is what resolves them.
    /// </summary>
    public IReadOnlyList<Expression> OpenFragments { get; }

    /// <summary>
    ///     Whether the whole query goes to the server untouched — the common case, and the one
    ///     that must stay on today's fast path.
    /// </summary>
    public bool IsWhollyServerExecutable
        => Shippable.Count == 1 && ReferenceEquals(Shippable[0], Root);

    /// <summary>
    ///     The verdict for a node of the analyzed tree.
    /// </summary>
    public NodeFacts FactsFor(Expression node)
        => _facts.TryGetValue(node, out NodeFacts facts)
            ? facts
            : throw new InvalidOperationException($"'{node}' is not part of the analyzed tree.");

    private void Collect(Expression node, List<Expression> shippable, List<Expression> open)
    {
        NodeFacts facts = _facts[node];

        // Ordinary local code — a constant, a comparison against a captured value. Nothing in
        // here executes anywhere, so it stays in the residual whole.
        if (!facts.HasQueryRoot)
        {
            return;
        }

        if (facts.ServerOk && ServerBoundaryAnalyzer.IsExecutableQuery(node))
        {
            // Maximal by construction: descent stops here, so the first node on any path that is
            // both server-ok and runnable is the largest one there.
            (facts.IsClosed ? shippable : open).Add(node);
            return;
        }

        foreach (Expression child in ChildrenOf(node))
        {
            Collect(child, shippable, open);
        }
    }

    private static IEnumerable<Expression> ChildrenOf(Expression node)
    {
        var children = new List<Expression>();
        new ChildCollector(children).Visit(node);
        return children;
    }

    /// <summary>
    ///     The direct children of one node — <see cref="ExpressionVisitor" /> knows the shape of
    ///     every node kind, so borrowing its traversal beats re-deriving one per kind.
    /// </summary>
    private sealed class ChildCollector(ICollection<Expression> children) : ExpressionVisitor
    {
        private bool _atRoot = true;

        public override Expression? Visit(Expression? node)
        {
            if (node is null)
            {
                return null;
            }

            if (_atRoot)
            {
                _atRoot = false;
                return base.Visit(node);
            }

            children.Add(node);
            return node;
        }
    }
}
