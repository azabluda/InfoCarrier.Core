namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Remotion.Linq.Clauses;

    public class InfoCarrierProjectionExpressionVisitorFactory : IProjectionExpressionVisitorFactory
    {
        public ExpressionVisitor Create(
            EntityQueryModelVisitor entityQueryModelVisitor,
            IQuerySource querySource)
            => new InfoCarrierProjectionExpressionVisitor(entityQueryModelVisitor);
    }
}
