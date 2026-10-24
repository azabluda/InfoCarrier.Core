# Configuring the client

Register the default HTTP client. `UseInfoCarrier` attaches it to a `DbContext`.

```csharp
builder.Services.AddInfoCarrierClient(options =>
{
    options.Endpoint = new Uri("https://api.example.com/infocarrier");
    options.ConfigureInfoCarrier();
});

builder.Services.AddDbContextFactory<ShopContext>(options => options.UseInfoCarrier());
```

`UseInfoCarrier()` resolves the default client. Standard context and pooling registrations also work.
For manual options, pass `UseInfoCarrier(client)` or call `UseApplicationServiceProvider(provider)` first.
Client registration does not register a context. Independent options can use one context type with
InfoCarrier or SQLite.

Other EF Core options work as usual: logging, `ConfigureWarnings`, tracking, and proxies.

## What the client is told about the server

The callback carries deployment facts and client-only store information. Put client-only calls
before the shared method:

```csharp
builder.Services.AddInfoCarrierClient(options =>
{
    options.Endpoint = new Uri("https://api.example.com/infocarrier");
    options.ServerUsesRelationalQueryRules = false;
    options.ConfigureInfoCarrier();
});
```

| Setting | What it says |
|---|---|
| `AddAllowedTypes(...)` | These Common Language Runtime types may appear beyond those implied by the model. Register them on both sides. See [Keys of your own types](../guide/querying.md#keys-of-your-own-types). |
| `AllowArbitrarySqlExecution(true)` | This client will send `FromSql` and `Database.SqlQuery<T>`. The server must grant it too. Only the server's setting is a boundary. See [Querying](../guide/querying.md#what-is-not-part-of-the-surface). |
| `ServerUsesRelationalQueryRules = false` | The server's store does not use relational query rules. A change to part of a document sends the whole document. **On a document store the server needs [its own setting](server.md#saying-the-store-is-a-document-store) as well**, and that setting keeps nested data. See [Querying](../guide/querying.md#rules-that-come-from-the-servers-store). |

Leave all three alone unless one of those sentences is true of your deployment.

## Configuration shared with the server

Put shared deployment facts in one application-owned method:

```csharp
builder.Services.AddInfoCarrierClient(options =>
{
    options.Endpoint = new Uri("https://api.example.com/infocarrier");
    options.ConfigureInfoCarrier();
});

public static class ShopDeployment
{
    public static InfoCarrierDeploymentBuilder ConfigureInfoCarrier(
        this InfoCarrierDeploymentBuilder builder)
        => builder
            .AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null })
            .AddAllowedTypes(typeof(SqlServerDbFunctionsExtensions))
            .AddValueMappers(new MoneyValueMapper())
            .AllowArbitrarySqlExecution(true);
}
```

Pass the method through `AddInfoCarrierServer` on the server. Matching settings guide planning, but
cannot add provider support. The server controls admission. Refusals are not retried.

Reference `InfoCarrier.Core.Abstractions` from the shared project, or link this source file.
Abstractions has no Entity Framework Core dependency.

Keep client-only settings outside this method.

## Direct creation and replaceable parts

Create the default client without dependency injection when the application owns an `HttpClient`:

```csharp
IInfoCarrierClient client = InfoCarrierClient.Create(httpClient, options =>
{
    options.Endpoint = new Uri("https://api.example.com/infocarrier");
    options.ConfigureInfoCarrier();
});
```

The caller owns `httpClient`. Use the low-level types below for a custom transport or serializer.

```csharp
var serializer = new SystemTextJsonInfoCarrierSerializer();
using var http = new HttpClient { BaseAddress = new Uri("https://your-app-server") };

IInfoCarrierClient client = new TransportInfoCarrierClient(
    new HttpInfoCarrierTransport(http, serializer),
    serializer);
```

| Object | Interface | Replace it when |
|---|---|---|
| `SystemTextJsonInfoCarrierSerializer` | `IInfoCarrierSerializer` | you want a different format on the wire |
| `HttpInfoCarrierTransport` | `IInfoCarrierTransport` | you are not using HTTP, or want to decorate every request |
| `TransportInfoCarrierClient` | `IInfoCarrierClient` | you are hosting the server in an unusual way |

All three are safe to share, and that is a contract rather than an accident of the current build.
Construct them once: one client serves every `DbContext` in the application, including concurrent
ones, which is why the DI example below registers it as a singleton.

## The HTTP transport

```csharp
new HttpInfoCarrierTransport(httpClient, serializer, requestUri: "infocarrier");
```

The third argument is the route, relative to the `HttpClient`'s `BaseAddress`. It defaults to
`"infocarrier"`, which is what `MapInfoCarrier()` defaults to on the server. Change one and change
the other.

Everything else about the HTTP call belongs to the `HttpClient`: base address, timeout, headers,
handlers, retry policies. Authenticate here.

```csharp
services.AddHttpClient("infocarrier", c =>
    {
        c.BaseAddress = new Uri(configuration["ApiBaseUrl"]!);
        c.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<BearerTokenHandler>();

services.AddSingleton<IInfoCarrierClient>(sp =>
{
    HttpClient http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("infocarrier");
    var serializer = sp.GetRequiredService<IInfoCarrierSerializer>();
    return new TransportInfoCarrierClient(new HttpInfoCarrierTransport(http, serializer), serializer);
});
```

## Payload limits

The serializer applies a size limit to what it will deserialize. One `InfoCarrierPayloadLimits`
object travels with it, and each end enforces the limit that applies to it: the server caps the
request it receives, the client caps the response it receives. Only the server's side is
default-on, because that is the side reading bytes from an untrusted peer.

```csharp
builder.Services.AddInfoCarrierClient(options =>
{
    options.Endpoint = new Uri("https://api.example.com/infocarrier");
    options.MaxResponseBytes = 16 * 1024 * 1024;
});
```

| | Default | Why |
|---|---|---|
| `MaxRequestBytes` | 64 MiB (`InfoCarrierPayloadLimits.DefaultMaxRequestBytes`) | An unauthenticated peer making your server allocate is the threat. No legitimate query tree comes near this. |
| `MaxResponseBytes` | `null`, no limit | You asked for the result, and the library has no basis for capping how large an answer your own query may have. Set it if a runaway query should fail loudly rather than exhaust memory, or if the hop back is not one you trust. |

Pass `null` to opt out of a limit. It is spelled as an explicit `null` rather than a very large
number so that opting out is visible in your code.

The client role exposes only `MaxResponseBytes`, because it never deserializes a request. Configure
`MaxRequestBytes` on the server role. See
[Configuring the server](server.md#payload-limits).

## Synchronous calls

A synchronous `DbContext` call blocks on the async path, so the calling thread waits out the round
trip. Every await inside the provider uses `ConfigureAwait(false)`, so it does not deadlock on a UI
synchronization context, but a WPF or WinForms caller on the UI thread freezes the window until the
answer arrives. Use the async API from a UI thread.

## Logging

Turn on standard EF Core logging while you are learning what crosses the wire:

```csharp
optionsBuilder
    .UseInfoCarrier(client)
    .LogTo(Console.WriteLine, LogLevel.Information);
```

At `Information` this provider adds one event of its own, `InfoCarrierEventId.QuerySplit`, raised
when part of a query stays on the client. It names how many queries the server ran, which
operators stayed here, and which unregistered key types kept them here. A `Where`, a `Skip` or a
`Take` among them means the server sent more rows than your query asked for.

To see the payloads themselves, decorate the transport. That is what the sample's wire inspector is.
See [Custom transports](transports.md).

## Counting round trips

The cost of this provider is round trips. To count them in a running application, listen to the
meter named `InfoCarrier.Core`:

```bash
dotnet-counters monitor --process-id <pid> --counters InfoCarrier.Core
```

It publishes two instruments. `infocarrier.client.round_trips` counts every request that finished,
successful or failed. `infocarrier.client.round_trip.duration` records how long each one took, in
seconds. Both carry the operation as a tag, so you can tell a query from a save, and a request that
failed also carries `error.type`.

Any OpenTelemetry exporter reads the same meter with `AddMeter("InfoCarrier.Core")`. Nothing is
measured while nothing is listening.

## The internal service provider

Register application [value mappers](value-mappers.md) with `AddValueMappers` in the client role
callback. Build EF's internal service provider yourself only when replacing a provider service or
assembling a custom provider configuration.

```csharp
ServiceProvider providerServices = new ServiceCollection()
    .AddEntityFrameworkInfoCarrier()
    .AddSingleton<IInfoCarrierValueMapper, MoneyValueMapper>()
    .BuildServiceProvider();

DbContextOptions options = new DbContextOptionsBuilder<ShopContext>()
    .UseInternalServiceProvider(providerServices)
    .UseInfoCarrier(client)
    .Options;
```

`AddEntityFrameworkInfoCarrier()` registers the provider and its standard value mappers. Build and
share this provider once. Otherwise, use the default provider.

## Client-side query filters and interceptors

They work, and they run on the client, so a filter defined only on the client is a convenience.
The server's own filters decide what comes back, and they are a default rather than a boundary,
because `IgnoreQueryFilters()` travels in the expression tree and the server honours it. See
[Where the checks go](server.md#where-the-checks-go) and [Multi-tenancy](../multi-tenancy.md).
