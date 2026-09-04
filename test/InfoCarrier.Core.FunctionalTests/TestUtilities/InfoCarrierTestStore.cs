// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The client <see cref="TestStore" /> wrapper seen by the spec tests (v1 pattern). A thin
///     shell over the <see cref="InfoCarrierBackendTestStore" />: initialization delegates to
///     the backend (which owns the real server provider and context factory), and the client
///     context is configured with <c>UseInfoCarrier(backend)</c> so all operations remote
///     through the backend-as-client.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="InfoCarrierTestStore" /> class.
/// </remarks>
public class InfoCarrierTestStore(InfoCarrierBackendTestStore backend)
    : TestStore(backend.Name, shared: false), IInfoCarrierClientTestStore
{
    private readonly InfoCarrierBackendTestStore _backend = backend;

    /// <summary>
    ///     The backend store this client remotes to.
    /// </summary>
    public InfoCarrierBackendTestStore Backend => _backend;

    /// <inheritdoc />
    public override async Task<TestStore> InitializeAsync(
        IServiceProvider? serviceProvider,
        Func<DbContext>? createContext,
        Func<DbContext, Task>? seed = null,
        Func<DbContext, Task>? clean = null)
    {
        // Ignore the fixture's serviceProvider/createContext for the STORE — the backend owns the
        // real server provider and context factory (v1 pattern).
        await _backend.InitializeAsync(_backend.ServiceProvider, _backend.CreateDbContext, seed, clean)
            .ConfigureAwait(false);

        // BUT BUILD THE CLIENT'S MODEL HERE, which is what `createContext` is used for (R172).
        // EF's own providers have one context, so initializing the store validates the model and
        // whatever that logs is logged before the base clears the log. This provider has two, and
        // only the server's was ever created during initialization — so the client's model was
        // built by the FIRST context a test created, inside the test, and its model-validation
        // events landed in what the test then read.
        //
        // `Warn_when_save_optional_dependent_with_null_values_sensitive` is where that shows:
        // it asserts a SINGLE warning, and the client's own `SensitiveDataLoggingEnabledWarning`
        // arrived beside the one the test caused. Touching `Model` is the whole of the fix — it is
        // what forces the build and the validation, and the context is disposed immediately.
        if (createContext is not null)
        {
            using DbContext client = createContext();
            _ = client.Model;
        }

        return this;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The CLIENT half of the raw-SQL seam (#60) when the fixture asked for it. Not a security
    ///     boundary - it decides only what this client will send, and the server refuses
    ///     independently.
    /// </remarks>
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => builder.UseInfoCarrier(_backend, ClientOptions(_backend));

    /// <summary>
    ///     The InfoCarrier client options a fixture's store implies. Shared with
    ///     <see cref="RelationalInfoCarrierTestStore" />, which needs the same answer.
    /// </summary>
    public static Action<InfoCarrierDbContextOptionsBuilder>? ClientOptions(InfoCarrierBackendTestStore backend)
    {
        bool arbitrarySql = backend.ArbitrarySqlExecution;
        Type[] allowedTypes = backend.AllowedTypes;

        // The raw-SQL grant and the projection types are INDEPENDENT declarations, and reading the
        // first as a precondition of the second was the shape this method had until
        // `SqlQueryTestBase` was adopted. A fixture may declare a DTO and grant no SQL.
        bool relationalStore = backend.ServerStoreIsRelational;

        if (!arbitrarySql && allowedTypes.Length == 0 && relationalStore)
        {
            return null;
        }

        Type? parameterType = arbitrarySql ? backend.StoreParameterType : null;

        return o =>
        {
            // Knowledge the client cannot derive: only the store knows what it is. Tier A says so
            // because EF's InMemory provider answers queries every relational provider refuses.
            if (!relationalStore)
            {
                o.UseNonRelationalServerStore();
            }

            if (arbitrarySql)
            {
                o.AllowArbitrarySqlExecution();
            }

            // The client half of the same pair. Both halves or neither, as ADR-012 requires: a type
            // admitted on the client alone produces a query the server refuses to read.
            if (parameterType is not null)
            {
                o.AllowTypes(parameterType);
            }

            if (allowedTypes.Length > 0)
            {
                o.AllowTypes(allowedTypes);
            }
        };
    }

    /// <inheritdoc />
    public override async Task CleanAsync(DbContext context)
    {
        await _backend.CleanAsync(_backend.CreateDbContext()).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await _backend.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
