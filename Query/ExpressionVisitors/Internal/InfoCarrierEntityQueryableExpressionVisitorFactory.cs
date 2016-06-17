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
        //private readonly IMaterializerFactory _materializerFactory;

        public InfoCarrierEntityQueryableExpressionVisitorFactory(
            IModel model)
            //IMaterializerFactory materializerFactory)
        {
            this.model = model;
            //_materializerFactory = materializerFactory;
        }

        public virtual ExpressionVisitor Create(
            EntityQueryModelVisitor queryModelVisitor, IQuerySource querySource)
            => new InfoCarrierEntityQueryableExpressionVisitor(
                this.model,
                //_materializerFactory,
                queryModelVisitor,
                querySource);
    }
}
