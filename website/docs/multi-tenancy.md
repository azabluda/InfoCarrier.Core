# Multi-tenancy

One question decides this page: what stops a caller reading another tenant's rows. A global query
filter does not, on its own.

## A query filter is a default, not a boundary

`IgnoreQueryFilters()` is an ordinary EF Core operator. It travels in the expression tree like any
other and the server honours it, so a client that wants your filter gone can have it gone. Query
filters also do not apply to writes at all.

Keep using them: they stop your own client leaking by accident and they keep tenant predicates out
of every call site. Just do not count them when the caller is hostile.

## Reads

Two controls, and a multi-tenant application wants both.

**The shared model is the coarse one.** A client composes over the entity types you put in the
shared project and nothing else. A type that must never leave the server belongs on a context only
the server has. This is absolute and it is cheap, but it works per type, so it cannot express
"this tenant's rows of this type".

**A query interceptor on the server is the fine one.** The server executes the client's tree through
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

This is the part that breaks quietly.

`InProcessInfoCarrierServer` resolves your `DbContext` from a scope it creates itself, off the
service provider it was constructed with. Registered the usual way, as a singleton, that provider is
the **root** one, so **the scope is not the ASP.NET Core request scope.**

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

The filter reads the tenant **through a member of the context**, which is what makes it a query
parameter re-read for every query. A value captured any other way, from a local or a static, is
captured once when the model is first built and then cached with it.

## What to test before you ship

1. A client sends `IgnoreQueryFilters()`. The rows still come back scoped.
2. A client submits a `SaveChanges` for a key belonging to another tenant. The server refuses.
3. A client sends `ExecuteDelete` over another tenant's rows. Nothing is deleted.
4. Two tenants in flight at once. Neither sees the other's rows, and neither sees the first
   tenant's filter.

The fourth catches the scope and model-caching mistakes above, and a single-tenant test run never
will.

## What this page does not cover

Authentication. Nothing on the wire carries an identity, and the library has no concept of a user or
a role. Authenticate the transport and gate the endpoint before any of the above matters. See
[Security](security.md).
