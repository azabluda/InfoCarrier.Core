// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexTableSplitting;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The shared fixture for the five <c>ComplexTableSplitting*RelationalTestBase</c> classes
///     below, on ADR-009 <b>Tier B</b> (R28).
/// </summary>
/// <remarks>
///     <para>
///         <b>A new family.</b> It is the complex-type counterpart of
///         <see cref="OwnedTableSplittingQueryInfoCarrierFixture" />: the complex property's
///         members become columns in the owner's own table. <b>Collections are not expressible that
///         way at all</b> — only JSON can hold one — so
///         <c>ComplexTableSplittingRelationalFixtureBase</c> both <c>Ignore</c>s every collection
///         in the model and nulls it out of the seed data, and a large share of these classes are
///         assertions that a collection query fails.
///     </para>
///     <para>
///         <b>This is one of the two answers to a question C0 could only answer once.</b> A
///         complex collection cannot live in ordinary columns, so a relational store either puts
///         the whole graph in a JSON column — <see cref="ComplexJsonQueryInfoCarrierFixture" />,
///         which R30 adopted — or does not hold collections at all, which is this family. C0 had
///         to mirror the JSON mapping by hand and had no way to state this one; both are now
///         adopted as EF writes them, one family each.
///     </para>
///     <para>
///         Nothing is mirrored by hand. Its own <c>StoreName</c>, per CLAUDE.md: the Tier B store
///         is file-backed and two fixtures sharing a name share a database.
///     </para>
/// </remarks>
public class ComplexTableSplittingQueryInfoCarrierFixture : ComplexTableSplittingRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "ComplexTableSplittingQueryInfoCarrierTest";

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    /// <remarks>
    ///     Required, and doubly so here: the fixture base declares it with
    ///     <c>transaction.GetDbTransaction()</c> (ADR-013), and the <c>BulkUpdate</c> class really
    ///     does run each test inside a transaction that a second context has to observe. That is
    ///     the 31-failure lesson recorded on
    ///     <see cref="ComplexJsonQueryInfoCarrierFixture.UseTransaction" />.
    /// </remarks>
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

// The five ComplexTableSplitting facets (ADR-004). Five and not seven: EF ships no Collection or
// SetOperations class for this family, which follows from the model rather than from EF's
// convenience -- there are no collections in it to run them against.
//
// R28 adopted them bare and measured 4 red of 115; R28a is the single override below. Nothing
// else of EF's is left unadopted: the rest of its SQLite suite for this family is bare, with no
// golden SQL anywhere in it -- the first family in this block of which that is true.

public class ComplexTableSplittingBulkUpdateQueryInfoCarrierTest(
    ComplexTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTableSplittingBulkUpdateRelationalTestBase<ComplexTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexTableSplittingMiscellaneousQueryInfoCarrierTest(
    ComplexTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTableSplittingMiscellaneousRelationalTestBase<ComplexTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexTableSplittingPrimitiveCollectionQueryInfoCarrierTest(
    ComplexTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTableSplittingPrimitiveCollectionRelationalTestBase<ComplexTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexTableSplittingProjectionQueryInfoCarrierTest(
    ComplexTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTableSplittingProjectionRelationalTestBase<ComplexTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <summary>
    ///     <c>ComplexTableSplittingProjectionSqliteTest</c>'s two, verbatim, and the whole of
    ///     R28's red: SQLite has no <c>APPLY</c>.
    /// </summary>
    /// <remarks>
    ///     <b>Unlike R26a and R27a, this override comes from the family's own SQLite class rather
    ///     than a sibling's</b> — EF issue #26708 disables the projection class for the two
    ///     <em>owned</em> families but not for this one.
    ///     <b>And there is no <c>TrackAll</c> arm to special-case.</b> R26 and R27 each had to
    ///     short-circuit tracking, because an owned dependent projected under <c>TrackAll</c>
    ///     reaches <c>AssertOwnedTrackingQuery</c> and the APPLY message arrives where a tracking
    ///     message was expected. A complex type is not tracked as an entity, so no such assertion
    ///     stands in the way and both arms raise <c>ApplyNotSupported</c> directly — measured in
    ///     R28, not assumed from the shape.
    /// </remarks>
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));
}

public class ComplexTableSplittingStructuralEqualityQueryInfoCarrierTest(
    ComplexTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTableSplittingStructuralEqualityRelationalTestBase<ComplexTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);
