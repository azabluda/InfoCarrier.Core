// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
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
            SqliteInfoCarrierTier.Instance,
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

// The four OwnedTableSplitting facets (ADR-004). EF ships no BulkUpdate, Collection or
// SetOperations class for this family, which is why it is four and not seven.
//
// R27 adopted them bare and measured 4 red of 70; R27a is the override subset below, and all four
// were one reason -- SQLite has no APPLY.
//
// Two things of EF's are deliberately NOT adopted:
//
//  - `OwnedTableSplittingStructuralEqualitySqliteTest` overrides several tests purely to assert
//    golden SQL. They pass here on the relational base's own assertion, and the SQL is the
//    *backing store's* statement text, which this client never emits.
//  - EF's `OwnedTableSplittingSqliteFixture` adds
//    `ConfigureWarnings(b => b.Ignore(SqliteEventId.CompositeKeyWithValueGeneration))`. Not needed
//    here, and measured rather than assumed: R27's bare run passed 66 of 70 with no such failure.
//    An unnecessary warning-ignore in a fixture is the kind of thing that later hides a real one.

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
    : OwnedTableSplittingProjectionRelationalTestBase<OwnedTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <summary>
    ///     The whole of R27's red: SQLite has no <c>APPLY</c>, in both
    ///     <c>QueryTrackingBehavior</c> arms. <b>EF ships no
    ///     <c>OwnedTableSplittingProjectionSqliteTest</c> to take the override from</b> — that
    ///     class is commented out upstream in full, for the same EF issue #26708 that disables
    ///     <c>OwnedNavigationsProjectionSqliteTest</c>.
    /// </summary>
    /// <remarks>
    ///     So these two bodies come from <c>OwnedJsonProjectionSqliteTest</c>, character for
    ///     character, exactly as
    ///     <see cref="OwnedNavigationsProjectionQueryInfoCarrierTest.Select_subquery_required_related_FirstOrDefault" />
    ///     does. <b>Two families now with the same upstream gap and the same substitute</b>, which
    ///     is worth stating once: #26708 costs EF two SQLite classes, and this provider runs both
    ///     of them with two tests red in each.
    /// </remarks>
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? Task.CompletedTask
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? Task.CompletedTask
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));
}

public class OwnedTableSplittingStructuralEqualityQueryInfoCarrierTest(
    OwnedTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedTableSplittingStructuralEqualityRelationalTestBase<OwnedTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);
