// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>AdHocManyToManyQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         The <c>AdHoc*</c> bases are EF's regression corpus: each test is a model built for one
///         reported bug, built <em>per test</em> rather than shared, which is why they need
///         <see cref="NonSharedModelInfoCarrierHarness" /> (A49). The two overrides below are the
///         whole of the wiring, and are the same in every class of this kind.
///     </para>
///     <para>
///         <b>Moved here from Tier A by R46, not added alongside it</b> — a base belongs to
///         exactly one tier. The relational base adds no tests of its own: its whole contribution
///         is a <c>TestSqlLoggerFactory</c>, a <c>ClearLog</c> and an <c>AssertSql</c>, which is
///         why the move is a re-parent and nothing more. EF's own
///         <c>AdHocManyToManyQuerySqliteTest</c> is twelve lines with no overrides, so the store
///         asks for nothing either.
///     </para>
/// </remarks>
public class AdHocManyToManyQuerySqliteInfoCarrierTest(NonSharedFixture fixture)
    : AdHocManyToManyQueryRelationalTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.Sqlite);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

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

/// <inheritdoc cref="AdHocManyToManyQuerySqliteInfoCarrierTest" />
public class AdHocQueryFiltersQuerySqliteInfoCarrierTest(NonSharedFixture fixture)
    : AdHocQueryFiltersQueryRelationalTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.Sqlite);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

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

/// <summary>
///     <c>AdHocAdvancedMappingsQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         Moved here from Tier A by R47. Unlike its two siblings above, this relational base does
///         add tests — seven — and all seven pass: <c>Passed: 38, Failed: 1, Total: 39</c> on
///         Tier A becomes <c>Passed: 45, Failed: 1, Total: 46</c> here, the one failure being the
///         same pre-existing <c>Casts_are_removed_from_expression_tree_when_redundant</c> on both.
///     </para>
///     <para>
///         <b>Four of the seven are the only TPT and TPC coverage anywhere in this repository</b>,
///         which <c>CLAUDE.md</c> names as the one real gap left by M7's dropped SQL Server tier. What
///         they establish is bounded and worth stating exactly: EF's <c>Context28196</c> tests are
///         regression tests for a crash, so they run
///         <c>Animals.OfType&lt;Pet&gt;().Where(a =&gt; a.Species.StartsWith("F"))</c> against a
///         <c>UseTpcMappingStrategy</c> and a <c>UseTptMappingStrategy</c> model and assert
///         nothing about the result. So this says the client builds such a model and the server
///         answers the query without throwing. It does not say TPT or TPC is correct.
///     </para>
///     <para>
///         <b>Two more use <c>AsSplitQuery()</c>, and they pass because the marker is silently
///         ignored rather than because splitting works.</b> Established, not assumed:
///         <c>INFOCARRIER_SERVER_SQL=1</c> on
///         <c>Two_similar_complex_properties_projected_with_split_query1</c> shows the server
///         executing <em>one</em> <c>SELECT</c> with a <c>LEFT JOIN</c>, where a split query is
///         two. A single query gives the same answers, so the assertion holds. <b>This is a finding
///         for #60 rather than a reason not to adopt</b>: nothing is red, and a consumer calling
///         <c>AsSplitQuery</c> here gets correct results from an unsplit query and no diagnostic.
///     </para>
/// </remarks>
public class AdHocAdvancedMappingsQuerySqliteInfoCarrierTest(NonSharedFixture fixture)
    : AdHocAdvancedMappingsQueryRelationalTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.Sqlite);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

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
