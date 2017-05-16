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
                queryCompilationContext,
                parentEntityQueryModelVisitor is null);
    }
}
