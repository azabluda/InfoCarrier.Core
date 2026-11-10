# Run the samples

Northwind contains one shared model, a SQLite server, a browser client, and a console client.
The Northwind model is larger than the Shop model used in the tutorial.

## Choose the source version

These instructions use the development checkout:

```powershell
git clone https://github.com/azabluda/InfoCarrier.Core.git
cd InfoCarrier.Core
```

For published 10.3 behavior, clone its tag instead in a separate working location you choose:

```powershell
git clone --branch v10.3.0 https://github.com/azabluda/InfoCarrier.Core.git InfoCarrier-10.3
```

Do not combine an older sample with new setup instructions.
The [installation page](installation.md) explains development and published packages.

## Browser

```powershell
dotnet run --project samples/Northwind.Server
```

Open <http://localhost:5199>. The server serves the browser files from the same origin.

| Page | What to try |
|---|---|
| Customers | Filter, sort, and page. Inspect the query sent for each action. |
| Order | Load details, change quantities, and save several edits in one request. |
| Transfer | Move an order and change stock inside a transaction. Select the failure option to observe rollback. |

The request inspector shows operations, byte counts, elapsed time, and decoded payloads.
Current queries contain typed expression data; do not assume every query is an embedded base64 string.
Inspect private data only in a development environment.

## Console

Keep the server running. In another terminal:

```powershell
dotnet run --project samples/Northwind.Demo
```

The console prints queries, projections, aggregates, relationship loads, saves, and a rolled-back transaction.
It also counts requests. A navigation load costs another request; a batch of edits can use one save.

The server creates and seeds `northwind.db` beside its binary.
Browser edits persist and change later console output.
Stop the server before deleting that sample database to reset it.

## Project roles

| Project | Purpose |
|---|---|
| `Northwind.Shared` | Entity classes, context, and shared settings |
| `Northwind.Server` | ASP.NET Core hosting, SQLite, and database setup |
| `Northwind.Client` | Blazor WebAssembly pages and request inspector |
| `Northwind.Demo` | Console client |

[Open in Codespaces](https://codespaces.new/azabluda/InfoCarrier.Core?quickstart=1) for the repository's
development environment. See [browser constraints](blazor-webassembly.md) before adapting the browser client.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
