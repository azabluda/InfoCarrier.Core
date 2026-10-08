// Licensed under the MIT license. See license.txt file in the project root for license information.


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

    /// <summary>Ordered anonymous data properties; null for ordinary named types.</summary>
    public IReadOnlyList<string>? ShapeMembers { get; init; }

    /// <summary>Element descriptor for an array containing anonymous shapes.</summary>
    public TypeNode? ArrayElement { get; init; }

    /// <summary>Rank of an array with an element descriptor; zero otherwise.</summary>
    public int ArrayRank { get; init; }

    // Length prefixes keep punctuation in model names from merging distinct descriptors.
    internal string CacheIdentity()
    {
        var text = new System.Text.StringBuilder();
        Append(this);
        return text.ToString();

        void Value(string? value) => text.Append(value?.Length ?? -1).Append(':').Append(value);
        void Append(TypeNode node)
        {
            Value(node.Name);
            Value(node.EntityTypeName);
            text.Append('/').Append(node.ArrayRank).Append('/').Append(node.ShapeMembers?.Count ?? -1).Append('/');
            if (node.ShapeMembers is not null)
            {
                foreach (string member in node.ShapeMembers) Value(member);
            }

            text.Append('/').Append(node.GenericArguments.Count).Append('/');
            foreach (TypeNode argument in node.GenericArguments) Append(argument);
            text.Append(node.ArrayElement is null ? '0' : '1');
            if (node.ArrayElement is not null) Append(node.ArrayElement);
        }
    }

    /// <inheritdoc />
    public override string ToString()
        => ArrayElement is not null ? $"{ArrayElement}[{new string(',', ArrayRank - 1)}]"
            : ShapeMembers is not null ? $"{Name}{{{string.Join(",", ShapeMembers)}}}<{string.Join(",", GenericArguments)}>"
            : GenericArguments.Count == 0
            ? (EntityTypeName is null ? Name : $"{Name} [{EntityTypeName}]")
            : $"{Name}<{string.Join(",", GenericArguments)}>{(EntityTypeName is null ? string.Empty : $" [{EntityTypeName}]")}";
}
