// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestModels.ManyToManyModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>ManyToManyTrackingRelationalTestBase</c> on ADR-009 <b>Tier B</b>, mirroring EF's own
///     <c>ManyToManyTrackingSqliteTest</c>.
/// </summary>
/// <remarks>
///     <para>
///         The join entity of a many-to-many has foreign keys but no navigations to link through,
///         so a skip navigation's changes reach the wire as join rows whose keys are both
///         placeholders. S3c-9 built the placeholder machinery on exactly that case; this is the
///         spec coverage for it.
///     </para>
///     <para>
///         <b>R35 moved this from Tier A to Tier B and onto the relational base.</b> R16 examined
///         the move and deferred it; what makes it worth taking now is that the move is what makes
///         the tests <em>real</em>, which is R13a's lesson. Two things this class used to assert
///         about itself stop being true on a real database, and both are deleted rather than
///         carried:
///     </para>
///     <para>
///         <b>The <c>ExecuteWithStrategyInTransactionAsync</c> reseed is gone.</b> Its remark said
///         <i>"without a real transaction there is no rollback to undo the test's mutations"</i> —
///         true of the InMemory store and false here. The base routes <b>47</b> call sites through
///         that helper, and on Tier B each one opens a real transaction that really rolls back.
///     </para>
///     <para>
///         <b><c>SupportsDatabaseDefaults => false</c> is gone too</b>, for the same reason: it
///         said <i>"the backend is the InMemory store, which has no database default values"</i>.
///         SQLite has them, and the fixture below now declares the six EF's own SQLite fixture
///         declares.
///     </para>
/// </remarks>
public class ManyToManyTrackingInfoCarrierTest(ManyToManyTrackingInfoCarrierTest.InfoCarrierFixture fixture)
    : ManyToManyTrackingRelationalTestBase<ManyToManyTrackingInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     <b>Written in the same commit as the store switch, which is the whole point of
    ///     CLAUDE.md's D6 rule.</b> The base declares
    ///     <c>UseTransaction(facade, t) => facade.UseTransaction(t.GetDbTransaction())</c>, and its
    ///     <c>ExecuteWithStrategyInTransactionAsync</c> opens <em>one</em> transaction that every
    ///     other context must then enlist in. On Tier A that transaction was ignored and nothing
    ///     showed; here it is real, and without this override the inner contexts would sit outside
    ///     it while the outer one held the store's write lock — the 471-lock-timeout shape D6
    ///     records. The tell was the base's own transaction strategy: 47 call sites.
    /// </remarks>
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    public class InfoCarrierFixture : ManyToManyTrackingRelationalFixture, ITestSqlLoggerFactory
    {
        private ITestStoreFactory _testStoreFactory;

        public TestSqlLoggerFactory TestSqlLoggerFactory
            => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override string StoreName
            => "ManyToManyTrackingInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

        /// <inheritdoc />
        /// <remarks>
        ///     <para>
        ///         The six default-value declarations are <c>ManyToManyTrackingSqliteTest</c>'s,
        ///         and the base's <c>ToTable</c> table-sharing pair comes from
        ///         <c>ManyToManyTrackingRelationalFixture</c> above rather than by hand.
        ///     </para>
        ///     <para>
        ///         <b>Safe to state here, and the reason matters (B4/B6/B12).</b>
        ///         <c>HasDefaultValue</c> and <c>HasDefaultValueSql</c> are modelling APIs, so both
        ///         the client's model and the server's make the statement for themselves out of
        ///         this one <c>OnModelCreating</c> — it is not a value one side computes from a
        ///         type mapping and the other has to agree with. <c>CURRENT_TIMESTAMP</c> is
        ///         SQLite's spelling and the server is the only side that ever sends it to a
        ///         store.
        ///     </para>
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder
                .Entity<JoinOneSelfPayload>()
                .Property(e => e.Payload)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder
                .SharedTypeEntity<Dictionary<string, object>>("JoinOneToThreePayloadFullShared")
                .IndexerProperty<string>("Payload")
                .HasDefaultValue("Generated");

            modelBuilder
                .Entity<JoinOneToThreePayloadFull>()
                .Property(e => e.Payload)
                .HasDefaultValue("Generated");

            modelBuilder
                .Entity<UnidirectionalJoinOneSelfPayload>()
                .Property(e => e.Payload)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder
                .SharedTypeEntity<Dictionary<string, object>>("UnidirectionalJoinOneToThreePayloadFullShared")
                .IndexerProperty<string>("Payload")
                .HasDefaultValue("Generated");

            modelBuilder
                .Entity<UnidirectionalJoinOneToThreePayloadFull>()
                .Property(e => e.Payload)
                .HasDefaultValue("Generated");
        }

        /// <summary>
        ///     Reseeds through the <em>backend</em> context rather than the client one.
        /// </summary>
        /// <remarks>
        ///     The base seeds through a client context, which would make every test's setup
        ///     depend on remoted SaveChanges — the thing under test. The initial seed already
        ///     runs server-side, so this keeps seeding to one mechanism.
        ///     <c>GraphUpdatesInfoCarrierTest</c> does the same.
        /// </remarks>
        public override async Task ReseedAsync()
        {
            InfoCarrierBackendTestStore backend = ((InfoCarrierTestStore)TestStore).Backend;
            using DbContext context = backend.CreateDbContext();
            await backend.CleanAsync(context);
            await CleanAsync(context);
            await SeedAsync((ManyToManyContext)context);
        }
    }
}
