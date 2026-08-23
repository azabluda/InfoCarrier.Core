# Samples

A client whose `DbContext` has **no database**, talking to a SQLite-backed server over HTTP.

> Using the library rather than working on it? The documentation site is the place to start:
> <https://azabluda.github.io/InfoCarrier.Core/>. This file is the samples' own notes — what each
> page demonstrates, and what running them established.

There are two clients, and they are the same client twice: a **browser** (Blazor WebAssembly) and
a **console**. Both build the same `NorthwindContext` from `Northwind.Shared`, both wire it with
`UseInfoCarrier`, and neither has a connection string.

## Run it in a browser

Nothing to install: [**open it in Codespaces**](https://codespaces.new/azabluda/InfoCarrier.Core?quickstart=1)
and `.devcontainer/devcontainer.json` builds the sample, starts the server and opens the demo. The
first launch takes about six minutes, measured, because there is no prebuild — see the comments
in that file for why not. It is quick after that: a stopped codespace resumes in seconds.

Locally it is one terminal, one command. The server hosts the client's files, so there is one
origin and no CORS.

```bash
dotnet run --project samples/Northwind.Server
```

Then open <http://localhost:5199>.

Three pages, and a **wire inspector** down the right-hand side showing every round trip: the
operation, the size each way, how long it took, and the **decoded** payload — including the
expression tree, which the panel expands out of the base64 it travels in.

**Customers** is an ordinary grid, and that is the point of it. Click a header to sort, open a
column's filter to narrow it, page through the rest — there is no *Run* button, because there is
nothing to run: sorting and filtering are already part of the expression tree the page sends.
Sorting is composed on the `IQueryable<Customer>` **before** `Skip`/`Take`, never through the
grid's own `ApplySorting`, which would sort the client-side projection record and quietly leave you
with server-side paging over client-side sorting. Filtering `Country` for *Germany* takes 65 rows to
8 and the panel shows the `Where` crossing the wire.

Each active filter shows as a chip in the grid footer that clears it. That is this sample's own
control rather than the grid's: `ColumnBase.Filtered` renders nothing in Fluent UI 4.14.4, so
without it an active filter is invisible and the item count is the only clue.

**Order** is a master-detail screen: a paged, sortable grid of orders on the left, the selected
order's detail on the right. It runs **two** `DbContext`s on purpose. The grid takes a fresh one per
page, because a grid provider may ask again before the last answer lands and a `DbContext` is not
thread-safe; the detail keeps one per selected order, because the unit of work is the thing being
demonstrated — several quantity edits accumulate in one change tracker and leave as **one**
`SaveChanges`. The master list is a projection over a join, so it shows *Alfreds Futterkiste* rather
than `ALFKI` and the server does the joining.

**Transfer** moves order 1 to another customer and takes a unit off a product's stock, both inside
one transaction, and offers a tickbox that makes the second save fail on the *server's* database.
You pick the new owner by company name; the id stays the bound value, because that is what the
foreign key needs. Either way the panel shows the whole shape — `BeginTransaction`, the saves, then
`Commit` or `Rollback` — and both figures are read back afterwards through a fresh context, so they
are the server's answer rather than the local change tracker agreeing with itself.

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

Expected output **against a freshly seeded store**, which is what the caveat below the transcript
is about — the browser pages write to the same `northwind.db`, and the Transfer page in particular
moves order 1 to another customer, so a store that has been clicked through answers differently:

```
  InfoCarrier.Core — Northwind demo
  Server: http://localhost:5199/
  This process has no database. Every line below crossed a TCP socket.

  A filtered query — the Where runs on the server
      ALFKI  Alfreds Futterkiste  (Berlin)
      BLAUS  Blauer See Delikatessen  (Mannheim)
      DRACD  Drachenblut Delikatessen  (Aachen)
      FRANK  Frankenversand  (München)
      KOENE  Königlich Essen  (Brandenburg)
      LEHMS  Lehmanns Marktstand  (Frankfurt a.M.)
      OTTIK  Ottilies Käseladen  (Köln)
      WANDK  Die Wandernde Kuh  (Stuttgart)

  A projection — only the selected columns cross the wire
      the first 8 orders, as Id + CustomerId pairs:
      1:ALFKI  2:ALFKI  3:ANATR  4:AROUT  5:BERGS  6:LETSS  7:OTTIK  8:SPECD

  An aggregate — the server answers with a number, not with rows
      order lines with quantity >= 10: 330

  Lazy loading — touching a navigation costs another round trip
      order 1 loaded          (round trips so far: 4)
      order.Customer -> Alfreds Futterkiste   (round trips so far: 5)
      order.OrderDetails -> 2 lines        (round trips so far: 6)

  Unit of work — two edits, one SaveChanges, one round trip
      2 lines edited, 2 rows written, 1 round trip for the save

  A transaction — rolled back, and the store never sees it
      inside the transaction: 31 products
      after the rollback:     30 products (was 30)

  Done. 14 round trips, none of which touched a database in this process.
```

The round-trip counts are the interesting part. Lazy loading costs a request *when the navigation is
touched*, and two edits cost **one** save. The projection takes only the first eight orders, and the
`Take` is part of the expression tree — the server returns eight rows rather than the client
trimming a list it already paid to receive.

**The console client lazy-loads normally**, unlike the browser: it is not WebAssembly, so a
synchronous navigation getter can block on the round trip. That is the same asymmetry described
above, seen from the other side.

**Delete `northwind.db` beside the server binary if you want these exact numbers.** The transcript
above is a fresh store: order 1 belongs to `ALFKI` and 330 order lines have a quantity of 10 or
more, both of which `InfoCarrier.Core.TransportTests` asserts against the same seed. Click through
the browser pages first and the same run prints `1:BERGS` and a different count — the Transfer page
moved the order and the Order page edited quantities, which is the demonstration working rather
than the numbers being wrong.

## The projects

| Project | What it is |
|---|---|
| `Northwind.Shared` | The model and **one** `NorthwindContext`, used by both halves. The wire carries entity type *names*, so the two models must agree; sharing the type makes that true by construction rather than by discipline. |
| `Northwind.Server` | ASP.NET Core + SQLite. `app.MapInfoCarrier()` is the one route it owns; everything else is the browser client's, which this host also serves, so there is one origin and no CORS. Creates and seeds `northwind.db` at start-up. |
| `Northwind.Client` | The Blazor WebAssembly client: three pages, the wire inspector, and no database. |
| `Northwind.Demo` | The console client above. |

**`Northwind.Client.Transport` and `Northwind.Server/Transport/` are gone, and that is the plan
working.** Both were written free of Northwind types so they could be promoted into packages, and
in M8-22 they were — as a file move, exactly as intended. `HttpInfoCarrierTransport` is now in
**`InfoCarrier.Core`** (it costs nothing: `System.Net.Http` is in the shared framework, which is
what makes it WebAssembly-safe), and `MapInfoCarrier` is in **`InfoCarrier.Core.AspNetCore`** (a
framework reference to `Microsoft.AspNetCore.App`, which is why it is not in the client package).

So the samples now reference the product the way an application would, and there is no
sample-owned transport left to keep honest.

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
the published output. It reports **88 IL trim warnings owned by `InfoCarrier.Core`**, and that is a
known, recorded number rather than a clean bill of health: the wire carries a type's *name* and the
far end resolves it, so `Assembly.GetType(string)` and `MakeGenericMethod` are what this provider is
made of, and `[DynamicallyAccessedMembers]` cannot describe them. The warnings mean the trimmer
cannot *prove* the reflection safe for an arbitrary model, not that it broke this one.

`eng/trim-ratchet.sh` gates the direction of that count against `eng/trim-baseline.txt`, exactly as
`eng/ratchet.sh` gates the spec suite. Everyone else's warnings are reported but not gated — EF Core
alone contributes 585 of the 853 total.

Both numbers have moved since they were first measured, in opposite directions and for unrelated
reasons, which is why `eng/trim-baseline.txt` records each one rather than only the current value.
`ours` rose 86 → 88 deliberately, for a `GroupBy` fix that needs exactly the two reflection shapes
this provider is built on; `total` fell 1129 → 853 because EF Core's own count dropped, and none of
that improvement is this repository's.

## What is not here yet

- **No automated test of the pages.** The 17 tests in `test/InfoCarrier.Core.TransportTests` cover
  the protocol over a real HTTP hop; the three pages were verified by driving a real browser, and
  that harness is not in the repository. CI would not notice a page breaking — only that the
  client still builds and publishes.
