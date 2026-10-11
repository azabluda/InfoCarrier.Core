// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.ValueMapping;

namespace InfoCarrier.Core;

/// <summary>
///     Configures deployment facts that must agree between an InfoCarrier client and server.
/// </summary>
/// <remarks>
///     Create one application method that receives this type, then pass that method to both
///     role-specific deployment entry points. Server registration remains the admission boundary.
/// </remarks>
public sealed class InfoCarrierDeploymentOptions
{
    private readonly List<Type> _anonymousShapeTypes = [];
    private readonly List<Type> _allowedTypes = [];
    private readonly List<IInfoCarrierValueMapper> _valueMappers = [];

    internal IReadOnlyList<Type> AnonymousShapeTypes => _anonymousShapeTypes;

    internal IReadOnlyList<Type> AllowedTypes => _allowedTypes;

    internal IReadOnlyList<IInfoCarrierValueMapper> ValueMappers => _valueMappers;

    /// <summary>
    ///     Gets or sets whether this deployment permits arbitrary SQL execution.
    /// </summary>
    /// <remarks>
    ///     The server setting grants the permission. The matching client setting only permits
    ///     application queries to send SQL.
    /// </remarks>
    public bool AllowArbitrarySqlExecution { get; set; }

    /// <summary>
    ///     Adds anonymous structures that the application declares on both deployment roles.
    /// </summary>
    /// <param name="shapes">Closed compiler-generated anonymous objects from trusted configuration.</param>
    public void AddAnonymousShapes(params object[] shapes)
    {
        Type[] types = AnonymousShapeSet.ValidatePrototypes(shapes, nameof(shapes));
        _anonymousShapeTypes.AddRange(types);
    }

    /// <summary>
    ///     Adds named types that wire payloads may name beyond the types implied by the model.
    /// </summary>
    /// <param name="types">The types to admit.</param>
    public void AddAllowedTypes(params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(types);
        if (types.Any(static type => type is null))
        {
            throw new ArgumentException("Allowed types cannot contain null.", nameof(types));
        }

        _allowedTypes.AddRange(types);
    }

    /// <summary>
    ///     Adds custom value mappers in first-claim-wins order.
    /// </summary>
    /// <param name="mappers">The mapper instances to use on this deployment role.</param>
    public void AddValueMappers(params IInfoCarrierValueMapper[] mappers)
    {
        ArgumentNullException.ThrowIfNull(mappers);
        if (mappers.Any(static mapper => mapper is null))
        {
            throw new ArgumentException("Value mappers cannot contain null.", nameof(mappers));
        }

        _valueMappers.AddRange(mappers);
    }
}
