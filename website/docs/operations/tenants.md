# Isolate tenants

A tenant is a customer or organization whose data must be kept separate.
Authenticate the caller before selecting data.
A filter on the client cannot enforce this rule, and a server query filter can be bypassed with
`IgnoreQueryFilters()`.

## A database per tenant

One clear approach is to select a separate database from a trusted server-side list.
The client still uses the shared `ShopContext`.
The server selects its database from an authenticated claim, not a path or connection string sent by the client.

The following replaces the context registration in the [first tutorial](../getting-started/first-app.md).
It assumes authentication and a `DataAccess` authorization policy have already been configured.

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Shop;

var tenantConnections = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["north"] = "Data Source=north.db",
    ["south"] = "Data Source=south.db"
};

builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<ShopContext>((sp, options) =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    string? tenant = http?.User.FindFirst("tenant")?.Value;
    if (http?.User.Identity?.IsAuthenticated != true ||
        tenant is null ||
        !tenantConnections.TryGetValue(tenant, out string? connection))
    {
        throw new UnauthorizedAccessException("No permitted tenant.");
    }

    options.UseSqlite(connection);
});

builder.Services.AddInfoCarrierServer<ShopContext>()
    .AddHttpCallerIdentity(http =>
        http.User.FindFirst("sub")?.Value + ":" + http.User.FindFirst("tenant")?.Value);

// After builder.Build():
app.MapInfoCarrier().RequireAuthorization("DataAccess");
```

The identity provider must issue trusted, stable subject and tenant claims.
Require those claims in your authorization policy.
Provision and seed each database with server-side administration code before accepting requests.
The tutorial's one-database startup seeding is not the provisioning procedure for this example.

With separate databases, removing query filters cannot reach the other tenant's tables.
A guessed key in `SaveChanges`, `ExecuteUpdate`, or `ExecuteDelete` addresses only the selected database.
Keep arbitrary SQL disabled; a database account or provider may offer ways to access other databases.
Use separate database credentials where available and test your actual deployment permissions.

## If tenants share tables

Add a tenant property to the shared model and read the tenant from authenticated server state.
Keep a query filter to prevent accidental mistakes, but add controls a caller cannot disable:

| Operation | Required server check |
|---|---|
| Reads | A query interceptor that enforces tenant restrictions even after `IgnoreQueryFilters` |
| `SaveChanges` | A save override or interceptor that checks ownership using stored data |
| `ExecuteUpdate`, `ExecuteDelete` | Query enforcement; these do not pass through `SaveChanges` |
| Raw SQL | Leave disabled unless database permissions provide the required isolation |

Never compare a submitted tenant value with another submitted value as proof of ownership.
Load the stored row or check its key against the caller's permitted set.
Account for related entities, newly inserted rows, and changes to tenant identifiers.

A universal interceptor is outside this example: the correct checks depend on the application's model.
The database-per-tenant example avoids presenting an incomplete shared-table filter as authorization.

## Request scope and transactions

InfoCarrier creates its own context scope. It is not the HTTP middleware's scope.
A scoped tenant service filled by middleware will not automatically contain the same data there.
`IHttpContextAccessor` lets the context registration read the authenticated request.

A transaction holds the context selected when it starts.
Bind its token to both user and tenant identity. Changing tenants must not let a caller reuse a token
for a different tenant. See [transaction deployment](transactions.md).

## Check two tenants

Before deployment, verify all of these with north and south data:

1. A north query cannot read a row that exists only in south, including with `IgnoreQueryFilters`.
2. A north save using that south key cannot change the south row.
3. A north bulk update and delete cannot change or remove south rows.
4. Concurrent requests keep their own database selection.
5. A south caller cannot use a transaction opened by north.
6. Missing, untrusted, or unknown claims fail before selecting a database.

See [security](security.md). Database selection does not replace endpoint authentication.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
