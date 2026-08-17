// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.Navigations;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The shared fixture for the seven <c>Navigations*TestBase</c> classes below, on ADR-009
///     <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Tier B</b>, and not marginally so (C0): <c>EFCore.InMemory.FunctionalTests</c> ships
///         no <c>Associations</c> directory at all, while SQLite ships the whole tree. There is no
///         "could go either way" here for A81's rule to adjudicate.
///     </para>
///     <para>
///         The <em>core</em> fixture, which is where the model and the data live —
///         <c>AssociationsQueryFixtureBase</c>, <c>AssociationsData</c>, <c>AssociationsModel</c>
///         and <c>NavigationsFixtureBase</c> are all in <c>EFCore.Specification.Tests</c>. The
///         relational assembly adds only <em>mapping-strategy</em> variants — <c>ComplexJson</c>,
///         <c>OwnedJson</c>, <c>ComplexTableSplitting</c>, <c>OwnedTableSplitting</c> — which the
///         compliance test does not ask for and each of which would need a hand-mirrored model, the
///         cost B3d priced at ~630 lines. Adopting the core family and not those is deliberate.
///     </para>
///     <para>
///         Its own <c>StoreName</c>, per CLAUDE.md: the Tier B store is file-backed and two
///         fixtures sharing a name share a database.
///     </para>
/// </remarks>
public class NavigationsQueryInfoCarrierFixture : NavigationsFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "NavigationsQueryInfoCarrierTest";

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
    ///     <para>
    ///         The six <c>AutoInclude()</c> calls are <c>NavigationsRelationalFixtureBase</c>'s,
    ///         mirrored by hand because this project references only the core specification
    ///         assembly. <b>They are not decoration — they are what the facet is about.</b> Every
    ///         query in these seven classes returns bare <c>RootEntity</c> rows and no test writes
    ///         an <c>Include</c>, yet <c>AssociationsQueryFixtureBase.AssertRootEntity</c> walks the
    ///         whole association graph and compares it against the fully-populated
    ///         <c>AssociationsData</c>. Without the auto-includes the associates are simply not
    ///         loaded, and <b>63 of the first run's 79 failures were one <c>Values differ</c></b> —
    ///         <c>Expected: Root5_RequiredAssociate, Actual: null</c>, out of that asserter.
    ///     </para>
    ///     <para>
    ///         Safe to mirror, and the reason is worth stating: <c>AutoInclude</c> is a
    ///         <em>core</em> modelling API, so it is a statement both models make for themselves
    ///         from the same <c>OnModelCreating</c> — not something one side computes and the other
    ///         has to agree with (the B4/B6/B12 hazard).
    ///     </para>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);

        modelBuilder.Entity<RootEntity>(b =>
        {
            b.Navigation(e => e.RequiredAssociate).AutoInclude();
            b.Navigation(e => e.OptionalAssociate).AutoInclude();
            b.Navigation(e => e.AssociateCollection).AutoInclude();
        });

        modelBuilder.Entity<AssociateType>(b =>
        {
            b.Navigation(e => e.RequiredNestedAssociate).AutoInclude();
            b.Navigation(e => e.OptionalNestedAssociate).AutoInclude();
            b.Navigation(e => e.NestedCollection).AutoInclude();
        });
    }
}

// The seven Navigations facets (ADR-004). Adopting these also satisfies the seven shared
// `Associations*TestBase` bases, which they derive from and which ComplianceTestBase resolves
// transitively.
//
// The overrides below are all EF's own, from `Navigations*RelationalTestBase` and
// `Navigations*SqliteTest`, and each was matched by *reason* before being taken (A63): every one
// of them is a limit of the backing store or of relational translation generally, raised here from
// the same place with the same message. Nothing else is overridden, and nothing here is red:
// the four `Index_*` (and four more on `OwnedNavigations`) were closed by C69, which forwards the
// one warning `AssertOrderedCollectionQuery` contracts for to the server that raises it — see
// `AssociationsWarnings`. C55 had diagnosed them and left them red on the belief that the event
// could only be forwarded globally, at 26 broken; all 26 turned out to be in Northwind and
// BulkUpdates classes, none in these families.

public class NavigationsCollectionQueryInfoCarrierTest(NavigationsQueryInfoCarrierFixture fixture)
    : NavigationsCollectionTestBase<NavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>NavigationsCollectionRelationalTestBase</c>'s two: <c>Distinct</c> over a projected
    ///     collection is something no relational provider can express, and EF states it on the
    ///     relational base rather than in SQLite's suite.
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

    /// <summary>
    ///     <c>NavigationsCollectionSqliteTest</c>'s: the query reaches SQL and asks SQLite for
    ///     <c>APPLY</c>, which it does not have.
    /// </summary>
    public override Task Distinct_projected(QueryTrackingBehavior queryTrackingBehavior)
        => AssertApplyNotSupported(() => base.Distinct_projected(queryTrackingBehavior));

    internal static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);
}

public class NavigationsIncludeQueryInfoCarrierTest(NavigationsQueryInfoCarrierFixture fixture)
    : NavigationsIncludeTestBase<NavigationsQueryInfoCarrierFixture>(fixture);

public class NavigationsMiscellaneousQueryInfoCarrierTest(NavigationsQueryInfoCarrierFixture fixture)
    : NavigationsMiscellaneousTestBase<NavigationsQueryInfoCarrierFixture>(fixture);

public class NavigationsPrimitiveCollectionQueryInfoCarrierTest(NavigationsQueryInfoCarrierFixture fixture)
    : NavigationsPrimitiveCollectionTestBase<NavigationsQueryInfoCarrierFixture>(fixture);

public class NavigationsProjectionQueryInfoCarrierTest(NavigationsQueryInfoCarrierFixture fixture)
    : NavigationsProjectionTestBase<NavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>NavigationsProjectionRelationalTestBase</c>'s. A relational store returns an
    ///     <em>empty</em> collection where the owner is null, not a null collection, so EF rewrites
    ///     the expected side rather than the query.
    /// </summary>
    public override Task Select_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => AssertQuery(
            ss => ss.Set<RootEntity>().OrderBy(e => e.Id).Select(x => x.OptionalAssociate!.NestedCollection),
            ss => ss.Set<RootEntity>().OrderBy(e => e.Id)
                .Select(x => x.OptionalAssociate!.NestedCollection ?? new List<NestedAssociateType>()),
            assertOrder: true,
            elementAsserter: (e, a) => AssertCollection(e, a, elementSorter: r => r.Id),
            queryTrackingBehavior: queryTrackingBehavior);

    /// <summary>
    ///     <c>NavigationsProjectionSqliteTest</c>'s two, both <c>APPLY</c>.
    /// </summary>
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));
}

public class NavigationsSetOperationsQueryInfoCarrierTest(NavigationsQueryInfoCarrierFixture fixture)
    : NavigationsSetOperationsTestBase<NavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>NavigationsSetOperationsRelationalTestBase</c>'s, for EF issues #33485 and #34849.
    /// </summary>
    public override async Task Over_associate_collection_projected(QueryTrackingBehavior queryTrackingBehavior)
        => Assert.Equal(
            RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Over_associate_collection_projected(queryTrackingBehavior))).Message);
}

public class NavigationsStructuralEqualityQueryInfoCarrierTest(NavigationsQueryInfoCarrierFixture fixture)
    : NavigationsStructuralEqualityTestBase<NavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>NavigationsStructuralEqualityRelationalTestBase</c>'s three, with EF's reason: a
    ///     traditional relational collection navigation cannot be compared reliably — a collection
    ///     on a null instance comes back empty rather than null, and element order is not preserved.
    /// </summary>
    public override Task Two_nested_collections()
        => Assert.ThrowsAsync<EqualException>(() => base.Two_nested_collections());

    /// <inheritdoc cref="Two_nested_collections" />
    public override Task Nested_collection_with_inline()
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Nested_collection_with_inline());

    /// <inheritdoc cref="Two_nested_collections" />
    public override Task Nested_collection_with_parameter()
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Nested_collection_with_parameter());
}
