// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Rewrites <c>SelectMany</c> over <c>GroupJoin</c> into a single <c>Join</c> or
///     <c>LeftJoin</c>, removing the transparent identifier the C# compiler put between them
///     (<c>docs/transparent-identifiers.md</c> §3.1,
///     [ADR-011](../../../docs/decisions.md#adr-011)).
/// </summary>
/// <remarks>
///     <para>
///         A mirror of EF Core's
///         <c>QueryableMethodNormalizingExpressionVisitor.TryFlattenGroupJoinSelectMany</c>
///         (<c>subrepos/efcore/src/EFCore/Query/Internal/QueryableMethodNormalizingExpressionVisitor.cs:566</c>).
///         EF runs it inside <c>CompileQuery</c> — after [ADR-006](../../../docs/decisions.md)
///         capture on the client, and after a deserialization that cannot happen on the server,
///         because the identifier is an anonymous type the server's assembly does not contain.
///     </para>
///     <para>
///         Two things it must do that EF gets for free, both learned by measurement.
///     </para>
///     <para>
///         <b>Match <see cref="Enumerable" /> as well as <see cref="Queryable" />.</b> EF
///         normalizes one to the other first (<c>TryConvertEnumerableToQueryable</c>), so by the
///         time its matcher runs everything is <see cref="Queryable" />. Here nothing has done
///         that: <c>from o in grouping.DefaultIfEmpty()</c> binds to
///         <see cref="Enumerable.DefaultIfEmpty{TSource}(IEnumerable{TSource})" />, because the
///         grouping is an <see cref="IEnumerable{T}" />. Matching only the
///         <see cref="Queryable" /> overloads made the first version of this file fire on
///         **nothing at all** while looking entirely correct.
///     </para>
///     <para>
///         <b>Decline a rewrite that strands a parameter.</b> Substituting the group-join result
///         selector reconstructs the identifier including its grouping member, so
///         <c>… into g from o in g where … select o</c> yields a join naming <c>g</c> while
///         binding only the outer and inner elements. EF's projection binding drops the dead
///         member before anything is compiled; this provider compiles the residual and gets
///         <c>variable 'g' … referenced from scope ''</c>. Where the tail of EF's pipeline is what
///         made the output legal, the honest move is to leave the tree alone.
///     </para>
/// </remarks>
internal sealed class GroupJoinFlattener : ExpressionVisitor
{
    /// <summary>
    ///     Flattens every <c>GroupJoin</c> + <c>SelectMany</c> pair in a captured query.
    /// </summary>
    internal static Expression Flatten(Expression query)
        => new GroupJoinFlattener().Visit(query);

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Children first, as EF does: an inner pair must already be flattened for the outer
        // match to see a plain `GroupJoin` underneath it.
        Expression visited = base.VisitMethodCall(node);

        return visited is MethodCallExpression call && IsQueryOperator(call.Method)
            ? TryFlatten(call)
            : visited;
    }

    private Expression TryFlatten(MethodCallExpression node)
    {
        if (node.Method.Name != nameof(Queryable.SelectMany)
            || node.Arguments is not [MethodCallExpression groupJoin, ..]
            || !IsQueryOperator(groupJoin.Method)
            || groupJoin.Method.Name != nameof(Queryable.GroupJoin)
            || groupJoin.Arguments.Count != 5
            || !groupJoin.Method.IsGenericMethod)
        {
            return node;
        }

        Expression outer = groupJoin.Arguments[0];
        Expression inner = groupJoin.Arguments[1];
        LambdaExpression outerKeySelector = Unquote(groupJoin.Arguments[2]);
        LambdaExpression innerKeySelector = Unquote(groupJoin.Arguments[3]);
        LambdaExpression groupJoinResultSelector = Unquote(groupJoin.Arguments[4]);

        ParameterExpression grouping = groupJoinResultSelector.Parameters[1];
        Type[] genericArguments = groupJoin.Method.GetGenericArguments();

        if (node.Arguments.Count != 3)
        {
            // `SelectMany` without a collection selector is the rarer half of EF's match and has
            // never appeared in the suite; leaving it out keeps the untested path out of the code.
            return node;
        }

        LambdaExpression collectionSelector = Unquote(node.Arguments[1]);
        LambdaExpression resultSelector = Unquote(node.Arguments[2]);

        Expression collectionBody = StripDefaultIfEmpty(collectionSelector.Body, out bool defaultIfEmpty);

        // Substituting the group-join result selector's body for the SelectMany parameter is what
        // makes `ti.grouping` read as `grouping` again — the `new { … }.Member` collapse happens
        // inside the replacing visitor, which is why the identifier simply disappears.
        collectionBody = ReplacingExpressionVisitor.Replace(
            collectionSelector.Parameters[0], groupJoinResultSelector.Body, collectionBody);

        if (IsCorrelated(collectionBody, grouping))
        {
            // EF declines here too, and has a `TODO` for it. Giving up in the same place is the
            // point of a mirror.
            return node;
        }

        inner = Visit(ReplacingExpressionVisitor.Replace(grouping, inner, collectionBody));

        Expression resultBody = ReplacingExpressionVisitor.Replace(
            resultSelector.Parameters[0], groupJoinResultSelector.Body, resultSelector.Body);

        LambdaExpression flattened = Expression.Lambda(
            resultBody, groupJoinResultSelector.Parameters[0], resultSelector.Parameters[1]);

        if (IsOpen(flattened))
        {
            return node;
        }

        genericArguments[^1] = flattened.ReturnType;

        MethodInfo join = JoinMethod(groupJoin.Method.DeclaringType!, defaultIfEmpty)
            .MakeGenericMethod(genericArguments);

        return Expression.Call(join, outer, inner, outerKeySelector, innerKeySelector, flattened);
    }

    private static bool IsQueryOperator(MethodInfo method)
        => method.DeclaringType == typeof(Queryable) || method.DeclaringType == typeof(Enumerable);

    private static MethodInfo JoinMethod(Type declaringType, bool defaultIfEmpty)
    {
        if (declaringType == typeof(Queryable))
        {
            return defaultIfEmpty ? QueryableMethods.LeftJoin : QueryableMethods.Join;
        }

        return typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == (defaultIfEmpty ? nameof(Enumerable.LeftJoin) : nameof(Enumerable.Join))
                && m.GetGenericArguments().Length == 4
                && m.GetParameters().Length == 5);
    }

    private static Expression StripDefaultIfEmpty(Expression body, out bool defaultIfEmpty)
    {
        if (body is MethodCallExpression call
            && IsQueryOperator(call.Method)
            && call.Method.Name == nameof(Queryable.DefaultIfEmpty)
            && call.Arguments.Count == 1)
        {
            defaultIfEmpty = true;
            return call.Arguments[0];
        }

        defaultIfEmpty = false;
        return body;
    }

    private static LambdaExpression Unquote(Expression node)
    {
        while (node is UnaryExpression { NodeType: ExpressionType.Quote } quote)
        {
            node = quote.Operand;
        }

        return (LambdaExpression)node;
    }

    private static bool IsCorrelated(Expression body, ParameterExpression grouping)
        => new CorrelationVerifier().Verify(body, grouping);

    private static bool IsOpen(LambdaExpression lambda)
    {
        var finder = new FreeParameterFinder();
        finder.Visit(lambda);
        return finder.Free.Count > 0;
    }

    /// <summary>
    ///     Whether a collection selector depends on anything but the grouping itself. EF's
    ///     <c>SelectManyVerifyingExpressionVisitor</c>, reproduced — over both operator families.
    /// </summary>
    private sealed class CorrelationVerifier : ExpressionVisitor
    {
        private static readonly HashSet<string> AllowedMethods =
            [nameof(Queryable.Where), nameof(Queryable.AsQueryable)];

        private readonly List<ParameterExpression> _allowedParameters = [];

        private ParameterExpression? _rootParameter;
        private int _rootParameterCount;
        private bool _correlated;

        public bool Verify(Expression body, ParameterExpression rootParameter)
        {
            _rootParameter = rootParameter;

            Visit(body);

            if (_rootParameterCount == 1)
            {
                // Walk the spine to whatever the chain is rooted at. Anything that is not a member
                // read, a query operator or the grouping itself means the selector is built from
                // something else, and the join key would be a lie.
                Expression? expression = body;
                while (expression is not null)
                {
                    if (expression is MemberExpression member)
                    {
                        expression = member.Expression;
                    }
                    else if (expression is MethodCallExpression call && IsQueryOperator(call.Method))
                    {
                        expression = call.Arguments[0];
                    }
                    else if (expression is ParameterExpression)
                    {
                        if (expression != _rootParameter)
                        {
                            _correlated = true;
                        }

                        break;
                    }
                    else
                    {
                        _correlated = true;
                        break;
                    }
                }
            }

            return _correlated;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            try
            {
                _allowedParameters.AddRange(node.Parameters);
                return base.VisitLambda(node);
            }
            finally
            {
                foreach (ParameterExpression parameter in node.Parameters)
                {
                    _allowedParameters.Remove(parameter);
                }
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (_correlated)
            {
                return node;
            }

            if (IsQueryOperator(node.Method) && !AllowedMethods.Contains(node.Method.Name))
            {
                if (node.Method.Name == nameof(Queryable.Select)
                    && node.Arguments.Count == 2
                    && Unquote(node.Arguments[1]) is { } selector
                    && selector.Body == selector.Parameters[0])
                {
                    // An identity projection changes nothing, so it changes nothing here either.
                    return node;
                }

                _correlated = true;
                return node;
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_allowedParameters.Contains(node))
            {
                return node;
            }

            if (node == _rootParameter)
            {
                _rootParameterCount++;
                return node;
            }

            _correlated = true;
            return base.VisitParameter(node);
        }
    }

    private sealed class FreeParameterFinder : ExpressionVisitor
    {
        private readonly List<ParameterExpression> _bound = [];

        public HashSet<ParameterExpression> Free { get; } = new(ReferenceEqualityComparer.Instance);

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            try
            {
                _bound.AddRange(node.Parameters);
                return base.VisitLambda(node);
            }
            finally
            {
                foreach (ParameterExpression parameter in node.Parameters)
                {
                    _bound.Remove(parameter);
                }
            }
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (!_bound.Contains(node))
            {
                Free.Add(node);
            }

            return node;
        }
    }
}
