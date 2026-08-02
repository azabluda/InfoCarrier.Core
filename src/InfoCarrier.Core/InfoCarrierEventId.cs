// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>
///     Event IDs for InfoCarrier provider events logged to an <see cref="ILogger" /> and sent to a
///     <see cref="DiagnosticSource" />. These are also the IDs passed to
///     <see cref="Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.ConfigureWarnings" />.
/// </summary>
public static class InfoCarrierEventId
{
    // These values must not change between releases. Add to the end of a section, never in the
    // middle.
    private enum Id
    {
        // Transaction events
        TransactionIgnoredWarning = CoreEventId.ProviderBaseId,
    }

    private static readonly string TransactionPrefix = DbLoggerCategory.Database.Transaction.Name + ".";

    /// <summary>
    ///     A transaction operation was requested but ignored, because the InfoCarrier provider does
    ///     not yet remote transactions (roadmap M4).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This event is in the <see cref="DbLoggerCategory.Database.Transaction" /> category
    ///         and defaults to <see cref="WarningBehavior.Throw" />, exactly as EF Core's InMemory
    ///         provider does for its own transaction-ignored warning. A caller who genuinely wants
    ///         the no-op has to say so — silently pretending a transaction exists is the failure
    ///         mode worth avoiding here.
    ///     </para>
    /// </remarks>
    public static readonly EventId TransactionIgnoredWarning =
        new((int)Id.TransactionIgnoredWarning, TransactionPrefix + Id.TransactionIgnoredWarning);
}
