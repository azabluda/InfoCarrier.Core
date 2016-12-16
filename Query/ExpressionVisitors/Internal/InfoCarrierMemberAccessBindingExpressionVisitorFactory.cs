namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Remotion.Linq.Clauses;

    public class InfoCarrierMemberAccessBindingExpressionVisitorFactory : IMemberAccessBindingExpressionVisitorFactory
    {
        public virtual ExpressionVisitor Create(
            QuerySourceMapping querySourceMapping,
            EntityQueryModelVisitor queryModelVisitor,
            bool inProjection)
            => new InfoCarrierMemberAccessBindingExpressionVisitor(querySourceMapping, queryModelVisitor, inProjection);
    }
}
