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

The store is seeded with **65 customers, 240 orders, 476 order lines, 30 products and 8
categories** — enough that the grid pages properly and each page change is visibly its own query.
The data is generated from row indices rather than from `Random`, so it is byte-identical on every
machine: `InfoCarrier.Core.TransportTests` asserts exact counts against it.

### Two things WebAssembly will not do

Both were found by running this app. **Neither is this provider's constraint** — they are the
browser's, and the console demo below has neither.

#### 1. Automatic lazy loading

`order.Customer` throws `PlatformNotSupportedException: Cannot wait on monitors on this runtime`,
and throws it *after* the request has already gone out — so the inspector shows the round trip
while the value never arrives.

A navigation property getter is **synchronous**, so a lazy load has to *block* on the HTTP round
trip, and a single-threaded runtime cannot block. `ILazyLoader.Load()` is synchronous too, so
injecting a loader instead fails identically.

**What the sample does:** the browser client does not enable lazy-loading proxies at all, so an
unloaded navigation is simply `null` rather than a confusing exception from inside a proxy. The
Order page loads navigations explicitly:

```csharp
await context.Entry(order).Reference(o => o.Customer).LoadAsync();
await context.Entry(order).Collection(o => o.OrderDetails).LoadAsync();
```

Nothing about the demonstration is lost: the navigation is still not fetched by the original query,
and asking for it still costs exactly one round trip.

The **server** still calls `UseLazyLoadingProxies()` — it is not a browser. That asymmetry is safe:
proxies change how an entity is constructed on the side that enables them, and the wire carries
entity type *names*.

#### 2. A compiled model

`dotnet ef dbcontext optimize` output cannot be loaded in WebAssembly as generated: EF initializes
the model on a `new Thread(…, 10 MB)` to avoid stack overflow on large models
([EF issue 31751](https://github.com/dotnet/efcore/issues/31751)), and WebAssembly has no threads.
Reading `Instance` throws `TypeInitializationException` wrapping `PlatformNotSupportedException`
from `Thread.Start`, and the app never renders at all. EF's own escape hatch —
`AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true)` — makes it initialize
inline, and that does work.

**The sample no longer uses one anyway, for a second and better reason.** `dotnet ef dbcontext
optimize` needs a startup project it can load, and a Blazor WebAssembly project emits no
`deps.json` — so the server has to be the startup project. But EF's tooling then takes the
configuration from the **startup application's own service provider**, silently ignoring the
client's `IDesignTimeDbContextFactory`. The generated model was the *server's*: annotated
`Relational:TableName` and `Proxies:LazyLoading = true`. The browser was running on a relational,
proxied model and appeared to work — which is the dangerous shape, because model divergence between
the two halves produces wrong answers rather than errors. It is removed rather than made almost
right.

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

## Publishing trimmed

```bash
dotnet publish samples/Northwind.Client -c Release
```

The client publishes with `PublishTrimmed=true` **and runs** — all three pages were driven against
the published output. It reports **86 IL trim warnings owned by `InfoCarrier.Core`**, and that is a
known, recorded number rather than a clean bill of health: the wire carries a type's *name* and the
far end resolves it, so `Assembly.GetType(string)` and `MakeGenericMethod` are what this provider is
made of, and `[DynamicallyAccessedMembers]` cannot describe them. The warnings mean the trimmer
cannot *prove* the reflection safe for an arbitrary model, not that it broke this one.

`eng/trim-ratchet.sh` gates the direction of that count against `eng/trim-baseline.txt`, exactly as
`eng/ratchet.sh` gates the spec suite. Everyone else's warnings are reported but not gated — EF Core
alone contributes 864 of the 1129 total.

## What is not here yet

- **No automated test of the pages.** The 17 tests in `test/InfoCarrier.Core.TransportTests` cover
  the protocol over a real HTTP hop; the three pages were verified by driving a real browser, and
  that harness is not in the repository. CI would not notice a page breaking — only that the
  client still builds and publishes.
- **The two transport files still live here**, in `samples/`, rather than in packages. Both are
  deliberately free of Northwind types — `HttpInfoCarrierTransport.cs` and
  `InfoCarrierEndpointExtensions.cs` — so promoting them is a file move. The wire inspector is an
  `IInfoCarrierTransport` **decorator** in the client project precisely to keep that true.
