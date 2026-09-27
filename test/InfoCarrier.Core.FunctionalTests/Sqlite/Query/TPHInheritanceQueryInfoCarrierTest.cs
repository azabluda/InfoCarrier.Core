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
///         <b>The only class of the core <c>InheritanceQueryTestBase</c>, since 2026-09-27.</b> This
///         base derives from it and adds the tests that only make sense when a discriminator is
///         really written to a store; EF hosts it on SQLite as <c>InheritanceQuerySqliteTest</c>.
///         Until that day this paragraph read "Not a duplicate of the Tier A inheritance test [...]
///         Two bases, one tier each", beside <c>InMemory.Query.InheritanceQueryInfoCarrierTest</c>,
///         which ran the core base on Tier A. It was a duplicate of every core test, because a
///         class runs every test of the bases above it. When the owner moved the Tier A bases that
///         make sense to Tier B, that class went and this one stayed.
///     </para>
///     <para>
///         <b>Why it earns its place next to the other two.</b> TPT and TPC are the mappings a
///         discriminator is stripped for; TPH is the one it must be left alone for, and the
///         narrowing that decides between them is the part most likely to be wrong. This base is
///         the direct assertion of the half that must not change. R128 replaced this repository's
///         hand-written <c>InfoCarrierHierarchyMappingConvention</c> with EF's own
///         <c>EntityTypeHierarchyMappingConvention</c>, so what is under test is now EF's
///         narrowing rather than a copy of it, and this assertion matters no less for that.
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
        => facade.UseTestTransaction(transaction);
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
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions,
            relationalClientStore: true,
                arbitrarySqlExecution: true);
}
