// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Reads a member through the construction that set it, as EF does before it translates:
///     <c>new { Name = g.LeaderNickname }.Name</c> is <c>g.LeaderNickname</c>, and
///     <c>(test ? new Dto { Name = x } : null).Name</c> is <c>test ? x : null</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Since 2026-09-26, found by #167's slow run.</b> A construction of a type only this
///         client has makes whatever holds it unshippable, even when the only thing read from it is
///         a value the server has. <c>where new { Name = g.LeaderNickname, Squad = g.LeaderSquadId
///         }.Name == "Marcus"</c> therefore ran on the client over every row of the table, where
///         EF's own client writes the filter: <c>Where_member_access_on_anonymous_type</c>.
///     </para>
///     <para>
///         <b>The construction half is EF's own <see cref="ReplacingExpressionVisitor" />, used with
///         nothing to replace</b>: its member visit folds a read through a
///         <see cref="NewExpression" /> that lists its members and through a
///         <see cref="MemberInitExpression" />, a cast to an interface included. The conditional
///         half is the shape EF's own optimizer removes before translation, and only with a
///         <see langword="null" /> branch whose member type can hold a <see langword="null" />, so
///         no type changes.
///     </para>
/// </remarks>
internal sealed class MemberReadFolder() : ReplacingExpressionVisitor([], [])
{
    /// <summary>Folds every member read through a construction in <paramref name="tree" />.</summary>
    public static Expression Fold(Expression tree)
        => new MemberReadFolder().Visit(tree);

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression memberExpression)
    {
        Expression folded = base.VisitMember(memberExpression);

        if (folded is MemberExpression { Expression: { } holder } member
            && Unconverted(holder) is ConditionalExpression conditional
            && (IsNull(conditional.IfTrue) || IsNull(conditional.IfFalse))
            && (!member.Type.IsValueType || Nullable.GetUnderlyingType(member.Type) is not null))
        {
            return Expression.Condition(
                conditional.Test,
                Branch(conditional.IfTrue, member),
                Branch(conditional.IfFalse, member),
                member.Type);
        }

        return folded;
    }

    private Expression Branch(Expression branch, MemberExpression member)
        => IsNull(branch)
            ? Expression.Constant(null, member.Type)
            : Visit(Expression.MakeMemberAccess(Unconverted(branch), member.Member));

    private static Expression Unconverted(Expression node)
    {
        while (node is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs } convert
               && !convert.Operand.Type.IsValueType)
        {
            node = convert.Operand;
        }

        return node;
    }

    private static bool IsNull(Expression node)
        => Unconverted(node) is ConstantExpression { Value: null };
}
