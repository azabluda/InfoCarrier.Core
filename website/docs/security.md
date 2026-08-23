# Security

Your server executes an expression tree that arrived over the network. That is the product, and the
thing to think about before deploying it. This page says what the library does about it and what it
leaves to you.

## What the library bounds

Deserialization is default-deny. The node kinds a payload may contain, the types it may name and the
methods it may call are each checked against an allowlist, and anything not on it is refused rather
than resolved. No assembly is loaded to satisfy a payload. The allowlist excludes the members that
would turn a resolved `Type` back into a call, which is the classic route from "a tree can name a
type" to "a tree can invoke anything".

There is a size bound too, applied before parsing begins and defaulting to 64 MiB on requests
towards the server. A flat array of a hundred million constants is only three levels deep, so a
depth limit alone does not bound the memory a parse costs. See
[server configuration](configuration/server.md#payload-limits).

A client also cannot reach past the server's model. The query is executed against the server's
`DbContext`, with its global query filters and its interceptors.

## What is yours

!!! danger "Authentication and authorization are out of scope"

    No identity travels in the envelope. The library has no concept of a user, a role or a
    permission, and it will execute a query for anyone who can reach the endpoint.

Two things to do, both required.

### 1. Authenticate the transport

It is an ordinary ASP.NET Core endpoint, so use ordinary ASP.NET Core:

```csharp
app.MapInfoCarrier()
   .RequireAuthorization("DataAccess");
```

On the client, authenticate the `HttpClient`. See
[client configuration](configuration/client.md#the-http-transport).

### 2. Decide what a caller may see, on the server's model

A global query filter on the server's model is applied to every query and cannot be composed past
from a client, which makes it the right place for row-level authorization. A filter declared only on
the client's model is a convenience: the client is code you shipped to someone else's machine.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().HasQueryFilter(o => o.TenantId == _tenant.Current);
    modelBuilder.Entity<Document>().HasQueryFilter(d => d.OwnerId == _user.Id);
}
```

For writes, the server's `SaveChanges` override or an EF interceptor is the equivalent place.
Everything a client submits is replayed through it.

## The shape of the exposure

A client can compose any query over the entity types your shared model exposes. That is the feature,
and it has four consequences.

The shared model is your API surface. Data that should never leave the server belongs out of it, or
on a server-only context.

Expensive queries are reachable. A caller can ask for a cross join. Bound it with query filters, a
paging convention, a rate limit at the gateway, or a statement timeout on the database.

Exception messages travel, with their stack traces. If yours carry connection strings, file paths or
internal identifiers, catch and rewrite them at the server boundary.

The endpoint is not a read-only API. `SaveChanges` and `ExecuteDelete` are part of it, so do not
expose it to the public internet without authentication.

## Transport security

Use HTTPS. The envelope is not encrypted or signed by this library, and a request that can be read
can be modified.

## Reporting a vulnerability

For anything you would rather not disclose publicly, contact the maintainer through the repository
first. Otherwise, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
