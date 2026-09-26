// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Splits a projection lambda into a server-side value projection and a client-side
///     reassembly (<c>docs/projection-split.md</c> §3.2).
/// </summary>
/// <remarks>
///     <para>
///         Cutting the query above a client-typed projection is not enough, and for three
///         separate reasons it is not even correct:
///     </para>
///     <list type="bullet">
///         <item>
///             <c>Select(c =&gt; new { c.City, c.Orders.Count })</c> cut at the projection ships
///             customers whose orders were never loaded, and the client answers <b>0</b>.
///         </item>
///         <item>
///             A correlated subquery in the projection body cannot be evaluated on the client at
///             all without issuing one query per row.
///         </item>
///         <item>
///             <c>GroupBy(…).Select(g =&gt; new { g.Key, g.Count() })</c> cut between the two
///             leaves a bare, non-composed <c>GroupBy</c> — which no provider can translate. The
///             cut <em>creates</em> that failure; the original query was fine.
///         </item>
///     </list>
///     <para>
///         So the projection is rewritten rather than cut. The body's maximal server-evaluable
///         subexpressions travel as a tuple, and the client rebuilds its own types from the tuple
///         slots. The server does the work it was always going to do, and only the values the
///         projection needs are on the wire — which is also wire-protocol W1.
///     </para>
/// </remarks>
internal sealed class ProjectionRewriter(ServerBoundaryAnalyzer analyzer) : ExpressionVisitor
{
    private static readonly MethodInfo QueryableSelect = typeof(Queryable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Queryable.Select)
            && m.GetGenericArguments().Length == 2
            && m.GetParameters() is [_, { ParameterType: { IsGenericType: true } second }]
            && second.GetGenericArguments()[0].GetGenericArguments().Length == 2);

    private static readonly MethodInfo EnumerableSelect = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Enumerable.Select)
            && m.GetGenericArguments().Length == 2
            && m.GetParameters() is [_, { ParameterType: { IsGenericType: true } second }]
            && second.GetGenericArguments().Length == 2);

    private static readonly MethodInfo EnumerableToList = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Enumerable.ToList) && m.GetParameters().Length == 1);

    private static readonly MethodInfo QueryableAsQueryable = typeof(Queryable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Queryable.AsQueryable) && m.IsGenericMethodDefinition);

    private static readonly MethodInfo QueryableWhere = typeof(Queryable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Queryable.Where)
            && m.GetParameters() is [_, { ParameterType: { IsGenericType: true } second }]
            && second.GetGenericArguments()[0].GetGenericArguments().Length == 2);

    private static readonly MethodInfo EnumerableWhere = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Enumerable.Where)
            && m.GetParameters() is [_, { ParameterType: { IsGenericType: true } second }]
            && second.GetGenericArguments().Length == 2);

    /// <summary>
    ///     The operators that count the rows below them and read nothing a projection computes, when
    ///     they are given no predicate. See <see cref="TryMoveBelowReassembly" />.
    /// </summary>
    private static readonly HashSet<string> RowCounting =
        [nameof(Queryable.Count), nameof(Queryable.LongCount), nameof(Queryable.Any)];

    /// <summary>
    ///     The terminal operators whose predicate overload is a <c>Where</c> under the overload that
    ///     takes the source alone. See <see cref="TryMoveBelowReassembly" />.
    /// </summary>
    private static readonly HashSet<string> PredicateTerminals =
    [
        nameof(Queryable.Count),
        nameof(Queryable.LongCount),
        nameof(Queryable.Any),
        nameof(Queryable.First),
        nameof(Queryable.FirstOrDefault),
        nameof(Queryable.Single),
        nameof(Queryable.SingleOrDefault),
        nameof(Queryable.Last),
        nameof(Queryable.LastOrDefault),
    ];

    // Read off `typeof(...)` directly, never through a `Type` parameter: the trimmer sees these, and
    // reflection over a parameter costs an IL2070 of its own (eng/trim-baseline.txt, 2026-09-26).
    private static readonly Dictionary<string, MethodInfo> QueryableWithoutPredicate = typeof(Queryable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m => IsSourceOnlyTerminal(m, typeof(IQueryable<>)))
        .ToDictionary(m => m.Name);

    private static readonly Dictionary<string, MethodInfo> EnumerableWithoutPredicate = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m => IsSourceOnlyTerminal(m, typeof(IEnumerable<>)))
        .ToDictionary(m => m.Name);

    /// <summary>
    ///     The single slot a projection gets when its body reads nothing from the row: the server
    ///     is being asked which rows exist, and for no column of them.
    /// </summary>
    /// <remarks>
    ///     <c>1</c> rather than anything of the entity's, because EF's own client writes
    ///     <c>SELECT 1</c> for the same projection, and this carrier is what the server's EF
    ///     translates. Nothing reads the slot: the reassembly rebuilds a body that never mentioned
    ///     the row.
    /// </remarks>
    private static readonly Expression RowPresence = Expression.Constant(1);

    private readonly HashSet<Expression> _reassemblies = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     The reassemblies whose tuple carries a <em>collection</em> slot.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>These are the ones an outer <c>Distinct</c> or set operation must not be allowed
    ///         to sit on.</b> EF refuses `Select(c => new { c.City, Orders = c.Orders.ToList() })`
    ///         followed by `Distinct()` on every relational provider, because the identifying
    ///         columns the collection needs do not survive the `Distinct`. This provider used to
    ///         answer it: the projection is rewritten before the boundary is drawn, so the
    ///         `Distinct` ends up above the CLIENT-side reassembly and never reaches the server at
    ///         all. The server SQL for such a query contains no `DISTINCT`, which is how this was
    ///         confirmed rather than reasoned.
    ///     </para>
    ///     <para>
    ///         Only a collection slot matters. `Select(c => new { c.City }).Distinct()` is an
    ///         ordinary query that every provider runs, and refusing it would be a regression.
    ///     </para>
    /// </remarks>
    private readonly HashSet<Expression> _collectionReassemblies = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     Every member the query reads anywhere, by declaring type and name.
    /// </summary>
    /// <remarks>
    ///     Whether a slot is read is not a question the selector building it can answer — the read
    ///     is in the <em>next</em> operator up, and this pass works innermost-first. So it is asked
    ///     of the whole tree, once, before any rewriting. See <see cref="IsQueryableCollection" />.
    /// </remarks>
    private IReadOnlySet<(Type, string)> _read = new HashSet<(Type, string)>();

    /// <summary>
    ///     The projections inside a lambda that a <c>FirstOrDefault</c> or <c>SingleOrDefault</c>
    ///     reads one row of. Their tuple is the reference-typed family, so that no row reads as
    ///     <see langword="null" />. See <see cref="TryMoveBelowReassembly" />.
    /// </summary>
    private IReadOnlySet<Expression> _singleResultSources = new HashSet<Expression>();

    /// <summary>
    ///     A client-side rebuild another pass already produced. Rewriting it would only wrap one
    ///     carrier in another.
    /// </summary>
    private Expression? _preserved;

    /// <summary>
    ///     The parameters of every lambda that encloses the node being visited, outermost first.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>A value of an inner projection may read an enclosing row, since 2026-09-22.</b>
    ///         <c>p.Blog.Posts.Select(s =&gt; new { s.Id, p.Blog.Title })</c> read only <c>s</c> into
    ///         the inner tuple, so <c>p.Blog.Title</c> stayed in the client-side rebuild, and the outer
    ///         rewrite then carried it in a slot of its own. The server never had to evaluate it inside
    ///         the collection, which on SQLite is the <c>APPLY</c> it cannot run, and this client
    ///         answered a query plain EF Core refuses with <c>ApplyNotSupported</c>.
    ///     </para>
    ///     <para>
    ///         An enclosing row is in scope wherever the inner projection runs, because the inner
    ///         call reaches the server only as part of the outer one. So the value goes into the
    ///         inner tuple, the server's EF sees the correlation EF's own client sees, and it
    ///         answers or refuses as EF does.
    ///     </para>
    /// </remarks>
    private readonly List<ParameterExpression> _enclosing = [];

    /// <summary>
    ///     The client's model, used for one question: is this call a function the model maps to
    ///     the store? See <see cref="CallsMappedFunction" />.
    /// </summary>
    private Microsoft.EntityFrameworkCore.Metadata.IModel? _model;

    /// <summary>
    ///     Rewrites every client-typed projection in <paramref name="query" /> that sits directly
    ///     on a server-executable source.
    /// </summary>
    /// <param name="query">The captured query, after parameter substitution.</param>
    /// <param name="analyzer">Decides what the server can express.</param>
    /// <param name="reassemblies">
    ///     The client-side <c>Select</c> nodes this pass introduced. They are the <em>only</em>
    ///     client-side operators a split is allowed to produce; see
    ///     <see cref="QuerySplitter" />'s client-evaluation guard.
    /// </param>
    /// <param name="collectionReassemblies">
    ///     The subset of <paramref name="reassemblies" /> whose tuple carries a collection slot.
    ///     <c>QuerySplitter</c> refuses a <c>Distinct</c> or set operation applied over one of
    ///     these, as every other relational provider does.
    /// </param>
    /// <param name="alreadyReassembled">
    ///     A reassembly an earlier pass produced, which this one must preserve rather than treat
    ///     as client code to rewrite again.
    /// </param>
    /// <param name="model">
    ///     The client's model, or <see langword="null" />. Read for one question only: whether a
    ///     call names a function the model maps to the store, which the client cannot compute.
    /// </param>
    public static Expression Rewrite(
        Expression query,
        ServerBoundaryAnalyzer analyzer,
        out IReadOnlySet<Expression> reassemblies,
        out IReadOnlySet<Expression> collectionReassemblies,
        Expression? alreadyReassembled = null,
        Microsoft.EntityFrameworkCore.Metadata.IModel? model = null)
    {
        var rewriter = new ProjectionRewriter(analyzer)
        {
            _preserved = alreadyReassembled,
            _read = MemberReadCollector.Find(query),
            _singleResultSources = SingleResultSourceFinder.Find(query),
            _model = model,
        };
        Expression result = rewriter.Visit(query);
        reassemblies = rewriter._reassemblies;
        collectionReassemblies = rewriter._collectionReassemblies;
        return result;
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        _enclosing.AddRange(node.Parameters);
        try
        {
            return base.VisitLambda(node);
        }
        finally
        {
            _enclosing.RemoveRange(_enclosing.Count - node.Parameters.Count, node.Parameters.Count);
        }
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Checked before descending, because this shape has to *replace* the in-place rewrite
        // rather than tidy up after it — see TryHoistCollectionProjection.
        if (TryHoistCollectionProjection(node) is { } hoisted)
        {
            return hoisted;
        }

        // Taken before descending, while it still names only the lambdas around this call.
        ParameterExpression[] enclosing = [.. _enclosing];

        // Innermost first: rewriting an inner projection can turn its result into something the
        // outer one can then be measured against. A plain Select visits its source first, because a
        // source that comes back as a rebuild is fused with the selector BEFORE the selector is
        // visited: see TryFuseSelectWithReassembly.
        MethodCallExpression call;
        if (IsPlainSelect(node))
        {
            Expression source = Visit(node.Arguments[0]);
            call = TryFuseSelectWithReassembly(node, source)
                ?? node.Update(node.Object, [source, Visit(node.Arguments[1])]);
        }
        else if (!ReferenceEquals(node, _preserved) && OrderingChain(node) is { } chain)
        {
            // The whole chain at once, from its top: a ThenBy needs an ordered source, and an
            // ordering moved below a reassembly comes back as the reassembly, which is not ordered.
            return VisitOrderingChain(chain);
        }
        else if (base.VisitMethodCall(node) is MethodCallExpression visited)
        {
            call = visited;
        }
        else
        {
            return node;
        }

        if (ReferenceEquals(node, _preserved))
        {
            _reassemblies.Add(call);
            return call;
        }

        if (TryMoveDistinctBelowReassembly(call) is { } moved)
        {
            return moved;
        }

        if (TryMoveBelowReassembly(call) is { } below)
        {
            return below;
        }

        if (!IsResultSelectorOperator(call, out LambdaExpression? selector))
        {
            return call;
        }

        // Everything but the selector — sources, key selectors — has to travel, or there is
        // nothing to hand the server.
        for (int i = 0; i < call.Arguments.Count - 1; i++)
        {
            Expression argument = call.Arguments[i];
            if (!analyzer.Analyze(argument).FactsFor(argument).ServerOk)
            {
                return call;
            }
        }

        BoundaryAnalysis bodyAnalysis = analyzer.Analyze(selector);
        if (bodyAnalysis.FactsFor(selector.Body).ServerOk)
        {
            // The projection is server-executable as written.
            return call;
        }

        List<Expression> fragments = [];
        var guards = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        CollectFragments(selector.Body, bodyAnalysis, [.. selector.Parameters, .. enclosing], fragments, guards);

        IReadOnlySet<Expression> consumed = Consumed(selector.Body);

        // A BODY THAT READS NOTHING FROM THE ROW STILL NEEDS ONE ROW PER ROW, AND NO COLUMN.
        //
        // This returned `call` until 2026-09-22, on the ground that there was nothing for the
        // server to compute. True, and it left the plain cut to ship the maximal `ServerOk`
        // subtree, which is the query root: `Select(b => new { F = flag })` read every column the
        // entity has, where EF's own client writes `SELECT 1`. Both answers are right, so nothing
        // in the suite could see it and only a comparison with EF's statement did
        // (`ServerParameterizationTest.A_projection_reading_no_column_matches_the_direct_query`).
        //
        // So the carrier holds one constant instead, and the reassembly below reads none of it.
        // What the server is asked for is the row count, which is what EF asks for.
        bool nullable = _singleResultSources.Contains(node);
        Expression tuple = fragments.Count > 0
            ? TupleCarrier.New([.. fragments.Select(f => Guarded(Materialized(f, consumed), f, guards))], nullable)
            : TupleCarrier.New([RowPresence], nullable);
        ParameterExpression row = Expression.Parameter(tuple.Type, "row");

        var slots = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < fragments.Count; i++)
        {
            slots[fragments[i]] = Requeryable(TupleCarrier.Read(row, i), fragments[i], consumed);
        }

        Expression clientBody = new SlotSubstitutingVisitor(slots).Visit(selector.Body)!;
        if (selector.Parameters.Any(p => ReferencesParameter(clientBody, p)))
        {
            // A row value the server could not carry — a parameter of a type it does not know.
            // Leaving it would build a lambda with an unbound parameter; the cut handles it.
            return call;
        }

        Type[] genericArguments = call.Method.GetGenericArguments();
        genericArguments[^1] = tuple.Type;

        // A nested projection over a collection navigation is an Enumerable call whose lambdas
        // are plain Funcs, not quoted expression trees. Rebuilding it in the Queryable flavour
        // makes Expression.Call reject the argument.
        bool quoted = call.Arguments[^1] is UnaryExpression { NodeType: ExpressionType.Quote };
        Expression Wrap(LambdaExpression lambda) => quoted ? Expression.Quote(lambda) : lambda;

        MethodCallExpression serverCall = Expression.Call(
            call.Method.GetGenericMethodDefinition().MakeGenericMethod(genericArguments),
            [
                .. call.Arguments.Take(call.Arguments.Count - 1),
                Wrap(Expression.Lambda(tuple, selector.Parameters)),
            ]);

        // Built with an explicit delegate type rather than inferred from the body, because the two
        // can legitimately differ: C# lets `Select(x => new { … })` typed `Func<T, object>` carry a
        // body whose own type is the anonymous one, and `LambdaExpression.ReturnType` is what the
        // *operator* was instantiated with. Inferring gave `Func<row, <>f__AnonymousType>` where
        // `Select<row, object>` wanted `Func<row, object>`, and `Expression.Call` rejected it
        // outright — `Multiple_single_result_in_projection_containing_owned_types`, both
        // parameterizations, before the query reached the wire at all.
        MethodCallExpression reassembly = Expression.Call(
            (quoted ? QueryableSelect : EnumerableSelect)
                .MakeGenericMethod(tuple.Type, selector.ReturnType),
            serverCall,
            Wrap(
                Expression.Lambda(
                    typeof(Func<,>).MakeGenericType(row.Type, selector.ReturnType), clientBody, row)));

        _reassemblies.Add(reassembly);
        if (fragments.Any(f => CarriesACollection(f, consumed)))
        {
            _collectionReassemblies.Add(reassembly);
        }

        return reassembly;
    }

    /// <summary>
    ///     Moves a <c>Distinct</c> from above a reassembly to below it, onto the tuple the server
    ///     computes, when the reassembly only constructs its result from the slots.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Since 2026-09-22.</b> <c>g.Weapons.Select(w =&gt; new { w.Name, … }).Distinct()</c>
    ///         inside a projection became a server-side tuple, a client-side rebuild, and a
    ///         <c>Distinct</c> above the rebuild, which ran on the client. The server joined the
    ///         weapons with no <c>DISTINCT</c> and this client removed the duplicates, answering a
    ///         query every relational provider refuses:
    ///         <c>InsufficientInformationToIdentifyElementOfCollectionJoin</c>, because a
    ///         <c>Distinct</c> without the element's key leaves nothing to attribute the joined rows
    ///         to their owner by. Moved, the <c>Distinct</c> reaches the server's EF, which decides.
    ///     </para>
    ///     <para>
    ///         <b>Sound only for a rebuild that constructs its result from the slots, each read
    ///         once.</b> Removing duplicates from the tuples is then what plain EF Core does: EF
    ///         removes them by the columns a construction reads and never calls the type's
    ///         <c>Equals</c>. Until 2026-09-25 this read "copies each slot into one member" and argued
    ///         from equality alone: "tuple equality and anonymous-type equality are both member by
    ///         member, so two rows are equal as tuples exactly when they are equal rebuilt". That
    ///         still holds for an anonymous type. For a type with an <c>Equals</c> of its own the
    ///         answer is EF's and not the type's, which is the answer every other provider gives. A
    ///         rebuild that computes from a slot, or drops one, can make two different tuples equal,
    ///         and is left alone.
    ///     </para>
    ///     <para>
    ///         <b>Not over a collection slot</b>: <c>QuerySplitter</c> refuses a <c>Distinct</c> over
    ///         such a reassembly with EF's own message, and moving it would bypass that.
    ///     </para>
    /// </remarks>
    private MethodCallExpression? TryMoveDistinctBelowReassembly(MethodCallExpression call)
    {
        if (call.Method.Name != nameof(Queryable.Distinct)
            || call.Arguments.Count != 1
            || (call.Method.DeclaringType != typeof(Queryable) && call.Method.DeclaringType != typeof(Enumerable))
            || call.Arguments[0] is not MethodCallExpression reassembly
            || !_reassemblies.Contains(reassembly)
            || _collectionReassemblies.Contains(reassembly)
            || StripQuotes(reassembly.Arguments[1]) is not LambdaExpression { Parameters: [var row] } rebuild
            || !CopiesEachSlotOnce(rebuild.Body, row))
        {
            return null;
        }

        MethodCallExpression distinct = Expression.Call(
            call.Method.GetGenericMethodDefinition().MakeGenericMethod(row.Type),
            reassembly.Arguments[0]);
        MethodCallExpression moved = reassembly.Update(reassembly.Object, [distinct, reassembly.Arguments[1]]);

        _reassemblies.Remove(reassembly);
        _reassemblies.Add(moved);
        return moved;
    }

    /// <summary>
    ///     Fuses a <c>Select</c> with the reassembly below it, when the reassembly only constructs
    ///     its result from the slots, so that the projection reads the server's tuple directly.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Since 2026-09-22.</b> <c>Blogs.Select(b =&gt; new { b.Title }).Distinct()</c>
    ///         followed by <c>Select(x =&gt; new { x.Title, Posts = posts.Where(p =&gt; p.Heading ==
    ///         x.Title).ToList() })</c> ran the second projection on the client, because it reads
    ///         <c>x</c>, which only the client rebuilds. Its subquery could not ship with it, so the
    ///         server read the whole <c>Posts</c> table in a statement of its own, where EF's own
    ///         client runs one <c>LEFT JOIN</c>. The same shape three levels deep is
    ///         <c>Correlated_collection_after_distinct_3_levels_without_original_identifiers</c>,
    ///         which every relational provider refuses and this client answered.
    ///     </para>
    ///     <para>
    ///         <c>Select(Select(server, rebuild), f)</c> is <c>Select(server, row =&gt;
    ///         f(rebuild(row)))</c> by the definition of <c>Select</c>, so the answer cannot change.
    ///         EF's <see cref="Microsoft.EntityFrameworkCore.Query.ReplacingExpressionVisitor" /> does
    ///         the substitution and folds <c>new { Title = row.Item1 }.Title</c> back to
    ///         <c>row.Item1</c>, which is what leaves the fused projection nothing client-typed to
    ///         read. It is then an ordinary projection over a server source, and the rewrite that
    ///         follows splits it as it splits any other.
    ///     </para>
    ///     <para>
    ///         <b>The selector is visited AFTER the fusion, and that is load-bearing.</b> Visited
    ///         first, a subquery inside it that reads <c>x.Title</c> has a source the server cannot
    ///         name, so its own projection is never rewritten; after the fusion that source reads
    ///         <c>row.Item1</c>, ships, and the server returned every column of every post where EF
    ///         reads one. Measured with <c>ServerParameterizationTest</c>.
    ///     </para>
    ///     <para>
    ///         Only over a rebuild that constructs its result from the slots, each read once, and
    ///         never over a collection slot, for the reasons
    ///         <see cref="TryMoveDistinctBelowReassembly" /> gives.
    ///     </para>
    /// </remarks>
    /// <param name="node">The <c>Select</c>, not yet visited.</param>
    /// <param name="source">Its source, already visited.</param>
    private MethodCallExpression? TryFuseSelectWithReassembly(MethodCallExpression node, Expression source)
    {
        if (source is not MethodCallExpression reassembly
            || !_reassemblies.Contains(reassembly)
            || _collectionReassemblies.Contains(reassembly)
            || StripQuotes(reassembly.Arguments[1]) is not LambdaExpression { Parameters: [var row] } rebuild
            || !CopiesEachSlotOnce(rebuild.Body, row)
            || StripQuotes(node.Arguments[1]) is not LambdaExpression { Parameters: [var element] } selector)
        {
            return null;
        }

        Expression body = Microsoft.EntityFrameworkCore.Query.ReplacingExpressionVisitor.Replace(
            element, rebuild.Body, selector.Body);
        LambdaExpression fused = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(row.Type, selector.ReturnType), body, row);

        _reassemblies.Remove(reassembly);
        return Expression.Call(
            node.Method.GetGenericMethodDefinition().MakeGenericMethod(row.Type, selector.ReturnType),
            reassembly.Arguments[0],
            Visit(node.Arguments[1] is UnaryExpression { NodeType: ExpressionType.Quote } ? Expression.Quote(fused) : fused));
    }

    /// <summary>
    ///     Moves an operator written above a reassembly below it, onto the tuple the server
    ///     computes, where it reads nothing the rebuild alone has.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Since 2026-09-26, found by #167's slow run.</b> An operator above a reassembly
    ///         stayed on the client, so the server sent every row the operator was about to drop or
    ///         count. <c>Select(x =&gt; new Dto { Id = x.OrderID }).Where(d =&gt; ((IHaveId)d).Id ==
    ///         10252)</c> read all 831 orders where plain EF Core reads one, and
    ///         <c>Select(o =&gt; new { Id = CodeFormat(o.OrderID) }).Count()</c> read every order to
    ///         count them. The carrier rewrite covers a filter or an ordering that reads the client
    ///         type directly; this covers the rest.
    ///     </para>
    ///     <para>
    ///         <b>Every reassembly is <c>Select(server, rebuild)</c>, which is row for row and keeps
    ///         the order</b>, and each move below follows from that:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <c>Count</c>, <c>LongCount</c> and <c>Any</c> without a predicate count the rows
    ///             and read none, so the rebuild is dropped. EF drops the projection under them too,
    ///             so client code in it is never called, here or there.
    ///         </item>
    ///         <item>
    ///             <c>Skip</c> and <c>Take</c> select the same rows below the rebuild as above it.
    ///         </item>
    ///         <item>
    ///             <c>Where</c> and an ordering read the rebuilt element through their lambda, which
    ///             is fused with the rebuild, as <see cref="TryFuseSelectWithReassembly" /> fuses a
    ///             projection. EF's <see cref="Microsoft.EntityFrameworkCore.Query.ReplacingExpressionVisitor" />
    ///             folds <c>new Dto { Id = row.Item1 }.Id</c>, through a cast to an interface too, to
    ///             <c>row.Item1</c>. The move happens only when the fused lambda is one the server can
    ///             run; a lambda that still needs the rebuild, or client code, stays where it was,
    ///             and <c>QuerySplitter</c> judges it there as before.
    ///         </item>
    ///         <item>
    ///             A predicate given to a terminal operator is a <c>Where</c> under the operator
    ///             without one, which is how EF normalizes it before translating.
    ///         </item>
    ///     </list>
    /// </remarks>
    private Expression? TryMoveBelowReassembly(MethodCallExpression call)
    {
        if (call.Method.DeclaringType is not { } declaring
            || (declaring != typeof(Queryable) && declaring != typeof(Enumerable))
            || !call.Method.IsGenericMethod
            || call.Arguments.Count is 0 or > 2
            || call.Arguments[0] is not MethodCallExpression reassembly
            || !_reassemblies.Contains(reassembly)
            || StripQuotes(reassembly.Arguments[1]) is not LambdaExpression { Parameters: [var row] } rebuild)
        {
            return null;
        }

        string name = call.Method.Name;
        Expression server = reassembly.Arguments[0];
        Type element = call.Method.GetGenericArguments()[0];

        if (call.Arguments.Count == 1)
        {
            if (name is nameof(Queryable.FirstOrDefault) or nameof(Queryable.SingleOrDefault)
                && _enclosing.Count > 0
                && !row.Type.IsValueType)
            {
                return OneRowRebuilt(call, reassembly, rebuild, element);
            }

            if (!RowCounting.Contains(name))
            {
                return null;
            }

            Forget(reassembly);
            return Expression.Call(call.Method.GetGenericMethodDefinition().MakeGenericMethod(row.Type), server);
        }

        Expression argument = call.Arguments[1];
        if (name is nameof(Queryable.Skip) or nameof(Queryable.Take) && argument.Type == typeof(int))
        {
            return Rebuilt(
                reassembly,
                Expression.Call(call.Method.GetGenericMethodDefinition().MakeGenericMethod(row.Type), server, argument),
                element);
        }

        if (StripQuotes(argument) is not LambdaExpression { Parameters: [_], ReturnType: var returned } lambda
            || !(returned == typeof(bool) && (name == nameof(Queryable.Where) || PredicateTerminals.Contains(name)))
            || Fuse(lambda, rebuild) is not { } fused)
        {
            return null;
        }

        bool quoted = argument is UnaryExpression { NodeType: ExpressionType.Quote };
        MethodCallExpression filtered = Expression.Call(
            (declaring == typeof(Queryable) ? QueryableWhere : EnumerableWhere).MakeGenericMethod(row.Type),
            server,
            quoted ? Expression.Quote(fused) : fused);

        if (name == nameof(Queryable.Where))
        {
            return Rebuilt(reassembly, filtered, element);
        }

        if (RowCounting.Contains(name))
        {
            Forget(reassembly);
            return Expression.Call(WithoutPredicate(declaring, name).MakeGenericMethod(row.Type), filtered);
        }

        return Expression.Call(
            WithoutPredicate(declaring, name).MakeGenericMethod(element),
            Rebuilt(reassembly, filtered, element));
    }

    /// <summary>
    ///     Runs a <c>FirstOrDefault</c> or <c>SingleOrDefault</c> inside a projection on the server's
    ///     tuple, and rebuilds the one row it returns on the client, or <see langword="null" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Since 2026-09-26, found by #167's slow run.</b>
    ///         <c>b.Posts.Select(p =&gt; new { p.Heading }).FirstOrDefault()</c> inside a projection
    ///         kept the operator above the rebuild, on the client, so the whole collection travelled
    ///         in a slot of the outer tuple and this client kept its first element. EF's own client
    ///         writes a <c>ROW_NUMBER()</c> window and reads one row per owner:
    ///         <c>Lift_projection_mapping_when_pushing_down_subquery</c> read 134 rows where plain
    ///         EF reads 24.
    ///     </para>
    ///     <para>
    ///         <b>The tuple is the reference-typed family</b>, chosen for this projection before it
    ///         was rewritten (<see cref="SingleResultSourceFinder" />), because a value tuple has no
    ///         value that says "no row". The operator runs on the server's tuple, the slot holds the
    ///         row or <see langword="null" />, and the client rebuilds the row only when there is
    ///         one. The rebuild goes into the outer body as an invocation over the one value, so the
    ///         outer rewrite lifts that value into a slot of its own, whole, and never evaluates it
    ///         twice.
    ///     </para>
    ///     <para>
    ///         <b>Only inside a lambda.</b> At the root of the query the operator already bounds the
    ///         rows the server sends (<c>QuerySplitter.WithRowLimitForTerminalOperator</c>).
    ///     </para>
    /// </remarks>
    private InvocationExpression OneRowRebuilt(
        MethodCallExpression call, MethodCallExpression reassembly, LambdaExpression rebuild, Type element)
    {
        ParameterExpression row = rebuild.Parameters[0];
        Forget(reassembly);

        MethodCallExpression single = Expression.Call(
            call.Method.GetGenericMethodDefinition().MakeGenericMethod(row.Type), reassembly.Arguments[0]);
        // `Expression.Default` is safe here, unlike in `Guarded`: this conditional stays on the
        // client and is never serialized.
        Expression body = Expression.Condition(
            Expression.Equal(row, Expression.Constant(null, row.Type)),
            Expression.Default(element),
            rebuild.Body.Type == element ? rebuild.Body : Expression.Convert(rebuild.Body, element));

        return Expression.Invoke(Expression.Lambda(body, row), single);
    }

    /// <summary>
    ///     The ordering operators from <paramref name="node" /> down to the <c>OrderBy</c> that
    ///     starts them, topmost first, or <see langword="null" /> when <paramref name="node" /> is not
    ///     such a chain of <see cref="Queryable" /> or <see cref="Enumerable" /> operators.
    /// </summary>
    private static List<MethodCallExpression>? OrderingChain(MethodCallExpression node)
    {
        var chain = new List<MethodCallExpression>();
        for (Expression current = node; ;)
        {
            if (current is not MethodCallExpression { Arguments.Count: 2 } call
                || (call.Method.DeclaringType != typeof(Queryable) && call.Method.DeclaringType != typeof(Enumerable))
                || StripQuotes(call.Arguments[1]) is not LambdaExpression { Parameters.Count: 1 })
            {
                return null;
            }

            chain.Add(call);
            switch (call.Method.Name)
            {
                case nameof(Queryable.OrderBy) or nameof(Queryable.OrderByDescending):
                    return chain;

                case nameof(Queryable.ThenBy) or nameof(Queryable.ThenByDescending):
                    current = call.Arguments[0];
                    break;

                default:
                    return null;
            }
        }
    }

    /// <summary>
    ///     Visits an ordering chain, and moves it below the reassembly it orders when every key
    ///     reads nothing the rebuild alone has. See <see cref="TryMoveBelowReassembly" />.
    /// </summary>
    /// <param name="chain">The chain, topmost first, as <see cref="OrderingChain" /> returns it.</param>
    private Expression VisitOrderingChain(List<MethodCallExpression> chain)
    {
        Expression source = Visit(chain[^1].Arguments[0]);

        if (source is MethodCallExpression reassembly
            && _reassemblies.Contains(reassembly)
            && StripQuotes(reassembly.Arguments[1]) is LambdaExpression { Parameters: [var row] } rebuild)
        {
            Expression? ordered = reassembly.Arguments[0];
            for (int i = chain.Count - 1; i >= 0 && ordered is not null; i--)
            {
                MethodCallExpression call = chain[i];
                if (Fuse((LambdaExpression)StripQuotes(call.Arguments[1]), rebuild) is { } fused)
                {
                    bool quoted = call.Arguments[1] is UnaryExpression { NodeType: ExpressionType.Quote };
                    ordered = Expression.Call(
                        call.Method.GetGenericMethodDefinition().MakeGenericMethod(row.Type, fused.ReturnType),
                        ordered,
                        quoted ? Expression.Quote(fused) : fused);
                }
                else
                {
                    ordered = null;
                }
            }

            if (ordered is not null)
            {
                return Rebuilt(reassembly, ordered, chain[0].Method.GetGenericArguments()[0]);
            }
        }

        Expression result = source;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            result = chain[i].Update(chain[i].Object, [result, Visit(chain[i].Arguments[1])]);
        }

        return result;
    }

    /// <summary>
    ///     <paramref name="lambda" /> over the tuple rather than over the rebuilt element, or
    ///     <see langword="null" /> when the server could not run it.
    /// </summary>
    /// <remarks>
    ///     <b>Not when it reads a slot that holds a sequence.</b> A slot can hold the grouping of a
    ///     <c>GroupJoin</c>, and a lambda that navigates out of a projected tuple back into such a
    ///     collection is what no provider translates: moving operators below a rebuild was measured
    ///     at 91 to 383 failures for that reason in 2026-08 (<c>docs/projection-split.md</c> §6a).
    ///     Such an operator stays on the client, where it answered before.
    /// </remarks>
    private LambdaExpression? Fuse(LambdaExpression lambda, LambdaExpression rebuild)
    {
        ParameterExpression row = rebuild.Parameters[0];
        Expression body = Microsoft.EntityFrameworkCore.Query.ReplacingExpressionVisitor.Replace(
            lambda.Parameters[0], rebuild.Body, lambda.Body);
        LambdaExpression fused = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(row.Type, lambda.ReturnType), body, row);

        return analyzer.Analyze(fused).FactsFor(fused.Body).ServerOk && !SequenceSlotFinder.Reads(body, row)
            ? fused
            : null;
    }

    /// <summary>
    ///     Finds the projections inside a lambda that a <c>FirstOrDefault</c> or
    ///     <c>SingleOrDefault</c> without a predicate reads one row of. See
    ///     <see cref="OneRowRebuilt" />.
    /// </summary>
    private sealed class SingleResultSourceFinder : ExpressionVisitor
    {
        private readonly HashSet<Expression> _found = new(ReferenceEqualityComparer.Instance);
        private int _depth;

        public static IReadOnlySet<Expression> Find(Expression query)
        {
            var finder = new SingleResultSourceFinder();
            finder.Visit(query);
            return finder._found;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _depth++;
            try
            {
                return base.VisitLambda(node);
            }
            finally
            {
                _depth--;
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (_depth > 0
                && node.Method.Name is nameof(Queryable.FirstOrDefault) or nameof(Queryable.SingleOrDefault)
                && (node.Method.DeclaringType == typeof(Queryable) || node.Method.DeclaringType == typeof(Enumerable))
                && node.Arguments is [MethodCallExpression source]
                && IsPlainSelect(source))
            {
                _found.Add(source);
            }

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>Finds a read of a tuple slot that holds a sequence. See <see cref="Fuse" />.</summary>
    private sealed class SequenceSlotFinder(ParameterExpression row) : ExpressionVisitor
    {
        private bool _found;

        public static bool Reads(Expression body, ParameterExpression row)
        {
            var finder = new SequenceSlotFinder(row);
            finder.Visit(body);
            return finder._found;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // A slot itself, not a navigation out of an entity a slot holds, which EF follows.
            if (node.Type != typeof(string)
                && typeof(System.Collections.IEnumerable).IsAssignableFrom(node.Type)
                && node.Expression is { } holder
                && TupleCarrier.IsCarrier(holder.Type)
                && RootOf(node) == row)
            {
                _found = true;
            }

            return base.VisitMember(node);
        }

        private static Expression? RootOf(MemberExpression node)
        {
            Expression? current = node;
            while (current is MemberExpression member)
            {
                current = member.Expression;
            }

            return current;
        }
    }

    /// <summary>
    ///     The reassembly again, over <paramref name="server" /> instead of its own source, and
    ///     recorded in its place.
    /// </summary>
    /// <param name="reassembly">The reassembly the operator was above.</param>
    /// <param name="server">Its source with the operator applied.</param>
    /// <param name="element">
    ///     The element type the operator had, which is the rebuild's type or one it implements,
    ///     as a <c>Where&lt;IHaveId&gt;</c> over a DTO has. The rebuild converts to it, so every
    ///     operator above sees the type it was written against.
    /// </param>
    private MethodCallExpression Rebuilt(MethodCallExpression reassembly, Expression server, Type element)
    {
        var rebuild = (LambdaExpression)StripQuotes(reassembly.Arguments[1]);
        if (rebuild.ReturnType != element)
        {
            rebuild = Expression.Lambda(
                typeof(Func<,>).MakeGenericType(rebuild.Parameters[0].Type, element),
                Expression.Convert(rebuild.Body, element),
                rebuild.Parameters);
        }

        bool quoted = reassembly.Arguments[1] is UnaryExpression { NodeType: ExpressionType.Quote };
        MethodCallExpression moved = Expression.Call(
            reassembly.Method.GetGenericMethodDefinition().MakeGenericMethod(rebuild.Parameters[0].Type, element),
            server,
            quoted ? Expression.Quote(rebuild) : rebuild);

        _reassemblies.Remove(reassembly);
        _reassemblies.Add(moved);
        if (_collectionReassemblies.Remove(reassembly))
        {
            _collectionReassemblies.Add(moved);
        }

        return moved;
    }

    /// <summary>Removes a reassembly an operator has dropped.</summary>
    private void Forget(MethodCallExpression reassembly)
    {
        _reassemblies.Remove(reassembly);
        _collectionReassemblies.Remove(reassembly);
    }

    /// <summary>
    ///     The overload of the terminal operator <paramref name="name" /> of
    ///     <paramref name="declaring" /> that takes the source alone.
    /// </summary>
    private static MethodInfo WithoutPredicate(Type declaring, string name)
        => (declaring == typeof(Queryable) ? QueryableWithoutPredicate : EnumerableWithoutPredicate)[name];

    private static bool IsSourceOnlyTerminal(MethodInfo method, Type sequence)
        => PredicateTerminals.Contains(method.Name)
            && method.IsGenericMethodDefinition
            && method.GetGenericArguments().Length == 1
            && method.GetParameters() is [{ ParameterType: { IsGenericType: true } source }]
            && source.GetGenericTypeDefinition() == sequence;

    /// <summary>
    ///     Whether <paramref name="node" /> is <c>Select(source, x =&gt; …)</c> of
    ///     <see cref="Queryable" /> or <see cref="Enumerable" />, without the index overload.
    /// </summary>
    private static bool IsPlainSelect(MethodCallExpression node)
        => node.Method.Name == nameof(Queryable.Select)
            && node.Arguments.Count == 2
            && (node.Method.DeclaringType == typeof(Queryable) || node.Method.DeclaringType == typeof(Enumerable))
            && StripQuotes(node.Arguments[1]) is LambdaExpression { Parameters.Count: 1 };

    /// <summary>
    ///     Whether <paramref name="body" /> constructs its result from the slots of
    ///     <paramref name="row" />, each read once, in order, and nothing else.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>A construction is any <c>new</c>, with an initializer or without, and one nested in
    ///         another, since 2026-09-25.</b> This accepted an anonymous type alone until then, so a
    ///         <c>Distinct</c> over <c>new OrderCountDTO(o.CustomerID)</c> stayed on the client and
    ///         the server sent every duplicate row, where plain EF Core runs
    ///         <c>SELECT DISTINCT "CustomerID"</c>. EF removes duplicates by the columns a
    ///         construction reads and never calls the type's <c>Equals</c>, so for any construction
    ///         the tuple is exactly what EF compares.
    ///     </para>
    ///     <para>
    ///         A construction that reads no slot is built over <see cref="RowPresence" />, the only
    ///         tuple this rewrite makes for a body with no fragment. Its one slot is the same constant
    ///         in every row, and EF writes <c>SELECT DISTINCT 1</c> for it.
    ///     </para>
    /// </remarks>
    private static bool CopiesEachSlotOnce(Expression body, ParameterExpression row)
    {
        List<Expression> values = [];
        if (!CollectConstructedValues(body, values))
        {
            return false;
        }

        if (values.Count == 0)
        {
            return true;
        }

        if (TupleCarrier.MakeType([.. values.Select(v => v.Type)]) != row.Type)
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (!Microsoft.EntityFrameworkCore.Query.ExpressionEqualityComparer.Instance.Equals(
                    values[i], TupleCarrier.Read(row, i)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Adds what <paramref name="node" /> is constructed from to <paramref name="values" />, in
    ///     order, reading through every construction nested in it; <see langword="false" /> when
    ///     <paramref name="node" /> is not a construction.
    /// </summary>
    private static bool CollectConstructedValues(Expression node, List<Expression> values)
    {
        IEnumerable<Expression> parts;
        switch (node)
        {
            case NewExpression construction:
                parts = construction.Arguments;
                break;

            case MemberInitExpression initialization
                when initialization.Bindings.All(b => b.BindingType == MemberBindingType.Assignment):
                parts = [
                    .. initialization.NewExpression.Arguments,
                    .. initialization.Bindings.Cast<MemberAssignment>().Select(a => a.Expression),
                ];
                break;

            default:
                return false;
        }

        foreach (Expression part in parts)
        {
            if (!CollectConstructedValues(part, values))
            {
                values.Add(part);
            }
        }

        return true;
    }

    /// <summary>
    ///     Whether a tuple slot built from this fragment holds a collection rather than a value.
    /// </summary>
    /// <remarks>
    ///     Broader than <see cref="IsQueryableCollection" />, which asks only about the exact
    ///     <see cref="IQueryable{T}" /> shape that cannot travel. Here the question is what the
    ///     slot <em>holds</em>, and a navigation already materialized into a list holds a
    ///     collection just as much as a queryable does. <see cref="string" /> is a sequence of
    ///     characters and is not one of these.
    /// </remarks>
    /// <remarks>
    ///     <b>`IsAssignableFrom` rather than a walk over `GetInterfaces()`, and the trim ratchet
    ///     is what decided it.</b> The first version asked each interface whether it was
    ///     `IEnumerable&lt;&gt;`, which is reflection over a type the model named and costs an
    ///     IL2070: `ours` went 90 to 91 for a question that needs no reflection at all. The
    ///     non-generic interface answers it, and `string` is the one sequence that is not a
    ///     collection.
    /// </remarks>
    private static bool CarriesACollection(Expression fragment, IReadOnlySet<Expression> consumed)
        => IsQueryableCollection(fragment, consumed)
            || (fragment.Type != typeof(string)
                && typeof(System.Collections.IEnumerable).IsAssignableFrom(fragment.Type));

    /// <summary>
    ///     Whether a call's last argument is the selector that <em>produces its element type</em> —
    ///     <c>Select</c>, <c>SelectMany</c>, <c>Join</c>, <c>GroupJoin</c>, <c>GroupBy</c> with a
    ///     result selector, <c>Zip</c>.
    /// </summary>
    /// <remarks>
    ///     Recognised structurally rather than by name, so no list has to be kept in step with
    ///     <see cref="Queryable" />: the operator returns <c>IQueryable&lt;TResult&gt;</c> where
    ///     <c>TResult</c> is its last generic argument and the last argument's return type.
    ///     <para>
    ///         The structural test is what keeps <c>OrderBy</c> and <c>Where</c> out.
    ///         <c>OrderBy&lt;TSource, TKey&gt;</c> also ends in a lambda returning its last
    ///         generic argument, but it returns a sequence of <c>TSource</c> — rewriting it would
    ///         silently replace the elements with their sort keys.
    ///     </para>
    /// </remarks>
    internal static bool IsResultSelectorOperator(
        MethodCallExpression call, [NotNullWhen(true)] out LambdaExpression? selector)
    {
        selector = null;

        if (call.Method.DeclaringType is not { } declaring
            || (declaring != typeof(Queryable) && declaring != typeof(Enumerable))
            || !call.Method.IsGenericMethod
            || call.Arguments.Count < 2)
        {
            return false;
        }

        Type resultType = call.Method.GetGenericArguments()[^1];
        if (ServerBoundaryAnalyzer.SequenceElementType(call.Method.ReturnType) != resultType
            || StripQuotes(call.Arguments[^1]) is not LambdaExpression lambda
            || lambda.ReturnType != resultType)
        {
            return false;
        }

        selector = lambda;
        return true;
    }

    /// <summary>
    ///     Rewrites <c>SelectMany(source, c =&gt; inner.Select(o =&gt; clientType))</c> so the
    ///     reassembly sits <em>above</em> the <c>SelectMany</c> instead of inside its collection
    ///     selector.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Rewritten in place, the reassembly is a client-typed <c>Select</c> nested inside the
    ///         collection selector — which makes the whole <c>SelectMany</c> client-side, so the
    ///         source ships alone and the residual reads navigations off rows the server never
    ///         sent. That is the refusal
    ///         <c>SelectMany_with_client_eval_with_collection_shaper</c> and <c>…_ignored</c> hit.
    ///     </para>
    ///     <para>
    ///         Hoisting works only if the client half needs nothing but the row, so the fragments
    ///         are collected against the <em>outer</em> parameter as well as the inner one:
    ///         <c>c.ContactName</c> has to travel in a slot, because after the hoist <c>c</c> is no
    ///         longer in scope. That is the one thing the in-place rewrite cannot do, since there
    ///         the outer parameter is still available and there was never a reason to carry it.
    ///     </para>
    ///     <para>
    ///         Deliberately narrow. It matches one shape — the two-argument <c>SelectMany</c>,
    ///         which <see cref="IsResultSelectorOperator" /> does not even consider, because its
    ///         lambda returns <c>IEnumerable&lt;TResult&gt;</c> rather than <c>TResult</c>.
    ///     </para>
    /// </remarks>
    private Expression? TryHoistCollectionProjection(MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(Queryable)
            || node.Method.Name != nameof(Queryable.SelectMany)
            || node.Arguments.Count != 2
            || !node.Method.IsGenericMethod
            || StripQuotes(node.Arguments[1]) is not LambdaExpression { Parameters: [var outer] } collectionSelector
            || collectionSelector.Body is not MethodCallExpression inner
            || !IsResultSelectorOperator(inner, out LambdaExpression? innerSelector))
        {
            return null;
        }

        Expression source = Visit(node.Arguments[0]);
        if (!analyzer.Analyze(source).FactsFor(source).ServerOk)
        {
            return null;
        }

        for (int i = 0; i < inner.Arguments.Count - 1; i++)
        {
            Expression argument = inner.Arguments[i];
            if (!analyzer.Analyze(argument).FactsFor(argument).ServerOk)
            {
                return null;
            }
        }

        BoundaryAnalysis bodyAnalysis = analyzer.Analyze(innerSelector);
        if (bodyAnalysis.FactsFor(innerSelector.Body).ServerOk)
        {
            // Nothing client-typed here; the ordinary path ships it whole.
            return null;
        }

        ParameterExpression[] rowParameters = [.. innerSelector.Parameters, outer];

        List<Expression> fragments = [];
        var guards = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        CollectFragments(innerSelector.Body, bodyAnalysis, rowParameters, fragments, guards);

        // DEFENSIVE, AND MEASURED UNREACHED: a whole-suite run on 2026-09-23 reached this line 7
        // times, with 2 fragments twice and 3 five times, and never with 0. The reason is that the
        // two conditions fight each other. A body with NO row reference is closed, so EF's
        // funcletizer lifts it into one parameter and `ServerOk` is true -- the exit above takes
        // it, which is what `A_hoisted_projection_reading_no_column_fails_where_EF_fails` walks
        // into. A body that DOES read the row bottoms out at a row parameter, which is itself a
        // fragment. It stays because an empty tuple below would be a crash rather than a fallback,
        // and because "unreached today" is not "unreachable".
        //
        // So it is NOT the `SELECT 1` case one level down (#155). That one was a real defect:
        // there the plain cut shipped every column. Here there is no cut to make and no answer to
        // lose, because the query never reaches a store on either side.
        if (fragments.Count == 0)
        {
            return null;
        }

        IReadOnlySet<Expression> consumed = Consumed(innerSelector.Body);

        Expression tuple = TupleCarrier.New([.. fragments.Select(f => Guarded(Materialized(f, consumed), f, guards))]);
        ParameterExpression row = Expression.Parameter(tuple.Type, "row");

        var slots = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < fragments.Count; i++)
        {
            slots[fragments[i]] = Requeryable(TupleCarrier.Read(row, i), fragments[i], consumed);
        }

        Expression clientBody = new SlotSubstitutingVisitor(slots).Visit(innerSelector.Body)!;
        if (rowParameters.Any(p => ReferencesParameter(clientBody, p)))
        {
            // Something the server could not carry. Hoisting would strand it, and the in-place
            // rewrite at least still answers.
            return null;
        }

        Type[] innerGenerics = inner.Method.GetGenericArguments();
        innerGenerics[^1] = tuple.Type;

        bool quoted = inner.Arguments[^1] is UnaryExpression { NodeType: ExpressionType.Quote };
        Expression Wrap(LambdaExpression lambda) => quoted ? Expression.Quote(lambda) : lambda;

        MethodCallExpression serverInner = Expression.Call(
            inner.Method.GetGenericMethodDefinition().MakeGenericMethod(innerGenerics),
            [.. inner.Arguments.Take(inner.Arguments.Count - 1), Wrap(Expression.Lambda(tuple, innerSelector.Parameters))]);

        Type[] outerGenerics = node.Method.GetGenericArguments();
        outerGenerics[^1] = tuple.Type;

        MethodCallExpression serverCall = Expression.Call(
            node.Method.GetGenericMethodDefinition().MakeGenericMethod(outerGenerics),
            source,
            Expression.Quote(Expression.Lambda(serverInner, outer)));

        MethodCallExpression reassembly = Expression.Call(
            QueryableSelect.MakeGenericMethod(tuple.Type, innerSelector.ReturnType),
            serverCall,
            Expression.Quote(Expression.Lambda(clientBody, row)));

        _reassemblies.Add(reassembly);
        if (fragments.Any(f => CarriesACollection(f, consumed)))
        {
            _collectionReassemblies.Add(reassembly);
        }

        return reassembly;
    }

    /// <summary>
    ///     Everything in <paramref name="body" /> that something else reads: a value handed to a
    ///     query operator, and a slot of a constructed row whose member the query reads elsewhere.
    /// </summary>
    private IReadOnlySet<Expression> Consumed(Expression body)
    {
        var consumed = new HashSet<Expression>(
            OperatorSourceCollector.Find(body), ReferenceEqualityComparer.Instance);

        AddReadSlots(body, consumed);

        return consumed;
    }

    private void AddReadSlots(Expression body, HashSet<Expression> consumed)
    {
        switch (body)
        {
            case NewExpression { Members: { } members } constructed:
                for (int i = 0; i < members.Count && i < constructed.Arguments.Count; i++)
                {
                    if (IsRead(members[i]))
                    {
                        consumed.Add(constructed.Arguments[i]);
                    }
                }

                break;

            case MemberInitExpression init:
                AddReadSlots(init.NewExpression, consumed);
                foreach (MemberBinding binding in init.Bindings)
                {
                    if (binding is MemberAssignment assignment && IsRead(assignment.Member))
                    {
                        consumed.Add(assignment.Expression);
                    }
                }

                break;
        }
    }

    private bool IsRead(MemberInfo member)
        => member.DeclaringType is { } declaring && _read.Contains((declaring, member.Name));

    /// <summary>
    ///     Whether a fragment is a queryable collection, which cannot travel as one.
    /// </summary>
    /// <remarks>
    ///     Deliberately the exact <see cref="IQueryable{T}" /> and nothing derived from it. An
    ///     <c>IOrderedQueryable&lt;T&gt;</c> fragment may have a <c>ThenBy</c> above it, and handing
    ///     that an <see cref="IQueryable{T}" /> back would fail to rebuild the enclosing call.
    /// </remarks>
    private static bool IsQueryableCollection(Expression fragment, IReadOnlySet<Expression> consumed)
        => fragment.Type.IsGenericType
            && fragment.Type.GetGenericTypeDefinition() == typeof(IQueryable<>)
            // Only when something *reads* it. A queryable handed straight to the result is what the
            // caller asked for, and EF is right to refuse it — three spec tests assert exactly that
            // error (`AssertInvalidMaterializationType`) and materializing here suppresses it.
            //
            // Two kinds of read count, and they are found in different places. An operator applied
            // in the projection body itself — `frag.Select(…)` — is `consumed`. A slot of a row the
            // body constructs is read by the *next* operator up, which this innermost-first pass has
            // not reached yet; `_read` is collected from the whole tree for that case, and it is what
            // separates `select new { l2, innerL1s }` followed by `ti => ti.innerL1s.ToList()` — a
            // `let`, an intermediate — from `select new { Subquery = q }`, where nothing ever looks
            // at `Subquery` and EF's refusal is the answer.
            && consumed.Contains(fragment);

    /// <summary>
    ///     Materializes a collection-valued fragment on its way into a tuple slot.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         EF refuses a final projection that returns an <see cref="IQueryable{T}" /> —
    ///         <c>CoreStrings</c>: "Collections in the final projection must be an
    ///         <c>IEnumerable&lt;T&gt;</c> type such as <c>List&lt;T&gt;</c>". The largest
    ///         server-evaluable fragment of
    ///         <c>Select(c =&gt; c.Orders.…Take(1).Select(…).ToList())</c> is the <c>Take(1)</c>
    ///         subquery, and putting it in a slot verbatim shipped exactly the projection EF
    ///         rejects — before running anything, so the failure named the projection and not this
    ///         rewrite.
    ///     </para>
    ///     <para>
    ///         This is the boundary on the rule phase E1 established. Descending past a
    ///         <c>ToList</c> is right at the <em>end of a query</em>, where it asks the server to
    ///         translate a materialization; it is wrong <em>inside a projection</em>, where EF
    ///         requires one. Same operator, opposite meaning, decided by position.
    ///     </para>
    /// </remarks>
    private static Expression Materialized(Expression fragment, IReadOnlySet<Expression> consumed)
        => IsQueryableCollection(fragment, consumed)
            ? Expression.Call(
                EnumerableToList.MakeGenericMethod(fragment.Type.GetGenericArguments()[0]),
                fragment)
            : UnbuildableNavigationElement(fragment) is { } element
                ? Expression.Call(EnumerableToList.MakeGenericMethod(element), fragment)
                : fragment;

    /// <summary>
    ///     The element type of a <em>bare navigation read</em> whose declared collection type the
    ///     store's shaper cannot build, or <see langword="null" /> when there is no such problem.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A slot carries a fragment with the type the user's model gave it, and a collection
    ///         navigation may be declared as something the shaper cannot fill.
    ///         <c>IReadOnlyList&lt;Name&gt;</c> is the spec's own — <c>OwnsMany_correlated_projection</c>
    ///         maps it with a <c>protected</c> setter — and InMemory's
    ///         <c>MaterializeCollection&lt;TElement, TCollection&gt;</c> constrains
    ///         <c>TCollection : class, ICollection&lt;TElement&gt;</c>, so the generic method
    ///         cannot even be closed over it: <c>VerificationException</c>, before a row is read.
    ///     </para>
    ///     <para>
    ///         EF never meets this in the user's own query because a projection returning a
    ///         collection ends in <c>ToList</c> or <c>ToArray</c> — its documented requirement. The
    ///         shape only arises because <em>this</em> rewrite is what puts the bare navigation in
    ///         a slot, so it is this rewrite that owes the materialization. A <c>List&lt;T&gt;</c>
    ///         satisfies every collection interface the body could have been written against, so
    ///         the client-side read needs no adjustment.
    ///     </para>
    ///     <para>
    ///         <b>A member read and nothing else.</b> Stated over any sequence type this cost 27
    ///         tests, and the two shapes it wrongly caught say why. An <c>IGrouping&lt;K, T&gt;</c>
    ///         is an <c>IEnumerable&lt;T&gt;</c> that is not an <c>ICollection&lt;T&gt;</c>, and
    ///         <c>ToList</c>-ing one throws its key away — twenty of the twenty-seven were
    ///         <c>GroupBy</c>. And <c>b.Posts1.OrderBy(p =&gt; p.Id)</c> in a final projection is
    ///         something EF <em>refuses</em>, which
    ///         <c>Collection_without_setter_materialized_correctly</c> asserts; materializing it
    ///         suppressed the refusal. Both are composed sequences, not member reads, which is the
    ///         line this test draws.
    ///     </para>
    ///     <para>
    ///         Deliberately not merged with <see cref="IsQueryableCollection" />: that rule turns
    ///         on whether anything <em>reads</em> the fragment, because an unread
    ///         <see cref="IQueryable{T}" /> in a final projection is another refusal three spec
    ///         tests assert. This one is about a type that cannot be built at all, read or not.
    ///     </para>
    /// </remarks>
    private static Type? UnbuildableNavigationElement(Expression fragment)
    {
        if (fragment is not MemberExpression)
        {
            return null;
        }

        Type type = fragment.Type;
        if (type == typeof(string) || type.IsArray || typeof(IQueryable).IsAssignableFrom(type))
        {
            return null;
        }

        Type element = ServerBoundaryAnalyzer.SequenceElementType(type);

        if (element == type || typeof(ICollection<>).MakeGenericType(element).IsAssignableFrom(type))
        {
            return null;
        }

        // …and only when a `List<T>` is actually a legal value for the declared type. The whole
        // remedy here is to substitute one, so a declared type it does not satisfy must be left
        // alone — otherwise this rewrite replaces the fragment with something that cannot stand
        // where it stood.
        //
        // `MultiLineString` is the case that found this: it implements `IEnumerable<Geometry>`,
        // so every test above passes, but it is a *domain type that happens to be enumerable*
        // rather than a collection. Slotting it as `List<Geometry>` left `e.MultiLineString[0]`
        // and `.Count` unable to bind — "Method 'get_Item' declared on type 'GeometryCollection'
        // cannot be called with instance of type 'List<Geometry>'" (C18's four).
        //
        // The types this method exists for are unaffected: `IReadOnlyList<Name>` is satisfied by
        // a `List<Name>`, which is the point of it.
        return type.IsAssignableFrom(typeof(List<>).MakeGenericType(element))
            ? element
            : null;
    }

    /// <summary>
    ///     Reads a materialized slot back as the queryable the client-side body was built against.
    /// </summary>
    /// <remarks>
    ///     The reassembly still holds the operators the projection wrote — <c>Select</c>,
    ///     <c>ToList</c> — bound to <see cref="IQueryable{T}" />. Substituting a
    ///     <see cref="List{T}" /> under them would not rebuild.
    /// </remarks>
    private static Expression Requeryable(
        Expression slot, Expression fragment, IReadOnlySet<Expression> consumed)
        => IsQueryableCollection(fragment, consumed)
            ? Expression.Call(
                QueryableAsQueryable.MakeGenericMethod(fragment.Type.GetGenericArguments()[0]),
                slot)
            : slot;

    /// <summary>
    ///     Every expression the projection body feeds to a query operator as its source.
    /// </summary>
    private sealed class OperatorSourceCollector : ExpressionVisitor
    {
        private readonly HashSet<Expression> _sources = new(ReferenceEqualityComparer.Instance);

        public static IReadOnlySet<Expression> Find(Expression body)
        {
            var collector = new OperatorSourceCollector();
            collector.Visit(body);
            return collector._sources;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType is { } declaring
                && (declaring == typeof(Queryable) || declaring == typeof(Enumerable))
                && node.Arguments.Count > 0)
            {
                _sources.Add(node.Arguments[0]);
            }

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>
    ///     Every member the query reads, anywhere in it.
    /// </summary>
    /// <remarks>
    ///     By declaring type and name rather than by <see cref="MemberInfo" />: the member a
    ///     <see cref="NewExpression" /> records for a constructor argument and the one a
    ///     <see cref="MemberExpression" /> reads back are not required to be the same reflection
    ///     object — a property's getter and the property itself both name it.
    /// </remarks>
    private sealed class MemberReadCollector : ExpressionVisitor
    {
        private readonly HashSet<(Type, string)> _members = [];

        public static IReadOnlySet<(Type, string)> Find(Expression query)
        {
            var collector = new MemberReadCollector();
            collector.Visit(query);
            return collector._members;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member.DeclaringType is { } declaring)
            {
                _members.Add((declaring, node.Member.Name));
            }

            return base.VisitMember(node);
        }
    }

    private static bool ReferencesParameter(Expression expression, ParameterExpression parameter)
    {
        bool found = false;
        new ParameterFinder(parameter, () => found = true).Visit(expression);
        return found;
    }

    /// <summary>
    ///     Whether a subtree calls a function the model maps to the store, which the client
    ///     therefore cannot compute.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>This is the whole of R158, and the defect it closes had survived every store.</b>
    ///         A mapped function whose arguments are all constants or parameters is
    ///         <em>closed</em>: it refers to no row. <see cref="CollectFragments" /> lifts a
    ///         subtree into the server's tuple only when it refers to a row, which is right for
    ///         <c>1 + 1</c> — the client can compute that, and lifting it would put a constant on
    ///         the wire once per row for nothing. It is wrong here. <c>CustomerOrderCount(1)</c>
    ///         is closed and the client cannot compute it at all: the CLR method is a declaration,
    ///         and EF's specification contexts give it a body that throws precisely so that a
    ///         provider running it locally is caught by name rather than by a wrong number.
    ///     </para>
    ///     <para>
    ///         <b>The condition is "the client cannot", not "the server could".</b> Almost
    ///         anything closed could be lifted, and lifting it would cost payload and gain
    ///         nothing. Only a mapped call has to be.
    ///     </para>
    ///     <para>
    ///         <b>It does not force client evaluation off where EF requires it.</b> Lifting
    ///         happens inside a client-typed projection that is already being split; the mapped
    ///         call becomes one tuple slot and whatever client code wraps it still runs on the
    ///         client, over the value the store returned. That is the same shape as a correlated
    ///         mapped call, which has always worked.
    ///     </para>
    /// </remarks>
    private bool CallsMappedFunction(Expression node)
    {
        if (_model is null)
        {
            return false;
        }

        if (node is MethodCallExpression call
            && Microsoft.EntityFrameworkCore.RelationalModelExtensions.FindDbFunction(_model, call.Method) is not null)
        {
            return true;
        }

        return ChildrenOf(node).Any(CallsMappedFunction);
    }

    /// <summary>
    ///     The maximal subexpressions of a projection body that the server can evaluate for a row.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A fragment must read the row: a body constant needs no round trip and stays on the
    ///         client, where it costs nothing. <b>Unless the client cannot compute it</b>, which
    ///         is a mapped store function and nothing else. See
    ///         <see cref="CallsMappedFunction" />.
    ///     </para>
    ///     <para>
    ///         A fragment taken from the branch of a conditional carries that conditional's test in
    ///         <paramref name="guards" />. See <see cref="Guarded" /> for why.
    ///     </para>
    /// </remarks>
    private void CollectFragments(
        Expression node,
        BoundaryAnalysis analysis,
        IReadOnlyCollection<ParameterExpression> rowParameters,
        List<Expression> fragments,
        IDictionary<Expression, Expression> guards,
        Expression? guard = null)
    {
        NodeFacts facts = analysis.FactsFor(node);

        // `Free.Count > 0` asks "does this refer to a row", and a fragment that does is what the
        // server has to compute. A CLOSED subtree is normally the client's to compute, and
        // cheaper there — unless it calls a function only the store has. See above.
        if (facts.ServerOk
            && facts.Free.All(rowParameters.Contains)
            && (facts.Free.Count > 0 || CallsMappedFunction(node)))
        {
            fragments.Add(node);
            if (guard is not null)
            {
                guards[node] = guard;
            }

            return;
        }

        if (node is ConditionalExpression conditional)
        {
            CollectFragments(conditional.Test, analysis, rowParameters, fragments, guards, guard);

            // A test the server cannot evaluate cannot guard anything, and refusing to lift the
            // branches under one is worse than lifting them unguarded: it costs six tests that
            // pass today — every one of them guarded by an *entity* compared to null, which this
            // analyzer will not ship — and fixes none. Measured, A36.
            NodeFacts test = analysis.FactsFor(conditional.Test);
            bool guardable = test.ServerOk && test.Free.All(rowParameters.Contains);

            CollectFragments(
                conditional.IfTrue, analysis, rowParameters, fragments, guards,
                guardable ? And(guard, conditional.Test) : guard);
            CollectFragments(
                conditional.IfFalse, analysis, rowParameters, fragments, guards,
                guardable ? And(guard, Expression.Not(conditional.Test)) : guard);
            return;
        }

        foreach (Expression child in ChildrenOf(node))
        {
            CollectFragments(child, analysis, rowParameters, fragments, guards, guard);
        }
    }

    private static Expression And(Expression? guard, Expression test)
        => guard is null ? test : Expression.AndAlso(guard, test);

    /// <summary>
    ///     Wraps a fragment lifted out of a conditional branch in the test that guarded it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>Select(x =&gt; new { x.Note, Nullable = x.GearNickName != null ? new { x.Gear.Nickname,
    ///         x.Gear.SquadId } : null })</c> is client-typed at the conditional, so
    ///         <see cref="CollectFragments" /> descends through it and takes <c>x.Gear.Nickname</c>
    ///         and <c>x.Gear.SquadId</c> as fragments of their own — <em>outside the test that was
    ///         guarding them</em>. The server then evaluates <c>x.Gear.SquadId</c> for a tag with no
    ///         gear, which is exactly the dereference the <c>!= null</c> existed to prevent:
    ///         <c>Nullable object must have a value</c>, 26 times over
    ///         <c>GearsOfWarQueryTestBase</c>.
    ///     </para>
    ///     <para>
    ///         The slot travels as <c>test ? fragment : default</c>, so the server evaluates it only
    ///         where the projection would have. The client body still holds the conditional and only
    ///         reads the slot down the branch it belongs to, so the default is never observed.
    ///     </para>
    ///     <para>
    ///         The default is a <see cref="ConstantExpression" /> and not
    ///         <see cref="Expression.Default(Type)" />: a <c>DefaultExpression</c> is not one of the
    ///         serializable kinds (research-findings §5), so a guard built from one made the whole
    ///         rewritten call unshippable — six tests fell back to the residual, where the
    ///         navigation they read had no query to carry it.
    ///     </para>
    /// </remarks>
    private static Expression Guarded(
        Expression shipped, Expression fragment, IReadOnlyDictionary<Expression, Expression> guards)
        => guards.TryGetValue(fragment, out Expression? guard)
            ? Expression.Condition(
                guard,
                shipped,
                Expression.Constant(
                    shipped.Type.IsValueType ? Activator.CreateInstance(shipped.Type) : null, shipped.Type))
            : shipped;

    private static IEnumerable<Expression> ChildrenOf(Expression node)
    {
        var children = new List<Expression>();
        new ChildCollector(children).Visit(node);
        return children;
    }

    private static Expression StripQuotes(Expression node)
    {
        while (node is UnaryExpression { NodeType: ExpressionType.Quote } quote)
        {
            node = quote.Operand;
        }

        return node;
    }

    private sealed class SlotSubstitutingVisitor(IReadOnlyDictionary<Expression, Expression> slots)
        : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
            => node is not null && slots.TryGetValue(node, out Expression? slot)
                ? slot
                : base.Visit(node);
    }

    private sealed class ParameterFinder(ParameterExpression parameter, Action onFound) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == parameter)
            {
                onFound();
            }

            return node;
        }
    }

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
