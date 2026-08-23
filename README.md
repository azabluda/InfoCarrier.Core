<div align="center">

![InfoCarrier.Core: EF Core in your client, real database on the server](docs/assets/infocarrier-core-banner.png)

[![Build & Test](https://github.com/azabluda/InfoCarrier.Core/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/azabluda/InfoCarrier.Core/actions/workflows/build.yml)
[![Spec suite](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2Fazabluda%2FInfoCarrier.Core%2Fbadges%2Fspec-suite.json)](https://github.com/azabluda/InfoCarrier.Core/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/vpre/InfoCarrier.Core?label=InfoCarrier.Core&color=004880)](https://www.nuget.org/packages/InfoCarrier.Core)
[![EF Core 10](https://img.shields.io/badge/EF%20Core-10.0%20on%20.NET%2010-512BD4)](https://github.com/dotnet/efcore)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](license.txt)

[Documentation](https://azabluda.github.io/InfoCarrier.Core/) &nbsp;·&nbsp;
[Samples](samples/) &nbsp;·&nbsp;
[Try it in Codespaces](https://codespaces.new/azabluda/InfoCarrier.Core?quickstart=1) &nbsp;·&nbsp;
[Contributing](CONTRIBUTING.md)

</div>

---

InfoCarrier.Core is an Entity Framework Core provider for the client side of a multi-tier
application. Your client gets a real `DbContext` with LINQ, change tracking, the identity map,
navigation fix-up, lazy loading and transactions, but no connection string and no database driver.
Queries and units of work travel to your application server, which executes them with an ordinary
EF Core provider.

## Installation

```sh
# In the client project, and in the server project too.
dotnet add package InfoCarrier.Core --version 10.0.0-preview.1

# In the server project only.
dotnet add package InfoCarrier.Core.AspNetCore --version 10.0.0-preview.1
```

Both halves need .NET 10 and EF Core 10. Name the version, as above: without `--version`, NuGet
resolves the newest stable release, which belongs to the earlier and incompatible 3.1 line.

## Basic usage

The `DbContext` and the entity classes are shared source between the client and the server, so
both halves build the same model. On the client, point the context at a transport:

```csharp
var serializer = new SystemTextJsonInfoCarrierSerializer();
using var http = new HttpClient { BaseAddress = new Uri("https://your-app-server") };

IInfoCarrierClient client = new TransportInfoCarrierClient(
    new HttpInfoCarrierTransport(http, serializer), serializer);

var options = new DbContextOptionsBuilder<ShopContext>().UseInfoCarrier(client).Options;

// Everything below this line is ordinary EF Core.
await using var context = new ShopContext(options);

List<Order> recent = await context.Orders
    .Include(o => o.Customer)
    .Where(o => o.Customer!.Country == "Germany" && o.Freight > 50m)
    .OrderByDescending(o => o.PlacedOn)
    .Take(20)
    .ToListAsync();

recent[0].Freight = 0m;
await context.SaveChangesAsync();
```

That query is not evaluated on the client. It crosses the wire as an expression tree, and your
server runs it against SQL Server, PostgreSQL, SQLite, or whatever provider it uses. On the
server, `app.MapInfoCarrier()` is the endpoint that receives it.

[Your first client and server](https://azabluda.github.io/InfoCarrier.Core/getting-started/first-app/)
builds the whole pair, and [the samples](samples/) run one command each.

## What works

Queries and the client/server projection split, `SaveChanges` including many-to-many graphs, lazy
loading, explicit loading, transactions with savepoints, `ExecuteUpdate` and `ExecuteDelete`,
complex types, JSON-mapped owned collections, spatial types, compiled models, and Blazor
WebAssembly published trimmed. HTTP is included. To use gRPC, WCF or a message bus, write one small
class. `IInfoCarrierTransport` has one method.

The provider runs Microsoft's own EF Core specification suite, the same suite the SQL Server,
SQLite and InMemory providers run. The
[limitations page](https://azabluda.github.io/InfoCarrier.Core/limitations/) lists every scenario
in that suite which behaves differently here, with a worked example for each. Read it before you
adopt.

## Getting support

If you encounter a bug, have a question, or would like to request a feature,
[open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).

## Credits

Built on [Entity Framework Core](https://github.com/dotnet/efcore) and judged by its test suite.

[InfoCarrier.Core 1.0 to 3.1](https://github.com/azabluda/InfoCarrier.Core/tree/release/3.1), by
[on/off it-solutions gmbh](http://www.onoff-it-solutions.info), proved the idea, and built it on
[Remote.Linq](https://github.com/6bee/Remote.Linq) and
[aqua-core](https://github.com/6bee/aqua-core) by Christof Senn. Version 10 has its own serializer
and no longer depends on them.

MIT, Copyright (c) Alexander Zabluda. See [license.txt](license.txt).
