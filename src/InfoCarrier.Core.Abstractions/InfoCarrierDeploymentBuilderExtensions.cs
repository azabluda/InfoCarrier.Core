// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.ValueMapping;

namespace InfoCarrier.Core;

/// <summary>Configures shared deployment facts while preserving the concrete builder type.</summary>
public static class InfoCarrierDeploymentBuilderExtensions
{
    /// <summary>Applies an ordinary shared configuration method while preserving the concrete builder type.</summary>
    /// <remarks>
    /// The callback runs synchronously, exactly once, at this position in the configuration chain.
    /// It receives this builder; snapshot creation and registration identity remain unchanged.
    /// Existing ordering rules apply: mapper additions retain first-claim-wins order.
    /// Sharing a method does not prove agreement between separately deployed applications.
    /// </remarks>
    /// <param name="builder">The existing builder to configure.</param>
    /// <param name="configure">The shared configuration to apply. Exceptions propagate to the caller.</param>
    /// <returns>The same concrete builder.</returns>
    /// <exception cref="ArgumentNullException">The builder or callback is null.</exception>
    public static TBuilder ConfigureShared<TBuilder>(
        this TBuilder builder, Action<InfoCarrierDeploymentBuilder> configure)
        where TBuilder : InfoCarrierDeploymentBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        configure(builder);
        return builder;
    }

    /// <summary>Sets whether this deployment permits arbitrary SQL execution.</summary>
    /// <returns>The same concrete builder.</returns>
    public static TBuilder AllowArbitrarySqlExecution<TBuilder>(this TBuilder builder, bool allowed)
        where TBuilder : InfoCarrierDeploymentBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.SetArbitrarySqlExecution(allowed);
        return builder;
    }

    /// <summary>Adds anonymous structures declared on both deployment roles.</summary>
    /// <returns>The same concrete builder.</returns>
    public static TBuilder AddAnonymousShapes<TBuilder>(this TBuilder builder, params object[] shapes)
        where TBuilder : InfoCarrierDeploymentBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddAnonymousShapesCore(shapes);
        return builder;
    }

    /// <summary>Adds named types beyond those implied by the model.</summary>
    /// <returns>The same concrete builder.</returns>
    public static TBuilder AddAllowedTypes<TBuilder>(this TBuilder builder, params Type[] types)
        where TBuilder : InfoCarrierDeploymentBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddAllowedTypesCore(types);
        return builder;
    }

    /// <summary>Adds custom value mappers in first-claim-wins order.</summary>
    /// <returns>The same concrete builder.</returns>
    public static TBuilder AddValueMappers<TBuilder>(this TBuilder builder, params IInfoCarrierValueMapper[] mappers)
        where TBuilder : InfoCarrierDeploymentBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddValueMappersCore(mappers);
        return builder;
    }
}
