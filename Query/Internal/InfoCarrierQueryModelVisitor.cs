namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Aqua.Dynamic;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Modeling;
    using Remote.Linq;
    using TrackableEntities.Client;
    using Utils;

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

        private static IQueryable<TEntity> EntityQuery<TEntity>(
            QueryContext queryContext,
            IEntityType entityType,
            IKey key,
            //Func<IEntityType, ValueBuffer, object> materializer,
            bool queryStateManager)
            where TEntity : Entity
        {
            ServerContext sctx = ((InfoCarrierQueryContext)queryContext).ServerContext;
            var dataContext = sctx.DataContext; // UGLY: force creation of DataContext

            IQueryable qry = RemoteQueryable.Create(
                typeof(TEntity),
                sctx.QueryData,
                null,
                new DynamicObjectEntityMapper(
                    (obj, targetType, mapper) =>
                    {
                        foreach (DynamicObject dobj in obj.YieldAs<DynamicObject>())
                        {
                            if (!typeof(IEntity).IsAssignableFrom(targetType))
                            {
                                continue;
                            }

                            // TODO: may not work if the key properties are not the first, or there exists more than one key
                            var keyValueBuffer = new ValueBuffer(
                                key.Properties
                                    .Select(p => mapper.MapFromDynamicObjectGraphCustomImpl(dobj.Get(p.Name), p.ClrType))
                                    .ToList());

                            // Get/create instance of entity from EFC's identity map
                            object entity = queryContext
                                .QueryBuffer
                                .GetEntity(
                                    key,
                                    new EntityLoadInfo(
                                        keyValueBuffer,
                                        vr =>
                                        {
                                            IEntity instance = (IEntity)Activator.CreateInstance(targetType, true);
                                            instance.EntityContext = dataContext;
                                            return instance;
                                        }),
                                    queryStateManager,
                                    throwOnNullKey: false);

                            // Set entity properties
                            var targetProperties = entity.GetType().GetProperties().Where(p => p.CanWrite);
                            foreach (PropertyInfo property in targetProperties)
                            {
                                object rawValue;
                                if (dobj.TryGet(property.Name, out rawValue))
                                {
                                    object value = mapper.MapFromDynamicObjectGraphCustomImpl(rawValue, property.PropertyType);
                                    property.SetValue(entity, value);
                                }
                            }

                            return entity;
                        }

                        if (targetType.IsGenericType &&
                            targetType.GetGenericTypeDefinition() == typeof(ChangeTrackingCollection<>))
                        {
                            object list = mapper.MapFromDynamicObjectGraphDefaultImpl(
                                obj,
                                typeof(List<>).MakeGenericType(targetType.GenericTypeArguments));

                            return Activator.CreateInstance(
                                typeof(ChangeTrackingCollection<>).MakeGenericType(targetType.GenericTypeArguments),
                                list,
                                !dataContext.ChangeTrackingEnabled);
                        }

                        return mapper.MapFromDynamicObjectGraphDefaultImpl(obj, targetType);
                    }));

            return qry.Cast<TEntity>();
        }

        private sealed class DynamicObjectEntityMapper : DynamicObjectMapper
        {
            private readonly Func<object, Type, DynamicObjectEntityMapper, object> materializer;

            public DynamicObjectEntityMapper(Func<object, Type, DynamicObjectEntityMapper, object> materializer)
                : base(formatPrimitiveTypesAsString: true)
            {
                this.materializer = materializer;
            }

            public object MapFromDynamicObjectGraphCustomImpl(object obj, Type targetType)
            {
                return this.MapFromDynamicObjectGraph(obj, targetType);
            }

            public object MapFromDynamicObjectGraphDefaultImpl(object obj, Type targetType)
            {
                return base.MapFromDynamicObjectGraph(obj, targetType);
            }

            protected override object MapFromDynamicObjectGraph(object obj, Type targetType)
            {
                return this.materializer(obj, targetType, this);
            }
        }
    }
}
