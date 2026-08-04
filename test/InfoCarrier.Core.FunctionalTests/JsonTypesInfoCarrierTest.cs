// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>JsonTypesTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     <para>
///         Every mapped type written and read back through its <c>JsonValueReaderWriter</c> — which
///         is exactly the mechanism A34 made this provider's fallback for a value the wire has no
///         primitive for. The base builds its model per test, so the two forwarded members are
///         A49's <see cref="NonSharedModelInfoCarrierHarness" />.
///     </para>
///     <para>
///         **EF's eight spatial overrides are deliberately not adopted.** They assert
///         <c>NullReferenceException</c>, which is what InMemory raises when it maps a spatial type
///         and then cannot write it as JSON. This provider raises <c>InvalidOperationException</c>
///         one step earlier — <c>InfoCarrierTypeMappingSource</c> maps no spatial type at all, so
///         the model never builds. Copying the override would assert a symptom this provider does
///         not have (A39); leaving the whole family red says the true thing, which is that spatial
///         support is absent.
///     </para>
/// </remarks>
public class JsonTypesInfoCarrierTest(NonSharedFixture fixture) : JsonTypesTestBase(fixture)
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
