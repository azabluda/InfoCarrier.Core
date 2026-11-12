<div align="center">

![InfoCarrier.Core: EF Core in your client, real database on the server](docs/assets/infocarrier-core-banner.png)

[![Build & Test](https://github.com/azabluda/InfoCarrier.Core/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/azabluda/InfoCarrier.Core/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/InfoCarrier.Core)](https://www.nuget.org/packages/InfoCarrier.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](license.txt)

[Published documentation](https://azabluda.github.io/InfoCarrier.Core/) ·
[Getting started](website/docs/getting-started/installation.md) ·
[Samples](samples/) · [Contributing](CONTRIBUTING.md)

</div>

InfoCarrier.Core is an Entity Framework Core provider for client applications that use a database
through an application server. Write LINQ and save tracked changes through one generic server endpoint, without a controller
for every entity. The database driver and connection string stay on the server.

This README previews unreleased version 11 APIs, targeting .NET 11 and Entity Framework Core 11.
For available 10.3 packages, use the [published documentation](https://azabluda.github.io/InfoCarrier.Core/).
The [installation guide](website/docs/getting-started/installation.md) describes package-based setup for version 11.

## Basic usage

Both applications share a context and entity model. On the client:

```csharp
using var http = new HttpClient();
IInfoCarrierClient client = InfoCarrierClient.Create(http, o =>
    o.WithEndpoint("https://your-app-server/infocarrier"));

var options = new DbContextOptionsBuilder<ShopContext>().UseInfoCarrier(client).Options;
await using var context = new ShopContext(options);

var recent = await context.Orders
    .Where(o => o.Customer!.Country == "Germany")
    .OrderByDescending(o => o.PlacedOn)
    .Take(20)
    .ToListAsync();

if (recent.Count > 0)
{
    recent[0].Freight = 0m;
    await context.SaveChangesAsync();
}
```

The server registers its real database context, calls `AddInfoCarrierServer<ShopContext>()`,
and maps `app.MapInfoCarrier()`.
The [complete SQLite tutorial](website/docs/getting-started/first-app.md) supplies imports,
project setup, model code, seed data, and a save followed by a fresh read.

## Features and limits

Queries, saves, relationship loading, transactions, savepoints, and supported bulk operations run
through the server. Asynchronous non-tracking queries can stream results.
Supported final projections can run locally. Filtering, ordering, grouping, joins, and equality
must run on the server unless the application explicitly chooses local execution.

HTTP is included. Custom transports implement both `Send` and `SendAsync`.
The server's database provider determines which queries it can translate.
Read [limitations](website/docs/reference/limitations.md),
[security](website/docs/operations/security.md), and
[client/server compatibility](website/docs/reference/compatibility.md) before adopting.

## Credits

Built on [Entity Framework Core](https://github.com/dotnet/efcore).

[InfoCarrier.Core 1.0 to 3.1](https://github.com/azabluda/InfoCarrier.Core/tree/release/3.1),
by [on/off it-solutions gmbh](http://www.onoff-it-solutions.info), used
[Remote.Linq](https://github.com/6bee/Remote.Linq) and
[aqua-core](https://github.com/6bee/aqua-core) by Christof Senn.
The current implementation uses its own serializer.

MIT, Copyright (c) Alexander Zabluda. See [license.txt](license.txt).

## Support

For questions or bugs, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
Report vulnerabilities through a
[private advisory](https://github.com/azabluda/InfoCarrier.Core/security/advisories/new).
See [SECURITY.md](SECURITY.md) for scope and supported versions.
