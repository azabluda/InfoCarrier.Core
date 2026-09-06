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
    private EventDefinitionBase? _logQuerySplit;
    private EventDefinitionBase? _logQuerySplitClientOperators;

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

    /// <summary>
    ///     The definition for <see cref="InfoCarrierEventId.QuerySplit" /> that also names what
    ///     the client kept.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The same event id, a second definition, and the reason is compatibility.</b>
    ///         <see cref="LogQuerySplit" /> returns <see cref="EventDefinition{T}" /> and is
    ///         public, so widening it to carry a second parameter would change a published
    ///         signature. The id is what a caller passes to <c>ConfigureWarnings</c> and
    ///         <c>LogTo</c>, and it is deliberately unchanged: this is the same event, said
    ///         better.
    ///     </para>
    ///     <para>
    ///         <b>The second parameter is the actionable half.</b> The first says how many queries
    ///         the server runs, which tells a reader that a split happened. It does not say
    ///         whether the split cost anything, and the difference is whether the part left here
    ///         only reshapes rows or also drops them. <c>RowRemovingOperators</c> answers that,
    ///         and it is walked only after the log guards, because a split is decided per
    ///         execution.
    ///     </para>
    /// </remarks>
    /// <param name="logger">The logger whose options supply the warnings configuration.</param>
    public virtual EventDefinition<int, string> LogQuerySplitClientOperators(IDiagnosticsLogger logger)
        => (EventDefinition<int, string>)(_logQuerySplitClientOperators ??= new EventDefinition<int, string>(
            logger.Options,
            InfoCarrierEventId.QuerySplit,
            LogLevel.Information,
            "InfoCarrierEventId.QuerySplit",
            level => LoggerMessage.Define<int, string>(
                level,
                InfoCarrierEventId.QuerySplit,
                "Part of the query cannot be sent to the server. The server runs {serverQueryCount} "
                    + "query/queries and this client runs the rest over the rows returned. "
                    + "{clientOperators}")));
}
