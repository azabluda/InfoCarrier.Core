// Licensed under the MIT license. See license.txt file in the project root for license information.

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

    private readonly HashSet<Expression> _reassemblies = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     A client-side rebuild another pass already produced. Rewriting it would only wrap one
    ///     carrier in another.
    /// </summary>
    private Expression? _preserved;

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
    public static Expression Rewrite(
        Expression query,
        ServerBoundaryAnalyzer analyzer,
        out IReadOnlySet<Expression> reassemblies,
        Expression? alreadyReassembled = null)
    {
        var rewriter = new ProjectionRewriter(analyzer) { _preserved = alreadyReassembled };
        Expression result = rewriter.Visit(query);
        reassemblies = rewriter._reassemblies;
        return result;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Checked before descending, because this shape has to *replace* the in-place rewrite
        // rather than tidy up after it — see TryHoistCollectionProjection.
        if (TryHoistCollectionProjection(node) is { } hoisted)
        {
            return hoisted;
        }

        // Innermost first: rewriting an inner projection can turn its result into something the
        // outer one can then be measured against.
        if (base.VisitMethodCall(node) is not MethodCallExpression call)
        {
            return node;
        }

        if (ReferenceEquals(node, _preserved))
        {
            _reassemblies.Add(call);
            return call;
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

        BoundaryAnalysis bodyAnalysis = analyzer.Analyze(selector!);
        if (bodyAnalysis.FactsFor(selector.Body).ServerOk)
        {
            // The projection is server-executable as written.
            return call;
        }

        List<Expression> fragments = [];
        CollectFragments(selector!.Body, bodyAnalysis, selector.Parameters, fragments);
        if (fragments.Count == 0)
        {
            // A body that reads nothing from the row — leave it to the plain cut.
            return call;
        }

        IReadOnlySet<Expression> consumed = OperatorSourceCollector.Find(selector.Body);

        Expression tuple = TupleCarrier.New([.. fragments.Select(f => Materialized(f, consumed))]);
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

        MethodCallExpression reassembly = Expression.Call(
            (quoted ? QueryableSelect : EnumerableSelect)
                .MakeGenericMethod(tuple.Type, selector.ReturnType),
            serverCall,
            Wrap(Expression.Lambda(clientBody, row)));

        _reassemblies.Add(reassembly);
        return reassembly;
    }

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
    internal static bool IsResultSelectorOperator(MethodCallExpression call, out LambdaExpression? selector)
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

        BoundaryAnalysis bodyAnalysis = analyzer.Analyze(innerSelector!);
        if (bodyAnalysis.FactsFor(innerSelector.Body).ServerOk)
        {
            // Nothing client-typed here; the ordinary path ships it whole.
            return null;
        }

        ParameterExpression[] rowParameters = [.. innerSelector.Parameters, outer];

        List<Expression> fragments = [];
        CollectFragments(innerSelector.Body, bodyAnalysis, rowParameters, fragments);
        if (fragments.Count == 0)
        {
            return null;
        }

        IReadOnlySet<Expression> consumed = OperatorSourceCollector.Find(innerSelector.Body);

        Expression tuple = TupleCarrier.New([.. fragments.Select(f => Materialized(f, consumed))]);
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
        return reassembly;
    }

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
            // Only when the projection *composes over* it. A queryable handed straight to the
            // result is what the caller asked for, and EF is right to refuse it — two spec tests
            // assert exactly that error (`AssertInvalidMaterializationType`), and materializing
            // here suppressed it. The projection's own operators are the signal: `frag.Select(…)`
            // is an intermediate, `new Dto { Orders = frag }` is the answer.
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
            : fragment;

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

    private static bool ReferencesParameter(Expression expression, ParameterExpression parameter)
    {
        bool found = false;
        new ParameterFinder(parameter, () => found = true).Visit(expression);
        return found;
    }

    /// <summary>
    ///     The maximal subexpressions of a projection body that the server can evaluate for a row.
    /// </summary>
    /// <remarks>
    ///     A fragment must read the row: a body constant needs no round trip and stays on the
    ///     client, where it costs nothing.
    /// </remarks>
    private static void CollectFragments(
        Expression node,
        BoundaryAnalysis analysis,
        IReadOnlyCollection<ParameterExpression> rowParameters,
        List<Expression> fragments)
    {
        NodeFacts facts = analysis.FactsFor(node);

        if (facts.ServerOk && facts.Free.Count > 0 && facts.Free.All(rowParameters.Contains))
        {
            fragments.Add(node);
            return;
        }

        foreach (Expression child in ChildrenOf(node))
        {
            CollectFragments(child, analysis, rowParameters, fragments);
        }
    }

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
