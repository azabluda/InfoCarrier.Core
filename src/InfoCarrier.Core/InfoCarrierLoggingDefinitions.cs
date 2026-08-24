// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>
///     InfoCarrier provider logging definitions. Providers supply their own subclass so EF's
///     <c>DiagnosticsLogger</c> resolves provider-specific event payloads.
/// </summary>
public class InfoCarrierLoggingDefinitions : LoggingDefinitions
{
    private EventDefinitionBase? _logTransactionsNotSupported;
    private EventDefinitionBase? _logQuerySplit;

    /// <summary>
    ///     The definition for <see cref="InfoCarrierEventId.TransactionIgnoredWarning" />.
    /// </summary>
    /// <remarks>
    ///     Cached on this instance the way EF caches its generated definitions: the
    ///     <see cref="EventDefinitionBase.WarningBehavior" /> is resolved once from the context's
    ///     warnings configuration, and this class is a singleton per service provider.
    /// </remarks>
    /// <param name="logger">The logger whose options supply the warnings configuration.</param>
    public virtual EventDefinition LogTransactionsNotSupported(IDiagnosticsLogger logger)
        => (EventDefinition)(_logTransactionsNotSupported ??= new EventDefinition(
            logger.Options,
            InfoCarrierEventId.TransactionIgnoredWarning,
            LogLevel.Warning,
            "InfoCarrierEventId.TransactionIgnoredWarning",
            level => LoggerMessage.Define(
                level,
                InfoCarrierEventId.TransactionIgnoredWarning,
                "Transactions are not supported by the InfoCarrier provider and the operation was "
                    + "ignored.")));

    /// <summary>
    ///     The definition for <see cref="InfoCarrierEventId.QuerySplit" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The parameter is the number of queries the server runs. It is the cheapest useful
    ///         number: a split of one server query means the whole remainder is client work over
    ///         one result set, which is the shape that fetches a table when the remainder holds
    ///         the filter.
    ///     </para>
    ///     <para>
    ///         Cached on this instance exactly as the definition above is, for the same reason.
    ///     </para>
    /// </remarks>
    /// <param name="logger">The logger whose options supply the warnings configuration.</param>
    public virtual EventDefinition<int> LogQuerySplit(IDiagnosticsLogger logger)
        => (EventDefinition<int>)(_logQuerySplit ??= new EventDefinition<int>(
            logger.Options,
            InfoCarrierEventId.QuerySplit,
            LogLevel.Information,
            "InfoCarrierEventId.QuerySplit",
            level => LoggerMessage.Define<int>(
                level,
                InfoCarrierEventId.QuerySplit,
                "Part of the query cannot be sent to the server. The server runs {serverQueryCount} "
                    + "query/queries and this client runs the rest over the rows returned. A filter "
                    + "left on the client does not reduce what crosses the wire.")));
}
