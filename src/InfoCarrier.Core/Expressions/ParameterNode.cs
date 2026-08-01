// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A lambda/closure parameter. Parameter identity is remapped between client and server by
///     <see cref="Id" /> (requirements §2.3); the server rebuilds one
///     <see cref="System.Linq.Expressions.ParameterExpression" /> per distinct id.
/// </summary>
public sealed record ParameterNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Parameter;

    /// <summary>
    ///     Identity of this parameter within the message, assigned from the reference identity
    ///     of the source <see cref="System.Linq.Expressions.ParameterExpression" />. All
    ///     occurrences of one parameter share an id; distinct parameters never do.
    /// </summary>
    /// <remarks>
    ///     <see cref="Name" /> cannot serve as identity. Unnamed parameters
    ///     (<c>Expression.Parameter(type)</c>) all have a null name and would collapse into a
    ///     single parameter, and two lambdas may legitimately both name a parameter <c>c</c>
    ///     while being unrelated — both cases produce a tree whose body references a parameter
    ///     its lambda does not declare, which EF reports as
    ///     "The LINQ expression '…' could not be translated".
    /// </remarks>
    public required int Id { get; init; }

    /// <summary>
    ///     The parameter name, preserved for diagnostics only — never for identity.
    ///     Null for unnamed parameters.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     The parameter type.
    /// </summary>
    public required TypeNode Type { get; init; }
}
