namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Modeling;

    public class InfoCarrierQueryModelVisitor : EntityQueryModelVisitor
    {
        //private readonly IMaterializerFactory _materializerFactory;

        public InfoCarrierQueryModelVisitor(
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
            IExpressionPrinter expressionPrinter,
            //IMaterializerFactory materializerFactory,
            QueryCompilationContext queryCompilationContext)
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
                expressionPrinter,
                //IMaterializerFactory materializerFactory,
                queryCompilationContext)
        {
            //_materializerFactory = materializerFactory;
        }

        public static MethodInfo ProjectionQueryMethodInfo { get; }
            = typeof(InfoCarrierQueryModelVisitor).GetTypeInfo()
                .GetDeclaredMethod(nameof(ProjectionQuery));

        public static MethodInfo EntityQueryMethodInfo { get; }
            = typeof(InfoCarrierQueryModelVisitor).GetTypeInfo()
                .GetDeclaredMethod(nameof(EntityQuery));

        private static IEnumerable<ValueBuffer> ProjectionQuery(
            QueryContext queryContext,
            IEntityType entityType)
        {
            throw new NotImplementedException();
            //return ((InfoCarrierQueryContext)queryContext).Store
            //    .GetTables(entityType)
            //    .SelectMany(t => t.Rows.Select(vs => new ValueBuffer(vs)));
        }

        private static IEnumerable<TEntity> EntityQuery<TEntity>(
            QueryContext queryContext,
            IEntityType entityType,
            IKey key,
            //Func<IEntityType, ValueBuffer, object> materializer,
            bool queryStateManager)
            where TEntity : Entity
        {
            return ((InfoCarrierQueryContext)queryContext).ServerContext.DataContext.All<TEntity>();
            //    .GetTables(entityType)
            //    .SelectMany(t =>
            //        t.Rows.Select(vs =>
            //        {
            //            var valueBuffer = new ValueBuffer(vs);

            //            return (TEntity)queryContext
            //                .QueryBuffer
            //                .GetEntity(
            //                    key,
            //                    new EntityLoadInfo(
            //                        valueBuffer,
            //                        vr => materializer(t.EntityType, vr)),
            //                    queryStateManager,
            //                    throwOnNullKey: false);
            //        }));
        }
    }
}
