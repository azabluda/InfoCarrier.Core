namespace InfoCarrier.Core.Client.Infrastructure.Internal
{
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;

    public class InfoCarrierModelSource : ModelSource
    {
        public InfoCarrierModelSource(IDbSetFinder setFinder, ICoreConventionSetBuilder coreConventionSetBuilder, IModelCustomizer modelCustomizer, IModelCacheKeyFactory modelCacheKeyFactory)
            : base(setFinder, coreConventionSetBuilder, modelCustomizer, modelCacheKeyFactory)
        {
        }
    }
}
