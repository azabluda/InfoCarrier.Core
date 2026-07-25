// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     An InMemory-backed <see cref="InfoCarrierBackendTestStore" /> — the first backend
///     (architecture §5: InMemory first, then SQL Server). The server context runs against
///     EF Core's InMemory provider.
/// </summary>
public class InMemoryInfoCarrierBackendTestStore : InfoCarrierBackendTestStore
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InMemoryInfoCarrierBackendTestStore" /> class.
    /// </summary>
    public InMemoryInfoCarrierBackendTestStore(string name, bool shared = true)
        : base(name, shared)
    {
    }

    /// <inheritdoc />
    protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkInMemoryDatabase();

    /// <inheritdoc />
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => builder.UseInMemoryDatabase(Name);
}
