// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexJson;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The shared fixture for the seven <c>ComplexJson*RelationalTestBase</c> classes below, on
///     ADR-009 <b>Tier B</b> (R30).
/// </summary>
/// <remarks>
///     <para>
///         <b>This was <c>ComplexPropertiesQueryInfoCarrierFixture</c>, and R30 is the hand copy
///         being cashed in.</b> C0 adopted the <em>core</em> <c>ComplexProperties</c> family and
///         mirrored <c>ComplexJsonRelationalFixtureBase</c>'s ~20 lines of <c>ToJson()</c> by
///         hand, because a relational store has no other way to hold a complex collection —
///         without them 134 of 136 tests failed identically on <i>"The complex collection property
///         'RootEntity.AssociateCollection' must be mapped to a JSON column"</i>. C0 called that
///         the one place its "core family, not the mapping-strategy variants" line bent.
///         <b>The copy was checked against the original before it was deleted: byte-identical
///         apart from the wording of one comment.</b>
///     </para>
///     <para>
///         <b>So this is a re-parent, not a new family, and running both would be duplication
///         rather than coverage</b> (CLAUDE.md). The <c>ComplexJson*RelationalTestBase</c> classes
///         derive from the <c>ComplexProperties*TestBase</c> ones, so the compliance test resolves
///         both transitively, and the <em>non</em>-JSON complex mapping is not lost either: R28
///         adopted it as <see cref="ComplexTableSplittingQueryInfoCarrierFixture" />. Two complex
///         mappings, one family each, no model mirrored by hand any more.
///     </para>
///     <para>
///         <b>And it corrects a C0-era remark that stood on this file.</b> It said the
///         <c>ComplexJson*</c> bases "assert SQL and stay unadopted". They do not: across all 35
///         <c>Query.Associations.*RelationalTestBase</c> classes there is not one
///         <c>AssertSql("…")</c> with an argument. Every use is the helper declaration or an empty
///         <c>AssertSql()</c> meaning "nothing was executed" — which here reads the <em>client's</em>
///         <c>TestSqlLoggerFactory</c>, and this client emits no SQL, so it passes trivially.
///         Weaker than on SQLite, not false.
///     </para>
/// </remarks>
public class ComplexJsonQueryInfoCarrierFixture : ComplexJsonRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "ComplexJsonQueryInfoCarrierTest";

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    /// <remarks>
    ///     <c>AssociationsQueryFixtureBase.UseTransaction</c> throws <c>NotSupportedException</c>
    ///     and every relational fixture overrides it with <c>transaction.GetDbTransaction()</c>,
    ///     which ADR-013 makes unreachable here. The base runs each bulk-update test inside a
    ///     transaction and makes a second context observe the same uncommitted state.
    ///     <b>All 31 <c>ComplexPropertiesBulkUpdate</c> failures were this and not
    ///     <c>ExecuteUpdate</c> at all</b> — the stack named the fixture one frame in. The provider
    ///     has its own enlistment (Phase T / M4).
    /// </remarks>
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

// The seven ComplexJson facets (ADR-004). Adopting these also satisfies the seven core
// `ComplexProperties*TestBase` classes, which they derive from and which ComplianceTestBase
// resolves transitively.
//
// R30 adopted them bare and measured 10 red of 136, all one reason; R30a is the single override
// class below. The re-parent deleted nine overrides this file used to restate by hand -- five in
// BulkUpdate, two in Collection and two in SetOperations -- every one of which was copied out of
// a `ComplexJson*RelationalTestBase` in C20 and is now inherited verbatim.
//
// Nothing of EF's SQLite suite is left unadopted: outside ComplexJsonProjectionSqliteTest, not
// one class in it carries a single override, golden SQL included.

public class ComplexJsonBulkUpdateQueryInfoCarrierTest(
    ComplexJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexJsonBulkUpdateRelationalTestBase<ComplexJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexJsonCollectionQueryInfoCarrierTest(
    ComplexJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexJsonCollectionRelationalTestBase<ComplexJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexJsonMiscellaneousQueryInfoCarrierTest(
    ComplexJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexJsonMiscellaneousRelationalTestBase<ComplexJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexJsonPrimitiveCollectionQueryInfoCarrierTest(
    ComplexJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexJsonPrimitiveCollectionRelationalTestBase<ComplexJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexJsonProjectionQueryInfoCarrierTest(
    ComplexJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexJsonProjectionRelationalTestBase<ComplexJsonQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <summary>
    ///     <c>ComplexJsonProjectionSqliteTest</c>'s five, adopted whole and the whole of R30's
    ///     red: SQLite has no <c>APPLY</c>.
    /// </summary>
    /// <remarks>
    ///     <b>No <c>TrackAll</c> arm to short-circuit</b>, unlike the three owned families' five
    ///     identically-named tests. A complex type is not tracked as an entity, so
    ///     <c>AssertOwnedTrackingQuery</c> never intervenes and both arms raise
    ///     <c>ApplyNotSupported</c> directly. EF's class is written the same way, and R30 measured
    ///     all ten arms that way rather than inferring it from the shape.
    /// </remarks>
    public override Task SelectMany_associate_collection(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.SelectMany_associate_collection(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task SelectMany_nested_collection_on_required_associate(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.SelectMany_nested_collection_on_required_associate(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task SelectMany_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.SelectMany_nested_collection_on_optional_associate(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));
}

public class ComplexJsonSetOperationsQueryInfoCarrierTest(
    ComplexJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexJsonSetOperationsRelationalTestBase<ComplexJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);

/// <remarks>
///     <para>
///         <b>Green with no override, and that settles the #62 note this file used to carry.</b>
///         The old note deleted an override of <c>Contains_with_nested_and_composed_operators</c>
///         that asserted <c>InvalidOperationException</c>, quoting
///         <c>ComplexTableSplittingStructuralEqualityRelationalTestBase</c>, once the query
///         started translating — and called the result "a query this provider answers that other
///         EF providers refuse".
///     </para>
///     <para>
///         <b>The sharper reading, now that both mappings are adopted as EF states them:</b>
///         <c>ComplexJsonStructuralEqualityRelationalTestBase</c> declares no override at all, so
///         passing here is <em>agreement</em> with EF rather than divergence from it; and R28 runs
///         the table-splitting base, where EF does assert the throw and this provider throws.
///         <b>The difference was the mapping, not the provider</b> — the borrowed override never
///         should have been applied to a JSON-mapped model. Deleting it was right for a better
///         reason than the one recorded.
///     </para>
/// </remarks>
public class ComplexJsonStructuralEqualityQueryInfoCarrierTest(
    ComplexJsonQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexJsonStructuralEqualityRelationalTestBase<ComplexJsonQueryInfoCarrierFixture>(fixture, testOutputHelper);
