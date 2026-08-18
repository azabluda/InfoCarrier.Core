# Configuring the server

The server is an ordinary EF Core application. Four registrations and one endpoint turn it into an
InfoCarrier server.

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;

builder.Services.AddDbContext<ShopContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ShopContext>());

builder.Services
    .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
    .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
    .AddInfoCarrierStandardValueMappers();

WebApplication app = builder.Build();

app.MapInfoCarrier();
```

## The registrations, one at a time

**`AddDbContext<ShopContext>`** — your context on your real provider. InfoCarrier never sees the
connection string.

**`AddScoped<DbContext>`** — the server resolves the context per request as the base type
`DbContext`, so the base type has to resolve. Without this line, every request fails with a
service-resolution error.

**`IInfoCarrierSerializer`** — the same format the client uses. Both ends must agree.

**`IInfoCarrierServer`** — `InProcessInfoCarrierServer` executes a request against a `DbContext`
resolved from this service provider. *In-process* means it runs in the same process as the database
connection; it is the normal implementation, not a test double.

**`AddInfoCarrierStandardValueMappers()`** — the mappers for BCL types the wire cannot walk
(`IPAddress` and `Uri`). The client gets these automatically; a server builds its own service
collection, so it has to ask. A type mapped on one side only is worse than one mapped on neither.
See [Value mappers](value-mappers.md).

## The endpoint

```csharp
app.MapInfoCarrier();               // route: "infocarrier"
app.MapInfoCarrier("api/data");     // or your own
```

It returns the usual `IEndpointConventionBuilder`, so it takes conventions like any other endpoint:

```csharp
app.MapInfoCarrier()
   .RequireAuthorization("DataAccess")
   .RequireCors("client")
   .WithName("InfoCarrier");
```

Whatever route you choose, the client's transport must name the same one — see
[Configuring the client](client.md#the-http-transport).

## Payload limits

A server deserializes what an untrusted peer sent it, so this is the direction that matters. The
default is 64 MiB per request; set your own if you know what your clients legitimately send:

```csharp
builder.Services.AddSingleton<IInfoCarrierSerializer>(
    _ => new SystemTextJsonInfoCarrierSerializer(
        new InfoCarrierPayloadLimits(maxRequestBytes: 8 * 1024 * 1024)));
```

A query tree is kilobytes, and a `SaveChanges` request is bounded by the graph the client tracked,
so a low ceiling is usually safe. Cap the request bytes at your gateway too — this bound is the
last line, not the first.

## Context lifetime

`InProcessInfoCarrierServer` takes a **fresh scope per request**, so every request gets a clean
change tracker. That is deliberate: a client request is self-contained, carrying the state the
server needs to act on it, and leftover tracked entities from a previous request would collide with
the next one.

The exception is a transaction. `BeginTransaction` pins one context, and its connection, until the
commit or rollback — which is why transactions should be short. See
[Transactions](../guide/transactions.md).

## Query filters are your authorization boundary

The server executes the client's query against the **server's** model. A global query filter defined
there is applied to every query, and a client cannot compose past it:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().HasQueryFilter(o => o.TenantId == _tenant.Current);
}
```

This is the intended place to decide what a caller may see. Anything a client's own model declares
is a convenience on the client and not a boundary.

## Interceptors and SaveChanges overrides

Anything you register on the server's context runs when the server executes a request: EF
interceptors, `SaveChanges` overrides, auditing, soft-delete conventions. The client's request is
just work arriving at your context.

Nothing an application registers on the *client* is forwarded to the server, and nothing the server
registers reaches the client. The two are separate EF Core instances and either may have hooks of
its own — which is exactly what you want when the server is the side that must not be bypassed.

## Model parity

Both halves build a model from the same `DbContext` source, and the wire names entity types and
properties. Two rules follow:

- **Deploy both halves together** when the model changes. A property the client names and the
  server does not know is a failed request.
- **If a model-shaping option is enabled on one side, enable it on the other.**
  `UseLazyLoadingProxies()` is the common example — it adds a convention, so it belongs on both, or
  neither. (A browser client is the deliberate exception; see
  [Blazor WebAssembly](../platforms/blazor-webassembly.md).)

## Health of the endpoint

`MapInfoCarrier` answers a malformed body, or a client speaking a different protocol version, with
`400` and a plain-text message naming the problem — no stack trace and no server paths. Anything
the server *ran* and that failed comes back inside a normal response as a fault, so your client sees
an exception rather than an HTTP error; see [Handling errors](../guide/errors.md).
