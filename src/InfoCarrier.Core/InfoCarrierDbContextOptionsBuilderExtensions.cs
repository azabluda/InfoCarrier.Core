// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace InfoCarrier.Core;

/// <summary>
///     Extension methods for configuring an InfoCarrier client context.
/// </summary>
public static class InfoCarrierDbContextOptionsBuilderExtensions
{
    /// <summary>
    ///     Configures the context to use the InfoCarrier provider, remoting all database
    ///     operations through the supplied <see cref="IInfoCarrierClient" />.
    /// </summary>
    /// <param name="optionsBuilder">The options builder.</param>
    /// <param name="client">The client that ships operations to the server.</param>
    /// <returns>The same builder for chaining.</returns>
    public static DbContextOptionsBuilder UseInfoCarrier(
        this DbContextOptionsBuilder optionsBuilder,
        IInfoCarrierClient client)
    {
        InfoCarrierOptionsExtension extension = GetOrCreateExtension(optionsBuilder)
            .WithInfoCarrierClient(client);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }

    /// <summary>
    ///     Configures the context to use the InfoCarrier provider (generic overload).
    /// </summary>
    public static DbContextOptionsBuilder<TContext> UseInfoCarrier<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        IInfoCarrierClient client)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseInfoCarrier((DbContextOptionsBuilder)optionsBuilder, client);

    private static InfoCarrierOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.Options.FindExtension<InfoCarrierOptionsExtension>()
            ?? new InfoCarrierOptionsExtension();
}
