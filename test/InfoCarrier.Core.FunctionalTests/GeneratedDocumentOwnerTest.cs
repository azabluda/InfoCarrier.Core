// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests;

public class GeneratedDocumentOwnerTest
{
    [Theory]
    [InlineData(false, false), InlineData(false, true), InlineData(true, false), InlineData(true, true)]
    public async Task Nested_changes_accept_generated_owner_values(bool async, bool deferAcceptance)
    {
        string database = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<RevisionContext>(b => b.UseInMemoryDatabase(database));
        services.AddScoped<DbContext>(p => p.GetRequiredService<RevisionContext>());
        await using var provider = services.BuildServiceProvider();
        await using var server = new InProcessInfoCarrierServer(provider,
            new InfoCarrierServerOptions().WithStoresOwnedDataInsideOwner(true));
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var envelope = new InfoCarrierEnvelopeServer(server, serializer);
        var transport = new TestUtilities.InProcessInfoCarrierTransport(envelope.Dispatch, envelope.DispatchAsync, serializer);
        var client = new TransportInfoCarrierClient(transport, serializer);
        var options = new DbContextOptionsBuilder<RevisionContext>().UseInfoCarrier(InfoCarrierClient.Configure(client, o => o.ServerUsesRelationalQueryRules = false)).Options;
        await using var context = new RevisionContext(options);
        var root = new Root { Id = "owner", Children = [new() { Id = "one" }] };
        context.Add(root);
        await Save(true);
        int original = root.Revision;
        root.Children.Add(new() { Id = "two" });
        await Save(!deferAcceptance);
        int generated = context.Entry(root).Property(r => r.Revision).CurrentValue;
        Assert.True(generated > original);
        if (deferAcceptance) context.ChangeTracker.AcceptAllChanges();
        Assert.Equal(generated, root.Revision);
        Assert.Equal(generated, context.Entry(root).Property(r => r.Revision).OriginalValue);
        Assert.False(context.ChangeTracker.HasChanges());
        root.Children.Add(new() { Id = "three" });
        await Save(true);
        await using var scope = provider.CreateAsyncScope();
        var read = scope.ServiceProvider.GetRequiredService<RevisionContext>();
        var actual = await read.Set<Root>().Include(r => r.Children).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(root.Revision, actual.Revision);
        Assert.Equal(new[] { "one", "three", "two" }, actual.Children.Select(c => c.Id).Order());

        async Task Save(bool accept)
        {
            if (async) await context.SaveChangesAsync(accept, TestContext.Current.CancellationToken);
            else context.SaveChanges(accept);
        }
    }

    public class RevisionContext(DbContextOptions<RevisionContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Root>().Property(r => r.Revision).ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<Root>().OwnsMany(r => r.Children, b => b.HasKey("RootId", nameof(Child.Id)));
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            GenerateRevision();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            GenerateRevision();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void GenerateRevision()
        {
            if (!Database.IsInMemory()) return;
            ChangeTracker.DetectChanges();
            var changedOwners = ChangeTracker.Entries<Child>()
                .Where(e => e.State != EntityState.Unchanged).Select(e => e.Property<string>("RootId").CurrentValue).ToHashSet();
            foreach (var entry in ChangeTracker.Entries<Root>())
            {
                if (entry.State == EntityState.Added || changedOwners.Contains(entry.Entity.Id))
                    entry.Entity.Revision++;
            }
        }
    }

    public class Root
    {
        public string Id { get; set; } = "";
        public int Revision { get; set; }
        public List<Child> Children { get; set; } = [];
    }
    public class Child { public string Id { get; set; } = ""; }
}
