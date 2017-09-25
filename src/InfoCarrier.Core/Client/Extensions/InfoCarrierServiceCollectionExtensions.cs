namespace InfoCarrier.Core.Client
{
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.EntityFrameworkCore.ValueGeneration;
    using Microsoft.Extensions.DependencyInjection;
    using Query.ExpressionVisitors.Internal;
    using Query.Internal;
    using Remotion.Linq.Parsing.ExpressionVisitors.TreeEvaluation;
    using Storage.Internal;

    public static class InfoCarrierServiceCollectionExtensions
    {
        public static IServiceCollection AddEntityFrameworkInfoCarrierBackend(this IServiceCollection serviceCollection)
        {
            var builder = new EntityFrameworkServicesBuilder(serviceCollection)
                .TryAdd<IQueryCompiler, InfoCarrierQueryCompiler>()
                .TryAdd<IDatabaseProvider, DatabaseProvider<InfoCarrierOptionsExtension>>()
                .TryAdd<IValueGeneratorSelector, RelationalValueGeneratorSelector>()
                .TryAdd<IDatabase>(p => p.GetService<IInfoCarrierDatabase>())
                .TryAdd<IDbContextTransactionManager, InfoCarrierTransactionManager>()
                .TryAdd<IDatabaseCreator, InfoCarrierDatabaseCreator>()
                .TryAdd<IQueryContextFactory, InfoCarrierQueryContextFactory>()
                .TryAdd<IEntityQueryModelVisitorFactory, InfoCarrierQueryModelVisitorFactory>()
                .TryAdd<IEntityQueryableExpressionVisitorFactory, InfoCarrierEntityQueryableExpressionVisitorFactory>()
                .TryAdd<IEvaluatableExpressionFilter, InfoCarrierEvaluatableExpressionFilter>()
                .TryAddProviderSpecificServices(b => b
                    .TryAddScoped<IInfoCarrierDatabase, InfoCarrierDatabase>()
                    .TryAddScoped<IMaterializerFactory, MaterializerFactory>());

            builder.TryAddCoreServices();

            return serviceCollection;
        }
    }
}
