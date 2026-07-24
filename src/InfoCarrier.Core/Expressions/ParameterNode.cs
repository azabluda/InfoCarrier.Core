// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A lambda/closure parameter. Parameter identity is remapped between client and server
///     by <see cref="Name" /> + position (requirements §2.3); the server rebuilds
///     <see cref="System.Linq.Expressions.ParameterExpression" /> instances during translation.
/// </summary>
public sealed record ParameterNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Parameter;

    /// <summary>
    ///     The parameter name (null for unnamed parameters).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     The parameter type.
    /// </summary>
    public required TypeNode Type { get; init; }
}
