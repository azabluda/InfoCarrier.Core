// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace InfoCarrier.Core;

/// <summary>
///     In-process <see cref="IInfoCarrierServer" />: executes InfoCarrier operations against
///     a real EF Core <see cref="Microsoft.EntityFrameworkCore.DbContext" /> resolved from DI.
///     This is the server half of the in-process test transport.
/// </summary>
/// <remarks>
///     Query rebinding and execution (stub → <c>DbSet&lt;T&gt;</c> → <c>QueryRootExpression</c>)
///     land in Step 5; SaveChanges replay lands in Step 10. This shell establishes the DI shape
///     and the operation routing the test harness exercises.
/// </remarks>
public sealed class InProcessInfoCarrierServer : IInfoCarrierServer
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InProcessInfoCarrierServer" /> class.
    /// </summary>
    public InProcessInfoCarrierServer(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    /// <inheritdoc />
    public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Server query execution lands in Step 5.");

    /// <inheritdoc />
    public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("SaveChanges replay lands in Step 10.");

    /// <inheritdoc />
    public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Transaction support lands with SaveChanges (Step 10).");

    /// <inheritdoc />
    public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Transaction support lands with SaveChanges (Step 10).");

    /// <inheritdoc />
    public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Transaction support lands with SaveChanges (Step 10).");
}
