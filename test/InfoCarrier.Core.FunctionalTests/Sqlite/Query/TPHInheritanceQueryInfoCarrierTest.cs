// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>TPHInheritanceQueryTestBase</c> on ADR-009 <b>Tier B</b> — the third leg of the
///     inheritance mapping, beside <see cref="TPTInheritanceQueryInfoCarrierTest" /> and
///     <see cref="TPCInheritanceQueryInfoCarrierTest" /> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Not a duplicate of the Tier A inheritance test, and the distinction is ADR-009's.</b>
///         <c>InMemory.Query.InheritanceQueryInfoCarrierTest</c> adopts the <em>core</em>
///         <c>InheritanceQueryTestBase</c>. This is a different base: it adds the tests that only
///         make sense when a discriminator is really written to a store, and EF hosts it on SQLite
///         itself, as <c>InheritanceQuerySqliteTest</c>. Two bases, one tier each.
///     </para>
///     <para>
///         <b>Why it earns its place next to the other two.</b> TPT and TPC are the mappings
///         <c>InfoCarrierHierarchyMappingConvention</c> strips a discriminator for. TPH is the one
///         it must leave alone, and the narrowing that decides between them is the part of that
///         convention most likely to be wrong. This base is the direct assertion of the half that
///         must not change.
///     </para>
///     <para>
///         The <c>UseTransaction</c> override is required: this base inherits
///         <c>InheritanceQueryTestBase</c>, which uses
///         <c>ExecuteWithStrategyInTransactionAsync</c>. EF's own SQLite class overrides it with
///         <c>transaction.GetDbTransaction()</c>, which ADR-013 makes unreachable here.
///     </para>
/// </remarks>
public class TPHInheritanceQueryInfoCarrierTest(
    TPHInheritanceQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : TPHInheritanceQueryTestBase<TPHInheritanceQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <inheritdoc />
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

/// <summary>
///     The TPH inheritance fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class TPHInheritanceQueryInfoCarrierFixture : TPHInheritanceQueryFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions,
            relationalClientStore: true);
}
