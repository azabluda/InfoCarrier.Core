// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from the pinned official EF Core 11 materialization-interceptor suite.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Specifications;

public class MaterializationInterceptionTest(NonSharedFixture fixture) :
    MaterializationInterceptionTestBase<MaterializationInterceptionTest.CosmosLibraryContext>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/MaterializationInterceptionCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 9, 10, Skip = true, Justification = Upstream.GaveNoReason)]
    public override Task Intercept_query_materialization_with_owned_types_projecting_collection(bool async, bool usePooling)
        => throw SkipException.ForSkip(Upstream.GaveNoReason);

    [StoreLimit(UpstreamRepository.EfCore, Source, 12, 13, Justification = Upstream.GaveNoReason)]
    public override Task Intercept_query_materialization_with_owned_types(bool async, bool usePooling)
        => NoSyncTest(async, a => base.Intercept_query_materialization_with_owned_types(a, usePooling));

    public class CosmosLibraryContext(DbContextOptions options) : LibraryContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Book>(b =>
            {
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.HasPartitionKey(e => e.Title);
                b.HasKey(e => new { e.Id, e.Title });
            });

            modelBuilder.Entity<Pamphlet>(b =>
            {
                b.Property(e => e.Id).ValueGeneratedOnAdd();
                b.HasPartitionKey(e => e.Title);
                b.HasKey(e => new { e.Id, e.Title });
            });

            modelBuilder.Entity<TestEntity30244>(b =>
            {
                b.HasPartitionKey(e => e.Title);
                b.HasKey(e => new { e.Id, e.Title });
                // Make Cosmos's implicit embedded ownership explicit on the relational client.
                b.OwnsMany(e => e.Settings);
            });
        }
    }

    [StoreIssue(IssueTracker.EfCore, 33600, UpstreamRepository.EfCore, Source, 43, 45, Skip = true, Justification = "Issue #33600 - flaky test")]
    [Theory(Skip = "Issue #33600 - flaky test")]
    [InlineData(false, false), InlineData(false, true), InlineData(true, false), InlineData(true, true)]
    public override Task Binding_interceptors_are_used_by_queries(bool inject, bool usePooling)
        => base.Binding_interceptors_are_used_by_queries(inject, usePooling);

    [StoreIssue(IssueTracker.EfCore, 33600, UpstreamRepository.EfCore, Source, 47, 49, Skip = true, Justification = "Issue #33600 - flaky test")]
    [Theory(Skip = "Issue #33600 - flaky test")]
    [InlineData(false, false), InlineData(false, true), InlineData(true, false), InlineData(true, true)]
    public override Task Multiple_materialization_interceptors_can_be_used(bool inject, bool usePooling)
        => base.Multiple_materialization_interceptors_can_be_used(inject, usePooling);

    [StoreIssue(IssueTracker.EfCore, 33600, UpstreamRepository.EfCore, Source, 51, 53, Skip = true, Justification = "Issue #33600 - flaky test")]
    [Theory(Skip = "Issue #33600 - flaky test")]
    [InlineData(false, false), InlineData(false, true), InlineData(true, false), InlineData(true, true)]
    public override Task Intercept_query_materialization_for_empty_constructor(bool inject, bool usePooling)
        => base.Intercept_query_materialization_for_empty_constructor(inject, usePooling);

    [StoreIssue(IssueTracker.EfCore, 33600, UpstreamRepository.EfCore, Source, 55, 57, Skip = true, Justification = "Issue #33600 - flaky test")]
    [Theory(Skip = "Issue #33600 - flaky test")]
    [InlineData(false, false), InlineData(false, true), InlineData(true, false), InlineData(true, true)]
    public override Task Intercept_query_materialization_for_full_constructor(bool inject, bool usePooling)
        => base.Intercept_query_materialization_for_full_constructor(inject, usePooling);

    private readonly NonSharedModelInfoCarrierHarness _nonShared = new(new CosmosTier());

    protected override ITestStoreFactory NonSharedTestStoreFactory => _nonShared.TestStoreFactory;

    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.DependencyInjection.IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true,
        bool skipValidation = false)
    {
        NonSharedFixture = null;
        // Model configuration belongs on both halves; the caller's observable interceptor belongs on the client.
        // Match the existing SQLite materialization fixture rather than registering the hook twice.
        _nonShared.Prepare(typeof(TContext), onModelCreating, addServices: null, onConfiguring: null, configureConventions, AddNonSharedOptions);
        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider, skipValidation);
    }


    private static async Task NoSyncTest(bool async, Func<bool, Task> test)
    {
        if (async) await test(true);
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => test(false));
            Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", error.Message);
        }
    }

}

