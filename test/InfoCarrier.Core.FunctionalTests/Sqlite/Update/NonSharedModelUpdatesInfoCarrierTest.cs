// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Update;

/// <summary>
///     <c>NonSharedModelUpdatesTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         Saving a graph whose foreign keys form a cycle, and the entries a
///         <c>DbUpdateException</c> carries when several inserts fail at once. Both need a store
///         that enforces constraints, so InMemory cannot host them.
///     </para>
///     <para>
///         <b>The base's <c>UseTransaction</c> is <c>public void</c> and calls
///         <c>GetDbTransaction()</c>, which ADR-013 puts out of reach.</b> It is reachable anyway
///         because the method that <em>calls</em> it, <c>ExecuteWithStrategyInTransactionAsync</c>,
///         is <c>protected virtual</c>: overriding that one hands <c>TestHelpers</c> a different
///         enlistment and never touches the unreachable member. ADR-013's rule is about a base
///         whose only route runs through the non-virtual member; this base has another.
///     </para>
/// </remarks>
public class NonSharedModelUpdatesInfoCarrierTest(NonSharedFixture fixture) : NonSharedModelUpdatesTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.Sqlite);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    protected override Task ExecuteWithStrategyInTransactionAsync(
        ContextFactory<DbContext> contextFactory,
        Func<DbContext, Task> testOperation,
        Func<DbContext, Task>? nestedTestOperation1 = null,
        Func<DbContext, Task>? nestedTestOperation2 = null,
        Func<DbContext, Task>? nestedTestOperation3 = null)
        => TestHelpers.ExecuteWithStrategyInTransactionAsync(
            contextFactory.CreateContext,
            (facade, transaction) => facade.UseInfoCarrierTransaction(transaction),
            testOperation,
            nestedTestOperation1,
            nestedTestOperation2,
            nestedTestOperation3);

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
