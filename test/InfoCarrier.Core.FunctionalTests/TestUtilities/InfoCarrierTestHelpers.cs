// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     EF's <see cref="TestHelpers" /> for the InfoCarrier client provider, mirroring
///     <c>InMemoryTestHelpers</c>.
/// </summary>
/// <remarks>
///     A spec fixture that builds its model <em>externally</em> — <c>F1FixtureBase</c> is the one
///     that does, deliberately, as regression coverage for building a model away from a context
///     instance — reaches for <c>TestHelpers.CreateConventionBuilder()</c>. That needs the
///     provider's own conventions, so each provider supplies its own.
/// </remarks>
public class InfoCarrierTestHelpers : TestHelpers
{
    private InfoCarrierTestHelpers()
    {
    }

    /// <summary>
    ///     The singleton instance.
    /// </summary>
    public static InfoCarrierTestHelpers Instance { get; } = new();

    /// <inheritdoc />
    public override LoggingDefinitions LoggingDefinitions { get; } = new InfoCarrierLoggingDefinitions();

    /// <inheritdoc />
    public override IServiceCollection AddProviderServices(IServiceCollection services)
        => services.AddEntityFrameworkInfoCarrier();

    /// <inheritdoc />
    /// <remarks>
    ///     The client is never used: these options exist to give the model builder a provider,
    ///     not to run anything. A stub that throws says so, rather than silently talking to a
    ///     server nobody configured.
    /// </remarks>
    public override DbContextOptionsBuilder UseProviderOptions(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseInfoCarrier(UnusedClient.Instance);

    private sealed class UnusedClient : IInfoCarrierClient
    {
        public static UnusedClient Instance { get; } = new();

        public string ServerUrl => nameof(InfoCarrierTestHelpers);

        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext clientContext, CancellationToken cancellationToken = default)
            => throw new NotSupportedException(Message);

        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, DbContext clientContext, CancellationToken cancellationToken = default)
            => throw new NotSupportedException(Message);

        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException(Message);

        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException(Message);

        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException(Message);

        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException(Message);

        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException(Message);

        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException(Message);

        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException(Message);

        private static string Message
            => $"{nameof(InfoCarrierTestHelpers)} builds models; it has no server to talk to.";
    }
}
