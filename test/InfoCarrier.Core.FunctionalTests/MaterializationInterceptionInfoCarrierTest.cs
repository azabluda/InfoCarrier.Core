// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>MaterializationInterceptionTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     Adopted for its own sake rather than for the count. This provider does <em>not</em>
///     materialize rows through EF's shaper — <see cref="ClientResultMaterializer" /> builds them
///     from the wire — so <c>IMaterializationInterceptor</c> never runs on the client, which is the
///     root of the `Nullable_client_side_concurrency_token_can_be_used` singleton the residual has
///     carried since A31. This base is where that shows up as a family rather than as one test.
/// </remarks>
public class MaterializationInterceptionInfoCarrierTest(NonSharedFixture fixture)
    : MaterializationInterceptionTestBase<MaterializationInterceptionInfoCarrierTest.InfoCarrierLibraryContext>(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.InMemory);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    protected override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base.AddOptions(builder).ConfigureWarnings(c => c.Ignore(InMemoryEventId.TransactionIgnoredWarning));

    /// <inheritdoc />
    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<IServiceCollection, IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true)
    {
        Fixture = null;
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }

    /// <summary>
    ///     EF's own <c>InMemoryLibraryContext</c>: the backing store is InMemory, so the owned
    ///     collection has to be mapped the way that provider maps one.
    /// </summary>
    public class InfoCarrierLibraryContext(DbContextOptions options) : LibraryContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestEntity30244>().OwnsMany(e => e.Settings);
        }
    }
}
