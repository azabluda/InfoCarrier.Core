namespace InfoCarrier.Core.Client.Query.Internal
{
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Query.Internal;

    public class InfoCarrierQueryModelVisitorFactory : EntityQueryModelVisitorFactory
    {
        public InfoCarrierQueryModelVisitorFactory(
            IQueryOptimizer queryOptimizer,
            INavigationRewritingExpressionVisitorFactory navigationRewritingExpressionVisitorFactory,
            ISubQueryMemberPushDownExpressionVisitor subQueryMemberPushDownExpressionVisitor,
            IQuerySourceTracingExpressionVisitorFactory querySourceTracingExpressionVisitorFactory,
            IEntityResultFindingExpressionVisitorFactory entityResultFindingExpressionVisitorFactory,
            ITaskBlockingExpressionVisitor taskBlockingExpressionVisitor,
            IMemberAccessBindingExpressionVisitorFactory memberAccessBindingExpressionVisitorFactory,
            IOrderingExpressionVisitorFactory orderingExpressionVisitorFactory,
            IProjectionExpressionVisitorFactory projectionExpressionVisitorFactory,
            IEntityQueryableExpressionVisitorFactory entityQueryableExpressionVisitorFactory,
            IQueryAnnotationExtractor queryAnnotationExtractor,
            IResultOperatorHandler resultOperatorHandler,
            IEntityMaterializerSource entityMaterializerSource,
            IExpressionPrinter expressionPrinter)
            : base(
                queryOptimizer,
                navigationRewritingExpressionVisitorFactory,
                subQueryMemberPushDownExpressionVisitor,
                querySourceTracingExpressionVisitorFactory,
                entityResultFindingExpressionVisitorFactory,
                taskBlockingExpressionVisitor,
                memberAccessBindingExpressionVisitorFactory,
                orderingExpressionVisitorFactory,
                projectionExpressionVisitorFactory,
                entityQueryableExpressionVisitorFactory,
                queryAnnotationExtractor,
                resultOperatorHandler,
                entityMaterializerSource,
                expressionPrinter)
        {
        }

        public override EntityQueryModelVisitor Create(
                QueryCompilationContext queryCompilationContext, EntityQueryModelVisitor parentEntityQueryModelVisitor)
            => new InfoCarrierQueryModelVisitor(
                this.QueryOptimizer,
                this.NavigationRewritingExpressionVisitorFactory,
                this.SubQueryMemberPushDownExpressionVisitor,
                this.QuerySourceTracingExpressionVisitorFactory,
                this.EntityResultFindingExpressionVisitorFactory,
                this.TaskBlockingExpressionVisitor,
                this.MemberAccessBindingExpressionVisitorFactory,
                this.OrderingExpressionVisitorFactory,
                this.ProjectionExpressionVisitorFactory,
                this.EntityQueryableExpressionVisitorFactory,
                this.QueryAnnotationExtractor,
                this.ResultOperatorHandler,
                this.EntityMaterializerSource,
                this.ExpressionPrinter,
                queryCompilationContext);
    }
}
