// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned EF Core 11 HierarchicalPartitionKeyTest.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications;

// TODO: Consider removing these in favor of ReadItemPartitionKeyQueryTest
public class HierarchicalPartitionKeyTest : IClassFixture<HierarchicalPartitionKeyTest.CosmosHierarchicalPartitionKeyFixture>
{
    private const string DatabaseName = nameof(HierarchicalPartitionKeyTest);





    protected CosmosHierarchicalPartitionKeyFixture Fixture { get; }

    public HierarchicalPartitionKeyTest(CosmosHierarchicalPartitionKeyFixture fixture)
    {
        Fixture = fixture;

    }

    [Fact]
    public virtual async Task Can_add_update_delete_end_to_end_with_partition_key()
    {

        await PartitionKeyTestAsync(
            ctx => ctx.Customers.OrderBy(c => c.PartitionKey1).FirstAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.OrderBy(c => c.PartitionKey1).Skip(1).FirstAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.OrderBy(c => c.PartitionKey1).LastAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.OrderBy(c => c.PartitionKey1).ToListAsync(TestContext.Current.CancellationToken),
            2);
    }

    [Fact]
    public virtual async Task Can_add_update_delete_end_to_end_with_with_partition_key_extension()
    {

        await PartitionKeyTestAsync(
            ctx => ctx.Customers.WithPartitionKey("A", 1.1, true).SingleAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.WithPartitionKey("B", 2.1, false).SingleAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.WithPartitionKey("B", 2.1, false).LastAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.WithPartitionKey("B", 2.1, false).ToListAsync(TestContext.Current.CancellationToken),
            1);
    }

    [Fact]
    public async Task Can_query_with_implicit_partition_key_filter()
    {

        await PartitionKeyTestAsync(
            ctx => ctx.Customers
                .Where(b => (b.Id == 42 || b.Name == "John Snow")
                    && b.PartitionKey1 == "A"
                    && b.PartitionKey2 == 1.1
                    && b.PartitionKey3)
                .SingleAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers
                .Where(b => (b.Id == 42 || b.Name == "John Snow")
                    && b.PartitionKey1 == "B"
                    && b.PartitionKey2 == 2.1
                    && !b.PartitionKey3)
                .SingleAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers.WithPartitionKey("B", 2.1, false).LastAsync(TestContext.Current.CancellationToken),
            ctx => ctx.Customers
                .Where(b => b.Id == 42
                    && ((b.PartitionKey1 == "A" && b.PartitionKey2 == 1.1 && b.PartitionKey3)
                        || (b.PartitionKey1 == "B" && b.PartitionKey2 == 2.1 && !b.PartitionKey3)))
                .ToListAsync(TestContext.Current.CancellationToken),
            2);
    }

    protected virtual async Task PartitionKeyTestAsync(
        Func<HierarchicalPartitionKeyContext, Task<Customer>> read1SingleTask,
        Func<HierarchicalPartitionKeyContext, Task<Customer>> read2SingleTask,
        Func<HierarchicalPartitionKeyContext, Task<Customer>> readLastTask,
        Func<HierarchicalPartitionKeyContext, Task<List<Customer>>> readListTask,
        int listCount)
    {
        var customer1 = new Customer
        {
            Id = 42,
            Name = "Theon",
            PartitionKey1 = "A",
            PartitionKey2 = 1.1,
            PartitionKey3 = true,
        };

        var customer2 = new Customer
        {
            Id = 42,
            Name = "Theon Twin",
            PartitionKey1 = "B",
            PartitionKey2 = 2.1,
            PartitionKey3 = false,
        };

        await using (var innerContext = CreateContext())
        {


            await innerContext.AddAsync(customer1, TestContext.Current.CancellationToken);
            await innerContext.AddAsync(customer2, TestContext.Current.CancellationToken);
            await innerContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Read & update in first partition
        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await read1SingleTask(innerContext);



            Assert.Equal(42, customerFromStore.Id);
            Assert.Equal("Theon", customerFromStore.Name);
            Assert.Equal("A", customerFromStore.PartitionKey1);
            Assert.Equal(1.1, customerFromStore.PartitionKey2);
            Assert.True(customerFromStore.PartitionKey3);

            customerFromStore.Name = "Theon Greyjoy";

            await innerContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Read & update in second partition
        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await read2SingleTask(innerContext);



            Assert.Equal(42, customerFromStore.Id);
            Assert.Equal("Theon Twin", customerFromStore.Name);
            Assert.Equal("B", customerFromStore.PartitionKey1);
            Assert.Equal(2.1, customerFromStore.PartitionKey2);
            Assert.False(customerFromStore.PartitionKey3);

            customerFromStore.Name = "Theon Bluejoy";

            await innerContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Read list from all partitions
        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await readListTask(innerContext);

            Assert.Equal(listCount, customerFromStore.Count);
        }

        // Test exceptions
        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await read1SingleTask(innerContext);
            customerFromStore.PartitionKey1 = "C";

            Assert.Equal(
                CoreStrings.KeyReadOnly(nameof(Customer.PartitionKey1), nameof(Customer)),
                Assert.Throws<InvalidOperationException>(() => innerContext.SaveChanges()).Message);
        }

        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await read1SingleTask(innerContext);
            customerFromStore.PartitionKey2 = 2.1;

            Assert.Equal(
                CoreStrings.KeyReadOnly(nameof(Customer.PartitionKey2), nameof(Customer)),
                Assert.Throws<InvalidOperationException>(() => innerContext.SaveChanges()).Message);
        }

        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await read1SingleTask(innerContext);
            customerFromStore.PartitionKey3 = false;

            Assert.Equal(
                CoreStrings.KeyReadOnly(nameof(Customer.PartitionKey3), nameof(Customer)),
                Assert.Throws<InvalidOperationException>(() => innerContext.SaveChanges()).Message);
        }

        // Read update & delete
        await using (var innerContext = CreateContext())
        {
            var customerFromStore = await read1SingleTask(innerContext);

            Assert.Equal(42, customerFromStore.Id);
            Assert.Equal("Theon Greyjoy", customerFromStore.Name);
            Assert.Equal("A", customerFromStore.PartitionKey1);
            Assert.Equal(1.1, customerFromStore.PartitionKey2);
            Assert.True(customerFromStore.PartitionKey3);

            innerContext.Remove(customerFromStore);

            var lastTask = await readLastTask(innerContext);

            innerContext.Remove(lastTask);

            await innerContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var innerContext = CreateContext())
        {
            Assert.Empty(await readListTask(innerContext));
        }
    }

    protected HierarchicalPartitionKeyContext CreateContext()
        => Fixture.CreateContext();

    public class CosmosHierarchicalPartitionKeyFixture : SharedStoreFixtureBase<HierarchicalPartitionKeyContext>
    {
        protected override string StoreName
            => DatabaseName;

        protected override bool UsePooling
            => false;

        private ITestStoreFactory? _factory;
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, null, allowedTypes: [typeof(CosmosQueryableExtensions)]);


    }

    public class HierarchicalPartitionKeyContext(DbContextOptions dbContextOptions) : DbContext(dbContextOptions)
    {
        public virtual DbSet<Customer> Customers
            => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Customer>(cb =>
            {
                cb.HasPartitionKey(c => new
                {
                    c.PartitionKey1,
                    c.PartitionKey2,
                    c.PartitionKey3
                });
                cb.HasKey(c => new
                {
                    c.Id,
                    c.PartitionKey1,
                    c.PartitionKey2,
                    c.PartitionKey3
                });
            });
    }

    public class Customer
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? PartitionKey1 { get; set; }
        public double PartitionKey2 { get; set; }
        public bool PartitionKey3 { get; set; }
    }
}

