// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>
///     Raises this provider's own log events, in the shape EF Core raises its own
///     (<c>CoreLoggerExtensions</c>).
/// </summary>
public static class InfoCarrierLoggerExtensions
{
    /// <summary>
    ///     Raises a client query or save failure through the configured diagnostic channels.
    /// </summary>
    /// <remarks>
    ///     Reports classification details only. Callers must not include exception messages,
    ///     queries, entity values, or request contents in these fields.
    /// </remarks>
    /// <typeparam name="TCategory">The query or update diagnostic category.</typeparam>
    /// <param name="diagnostics">The logger for the current operation.</param>
    /// <param name="eventId">
    ///     <see cref="InfoCarrierEventId.ClientQueryFailure" /> or
    ///     <see cref="InfoCarrierEventId.ClientSaveFailure" />.
    /// </param>
    /// <param name="level">The default severity for this failure classification.</param>
    /// <param name="operation">The bounded local operation name.</param>
    /// <param name="phase">The phase observed by this client.</param>
    /// <param name="outcome">The bounded outcome classification.</param>
    /// <param name="exceptionType">The runtime exception type name, without its contents.</param>
    public static void ClientFailure<TCategory>(this IDiagnosticsLogger<TCategory> diagnostics, EventId eventId,
        LogLevel level, string operation, string phase, string outcome, string exceptionType)
        where TCategory : LoggerCategory<TCategory>, new()
    {
        var definitions = (InfoCarrierLoggingDefinitions)diagnostics.Definitions;
        EventDefinition<string, string, string, string> definition = definitions.LogClientFailure(diagnostics, eventId, level);
        if (diagnostics.ShouldLog(definition))
        {
            definition.Log(diagnostics, operation, phase, outcome, exceptionType);
        }
        if (diagnostics.NeedsEventData(definition, out bool diagnosticSourceEnabled, out bool simpleLogEnabled))
        {
            var data = new ClientFailureEventData(definition, ClientFailureMessage, operation, phase, outcome, exceptionType);
            diagnostics.DispatchEventData(definition, data, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }

    private static string ClientFailureMessage(EventDefinitionBase definition, EventData data)
    {
        var failure = (ClientFailureEventData)data;
        return ((EventDefinition<string, string, string, string>)definition)
            .GenerateMessage(failure.Operation, failure.Phase, failure.Outcome, failure.ExceptionType);
    }

    /// <summary>
    ///     Raises <see cref="InfoCarrierEventId.QuerySplit" />, naming the operators the client
    ///     kept that remove rows.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Version 11 refuses implicit local filtering, ordering, grouping, joins and equality
    ///         before raising this event. Eligible projection reconstruction and bounded terminal
    ///         operations still report the remaining client work.
    ///     </para>
    /// </remarks>
    /// <param name="diagnostics">The query logger.</param>
    /// <param name="serverQueryCount">How many queries the server runs for this execution.</param>
    /// <param name="clientRemainder">The part of the query this client runs.</param>
    public static void QuerySplit(
        this IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        int serverQueryCount,
        Expression clientRemainder)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(clientRemainder);
        if (diagnostics.Definitions is not InfoCarrierLoggingDefinitions definitions)
        {
            return;
        }

        EventDefinition<int, string> definition = definitions.LogQuerySplitClientOperators(diagnostics);

        bool shouldLog = diagnostics.ShouldLog(definition);
        bool needsEventData = diagnostics.NeedsEventData(
            definition, out bool diagnosticSourceEnabled, out bool simpleLogEnabled);

        if (!shouldLog && !needsEventData)
        {
            return;
        }

        string clientOperators = RowRemovingOperators.Describe(clientRemainder);

        if (shouldLog)
        {
            definition.Log(diagnostics, serverQueryCount, clientOperators);
        }

        if (needsEventData)
        {
            var eventData = new QuerySplitEventData(
                definition, QuerySplitWithClientOperators, serverQueryCount, clientOperators);

            diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }

    private static string QuerySplitWithClientOperators(EventDefinitionBase definition, EventData payload)
    {
        var d = (EventDefinition<int, string>)definition;
        var p = (QuerySplitEventData)payload;
        return d.GenerateMessage(p.ServerQueryCount, p.ClientOperators);
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

    /// <summary>
    ///     One sentence naming the operators the client kept that remove rows, or saying that it
    ///     kept none.
    /// </summary>
    /// <remarks>
    ///     Empty for a payload built by the constructor that predates it, which carries no such
    ///     sentence. A subscriber that wants the operators should test for an empty string rather
    ///     than assume one is always present.
    /// </remarks>
    public virtual string ClientOperators { get; } = string.Empty;

    /// <summary>
    ///     Initializes a new instance of the <see cref="QuerySplitEventData" /> class that also
    ///     carries what the client kept.
    /// </summary>
    /// <param name="eventDefinition">The event definition.</param>
    /// <param name="messageGenerator">Builds the message, lazily.</param>
    /// <param name="serverQueryCount">How many queries the server runs for this execution.</param>
    /// <param name="clientOperators">
    ///     One sentence naming the operators the client kept that remove rows.
    /// </param>
    public QuerySplitEventData(
        EventDefinitionBase eventDefinition,
        Func<EventDefinitionBase, EventData, string> messageGenerator,
        int serverQueryCount,
        string clientOperators)
        : this(eventDefinition, messageGenerator, serverQueryCount)
        => ClientOperators = clientOperators;
}
