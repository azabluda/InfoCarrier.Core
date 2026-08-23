# Blazor WebAssembly

A browser client works, and it is the case this provider is most obviously for: a `DbContext` in the
browser composing real LINQ, with the database behind your server. The sample's three pages run in
WebAssembly, trimmed.

Three things work differently here, and the first two are the browser's rather than this
provider's. A console or desktop client has neither.

The packages are the same as anywhere else: `InfoCarrier.Core` in the client, and
`InfoCarrier.Core.AspNetCore` in the server that answers it. See
[Installation](../getting-started/installation.md) and
[Configuring the server](../configuration/server.md); the sample this page refers to throughout is
[Run the samples](../getting-started/samples.md).

## Automatic lazy loading is impossible

```csharp
string company = order.Customer!.Company;
// PlatformNotSupportedException: Cannot wait on monitors on this runtime
```

A navigation property getter is synchronous, so a lazy load has to block on the round trip, and a
single-threaded WebAssembly runtime cannot block. It throws after the request has already gone out,
so you see the traffic while the value never arrives. `ILazyLoader.Load()` is synchronous too, so
injecting it instead does not help.

Do not call `UseLazyLoadingProxies()` in a browser client. Leaving it off means an unloaded
navigation is `null`. Load explicitly:

```csharp
await context.Entry(order).Reference(o => o.Customer).LoadAsync();
await context.Entry(order).Collection(o => o.Lines).LoadAsync();
```

The round-trip count is unchanged, but every call site that touches a navigation becomes `async`,
so a component cannot reach one from markup.

!!! note "The asymmetry with the server is safe"

    Your server may still call `UseLazyLoadingProxies()`. Proxies change how an entity is
    constructed on the side that enables them, and the wire carries entity type names rather than
    proxy types, so the two halves stay in step. It is the one deliberate exception to enabling
    model-shaping options on both halves.

## A compiled model is more trouble than it is worth here

`dotnet ef dbcontext optimize` output cannot be loaded in WebAssembly as generated. EF initializes
the model on a `new Thread(…, 10 MB)` to avoid stack overflow on large models
([dotnet/efcore#31751](https://github.com/dotnet/efcore/issues/31751)), and WebAssembly has no
threads, so reading `Instance` throws and the app never renders. EF's own escape hatch initializes
it inline instead, and works:

```csharp
AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true);
```

The second reason to skip it is better. The tooling needs a startup project it can load, and a
Blazor WebAssembly project emits no `deps.json`, so your server becomes the startup project. EF then
takes its configuration from the server's service provider and ignores an
`IDesignTimeDbContextFactory` in the client. What you get is a client compiled model annotated with
the server's table names and proxy settings, and the browser runs on the wrong model while appearing
to work.

Build the model at start-up instead. If you do use a compiled model, check which context it came
from first: an annotation such as `Relational:TableName` on a client model is the tell.

## Trimming

The client publishes with `PublishTrimmed=true` and runs. The published sample was driven through all
three pages: queries, the projection split, both navigation loads, a unit of work and a committed
transaction.

It reports IL trim warnings attributable to `InfoCarrier.Core`, which is expected. The wire carries a
type's name and the far end resolves it, so `Assembly.GetType(string)` and `MakeGenericMethod` are
what this provider is made of, and `[DynamicallyAccessedMembers]` cannot describe "whatever type the
caller's model names". The trimmer cannot prove that reflection safe for an arbitrary model, which is
not the same as it breaking yours. Test the paths your model uses.

## Wiring a browser client

Everything singleton, because one user and one tab means a scope has no lifetime behind it. And a
context factory rather than a context, so each page owns its own unit of work.

```csharp
WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

// Same origin as the page: let your server host the client's files and there is no CORS.
builder.Services.AddSingleton(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});

builder.Services.AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>();

builder.Services.AddSingleton<IInfoCarrierClient>(sp =>
{
    var serializer = sp.GetRequiredService<IInfoCarrierSerializer>();
    return new TransportInfoCarrierClient(
        new HttpInfoCarrierTransport(sp.GetRequiredService<HttpClient>(), serializer),
        serializer);
});

builder.Services.AddDbContextFactory<ShopContext>((sp, o) =>
    o.UseInfoCarrier(sp.GetRequiredService<IInfoCarrierClient>()));
```

Serialization is source-generated, so it survives trimming where reflection-based JSON does not.

## A grid is a special case

A grid's items provider may ask for a page before the previous answer lands, and a `DbContext` is
not thread-safe. Take a fresh context per page, and order on the `IQueryable` before `Skip` and
`Take` rather than through the grid's sorting of the projected row. Otherwise the server pages an
unordered set and the client sorts the page.

```csharp
private async ValueTask<GridItemsProviderResult<CustomerRow>> LoadAsync(
    GridItemsProviderRequest<CustomerRow> request)
{
    await using ShopContext context = await Contexts.CreateDbContextAsync();

    IQueryable<Customer> query = context.Customers;
    // filter, then order on the entity, then page
    ...
}
```
