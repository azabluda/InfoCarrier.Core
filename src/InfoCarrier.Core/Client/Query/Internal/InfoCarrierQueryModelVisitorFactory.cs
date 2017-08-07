namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using Microsoft.EntityFrameworkCore.Query;

    public class InfoCarrierQueryModelVisitorFactory : EntityQueryModelVisitorFactory
    {
        public InfoCarrierQueryModelVisitorFactory(EntityQueryModelVisitorDependencies dependencies)
            : base(dependencies)
        {
        }

        public override EntityQueryModelVisitor Create(
            QueryCompilationContext queryCompilationContext,
            EntityQueryModelVisitor parentEntityQueryModelVisitor)
            => throw new InvalidOperationException(@"InfoCarrier.Core is not using EntityQueryModelVisitor");
    }
}
