namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Remotion.Linq.Clauses;

    public class InfoCarrierEntityQueryableExpressionVisitorFactory : IEntityQueryableExpressionVisitorFactory
    {
        private readonly IModel model;

        public InfoCarrierEntityQueryableExpressionVisitorFactory(IModel model)
        {
            this.model = model;
        }

        public virtual ExpressionVisitor Create(
            EntityQueryModelVisitor queryModelVisitor, IQuerySource querySource)
            => new InfoCarrierEntityQueryableExpressionVisitor(
                this.model,
                queryModelVisitor,
                querySource);
    }
}
