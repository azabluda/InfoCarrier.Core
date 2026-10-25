// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>Options for one InfoCarrier client registration.</summary>
public sealed class InfoCarrierClientOptions : InfoCarrierDeploymentBuilder
{
    /// <summary>The absolute HTTP endpoint used by this client.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>Whether the server applies relational query rules.</summary>
    public bool ServerUsesRelationalQueryRules { get; set; } = true;

    /// <summary>The largest response payload the client accepts, or null for no limit.</summary>
    public int? MaxResponseBytes { get; set; }

    internal InfoCarrierClientRegistration Build(bool requireEndpoint = true)
    {
        if (requireEndpoint && (Endpoint is null || !Endpoint.IsAbsoluteUri))
        {
            throw new InvalidOperationException(
                $"{nameof(Endpoint)} must be an absolute URI. Configured value: '{Endpoint}'.");
        }

        Positive(MaxResponseBytes, nameof(MaxResponseBytes));
        return new(new InfoCarrierRegistrationIdentity(), Endpoint, ServerUsesRelationalQueryRules,
            MaxResponseBytes, CreateSnapshot());
    }

    internal static void Positive(int? value, string name)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "The value must be positive, or null to disable the limit.");
        }
    }
}

/// <summary>Options for one InfoCarrier server registration.</summary>
public sealed class InfoCarrierServerOptions : InfoCarrierDeploymentBuilder
{
    /// <summary>Whether the store writes owned data inside its owner's record.</summary>
    public bool StoresOwnedDataInsideOwner { get; set; }

    /// <summary>The largest request payload the server accepts, or null for no limit.</summary>
    public int? MaxRequestBytes { get; set; } = InfoCarrierPayloadLimits.DefaultMaxRequestBytes;

    /// <summary>How long an untouched transaction remains open, or null for no timeout.</summary>
    public TimeSpan? TransactionIdleTimeout { get; set; }

    /// <summary>Server log forwarding settings.</summary>
    public InfoCarrierLogForwardingOptions LogForwarding { get; } = new();

    internal InfoCarrierServerRegistration Build(Type contextType)
    {
        InfoCarrierClientOptions.Positive(MaxRequestBytes, nameof(MaxRequestBytes));
        if (TransactionIdleTimeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TransactionIdleTimeout), TransactionIdleTimeout, "The timeout must be positive, or null to disable it.");
        }

        if (LogForwarding.IncludeSensitiveData && LogForwarding.MinimumLevel is null)
        {
            throw new InvalidOperationException(
                $"{nameof(LogForwarding)}.{nameof(LogForwarding.IncludeSensitiveData)} requires "
                + $"{nameof(LogForwarding)}.{nameof(LogForwarding.MinimumLevel)}.");
        }

        return new(
            new InfoCarrierRegistrationIdentity(),
            contextType,
            StoresOwnedDataInsideOwner,
            MaxRequestBytes,
            TransactionIdleTimeout,
            LogForwarding.MinimumLevel,
            LogForwarding.IncludeSensitiveData,
            CreateSnapshot());
    }
}

/// <summary>Options for forwarding server log events to clients.</summary>
public sealed class InfoCarrierLogForwardingOptions
{
    /// <summary>The lowest forwarded level, or null when forwarding is disabled.</summary>
    public LogLevel? MinimumLevel { get; set; }

    /// <summary>Whether forwarding remains enabled when the server context logs sensitive data.</summary>
    public bool IncludeSensitiveData { get; set; }
}

internal sealed record InfoCarrierClientRegistration(
    InfoCarrierRegistrationIdentity Identity,
    Uri? Endpoint,
    bool ServerUsesRelationalQueryRules,
    int? MaxResponseBytes,
    InfoCarrierDeploymentSnapshot Deployment)
{
    internal string HttpClientName { get; } = $"InfoCarrier.Core.Client.{Guid.NewGuid():N}";
}

internal sealed record InfoCarrierServerRegistration(
    InfoCarrierRegistrationIdentity Identity,
    Type ContextType,
    bool StoresOwnedDataInsideOwner,
    int? MaxRequestBytes,
    TimeSpan? TransactionIdleTimeout,
    LogLevel? LogForwardingMinimumLevel,
    bool IncludeSensitiveLogData,
    InfoCarrierDeploymentSnapshot Deployment,
    Func<IServiceProvider, IInfoCarrierServerCallerIdentity>? CallerIdentityFactory = null)
{
    internal bool AllowsArbitrarySql => Deployment.AllowArbitrarySqlExecution;
    internal IReadOnlyList<Type> AllowedTypes => Deployment.AllowedTypes;
    internal IReadOnlyList<Type> AnonymousShapeTypes => Deployment.AnonymousShapeTypes;
    internal Func<IServiceProvider, DbContext>? ContextResolver { get; init; }

    internal DbContext ResolveContext(IServiceProvider provider)
        => ContextResolver is { } resolve
            ? resolve(provider) ?? throw new InvalidOperationException(
                $"The InfoCarrier context resolver for '{ContextType.Name}' returned null.")
            : (DbContext)provider.GetRequiredService(ContextType);
}

/// <summary>Opaque identity for one complete role registration.</summary>
internal sealed class InfoCarrierRegistrationIdentity;
