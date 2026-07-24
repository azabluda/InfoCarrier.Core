// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     An invocation of a lambda or delegate: <c>func(a, b)</c>.
/// </summary>
public sealed record InvocationNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Invocation;

    /// <summary>
    ///     The lambda/delegate being invoked.
    /// </summary>
    public required ExpressionNode Expression { get; init; }

    /// <summary>
    ///     The invocation arguments.
    /// </summary>
    public IReadOnlyList<ExpressionNode> Arguments { get; init; } = [];

    /// <summary>
    ///     The result type.
    /// </summary>
    public required TypeNode Type { get; init; }
}
