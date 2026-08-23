# Custom transports

A transport is one interface with one method:

```csharp
public interface IInfoCarrierTransport
{
    Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default);
}
```

An `InfoCarrierEnvelope` is a serializable record. Get one to the server, get the answer back, and
you have a transport. HTTP is the default because it is what most applications want. Nothing in the
library depends on it.

## Decorating the HTTP one

The most common reason to touch this seam is to observe or adjust every request without replacing
the transport, and a decorator does that in a few lines:

```csharp
public sealed class LoggingTransport(IInfoCarrierTransport inner, ILogger<LoggingTransport> logger)
    : IInfoCarrierTransport
{
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

For gRPC, a message bus, WCF or named pipes, the client half is the same shape as HTTP:

```csharp
public sealed class MyTransport(IMyChannel channel, IInfoCarrierSerializer serializer)
    : IInfoCarrierTransport
{
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

`IInfoCarrierSerializer` is four methods over a `byte[]`: `Serialize`, `Deserialize` and their async
counterparts. The library never assumes JSON. Implement it for MessagePack or protobuf if the
payload size matters to you, and register the same implementation on both halves.

The one thing to keep is the size bound: a deserializer that will parse anything it is handed is
what `InfoCarrierPayloadLimits` exists to prevent. See
[Configuring the server](server.md#payload-limits).
