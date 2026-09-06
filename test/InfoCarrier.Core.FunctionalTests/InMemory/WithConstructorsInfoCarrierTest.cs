// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

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

    /// <summary>
    ///     The <em>server-side</em> context: the shared model plus the InMemory defining query that
    ///     produces the keyless <c>BlogQuery</c>'s rows.
    /// </summary>
    /// <remarks>
    ///     The base maps <c>BlogQuery</c> as keyless and stops there; where its rows come from is
    ///     the store's business, and EF's own <c>WithConstructorsInMemoryFixture</c> supplies them
    ///     with <c>ToInMemoryQuery</c>. This context carries it, and BOTH sides build from it.
    ///     <para>
    ///         <b>It used to sit on a separate server context, on the belief that a defining query
    ///         cannot be part of the client's model because the client has no store to run it
    ///         against.</b> That belief was measured and is wrong: the client holds the annotation
    ///         and never runs it, and converging every fixture onto one context class changed no
    ///         answer anywhere in the suite. Version 1 of this provider never split them.
    ///     </para>
    /// </remarks>
    public class WithConstructorsInfoCarrierContext(DbContextOptions options)
        : WithConstructorsContext(options)
    {
        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BlogQuery>().HasNoKey()
                .ToInMemoryQuery(() => Set<Blog>().Select(b => new BlogQuery(b.Title, b.MonthlyRevenue)));
        }
    }

    public class InfoCarrierFixture : WithConstructorsFixtureBase
    {
        /// <inheritdoc />
        /// <remarks>
        ///     Both sides build from ONE <c>OnModelCreating</c>, as version 1 of this provider did.
        /// </remarks>
        protected override Type ContextType
            => typeof(WithConstructorsInfoCarrierContext);

        private ITestStoreFactory? _testStoreFactory;

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
            InfoCarrierBackendTestStore backend = ((IInfoCarrierClientTestStore)TestStore).Backend;
            using DbContext context = backend.CreateDbContext();
            await backend.CleanAsync(context);
            await CleanAsync(context);
            await SeedAsync((WithConstructorsContext)context);
        }
    }
}
