// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A binary operator. The operator kind is carried by <see cref="Operator" /> as an
///     explicit enum map — never an int-cast across the boundary (expression-serialization §3.7).
/// </summary>
public sealed record BinaryNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Binary;

    /// <summary>
    ///     The binary operator (Add, Equal, AndAlso, …). Stored as the
    ///     <see cref="System.Linq.Expressions.ExpressionType" /> value name; mapped explicitly
    ///     during translation.
    /// </summary>
    public required string Operator { get; init; }

    /// <summary>
    ///     The left operand.
    /// </summary>
    public required ExpressionNode Left { get; init; }

    /// <summary>
    ///     The right operand.
    /// </summary>
    public required ExpressionNode Right { get; init; }

    /// <summary>
    ///     The result type.
    /// </summary>
    public required TypeNode Type { get; init; }

    /// <summary>
    ///     The implementing method for user-defined operators; null for built-in operators.
    /// </summary>
    public MethodNode? Method { get; init; }

    /// <summary>
    ///     Whether this is a lifted (nullable) operator.
    /// </summary>
    public bool IsLiftedToNull { get; init; }
}
