# Replace serialization

A serializer converts envelopes and operation payloads to bytes and back.
The built-in JSON serializer is sufficient for the normal application setup.
Replace it when your application requires a different message format or serializer behavior.
Use compatible implementations on the client and server.

`IInfoCarrierSerializer` requires synchronous and asynchronous byte-array methods.
It also has stream methods with buffered defaults. Override those when your implementation can
read or write incrementally.

## A complete wrapper

This wrapper keeps the built-in format and logs serialization calls.
Use it to learn the interface before changing formats:

```csharp
using InfoCarrier.Core;
using Microsoft.Extensions.Logging;

public sealed class LoggingSerializer(ILogger<LoggingSerializer> logger) : IInfoCarrierSerializer
{
    private readonly IInfoCarrierSerializer inner = new SystemTextJsonInfoCarrierSerializer();

    public byte[] Serialize<T>(T value)
    {
        logger.LogDebug("Serializing {Type}", typeof(T).Name);
        return inner.Serialize(value);
    }

    public T? Deserialize<T>(byte[] payload) => inner.Deserialize<T>(payload);

    public ValueTask<byte[]> SerializeAsync<T>(
        T value, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Serializing {Type}", typeof(T).Name);
        return inner.SerializeAsync(value, cancellationToken);
    }

    public ValueTask<T?> DeserializeAsync<T>(
        byte[] payload, CancellationToken cancellationToken = default)
        => inner.DeserializeAsync<T>(payload, cancellationToken);
}
```

Select it through [service replacement](services.md):

```csharp
builder.Services.AddInfoCarrierClient(o =>
    o.WithEndpoint("https://api.example.com/infocarrier"))
    .ReplaceService<IInfoCarrierSerializer, LoggingSerializer>();
```

Select the same compatible implementation on the server builder.
The host supplies constructor dependencies and owns disposal.

## What a custom format must cover

| Content | Who encodes it |
|---|---|
| Envelopes and operation payloads | Your selected `IInfoCarrierSerializer` |
| Protocol 4 query expression objects and buffered result rows | Your selected serializer, as part of those payloads |
| Historical protocol 3 requests and responses | Your serializer must handle these too if your host serves protocol 3 |
| Streamed query messages | Fixed built-in JSON format; replacing the envelope serializer does not replace it |
| Application values inside save data | Follow value-mapping and payload contracts; these are not a replacement for every nested representation |

Set `MediaType` on both role options when using a different HTTP payload format.
The default is `application/json`. Stream responses keep their JSON content type.
A different stream format needs its own adapter.

Registered replacements keep that registration's byte limits.
Direct composition must enforce its own `InfoCarrierPayloadLimits`.
Custom formats must preserve version identification and the promised behavior of supported clients.
See [compatibility](../reference/compatibility.md) and [limits](../reference/options.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
