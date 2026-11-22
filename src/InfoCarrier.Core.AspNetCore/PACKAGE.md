# InfoCarrier.Core.AspNetCore

InfoCarrier.Core.AspNetCore is the ASP.NET Core HTTP endpoint for InfoCarrier clients.
It depends on InfoCarrier.Core and the ASP.NET Core shared framework.
Client applications reference Core without this server package.

## Usage

Register the server context and map its endpoint:

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
Version 11.0.0 requires .NET 11 and Entity Framework Core 11.

## Getting started

Follow the [tutorial](https://azabluda.github.io/InfoCarrier.Core/getting-started/first-app/).
Use a matching InfoCarrier.Core version on the client.

## Additional documentation

See [server setup](https://azabluda.github.io/InfoCarrier.Core/applications/configuration/server/)
and [security](https://azabluda.github.io/InfoCarrier.Core/operations/security/).

## Feedback

[Open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new) for questions or problems.
