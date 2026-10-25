// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.AspNetCore;
using InfoCarrier.Core.DocumentStoreTests.TestUtilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests;

public class NamedDocumentPolicyTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mongo_repairs_stubs_without_enabling_document_repair_on_inmemory(bool clientsFirst)
    {
        await using EmbeddedMongo mongo = await EmbeddedMongo.StartAsync();
        string memoryName = "named-policy-" + Guid.NewGuid().ToString("N");
        var probe = new MemoryReadProbe();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication? app = null;
        if (clientsFirst) { AddClients(); }
        builder.Services.AddKeyedScoped<Context>("mongo", (_, _) => new Context(new DbContextOptionsBuilder<Context>()
            .UseMongoDB(mongo.ConnectionString, "named-policy").Options));
        builder.Services.AddKeyedScoped<Context>("memory", (_, _) => new Context(new DbContextOptionsBuilder<Context>()
            .UseInMemoryDatabase(memoryName).AddInterceptors(probe).Options));
        builder.Services.AddInfoCarrierServer<Context>("mongo", o => o.WithStoresOwnedDataInsideOwner(true),
            sp => sp.GetRequiredKeyedService<Context>("mongo"));
        builder.Services.AddInfoCarrierServer<Context>("memory", o => o.WithStoresOwnedDataInsideOwner(false),
            sp => sp.GetRequiredKeyedService<Context>("memory"));
        if (!clientsFirst) { AddClients(); }
        app = builder.Build();
        await using (app)
        {
            foreach (string name in new[] { "mongo", "memory" })
            {
                app.MapInfoCarrier("/" + name, serverName: name);
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                Context context = scope.ServiceProvider.GetRequiredKeyedService<Context>(name);
                await context.Database.EnsureCreatedAsync();
                context.Add(new Root { Id = "one", Name = name + "-seed", Detail = new Detail { City = name + "-city" } });
                await context.SaveChangesAsync();
            }
            await app.StartAsync();
            await Task.WhenAll(Update("mongo"), Update("memory"));
            Assert.Equal(0, probe.OwnedReads);
            foreach (string name in new[] { "mongo", "memory" })
            {
                await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
                Context context = scope.ServiceProvider.GetRequiredKeyedService<Context>(name);
                Root stored = await context.Roots.SingleAsync();
                Assert.Equal(name + "-updated", stored.Name);
                Assert.Equal(name + "-city", stored.Detail.City);
            }
            // The independent read proves the probe observes stored owned data.
            Assert.True(probe.OwnedReads > 0);
        }

        void AddClients()
        {
            foreach (string name in new[] { "mongo", "memory" })
            {
                // Both stores need non-relational query rules; only Mongo needs document repair.
                builder.Services.AddInfoCarrierClient(name, o => o.WithEndpoint("http://localhost/" + name)
                    .WithServerUsesRelationalQueryRules(false))
                    .ConfigurePrimaryHttpMessageHandler(() => app!.GetTestServer().CreateHandler());
            }
        }

        async Task Update(string name)
        {
            await using var context = new Context(new DbContextOptionsBuilder<Context>()
                .UseApplicationServiceProvider(app!.Services).UseInfoCarrier(name).Options);
            var stub = new Root { Id = "one", Name = name + "-updated" };
            context.Attach(stub);
            context.Entry(stub).Property(value => value.Name).IsModified = true;
            Assert.Equal(1, await context.SaveChangesAsync());
            Assert.Equal(EntityState.Unchanged, context.Entry(stub).State);
        }
    }

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Root> Roots => Set<Root>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Root>().OwnsOne(value => value.Detail);
    }

    public sealed class Root
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public Detail Detail { get; set; } = null!;
    }

    public sealed class Detail
    {
        public string City { get; set; } = "";
    }

    private sealed class MemoryReadProbe : IMaterializationInterceptor
    {
        private int _ownedReads;
        internal int OwnedReads => Volatile.Read(ref _ownedReads);
        public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
        {
            if (entity is Detail { City: "memory-city" }) { Interlocked.Increment(ref _ownedReads); }
            return entity;
        }
    }
}
