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

## The transport is yours

Three pieces, and only the middle one is yours to choose:

| Piece | Who provides it |
|---|---|
| `IInfoCarrierClient` — turns EF operations into requests | this package |
| `IInfoCarrierTransport` — carries an envelope to the server and back | **you** (an HTTP binding is in the repository samples) |
| `IInfoCarrierServer` — replays a request against a real `DbContext` | this package |

HTTP, gRPC, WCF, a message bus, in-process — the library does not care.

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
<https://github.com/azabluda/InfoCarrier.Core/blob/master/docs/limitations.md>

Still to come before a stable release: a shipped gRPC binding and streaming results as
`IAsyncEnumerable`.

## Security

The server executes an expression tree that arrived over the network. That is bounded deliberately
— a default-deny allowlist over node kinds, types and methods, no assembly loaded to satisfy a
payload, and reflection entry points blocked. The review, its adversarial tests, and the weaknesses
that are *accepted rather than solved*, are at
<https://github.com/azabluda/InfoCarrier.Core/blob/master/docs/security-review.md>.

**Authentication and authorization are out of scope and are yours.** No identity travels in the
envelope. Authenticate the transport, and use query filters on the server's model to decide what a
caller may see.

## Documentation and samples

<https://github.com/azabluda/InfoCarrier.Core>

## License

MIT — Copyright (c) Alexander Zabluda.
