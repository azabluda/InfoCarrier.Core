// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <c>AdHocComplexTypeQueryTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     EF's complex-type regression corpus, one model per reported bug. Adoptable since A32 made
///     complex types work and A49 built the harness; the two forwarded members below are the whole
///     of the wiring, as in every other <c>NonSharedModelTestBase</c> suite here.
/// </remarks>
public class AdHocComplexTypeQueryInfoCarrierTest(NonSharedFixture fixture)
    : AdHocComplexTypeQueryTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.InMemory);

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
