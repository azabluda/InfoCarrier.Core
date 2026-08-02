// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     The stub transaction handed back by <see cref="InfoCarrierTransactionManager" /> while the
///     provider ignores transactions (roadmap M4). Commit and rollback do nothing; the warning
///     that says so is raised by the manager, not here.
/// </summary>
public class InfoCarrierTransaction : IDbContextTransaction
{
    /// <inheritdoc />
    public virtual Guid TransactionId { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public virtual void Commit()
    {
    }

    /// <inheritdoc />
    public virtual Task CommitAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual void Rollback()
    {
    }

    /// <inheritdoc />
    public virtual Task RollbackAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual void Dispose()
        => GC.SuppressFinalize(this);

    /// <inheritdoc />
    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return default;
    }
}
