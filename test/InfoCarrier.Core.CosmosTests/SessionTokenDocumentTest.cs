// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from the application cases in pinned EF Core 11 CosmosSessionTokensTest.
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Cosmos.Infrastructure;
using Microsoft.EntityFrameworkCore.Cosmos.Storage.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications.Sessions;

#pragma warning disable EF1001 // Exercise the official provider's session-storage extension point.
#pragma warning disable EF9102 // Preserve the official experimental paging application tests.

public class SessionTokenDocumentTest : IAsyncLifetime
{
    private const string OtherContainerName = "Other";
    private readonly TestSessionTokenStorage _sessionTokenStorage = new();
    private readonly DocumentApplicationStore<CosmosSessionTokenContext> _store;

    public SessionTokenDocumentTest()
    {
        _store = new(o => new(o) { Storage = _sessionTokenStorage })
        {
            ConfigureBackend = b => b.UseCosmos(c => c.SessionTokenManagementMode(SessionTokenManagementMode.SemiAutomatic))
                .ReplaceService<ISessionTokenStorageFactory, TestSessionTokenStorageFactory>(),
            AllowedTypes = [typeof(CosmosPage<Customer>), typeof(CosmosPage<OtherContainerCustomer>),
                typeof(Task<CosmosPage<Customer>>), typeof(Task<CosmosPage<OtherContainerCustomer>>), typeof(CancellationToken)]
        };
    }
    public ValueTask InitializeAsync() => _store.InitializeAsync();
    public ValueTask DisposeAsync() => _store.DisposeAsync();
    protected Task<CosmosSessionTokenContext> CreateContext() => Task.FromResult(_store.CreateContext());

    [Fact]
    public async Task Paging_returns_values_and_continuation_like_the_direct_store()
    {
        using var client = await CreateContext();
        client.Customers.AddRange(Enumerable.Range(1, 3).Select(i => new Customer { Id = i.ToString(), PartitionKey = "1" }));
        await client.SaveChangesAsync(TestContext.Current.CancellationToken);
        client.ChangeTracker.Clear();
        var remoteIds = await ReadPages(client.Customers.AsNoTracking());
        using var direct = _store.CreateBackendContext();
        var directIds = await ReadPages(direct.Customers.AsNoTracking());
        Assert.Equal(new[] { "1", "2", "3" }, remoteIds);
        Assert.Equal(directIds, remoteIds);
        Assert.Empty(client.ChangeTracker.Entries());

        static async Task<List<string?>> ReadPages(IQueryable<Customer> source)
        {
            List<string?> ids = [];
            string? continuation = null;
            for (int i = 0; i < 10; i++)
            {
                var page = await source.OrderBy(c => c.Id).ToPageAsync(1, continuation,
                    cancellationToken: TestContext.Current.CancellationToken);
                Assert.InRange(page.Values.Count, 0, 1);
                ids.AddRange(page.Values.Select(c => c.Id));
                continuation = page.ContinuationToken;
                if (string.IsNullOrEmpty(continuation)) return ids;
            }
            Assert.Fail("The three-document query did not finish within ten one-document pages.");
            return ids;
        }
    }

    private static async Task<InfoCarrierServerException> AssertCosmosFailure(Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<InfoCarrierServerException>(action);
        Assert.Equal(typeof(CosmosException).FullName, error.ServerExceptionTypeName);
        return error;
    }
    private static InfoCarrierServerException StoreCause(DbUpdateException error)
    {
        var cause = Assert.IsType<InfoCarrierServerException>(Assert.IsType<DbUpdateException>(error.InnerException).InnerException);
        Assert.Equal(typeof(CosmosException).FullName, cause.ServerExceptionTypeName);
        return cause;
    }

    [Fact]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs", 70, 90,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the transmitted store exception type and message; update faults retain the client-entry wrapper.")]
    public virtual async Task Query_uses_session_token()
    {
        using var context = await CreateContext();

        _sessionTokenStorage.SessionTokens = new Dictionary<string, string?>
        {
            { OtherContainerName, "invalidtoken" }, { nameof(CosmosSessionTokenContext), "invalidtoken" }
        };

        var exes = new List<InfoCarrierServerException>
        {
            await AssertCosmosFailure(() => context.Customers.ToListAsync()),
            await AssertCosmosFailure(() => context.OtherContainerCustomers.ToListAsync())
        };

        foreach (var ex in exes)
        {
            Assert.Contains("The session token provided 'invalidtoken' is", ex.Message);
        }
    }

    [Fact]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs", 92, 112,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the transmitted store exception type and message; update faults retain the client-entry wrapper.")]
    public virtual async Task PagingQuery_uses_session_token()
    {
        using var context = await CreateContext();

        _sessionTokenStorage.SessionTokens = new Dictionary<string, string?>
        {
            { OtherContainerName, "invalidtoken" }, { nameof(CosmosSessionTokenContext), "invalidtoken" }
        };

        var exes = new List<InfoCarrierServerException>
        {
            await AssertCosmosFailure(() => context.Customers.ToPageAsync(1, null)),
            await AssertCosmosFailure(() => context.OtherContainerCustomers.ToPageAsync(1, null)),
        };

        foreach (var ex in exes)
        {
            Assert.Contains("The session token provided 'invalidtoken' is", ex.Message);
        }
    }

    [Fact]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs", 114, 135,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the transmitted store exception type and message; update faults retain the client-entry wrapper.")]
    public virtual async Task Shaped_query_uses_session_token()
    {
        using var context = await CreateContext();

        _sessionTokenStorage.SessionTokens = new Dictionary<string, string?>
        {
            { OtherContainerName, "invalidtoken" }, { nameof(CosmosSessionTokenContext), "invalidtoken" }
        };

        var exes = new List<InfoCarrierServerException>
        {
            await AssertCosmosFailure(() => context.Customers.Select(x => new { x.Id, x.PartitionKey }).ToListAsync()),
            await AssertCosmosFailure(()
                => context.OtherContainerCustomers.Select(x => new { x.Id, x.PartitionKey }).ToListAsync())
        };

        foreach (var ex in exes)
        {
            Assert.Contains("The session token provided 'invalidtoken' is", ex.Message);
        }
    }

    [Fact]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs", 137, 159,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the transmitted store exception type and message; update faults retain the client-entry wrapper.")]
    public virtual async Task Read_item_uses_session_token()
    {
        using var context = await CreateContext();

        _sessionTokenStorage.SessionTokens = new Dictionary<string, string?>
        {
            { OtherContainerName, "invalidtoken" }, { nameof(CosmosSessionTokenContext), "invalidtoken" }
        };

        var exes = new List<InfoCarrierServerException>
        {
            await AssertCosmosFailure(()
                => context.Customers.FirstOrDefaultAsync(x => x.Id == "1" && x.PartitionKey == "1")),
            await AssertCosmosFailure(()
                => context.OtherContainerCustomers.FirstOrDefaultAsync(x => x.Id == "1" && x.PartitionKey == "1"))
        };

        foreach (var ex in exes)
        {
            Assert.Contains("BadRequest (400)", ex.Message);
        }
    }

    [Fact]
    public virtual async Task Query_uses_TrackSessionToken()
    {
        using var context = await CreateContext();

        await context.Customers.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        await context.OtherContainerCustomers.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, _sessionTokenStorage.TrackSessionTokenCalls.Count);
        var (containerName, sessionToken) =
            _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == nameof(CosmosSessionTokenContext));
        var otherContainerCall = _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == OtherContainerName);

        Assert.NotEmpty(sessionToken);
        Assert.NotEmpty(otherContainerCall.sessionToken);
    }

    [Fact]
    public virtual async Task PagingQuery_uses_TrackSessionToken()
    {
        using var context = await CreateContext();

        await context.Customers.ToPageAsync(1, null, cancellationToken: TestContext.Current.CancellationToken);
        await context.OtherContainerCustomers.ToPageAsync(1, null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, _sessionTokenStorage.TrackSessionTokenCalls.Count);
        var (containerName, sessionToken) =
            _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == nameof(CosmosSessionTokenContext));
        var otherContainerCall = _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == OtherContainerName);

        Assert.NotEmpty(sessionToken);
        Assert.NotEmpty(otherContainerCall.sessionToken);
    }

    [Fact]
    public virtual async Task Read_item_uses_TrackSessionToken()
    {
        using var context = await CreateContext();

        await context.Customers.FirstOrDefaultAsync(x => x.Id == "1" && x.PartitionKey == "1", cancellationToken: TestContext.Current.CancellationToken);
        await context.OtherContainerCustomers.FirstOrDefaultAsync(x => x.Id == "1" && x.PartitionKey == "1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, _sessionTokenStorage.TrackSessionTokenCalls.Count);
        var (containerName, sessionToken) =
            _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == nameof(CosmosSessionTokenContext));
        var otherContainerCall = _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == OtherContainerName);

        Assert.NotEmpty(sessionToken);
        Assert.NotEmpty(otherContainerCall.sessionToken);
    }

    [Fact]
    public virtual async Task Read_item_enumerable_uses_TrackSessionToken()
    {
        using var context = await CreateContext();

        await context.Customers.Where(x => x.Id == "1" && x.PartitionKey == "1").ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        await context.OtherContainerCustomers.Where(x => x.Id == "1" && x.PartitionKey == "1").ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, _sessionTokenStorage.TrackSessionTokenCalls.Count);
        var (containerName, sessionToken) =
            _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == nameof(CosmosSessionTokenContext));
        var otherContainerCall = _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == OtherContainerName);

        Assert.NotEmpty(sessionToken);
        Assert.NotEmpty(otherContainerCall.sessionToken);
    }

    [Fact]
    public virtual async Task Add_AutoTransactionBehavior_Never_uses_TrackSessionToken()
    {
        using var context = await CreateContext();
        _store.TransactionMode = AutoTransactionBehavior.Never; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
        context.OtherContainerCustomers.Add(new OtherContainerCustomer { Id = "1", PartitionKey = "1" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _sessionTokenStorage.TrackSessionTokenCalls.Count);
        var (containerName, sessionToken) =
            _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == nameof(CosmosSessionTokenContext));
        var otherContainerCall = _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == OtherContainerName);

        Assert.NotEmpty(sessionToken);
        Assert.NotEmpty(otherContainerCall.sessionToken);
    }

    [Theory, InlineData(true), InlineData(false)]
    public virtual async Task Add_AutoTransactionBehavior_Always_uses_TrackSessionToken(bool defaultContainer)
    {
        using var context = await CreateContext();
        _store.TransactionMode = AutoTransactionBehavior.Always; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
        if (defaultContainer)
        {
            context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
        }
        else
        {
            context.OtherContainerCustomers.Add(new OtherContainerCustomer { Id = "1", PartitionKey = "1" });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Single(_sessionTokenStorage.TrackSessionTokenCalls);
        var (containerName, sessionToken) = _sessionTokenStorage.TrackSessionTokenCalls.First();

        if (defaultContainer)
        {
            Assert.Equal(nameof(CosmosSessionTokenContext), containerName);
        }
        else
        {
            Assert.Equal(OtherContainerName, containerName);
        }

        Assert.NotEmpty(sessionToken);
    }

    [Fact]
    public virtual async Task Delete_never_uses_TrackSessionToken()
    {
        using var context = await CreateContext();
        _store.TransactionMode = AutoTransactionBehavior.Never; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;

        var customer = new Customer { Id = "1", PartitionKey = "1" };
        var otherContainerCustomer = new OtherContainerCustomer { Id = "1", PartitionKey = "1" };
        context.Customers.Add(customer);
        context.OtherContainerCustomers.Add(otherContainerCustomer);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Order between the two containers within a single SaveChanges is not guaranteed; look up by name.
        Assert.Equal(2, _sessionTokenStorage.TrackSessionTokenCalls.Count);
        var (containerName, sessionToken) =
            _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == nameof(CosmosSessionTokenContext));
        var initialOtherContainerCall = _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == OtherContainerName);

        // Separate the create phase from the delete phase so the assertions below don't depend on
        // the relative ordering or count of the two phases' tracked calls.
        _sessionTokenStorage.TrackSessionTokenCalls.Clear();

        context.Customers.Remove(customer);
        context.OtherContainerCustomers.Remove(otherContainerCustomer);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _sessionTokenStorage.TrackSessionTokenCalls.Count);
        var defaultContainerCall =
            _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == nameof(CosmosSessionTokenContext));
        var otherContainerCall = _sessionTokenStorage.TrackSessionTokenCalls.Single(c => c.containerName == OtherContainerName);

        Assert.NotEmpty(defaultContainerCall.sessionToken);
        Assert.NotEmpty(otherContainerCall.sessionToken);

        Assert.NotEqual(sessionToken, defaultContainerCall.sessionToken);
        Assert.NotEqual(initialOtherContainerCall.sessionToken, otherContainerCall.sessionToken);
    }

    [Theory, InlineData(true), InlineData(false)]
    public virtual async Task Delete_always_uses_TrackSessionToken(bool defaultContainer)
    {
        using var context = await CreateContext();
        _store.TransactionMode = AutoTransactionBehavior.Always; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        if (defaultContainer)
        {
            context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
        }
        else
        {
            context.OtherContainerCustomers.Add(new OtherContainerCustomer { Id = "1", PartitionKey = "1" });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();
        var (containerName, sessionToken) = _sessionTokenStorage.TrackSessionTokenCalls[0];

        if (defaultContainer)
        {
            context.Customers.Remove(new Customer { Id = "1", PartitionKey = "1" });
        }
        else
        {
            context.OtherContainerCustomers.Remove(new OtherContainerCustomer { Id = "1", PartitionKey = "1" });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _sessionTokenStorage.TrackSessionTokenCalls.Count);
        var call = _sessionTokenStorage.TrackSessionTokenCalls[1];

        if (defaultContainer)
        {
            Assert.Equal(nameof(CosmosSessionTokenContext), call.containerName);
        }
        else
        {
            Assert.Equal(OtherContainerName, call.containerName);
        }

        Assert.NotEmpty(call.sessionToken);

        Assert.Equal(containerName, call.containerName);
        Assert.NotEqual(sessionToken, call.sessionToken);
    }

    [Fact]
    [InfoCarrierDesign(9, Justification = "Incomplete document writes read missing owned values before saving.",
        Deviation = DeviationKind.Other, DeviationNote = "Assert the additional repair-read token and the unchanged write-token observations.")]
    public virtual async Task Update_never_uses_TrackSessionToken()
    {
        using var context = await CreateContext();
        _store.TransactionMode = AutoTransactionBehavior.Never; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;

        var customer = new Customer { Id = "1", PartitionKey = "1" };
        var otherContainerCustomer = new OtherContainerCustomer { Id = "1", PartitionKey = "1" };
        context.Customers.Add(customer);
        context.OtherContainerCustomers.Add(otherContainerCustomer);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Order between the two containers within a single SaveChanges is not guaranteed; look up by name.
        var (containerName, sessionToken) = _sessionTokenStorage.TrackSessionTokenCalls.Take(2)
            .Single(c => c.containerName == nameof(CosmosSessionTokenContext));
        var initialOtherContainerCall =
            _sessionTokenStorage.TrackSessionTokenCalls.Take(2).Single(c => c.containerName == OtherContainerName);

        customer.Name = "updated";
        otherContainerCustomer.Name = "updated";

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, _sessionTokenStorage.TrackSessionTokenCalls.Count);
        var repairRead = _sessionTokenStorage.TrackSessionTokenCalls[2];
        Assert.Equal(nameof(CosmosSessionTokenContext), repairRead.containerName);
        Assert.NotEmpty(repairRead.sessionToken);
        var defaultContainerCall = _sessionTokenStorage.TrackSessionTokenCalls.Skip(3)
            .Single(c => c.containerName == nameof(CosmosSessionTokenContext));
        var otherContainerCall = _sessionTokenStorage.TrackSessionTokenCalls.Skip(3).Single(c => c.containerName == OtherContainerName);

        Assert.NotEmpty(defaultContainerCall.sessionToken);
        Assert.NotEmpty(otherContainerCall.sessionToken);

        Assert.NotEqual(sessionToken, defaultContainerCall.sessionToken);
        Assert.NotEqual(initialOtherContainerCall.sessionToken, otherContainerCall.sessionToken);
    }

    [Theory, InlineData(true), InlineData(false)]
    [InfoCarrierDesign(9, Justification = "Incomplete document writes read missing owned values before saving.",
        Deviation = DeviationKind.Other, DeviationNote = "Assert the additional repair-read token and the unchanged write-token observations.")]
    public virtual async Task Update_always_uses_TrackSessionToken(bool defaultContainer)
    {
        using var context = await CreateContext();
        _store.TransactionMode = AutoTransactionBehavior.Always; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        if (defaultContainer)
        {
            context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
        }
        else
        {
            context.OtherContainerCustomers.Add(new OtherContainerCustomer { Id = "1", PartitionKey = "1" });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();
        var (containerName, sessionToken) = _sessionTokenStorage.TrackSessionTokenCalls[0];

        if (defaultContainer)
        {
            context.Customers.Update(
                new Customer
                {
                    Id = "1",
                    Name = "updated",
                    PartitionKey = "1"
                });
        }
        else
        {
            context.OtherContainerCustomers.Update(
                new OtherContainerCustomer
                {
                    Id = "1",
                    Name = "updated",
                    PartitionKey = "1"
                });
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(defaultContainer ? 3 : 2, _sessionTokenStorage.TrackSessionTokenCalls.Count);
        if (defaultContainer)
        {
            var repairRead = _sessionTokenStorage.TrackSessionTokenCalls[1];
            Assert.Equal(nameof(CosmosSessionTokenContext), repairRead.containerName);
            Assert.NotEmpty(repairRead.sessionToken);
        }
        var call = _sessionTokenStorage.TrackSessionTokenCalls.Last();

        if (defaultContainer)
        {
            Assert.Equal(nameof(CosmosSessionTokenContext), call.containerName);
        }
        else
        {
            Assert.Equal(OtherContainerName, call.containerName);
        }

        Assert.NotEmpty(call.sessionToken);

        Assert.Equal(containerName, call.containerName);
        Assert.NotEqual(sessionToken, call.sessionToken);
    }

    [Theory, InlineData(AutoTransactionBehavior.WhenNeeded, true), InlineData(AutoTransactionBehavior.WhenNeeded, false),
     InlineData(AutoTransactionBehavior.Never, false), InlineData(AutoTransactionBehavior.Never, true),
     InlineData(AutoTransactionBehavior.Always, false), InlineData(AutoTransactionBehavior.Always, true)]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs", 465, 492,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the transmitted store exception type and message; update faults retain the client-entry wrapper.")]
    public virtual async Task Add_uses_GetSessionToken(AutoTransactionBehavior autoTransactionBehavior, bool defaultContainer)
    {
        using var context = await CreateContext();
        _store.TransactionMode = autoTransactionBehavior;
        context.Database.AutoTransactionBehavior = autoTransactionBehavior;

        // Only way we can test this is by setting a session token that will fail the request if used..
        // Only way to do this for a write is to set an invalid session token..
        _sessionTokenStorage.SessionTokens = new Dictionary<string, string?>
        {
            { defaultContainer ? nameof(CosmosSessionTokenContext) : OtherContainerName, "invalidtoken" }
        };

        if (defaultContainer)
        {
            context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
        }
        else
        {
            context.OtherContainerCustomers.Add(new OtherContainerCustomer { Id = "1", PartitionKey = "1" });
        }

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("The session token provided 'invalidtoken' is", StoreCause(ex).Message);
    }

    [Theory, InlineData(AutoTransactionBehavior.WhenNeeded, true), InlineData(AutoTransactionBehavior.WhenNeeded, false),
     InlineData(AutoTransactionBehavior.Never, false), InlineData(AutoTransactionBehavior.Never, true),
     InlineData(AutoTransactionBehavior.Always, false), InlineData(AutoTransactionBehavior.Always, true)]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs", 494, 522,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the transmitted store exception type and message; update faults retain the client-entry wrapper.")]
    [InfoCarrierDesign(9, Justification = "An invalid backend token refuses the required incomplete-document repair read.",
        Case = "defaultContainer=true", Deviation = DeviationKind.RefusedEarlier,
        DeviationNote = "The same Cosmos BadRequest occurs during the repair read before the provider write pipeline.")]
    public virtual async Task Update_uses_session_token(AutoTransactionBehavior autoTransactionBehavior, bool defaultContainer)
    {
        using var context = await CreateContext();
        _store.TransactionMode = autoTransactionBehavior;
        context.Database.AutoTransactionBehavior = autoTransactionBehavior;

        // Only way we can test this is by setting a session token that will fail the request if used..
        // Only way to do this for a write is to set an invalid session token..
        _sessionTokenStorage.SessionTokens = new Dictionary<string, string?>
        {
            { defaultContainer ? nameof(CosmosSessionTokenContext) : OtherContainerName, "invalidtoken" }
        };

        if (defaultContainer)
        {
            context.Customers.Update(new Customer { Id = "1", PartitionKey = "1" });
        }
        else
        {
            context.OtherContainerCustomers.Update(new OtherContainerCustomer { Id = "1", PartitionKey = "1" });
        }

        if (defaultContainer)
        {
            var repairFailure = await AssertCosmosFailure(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Contains("The session token provided 'invalidtoken' is", repairFailure.Message);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Contains("The session token provided 'invalidtoken' is", StoreCause(ex).Message);
        }
    }

    [Theory, InlineData(AutoTransactionBehavior.WhenNeeded, true), InlineData(AutoTransactionBehavior.WhenNeeded, false),
     InlineData(AutoTransactionBehavior.Never, false), InlineData(AutoTransactionBehavior.Never, true),
     InlineData(AutoTransactionBehavior.Always, false), InlineData(AutoTransactionBehavior.Always, true)]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs", 524, 552,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the transmitted store exception type and message; update faults retain the client-entry wrapper.")]
    public virtual async Task Delete_uses_session_token(AutoTransactionBehavior autoTransactionBehavior, bool defaultContainer)
    {
        using var context = await CreateContext();
        _store.TransactionMode = autoTransactionBehavior;
        context.Database.AutoTransactionBehavior = autoTransactionBehavior;

        // Only way we can test this is by setting a session token that will fail the request if used..
        // Only way to do this for a write is to set an invalid session token..
        _sessionTokenStorage.SessionTokens = new Dictionary<string, string?>
        {
            { defaultContainer ? nameof(CosmosSessionTokenContext) : OtherContainerName, "invalidtoken" }
        };

        if (defaultContainer)
        {
            context.Customers.Remove(new Customer { Id = "1", PartitionKey = "1" });
        }
        else
        {
            context.OtherContainerCustomers.Remove(new OtherContainerCustomer { Id = "1", PartitionKey = "1" });
        }

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("The session token provided 'invalidtoken' is", StoreCause(ex).Message);
    }

    public sealed class TestSessionTokenStorageFactory : ISessionTokenStorageFactory
    {
        public ISessionTokenStorage Create(DbContext context) => ((CosmosSessionTokenContext)context).Storage;
    }

    private class TestSessionTokenStorage : ISessionTokenStorage
    {
        public Dictionary<string, string?> SessionTokens { get; set; } =
            new() { { nameof(CosmosSessionTokenContext), null }, { OtherContainerName, null } };

        public List<string> AppendDefaultContainerSessionTokenCalls { get; } = [];
        public List<IReadOnlyDictionary<string, string>> AppendSessionTokensCalls { get; } = [];
        public List<string> SetDefaultContainerSessionTokenCalls { get; } = [];

        public List<IReadOnlyDictionary<string, string?>> SetSessionTokensCalls { get; } = [];
        public List<(string containerName, string sessionToken)> TrackSessionTokenCalls { get; } = [];
        public bool ClearCalled { get; set; }

        public void AppendDefaultContainerSessionToken(string sessionToken)
            => AppendDefaultContainerSessionTokenCalls.Add(sessionToken);

        public void AppendSessionTokens(IReadOnlyDictionary<string, string> sessionTokens)
            => AppendSessionTokensCalls.Add(sessionTokens);

        public void Clear()
            => ClearCalled = true;

        public string? GetDefaultContainerTrackedToken()
            => SessionTokens.FirstOrDefault().Value;

        public string? GetSessionToken(string containerName)
            => SessionTokens[containerName];

        public IReadOnlyDictionary<string, string?> GetTrackedTokens()
            => SessionTokens;

        public void SetDefaultContainerSessionToken(string sessionToken)
            => SetDefaultContainerSessionTokenCalls.Add(sessionToken);

        public void SetSessionTokens(IReadOnlyDictionary<string, string?> sessionTokens)
            => SetSessionTokensCalls.Add(sessionTokens);

        public void TrackSessionToken(string containerName, string? sessionToken)
            => TrackSessionTokenCalls.Add((containerName, sessionToken!));
    }

    public class CosmosSessionTokenContext(DbContextOptions options) : PoolableDbContext(options)
    {
        public ISessionTokenStorage Storage { get; init; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<OtherContainerCustomer> OtherContainerCustomers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Customer>(b =>
            {
                b.HasKey(c => c.Id);
                b.Property(c => c.ETag).IsETagConcurrency();
                b.OwnsMany(x => x.Children);
                b.HasPartitionKey(c => c.PartitionKey);
            });

            builder.Entity<OtherContainerCustomer>(b =>
            {
                b.HasKey(c => c.Id);
                b.HasPartitionKey(c => c.PartitionKey);
                b.ToContainer(OtherContainerName);
            });
        }
    }

    public class Customer
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? ETag { get; set; }

        public string? PartitionKey { get; set; }

        public ICollection<DummyChild> Children { get; } = new HashSet<DummyChild>();
    }

    public class DummyChild
    {
        public string? Id { get; init; }
    }

    public class OtherContainerCustomer
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? PartitionKey { get; set; }
    }
}
