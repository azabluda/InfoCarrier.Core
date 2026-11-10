# Test without a network

A loopback transport sends serialized envelopes directly to a server in the same process.
It tests query and save behavior without HTTP.
It does not test authentication, HTTP headers, streaming, or proxy limits.

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.Common;

public sealed class LoopbackTransport(
    IInfoCarrierServer server, IInfoCarrierSerializer serializer) : IInfoCarrierTransport
{
    private readonly InfoCarrierEnvelopeServer endpoint = new(server, serializer);

    private InfoCarrierEnvelope Copy(InfoCarrierEnvelope envelope)
        => serializer.Deserialize<InfoCarrierEnvelope>(serializer.Serialize(envelope))
           ?? throw new InfoCarrierTransportException("Missing envelope.");

    public InfoCarrierEnvelope Send(InfoCarrierEnvelope request)
        => Copy(endpoint.Dispatch(Copy(request)));

    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
        => Copy(await endpoint.DispatchAsync(Copy(request), cancellationToken));
}
```

Serializing both directions prevents the test from sharing objects that would be copied over HTTP.

## Build the server and client

This uses the shared `ShopContext` from the [first tutorial](../getting-started/first-app.md).
The test project needs InfoCarrier.Core and the SQLite provider, plus the dependency injection namespaces.

```csharp
using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shop;

var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<ShopContext>(o => o.UseSqlite("Data Source=loopback.db"));
services.AddInfoCarrierServer<ShopContext>();

await using var provider = services.BuildServiceProvider();
await using (var scope = provider.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
    await db.Database.EnsureCreatedAsync();
}

var serializer = new SystemTextJsonInfoCarrierSerializer();
IInfoCarrierClient client = new TransportInfoCarrierClient(
    new LoopbackTransport(provider.GetRequiredService<IInfoCarrierServer>(), serializer),
    serializer);

var options = new DbContextOptionsBuilder<ShopContext>().UseInfoCarrier(client).Options;
await using var context = new ShopContext(options);
int customers = await context.Customers.CountAsync();
Console.WriteLine(customers);
```

Use a separate database file for each test that runs concurrently.
Dispose the host before deleting its database.
Test through real HTTP as well before relying on transport behavior.
See [custom hosts](transports.md) and [security](../operations/security.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
