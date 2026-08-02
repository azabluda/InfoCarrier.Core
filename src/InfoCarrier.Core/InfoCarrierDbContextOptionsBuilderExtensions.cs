// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        ConfigureWarnings(optionsBuilder);
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

    /// <summary>
    ///     Defaults <see cref="InfoCarrierEventId.TransactionIgnoredWarning" /> to
    ///     <see cref="WarningBehavior.Throw" />, as EF Core's InMemory provider does for its own.
    ///     The provider ignores transactions until milestone M4, and a silent ignore is worse than
    ///     a loud one; <c>TryWithExplicit</c> leaves an application's own setting alone.
    /// </summary>
    private static void ConfigureWarnings(DbContextOptionsBuilder optionsBuilder)
    {
        CoreOptionsExtension coreOptionsExtension =
            optionsBuilder.Options.FindExtension<CoreOptionsExtension>() ?? new CoreOptionsExtension();

        coreOptionsExtension = coreOptionsExtension.WithWarningsConfiguration(
            coreOptionsExtension.WarningsConfiguration.TryWithExplicit(
                InfoCarrierEventId.TransactionIgnoredWarning, WarningBehavior.Throw));

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(coreOptionsExtension);
    }

    private static InfoCarrierOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.Options.FindExtension<InfoCarrierOptionsExtension>()
            ?? new InfoCarrierOptionsExtension();
}
