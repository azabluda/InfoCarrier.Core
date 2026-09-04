// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>
///     Writes the log events a server sent back into the client context's own log (#97, R172).
/// </summary>
/// <remarks>
///     <para>
///         <b>Under the server's category and the server's event id, not this provider's.</b> A
///         forwarded <c>OptionalDependentWithAllNullPropertiesWarning</c> is that event; renaming
///         it would make a caller's existing filters and handlers miss it, and would claim the
///         event as this provider's when it is EF's.
///     </para>
///     <para>
///         <b>Through <see cref="ILoggerFactory" /> rather than
///         <c>IDiagnosticsLogger&lt;T&gt;</c>.</b> EF's diagnostics logger is typed by category and
///         gated by the client context's own <c>ConfigureWarnings</c>, neither of which applies
///         here: the category is whatever the server wrote under, and the decision to raise the
///         event was the server's to make. What the client still controls is its logging
///         configuration, which the factory honours.
///     </para>
/// </remarks>
internal static class ServerLogReplay
{
    /// <summary>
    ///     Re-raises <paramref name="events" /> on <paramref name="context" />'s logger. Does
    ///     nothing when the server sent none, which is the default.
    /// </summary>
    public static void Replay(IReadOnlyList<ServerLogEvent>? events, DbContext context)
    {
        if (events is null or { Count: 0 })
        {
            return;
        }

        var factory = context.GetService<ILoggerFactory>();
        foreach (ServerLogEvent forwarded in events)
        {
            factory.CreateLogger(forwarded.Category).Log(
                (LogLevel)forwarded.Level,
                new EventId(forwarded.EventId, forwarded.EventName),
                forwarded.Message,
                exception: null,

                // The message the server's own formatter produced, verbatim. There is no state
                // object to format: what crossed the wire is text, for the reason
                // `ServerLogEvent` records.
                static (message, _) => message);
        }
    }
}
