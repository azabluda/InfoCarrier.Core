// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

/// <summary>
///     In-process <see cref="IInfoCarrierServer" />: executes InfoCarrier operations against
///     a real EF Core <see cref="Microsoft.EntityFrameworkCore.DbContext" /> resolved from DI.
///     This is the server half of the in-process test transport.
/// </summary>
/// <remarks>
///     Query rebinding and execution run through <see cref="ServerQueryExecutor" /> (Step 5);
///     SaveChanges replay lands in Step 10. The server resolves the context and serializer
///     per-request from DI (DI-first, requirements §4.2).
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
    public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
    {
        // Resolve the server context within a per-request scope (the server is a singleton;
        // the context is scoped). Build the model-aware serializer from the context's model —
        // IModel is scoped to the context and must not be resolved from DI directly.
        await using var scope = _serviceProvider.CreateAsyncScope();
        DbContext context = scope.ServiceProvider.GetRequiredService<DbContext>();
        ExpressionSerializer serializer = ExpressionSerializer.CreateForModel(context.Model);
        var executor = new ServerQueryExecutor(context, serializer);
        return await executor.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
    }

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
