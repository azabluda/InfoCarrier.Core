// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A conditional (ternary) expression: <c>test ? ifTrue : ifFalse</c>.
/// </summary>
public sealed record ConditionalNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Conditional;

    /// <summary>
    ///     The test expression.
    /// </summary>
    public required ExpressionNode Test { get; init; }

    /// <summary>
    ///     The expression evaluated when the test is true.
    /// </summary>
    public required ExpressionNode IfTrue { get; init; }

    /// <summary>
    ///     The expression evaluated when the test is false.
    /// </summary>
    public required ExpressionNode IfFalse { get; init; }

    /// <summary>
    ///     The result type.
    /// </summary>
    public required TypeNode Type { get; init; }
}
