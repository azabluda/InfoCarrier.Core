// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>TPTInheritanceQueryTestBase</c> on ADR-009 <b>Tier B</b> — the first table-per-type
///     query coverage in this repository, and the probe that prices the other nine TPT and TPC
///     bases (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Tier B and not Tier A.</b> TPT is a relational mapping and ADR-009 puts a base on
///         the one tier that translates it. InMemory cannot host one table per type.
///     </para>
///     <para>
///         <b>The <c>UseTransaction</c> override is required, and the grep that says otherwise is
///         reading the wrong file.</b> None of the ten TPT and TPC bases mentions
///         <c>ExecuteWithStrategyInTransactionAsync</c>, but <c>InheritanceQueryTestBase</c>,
///         which this one inherits, uses it. On Tier A the transaction is ignored and nothing
///         shows; on Tier B it is real, and without this override the inner contexts stay outside
///         the transaction the outer one holds the write lock for. EF's own
///         <c>TPTInheritanceQuerySqliteTest</c> overrides the same method with
///         <c>transaction.GetDbTransaction()</c>, which is ADR-013's trap: that call needs a
///         relational client, and this client is not one.
///     </para>
///     <para>
///         <b>The fixture needs no work to satisfy <c>ITestSqlLoggerFactory</c>.</b>
///         <c>TPTInheritanceQueryFixture</c> implements it by casting <c>ListLoggerFactory</c>,
///         and <c>InfoCarrierTestStoreFactory.CreateListLoggerFactory</c> already returns a
///         <c>TestSqlLoggerFactory</c> for exactly that reason. No test in this base calls
///         <c>AssertSql</c>, so nothing here asserts a golden string.
///     </para>
/// </remarks>
public class TPTInheritanceQueryInfoCarrierTest(
    TPTInheritanceQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : TPTInheritanceQueryTestBase<TPTInheritanceQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <inheritdoc />
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

/// <summary>
///     The TPT inheritance fixture, wired to a SQLite backend behind the wire.
/// </summary>
/// <remarks>
///     No <c>serverContextType</c>, unlike the Tier A inheritance fixture. That one exists to give
///     the server an InMemory defining query for the keyless <c>AnimalQuery</c>;
///     <c>TPTInheritanceQueryFixture</c> ignores the keyless types outright, because EF maps them
///     to TPH.
/// </remarks>
public class TPTInheritanceQueryInfoCarrierFixture : TPTInheritanceQueryFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}
