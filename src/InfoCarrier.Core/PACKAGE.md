`InfoCarrier.Core` is an Entity Framework Core provider for the client side of a multi-tier
application. Your client gets a real `DbContext` with LINQ, change tracking, the identity map,
navigation fix-up, lazy loading and transactions, but no connection string and no database driver.
Queries and units of work travel to your application server, which executes them with an ordinary
EF Core provider.

Reference it from your client, and from your server alongside `InfoCarrier.Core.AspNetCore`,
which depends on it. The `DbContext` and the entity classes are shared source, so both halves build
the same model.

Both halves need .NET 10 and EF Core 10. Install with
`dotnet add package InfoCarrier.Core`.

## Usage

Build a client over a transport, then call `UseInfoCarrier` to choose the InfoCarrier provider for
your `DbContext`. For example:

```csharp
var serializer = new SystemTextJsonInfoCarrierSerializer();
var http = new HttpClient { BaseAddress = new Uri("https://your-app-server") };

IInfoCarrierClient client = new TransportInfoCarrierClient(
    new HttpInfoCarrierTransport(http, serializer), serializer);

DbContextOptions options = new DbContextOptionsBuilder<ShopContext>()
    .UseInfoCarrier(client)
    .Options;

await using var context = new ShopContext(options);
```

`client` is safe to share: it holds no mutable state, so one instance serves every `DbContext` in
your application. `MapInfoCarrier()` and the transport both default to the route `infocarrier`,
so the `BaseAddress` above needs no path.

Everything after that is ordinary EF Core. A query is not evaluated on the client: it crosses the
wire as an expression tree, and the server runs it against its own provider.
`HttpInfoCarrierTransport` is in this package; to serve what it sends, add
[InfoCarrier.Core.AspNetCore](https://www.nuget.org/packages/InfoCarrier.Core.AspNetCore) to your
server.

## Additional documentation

[Your first client and server](https://azabluda.github.io/InfoCarrier.Core/getting-started/first-app/)
builds a working pair in one page, and the
[documentation site](https://azabluda.github.io/InfoCarrier.Core/) covers querying, saving changes,
transactions, custom transports and Blazor WebAssembly. The
[limitations page](https://azabluda.github.io/InfoCarrier.Core/limitations/) lists every scenario
that behaves differently here from another EF Core provider. Read it before you adopt.

## Feedback

If you encounter a bug, have a question, or would like to request a feature,
[open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
