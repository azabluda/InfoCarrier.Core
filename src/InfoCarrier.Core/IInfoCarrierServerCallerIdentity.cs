// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core;

/// <summary>
///     Present in a <em>server's</em> service collection when that server binds a transaction to
///     the caller who opened it (#54, part 2).
/// </summary>
/// <remarks>
///     <para>
///         <b>Absent by default, and that is a decision rather than an oversight.</b> A server
///         that does not register this treats the transaction token as the only credential, which
///         is how every version up to <c>10.1.0</c> behaved. Turning binding on by default would
///         break any deployment whose caller identity is not stable across requests: an anonymous
///         endpoint, or a token refresh that changes the claim being compared, would start failing
///         transactions in the middle rather than at the start.
///     </para>
///     <para>
///         <b>WHAT IT PROTECTS AGAINST IS WIDER THAN ENDING SOMEBODY ELSE'S TRANSACTION.</b> The
///         token is a bearer credential, and the server hands back the opener's own
///         <c>DbContext</c>, on the opener's connection, inside the opener's transaction. So a
///         caller holding a token it did not open can query and save INSIDE that transaction and
///         then commit it. Ending it is the smaller half.
///     </para>
///     <para>
///         <b>The identity is observed, not asserted, so nothing changes on the wire.</b> The
///         envelope carries no caller identity and does not need to: an authenticated transport
///         already knows who is calling.
///         <c>InfoCarrierEndpointExtensions.AddInfoCarrierHttpCallerIdentity</c> is the ASP.NET
///         Core implementation and reads <c>HttpContext.User</c>. A transport with no ambient
///         identity would need a protocol change, and that case is still open.
///     </para>
///     <para>
///         <b>WHICH claim identifies a caller is the deployment's decision and not this
///         library's.</b> A login name, a subject id, and a tenant claim combined with a user id
///         are all defensible, and they behave differently when a token is refreshed. So this
///         interface asks for the answer rather than computing it.
///     </para>
///     <para>
///         <b>A null answer is a value like any other, and binds.</b> A transaction opened while
///         <see cref="CurrentCallerId" /> is <see langword="null" /> can be used only while it is
///         <see langword="null" /> again. That protects an authenticated caller from an anonymous
///         one, and it does NOT separate two anonymous callers, because nothing distinguishes
///         them. A deployment that needs that separation has to authenticate the transport, which
///         the security page says anyway.
///     </para>
/// </remarks>
public interface IInfoCarrierServerCallerIdentity
{
    /// <summary>
    ///     Identifies the caller of the request being served, or <see langword="null" /> when
    ///     there is no caller to name.
    /// </summary>
    /// <remarks>
    ///     Read once when a transaction is opened and once per request that names its token, so an
    ///     implementation must answer for the request in flight rather than for a session. The
    ///     ASP.NET Core implementation reads an async-local <c>HttpContext</c>, which does exactly
    ///     that even though the server itself is a singleton.
    /// </remarks>
    string? CurrentCallerId { get; }
}
