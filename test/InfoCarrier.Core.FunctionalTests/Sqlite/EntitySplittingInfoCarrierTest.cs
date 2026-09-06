// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>EntitySplittingTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         The write side of entity splitting. <c>EntitySplittingQueryInfoCarrierTest</c> covers
///         reading one entity back out of several tables; this covers saving one into them, which
///         nothing else in the suite does.
///     </para>
///     <para>
///         <b>Its second test is unreachable, and the reason is ADR-013.</b>
///         <c>ExecuteDelete_throws_for_entity_splitting</c> calls
///         <c>TestHelpers.ExecuteWithStrategyInTransactionAsync</c> inline, passing the base's
///         <c>public void UseTransaction</c>, which calls <c>GetDbTransaction()</c>. There is no
///         virtual hook between the test and that member, so unlike
///         <c>NonSharedModelUpdatesTestBase</c> there is nothing to override. It is left failing
///         rather than suppressed, and classified in <c>known-failures.txt</c>.
///     </para>
/// </remarks>
public class EntitySplittingInfoCarrierTest(NonSharedFixture fixture, ITestOutputHelper testOutputHelper)
    : EntitySplittingTestBase(fixture, testOutputHelper)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(SqliteInfoCarrierTier.Instance);

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
