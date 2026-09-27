// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>MaterializationInterceptionTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     Adopted for its own sake rather than for the count. This provider does <em>not</em>
///     materialize rows through EF's shaper — <see cref="ClientResultMaterializer" /> builds them
///     from the wire — so <c>IMaterializationInterceptor</c> never runs on the client, which is the
///     root of the `Nullable_client_side_concurrency_token_can_be_used` singleton the residual has
///     carried since A31. This base is where that shows up as a family rather than as one test.
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. The owned collection is
///         mapped to JSON, as in EF's own <c>MaterializationInterceptionSqliteTest</c>, and the
///         options no longer ignore InMemory's <c>TransactionIgnoredWarning</c>.
///     </para>
/// </remarks>
public class MaterializationInterceptionInfoCarrierTest(NonSharedFixture fixture)
    : MaterializationInterceptionTestBase<MaterializationInterceptionInfoCarrierTest.InfoCarrierLibraryContext>(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(SqliteInfoCarrierTier.Instance);

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: projecting the owned collection needs <c>APPLY</c>, which SQLite does not have.
    ///     It answered on Tier A, where InMemory runs the projection.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/MaterializationInterceptionSqliteTest.cs", 11, 16,
        Justification = Upstream.GaveNoReason)]
    public override async Task Intercept_query_materialization_with_owned_types_projecting_collection(bool async, bool usePooling)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Intercept_query_materialization_with_owned_types_projecting_collection(async, usePooling))).Message);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

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

        // The model goes to the server; the interceptors do not (C71). `AddProviderOptions`
        // forwards the test's `onConfiguring` and `addServices` so the two models match (A49) —
        // and for *this* base those two parameters carry nothing but interceptors:
        // `SingletonInterceptorsTestBase.CreateContext` sets `onConfiguring` to
        // `o => o.AddInterceptors(interceptors)` or null, and `addServices` to the matching
        // registrations or null, and every test in the family goes through it. So forwarding them
        // registers the caller's hook on *both* EF instances, which is what B16's twelve
        // `Assert.Same` failures and A71's ten `AddInterceptors` refusals are.
        //
        // Not a statement about the product, which correctly lets a deployment hook either side
        // or both (B16): it is this harness declining to make a one-context spec base look like
        // two. Scoped to this class because only here is the forwarded payload *only* interceptors
        // — `PropertyValuesFixtureBase` registers one server-side on purpose and keeps it.
        _harness.Prepare(
            typeof(TContext), onModelCreating, addServices: null, onConfiguring: null, configureConventions, AddOptions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }

    /// <summary>
    ///     EF's own <c>SqliteLibraryContext</c>: the backing store is SQLite, so the owned
    ///     collection is mapped the way that provider's test maps one.
    /// </summary>
    public class InfoCarrierLibraryContext(DbContextOptions options) : LibraryContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestEntity30244>().OwnsMany(e => e.Settings, b => b.ToJson());
        }
    }
}
