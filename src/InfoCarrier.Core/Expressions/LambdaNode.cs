// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A lambda expression. Parameters are <see cref="ParameterNode" />s whose identity the
///     server remaps when rebuilding the tree (requirements §2.3).
/// </summary>
public sealed record LambdaNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Lambda;

    /// <summary>
    ///     The lambda body.
    /// </summary>
    public required ExpressionNode Body { get; init; }

    /// <summary>
    ///     The lambda parameters, in order.
    /// </summary>
    public IReadOnlyList<ParameterNode> Parameters { get; init; } = [];

    /// <summary>
    ///     The lambda's delegate type (e.g. <c>Func&lt;Order,bool&gt;</c>).
    /// </summary>
    public required TypeNode Type { get; init; }

    /// <summary>
    ///     The return type of the lambda body.
    /// </summary>
    public required TypeNode ReturnType { get; init; }
}
