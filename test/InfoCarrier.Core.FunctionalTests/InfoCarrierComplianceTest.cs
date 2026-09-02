// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Update;

namespace InfoCarrier.Core.FunctionalTests;

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
public class InfoCarrierComplianceTest : RelationalComplianceTestBase
{
    /// <inheritdoc />
    protected override Assembly TargetAssembly
        => typeof(InfoCarrierComplianceTest).Assembly;

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

        // Stored procedures, which SQLite does not have. EF's list carries the same entry.
        typeof(FromSqlSprocQueryTestBase<>),
        typeof(StoredProcedureUpdateTestBase),

        // Also stored procedures, despite the name. Its first three tests are
        // Executes_stored_procedure, _with_parameter and _with_generated_parameter, and every one
        // of them runs a sproc through Database.ExecuteSqlRaw. EF's list carries it beside the two
        // above for that reason, and D8 item 2 used to pair it with `SqlQuery<T>` -- which is a
        // client limitation, where this is a store one (R102).
        typeof(SqlExecutorTestBase<>),
    ];
}
