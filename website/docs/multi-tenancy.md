# Multi-tenancy

The question for a multi-tenant server is what stops a caller reading another tenant's rows. A
global query filter does not, on its own: `IgnoreQueryFilters()` travels in the expression tree and
the server honours it, and no filter reaches `SaveChanges`. [Security](security.md) has that
reasoning; this page is what to do instead.

Keep the filters. They stop your own client leaking by accident and keep tenant predicates out of
every call site. Just do not rely on them against a hostile caller.

## Reads

Two controls, and you want both.

The shared model is the coarse one. A client composes over the entity types you put in the shared
project and nothing else, so a type that must never leave the server belongs on a context only the
server has. That control is absolute and cheap, but it works per type: it cannot say "this tenant's
rows of this type".

A query interceptor on the server is the fine one. The server executes the client's tree through
its own query provider, so EF's pipeline runs there in full and an `IQueryExpressionInterceptor`
registered on the server's context sees the tree before it is translated. Re-apply the tenant
predicate there, whatever the incoming tree says. The client cannot reach it.

## Writes

`SaveChanges` is not a query, so no filter stands in its way and a client can submit a row whose key
belongs to another tenant. The check goes in the server's `SaveChanges` override or an EF
interceptor, and a client can name neither.

`ExecuteDelete` and `ExecuteUpdate` translate from a query, so a filter does narrow the rows they
touch. That is not a boundary either, because `IgnoreQueryFilters()` switches it off for them as it
does for a read. Neither has a separate server path, so a `SaveChanges` override never runs for
them, and the query interceptor above is what guards them.

**Read the values you check from the store, not from the entity the client sent.** A client controls
the original values in its own payload, so comparing the incoming row's tenant against the incoming
row's tenant proves nothing. Load the row, or check the key against what the tenant is allowed to
address.

## Resolving the tenant on the server

`InProcessInfoCarrierServer` resolves your `DbContext` from a scope it creates itself, off the
service provider handed to its constructor. You register it as a singleton, and a singleton resolves
from the root container, so the provider it holds is the root provider and the scope it makes is a
child of the root. **That scope is not the ASP.NET Core request scope.**

A scoped service that middleware populates is therefore a different, empty instance by the time your
filter reads it. Nothing throws; the tenant is the default.

Resolve the tenant from something that flows instead. `IHttpContextAccessor` is a singleton backed
by an async-local, so it reaches the new scope and carries the authenticated principal.

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
parameter, re-read for every query. A value taken any other way, from a local or a static, is
captured once when the model is first built and then cached with it.

## What to test before you ship

1. A client sends `IgnoreQueryFilters()`. The rows still come back scoped.
2. A client submits a `SaveChanges` for a key belonging to another tenant. The server refuses.
3. A client sends `ExecuteDelete` over another tenant's rows. Nothing is deleted.
4. Two tenants querying at once. Neither sees the other's rows, and the second tenant's query is
   filtered by its own tenant, not by the first one's.

The fourth catches both mistakes above: a filter reading the wrong tenant, or a model cached with
the first tenant's value, looks correct until a second tenant arrives. A single-tenant run never
finds either.

## What this page does not cover

Authentication. Nothing on the wire carries an identity, so authenticate the transport and gate the
endpoint before any of the above matters. See [Security](security.md).
