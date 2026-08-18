# InfoCarrier.Core

**Use the full power of Entity Framework Core in a client application that has no database.**

InfoCarrier.Core is a non-relational EF Core provider that you deploy on the *client* side of a
multi-tier application. Your client gets a real `DbContext` with LINQ, change tracking, the identity
map, navigation fix-up, lazy loading and transactions, but no connection string and no database
driver. Queries and units of work travel to your application server and run there against the real
database.

The `DbContext` and the entity classes are shared source between client and server, so you write the
model once.

> **Not backward compatible with `3.1.1`.** This is a ground-up rewrite. Your `DbContext` and your
> entity classes carry over unchanged; the wiring around them does not, and existing code will not
> compile. See [Upgrading from 3.1](#upgrading-from-31) below.

## Example

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

That query is not evaluated on the client. It crosses the wire as an expression tree, and the server
runs it against SQL Server, SQLite, PostgreSQL, or whatever provider the server uses.

## Installing

```bash
dotnet add package InfoCarrier.Core --version 10.0.0-preview.1              # the client and the server
dotnet add package InfoCarrier.Core.AspNetCore --version 10.0.0-preview.1   # the server endpoint
```

> **Name the version.** `InfoCarrier.Core` has been on nuget.org since 1.0, and its newest *stable*
> release is `3.1.1`, the earlier line built for EF Core 3.1. An unversioned
> `dotnet add package InfoCarrier.Core` installs that one, and will keep doing so until a stable
> `10.x` ships. Passing `--prerelease` works too.

`InfoCarrier.Core.AspNetCore` is new in this generation and has no older version to fall back to,
but pin it anyway so the two halves cannot drift apart.

Both packages ship symbols and SourceLink, so you can step into the provider from a debugger.

## Two packages

| Package | What it is for | Cost |
|---|---|---|
| `InfoCarrier.Core` | The provider, the wire contracts, and `HttpInfoCarrierTransport` | one dependency: `Microsoft.EntityFrameworkCore` |
| `InfoCarrier.Core.AspNetCore` | `app.MapInfoCarrier()`, the server endpoint | a framework reference to `Microsoft.AspNetCore.App` |

A client references only `InfoCarrier.Core`. The HTTP transport is in it because it costs nothing:
`System.Net.Http` is in the shared framework, so it is safe in Blazor WebAssembly. The ASP.NET Core
endpoint is a separate package because it is not free, and a WPF, MAUI or WebAssembly client should
not have to be an ASP.NET Core app to restore its data-access library.

To use gRPC, WCF or a message bus instead of HTTP, you implement one small interface.

## Upgrading from 3.1

Your `DbContext` and your entity classes are unchanged. Everything around them moves.

| | `3.1.1` | `10.0.0-preview.1` |
|---|---|---|
| Client wiring | `UseInfoCarrierClient(client)` | `UseInfoCarrier(client)` |
| Transport | none shipped, yours to write | `HttpInfoCarrierTransport`, or yours |
| Server endpoint | none shipped, yours to write | `app.MapInfoCarrier()` |
| `IInfoCarrierClient` | sync and async pairs | async only, different shape |
| `IInfoCarrierServer` | `QueryData` / `SaveChanges` (+ async) | different shape |
| `IInfoCarrierValueMapper` | `TryMapToDynamicObject` / `TryMapFromDynamicObject` | `TryMapToWire` / `TryMapFromWire` |
| Dependencies | Remote.Linq and Aqua | none beyond `Microsoft.EntityFrameworkCore` |

The three interfaces kept their names and changed their shapes, so an existing implementation fails
to compile instead of compiling and misbehaving.

Step by step, with before-and-after code:
<https://azabluda.github.io/InfoCarrier.Core/getting-started/upgrading-from-3-1/>

## Status

This package is `10.0.0-preview.1`. It is exercised by Microsoft's own
`EFCore.Specification.Tests`, the same suite EF Core's SQL Server, SQLite and InMemory providers
run. The failures that remain are known, classified, and gated in CI so their number cannot grow
unnoticed.

Read the limitations page before adopting:
<https://azabluda.github.io/InfoCarrier.Core/limitations/>

Still to come before a stable release: a shipped gRPC binding, and streaming results as
`IAsyncEnumerable`.

## Security

Your server executes an expression tree that arrived over the network. That path is bounded by a
default-deny allowlist over node kinds, types and methods; no assembly is loaded to satisfy a
payload; and the reflection entry points that would turn a resolved `Type` into a call are blocked.
The review, its adversarial tests, and the weaknesses that are accepted rather than solved, are at
<https://github.com/azabluda/InfoCarrier.Core/blob/main/docs/security-review.md>.

Authentication and authorization are out of scope and remain yours. No identity travels in the
envelope. Authenticate the transport, and use query filters on the server's model to decide what a
caller may see.

## Documentation and samples

Documentation: <https://azabluda.github.io/InfoCarrier.Core/>

Source and samples: <https://github.com/azabluda/InfoCarrier.Core>

## License

MIT, Copyright (c) Alexander Zabluda.
