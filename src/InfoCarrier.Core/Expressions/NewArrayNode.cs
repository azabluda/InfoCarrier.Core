// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     An array construction (<c>new T[] { … }</c>).
/// </summary>
public sealed record NewArrayNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.NewArray;

    /// <summary>
    ///     The array type (e.g. <c>System.Int32[]</c>).
    /// </summary>
    public required TypeNode Type { get; init; }

    /// <summary>
    ///     The element expressions.
    /// </summary>
    public IReadOnlyList<ExpressionNode> Expressions { get; init; } = [];
}
