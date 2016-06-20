namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
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
            if (async)
            {
                throw new NotImplementedException();
            }

            return new QueryCompilationContext(
                this.Model,
                this.Logger,
                this.EntityQueryModelVisitorFactory,
                this.RequiresMaterializationExpressionVisitorFactory,
                new InfoCarrierLinqOperatorProvider(),
                this.ContextType,
                this.TrackQueryResults);
        }
    }
}