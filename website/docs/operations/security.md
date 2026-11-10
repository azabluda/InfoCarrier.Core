# Secure the deployment

Protect the InfoCarrier endpoint before exposing it to other users.
It accepts both client-composed queries and writes.

This guide assumes a working client/server application and an ASP.NET Core authentication scheme
configured for your identity provider.

## 1. Authenticate requests

Add an authorization policy before building the server application:

```csharp
builder.Services.AddAuthorization(options => options.AddPolicy("DataAccess", policy =>
    policy.RequireAuthenticatedUser()));
```

Map the endpoint with that policy:

```csharp
app.MapInfoCarrier().RequireAuthorization("DataAccess");
```

Use the application's authentication middleware and configure the client's HTTP authentication.
See [client HTTP configuration](../applications/configuration/client.md#configure-http).
The identity provider, credentials, and refresh behavior belong to your application.

Verify that an unauthenticated request receives an authorization failure and cannot query or save.
A browser cross-origin policy does not establish identity or grant data permissions.

## 2. Enforce data permissions on the server

Authentication identifies the caller. Your application must also decide which data that caller
may read or change.

| Operation | Where to enforce permission |
|---|---|
| Queries | Server-side query enforcement that a client cannot disable |
| `SaveChanges` | A server save override or interceptor, checking ownership using stored data |
| `ExecuteUpdate`, `ExecuteDelete` | Query enforcement; these operations do not pass through `SaveChanges` |

Keep server query filters as useful defaults, but do not rely on them for authorization.
A client can send `IgnoreQueryFilters()`.
Filters also do not protect a save submitted for another caller's key.

For tenant separation, see the [tenant guide](tenants.md).
It shows a database-per-tenant example and explains the separate controls needed for shared tables.

Leave arbitrary SQL disabled unless your application's trust model permits it.
A raw SQL caller can execute commands using the database account's rights, outside ordinary model filtering.
Restrict that database account as well.

## 3. Bind transactions to callers

A transaction token permits work inside an existing server transaction.
Bind it to a stable authenticated identity:

```csharp
builder.Services.AddInfoCarrierServer<ShopContext>()
    .AddHttpCallerIdentity(http => http.User.FindFirst("sub")?.Value);
```

Require the selected identity claim in your authorization policy.
If users can change tenants, include tenant identity in the binding too.
See [transaction deployment](transactions.md#binding-a-transaction-to-its-caller).

## 4. Protect the transport and resources

Use HTTPS between client and server, including any sensitive hop behind a reverse proxy.
The library does not encrypt or sign message content itself.

Set request-size limits at the gateway and server.
The InfoCarrier server default is 64 MiB per request; choose an application-appropriate value.
See [options and limits](../reference/options.md#byte-limits).

An authenticated user can still submit an expensive query.
Apply rate limits, database command timeouts, or server query checks according to your workload.
Keep transactions short and configure [idle expiry](transactions.md#evicting-an-abandoned-transaction).

## 5. Check returned information

Server exception messages and stack traces can reach the client.
Review messages for connection details, source paths, and internal identifiers.
Rewrite sensitive errors at your server boundary.

Log forwarding is off by default.
Enable only the levels appropriate for the client, and grant sensitive-data forwarding separately.
See [diagnostics](diagnostics.md#sending-the-servers-log-to-the-client).

## What the library checks

Message decoding checks permitted expression kinds, types, and methods.
It does not load an assembly just because a request names one.
The server executes queries against its configured model.
These checks reduce the accepted request surface; they do not replace the permission rules above.

`AddAllowedTypes` widens type permissions. Register trusted types only.
Do not register runtime invocation or reflection types such as `Binder`, `MethodInfo`, or `Activator`.

Anonymous structures must match a catalog built from trusted startup configuration.
Unknown structures cannot request arbitrary type generation.
The catalog has [size and nesting limits](../reference/options.md#anonymous-structures);
these do not bound every permitted method's execution cost.

## Reporting a vulnerability

[Open a private advisory](https://github.com/azabluda/InfoCarrier.Core/security/advisories/new)
rather than publishing sensitive details in an issue.
See the repository [security policy](https://github.com/azabluda/InfoCarrier.Core/blob/main/SECURITY.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
