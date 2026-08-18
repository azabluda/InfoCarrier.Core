# InfoCarrier.Core

**Use the full power of Entity Framework Core in a client application that has no database.**

InfoCarrier.Core is a non-relational EF Core provider that you deploy on the *client* side of a
multi-tier application. Your client gets a real `DbContext` — LINQ, change tracking, the identity
map, navigation fix-up, lazy loading, transactions — but no connection string and no database
driver. Queries and units of work travel to your application server and run there against the real
database.

The `DbContext` and the entity classes are shared source between client and server. You write the
model once.

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

That query is not evaluated on the client. It crosses the wire as an expression tree and the server
runs it against SQL Server, SQLite, PostgreSQL — whatever your server-side provider is.

## Two packages

| Package | What it is for | Cost |
|---|---|---|
| **`InfoCarrier.Core`** | The provider, the wire contracts, and `HttpInfoCarrierTransport` | one dependency: `Microsoft.EntityFrameworkCore` |
| **`InfoCarrier.Core.AspNetCore`** | `app.MapInfoCarrier()` — the server endpoint | a framework reference to `Microsoft.AspNetCore.App` |

**A client references only `InfoCarrier.Core`.** The HTTP transport is in it because it costs
nothing — `System.Net.Http` is in the shared framework, so it is safe in Blazor WebAssembly. The
ASP.NET Core endpoint is a separate package precisely because it is *not* free: a WPF, MAUI or
WebAssembly client must not have to be an ASP.NET Core app to restore its data-access library.

`IInfoCarrierTransport` is one method, so HTTP is a default rather than a requirement. gRPC, WCF,
a message bus or a direct in-process call are all a small class.

## Getting the packages

**Neither package is on nuget.org yet**, so `dotnet add package InfoCarrier.Core` will not find
one. A pushed NuGet version can be unlisted but never withdrawn, and this is a preview; the
irreversible step is deliberately left until the surface settles.

Until then, both `.nupkg` files — with their symbol packages — are attached to each
[GitHub Release](https://github.com/azabluda/InfoCarrier.Core/releases). Point a local feed at the
folder you downloaded them into:

```bash
dotnet nuget add source ./packages --name infocarrier-local
dotnet add package InfoCarrier.Core --version 10.0.0-preview.1
```

Or build them from the repository, which needs nothing but the .NET 10 SDK. **Note the branch** —
this is a ground-up rewrite for EF Core 10, and the repository's default branch is still v1:

```bash
git clone -b v10-claude https://github.com/azabluda/InfoCarrier.Core.git
cd InfoCarrier.Core
dotnet pack InfoCarrier.Core.slnx -c Release -o artifacts/pack
```

Both packages land in `artifacts/pack`; every other project in the solution opts out of packing.

## Status — preview

This package is `10.0.0-preview.1`. It is exercised by Microsoft's own
`EFCore.Specification.Tests`, the same suite EF Core's SQL Server, SQLite and InMemory providers
run:

```
Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177
```

The nine failures are known, classified, and gated in CI so the number cannot grow unnoticed. They
amount to one unsupported scenario, one query to treat with caution, and a few differences that are
not defects.

**Read the limitations page before adopting:**
<https://azabluda.github.io/InfoCarrier.Core/limitations/>

Still to come before a stable release: a shipped gRPC binding and streaming results as
`IAsyncEnumerable`.

## Security

The server executes an expression tree that arrived over the network. That is bounded deliberately
— a default-deny allowlist over node kinds, types and methods, no assembly loaded to satisfy a
payload, and reflection entry points blocked. The review, its adversarial tests, and the weaknesses
that are *accepted rather than solved*, are at
<https://github.com/azabluda/InfoCarrier.Core/blob/v10-claude/docs/security-review.md>.

**Authentication and authorization are out of scope and are yours.** No identity travels in the
envelope. Authenticate the transport, and use query filters on the server's model to decide what a
caller may see.

## Documentation and samples

**Documentation:** <https://azabluda.github.io/InfoCarrier.Core/>

**Source and samples:** <https://github.com/azabluda/InfoCarrier.Core/tree/v10-claude>

## License

MIT — Copyright (c) Alexander Zabluda.
