// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.AspNetCore;

/// <summary>
///     Binds a server-held transaction to the authenticated caller that opened it (#54, part 2).
/// </summary>
public static class InfoCarrierHttpCallerIdentityExtensions
{
    /// <summary>Binds transactions from this server registration to the authenticated HTTP caller.</summary>
    /// <remarks>
    /// The transport must authenticate requests. Use a stable caller identifier for the transaction lifetime.
    /// Null identifies an anonymous caller; it does not distinguish separate anonymous users.
    /// </remarks>
    public static InfoCarrierServerBuilder AddHttpCallerIdentity(
        this InfoCarrierServerBuilder builder,
        Func<HttpContext, string?> callerId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(callerId);
        builder.Services.AddHttpContextAccessor();
        builder.ConfigureCallerIdentity(sp => new HttpCallerIdentity(
            sp.GetRequiredService<IHttpContextAccessor>(), callerId));
        return builder;
    }


    /// <summary>
    ///     Reads the caller out of the request in flight.
    /// </summary>
    /// <remarks>
    ///     <b>No request means no caller</b>, which happens when a server uses
    ///     <c>InProcessInfoCarrierServer</c> directly rather than through the endpoint. That
    ///     answers <see langword="null" />, and null binds, so an in-process transaction is usable
    ///     in process and not from a request that carries an identity.
    /// </remarks>
    private sealed class HttpCallerIdentity(
        IHttpContextAccessor accessor,
        Func<HttpContext, string?> callerId) : IInfoCarrierServerCallerIdentity
    {
        public string? CurrentCallerId
            => accessor.HttpContext is { } http ? callerId(http) : null;
    }
}
