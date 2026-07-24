// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A reference to a method (not the call itself — see <see cref="MethodCallNode" />).
///     Identified by declaring type + name + generic arguments + parameter-type signature,
///     so the server re-resolves the exact overload against its own type model. Reflection
///     <c>MethodInfo</c> never crosses the wire.
/// </summary>
public sealed record MethodNode
{
    /// <summary>
    ///     The type declaring the method.
    /// </summary>
    public required TypeNode DeclaringType { get; init; }

    /// <summary>
    ///     The method name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Generic type arguments, empty for non-generic methods.
    /// </summary>
    public IReadOnlyList<TypeNode> GenericArguments { get; init; } = [];

    /// <summary>
    ///     The parameter types, used to disambiguate overloads during server re-resolution.
    /// </summary>
    public IReadOnlyList<TypeNode> ParameterTypes { get; init; } = [];

    /// <summary>
    ///     The return type.
    /// </summary>
    public required TypeNode ReturnType { get; init; }
}
