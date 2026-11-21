// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.CosmosTests;

// Exact model exclusions from the pinned official ValueConvertersEndToEndCosmosTest.
public class ValueConvertersEndToEndTest(ValueConvertersEndToEndTest.CosmosFixture fixture)
    : ValueConvertersEndToEndTestBase<ValueConvertersEndToEndTest.CosmosFixture>(fixture)
{
    public class CosmosFixture : ValueConvertersEndToEndFixtureBase
    {
        private ITestStoreFactory? _factory;
        protected override bool UsePooling => false;
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating, onAddOptions: AddOptions,
            configureConventions: ConfigureConventions);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder).ConfigureWarnings(w => w.Ignore(CosmosEventId.NoPartitionKeyDefined));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            modelBuilder.Entity<ConvertingEntity>(b =>
            {
                // Official provider exclusions for EF Core issue 24684.
                b.Ignore(e => e.StringToDateTimeOffset);
                b.Ignore(e => e.NullableStringToDateTimeOffset);
                b.Ignore(e => e.StringToNullableDateTimeOffset);
                b.Ignore(e => e.NullableStringToNullableDateTimeOffset);
            });
        }
    }
}
