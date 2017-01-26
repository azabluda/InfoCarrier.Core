namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Client.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.Extensions.DependencyInjection;

    internal class TestInfoCarrierModelSource : InfoCarrierModelSource
    {
        private readonly TestModelSource testModelSource;

        public TestInfoCarrierModelSource(
            Action<ModelBuilder> onModelCreating,
            IDbSetFinder setFinder,
            ICoreConventionSetBuilder coreConventionSetBuilder)
            : base(setFinder, coreConventionSetBuilder, new ModelCustomizer(), new ModelCacheKeyFactory())
        {
            this.testModelSource = new TestModelSource(onModelCreating, setFinder, coreConventionSetBuilder, new ModelCustomizer(), new ModelCacheKeyFactory());
        }

        public override IModel GetModel(DbContext context, IConventionSetBuilder conventionSetBuilder, IModelValidator validator)
            => this.testModelSource.GetModel(context, conventionSetBuilder, validator);

        public static Func<IServiceProvider, InfoCarrierModelSource> GetFactory(Action<ModelBuilder> onModelCreating)
            => p => new TestInfoCarrierModelSource(
                onModelCreating,
                p.GetRequiredService<IDbSetFinder>(),
                p.GetRequiredService<ICoreConventionSetBuilder>());
    }
}