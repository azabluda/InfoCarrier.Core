# Blazor WebAssembly

A browser client works, and it is the case this provider is most obviously *for*: a `DbContext` in
the browser, composing real LINQ, with the database behind your server. The sample's three pages
run in WebAssembly, published trimmed.

Two constraints apply, and **neither is this provider's** — they are the browser's. A console or
desktop client has neither.

## Automatic lazy loading is impossible

```csharp
string company = order.Customer!.Company;
// PlatformNotSupportedException: Cannot wait on monitors on this runtime
```

A navigation property getter is **synchronous**, so a lazy load has to block on the round trip, and
a single-threaded WebAssembly runtime cannot block. It throws *after* the request has already gone
out, so you see the traffic while the value never arrives. Injecting `ILazyLoader` instead does not
help: `ILazyLoader.Load()` is synchronous too.

**Do not call `UseLazyLoadingProxies()` in a browser client.** Leaving it off means an unloaded
navigation is simply `null`, which is honest and debuggable, rather than an exception from inside a
proxy. Load explicitly:

```csharp
await context.Entry(order).Reference(o => o.Customer).LoadAsync();
await context.Entry(order).Collection(o => o.Lines).LoadAsync();
```

Nothing is lost: the navigation is still not fetched by the original query, and asking for it still
costs exactly one round trip.

!!! note "The asymmetry with the server is safe"

    Your **server** may still call `UseLazyLoadingProxies()` — it is not a browser. Proxies change
    how an entity is constructed on the side that enables them, and the wire carries entity type
    *names*. The sample is wired exactly this way: proxies on the server, none in the browser.

    This is the one deliberate exception to "enable model-shaping options on both halves".

## A compiled model needs a switch, and probably is not worth it

`dotnet ef dbcontext optimize` output cannot be loaded in WebAssembly as generated. EF initializes
the model on a `new Thread(…, 10 MB)` to avoid stack overflow on large models
([dotnet/efcore#31751](https://github.com/dotnet/efcore/issues/31751)), and WebAssembly has no
threads — reading `Instance` throws `TypeInitializationException` wrapping
`PlatformNotSupportedException`, and the app never renders.

EF's own escape hatch makes it initialize inline, and it works:

```csharp
AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true);
```

There is a second, better reason to skip it. `dotnet ef dbcontext optimize` needs a startup project
it can load, and a Blazor WebAssembly project emits no `deps.json` — so your **server** ends up being
the startup project, and EF's tooling then takes its configuration from the *server's* service
provider, silently ignoring an `IDesignTimeDbContextFactory` in the client project.

The result is a "client" compiled model annotated with the server's relational table names and the
server's proxy settings. The browser runs on the wrong model and appears to work, which is the
dangerous shape: a model divergence between the two halves produces wrong answers rather than
errors.

**Build the model at start-up instead**, as the sample does. If you do use a compiled model, verify
which context it was generated from before trusting it — a one-line annotation like
`Relational:TableName` on a client model is the tell.

## Trimming

The client publishes with `PublishTrimmed=true` and runs. The published sample was driven through
all three pages: queries, the projection split, both async navigation loads, a unit of work and a
committed transaction.

It reports IL trim warnings attributable to `InfoCarrier.Core`, and that is expected rather than a
clean bill of health. The wire carries a type's **name** and the far end resolves it, so
`Assembly.GetType(string)` and `MakeGenericMethod` are what this provider is made of, and
`[DynamicallyAccessedMembers]` cannot describe "whatever type the caller's model names". The
warnings mean the trimmer cannot *prove* the reflection safe for an arbitrary model — not that it
broke yours.

If you publish trimmed, test the paths your model actually uses. The repository gates the direction
of that warning count on every build so it cannot quietly grow.

## Wiring a browser client

Everything singleton: one user, one tab, so a scope has no lifetime behind it. A **context factory**
rather than a context, so each page owns its own unit of work.

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

Serialization is source-generated, so it survives a trimmed build where reflection-based JSON does
not.

## A grid is a special case

A grid's items provider may ask for a page before the previous answer lands, and a `DbContext` is
not thread-safe. Take a fresh context per page:

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

And **order on the `IQueryable` before `Skip`/`Take`**, not through the grid's own sorting of the
projected row — otherwise the server pages an unordered set and the client sorts the page.
