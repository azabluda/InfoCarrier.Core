// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace InfoCarrier.Core;

/// <summary>
///     Server-side abstraction that executes InfoCarrier operations against a real
///     EF Core <see cref="Microsoft.EntityFrameworkCore.DbContext" /> bound to a real provider.
/// </summary>
/// <remarks>
///     The server resolves entity types through its own model, rebinds query-root stubs to
///     real <c>DbSet&lt;T&gt;</c> / <c>QueryRootExpression</c> nodes, and executes the query
///     (research-findings §2, §8).
/// </remarks>
public interface IInfoCarrierServer
{
    /// <summary>
    ///     Executes a deserialized query against the server context and returns wire-format rows.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The query result rows and metadata.</returns>
    Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Replays change entries against the server context and returns store-generated values.
    /// </summary>
    /// <param name="request">The change entries.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>Store-generated values keyed back to the submitted entries.</returns>
    Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Begins a transaction on the server context and returns a token identifying it.
    /// </summary>
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
