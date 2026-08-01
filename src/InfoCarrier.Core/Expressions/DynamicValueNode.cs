// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A shape-based value graph for non-primitive constants (aqua §2.3 <c>DynamicObject</c>,
///     greenfield per ADR-001/ADR-008). Carries anonymous types, client DTOs, records, value
///     tuples, collections, and entity references across the client/server type boundary.
/// </summary>
/// <remarks>
///     Client-materialized values use shape identity (collisions acceptable — research-findings
///     §7). <strong>Entity</strong> values are carried by EF entity-type name + key values via
///     <see cref="EntityKey" />, never by shape, so entities never merge on shape alone.
/// </remarks>
public sealed record DynamicValueNode
{
    /// <summary>
    ///     The runtime type of the value.
    /// </summary>
    public required TypeNode Type { get; init; }

    /// <summary>
    ///     For entity values: the EF entity-type name + key property values identifying the
    ///     entity (research-findings §7). Null for non-entity values.
    /// </summary>
    public EntityKeyNode? EntityKey { get; init; }

    /// <summary>
    ///     The value's properties, in MetadataToken order (aqua shape identity). Each entry
    ///     maps a member name to a nested value (primitive, or another <see cref="DynamicValueNode" />
    ///     via <see cref="DynamicPropertyValue.Value" />). Empty for collection/scalar shapes.
    /// </summary>
    public IReadOnlyList<DynamicPropertyValue> Properties { get; init; } = [];

    /// <summary>
    ///     For collection/array values: the element values, in order.
    /// </summary>
    public IReadOnlyList<DynamicValueNode>? Items { get; init; }

    /// <summary>
    ///     For scalar values: the primitive itself. Set when a primitive appears where a
    ///     dynamic value is required — most commonly as an element of a collection constant
    ///     (<c>List&lt;string&gt;</c> in a <c>Contains</c> closure). Null for entity,
    ///     collection, and object shapes.
    /// </summary>
    /// <remarks>
    ///     Without this slot a primitive element falls through to the object-shape branch,
    ///     which is wrong in two ways: <see cref="string" /> maps its <c>Length</c> property
    ///     and then fails to rehydrate at all, while <see cref="int" /> maps an empty property
    ///     set and rehydrates <em>silently</em> as <c>0</c>.
    /// </remarks>
    public object? PrimitiveValue { get; init; }
}

/// <summary>
///     One member of a <see cref="DynamicValueNode" /> shape: a name plus either a primitive
///     value or a nested dynamic value.
/// </summary>
public sealed record DynamicPropertyValue
{
    /// <summary>
    ///     The member name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The primitive value; null when <see cref="Value" /> carries a nested dynamic value.
    /// </summary>
    public object? PrimitiveValue { get; init; }

    /// <summary>
    ///     The nested dynamic value; null when <see cref="PrimitiveValue" /> carries the value.
    /// </summary>
    public DynamicValueNode? Value { get; init; }
}

/// <summary>
///     An entity reference by EF identity: entity-type name plus key property values
///     (research-findings §7 — entities are identified by model identity, never by shape).
/// </summary>
public sealed record EntityKeyNode
{
    /// <summary>
    ///     The EF entity-type name (the shared-type discriminator).
    /// </summary>
    public required string EntityTypeName { get; init; }

    /// <summary>
    ///     The key property values, in key order.
    /// </summary>
    public required IReadOnlyList<object?> KeyValues { get; init; }
}
