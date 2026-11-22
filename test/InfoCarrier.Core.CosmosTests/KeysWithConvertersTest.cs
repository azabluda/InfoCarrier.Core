// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.CosmosTests;

// Official EF Core 11 KeysWithConvertersCosmosTest at 9e685c7a52d1a344d133a3d370e458db97417b3f.
public class KeysWithConvertersTest(KeysWithConvertersTest.CosmosFixture fixture)
    : KeysWithConvertersTestBase<KeysWithConvertersTest.CosmosFixture>(fixture)
{
    public class CosmosFixture : KeysWithConvertersFixtureBase
    {
        private ITestStoreFactory? _factory;
        protected override bool UsePooling => false;
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating, onAddOptions: AddOptions,
            configureConventions: ConfigureConventions);
        public override bool UseInclude => false;

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            modelBuilder.HasRootDiscriminatorInJsonId();
        }

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder.ConfigureWarnings(w => w.Ignore(
                CoreEventId.MappedEntityTypeIgnoredWarning,
                CosmosEventId.NoPartitionKeyDefined,
                CoreEventId.CollectionWithoutComparer)));
    }
}
