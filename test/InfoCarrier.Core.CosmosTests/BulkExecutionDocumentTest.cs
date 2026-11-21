// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned EF Core 11 CosmosBulkExecutionTest.

using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.EntityFrameworkCore.Cosmos.Diagnostics.Internal;
using Microsoft.EntityFrameworkCore.Cosmos.Infrastructure;
using Microsoft.EntityFrameworkCore.Cosmos.Internal;

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications.Bulk;

#pragma warning disable EF1001 // Preserve the official provider warning message assertion.

public class BulkExecutionDocumentTest(NonSharedFixture nonSharedFixture)
    : NonSharedModelTestBase(nonSharedFixture), IClassFixture<NonSharedFixture>
{
    protected override string NonSharedStoreName
        => nameof(BulkExecutionDocumentTest);

    [Fact]
    public virtual async Task DoesNotBatchSingleBatchableWrite()
    {
        var factory = await InitializeNonSharedTest<CosmosBulkExecutionContext>(
            onConfiguring: cfg => cfg.ConfigureWarnings(w => w.Ignore(CosmosEventId.BulkExecutionWithTransactionalBatch)));
        using var context = factory.CreateDbContext();

        context.Add(new Customer { PartitionKey = "4" });
        context.AddRange(Enumerable.Range(0, 3).Select(x => new Customer()));
        context.AddRange(Enumerable.Range(0, 3).Select(x => new Customer { PartitionKey = "2" }));
        context.Add(new Customer { PartitionKey = "3" });

        _backendLogger.Clear();

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(CosmosEventId.ExecutedCreateItem, _backendLogger.Log[0].Id);
        Assert.Equal(CosmosEventId.ExecutedCreateItem, _backendLogger.Log[1].Id);
        Assert.Equal(CosmosEventId.ExecutedTransactionalBatch, _backendLogger.Log[2].Id);
        Assert.Equal(CosmosEventId.ExecutedTransactionalBatch, _backendLogger.Log[3].Id);
    }

    [Fact]
    public virtual async Task AutoTransactionBehaviorNever_DoesNotThrow()
    {
        var contextFactory = await InitializeNonSharedTest<CosmosBulkExecutionContext>(
            onConfiguring: cfg => cfg.UseCosmos(x => x.BulkExecutionAllowed()));
        using var context = contextFactory.CreateDbContext();
        _mode = AutoTransactionBehavior.Never;
        context.Database.AutoTransactionBehavior = _mode;

        context.AddRange(Enumerable.Range(0, 100).Select(x => new Customer()));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public virtual async Task AutoTransactionBehaviorWhenNeeded_Throws()
    {
        var contextFactory = await InitializeNonSharedTest<CosmosBulkExecutionContext>(
            onConfiguring: cfg => cfg.UseCosmos(x => x.BulkExecutionAllowed()));
        using var context = contextFactory.CreateDbContext();
        _mode = AutoTransactionBehavior.WhenNeeded;
        context.Database.AutoTransactionBehavior = _mode;

        context.AddRange(Enumerable.Range(0, 200).Select(x => new Customer()));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(BulkExecutionWithTransactionalBatchMessage, ex.Message);
    }

    [Fact]
    public virtual async Task AutoTransactionBehaviorAlways_Throws()
    {
        var contextFactory = await InitializeNonSharedTest<CosmosBulkExecutionContext>(
            onConfiguring: cfg => cfg.UseCosmos(x => x.BulkExecutionAllowed()));
        using var context = contextFactory.CreateDbContext();
        _mode = AutoTransactionBehavior.Always;
        context.Database.AutoTransactionBehavior = _mode;

        context.AddRange(Enumerable.Range(0, 200).Select(x => new Customer()));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(BulkExecutionWithTransactionalBatchMessage, ex.Message);
    }

    [Fact]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Update/CosmosBulkExecutionTest.cs", 77, 89,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "The client-entry exception retains the reconstructed server exception as its inner exception.")]
    public async Task SessionEnabled_Throws()
    {
        var contextFactory = await InitializeNonSharedTest<CosmosBulkExecutionContext>(
            onConfiguring: cfg => cfg.UseCosmos(c => c.BulkExecutionAllowed()
                .SessionTokenManagementMode(SessionTokenManagementMode.SemiAutomatic)));
        using var context = contextFactory.CreateDbContext();
        _mode = AutoTransactionBehavior.Never;
        context.Database.AutoTransactionBehavior = _mode;
        _sessionToken = "0:-1#1"; // Actual backend facade setup, not a client wire feature.
        context.Add(new Customer());
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.IsType<InvalidOperationException>(Assert.IsType<DbUpdateException>(ex.InnerException).InnerException);
    }

    [Fact]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Update/CosmosBulkExecutionTest.cs", 91, 103,
        Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "The client-entry exception retains the reconstructed server exception as its inner exception.")]
    public async Task Trigger_Throws()
    {
        var contextFactory = await InitializeNonSharedTest<CosmosBulkExecutionContext>(
            onModelCreating: b => b.Entity<Customer>().HasTrigger(
                NonSharedStoreName, TriggerType.Post, TriggerOperation.Create),
            onConfiguring: cfg => cfg.UseCosmos(c => c.BulkExecutionAllowed()));
        using var context = contextFactory.CreateDbContext();
        _mode = AutoTransactionBehavior.Never;
        context.Database.AutoTransactionBehavior = _mode;
        context.Add(new Customer());
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.IsType<InvalidOperationException>(Assert.IsType<DbUpdateException>(ex.InnerException).InnerException);
    }

    private string BulkExecutionWithTransactionalBatchMessage
        => CoreStrings.WarningAsErrorTemplate(
            CosmosEventId.BulkExecutionWithTransactionalBatch.ToString(),
            CosmosResources.LogBulkExecutionWithTransactionalBatch(new TestLogger<CosmosLoggingDefinitions>()).GenerateMessage(),
            "CosmosEventId.BulkExecutionWithTransactionalBatch");

    public class CosmosBulkExecutionContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Customer> Customers { get; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Customer>().HasPartitionKey(x => x.PartitionKey);
    }

    public class Customer
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string PartitionKey { get; set; } = "1";
    }


    private readonly NonSharedModelInfoCarrierHarness _nonShared = new(new CosmosTier());
    private readonly ListLoggerFactory _backendLogger = new(c => c == DbLoggerCategory.Database.Command.Name);
    private AutoTransactionBehavior _mode = AutoTransactionBehavior.WhenNeeded;
    private string? _sessionToken;
    protected override ITestStoreFactory NonSharedTestStoreFactory => _nonShared.TestStoreFactory;

    protected override DbContextOptionsBuilder AddNonSharedOptions(DbContextOptionsBuilder builder)
    {
        using var backend = ((IInfoCarrierClientTestStore)NonSharedTestStore).Backend.CreateDbContext();
        return base.AddNonSharedOptions(builder).UseModel(backend.Model);
    }

    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<IServiceCollection, IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true, bool useServiceProvider = true, bool skipValidation = false)
    {
        NonSharedFixture = null;
        _nonShared.Prepare(typeof(TContext), onModelCreating,
            services => (addServices?.Invoke(services) ?? services)
                .Replace(ServiceDescriptor.Singleton<ILoggerFactory>(_backendLogger)),
            builder =>
            {
                onConfiguring?.Invoke(builder);
                builder.UseCosmos(c => c.BulkExecutionAllowed()).AddInterceptors(
                    new BackendSetup(() => _mode, () => _sessionToken));
            }, configureConventions);
        return base.CreateContextFactory<TContext>(onModelCreating, onConfiguring, addServices,
            configureConventions, shouldLogCategory, createTestStore, usePooling, useServiceProvider, skipValidation);
    }

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally { _backendLogger.Dispose(); }
    }

    private sealed class BackendSetup(Func<AutoTransactionBehavior> mode, Func<string?> sessionToken) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;
            context.Database.AutoTransactionBehavior = mode();
            if (sessionToken() is { } token) context.Database.UseSessionToken(token);
            return ValueTask.FromResult(result);
        }
    }
}
