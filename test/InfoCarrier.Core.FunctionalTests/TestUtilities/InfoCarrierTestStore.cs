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
public class InfoCarrierTestStore(InfoCarrierBackendTestStore backend) : TestStore(backend.Name, shared: false)
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
        // Ignore the fixture's serviceProvider/createContext — the backend owns the real
        // server provider and context factory (v1 pattern).
        await _backend.InitializeAsync(_backend.ServiceProvider, _backend.CreateDbContext, seed, clean)
            .ConfigureAwait(false);
        return this;
    }

    /// <inheritdoc />
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => builder.UseInfoCarrier(_backend);

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
