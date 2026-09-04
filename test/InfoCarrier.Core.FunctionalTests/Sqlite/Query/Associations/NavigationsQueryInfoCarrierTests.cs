// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Associations.Navigations;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Abstractions;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The shared fixture for the seven <c>Navigations*RelationalTestBase</c> classes below, on
///     ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Tier B</b>, and not marginally so (C0): <c>EFCore.InMemory.FunctionalTests</c> ships
///         no <c>Associations</c> directory at all, while SQLite ships the whole tree. There is no
///         "could go either way" here for A81's rule to adjudicate.
///     </para>
///     <para>
///         <b>R25 re-parented this onto <c>NavigationsRelationalFixtureBase</c>.</b> C0 adopted the
///         core fixture and mirrored the relational one's six <c>AutoInclude()</c> calls by hand,
///         because the test project did not then reference
///         <c>EFCore.Relational.Specification.Tests</c>. ADR-013 made that reference available and
///         the hand copy is now the real thing: the base supplies the auto-includes, the
///         <c>ITestSqlLoggerFactory</c> the relational test bases need, and a <c>StoreName</c> this
///         class still overrides.
///     </para>
///     <para>
///         Its own <c>StoreName</c>, per CLAUDE.md: the Tier B store is file-backed and two
///         fixtures sharing a name share a database.
///     </para>
/// </remarks>
public class NavigationsQueryInfoCarrierFixture : NavigationsRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "NavigationsQueryInfoCarrierTest";

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
    ///     <b>The <c>UseTransaction</c> that CLAUDE.md says to write in the same commit as the
    ///     store switch — except that here it is the fixture, not the base, that carries it.</b>
    ///     Grepping these bases for <c>ExecuteWithStrategyInTransactionAsync</c> finds nothing;
    ///     what needs the override is <c>NavigationsRelationalFixtureBase.UseTransaction</c>
    ///     itself, which calls <c>transaction.GetDbTransaction()</c> and is unreachable on a client
    ///     with no database (ADR-013). The provider has its own enlistment (Phase T / M4).
    /// </remarks>
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

// The seven Navigations facets (ADR-004), on their relational bases since R25. Adopting these
// also satisfies the seven core `Navigations*TestBase` classes and the seven shared
// `Associations*TestBase` ones, which they derive from and which ComplianceTestBase resolves
// transitively.
//
// R25 deleted six overrides that this file used to restate by hand -- the two
// `Distinct_over_projected_*`, `Select_nested_collection_on_optional_associate`,
// `Over_associate_collection_projected` and the three `Nested_collection_*` ones. Every one was
// copied out of a `Navigations*RelationalTestBase`, and the re-parent inherits them verbatim.
// What is left below is EF's `Navigations*SqliteTest` overrides, which the relational bases do
// not carry: each is a limit of the backing store, raised here from the same place with the same
// message.
//
// The relational bases add no test of their own. What they add is `AssertSql`, and it is worth
// being honest about what that is worth here: it reads the *client's* TestSqlLoggerFactory, and
// this client has no database and emits no SQL. No base in this family calls it with an argument
// -- every call is the empty `AssertSql()` meaning "nothing was executed" -- so the assertion is
// true here but trivially so, weaker than it is on SQLite rather than false. `ServerSqlLog` is
// where the server's statements can actually be read.

public class NavigationsCollectionQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsCollectionRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
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

public class NavigationsIncludeQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsIncludeRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class NavigationsMiscellaneousQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsMiscellaneousRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class NavigationsPrimitiveCollectionQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsPrimitiveCollectionRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class NavigationsProjectionQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsProjectionRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
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

public class NavigationsSetOperationsQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsSetOperationsRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class NavigationsStructuralEqualityQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsStructuralEqualityRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);
