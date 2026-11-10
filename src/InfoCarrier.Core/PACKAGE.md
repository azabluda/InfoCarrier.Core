# InfoCarrier.Core

InfoCarrier.Core is an Entity Framework Core provider for client applications that access a database
through an application server. It provides LINQ queries, change tracking, and transactions without
a local database driver.

## Usage

For version 11 development code, configure a client and a context factory:

```csharp
using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;

builder.Services.AddInfoCarrierClient(o =>
    o.WithEndpoint("https://your-app-server/infocarrier"));
builder.Services.AddDbContextFactory<ShopContext>(o => o.UseInfoCarrier());
```

The server uses its own database provider and the `InfoCarrier.Core.AspNetCore` HTTP endpoint.
Both sides need matching model definitions and compatible InfoCarrier versions.
This development build still uses Entity Framework Core 10.

## Getting started

Follow the [source tutorial](https://github.com/azabluda/InfoCarrier.Core/blob/main/website/docs/getting-started/first-app.md)
for a complete model, database setup, and client/server example.
Use [installation](https://github.com/azabluda/InfoCarrier.Core/blob/main/website/docs/getting-started/installation.md)
for the distinction between development builds and published packages.

## Additional documentation

Read the [development guides](https://github.com/azabluda/InfoCarrier.Core/tree/main/website/docs).
The [public documentation](https://azabluda.github.io/InfoCarrier.Core/) describes the published release.

## Feedback

[Open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new) for questions or problems.
