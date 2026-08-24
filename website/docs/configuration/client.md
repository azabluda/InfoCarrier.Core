# Configuring the client

The whole client configuration is `UseInfoCarrier`, plus whichever of the three replaceable objects
you want to change.

```csharp
optionsBuilder.UseInfoCarrier(client);
```

Everything else on `DbContextOptionsBuilder` is EF Core's and works as usual: logging,
`EnableSensitiveDataLogging`, `ConfigureWarnings`, query-tracking behaviour, proxies.

## The three objects

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

The serializer applies a size bound to what it will deserialize. One `InfoCarrierPayloadLimits`
object travels with it and each half enforces the half that applies: the server bounds the request
it receives, the client bounds the response it receives. Only the server's side is default-on,
because that is the side reading bytes from an untrusted peer.

```csharp
var serializer = new SystemTextJsonInfoCarrierSerializer(
    new InfoCarrierPayloadLimits(
        maxRequestBytes: 4 * 1024 * 1024,     // towards the server
        maxResponseBytes: 16 * 1024 * 1024)); // back to the client
```

| | Default | Why |
|---|---|---|
| `MaxRequestBytes` | 64 MiB (`InfoCarrierPayloadLimits.DefaultMaxRequestBytes`) | An unauthenticated peer making your server allocate is the threat. No legitimate query tree comes near this. |
| `MaxResponseBytes` | `null`, no bound | You asked for the result, and the library has no basis for capping how large an answer your own query may have. Set it if a runaway query should fail loudly rather than exhaust memory, or if the hop back is not one you trust. |

Pass `null` to opt out of a bound. It is spelled as an explicit `null` rather than a very large
number so that opting out is visible in your code.

`MaxRequestBytes` set here bounds nothing on the client, which never deserializes a request. It
matters on the server, and the server builds its own serializer with its own limits. See
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

To see the payloads themselves, decorate the transport. That is what the sample's wire inspector is.
See [Custom transports](transports.md).

## The internal service provider

Build EF's internal service provider yourself for two things: to register a
[value mapper](value-mappers.md) on the client, and to replace a provider service.

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

`AddEntityFrameworkInfoCarrier()` registers everything the provider needs, including the value
mappers that ship with it. Build the provider once and share it: EF caches services on it, and a new
one per context is a leak. If you need neither feature, do not do this. The default path builds and
caches the service provider for you.

## Client-side query filters and interceptors

They work, and they run on the client, so a filter defined only on the client is a convenience.
The server's own filters decide what comes back, and they are a default rather than a boundary,
because `IgnoreQueryFilters()` travels in the expression tree and the server honours it. See
[Where the checks go](server.md#where-the-checks-go) and [Multi-tenancy](../multi-tenancy.md).
