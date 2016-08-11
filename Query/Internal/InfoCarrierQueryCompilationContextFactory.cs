namespace InfoCarrier.Core.Client.Query.Internal
{
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.Extensions.Logging;

    public class InfoCarrierQueryCompilationContextFactory : QueryCompilationContextFactory
    {
        public InfoCarrierQueryCompilationContextFactory(
            IModel model,
            ILogger<QueryCompilationContextFactory> logger,
            IEntityQueryModelVisitorFactory entityQueryModelVisitorFactory,
            IRequiresMaterializationExpressionVisitorFactory requiresMaterializationExpressionVisitorFactory,
            ICurrentDbContext currentContext)
            : base(
                model,
                logger,
                entityQueryModelVisitorFactory,
                requiresMaterializationExpressionVisitorFactory,
                currentContext)
        {
        }

        public override QueryCompilationContext Create(bool async)
        {
            return new InfoCarrierQueryCompilationContext(
                this.Model,
                this.Logger,
                this.EntityQueryModelVisitorFactory,
                this.RequiresMaterializationExpressionVisitorFactory,
                new LinqOperatorProvider(),
                this.ContextType,
                this.TrackQueryResults);
        }
    }
}