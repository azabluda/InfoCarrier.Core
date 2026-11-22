// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned EF Core 11 PartitionKeyTest.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications;

// TODO: Consider removing these in favor of ReadItemPartitionKeyQueryTest
public class PartitionKeyTest : IClassFixture<PartitionKeyTest.CosmosPartitionKeyFixture>
{
    private const string DatabaseName = nameof(PartitionKeyTest);





    protected CosmosPartitionKeyFixture Fixture { get; }

    public PartitionKeyTest(CosmosPartitionKeyFixture fixture)
    {
        Fixture = fixture;

    }

    [Fact]
    public virtual async Task Can_add_update_delete_end_to_end_with_partition_key()
    {

        await PartitionKeyTestAsync(
            ctx => ctx.Customers.OrderBy(c => c.PartitionKey).FirstAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.OrderBy(c => c.PartitionKey).LastAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.OrderBy(c => c.PartitionKey).ToListAsync(TestContext.Current.CancellationToken),
            2);
    }

    [Fact]
    public virtual async Task Can_add_update_delete_end_to_end_with_with_partition_key_extension()
    {

        await PartitionKeyTestAsync(
            ctx => ctx.Customers.WithPartitionKey("1").SingleAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.WithPartitionKey("2").LastAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.WithPartitionKey("2").ToListAsync(TestContext.Current.CancellationToken),
            1);
    }

    [Fact]
    public async Task Can_query_with_implicit_partition_key_filter()
    {

        await PartitionKeyTestAsync(
            ctx => ctx.Customers
                .Where(b => (b.Id == 42 || b.Name == "John Snow") && b.PartitionKey == 1)
                .FirstAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.WithPartitionKey("2").LastAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers
                .Where(b => b.Id == 42 && (b.PartitionKey == 1 || b.PartitionKey == 2))
                .ToListAsync(TestContext.Current.CancellationToken),
            2);
    }

    protected virtual async Task PartitionKeyTestAsync(
        Func<PartitionKeyContext, Task<Customer>> readSingleTask,
        Func<PartitionKeyContext, Task<Customer>> readLastTask,
        Func<PartitionKeyContext, Task<List<Customer>>> readListTask,
        int listCount)
    {
        var customer1 = new Customer
        {
            Id = 42,
            Name = "Theon",
            PartitionKey = 1
        };

        var customer2 = new Customer
        {
            Id = 42,
            Name = "Theon Twin",
            PartitionKey = 2
        };

        await using (var innerContext = CreateContext())
        {


            await innerContext.AddAsync(customer1, TestContext.Current.CancellationToken);
            await innerContext.AddAsync(customer2, TestContext.Current.CancellationToken);
            await innerContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Read & update
        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await readSingleTask(innerContext);


            Assert.Equal(42, customerFromStore.Id);
            Assert.Equal("Theon", customerFromStore.Name);
            Assert.Equal(1, customerFromStore.PartitionKey);

            customerFromStore.Name = "Theon Greyjoy";

            await innerContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Read list
        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await readListTask(innerContext);

            Assert.Equal(listCount, customerFromStore.Count);
        }

        // Test exception
        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await readSingleTask(innerContext);
            customerFromStore.PartitionKey = 2;

            Assert.Equal(
                CoreStrings.KeyReadOnly(nameof(Customer.PartitionKey), nameof(Customer)),
                Assert.Throws<InvalidOperationException>(() => innerContext.SaveChanges()).Message);
        }

        // Read update & delete
        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await readSingleTask(innerContext);

            Assert.Equal(42, customerFromStore.Id);
            Assert.Equal("Theon Greyjoy", customerFromStore.Name);
            Assert.Equal(1, customerFromStore.PartitionKey);

            innerContext.Remove(customerFromStore);

            innerContext.Remove(await readLastTask(innerContext));

            await innerContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var innerContext = CreateContext())
        {
            Assert.Empty(await readListTask(innerContext));
        }
    }

    protected PartitionKeyContext CreateContext()
        => Fixture.CreateContext();

    public class CosmosPartitionKeyFixture : SharedStoreFixtureBase<PartitionKeyContext>
    {
        protected override string StoreName
            => DatabaseName;

        protected override bool UsePooling
            => false;

        private ITestStoreFactory? _factory;
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, null, allowedTypes: [typeof(CosmosQueryableExtensions)]);


    }

    public class PartitionKeyContext(DbContextOptions dbContextOptions) : DbContext(dbContextOptions)
    {
        public virtual DbSet<Customer> Customers
            => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Customer>(cb =>
            {
                cb.HasPartitionKey(c => c.PartitionKey);
                cb.Property(c => c.PartitionKey).HasConversion<string>();
                cb.HasKey(c => new { c.Id, c.PartitionKey });
            });
    }

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int PartitionKey { get; set; }
    }
}
