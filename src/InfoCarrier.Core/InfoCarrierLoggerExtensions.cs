// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
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

    /// <summary>
    ///     Raises <see cref="InfoCarrierEventId.QuerySplit" />, naming the operators the client
    ///     kept that remove rows.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>An overload rather than a parameter, because the other one is published.</b>
    ///         Adding an argument to the two-parameter form would be source-compatible and binary
    ///         breaking: the compiler emits one member and the old arity leaves the assembly. Both
    ///         raise the same event id.
    ///     </para>
    ///     <para>
    ///         <b>The residual is walked inside the guards and never outside them.</b> The split
    ///         is decided per execution rather than per compiled query, so a hot split query
    ///         reaches this method on every execution and nothing here may walk a tree, build a
    ///         message or allocate an <see cref="EventData" /> when no one is listening. Both
    ///         guards are asked first and the walk happens once for the two of them.
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

        LogQuerySplit(diagnostics, serverQueryCount, clientRemainder, allowlist: null);
    }

    /// <summary>
    ///     Raises <see cref="InfoCarrierEventId.QuerySplit" />, naming the operators the client
    ///     kept that remove rows and the key types the server was not told about.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The key types are the half a caller can act on.</b> A <c>GroupBy</c> or a
    ///         <c>Join</c> keyed on a type the application declared stays on the client when that
    ///         type is not registered, and the server sends every row. The query succeeds, so
    ///         nothing else reports it, and the type's name is what
    ///         <see cref="InfoCarrierDbContextOptionsBuilder.AllowTypes" /> takes.
    ///     </para>
    ///     <para>
    ///         <b>Another overload, for the reason the one above gives.</b> The allowlist is the
    ///         server's own list, the one the boundary was drawn with, so the event names exactly
    ///         the types the boundary refused. A split with none to name logs the message it logged
    ///         before.
    ///     </para>
    /// </remarks>
    /// <param name="diagnostics">The query logger.</param>
    /// <param name="serverQueryCount">How many queries the server runs for this execution.</param>
    /// <param name="clientRemainder">The part of the query this client runs.</param>
    /// <param name="allowlist">The types the server accepts, which drew the boundary.</param>
    public static void QuerySplit(
        this IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        int serverQueryCount,
        Expression clientRemainder,
        TypeAllowlist allowlist)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(clientRemainder);
        ArgumentNullException.ThrowIfNull(allowlist);

        LogQuerySplit(diagnostics, serverQueryCount, clientRemainder, allowlist);
    }

    private static void LogQuerySplit(
        IDiagnosticsLogger<DbLoggerCategory.Query> diagnostics,
        int serverQueryCount,
        Expression clientRemainder,
        TypeAllowlist? allowlist)
    {
        if (diagnostics.Definitions is not InfoCarrierLoggingDefinitions definitions)
        {
            return;
        }

        EventDefinition<int, string> definition = definitions.LogQuerySplitClientOperators(diagnostics);

        // One question for all three definitions: they share the event id and the level, and
        // `ConfigureWarnings` is keyed by the id, so each would give the same answer.
        bool shouldLog = diagnostics.ShouldLog(definition);
        bool needsEventData = diagnostics.NeedsEventData(
            definition, out bool diagnosticSourceEnabled, out bool simpleLogEnabled);

        if (!shouldLog && !needsEventData)
        {
            return;
        }

        string clientOperators = RowRemovingOperators.Describe(clientRemainder);
        IReadOnlyList<Type> keyTypes = allowlist is null ? [] : UnregisteredKeyTypes.Find(clientRemainder, allowlist);

        if (keyTypes.Count > 0)
        {
            EventDefinition<int, string, string> named = definitions.LogQuerySplitUnregisteredKeyTypes(diagnostics);

            if (shouldLog)
            {
                named.Log(diagnostics, serverQueryCount, clientOperators, UnregisteredKeyTypes.Describe(keyTypes));
            }

            if (needsEventData)
            {
                var eventData = new QuerySplitEventData(
                    named, QuerySplitWithUnregisteredKeyTypes, serverQueryCount, clientOperators, keyTypes);

                diagnostics.DispatchEventData(named, eventData, diagnosticSourceEnabled, simpleLogEnabled);
            }

            return;
        }

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

    private static string QuerySplit(EventDefinitionBase definition, EventData payload)
    {
        var d = (EventDefinition<int>)definition;
        var p = (QuerySplitEventData)payload;
        return d.GenerateMessage(p.ServerQueryCount);
    }

    private static string QuerySplitWithClientOperators(EventDefinitionBase definition, EventData payload)
    {
        var d = (EventDefinition<int, string>)definition;
        var p = (QuerySplitEventData)payload;
        return d.GenerateMessage(p.ServerQueryCount, p.ClientOperators);
    }

    private static string QuerySplitWithUnregisteredKeyTypes(EventDefinitionBase definition, EventData payload)
    {
        var d = (EventDefinition<int, string, string>)definition;
        var p = (QuerySplitEventData)payload;
        return d.GenerateMessage(
            p.ServerQueryCount, p.ClientOperators, UnregisteredKeyTypes.Describe(p.UnregisteredKeyTypes));
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

    /// <summary>
    ///     The types that keys of the operators left on the client construct and that the server
    ///     was not told about. Registering one with
    ///     <see cref="InfoCarrierDbContextOptionsBuilder.AllowTypes" /> on the client and the
    ///     server can send its operator to the server.
    /// </summary>
    /// <remarks>
    ///     Empty when there is none, and for a payload built by a constructor that predates it.
    /// </remarks>
    public virtual IReadOnlyList<Type> UnregisteredKeyTypes { get; } = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="QuerySplitEventData" /> class that also
    ///     carries the key types the server was not told about.
    /// </summary>
    /// <param name="eventDefinition">The event definition.</param>
    /// <param name="messageGenerator">Builds the message, lazily.</param>
    /// <param name="serverQueryCount">How many queries the server runs for this execution.</param>
    /// <param name="clientOperators">
    ///     One sentence naming the operators the client kept that remove rows.
    /// </param>
    /// <param name="unregisteredKeyTypes">
    ///     The key types that kept an operator on the client.
    /// </param>
    public QuerySplitEventData(
        EventDefinitionBase eventDefinition,
        Func<EventDefinitionBase, EventData, string> messageGenerator,
        int serverQueryCount,
        string clientOperators,
        IReadOnlyList<Type> unregisteredKeyTypes)
        : this(eventDefinition, messageGenerator, serverQueryCount, clientOperators)
        => UnregisteredKeyTypes = unregisteredKeyTypes;
}
