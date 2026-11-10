# InfoCarrier.Core.AspNetCore

InfoCarrier.Core.AspNetCore is the ASP.NET Core HTTP endpoint for InfoCarrier clients.
It depends on InfoCarrier.Core and the ASP.NET Core shared framework.
Client applications reference Core without this server package.

## Usage

For version 11 development code, register the server context and map its endpoint:

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContext<ShopContext>(o => o.UseSqlite(connectionString));
builder.Services.AddInfoCarrierServer<ShopContext>();

// After building the application:
app.MapInfoCarrier().RequireAuthorization("DataAccess");
```

Configure the named authorization policy and the database provider in the application.
The endpoint permits queries and writes; enforce access rules on the server.
The shared model and imports are shown in the complete tutorial.
This development build still uses Entity Framework Core 10.

## Getting started

Follow the [source tutorial](https://github.com/azabluda/InfoCarrier.Core/blob/main/website/docs/getting-started/first-app.md).
Use a matching InfoCarrier.Core version on the client.

## Additional documentation

See [server setup](https://github.com/azabluda/InfoCarrier.Core/blob/main/website/docs/applications/configuration/server.md)
and [security](https://github.com/azabluda/InfoCarrier.Core/blob/main/website/docs/operations/security.md).
The [public documentation](https://azabluda.github.io/InfoCarrier.Core/) describes the published release.

## Feedback

[Open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new) for questions or problems.
