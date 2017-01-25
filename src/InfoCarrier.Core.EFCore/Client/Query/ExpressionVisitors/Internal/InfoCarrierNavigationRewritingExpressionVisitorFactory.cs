namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Remotion.Linq;

    public class InfoCarrierNavigationRewritingExpressionVisitorFactory : INavigationRewritingExpressionVisitorFactory
    {
        public NavigationRewritingExpressionVisitor Create(EntityQueryModelVisitor queryModelVisitor)
            => new SuppressNavigationRewriting(queryModelVisitor);

        private class SuppressNavigationRewriting : NavigationRewritingExpressionVisitor
        {
            public SuppressNavigationRewriting(EntityQueryModelVisitor queryModelVisitor)
                : base(queryModelVisitor)
            {
            }

            public override void Rewrite(QueryModel queryModel, QueryModel parentQueryModel)
            {
                // Don't rewrite navigations during query optimization.
                // We want to pass the original LINQ to the service.
            }
        }
    }
}
