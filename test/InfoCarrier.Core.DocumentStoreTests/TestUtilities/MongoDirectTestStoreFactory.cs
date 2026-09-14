// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.DocumentStoreTests.TestUtilities;

/// <summary>
///     The same MongoDB store with NO InfoCarrier client in front of it: plain EF Core, straight at
///     the database.
/// </summary>
/// <remarks>
///     <para>
///         <b>THIS EXISTS TO ANSWER ONE QUESTION AND IT IS NOT A TIER.</b> A red specification test
///         on Tier D means nothing until somebody has asked whether the STORE can answer the query
///         at all. Running the same bases through this factory answers it for every test at once:
///         what is red here and red on Tier D is <c>MongoDB.EntityFrameworkCore</c>'s behaviour,
///         and what is red on Tier D alone is this repository's.
///     </para>
///     <para>
///         <b>IT IS ALSO A CONFLICT OF INTEREST, AND THAT IS WHY <c>eng/tier-d-control.py</c>
///         EXISTS.</b> The worse this control is wired, the more it fails, and the more of Tier D's
///         reds get attributed to the store rather than to us. The bias runs one way and it needs
///         no bad intent to operate. So the integrity of this control is GATED rather than trusted:
///         that script fails the build if this control fails anything Tier D passes, outside a
///         named allowance list. A sloppier control breaks CI instead of flattering us.
///     </para>
///     <para>
///         <b>The properties below are the real ones, and passing <c>null</c> for
///         <c>OnModelCreating</c> was the first version's bug (2026-09-14).</b>
///         <see cref="InfoCarrierBackendTestStore" /> builds a server context of its own out of
///         these, so an absent model customization would have given this control a DIFFERENT model
///         from the one Tier D measures — which is precisely the sloppiness that biases the result.
///         The fixture hands in its own <c>OnModelCreating</c>, exactly as
///         <c>OwnedNavigationsInfoCarrierFixture</c> hands it to
///         <see cref="InfoCarrierTestStoreFactory" />.
///     </para>
///     <para>
///         <b>No <c>AddInfoCarrierServerDocumentStore()</c>, and that is the one deliberate
///         difference.</b> It is the server half of #100, which repairs an incomplete change set
///         arriving over the wire. There is no wire here, so there is nothing to repair.
///     </para>
/// </remarks>
public sealed class MongoDirectTestStoreFactory(Action<ModelBuilder, DbContext> onModelCreating) : ITestStoreFactory
{
    /// <inheritdoc />
    public TestStore Create(string storeName)
        => new MongoInfoCarrierBackendTestStore(storeName, shared: false, Properties());

    /// <inheritdoc />
    public TestStore GetOrCreate(string storeName)
        => new MongoInfoCarrierBackendTestStore(storeName, shared: true, Properties());

    /// <inheritdoc />
    /// <remarks>
    ///     The store's own services and nothing else. <see cref="TestStoreIndex" /> is per store
    ///     here for the reason <see cref="MongoInfoCarrierBackendTestStore" /> gives: a store name
    ///     is not unique to a server on this tier, and the default index is process-global.
    /// </remarks>
    public IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
        => serviceCollection
            .AddEntityFrameworkMongoDB()
            .AddSingleton<TestStoreIndex>();

    /// <inheritdoc />
    public ListLoggerFactory CreateListLoggerFactory(Func<string, bool> shouldLogCategory)
        => new(shouldLogCategory);

    private SharedTestStoreProperties Properties()
        => new()
        {
            ContextType = typeof(PoolableDbContext),
            OnModelCreating = onModelCreating,
        };
}
