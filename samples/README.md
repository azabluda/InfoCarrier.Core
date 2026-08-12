# Samples

A client whose `DbContext` has **no database**, talking to a SQLite-backed server over HTTP.

## Run it

Two terminals, no arguments.

```bash
# terminal 1 — the server. Creates and seeds northwind.db beside the binary.
dotnet run --project samples/Northwind.Server

# terminal 2 — the client. Its process has no database and no connection string.
dotnet run --project samples/Northwind.Demo
```

The server's launch profile pins `http://localhost:5199`; the demo defaults to the same address and
takes an override as its first argument.

Expected output:

```
  InfoCarrier.Core — Northwind demo
  Server: http://localhost:5199/
  This process has no database. Every line below crossed a TCP socket.

  A filtered query — the Where runs on the server
      ALFKI  Alfreds Futterkiste  (Berlin)

  A projection — only the selected columns cross the wire
      5 orders, as Id + CustomerId pairs:
      1:ALFKI  2:ALFKI  3:ANATR  4:AROUT  5:BERGS

  An aggregate — the server answers with a number, not with rows
      order lines with quantity >= 10: 3

  Lazy loading — touching a navigation costs another round trip
      order 1 loaded          (round trips so far: 4)
      order.Customer -> Alfreds Futterkiste   (round trips so far: 5)
      order.OrderDetails -> 2 lines        (round trips so far: 6)

  Unit of work — two edits, one SaveChanges, one round trip
      2 lines edited, 2 rows written, 1 round trip for the save

  A transaction — rolled back, and the store never sees it
      inside the transaction: 7 products
      after the rollback:     6 products (was 6)

  Done. 14 round trips, none of which touched a database in this process.
```

The round-trip counts are the interesting part. Lazy loading costs a request *when the navigation is
touched*, and two edits cost **one** save.

## The projects

| Project | What it is |
|---|---|
| `Northwind.Shared` | The model and **one** `NorthwindContext`, used by both halves. The wire carries entity type *names*, so the two models must agree; sharing the type makes that true by construction rather than by discipline. |
| `Northwind.Server` | ASP.NET Core + SQLite. One route, `POST /infocarrier`, which hands the envelope to the product's `InfoCarrierEnvelopeServer`. There is no UI — a `GET /` is a 404. |
| `Northwind.Client.Transport` | `HttpInfoCarrierTransport`, an `IInfoCarrierTransport` over `HttpClient`. |
| `Northwind.Demo` | The console client above. |

**`Northwind.Client.Transport` and `Northwind.Server/Transport/` contain no Northwind types**, on
purpose: they are written to be promoted into `InfoCarrier.Core.Http` and
`InfoCarrier.Core.AspNetCore` packages later. Keep it that way when editing them.

## The whole client wiring

This is the point of the sample, and it is four lines:

```csharp
var serializer = new SystemTextJsonInfoCarrierSerializer();
using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5199") };
var client = new TransportInfoCarrierClient(new HttpInfoCarrierTransport(http, serializer), serializer);

var options = new DbContextOptionsBuilder<NorthwindContext>().UseInfoCarrier(client).Options;
```

No connection string, no provider for a store, no database.

## What is not here yet

The **Blazor WebAssembly client** — three pages, Fluent UI and a wire-inspector panel — is phase 2
of this milestone. See
[`docs/superpowers/specs/2026-08-11-blazor-wasm-sample-design.md`](../docs/superpowers/specs/2026-08-11-blazor-wasm-sample-design.md).
Phase 1 deliberately stopped at the transport so that a trimming problem in the browser could not
stall it.
