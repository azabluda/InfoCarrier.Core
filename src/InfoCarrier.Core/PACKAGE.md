# InfoCarrier.Core

InfoCarrier.Core is an Entity Framework Core provider for client applications that access a database
through an application server. It provides LINQ queries, change tracking, and transactions without
a local database driver.

## Usage

Configure a client and a context factory:

```csharp
using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;

builder.Services.AddInfoCarrierClient(o =>
    o.WithEndpoint("https://your-app-server/infocarrier"));
builder.Services.AddDbContextFactory<ShopContext>(o => o.UseInfoCarrier());
```

The server uses its own database provider and the `InfoCarrier.Core.AspNetCore` HTTP endpoint.
Both sides need matching model definitions and compatible InfoCarrier versions.
Version 11.0.0 requires .NET 11 and Entity Framework Core 11.

## Getting started

Follow the [tutorial](https://azabluda.github.io/InfoCarrier.Core/getting-started/first-app/)
for a complete model, database setup, and client/server example.
See [installation](https://azabluda.github.io/InfoCarrier.Core/getting-started/installation/)
for package and framework requirements.

## Additional documentation

Read the [documentation](https://azabluda.github.io/InfoCarrier.Core/) and
the [11.0 release notes](https://azabluda.github.io/InfoCarrier.Core/release-notes/11.0/).

## Feedback

[Open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new) for questions or problems.
