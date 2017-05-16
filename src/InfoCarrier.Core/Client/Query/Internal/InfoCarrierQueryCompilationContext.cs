namespace InfoCarrier.Core.Client.Query.Internal
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Remotion.Linq;

    public class InfoCarrierQueryCompilationContext : QueryCompilationContext
    {
        public InfoCarrierQueryCompilationContext(
            QueryCompilationContextDependencies dependencies,
            bool async,
            bool trackQueryResults)
            : base(
                dependencies,
                new LinqOperatorProvider(),
                trackQueryResults)
        {
            this.Async = async;
        }

        internal bool Async { get; }

        public override void FindQuerySourcesRequiringMaterialization(EntityQueryModelVisitor queryModelVisitor, QueryModel queryModel)
        {
            // No tricky manipulations here. We just want to pass the original query to Remote.Linq
            // who (hopefully) knows how to deal with sources requiring materialization.
        }
    }
}
