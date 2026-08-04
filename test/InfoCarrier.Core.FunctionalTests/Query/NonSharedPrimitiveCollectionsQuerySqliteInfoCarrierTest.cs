// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <c>NonSharedPrimitiveCollectionsQueryTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     One model per element type — <c>int[]</c>, <c>List&lt;DateTime&gt;</c>, an enum array, a
///     nullable one — each queried through <c>Count</c>, <c>Contains</c> and an indexer, which is
///     the question of whether a collection-valued property survives the round trip at all.
///     <para>
///         <b>Tier B</b>, by the rule A79 established: EF ships
///         <c>NonSharedPrimitiveCollectionsQuerySqliteTest</c> and no InMemory counterpart, because
///         a primitive collection is a thing a store either maps or does not. The core base rather
///         than the relational one, which asserts SQL a client with no database does not have.
///     </para>
/// </remarks>
public class NonSharedPrimitiveCollectionsQuerySqliteInfoCarrierTest(NonSharedFixture fixture)
    : NonSharedPrimitiveCollectionsQueryTestBase(fixture)
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
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
