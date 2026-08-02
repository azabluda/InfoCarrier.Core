// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Evaluates <c>EF.Property</c> on the client side of the projection boundary
///     (<c>docs/projection-split.md</c> §3.4).
/// </summary>
/// <remarks>
///     <para>
///         <c>EF.Property</c> has no runtime implementation — EF replaces it during translation,
///         which this provider does not perform on the client. It used to be refused outright,
///         but a mapped property is readable from the materialized entity through the model's own
///         accessor, which is what EF would have compiled it to anyway.
///     </para>
///     <para>
///         A shadow property is a different matter: its value lives in the change tracker, not on
///         the instance, so it is refused with a message that says which.
///     </para>
/// </remarks>
public static class ClientPropertyReader
{
    /// <summary>
    ///     Reads <paramref name="propertyName" /> from <paramref name="entity" /> using EF
    ///     metadata.
    /// </summary>
    public static T? Read<T>(object? entity, string propertyName, IModel model)
    {
        if (entity is null)
        {
            return default;
        }

        IEntityType entityType = FindEntityType(entity.GetType(), model)
            ?? throw new InvalidOperationException(
                $"EF.Property was applied on the client to '{entity.GetType()}', which is not an "
                    + "entity type in the model.");

        if (entityType.FindProperty(propertyName) is { } property)
        {
            return property.IsShadowProperty()
                ? throw new InvalidOperationException(
                    $"'{entityType.DisplayName()}.{propertyName}' is a shadow property, so its value "
                        + "is held by the change tracker rather than the entity. Reading it on the "
                        + "client side of the projection boundary is not supported "
                        + "(docs/projection-split.md §7).")
                : (T?)property.GetGetter().GetClrValue(entity);
        }

        if (entityType.FindNavigation(propertyName) is { } navigation)
        {
            return (T?)navigation.GetGetter().GetClrValue(entity);
        }

        throw new InvalidOperationException(
            $"'{entityType.DisplayName()}' has no property or navigation named '{propertyName}'.");
    }

    // A proxy or a derived instance still has to resolve to the mapped type.
    private static IEntityType? FindEntityType(Type type, IModel model)
    {
        for (Type? candidate = type; candidate is not null; candidate = candidate.BaseType)
        {
            if (model.FindEntityType(candidate) is { } entityType)
            {
                return entityType;
            }
        }

        return null;
    }
}
