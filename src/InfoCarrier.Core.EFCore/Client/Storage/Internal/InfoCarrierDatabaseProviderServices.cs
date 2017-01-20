namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
    using System.Reflection;
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.EntityFrameworkCore.ValueGeneration;
    using Query;
    using Query.ExpressionVisitors.Internal;
    using Query.Internal;
    using ValueGeneration.Internal;

    public class InfoCarrierDatabaseProviderServices : DatabaseProviderServices
    {
        public InfoCarrierDatabaseProviderServices(IServiceProvider services)
            : base(services)
        {
        }

        public override string InvariantName => this.GetType().GetTypeInfo().Assembly.GetName().Name;

        public override IDatabase Database => this.GetService<IInfoCarrierDatabase>();

        public override IDbContextTransactionManager TransactionManager => this.GetService<InfoCarrierTransactionManager>();

        public override IQueryContextFactory QueryContextFactory => this.GetService<InfoCarrierQueryContextFactory>();

        public override IDatabaseCreator Creator => this.GetService<InfoCarrierDatabaseCreator>();

        public override IValueGeneratorSelector ValueGeneratorSelector => this.GetService<InfoCarrierValueGeneratorSelector>();

        public override IResultOperatorHandler ResultOperatorHandler => this.GetService<InfoCarrierResultOperatorHandler>();

        public override IModelSource ModelSource => this.GetService<InfoCarrierModelSource>();

        public override IValueGeneratorCache ValueGeneratorCache => this.GetService<InfoCarrierValueGeneratorCache>();

        public override IEntityQueryableExpressionVisitorFactory EntityQueryableExpressionVisitorFactory => this.GetService<InfoCarrierEntityQueryableExpressionVisitorFactory>();

        public override IEntityQueryModelVisitorFactory EntityQueryModelVisitorFactory => this.GetService<InfoCarrierQueryModelVisitorFactory>();

        public override IQueryCompilationContextFactory QueryCompilationContextFactory => this.GetService<InfoCarrierQueryCompilationContextFactory>();
    }
}
