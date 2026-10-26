// Licensed under the MIT license. See license.txt file in the project root for license information.
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Maps non-primitive constant values to and from <see cref="DynamicValueNode" /> graphs
///     (expression-serialization §3.1 EF-metadata-driven). Implemented by
///     <c>DynamicValueMapper</c> (B5); declared as a seam so <see cref="ExpressionToNodeTranslator" />
///     and the reverse translator stay decoupled from value-mapping internals.
/// </summary>
public interface IDynamicValueMapper
{
    /// <summary>The entity materializer for the current response; callers restore nested hooks.</summary>
    Func<DynamicValueNode, object?>? EntityMaterializer { get; set; }

    /// <summary>The model-aware type writer used by this mapper.</summary>
    TypeNodeMapper TypeMapper { get; }

    /// <summary>Starts a new message reference scope.</summary>
    void ResetReferenceScope();

    /// <summary>Maps a row using the actual context's tracking and navigation state.</summary>
    DynamicValueNode ToRowValue(object? value, Type type,
        Func<object, INavigationBase, bool> isNavigationLoaded, Func<object, bool> isTracked,
        Func<object, IProperty, object?> readShadowValue,
        Func<object, ISkipNavigation, bool, IEnumerable<object>> readJoinEntities,
        Func<object, IEntityType?> findEntityType);

    /// <summary>Maps a result row with model identity for projected owned entities.</summary>
    DynamicValueNode ToRowValue(object? value, Type type,
        Func<object, INavigationBase, bool> isNavigationLoaded, Func<object, bool> isTracked,
        Func<object, IProperty, object?> readShadowValue,
        Func<object, ISkipNavigation, bool, IEnumerable<object>> readJoinEntities,
        Func<object, IEntityType?> findEntityType, Query.ProjectionShape? shape);

    /// <summary>Maps a complex property using its model metadata.</summary>
    DynamicValueNode ToComplexValue(object? value, IComplexProperty complexProperty);

    /// <summary>Reads a property using its declared type.</summary>
    object? FromPropertyValue(DynamicPropertyValue property, Type declaredType);

    /// <summary>Registers an entity in the current message reference scope.</summary>
    void RegisterMaterialized(int id, object? value);

    /// <summary>Restores a non-entity shape.</summary>
    object? FromShape(DynamicValueNode node);

    /// <summary>
    ///     Maps a live value to its shape-based / entity-keyed node graph.
    /// </summary>
    DynamicValueNode ToDynamicValue(object? value, Type type);

    /// <summary>
    ///     Materializes a live value from its node graph.
    /// </summary>
    object? FromDynamicValue(DynamicValueNode node);
}
