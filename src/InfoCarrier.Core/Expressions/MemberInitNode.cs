// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A single member binding in a <see cref="MemberInitNode" /> (assignment of a value to a
///     member of a newly constructed object).
/// </summary>
public sealed record MemberBindingNode
{
    /// <summary>
    ///     The member being assigned.
    /// </summary>
    public required TypeNode DeclaringType { get; init; }

    /// <summary>
    ///     The member name.
    /// </summary>
    public required string MemberName { get; init; }

    /// <summary>
    ///     Whether the member is a property or a field.
    /// </summary>
    public MemberKind MemberKind { get; init; }

    /// <summary>
    ///     The value assigned to the member.
    /// </summary>
    public required ExpressionNode Value { get; init; }
}

/// <summary>
///     A member-init expression: <c>new T { A = x, B = y }</c>. Common in DTO / anonymous
///     projections (requirements §3).
/// </summary>
public sealed record MemberInitNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.MemberInit;

    /// <summary>
    ///     The object construction being initialized.
    /// </summary>
    public required NewNode NewExpression { get; init; }

    /// <summary>
    ///     The member bindings (assignments).
    /// </summary>
    public IReadOnlyList<MemberBindingNode> Bindings { get; init; } = [];

    /// <summary>
    ///     The result type.
    /// </summary>
    public required TypeNode Type { get; init; }
}
