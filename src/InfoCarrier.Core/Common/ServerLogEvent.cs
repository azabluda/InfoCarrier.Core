// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common;

/// <summary>
///     One log event the server raised while executing a request, carried back to the client
///     (#97, R172).
/// </summary>
/// <remarks>
///     <para>
///         <b>The client cannot see the server's log any other way.</b> Everything EF writes about
///         a store round trip is written by the server's provider on the server's context, and a
///         caller holding only the client context has no route to it. This record is that route,
///         and it is one the server has to open deliberately — see
///         <see cref="IInfoCarrierServerLogForwarding" />.
///     </para>
///     <para>
///         <b>The message is a formatted string and not a structured event, on purpose.</b> A log
///         event's <c>state</c> is an arbitrary object graph whose types are the *server's*, which
///         is exactly what <c>TypeAllowlist</c> exists to keep off the wire (ADR-008 constraint 2).
///         What crosses is what the server's own formatter produced, so the client re-raises text
///         the server already decided to write.
///     </para>
/// </remarks>
public sealed record ServerLogEvent
{
    /// <summary>
    ///     The <c>Microsoft.Extensions.Logging.LogLevel</c> the server logged at, as its integer
    ///     value.
    /// </summary>
    /// <remarks>
    ///     An integer rather than the enum, because the wire is a contract and an enum's numbering
    ///     is a detail of the assembly that declares it. The client casts it back.
    /// </remarks>
    public required int Level { get; init; }

    /// <summary>
    ///     The numeric part of the event's <c>EventId</c> — <c>CoreEventId</c>'s or
    ///     <c>RelationalEventId</c>'s value, unchanged.
    /// </summary>
    public required int EventId { get; init; }

    /// <summary>
    ///     The name part of the event's <c>EventId</c>, or <see langword="null" /> when the server
    ///     raised it without one.
    /// </summary>
    public string? EventName { get; init; }

    /// <summary>
    ///     The logger category the server wrote under, such as
    ///     <c>Microsoft.EntityFrameworkCore.Update</c>. The client re-raises under the same one,
    ///     so a caller's log filters apply to a forwarded event exactly as to a local one.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    ///     The message the server's own formatter produced.
    /// </summary>
    public required string Message { get; init; }
}
