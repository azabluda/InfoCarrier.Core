// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Query.Associations;

/// <summary>
///     The shared fixture for the six <c>OwnedNavigations*TestBase</c> classes below, on ADR-009
///     <b>Tier B</b> (C0).
/// </summary>
/// <remarks>
///     <para>
///         Unlike C1's, this family's <em>core</em> fixture is complete on its own: it maps the
///         whole owned graph and only the table <em>layout</em> is relational.
///         <c>OwnedNavigationsRelationalFixtureBase</c> exists solely to move each owned navigation
///         to its own table with <c>ToTable</c>, disabling the default table splitting — a physical
///         choice these tests do not assert, since the SQL-asserting bases are the ones we do not
///         take. No auto-includes either: an owned dependent comes with its owner's row by
///         definition, which is the same fact B10 turned on.
///     </para>
///     <para>
///         <c>AreCollectionsOrdered</c> <b>is</b> mirrored, and is the one thing here that has to
///         be. The relational fixture sets it false because a relational store does not preserve
///         the order of an owned collection; the core fixture leaves the base's <c>true</c>
///         standing because a document store does. The backing store here is SQLite, so the
///         relational answer is the true one — and this is a statement about the <em>store</em>,
///         which is exactly the class of thing this project must mirror by hand.
///     </para>
/// </remarks>
public class OwnedNavigationsQueryInfoCarrierFixture : OwnedNavigationsFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "OwnedNavigationsQueryInfoCarrierTest";

    /// <inheritdoc />
    public override bool AreCollectionsOrdered
        => false;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         The <c>ToTable</c> calls are <c>OwnedTableSplittingRelationalFixtureBase</c>'s and
    ///         <c>OwnedNavigationsRelationalFixtureBase</c>'s, in that order, mirrored by hand.
    ///         <b>Without them the model does not validate at all</b> — <i>"The table
    ///         'RootEntity_NestedCollection' cannot be used for entity type …"</i>, 69 of the first
    ///         run's 81 failures, because three different owners each have a <c>NestedCollection</c>
    ///         and the default table-splitting convention gives all three the same table name.
    ///     </para>
    ///     <para>
    ///         Precedent and reason: B3c mirrored <c>ToJson()</c> the same way. A physical table
    ///         name is the backing store's business and the client has no store — but both sides
    ///         run this one <c>OnModelCreating</c>, so if the mapping is not stated here it is not
    ///         stated at all, and the server's model is the one that has to be valid.
    ///     </para>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);

        // OwnedTableSplittingRelationalFixtureBase: every owned *collection* gets its own table.
        modelBuilder.Entity<RootEntity>(b =>
        {
            b.OwnsOne(
                e => e.RequiredAssociate,
                rrb => rrb.OwnsMany(r => r.NestedCollection, rcb => rcb.ToTable("RequiredRelated_NestedCollection")));

            b.OwnsOne(
                e => e.OptionalAssociate,
                orb => orb.OwnsMany(r => r.NestedCollection, rcb => rcb.ToTable("OptionalRelated_NestedCollection")));

            b.OwnsMany(
                e => e.AssociateCollection, rcb =>
                {
                    rcb.ToTable("RelatedCollection");
                    rcb.OwnsMany(r => r.NestedCollection, rnb => rnb.ToTable("RelatedCollection_NestedCollection"));
                });
        });

        // OwnedNavigationsRelationalFixtureBase: and every owned *reference* too, which is what
        // makes this family "owned navigations mapped to separate tables" rather than splitting.
        modelBuilder.Entity<RootEntity>(b =>
        {
            b.OwnsOne(
                e => e.RequiredAssociate, rrb =>
                {
                    rrb.ToTable("RequiredRelated");
                    rrb.OwnsOne(r => r.RequiredNestedAssociate, rnb => rnb.ToTable("RequiredRelated_RequiredNested"));
                    rrb.OwnsOne(r => r.OptionalNestedAssociate, rnb => rnb.ToTable("RequiredRelated_OptionalNested"));
                });

            b.OwnsOne(
                e => e.OptionalAssociate, orb =>
                {
                    orb.ToTable("OptionalRelated");
                    orb.OwnsOne(r => r.RequiredNestedAssociate, rnb => rnb.ToTable("OptionalRelated_RequiredNested"));
                    orb.OwnsOne(r => r.OptionalNestedAssociate, rnb => rnb.ToTable("OptionalRelated_OptionalNested"));
                });

            b.OwnsMany(
                e => e.AssociateCollection, rcb =>
                {
                    rcb.OwnsOne(r => r.RequiredNestedAssociate, rnb => rnb.ToTable("RelatedCollection_RequiredNested"));
                    rcb.OwnsOne(r => r.OptionalNestedAssociate, rnb => rnb.ToTable("RelatedCollection_OptionalNested"));
                });
        });
    }
}

// The six OwnedNavigations facets (ADR-004), starting with no overrides. Every failure is real
// information, triaged in docs/implementation-plan.md under C2.

public class OwnedNavigationsCollectionQueryInfoCarrierTest(OwnedNavigationsQueryInfoCarrierFixture fixture)
    : OwnedNavigationsCollectionTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>OwnedNavigationsCollectionRelationalTestBase</c>'s two, and
    ///     <c>OwnedNavigationsCollectionSqliteTest</c>'s one. Same limits as C1's, from the same
    ///     places.
    /// </summary>
    public override async Task Distinct_over_projected_nested_collection()
        => Assert.Equal(
            RelationalStrings.DistinctOnCollectionNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                base.Distinct_over_projected_nested_collection)).Message);

    /// <inheritdoc cref="Distinct_over_projected_nested_collection" />
    public override async Task Distinct_over_projected_filtered_nested_collection()
        => Assert.Equal(
            RelationalStrings.DistinctOnCollectionNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                base.Distinct_over_projected_filtered_nested_collection)).Message);

    /// <inheritdoc cref="Distinct_over_projected_nested_collection" />
    public override Task Distinct_projected(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Distinct_projected(queryTrackingBehavior));
}

public class OwnedNavigationsMiscellaneousQueryInfoCarrierTest(OwnedNavigationsQueryInfoCarrierFixture fixture)
    : OwnedNavigationsMiscellaneousTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture);

public class OwnedNavigationsPrimitiveCollectionQueryInfoCarrierTest(OwnedNavigationsQueryInfoCarrierFixture fixture)
    : OwnedNavigationsPrimitiveCollectionTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture);

public class OwnedNavigationsProjectionQueryInfoCarrierTest(OwnedNavigationsQueryInfoCarrierFixture fixture)
    : OwnedNavigationsProjectionTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>OwnedNavigationsProjectionRelationalTestBase</c>'s two. The second is a full test
    ///     replacement rather than an exception assertion, and EF states why: a traditional
    ///     relational collection navigation projected from a <see langword="null" /> instance comes
    ///     back as an <em>empty</em> collection, not as null — which is the opposite of both client
    ///     evaluation and the JSON collection behaviour. Ours failed with <c>Assert.Null() Failure:
    ///     Value is not null</c>, which is that sentence stated from the other side.
    /// </summary>
    public override Task Select_required_associate_via_optional_navigation(QueryTrackingBehavior queryTrackingBehavior)
        => AssertOwnedTrackingQuery(
            queryTrackingBehavior,
            () => base.Select_required_associate_via_optional_navigation(queryTrackingBehavior));

    /// <inheritdoc cref="Select_required_associate_via_optional_navigation" />
    public override Task Select_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_nested_collection_on_optional_associate(queryTrackingBehavior)
            : AssertQuery(
                ss => ss.Set<RootEntity>().OrderBy(e => e.Id).Select(x => x.OptionalAssociate!.NestedCollection),
                ss => ss.Set<RootEntity>().OrderBy(e => e.Id)
                    .Select(x => x.OptionalAssociate!.NestedCollection ?? new List<NestedAssociateType>()),
                assertOrder: true,
                elementAsserter: (e, a) => AssertCollection(e, a, elementSorter: r => r.Id),
                queryTrackingBehavior: queryTrackingBehavior);

    /// <summary>
    ///     SQLite has no <c>APPLY</c>, and <c>OwnedJsonProjectionSqliteTest</c> borrows the same
    ///     limit. **Only for <c>NoTracking</c>.** EF no-ops the <c>TrackAll</c> half because on
    ///     SQLite it reaches the APPLY refusal before the base's "can't track owned entities"
    ///     assertion; here <c>TrackAll</c> fails on a string comparison instead, which is a
    ///     different statement — so it is left red rather than silenced under a reason that is
    ///     not ours (A63 cuts both ways).
    /// </summary>
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior)
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_optional_related_FirstOrDefault" />
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior)
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));
}

public class OwnedNavigationsSetOperationsQueryInfoCarrierTest(OwnedNavigationsQueryInfoCarrierFixture fixture)
    : OwnedNavigationsSetOperationsTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>OwnedNavigationsSetOperationsRelationalTestBase</c>'s two, with EF's reasons: issues
    ///     #33485 / #34849 for the first, and for the second that an owned navigation models each
    ///     property as its own structural type even when the CLR type is shared, so a set operation
    ///     over two of them is a set operation over different types.
    /// </summary>
    public override async Task Over_associate_collection_projected(QueryTrackingBehavior queryTrackingBehavior)
        => Assert.Equal(
            RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Over_associate_collection_projected(queryTrackingBehavior))).Message);

    /// <inheritdoc cref="Over_associate_collection_projected" />
    public override async Task Over_different_collection_properties()
        => Assert.Equal(
            RelationalStrings.SetOperationOverDifferentStructuralTypes(
                "RootEntity.RequiredAssociate#AssociateType.NestedCollection#NestedAssociateType",
                "RootEntity.OptionalAssociate#AssociateType.NestedCollection#NestedAssociateType"),
            (await Assert.ThrowsAsync<InvalidOperationException>(
                base.Over_different_collection_properties)).Message);
}

public class OwnedNavigationsStructuralEqualityQueryInfoCarrierTest(OwnedNavigationsQueryInfoCarrierFixture fixture)
    : OwnedNavigationsStructuralEqualityTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>OwnedNavigationsStructuralEqualityRelationalTestBase</c>'s four. EF asserts only the
    ///     exception type and records the message in a comment, because an owned collection under a
    ///     relational store carries a synthesized ordinal key and shadow foreign keys that
    ///     <c>Contains</c> cannot read — <i>"no backing field could be found … and the property does
    ///     not have a getter"</i>. Asserted the same way here, for the same reason.
    /// </summary>
    public override Task Contains_with_inline()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Contains_with_inline);

    /// <inheritdoc cref="Contains_with_inline" />
    public override Task Contains_with_parameter()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Contains_with_parameter);

    /// <inheritdoc cref="Contains_with_inline" />
    public override Task Contains_with_operators_composed_on_the_collection()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Contains_with_operators_composed_on_the_collection);

    /// <inheritdoc cref="Contains_with_inline" />
    public override Task Contains_with_nested_and_composed_operators()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Contains_with_nested_and_composed_operators);
}
