// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The Tier A harness for EF's <c>NonSharedModelTestBase</c> suites — the <c>AdHoc*</c> query
///     bases and everything else that builds its model per test.
/// </summary>
/// <remarks>
///     <para>
///         Every other fixture in this suite has <em>one</em> <see cref="DbContext" /> type for its
///         lifetime, and <see cref="InfoCarrierTestStoreFactory" /> captures it up front because
///         <see cref="ITestStoreFactory" />'s members take only a store name. A
///         <c>NonSharedModelTestBase</c> does not: each test calls
///         <c>InitializeAsync&lt;TContext&gt;</c> with a context type and, sometimes, a model
///         customization of its own. The backend store builds its <em>server</em> service provider
///         eagerly from those, so they have to be in hand before the store is created.
///     </para>
///     <para>
///         This is a <b>mixin, not a base class</b>: the spec bases already derive from
///         <c>NonSharedModelTestBase</c>, so an adopting class holds one of these and forwards two
///         members to it. <c>CreateContextFactory&lt;TContext&gt;</c> is the hook — EF's base calls
///         it before <c>CreateTestStore</c>, and it is where <c>TContext</c> first exists.
///     </para>
///     <para>
///         The adopting class must also clear <c>Fixture</c>. <c>NonSharedFixture</c> caches one
///         store for the whole test class, which is sound for a provider whose store is just a
///         database name and wrong here, because this store carries a server provider built for one
///         context type. One backend per test costs time and is the only thing that can be correct.
///     </para>
/// </remarks>
/// <param name="backend">The backing provider, usually <c>InfoCarrierTestStoreFactory.InMemory</c>.</param>
public sealed class NonSharedModelInfoCarrierHarness(
    InfoCarrierTestStoreFactory.InfoCarrierBackendTestStoreFactory backend)
{
    private SharedTestStoreProperties _pending;
    private ITestStoreFactory? _testStoreFactory;

    /// <summary>
    ///     The factory to return from the adopting class's <c>TestStoreFactory</c>.
    /// </summary>
    public ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.CreateDeferred(backend, () => _pending);

    /// <summary>
    ///     Records what the next store must be built for. Call from the adopting class's
    ///     <c>CreateContextFactory&lt;TContext&gt;</c>, before delegating to the base.
    /// </summary>
    /// <param name="addOptions">
    ///     The adopting class's own <c>AddOptions</c>, when it has one. Only the seeders are taken
    ///     from it; see <see cref="ServerOptions" />.
    /// </param>
    public void Prepare(
        Type contextType,
        Action<ModelBuilder>? onModelCreating,
        Func<IServiceCollection, IServiceCollection>? addServices,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<DbContextOptionsBuilder, DbContextOptionsBuilder>? addOptions = null)
        => _pending = new SharedTestStoreProperties
        {
            ContextType = contextType,
            OnModelCreating = onModelCreating is null ? null : (modelBuilder, _) => onModelCreating(modelBuilder),
            ConfigureConventions = configureConventions,
            OnAddServices = addServices,

            OnAddOptions = ServerOptions(onConfiguring, addOptions),

            CopyDbContextParameters = CopyContextState,
        };

    /// <summary>
    ///     Builds the options delegate the server context is created with.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The server gets the test's own <c>onConfiguring</c>, unlike the fixture-wide
    ///         <c>AddOptions</c> a shared fixture supplies (A29). It is a different thing: a
    ///         <c>NonSharedModelTestBase</c> test writes <c>onConfiguring</c> for the one context
    ///         it is about to build, and the server builds that same context.
    ///         <c>Can_ignore_invalid_include_path_error</c> suppresses a warning there and asserts
    ///         the query then runs, which it cannot if only the client hears about the suppression.
    ///     </para>
    ///     <para>
    ///         <b>The seeders are the exception, and they are why this method exists.</b>
    ///         <c>NonSharedModelTestBase.ConfigureOptions</c> applies <c>AddOptions</c> to the
    ///         <em>client</em> only, and a base such as <c>EntitySplittingQueryTestBase</c> seeds
    ///         through <c>AddOptions(...).UseSeeding(...)</c>. A seeder runs inside
    ///         <c>EnsureCreated</c>, and <c>EnsureCreated</c> runs on the server, so the seeder
    ///         never fired and every query answered zero rows against five expected.
    ///     </para>
    ///     <para>
    ///         Only the seeders cross, not the whole of <c>AddOptions</c>. The distinction is what
    ///         the option acts on: a seeder acts on the <em>store</em>, which is the server's, while
    ///         warning behavior and sensitive-data logging describe how a context behaves and each
    ///         side owns its own. They are read back off a throwaway builder because
    ///         <c>UseSeeding</c> has no getter.
    ///     </para>
    /// </remarks>
    private static Func<DbContextOptionsBuilder, DbContextOptionsBuilder>? ServerOptions(
        Action<DbContextOptionsBuilder>? onConfiguring,
        Func<DbContextOptionsBuilder, DbContextOptionsBuilder>? addOptions)
    {
        Action<DbContext, bool>? seeder = null;
        Func<DbContext, bool, CancellationToken, Task>? asyncSeeder = null;

        if (addOptions is not null)
        {
            var probe = new DbContextOptionsBuilder();
            _ = addOptions(probe);

            CoreOptionsExtension? core = probe.Options.FindExtension<CoreOptionsExtension>();
            seeder = core?.Seeder;
            asyncSeeder = core?.AsyncSeeder;
        }

        if (onConfiguring is null && seeder is null && asyncSeeder is null)
        {
            return null;
        }

        return builder =>
        {
            if (seeder is not null)
            {
                builder.UseSeeding(seeder);
            }

            if (asyncSeeder is not null)
            {
                builder.UseAsyncSeeding(asyncSeeder);
            }

            onConfiguring?.Invoke(builder);
            return builder;
        };
    }

    /// <summary>
    ///     Mirrors the client context's own state onto the server context for one request.
    /// </summary>
    /// <remarks>
    ///     A query filter is part of the model, so both sides apply it — and a filter may close
    ///     over a property of the context itself. `MultiContext_query_filter_test` writes
    ///     <c>context.Tenant = 1</c> and expects the filter <c>e.SomeValue == Tenant</c> to follow;
    ///     the server builds its own instance of the same context type, where <c>Tenant</c> is
    ///     still 0, and the query answers nothing.
    ///     <para>
    ///         A shared fixture names the properties to copy by hand. Here there is no fixture to
    ///         name them in, so every writable public instance property the context type declares
    ///         *below* <see cref="DbContext" /> is copied — which is what those hand-written
    ///         copiers do, generalized. `DbSet` properties are skipped: they are the model, not
    ///         state, and each side owns its own.
    ///     </para>
    /// </remarks>
    private static void CopyContextState(DbContext client, DbContext server)
    {
        foreach (System.Reflection.PropertyInfo property in client.GetType().GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!property.CanRead
                || !property.CanWrite
                || property.GetIndexParameters().Length > 0
                || property.DeclaringType == typeof(DbContext)
                || (property.PropertyType.IsGenericType
                    && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>)))
            {
                continue;
            }

            property.SetValue(server, property.GetValue(client));
        }
    }
}
