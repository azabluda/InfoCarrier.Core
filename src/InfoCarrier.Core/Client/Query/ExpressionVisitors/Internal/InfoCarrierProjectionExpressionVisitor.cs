namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Common;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Query.Internal;
    using Remotion.Linq.Clauses.Expressions;

    public class InfoCarrierProjectionExpressionVisitor : DefaultQueryExpressionVisitor
    {
        private static readonly MethodInfo GetDummyOrderByMethod
            = Utils.GetMethodInfo(() => GetDummyOrderBy<object>()).GetGenericMethodDefinition();

        public InfoCarrierProjectionExpressionVisitor(
            EntityQueryModelVisitor entityQueryModelVisitor)
            : base(entityQueryModelVisitor)
        {
        }

        protected override Expression VisitSubQuery(SubQueryExpression expression)
        {
            Expression subExpression = base.VisitSubQuery(expression);

            if (subExpression.Type == expression.Type)
            {
                return subExpression;
            }

            // IOrderedQueryable is expected but subExpression is built as IQueryable.
            // We add a dummy .OrderBy(x => null) clause.
            if (typeof(IQueryable).GetTypeInfo().IsAssignableFrom(expression.Type.GetTypeInfo()))
            {
                Type elementType = expression.Type.GetSequenceType();

                return Expression.Call(
                    InfoCarrierQueryableLinqOperatorProvider.Instance.OrderBy
                        .MakeGenericMethod(elementType, typeof(object)),
                    subExpression,
                    GetDummyOrderByMethod
                        .MakeGenericMethod(elementType)
                        .ToDelegate<Func<Expression>>()
                        .Invoke());
            }

            return subExpression;
        }

        private static Expression<Func<TSource, object>> GetDummyOrderBy<TSource>()
        {
            return x => null;
        }
    }
}
