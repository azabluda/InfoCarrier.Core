# Configuring the client

Register the default HTTP client. `UseInfoCarrier` attaches it to a `DbContext`.

```csharp
builder.Services.AddInfoCarrierClient(options => options
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureInfoCarrier));

builder.Services.AddDbContextFactory<ShopContext>(options => options.UseInfoCarrier());
```

`UseInfoCarrier()` resolves the default client. Standard context and pooling registrations also work.
For manual options, pass `UseInfoCarrier(client)` or call `UseApplicationServiceProvider(provider)` first.
Client registration does not register a context. Independent options can use one context type with
InfoCarrier or SQLite.

Other EF Core options work as usual: logging, `ConfigureWarnings`, tracking, and proxies.

## Named clients

Register a name when an application calls several servers:

```csharp
builder.Services.AddInfoCarrierClient("orders", options => options
    .WithEndpoint("https://orders.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureInfoCarrier));

builder.Services.AddDbContextFactory<ShopContext>(options =>
    options.UseInfoCarrier(clientName: "orders"));
```

Names are case-sensitive. Empty names and duplicate names fail during registration.
A missing name fails when context options are resolved. It never falls back to the default.
`UseInfoCarrier()` selects only the unnamed registration, even when exactly one named client exists.
Named registration does not change that default. Repeating the unnamed registration replaces it.

Each registration has its own transport and serializer configuration. Configure its returned
`IHttpClientBuilder` to set headers or handlers. InfoCarrier keeps these configurations separate
from application-owned HTTP clients with the same public name. The host disposes activated clients;
the HTTP client factory owns pooled handlers.

A client name does not name an Entity Framework context factory. For two destinations using the
same context type, give each application factory independent options:

```csharp
builder.Services.AddKeyedSingleton<IDbContextFactory<ShopContext>>("orders", (sp, _) =>
    new ShopContextFactory(new DbContextOptionsBuilder<ShopContext>()
        .UseApplicationServiceProvider(sp).UseInfoCarrier("orders").Options));
builder.Services.AddKeyedSingleton<IDbContextFactory<ShopContext>>("archive", (sp, _) =>
    new ShopContextFactory(new DbContextOptionsBuilder<ShopContext>()
        .UseApplicationServiceProvider(sp).UseInfoCarrier("archive").Options));

public sealed class ShopContextFactory(DbContextOptions<ShopContext> options)
    : IDbContextFactory<ShopContext>
{
    public ShopContext CreateDbContext() => new(options);
}
```

Register both InfoCarrier clients first, including `archive` with its endpoint and shared settings.
Resolve these application factories with `GetRequiredKeyedService<IDbContextFactory<ShopContext>>(name)`.
Calling ordinary `AddDbContextFactory<ShopContext>` twice does not create named factories.

## What the client is told about the server

Fluent methods and properties configure options. `ConfigureShared` preserves the role type in a chain:

```csharp
builder.Services.AddInfoCarrierClient(options => options
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureInfoCarrier)
    .WithServerUsesRelationalQueryRules(false));
```

| Setting | What it says |
|---|---|
| `AddAllowedTypes(...)` | These Common Language Runtime types may appear beyond those implied by the model. Register them on both sides. See [Keys of your own types](../guide/querying.md#keys-of-your-own-types). |
| `AllowArbitrarySqlExecution(true)` | This client will send `FromSql` and `Database.SqlQuery<T>`. The server must grant it too. Only the server's setting is a boundary. See [Querying](../guide/querying.md#what-is-not-part-of-the-surface). |
| `WithServerUsesRelationalQueryRules(false)` | The server's store does not use relational query rules. A change to part of a document sends the whole document. **On a document store the server needs [its own setting](server.md#saying-the-store-is-a-document-store) as well**, and that setting keeps nested data. See [Querying](../guide/querying.md#rules-that-come-from-the-servers-store). |

Leave all three alone unless one of those sentences is true of your deployment.

## Configuration shared with the server

Put shared deployment facts in one application-owned method:

```csharp
builder.Services.AddInfoCarrierClient(options => options
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureInfoCarrier));

public static class ShopDeployment
{
    public static void ConfigureInfoCarrier(InfoCarrierDeploymentBuilder builder)
        => builder
            .AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null })
            .AddAllowedTypes(typeof(SqlServerDbFunctionsExtensions))
            .AddValueMappers(new MoneyValueMapper())
            .AllowArbitrarySqlExecution(true);
}
```

Apply the same method through `ConfigureShared` on the server. It runs immediately at that position
in the role callback. Shared methods can call other ordinary shared methods. Pass arguments with
`.ConfigureShared(b => ShopDeployment.ConfigureInfoCarrier(b, settings))` when needed.

Existing ordering rules apply. Later mapper additions do not override earlier matching mappers.
Sharing a method does not prove agreement between separately deployed applications. Matching settings
cannot add provider support. The server controls admission. Refusals are not retried.

Reference `InfoCarrier.Core.Abstractions` from the shared project, or link this source file.
Abstractions has no Entity Framework Core dependency.

Keep client-only settings outside this method.

Version 11 removes `InfoCarrierOptionsExtension.WithAllowedTypes` and `WithArbitrarySqlExecution`.
Configure deployment settings through client callbacks. For a custom client, use
`UseInfoCarrier(client, configure)`.

## Direct creation and replaceable parts

Create the default client without dependency injection when the application owns an `HttpClient`:

```csharp
IInfoCarrierClient client = InfoCarrierClient.Create(httpClient, options => options
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureInfoCarrier));
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
builder.Services.AddInfoCarrierClient(options => options
    .WithEndpoint("https://api.example.com/infocarrier")
    .WithMaxResponseBytes(16 * 1024 * 1024));
```

| | Default | Why |
|---|---|---|
| `MaxRequestBytes` | 64 MiB (`InfoCarrierPayloadLimits.DefaultMaxRequestBytes`) | An unauthenticated peer making your server allocate is the threat. No legitimate query tree comes near this. |
| `MaxResponseBytes` | `null`, no limit | You asked for the result, and the library has no basis for capping how large an answer your own query may have. Set it if a runaway query should fail loudly rather than exhaust memory, or if the hop back is not one you trust. |

Pass `null` to opt out of a limit. It is spelled as an explicit `null` rather than a very large
number so that opting out is visible in your code.

The response limit counts the complete streamed response, including frame metadata, rather than
each row separately. Enumeration can return rows before a later read exceeds the limit.
Each named client uses its own limit.

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

`infocarrier.client.round_trips` counts completed attempts, including failures before sending.
`infocarrier.client.round_trip.duration` records seconds through response disposal. Streamed queries
include enumeration time. Both carry `infocarrier.operation` and `infocarrier.outcome`: `success`,
`error`, `cancelled`, or `incomplete` when enumeration stops before completion. Failures carry `error.type`.
Fault tags describe a loaded reported type, or `InfoCarrierServerException` for unknown types.

Registered clients carry `infocarrier.client.name` and `infocarrier.client.registration`. The name
is empty for unnamed registrations. The number starts at one within each host and follows registration
order, distinguishing repeated unnamed registrations from a named client called `default`.
Directly constructed clients omit these labels. URLs, caller identities, and transaction tokens are
not metric labels. A host serving incoming requests also publishes separate
[server instruments](server.md#counting-server-requests).

Any OpenTelemetry exporter reads the same meter with `AddMeter("InfoCarrier.Core")`. Nothing is
measured while nothing is listening.

## The internal service provider

For registration-local wire serializers and transports, use
[component replacement](transports.md#replacing-registered-components). Those services belong to the host, outside EF's internal provider.

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
