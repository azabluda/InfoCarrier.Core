// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedTableSplitting;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The shared fixture for the four <c>OwnedTableSplitting*RelationalTestBase</c> classes below,
///     on ADR-009 <b>Tier B</b> (R27).
/// </summary>
/// <remarks>
///     <para>
///         <b>A new family, not a re-parent</b>, and the first of four this repository has never
///         run. It is the owned-entity mapping that <see cref="OwnedNavigationsQueryInfoCarrierFixture" />
///         switches <em>off</em>: an owned reference lives in its owner's table (table splitting)
///         and only an owned collection gets a table of its own.
///         <c>OwnedNavigationsRelationalFixtureBase</c> derives from this one and overrides
///         precisely that, with a <c>ToTable</c> per owned reference.
///     </para>
///     <para>
///         <b>Tier B</b>, for C0's reason unchanged: <c>EFCore.InMemory.FunctionalTests</c> ships
///         no <c>Associations</c> directory at all, and table splitting is a statement about tables,
///         so the tier that translates is the only one whose green means anything.
///     </para>
///     <para>
///         Nothing is mirrored by hand. The whole model, <c>AreCollectionsOrdered</c> included,
///         comes from <c>OwnedTableSplittingRelationalFixtureBase</c>. Its own <c>StoreName</c>,
///         per CLAUDE.md: the Tier B store is file-backed and two fixtures sharing a name share a
///         database.
///     </para>
/// </remarks>
public class OwnedTableSplittingQueryInfoCarrierFixture : OwnedTableSplittingRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "OwnedTableSplittingQueryInfoCarrierTest";

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            onAddOptions: AssociationsWarnings.ThrowOnUnorderedRowLimiting,
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    /// <remarks>
    ///     The fixture, not the base, is what carries the <c>UseTransaction</c> trap in this
    ///     family; see <see cref="NavigationsQueryInfoCarrierFixture.UseTransaction" />.
    /// </remarks>
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

// The four OwnedTableSplitting facets (ADR-004), adopted bare: no override at all, so that every
// failure is measured and classified before anything is written to answer it. EF ships no
// BulkUpdate, Collection or SetOperations class for this family, which is why it is four and not
// seven.

public class OwnedTableSplittingMiscellaneousQueryInfoCarrierTest(
    OwnedTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedTableSplittingMiscellaneousRelationalTestBase<OwnedTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedTableSplittingPrimitiveCollectionQueryInfoCarrierTest(
    OwnedTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedTableSplittingPrimitiveCollectionRelationalTestBase<OwnedTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedTableSplittingProjectionQueryInfoCarrierTest(
    OwnedTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedTableSplittingProjectionRelationalTestBase<OwnedTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedTableSplittingStructuralEqualityQueryInfoCarrierTest(
    OwnedTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedTableSplittingStructuralEqualityRelationalTestBase<OwnedTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);
