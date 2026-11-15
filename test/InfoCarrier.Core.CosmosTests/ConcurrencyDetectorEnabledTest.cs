// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests;

public class ConcurrencyDetectorEnabledTest(ConcurrencyDetectorEnabledTest.CosmosFixture fixture)
    : ConcurrencyDetectorEnabledTestBase<ConcurrencyDetectorEnabledTest.CosmosFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/ConcurrencyDetectorEnabledCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 10, 17, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 10, 17, Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override Task Find(bool async)
        => async ? base.Find(true) : throw SkipException.ForSkip("Always throws for sync.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 19, 26, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 19, 26, Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override Task Count(bool async)
        => async ? base.Count(true) : throw SkipException.ForSkip("Always throws for sync.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 28, 35, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 28, 35, Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override Task First(bool async)
        => async ? base.First(true) : throw SkipException.ForSkip("Always throws for sync.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 37, 44, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 37, 44, Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override Task Last(bool async)
        => async ? base.Last(true) : throw SkipException.ForSkip("Always throws for sync.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 46, 53, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 46, 53, Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override Task Single(bool async)
        => async ? base.Single(true) : throw SkipException.ForSkip("Always throws for sync.");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 55, 57, Skip = true, Justification = "Issue #17246")]
    public override Task Any(bool async) => throw SkipException.ForSkip("Issue #17246");

    [StoreLimit(UpstreamRepository.EfCore, Source, 59, 66, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 59, 66, Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override Task ToList(bool async)
        => async ? base.ToList(true) : throw SkipException.ForSkip("Always throws for sync.");

    public class CosmosFixture : ConcurrencyDetectorFixtureBase
    {
        private ITestStoreFactory? _factory;
        protected override bool UsePooling => false;
        protected override string StoreName => "CosmosConcurrencyDetectorEnabled";
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating,
            configureConventions: ConfigureConventions);
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(w => w.Ignore(CosmosEventId.NoPartitionKeyDefined));

    }
}

