// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
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

            // `EF.Functions.Collate` and friends must survive parameter extraction rather than be
            // evaluated on the client, and EF's core filter does not know the relational host that
            // declares them -- see `InfoCarrierEvaluatableExpressionFilter`.
            .TryAdd<IEvaluatableExpressionFilter, InfoCarrierEvaluatableExpressionFilter>()

            // The client's model has to agree with the backing store's, and one key shape is
            // decided by the caller's own `ToJson()` rather than by the store — see
            // `InfoCarrierKeyDiscoveryConvention`.
            .TryAdd<IProviderConventionSetBuilder, InfoCarrierConventionSetBuilder>()

            // A hierarchy with no discriminator is legal on this client, because the server's
            // provider owns the inheritance mapping and the wire names the concrete type — see
            // `InfoCarrierModelValidator`.
            .TryAdd<IModelValidator, InfoCarrierModelValidator>()

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
        services.AddInfoCarrierStandardValueMappers();

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

        services.TryAddScoped<TypeNodeMapper>();
        services.TryAddScoped<TypeNodeResolver>();
        services.TryAddScoped<IDynamicValueMapper, DynamicValueMapper>();
        services.TryAddScoped<ExpressionToNodeTranslator>();
        services.TryAddScoped<IExpressionSerializer, ExpressionSerializer>();

        return services;
    }

    /// <summary>
    ///     Registers the value mappers this provider ships for BCL types the wire cannot walk
    ///     (ADR-012, amended 2026-08-11).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Called for you by <see cref="AddEntityFrameworkInfoCarrier" />, which is the
    ///         <em>client</em> half. **A server has to call it too**, and that is the whole reason
    ///         it is public: a value mapped on one side only is worse than one mapped on neither,
    ///         because the two models then disagree about what a wire value means. The server's
    ///         service collection is the application's to build, so this is the one line it needs.
    ///     </para>
    ///     <para>
    ///         <see cref="System.Net.IPAddress" /> and <see cref="Uri" /> only. Both are BCL types
    ///         whose members throw for perfectly ordinary instances — <c>ScopeId</c> for an IPv4
    ///         address, <c>AbsolutePath</c> for a relative URI — so an application that stores one
    ///         has opted into nothing and should not have to discover this seam. A **geometry** is
    ///         deliberately absent: shipping it would put a NetTopologySuite dependency in this
    ///         package for a type most callers never use, and an application that wants one
    ///         registers its own mapper here alongside these.
    ///     </para>
    ///     <para>
    ///         <c>TryAddEnumerable</c>, so calling this twice does not put a mapper in the chain
    ///         twice — the chain is consumed as an <c>IEnumerable&lt;T&gt;</c>, where a duplicate
    ///         registration is visible.
    ///     </para>
    /// </remarks>
    /// <param name="services">The collection to register in.</param>
    /// <returns>The same collection, so calls chain.</returns>
    public static IServiceCollection AddInfoCarrierStandardValueMappers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ValueMapping.IInfoCarrierValueMapper, ValueMapping.IPAddressValueMapper>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ValueMapping.IInfoCarrierValueMapper, ValueMapping.UriValueMapper>());

        return services;
    }
}
