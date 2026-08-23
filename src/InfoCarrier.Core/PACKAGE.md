`InfoCarrier.Core` is an Entity Framework Core provider for the client side of a multi-tier
application. Your client gets a real `DbContext` with LINQ, change tracking, the identity map,
navigation fix-up, lazy loading and transactions, but no connection string and no database driver.
Queries and units of work travel to your application server, which executes them with an ordinary
EF Core provider.

Reference this package from your client and from your server. The `DbContext` and the entity
classes are shared source, so both halves build the same model.

Use the `--version` option to install a 10.0 preview. Without it, NuGet resolves the newest stable
release, which belongs to the earlier 3.1 line and is not compatible with this one.

## Usage

Call the `UseInfoCarrier` method to choose the InfoCarrier provider for your `DbContext`, passing
a client built over a transport. For example:

```csharp
var serializer = new SystemTextJsonInfoCarrierSerializer();
var http = new HttpClient { BaseAddress = new Uri("https://your-app-server") };

IInfoCarrierClient client = new TransportInfoCarrierClient(
    new HttpInfoCarrierTransport(http, serializer), serializer);

protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.UseInfoCarrier(client);
```

Everything after that is ordinary EF Core. A query is not evaluated on the client: it crosses the
wire as an expression tree, and the server runs it against its own provider.

`HttpInfoCarrierTransport` is in this package. To serve the requests it sends, add
[InfoCarrier.Core.AspNetCore](https://www.nuget.org/packages/InfoCarrier.Core.AspNetCore) to your
server.

## Getting started

See [Your first client and server](https://azabluda.github.io/InfoCarrier.Core/getting-started/first-app/),
which builds a working pair in one page.

## Additional documentation

See the [documentation site](https://azabluda.github.io/InfoCarrier.Core/) for querying, saving
changes, transactions, custom transports and Blazor WebAssembly. The
[limitations page](https://azabluda.github.io/InfoCarrier.Core/limitations/) lists every scenario
that behaves differently here from another EF Core provider, with a worked example for each. Read
it before you adopt.

## Feedback

If you encounter a bug, have a question, or would like to request a feature,
[open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
