// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     InfoCarrier additions to <see cref="DatabaseFacade" />.
/// </summary>
public static class InfoCarrierDatabaseFacadeExtensions
{
    /// <summary>
    ///     Runs this context's requests inside a server transaction another context began
    ///     (requirements §2.9, wire-protocol W3).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The InfoCarrier equivalent of relational <c>UseTransaction</c>. There the shared
    ///         thing is a <c>DbTransaction</c> on a connection; here it is the server's token,
    ///         which is what makes sharing possible at all across a transport that may be
    ///         stateless and may not keep a connection per client.
    ///     </para>
    ///     <para>
    ///         The returned transaction is not owned by this context: ending it detaches this
    ///         context and leaves the transaction to whoever began it. Two contexts both able to
    ///         commit one transaction would make the outcome depend on disposal order.
    ///     </para>
    /// </remarks>
    /// <param name="facade">The database facade of the context that should join.</param>
    /// <param name="transaction">A transaction from another InfoCarrier context.</param>
    public static IDbContextTransaction UseInfoCarrierTransaction(
        this DatabaseFacade facade,
        IDbContextTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction is not InfoCarrierTransaction infoCarrierTransaction)
        {
            throw new ArgumentException(
                $"Expected an {nameof(InfoCarrierTransaction)}; a transaction from another "
                    + "provider names nothing this server knows about.",
                nameof(transaction));
        }

        var manager = (InfoCarrierTransactionManager)((IInfrastructure<IServiceProvider>)facade)
            .GetService<IDbContextTransactionManager>();

        return manager.UseTransaction(infoCarrierTransaction.ServerTransactionId);
    }
}
