// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     The first end-to-end smoke test (E3): a client context using the InfoCarrier provider
///     queries through the in-process transport against a server context on the InMemory
///     provider. Proves the vertical slice: capture → serialize → rebind → execute →
///     materialize.
/// </summary>
public class InMemorySmokeTest
{
    private static IServiceProvider BuildServerProvider(string databaseName)
        => new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
            .AddScoped<IExpressionSerializer, ExpressionSerializer>()
            .AddScoped<InfoCarrier.Core.Expressions.TypeNodeMapper>()
            .AddScoped<InfoCarrier.Core.Expressions.TypeNodeResolver>()
            .AddScoped<InfoCarrier.Core.Expressions.IDynamicValueMapper, InfoCarrier.Core.Expressions.DynamicValueMapper>()
            .AddScoped<InfoCarrier.Core.Expressions.ExpressionToNodeTranslator>()
            .AddDbContext<SmokeContext>(b => b.UseInMemoryDatabase(databaseName))
            .AddScoped<DbContext>(sp => sp.GetRequiredService<SmokeContext>())
            .BuildServiceProvider(validateScopes: true);

    [Fact]
    public async Task Client_query_round_trips_through_server()
    {
        string databaseName = Guid.NewGuid().ToString();

        // Seed the server store directly.
        IServiceProvider serverProvider = BuildServerProvider(databaseName);
        using (IServiceScope scope = serverProvider.CreateScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<SmokeContext>();
            seed.Blogs.AddRange(
                new Blog { Id = 1, Title = "alpha" },
                new Blog { Id = 2, Title = "beta" });
            seed.SaveChanges();
        }

        // The in-process client: ships operations to the in-process server over the transport.
        var server = new InProcessInfoCarrierServer(serverProvider);
        var transport = new InProcessInfoCarrierTransport(
            (envelope, ct) => DispatchAsync(server, envelope, ct),
            new SystemTextJsonInfoCarrierSerializer());
        var client = new TransportInfoCarrierClient(transport, new SystemTextJsonInfoCarrierSerializer());

        // Let EF build its own internal service provider via the InfoCarrier options
        // extension's ApplyServices (which calls AddEntityFrameworkInfoCarrier).
        var clientOptions = new DbContextOptionsBuilder<SmokeContext>()
            .UseInfoCarrier(client)
            .Options;

        await using var context = new SmokeContext(clientOptions);
        List<Blog> blogs = await context.Blogs.OrderBy(b => b.Id).ToListAsync();

        Assert.Equal(2, blogs.Count);
        Assert.Equal("alpha", blogs[0].Title);
        Assert.Equal("beta", blogs[1].Title);
    }

    private static async Task<Common.InfoCarrierEnvelope> DispatchAsync(
        IInfoCarrierServer server,
        Common.InfoCarrierEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        switch (envelope.Operation)
        {
            case Common.InfoCarrierOperation.Query:
            {
                var request = serializer.Deserialize<Common.QueryDataRequest>(envelope.Payload)!;
                Common.QueryDataResult result = await server.QueryDataAsync(request, cancellationToken);
                return envelope with { Payload = serializer.Serialize(result) };
            }

            default:
                throw new NotSupportedException($"Operation {envelope.Operation} not supported in smoke test.");
        }
    }
}
