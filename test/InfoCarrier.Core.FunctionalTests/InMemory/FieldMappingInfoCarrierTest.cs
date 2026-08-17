// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>FieldMappingTestBase</c> on Tier A: entities whose state lives in backing fields rather
///     than properties, including read-only collections and fields with no property at all.
/// </summary>
/// <remarks>
///     Adopted because this provider reads and writes through backing fields in several places
///     it had to learn the hard way — L6 (a navigation is read through its field, never its
///     property, or reading it *is* a lazy load) and L18 (a shadow navigation has no member at
///     all). Those were found through other bases; this one aims at the behaviour directly.
/// </remarks>
public class FieldMappingInfoCarrierTest(FieldMappingInfoCarrierTest.InfoCarrierFixture fixture)
    : FieldMappingTestBase<FieldMappingInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     Mirrors <c>FieldMappingInMemoryTest</c>: the update tests dirty the shared store, and
    ///     the backend does not roll back.
    /// </remarks>
    protected override async Task UpdateAsync<TBlog>(string navigation)
    {
        await base.UpdateAsync<TBlog>(navigation);
        await Fixture.ReseedAsync();
    }

    public class InfoCarrierFixture : FieldMappingFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "FieldMappingInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     Reseeds through the <em>backend</em> context, not the client one: seeding through
        ///     the client would make every test's setup depend on remoted SaveChanges, which is
        ///     part of what is under test.
        /// </remarks>
        public override async Task ReseedAsync()
        {
            InfoCarrierBackendTestStore backend = ((InfoCarrierTestStore)TestStore).Backend;
            using DbContext context = backend.CreateDbContext();
            await backend.CleanAsync(context);
            await CleanAsync(context);
            await SeedAsync((PoolableDbContext)context);
        }
    }
}
