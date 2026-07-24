// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json.Serialization;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Assembly-free type identity on the wire (aqua §2.3 shape, hardened per
///     research-findings §7). Carries the CLR full name + generic arguments — no
///     assembly/version travels. For entity-typed values, <see cref="EntityTypeName" />
///     additionally carries the EF entity-type name (the distinguishing key for
///     shared-type entities) so entities never merge on shape alone.
/// </summary>
public sealed record TypeNode
{
    /// <summary>
    ///     The CLR full name (e.g. <c>System.String</c>, <c>Northwind.Order</c>). For generic
    ///     types this is the generic type definition's full name; arguments are in
    ///     <see cref="GenericArguments" />.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Generic argument type identities, empty for non-generic types.
    /// </summary>
    public IReadOnlyList<TypeNode> GenericArguments { get; init; } = [];

    /// <summary>
    ///     The EF entity-type name when this type is an entity in the model (research-findings
    ///     §3/§7). Null for non-entity types. This is the shared-type discriminator.
    /// </summary>
    public string? EntityTypeName { get; init; }

    /// <inheritdoc />
    public override string ToString()
        => GenericArguments.Count == 0
            ? (EntityTypeName is null ? Name : $"{Name} [{EntityTypeName}]")
            : $"{Name}<{string.Join(",", GenericArguments)}>";
}
