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

        // Query events. Based 100 above the transaction section so each section can grow without
        // renumbering the other; the rule above is what makes the gap worth leaving.
        QuerySplit = CoreEventId.ProviderBaseId + 100,
    }

    private static readonly string TransactionPrefix = DbLoggerCategory.Database.Transaction.Name + ".";

    private static readonly string QueryPrefix = DbLoggerCategory.Query.Name + ".";

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

    /// <summary>
    ///     Part of a query could not be sent to the server, so the client executes the remainder
    ///     over the rows the server returned.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Splitting a query is normal and is what lets this provider answer a query whose
    ///         projection calls your own code. This event exists because one case of it is not
    ///         normal and is otherwise silent: when the part left on the client is a
    ///         <c>Where</c>, the server applies no filter and every row crosses the wire. The
    ///         query returns the correct rows, so nothing else tells you.
    ///     </para>
    ///     <para>
    ///         Logged at <see cref="LogLevel.Information" /> and only when a split actually
    ///         happens. A query the server executes whole raises nothing, which is the common
    ///         case and stays as cheap as it was. Note that the split is decided per execution
    ///         rather than per compilation, so a hot split query logs on every execution.
    ///     </para>
    /// </remarks>
    public static readonly EventId QuerySplit =
        new((int)Id.QuerySplit, QueryPrefix + Id.QuerySplit);
}
