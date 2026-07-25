// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

/// <summary>
///     Service registration for the InfoCarrier client provider (DI-first, requirements §4.2).
/// </summary>
public static class InfoCarrierServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the InfoCarrier client provider's EF Core services. Called by
    ///     <see cref="InfoCarrierOptionsExtension.ApplyServices" />. Uses
    ///     <see cref="EntityFrameworkServicesBuilder" /> so EF's core services are registered
    ///     alongside the provider-specific ones (the <see cref="IDatabase" /> raw-capture entry
    ///     point, ADR-006).
    /// </summary>
    public static IServiceCollection AddEntityFrameworkInfoCarrier(this IServiceCollection services)
    {
        var builder = new EntityFrameworkServicesBuilder(services)
            .TryAdd<IDatabaseProvider, DatabaseProvider<InfoCarrierOptionsExtension>>()
            .TryAdd<LoggingDefinitions, InfoCarrierLoggingDefinitions>()
            .TryAdd<IDatabase, InfoCarrierDatabase>()
            .TryAdd<IQueryContextFactory, InfoCarrierQueryContextFactory>()
            .TryAdd<ITypeMappingSource, InfoCarrierTypeMappingSource>()
            .TryAdd<IValueGeneratorSelector, InfoCarrierValueGeneratorSelector>()
            .TryAdd<IDbContextTransactionManager, InfoCarrierTransactionManager>()
            .TryAdd<IDatabaseCreator, InfoCarrierDatabaseCreator>()
            .TryAddCoreServices();

        // Expression serialization pipeline (DI-resolved, no statics).
        services.AddScoped<TypeNodeMapper>();
        services.AddScoped<TypeNodeResolver>();
        services.AddScoped<IDynamicValueMapper, DynamicValueMapper>();
        services.AddScoped<ExpressionToNodeTranslator>();
        services.AddScoped<IExpressionSerializer, ExpressionSerializer>();

        return services;
    }
}
