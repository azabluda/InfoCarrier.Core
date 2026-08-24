// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoCarrier.Core;

/// <summary>
///     Raises this provider's own log events, in the shape EF Core raises its own
///     (<c>CoreLoggerExtensions</c>).
/// </summary>
public static class InfoCarrierLoggerExtensions
{
    /// <summary>
    ///     Raises <see cref="InfoCarrierEventId.QuerySplit" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Both halves are required and the second one is easy to omit.</b>
    ///         <see cref="IDiagnosticsLogger.ShouldLog" /> covers the <c>ILogger</c> a caller
    ///         supplies through <c>UseLoggerFactory</c>. It says nothing about
    ///         <c>DbContextOptionsBuilder.LogTo</c>, which reaches a context through
    ///         <c>IDbContextLogger</c> and is answered by <see cref="IDiagnosticsLogger.NeedsEventData" />
    ///         and <c>DispatchEventData</c> instead. Keep both: with <c>ShouldLog</c> alone the
    ///         event reaches nothing when a context configures <c>LogTo</c> and no logger factory,
    ///         which is the most common way a developer reads EF logs.
    ///     </para>
    ///     <para>
    ///         Both guards matter for cost, not only for correctness. The split is decided per
    ///         execution rather than once per compiled query, so a hot split query reaches this
    ///         method often and nothing here may build a message or an
    ///         <see cref="EventData" /> when no one is listening.
    ///     </para>
    /// </remarks>
    /// <param name="diagnostics">The query logger.</param>
    /// <param name="serverQueryCount">How many queries the server runs for this execution.</param>
    public static void QuerySplit(
        this IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        int serverQueryCount)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (diagnostics.Definitions is not InfoCarrierLoggingDefinitions definitions)
        {
            return;
        }

        EventDefinition<int> definition = definitions.LogQuerySplit(diagnostics);

        if (diagnostics.ShouldLog(definition))
        {
            definition.Log(diagnostics, serverQueryCount);
        }

        if (diagnostics.NeedsEventData(definition, out bool diagnosticSourceEnabled, out bool simpleLogEnabled))
        {
            var eventData = new QuerySplitEventData(definition, QuerySplit, serverQueryCount);

            diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }

    private static string QuerySplit(EventDefinitionBase definition, EventData payload)
    {
        var d = (EventDefinition<int>)definition;
        var p = (QuerySplitEventData)payload;
        return d.GenerateMessage(p.ServerQueryCount);
    }
}

/// <summary>
///     The payload for <see cref="InfoCarrierEventId.QuerySplit" />.
/// </summary>
/// <param name="eventDefinition">The event definition.</param>
/// <param name="messageGenerator">Builds the message, lazily.</param>
/// <param name="serverQueryCount">How many queries the server runs for this execution.</param>
public class QuerySplitEventData(
    EventDefinitionBase eventDefinition,
    Func<EventDefinitionBase, EventData, string> messageGenerator,
    int serverQueryCount)
    : EventData(eventDefinition, messageGenerator)
{
    /// <summary>
    ///     How many queries the server runs for this execution. One means the whole remainder is
    ///     client work over a single result set.
    /// </summary>
    public virtual int ServerQueryCount { get; } = serverQueryCount;
}
