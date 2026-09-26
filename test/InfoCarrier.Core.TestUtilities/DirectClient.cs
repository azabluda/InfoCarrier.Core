// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The plain-EF half of a slow run: the client context is a plain EF Core context on a store of
///     its own, with the server's options, and InfoCarrier is not involved (#167, ADR-014).
/// </summary>
/// <remarks>
///     <para>
///         <b>Per async flow, not per process.</b> A slow run runs both halves in one process, so
///         the side is an async-local that <see cref="LiveComparison" /> sets around the plain-EF
///         run of a test case, and a store reads it once, when it is created
///         (<see cref="InfoCarrierBackendTestStore.IsDirect" />). The satellite
///         <c>experiment/direct-baseline</c> read a process-wide environment variable instead, and
///         needed two runs.
///     </para>
///     <para>
///         <b>Off in every normal run</b>, where nothing sets it.
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

    /// <summary>The suffix a plain-EF store adds to its name, so the two halves never share a file.</summary>
    public const string StoreSuffix = ".direct";

    /// <summary>Sets the side for this async flow and the flows it starts.</summary>
    internal static void Set(bool direct)
        => Current.Value = direct;

    /// <summary>
    ///     Enlists <paramref name="facade" /> in a transaction another context of the same test
    ///     began: over the wire through the server's token, or through the shared connection in the
    ///     plain-EF half of a slow run.
    /// </summary>
    public static IDbContextTransaction? UseTestTransaction(this DatabaseFacade facade, IDbContextTransaction transaction)
        => IsEnabled
            ? facade.UseTransaction(transaction.GetDbTransaction())
            : facade.UseInfoCarrierTransaction(transaction);
}
