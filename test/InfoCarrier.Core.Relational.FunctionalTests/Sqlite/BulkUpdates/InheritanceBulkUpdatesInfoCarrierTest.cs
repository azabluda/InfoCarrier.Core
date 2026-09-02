// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.BulkUpdates;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.BulkUpdates;

/// <summary>
///     <c>ExecuteUpdate</c> and <c>ExecuteDelete</c> over the three inheritance mappings, filtered
///     and unfiltered, on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>These write, where every inheritance base adopted so far only read.</b> A bulk
///         update against a hierarchy has to name the right store object for the right derived
///         type, and under TPT and TPC there is more than one. Six classes close seven entries on
///         the compliance list, because <c>TPHInheritanceBulkUpdatesTestBase</c> and
///         <c>FiltersInheritanceBulkUpdatesRelationalTestBase</c> are on the same chain.
///     </para>
///     <para>
///         <b>The <c>UseTransaction</c> override is on the FIXTURE here, and finding that mattered
///         more than anything else in this file.</b> Every other base in this repository declares
///         it on the test class, so EF's own SQLite bulk-update classes not overriding it reads at
///         first like evidence that no transaction is involved. The opposite is true:
///         <c>BulkUpdatesAsserter.AssertDelete</c> calls
///         <c>TestHelpers.ExecuteWithStrategyInTransactionAsync</c> on every assertion, and
///         <c>InheritanceBulkUpdatesFixtureBase</c> declares <c>UseTransaction</c> <b>abstract</b>
///         so the fixture supplies it. Inheriting EF's relational implementation would call
///         <c>GetDbTransaction()</c>, which ADR-013 makes unreachable on this client, and the
///         symptom would have been the documented one: every inner context outside the transaction
///         the outer one holds the write lock for, and a run full of
///         <c>SQLite Error 5: 'database is locked'</c>.
///     </para>
///     <para>
///         <b>No golden strings.</b> EF's SQLite classes override every test with <c>AssertSql</c>
///         and assert <c>Check_all_tests_overridden</c>; those live in the provider's subclass, not
///         in the base. Adopting the base directly takes the behaviour and leaves the dialect
///         behind, which is the distinction plan item R3 turns on.
///     </para>
/// </remarks>
public class TPHInheritanceBulkUpdatesInfoCarrierTest(
    TPHInheritanceBulkUpdatesInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : TPHInheritanceBulkUpdatesTestBase<TPHInheritanceBulkUpdatesInfoCarrierFixture>(fixture, testOutputHelper);

/// <inheritdoc cref="TPHInheritanceBulkUpdatesInfoCarrierTest" />
public class TPTInheritanceBulkUpdatesInfoCarrierTest(
    TPTInheritanceBulkUpdatesInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : TPTInheritanceBulkUpdatesTestBase<TPTInheritanceBulkUpdatesInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <inheritdoc />
    /// <remarks>
    ///     EF's own override, for EF issue <b>#31402</b>: a TPT <c>ExecuteUpdate</c> that reaches a
    ///     base property generates SQL naming a column the derived table does not have. EF asserts
    ///     <c>SqliteException</c>; across this wire the same failure arrives wrapped, so the
    ///     assertion is on the wrapped type name and the engine's own message, which is stronger
    ///     than asserting the wrapper alone. The base test still runs and still fails in SQLite.
    /// </remarks>
    public override Task Update_base_property_on_derived_type(bool async)
        => AssertStoreRefuses(() => base.Update_base_property_on_derived_type(async));

    /// <inheritdoc />
    /// <remarks>EF issue <b>#31402</b>, the same defect reached by a different query.</remarks>
    public override Task Update_base_type_with_OfType(bool async)
        => AssertStoreRefuses(() => base.Update_base_type_with_OfType(async));

    internal static async Task AssertStoreRefuses(Func<Task> query)
    {
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(query);

        Assert.Equal(typeof(SqliteException).FullName, exception.ServerExceptionTypeName);
        Assert.Contains("no such column", exception.Message);
    }
}

/// <inheritdoc cref="TPHInheritanceBulkUpdatesInfoCarrierTest" />
public class TPCInheritanceBulkUpdatesInfoCarrierTest(
    TPCInheritanceBulkUpdatesInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : TPCInheritanceBulkUpdatesTestBase<TPCInheritanceBulkUpdatesInfoCarrierFixture>(fixture, testOutputHelper);

/// <summary>
///     The TPH filtered variant. Its base is the shared
///     <c>FiltersInheritanceBulkUpdatesRelationalTestBase</c> rather than a TPH-specific one, which
///     is why adopting it closes that entry too.
/// </summary>
public class TPHFiltersInheritanceBulkUpdatesInfoCarrierTest(
    TPHFiltersInheritanceBulkUpdatesInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : FiltersInheritanceBulkUpdatesRelationalTestBase<TPHFiltersInheritanceBulkUpdatesInfoCarrierFixture>(
        fixture,
        testOutputHelper)
{
    /// <inheritdoc />
    /// <remarks>Nothing here asserts SQL, so there is no log to clear.</remarks>
    protected override void ClearLog()
    {
    }
}

/// <inheritdoc cref="TPHInheritanceBulkUpdatesInfoCarrierTest" />
public class TPTFiltersInheritanceBulkUpdatesInfoCarrierTest(
    TPTFiltersInheritanceBulkUpdatesInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : TPTFiltersInheritanceBulkUpdatesTestBase<TPTFiltersInheritanceBulkUpdatesInfoCarrierFixture>(
        fixture,
        testOutputHelper)
{
    /// <inheritdoc />
    /// <remarks>Nothing here asserts SQL, so there is no log to clear.</remarks>
    protected override void ClearLog()
    {
    }

    /// <inheritdoc />
    /// <remarks>EF issue <b>#31402</b>, as on the unfiltered TPT class.</remarks>
    public override Task Update_base_property_on_derived_type(bool async)
        => TPTInheritanceBulkUpdatesInfoCarrierTest.AssertStoreRefuses(
            () => base.Update_base_property_on_derived_type(async));

    /// <inheritdoc />
    /// <remarks>EF issue <b>#31402</b>, as on the unfiltered TPT class.</remarks>
    public override Task Update_base_type_with_OfType(bool async)
        => TPTInheritanceBulkUpdatesInfoCarrierTest.AssertStoreRefuses(
            () => base.Update_base_type_with_OfType(async));
}

/// <inheritdoc cref="TPHInheritanceBulkUpdatesInfoCarrierTest" />
public class TPCFiltersInheritanceBulkUpdatesInfoCarrierTest(
    TPCFiltersInheritanceBulkUpdatesInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : TPCFiltersInheritanceBulkUpdatesTestBase<TPCFiltersInheritanceBulkUpdatesInfoCarrierFixture>(
        fixture,
        testOutputHelper)
{
    /// <inheritdoc />
    /// <remarks>Nothing here asserts SQL, so there is no log to clear.</remarks>
    protected override void ClearLog()
    {
    }
}

/// <summary>
///     The TPH bulk-update fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class TPHInheritanceBulkUpdatesInfoCarrierFixture : TPHInheritanceBulkUpdatesFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

/// <summary>
///     The TPT bulk-update fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class TPTInheritanceBulkUpdatesInfoCarrierFixture : TPTInheritanceBulkUpdatesFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

/// <summary>
///     The TPC bulk-update fixture, wired to a SQLite backend behind the wire.
/// </summary>
/// <remarks>
///     <c>UseGeneratedKeys</c> is <see langword="false" />, as on every other TPC fixture here and
///     for the same reason: TPC needs a key generator shared across tables and SQLite has neither
///     sequences nor HiLo.
/// </remarks>
public class TPCInheritanceBulkUpdatesInfoCarrierFixture : TPCInheritanceBulkUpdatesFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    public override bool UseGeneratedKeys
        => false;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

/// <summary>
///     The TPH bulk-update fixture with global query filters on.
/// </summary>
public class TPHFiltersInheritanceBulkUpdatesInfoCarrierFixture : TPHInheritanceBulkUpdatesInfoCarrierFixture
{
    /// <inheritdoc />
    public override bool EnableFilters
        => true;
}

/// <summary>
///     The TPT bulk-update fixture with global query filters on.
/// </summary>
public class TPTFiltersInheritanceBulkUpdatesInfoCarrierFixture : TPTInheritanceBulkUpdatesInfoCarrierFixture
{
    /// <inheritdoc />
    public override bool EnableFilters
        => true;
}

/// <summary>
///     The TPC bulk-update fixture with global query filters on.
/// </summary>
public class TPCFiltersInheritanceBulkUpdatesInfoCarrierFixture : TPCInheritanceBulkUpdatesInfoCarrierFixture
{
    /// <inheritdoc />
    public override bool EnableFilters
        => true;
}
