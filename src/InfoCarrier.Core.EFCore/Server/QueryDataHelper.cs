namespace InfoCarrier.Core.Server
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.Extensions.DependencyInjection;
    using Remote.Linq;
    using Remote.Linq.DynamicQuery;
    using Remote.Linq.ExpressionVisitors;

    public sealed class QueryDataHelper : IDisposable
    {
        private readonly DbContext dbContext;
        private readonly System.Linq.Expressions.Expression linqExpression;
        private readonly ITypeResolver typeResolver = new TypeResolver();

        public QueryDataHelper(Func<DbContext> dbContextFactory, Remote.Linq.Expressions.Expression rlinq)
        {
            this.dbContext = dbContextFactory();

            // UGLY: this resembles Remote.Linq.Expressions.ExpressionExtensions.PrepareForExecution()
            // but excludes PartialEval (otherwise simple queries like db.Set<X>().First() are executed
            // prematurely)
            this.linqExpression = rlinq
                .ReplaceNonGenericQueryArgumentsByGenericArguments()
                .ReplaceResourceDescriptorsByQueryable(
                    this.typeResolver,
                    provider: type =>
                        MethodInfoExtensions.GetMethodInfo(() => this.dbContext.Set<object>())
                            .GetGenericMethodDefinition()
                            .MakeGenericMethod(type)
                            .ToDelegate<Func<IQueryable>>(this.dbContext)
                            .Invoke())
                .ToLinqExpression(this.typeResolver);

            this.linqExpression = new FixIncludeVisitor().ReplaceRlinqIncludes(this.linqExpression);

            // Replace NullConditionalExpressionStub MethodCallExpression with NullConditionalExpression
            this.linqExpression = new ReplaceNullConditionalExpressionVisitor(false).Visit(this.linqExpression);
        }

        internal static string EntityTypeNameTag { get; } = "__EntityType";

        internal static Type GetSequenceType(Type type, Type ifNotSequence)
        {
            // Despite formally a string is a sequence of chars, we treat it as a scalar type
            if (type == typeof(string))
            {
                return ifNotSequence;
            }

            // Arrays is another special case
            if (type.IsArray)
            {
                return ifNotSequence;
            }

            // Grouping is another special case
            if (type.IsGrouping())
            {
                return ifNotSequence;
            }

            return type.TryGetSequenceType() ?? ifNotSequence;
        }

        public IEnumerable<DynamicObject> QueryData()
        {
            var resultType = this.linqExpression.Type.GetGenericTypeImplementations(typeof(IQueryable<>)).Select(t => t.GetGenericArguments().Single()).FirstOrDefault();
            resultType = resultType == null ? this.linqExpression.Type : typeof(IEnumerable<>).MakeGenericType(resultType);

            object queryResult =
                typeof(QueryDataHelper).GetTypeInfo()
                .GetDeclaredMethod(nameof(this.ExecuteExpression))
                .MakeGenericMethod(resultType)
                .ToDelegate<Func<object>>(this)
                .Invoke();

            if (queryResult is IEnumerable enumerable)
            {
                if (GetSequenceType(resultType, null) != null)
                {
                    // TRICKY: sometimes EF returns enumerable result as ExceptionInterceptor<T> which
                    // isn't fully ready for mapping to DynamicObjects (some complex self-referencing navigation
                    // properties may not have received their values yet). We have to force materialization.
                    queryResult = enumerable.Cast<object>().ToList();
                }
                else
                {
                    // Little trick for a single result item of type IGrouping/array/string
                    queryResult = new[] { queryResult };
                }
            }

            return this.MapResult(queryResult);
        }

        public async Task<IEnumerable<DynamicObject>> QueryDataAsync()
        {
            Type elementType = GetSequenceType(this.linqExpression.Type, this.linqExpression.Type);

            object queryResult = await typeof(QueryDataHelper).GetTypeInfo()
                .GetDeclaredMethod(nameof(this.ExecuteExpressionAsync))
                .MakeGenericMethod(elementType)
                .ToDelegate<Func<Task<object>>>(this)
                .Invoke();

            return this.MapResult(queryResult);
        }

        private object ExecuteExpression<T>()
        {
            IQueryProvider provider = this.dbContext.GetService<IAsyncQueryProvider>();
            return provider.Execute<T>(this.linqExpression);
        }

        private async Task<object> ExecuteExpressionAsync<T>()
        {
            IAsyncQueryProvider provider = this.dbContext.GetService<IAsyncQueryProvider>();

            var queryResult = new List<T>();
            using (var enumerator = provider.ExecuteAsync<T>(this.linqExpression).GetEnumerator())
            {
                while (await enumerator.MoveNext())
                {
                    queryResult.Add(enumerator.Current);
                }
            }

            return queryResult;
        }

        private IEnumerable<DynamicObject> MapResult(object queryResult)
        {
            IEnumerable<DynamicObject> result =
                Remote.Linq.Expressions.ExpressionExtensions.ConvertResultToDynamicObjects(
                    queryResult,
                    new EntityToDynamicObjectMapper(this.dbContext, this.typeResolver));

            return result;
        }

        public void Dispose()
        {
            this.dbContext.Dispose();
        }

        private class EntityToDynamicObjectMapper : DynamicObjectMapper
        {
            private readonly IStateManager stateManager;
            private readonly Dictionary<object, DynamicObject> cachedEntities =
                new Dictionary<object, DynamicObject>();

            public EntityToDynamicObjectMapper(DbContext dbContext, ITypeResolver typeResolver)
                : base(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true }, typeResolver)
            {
                this.stateManager = dbContext.GetInfrastructure().GetRequiredService<IStateManager>();
            }

            protected override bool ShouldMapToDynamicObject(IEnumerable collection) =>
                collection.GetType().GetGenericTypeImplementations(typeof(IGrouping<,>)).Any()
                || base.ShouldMapToDynamicObject(collection);

            protected override DynamicObject MapToDynamicObjectGraph(object obj, Func<Type, bool> setTypeInformation)
            {
                if (obj == null)
                {
                    return null;
                }

                Type objType = obj.GetType();

                // Special mapping of arrays
                if (objType.IsArray)
                {
                    var array = ((IEnumerable)obj)
                        .Cast<object>()
                        .Select(x => this.MapToDynamicObjectGraph(x, setTypeInformation))
                        .ToArray();
                    var dto = new DynamicObject();
                    dto.Add("ArrayType", new Aqua.TypeSystem.TypeInfo(objType, includePropertyInfos: false));
                    dto.Add("Elements", array);
                    return dto;
                }

                // Special mapping of IGrouping<,>
                foreach (var groupingType in objType.GetGenericTypeImplementations(typeof(IGrouping<,>)))
                {
                    object mappedGrouping =
                        MethodInfoExtensions.GetMethodInfo(() => this.MapGrouping<object, object>(null, null))
                            .GetGenericMethodDefinition()
                            .MakeGenericMethod(groupingType.GenericTypeArguments)
                            .Invoke(this, new[] { obj, setTypeInformation });
                    return (DynamicObject)mappedGrouping;
                }

                // Special mapping of entities
                if (this.stateManager.Context.Model.FindEntityType(objType) != null)
                {
                    if (this.cachedEntities.TryGetValue(obj, out DynamicObject dto))
                    {
                        return dto;
                    }

                    this.cachedEntities.Add(obj, dto = new DynamicObject(objType));

                    InternalEntityEntry entry = this.stateManager.GetOrCreateEntry(obj);
                    dto.Add(EntityTypeNameTag, entry.EntityType.Name);

                    foreach (MemberEntry prop in entry.ToEntityEntry().Members)
                    {
                        dto.Add(
                            prop.Metadata.Name,
                            this.MapToDynamicObjectGraph(prop.CurrentValue, setTypeInformation));
                    }

                    return dto;
                }

                return base.MapToDynamicObjectGraph(obj, setTypeInformation);
            }

            private DynamicObject MapGrouping<TKey, TElement>(IGrouping<TKey, TElement> grouping, Func<Type, bool> setTypeInformation)
            {
                var mappedGrouping = new DynamicObject(typeof(IGrouping<TKey, TElement>));
                mappedGrouping.Add("Key", this.MapToDynamicObjectGraph(grouping.Key, setTypeInformation));
                mappedGrouping.Add("Elements", this.MapCollection(grouping, setTypeInformation).ToList());
                return mappedGrouping;
            }
        }

        // Remote.Linq falls into premature evaluation of EntityFrameworkQueryableExtensions.Include(string)
        // extension method, therefore we had to replace those with QueryFunctions.Include(string) in the tree.
        // Here we are restoring the EF version back.
        private class FixIncludeVisitor : ExpressionVisitorBase
        {
            private static readonly System.Reflection.MethodInfo QfIncludeMethod =
                MethodInfoExtensions.GetMethodInfo(() => QueryFunctions.Include<object>(null, null)).GetGenericMethodDefinition();

            private static readonly System.Reflection.MethodInfo EfIncludeMethod =
                MethodInfoExtensions.GetMethodInfo(() => EntityFrameworkQueryableExtensions.Include<object>(null, null)).GetGenericMethodDefinition();

            internal System.Linq.Expressions.Expression ReplaceRlinqIncludes(System.Linq.Expressions.Expression expression)
            {
                return this.Visit(expression);
            }

            protected override System.Linq.Expressions.Expression VisitMethodCall(System.Linq.Expressions.MethodCallExpression m)
            {
                if (m.Method.IsGenericMethod
                    && m.Method.GetGenericMethodDefinition() == QfIncludeMethod)
                {
                    return System.Linq.Expressions.Expression.Call(
                        EfIncludeMethod.MakeGenericMethod(m.Method.GetGenericArguments()),
                        m.Arguments.Select(this.Visit));
                }

                return base.VisitMethodCall(m);
            }
        }
    }
}
