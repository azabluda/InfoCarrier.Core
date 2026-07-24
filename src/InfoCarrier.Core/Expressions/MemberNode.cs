// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     The kind of member a <see cref="MemberNode" /> refers to.
/// </summary>
public enum MemberKind
{
    /// <summary>A property.</summary>
    Property = 0,

    /// <summary>A field.</summary>
    Field = 1,
}

/// <summary>
///     Member access (property or field). The member is identified by declaring type + name +
///     kind — reflection <c>MemberInfo</c> never crosses the wire; the server re-resolves the
///     member against its own type model.
/// </summary>
public sealed record MemberNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Member;

    /// <summary>
    ///     The type declaring the member.
    /// </summary>
    public required TypeNode DeclaringType { get; init; }

    /// <summary>
    ///     The member name.
    /// </summary>
    public required string MemberName { get; init; }

    /// <summary>
    ///     Whether this is a property or a field.
    /// </summary>
    public required MemberKind MemberKind { get; init; }

    /// <summary>
    ///     The member's value type.
    /// </summary>
    public required TypeNode Type { get; init; }

    /// <summary>
    ///     The instance expression; null for static members.
    /// </summary>
    public ExpressionNode? Instance { get; init; }
}
