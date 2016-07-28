namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Query.Internal;
    using Remotion.Linq.Clauses;

    public class InfoCarrierEntityQueryableExpressionVisitor : EntityQueryableExpressionVisitor
    {
        private readonly IModel model;
        private readonly IQuerySource querySource;

        public InfoCarrierEntityQueryableExpressionVisitor(
            IModel model,
            EntityQueryModelVisitor entityQueryModelVisitor,
            IQuerySource querySource)
            : base(entityQueryModelVisitor)
        {
            this.model = model;
            this.querySource = querySource;
        }

        protected override Expression VisitEntityQueryable(Type elementType)
        {
            return Expression.Call(
                InfoCarrierQueryModelVisitor.EntityQueryMethodInfo.MakeGenericMethod(elementType),
                Expression.Constant(this.querySource),
                EntityQueryModelVisitor.QueryContextParameter,
                Expression.Constant(this.model),
                Expression.Constant(this.QueryModelVisitor.QueryCompilationContext.IsTrackingQuery));
        }
    }
}
