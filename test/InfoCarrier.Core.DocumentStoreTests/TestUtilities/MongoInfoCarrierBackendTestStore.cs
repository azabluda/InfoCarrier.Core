// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.DocumentStoreTests.TestUtilities;

/// <summary>
///     ADR-009 Tier D's backend store for the specification bases: an embedded MongoDB behind the
///     shared harness.
/// </summary>
/// <remarks>
///     <para>
///         <b>The fourth of these, and the first whose store is not a database of tables.</b> The
///         other three are in the spec project beside the tiers they serve; this one is here for
///         the same reason its tier is a project of its own, which is that
///         <c>MongoDB.EntityFrameworkCore</c> needs a newer EF Core than <c>src/</c> is built
///         against.
///     </para>
///     <para>
///         <b>ONE SERVER PER STORE.</b> A shared server with a database per store was measured on
///         2026-09-11 and does not isolate; per-store servers do. See
///         <see cref="EmbeddedMongo" /> for the process bookkeeping that makes stopping reliable.
///     </para>
///     <para>
///         <b>NO <c>ToCollection</c> ANYWHERE, AND THAT IS A CONSTRAINT RATHER THAN A STYLE.</b>
///         The harness builds ONE model from ONE customization and gives it to both halves, and the
///         client has no MongoDB provider, so a model that names a Mongo-only API cannot be built
///         on the client at all. The collection name therefore has to come from the provider's own
///         convention. <see cref="ShopServerContext" /> can say <c>ToCollection</c> because it is a
///         hand-written server context with a hand-written client twin; a specification fixture has
///         no such twin.
///     </para>
/// </remarks>
public class MongoInfoCarrierBackendTestStore(
    string name,
    bool shared,
    SharedTestStoreProperties testStoreProperties)
    : InfoCarrierBackendTestStore(name, shared, testStoreProperties)
{
    private EmbeddedMongo? _server;

    /// <summary>
    ///     Knowledge the client cannot derive, and the whole reason this tier exists (#51, #100).
    /// </summary>
    /// <remarks>
    ///     False here makes the client send a whole document with any change to part of one, which
    ///     is what a store with no partial write requires. The server half,
    ///     <c>AddInfoCarrierServerDocumentStore()</c>, is registered by
    ///     <see cref="MongoInfoCarrierTier" />.
    /// </remarks>
    public override bool ServerStoreIsRelational => false;

    /// <inheritdoc />
    /// <remarks>
    ///     The harness builds its own internal service provider for the server, so the provider's
    ///     own services have to be registered by name. <c>DocumentStoreFixture</c> does not need
    ///     this because it lets EF build the internal provider for it.
    /// </remarks>
    protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
        => serviceCollection
            .AddEntityFrameworkMongoDB()
            .AddSingleton<TestStoreIndex>();

    /// <inheritdoc />
    /// <remarks>
    ///     <b>PER STORE, NOT PER PROCESS, AND THE DEFAULT BREAKS THIS TIER.</b> EF's
    ///     <see cref="TestStoreIndex" /> remembers which store NAMES have been created, and its
    ///     default instance is process-global. Here a store name is not unique to a server: every
    ///     fixture sharing a <c>StoreName</c> starts a <c>mongod</c> of its own, so the second
    ///     fixture is told its store already exists and skips seeding a database that is empty on
    ///     ITS server.
    ///     <c>InMemoryInfoCarrierBackendTestStore</c> does the same thing for the same reason.
    /// </remarks>
    protected override TestStoreIndex GetTestStoreIndex(IServiceProvider? serviceProvider)
        => serviceProvider?.GetService<TestStoreIndex>() ?? base.GetTestStoreIndex(serviceProvider);

    /// <inheritdoc />
    /// <remarks>
    ///     The database is named for the store, so two stores on one process never share one even
    ///     though they do not share a server either.
    /// </remarks>
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => base.AddProviderOptions(builder)
            .UseMongoDB(
                _server?.ConnectionString
                ?? throw new InvalidOperationException(
                    $"{nameof(MongoInfoCarrierBackendTestStore)} was asked for options before its "
                    + "server started. InitializeAsync starts it, and nothing may build a context "
                    + "before that."),
                Name);

    /// <inheritdoc />
    /// <remarks>
    ///     The server starts before anything can ask for a context, because
    ///     <see cref="AddProviderOptions" /> needs its connection string and is called per context
    ///     resolution.
    /// </remarks>
    public override async Task<TestStore> InitializeAsync(
        IServiceProvider? serviceProvider,
        Func<DbContext>? createContext,
        Func<DbContext, Task>? seed = null,
        Func<DbContext, Task>? clean = null)
    {
        _server ??= await EmbeddedMongo.StartAsync();

        return await base.InitializeAsync(serviceProvider, createContext, seed, clean);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Dropping the database is the only way to empty this store: MongoDB has no truncate, and
    ///     a collection left behind by one test class is data the next one would read.
    /// </remarks>
    public override async Task CleanAsync(DbContext context)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (_server is { } server)
        {
            _server = null;
            await server.DisposeAsync();
        }
    }
}
