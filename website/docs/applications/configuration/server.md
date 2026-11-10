# Configure a server

Register the database context, configure InfoCarrier, and map the HTTP endpoint:

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContext<ShopContext>(o =>
    o.UseSqlite("Data Source=shop.db"));

builder.Services.AddInfoCarrierServer<ShopContext>();

var app = builder.Build();
app.MapInfoCarrier();
await app.RunAsync();
```

This uses the [shared model](../../getting-started/first-app.md).
The first tutorial also shows database creation and seed data.
The server registration adds the serializer, server implementation, and standard value mappers.
It does not register your database context or map an endpoint.

Create tables and apply migrations through the server's database context.
The client's context cannot create or inspect a database connection.

## Map and protect the endpoint

The default route is `infocarrier`. To choose another route:

```csharp
app.MapInfoCarrier("api/data")
    .RequireAuthorization("DataAccess")
    .RequireCors("client");
```

Configure the named authorization and cross-origin policies in ASP.NET Core.
The client's endpoint address must include the same route.
Follow the [security guide](../../operations/security.md) before deployment.
A server query filter is useful, but callers can send `IgnoreQueryFilters()`.
Filters do not authorize changes made through `SaveChanges`.

## Context lifetime

An ordinary request gets a fresh service scope and database context.
A transaction keeps its context between requests until commit, rollback, expiry, or shutdown.
Requests using that transaction must reach the same server process.
See [transaction deployment](../../operations/transactions.md).

The context scope is created by InfoCarrier. It is not the HTTP request scope.
Read request identity through `IHttpContextAccessor` when it must reach that context.
See [tenant isolation](../../operations/tenants.md).

## Request limits and failures

The default request limit is 64 MiB. Lower it for an application that expects small requests:

```csharp
builder.Services.AddInfoCarrierServer<ShopContext>(o =>
    o.WithMaxRequestBytes(8 * 1024 * 1024));
```

Apply limits at the reverse proxy or gateway as well.
Each server registration uses its own limit, including streamed query requests.
See [options](../../reference/options.md).

Malformed requests and incompatible protocols receive HTTP 400 with a plain-text explanation.
Failures during execution return a reported server fault.
See [errors](../../operations/errors.md).

## Related tasks

- [Shared settings](shared.md) for model agreement and trusted types.
- [Multiple endpoints](multiple-endpoints.md) for separate server settings and context selection.
- [Document databases](document-stores.md) for owned document data.
- [Transactions](../../operations/transactions.md) for expiry and caller binding.
- [Diagnostics](../../operations/diagnostics.md) for forwarded logs and measurements.
- [Custom hosts](../../extending/transports.md) for direct server construction.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
