// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     An opt-in run of a tier with InfoCarrier REMOVED: the client context is a plain EF Core
///     context on the server's own store, with the server's options.
/// </summary>
/// <remarks>
///     <para>
///         <b>Off unless <c>INFOCARRIER_DIRECT_CLIENT</c> is set, and a run with it on asserts
///         nothing about this provider.</b> Its whole product is the server SQL log
///         (<see cref="ServerSqlLog" />): the statements plain EF runs for each test method, to set
///         beside the statements the server runs for the same method when InfoCarrier is in
///         between. That is a plain-EF baseline for every test method, not only for the ones EF's
///         own <c>AssertSql</c> covers.
///     </para>
///     <para>
///         Tests that assert something specific to InfoCarrier fail in such a run, and that is
///         expected: only the log is wanted.
///     </para>
/// </remarks>
public static class DirectClient
{
    /// <summary>
    ///     Whether the client is plain EF Core on the server's store rather than InfoCarrier.
    /// </summary>
    public static bool IsEnabled { get; }
        = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INFOCARRIER_DIRECT_CLIENT"));

    /// <summary>
    ///     Enlists <paramref name="facade" /> in a transaction another context of the same test
    ///     began: over the wire through the server's token, or directly through the shared
    ///     connection when <see cref="IsEnabled" />.
    /// </summary>
    public static IDbContextTransaction? UseTestTransaction(this DatabaseFacade facade, IDbContextTransaction transaction)
        => IsEnabled
            ? facade.UseTransaction(transaction.GetDbTransaction())
            : facade.UseInfoCarrierTransaction(transaction);
}
