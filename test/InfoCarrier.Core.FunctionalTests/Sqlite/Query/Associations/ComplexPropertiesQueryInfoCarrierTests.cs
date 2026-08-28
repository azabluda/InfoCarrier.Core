// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The shared fixture for the seven <c>ComplexProperties*TestBase</c> classes below, on ADR-009
///     <b>Tier B</b> (C0).
/// </summary>
/// <remarks>
///     <para>
///         <b>This is A77's verdict being cashed.</b> A77 tried the complex-property query family
///         on Tier A, found that EF's InMemory provider does not translate a complex property
///         access at all, and concluded "not adoptable". Phase B's rule corrects that to "Tier B" —
///         the same correction A79 made for <c>FunkyDataQuery</c>, and the reason EF ships no
///         InMemory test here is the reason it belongs on the store that translates.
///     </para>
///     <para>
///         The core fixture stands alone: it maps the whole complex graph, references and
///         collections, plus the <c>ValueRootEntity</c> tree that exists because value types are
///         only supported as complex types. The relational assembly's two variants are
///         <c>ComplexJson</c> and <c>ComplexTableSplitting</c> — mapping strategies, not this
///         family — so unlike C2 there is nothing to mirror.
///     </para>
/// </remarks>
public class ComplexPropertiesQueryInfoCarrierFixture : ComplexPropertiesFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "ComplexPropertiesQueryInfoCarrierTest";

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    /// <remarks>
    ///     <c>AssociationsQueryFixtureBase.UseTransaction</c> throws <c>NotSupportedException</c>
    ///     and every relational fixture overrides it, because the base runs each bulk-update test
    ///     inside a transaction and makes a second context observe the same uncommitted state.
    ///     <b>All 31 <c>ComplexPropertiesBulkUpdate</c> failures were this and not
    ///     <c>ExecuteUpdate</c> at all</b> — the stack named the fixture one frame in. The provider
    ///     has its own enlistment (Phase T / M4), which <c>StoreGenerated</c>,
    ///     <c>ConferencePlanner</c> and <c>OptimisticConcurrency</c> already use.
    /// </remarks>
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         The <c>ToJson()</c> calls are <c>ComplexJsonRelationalFixtureBase</c>'s, ~20 lines,
    ///         mirrored by hand. <b>Without them nothing runs at all</b> — 134 of 136 tests failed
    ///         identically on <i>"The complex collection property 'RootEntity.AssociateCollection'
    ///         must be mapped to a JSON column"</i>. That is not a preference: a relational store
    ///         has no other way to hold a complex collection, so on Tier B this mapping is what the
    ///         family <em>is</em>.
    ///     </para>
    ///     <para>
    ///         Which makes this the one place C0's "we adopt the core family, not the
    ///         mapping-strategy variants" line bends, and it is worth being explicit about why it
    ///         is not broken: what is mirrored is the <em>mapping</em>, not the
    ///         <c>ComplexJson*TestBase</c> classes. Those assert SQL and stay unadopted; the seven
    ///         classes below are still the core ones.
    ///     </para>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);

        modelBuilder.Entity<RootEntity>(b =>
        {
            b.ComplexProperty(e => e.RequiredAssociate, rrb => rrb.ToJson());
            b.ComplexProperty(e => e.OptionalAssociate, orb => orb.ToJson());
            b.ComplexCollection(e => e.AssociateCollection, rcb => rcb.ToJson());
        });

        modelBuilder.Entity<ValueRootEntity>(b =>
        {
            b.ComplexProperty(e => e.RequiredAssociate, rrb => rrb.ToJson());

            b.ComplexProperty(e => e.OptionalAssociate, orb =>
            {
                orb.ToJson();

                // EF's own note: without this, the model reports an ambiguous property.
                orb.ComplexProperty(r => r.OptionalNested).IsRequired(false);
            });

            b.ComplexCollection(e => e.AssociateCollection, rcb => rcb.ToJson());
        });
    }
}

// The seven ComplexProperties facets (ADR-004). C3 adopted them with no overrides at all,
// deliberately: the batch discipline is adopt, classify, then work the failures. C20 is that
// second pass — the overrides below come from EF's `ComplexJson*` classes, which is where a
// relational limit on a JSON-mapped complex type is stated, and each was matched by reason
// against a measured failure first (A63). Not every reason matched; those stay red.

public class ComplexPropertiesBulkUpdateQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesBulkUpdateTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>ComplexJsonBulkUpdateRelationalTestBase</c>'s five, with EF's issue numbers: #36678
    ///     for <c>ExecuteDelete</c> on a complex type, #36336 for <c>ExecuteUpdate</c> over a
    ///     projected complex associate, #36679 for non-constant inline collections, #36722 for
    ///     <c>ExecuteUpdate</c> inside a JSON-mapped structural collection. All five are the
    ///     backing store's, and C19 is what made them legible: until <c>ITuple</c> was allowlisted
    ///     every one of them failed as <c>UnreachableException</c> instead.
    /// </summary>
    public override Task Delete_required_associate()
        => AssertTranslationFailedWithDetails(
            RelationalStrings.ExecuteDeleteOnNonEntityType, base.Delete_required_associate);

    /// <inheritdoc cref="Delete_required_associate" />
    public override Task Delete_optional_associate()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Delete_optional_associate);

    /// <inheritdoc cref="Delete_required_associate" />
    public override Task Update_property_on_projected_associate_with_OrderBy_Skip()
        => AssertTranslationFailedWithDetails(
            RelationalStrings.ExecuteUpdateSubqueryNotSupportedOverComplexTypes("RootEntity.RequiredAssociate#AssociateType"),
            base.Update_property_on_projected_associate_with_OrderBy_Skip);

    /// <inheritdoc cref="Delete_required_associate" />
    public override Task Update_collection_referencing_the_original_collection()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_collection_referencing_the_original_collection);

    /// <inheritdoc cref="Delete_required_associate" />
    public override Task Update_inside_structural_collection()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_inside_structural_collection);
}

public class ComplexPropertiesCollectionQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesCollectionTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>ComplexJsonCollectionRelationalTestBase</c>'s two. EF issue #36421 for the first
    ///     (projecting a complex JSON type out of a <c>Distinct</c>); the second is the same
    ///     <c>DistinctOnCollectionNotSupported</c> limit C1 and C2 already borrowed.
    /// </summary>
    public override async Task Distinct_projected(QueryTrackingBehavior queryTrackingBehavior)
        => Assert.Equal(
            RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Distinct_projected(queryTrackingBehavior))).Message);

    /// <inheritdoc cref="Distinct_projected" />
    public override async Task Distinct_over_projected_filtered_nested_collection()
        => Assert.Equal(
            RelationalStrings.DistinctOnCollectionNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                base.Distinct_over_projected_filtered_nested_collection)).Message);
}

public class ComplexPropertiesMiscellaneousQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesMiscellaneousTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture);

public class ComplexPropertiesPrimitiveCollectionQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesPrimitiveCollectionTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture);

public class ComplexPropertiesProjectionQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesProjectionTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>ComplexJsonProjectionSqliteTest</c>'s five. SQLite has no <c>APPLY</c>, and each of
    ///     these needs one — this provider surfaces `SqliteStrings.ApplyNotSupported` verbatim,
    ///     the backing store's answer relayed unchanged.
    /// </summary>
    public override Task SelectMany_associate_collection(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.SelectMany_associate_collection(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task SelectMany_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.SelectMany_nested_collection_on_optional_associate(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task SelectMany_nested_collection_on_required_associate(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.SelectMany_nested_collection_on_required_associate(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));
}

public class ComplexPropertiesSetOperationsQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesSetOperationsTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>ComplexJsonSetOperationsRelationalTestBase</c>'s two, with EF's reasons: issues
    ///     #33485 / #34849 for the first — EF notes it fails the same way with ordinary
    ///     navigations, which is why C2 borrowed the identical override for `OwnedNavigations` —
    ///     and for the second that complex mapping models two properties as different structural
    ///     types even when the CLR type is shared.
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

/// <remarks>
///     <para>
///         <b>The <c>Contains_with_nested_and_composed_operators</c> override is gone, and #62 is
///         why.</b> It asserted <c>InvalidOperationException</c>, quoting EF's own
///         <c>ComplexTableSplittingStructuralEqualityRelationalTestBase</c>: <i>"Collections are
///         not supported with table splitting, only JSON. Note that the exception is correct,
///         since the collections in the test data are null for table splitting."</i>
///     </para>
///     <para>
///         Once a complex value crosses as a parameter rather than as inlined literals, the query
///         translates and <b>the base's own assertion passes</b> — checked by running the base
///         directly, not inferred from the absence of an exception. So this is a query this
///         provider answers and EF's relational providers refuse, which
///         <c>website/docs/limitations.md</c> already has a section for. An override of ours that
///         outlives its limitation is a workaround to delete, and this one has.
///     </para>
///     <para>
///         <c>OwnedNavigationsQueryInfoCarrierTests</c> keeps its identical override, and that is
///         not an oversight: it still throws, and it was measured in the same run.
///     </para>
/// </remarks>
public class ComplexPropertiesStructuralEqualityQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesStructuralEqualityTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture);
