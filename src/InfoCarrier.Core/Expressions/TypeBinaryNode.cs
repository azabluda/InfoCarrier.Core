// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A type test (<c>x is T</c>) or cast-as (<c>x as T</c>) expression
///     (<see cref="System.Linq.Expressions.ExpressionType.TypeIs" /> /
///     <see cref="System.Linq.Expressions.ExpressionType.TypeEqual" />).
/// </summary>
public sealed record TypeBinaryNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.TypeBinary;

    /// <summary>
    ///     The operator (<c>TypeIs</c> or <c>TypeEqual</c>), mapped explicitly.
    /// </summary>
    public required string Operator { get; init; }

    /// <summary>
    ///     The expression being tested.
    /// </summary>
    public required ExpressionNode Operand { get; init; }

    /// <summary>
    ///     The type being tested against.
    /// </summary>
    public required TypeNode TypeOperand { get; init; }

    /// <summary>
    ///     The result type (bool for TypeIs/TypeEqual).
    /// </summary>
    public required TypeNode Type { get; init; }
}
