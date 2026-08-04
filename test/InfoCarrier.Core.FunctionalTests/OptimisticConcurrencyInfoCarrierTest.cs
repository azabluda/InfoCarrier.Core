// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestModels.ConcurrencyModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>OptimisticConcurrencyTestBase</c> on ADR-009 Tier B — the SQLite backend, the only tier
///     that can make a concurrency check at all.
/// </summary>
/// <remarks>
///     <para>
///         Tier A is deliberately not adopted: EF's InMemory provider performs no concurrency
///         check, which is why EF's own <c>OptimisticConcurrencyInMemoryTest</c> skips sixteen of
///         these tests. Running them here would assert InMemory's limits, not this provider's.
///     </para>
///     <para>
///         The skipped tests below are EF's own <c>OptimisticConcurrencySqliteTestBase</c> skips,
///         mirrored one for one — the backend <em>is</em> SQLite, so its limits are ours.
///     </para>
/// </remarks>
public class OptimisticConcurrencyInfoCarrierTest(OptimisticConcurrencyInfoCarrierTest.InfoCarrierFixture fixture)
    : OptimisticConcurrencyTestBase<OptimisticConcurrencyInfoCarrierTest.InfoCarrierFixture, byte[]>(fixture),
        IAsyncLifetime
{
    /// <summary>
    ///     Joins the second context of a concurrency test to the transaction the first began.
    /// </summary>
    /// <remarks>
    ///     The base opens a transaction on one context and then makes a *second* one observe the
    ///     same uncommitted state — that is the whole shape of a concurrency test. While
    ///     transactions were ignored (pre-M4) this hook could stay empty, because nothing was
    ///     isolated from anything. Now that the server holds a real one on Tier B, an unenlisted
    ///     second context runs on its own SQLite connection and gets "database is locked".
    /// </remarks>
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    /// <summary>
    ///     Restores the store before every test.
    /// </summary>
    /// <remarks>
    ///     Every test in this base wraps its work in a transaction and relies on the rollback to
    ///     undo it. This provider ignores transactions (roadmap M4), so nothing is undone: the
    ///     first test that deletes a row leaves it deleted, and the next one to want it fails with
    ///     "sequence contains no elements" — a symptom of the missing rollback and not of anything
    ///     the test is about. <c>GraphUpdatesInfoCarrierTest</c> reseeds for the same reason;
    ///     here there is no <c>ExecuteWithStrategyInTransactionAsync</c> to hang it on, so xUnit's
    ///     per-test lifetime is the hook.
    /// </remarks>
    public Task InitializeAsync()
        => Fixture.ReseedAsync();

    /// <inheritdoc />
    public Task DisposeAsync()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task Simple_concurrency_exception_can_be_resolved_with_store_values()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task Simple_concurrency_exception_can_be_resolved_with_client_values()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task Simple_concurrency_exception_can_be_resolved_with_new_values()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task Simple_concurrency_exception_can_be_resolved_with_store_values_using_equivalent_of_accept_changes()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task Simple_concurrency_exception_can_be_resolved_with_store_values_using_Reload()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task Updating_then_deleting_the_same_entity_results_in_DbUpdateConcurrencyException()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task
        Updating_then_deleting_the_same_entity_results_in_DbUpdateConcurrencyException_which_can_be_resolved_with_store_values()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task
        Change_in_independent_association_after_change_in_different_concurrency_token_results_in_independent_association_exception()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task Change_in_independent_association_results_in_independent_association_exception()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task Two_concurrency_issues_in_one_to_many_related_entities_can_be_handled_by_dealing_with_dependent_first()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Optimistic Offline Lock #2195")]
    public override Task Two_concurrency_issues_in_one_to_one_related_entities_can_be_handled_by_dealing_with_dependent_first()
        => Task.CompletedTask;

    /// <summary>
    ///     The F1 fixture, wired to a SQLite backend behind the wire.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>F1FixtureBase</c> builds its model <em>externally</em> and applies it with
    ///         <c>UseModel</c>, and <c>F1Context</c> has no <c>OnModelCreating</c> of its own — so
    ///         the usual <c>OnModelCreating</c> route this repo's stores take would leave the
    ///         server with a bare convention model and none of the concurrency tokens. The server
    ///         is therefore handed its own copy of the model, built over SQLite's convention set
    ///         so it carries the relational annotations the client's must not have.
    ///     </para>
    ///     <para>
    ///         The relational configuration in <see cref="BuildServerModel" /> is EF's
    ///         <c>F1RelationalFixture.BuildModelExternal</c>. It is duplicated rather than
    ///         inherited because that class ships in
    ///         <c>Microsoft.EntityFrameworkCore.Relational.Specification.Tests</c>, which a
    ///         non-relational provider has no business referencing.
    ///     </para>
    /// </remarks>
    public class InfoCarrierFixture : F1FixtureBase<byte[]>
    {
        private ITestStoreFactory _testStoreFactory;

        public override TestHelpers TestHelpers
            => InfoCarrierTestHelpers.Instance;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                onAddOptions: b => b
                    .UseModel(BuildServerModel())
                    .UseSeeding((c, _) =>
                    {
                        if (((F1Context)c).EngineSuppliers.Any())
                        {
                            return;
                        }

                        F1Context.AddSeedData((F1Context)c);
                        c.SaveChanges();
                    })
                    .UseAsyncSeeding(async (c, _, t) =>
                    {
                        if (await ((F1Context)c).EngineSuppliers.AnyAsync(t))
                        {
                            return;
                        }

                        F1Context.AddSeedData((F1Context)c);
                        await c.SaveChangesAsync(t);
                    }),
                onAddServices: s => s.AddSingleton<ISingletonInterceptor, F1MaterializationInterceptor>(),
                configureConventions: ConfigureConventions);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            // Mirrors F1InMemoryFixtureBase, which ignores InMemory's equivalent warning: every
            // test here opens a transaction, and this provider ignores transactions (roadmap M4).
            => base.AddOptions(builder)
                .ConfigureWarnings(w => w.Ignore(InfoCarrierEventId.TransactionIgnoredWarning));

        /// <summary>
        ///     Rebuilds the store from scratch, server-side.
        /// </summary>
        /// <remarks>
        ///     The base reseeds through a <em>client</em> context, which would make every test's
        ///     setup depend on remoted SaveChanges — the thing under test. Recreating the schema
        ///     re-runs the <c>UseSeeding</c> hook configured on the server options above, which is
        ///     the one mechanism that seeds this store.
        /// </remarks>
        public override async Task ReseedAsync()
        {
            InfoCarrierBackendTestStore backend = ((InfoCarrierTestStore)TestStore).Backend;
            using DbContext context = backend.CreateDbContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        /// <summary>
        ///     The same model as the client's, plus the relational mapping the store needs.
        /// </summary>
        private IModel BuildServerModel()
        {
            ModelBuilder builder = SqliteConventionSetBuilder.CreateModelBuilder();

            BuildModelExternal(builder);

            builder.Entity<Chassis>().ToTable("Chassis");
            builder.Entity<Team>().ToTable("Teams").Property(e => e.Id).ValueGeneratedNever();
            builder.Entity<Driver>().ToTable("Drivers");
            builder.Entity<Engine>().ToTable("Engines");
            builder.Entity<EngineSupplier>().ToTable("EngineSuppliers");
            builder.Entity<Gearbox>().ToTable("Gearboxes");
            builder.Entity<Sponsor>().ToTable("Sponsors");

            builder.Entity<Fan>().Property(e => e.Id).ValueGeneratedNever();
            builder.Entity<FanTpt>(b =>
            {
                b.UseTptMappingStrategy();
                b.Property(e => e.Id).ValueGeneratedNever();
            });
            builder.Entity<FanTpc>(b =>
            {
                b.UseTpcMappingStrategy();
                b.Property(e => e.Id).ValueGeneratedNever();
            });

            builder.Entity<Circuit>(b =>
            {
                b.ToTable("Circuits");
                b.Property(e => e.Name).HasColumnName("Name");
                b.Property(e => e.Id).ValueGeneratedNever();
            });
            builder.Entity<City>(b =>
            {
                b.ToTable("Circuits");
                b.Property(e => e.Name).HasColumnName("Name");
                b.Property(e => e.Id).ValueGeneratedNever();
            });

            return (IModel)builder.Model;
        }
    }
}
