`InfoCarrier.Core.AspNetCore` is the ASP.NET Core server binding for
[InfoCarrier.Core](https://www.nuget.org/packages/InfoCarrier.Core). It maps one endpoint that
receives a client's queries and units of work and executes them against a real database.

Reference this package from your server only. Your client needs `InfoCarrier.Core` alone.

Both halves need .NET 10 and EF Core 10. Name the version when you install:
`dotnet add package InfoCarrier.Core.AspNetCore --version 10.0.0-preview.1`, and give both packages
the same version.

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

The endpoint checks the protocol version and answers a mismatch with `400`. A failure inside a
request it did run comes back inside a normal response, and surfaces on the client as the exception
EF would have thrown locally. Your server's own model, query filters and interceptors apply, because
it is an ordinary EF Core application.

Authentication and authorization are yours. No identity travels in the request, so authenticate the
transport and use query filters on the server's model to decide what a caller may see.

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
