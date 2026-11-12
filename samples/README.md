# Northwind samples

The Northwind samples are a SQLite server with browser and console clients.
This checkout uses version 11 development code and Entity Framework Core 11.
For the published sample, use tag `v10.3.0` and its matching instructions.

## Run

From the repository root:

```powershell
dotnet run --project samples/Northwind.Server
```

Open <http://localhost:5199> for the browser.
In another terminal, run the console client:

```powershell
dotnet run --project samples/Northwind.Demo
```

The server creates and seeds `northwind.db` beside its binary.
Edits persist. Stop the server before deleting that sample database to reset it.

The browser's Customers, Order, and Transfer pages demonstrate queries, related data, saves,
and transactions. The inspector shows operations, sizes, elapsed time, and decoded payloads.
The console also demonstrates lazy loading, which browser HTTP cannot support synchronously.

## Read more

- [Sample guide](../website/docs/applications/northwind-sample.md)
- [Browser setup](../website/docs/applications/blazor-webassembly.md)
- [Custom transports](../website/docs/extending/transports.md)
- [First application](../website/docs/getting-started/first-app.md), using the smaller Shop model

Publish the browser sample with `dotnet publish samples/Northwind.Client -c Release`.
Test your own model after trimming; a successful build alone does not verify every runtime path.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
