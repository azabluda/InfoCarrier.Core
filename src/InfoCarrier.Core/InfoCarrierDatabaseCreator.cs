// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     Client-side database creator. The client has no physical store — schema operations are
///     no-ops (the server owns the real database). <see cref="EnsureCreated" /> reports success
///     so test seeding flows through.
/// </summary>
public class InfoCarrierDatabaseCreator : IDatabaseCreator
{
    /// <inheritdoc />
    public virtual bool EnsureCreated() => true;

    /// <inheritdoc />
    public virtual Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc />
    public virtual bool EnsureDeleted() => true;

    /// <inheritdoc />
    public virtual Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc />
    public virtual bool CanConnect() => true;

    /// <inheritdoc />
    public virtual Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc />
    public virtual bool HasTables() => true;

    /// <inheritdoc />
    public virtual Task<bool> HasTablesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc />
    public virtual void CreateTables()
    {
    }

    /// <inheritdoc />
    public virtual Task CreateTablesAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
