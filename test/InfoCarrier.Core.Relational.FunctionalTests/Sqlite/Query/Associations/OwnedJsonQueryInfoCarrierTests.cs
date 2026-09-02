// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
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

// The six OwnedJson facets (ADR-004). R29 adopted them bare and measured 16 red of 87 -- the
// first family in this block whose failures are NOT all one reason. Three groups:
//
//  A. Twelve: SQLite has no APPLY. Every one has an EF override, and R29a adopts all six methods
//     (six tests times two QueryTrackingBehavior arms).
//  B. Three `Contains_*`: the relational base asserts KeyNotFoundException, this provider raises
//     InvalidOperationException instead. LEFT FAILING -- see the remark on
//     OwnedJsonStructuralEqualityQueryInfoCarrierTest.
//  C. One, `Associate_with_parameter_null`: it fails because it PASSES. Also left failing, and
//     for a better reason -- see the same remark.
//
// The BulkUpdate class is the odd one here and it is worth knowing before reading it: it derives
// from BulkUpdatesTestBase directly rather than from an Associations base, because bulk update is
// not supported over owned JSON at all. It is three hand-written tests asserting that the right
// exception is raised, not the usual facet sweep. All three pass.

public class OwnedJsonBulkUpdateQueryInfoCarrierTest(
    OwnedJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedJsonBulkUpdateRelationalTestBase<OwnedJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedJsonCollectionQueryInfoCarrierTest(
    OwnedJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedJsonCollectionRelationalTestBase<OwnedJsonQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <summary>
    ///     <c>OwnedJsonCollectionSqliteTest</c>'s, adopted whole including the <c>TrackAll</c> arm
    ///     and EF's reason for it: <i>"Base test expects 'can't track owned entities' exception,
    ///     but with SQLite we get 'no CROSS APPLY'"</i>. Both arms measured that way in R29.
    /// </summary>
    public override Task Distinct_projected(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? Task.CompletedTask
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.Distinct_projected(queryTrackingBehavior));
}

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
    : OwnedJsonProjectionRelationalTestBase<OwnedJsonQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <summary>
    ///     <c>OwnedJsonProjectionSqliteTest</c>'s five, adopted whole. <b>This is the class R26a
    ///     and R27a have been borrowing from</b>, because EF issue #26708 leaves the two other
    ///     owned families without a projection class of their own; here it is used where it was
    ///     written.
    /// </summary>
    /// <remarks>
    ///     All ten arms measured in R29: <c>NoTracking</c> raises
    ///     <c>SqliteStrings.ApplyNotSupported</c> bare, and <c>TrackAll</c> reaches
    ///     <c>AssertOwnedTrackingQuery</c> and gets the APPLY message where a tracking message was
    ///     expected, which is EF's stated reason for short-circuiting that arm.
    /// </remarks>
    public override Task SelectMany_associate_collection(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? Task.CompletedTask
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.SelectMany_associate_collection(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task SelectMany_nested_collection_on_required_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? Task.CompletedTask
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.SelectMany_nested_collection_on_required_associate(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task SelectMany_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? Task.CompletedTask
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.SelectMany_nested_collection_on_optional_associate(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? Task.CompletedTask
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? Task.CompletedTask
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));
}

/// <remarks>
///     <para>
///         <b>Four tests in this class are left failing on purpose, and they are two different
///         things.</b> Nothing is overridden: EF's <c>OwnedJsonStructuralEqualitySqliteTest</c>
///         overrides every one of them only to assert golden SQL, calling <c>base</c> for the
///         behaviour, so there is no upstream statement of a different <em>reason</em> to adopt.
///     </para>
///     <para>
///         <b>Three are an exception-type difference on a path unsupported either way.</b>
///         <c>Contains_with_parameter</c>,
///         <c>Contains_with_operators_composed_on_the_collection</c> and
///         <c>Contains_with_nested_and_composed_operators</c>: the relational base asserts
///         <c>KeyNotFoundException</c>, and this provider raises <c>InvalidOperationException</c>
///         instead — <i>"No backing field could be found for property
///         'RootEntity.RequiredAssociate#AssociateType.NestedCollection#NestedAssociateType.AssociateTypeRootEntityId'
///         and the property does not have a getter"</i>. Both are the owned JSON collection's
///         synthetic key machinery failing to be read, and the difference is which point of it
///         fails first. <b>Writing an override for this would be overriding a spec test to make
///         the suite green, which CLAUDE.md forbids</b>, and adopting one is not on offer because
///         EF has none. Left failing per ADR-004. <c>Contains_with_inline</c>, which the base
///         asserts as <c>InvalidOperationException</c>, passes.
///     </para>
///     <para>
///         <b>The fourth is the good kind: <c>Associate_with_parameter_null</c> fails because it
///         passes.</b> The relational base wraps it in
///         <c>Assert.ThrowsAsync&lt;EqualException&gt;</c> with EF issue #36401 as the reason —
///         that is, <em>EF expects a wrong answer here</em>. This provider returns the right one,
///         so no <c>EqualException</c> is thrown and the wrapper is what fails. That is the
///         category R21 and R22 each found once: a query this provider answers that other EF
///         providers get wrong, and <c>website/docs/limitations.md</c> already has a section for
///         it. <b>Left failing rather than papered over, because the red is the evidence.</b>
///     </para>
/// </remarks>
public class OwnedJsonStructuralEqualityQueryInfoCarrierTest(
    OwnedJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedJsonStructuralEqualityRelationalTestBase<OwnedJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);
