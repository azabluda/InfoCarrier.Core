// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <c>OwnedQueryTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     An owned entity type has no identity of its own: it is addressed through its owner, and the
///     wire has to carry it that way (ADR-008). Every override below is EF's own
///     <c>OwnedQueryInMemoryTest</c> — operators over an owned *collection* that the InMemory store
///     cannot compose, and this backing store is that store.
/// </remarks>
public class OwnedQueryInfoCarrierTest(OwnedQueryInfoCarrierTest.OwnedQueryInfoCarrierFixture fixture)
    : OwnedQueryTestBase<OwnedQueryInfoCarrierTest.OwnedQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     The owned-query fixture, wired to an InMemory backend behind the wire. Nested, because
    ///     <c>OwnedQueryFixtureBase</c> is nested in the base it belongs to — EF's own InMemory
    ///     class nests its fixture for the same reason.
    /// </summary>
    public class OwnedQueryInfoCarrierFixture : OwnedQueryFixtureBase, ITestSqlLoggerFactory
    {
        /// <summary>
        ///     The compliance gate's second assertion (R54). The property is real —
        ///     <c>InfoCarrierTestStoreFactory.CreateListLoggerFactory</c> returns a
        ///     <c>TestSqlLoggerFactory</c> — but what it observes is the <em>client's</em> log, and
        ///     this client has no database and emits no SQL. <c>ServerSqlLog</c> is where the
        ///     server's statements can actually be read.
        /// </summary>
        public TestSqlLoggerFactory TestSqlLoggerFactory
            => (TestSqlLoggerFactory)ListLoggerFactory;

        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }

    /// <inheritdoc />
    public override Task Contains_over_owned_collection(bool async)
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Contains_over_owned_collection(async));

    /// <inheritdoc />
    public override Task ElementAt_over_owned_collection(bool async)
        => AssertTranslationFailed(() => base.ElementAt_over_owned_collection(async));

    /// <inheritdoc />
    public override Task ElementAtOrDefault_over_owned_collection(bool async)
        => AssertTranslationFailed(() => base.ElementAt_over_owned_collection(async));

    /// <inheritdoc />
    public override Task FirstOrDefault_over_owned_collection(bool async)
        => Assert.ThrowsAsync<NullReferenceException>(() => base.FirstOrDefault_over_owned_collection(async));

    /// <inheritdoc />
    public override Task OrderBy_ElementAt_over_owned_collection(bool async)
        => AssertTranslationFailed(() => base.OrderBy_ElementAt_over_owned_collection(async));
}

/// <summary>
///     <c>SharedTypeQueryTestBase</c> on Tier A — a model whose entity types are keyed by name
///     rather than by CLR type, so several share one <see cref="System.Collections.Generic.Dictionary{TKey,TValue}" />.
/// </summary>
/// <remarks>
///     Non-shared-model, so it goes through <see cref="NonSharedModelInfoCarrierHarness" /> (A49).
///     EF's InMemory class adds a <c>ToInMemoryQuery</c> test of its own; that is the store's, not
///     the spec base's, and is not carried (A47).
/// </remarks>
public class SharedTypeQueryInfoCarrierTest(NonSharedFixture fixture)
    : SharedTypeQueryTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.InMemory);

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
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddOptions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}

/// <inheritdoc cref="SharedTypeQueryInfoCarrierTest" />
public class OwnedEntityQueryInfoCarrierTest(NonSharedFixture fixture)
    : OwnedEntityQueryTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.InMemory);

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
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddOptions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
