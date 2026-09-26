// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     A run with InfoCarrier REMOVED: the client context is a plain EF Core context on a store of
///     its own, with the server's options (#167, ADR-014).
/// </summary>
/// <remarks>
///     <para>
///         <b>SPIKE. Per async flow, not per process.</b> The satellite read a process-wide
///         environment variable; the live comparison runs both sides in one process, so the side
///         is an async-local that <c>CurrentTestFramework</c> sets around the direct run, and a
///         store reads it once, when it is created (<see cref="InfoCarrierBackendTestStore.IsDirect" />).
///     </para>
/// </remarks>
public static class DirectClient
{
    private static readonly AsyncLocal<bool> Current = new();

    /// <summary>
    ///     Whether the client being built in this async flow is plain EF Core rather than
    ///     InfoCarrier.
    /// </summary>
    public static bool IsEnabled => Current.Value;

    /// <summary>The suffix a direct store adds to its name, so the two sides never share a file.</summary>
    public const string StoreSuffix = ".direct";

    /// <summary>Sets the side for this async flow and the flows it starts.</summary>
    public static void Set(bool direct)
        => Current.Value = direct;

    /// <summary>
    ///     Enlists <paramref name="facade" /> in a transaction another context of the same test
    ///     began: over the wire through the server's token, or directly through the shared
    ///     connection on the direct side.
    /// </summary>
    public static IDbContextTransaction? UseTestTransaction(this DatabaseFacade facade, IDbContextTransaction transaction)
        => IsEnabled
            ? facade.UseTransaction(transaction.GetDbTransaction())
            : facade.UseInfoCarrierTransaction(transaction);
}
