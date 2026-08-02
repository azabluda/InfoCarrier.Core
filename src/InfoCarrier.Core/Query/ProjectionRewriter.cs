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

    private readonly HashSet<Expression> _reassemblies = new(ReferenceEqualityComparer.Instance);

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
        out IReadOnlySet<Expression> reassemblies)
    {
        var rewriter = new ProjectionRewriter(analyzer);
        Expression result = rewriter.Visit(query);
        reassemblies = rewriter._reassemblies;
        return result;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Innermost first: rewriting an inner projection can turn its result into something the
        // outer one can then be measured against.
        if (base.VisitMethodCall(node) is not MethodCallExpression call)
        {
            return node;
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

        Expression tuple = TupleCarrier.New(fragments);
        ParameterExpression row = Expression.Parameter(tuple.Type, "row");

        var slots = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < fragments.Count; i++)
        {
            slots[fragments[i]] = TupleCarrier.Read(row, i);
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
