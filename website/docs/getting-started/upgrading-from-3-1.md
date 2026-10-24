# Upgrading from 3.1

InfoCarrier.Core 10 is a rewrite. It shares its name and its idea with the 1.0 to 3.1 line and
almost nothing else: the expression serializer, the wire format, the client/server split and the
security model are all new code.

This guide targets the version 11 prerelease configuration API.

Your `DbContext` and your entity classes are unchanged. Everything around them moves, and it will
not compile until you have moved it. The three public interfaces kept their names and changed their
shapes, so an old implementation fails to build rather than building and misbehaving.

!!! warning "Check these two before you start"

    Your client must run on .NET 10. `3.1.1` targeted `netstandard2.0`, so it ran on .NET
    Framework; this generation targets `net10.0` only. If your client is a .NET Framework
    application, this upgrade is a port of the client first.

    Read [Limitations](../limitations.md) before you commit to the port. It lists every
    scenario that behaves differently here from another EF Core provider, and it is shorter to
    read now than to discover later.

## What did not change

The `DbContext` class, its `DbSet<>` properties and its `OnModelCreating`. Your entity classes, and
the fact that they are shared source between client and server. The queries you write against them,
and `SaveChanges` as a unit of work.

## The five things that did

### 1. One package became two

```xml
<!-- before -->
<PackageReference Include="InfoCarrier.Core" Version="3.1.1" />

<!-- after: the client and the shared model project -->
<PackageReference Include="InfoCarrier.Core" Version="11.0.0-*" />

<!-- after: the ASP.NET Core server, in addition to the above -->
<PackageReference Include="InfoCarrier.Core.AspNetCore" Version="11.0.0-*" />
```

Replace the floating versions with the same published prerelease version when pinning packages.
`Remote.Linq` and `Aqua` are gone. What remains is `Microsoft.EntityFrameworkCore` and
`Microsoft.EntityFrameworkCore.Relational`, neither of which brings a database driver. If you
referenced `Remote.Linq` or `Aqua` directly, remove it.

### 2. `UseInfoCarrierClient` became `UseInfoCarrier`

```csharp
// before
using InfoCarrier.Core.Client;
optionsBuilder.UseInfoCarrierClient(new MyInfoCarrierClientImpl());

// after
using InfoCarrier.Core;
services.AddInfoCarrierClient(o => o
    .WithEndpoint("https://your-app-server/infocarrier"));
services.AddDbContextFactory<ShopContext>(o => o.UseInfoCarrier());
```

### 3. The transport you wrote is now optional

`3.1` shipped no transport, so every application implemented `IInfoCarrierClient` itself: an
`HttpClient`, hand-configured serializer settings, one route per operation, and a cache keyed on a
transaction-id header to carry transactions between calls. The default client registration replaces that plumbing.

```csharp
using InfoCarrier.Core;

// Without dependency injection, create the same client directly.
using var http = new HttpClient();
IInfoCarrierClient client = InfoCarrierClient.Create(http, o => o
    .WithEndpoint("https://your-app-server/infocarrier"));
optionsBuilder.UseInfoCarrier(client);
```

Delete your old client implementation. If you are not on HTTP, implement `IInfoCarrierTransport`
instead. It has one method, and that method moves the request without reading it. See
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
builder.Services.AddInfoCarrierServer<ShopContext>();

app.MapInfoCarrier();
```

`AddInfoCarrierServer<ShopContext>` registers the serializer and server, then resolves your concrete
context through the host container. Standard value mappers are automatic. For shared shapes, types,
and custom mappers, pass the same application-owned configuration helper to both role callbacks.
See [Configuring the server](../configuration/server.md).

### 5. The three interfaces changed shape

Implement these only if you are doing something unusual. Most applications now use the shipped
implementations and touch none of them.

| Interface | `3.1.1` | `11.x` |
|---|---|---|
| `IInfoCarrierClient` | `ServerUrl`, plus sync and async pairs of `QueryData`, `SaveChanges` and the three transaction commands | nine `…Async` methods, no sync half, savepoints included |
| `IInfoCarrierServer` | `QueryData` / `SaveChanges` (+ async), each taking a `Func<DbContext>` | the same nine `…Async` operations as the client; the `DbContext` comes from your service provider |
| `IInfoCarrierValueMapper` | `TryMapToDynamicObject` / `TryMapFromDynamicObject`, over Aqua's `DynamicObject` | `TryMapToWire(object, Type, out object?)` / `TryMapFromWire(object?, Type, out object?)` |

The client contract is async only. `3.1`'s sync members existed to satisfy EF Core's synchronous
API and were routinely implemented with `.Result` or `.Wait()`. Synchronous `DbContext` calls still
work; they no longer oblige you to write a blocking transport.

Value mappers moved to `InfoCarrier.Core.ValueMapping` and no longer name a serializer type in their
signature. Two are built in, `IPAddress` and `Uri`, so a mapper you wrote for either can be deleted.
See [Value mappers](../configuration/value-mappers.md).

## Namespaces, at a glance

| `3.1.1` | `11.x` |
|---|---|
| `InfoCarrier.Core.Client` | `InfoCarrier.Core` |
| `InfoCarrier.Core.Server` | `InfoCarrier.Core` |
| `InfoCarrier.Core.Common` | `InfoCarrier.Core.Common`, unchanged in role |
| `InfoCarrier.Core.Common.ValueMapping` | `InfoCarrier.Core.ValueMapping` |
| (none) | `InfoCarrier.Core.AspNetCore` |

## A checklist

1. Confirm the client can target `net10.0`.
2. Pin the same version 11 prerelease on both packages.
3. Drop any direct `Remote.Linq` or `Aqua` reference.
4. Register `AddInfoCarrierClient` and replace `UseInfoCarrierClient` with `UseInfoCarrier`.
5. Delete your `IInfoCarrierClient` implementation, or reduce it to an `IInfoCarrierTransport`.
6. Replace the server controller with `MapInfoCarrier()` and the registrations above.
7. Port any value mapper to `TryMapToWire` and `TryMapFromWire`, deleting `IPAddress` and `Uri`
   ones.
8. Read [Limitations](../limitations.md) before you ship.

[What is new in 10.0](../release-notes/10.0.md) lists what this generation can do that `3.1` could
not. If something in your application has no route across,
[open an issue](https://github.com/azabluda/InfoCarrier.Core/issues).
