# Run the samples

The repository carries a Northwind sample: one shared model, one SQLite-backed server, and the
same client twice, as a browser app and as a console app. Neither client has a connection string.

```bash
git clone https://github.com/azabluda/InfoCarrier.Core.git
cd InfoCarrier.Core
```

## In a browser

One terminal, one command. The server hosts the client's files, so there is one origin and no CORS.

```bash
dotnet run --project samples/Northwind.Server
```

Open <http://localhost:5199>. A wire inspector down the right-hand side shows every round trip:
the operation, the size each way, how long it took, and the decoded payload, including the
expression tree unpacked out of the base64 it travels in.

Three ordinary pages:

*Customers* is a grid you sort, filter and page. There is no *Run* button, because the sort, the
filter and the paging are already part of the expression tree the page sends. Filtering `Country`
for *Germany* takes 65 rows to 8, and the panel shows the `Where` crossing the wire.

*Order* is master-detail. Several quantity edits accumulate in one change tracker and leave as one
`SaveChanges`, and the master list is a projection over a join the server performs.

*Transfer* moves an order to another customer and takes a unit off a product's stock inside one
transaction. A tickbox makes the second save fail on the server's database, so you can watch the
rollback, and both figures are read back through a fresh context afterwards.

## In a console

Two terminals.

```bash
# terminal 1: the server. Creates and seeds northwind.db beside the binary.
dotnet run --project samples/Northwind.Server

# terminal 2: the client. This process has no connection string.
dotnet run --project samples/Northwind.Demo
```

It walks through a filtered query, a projection, an aggregate, lazy loading, a unit of work and a
rolled-back transaction, printing the running round-trip count as it goes. The counts are the
interesting part: touching a navigation costs a request when it is touched, and two edits cost one
save. Delete `northwind.db` first for the transcript's exact numbers.

This client lazy-loads normally, unlike the browser one. It is not WebAssembly, so a synchronous
navigation getter can block on the round trip. See
[Blazor WebAssembly](../platforms/blazor-webassembly.md) for the other side of that.

## What the sample projects are

| Project | What it is |
|---|---|
| `Northwind.Shared` | The model and one `NorthwindContext`, used by both halves. |
| `Northwind.Server` | ASP.NET Core and SQLite. `app.MapInfoCarrier()` is the one route it owns, and it also serves the browser client's files. |
| `Northwind.Client` | The Blazor WebAssembly client: three pages and the wire inspector. |
| `Northwind.Demo` | The console client. |

The wire inspector is worth reading as code. It is an `IInfoCarrierTransport` decorator of about
thirty lines, which is all it takes to log, retry, compress or authenticate every request. See
[Custom transports](../configuration/transports.md).
