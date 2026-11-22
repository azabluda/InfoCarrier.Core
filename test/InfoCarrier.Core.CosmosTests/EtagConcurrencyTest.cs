// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned EF Core 11 CosmosConcurrencyTest.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications;

public class EtagConcurrencyTest : IAsyncLifetime
{
    private const string DatabaseName = "CosmosConcurrencyTest";

    private TestStore? _store;
    private DbContextOptions? _options;
    private bool _directStore;
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public async ValueTask DisposeAsync()
    {
        if (_store is not null) await _store.DisposeAsync();
    }

    private async Task<DbContextOptions> CreateOptionsAsync(bool? contentResponseOnWriteEnabled)
    {
        _store = InfoCarrierTestStoreFactory.Create(new CosmosTier(), typeof(ConcurrencyContext), null,
            onAddOptions: builder =>
            {
                if (contentResponseOnWriteEnabled is { } enabled)
                {
#pragma warning disable CS0618 // Exercise all three settings from the official provider test.
                    builder.UseCosmos(options => options.ContentResponseOnWriteEnabled(enabled));
#pragma warning restore CS0618
                }
                return ConfigureServerOptions(builder);
            }).Create(DatabaseName);
        _options = _store.AddProviderOptions(new DbContextOptionsBuilder()).Options;
        await _store.InitializeAsync(null, () => new ConcurrencyContext(_options));
        return _options;
    }

    [Fact]
    public virtual Task Adding_the_same_entity_twice_results_in_DbUpdateException()
        => ConcurrencyTestAsync<DbUpdateException>(ctx =>
        {
            ctx.Customers.Add(
                new Customer
                {
                    Id = "1",
                    Name = "CreatedTwice",
                });
            return Task.CompletedTask;
        });

    [Fact]
    public virtual Task Updating_then_deleting_the_same_entity_results_in_DbUpdateConcurrencyException()
        => ConcurrencyTestAsync<DbUpdateConcurrencyException>(
            ctx =>
            {
                ctx.Customers.Add(
                    new Customer
                    {
                        Id = "2",
                        Name = "Added",
                    });
                return Task.CompletedTask;
            }, async ctx => (await ctx.Customers.SingleAsync(c => c.Id == "2")).Name = "Updated",
            async ctx => ctx.Customers.Remove(await ctx.Customers.SingleAsync(c => c.Id == "2")));

    [Fact]
    public virtual Task Updating_then_updating_the_same_entity_results_in_DbUpdateConcurrencyException()
        => ConcurrencyTestAsync<DbUpdateConcurrencyException>(
            ctx =>
            {
                ctx.Customers.Add(
                    new Customer
                    {
                        Id = "3",
                        Name = "Added",
                    });
                return Task.CompletedTask;
            }, async ctx => (await ctx.Customers.SingleAsync(c => c.Id == "3")).Name = "Updated",
            async ctx => (await ctx.Customers.SingleAsync(c => c.Id == "3")).Name = "Updated");

    [Theory, InlineData(null), InlineData(true), InlineData(false)]
    public async Task Etag_is_updated_in_entity_after_SaveChanges(bool? contentResponseOnWriteEnabled)
    {
        var options = await CreateOptionsAsync(contentResponseOnWriteEnabled);

        var customer = new Customer
        {
            Id = "5",
            Name = "Theon",
            Children = { new DummyChild { Id = "0" } }
        };

        string? etag = null;
        await using (var context = CreateContext(options))
        {
            await _store!.CleanAsync(context);

            await context.AddAsync(customer, TestContext.Current.CancellationToken);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            etag = customer.ETag;
        }

        await using (var context = CreateContext(options))
        {
            var customerFromStore = await context.Set<Customer>().SingleAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(customerFromStore.ETag);
            Assert.Equal(etag.Trim('"'), customerFromStore.ETag.Trim('"'));

            customerFromStore.Children.Add(new DummyChild { Id = "1" });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(customerFromStore.ETag);
            Assert.NotEqual(etag.Trim('"'), customerFromStore.ETag.Trim('"'));

            customerFromStore.Children.Add(new DummyChild { Id = "2" });

            Assert.NotEmpty(customerFromStore.ETag);
            Assert.NotEqual(etag.Trim('"'), customerFromStore.ETag.Trim('"'));

            customerFromStore.Children.Add(new DummyChild { Id = "3" });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(customerFromStore.ETag);
            Assert.NotEqual(etag.Trim('"'), customerFromStore.ETag.Trim('"'));
        }
    }

    [Theory, InlineData(null), InlineData(true), InlineData(false)]
    public async Task Etag_is_updated_in_derived_entity_after_SaveChanges(bool? contentResponseOnWriteEnabled)
    {
        var options = await CreateOptionsAsync(contentResponseOnWriteEnabled);

        var customer = new PremiumCustomer
        {
            Id = "5",
            Name = "Theon",
            LoyaltyLevel = "Bronze",
            Children = { new DummyChild { Id = "0" } }
        };

        string? etag = null;
        await using (var context = CreateContext(options))
        {
            await _store!.CleanAsync(context);

            await context.AddAsync(customer, TestContext.Current.CancellationToken);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            etag = customer.ETag;
        }

        await using (var context = CreateContext(options))
        {
            var customerFromStore = await context.Set<PremiumCustomer>().SingleAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(customerFromStore.ETag);
            Assert.Equal(etag.Trim('"'), customerFromStore.ETag.Trim('"'));

            customerFromStore.Children.Add(new DummyChild { Id = "1" });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(customerFromStore.ETag);
            Assert.NotEqual(etag.Trim('"'), customerFromStore.ETag.Trim('"'));

            customerFromStore.Children.Add(new DummyChild { Id = "2" });

            Assert.NotEmpty(customerFromStore.ETag);
            Assert.NotEqual(etag.Trim('"'), customerFromStore.ETag.Trim('"'));

            customerFromStore.Children.Add(new DummyChild { Id = "3" });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(customerFromStore.ETag);
            Assert.NotEqual(etag.Trim('"'), customerFromStore.ETag.Trim('"'));
        }
    }

    /// <summary>
    ///     Runs the two actions with two different contexts and calling
    ///     SaveChanges such that storeChange will succeed and the store will reflect this change, and
    ///     then clientChange will result in a concurrency exception.
    ///     After the exception is caught the resolver action is called, after which SaveChanges is called
    ///     again. Finally, a new context is created and the validator is called so that the state of
    ///     the database at the end of the process can be validated.
    /// </summary>
    protected virtual Task ConcurrencyTestAsync<TException>(
        Func<ConcurrencyContext, Task> change)
        where TException : DbUpdateException
        => ConcurrencyTestAsync<TException>(
            null, change, change);

    /// <summary>
    ///     Runs the two actions with two different contexts and calling
    ///     SaveChanges such that storeChange will succeed and the store will reflect this change, and
    ///     then clientChange will result in a concurrency exception.
    ///     After the exception is caught the resolver action is called, after which SaveChanges is called
    ///     again. Finally, a new context is created and the validator is called so that the state of
    ///     the database at the end of the process can be validated.
    /// </summary>
    protected virtual async Task ConcurrencyTestAsync<TException>(
        Func<ConcurrencyContext, Task>? seedAction,
        Func<ConcurrencyContext, Task> storeChange,
        Func<ConcurrencyContext, Task> clientChange)
        where TException : DbUpdateException
    {
        await CreateOptionsAsync(null);
        using var outerContext = CreateContext();
        await _store!.CleanAsync(outerContext);

        if (seedAction != null)
        {
            await seedAction(outerContext);
        }

        await outerContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (clientChange != null)
        {
            await clientChange(outerContext);
        }

        using (var innerContext = CreateContext())
        {
            if (storeChange != null)
            {
                await storeChange(innerContext);
            }

            await innerContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var updateException =
            await Assert.ThrowsAnyAsync<TException>(() => outerContext.SaveChangesAsync(TestContext.Current.CancellationToken));

        var entry = updateException.Entries.Single();
        Assert.IsAssignableFrom<Customer>(entry.Entity);
    }

    protected virtual DbContextOptionsBuilder ConfigureServerOptions(DbContextOptionsBuilder builder) => builder;

    protected virtual ConcurrencyContext CreateContext()
        => CreateContext(_options!);

    protected virtual ConcurrencyContext CreateContext(DbContextOptions options)
        => _directStore
            ? (ConcurrencyContext)((IInfoCarrierClientTestStore)_store!).Backend.CreateDbContext()
            : new(options);

    [Theory, InlineData(null), InlineData(true), InlineData(false)]
    public async Task Direct_store_updates_root_etag_after_nested_changes(bool? contentResponseOnWriteEnabled)
    {
        _directStore = true;
        await Etag_is_updated_in_entity_after_SaveChanges(contentResponseOnWriteEnabled);
    }

    [Theory, InlineData(null), InlineData(true), InlineData(false)]
    public async Task Direct_store_updates_derived_etag_after_nested_changes(bool? contentResponseOnWriteEnabled)
    {
        _directStore = true;
        await Etag_is_updated_in_derived_entity_after_SaveChanges(contentResponseOnWriteEnabled);
    }


    [Theory]
    [InlineData(false, null), InlineData(false, true), InlineData(false, false)]
    [InlineData(true, null), InlineData(true, true), InlineData(true, false)]
    public async Task Deferred_acceptance_preserves_generated_owner_values(bool direct, bool? contentResponseOnWriteEnabled)
    {
        _directStore = direct;
        var options = await CreateOptionsAsync(contentResponseOnWriteEnabled);
        await using var context = CreateContext(options);
        var customer = new Customer { Id = "deferred", Name = "Before", Children = { new DummyChild { Id = "first" } } };
        context.Add(customer);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        string original = customer.ETag;
        customer.Children.Add(new DummyChild { Id = "second" });
        await context.SaveChangesAsync(false, TestContext.Current.CancellationToken);
        string generated = context.Entry(customer).Property(c => c.ETag).CurrentValue;
        Assert.NotEqual(original, generated);
        context.ChangeTracker.AcceptAllChanges();
        Assert.Equal(generated, customer.ETag);
        Assert.Equal(generated, context.Entry(customer).Property(c => c.ETag).OriginalValue);
        Assert.False(context.ChangeTracker.HasChanges());
        customer.Children.Add(new DummyChild { Id = "third" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var backend = (ConcurrencyContext)((IInfoCarrierClientTestStore)_store!).Backend.CreateDbContext();
        var stored = await backend.Customers.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(customer.ETag.Trim('"'), stored.ETag.Trim('"'));
        Assert.Equal(new[] { "first", "second", "third" }, stored.Children.Select(c => c.Id).Order());
    }

    public class ConcurrencyContext(DbContextOptions options) : PoolableDbContext(options)
    {
        public DbSet<Customer> Customers
            => Set<Customer>();

        public DbSet<PremiumCustomer> PremiumCustomers
            => Set<PremiumCustomer>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<Customer>(b =>
            {
                b.HasKey(c => c.Id);
                b.Property(c => c.ETag).IsETagConcurrency();
                b.OwnsMany(x => x.Children);
                b.HasPartitionKey(c => c.Id);
            });

            builder.Entity<PremiumCustomer>().HasBaseType<Customer>();
        }
    }

    public class Customer
    {
        public string Id { get; set; } = null!;

        public string Name { get; set; } = null!;

        public string ETag { get; set; } = null!;

        public ICollection<DummyChild> Children { get; } = new HashSet<DummyChild>();
    }

    public class DummyChild
    {
        public string Id { get; init; } = null!;
    }

    public class PremiumCustomer : Customer
    {
        public string LoyaltyLevel { get; set; } = null!;
    }
}
