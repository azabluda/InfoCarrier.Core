# Write transports and custom hosts

Use the built-in HTTP transport for ordinary client/server calls.
Configure its headers, handlers, and timeout through [client configuration](../applications/configuration/client.md#configure-http).
Write a transport when you need another protocol or a wrapper around request exchange.
Another protocol needs both a client transport and a server host.
A transport moves an envelope, which contains the operation, version, payload, and any reported fault.

```csharp
public interface IInfoCarrierTransport
{
    InfoCarrierEnvelope Send(InfoCarrierEnvelope request);
    Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default);
}
```

Import `InfoCarrier.Core` and `InfoCarrier.Core.Common`.
Implement a real synchronous path if the protocol supports it.
Do not hide an asynchronous request behind `.Result` in user-interface code.

## Decorate an existing transport

This complete wrapper records successful calls while keeping the original synchronous path:

```csharp
using System.Diagnostics;
using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using Microsoft.Extensions.Logging;

public sealed class LoggingTransport(
    IInfoCarrierTransport inner, ILogger<LoggingTransport> logger) : IInfoCarrierTransport
{
    public InfoCarrierEnvelope Send(InfoCarrierEnvelope request)
    {
        long started = Stopwatch.GetTimestamp();
        InfoCarrierEnvelope response = inner.Send(request);
        logger.LogInformation("{Operation} took {Elapsed}",
            request.Operation, Stopwatch.GetElapsedTime(started));
        return response;
    }

    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();
        InfoCarrierEnvelope response = await inner.SendAsync(request, cancellationToken);
        logger.LogInformation("{Operation} took {Elapsed}",
            request.Operation, Stopwatch.GetElapsedTime(started));
        return response;
    }
}
```

For manual composition:

```csharp
var serializer = new SystemTextJsonInfoCarrierSerializer();
IInfoCarrierTransport transport = new HttpInfoCarrierTransport(http, serializer);
transport = new LoggingTransport(transport, logger);
IInfoCarrierClient client = new TransportInfoCarrierClient(transport, serializer);
```

This wrapper implements envelope exchange only. It does not forward the streaming interface.
Implement `IInfoCarrierStreamTransport` too if progressive query delivery must be preserved.

## Host the server side

Resolve the complete selected pair from the host:

```csharp
InfoCarrierServerEndpoint selected = app.Services.GetInfoCarrierServerEndpoint("orders");
var dispatcher = selected.CreateEnvelopeServer();
```

The same resolver accepts a retained server builder, a unique context type through
`GetInfoCarrierServerEndpoint<ShopContext>()`, or no argument for the unnamed default.
Missing selections and ambiguous context types fail. The pair borrows host-owned services and exposes
`MaxRequestBytes` and `MediaType`. Enforce the request limit before buffering custom protocol input.
Dispatch each decoded envelope through `dispatcher.DispatchAsync`. A custom protocol adapter owns its
framing, authentication, cancellation, and transport measurements.

Advanced consumers can resolve a client with `provider.GetInfoCarrierClient("orders")`.
Typical application code only injects its context or context factory.

The selected dispatcher provides both `Dispatch` and `DispatchAsync`.
Preserve cancellation and protocol identification. Do not interpret or execute arbitrary payloads yourself.

For direct construction outside role registration, register a base `DbContext` alias resolving
your concrete server context. Pass `InfoCarrierServerOptions` to
`new InProcessInfoCarrierServer(provider, options)`.
Its settings are copied at construction. Configure the serializer's payload limits separately.
Dispose the server and the service provider you created.

## Failure and security rules

- Pass cancellation through. Preserve `OperationCanceledException`.
- Use `InfoCarrierTransportException` for connection or malformed-response failures.
- Keep a reported server fault inside the response envelope.
- Report incompatible protocols separately using `InfoCarrierProtocolException`.
- Enforce byte limits before buffering custom input.
- Implement authentication and authorization in your host. The dispatcher does not add them.
- Do not automatically retry writes. A lost response can follow a successful commit.

See [compatibility](../reference/compatibility.md), [serialization](serialization.md),
[service replacement](services.md), and [testing without a network](testing.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
