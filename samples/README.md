# Samples

A client whose `DbContext` has **no database**, talking to a SQLite-backed server over HTTP.

There are two clients, and they are the same client twice: a **browser** (Blazor WebAssembly) and
a **console**. Both build the same `NorthwindContext` from `Northwind.Shared`, both wire it with
`UseInfoCarrier`, and neither has a connection string.

## Run it in a browser

One terminal, one command. The server hosts the client's files, so there is one origin and no CORS.

```bash
dotnet run --project samples/Northwind.Server
```

Then open <http://localhost:5199>.

Three pages, and a **wire inspector** down the right-hand side showing every round trip: the
operation, the size each way, how long it took, and the **decoded** payload — including the
expression tree, which the panel expands out of the base64 it travels in.

The Customers page prints the runtime type of the rows it loaded. It says `CustomerProxy`, so
Castle DynamicProxy does emit types inside WebAssembly.

### Two things WebAssembly will not do, and what the sample does instead

Both were found by running this app, and both are the browser's constraints rather than this
provider's — a console or desktop client of InfoCarrier has neither.

| What fails | Why | What the sample does |
|---|---|---|
| **Automatic lazy loading.** `order.Customer` throws `PlatformNotSupportedException: Cannot wait on monitors on this runtime` — *after* the request has gone out. | A navigation property getter is synchronous, so it must **block** on the HTTP round trip. A single-threaded runtime cannot block. `ILazyLoader.Load()` is synchronous too, so it fails identically. | `Entry(x).Reference(…).LoadAsync()` and `Entry(x).Collection(…).LoadAsync()` on the Order page. The navigation is still not fetched by the original query, and asking for it still costs exactly one round trip. |
| **A compiled model, out of the box.** Reading `NorthwindContextModel.Instance` throws `TypeInitializationException` wrapping `PlatformNotSupportedException` from `Thread.Start`, and the app never renders. | EF's generated model initializes itself on a `new Thread(…, 10 MB)` to avoid stack overflow on large models (EF issue 31751). WebAssembly has no threads. | `AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true)` as the first line of `Program.cs` — EF's own escape hatch, emitted into the generated file. It initializes inline instead. |

### Regenerating the compiled model

`dotnet ef` is pinned in `.config/dotnet-tools.json`; run `dotnet tool restore` once.

```bash
dotnet ef dbcontext optimize \
  --project samples/Northwind.Client/Northwind.Client.csproj \
  --startup-project samples/Northwind.Server/Northwind.Server.csproj \
  --context NorthwindContext \
  --output-dir CompiledModel \
  --namespace Northwind.Client.CompiledModel
```

**The server is the startup project because the client cannot be one**: the SDK emits no
`deps.json` for a Blazor WebAssembly project, so `dotnet ef` cannot load it. The server already
references the client (it hosts it), so the client's assembly — and its
`IDesignTimeDbContextFactory` — are reachable from the server's output.

The model is generated against the **client's** configuration, not the server's. Both halves
describe the same entities, but only the client's model is an InfoCarrier one, and it is the
client that uses it.

## Run it in a console

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
