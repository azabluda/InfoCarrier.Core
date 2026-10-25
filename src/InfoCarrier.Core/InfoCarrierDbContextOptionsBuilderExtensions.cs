// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

/// <summary>Extension methods for attaching an InfoCarrier client to a context.</summary>
public static class InfoCarrierDbContextOptionsBuilderExtensions
{
    /// <summary>Attaches the default client registered with <c>AddInfoCarrierClient</c>.</summary>
    /// <remarks>
    ///     Standard Entity Framework context registrations supply the application service provider.
    ///     For manually constructed options, supply a client explicitly or call
    ///     <see cref="DbContextOptionsBuilder.UseApplicationServiceProvider" /> first.
    /// </remarks>
    [RequiresUnreferencedCode(
        "InfoCarrier resolves types by wire names. Test all trimmed model paths."),
     RequiresDynamicCode(
        "InfoCarrier builds and compiles expression trees at run time.")]
    public static DbContextOptionsBuilder UseInfoCarrier(this DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        IServiceProvider provider = optionsBuilder.Options.FindExtension<CoreOptionsExtension>()
            ?.ApplicationServiceProvider
            ?? throw new InvalidOperationException(
                "UseInfoCarrier() requires an application service provider. Use a standard Entity Framework "
                + "context registration, call UseApplicationServiceProvider, or pass a client explicitly.");
        IInfoCarrierClient client = provider.GetService<IInfoCarrierClient>()
            ?? throw new InvalidOperationException(
                "No default InfoCarrier client is registered. Call AddInfoCarrierClient "
                + "before resolving context options, or pass a client explicitly.");

        return UseInfoCarrier(optionsBuilder, client);
    }

    /// <summary>Attaches the named client registered with <c>AddInfoCarrierClient</c>.</summary>
    /// <remarks>Names are case-sensitive. A missing name never falls back to the default client.</remarks>
    [RequiresUnreferencedCode(
        "InfoCarrier resolves types by wire names. Test all trimmed model paths."),
     RequiresDynamicCode(
        "InfoCarrier builds and compiles expression trees at run time.")]
    public static DbContextOptionsBuilder UseInfoCarrier(
        this DbContextOptionsBuilder optionsBuilder,
        string clientName)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        IServiceProvider provider = optionsBuilder.Options.FindExtension<CoreOptionsExtension>()
            ?.ApplicationServiceProvider
            ?? throw new InvalidOperationException(
                "Named UseInfoCarrier requires an application service provider. Use a standard Entity Framework "
                + "context registration, call UseApplicationServiceProvider, or pass a client explicitly.");
        InfoCarrierClientRegistrationStore store = provider.GetService<InfoCarrierClientRegistrationStore>()
            ?? throw new InvalidOperationException(
                $"No InfoCarrier client named '{clientName}' is registered. Call AddInfoCarrierClient with that name.");
        return UseInfoCarrier(optionsBuilder, store.Get(provider, clientName));
    }

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

    /// <summary>Attaches the default registered InfoCarrier client to the typed context.</summary>
    [RequiresUnreferencedCode(
        "InfoCarrier resolves types by wire names. Test all trimmed model paths."),
     RequiresDynamicCode(
        "InfoCarrier builds and compiles expression trees at run time.")]
    public static DbContextOptionsBuilder<TContext> UseInfoCarrier<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseInfoCarrier((DbContextOptionsBuilder)optionsBuilder);

    /// <summary>Attaches the named registered InfoCarrier client to the typed context.</summary>
    [RequiresUnreferencedCode(
        "InfoCarrier resolves types by wire names. Test all trimmed model paths."),
     RequiresDynamicCode(
        "InfoCarrier builds and compiles expression trees at run time.")]
    public static DbContextOptionsBuilder<TContext> UseInfoCarrier<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string clientName)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseInfoCarrier((DbContextOptionsBuilder)optionsBuilder, clientName);

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
