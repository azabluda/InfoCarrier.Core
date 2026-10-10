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
        // Query events, based 100 above `ProviderBaseId` so that a transaction section can be
        // added later without renumbering this one.
        QuerySplit = CoreEventId.ProviderBaseId + 100,
    }

    private static readonly string QueryPrefix = DbLoggerCategory.Query.Name + ".";

    /// <summary>
    ///     Part of a query could not be sent to the server, so the client executes the remainder
    ///     over the rows the server returned.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Splitting a query is normal and is what lets this provider answer a query whose
    ///         projection calls your own code. It is also a wire cost the caller cannot otherwise
    ///         see: the server sends every row the shipped part yields, and the client discards
    ///         whatever the remainder drops, so more bytes can cross than the caller receives.
    ///         The answer is correct either way, so nothing else reports it.
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

    /// <summary>A local client query failed during preparation or result processing.</summary>
    /// <remarks>The event carries bounded phase, outcome, and exception type, without exception contents.</remarks>
    public static readonly EventId ClientQueryFailure = new(35101, QueryPrefix + nameof(ClientQueryFailure));

    /// <summary>A local client save failed while constructing changes or processing a response.</summary>
    /// <remarks>A processing failure does not imply that the server write was rolled back.</remarks>
    public static readonly EventId ClientSaveFailure = new(35102, DbLoggerCategory.Update.Name + "." + nameof(ClientSaveFailure));
}
