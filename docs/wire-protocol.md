# Wire protocol contract

Status: implemented contract 3 on the version 11 development line. Package support policy and
migration follow-up: [issue 50 scope](plans/v11/wire-versioning.md).

## 1. Version identification and admission

The wire version is an independent integer, not a package version or a schema revision alone.
It identifies structure, field meanings, defaults, and execution guarantees. The current value is
`InfoCarrierEnvelope.CurrentProtocolVersion`, 3. `QueryStreamFrameCodec.Version` uses that same
constant. There is no protocol minor number and no implicit negotiation.

| Path | Request identification | Response identification |
|---|---|---|
| Buffered, synchronous or asynchronous | `InfoCarrierEnvelope.ProtocolVersion` | `InfoCarrierEnvelope.ProtocolVersion` |
| Streamed query | `QueryDataRequest.ProtocolVersion`; HTTP also checks `InfoCarrier-Protocol-Version` before body parsing | Version in `QueryStartFrame` |

Missing request identification is zero. The server accepts only contract 3. It rejects older,
newer, and unidentified contracts before calling the operation server. Old buffered contracts 1
and 2 and pre-policy unversioned streamed requests are rejected. Clients validate the response
contract before reading its payload, rehydrating faults, or yielding rows. Internal in-process
operation calls without a wire exchange are not a separate wire admission boundary.

`TransportInfoCarrierClient` stamps envelopes and streamed query requests. A custom transport must
preserve identification. A custom producer of streamed requests must supply it explicitly;
`QueryDataRequest` has no current-version default that could admit an old request accidentally.

Streamed HTTP requests use `InfoCarrier-Stream-Operation: Query`. The old `InfoCarrier-Operation`
selector is refused explicitly. Using a new selector also prevents a pre-policy streaming server
from ignoring the body version and executing a new request: without its recognized selector it
tries an envelope, whose required operation and payload are absent from the query body.
For the current selector, the HTTP version header is checked before query-body deserialization.
The body version is checked too. A future format with a different body shape therefore receives
a version refusal without needing the current server's query parser to understand it.

### Refusals and diagnostics

Buffered dispatch throws `InfoCarrierProtocolException` outside its operation-fault handler.
The HTTP endpoint checks streamed requests before obtaining query frames. Both refusal paths
answer status 400 with a plain text explanation and these response headers:

| Header | Value |
|---|---|
| `InfoCarrier-Protocol-Version` | Server's wire version |
| `InfoCarrier-Client-Protocol-Version` | Received client version, including zero when missing |

The updated HTTP transport identifies these refusals without parsing prose and raises
`InfoCarrierProtocolException`. Its `ClientVersion` and `ServerVersion` properties and message
identify the disagreement and which side needs upgrading. Response version mismatches, including
query start frames, use the same exception. Retrying cannot fix a contract mismatch. Other HTTP
errors remain transport failures. Legacy peers without the new diagnostic headers retain their
existing behavior; no legacy diagnostic or contract converter is added.

### Package support window

A version 11 server supports clients from its current and previous minor release. Version 11.0
supports 11.0 only. Version 10 and earlier version 11 development contracts are excluded. The
window is release-based, not time-based. Product releases may share a wire version; acceptance of
an older or newer package using the same contract does not extend the support promise. Admission
checks the contract and does not infer or require the caller's package version.

**Issue 50 ships no migration mechanism.** Its [completion criteria and follow-up story](plans/v11/wire-versioning.md)
separate admission and refusal tests from migration design. Real changes, potentially from
[issue #99](https://github.com/azabluda/InfoCarrier.Core/issues/99) or
[issue #220](https://github.com/azabluda/InfoCarrier.Core/issues/220), are needed to assess conversion
correctness, software design quality, and flexibility. Neither issue is assumed migratable.

### Contract evolution

Preserve existing field names, types, and meanings. Add fields with defaults that preserve older
behavior, and give new expression node kinds new discriminator numbers. A field or interpretation
that carries a new promise requires a contract version change, even if the schema is unchanged.
Unknown object fields are ignored by the default serializer; unknown expression discriminators
and query frame tags are refused. Frame arities are checked exactly. Structural additivity alone
therefore does not prove that either peer understands a changed contract.

Changing the contract number does not fulfill the previous-minor support promise. A future
incompatible change must retain supported behavior, complete the migration follow-up, wait for a
suitable release boundary, or obtain an explicit owner decision changing that promise.

### Protocol 2 history: bounded anonymous shapes (2026-10-07)

Contract 2 introduced bounded anonymous-shape descriptors; contract 3 retains that vocabulary.
TypeNode uses a reserved `InfoCarrier.AnonymousShape.v1/` identity token, ordered `shapeMembers`,
and property-type `genericArguments`. Arrays containing shapes use `arrayElement` and `arrayRank`.
The shape vocabulary version is independent of the wire contract version.

Identity tokens are not assembly load instructions. Anonymous structures must match a trusted
server catalog, and all components must pass admission checks. Responses retain descriptors so
clients reconstruct original types. No generated code or client assemblies travel from server to
client. Named application types require explicit registration. See the
[descriptor decision](decisions.md#amendment-2026-10-07-bounded-anonymous-data-descriptors) and its
[trusted-catalog amendment](decisions.md#amendment-2026-10-08-trusted-structural-catalogs).

## 2. Operations and responses

Buffered envelopes contain `operation`, serialized `payload`, optional `correlationId`, and an
optional response `fault`. The operation is numeric. The nine operations are query, save changes,
begin/commit/rollback transaction, create/rollback/release savepoint, and savepoint support.
The server echoes operation and correlation identity in its response. An unknown operation within
an accepted contract becomes a fault; a contract mismatch remains outside that fault path.

Synchronous queries and buffered transports return `QueryDataResult`. Asynchronous HTTP queries
send the query body directly, selected by `InfoCarrier-Stream-Operation: Query`, and return tagged frames:
start, row, log, complete, or fault. A stream starts with a versioned start frame and ends with
one complete or fault frame. Frames after termination, unknown tags, and incomplete sequences
are refused. Ordinary query failures can travel as fault frames after rows have been sent.
Commands retain buffered envelopes because their results are atomic.

Query requests carry the expression tree and execution context, including tracking, single-result,
transaction, and split-query information. Change sets carry entry states, changed values, original
concurrency values, and generated-value results. Transactions use explicit server-held identity
tokens across stateless exchanges. Operation payload records live in `Common/`; frame types and
their codec live in `Query/`.

## 3. Verification and remaining work

Refusal tests use small fixed old payloads and applicable headers. They assert explicit diagnostics
and zero operation-server calls, rather than a generic parsing failure. Current-client acceptance,
response version checks, frame decoding, and the regular functional and transport suites exercise
contract 3. No older client build must be preserved to test rejection.

From 11.1, acceptance evidence must use a real published previous-minor client package against the
current sample server in a separate process. Refusal fixtures are not that evidence. Committed
traffic replay does not substitute for acceptance or migration tests. The separate migration
story must use real contract changes and record both supported conversions and impossible cases.
