# Security

**Your server executes an expression tree that arrived over the network.** That is the product, and
it is the thing to think about before deploying it. This page is what the library does about it and
what it leaves to you.

## What the library bounds

**Deserialization is default-deny.** The node kinds a payload may contain, the types it may name and
the methods it may call are each checked against an allowlist. Anything not on it is refused rather
than resolved.

**No assembly is loaded to satisfy a payload.** A type name that does not resolve within what the
server already has loaded is a refusal, not a load.

**Reflection entry points are blocked.** The allowlist deliberately excludes the members that would
turn a resolved `Type` back into a call — the classic route from "a tree can name a type" to "a tree
can invoke anything".

**There is a size bound**, applied before parsing begins, defaulting to 64 MiB on requests towards
the server. A flat array of a hundred million constants is only three levels deep, so a depth limit
alone does not bound the memory a parse costs. See
[server configuration](configuration/server.md#payload-limits).

**A client cannot reach past the server's model.** The query is executed against the server's
`DbContext`, with the server's model, its global query filters and its interceptors.

## What is yours

!!! danger "Authentication and authorization are out of scope"

    **No identity travels in the envelope.** The library has no concept of a user, a role or a
    permission, and it will happily execute a query for anyone who can reach the endpoint.

Two things to do, and they are not optional:

### 1. Authenticate the transport

The endpoint is an ordinary ASP.NET Core endpoint, so use ordinary ASP.NET Core:

```csharp
app.MapInfoCarrier()
   .RequireAuthorization("DataAccess");
```

On the client, authenticate the `HttpClient` — a bearer-token handler, a client certificate,
whatever your infrastructure uses. See
[client configuration](configuration/client.md#the-http-transport).

### 2. Decide what a caller may see, on the server's model

A global query filter on the **server's** model is applied to every query and cannot be composed
past from a client:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().HasQueryFilter(o => o.TenantId == _tenant.Current);
    modelBuilder.Entity<Document>().HasQueryFilter(d => d.OwnerId == _user.Id);
}
```

This is the right place for row-level authorization. A filter declared only on the client's model is
a convenience and not a boundary — the client is code you shipped to someone else's machine.

For writes, the server's `SaveChanges` override or an EF interceptor is the equivalent place:
everything a client submits is replayed through it.

## Think about the shape of the exposure

A client can compose any query over the entity types your shared model exposes. That is the feature.
It also means:

- **The shared model is your API surface.** An entity type in the shared project is a type any client
  can query. If some data should never leave the server, keep it out of the shared model, or map it
  on a server-only context.
- **Expensive queries are reachable.** A caller can ask for a cross join. Bound it: query filters,
  a paging convention, a per-caller rate limit at the gateway, a statement timeout on the database.
- **Exception messages travel.** A server-side failure comes back with its message and its stack
  trace. If your messages carry connection strings, file paths or internal identifiers, that is now
  visible to the client — review them, and prefer catching and rewriting at the server boundary.
- **Do not expose the endpoint to the public internet without authentication.** It is not a
  read-only API; `SaveChanges` and `ExecuteDelete` are part of the same endpoint.

## Transport security

Use HTTPS. The envelope is not encrypted or signed by this library, and a request that can be read
can be modified. The transport is the layer that establishes trust, which is why it is a seam you
own.

## Reporting a vulnerability

Open an issue at [github.com/azabluda/InfoCarrier.Core](https://github.com/azabluda/InfoCarrier.Core)
— or, for anything you would rather not disclose publicly, contact the maintainer through the
repository before filing.
