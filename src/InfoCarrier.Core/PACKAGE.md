`InfoCarrier.Core` is an Entity Framework Core provider for the client side of a multi-tier
application. A WPF, Blazor WebAssembly, MAUI or console client gets a real `DbContext`, but no
connection string or database driver. Queries and units of work travel to your application server,
which executes them with an ordinary EF Core provider.

Reference it from both applications. The server also references `InfoCarrier.Core.AspNetCore`.
Both applications share the `DbContext` and entity source.

Both halves need .NET 10 and EF Core 10. Install with
`dotnet add package InfoCarrier.Core`.

## Usage

Build a client over a transport, then call `UseInfoCarrier` to choose the InfoCarrier provider for
your `DbContext`. For example:

```csharp
// A WPF, Blazor WebAssembly, MAUI or console app.
var serializer = new SystemTextJsonInfoCarrierSerializer();
var http = new HttpClient { BaseAddress = new Uri("https://your-app-server") };

IInfoCarrierClient client = new TransportInfoCarrierClient(
    new HttpInfoCarrierTransport(http, serializer), serializer);

DbContextOptions options = new DbContextOptionsBuilder<ShopContext>()
    .UseInfoCarrier(client, remote => remote.UseRegisteredAnonymousShapes(
        new { Id = 0, CustomerId = (string?)null }))
    .Options;

await using var context = new ShopContext(options);
```

Register the same anonymous structures on the server:

```csharp
services.AddInfoCarrierAnonymousShapes(
    new { Id = 0, CustomerId = (string?)null });
```

With neither declaration, or only the server declaration, eligible projections are rebuilt on the
client. A client-only declaration reaches the server and is refused. Matching declarations let the
server provider translate or refuse the query. A refusal returns to the caller without a client
evaluation retry.

`client` is safe to share: it holds no mutable state, so one instance serves every `DbContext` in
your application. `MapInfoCarrier()` and the transport both default to the route `infocarrier`,
so the `BaseAddress` above needs no path.

Everything after that is ordinary EF Core. The server executes the database query. The client can
rebuild an eligible final projection from returned values.
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
