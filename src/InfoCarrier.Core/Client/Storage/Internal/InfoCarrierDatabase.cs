// Copyright (c) Alexander Zabluda. All rights reserved.
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
        private static readonly System.Reflection.MethodInfo ExecuteQueryMethod
            = typeof(InfoCarrierDatabase).GetTypeInfo().GetDeclaredMethod(nameof(InfoCarrierDatabase.ExecuteQuery));

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
                elementType = typeof(TResult) == typeof(IEnumerable) ? typeof(object) : resultType.TryGetSequenceType();
            }

            var executeQuery = ExecuteQueryMethod.MakeGenericMethod(elementType)
                .ToDelegate<Func<QueryContext, Expression, IInfoCarrierClient, bool, bool, object>>();

            return queryContext => (TResult)executeQuery(queryContext, expression, this.infoCarrierClient, async, singleResult);
        }

        private static object ExecuteQuery<TElement>(
            QueryContext queryContext,
            Expression query,
            IInfoCarrierClient infoCarrierClient,
            bool async,
            bool singleResult)
        {
            return new QueryExecutor<TElement>(queryContext, query, infoCarrierClient)
                .Execute(async, singleResult);
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

        private class QueryExecutor<TElement>
        {
            private readonly QueryContext queryContext;
            private readonly IInfoCarrierClient infoCarrierClient;
            private readonly InfoCarrierQueryResultMapper resultMapper;
            private QueryDataRequest queryDataRequest;

            public QueryExecutor(QueryContext queryContext, Expression query, IInfoCarrierClient infoCarrierClient)
            {
                this.queryContext = queryContext;
                this.infoCarrierClient = infoCarrierClient;

                IReadOnlyDictionary<string, IEntityType> entityTypeMap = queryContext.Context.Model.GetEntityTypes().ToDictionary(x => x.DisplayName());
                ITypeResolver typeResolver = TypeResolver.Instance;
                ITypeInfoProvider typeInfoProvider = new TypeInfoProvider();
                this.resultMapper = new InfoCarrierQueryResultMapper(queryContext, typeResolver, typeInfoProvider, entityTypeMap);

                // Substitute query parameters
                query = new SubstituteParametersExpressionVisitor(queryContext).Visit(query);

                // UGLY: this resembles Remote.Linq.DynamicQuery.RemoteQueryProvider<>.TranslateExpression()
                var rlinq = query
                    .ToRemoteLinqExpression(typeInfoProvider, InfoCarrierEvaluatableExpressionFilter.CanBeEvaluated)
                    .ReplaceQueryableByResourceDescriptors(typeInfoProvider)
                    .ReplaceGenericQueryArgumentsByNonGenericArguments();

                this.queryDataRequest = new QueryDataRequest(
                    rlinq,
                    queryContext.Context.ChangeTracker.QueryTrackingBehavior);
            }

            public object Execute(bool async, bool singleResult)
            {
                using var cs = this.queryContext.ConcurrencyDetector.EnterCriticalSection();

                if (async)
                {
                    var asyncEnum = this.ExecuteAsync();
                    return singleResult ? (object)FirstOrDefaultAsync(asyncEnum) : asyncEnum;
                }

                var queryDataResult = this.infoCarrierClient.QueryData(this.queryDataRequest, this.queryContext.Context);
                var mapped = this.resultMapper.MapAndTrackResults<TElement>(queryDataResult.MappedResults);
                return singleResult ? (object)mapped.FirstOrDefault() : mapped;
            }

            private async IAsyncEnumerable<TElement> ExecuteAsync()
            {
                var queryDataResult = await this.infoCarrierClient.QueryDataAsync(this.queryDataRequest, this.queryContext.Context, default);
                var mapped = this.resultMapper.MapAndTrackResults<TElement>(queryDataResult.MappedResults);
                foreach (var element in mapped)
                {
                    yield return element;
                }
            }

            private static async Task<TElement> FirstOrDefaultAsync(IAsyncEnumerable<TElement> asyncEnum)
            {
                await foreach (var value in asyncEnum)
                {
                    return value;
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
                    [ExcludeFromCodeCoverage]
                    get => throw new NotImplementedException();
                }

                public IQueryProvider Provider
                {
                    [ExcludeFromCodeCoverage]
                    get => throw new NotImplementedException();
                }

                IRemoteQueryProvider IRemoteQueryable.Provider
                {
                    [ExcludeFromCodeCoverage]
                    get => throw new NotImplementedException();
                }

                [ExcludeFromCodeCoverage]
                IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

                [ExcludeFromCodeCoverage]
                IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotImplementedException();
            }
        }
    }
}
