# Configure a client

Register the client once, then give each screen or operation its own context:

```csharp
using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;

builder.Services.AddInfoCarrierClient(o => o
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureInfoCarrier));

builder.Services.AddDbContextFactory<ShopContext>(o => o.UseInfoCarrier());
```

This example assumes the [shared model](../../getting-started/first-app.md) and
[shared configuration method](shared.md).
Inject `IDbContextFactory<ShopContext>` into the code that needs data.
Dispose each context after the operation. Contexts are not thread-safe.

`UseInfoCarrier()` selects the unnamed client. It does not select a named client automatically.
Registration does not create a context, so add the context or factory separately.
Pooling and ordinary `AddDbContext` registrations are also supported.

## Configure HTTP

The returned builder supports the usual HTTP client configuration:

```csharp
builder.Services.AddInfoCarrierClient(o =>
    o.WithEndpoint("https://api.example.com/infocarrier"))
    .ConfigureHttpClient(http => http.Timeout = TimeSpan.FromSeconds(30));
```

Add authentication headers or handlers through that builder.
The endpoint must be an absolute address, including the route mapped on the server.
Keep credentials out of source code. See [security](../../operations/security.md).

Set `MaxResponseBytes` when the application needs a response-size limit.
The default is unlimited. A stream can exceed the limit after some rows have arrived.
See [options](../../reference/options.md) and [streaming](../streaming.md).

## Create a client directly

For a console application or another application that owns an `HttpClient`:

```csharp
using var http = new HttpClient();
IInfoCarrierClient client = InfoCarrierClient.Create(http, o => o
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureInfoCarrier));

var options = new DbContextOptionsBuilder<ShopContext>()
    .UseInfoCarrier(client).Options;
```

The caller owns and disposes `http`.
For a custom client, `InfoCarrierClient.Configure(client, configure)` applies the shared settings.
`UseInfoCarrier(client, configure)` is also available when building context options directly.
Do not mutate provider internals to change deployment settings.

## Synchronous calls

Built-in synchronous calls use synchronous transport and server methods.
They block the calling thread while waiting for the response.
Use asynchronous methods in desktop user-interface code and in browser applications.
Browser HTTP does not support synchronous requests.

## Related tasks

- [Shared settings](shared.md) for types, anonymous structures, value conversion, and SQL permission.
- [Multiple endpoints](multiple-endpoints.md) for names and independent context factories.
- [Document databases](document-stores.md) for non-relational query rules.
- [Diagnostics](../../operations/diagnostics.md) for logs and request counts.
- [Service replacement](../../extending/services.md) for custom implementations.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
