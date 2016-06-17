namespace InfoCarrier.Core.Client
{
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Query.ExpressionVisitors.Internal;
    using Query.Internal;
    using Storage.Internal;
    using ValueGeneration.Internal;

    public static class InfoCarrierServiceCollectionExtensions
    {
        public static IServiceCollection AddEntityFrameworkInfoCarrierBackend(this IServiceCollection services)
        {
            services.AddEntityFramework();

            services.TryAddEnumerable(ServiceDescriptor
                .Singleton<IDatabaseProvider, DatabaseProvider<InfoCarrierDatabaseProviderServices, InfoCarrierOptionsExtension>>());

            services.TryAdd(new ServiceCollection()
                .AddSingleton<InfoCarrierValueGeneratorCache>()
                //.AddSingleton<IInfoCarrierStoreSource, InfoCarrierStoreSource>()
                //.AddSingleton<IInfoCarrierTableFactory, InfoCarrierTableFactory>()
                .AddSingleton<InfoCarrierModelSource>()
                //.AddScoped<InfoCarrierValueGeneratorSelector>()
                .AddScoped<InfoCarrierDatabaseProviderServices>()
                .AddScoped<IInfoCarrierDatabase, InfoCarrierDatabase>()
                .AddScoped<InfoCarrierTransactionManager>()
                .AddScoped<InfoCarrierDatabaseCreator>()
                .AddQuery());

            return services;
        }

        private static IServiceCollection AddQuery(this IServiceCollection serviceCollection)
            => serviceCollection
                //.AddScoped<IMaterializerFactory, MaterializerFactory>()
                .AddScoped<InfoCarrierQueryContextFactory>()
                .AddScoped<InfoCarrierQueryModelVisitorFactory>()
                .AddScoped<InfoCarrierEntityQueryableExpressionVisitorFactory>();
    }
}
