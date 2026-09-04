// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>
///     Present in a <em>server's</em> service collection when that server sends the log events it
///     raises while executing a request back to the client (#97, R172).
/// </summary>
/// <remarks>
///     <para>
///         <b>The presence of this service is the grant, and it is off by default.</b> Same shape
///         as <see cref="IInfoCarrierArbitrarySqlExecution" />, and for the same kind of reason: a
///         server's log is the server's, and a client is a different trust domain. Registered with
///         <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierServerLogForwarding" />,
///         which also installs the capture the server reads.
///     </para>
///     <para>
///         <b>What a deployment gives up by registering it.</b> A forwarded event is text the
///         server's provider wrote about the server's store, so it can name tables, columns and
///         SQL. That is schema disclosure to whoever holds a client, and it is why the default
///         minimum level is <see cref="LogLevel.Warning" /> rather than
///         <see cref="LogLevel.Information" />: EF logs every executed command at
///         <c>Information</c>, so a lower level ships the server's SQL on every request.
///     </para>
///     <para>
///         <b>It does not carry values.</b> The second grant,
///         <see cref="IInfoCarrierSensitiveServerLogForwarding" />, is what lets an event cross
///         when the server's context has <c>EnableSensitiveDataLogging</c> on — see there.
///     </para>
/// </remarks>
public interface IInfoCarrierServerLogForwarding
{
    /// <summary>
    ///     The lowest level that crosses. Events below it are dropped on the server and never
    ///     reach the wire.
    /// </summary>
    LogLevel MinimumLevel { get; }
}

/// <summary>
///     Present in a <em>server's</em> service collection when that server may forward log events
///     even though its own context logs sensitive data (#97, R172).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this is a second grant and not a filter.</b> <c>EnableSensitiveDataLogging</c>
///         is not a per-event flag; it changes what EF's own message templates say, across the
///         board. So an event raised by a context that has it on may carry key values, parameter
///         values or property values in its text, and there is no way to tell from outside which
///         ones do. The honest rule is therefore all-or-nothing: with sensitive logging on and
///         this grant absent, a server forwards nothing at all.
///     </para>
///     <para>
///         <b>The client cannot ask for this.</b> Both grants are the server's own registration.
///         A client option that widened disclosure would be a hole rather than a feature, which is
///         the same division <see cref="IInfoCarrierArbitrarySqlExecution" /> records.
///     </para>
/// </remarks>
public interface IInfoCarrierSensitiveServerLogForwarding
{
}
