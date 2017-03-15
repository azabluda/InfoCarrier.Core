namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Storage.Internal;

    public class BuiltInDataTypesInfoCarrierFixture : BuiltInDataTypesFixtureBase
    {
        private readonly InfoCarrierInMemoryTestHelper<DbContext> helper;

        public BuiltInDataTypesInfoCarrierFixture()
        {
            this.helper = InfoCarrierInMemoryTestHelper.Create(
                this.OnModelCreating,
                (opt, _) => new DbContext(opt));
        }

        public override bool SupportsBinaryKeys
            => false;

        public override DateTime DefaultDateTime
            => default(DateTime);

        public override DbContext CreateContext()
            => this.helper.CreateInfoCarrierContext();

        public override void Dispose()
        {
            using (var context = this.helper.CreateInMemoryContext())
            {
                var storeSource = context.GetService<IInMemoryStoreSource>();
                storeSource.GetGlobalStore().Clear();
            }
        }
    }
}
