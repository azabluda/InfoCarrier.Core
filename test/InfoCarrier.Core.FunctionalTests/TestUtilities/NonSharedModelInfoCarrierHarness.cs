// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
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
    public void Prepare(
        Type contextType,
        Action<ModelBuilder>? onModelCreating,
        Func<IServiceCollection, IServiceCollection>? addServices,
        Action<DbContextOptionsBuilder>? onConfiguring = null)
        => _pending = new SharedTestStoreProperties
        {
            ContextType = contextType,
            OnModelCreating = onModelCreating is null ? null : (modelBuilder, _) => onModelCreating(modelBuilder),
            OnAddServices = addServices,

            // The server gets the test's own `onConfiguring`, unlike the fixture-wide `AddOptions`
            // a shared fixture supplies (A29). It is a different thing: a `NonSharedModelTestBase`
            // test writes `onConfiguring` for the one context it is about to build, and the server
            // builds that same context. `Can_ignore_invalid_include_path_error` suppresses a
            // warning there and asserts the query then runs — which it cannot if only the client
            // hears about the suppression.
            OnAddOptions = onConfiguring is null
                ? null
                : builder =>
                {
                    onConfiguring(builder);
                    return builder;
                },

            CopyDbContextParameters = CopyContextState,
        };

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
