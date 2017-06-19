namespace InfoCarrier.Core.Client.Query.Internal
{
    using Microsoft.EntityFrameworkCore.Query;

    public class InfoCarrierQueryModelVisitorFactory : EntityQueryModelVisitorFactory
    {
        public InfoCarrierQueryModelVisitorFactory(EntityQueryModelVisitorDependencies dependencies)
            : base(dependencies)
        {
        }

        public override EntityQueryModelVisitor Create(
                QueryCompilationContext queryCompilationContext, EntityQueryModelVisitor parentEntityQueryModelVisitor)
            => new InfoCarrierQueryModelVisitor(
                this.Dependencies,
                queryCompilationContext);

        private class InfoCarrierQueryModelVisitor : EntityQueryModelVisitor
        {
            public InfoCarrierQueryModelVisitor(
                EntityQueryModelVisitorDependencies dependencies,
                QueryCompilationContext queryCompilationContext)
                : base(dependencies, queryCompilationContext)
            {
            }
        }
    }
}
