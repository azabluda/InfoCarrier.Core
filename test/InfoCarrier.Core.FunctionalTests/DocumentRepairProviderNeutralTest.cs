// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests;

public class DocumentRepairProviderNeutralTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Repair_preserves_context_filters_transactions_and_unsent_values(bool sqlite, bool async)
    {
        string name = "document-repair-" + Guid.NewGuid().ToString("N");
        string path = Path.Combine(AppContext.BaseDirectory, name + ".db");
        var services = new ServiceCollection();
        services.AddDbContext<RepairContext>(options =>
        {
            if (sqlite) options.UseSqlite("Data Source=" + path + ";Pooling=False");
            else options.UseInMemoryDatabase(name);
        });
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<RepairContext>());
        await using ServiceProvider provider = services.BuildServiceProvider();
        try
        {
            await using (AsyncServiceScope scope = provider.CreateAsyncScope())
            {
                RepairContext seed = scope.ServiceProvider.GetRequiredService<RepairContext>();
                await seed.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
                seed.Add(new Root { Id = "one", Name = "Before", Tenant = "blue", Detail = new() { City = "Berlin" },
                    Lines = [new() { Id = 1, Text = "First" }, new() { Id = 2, Text = "Second" }] });
                seed.Add(new Root { Id = "hidden", Name = "Hidden", Tenant = "red", Detail = new() { City = "Paris" } });
                await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            var serializer = new SystemTextJsonInfoCarrierSerializer();
            await using var server = new InProcessInfoCarrierServer(provider,
                new InfoCarrierServerOptions().WithStoresOwnedDataInsideOwner(true));
            var envelope = new InfoCarrierEnvelopeServer(server, serializer);
            var transport = new TestUtilities.InProcessInfoCarrierTransport(envelope.Dispatch, envelope.DispatchAsync, serializer);
            var client = new TransportInfoCarrierClient(transport, serializer);
            await using var write = new RepairContext(new DbContextOptionsBuilder<RepairContext>().UseInfoCarrier(client).Options);
            await using IDbContextTransaction? transaction = sqlite
                ? async ? await write.Database.BeginTransactionAsync(TestContext.Current.CancellationToken) : write.Database.BeginTransaction()
                : null;
            var stub = new Root { Id = "one", Name = "After" };
            write.Attach(stub);
            write.Entry(stub).Property(r => r.Name).IsModified = true;
            if (async) await write.SaveChangesAsync(TestContext.Current.CancellationToken);
            else write.SaveChanges();
            if (transaction is not null)
            {
                if (async) await transaction.CommitAsync(TestContext.Current.CancellationToken);
                else transaction.Commit();
            }
            await using AsyncServiceScope readScope = provider.CreateAsyncScope();
            RepairContext read = readScope.ServiceProvider.GetRequiredService<RepairContext>();
            Root actual = await read.Set<Root>().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("After", actual.Name);
            Assert.Equal("blue", actual.Tenant);
            Assert.Equal("Berlin", actual.Detail.City);
            Assert.Equal(["First", "Second"], actual.Lines.OrderBy(l => l.Id).Select(l => l.Text));
        }
        finally
        {
            await using AsyncServiceScope cleanup = provider.CreateAsyncScope();
            await cleanup.ServiceProvider.GetRequiredService<RepairContext>().Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
    }

    public class RepairContext(DbContextOptions<RepairContext> options) : DbContext(options)
    {
        public string Tenant { get; } = "blue";
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Root>().HasQueryFilter(r => r.Tenant == Tenant);
            modelBuilder.Entity<Root>().OwnsOne(r => r.Detail);
            modelBuilder.Entity<Root>().OwnsMany(r => r.Lines, owned => owned.HasKey("RootId", nameof(Line.Id)));
        }
    }
    public class Root
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Tenant { get; set; } = "";
        public Detail Detail { get; set; } = null!;
        public List<Line> Lines { get; set; } = [];
    }
    public class Detail { public string City { get; set; } = ""; }
    public class Line { public int Id { get; set; } public string Text { get; set; } = ""; }
}
