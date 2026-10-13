// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Runtime.CompilerServices;
using InfoCarrier.Core.ValueMapping;

namespace InfoCarrier.Core;

/// <summary>
///     Configures deployment facts that must agree between an InfoCarrier client and server.
/// </summary>
public class InfoCarrierDeploymentBuilder
{
    private readonly List<Type> _anonymousShapeTypes = [];
    private readonly List<Type> _allowedTypes = [];
    private readonly List<IInfoCarrierValueMapper> _valueMappers = [];
    private bool _allowArbitrarySqlExecution;

    /// <summary>
    ///     Sets whether this deployment permits arbitrary SQL execution.
    /// </summary>
    /// <param name="allowed">Whether to permit arbitrary SQL execution.</param>
    /// <returns>The same builder, so calls chain.</returns>
    public virtual InfoCarrierDeploymentBuilder AllowArbitrarySqlExecution(bool allowed)
    {
        _allowArbitrarySqlExecution = allowed;
        return this;
    }

    /// <summary>
    ///     Adds anonymous structures that the application declares on both deployment roles.
    /// </summary>
    /// <param name="shapes">Closed compiler-generated anonymous objects from trusted configuration.</param>
    /// <returns>The same builder, so calls chain.</returns>
    public InfoCarrierDeploymentBuilder AddAnonymousShapes(params object[] shapes)
    {
        _anonymousShapeTypes.AddRange(ValidateAnonymousPrototypes(shapes, nameof(shapes)));
        return this;
    }

    /// <summary>
    ///     Adds named types that wire payloads may name beyond the types implied by the model.
    /// </summary>
    /// <param name="types">The types to admit.</param>
    /// <returns>The same builder, so calls chain.</returns>
    public InfoCarrierDeploymentBuilder AddAllowedTypes(params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(types);
        if (types.Any(static type => type is null))
        {
            throw new ArgumentException("Allowed types cannot contain null.", nameof(types));
        }

        _allowedTypes.AddRange(types);
        return this;
    }

    /// <summary>
    ///     Adds custom value mappers in first-claim-wins order.
    /// </summary>
    /// <param name="mappers">The mapper instances to use on this deployment role.</param>
    /// <returns>The same builder, so calls chain.</returns>
    public InfoCarrierDeploymentBuilder AddValueMappers(params IInfoCarrierValueMapper[] mappers)
    {
        ArgumentNullException.ThrowIfNull(mappers);
        if (mappers.Any(static mapper => mapper is null))
        {
            throw new ArgumentException("Value mappers cannot contain null.", nameof(mappers));
        }

        _valueMappers.AddRange(mappers);
        return this;
    }

    internal InfoCarrierDeploymentSnapshot CreateSnapshot()
        => new(
            Array.AsReadOnly(_anonymousShapeTypes.ToArray()),
            Array.AsReadOnly(_allowedTypes.ToArray()),
            Array.AsReadOnly(_valueMappers.ToArray()),
            _allowArbitrarySqlExecution);

    internal static Type[] ValidateAnonymousPrototypes(object[] shapes, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        Type[] types = shapes.Select(shape => shape?.GetType()
            ?? throw new ArgumentException("Anonymous-shape prototypes cannot be null.", parameterName)).ToArray();
        if (types.Any(static type => !IsOriginalAnonymousType(type) || type.ContainsGenericParameters))
        {
            throw new ArgumentException(
                "Use anonymous objects as shape prototypes; register named records with AddAllowedTypes.",
                parameterName);
        }

        return types;
    }

    private static bool IsOriginalAnonymousType(Type type)
        => type.IsSealed && !type.IsVisible
            && type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal)
            && type.IsDefined(typeof(CompilerGeneratedAttribute), false);
}

internal sealed record InfoCarrierDeploymentSnapshot(
    IReadOnlyList<Type> AnonymousShapeTypes,
    IReadOnlyList<Type> AllowedTypes,
    IReadOnlyList<IInfoCarrierValueMapper> ValueMappers,
    bool AllowArbitrarySqlExecution);
