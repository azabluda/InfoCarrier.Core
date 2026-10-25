// Licensed under the MIT license. See license.txt file in the project root for license information.

// The document-mapping seam is [Obsolete] as of 10.1.0 and is still registered by default, so
// this provider goes on using it until the major that removes it. Suppressed per FILE, which is
// how EF1001 is handled here and for the same reason: a NEW use elsewhere still warns.
// See docs/versioning.md, "Where a breaking change goes".
#pragma warning disable CS0618 // Type or member is obsolete.

// This provider registers EF's own relational model-building services on a client that has no
// database, and three of them (the row-value factories) are internal. EF Core's own providers
// suppress EF1001 per file for the same reason; see CLAUDE.md.
#pragma warning disable EF1001 // Internal EF Core API usage.

using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update.Internal;
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
            .TryAdd<ITypeMappingSource, Relational.InfoCarrierRelationalTypeMappingSource>()
            .TryAdd<IValueGeneratorSelector, InfoCarrierValueGeneratorSelector>()
            .TryAdd<IDbContextTransactionManager, InfoCarrierTransactionManager>()

            // `EF.Functions.Collate` and friends must survive parameter extraction rather than be
            // evaluated on the client, and EF's core filter does not know the relational host that
            // declares them -- see `InfoCarrierEvaluatableExpressionFilter`.
            .TryAdd<IEvaluatableExpressionFilter, InfoCarrierEvaluatableExpressionFilter>()

            // The client's model has to agree with the backing store's, and one key shape is
            // decided by the caller's own `ToJson()` rather than by the store — see
            // `InfoCarrierKeyDiscoveryConvention`.
            .TryAdd<IProviderConventionSetBuilder, Relational.InfoCarrierRelationalConventionSetBuilder>()

            // A hierarchy with no discriminator is legal on this client, because the server's
            // provider owns the inheritance mapping and the wire names the concrete type — see
            // `InfoCarrierModelValidator`.
            .TryAdd<IModelValidator, InfoCarrierModelValidator>()

            // Deliberately no IQueryableMethodTranslatingExpressionVisitorFactory or
            // IShapedQueryCompilingExpressionVisitorFactory. EF Core uses them only after its
            // standard IDatabase creates a QueryCompilationContext. InfoCarrier's IDatabase
            // captures the expression earlier and sends it to the server (ADR-006).
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
        AddStandardValueMappers(services);

        // The one question this provider asks about how the backing store lays out a nested
        // structure (M9 J5, `docs/architecture.md` §6a D3). `InfoCarrierConventionSetBuilder` and
        // `InfoCarrierDatabase` both take it, and EF resolves both from this same collection once
        // it is built, so a plain registration here reaches them.
        //
        // **Not through `EntityFrameworkServicesBuilder`**, which validates every `TryAdd` against
        // its own list of EF service contracts and refuses anything else: routing it there put
        // *"The database provider attempted to register an implementation of the
        // 'IInfoCarrierDocumentMapping' service"* on **21,991** tests in one run. The value mappers
        // above are registered here for the same reason — a provider's own service is the
        // application's collection's business, not EF's.
        services.TryAddSingleton<Metadata.IInfoCarrierDocumentMapping, Metadata.AnnotationDocumentMapping>();

        // How to recognise EF's relational raw-SQL query roots (#97). One implementation, and it
        // names EF's two types outright; before the relational reference came back it did the same
        // job by reflection. Registered here rather than through EF's builder for the same reason
        // as the line above -- a provider's own service is the application's collection's
        // business, not EF's -- and `TryAdd`, so an application can still put its own in the slot.
        services.TryAddSingleton<Metadata.IInfoCarrierRelationalQueryRoots, Relational.InfoCarrierRelationalQueryRoots>();

        // THE CLIENT'S FACADE DEPENDENCIES, which `Database.SqlQuery<T>` type-tests for before it
        // builds anything. `AddInfoCarrierRelationalClient()` used to add them and there is no
        // opt-in left (R135), so they go in unconditionally. `RemoveAll` first on both, so a
        // repeated call to this method leaves the collection unchanged.
        services.RemoveAll<IRelationalDatabaseFacadeDependencies>();
        services.RemoveAll<IDatabaseFacadeDependencies>();
        services.AddScoped<IRelationalDatabaseFacadeDependencies, Relational.InfoCarrierRelationalFacadeDependencies>();
        services.AddScoped<IDatabaseFacadeDependencies>(
            p => p.GetRequiredService<IRelationalDatabaseFacadeDependencies>());

        // A RELATIONAL MODEL ON THE CLIENT, hand-wired rather than taken wholesale.
        // `EntityFrameworkRelationalServicesBuilder` would bring a connection, a migrator, a SQL
        // generator and a database creator with it, none of which a client without a database can
        // supply. What `GetRelationalModel()` needs is this much and no more, read out of
        // `RelationalModelRuntimeInitializer` rather than guessed.
        //
        // NOT through `EntityFrameworkServicesBuilder`, which validates every `TryAdd` against
        // EF's CORE service list and answers a relational contract with *"This is not a service
        // defined by Entity Framework"* -- 97 `DataAnnotation` tests in one run, before the
        // fixture could build a single model.
        //
        // The SAME instance answers `ITypeMappingSource` and `IRelationalTypeMappingSource`. EF's
        // relational model building casts one to the other and would fail on two objects; more to
        // the point, two sources could disagree about one property, which is the class of defect
        // this repository has paid for before.
        services.TryAddSingleton(
            p => (IRelationalTypeMappingSource)p.GetRequiredService<ITypeMappingSource>());
        services.TryAddSingleton<RelationalTypeMappingSourceDependencies>();
        services.TryAddSingleton<IRelationalAnnotationProvider, RelationalAnnotationProvider>();
        services.TryAddSingleton<RelationalAnnotationProviderDependencies>();
        services.TryAddSingleton<IRowKeyValueFactoryFactory, RowKeyValueFactoryFactory>();
        services.TryAddSingleton<IRowForeignKeyValueFactoryFactory, RowForeignKeyValueFactoryFactory>();
        services.TryAddSingleton<IRowIndexValueFactoryFactory, RowIndexValueFactoryFactory>();
        services.TryAddSingleton<RelationalModelDependencies>();
        services.TryAddSingleton<RelationalModelRuntimeInitializerDependencies>();
        services.RemoveAll<IModelRuntimeInitializer>();
        services.AddSingleton<IModelRuntimeInitializer, RelationalModelRuntimeInitializer>();

        services.TryAddScoped<TypeNodeMapper>();
        services.TryAddScoped<TypeNodeResolver>();
        services.TryAddScoped<IDynamicValueMapper, DynamicValueMapper>();
        services.TryAddScoped<ExpressionToNodeTranslator>();
        services.TryAddScoped<IExpressionSerializer, ExpressionSerializer>();

        return services;
    }

    private static IServiceCollection AddStandardValueMappers(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ValueMapping.IInfoCarrierValueMapper, ValueMapping.IPAddressValueMapper>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ValueMapping.IInfoCarrierValueMapper, ValueMapping.UriValueMapper>());

        return services;
    }
}
