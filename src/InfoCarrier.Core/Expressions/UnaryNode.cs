// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A unary operator (Not, Negate, Convert, Quote, …). The operator kind is an explicit
///     map, never an int-cast (expression-serialization §3.7).
/// </summary>
public sealed record UnaryNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Unary;

    /// <summary>
    ///     The unary operator (Not, Negate, Convert, Quote, …). Stored as the
    ///     <see cref="System.Linq.Expressions.ExpressionType" /> value name; mapped explicitly.
    /// </summary>
    public required string Operator { get; init; }

    /// <summary>
    ///     The operand.
    /// </summary>
    public required ExpressionNode Operand { get; init; }

    /// <summary>
    ///     The result type.
    /// </summary>
    public required TypeNode Type { get; init; }

    /// <summary>
    ///     The implementing method for user-defined conversions/operators; null otherwise.
    /// </summary>
    public MethodNode? Method { get; init; }
}
