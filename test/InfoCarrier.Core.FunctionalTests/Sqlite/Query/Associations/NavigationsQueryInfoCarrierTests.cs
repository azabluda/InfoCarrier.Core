// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Associations.Navigations;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Sdk;

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
            configureConventions: ConfigureConventions,
            relationalClientStore: true,
            arbitrarySqlExecution: true);

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
        => facade.UseTestTransaction(transaction);
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
// true here but trivially so, weaker than it is on SQLite rather than false. The server's
// statements are read by `ServerSqlRecorder`, and compared with plain EF's by the slow run of #167.

public class NavigationsCollectionQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsCollectionRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper)
{
    /// <summary>
    ///     <c>NavigationsCollectionSqliteTest</c>'s: the query reaches SQL and asks SQLite for
    ///     <c>APPLY</c>, which it does not have.
    /// </summary>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsCollectionSqliteTest.cs", 11, 12,
        Justification = Upstream.GaveNoReason)]
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
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsProjectionSqliteTest.cs", 11, 12,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_subquery_FirstOrDefault_complex_collection(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_FirstOrDefault_complex_collection(queryTrackingBehavior));

    /// <summary>
    ///     <c>NavigationsProjectionSqliteTest</c>'s two, both <c>APPLY</c>.
    /// </summary>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsProjectionSqliteTest.cs", 14, 15,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsProjectionSqliteTest.cs", 17, 18,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NavigationsCollectionQueryInfoCarrierTest.AssertApplyNotSupported(
            () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));
}

public class NavigationsSetOperationsQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsSetOperationsRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class NavigationsBulkUpdateQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsBulkUpdateRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper)
    {
    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 16, 18,
        Justification = "FK constraint failures (SQLite enforces FK constraints on DELETE, blocking cascade)", Deviation = DeviationKind.StoreExceptionAsData)]
    public override Task Delete_entity_with_associations()
        => SqliteStoreRefusal.AssertAsync(base.Delete_entity_with_associations, "FOREIGN KEY constraint failed");

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 20, 21,
        Justification = "FK constraint failures (SQLite enforces FK constraints on DELETE, blocking cascade)", Deviation = DeviationKind.StoreExceptionAsData)]
    public override Task Delete_required_associate()
        => SqliteStoreRefusal.AssertAsync(base.Delete_required_associate, "FOREIGN KEY constraint failed");

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 23, 24,
        Justification = "FK constraint failures (SQLite enforces FK constraints on DELETE, blocking cascade)", Deviation = DeviationKind.StoreExceptionAsData)]
    public override Task Delete_optional_associate()
        => SqliteStoreRefusal.AssertAsync(base.Delete_optional_associate, "FOREIGN KEY constraint failed");

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 26, 28,
        Justification = "Translation not yet supported for navigation-mapped associations")]
    public override Task Update_associate_to_parameter()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_associate_to_parameter);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 30, 31,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_associate_to_inline()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_associate_to_inline);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 33, 34,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_associate_to_inline_with_lambda()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_associate_to_inline_with_lambda);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 36, 37,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_associate_to_another_associate()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_associate_to_another_associate);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 39, 40,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_associate_to_null()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_associate_to_null);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 42, 43,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_associate_to_null_with_lambda()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_associate_to_null_with_lambda);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 45, 46,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_associate_to_null_parameter()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_associate_to_null_parameter);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 48, 49,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_nested_associate_to_parameter()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_nested_associate_to_parameter);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 51, 52,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_nested_associate_to_inline_with_lambda()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_nested_associate_to_inline_with_lambda);

    /// <inheritdoc />
    [StoreDefect("1.14",
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 54, 55,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_nested_associate_to_another_nested_associate()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_nested_associate_to_another_nested_associate);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 57, 58,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_nested_collection_to_parameter()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_nested_collection_to_parameter);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 60, 61,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_nested_collection_to_inline_with_lambda()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_nested_collection_to_inline_with_lambda);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 63, 64,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_nested_collection_to_another_nested_collection()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_nested_collection_to_another_nested_collection);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 66, 67,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_collection_to_parameter()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_collection_to_parameter);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 69, 70,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_collection_referencing_the_original_collection()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_collection_referencing_the_original_collection);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 72, 73,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_primitive_collection_to_another_collection()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_primitive_collection_to_another_collection);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 75, 76,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_inside_structural_collection()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_inside_structural_collection);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 78, 79,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_multiple_properties_inside_associates_and_on_entity_type()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_multiple_properties_inside_associates_and_on_entity_type);

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 81, 82,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_multiple_projected_associates_via_anonymous_type()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Update_multiple_projected_associates_via_anonymous_type);

    /// <inheritdoc />
    [StoreDefect("1.15",
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/Navigations/NavigationsBulkUpdateSqliteTest.cs", 84, 85,
        Justification = Upstream.GaveNoReason)]
    public override Task Update_property_on_projected_associate_with_OrderBy_Skip()
        => Assert.ThrowsAsync<EqualException>(base.Update_property_on_projected_associate_with_OrderBy_Skip);


    }

public class NavigationsStructuralEqualityQueryInfoCarrierTest(
    NavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : NavigationsStructuralEqualityRelationalTestBase<NavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);
