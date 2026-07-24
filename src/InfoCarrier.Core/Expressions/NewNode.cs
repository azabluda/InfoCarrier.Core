// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     An object construction (<c>new T(args)</c>). The constructor is identified by declaring
///     type + parameter-type signature so the server re-resolves the exact overload (used for
///     anonymous types, records, DTOs, value tuples in projections).
/// </summary>
public sealed record NewNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.New;

    /// <summary>
    ///     The type being constructed.
    /// </summary>
    public required TypeNode Type { get; init; }

    /// <summary>
    ///     The constructor parameter types, for overload resolution.
    /// </summary>
    public IReadOnlyList<TypeNode> ConstructorParameterTypes { get; init; } = [];

    /// <summary>
    ///     The constructor arguments, in order.
    /// </summary>
    public IReadOnlyList<ExpressionNode> Arguments { get; init; } = [];

    /// <summary>
    ///     The member names the arguments bind to (anonymous-type / record positional members),
    ///     parallel to <see cref="Arguments" />. Null when the constructor has no member binding
    ///     (plain <c>new</c>).
    /// </summary>
    public IReadOnlyList<string>? Members { get; init; }
}
