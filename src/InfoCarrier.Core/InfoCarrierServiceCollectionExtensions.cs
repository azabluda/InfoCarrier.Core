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
using Microsoft.Extensions.DependencyInjection.Extensions;

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

            // Required of every provider by `EntityFrameworkServicesBuilder.CoreServices`, and
            // deliberately unimplementable here — see `InfoCarrierQueryPipelineFactories`. Both
            // throw with the reason. Registering them replaces EF's generic "no service has been
            // registered" with a sentence that explains ADR-006, and nothing in this provider
            // resolves either one.
            .TryAdd<IQueryableMethodTranslatingExpressionVisitorFactory,
                InfoCarrierQueryableMethodTranslatingExpressionVisitorFactory>()
            .TryAdd<IShapedQueryCompilingExpressionVisitorFactory,
                InfoCarrierShapedQueryCompilingExpressionVisitorFactory>()
            .TryAddCoreServices();

        // Expression serialization pipeline (DI-resolved, no statics).
        //
        // `TryAdd*`, not `Add*`. **Calling this method twice must not change the collection** —
        // `EntityFrameworkServiceCollectionExtensionsTestBase.Repeated_calls_to_add_do_not_modify_collection`
        // asserts it, and it was failing at *Expected 121, Actual 126*: exactly these five
        // registrations, appended a second time. Everything above goes through
        // `EntityFrameworkServicesBuilder`, which is already idempotent; these five bypassed it
        // because they are this provider's own services rather than EF contracts.
        //
        // Duplicate registrations are not harmless. The last one wins for a single resolve, so
        // the visible behaviour is unchanged — but every one of these is `Scoped`, and an
        // `IEnumerable<T>` resolve would yield the service twice, which is exactly how the
        // value-mapper chain of ADR-012 is consumed.
        services.TryAddScoped<TypeNodeMapper>();
        services.TryAddScoped<TypeNodeResolver>();
        services.TryAddScoped<IDynamicValueMapper, DynamicValueMapper>();
        services.TryAddScoped<ExpressionToNodeTranslator>();
        services.TryAddScoped<IExpressionSerializer, ExpressionSerializer>();

        return services;
    }
}
