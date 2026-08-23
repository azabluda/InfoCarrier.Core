# Security

Your server executes an expression tree that arrived over the network. That is the product, and the
thing to think about before deploying it.

## What the library bounds

Deserialization is default-deny. The node kinds a payload may contain, the types it may name and the
methods it may call are each checked against an allowlist, anything not on it is refused rather than
resolved, and no assembly is loaded to satisfy a payload. The allowlist excludes the members that
would turn a resolved `Type` back into a call, the classic route from "a tree can name a type" to "a
tree can invoke anything".

There is a size bound too, applied before parsing begins and defaulting to 64 MiB on requests
towards the server. A flat array of a hundred million constants is only three levels deep, so a
depth limit alone does not bound the memory a parse costs. See
[server configuration](configuration/server.md#payload-limits).

A client also cannot name a type the server's model does not have. The query runs against the
server's `DbContext`, so the entity types in your shared model are the whole of what a client can
compose over. That surface is a real bound. A query filter is not one, for the reason below.

## What is yours

!!! danger "Authentication and authorization are out of scope"

    No identity travels in the envelope. The library has no concept of a user, a role or a
    permission, and it will execute a query for anyone who can reach the endpoint.

Two things to do, both required.

### 1. Authenticate the transport

The endpoint takes ordinary ASP.NET Core conventions:

```csharp
app.MapInfoCarrier()
   .RequireAuthorization("DataAccess");
```

On the client, authenticate the `HttpClient`. See
[client configuration](configuration/client.md#the-http-transport).

### 2. Decide what a caller may see, on the server's model

A global query filter on the server's model applies to every query by default. A filter declared
only on the client's model is a convenience, because the client is code you shipped to someone
else's machine.

**A filter is a default, not a boundary.** `IgnoreQueryFilters()` is an ordinary EF Core operator
that travels in the expression tree, and the server honours it. Query filters also do not apply to
writes, so a client can submit a `SaveChanges` for a row whose key belongs to another tenant.

For writes, the check a client cannot name is the server's `SaveChanges` override or an EF
interceptor, and you read the values you check from the store rather than from the entity the client
sent. **For reads there is no such hook**: what a client can ask for is decided by which entity types
are in the shared model, which is why keeping a type out of it is the read-side control.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().HasQueryFilter(o => o.TenantId == _tenant.Current);
    modelBuilder.Entity<Document>().HasQueryFilter(d => d.OwnerId == _user.Id);
}
```

That check is the server's `SaveChanges` override or an EF interceptor: everything a client submits
is replayed through it, and the client cannot name either one.

## The shape of the exposure

A client can compose any query over the entity types your shared model exposes. That is the feature,
and it has four consequences.

Expensive queries are reachable. A caller can ask for a cross join. Bound it with query filters, a
paging convention, a rate limit at the gateway, or a statement timeout on the database.

Exception messages travel, with their stack traces. If yours carry connection strings, file paths or
internal identifiers, catch and rewrite them at the server boundary.

The endpoint is not a read-only API. `SaveChanges` and `ExecuteDelete` are part of it, so do not
expose it to the public internet without authentication.

## Transport security

Use HTTPS. The envelope is not encrypted or signed by this library, so a request that can be read
can be modified, and TLS terminating at a gateway leaves the hop behind it unprotected.

## Reporting a vulnerability

For anything you would rather not disclose publicly, contact the maintainer through the repository
first. Otherwise, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
