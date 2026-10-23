// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace InfoCarrier.Core;

/// <summary>Extension methods for attaching an InfoCarrier client to a context.</summary>
public static class InfoCarrierDbContextOptionsBuilderExtensions
{
    /// <summary>Configures the context to use the supplied InfoCarrier client.</summary>
    [RequiresUnreferencedCode(
        "InfoCarrier resolves types by wire names. Test all trimmed model paths."),
     RequiresDynamicCode(
        "InfoCarrier builds and compiles expression trees at run time.")]
    public static DbContextOptionsBuilder UseInfoCarrier(
        this DbContextOptionsBuilder optionsBuilder,
        IInfoCarrierClient client)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(client);

        EnsureCoreOptionsFirst(optionsBuilder);
        InfoCarrierOptionsExtension extension = GetOrCreateExtension(optionsBuilder)
            .WithInfoCarrierClient(client);

        if (client is IInfoCarrierClientConfiguration { Registration: { } registration })
        {
            extension = extension.WithDeployment(registration.Deployment);
            if (!registration.ServerUsesRelationalQueryRules)
            {
                extension = extension.WithNonRelationalServerStore();
            }
        }

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }

    /// <summary>Configures and attaches an existing transport-independent client.</summary>
    [RequiresUnreferencedCode(
        "InfoCarrier resolves types by wire names. Test all trimmed model paths."),
     RequiresDynamicCode(
        "InfoCarrier builds and compiles expression trees at run time.")]
    public static DbContextOptionsBuilder UseInfoCarrier(
        this DbContextOptionsBuilder optionsBuilder,
        IInfoCarrierClient client,
        Action<InfoCarrierClientOptions> configure)
        => UseInfoCarrier(optionsBuilder, InfoCarrierClient.Configure(client, configure));

    /// <summary>Configures the typed context to use the supplied InfoCarrier client.</summary>
    [RequiresUnreferencedCode(
        "InfoCarrier resolves types by wire names. Test all trimmed model paths."),
     RequiresDynamicCode(
        "InfoCarrier builds and compiles expression trees at run time.")]
    public static DbContextOptionsBuilder<TContext> UseInfoCarrier<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        IInfoCarrierClient client)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseInfoCarrier((DbContextOptionsBuilder)optionsBuilder, client);

    /// <summary>Configures and attaches an existing transport-independent client.</summary>
    [RequiresUnreferencedCode(
        "InfoCarrier resolves types by wire names. Test all trimmed model paths."),
     RequiresDynamicCode(
        "InfoCarrier builds and compiles expression trees at run time.")]
    public static DbContextOptionsBuilder<TContext> UseInfoCarrier<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        IInfoCarrierClient client,
        Action<InfoCarrierClientOptions> configure)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseInfoCarrier(
            (DbContextOptionsBuilder)optionsBuilder, client, configure);

    private static void EnsureCoreOptionsFirst(DbContextOptionsBuilder optionsBuilder)
    {
        CoreOptionsExtension coreOptionsExtension =
            optionsBuilder.Options.FindExtension<CoreOptionsExtension>() ?? new CoreOptionsExtension();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(coreOptionsExtension);
    }

    private static InfoCarrierOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.Options.FindExtension<InfoCarrierOptionsExtension>()
            ?? new InfoCarrierOptionsExtension();
}
