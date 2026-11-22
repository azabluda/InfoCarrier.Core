// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned EF Core 11 CosmosTransactionalBatchTest.

using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.EntityFrameworkCore.Cosmos.Internal;
using Microsoft.EntityFrameworkCore.Cosmos.Storage.Internal;

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications.Transactions;

#pragma warning disable EF1001 // Preserve the pinned Cosmos execution strategy and refusal assertions.

public class TransactionalBatchDocumentTest : IAsyncLifetime
{
    private DocumentApplicationStore<TransactionalBatchContext> Fixture { get; } = new(o => new(o));

    [Fact]
    public virtual async Task
        SaveChanges_fails_for_duplicate_key_in_same_partition_prevents_other_inserts_in_same_partition_even_if_staged_before_add()
    {
        using (var arrangeContext = Fixture.CreateContext())
        {
            arrangeContext.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
            await arrangeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var context = Fixture.CreateContext();

        context.Customers.Add(new Customer { Id = "2", PartitionKey = "1" });
        context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });

        var updateException =
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Single(updateException.Entries);
        Assert.IsAssignableFrom<Customer>(updateException.Entries.First().Entity);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, customersCount);
    }

    [Fact]
    public virtual async Task SaveChanges_transactional_batch_failure_flows_through_execution_strategy()
    {
        using (var arrangeContext = Fixture.CreateContext())
        {
            arrangeContext.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
            await arrangeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var recordedExceptions = new List<Exception>();

        using var context = Fixture.CreateContext(
            b => b.UseCosmos(cfg => cfg.ExecutionStrategy(d => new RecordingExecutionStrategy(d, recordedExceptions))));
        Fixture.TransactionMode = AutoTransactionBehavior.Always; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        context.Customers.Add(new Customer { Id = "2", PartitionKey = "1" });
        context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });

        // Before the fix the transactional batch failure never reached the execution strategy, so it could not be
        // retried and RetryLimitExceededException was never thrown.
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.NotEmpty(recordedExceptions);
        Assert.All(
            recordedExceptions,
            e => Assert.Equal(HttpStatusCode.Conflict, Assert.IsType<CosmosException>(e).StatusCode));

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, customersCount);
    }

    [Fact]
    public virtual async Task SaveChanges_suppressed_concurrency_exception_in_transactional_batch_reports_zero_rows_affected()
    {
        using (var arrangeContext = Fixture.CreateContext())
        {
            arrangeContext.Customers.Add(
                new Customer
                {
                    Id = "1",
                    PartitionKey = "1",
                    Name = "Original"
                });
            await arrangeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var staleContext = Fixture.CreateContext(b => b.AddInterceptors(new ConcurrencySuppressingInterceptor()));
        Fixture.TransactionMode = AutoTransactionBehavior.Always;
        staleContext.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        var customer = await staleContext.Customers.SingleAsync(c => c.Id == "1", cancellationToken: TestContext.Current.CancellationToken);

        // Change the ETag in the database so the stale context will conflict
        using (var updateContext = Fixture.CreateContext())
        {
            var current = await updateContext.Customers.SingleAsync(c => c.Id == "1", cancellationToken: TestContext.Current.CancellationToken);
            current.Name = "Updated by other";
            await updateContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Update from the stale context — PreconditionFailed → DbUpdateConcurrencyException, then suppressed
        customer.Name = "Updated by stale";
        var rowsAffected = await staleContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // The suppressed exception means the batch was not committed, so rows affected should be 0
        Assert.Equal(0, rowsAffected);

        using var assertContext = Fixture.CreateContext();
        var customerInStore = await assertContext.Customers.SingleAsync(c => c.Id == "1", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Updated by other", customerInStore.Name);
    }

    private sealed class ConcurrencySuppressingInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(
            ConcurrencyExceptionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
            => new(InterceptionResult.Suppress());
    }

    private sealed class RecordingExecutionStrategy(ExecutionStrategyDependencies dependencies, List<Exception> recordedExceptions)
        : CosmosExecutionStrategy(dependencies, maxRetryCount: 2, maxRetryDelay: TimeSpan.FromMilliseconds(1))
    {
        protected override bool ShouldRetryOn(Exception exception)
        {
            recordedExceptions.Add(exception);
            return true;
        }
    }

    [Fact]
    public virtual async Task SaveChanges_fails_for_duplicate_key_in_same_partition_writes_only_partition_staged_before_error()
    {
        using (var arrangeContext = Fixture.CreateContext())
        {
            arrangeContext.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
            arrangeContext.Customers.Add(new Customer { Id = "2", PartitionKey = "1" });
            await arrangeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var context = Fixture.CreateContext();

        context.Customers.Add(new Customer { Id = "4", PartitionKey = "2" });
        context.Customers.Add(new Customer { Id = "3", PartitionKey = "1" });
        context.Customers.Add(new Customer { Id = "2", PartitionKey = "1" });
        context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
        context.Customers.Add(new Customer { Id = "5", PartitionKey = "2" });
        context.Customers.Add(new Customer { Id = "6", PartitionKey = "3" });

        var updateException =
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Single(updateException.Entries);
        Assert.IsAssignableFrom<Customer>(updateException.Entries.First().Entity);
        Assert.IsAssignableFrom<Customer>(updateException.Entries.Last().Entity);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        // The batch from the first partition found should have executed (partition 2, ids: 4,5)
        // No other batches after (and including) batch for partition 1 should have executed
        Assert.Equal(4, customersCount);
    }

    [Fact]
    public virtual async Task
        SaveChanges_transaction_behavior_never_fails_for_duplicate_key_in_same_partition_writes_all_staged_before_error()
    {
        using (var arrangeContext = Fixture.CreateContext())
        {
            arrangeContext.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
            arrangeContext.Customers.Add(new Customer { Id = "2", PartitionKey = "1" });
            await arrangeContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var context = Fixture.CreateContext();
        Fixture.TransactionMode = AutoTransactionBehavior.Never; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;

        context.Customers.Add(new Customer { Id = "4", PartitionKey = "2" });
        context.Customers.Add(new Customer { Id = "3", PartitionKey = "1" });
        context.Customers.Add(new Customer { Id = "2", PartitionKey = "1" });
        context.Customers.Add(new Customer { Id = "1", PartitionKey = "1" });
        context.Customers.Add(new Customer { Id = "5", PartitionKey = "2" });
        context.Customers.Add(new Customer { Id = "6", PartitionKey = "3" });

        var updateException =
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Single(updateException.Entries);
        Assert.IsAssignableFrom<Customer>(updateException.Entries.First().Entity);
        Assert.IsAssignableFrom<Customer>(updateException.Entries.Last().Entity);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        // The batch from the first partition found should have executed (partition 2, ids: 4,5)
        // No other batches after (and including) batch for partition 1 should have executed
        Assert.Equal(4, customersCount);
    }

    [Fact]
    public virtual async Task SaveChanges_transaction_behavior_always_fails_for_multiple_partitionkeys()
    {
        using var context = Fixture.CreateContext();
        Fixture.TransactionMode = AutoTransactionBehavior.Always; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        context.Customers.Add(new Customer { Id = "4", PartitionKey = "2" });
        context.Customers.Add(new Customer { Id = "3", PartitionKey = "1" });

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(CosmosStrings.SaveChangesAutoTransactionBehaviorAlwaysAtomicity, exception.Message);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, customersCount);
    }

    [Fact]
    public virtual async Task SaveChanges_succeeds_for_101_entities_in_same_partition()
    {
        using var context = Fixture.CreateContext();

        context.Customers.AddRange(Enumerable.Range(0, 101).Select(x => new Customer { Id = x.ToString(), PartitionKey = "1" }));

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(101, customersCount);
    }

    // Linux emulator enforces different transactional batch limits.
    [Fact(Skip = "Linux emulator enforces different transactional batch limits.")]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosTransactionalBatchTest.cs", 247, 262, Skip = true, Justification = "Linux emulator enforces different transactional batch limits.")]
    public virtual async Task SaveChanges_transaction_behavior_always_fails_for_101_entities_in_same_partition()
    {
        using var context = Fixture.CreateContext();
        Fixture.TransactionMode = AutoTransactionBehavior.Always; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        context.Customers.AddRange(Enumerable.Range(0, 101).Select(x => new Customer { Id = x.ToString(), PartitionKey = "1" }));

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(CosmosStrings.SaveChangesAutoTransactionBehaviorAlwaysAtomicity, exception.Message);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, customersCount);
    }

    [Fact]
    public virtual async Task SaveChanges_transaction_behavior_always_succeeds_for_100_entities_in_same_partition()
    {
        using var context = Fixture.CreateContext();
        Fixture.TransactionMode = AutoTransactionBehavior.Always; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        context.Customers.AddRange(Enumerable.Range(0, 100).Select(x => new Customer { Id = x.ToString(), PartitionKey = "1" }));

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(100, customersCount);
    }

    [Fact]
    public virtual async Task SaveChanges_transaction_behavior_always_fails_for_multiple_entities_with_triggers()
    {
        using var context = Fixture.CreateContext();
        Fixture.TransactionMode = AutoTransactionBehavior.Always; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        context.CustomersWithTrigger.Add(new CustomerWithTrigger { Id = "4", PartitionKey = "2" });
        context.CustomersWithTrigger.Add(new CustomerWithTrigger { Id = "3", PartitionKey = "1" });

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(CosmosStrings.SaveChangesAutoTransactionBehaviorAlwaysTriggerAtomicity, exception.Message);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.CustomersWithTrigger.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, customersCount);
    }

    // Triggers are not supported in the Linux emulator
    [Fact(Skip = "Triggers are not supported in the Linux emulator")]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosTransactionalBatchTest.cs", 296, 330, Skip = true, Justification = "Triggers are not supported in the Linux emulator")]
    public virtual Task SaveChanges_transaction_behavior_always_succeeds_for_single_entity_with_trigger()
        => throw Xunit.Sdk.SkipException.ForSkip("Triggers are not supported in the Linux emulator");


    [Fact]
    public virtual async Task SaveChanges_transaction_behavior_always_fails_for_single_entity_with_trigger_and_entity_without_trigger()
    {
        using var context = Fixture.CreateContext();
        Fixture.TransactionMode = AutoTransactionBehavior.Always; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        context.Customers.Add(new Customer { Id = "4", PartitionKey = "2" });
        context.CustomersWithTrigger.Add(new CustomerWithTrigger { Id = "4", PartitionKey = "2" });

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(CosmosStrings.SaveChangesAutoTransactionBehaviorAlwaysTriggerAtomicity, exception.Message);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.CustomersWithTrigger.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, customersCount);
    }

    [Fact]
    public virtual async Task SaveChanges_three_1mb_entries_succeeds()
    {
        using var context = Fixture.CreateContext();

        context.Customers.Add(
            new Customer
            {
                Id = "1",
                Name = new string('x', 1_000_000),
                PartitionKey = "1"
            });
        context.Customers.Add(
            new Customer
            {
                Id = "2",
                Name = new string('x', 1_000_000),
                PartitionKey = "1"
            });
        context.Customers.Add(
            new Customer
            {
                Id = "3",
                Name = new string('x', 1_000_000),
                PartitionKey = "1"
            });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, customersCount);
    }

    // Linux emulator times out instead of rejecting oversized requests.
    [Fact(Skip = "Linux emulator times out instead of rejecting oversized requests.")]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosTransactionalBatchTest.cs", 383, 405, Skip = true, Justification = "Linux emulator times out instead of rejecting oversized requests.")]
    public virtual async Task SaveChanges_entity_too_large_throws()
    {
        using var context = Fixture.CreateContext();

        context.Customers.Add(
            new Customer
            {
                Id = "1",
                Name = new string('x', 50_000_000),
                PartitionKey = "1"
            });

        var exception = await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(exception.InnerException);
        var cosmosException = Assert.IsAssignableFrom<CosmosException>(exception.InnerException);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, cosmosException.StatusCode);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, customersCount);
    }

    [Fact]
    public virtual async Task SaveChanges_too_large_entry_after_smaller_throws_after_saving_smaller()
    {
        // Permit this official oversized-document input to reach the actual provider.
        Fixture.PayloadLimits = new InfoCarrierPayloadLimits(128 * 1024 * 1024);
        using var context = Fixture.CreateContext();

        context.Customers.Add(
            new Customer
            {
                Id = "1",
                Name = new string('x', 1_000_000),
                PartitionKey = "1"
            });
        context.Customers.Add(
            new Customer
            {
                Id = "2",
                Name = new string('x', 50_000_000),
                PartitionKey = "1"
            });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, customersCount);
        Assert.Equal("1", (await assertContext.Customers.FirstAsync(cancellationToken: TestContext.Current.CancellationToken)).Id);
    }

    // Linux emulator times out instead of rejecting oversized transactional batches.
    [Fact(Skip = "Linux emulator times out instead of rejecting oversized transactional batches.")]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosTransactionalBatchTest.cs", 435, 465, Skip = true, Justification = "Linux emulator times out instead of rejecting oversized transactional batches.")]
    public virtual async Task SaveChanges_transaction_behavior_always_payload_larger_than_cosmos_limit_throws()
    {
        using var context = Fixture.CreateContext();
        Fixture.TransactionMode = AutoTransactionBehavior.Always; // Explicit backend deployment configuration.
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;

        context.Customers.Add(
            new Customer
            {
                Id = "1",
                Name = new string('x', 50_000_000 / 2),
                PartitionKey = "1"
            });
        context.Customers.Add(
            new Customer
            {
                Id = "2",
                Name = new string('x', 50_000_000 / 2),
                PartitionKey = "1"
            });

        var exception = await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(exception.InnerException);
        var cosmosException = Assert.IsAssignableFrom<CosmosException>(exception.InnerException);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, cosmosException.StatusCode);

        using var assertContext = Fixture.CreateContext();
        var customersCount = await assertContext.Customers.CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, customersCount);
    }

    /// <summary>
    ///     How many bytes of data can be in a customer's properties to reach the max request size in a EF transactional batch request
    /// </summary>
    private const int MaxSerializedCustomerTransactionalBatchRequestSize = 2094389;

    private const int MaxKeySize = 1023;

    // Linux emulator enforces different transactional batch size limits.
    [Theory(Skip = "Linux emulator enforces different transactional batch size limits."), InlineData(true), InlineData(false)]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/CosmosTransactionalBatchTest.cs", 474, 510, Skip = true, Justification = "Linux emulator enforces different transactional batch size limits.")]
    public virtual async Task SaveChanges_exactly_2_mib_does_not_split_and_one_byte_over_splits(bool oneByteOver)
    {
        using var context = Fixture.CreateContext();

        var customer1 = new Customer { Id = new string('x', MaxKeySize), PartitionKey = new string('x', MaxKeySize) };
        var customer2 = new Customer { Id = new string('y', MaxKeySize), PartitionKey = new string('x', MaxKeySize) };

        context.Customers.Add(customer1);
        context.Customers.Add(customer2);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Fixture.Logger.Clear();

        customer1.Name = new string('x', (MaxSerializedCustomerTransactionalBatchRequestSize / 2) - (2 * MaxKeySize));
        customer2.Name = new string('x', (MaxSerializedCustomerTransactionalBatchRequestSize / 2) - (2 * MaxKeySize));

        if (oneByteOver)
        {
            customer1.Name += 'x';
        }

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var assertContext = Fixture.CreateContext();
        Assert.Equal(2, (await assertContext.Customers.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Count);

        if (oneByteOver)
        {
            Assert.Equal(2, Fixture.Logger.Log.Count(x => x.Id == CosmosEventId.ExecutedTransactionalBatch));
        }
        else
        {
            Assert.Equal(1, Fixture.Logger.Log.Count(x => x.Id == CosmosEventId.ExecutedTransactionalBatch));
        }
    }

    private const int MaxSpecialCharsInId = MaxKeySize / 3;

    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/323
    [Theory, InlineData(true), InlineData(false)]
    public virtual async Task SaveChanges_update_id_contains_special_chars_which_makes_request_larger_than_2_mib_splits_into_2_batches(
        bool isIdSpecialChar)
    {
        using var context = Fixture.CreateContext();
        Fixture.Logger.Clear();

        var id1 = isIdSpecialChar ? new string('€', MaxSpecialCharsInId) : new string('x', MaxSpecialCharsInId);
        var id2 = isIdSpecialChar ? new string('Ω', MaxSpecialCharsInId) : new string('y', MaxSpecialCharsInId);

        var customer1 = new Customer
        {
            Id = id1,
            Name = new string('x', (MaxSerializedCustomerTransactionalBatchRequestSize / 2) - MaxKeySize - 1),
            PartitionKey = new string('€', MaxSpecialCharsInId)
        };
        var customer2 = new Customer
        {
            Id = id2,
            Name = new string('x', (MaxSerializedCustomerTransactionalBatchRequestSize / 2) - MaxKeySize - 1),
            PartitionKey = new string('€', MaxSpecialCharsInId)
        };

        context.Customers.Add(customer1);
        context.Customers.Add(customer2);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        // The create doesn't duplicate the id in the payload, so it should fit in one batch even with special chars
        Assert.Equal(1, Fixture.Logger.Log.Count(x => x.Id == CosmosEventId.ExecutedTransactionalBatch));

        Fixture.Logger.Clear();

        context.Update(customer1);
        context.Update(customer2);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var assertContext = Fixture.CreateContext();
        Assert.Equal(2, (await context.Customers.ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).Count);

        // The id being a special character should make the difference whether this fits in 1 batch.
        if (isIdSpecialChar)
        {
            Assert.Equal(2, Fixture.Logger.Log.Count(x => x.Id == CosmosEventId.ExecutedTransactionalBatch));
        }
        else
        {
            Assert.Equal(1, Fixture.Logger.Log.Count(x => x.Id == CosmosEventId.ExecutedTransactionalBatch));
        }
    }

    [Fact]
    public async Task SaveChanges_transaction_behavior_never_does_not_use_transactions()
    {
        TransactionalBatchContext CreateContext()
        {
            var context = Fixture.CreateContext();
            Fixture.TransactionMode = AutoTransactionBehavior.Never; // Explicit backend deployment configuration.
            context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
            return context;
        }

        var customers = new Customer[]
        {
            new()
            {
                Id = "42",
                Name = "Theon",
                PartitionKey = "1"
            },
            new()
            {
                Id = "43",
                Name = "Rob",
                PartitionKey = "1"
            }
        };

        using (var context = CreateContext())
        {
            Fixture.Logger.Clear();

            context.AddRange(customers);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var logEntries = Fixture.Logger.Log.Where(e => e.Id == CosmosEventId.ExecutedCreateItem).ToList();
            Assert.Equal(2, logEntries.Count);
            foreach (var logEntry in logEntries)
            {
                Assert.Equal(LogLevel.Information, logEntry.Level);
                Assert.Contains("CreateItem", logEntry.Message);
            }
        }

        using (var context = CreateContext())
        {
            Fixture.Logger.Clear();
            var customerFromStore1 = await context.Set<Customer>().FirstAsync(x => x.Id == "42", cancellationToken: TestContext.Current.CancellationToken);
            var customerFromStore2 = await context.Set<Customer>().LastAsync(x => x.Id == "43", cancellationToken: TestContext.Current.CancellationToken);

            customerFromStore1.Name += " Greyjoy";
            customerFromStore2.Name += " Stark";

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var logEntries = Fixture.Logger.Log.Where(e => e.Id == CosmosEventId.ExecutedReplaceItem).ToList();
            Assert.Equal(2, logEntries.Count);
            foreach (var logEntry in logEntries)
            {
                Assert.Equal(LogLevel.Information, logEntry.Level);
                Assert.Contains("ReplaceItem", logEntry.Message);
            }
        }

        using (var context = CreateContext())
        {
            Fixture.Logger.Clear();
            var customerFromStore1 = await context.Set<Customer>().FirstAsync(x => x.Id == "42", cancellationToken: TestContext.Current.CancellationToken);
            var customerFromStore2 = await context.Set<Customer>().LastAsync(x => x.Id == "43", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("42", customerFromStore1.Id);
            Assert.Equal("Theon Greyjoy", customerFromStore1.Name);

            Assert.Equal("43", customerFromStore2.Id);
            Assert.Equal("Rob Stark", customerFromStore2.Name);

            context.Remove(customerFromStore1);
            context.Remove(customerFromStore2);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var logEntries = Fixture.Logger.Log.Where(e => e.Id == CosmosEventId.ExecutedDeleteItem).ToList();
            Assert.Equal(2, logEntries.Count);
            foreach (var logEntry in logEntries)
            {
                Assert.Equal(LogLevel.Information, logEntry.Level);
                Assert.Contains("DeleteItem", logEntry.Message);
            }
        }

        using (var context = CreateContext())
        {
            Fixture.Logger.Clear();
            Assert.Empty(await context.Set<Customer>().ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    public ValueTask InitializeAsync() => Fixture.InitializeAsync();
    public ValueTask DisposeAsync() => Fixture.DisposeAsync();

    public class TransactionalBatchContext(DbContextOptions options) : PoolableDbContext(options)
    {
        public DbSet<Customer> Customers { get; set; } = null!;

        public DbSet<CustomerWithTrigger> CustomersWithTrigger { get; set; } = null!;

        public DbSet<Order> Orders { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Customer>(b =>
            {
                b.HasKey(c => c.Id);
                b.Property(c => c.ETag).IsETagConcurrency();
                b.OwnsMany(x => x.Children);
                b.HasPartitionKey(c => c.PartitionKey);
            });

            builder.Entity<CustomerWithTrigger>(b =>
            {
                b.HasKey(c => c.Id);
                b.Property(c => c.ETag).IsETagConcurrency();
                b.HasPartitionKey(c => c.PartitionKey);
                b.HasTrigger("trigger", TriggerType.Pre, TriggerOperation.All);
            });

            builder.Entity<Order>(b =>
            {
                b.HasKey(c => c.Id);
                b.HasPartitionKey(c => c.PartitionKey);
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

    public class CustomerWithTrigger
    {
        public string? Id { get; set; }

        public string? ETag { get; set; }

        public string? PartitionKey { get; set; }
    }

    public class DummyChild
    {
        public string? Id { get; init; }
    }

    public class Order
    {
        public string? Id { get; set; }

        public string? PartitionKey { get; set; }
    }
}
