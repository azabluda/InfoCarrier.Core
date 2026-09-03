// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>TPCInheritanceQueryTestBase</c> on ADR-009 <b>Tier B</b> — the table-per-concrete-type
///     sibling of <see cref="TPTInheritanceQueryInfoCarrierTest" /> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Adopted second, and on purpose.</b> TPT and TPC reach the client's model validator
///         by the same route — a hierarchy with no discriminator — so a defect in one is a defect
///         in the other until measured otherwise. Running both is what turns
///         <c>InfoCarrierModelValidator</c> from a fix for one base into a fix for the mapping.
///     </para>
///     <para>
///         <b>The <c>UseTransaction</c> override is required</b>, for the reason spelled out on
///         <see cref="TPTInheritanceQueryInfoCarrierTest" />: neither TPC base names
///         <c>ExecuteWithStrategyInTransactionAsync</c>, but the <c>InheritanceQueryTestBase</c>
///         both inherit does. EF's own <c>TPCInheritanceQuerySqliteTest</c> overrides it with
///         <c>transaction.GetDbTransaction()</c>, which ADR-013 makes unreachable here.
///     </para>
/// </remarks>
public class TPCInheritanceQueryInfoCarrierTest(
    TPCInheritanceQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : TPCInheritanceQueryTestBase<TPCInheritanceQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <inheritdoc />
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

/// <summary>
///     The TPC inheritance fixture, wired to a SQLite backend behind the wire.
/// </summary>
/// <remarks>
///     <c>UseGeneratedKeys</c> is <see langword="false" />, copied from EF's own
///     <c>TPCInheritanceQuerySqliteFixture</c> rather than guessed. TPC gives each concrete type
///     its own table, so keys have to be unique across all of them, which needs a generator shared
///     between tables — a sequence or HiLo. SQLite has neither, so the model supplies its own key
///     values. This is a property of the backing store and says nothing about the wire.
/// </remarks>
public class TPCInheritanceQueryInfoCarrierFixture : TPCInheritanceQueryFixture
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
}
