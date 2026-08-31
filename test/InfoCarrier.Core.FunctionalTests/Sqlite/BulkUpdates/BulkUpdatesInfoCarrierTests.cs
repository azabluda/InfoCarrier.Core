// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.BulkUpdates;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.Sqlite.BulkUpdates;

/// <summary>
///     The four <c>BulkUpdates</c> bases on ADR-009 <b>Tier B</b> (C0), plus
///     <c>BulkUpdatesTestBase</c> which they all derive from and which
///     <c>ComplianceTestBase</c> therefore resolves transitively.
/// </summary>
/// <remarks>
///     <para>
///         <b>Tier B is not a judgement call here</b>: <c>EFCore.InMemory.FunctionalTests</c>
///         contains no <c>BulkUpdates</c> file at all, because <c>ExecuteUpdate</c> and
///         <c>ExecuteDelete</c> are things a store either implements or does not. SQLite ships
///         fifteen.
///     </para>
///     <para>
///         <b>These were adopted red and are now green, and the gap between was one name.</b>
///         Both operations reach a provider as ordinary query trees — <c>ExecuteDelete</c> is
///         <c>Provider.Execute&lt;int&gt;(Call(ExecuteDeleteMethodInfo, source.Expression))</c>, and
///         <c>ExecuteUpdate</c> builds its setters before calling the provider, so the
///         <c>Action&lt;UpdateSettersBuilder&lt;T&gt;&gt;</c> never enters the tree. C0 read that as
///         "probably a pure adoption"; C3 and C4 read the resulting
///         <c>UnreachableException: Can't call this overload directly</c> as proof that the split
///         evaluated the call on the client and that shipping the operators was new product scope.
///         **Both readings were half right.** The split did evaluate it on the client — because
///         <c>ExecuteUpdate</c>'s rewritten call names <c>IReadOnlyList&lt;ITuple&gt;</c> and
///         <c>ITuple</c> was not on the ADR-008 allowlist, so the call was refused as unshippable.
///         Adding it closed 153 (C19). <c>ExecuteDelete</c> had never been broken at all.
///     </para>
/// </remarks>
public class NorthwindBulkUpdatesInfoCarrierFixture<TModelCustomizer>
    : NorthwindBulkUpdatesRelationalFixture<TModelCustomizer>
    where TModelCustomizer : ITestModelCustomizer, new()
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            copyDbContextParameters: (client, server) =>
                ((NorthwindContext)server).TenantPrefix = ((NorthwindContext)client).TenantPrefix,
            serverContextType: typeof(NorthwindInfoCarrierSqliteServerContext),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    /// <remarks>
    ///     The base runs each test in a transaction and has a second context observe the same
    ///     uncommitted state; the provider's own enlistment is what C3 established this needs.
    /// </remarks>
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

/// <summary>
///     The inheritance fixture for the two inheritance bulk-update classes, TPH — the default
///     strategy, and the one EF's own <c>TPHInheritanceBulkUpdatesSqliteFixture</c> uses.
/// </summary>
public class InheritanceBulkUpdatesInfoCarrierFixture : InheritanceBulkUpdatesFixtureBase, ITestSqlLoggerFactory
{
    /// <summary>
    ///     The compliance gate's second assertion (R54). The property is real —
    ///     <c>InfoCarrierTestStoreFactory.CreateListLoggerFactory</c> returns a
    ///     <c>TestSqlLoggerFactory</c> — but what it observes is the <em>client's</em> log, and
    ///     this client has no database and emits no SQL. <c>ServerSqlLog</c> is where the
    ///     server's statements can actually be read.
    /// </summary>
    public TestSqlLoggerFactory TestSqlLoggerFactory
        => (TestSqlLoggerFactory)ListLoggerFactory;

    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "InheritanceBulkUpdatesInfoCarrierTest";

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);

    /// <inheritdoc cref="NorthwindBulkUpdatesInfoCarrierFixture{TModelCustomizer}.UseTransaction" />
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

public class FiltersInheritanceBulkUpdatesInfoCarrierFixture : InheritanceBulkUpdatesInfoCarrierFixture
{
    /// <inheritdoc />
    public override bool EnableFilters
        => true;

    /// <inheritdoc />
    protected override string StoreName
        => "FiltersInheritanceBulkUpdatesInfoCarrierTest";
}

/// <summary>
///     <c>NorthwindBulkUpdatesTestBase</c> on Tier B.
/// </summary>
/// <remarks>
///     <para>
///         The overrides below are EF's, taken after C19 made the class mostly green, and they
///         come from <b>two</b> assemblies for the reason Phase C kept rediscovering: a limit
///         every relational provider has is stated on
///         <c>NorthwindBulkUpdatesRelationalTestBase</c>, not in SQLite's own suite, and
///         `EFCore.Relational.Specification.Tests` is not referenced here — so they are mirrored
///         by hand, each matched by reason against a measured failure first (A63).
///     </para>
///     <para>
///         Four failures are deliberately left red: two are EF issue #28886, which EF's own
///         SQLite suite carries as a <c>[ConditionalTheory(Skip = …)]</c> and which reproduces
///         here exactly (<c>SQLite Error 1: 'no such column'</c>) — recorded rather than skipped,
///         as `PrimitiveCollectionsQuery`'s EF issue #30730 already is. The other two are
///         <c>Update_with_invalid_lambda_in_set_property_throws</c>; see C20.
///     </para>
/// </remarks>
public class NorthwindBulkUpdatesInfoCarrierTest(
    NorthwindBulkUpdatesInfoCarrierFixture<NoopModelCustomizer> fixture,
    ITestOutputHelper testOutputHelper)
    : NorthwindBulkUpdatesRelationalTestBase<NorthwindBulkUpdatesInfoCarrierFixture<NoopModelCustomizer>>(
        fixture,
        testOutputHelper)
{
    // --- SQLite has no APPLY. Stated in EF's own SQLite suite, and the message this provider
    // surfaces is `SqliteStrings.ApplyNotSupported` character for character.

    /// <inheritdoc />
    public override async Task Delete_with_cross_apply(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Delete_with_cross_apply(async))).Message);

    /// <inheritdoc />
    public override async Task Delete_with_outer_apply(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Delete_with_outer_apply(async))).Message);

    /// <inheritdoc />
    public override async Task Update_with_cross_apply_set_constant(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Update_with_cross_apply_set_constant(async))).Message);

    /// <inheritdoc />
    public override async Task Update_with_outer_apply_set_constant(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Update_with_outer_apply_set_constant(async))).Message);

    /// <inheritdoc />
    public override async Task Update_with_cross_join_cross_apply_set_constant(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Update_with_cross_join_cross_apply_set_constant(async))).Message);

    /// <inheritdoc />
    public override async Task Update_with_cross_join_outer_apply_set_constant(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Update_with_cross_join_outer_apply_set_constant(async))).Message);

    // --- EF's own open bug, adopted verbatim (C94). `NorthwindBulkUpdatesSqliteTest` carries
    // `[ConditionalTheory(Skip = "Issue#28886")]` on exactly these two, and the SQL it records
    // under the skip is the truncated `UPDATE` this provider produces character for character —
    // `no such column: c.CustomerID` / `o.OrderID`. **Issue #28886 is SQLite's**: it appears
    // nowhere in EF's SQL Server suite, and these classes are Tier B, so adopting the attribute
    // here loses nothing when M7 brings a Tier C.
    //
    // A63's rule, which is the one that permits this at all: adopting EF's *own* override where
    // the reason matches is not the suppression CLAUDE.md forbids. What is forbidden is inventing
    // a skip; this is recording that the reference provider fails the same test for the same
    // documented reason.

    /// <inheritdoc />
    [ConditionalTheory(Skip = "Issue#28886"), MemberData(nameof(IsAsyncData))]
    public override Task Update_with_cross_join_left_join_set_constant(bool async)
        => base.Update_with_cross_join_left_join_set_constant(async);

    /// <inheritdoc />
    [ConditionalTheory(Skip = "Issue#28886"), MemberData(nameof(IsAsyncData))]
    public override Task Update_with_two_inner_joins(bool async)
        => base.Update_with_two_inner_joins(async);
}

public class InheritanceBulkUpdatesInfoCarrierTest(InheritanceBulkUpdatesInfoCarrierFixture fixture)
    : InheritanceBulkUpdatesTestBase<InheritanceBulkUpdatesInfoCarrierFixture>(fixture);

public class FiltersInheritanceBulkUpdatesInfoCarrierTest(FiltersInheritanceBulkUpdatesInfoCarrierFixture fixture)
    : FiltersInheritanceBulkUpdatesTestBase<FiltersInheritanceBulkUpdatesInfoCarrierFixture>(fixture);

/// <summary>
///     The non-shared-model variant, through the same harness
///     <c>NonSharedPrimitiveCollectionsQuerySqliteInfoCarrierTest</c> uses.
/// </summary>
/// <remarks>
///     <b>R33 re-parented this onto <c>NonSharedModelBulkUpdatesRelationalTestBase</c>.</b> That
///     base takes the same <c>NonSharedFixture</c> and adds six tests of its own; it carries no
///     <c>FromSql</c>, no <c>AsSplitQuery</c> and no <c>RelationalTestStore</c> cast, which is what
///     separates it from <c>NorthwindBulkUpdatesRelationalTestBase</c> — that one adds two
///     <c>FromSqlRaw</c> tests and is gated on #60.
/// </remarks>
public class NonSharedModelBulkUpdatesInfoCarrierTest(NonSharedFixture fixture)
    : NonSharedModelBulkUpdatesRelationalTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.Sqlite);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc cref="NorthwindBulkUpdatesInfoCarrierFixture{TModelCustomizer}.UseTransaction" />
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    /// <inheritdoc />
    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<IServiceCollection, IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true)
    {
        Fixture = null;
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddOptions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
