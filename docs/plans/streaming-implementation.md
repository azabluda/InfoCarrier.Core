# Streaming query implementation plan

Status: proposed implementation plan for [issue #45](https://github.com/azabluda/InfoCarrier.Core/issues/45).

This plan starts from the accepted research in [streaming-research.md](streaming-research.md).
It does not preserve the version 10 transport, serializer, envelope, or result contracts.

## Outcome

A query is one streamed exchange from request serialization through Entity Framework
materialization. No common query path builds the complete provider result, wire-node list,
serialized payload, envelope payload, or HTTP response body.

Commands remain unary because their results are small and atomic. They use the same stream-based
request and response infrastructure, without the query frame sequence.

## Public contract replacement

Replace `IInfoCarrierTransport.SendAsync(InfoCarrierEnvelope, ...)` with an exchange contract:

```csharp
public interface IInfoCarrierTransport
{
    ValueTask<IInfoCarrierResponse> SendAsync(
        InfoCarrierOperation operation,
        Func<Stream, CancellationToken, ValueTask> writeRequest,
        CancellationToken cancellationToken = default);
}

public interface IInfoCarrierResponse : IAsyncDisposable
{
    Stream Content { get; }
}
```

The callback writes directly to the request stream. The response owns its stream and cancellation
lifetime. Disposing it is the required early-exit signal.

Replace every byte-array serializer member with stream members:

```csharp
public interface IInfoCarrierSerializer
{
    ValueTask SerializeAsync<T>(
        Stream destination,
        T value,
        CancellationToken cancellationToken = default);

    ValueTask<T?> DeserializeAsync<T>(
        Stream source,
        CancellationToken cancellationToken = default);
}
```

Delete the synchronous methods and both asynchronous methods returning or accepting `byte[]`.
Synchronous query execution uses a separate synchronous transport capability. It does not block
the asynchronous contract.

`IInfoCarrierClient.QueryDataAsync` becomes a streamed query operation. It returns an owned
asynchronous result sequence, not `QueryDataResult`. Save, transaction, and savepoint members keep
typed unary results.

Delete these query response containers:

- `InfoCarrierEnvelope.Payload` from the query path;
- `QueryDataResult.SerializedResults`;
- `QueryDataResult.ServerLog`;
- any adapter whose only purpose is rebuilding those buffers.

## Wire protocol

The response is one compact tagged JSON array. Its frames are:

| Tag | Frame | Payload |
|---:|---|---|
| 0 | Start | Protocol version, result type, tracking mode, and result-slot identity. |
| 1 | Row | One `DynamicValueNode`. |
| 2 | Log | One ordered forwarded log event. |
| 3 | Complete | Successful terminal state and final counters. |
| 4 | Fault | Serialized server failure. |

The writer owns one `Utf8JsonWriter` for the complete response. It must not call a general-purpose
serializer wrapper once per row. The real-node benchmark measured about 302 allocated bytes per
such call. A dedicated frame codec writes tags and delegates node fields directly.

Reference identifiers have result-stream scope. The server mapper resets before the start frame,
not before each row. The client reference table has the same lifetime. This is required when two
tracked rows share an entity, including many-to-many navigations.

The start frame declares the protocol version. Unknown versions or tags fail before yielding a
row. A fault frame throws from the next `MoveNextAsync`. End-of-stream without a terminal frame is
a protocol failure.

## Execution and buffering policy

Buffering belongs to an operator, not the transport.

| Query shape | First-row policy | Required buffering |
|---|---|---|
| One server query with no residual | Yield each materialized row. | None. |
| Row-local `Where` or `Select` residual | Apply per row and yield. | None. |
| `Skip` or `Take` residual | Maintain counters and yield. | Constant state. |
| `Distinct` residual | Yield new values. | Seen-key set. |
| Residual ordering, grouping, reverse, or aggregate | Wait for completion. | One result slot. |
| Split concatenation | Drain slots in declared order. | Current slot only. |
| Split join or correlation | Buffer the build side. | The required side only. |
| Arbitrary residual not classified as streaming | Wait for completion. | All referenced slots. |

The query splitter must emit a client execution plan with explicit operator capabilities. It must
not compile one opaque synchronous residual and force every query through `List<object?>`.

Each split result has a slot identifier in its start frame. The plan decides whether that slot can
flow, needs bounded state, or must complete. Later slots can start only when the plan permits it.

## Tracked materialization

Tracked rows follow plain Entity Framework behavior. Relationship fixup can change earlier
entities while later rows arrive. The provider does not hold rows merely to present final
navigation state on the first yield.

An included collection is complete when the backing provider yields its root row. The frame mapper
then writes that loaded collection and its join-row metadata. Shared entities in later frames use
the result-stream reference scope.

Client materialization owns one mapper hook and one reference scope until completion or disposal.
Join rows can be attached when their owning row arrives. Deferred work that needs another frame is
kept as bounded pending state and checked at completion.

## Lifetime and failure rules

The server request scope owns the context, provider enumerator, active reader, and response writer.
It remains alive until completion, failure, cancellation, or response disposal.

The client enumerator owns `IInfoCarrierResponse`. Its disposal closes the response stream. HTTP
disconnect cancellation then disposes the provider enumerator and context. The SQLite research
probe confirms that this closes the active connection.

The server writes a fault frame for an ordinary execution failure when the response remains
writable. A disconnect needs no fault frame. The client reports either failure through Entity
Framework query logging exactly once.

`MaxResponseBytes` becomes a counting stream limit. It stops writing immediately after the limit.
It does not buffer the response to determine its size.

## HTTP requirements

The HTTP client uses `ResponseHeadersRead`. The WebAssembly client also sets
`WebAssemblyEnableStreamingResponse`. The browser research showed buffering when either setting
was absent.

The server must flush after the start frame and first row. Later flushing uses a byte or time
threshold. Flushing every small row would trade memory savings for excessive network overhead.

## Implementation sequence

### 1. Frame codec

Implement the tagged reader and writer with source-generated node metadata. Pin malformed input,
unknown versions, unknown tags, missing terminal frames, trailing faults, logs, and size limits.

Success condition: a real node round trip adds no per-row serializer-wrapper allocation.

### 2. Stream transport

Replace the envelope transport and implement HTTP plus in-process exchanges. Add the browser
streaming option. Prove response disposal cancellation through both transports.

Success condition: the client reads the first frame while the server remains inside enumeration.

### 3. Server producer

Change `ServerQueryExecutor` from `List<object?>` production to an asynchronous row producer.
Preserve one mapper reference scope, tracking probes, navigation state, join rows, logs, and faults.

Success condition: provider rows become frames without an intermediate result list.

### 4. Client materializer

Materialize one row frame at a time. Keep identity resolution, relationship fixup, deferred join
rows, logging, and critical-section ownership valid through enumerator disposal.

Success condition: pass-through queries yield before the server completes.

### 5. Residual execution plan

Replace the opaque residual with classified streaming operators. Start with identity, projection,
filtering, skip, and take. Keep a conservative buffered node for all unclassified residuals.

Success condition: classification changes timing and memory, never answers.

### 6. Split queries

Add result slots and explicit dependencies. Stream independent and concatenated slots. Buffer only
the side required by joins, correlations, ordering, grouping, or aggregates.

Success condition: every slot states why it streams or buffers.

### 7. Remove old contracts

Delete the query envelope payload, serialized-result property, byte-array serializer methods, and
compatibility adapters. Update the public API baseline and migration documentation in the same
change.

## Verification gates

Each implementation slice must pass:

1. The complete provider specification suite.
2. The complete transport suite.
3. Browser first-byte verification.
4. Cancellation with connection closure.
5. Fault-after-row behavior.
6. Tracked include and many-to-many reference-scope tests.
7. Small and large response measurements.

Measure response bytes, first-row latency, completion time, client peak memory, server peak memory,
and allocations. Compare each slice with the committed version 10 baseline and the preceding
version 11 slice.

## Pull-request boundaries

Use small vertical changes despite the feature's size:

1. Protocol codec and executable format tests.
2. Stream exchange contract and transport implementations.
3. Server provider-to-frame production.
4. Client frame-to-entity materialization.
5. Streaming residual operators.
6. Split-query dependencies.
7. Old-contract removal, public API baseline, and migration documentation.

No pull request ships a compatibility adapter. Temporary adapters can exist only inside an
unmerged branch and must disappear before its validation.
