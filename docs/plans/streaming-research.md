# Streaming research

Status: investigation for the 11.0.0 milestone. Research does not promise implementation.

The parked `park/d7-streaming-half` branch is evidence, not an implementation base. Its useful
results and failed approaches must be checked again against current `main`. No production design
is accepted until the measurements below compare the current buffered path with isolated
prototypes.

[Issue #45](https://github.com/azabluda/InfoCarrier.Core/issues/45) records the decision and already
belongs to the 11.0.0 milestone. This document turns that decision into research questions without
committing to gRPC or a specific streaming design.

## Clean-slate premise

Version 10 contracts are evidence, not constraints. Version 11 may replace transport, serializer,
result, and wire types together. The design must optimize the complete provider-to-consumer flow,
not preserve a migration path for existing implementations.

The investigation starts from observable Entity Framework behavior:

- asynchronous enumeration can yield values before the complete query succeeds;
- an exception after earlier values is an enumeration failure, not a protocol violation;
- tracking and relationship fixup can evolve while enumeration continues;
- cancellation and early disposal must stop the provider and release every owned resource;
- buffering remains valid only where query semantics require the complete input.

No byte-array method, envelope shape, result container, or operation split is presumed to survive.

## Question

Which buffering layers should be removed, for which query shapes, and what measured benefit pays
for the protocol and lifetime costs?

"Streaming" is not one feature. It can mean:

1. the HTTP response starts before the server finishes producing its response;
2. the server does not retain all provider rows;
3. the client does not retain the complete response body;
4. the client does not retain all wire rows;
5. Entity Framework yields a materialized row before the complete remote result exists.

Each level has different benefits and different correctness constraints. A prototype must name
which level it implements.

## Current buffering boundaries

The current query path buffers at these distinct boundaries:

| Boundary | Current representation | Why it exists today |
|---|---|---|
| Provider enumeration | `ArrayList` in `ServerQueryExecutor.Buffer` | The server change tracker must be complete before row metadata is mapped. |
| Wire mapping | `List<DynamicValueNode>` | Rows are mapped after provider enumeration. |
| Inner result payload | `QueryDataResult.SerializedResults` as `byte[]` | The complete row list is one JSON value. |
| Envelope payload | `InfoCarrierEnvelope.Payload` as `byte[]` | Every operation uses one request-response envelope. |
| HTTP response | `ReadAsByteArrayAsync` | The transport deserializes one complete envelope. |
| Client wire decode | `List<DynamicValueNode>` | `ClientResultMaterializer` deserializes all row nodes first. |
| Client materialization | `List<TElement>` | The mapper hook and relationship reconstruction require one protected materialization scope. |

The two nested `byte[]` properties also become nested base64 strings in JSON. Removing one copy is
not equivalent to removing the next one.

## Current correctness constraints

The investigation must preserve these behaviors:

- Tracked results can gain navigation and join-row state from later provider rows.
- No-tracking results do not require a complete server state manager.
- Client identity resolution and `AttachJoinRows` currently see the complete row set.
- A nested lazy-load exchange must not replace the outer mapper hook.
- Client residual operators can require every remote row before producing one result.
- A split query can require several complete server results before residual evaluation.
- A server exception can occur before enumeration, during enumeration, or after some rows.
- Forwarded server logs are collected after query execution and replayed before client results.
- Cancellation must reach the provider command and release the server context.
- An abandoned enumeration must release the HTTP response, server context, and store connection.
- `MaxResponseBytes` must still reject an oversized response without first buffering it.
- Synchronous Entity Framework enumeration cannot block on asynchronous network input in runtimes
  that prohibit blocking.

## Evidence retained from the parked branch

The old work established several facts worth retesting:

- HTTP streaming does not require gRPC.
- The old browser probe attributed streaming to `ResponseHeadersRead`.
- A transport-handler timing probe could pass against a deliberately buffered response.
- The useful probe observed the transport returning while the server had produced zero rows.
- Removing provider buffering for tracked queries produced incorrect navigation state.
- Restricting provider streaming to no-tracking queries avoided that specific defect.
- A top-level tagged JSON array permitted source-generated incremental reading.
- A trailing fault was safe only while the client still buffered before yielding rows.
- The work changed public transport, serializer, and result contracts.
- It never measured response bytes, peak memory, first-row latency, or throughput.

These are hypotheses and test designs for the new work. They are not permission to restore the old
implementation.

## Workloads

Every prototype must run the same workload matrix:

| Workload | Purpose |
|---|---|
| Small scalar projection | Shows fixed overhead and prevents optimizing only giant responses. |
| Large flat no-tracking projection | Best case for provider-to-caller streaming. |
| Large tracked entity query | Measures wire-only streaming without weakening tracker semantics. |
| Tracked include and many-to-many query | Detects incomplete navigation and join metadata. |
| Query with a client residual | Shows when early rows cannot reach the caller. |
| Query split into several server queries | Shows cross-result buffering requirements. |
| Failure after some provider rows | Defines fault timing and partial-result behavior. |
| Cancellation and early enumeration exit | Proves resource release. |
| Forwarded server logging enabled | Defines when logs become visible. |
| Browser HTTP transport | Checks the runtime-specific response behavior. |

Use one ordinary result and one result large enough to make memory differences visible. The old
560 MB specification result is evidence that such shapes exist, but it is not itself a benchmark.

## Measurements

Measure these independently on current `main` and each prototype:

1. Response bytes on the wire.
2. Peak working set for client and server processes separately.
3. Time to response headers.
4. Time to the first materialized row.
5. Time to complete enumeration.
6. Allocated bytes where process separation permits a reliable value.

Report medians and ranges after one warm-up. A passing correctness suite is required but does not
measure streaming.

## Prototype ladder

Build the smallest prototypes that separate the benefits:

1. Stream the HTTP representation after the server has buffered provider rows.
2. Stream provider rows only for no-tracking queries, while the client still buffers.
3. Stream wire rows into client materialization, while preserving all-or-nothing delivery.
4. Yield materialized rows only for query shapes that need no complete residual or relationship
   state.

Stop after each prototype and compare its benefit with its permanent contract cost. Later steps do
not inherit approval from earlier ones.

## First measurement: outer envelope

The first harness compares byte-array serialization with direct stream serialization. It uses an
8 MiB synthetic payload, which becomes an 11,184,894-byte envelope. Each result is the median and
range of five runs after warm-up. The run used a Debug build on .NET 10.0.12 on 2026-10-21.

| Path | Time in milliseconds | Allocated bytes |
|---|---:|---:|
| Buffered server write | 4.93 [4.36, 6.29] | 27,980,872 [11,203,344, 27,980,872] |
| Streamed server write | 2.50 [2.36, 4.24] | 16,796,400 [16,796,400, 16,796,400] |
| Buffered client read | 5.19 [5.05, 5.39] | 25,168,864 [25,168,864, 25,168,864] |
| Streamed client read | 11.98 [10.93, 14.08] | 41,929,840 [41,929,840, 58,707,080] |

The server result supports direct response serialization. It reduced median allocation by 40
percent and median time by 49 percent for this payload. Wire size did not change.

The client result rejects direct envelope deserialization as the first implementation slice. It
increased median allocation by 67 percent and median time by 131 percent. The client therefore
continues to buffer and deserialize the outer envelope as a byte array.

This harness isolates one buffering boundary. It does not measure provider enumeration, row
mapping, peak process memory, or time to the first row. Those measurements remain necessary before
any row-streaming scope is accepted.

## Target architecture hypothesis

The first measurement does not select an implementation slice. It only proves that direct server
serialization can remove one allocation. The complete design hypothesis is a framed query stream:

1. The client writes a query request directly to the transport request stream.
2. The server opens the provider enumerator and owns its context until stream disposal.
3. A start frame carries protocol version and result metadata.
4. Row frames carry independently decodable wire values.
5. Log frames preserve server ordering without waiting for completion.
6. A completion frame closes a successful result.
7. A fault frame terminates enumeration by throwing on the client.
8. Cancellation or early disposal closes the response, provider enumerator, context, and command.

The query executor consumes frames as an asynchronous sequence. A pass-through query can yield each
materialized value immediately. A client residual, split query, or relationship operation may
buffer only the input that operation needs. The transport and protocol must not impose whole-result
buffering on every query.

Tracked queries require comparison with plain Entity Framework enumeration. The earlier branch
treated navigation state before complete enumeration as a defect. That conclusion must be tested
against local Entity Framework behavior rather than assumed. Progressive fixup may be the correct
semantic model.

Synchronous execution requires its own reader. The design must not implement it by blocking an
asynchronous network operation on runtimes that prohibit blocking. A transport can expose separate
synchronous and asynchronous capabilities, with an explicit refusal where synchronous networking
cannot exist.

This hypothesis deliberately replaces the nested `InfoCarrierEnvelope.Payload`,
`QueryDataResult.SerializedResults`, and query use of the unary transport. It also removes any
serializer operation whose only result is a complete byte array.

## Clean-slate experiment results

### Tracking semantics

A plain Entity Framework query projected one tracked `Order` with two successive `OrderDetail`
rows. Lazy loading was disabled. After the first `MoveNextAsync`, the order contained one detail.
After the second, the same order instance contained both details.

Progressive relationship fixup is therefore normal Entity Framework enumeration behavior. The
parked branch's incomplete-navigation result did not justify buffering the complete provider result.
A streamed implementation should reproduce progressive fixup instead.

### Framing semantics

A research transport now returns response headers with an open body and a framed JSON sequence.
The client receives its first row while the producer remains blocked. Three executable probes show:

- a row arrives before server completion;
- a terminal fault throws from the next client `MoveNextAsync` after an earlier row;
- disposing early cancels the producer instead of leaving it blocked.

The successful sequence also carries a log frame before its completion frame. These results support
start, row, log, fault, and completion as protocol concepts.

The same transport now consumes a real SQLite provider enumerator. The first row reaches the
client while the provider context remains alive. The client then drains the sequence and receives
the same row count as a direct provider query.

A second probe maps a tracked `OrderDetail` and a two-value projection into real
`DynamicValueNode` instances. Both nodes cross the framed response independently. The entity node
retains its entity key and scalar properties. The projection node retains both projected values.

### Resource lifetime

The real-provider early-exit probe observes the SQLite connection state. The connection opens
while the query enumerator is active. Disposing the client response cancels the producer, disposes
the provider iterator and context, and closes the connection. The producer never writes a
completion frame.

This proves the required ownership chain for the prototype:

`response disposal -> producer cancellation -> provider iterator disposal -> context disposal -> connection close`

The connection closure is the observable provider-level proof. A separate command wrapper is not
needed for this prototype because the active query reader owns the open connection.

### Browser transport

The browser probe runs inside the .NET 10 WebAssembly sample against an endpoint that flushes two
bytes, waits two seconds, and then completes. The 2026-10-21 run produced:

| Completion mode | WebAssembly streaming option | Stream type | Headers | First byte |
|---|---:|---|---:|---:|
| `ResponseHeadersRead` | enabled | `StreamContent.ReadOnlyStream` | 352 ms | 360 ms |
| `ResponseHeadersRead` | disabled | `MemoryStream` | 51 ms | 2,060 ms |
| `ResponseContentRead` | enabled | `MemoryStream` | 2,013 ms | 2,013 ms |

The earlier branch's conclusion was incomplete. Browser streaming requires both
`ResponseHeadersRead` and `WebAssemblyEnableStreamingResponse`. Either setting alone buffers the
body. The browser reported no console errors.

### Frame encoding cost

The first object-frame encoding repeated property names on every row. It is rejected. A compact
tagged-array encoding writes through one reusable `Utf8JsonWriter` buffer and remains viable.
Measurements used five runs after warm-up on .NET 10.0.12 on 2026-10-21:

| Rows | Encoding | Bytes | Median time | Median allocated bytes |
|---:|---|---:|---:|---:|
| 10 | Buffered integer array | 21 | 0.04 ms | 18,424 |
| 10 | Object frames | 439 | 0.08 ms | 21,712 |
| 10 | Compact tagged frames | 69 | 0.02 ms | 17,920 |
| 10,000 | Buffered integer array | 48,891 | 0.15 ms | 165,648 |
| 10,000 | Object frames | 388,969 | 13.06 ms | 2,659,072 |
| 10,000 | Compact tagged frames | 88,899 | 1.19 ms | 18,544 |

Compact framing reduced allocation by 89 percent against the buffered large result. It increased
wire size by 82 percent and serialization time by 693 percent for trivial integers. Real wire rows
are larger, so the workload matrix must determine whether the fixed frame overhead remains material.

A second benchmark used 476 real four-value projection nodes from SQLite. It used source-generated
metadata and five runs after warm-up:

| Encoding | Bytes | Median time | Median allocated bytes |
|---|---:|---:|---:|
| Buffered node list | 344,358 | 4.80 ms | 42,360 |
| Compact row frames | 346,270 | 5.80 ms | 186,096 |

Framing added 0.56 percent to the real-node wire size and 21 percent to serialization time. The
measured implementation called the serializer once per row and allocated about 302 bytes per call.
The production codec must write node fields through one writer without that wrapper call.

### Tracked includes and many-to-many references

A real SQLite many-to-many query loaded two posts sharing one tag. Entity Framework completed each
included collection before yielding its post. Each post then mapped into an independently
serialized row node with loaded-navigation and join-row metadata.

The second row referred to the shared tag by an identifier established in the first row. Therefore,
mapper reference scope belongs to the complete result stream. Resetting it at each frame would
produce incorrect identity and relationship fixup.

### Workload status

| Workload | Current evidence | Remaining work |
|---|---|---|
| Small scalar projection | Compact framing measured with 10 integers. | Retain as the fixed-overhead guard. |
| Large flat no-tracking projection | Real SQLite rows cross incrementally; 476 real nodes were measured. | Add separate-process peak memory. |
| Large tracked entity query | A mapped tracked entity node crosses independently. | Add separate-process peak memory. |
| Tracked include and many-to-many query | Included collections, join rows, and cross-frame references are preserved. | Repeat through production materialization. |
| Query with a client residual | Current executor buffers all slots before one opaque residual. | Implement the classified policy in the plan. |
| Query split into several server queries | Current executor completes every slot before residual execution. | Implement explicit slot dependencies. |
| Failure after some provider rows | A row followed by a fault throws during enumeration. | Drive the fault from a real provider. |
| Cancellation and early enumeration exit | Cancellation disposes the iterator and context, then closes SQLite. | Repeat through the production executor. |
| Forwarded server logging enabled | A log frame preserves ordering before completion. | Connect real log capture. |
| Browser HTTP transport | Both browser streaming switches are required. | Keep both in the transport contract. |

## Protocol selection

The research selects a compact tagged JSON frame sequence for version 11 query responses. This is
not a compatibility extension of the unary envelope. It replaces that query response shape.

The selected frame concepts are:

| Tag | Meaning | Required content |
|---:|---|---|
| 0 | Start | Protocol version and result metadata. |
| 1 | Row | One independently decodable `DynamicValueNode`. |
| 2 | Log | One ordered server log event. |
| 3 | Complete | Successful terminal state. |
| 4 | Fault | Terminal server failure details. |

One reusable `Utf8JsonWriter` writes the outer sequence. The reader consumes one complete frame at
a time. It never requires the complete response body. Object frames are rejected because their
repeated names caused excessive size and allocation.

The asynchronous HTTP transport must request `ResponseHeadersRead`. Its WebAssembly path must also
set `WebAssemblyEnableStreamingResponse`. Early disposal owns cancellation of the server producer.

This selection fixes the wire mechanism, not every execution policy. Pass-through queries can
yield each row. Residual and split queries buffer at their semantic boundary. Later workload tests
must define those boundaries without changing the selected frame protocol.

The concrete replacement contracts, buffering policy, implementation sequence, and pull-request
boundaries are in [streaming-implementation.md](streaming-implementation.md).

## Public contract questions

The old branch replaced members on `IInfoCarrierTransport`, `IInfoCarrierSerializer`, and
`QueryDataResult`. Version 11 permits their removal. The investigation must select the clearest
complete contract and an explicitly versioned wire protocol, without preserving obsolete members
or adding compatibility adapters.

Research must still determine which changes earn inclusion. Major-version freedom removes the
compatibility constraint, but it does not justify complexity without measured benefit.

## Exit conditions for research

The investigation is complete when it can answer all of these questions with measurements:

- Which buffering boundary dominates memory for ordinary and large results?
- Does wire-only streaming provide enough benefit without caller-visible streaming?
- Which query shapes can yield early without changing answers or exception behavior?
- How are trailing faults and forwarded logs delivered?
- What closes server resources after cancellation or early exit?
- Which public contracts must change, and which changes can remain internal?
- Which protocol version boundary carries the new response format?

Only then should the version 11 row-streaming scope be chosen.
