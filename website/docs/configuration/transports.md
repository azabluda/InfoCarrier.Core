# Custom transports

A transport implements synchronous and asynchronous envelope exchange:

```csharp
public interface IInfoCarrierTransport
{
    InfoCarrierEnvelope Send(InfoCarrierEnvelope request);

    Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default);
}
```

An `InfoCarrierEnvelope` is a serializable record. Get one to the server, get the answer back, and
you have a transport. HTTP is the default adapter. Another protocol needs its own client transport
and corresponding server hosting.

## Replacing registered components

Replace a serializer for one client without changing other clients:

```csharp
services.AddInfoCarrierClient("orders", o => o.WithEndpoint(ordersEndpoint))
    .ReplaceService<IInfoCarrierSerializer, OrdersSerializer>();

services.AddInfoCarrierClient("catalog", o => o.WithEndpoint(catalogEndpoint));
```

In the orders server application, select the compatible serializer on its server registration:

```csharp
var orders = services.AddInfoCarrierServer<OrdersContext>("orders")
    .ReplaceService<IInfoCarrierSerializer, OrdersSerializer>();

app.MapInfoCarrier(orders, "api/orders");
```

This works with unnamed, named, retained-handle, and typed endpoint selection. Context injection
does not change: `UseInfoCarrier("orders")` selects the client, while application code uses its context.

`ReplaceService<TService, TImplementation>()` preserves the service's lifetime, as Entity Framework's
method does. `ReplaceService<TService, TCurrentImplementation, TNewImplementation>()` targets an
original implementation. A matching targeted choice takes precedence over a contract-wide choice.
Repeated calls for the same service and original implementation use the last choice. There is no
lifetime argument or construction lambda.

Currently, client registrations support replacement of `IInfoCarrierSerializer` and
`IInfoCarrierTransport`; server registrations support `IInfoCarrierSerializer`. Other contracts
fail during configuration. This does not replace Entity Framework's internal provider services.

The host constructs replacements and supplies their constructor dependencies. No separate
`AddSingleton<OrdersSerializer>()` is needed. These components retain singleton lifetime and must
support concurrent calls. The host disposes them; a transport must not dispose its injected serializer.

For a replacement transport, select registration-local dependencies through inherited keys:

```csharp
services.AddInfoCarrierClient("orders", o => o.WithEndpoint(ordersEndpoint))
    .ReplaceService<IInfoCarrierSerializer, OrdersSerializer>()
    .ReplaceService<IInfoCarrierTransport, OrdersTransport>();

// Constructor of an application implementation of IInfoCarrierTransport:
public OrdersTransport(
    [FromKeyedServices] IInfoCarrierSerializer serializer,
    ILogger<OrdersTransport> logger)
{
    // Retain dependencies; implement Send and SendAsync for the application's protocol.
}
```

Import `Microsoft.Extensions.DependencyInjection` for the annotation. Parameterless
`[FromKeyedServices]` selects the serializer from the transport's own registration. Unannotated
dependencies use ordinary host resolution; they do not inherit its key. Do not put public registration
names in these constructors. Custom channel services can use ordinary host injection.

The client builder still implements `IHttpClientBuilder`, so existing handler configuration chains
work. A replacement transport does not activate that private HTTP client or its handler callbacks.
Use custom protocol hosting on the server; `MapInfoCarrier` remains HTTP hosting.
The registration still requires an absolute endpoint URI. Configure application channel dependencies
through host services; replacing the transport does not configure a channel automatically.

Registration limits wrap custom deserialization too. Streamed response limits include all framing.
The built-in query stream still uses JSON frames and JSON row data; replacing the envelope/payload
serializer does not replace that format. A different streaming format needs a separate adapter.
The built-in HTTP adapter also retains its `application/json` content type.

## Decorating the HTTP one

The most common reason to touch this seam is to observe or adjust every request without replacing
the transport, and a decorator does that in a few lines:

```csharp
public sealed class LoggingTransport(IInfoCarrierTransport inner, ILogger<LoggingTransport> logger)
    : IInfoCarrierTransport
{
    public InfoCarrierEnvelope Send(InfoCarrierEnvelope request)
        => SendAsync(request).GetAwaiter().GetResult();

    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();

        InfoCarrierEnvelope response = await inner.SendAsync(request, cancellationToken);

        logger.LogInformation(
            "{Operation} took {Elapsed}",
            request.Operation,
            Stopwatch.GetElapsedTime(started));

        return response;
    }
}
```

```csharp
IInfoCarrierTransport transport = new HttpInfoCarrierTransport(http, serializer);
transport = new LoggingTransport(transport, logger);

IInfoCarrierClient client = new TransportInfoCarrierClient(transport, serializer);
```

The sample's wire inspector is exactly this shape: a decorator that records the operation, the byte
counts each way, the elapsed time and the decoded payload, and shows them beside the page. It is
worth reading before you write your own.

Other things worth a decorator: a retry policy, an offline queue, compression, correlation ids,
per-request telemetry.

## In-process

For tests, or for a client and server that live in the same process, hand the envelope straight to
the server. Serializing both ways keeps the test honest, because nothing then travels by reference
that would not survive HTTP:

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.Common;

public sealed class LoopbackTransport(IInfoCarrierServer server, IInfoCarrierSerializer serializer)
    : IInfoCarrierTransport
{
    private readonly InfoCarrierEnvelopeServer _endpoint = new(server, serializer);

    public InfoCarrierEnvelope Send(InfoCarrierEnvelope request)
        => SendAsync(request).GetAwaiter().GetResult();

    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        byte[] payload = serializer.Serialize(request);
        InfoCarrierEnvelope onTheWire = serializer.Deserialize<InfoCarrierEnvelope>(payload)!;

        InfoCarrierEnvelope response = await _endpoint.DispatchAsync(onTheWire, cancellationToken);

        return serializer.Deserialize<InfoCarrierEnvelope>(serializer.Serialize(response))!;
    }
}
```

`InfoCarrierEnvelopeServer` is the part `MapInfoCarrier` uses: it checks the protocol version,
dispatches the operation, and turns a server-side failure into a fault carried in the response. Any
transport you write should hand the envelope to it rather than reimplementing that.

## A different protocol

For gRPC, a message bus or named pipes, the client half is the same shape as HTTP:

```csharp
public sealed class MyTransport(IMyChannel channel, IInfoCarrierSerializer serializer)
    : IInfoCarrierTransport
{
    public InfoCarrierEnvelope Send(InfoCarrierEnvelope request)
        => SendAsync(request).GetAwaiter().GetResult();

    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        byte[] payload = await serializer.SerializeAsync(request, cancellationToken);
        byte[] answer = await channel.CallAsync(payload, cancellationToken);

        return await serializer.DeserializeAsync<InfoCarrierEnvelope>(answer, cancellationToken)
            ?? throw new InfoCarrierTransportException("the server did not return an envelope");
    }
}
```

Three rules the HTTP transport follows and yours should:

1. Pass the cancellation token through. A cancelled request is the caller's signal.
2. Throw `InfoCarrierTransportException` for a failure of the journey: unreachable server, malformed
   answer, protocol error. Do not let a raw `JsonException` or an `HttpRequestException` surface,
   which would misreport where the fault is. A failure the server itself reported is not this; it
   arrives inside the envelope as data.
3. Include the detail in the message. The HTTP transport includes both the status code and the
   response body, because a bare status is indistinguishable from a dozen unrelated causes.

The server half is whatever hosts your protocol, ending in a call to
`InfoCarrierEnvelopeServer.DispatchAsync`.

## A different serializer

`IInfoCarrierSerializer` provides byte-array serialization/deserialization and their async counterparts,
plus stream methods with buffered defaults. Select compatible implementations on both halves through
`ReplaceService`, or pass the serializer explicitly when assembling a transport and client yourself.
Query framing and row data still use the built-in JSON format.

Registered replacements retain the role's byte limits. Direct custom composition must enforce its
own limits with `InfoCarrierPayloadLimits`. See
[Configuring the server](server.md#payload-limits).
