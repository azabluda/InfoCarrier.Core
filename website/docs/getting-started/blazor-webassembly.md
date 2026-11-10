# Blazor WebAssembly

A browser client works, and it is the case this provider is most obviously for: a `DbContext` in the
browser composing real LINQ, with the database behind your server. The sample's three pages run in
WebAssembly, trimmed.

Three things work differently here. The first two are the browser's rather than this provider's,
and a console or desktop client has neither. Everything else on this page assumes a working server,
which is [Configuring the server](../applications/configuration/server.md), and the sample referred to
throughout is [Run the samples](../getting-started/samples.md).

## Use asynchronous loading

Browser HTTP cannot make synchronous requests. Use asynchronous queries, saves, and navigation loads.
Do not enable lazy-loading proxies in the browser client, because a property getter cannot await HTTP.

```csharp
await context.Entry(order).Reference(o => o.Customer).LoadAsync();
await context.Entry(customer).Collection(c => c.Orders).LoadAsync();
```

Your server can still use lazy-loading proxies. The connection carries mapped entity names rather
than generated proxy types. Test the model configuration on both sides.

## Compiled models

`dotnet ef dbcontext optimize` output cannot be loaded in WebAssembly as generated. EF initializes
the model on a `new Thread(…, 10 MB)` to avoid stack overflow on large models
([dotnet/efcore#31751](https://github.com/dotnet/efcore/issues/31751)), and WebAssembly has no
threads, so reading `Instance` throws and the app never renders. EF's own escape hatch initializes
it inline instead, and works:

```csharp
AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true);
```

Also check how the model is generated. The tooling needs a startup project it can load, and a
Blazor WebAssembly project emits no `deps.json`, so your server becomes the startup project. EF then
takes its configuration from the server's service provider and ignores an
`IDesignTimeDbContextFactory` in the client. What you get is a client compiled model annotated with
the server's store types and proxy settings, and the browser runs on the wrong model while appearing
to work.

Build the model at start-up instead. If you do use a compiled model, check which context it came
from first. A table name is not the tell: your client model carries those too, from the same
`[Table]` attributes and `DbSet` names the server compiles. Look for something only the server's
provider writes, such as a `SqlServer:` or `Sqlite:` annotation, or a store type that names a
database. Your client's own mappings carry EF Core's neutral type names and name no store.

## Trimming

The client publishes with `PublishTrimmed=true` and runs. The published sample was driven through
all three pages, covering queries, the projection split, both navigation loads, a unit of work and
a committed transaction.

The publish reports IL trim warnings attributable to `InfoCarrier.Core`, which is expected. The wire
carries a type's name and the far end resolves it, so `Assembly.GetType(string)` and
`MakeGenericMethod` are what this provider is made of, and `[DynamicallyAccessedMembers]` cannot
describe "whatever type the caller's model names". The trimmer cannot prove that reflection safe for
an arbitrary model, which is not the same as it breaking yours. Test the paths your model uses.

## Wiring a browser client

The client references `InfoCarrier.Core` and nothing else; the server that answers it adds
`InfoCarrier.Core.AspNetCore`. See [Installation](../getting-started/installation.md).

Everything singleton, because one user and one tab means a scope has no lifetime behind it, and a
context factory rather than a context, so each page owns its own unit of work.

```csharp
WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

// Same origin as the page: let your server host the client's files and there is no CORS.
builder.Services.AddInfoCarrierClient(options =>
    options.WithEndpoint(builder.HostEnvironment.BaseAddress + "infocarrier"));

builder.Services.AddDbContextFactory<ShopContext>(o => o.UseInfoCarrier());
```

Serialization is source-generated, so it survives trimming where reflection-based JSON does not.

## A grid is a special case

A grid's items provider may ask for a page before the previous answer lands, and a `DbContext` is
not thread-safe. Take a fresh context per page, and order on the `IQueryable` before `Skip` and
`Take` rather than through the grid's sorting of the projected row. Otherwise the query can fail or return incorrectly ordered pages.

This method assumes an injected `IDbContextFactory<ShopContext>` named `Contexts`.
Connect it to the paging callback of your chosen grid:

```csharp
private async Task<List<Customer>> LoadPageAsync(
    int pageIndex, int pageSize, CancellationToken cancellationToken)
{
    await using ShopContext context = await Contexts.CreateDbContextAsync();
    return await context.Customers.AsNoTracking()
        .OrderBy(c => c.Company).ThenBy(c => c.Id)
        .Skip(pageIndex * pageSize).Take(pageSize)
        .ToListAsync(cancellationToken);
}
```

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
