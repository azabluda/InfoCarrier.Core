// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core;

/// <summary>
///     Client-side abstraction for shipping InfoCarrier operations to the server.
///     This is the testability seam the in-process test transport implements
///     (v1's <c>InfoCarrierBackendTestStore</c> doubling as the client).
/// </summary>
/// <remarks>
///     All operations are async-first (requirements §4.3). Sync overloads are provided
///     for EF Core's sync code paths but never block on async internally.
/// </remarks>
public interface IInfoCarrierClient
{
    /// <summary>
    ///     Executes a query on the server and returns the materialized wire result.
    /// </summary>
    /// <param name="request">The serialized query request.</param>
    /// <param name="clientContext">
    ///     The context the query is running against. A transport needs it for anything the wire
    ///     format does not carry but the server must know: EF applies a context-dependent query
    ///     filter — <c>HasQueryFilter(c =&gt; c.Name.StartsWith(context.TenantPrefix))</c> — on
    ///     the *server's* model, using the *server's* context, and the client's value has to get
    ///     there somehow. v1 passed the context for the same reason.
    /// </param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The query result rows and metadata.</returns>
    Task<QueryDataResult> QueryDataAsync(
        QueryDataRequest request,
        DbContext clientContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persists change-tracker entries on the server and returns store-generated values.
    /// </summary>
    /// <param name="request">The serialized change entries.</param>
    /// <param name="clientContext">The context the save is running against.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>Store-generated values keyed back to the submitted entries.</returns>
    Task<SaveChangesResult> SaveChangesAsync(
        SaveChangesRequest request,
        DbContext clientContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Begins a server-side transaction and returns a token identifying it.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A token representing the open server transaction (wire-protocol W3).</returns>
    Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Commits the server transaction identified by <paramref name="transactionId" />.
    /// </summary>
    Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Rolls back the server transaction identified by <paramref name="transactionId" />.
    /// </summary>
    Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default);
}
