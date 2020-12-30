// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

// ReSharper disable once CheckNamespace
namespace InfoCarrier.Core.Client
{
    using InfoCarrier.Core.Client.Infrastructure.Internal;
    using InfoCarrier.Core.Client.Query.Internal;
    using InfoCarrier.Core.Client.Storage.Internal;
    using InfoCarrier.Core.Client.ValueGeneration;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.EntityFrameworkCore.ValueGeneration;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    ///     InfoCarrier client specific extension methods for <see cref="IServiceCollection" />.
    /// </summary>
    public static class InfoCarrierServiceCollectionExtensions
    {
        /// <summary>
        ///     <para>
        ///         Adds the client related services required by InfoCarrier.Core for Entity Framework
        ///         to an <see cref="IServiceCollection" />. You use this method when using dependency injection
        ///         in your application, such as with ASP.NET. For more information on setting up dependency
        ///         injection, see http://go.microsoft.com/fwlink/?LinkId=526890.
        ///     </para>
        ///     <para>
        ///         You only need to use this functionality when you want Entity Framework to resolve the services it uses
        ///         from an external dependency injection container. If you are not using an external
        ///         dependency injection container, Entity Framework will take care of creating the services it requires.
        ///     </para>
        /// </summary>
        /// <param name="serviceCollection"> The <see cref="IServiceCollection" /> to add services to. </param>
        /// <returns>
        ///     The same service collection so that multiple calls can be chained.
        /// </returns>
        public static IServiceCollection AddEntityFrameworkInfoCarrierClient(this IServiceCollection serviceCollection)
        {
            var builder = new EntityFrameworkServicesBuilder(serviceCollection)
                .TryAdd<LoggingDefinitions, InfoCarrierLoggingDefinitions>()
                .TryAdd<IDatabaseProvider, DatabaseProvider<InfoCarrierOptionsExtension>>()
                .TryAdd<IValueGeneratorSelector, InfoCarrierValueGeneratorSelector>()
                .TryAdd<IDatabase, InfoCarrierDatabase>()
                .TryAdd<IDbContextTransactionManager, InfoCarrierTransactionManager>()
                .TryAdd<IDatabaseCreator, InfoCarrierDatabaseCreator>()
                .TryAdd<IQueryContextFactory, InfoCarrierQueryContextFactory>()
                .TryAdd<IEvaluatableExpressionFilter, InfoCarrierEvaluatableExpressionFilter>()
                .TryAdd<ITypeMappingSource, InfoCarrierTypeMappingSource>();

            builder.TryAddCoreServices();

            return serviceCollection;
        }
    }
}
