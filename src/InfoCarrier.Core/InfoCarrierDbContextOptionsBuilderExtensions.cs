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

        // `ConfigureWarnings` **before** `AddOrUpdateExtension`, which is the order every EF
        // provider uses and is not arbitrary. `DbContextOptions.Extensions` yields extensions by
        // *insertion ordinal*, and that order is what `BuildOptionsFragment` prints in the
        // context-initialized log line. Configuring warnings is what first creates
        // `CoreOptionsExtension`, so doing it first puts the core options ahead of the provider's
        // — `"NoTracking using InfoCarrier"`, which is the shape `LoggingTestBase` composes its
        // expectation in (`ExpectedMessage("NoTracking " + DefaultOptions)`) and the shape every
        // other provider produces. Adding ours first printed `"using InfoCarrier NoTracking"`.
        ConfigureWarnings(optionsBuilder);
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
