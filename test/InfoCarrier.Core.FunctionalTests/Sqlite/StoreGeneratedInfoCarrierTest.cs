// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>StoreGeneratedTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         What happens to a property the <em>store</em> fills in — an identity key, a column with
///         a default, a computed value — through every combination of "before save" and "after
///         save" behaviour. It is squarely this provider's business, because a store-generated
///         value is produced on the far side of the wire and has to come back: the client's
///         <c>SaveChanges</c> ships a change set and must return with the values the server's
///         database chose.
///     </para>
///     <para>
///         <b>Tier B</b>, by the rule A79 established. This base was on the do-not-adopt list for
///         the superseded reason — EF's InMemory suite does not derive from it — and EF ships
///         <c>StoreGeneratedSqliteTest</c>. A store with no generated values cannot host a suite
///         about generated values.
///     </para>
///     <para>
///         It also needs B5: <c>StoreGeneratedFixtureBase</c> registers its three dozen wrapped-key
///         value converters in <c>ConfigureConventions</c> and nowhere else, so before B5 the
///         server would have built a different model from the client's.
///     </para>
/// </remarks>
public class StoreGeneratedInfoCarrierTest(StoreGeneratedInfoCarrierTest.StoreGeneratedInfoCarrierFixture fixture)
    : StoreGeneratedTestBase<StoreGeneratedInfoCarrierTest.StoreGeneratedInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     EF's own <c>StoreGeneratedSqliteTest</c> skips this one — SQLite has no computed
    ///     columns — and the reason is the backing store's, so it is ours too.
    /// </remarks>
    public override Task Fields_used_correctly_for_store_generated_values()
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>
    ///     The base runs each test inside a transaction and makes a second context observe the
    ///     same uncommitted state; without enlisting, the second runs on its own SQLite connection
    ///     and gets "database is locked". The same hook <c>ConferencePlanner</c> and
    ///     <c>OptimisticConcurrency</c> need.
    /// </remarks>
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    public class StoreGeneratedInfoCarrierFixture : StoreGeneratedFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "StoreGeneratedInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     EF's <c>StoreGeneratedSqliteFixture</c>'s, minus the parts that belong to a client
        ///     with no database. <c>BoolWithDefaultWarning</c> is ignored because the model below
        ///     gives <c>bool</c> columns defaults, which is what the warning is about.
        /// </remarks>
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => builder
                .EnableSensitiveDataLogging()
                .ConfigureWarnings(b => b.Default(WarningBehavior.Throw)
                    .Ignore(CoreEventId.SensitiveDataLoggingEnabledWarning)
                    .Ignore(RelationalEventId.BoolWithDefaultWarning));

        /// <inheritdoc />
        /// <remarks>
        ///     Copied from EF's <c>StoreGeneratedSqliteFixture</c>. A store-generated value has to
        ///     come from somewhere, and on SQLite that somewhere is a column default — which is
        ///     why this configuration is the backing store's to state and not something this
        ///     provider could invent. Mirrored, with the reason, rather than written afresh.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.Entity<Gumball>(b =>
            {
                b.Property(e => e.Identity).HasDefaultValue("Banana Joe");
                b.Property(e => e.IdentityReadOnlyBeforeSave).HasDefaultValue("Doughnut Sheriff");
                b.Property(e => e.IdentityReadOnlyAfterSave).HasDefaultValue("Anton");
                b.Property(e => e.AlwaysIdentity).HasDefaultValue("Banana Joe");
                b.Property(e => e.AlwaysIdentityReadOnlyBeforeSave).HasDefaultValue("Doughnut Sheriff");
                b.Property(e => e.AlwaysIdentityReadOnlyAfterSave).HasDefaultValue("Anton");
                b.Property(e => e.Computed).HasDefaultValue("Alan");
                b.Property(e => e.ComputedReadOnlyBeforeSave).HasDefaultValue("Carmen");
                b.Property(e => e.ComputedReadOnlyAfterSave).HasDefaultValue("Tina Rex");
                b.Property(e => e.AlwaysComputed).HasDefaultValue("Alan");
                b.Property(e => e.AlwaysComputedReadOnlyBeforeSave).HasDefaultValue("Carmen");
                b.Property(e => e.AlwaysComputedReadOnlyAfterSave).HasDefaultValue("Tina Rex");
            });

            modelBuilder.Entity<Anais>(b =>
            {
                b.Property(e => e.OnAdd).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddUseBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddIgnoreBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddThrowBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddUseBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddIgnoreBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddThrowBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddUseBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddIgnoreBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddThrowBeforeThrowAfter).HasDefaultValue("Rabbit");

                b.Property(e => e.OnAddOrUpdate).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateUseBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateIgnoreBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateThrowBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateUseBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateIgnoreBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateThrowBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateUseBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateIgnoreBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnAddOrUpdateThrowBeforeThrowAfter).HasDefaultValue("Rabbit");

                b.Property(e => e.OnUpdate).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateUseBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateIgnoreBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateThrowBeforeUseAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateUseBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateIgnoreBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateThrowBeforeIgnoreAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateUseBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateIgnoreBeforeThrowAfter).HasDefaultValue("Rabbit");
                b.Property(e => e.OnUpdateThrowBeforeThrowAfter).HasDefaultValue("Rabbit");
            });

            modelBuilder.Entity<WithNoBackingFields>(b =>
            {
                b.Property(e => e.TrueDefault).HasDefaultValue(true);
                b.Property(e => e.NonZeroDefault).HasDefaultValue(-1);
                b.Property(e => e.FalseDefault).HasDefaultValue(false);
                b.Property(e => e.ZeroDefault).HasDefaultValue(0);
            });

            modelBuilder.Entity<WithNullableBackingFields>(b =>
            {
                b.Property(e => e.NullableBackedBoolTrueDefault).HasDefaultValue(true);
                b.Property(e => e.NullableBackedIntNonZeroDefault).HasDefaultValue(-1);
                b.Property(e => e.NullableBackedBoolFalseDefault).HasDefaultValue(false);
                b.Property(e => e.NullableBackedIntZeroDefault).HasDefaultValue(0);
            });

            modelBuilder.Entity<WithObjectBackingFields>(b =>
            {
                b.Property(e => e.NullableBackedBoolTrueDefault).HasDefaultValue(true);
                b.Property(e => e.NullableBackedIntNonZeroDefault).HasDefaultValue(-1);
                b.Property(e => e.NullableBackedBoolFalseDefault).HasDefaultValue(false);
                b.Property(e => e.NullableBackedIntZeroDefault).HasDefaultValue(0);
            });

            modelBuilder.Entity<NonStoreGenDependent>().Property(e => e.HasTemp).HasDefaultValue(777);

            base.OnModelCreating(modelBuilder, context);
        }
    }
}
