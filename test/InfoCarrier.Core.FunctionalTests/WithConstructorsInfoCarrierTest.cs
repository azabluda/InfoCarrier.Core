// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>WithConstructorsTestBase</c> on Tier A: entities with no parameterless constructor,
///     bound by parameter name to properties, foreign keys and injected services.
/// </summary>
/// <remarks>
///     This is the base that aims at what L1 discovered indirectly. Building entities with
///     <c>Activator.CreateInstance</c> skips EF's materializer, so constructor binding and
///     service-property injection never run — which is why no entity had a working
///     <c>ILazyLoader</c> until the client started going through
///     <c>GetOrCreateMaterializer</c>. A constructor-bound model exercises that directly.
/// </remarks>
public class WithConstructorsInfoCarrierTest(WithConstructorsInfoCarrierTest.InfoCarrierFixture fixture)
    : WithConstructorsTestBase<WithConstructorsInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     Mirrors <c>WithConstructorsInMemoryTest</c>: the update dirties the shared store.
    /// </remarks>
    public override async Task Query_and_update_using_constructors_with_property_parameters()
    {
        await base.Query_and_update_using_constructors_with_property_parameters();
        await Fixture.ReseedAsync();
    }

    public class InfoCarrierFixture : WithConstructorsFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "WithConstructorsInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        public override async Task ReseedAsync()
        {
            InfoCarrierBackendTestStore backend = ((InfoCarrierTestStore)TestStore).Backend;
            using DbContext context = backend.CreateDbContext();
            await backend.CleanAsync(context);
            await CleanAsync(context);
            await SeedAsync((WithConstructorsContext)context);
        }
    }
}
