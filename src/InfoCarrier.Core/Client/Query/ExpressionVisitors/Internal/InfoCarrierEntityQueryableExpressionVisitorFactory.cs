namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Remotion.Linq.Clauses;

    public class InfoCarrierEntityQueryableExpressionVisitorFactory : IEntityQueryableExpressionVisitorFactory
    {
        public virtual ExpressionVisitor Create(
            EntityQueryModelVisitor queryModelVisitor, IQuerySource querySource)
            => throw new InvalidOperationException(@"InfoCarrier.Core is not using EntityQueryModelVisitor");
    }
}
