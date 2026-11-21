// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests;

public class ConcurrencyDetectorDisabledTest(ConcurrencyDetectorDisabledTest.CosmosFixture fixture)
    : ConcurrencyDetectorDisabledTestBase<ConcurrencyDetectorDisabledTest.CosmosFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/ConcurrencyDetectorDisabledCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 20, 21, Justification = Upstream.GaveNoReason)]
    public override Task Find(bool async) => Fixture.NoSyncTest(async, base.Find);

    [StoreLimit(UpstreamRepository.EfCore, Source, 17, 18, Justification = Upstream.GaveNoReason)]
    public override Task Count(bool async) => Fixture.NoSyncTest(async, base.Count);

    [StoreLimit(UpstreamRepository.EfCore, Source, 23, 24, Justification = Upstream.GaveNoReason)]
    public override Task First(bool async) => Fixture.NoSyncTest(async, base.First);

    [StoreLimit(UpstreamRepository.EfCore, Source, 26, 27, Justification = Upstream.GaveNoReason)]
    public override Task Last(bool async) => Fixture.NoSyncTest(async, base.Last);

    [StoreLimit(UpstreamRepository.EfCore, Source, 29, 30, Justification = Upstream.GaveNoReason)]
    public override Task Single(bool async) => Fixture.NoSyncTest(async, base.Single);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 10, 12, Skip = true, Justification = "Issue #17246")]
    public override Task Any(bool async) => throw SkipException.ForSkip("Issue #17246");

    [StoreLimit(UpstreamRepository.EfCore, Source, 32, 33, Justification = Upstream.GaveNoReason)]
    public override Task ToList(bool async) => Fixture.NoSyncTest(async, base.ToList);

    [StoreLimit(UpstreamRepository.EfCore, Source, 14, 15, Justification = Upstream.GaveNoReason)]
    public override Task SaveChanges(bool async) => Fixture.NoSyncTest(async, base.SaveChanges);

    public class CosmosFixture : ConcurrencyDetectorFixtureBase
    {
        private ITestStoreFactory? _factory;
        protected override bool UsePooling => false;
        protected override string StoreName => "CosmosConcurrencyDetectorDisabled";
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating,
            onAddOptions: builder => builder.EnableThreadSafetyChecks(false),
            configureConventions: ConfigureConventions);
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder.EnableThreadSafetyChecks(false))
                .ConfigureWarnings(w => w.Ignore(CosmosEventId.NoPartitionKeyDefined));

        public async Task NoSyncTest(bool async, Func<bool, Task> test)
        {
            if (async)
            {
                await test(true);
            }
            else
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => test(false));
                Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", error.Message);
            }
        }

    }
}

