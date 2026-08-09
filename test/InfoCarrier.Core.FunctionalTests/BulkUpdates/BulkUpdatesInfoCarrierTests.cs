// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.BulkUpdates;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.BulkUpdates;

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
///         <b>These are adopted knowing they fail, and C3 is why.</b> Both operations reach a
///         provider as ordinary query trees — <c>ExecuteDelete</c> is
///         <c>Provider.Execute&lt;int&gt;(Call(ExecuteDeleteMethodInfo, source.Expression))</c>, and
///         <c>ExecuteUpdate</c> builds its setters before calling the provider, so the
///         <c>Action&lt;UpdateSettersBuilder&lt;T&gt;&gt;</c> never enters the tree. C0 read that as
///         "probably a pure adoption". C3 showed the missing half: the projection split
///         <em>evaluates</em> the call on the client, where
///         <c>EntityFrameworkQueryableExtensions.ExecuteUpdate(IQueryable, IReadOnlyList&lt;…&gt;)</c>
///         is a marker overload that throws <c>UnreachableException: Can't call this overload
///         directly</c>. Shipping the two operators to the server instead is **product work and new
///         scope** — the roadmap mentions neither — so the bases are adopted and left red rather
///         than the scope being absorbed. See C4 in docs/implementation-plan.md.
///     </para>
/// </remarks>
public class NorthwindBulkUpdatesInfoCarrierFixture<TModelCustomizer>
    : NorthwindBulkUpdatesFixture<TModelCustomizer>
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
public class InheritanceBulkUpdatesInfoCarrierFixture : InheritanceBulkUpdatesFixtureBase
{
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

public class NorthwindBulkUpdatesInfoCarrierTest(NorthwindBulkUpdatesInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindBulkUpdatesTestBase<NorthwindBulkUpdatesInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class InheritanceBulkUpdatesInfoCarrierTest(InheritanceBulkUpdatesInfoCarrierFixture fixture)
    : InheritanceBulkUpdatesTestBase<InheritanceBulkUpdatesInfoCarrierFixture>(fixture);

public class FiltersInheritanceBulkUpdatesInfoCarrierTest(FiltersInheritanceBulkUpdatesInfoCarrierFixture fixture)
    : FiltersInheritanceBulkUpdatesTestBase<FiltersInheritanceBulkUpdatesInfoCarrierFixture>(fixture);

/// <summary>
///     The non-shared-model variant, through the same harness
///     <c>NonSharedPrimitiveCollectionsQuerySqliteInfoCarrierTest</c> uses.
/// </summary>
public class NonSharedModelBulkUpdatesInfoCarrierTest(NonSharedFixture fixture)
    : NonSharedModelBulkUpdatesTestBase(fixture)
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
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
