# Security

Your server executes an expression tree that arrived over the network. That is the product, and the
thing to think about before deploying it.

## What the library stops

Deserialization is default-deny. The node kinds a payload may contain, the types it may name and the
methods it may call are each checked against an allowlist; anything not on it is refused rather than
resolved, and the library loads no assembly to satisfy a payload. The allowlist excludes the
members that would turn a resolved `Type` back into a call, the classic route from "a tree can name
a type" to "a tree can invoke anything".

The review behind that is published, including the weaknesses accepted rather than solved and
the reasoning for each: [security-review.md](https://github.com/azabluda/InfoCarrier.Core/blob/main/docs/security-review.md).
It is written for someone auditing this, not for someone adopting it. Read §2 first: the boundary
is a conjunction across several clauses, and the clause-by-clause argument is what makes the claim
above checkable rather than a promise.

There is a size limit too, applied before parsing begins and defaulting to 64 MiB on requests
towards the server. A flat array of a hundred million constants is only three levels deep, so a
depth limit alone does not cap the memory a parse costs. See
[server configuration](configuration/server.md#payload-limits).

A client also cannot name a type the server's model does not have. The query runs against the
server's `DbContext`, so the entity types in your shared model are the whole of what a client can
compose over. That surface is a real boundary. A query filter is not one, for the reason below.

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

A rule a caller must not be able to switch off goes where the caller cannot reach: a query
interceptor on the server for reads, and the server's `SaveChanges` override or an EF interceptor
for writes. Everything a client's `SaveChanges` sends is replayed through that override or
interceptor, and the client can name neither.

[Multi-tenancy](multi-tenancy.md) is the worked version, including the scope trap that makes a
correct-looking tenant filter read the wrong tenant.

## The shape of the exposure

A client can compose any query over the entity types your shared model exposes. That is the feature,
and it has three consequences.

Expensive queries are reachable. A caller can ask for a cross join. Cap the cost where the caller
cannot reach: a rate limit at the gateway, a statement timeout on the database, or a query
interceptor on the server. A query filter will not do it, for the same reason it is not a boundary
above.

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
