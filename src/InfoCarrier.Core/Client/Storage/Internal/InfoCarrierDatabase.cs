// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Aqua.TypeSystem;
    using InfoCarrier.Core.Client.Infrastructure.Internal;
    using InfoCarrier.Core.Client.Query.Internal;
    using InfoCarrier.Core.Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.EntityFrameworkCore.Update;
    using Remote.Linq;
    using Remote.Linq.ExpressionVisitors;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InfoCarrierDatabase : IDatabase
    {
        private readonly IInfoCarrierClient infoCarrierClient;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1642:ConstructorSummaryDocumentationMustBeginWithStandardText", Justification = "Entity Framework Core internal.")]
        public InfoCarrierDatabase(IDbContextOptions options)
        {
            this.infoCarrierClient = options.Extensions.OfType<InfoCarrierOptionsExtension>().First().InfoCarrierClient;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1618:Generic type parameters should be documented", Justification = "Entity Framework Core internal.")]
        public Func<QueryContext, TResult> CompileQuery<TResult>(Expression query, bool async)
        {
            // Replace EntityQueryable with stub
            var expression = EntityQueryableStubVisitor.Replace(query);
            var singleResult = Utils.QueryReturnsSingleResult(query);

            Type resultType = typeof(TResult);
            Type elementType;
            if (singleResult)
            {
                if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    elementType = resultType.GenericTypeArguments.Single();
                }
                else
                {
                    elementType = resultType;
                }
            }
            else
            {
                elementType = resultType.TryGetSequenceType();
            }

            var preparedQueryType = typeof(PreparedQuery<>).MakeGenericType(elementType);
            var executePreparedQuery = preparedQueryType.GetTypeInfo()
                .GetDeclaredMethod(nameof(PreparedQuery<int>.Execute));
                //.ToDelegate<Func<object, bool, object>>();

            return queryContext =>
            {
                object x = Activator.CreateInstance(preparedQueryType, queryContext, expression, infoCarrierClient);
                try
                {
                    return (TResult)executePreparedQuery.Invoke(x, new object[] { async, singleResult });
                }
                catch (TargetInvocationException e) when (e.InnerException != null)
                {
                    throw e.InnerException;
                }
                //return (TResult)executePreparedQuery(x, async);
            };
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public int SaveChanges(IList<IUpdateEntry> entries)
        {
            SaveChangesResult result = this.infoCarrierClient.SaveChanges(new SaveChangesRequest(entries));
            return result.ApplyTo(entries);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public async Task<int> SaveChangesAsync(
            IList<IUpdateEntry> entries,
            CancellationToken cancellationToken = default)
        {
            SaveChangesResult result = await this.infoCarrierClient.SaveChangesAsync(new SaveChangesRequest(entries), cancellationToken);
            return result.ApplyTo(entries);
        }

        class PreparedQuery<TElement>
        {
            private readonly QueryContext queryContext;
            private readonly IInfoCarrierClient infoCarrierClient;
            private readonly InfoCarrierQueryResultMapper resultMapper;
            private QueryDataRequest queryDataRequest;

            public PreparedQuery(QueryContext queryContext, Expression query, IInfoCarrierClient infoCarrierClient)
            {
                this.queryContext = queryContext;
                this.infoCarrierClient = infoCarrierClient;

                IReadOnlyDictionary<string, IEntityType> entityTypeMap = queryContext.Context.Model.GetEntityTypes().ToDictionary(x => x.DisplayName());
                ITypeResolver typeResolver = new TypeResolver();
                ITypeInfoProvider typeInfoProvider = new TypeInfoProvider();
                this.resultMapper = new InfoCarrierQueryResultMapper(queryContext, typeResolver, typeInfoProvider, entityTypeMap);

                // Substitute query parameters
                query = new SubstituteParametersExpressionVisitor(queryContext).Visit(query);

                // UGLY: this resembles Remote.Linq.DynamicQuery.RemoteQueryProvider<>.TranslateExpression()
                var rlinq = query
                    .ToRemoteLinqExpression(typeInfoProvider, Remote.Linq.EntityFrameworkCore.ExpressionEvaluator.CanBeEvaluated)
                    .ReplaceQueryableByResourceDescriptors(typeInfoProvider)
                    .ReplaceGenericQueryArgumentsByNonGenericArguments();

                this.queryDataRequest = new QueryDataRequest(
                    rlinq,
                    queryContext.Context.ChangeTracker.QueryTrackingBehavior);
            }

            public object Execute(bool async, bool singleResult)
            {
                if (async)
                {
                    var asyncEnum = ExecuteAsync();
                    return singleResult ? (object)XXX(asyncEnum) : asyncEnum;
                }

                var queryDataResult = this.infoCarrierClient.QueryData(this.queryDataRequest, queryContext.Context);
                var mapped = this.resultMapper.MapAndTrackResults<TElement>(queryDataResult.MappedResults);
                return singleResult ? (object)mapped.FirstOrDefault() : mapped;
            }

            private async IAsyncEnumerable<TElement> ExecuteAsync()
            {
                var queryDataResult = await this.infoCarrierClient.QueryDataAsync(this.queryDataRequest, queryContext.Context, default);
                var mapped = this.resultMapper.MapAndTrackResults<TElement>(queryDataResult.MappedResults);
                foreach (var element in mapped)
                {
                    yield return element;
                }
            }

            private async Task<TElement> XXX(IAsyncEnumerable<TElement> asyncEnum)
            {
                await foreach (var VARIABLE in asyncEnum)
                {
                    return VARIABLE;
                }

                return default;
            }
        }

        private class SubstituteParametersExpressionVisitor : ExpressionVisitor
        {
            private readonly QueryContext queryContext;

            public SubstituteParametersExpressionVisitor(QueryContext queryContext)
            {
                this.queryContext = queryContext;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node.Name?.StartsWith(CompiledQueryCache.CompiledQueryParameterPrefix, StringComparison.Ordinal) == true)
                {
                    object paramValue = Activator.CreateInstance(
                        typeof(ValueWrapper<>).MakeGenericType(node.Type),
                        this.queryContext.ParameterValues[node.Name]);

                    return Expression.Property(
                        Expression.Constant(paramValue),
                        nameof(ValueWrapper<object>.Value));
                }

                return base.VisitParameter(node);
            }

            private struct ValueWrapper<T>
            {
                public ValueWrapper(T value) => this.Value = value;

                public T Value { get; set; }
            }
        }

        private class EntityQueryableStubVisitor : ExpressionVisitorBase
        {
            internal static Expression Replace(Expression expression)
                => new EntityQueryableStubVisitor().Visit(expression);

            protected override Expression VisitConstant(ConstantExpression constantExpression)
                => constantExpression.IsEntityQueryable()
                    ? this.VisitEntityQueryable(((IQueryable)constantExpression.Value).ElementType)
                    : constantExpression;

            private Expression VisitEntityQueryable(Type elementType)
            {
                object stub = Activator.CreateInstance(typeof(RemoteQueryableStub<>).MakeGenericType(elementType));
                return Expression.Constant(stub);
            }

            private class RemoteQueryableStub<T> : IRemoteQueryable, IQueryable<T>
            {
                public Type ElementType => typeof(T);

                public Expression Expression
                {
                    [ExcludeFromCoverage]
                    get => throw new NotImplementedException();
                }

                public IQueryProvider Provider
                {
                    [ExcludeFromCoverage]
                    get => throw new NotImplementedException();
                }

                IRemoteQueryProvider IRemoteQueryable.Provider
                {
                    [ExcludeFromCoverage]
                    get => throw new NotImplementedException();
                }

                [ExcludeFromCoverage]
                IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

                [ExcludeFromCoverage]
                IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotImplementedException();
            }
        }
    }
}
