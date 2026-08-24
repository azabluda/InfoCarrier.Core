# Multi-tenancy

The question for a multi-tenant server is what stops a caller reading another tenant's rows. A
global query filter does not, on its own: `IgnoreQueryFilters()` travels in the expression tree and
the server honours it, and filters never apply to writes. [Security](security.md) has that
reasoning; this page is what to do instead.

Keep the filters. They stop your own client leaking by accident and keep tenant predicates out of
every call site. Just do not count them against a hostile caller.

## Reads

Two controls, and a multi-tenant application wants both.

The shared model is the coarse one. A client composes over the entity types you put in the shared
project and nothing else, so a type that must never leave the server belongs on a context only the
server has. Absolute, and cheap, but it works per type: it cannot say "this tenant's rows of this
type".

A query interceptor on the server is the fine one. The server executes the client's tree through
its own query provider, so EF's pipeline runs there in full and an `IQueryExpressionInterceptor`
registered on the server's context sees the tree before it is translated. Re-apply the tenant
predicate there, whatever the incoming tree says. It is the one place the client cannot reach.

## Writes

A client can submit a `SaveChanges` for a row whose key belongs to another tenant, and no filter
stands in the way. The check goes in the server's `SaveChanges` override or an EF interceptor:
everything a client submits is replayed through one of those, and a client can name neither.

**Read the values you check from the store, not from the entity the client sent.** A client controls
the original values in its own payload, so comparing the incoming row's tenant against the incoming
row's tenant proves nothing. Load the row, or check the key against what the tenant is allowed to
address.

## Resolving the tenant on the server

`InProcessInfoCarrierServer` resolves your `DbContext` from a scope it creates itself, off the
service provider handed to its constructor. You register it as a singleton, and a singleton is
resolved from the root container, so the provider it holds is the root provider. The scope it makes
is a child of the root. **It is not the ASP.NET Core request scope.**

A scoped service that middleware populates is therefore a different, empty instance by the time your
filter reads it. Nothing throws; the tenant is whatever the default is.

Resolve the tenant from something that flows instead. `IHttpContextAccessor` is a singleton backed
by an async-local, so it reaches the new scope and carries the authenticated principal with it.

```csharp
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<ShopContext>((sp, o) => o.UseSqlServer(connectionString));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ShopContext>());
```

```csharp
public class ShopContext(DbContextOptions options, IHttpContextAccessor accessor) : DbContext(options)
{
    private string Tenant
        => accessor.HttpContext?.User.FindFirst("tenant")?.Value
           ?? throw new InvalidOperationException("no tenant on the request");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Order>().HasQueryFilter(o => o.TenantId == Tenant);
}
```

The filter reads the tenant through a member of the context, which is what makes it a query
parameter re-read for every query. A value captured any other way, from a local or a static, is
captured once when the model is first built and then cached with it.

## What to test before you ship

1. A client sends `IgnoreQueryFilters()`. The rows still come back scoped.
2. A client submits a `SaveChanges` for a key belonging to another tenant. The server refuses.
3. A client sends `ExecuteDelete` over another tenant's rows. Nothing is deleted.
4. Two tenants querying at once. Neither sees the other's rows, and the second tenant's query is
   filtered by its own tenant rather than by the first one's.

The fourth catches both mistakes above, because a filter that read the wrong tenant, or a model
cached with the first tenant's value, looks perfectly correct until a second tenant arrives. A
single-tenant run never finds either.

## What this page does not cover

Authentication. Nothing on the wire carries an identity, so authenticate the transport and gate the
endpoint before any of the above matters. See [Security](security.md).
