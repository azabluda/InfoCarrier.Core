<div align="center">

![InfoCarrier.Core — EF Core in your client, real database on the server](docs/assets/infocarrier-core-banner.png)

**Use the full power of Entity Framework Core in a client application that has no database.**

[![NuGet](https://img.shields.io/nuget/vpre/InfoCarrier.Core?label=InfoCarrier.Core&color=004880)](https://www.nuget.org/packages/InfoCarrier.Core)
[![NuGet](https://img.shields.io/nuget/vpre/InfoCarrier.Core.AspNetCore?label=InfoCarrier.Core.AspNetCore&color=004880)](https://www.nuget.org/packages/InfoCarrier.Core.AspNetCore)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![EF Core 10](https://img.shields.io/badge/EF%20Core-10.0-512BD4)](https://github.com/dotnet/efcore)
[![Spec suite](https://img.shields.io/badge/EF%20spec%20suite-22%2C472%20passing-brightgreen)](docs/limitations.md)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](license.txt)

</div>

---

## What it is

InfoCarrier.Core is a **non-relational provider for Entity Framework Core** that you deploy on the
*client* side of a multi-tier application. Your client gets a real `DbContext` — LINQ, change
tracking, the identity map, navigation fix-up, lazy loading, transactions — but no connection
string and no database driver. Queries and units of work are translated into serializable requests
and executed by your application server against the real database.

The `DbContext` and the entity classes are **shared source between client and server**. You write
the model once.

```csharp
// In a WPF, Blazor WebAssembly, MAUI or console app — no database here.
var options = new DbContextOptionsBuilder<NorthwindContext>()
    .UseInfoCarrier(client)
    .Options;

await using var context = new NorthwindContext(options);

var recent = await context.Orders
    .Include(o => o.Customer)
    .Where(o => o.Customer!.Country == "Germany" && o.Freight > 50m)
    .OrderByDescending(o => o.OrderDate)
    .Take(20)
    .ToListAsync();

recent[0].Freight = 0m;
await context.SaveChangesAsync();   // a unit of work, executed on the server
```

That LINQ query is not evaluated in the client. It crosses the wire as an expression tree, and the
server runs it against SQL Server, SQLite, PostgreSQL — whatever your server-side provider is.

## Why you might want this

- **Your rich client needs more than a REST façade.** Hand-rolling an endpoint per screen, and a
  DTO per endpoint, is the cost this removes. The client composes the query it actually needs.
- **You already know the API.** It is EF Core. There is nothing new to learn on the client.
- **One model, not two.** No DTO layer to keep in sync with the entities.
- **The transport is yours.** This library turns commands into serializable objects and results
  back into tracked entities. How they travel — HTTP, gRPC, WCF, a message bus, in-process — is
  your decision.

## Status

Working today, and exercised end-to-end by the test suite: **queries**, the **client/server
projection split**, **`SaveChanges`** including many-to-many graphs, **lazy loading**,
**transactions with savepoints**, **complex types**, **JSON-mapped owned collections**, **spatial
types**, and **compiled models**.

This provider inherits Microsoft's `EFCore.Specification.Tests` — the same suite EF Core's own SQL
Server, SQLite and InMemory providers run:

```
Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177
```

**The nine failures are known, classified and gated in CI so the number cannot grow unnoticed.**
They come to *one* unsupported scenario, one query to treat with caution, and a few differences
that are not defects — two of them are queries this provider **answers** that other EF Core
providers reject.

> **Evaluating this library? Read [`docs/limitations.md`](docs/limitations.md) first.**
> Every limitation, with a worked example, written for someone who does not care about the
> internals. It takes a few minutes to tell whether any of them touches your application.

**Both packages are on nuget.org**, at `10.0.0-preview.1`, with symbol packages and SourceLink:

```bash
dotnet add package InfoCarrier.Core --version 10.0.0-preview.1              # the client and the server
dotnet add package InfoCarrier.Core.AspNetCore --version 10.0.0-preview.1   # the server endpoint
```

> ⚠️ **Name the version.** `InfoCarrier.Core` has been on nuget.org since **1.0**, and its newest
> *stable* release is **`3.1.1`** — the earlier line, built for EF Core 3.1. An unversioned
> `dotnet add package InfoCarrier.Core` installs **that**, not this one, and will keep doing so
> until a stable `10.x` ships. Name the version, or pass `--prerelease`.

**Not yet done:** a shipped gRPC binding, and streaming results as `IAsyncEnumerable`. Both may
change `IInfoCarrierTransport`, which is why the version still says `preview`.

## How it fits together

Two packages, split on the only line that costs anything.

| Package | What it is for | Cost |
|---|---|---|
| **`InfoCarrier.Core`** | The provider, the wire contracts, and `HttpInfoCarrierTransport` | one dependency: `Microsoft.EntityFrameworkCore` |
| **`InfoCarrier.Core.AspNetCore`** | `app.MapInfoCarrier()` — the server endpoint | a framework reference to `Microsoft.AspNetCore.App` |

**A client references only `InfoCarrier.Core`.** The HTTP transport lives there because it costs
nothing — `System.Net.Http` is in the shared framework, so it is safe in Blazor WebAssembly. The
ASP.NET Core endpoint is separate precisely because it is *not* free: a WPF, MAUI or WebAssembly
client should not have to be an ASP.NET Core app to restore its data-access library.

`IInfoCarrierTransport` is a single method, so HTTP is the default rather than a requirement —
gRPC, WCF, a message bus or a direct in-process call are each a small class.

### Client

```csharp
var serializer = new SystemTextJsonInfoCarrierSerializer();
using var httpClient = new HttpClient { BaseAddress = new Uri("https://your-app-server") };

IInfoCarrierClient client = new TransportInfoCarrierClient(
    new HttpInfoCarrierTransport(httpClient, serializer),
    serializer);

var options = new DbContextOptionsBuilder<NorthwindContext>()
    .UseInfoCarrier(client)
    .Options;
```

### Server

```csharp
builder.Services.AddDbContext<NorthwindContext>(o => o.UseSqlite(connectionString));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<NorthwindContext>());

builder.Services
    .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
    .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
    .AddInfoCarrierStandardValueMappers();

// One endpoint, from InfoCarrier.Core.AspNetCore.
app.MapInfoCarrier();
```

The server executes the client's tree against its own model, with its own query filters and its
own interceptors. A client cannot reach past what the server's model exposes.

## Try it

```bash
dotnet run --project samples/Northwind.Server
```

Open <http://localhost:5199>. You get a **Blazor WebAssembly** client whose `DbContext` has no
database, running against a SQLite-backed server, plus a **wire inspector** down the side showing
every round trip — the operation, the bytes each way, the time, and the decoded payload, including
the expression tree unpacked out of the base64 it travels in.

Three pages, deliberately ordinary. **Customers** is a grid that sorts and filters in its own
column headers, so the `OrderBy`, the `Where` and the `Skip`/`Take` in the panel are the ones the
grid itself composed. **Order** is master-detail: pick an order on the left, edit line quantities
on the right, and several edits leave as one `SaveChanges`. **Transfer** moves an order to another
customer and adjusts stock inside one transaction, with a tickbox that makes the second save fail
on the server so you can watch the rollback.

There is a console client too, which is the same client without a browser:

```bash
dotnet run --project samples/Northwind.Demo
```

See [`samples/README.md`](samples/README.md), which also records the two things WebAssembly will
not do — and why neither is this provider's constraint.

## How it differs from InfoCarrier.Core 1.0–3.1

This is a **ground-up rewrite** of
[InfoCarrier.Core 1.0–3.1](https://github.com/azabluda/InfoCarrier.Core/tree/master) for EF Core
10, not a port. That line ran from `1.0` to `3.1.1` and targeted EF Core 1.x through 3.1.

- **No Remote.Linq or Aqua dependency.** The expression serializer is in-tree and purpose-built,
  which is what makes the wire format, its type allowlist and its AOT story ours to reason about.
- **A security boundary that is stated and tested.** Deserialization is default-deny across node
  kinds, types and methods, and the review behind it is
  [`docs/security-review.md`](docs/security-review.md) — including what is *accepted* and why.
- **The EF specification suite is the acceptance criterion.** The earlier line's stated failure mode was
  suppressing tests; here a red test is information, skipping one to go green is forbidden, and
  the failure count is ratcheted in CI.
- **Trimming and AOT are measured, not assumed.** The Blazor client publishes trimmed and runs.

## Security

The server executes an expression tree that arrived over the network. That is the product, and it
is bounded deliberately: a default-deny allowlist over node kinds, types and methods; no assembly
is loaded to satisfy a payload; and reflection entry points that would turn a resolved `Type` into
a call are blocked. The reasoning, the adversarial tests, and the weaknesses that are **accepted
rather than solved**, are in [`docs/security-review.md`](docs/security-review.md).

**Authentication and authorization are out of scope and are yours.** No identity travels in the
envelope. Authenticate the transport, and use query filters on the *server's* model to decide what
a caller may see.

## Documentation

**If you are here to *use* this library, the documentation site is written for you:**
<https://azabluda.github.io/InfoCarrier.Core/> — installation, a first client and server,
querying, saving, transactions, configuration, and the limitations, with runnable code on every
page and no internals. Its source is [`website/`](website/); `mkdocs build --strict` is the gate,
and [`.github/workflows/docs.yml`](.github/workflows/docs.yml) publishes it.

The table below is the other audience: the documents this repository is *developed* against.

| Doc | Contents |
|---|---|
| [`docs/limitations.md`](docs/limitations.md) | **Start here if you are evaluating** — every known limitation, with an example |
| [`docs/security-review.md`](docs/security-review.md) | The deserialization path, its bound, and what is accepted |
| [`docs/architecture.md`](docs/architecture.md) | Components, test strategy, open questions |
| [`docs/decisions.md`](docs/decisions.md) | ADR log — the decisions and why |
| [`docs/wire-protocol.md`](docs/wire-protocol.md) | Client ↔ server contract |
| [`docs/expression-serialization.md`](docs/expression-serialization.md) | How a LINQ tree becomes bytes |
| [`docs/projection-split.md`](docs/projection-split.md) | What runs on the server, what runs on the client |
| [`docs/roadmap.md`](docs/roadmap.md) | Milestones and CI strategy |
| [`docs/build-warnings.md`](docs/build-warnings.md) | Which warnings are fatal, which are suppressed, where, and why |
| [`docs/versioning.md`](docs/versioning.md) | How a version is decided, and how to cut a release |

## Build and test

```bash
dotnet build InfoCarrier.Core.slnx

dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj
dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj
```

The first is the inherited EF specification suite; the second drives a real HTTP hop against an
ASP.NET Core server.

The build is clean — `0 Warning(s)` — and stays that way because **warnings are errors in CI, and
only in CI**. Locally a warning is a warning, so half-finished work still builds. To see what the
server will see:

```bash
CI=true dotnet build InfoCarrier.Core.slnx --configuration Release
```

[`docs/build-warnings.md`](docs/build-warnings.md) records what is suppressed, where, and why —
read it before adding a `NoWarn`.

## Credits

- [Entity Framework Core](https://github.com/dotnet/efcore) by Microsoft — this provider is built
  on it and judged by its test suite.
- [InfoCarrier.Core 1.0–3.1](https://github.com/azabluda/InfoCarrier.Core/tree/master), by
  [on/off it-solutions gmbh](http://www.onoff-it-solutions.info), which proved the idea.
- [Remote.Linq](https://github.com/6bee/Remote.Linq) and
  [aqua-core](https://github.com/6bee/aqua-core) by Christof Senn — that line's foundation, and
  specification material for this rewrite.

## License

MIT — Copyright (c) Alexander Zabluda. See [license.txt](license.txt).
