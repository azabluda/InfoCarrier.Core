# Use multiple clients and endpoints

Use separate registrations when destinations need different addresses, limits, or permissions.
Client names and server names are independent.

## Named clients

Register a name when an application calls several servers:

```csharp
builder.Services.AddInfoCarrierClient("orders", options => options
    .WithEndpoint("https://orders.example.com/infocarrier"));

builder.Services.AddDbContextFactory<ShopContext>(options =>
    options.UseInfoCarrier(clientName: "orders"));
```

Names are case-sensitive. Empty names and duplicate names fail during registration.
A missing name fails when context options are resolved. It never falls back to the default.
`UseInfoCarrier()` selects only the unnamed registration, even when exactly one named client exists.
Named registration does not change that default. Repeating the unnamed registration replaces it.

Each registration has its own transport and serializer configuration. Configure its returned
`IHttpClientBuilder` to set headers or handlers. InfoCarrier keeps these configurations separate
from application-owned HTTP clients with the same public name. The host disposes activated clients;
the HTTP client factory owns pooled handlers.

A client name does not name an Entity Framework context factory. For two destinations using the
same context type, give each application factory independent options:

```csharp
builder.Services.AddKeyedSingleton<IDbContextFactory<ShopContext>>("orders", (sp, _) =>
    new ShopContextFactory(new DbContextOptionsBuilder<ShopContext>()
        .UseApplicationServiceProvider(sp).UseInfoCarrier("orders").Options));
builder.Services.AddKeyedSingleton<IDbContextFactory<ShopContext>>("archive", (sp, _) =>
    new ShopContextFactory(new DbContextOptionsBuilder<ShopContext>()
        .UseApplicationServiceProvider(sp).UseInfoCarrier("archive").Options));

public sealed class ShopContextFactory(DbContextOptions<ShopContext> options)
    : IDbContextFactory<ShopContext>
{
    public ShopContext CreateDbContext() => new(options);
}
```

Register both InfoCarrier clients first, including `archive` with its endpoint.
Apply shared settings separately if the application needs them.
Resolve these application factories with `GetRequiredKeyedService<IDbContextFactory<ShopContext>>(name)`.
Calling ordinary `AddDbContextFactory<ShopContext>` twice does not create named factories.

## Several endpoints

Register separate servers when endpoints need different policies, even when they use the same context type:

```csharp
InfoCarrierServerBuilder admin = builder.Services.AddInfoCarrierServer<ShopContext>("admin", o => o
    .AllowArbitrarySqlExecution(true));
builder.Services.AddInfoCarrierServer<ShopContext>("public", o => o
    .AllowArbitrarySqlExecution(false));

// After building the application
app.MapInfoCarrier(admin, "api/admin").RequireAuthorization("Admin");
app.MapInfoCarrier("api/public", serverName: "public");
```

A retained server builder selects its own registration. It must belong to this host's registration
snapshot. A name supports registration and mapping in separate modules. Names are case-sensitive;
empty or duplicate names fail during registration. Missing names and foreign handles fail during mapping.
Client and server names are independent, so each can use `orders` in the same host.

`MapInfoCarrier()` selects the unnamed default. Named registrations do not create or replace it.
Repeated unnamed server registration retains earlier handles and selects the latest as default.
`MapInfoCarrier<ShopContext>("api/data")` works only when exactly one server registration uses that
context type. Missing or ambiguous matches fail during mapping. A route pattern alone never selects a name.

Each endpoint keeps its server, serializer limits, shared settings, caller policy, and logging grants.
The same pair handles buffered requests and streamed queries. The host owns the activated servers
and disposes their open transactions at shutdown.

### Selecting a scoped context

For two registrations of one context type with different stores, resolve a host-owned scoped context.
The following fragment assumes `archiveConnection` is your server-side SQL Server connection string
and that the server references that database provider:

```csharp
builder.Services.AddKeyedScoped<ShopContext>("archive", (sp, _) =>
    new ShopContext(new DbContextOptionsBuilder<ShopContext>()
        .UseApplicationServiceProvider(sp).UseSqlServer(archiveConnection).Options));

builder.Services.AddInfoCarrierServer<ShopContext>("archive",
    configure: null,
    resolveContext: sp => sp.GetRequiredKeyedService<ShopContext>("archive"));
```

The resolver receives the operation or transaction scope. That scope owns context disposal.
Return a context registered in that scope; do not construct an untracked context in the resolver.
Without a resolver, the server uses the normal concrete context registration.

See [shared settings](shared.md), [options](../../reference/options.md), and [service replacement](../../extending/services.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
