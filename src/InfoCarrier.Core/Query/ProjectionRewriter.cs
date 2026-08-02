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

    /// <summary>
    ///     Rewrites every client-typed projection in <paramref name="query" /> that sits directly
    ///     on a server-executable source.
    /// </summary>
    public static Expression Rewrite(Expression query, ServerBoundaryAnalyzer analyzer)
        => new ProjectionRewriter(analyzer).Visit(query);

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Innermost first: rewriting an inner projection can turn its result into something the
        // outer one can then be measured against.
        if (base.VisitMethodCall(node) is not MethodCallExpression call)
        {
            return node;
        }

        if (call.Method.DeclaringType != typeof(Queryable)
            || call.Method.Name != nameof(Queryable.Select)
            || call.Arguments.Count != 2
            || StripQuotes(call.Arguments[1]) is not LambdaExpression selector
            || selector.Parameters.Count != 1)
        {
            return call;
        }

        Expression source = call.Arguments[0];
        BoundaryAnalysis sourceAnalysis = analyzer.Analyze(source);
        if (!sourceAnalysis.FactsFor(source).ServerOk
            || !ServerBoundaryAnalyzer.IsExecutableQuery(source))
        {
            // Nothing to hand the server; the whole projection is client work already.
            return call;
        }

        BoundaryAnalysis bodyAnalysis = analyzer.Analyze(selector);
        if (bodyAnalysis.FactsFor(selector.Body).ServerOk)
        {
            // The projection is server-executable as written.
            return call;
        }

        List<Expression> fragments = [];
        CollectFragments(selector.Body, bodyAnalysis, selector.Parameters[0], fragments);
        if (fragments.Count == 0)
        {
            // A body that reads nothing from the row — leave it to the plain cut.
            return call;
        }

        Expression tuple = TupleCarrier.New(fragments);
        Expression serverSelect = Expression.Call(
            QueryableSelect.MakeGenericMethod(selector.Parameters[0].Type, tuple.Type),
            source,
            Expression.Quote(Expression.Lambda(tuple, selector.Parameters[0])));

        ParameterExpression row = Expression.Parameter(tuple.Type, "row");
        var slots = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < fragments.Count; i++)
        {
            slots[fragments[i]] = TupleCarrier.Read(row, i);
        }

        Expression clientBody = new SlotSubstitutingVisitor(slots).Visit(selector.Body)!;

        return Expression.Call(
            QueryableSelect.MakeGenericMethod(tuple.Type, selector.ReturnType),
            serverSelect,
            Expression.Quote(Expression.Lambda(clientBody, row)));
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
        ParameterExpression row,
        List<Expression> fragments)
    {
        NodeFacts facts = analysis.FactsFor(node);

        if (facts.ServerOk && facts.Free.Count > 0 && facts.Free.All(p => p == row))
        {
            fragments.Add(node);
            return;
        }

        foreach (Expression child in ChildrenOf(node))
        {
            CollectFragments(child, analysis, row, fragments);
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
