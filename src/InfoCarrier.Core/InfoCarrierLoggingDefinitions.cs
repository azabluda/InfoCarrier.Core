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
                    + "ignored. Remoted transactions land in milestone M4.")));
}
