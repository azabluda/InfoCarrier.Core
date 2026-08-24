# Configuring the server

The server is an ordinary EF Core application. Five registrations and one endpoint make it an
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

| Registration | What it is for |
|---|---|
| `AddDbContext<ShopContext>` | Your context on your real provider. InfoCarrier never sees the connection string. |
| `AddScoped<DbContext>` | The server resolves the context per request as the base type, so the base type has to resolve. Without this line every request fails with a service-resolution error. |
| `IInfoCarrierSerializer` | The same format the client uses. Both ends must agree. |
| `IInfoCarrierServer` | `InProcessInfoCarrierServer` executes a request against a `DbContext` from this service provider. *In-process* means the same process as the database connection; it is the normal implementation, not a test double. |
| `AddInfoCarrierStandardValueMappers()` | The mappers for BCL types the wire cannot walk, `IPAddress` and `Uri`. The client gets these automatically; a server builds its own service collection, so it has to ask. See [Value mappers](value-mappers.md). |

## The endpoint

```csharp
app.MapInfoCarrier();               // route: "infocarrier"
app.MapInfoCarrier("api/data");     // or your own
```

It returns an `IEndpointConventionBuilder`, so it takes conventions like any other endpoint:

```csharp
app.MapInfoCarrier()
   .RequireAuthorization("DataAccess")
   .RequireCors("client")
   .WithName("InfoCarrier");
```

The client's transport must name the same route. See
[Configuring the client](client.md#the-http-transport).

A malformed body, or a client speaking a different protocol version, is answered with `400` and a
plain-text message naming the problem, with no stack trace and no server paths. Anything the server
ran and that failed comes back inside a normal response as a fault. See
[Handling errors](../guide/errors.md).

## Payload limits

A server deserializes what an untrusted peer sent it, so this is the direction that matters. The
default is 64 MiB per request. Set your own if you know what your clients legitimately send:

```csharp
builder.Services.AddSingleton<IInfoCarrierSerializer>(
    _ => new SystemTextJsonInfoCarrierSerializer(
        new InfoCarrierPayloadLimits(maxRequestBytes: 8 * 1024 * 1024)));
```

A query tree is kilobytes and a `SaveChanges` request is bounded by the graph the client tracked, so
a low ceiling is usually safe. Cap the request bytes at your gateway too. This bound is what catches
whatever the gateway lets through.

## Context lifetime

`InProcessInfoCarrierServer` takes a fresh scope per request, so every request gets a clean change
tracker. A client request is self-contained, and leftover tracked entities from a previous request
would collide with the next one.

The exception is a transaction, and it is the one case where server state outlives a request.
`BeginTransaction` pins one context and its connection until the commit or rollback, on the instance
that minted the token, so a load-balanced deployment needs session affinity for the life of a
transaction. [Transactions](../guide/transactions.md) has that and what an abandoned one costs.

## Where the checks go

A global query filter on the server's model applies to every query by default, which is what you
want for your own honest client. **It is not a control.** `IgnoreQueryFilters()` is an ordinary EF
Core operator, it travels in the expression tree like any other, and the server honours it. Query
filters also do not apply to writes, so a client can submit a `SaveChanges` for a row whose key
belongs to someone else.

So put a write check in the server's `SaveChanges` override or an EF interceptor, and a read check
in a query interceptor, which sees the client's tree before EF translates it.
[Multi-tenancy](../multi-tenancy.md) works both of them through, and [Security](../security.md) has
the threat model.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().HasQueryFilter(o => o.TenantId == _tenant.Current);
}
```

Everything else you register on the server's context runs too: EF interceptors, `SaveChanges`
overrides, auditing, soft-delete conventions. The client's request is work arriving at your context.
Nothing registered on the client reaches the server, so anything a client's own model declares is a
convenience.

## Model parity

Both halves build a model from the same `DbContext` source, and the wire names entity types and
properties, so deploy them together: a property the client names and the server does not know is a
failed request. Enable a model-shaping option on both sides or neither. A browser client is the
deliberate exception, covered on the [Blazor WebAssembly](../platforms/blazor-webassembly.md) page.
