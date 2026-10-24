// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.ValueMapping;

namespace InfoCarrier.Core;

/// <summary>Configures shared deployment facts while preserving the concrete builder type.</summary>
public static class InfoCarrierDeploymentBuilderExtensions
{
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
