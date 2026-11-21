// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests;

#pragma warning disable EF1001 // Simulate store-generated key changes with the framework update-entry API.

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

    [Theory]
    [InlineData(false, false), InlineData(false, true), InlineData(true, false), InlineData(true, true)]
    public async Task Generated_keys_can_exchange_existing_values(bool async, bool deferAcceptance)
    {
        string database = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<RekeyContext>(b => b.UseInMemoryDatabase(database).ReplaceService<Microsoft.EntityFrameworkCore.Storage.IDatabase, RekeyDatabase>());
        services.AddScoped<DbContext>(p => p.GetRequiredService<RekeyContext>());
        await using var provider = services.BuildServiceProvider();
        await using var server = new InProcessInfoCarrierServer(provider,
            new InfoCarrierServerOptions().WithStoresOwnedDataInsideOwner(true));
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var envelope = new InfoCarrierEnvelopeServer(server, serializer);
        var client = new TransportInfoCarrierClient(new TestUtilities.InProcessInfoCarrierTransport(
            envelope.Dispatch, envelope.DispatchAsync, serializer), serializer);
        var options = new DbContextOptionsBuilder<RekeyContext>().UseInfoCarrier(
            InfoCarrierClient.Configure(client, o => o.ServerUsesRelationalQueryRules = false)).Options;
        await using var context = new RekeyContext(options);
        var root = new RekeyRoot { Id = "owner", Children = [new() { Id = 1, Text = "first" }, new() { Id = 2, Text = "second" }] };
        context.Add(root);
        if (async) await context.SaveChangesAsync(!deferAcceptance, TestContext.Current.CancellationToken);
        else context.SaveChanges(!deferAcceptance);
        Assert.Equal(2, context.Entry(root.Children[0]).Property(c => c.Id).CurrentValue);
        Assert.Equal(1, context.Entry(root.Children[1]).Property(c => c.Id).CurrentValue);
        if (deferAcceptance) context.ChangeTracker.AcceptAllChanges();
        Assert.Equal(2, root.Children[0].Id);
        Assert.Equal(1, root.Children[1].Id);
        Assert.False(context.ChangeTracker.HasChanges());
        await using var scope = provider.CreateAsyncScope();
        var backend = scope.ServiceProvider.GetRequiredService<RekeyContext>();
        var actual = await backend.Set<RekeyRoot>().Include(r => r.Children).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("second", actual.Children.Single(c => c.Id == 1).Text);
        Assert.Equal("first", actual.Children.Single(c => c.Id == 2).Text);
    }

    public class RekeyContext(DbContextOptions<RekeyContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RekeyRoot>().OwnsMany(r => r.Children, b =>
            {
                b.HasKey("RekeyRootId", nameof(RekeyChild.Id));
                b.Property(c => c.Id).ValueGeneratedOnAdd();
            });
    }
    public class RekeyDatabase(
        Microsoft.EntityFrameworkCore.Storage.DatabaseDependencies dependencies,
        Microsoft.EntityFrameworkCore.InMemory.Storage.Internal.IInMemoryStoreProvider storeProvider,
        Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel designTimeModel,
        Microsoft.EntityFrameworkCore.Update.IUpdateAdapterFactory updateAdapterFactory,
        Microsoft.EntityFrameworkCore.Diagnostics.IDiagnosticsLogger<DbLoggerCategory.Update> updateLogger)
        : Microsoft.EntityFrameworkCore.InMemory.Storage.Internal.InMemoryDatabase(
            dependencies, storeProvider, designTimeModel, updateAdapterFactory, updateLogger)
    {
        public override int SaveChanges(IList<Microsoft.EntityFrameworkCore.Update.IUpdateEntry> entries)
        {
            GenerateKeys(entries);
            return base.SaveChanges(entries);
        }
        public override Task<int> SaveChangesAsync(IList<Microsoft.EntityFrameworkCore.Update.IUpdateEntry> entries,
            CancellationToken cancellationToken = default)
        {
            GenerateKeys(entries);
            return base.SaveChangesAsync(entries, cancellationToken);
        }
        private static void GenerateKeys(IList<Microsoft.EntityFrameworkCore.Update.IUpdateEntry> entries)
        {
            var children = entries.Where(e => e.EntityType.ClrType == typeof(RekeyChild) && e.EntityState == EntityState.Added).ToList();
            var keys = children.Select(e => 3 - (int)e.GetCurrentValue(e.EntityType.FindProperty("Id")!)!).ToList();
            for (int i = 0; i < children.Count; i++)
                children[i].ToEntityEntry().GetInfrastructure().SetTemporaryValue(children[i].EntityType.FindProperty("Id")!, -100 - i, setModified: false);
            for (int i = 0; i < children.Count; i++)
                children[i].SetStoreGeneratedValue(children[i].EntityType.FindProperty("Id")!, keys[i], setModified: false);
        }
    }
    public class RekeyRoot
    {
        public string Id { get; set; } = "";
        public List<RekeyChild> Children { get; set; } = [];
    }
    public class RekeyChild { public int Id { get; set; } public string Text { get; set; } = ""; }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task Document_collection_order_survives_tracking_order(bool async)
    {
        string database = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<RevisionContext>(b => b.UseInMemoryDatabase(database));
        string[] observedOrder = [];
        services.AddScoped<DbContext>(p =>
        {
            var backend = p.GetRequiredService<RevisionContext>();
            backend.ObserveOrder = order => observedOrder = order;
            return backend;
        });
        await using var provider = services.BuildServiceProvider();
        await using var server = new InProcessInfoCarrierServer(provider,
            new InfoCarrierServerOptions().WithStoresOwnedDataInsideOwner(true));
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var envelope = new InfoCarrierEnvelopeServer(server, serializer);
        var client = new TransportInfoCarrierClient(new TestUtilities.InProcessInfoCarrierTransport(
            envelope.Dispatch, envelope.DispatchAsync, serializer), serializer);
        var options = new DbContextOptionsBuilder<RevisionContext>().UseInfoCarrier(
            InfoCarrierClient.Configure(client, o => o.ServerUsesRelationalQueryRules = false)).Options;
        await using var context = new RevisionContext(options);
        var root = new Root { Id = "owner", Children = [new() { Id = "one" }, new() { Id = "two" }] };
        context.Add(root);
        root.Children.Reverse();
        if (async) await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        else context.SaveChanges();
        await using var scope = provider.CreateAsyncScope();
        var backend = scope.ServiceProvider.GetRequiredService<RevisionContext>();
        var actual = await backend.Set<Root>().Include(r => r.Children).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "two", "one" }, observedOrder);
        Assert.Equal(new[] { "one", "two" }, actual.Children.Select(c => c.Id).Order());
    }

    public class RevisionContext(DbContextOptions<RevisionContext> options) : DbContext(options)
    {
        public Action<string[]>? ObserveOrder { get; set; }
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
            foreach (var root in ChangeTracker.Entries<Root>())
                ObserveOrder?.Invoke(root.Entity.Children.Select(c => c.Id).ToArray());
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
