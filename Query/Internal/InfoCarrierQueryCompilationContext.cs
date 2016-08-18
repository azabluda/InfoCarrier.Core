namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.Extensions.Logging;
    using Remotion.Linq;

    public class InfoCarrierQueryCompilationContext : QueryCompilationContext
    {
        public InfoCarrierQueryCompilationContext(
            IModel model,
            ILogger logger,
            IEntityQueryModelVisitorFactory entityQueryModelVisitorFactory,
            IRequiresMaterializationExpressionVisitorFactory requiresMaterializationExpressionVisitorFactory,
            bool async,
            Type contextType,
            bool trackQueryResults)
            : base(
                model,
                logger,
                entityQueryModelVisitorFactory,
                requiresMaterializationExpressionVisitorFactory,
                new LinqOperatorProvider(),
                contextType,
                trackQueryResults)
        {
            this.Async = async;
        }

        internal bool Async { get; }

        public override void FindQuerySourcesRequiringMaterialization(EntityQueryModelVisitor queryModelVisitor, QueryModel queryModel)
        {
            // No tricky manipulations here. We just want to pass the original query to Remote.Linq
            // who (hopefully) knows how to deal with sources requiring materialization.
        }
    }
}
