// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Update;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     The coverage scoreboard (ADR-004). Fails while any
///     <c>EFCore.Specification.Tests</c> base class has no InfoCarrier subclass, listing every
///     one that is missing.
/// </summary>
/// <remarks>
///     <para>
///         <strong>This test is expected to be red for a long time, and that is its job.</strong>
///         It converts "adopt the EF Core suite" from an unbounded intention into a generated,
///         auditable inventory: every base is either implemented or listed in
///         <see cref="IgnoredTestBases" /> with a stated reason. Nothing can be silently
///         forgotten.
///     </para>
///     <para>
///         <strong>Two things may be ignored, and they are different axes.</strong> A base
///         <em>conceptually inapplicable to a remoting provider</em> is the original category and
///         still the common one. The second, added 2026-09-02, is a base <strong>EF's own
///         <c>SqliteComplianceTest</c> ignores</strong>: this suite's only relational store is
///         SQLite (ADR-009 Tier B), so a base the reference provider declares out of scope for
///         SQLite has no store here to run on either. CLAUDE.md's bar for leaving a base unadopted
///         is "EF ships no test for it on any store we have", and that is exactly what EF's list
///         records. Each such entry names EF's reason rather than inventing one.
///     </para>
///     <para>
///         <strong>Aligning the two lists means aligning what is <em>missing</em>, not deleting
///         what is adopted.</strong> Five bases EF's SQLite list ignores are implemented here, and
///         four of those are green: EF ignores the <c>Owned*Projection*</c> family for its own
///         issue #26708 and <c>TPCRelationshipsQueryTestBase</c> for a test-infrastructure reason,
///         and neither reaches this provider. Listing an implemented base changes nothing, and
///         removing its class to match would delete passing coverage.
///     </para>
///     <para>
///         A base that is merely not built yet must stay out of the list so this test keeps
///         reporting it.
///     </para>
/// </remarks>
public class RelationalInfoCarrierComplianceTest : RelationalComplianceTestBase
{
    /// <inheritdoc />
    protected override Assembly TargetAssembly
        => typeof(RelationalInfoCarrierComplianceTest).Assembly;

    /// <inheritdoc />
    /// <remarks>
    ///     <b>The RELATIONAL bases only, and this override is what makes two compliance tests add
    ///     up to the one they replaced.</b> <c>RelationalComplianceTestBase</c> concatenates the
    ///     core specification assembly's bases onto the relational ones, which was right while a
    ///     single assembly held both tiers. It is wrong now: every core base adopted on Tier A
    ///     lives in <c>InfoCarrier.Core.FunctionalTests</c>, and requiring it here would report a
    ///     hundred bases missing that are not missing at all. Tier A's
    ///     <see cref="InfoCarrierComplianceTest" /> answers for the core bases; this one answers
    ///     for the relational ones; between them nothing is unaccounted for.
    /// </remarks>
    protected override IEnumerable<Type> GetBaseTestClasses()
        => typeof(RelationalComplianceTestBase).Assembly.ExportedTypes
            .Where(t => t.Name.Contains("TestBase", StringComparison.Ordinal));

    /// <summary>
    ///     Bases that are conceptually inapplicable to InfoCarrier — each with the reason.
    ///     Seeded in M1-I3; the relational entries were added for #56 in R2 and R45.
    /// </summary>
    /// <remarks>
    ///     A base that is merely unadopted must NOT be listed here. Everything below needs a
    ///     service or an object that does not exist on this side of the wire, and no amount of
    ///     work in this repository would change that.
    /// </remarks>
    protected override ICollection<Type> IgnoredTestBases { get; } =
    [
        // Migrations run DDL against a database. The client has none. The server is an ordinary
        // EF application, and EF already tests migrations for the provider it references.
        typeof(MigrationsInfrastructureTestBase<>),
        typeof(MigrationsTestBase<>),

        // Asserts the SQL an IMigrationsSqlGenerator emits, resolved from the context's services.
        typeof(MigrationsSqlGeneratorTestBase),

        // Same shape: CreateSqlGenerator() must return an IUpdateSqlGenerator.
        typeof(UpdateSqlGeneratorTestBase),

        // Asserts that EntityFrameworkRelationalServicesBuilder.RelationalServices are registered.
        // InfoCarrier.Core stopped referencing EFCore.Relational in M9 and registers none of them.
        typeof(RelationalServiceCollectionExtensionsTestBase),

        // Interception of DbCommand, DbConnection and DbTransaction. The client holds no ADO.NET
        // object to intercept. On the server this is ordinary EF, which EF tests.
        typeof(CommandInterceptionTestBase),
        typeof(ConnectionInterceptionTestBase),
        typeof(TransactionInterceptionTestBase),

        // Resolves IReverseEngineerScaffolder and IMigrationsScaffolder from the provider's
        // design-time services. Both scaffold from a database, which the client does not have.
        typeof(DesignTimeTestBase<>),

        // Precompiled queries pregenerate a provider's SQL at build time on the client. This
        // client compiles no SQL at all: the server generates it per request, after the wire.
        typeof(PrecompiledQueryRelationalTestBase),
        typeof(PrecompiledSqlPregenerationQueryRelationalTestBase),
        typeof(AdHocPrecompiledQueryRelationalTestBase),

        // ---- Added in R45, each read in R41 before it was listed. ----

        // GetDbConnection(), GetDbTransaction(), UseTransaction(DbTransaction) and a
        // (RelationalTestStore)Fixture.TestStore cast run through all 44 of its tests. The client
        // has no database and no connection. Same reason as TransactionInterceptionTestBase above.
        typeof(TransactionTestBase<>),

        // Declares `protected abstract string DummyConnectionString` and
        // CreateBackingContext(string databaseName), and its three tests swap one connection
        // string for another inside a DbConnection interceptor. A connection string names a
        // database; the client has neither.
        typeof(TwoDatabasesTestBase),

        // Cannot even be closed here: TExtension is constrained to RelationalOptionsExtension and
        // TBuilder to RelationalDbContextOptionsBuilder<,>. This provider's options extension is
        // neither, and every one of its nine tests configures MaxBatchSize, CommandTimeout,
        // UseRelationalNulls, MigrationsAssembly or MigrationsHistoryTable through them.
        typeof(LoggingRelationalTestBase<,>),

        // Its whole contribution over the core base is GetModelMetadata, overridden as
        // new RelationalModelMetadata(context.Model, context.Database.GenerateCreateScript()).
        // GenerateCreateScript is relational-only, and every test routes through it.
        typeof(ModelBuilding101RelationalTestBase),

        // Asserts GetTableName() on the compiled model, which here is the *client's*. Its eleven
        // tests build models with ToTable, SplitToTable, sprocs, sequences and check constraints.
        // M9 removed the relational model from the client; this is that boundary, not a gap.
        typeof(CompiledModelRelationalTestBase),

        // Same boundary. AssertElementFacets asserts FindRelationalTypeMapping(), IsFixedLength()
        // and GetStoreType() on the client's model, and the store types it expects are the backing
        // provider's. R23 measured 104 red of 576 on exactly that assumption and reverted.
        typeof(JsonTypesRelationalTestBase),

        // ---- Added in R103. THE SECOND CATEGORY: EF's own SqliteComplianceTest ignores these,
        // ---- and SQLite is the only relational store this suite has (ADR-009 Tier B). Only
        // ---- SQL Server implements any of the three. The reasons below are EF's, not ours.
        // ---- The other five bases on EF's list are implemented here and stay implemented; see
        // ---- the class remarks.

        // ---- Added in R105, each re-read before it was listed and none taken on trust. All three
        // ---- are the FIRST category: the client is not relational. They had been reported as
        // ---- missing with no recorded reason, which is the one state this gate is meant to make
        // ---- impossible.

        // All 136 of its tests run through `TestHelpers.ExecuteWithStrategyInTransactionAsync`,
        // and the `UseTransaction` they hand it is declared on the test base itself as
        // `public void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        //     => facade.UseTransaction(transaction.GetDbTransaction());`
        // -- NON-virtual, so no fixture can replace it with `UseInfoCarrierTransaction`, and
        // `GetDbTransaction()` needs a relational client. This is ADR-013's own worked example
        // ("One use costs a test; 136 costs the base"), re-checked against EF 10 rather than
        // carried over: the count is still 136 of 136 and the method is still non-virtual.
        typeof(JsonUpdateTestBase<>),

        // `StoreValueGenerationFixtureBase.OnModelCreating` opens with
        // `context.GetService<ISqlGenerationHelper>()` and builds every computed column from it.
        // That service lives in `EFCore.Relational`, and this harness runs a fixture's
        // `OnModelCreating` on BOTH sides, so the client throws before a test runs. Blocked by
        // the same thing as `SqlQuery<T>` (D8 item 2, R102) and by nothing smaller.
        typeof(StoreValueGenerationTestBase<>),

        // Its two abstract members are `SetQuerySplittingBehavior` and
        // `ClearQuerySplittingBehavior`, and EF's SQLite class implements them by configuring a
        // `RelationalOptionsExtension` on the CLIENT's options builder -- the second by writing a
        // private field through reflection. A remoting client has no such extension.
        //
        // **The reason recorded until now was narrower and wrong.** ADR-013's R77 amendment said
        // this base "calls CloseConnection() on the cast store", which is true of exactly one test
        // of ten and would have cost a test rather than the base under R14's rule. The blocker is
        // the required surface, not that one call. Its subject is moot here as well:
        // `QuerySplitter`'s `SplitHintStrippingVisitor` removes `AsSplitQuery` on purpose.
        typeof(AdHocQuerySplittingQueryTestBase),

        // Stored procedures, which SQLite does not have. EF's list carries the same entry.
        typeof(FromSqlSprocQueryTestBase<>),
        typeof(StoredProcedureUpdateTestBase),

        // Also stored procedures, despite the name. Its first three tests are
        // Executes_stored_procedure, _with_parameter and _with_generated_parameter, and every one
        // of them runs a sproc through Database.ExecuteSqlRaw. EF's list carries it beside the two
        // above for that reason, and D8 item 2 used to pair it with `SqlQuery<T>` -- which is a
        // client limitation, where this is a store one (R102).
        typeof(SqlExecutorTestBase<>),

        // ---- Added in R122, when the test projects split. ----

        // FOURTEEN LINES AND NO TESTS OF ITS OWN, which is what decided this. Its whole
        // contribution over the core `SpatialQueryTestBase` is a `RelationalQueryAsserter`, and
        // the only thing that asserter does differently is call `TestSqlLoggerFactory.OutputSql()`
        // when an assertion fails -- on a client that has no database and emits no SQL.
        //
        // R50 had adopted it on Tier A, where the backing store is InMemory. The split ended that:
        // Tier A cannot reference the relational specification assembly, because a relational
        // client over a non-relational backend is the disagreement the seam exists to prevent.
        // Adopting it HERE instead would need SpatiaLite -- EF's own `SpatialQuerySqliteTest`
        // derives from it and this repository references no `Sqlite.NetTopologySuite` -- so the
        // base would bring a native dependency to gain nothing it declares.
        //
        // THE COVERAGE DID NOT MOVE. `SpatialQueryInfoCarrierTest` still runs, on Tier A, against
        // the core base: 168 of 168, unchanged by the swap, because the relational base declared
        // no tests to lose.
        typeof(SpatialQueryRelationalTestBase<>),
    ];
}
