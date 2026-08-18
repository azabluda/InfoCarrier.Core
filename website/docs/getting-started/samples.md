# Run the samples

The repository carries a Northwind sample: one shared model, one SQLite-backed server, and two
clients that are the same client twice, one a browser and one a console. Neither has a connection
string.

```bash
git clone https://github.com/azabluda/InfoCarrier.Core.git
cd InfoCarrier.Core
```

## In a browser

One terminal, one command. The server hosts the client's files, so there is one origin and no CORS.

```bash
dotnet run --project samples/Northwind.Server
```

Open <http://localhost:5199>. You get a Blazor WebAssembly client whose `DbContext` has no
database, and a **wire inspector** down the right-hand side showing every round trip: the
operation, the size each way, how long it took, and the decoded payload, including the expression
tree, unpacked out of the base64 it travels in.

Three pages, deliberately ordinary:

**Customers** is a grid. Click a header to sort, open a column filter to narrow it, page through
the rest. There is no *Run* button because there is nothing to run: the sort, the filter and the
paging are already part of the expression tree the page sends. Filtering `Country` for *Germany*
takes 65 rows to 8, and the panel shows the `Where` crossing the wire.

**Order** is master-detail: a paged grid of orders on the left, the selected order's lines on the
right. Several quantity edits accumulate in one change tracker and leave as **one** `SaveChanges`.
The master list is a projection over a join, so it shows *Alfreds Futterkiste* rather than `ALFKI`,
and the server does the joining.

**Transfer** moves an order to another customer and takes a unit off a product's stock, both inside
one transaction, with a tickbox that makes the second save fail on the server's database, so you
can watch the rollback. Both figures are read back afterwards through a fresh context, so what you
see is the server's answer rather than the local change tracker agreeing with itself.

## In a console

Two terminals, no arguments.

```bash
# terminal 1 — the server. Creates and seeds northwind.db beside the binary.
dotnet run --project samples/Northwind.Server

# terminal 2 — the client. This process has no database and no connection string.
dotnet run --project samples/Northwind.Demo
```

It walks through a filtered query, a projection, an aggregate, lazy loading, a unit of work and a
rolled-back transaction, printing the running round-trip count as it goes. The counts are the
interesting part: touching a navigation costs a request *when it is touched*, and two edits cost
**one** save.

!!! tip "Delete `northwind.db` first if you want the transcript's exact numbers"

    The browser pages write to the same store, and the Transfer page moves an order to a different
    customer. A store that has been clicked through answers differently, which is the
    demonstration working, not the numbers being wrong.

The console client **lazy-loads normally**, unlike the browser one. It is not WebAssembly, so a
synchronous navigation getter can block on the round trip. See
[Blazor WebAssembly](../platforms/blazor-webassembly.md) for the other side of that.

## What the sample projects are

| Project | What it is |
|---|---|
| `Northwind.Shared` | The model and one `NorthwindContext`, used by both halves. |
| `Northwind.Server` | ASP.NET Core + SQLite. `app.MapInfoCarrier()` is the one route it owns; it also serves the browser client's files. |
| `Northwind.Client` | The Blazor WebAssembly client: three pages, the wire inspector, no database. |
| `Northwind.Demo` | The console client. |

The wire inspector is worth a look at as code as well as in the browser: it is an
`IInfoCarrierTransport` **decorator**, roughly thirty lines, which is all it takes to log, retry,
compress or authenticate every request. See [Custom transports](../configuration/transports.md).
