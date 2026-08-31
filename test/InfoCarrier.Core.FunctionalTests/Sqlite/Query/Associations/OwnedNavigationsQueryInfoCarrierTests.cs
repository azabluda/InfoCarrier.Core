// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The shared fixture for the six <c>OwnedNavigations*RelationalTestBase</c> classes below, on
///     ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         <b>R26 re-parented this onto <c>OwnedNavigationsRelationalFixtureBase</c>.</b> C0 mapped
///         the family on the core fixture and mirrored the relational one's <c>ToTable</c> calls by
///         hand — <c>OwnedTableSplittingRelationalFixtureBase</c>'s and
///         <c>OwnedNavigationsRelationalFixtureBase</c>'s, in that order — because the test project
///         did not then reference <c>EFCore.Relational.Specification.Tests</c>. The hand copy also
///         mirrored <c>AreCollectionsOrdered</c>. All of it is now the real thing, and the copy was
///         not complete: the base also calls <c>ValueGeneratedNever()</c> on every owned key and
///         states <c>IsRequired</c> on the associate navigations, neither of which C0 carried.
///     </para>
///     <para>
///         Its own <c>StoreName</c>, per CLAUDE.md: the Tier B store is file-backed and two
///         fixtures sharing a name share a database.
///     </para>
/// </remarks>
public class OwnedNavigationsQueryInfoCarrierFixture : OwnedNavigationsRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "OwnedNavigationsQueryInfoCarrierTest";

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

// The six OwnedNavigations facets (ADR-004). R26 adopted them bare and measured 8 red of 91;
// R26a is the override subset, and every one of the eight was the same reason -- SQLite has no
// APPLY -- so every override below comes from EF's own SQLite suite.
//
// The four `Contains_*` assertions, the two `Distinct_over_projected_*` and the two rewrites in
// `Projection` that this file used to restate by hand are all gone: they were
// `OwnedNavigations*RelationalTestBase`'s, and the re-parent inherits them verbatim.
//
// What is deliberately NOT adopted from EF's SQLite suite:
// `OwnedNavigationsStructuralEqualitySqliteTest` overrides several tests purely to assert golden
// SQL. Those pass here on the relational base's own assertion, and the golden SQL is the
// *backing store's* statement text, which this client never emits -- taking them would assert
// nothing and would couple this file to SQLite's formatting.

public class OwnedNavigationsCollectionQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsCollectionRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <summary>
    ///     <c>OwnedNavigationsCollectionSqliteTest</c>'s, adopted whole including its
    ///     <c>TrackAll</c> arm and EF's reason for it.
    /// </summary>
    /// <remarks>
    ///     Both arms measured as APPLY in R26: <c>NoTracking</c> raised
    ///     <c>SqliteStrings.ApplyNotSupported</c> bare, and <c>TrackAll</c> reached
    ///     <c>AssertOwnedTrackingQuery</c> expecting <i>"A tracking query is attempting to
    ///     project"</i> and got the APPLY message instead. That is EF's comment word for word:
    ///     <i>"Base test expects 'can't track owned entities' exception, but with SQLite we get
    ///     'no CROSS APPLY'"</i>. Reason matched before the override was taken (A63).
    /// </remarks>
    public override Task Distinct_projected(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? Task.CompletedTask
            : NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
                () => base.Distinct_projected(queryTrackingBehavior));
}

public class OwnedNavigationsMiscellaneousQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsMiscellaneousRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedNavigationsPrimitiveCollectionQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsPrimitiveCollectionRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedNavigationsProjectionQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsProjectionRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <summary>
    ///     The same APPLY limit in the same two arms, but <b>EF ships no
    ///     <c>OwnedNavigationsProjectionSqliteTest</c> to take it from.</b> That whole class is
    ///     commented out upstream, for EF issue #26708 (<i>"Stop generating composite keys for
    ///     owned collections on SQLite"</i>), so there is no override there to adopt.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>OwnedJsonProjectionSqliteTest</c> is the nearest statement of the same limit and
    ///         is where these two bodies come from, character for character including the
    ///         <c>TrackAll</c> arm. C20 and C57 already borrowed from there for the same reason.
    ///     </para>
    ///     <para>
    ///         <b>Worth recording rather than quietly enjoying: the rest of this class passes
    ///         here.</b> R26 measured it with no override at all and only these two tests failed,
    ///         in both arms. That is an observation about a class EF does not run, not a claim
    ///         that #26708 is fixed, and nothing here depends on which it is.
    ///     </para>
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

public class OwnedNavigationsSetOperationsQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsSetOperationsRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <summary>
    ///     <c>OwnedNavigationsSetOperationsSqliteTest</c>'s, now adoptable <em>verbatim</em>, and
    ///     that is the clearest single thing the re-parent bought.
    /// </summary>
    /// <remarks>
    ///     EF writes this as <c>Assert.ThrowsAsync&lt;EqualException&gt;</c>: the relational base
    ///     asserts <c>InsufficientInformationToIdentifyElementOfCollectionJoin</c>, SQLite raises
    ///     the APPLY message instead, and the nested assertion failure <em>is</em> the statement.
    ///     C57 could not write it that way, because this class then sat on the <em>core</em> base,
    ///     which makes no assertion to fail; it asserted the APPLY message directly and explained
    ///     the divergence in a paragraph. On the relational base EF's one line means here exactly
    ///     what it means upstream, and the paragraph is no longer needed.
    /// </remarks>
    public override Task Over_associate_collection_projected(QueryTrackingBehavior queryTrackingBehavior)
        => Assert.ThrowsAsync<EqualException>(() => base.Over_associate_collection_projected(queryTrackingBehavior));
}

public class OwnedNavigationsStructuralEqualityQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsStructuralEqualityRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);
