// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfoCarrier.Core;

/// <summary>
///     Service registration for the InfoCarrier client provider (DI-first, requirements §4.2).
/// </summary>
public static class InfoCarrierServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the InfoCarrier client provider's EF Core services. Called by
    ///     <see cref="InfoCarrierOptionsExtension.ApplyServices" />.
    /// </summary>
    public static IServiceCollection AddEntityFrameworkInfoCarrier(this IServiceCollection services)
    {
        // The provider's IDatabase — the raw-capture query entry point (ADR-006).
        services.TryAddScoped<IDatabase, InfoCarrierDatabase>();

        // Expression serialization pipeline (DI-resolved, no statics).
        services.TryAddScoped<TypeNodeMapper>();
        services.TryAddScoped<TypeNodeResolver>();
        services.TryAddScoped<IDynamicValueMapper, DynamicValueMapper>();
        services.TryAddScoped<ExpressionToNodeTranslator>();
        services.TryAddScoped<IExpressionSerializer, ExpressionSerializer>();

        return services;
    }
}
