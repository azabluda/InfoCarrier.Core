`InfoCarrier.Core` is an Entity Framework Core provider for the client side of a multi-tier
application. A WPF, Blazor WebAssembly, MAUI or console client gets a real `DbContext`, but no
connection string or database driver. Queries and units of work travel to your application server,
which executes them with an ordinary EF Core provider.

Reference it from both applications. The server also references `InfoCarrier.Core.AspNetCore`.
Both applications share the `DbContext` and entity source.

Both halves need .NET 10 and EF Core 10. Install with
`dotnet add package InfoCarrier.Core`.

## Usage

Create the default HTTP client, then call `UseInfoCarrier` to choose the InfoCarrier provider for
your `DbContext`. For example:

```csharp
// A WPF, Blazor WebAssembly, MAUI or console app.
var http = new HttpClient();
IInfoCarrierClient client = InfoCarrierClient.Create(http, options => options
    .WithEndpoint("https://your-app-server/infocarrier")
    .ConfigureInfoCarrier());

DbContextOptions options = new DbContextOptionsBuilder<ShopContext>()
    .UseInfoCarrier(client)
    .Options;

await using var context = new ShopContext(options);

public static class ShopDeployment
{
    public static TBuilder ConfigureInfoCarrier<TBuilder>(this TBuilder builder)
        where TBuilder : InfoCarrierDeploymentBuilder
        => builder.AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null });
}
```

Use the same method when registering the server role:

```csharp
services.AddInfoCarrierServer<ShopContext>(options => options.ConfigureInfoCarrier());
```

Reference `InfoCarrier.Core.Abstractions` from the project containing the shared method. The method
can also register named types, custom value mappers, and arbitrary SQL access.
Matching settings guide query planning. They do not grant translation support to the server's
database provider. The server remains the admission boundary. A refusal returns without a client
evaluation retry.

`client` is safe to share: it holds no mutable state, so one instance serves every `DbContext` in
your application. `MapInfoCarrier()` defaults to the route `infocarrier`, which is included in the
absolute client endpoint above.

Everything after that is ordinary EF Core. The server executes the database query. The client can
rebuild an eligible final projection from returned values.

Asynchronous, non-tracking queries stream rows as the server produces them. `Where`, `Select`,
`Skip`, `Take`, and `Distinct` can also run incrementally after a query split. Tracked queries,
single-result operations, and operators that need the complete input remain buffered. Disposing
an asynchronous enumerator early cancels the response and releases the server query.

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
