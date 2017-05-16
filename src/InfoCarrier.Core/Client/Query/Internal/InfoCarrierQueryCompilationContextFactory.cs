namespace InfoCarrier.Core.Client.Query.Internal
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.Internal;

    public class InfoCarrierQueryCompilationContextFactory : QueryCompilationContextFactory
    {
        public InfoCarrierQueryCompilationContextFactory(QueryCompilationContextDependencies dependencies)
            : base(dependencies)
        {
        }

        public override QueryCompilationContext Create(bool async)
        {
            return new InfoCarrierQueryCompilationContext(
                this.Dependencies,
                async,
                this.TrackQueryResults);
        }
    }
}