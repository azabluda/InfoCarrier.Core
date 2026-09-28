// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Refuses a client-side projection that holds an object the caller wrote into the query as a
///     constant, with EF's own three messages (#113).
/// </summary>
/// <remarks>
///     <para>
///         EF raises this in <c>ShapedQueryCompilingExpressionVisitor.VerifyNoClientConstant</c>, on
///         the shaper, which is downstream of ADR-006's capture point, so the client never ran it.
///         The typical case is a query written inside the <c>DbContext</c> that calls one of its
///         instance methods: the compiler captures <c>this</c> as a constant, EF's query cache keys
///         the entry on it, and the cache then keeps every disposed context that ran the query
///         until the entry is evicted. Measured 2026-09-15 on Tier B before this refusal existed.
///     </para>
///     <para>
///         <b>The check needs two moments, and that is why it is two methods.</b> Which constants
///         to refuse is decided on the tree as captured, where a closure-captured value is still an
///         EF query parameter and EF's check lets it pass. <c>QueryExecutor</c> then replaces every
///         parameter with its value as a plain constant, so in the tree it splits a local variable
///         and <c>this</c> look the same. Where they sit is decided on the residual, after the split,
///         because only what runs on the client is a client projection. The two meet by reference:
///         the rewrites between them keep a constant node they do not change.
///     </para>
///     <para>
///         The rule is EF's: a constant passes when it is null, when the client's type mapping
///         source maps its type, or when it is an empty array. The mapping source is the client's,
///         so this refuses on every store. Plain EF on the InMemory provider does not, because that
///         provider maps every type.
///     </para>
/// </remarks>
internal static class CapturedConstantValidator
{
    /// <summary>
    ///     Finds the constants in the captured query that EF would refuse in a client projection.
    /// </summary>
    /// <param name="query">The query as captured, before any parameter is replaced by its value.</param>
    /// <param name="typeMappingSource">The client's type mapping source.</param>
    /// <returns>The constants to refuse wherever they reach the client, compared by reference.</returns>
    public static IReadOnlySet<ConstantExpression> FindRefusable(Expression query, ITypeMappingSource typeMappingSource)
    {
        var finder = new RefusableConstantFinder(typeMappingSource);
        finder.Visit(query);
        return finder.Found;
    }

    /// <summary>
    ///     Throws EF's refusal if the residual holds one of <paramref name="refusable" />.
    /// </summary>
    /// <param name="residual">What the split leaves to run on the client.</param>
    /// <param name="refusable">What <see cref="FindRefusable" /> found in the captured query.</param>
    public static void Validate(Expression residual, IReadOnlySet<ConstantExpression> refusable)
    {
        if (refusable.Count > 0)
        {
            new ResidualVerifier(refusable).Visit(residual);
        }
    }

    private static Expression? RemoveConvert(Expression? expression)
    {
        while (expression is { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked })
        {
            expression = ((UnaryExpression)expression).Operand;
        }

        return expression;
    }

    private sealed class RefusableConstantFinder(ITypeMappingSource typeMappingSource) : ExpressionVisitor
    {
        public HashSet<ConstantExpression> Found { get; } = new(ReferenceEqualityComparer.Instance);

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is not null
                && node.Value is not Array { Length: 0 }
                && typeMappingSource.FindMapping(node.Type) is null)
            {
                Found.Add(node);
            }

            return node;
        }

        // A query root or an EF query parameter holds no constant the caller wrote, and an
        // extension node that cannot reduce throws when asked for its children.
        protected override Expression VisitExtension(Expression node)
            => node;
    }

    /// <summary>
    ///     EF's <c>ConstantVerifyingExpressionVisitor</c>, in EF's order, with "is not valid" read
    ///     as "was found refusable in the captured query".
    /// </summary>
    private sealed class ResidualVerifier(IReadOnlySet<ConstantExpression> refusable) : ExpressionVisitor
    {
        protected override Expression VisitConstant(ConstantExpression node)
            => refusable.Contains(node)
                ? throw new InvalidOperationException(
                    CoreStrings.ClientProjectionCapturingConstantInTree(QuerySplitter.DisplayName(node.Type)))
                : node;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (RemoveConvert(node.Object) is ConstantExpression instance && refusable.Contains(instance))
            {
                throw new InvalidOperationException(
                    CoreStrings.ClientProjectionCapturingConstantInMethodInstance(
                        QuerySplitter.DisplayName(instance.Type),
                        node.Method.Name));
            }

            foreach (Expression argument in node.Arguments)
            {
                if (RemoveConvert(argument) is ConstantExpression constant && refusable.Contains(constant))
                {
                    throw new InvalidOperationException(
                        CoreStrings.ClientProjectionCapturingConstantInMethodArgument(
                            QuerySplitter.DisplayName(constant.Type),
                            node.Method.Name));
                }
            }

            return base.VisitMethodCall(node);
        }

        protected override Expression VisitExtension(Expression node)
            => node;
    }
}
