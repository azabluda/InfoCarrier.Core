`InfoCarrier.Core.AspNetCore` is the ASP.NET Core server binding for
[InfoCarrier.Core](https://www.nuget.org/packages/InfoCarrier.Core). It maps one endpoint that
receives a client's queries and units of work and executes them against a real database.

Reference this package from your server only. Your client needs `InfoCarrier.Core` alone.

Both halves need .NET 10 and EF Core 10. Install with
`dotnet add package InfoCarrier.Core.AspNetCore`, and give both packages the same version.

## Usage

Register a `DbContext` on a real EF Core provider, register the InfoCarrier server services, then
call `MapInfoCarrier`. For example:

```csharp
builder.Services.AddDbContext<ShopContext>(o => o.UseSqlite(connectionString));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ShopContext>());

builder.Services
    .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
    .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
    .AddInfoCarrierStandardValueMappers();

var app = builder.Build();
app.MapInfoCarrier();
```

The endpoint checks the protocol version and answers a mismatch with `400`. A failure in a request
the server did run comes back inside a normal response, and surfaces on the client as the exception
EF would have thrown locally. Your server's own model, query filters and interceptors apply, because
it is an ordinary EF Core application. A client can switch a query filter off, because
`IgnoreQueryFilters()` travels in the expression tree, so a filter is a default and not an access
control.

Authentication and authorization are yours. No identity travels in the request, so authenticate the
transport.

## Getting started

See [Your first client and server](https://azabluda.github.io/InfoCarrier.Core/getting-started/first-app/),
which builds a working pair in one page.

## Additional documentation

See [The server](https://azabluda.github.io/InfoCarrier.Core/configuration/server/) for the
services this package expects, and [Security](https://azabluda.github.io/InfoCarrier.Core/security/)
for what the server will and will not execute.

## Feedback

If you encounter a bug, have a question, or would like to request a feature,
[open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
