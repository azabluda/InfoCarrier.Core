# Upgrading from 3.1

InfoCarrier.Core `10.0.0-preview.1` is a ground-up rewrite. It shares its name and its idea with
the `1.0`–`3.1` line and almost nothing else: the expression serializer, the wire format, the
client/server split and the security model are all new code.

**Your `DbContext` and your entity classes are unchanged.** Everything around them moves, and it
will not compile until you have moved it. That is deliberate — the three public interfaces kept
their names and changed their shapes, so an old implementation fails to build rather than building
and misbehaving.

!!! warning "Two things to check before you start"

    **The client must run on .NET 10.** `3.1.1` targeted `netstandard2.0`, so it ran on .NET
    Framework. This generation targets `net10.0` only. If your client is a .NET Framework
    application, this upgrade is a port of the client first.

    **Name the version when you install.** `3.1.1` is still the newest *stable* release on
    nuget.org, so an unversioned `dotnet add package` keeps giving you the old line — see
    [Installation](installation.md#installing).

## What did not change

- The `DbContext` class, its `DbSet<>` properties and its `OnModelCreating`.
- Your entity classes, and the fact that they are shared source between client and server.
- The queries you write against them, and `SaveChanges` as a unit of work.
- The idea: the client has no database, the server has the real one.

## The five things that did

### 1. One package became two

```xml
<!-- before -->
<PackageReference Include="InfoCarrier.Core" Version="3.1.1" />

<!-- after — the client and the shared model project -->
<PackageReference Include="InfoCarrier.Core" Version="10.0.0-preview.1" />

<!-- after — the ASP.NET Core server, in addition to the above -->
<PackageReference Include="InfoCarrier.Core.AspNetCore" Version="10.0.0-preview.1" />
```

`Remote.Linq` and `Aqua` are gone; the only remaining dependency is
`Microsoft.EntityFrameworkCore`. If you referenced either package *directly* — for example to
configure a serializer — remove it. See [Installation](installation.md) for why the split is
where it is.

### 2. `UseInfoCarrierClient` became `UseInfoCarrier`

```csharp
// before
using InfoCarrier.Core.Client;

optionsBuilder.UseInfoCarrierClient(new MyInfoCarrierClientImpl());
```

```csharp
// after
using InfoCarrier.Core;

optionsBuilder.UseInfoCarrier(client);
```

### 3. The transport you wrote is now optional

This is the largest saving, and the largest diff. `3.1` shipped **no** transport: every
application implemented `IInfoCarrierClient` itself, and the samples that came with it ran to
about a hundred lines each — an `HttpClient`, hand-configured Newtonsoft settings, one route per
operation, and a cache keyed on a transaction-id header to carry transactions between calls.

All of that is now three objects:

```csharp
using InfoCarrier.Core;

var serializer = new SystemTextJsonInfoCarrierSerializer();
using var http = new HttpClient { BaseAddress = new Uri("https://your-app-server") };

IInfoCarrierClient client = new TransportInfoCarrierClient(
    new HttpInfoCarrierTransport(http, serializer),
    serializer);
```

Delete your old client implementation. If you are not on HTTP — WCF, ServiceStack, a message bus,
a direct in-process call — you now implement `IInfoCarrierTransport`, which is **one method** that
moves an envelope and interprets nothing, rather than the whole client interface. See
[Custom transports](../configuration/transports.md).

### 4. The server endpoint ships too

```csharp
// before: a controller with a route per operation, plus AddInfoCarrierServer()
[Route("api")]
public class InfoCarrierController : ControllerBase
{
    [HttpPost, Route("QueryData")]
    public Task<QueryDataResult> PostQueryDataAsync([FromBody] QueryDataRequest request)
        => this.infoCarrierServer.QueryDataAsync(this.CreateDbContext, request);

    // ... and one action apiece for SaveChanges and the three transaction commands
}
```

```csharp
// after
using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;

builder.Services.AddDbContext<ShopContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ShopContext>());

builder.Services
    .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
    .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
    .AddInfoCarrierStandardValueMappers();

app.MapInfoCarrier();
```

`AddInfoCarrierServer()` is gone — register `InProcessInfoCarrierServer` yourself, as above.
`AddScoped<DbContext>` is the line people miss: the server resolves your context by its base type.
Full detail in [Configuring the server](../configuration/server.md).

### 5. The three interfaces changed shape

Implement these only if you are doing something unusual; most applications now use the shipped
implementations and touch none of them.

| Interface | `3.1.1` | `10.0.0-preview.1` |
|---|---|---|
| `IInfoCarrierClient` | `ServerUrl`, plus sync **and** async pairs of `QueryData`, `SaveChanges` and the three transaction commands | nine `…Async` methods, no sync half, savepoints included |
| `IInfoCarrierServer` | `QueryData` / `SaveChanges` (+ async), each taking a `Func<DbContext>` | the same nine `…Async` operations as the client; the `DbContext` comes from your service provider |
| `IInfoCarrierValueMapper` | `TryMapToDynamicObject` / `TryMapFromDynamicObject`, over Aqua's `DynamicObject` | `TryMapToWire(object, Type, out object?)` / `TryMapFromWire(object?, Type, out object?)` |

Two consequences worth calling out:

- **The client contract is async-only.** `3.1`'s sync members existed to satisfy EF Core's
  synchronous API and were routinely implemented with `.Result` or `.Wait()` — the samples carried
  a comment linking to the deadlock that causes. Synchronous `DbContext` calls still work; they no
  longer oblige you to write a blocking transport.
- **Value mappers moved namespace**, to `InfoCarrier.Core.ValueMapping`, and no longer name a
  serializer type in their signature. Two are now built in — `IPAddress` and `Uri` — so a mapper
  you wrote for either can simply be deleted. See
  [Value mappers](../configuration/value-mappers.md).

## Namespaces, at a glance

| `3.1.1` | `10.0.0-preview.1` |
|---|---|
| `InfoCarrier.Core.Client` | `InfoCarrier.Core` |
| `InfoCarrier.Core.Server` | `InfoCarrier.Core` |
| `InfoCarrier.Core.Common` | `InfoCarrier.Core.Common` — the wire contracts, unchanged in role |
| `InfoCarrier.Core.Common.ValueMapping` | `InfoCarrier.Core.ValueMapping` |
| — | `InfoCarrier.Core.AspNetCore` |

## What you get that 3.1 could not do

Most of this is EF Core 10 rather than InfoCarrier, which is rather the point — the provider
inherits what the version underneath it can express.

- Complex types, and JSON-mapped owned collections
- `ExecuteUpdate` and `ExecuteDelete`
- Many-to-many without an explicit join entity
- Savepoints — `3.1` had begin, commit and rollback only
- Spatial types without data loss: Z and M ordinates survive the round trip
- Blazor WebAssembly, published trimmed — with
  [one constraint worth reading first](../platforms/blazor-webassembly.md)
- A stated, tested [security boundary](../security.md) on the server, which `3.1` did not have

## A checklist

1. Confirm the client can target `net10.0`.
2. Pin `10.0.0-preview.1` on both packages.
3. Drop any direct `Remote.Linq` or `Aqua` reference.
4. `UseInfoCarrierClient` → `UseInfoCarrier`, and build the client from the three shipped objects.
5. Delete your `IInfoCarrierClient` implementation, or reduce it to an `IInfoCarrierTransport`.
6. Replace the server controller with `MapInfoCarrier()` and the registrations above.
7. Port any value mapper to `TryMapToWire` / `TryMapFromWire`, deleting `IPAddress` and `Uri` ones.
8. Read [Limitations](../limitations.md) before you ship.

If something in your application has no route across, please
[open an issue](https://github.com/azabluda/InfoCarrier.Core/issues) — while this is a preview,
that feedback still changes the shape of the API.
