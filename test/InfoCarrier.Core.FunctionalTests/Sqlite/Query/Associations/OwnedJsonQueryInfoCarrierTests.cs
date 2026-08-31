// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedJson;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The shared fixture for the six <c>OwnedJson*RelationalTestBase</c> classes below, on
///     ADR-009 <b>Tier B</b> (R29).
/// </summary>
/// <remarks>
///     <para>
///         <b>A new family</b>, and the third owned-entity mapping this block adopts: after
///         <see cref="OwnedNavigationsQueryInfoCarrierFixture" /> (a table per owned navigation)
///         and <see cref="OwnedTableSplittingQueryInfoCarrierFixture" /> (owned references in the
///         owner's table), this one puts the whole owned graph in a JSON column with
///         <c>OwnsOne(…).ToJson()</c> and <c>OwnsMany(…).ToJson()</c>.
///     </para>
///     <para>
///         <b>Six classes and not seven, and the missing one is not ours.</b>
///         <c>OwnedJsonSetOperationsRelationalTestBase</c> is commented out upstream in full: EF's
///         note is that every set operation over an owned JSON collection throws
///         <c>KeyNotFoundException</c> on the synthesized ordinal key. The compliance test asks
///         for six here for that reason.
///     </para>
///     <para>
///         Nothing is mirrored by hand. Its own <c>StoreName</c>, per CLAUDE.md: the Tier B store
///         is file-backed and two fixtures sharing a name share a database.
///     </para>
/// </remarks>
public class OwnedJsonQueryInfoCarrierFixture : OwnedJsonRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "OwnedJsonQueryInfoCarrierTest";

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

// The six OwnedJson facets (ADR-004), adopted bare: no override at all, so that every failure is
// measured and classified before anything is written to answer it.
//
// The BulkUpdate class is the odd one here and it is worth knowing before reading its failures:
// it derives from BulkUpdatesTestBase directly rather than from an Associations base, because
// bulk update is not supported over owned JSON at all. It is three hand-written tests asserting
// that the right exception is raised, not the usual facet sweep.

public class OwnedJsonBulkUpdateQueryInfoCarrierTest(
    OwnedJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedJsonBulkUpdateRelationalTestBase<OwnedJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedJsonCollectionQueryInfoCarrierTest(
    OwnedJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedJsonCollectionRelationalTestBase<OwnedJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedJsonMiscellaneousQueryInfoCarrierTest(
    OwnedJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedJsonMiscellaneousRelationalTestBase<OwnedJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedJsonPrimitiveCollectionQueryInfoCarrierTest(
    OwnedJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedJsonPrimitiveCollectionRelationalTestBase<OwnedJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedJsonProjectionQueryInfoCarrierTest(
    OwnedJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedJsonProjectionRelationalTestBase<OwnedJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedJsonStructuralEqualityQueryInfoCarrierTest(
    OwnedJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedJsonStructuralEqualityRelationalTestBase<OwnedJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);
