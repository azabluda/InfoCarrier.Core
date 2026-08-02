// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     Client-side transaction manager. Transactions remote to the server (requirements §2.9) in
///     milestone M4, which needs the wire-protocol W3 transaction token; until then every
///     transaction operation is <em>ignored</em> and says so.
/// </summary>
/// <remarks>
///     <para>
///         This mirrors EF Core's own <c>InMemoryTransactionManager</c>: the operation returns a
///         stub rather than throwing, and raises
///         <see cref="InfoCarrierEventId.TransactionIgnoredWarning" />, which
///         <see cref="InfoCarrierDbContextOptionsBuilderExtensions.UseInfoCarrier(DbContextOptionsBuilder, IInfoCarrierClient)" />
///         defaults to <see cref="WarningBehavior.Throw" />. A caller who wants the no-op opts in
///         with <c>ConfigureWarnings(w => w.Log(InfoCarrierEventId.TransactionIgnoredWarning))</c>,
///         so the ignore is never silent.
///     </para>
///     <para>
///         Throwing outright was the previous behaviour and it made the change-tracking spec bases
///         untestable: every mutation in <c>GraphUpdatesTestBase</c> and its siblings runs inside
///         <c>TestHelpers.ExecuteWithStrategyInTransactionAsync</c>, so a hard failure there hid
///         SaveChanges behind a feature scheduled two milestones later.
///     </para>
/// </remarks>
public class InfoCarrierTransactionManager : IDbContextTransactionManager
{
    private static readonly InfoCarrierTransaction StubTransaction = new();

    private readonly IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierTransactionManager" /> class.
    /// </summary>
    /// <param name="logger">The transaction-category diagnostics logger.</param>
    public InfoCarrierTransactionManager(IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger)
        => _logger = logger;

    /// <inheritdoc />
    public virtual IDbContextTransaction? CurrentTransaction => null;

    /// <inheritdoc />
    public virtual IDbContextTransaction BeginTransaction()
    {
        TransactionIgnored();
        return StubTransaction;
    }

    /// <inheritdoc />
    public virtual Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        TransactionIgnored();
        return Task.FromResult<IDbContextTransaction>(StubTransaction);
    }

    /// <inheritdoc />
    public virtual void CommitTransaction()
        => TransactionIgnored();

    /// <inheritdoc />
    public virtual Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        TransactionIgnored();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual void RollbackTransaction()
        => TransactionIgnored();

    /// <inheritdoc />
    public virtual Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        TransactionIgnored();
        return Task.CompletedTask;
    }

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

    private void TransactionIgnored()
    {
        EventDefinition definition =
            ((InfoCarrierLoggingDefinitions)_logger.Definitions).LogTransactionsNotSupported(_logger);

        if (_logger.ShouldLog(definition))
        {
            definition.Log(_logger);
        }

        if (_logger.NeedsEventData(definition, out bool diagnosticSourceEnabled, out bool simpleLogEnabled))
        {
            var eventData = new EventData(definition, (d, _) => ((EventDefinition)d).GenerateMessage());
            _logger.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
        }
    }
}
