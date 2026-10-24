// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>Fluent configuration of client and server role options.</summary>
public static class InfoCarrierRoleOptionsExtensions
{
    /// <summary>Sets the HTTP endpoint used by this client.</summary>
    public static InfoCarrierClientOptions WithEndpoint(this InfoCarrierClientOptions options, string endpoint)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(endpoint);
        return options.WithEndpoint(new Uri(endpoint, UriKind.RelativeOrAbsolute));
    }

    /// <summary>Sets the HTTP endpoint used by this client.</summary>
    public static InfoCarrierClientOptions WithEndpoint(this InfoCarrierClientOptions options, Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(endpoint);
        options.Endpoint = endpoint;
        return options;
    }

    /// <summary>Sets the response byte limit, or null for no limit.</summary>
    public static InfoCarrierClientOptions WithMaxResponseBytes(this InfoCarrierClientOptions options, int? bytes)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MaxResponseBytes = bytes;
        return options;
    }

    /// <summary>Sets whether the server applies relational query rules.</summary>
    public static InfoCarrierClientOptions WithServerUsesRelationalQueryRules(
        this InfoCarrierClientOptions options, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.ServerUsesRelationalQueryRules = enabled;
        return options;
    }

    /// <summary>Sets the request byte limit, or null for no limit.</summary>
    public static InfoCarrierServerOptions WithMaxRequestBytes(this InfoCarrierServerOptions options, int? bytes)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MaxRequestBytes = bytes;
        return options;
    }

    /// <summary>Sets the transaction idle timeout, or null for no timeout.</summary>
    public static InfoCarrierServerOptions WithTransactionIdleTimeout(
        this InfoCarrierServerOptions options, TimeSpan? timeout)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.TransactionIdleTimeout = timeout;
        return options;
    }

    /// <summary>Sets whether the store writes owned data inside its owner's record.</summary>
    public static InfoCarrierServerOptions WithStoresOwnedDataInsideOwner(
        this InfoCarrierServerOptions options, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.StoresOwnedDataInsideOwner = enabled;
        return options;
    }

    /// <summary>Sets log forwarding and its sensitive-data grant. A null level disables forwarding.</summary>
    /// <remarks>Each call sets both values. Sensitive forwarding is disabled unless explicitly requested.</remarks>
    public static InfoCarrierServerOptions WithLogForwarding(
        this InfoCarrierServerOptions options, LogLevel? minimumLevel, bool includeSensitiveData = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.LogForwarding.MinimumLevel = minimumLevel;
        options.LogForwarding.IncludeSensitiveData = includeSensitiveData;
        return options;
    }
}
