namespace InfoCarrier.Core.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.Extensions.DependencyInjection;
    using Remote.Linq;
    using Remote.Linq.Expressions;
    using Remote.Linq.ExpressionVisitors;

    public static class QueryDataHelper
    {
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

            return type.TryGetSequenceType() ?? ifNotSequence;
        }

        public static async Task<IEnumerable<DynamicObject>> QueryDataAsync(DbContext dbContext, Expression rlinq)
        {
            // UGLY: this resembles Remote.Linq.Expressions.ExpressionExtensions.PrepareForExecution()
            // but excludes PartialEval (otherwise simple queries like db.Set<X>().First() are executed
            // prematurely)
            var linqExpression = rlinq
                .ReplaceNonGenericQueryArgumentsByGenericArguments()
                .ReplaceResourceDescriptorsByQueryable(
                    typeResolver: null,
                    provider: type =>
                        MethodInfoExtensions.GetMethodInfo(() => dbContext.Set<object>())
                            .GetGenericMethodDefinition()
                            .MakeGenericMethod(type)
                            .ToDelegate<Func<IQueryable>>(dbContext)
                            .Invoke())
                .ToLinqExpression(typeResolver: null);

            IAsyncQueryProvider provider = dbContext.GetService<IAsyncQueryProvider>();
            Type elementType = GetSequenceType(linqExpression.Type, linqExpression.Type);

            object queryResult = await typeof(QueryDataHelper).GetTypeInfo()
                .GetDeclaredMethod(nameof(ExecuteExpression))
                .MakeGenericMethod(elementType)
                .ToDelegate<Func<IAsyncQueryProvider, System.Linq.Expressions.Expression, Task<object>>>()
                .Invoke(provider, linqExpression);

            IEnumerable<DynamicObject> result =
                Remote.Linq.Expressions.ExpressionExtensions.ConvertResultToDynamicObjects(
                    queryResult,
                    new EntityToDynamicObjectMapper(dbContext));

            return result;
        }

        private static async Task<object> ExecuteExpression<T>(
            IAsyncQueryProvider provider,
            System.Linq.Expressions.Expression expression)
        {
            // Temp workaround: execute queries synchronously, because async execution
            // is slow due to the known performance issue in EFC
            // https://github.com/aspnet/EntityFramework/issues/5816
            return Remote.Linq.Expressions.ExpressionExtensions.Execute(expression)
                ?? Enumerable.Repeat<object>(null, 1);

#pragma warning disable 162
            var queryResult = new List<T>();
            using (var enumerator = provider.ExecuteAsync<T>(expression).GetEnumerator())
            {
                while (await enumerator.MoveNext())
                {
                    queryResult.Add(enumerator.Current);
                }
            }

            return queryResult;
#pragma warning restore 162
        }

        private class EntityToDynamicObjectMapper : DynamicObjectMapper
        {
            private readonly IStateManager stateManager;

            public EntityToDynamicObjectMapper(DbContext dbContext)
                : base(new DynamicObjectMapperSettings { FormatPrimitiveTypesAsString = true })
            {
                this.stateManager = dbContext.GetInfrastructure().GetRequiredService<IStateManager>();
            }

            protected override DynamicObject MapToDynamicObjectGraph(object obj, Func<Type, bool> setTypeInformation)
            {
                DynamicObject dto = base.MapToDynamicObjectGraph(obj, setTypeInformation);

                if (obj == null || dto == null)
                {
                    return dto;
                }

                InternalEntityEntry entry = this.stateManager.TryGetEntry(obj);
                if (entry == null)
                {
                    return dto;
                }

                foreach (var shadowProp in entry.EntityType.GetProperties().Where(p => p.IsShadowProperty))
                {
                    dto.Add(
                        shadowProp.Name,
                        this.MapToDynamicObjectGraph(entry[shadowProp], setTypeInformation));
                }

                dto.Add(EntityTypeNameTag, entry.EntityType.Name);

                return dto;
            }
        }
    }
}
