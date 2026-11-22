// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned CosmosNonSharedSessionTokenTests; token controls configure actual backend storage.
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Cosmos.Infrastructure;
using Microsoft.EntityFrameworkCore.Cosmos.Storage.Internal;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications.Sessions;

using CosmosSessionTokenContext = SessionTokenDocumentTest.CosmosSessionTokenContext;
using Customer = SessionTokenDocumentTest.Customer;
using OtherContainerCustomer = SessionTokenDocumentTest.OtherContainerCustomer;

#pragma warning disable EF1001 // Preserve the official session-token storage behavior on the backend.

public class SessionTokenFailureDocumentTest(SessionTokenFailureDocumentTest.IsolatedEmulator emulator)
    : IAsyncLifetime, IClassFixture<SessionTokenFailureDocumentTest.IsolatedEmulator>
{
    // The official equality assertions require no intervening writes. This emulator advances
    // tokens even for writes in unrelated databases, so this class owns a separate container.
    public sealed class IsolatedEmulator : IAsyncLifetime
    {
        private readonly CosmosContainerFixture _container = new(publishEndpoint: false);
        public ValueTask InitializeAsync() => _container.InitializeAsync();
        public ValueTask DisposeAsync() => _container.DisposeAsync();
        internal DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string database)
            => _container.Configure(builder, database);
    }

    private const string OtherContainerName = "Other";
    private readonly DocumentApplicationStore<CosmosSessionTokenContext> _store = new(o => new(o) { Storage = NewStorage() })
    {
        ConfigureConnection = emulator.Configure,
        ConfigureBackend = b => b.UseCosmos(c => c.SessionTokenManagementMode(SessionTokenManagementMode.SemiAutomatic))
            .ReplaceService<ISessionTokenStorageFactory, SessionTokenDocumentTest.TestSessionTokenStorageFactory>()
    };
    public ValueTask InitializeAsync() => _store.InitializeAsync();
    public ValueTask DisposeAsync() => _store.DisposeAsync();
    private static ISessionTokenStorage NewStorage() => new SessionTokenStorage(nameof(CosmosSessionTokenContext),
        [nameof(CosmosSessionTokenContext), OtherContainerName], SessionTokenManagementMode.SemiAutomatic);
    private CosmosSessionTokenContext CreateContext(AutoTransactionBehavior mode = AutoTransactionBehavior.WhenNeeded)
    {
        var storage = NewStorage();
        return _store.CreateContext(createContext: o => new(o) { Storage = storage }, transactionMode: () => mode);
    }
    // This is deployment-side setup and observation. No Cosmos facade is added to the InfoCarrier client.
    private static ISessionTokenStorage BackendStorage(CosmosSessionTokenContext context) => context.Storage;
    private static async Task<InfoCarrierServerException> AssertCosmosFailure(Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<InfoCarrierServerException>(action);
        Assert.Equal(typeof(CosmosException).FullName, error.ServerExceptionTypeName);
        return error;
    }

    [Fact]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs", 630, 661,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData)]
    public virtual async Task UseSessionTokens_uses_session_tokens()
    {
        using var context = CreateContext();

        context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
        context.OtherContainerCustomers.Add(new OtherContainerCustomer { Id = "1", PartitionKey = "1" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sessionTokens = BackendStorage(context).GetTrackedTokens();

        Assert.NotNull(sessionTokens[nameof(CosmosSessionTokenContext)]);
        Assert.NotNull(sessionTokens[OtherContainerName]);

        // Only way we can test this is by setting a session token that will fail the request if used..
        // This will take a couple of seconds to fail
        var newTokens = sessionTokens.ToDictionary(x => x.Key, x => x.Value!.Substring(0, x.Value.IndexOf('#') + 1) + int.MaxValue);
        BackendStorage(context).SetSessionTokens(newTokens!);

        var exes = new List<InfoCarrierServerException>
            {
                await AssertCosmosFailure(() => context.Customers.ToListAsync()),
                await AssertCosmosFailure(() => context.OtherContainerCustomers.ToListAsync())
            };

        foreach (var ex in exes)
        {
            Assert.Contains("The read session is not available for the input session token.", ex.Message);
        }
    }

    [Fact]
    public virtual async Task ReadItem_does_not_exist_returns_null()
    {
        using var context = CreateContext();

        var result = await context.Customers.FirstOrDefaultAsync(x => x.Id == "nonexistent" && x.PartitionKey == "nonexistent", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs", 674, 707,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData)]
    public virtual async Task Read_item_session_not_found_throws_CosmosException()
    {
        using var context = CreateContext();

        context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
        context.OtherContainerCustomers.Add(new OtherContainerCustomer { Id = "1", PartitionKey = "1" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sessionTokens = BackendStorage(context).GetTrackedTokens();

        Assert.NotNull(sessionTokens[nameof(CosmosSessionTokenContext)]);
        Assert.NotNull(sessionTokens[OtherContainerName]);

        // Only way we can test this is by setting a session token that will fail the request if used..
        // This will take a couple of seconds to fail
        var newTokens = sessionTokens.ToDictionary(x => x.Key, x => x.Value!.Substring(0, x.Value.IndexOf('#') + 1) + int.MaxValue);
        BackendStorage(context).SetSessionTokens(newTokens!);

        var exes = new List<InfoCarrierServerException>
            {
                await AssertCosmosFailure(()
                    => context.Customers.FirstOrDefaultAsync(x => x.Id == "1" && x.PartitionKey == "1")),
                await AssertCosmosFailure(()
                    => context.OtherContainerCustomers.FirstOrDefaultAsync(x => x.Id == "1" && x.PartitionKey == "1"))
            };

        foreach (var ex in exes)
        {
            Assert.Contains("The read session is not available for the input session token.", ex.Message);
        }
    }

    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/331
    [Theory(Skip = "https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/331"),
     InlineData(AutoTransactionBehavior.Never), InlineData(AutoTransactionBehavior.Always)]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs", 767, 812,
        Skip = true, Justification = "https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/331")]
    public virtual async Task Optimistic_concurrency_precondition_failure_updates_session_token(
        AutoTransactionBehavior autoTransactionBehavior)
    {

        using var context = CreateContext(autoTransactionBehavior); // Actual backend mode for this logical context only.
        context.Database.AutoTransactionBehavior = autoTransactionBehavior;
        var customer = new Customer { Id = "1", PartitionKey = "1" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var createdSessionToken = BackendStorage(context).GetDefaultContainerTrackedToken();

        using var context2 = CreateContext();
        var customer2 = await context2.Customers.FirstAsync(x => x.Id == "1" && x.PartitionKey == "1", cancellationToken: TestContext.Current.CancellationToken);
        customer2.Name = "updated";
        await context2.SaveChangesAsync(TestContext.Current.CancellationToken);

        var updatedSessionToken = BackendStorage(context2).GetDefaultContainerTrackedToken();

        customer.Name = "updated again";

        Assert.NotEqual(createdSessionToken, updatedSessionToken);
        Assert.Equal(createdSessionToken, BackendStorage(context).GetDefaultContainerTrackedToken());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        var afterUpdateExceptionSessionToken = BackendStorage(context).GetDefaultContainerTrackedToken();
        Assert.Equal(updatedSessionToken, afterUpdateExceptionSessionToken);

        await context.Entry(customer).ReloadAsync(TestContext.Current.CancellationToken);
        customer.Name = "updated again";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await context2.Entry(customer2).ReloadAsync(TestContext.Current.CancellationToken);
        context2.Remove(customer2);
        await context2.SaveChangesAsync(TestContext.Current.CancellationToken);
        var removedSessionToken = BackendStorage(context2).GetDefaultContainerTrackedToken();

        context.Remove(customer);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        var afterRemoveExceptionSessionToken = BackendStorage(context).GetDefaultContainerTrackedToken();
        Assert.Equal(removedSessionToken, afterRemoveExceptionSessionToken);
    }

    [Theory, InlineData(AutoTransactionBehavior.Never), InlineData(AutoTransactionBehavior.Always)]
    // Observe each logical context's backend storage. The original token equality is unchanged;
    // the direct control and failure details distinguish backend behavior from transport behavior.
    public virtual async Task Add_conflict_updates_session_token(AutoTransactionBehavior autoTransactionBehavior)
        => await AssertConflictSessionToken(autoTransactionBehavior, direct: false);

    [Theory, InlineData(AutoTransactionBehavior.Never), InlineData(AutoTransactionBehavior.Always)]
    public async Task Direct_store_add_conflict_updates_session_token(AutoTransactionBehavior autoTransactionBehavior)
        => await AssertConflictSessionToken(autoTransactionBehavior, direct: true);

    [Theory, InlineData(false), InlineData(true)]
    public async Task Conflict_token_is_unchanged_while_shared_emulator_writes(bool direct)
    {
        await using var independentStore = new DocumentApplicationStore<CosmosSessionTokenContext>(o => new(o));
        await independentStore.InitializeAsync();
        await AssertConflictSessionToken(AutoTransactionBehavior.Never, direct, async () =>
        {
            await Task.Run(async () =>
            {
                using var independent = independentStore.CreateBackendContext();
                independent.Customers.Add(new Customer { Id = "independent", PartitionKey = "other" });
                await independent.SaveChangesAsync(TestContext.Current.CancellationToken);
                Assert.Equal(1, await independent.Customers.CountAsync(TestContext.Current.CancellationToken));
            });
        });
    }

    private async Task AssertConflictSessionToken(AutoTransactionBehavior autoTransactionBehavior, bool direct,
        Func<Task>? interveningWrite = null)
    {
        using var context = direct ? _store.CreateBackendContext() : CreateContext(autoTransactionBehavior);
        context.Database.AutoTransactionBehavior = autoTransactionBehavior;
        var customer = new Customer { Id = "1", PartitionKey = "1" };
        context.Customers.Add(customer);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var createdSessionToken = BackendStorage(context).GetDefaultContainerTrackedToken();
        if (interveningWrite is not null) await interveningWrite();

        using var context2 = direct ? _store.CreateBackendContext() : CreateContext();
        context2.Add(customer);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context2.SaveChangesAsync(TestContext.Current.CancellationToken));
        var afterExceptionSessionToken = BackendStorage(context2).GetDefaultContainerTrackedToken();

        Assert.True(createdSessionToken == afterExceptionSessionToken,
            $"Expected session token '{createdSessionToken}', actual '{afterExceptionSessionToken ?? "<null>"}'. "
            + $"Direct store: {direct}. Conflict failure: {error}");
    }

    [Fact]
    public virtual async Task Read_item_not_found_updates_session_token()
    {

        using var context2 = CreateContext();
        var customer = new OtherContainerCustomer { Id = "1", PartitionKey = "1" };
        context2.Add(customer);
        await context2.SaveChangesAsync(TestContext.Current.CancellationToken);

        var createdSessionToken = BackendStorage(context2).GetDefaultContainerTrackedToken()!;
        var customer2 = await context2.OtherContainerCustomers.FirstAsync(x => x.Id == "1" && x.PartitionKey == "1", cancellationToken: TestContext.Current.CancellationToken);
        context2.Remove(customer2);
        await context2.SaveChangesAsync(TestContext.Current.CancellationToken);

        var removedSessionToken = BackendStorage(context2).GetDefaultContainerTrackedToken();
        using var context = CreateContext();
        BackendStorage(context).SetDefaultContainerSessionToken(
            createdSessionToken); // Guarantee we don't read before creation, and we don't use the deleted session token.

        OtherContainerCustomer? result = null;
        var timeout = TimeSpan.FromSeconds(10);
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < timeout)
        {
            result = await context.OtherContainerCustomers.FirstOrDefaultAsync(x => x.Id == "1" && x.PartitionKey == "1", cancellationToken: TestContext.Current.CancellationToken);
            if (result
                == null) // We could theoretically hit a read replica that hasn't synced the delete yet. Because we used createdSessionToken. Chances are small
            {
                break;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.True(
            result is null,
            $"Timed out after waiting {timeout} for the deleted document to be not found before comparing session tokens.");

        var afterNotFoundSessionToken = BackendStorage(context).GetDefaultContainerTrackedToken();
        Assert.Equal(removedSessionToken, afterNotFoundSessionToken);
    }

    [Fact]
    public virtual async Task Remove_not_found_updates_session_token()
    {

        using var context2 = CreateContext();
        var customer = new OtherContainerCustomer { Id = "1", PartitionKey = "1" };
        context2.Add(customer);
        await context2.SaveChangesAsync(TestContext.Current.CancellationToken);

        var createdSessionToken = BackendStorage(context2).GetDefaultContainerTrackedToken()!;
        var customer2 = await context2.OtherContainerCustomers.FirstAsync(x => x.Id == "1" && x.PartitionKey == "1", cancellationToken: TestContext.Current.CancellationToken);
        context2.Remove(customer2);
        await context2.SaveChangesAsync(TestContext.Current.CancellationToken);

        var removedSessionToken = BackendStorage(context2).GetDefaultContainerTrackedToken();
        using var context = CreateContext();
        BackendStorage(context).SetDefaultContainerSessionToken(
            createdSessionToken); // Guarantee we don't read before creation, and we don't use the deleted session token.

        context.Remove(customer);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        var afterNotFoundSessionToken = BackendStorage(context).GetDefaultContainerTrackedToken();
        Assert.Equal(removedSessionToken, afterNotFoundSessionToken);
    }

    [Fact]
    public virtual async Task Replace_not_found_updates_session_token()
    {

        using var context2 = CreateContext();
        var customer = new OtherContainerCustomer { Id = "1", PartitionKey = "1" };
        context2.Add(customer);
        await context2.SaveChangesAsync(TestContext.Current.CancellationToken);

        var createdSessionToken = BackendStorage(context2).GetDefaultContainerTrackedToken()!;
        var customer2 = await context2.OtherContainerCustomers.FirstAsync(x => x.Id == "1" && x.PartitionKey == "1", cancellationToken: TestContext.Current.CancellationToken);
        context2.Remove(customer2);
        await context2.SaveChangesAsync(TestContext.Current.CancellationToken);

        var removedSessionToken = BackendStorage(context2).GetDefaultContainerTrackedToken();
        using var context = CreateContext();
        BackendStorage(context).SetDefaultContainerSessionToken(
            createdSessionToken); // Guarantee we don't read before creation, and we don't use the deleted session token.

        customer.Name = "updated";
        context.Update(customer);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        var afterNotFoundSessionToken = BackendStorage(context).GetDefaultContainerTrackedToken();
        Assert.Equal(removedSessionToken, afterNotFoundSessionToken);
    }
}
