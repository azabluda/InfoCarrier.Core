// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     Client-side transaction manager. Transactions remote to the server (requirements §2.9);
///     full transaction support lands in Step 10 (S3). This minimal implementation supports the
///     no-transaction path so queries and change tracking work.
/// </summary>
public class InfoCarrierTransactionManager : IDbContextTransactionManager
{
    /// <inheritdoc />
    public virtual IDbContextTransaction? CurrentTransaction => null;

    /// <inheritdoc />
    public virtual IDbContextTransaction BeginTransaction()
        => throw new NotSupportedException("Transactions land in Step 10 (S3).");

    /// <inheritdoc />
    public virtual Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Transactions land in Step 10 (S3).");

    /// <inheritdoc />
    public virtual void CommitTransaction()
        => throw new NotSupportedException("Transactions land in Step 10 (S3).");

    /// <inheritdoc />
    public virtual Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Transactions land in Step 10 (S3).");

    /// <inheritdoc />
    public virtual void RollbackTransaction()
        => throw new NotSupportedException("Transactions land in Step 10 (S3).");

    /// <inheritdoc />
    public virtual Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Transactions land in Step 10 (S3).");

    /// <inheritdoc />
    public virtual IDbContextTransaction? OnSaveChanges()
        => null;

    /// <inheritdoc />
    public virtual void ResetState()
    {
    }

    /// <inheritdoc />
    public virtual Task ResetStateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
