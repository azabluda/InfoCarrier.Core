// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfoCarrier.Core.AspNetCore;

/// <summary>
///     Binds a server-held transaction to the authenticated caller that opened it (#54, part 2).
/// </summary>
public static class InfoCarrierHttpCallerIdentityExtensions
{
    /// <summary>
    ///     Makes this <em>server</em> refuse a request that names a transaction opened by a
    ///     different caller.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The identity is OBSERVED, not asserted, so nothing changes on the wire.</b> The
    ///         envelope carries no caller and does not need to: this endpoint already receives the
    ///         <see cref="HttpContext" />, so an authenticated transport has already established
    ///         who is calling. An old client works against a server that turns this on, unmodified.
    ///     </para>
    ///     <para>
    ///         <b>IT DOES NOTHING UNLESS THE TRANSPORT AUTHENTICATES.</b> Without
    ///         <c>RequireAuthorization</c> or its equivalent, <c>HttpContext.User</c> carries no
    ///         identity, <paramref name="callerId" /> returns the same answer for everybody, and
    ///         every caller matches every transaction. This is a second lock on a door, not the
    ///         first one.
    ///     </para>
    ///     <para>
    ///         <b>You choose what identifies a caller, because only you can.</b> A subject id, a
    ///         login name, or a tenant claim combined with a user id are all defensible and they
    ///         behave differently when a token is refreshed. Pick a value that is STABLE for as
    ///         long as a transaction lives: if it changes mid-transaction, the caller loses its own
    ///         transaction and the work is rolled back by the idle sweep.
    ///     </para>
    ///     <para>
    ///         <b>A null answer binds like any other value.</b> A transaction opened while
    ///         <paramref name="callerId" /> returns <see langword="null" /> can be used only when
    ///         it returns <see langword="null" /> again. That separates an anonymous caller from a
    ///         named one, and does not separate two anonymous callers, because nothing
    ///         distinguishes them.
    ///     </para>
    ///     <example>
    ///         <code>
    ///         builder.Services.AddInfoCarrierHttpCallerIdentity(
    ///             http => http.User.FindFirst("sub")?.Value);
    ///
    ///         app.MapInfoCarrier().RequireAuthorization("DataAccess");
    ///         </code>
    ///     </example>
    /// </remarks>
    /// <param name="services">The server's service collection.</param>
    /// <param name="callerId">
    ///     Returns the value that identifies the caller of the request in flight, or
    ///     <see langword="null" /> when there is none. Called once when a transaction is opened and
    ///     once for every later request that names its token.
    /// </param>
    /// <returns>The same collection, so calls chain.</returns>
    public static IServiceCollection AddInfoCarrierHttpCallerIdentity(
        this IServiceCollection services,
        Func<HttpContext, string?> callerId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(callerId);

        // The accessor is what lets a SINGLETON server answer for the request in flight: it holds
        // the context in async-local storage, so it follows the call rather than the object.
        services.AddHttpContextAccessor();

        services.TryAddSingleton<IInfoCarrierServerCallerIdentity>(
            sp => new HttpCallerIdentity(sp.GetRequiredService<IHttpContextAccessor>(), callerId));

        return services;
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
