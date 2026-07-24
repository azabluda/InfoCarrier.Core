// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A method call. The method is a re-resolvable <see cref="MethodNode" />; instance calls
///     carry an <see cref="Instance" />, static/extension calls carry only <see cref="Arguments" />
///     (the receiver is argument 0 for extension methods such as LINQ operators).
/// </summary>
public sealed record MethodCallNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.MethodCall;

    /// <summary>
    ///     The method being called.
    /// </summary>
    public required MethodNode Method { get; init; }

    /// <summary>
    ///     The instance expression; null for static/extension methods.
    /// </summary>
    public ExpressionNode? Instance { get; init; }

    /// <summary>
    ///     The call arguments.
    /// </summary>
    public IReadOnlyList<ExpressionNode> Arguments { get; init; } = [];

    /// <summary>
    ///     The call's result type.
    /// </summary>
    public required TypeNode Type { get; init; }
}
