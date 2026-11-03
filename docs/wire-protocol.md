# Wire protocol contract

Status: implemented contract 4 with protocol 3 conversion on the version 11 development line. Package support policy and
migration follow-up: [issue 50 scope](plans/v11/wire-versioning.md).

## 1. Version identification and admission

The wire version is an independent integer, not a package version or a schema revision alone.
It identifies structure, field meanings, defaults, and execution guarantees. The current value is
`InfoCarrierEnvelope.CurrentProtocolVersion`, 4. `QueryStreamFrameCodec.Version` uses that same
constant. There is no protocol minor number and no implicit negotiation.

| Path | Request identification | Response identification |
|---|---|---|
| Buffered, synchronous or asynchronous | `InfoCarrierEnvelope.ProtocolVersion` | `InfoCarrierEnvelope.ProtocolVersion` |
| Streamed query | `QueryDataRequest.ProtocolVersion`; HTTP also checks `InfoCarrier-Protocol-Version` before body parsing | Version in `QueryStartFrame` |

Missing request identification is zero. The server accepts contracts 3 and 4. It rejects unsupported
and unidentified contracts before calling the operation server. Old buffered contracts 1
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

Issue 50 established contract 3 admission without migration. The follow-up now converts protocol 3
requests to the live protocol 4 data classes and converts responses back. Complete historical
classes, metadata, enums, and the frame codec live under `Wire/V3`; current classes remain the live
representation. There is no third complete canonical model or runtime mapping library. The
[migration research and experiment](plans/v11/wire-migration-research.md) record scope and limits.
Acceptance of the selected development client does not create a published prerelease support promise.

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

### Structural compatibility check

Run the check from the repository root:

    dotnet run --file eng/WireContract.cs

The local default reads the description at `HEAD`, so checking out an older commit uses its own
reference. This checks uncommitted work. Before pushing, include all feature-branch commits by
comparing with the target branch:

    dotnet run --file eng/WireContract.cs -- --base-ref origin/main

The C# tool in tools/InfoCarrier.WireContract inspects effective System.Text.Json metadata and
writes the candidate description temporarily. The reference is read directly from
[the committed description](wire-contracts/current.json) at the selected Git revision. No old source
build, package restore, or old runtime is needed.

The file is a JSON Schema 2020-12 catalogue. Shared message definitions and frame definitions live
under `$defs`; local `$ref` values connect them. Select a definition when using a standard validator:
the catalogue root itself intentionally imposes no message constraint. Fields use `properties`,
`required`, and `additionalProperties`; arrays use `items`, and frames use `prefixItems` with exact
lengths. Variant alternatives use `anyOf`, `allOf`, and discriminator constants.

The standard format lets independent validators check the description and selected message shapes.
Code generation is not an acceptance requirement. The structural safeguard additionally interprets
the annotations below; a standard validator alone does not determine wire compatibility.

`x-infocarrier` contains the supplementary information: coverage directions, serializer settings,
numeric enum identities, named frame positions, nested-payload inventory, allocation history, and
inspection inputs. Ordinary field names, requiredness, and references are not duplicated there.
The description format is 2, independently of wire version 4. Historical format-1 descriptions
remain readable without rewriting their Git revisions or building old source.

After an accepted contract change, run:

    dotnet run --file eng/WireContract.cs -- --base-ref origin/main --record

Recording first compares the actual implementation with the protected reference. A known break
without a fresh wire version, or incomplete analysis, prevents recording. Ordinary checks also
verify that the saved candidate contract matches its implementation. Editing the candidate JSON
cannot replace the target's description or make an incompatible change pass.

Commit the reviewed description with the code. It changes only when the contract or its recorded
inspection information changes; ordinary commits reuse the same file content. Compatible additions
can update the description without changing the wire integer. Git preserves earlier descriptions
with their corresponding source revisions, rather than creating a new file for every commit.

The covered paths are buffered envelopes, operation records and primitive payloads, separately
decoded expressions and dynamic values, type descriptors, reference fields, faults, logs, enum
assignments, and polymorphic discriminator mappings. An authored root list connects nested bytes
to their decoded contracts. Registered contexts are discovered across the shipped assemblies;
a registered type without a covered direction or an unsupported converter fails inspection.

The stream probe runs the actual writer and reader for all existing frame kinds. It checks tags,
lengths, named positions, and embedded payload agreement with serializer metadata. Distinct integer
values expose a Version/Slot swap. The examples exercise both start-frame Boolean/null branches;
they do not infer arbitrary conditional behavior in handwritten code.

Request checks ask whether the current server can read older requests. Response checks ask whether
older clients can read current responses. They do not promise newer clients work with older servers.

| Difference | Result |
|---|---|
| No covered structural change | Pass with the existing version |
| New optional request property | Pass when the reader permits omission |
| Additional response property | Pass when the older reader skips unknown fields |
| New required request property or changed JSON token type | Fail with the unchanged version |
| Removed or renamed field | Fail under the conservative field-preservation rule, even if parsing could succeed |
| Reassigned enum/discriminator or changed frame layout | Fail with the unchanged version |
| New variant | Compatible for older requests; incompatible for older response readers unless explicitly classified |
| Covered depth, null-omission, or strict/named-number option change | Conservatively incompatible; require a fresh contract |
| Unsupported encoding, other options change, or incomplete inventory | Fail as incomplete analysis; a version increase does not resolve it |

Numeric widening and equivalent CLR type replacements are not inferred. They can require explicit
classification even when a reader would accept them. Failures identify the path, reference revision,
direction, and both descriptions. Restore accidental changes, complete coverage or classification,
or deliberately allocate a fresh version for a known break. Then record the candidate description;
never replace the protected reference to dismiss a failure.

The schema describes the covered message shapes, not every runtime acceptance condition. Scalar
ranges, date parsing, resource limits, opaque application JSON, and semantic rules still need runtime
checks. Standard validators do not interpret `x-infocarrier` metadata. Some tools require strict
unknown-keyword rejection to be disabled before they accept these annotations. In particular, two integer frame
positions remain interchangeable to a generic validator; our named-position comparison and codec
probe detect Version/Slot changes.

The schema reader supports the generated subset rather than arbitrary JSON Schema. Unknown
constraints, missing metadata, external references, and inconsistent schema/annotation combinations
fail as incomplete analysis. Numeric representation modes beyond the covered strict/named-float
settings, value-type objects or collections, and polymorphic readers forbidding additional fields
need explicit coverage before use. The existing default routes remain covered. JsonSchema.Net is a
test-only dependency for independent schema validation, not a runtime dependency of the checker or
product. Our comparer continues using the same compatibility rules.

The check does not detect changed units, business meaning, constructor/setter behavior, or changes
inside arbitrary serialization code with unchanged metadata. Application JSON inside JsonElement,
application-specific models, and third-party serializers are outside its structural guarantee.
New handwritten serialization routes require explicit coverage. Transport selectors and admission
behavior remain covered by the existing transport tests, not inferred by this metadata comparison.

### References and allocation

The Fast gate compares GitHub's pull-request merge candidate with its target parent. Merge-group
checks use the queue's base revision. Push checks use the protected branch state before the entire
push. Local checks use HEAD unless a target is supplied. Missing references, stale merge
identities, incomplete inventories, and rewritten allocation histories fail explicitly.

Before the initial description reaches the target branch, the reference is description commit
e877418b42b8991f0d3dc9be9cdfdea794fe3d81, captured from development revision
1d8eb806291d15b1233fcdabb855b30843d31f63. This establishes no prerelease support promise.
The protected description contains its allocation history; the candidate's history cannot replace
it. A missing description fails, including at the initial reference.

Release tags preserve the same description file. To check an actual supported release revision,
add `--release-ref <release-tag-or-commit>`; repeat it for each applicable revision. These extra
comparisons require structural compatibility even if the candidate increases the wire number.
A representative patch does not prove compatibility with every supported patch. No stable version
11 release exists yet, so CI currently checks only its target reference. Release acceptance must
select the applicable release references and run the published-client tests from 11.1. Retired
release descriptions can remain in Git history without remaining active comparison targets.

Keep required checks current with the target branch, or use a merge queue, to check concurrent
allocations against the final merge candidate. Existing review controls must protect the workflow,
checker, coverage definitions, and reference-selection code. Administrators who bypass those
controls can bypass this check. No repository test can prevent its own authorized removal.

The description records inspection toolchain and dependency identities. The runner prints those
reference inputs and the actual candidate build inputs. Toolchain provenance changes alone do not
require a new wire version or description update. The contract is inspected on every run, including
after dependency changes; equal metadata does not prove unchanged dependency behavior.

The inspector's coverage remains a limitation of a saved description. Adding previously omitted
coverage may require a reviewed baseline-format transition. Missing historical information must
not be invented or silently treated as compatible. Check and description-format changes need the
same review protection as baseline selection.

Allocated integers are recorded once in the append-only [history](wire-versions.json).
The authoritative current value remains InfoCarrierEnvelope.CurrentProtocolVersion.
For an incompatible structural change or a deliberate meaning change:

1. Choose an integer greater than every allocated integer.
2. Change the authoritative constant and append the allocation to the history.
3. Record the reason here, including any supported clients that need separate compatibility work.
4. Run the structural check with `--record`, review and commit the description diff, then run
   the ordinary structural check, admission tests, and applicable repository checks.

A semantic-only increase passes without a fabricated field change. Reverting code or retiring a
contract does not free its integer. Compatible revisions may share one integer.
Follow the [semantic-change procedure](versioning.md#before-changing-a-fields-meaning) before
changing units, omitted-value meanings, or other promises, even when structural checks pass.
The [migration follow-up](https://github.com/azabluda/InfoCarrier.Core/issues/262) tracks preserving
supported-client behavior; allocating an integer does not complete that work.
This branch-state check does not replace supported-release acceptance. From 11.1, use real
published previous-minor clients as described below; a version increase alone can still refuse
clients the release policy requires supporting.

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

### Protocol 4: typed query content (2026-11-03)

Protocol 3 query bodies contain `serializedQuery`, a base64 string containing expression JSON.
Its buffered responses contain `serializedResults`, another encoded JSON document. Protocol 4
replaces these with the typed `query` object and `rows` array. The outer `payload` remains arbitrary
serializer-produced bytes. Save-value blobs retain their encoding. Binary application values
remain binary values; they are not interpreted as embedded JSON.

The live context includes the node vocabulary, null omission, named floating-point literals, and
depth 260 for operation wrappers. The standalone node context retains depth 256 and its reference
identifiers. Selected server limits still guard decoded graphs before translation. Envelope and streamed
request bytes are bounded before buffering; a metadata projection reads body identification before
selecting the full decoder. Streamed requests reject a header/body mismatch before decoding the graph.
Unsupported envelope versions retain operation identity for diagnostics and are refused by dispatch
before opening their opaque payload. Streamed response rows remain incremental and use the existing frame vocabulary.

Protocol 3 decoding uses its own contexts, then explicit mappings of every node and value shape.
Mappings preserve enum meanings, parameter/reference identifiers, null markers, tracking and navigation
state, original concurrency values, generated values, transaction tokens, faults, and logs. Converted
descriptors still pass current type and method admission. A protocol 3 request receives protocol 3
responses and start frames. The original effective description is retained in
[protocol3.json](wire-contracts/protocol3.json); its types and exercised frames are checked independently
of increases to the live integer. The protected Git comparison also verifies the first physical copy.

Custom serializers must handle the historical request/result types when serving protocol 3, as well
as the live classes and streamed identification projection. The default serializer selects generated
metadata explicitly. The prefix-serializer tests exercise non-JSON operation bytes; no decoder assumes
the outer payload is JSON. A custom serializer owns how typed graphs are encoded inside its payload.

`InfoCarrierWireProtocol` receives the host's singleton `IWireProtocolCodec` services through its
constructor. Each implementation owns one protocol's message encoding and frame handling; historical
implementations call explicit mapping helpers. Role registration supplies current and protocol 3 codecs.
Advanced hosts can replace the collection through ordinary dependency injection registrations.
Duplicate version identities or a missing current codec are configuration errors. Metadata selection
is cached per host selector, without assembly scanning or reflection fallback. The selector requires
its injected collection. Legacy direct serializer and dispatcher constructors handle the current
contract only. Custom adapters use the selected endpoint's `CreateEnvelopeServer` method to
retain host codec selection. See the
[codec decision](decisions.md#amendment-2026-11-03-dependency-injected-protocol-codecs).

`SerializedQuery` and `SerializedResults` remain ignored source-compatibility accessors for custom
in-process adapters. They are absent from protocol 4 metadata and normal query execution uses typed
graphs directly. Decoded-graph size verification currently serializes a graph once before translation;
the experiment does not claim that all encoding or allocations disappeared.

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
contracts 3 and 4. No older client build must be preserved merely to test rejection.

From 11.1, acceptance evidence must use a real published previous-minor client package against the
current sample server in a separate process. Refusal fixtures are not that evidence. Committed
traffic replay does not substitute for acceptance or migration tests. The separate migration
story must use real contract changes and record both supported conversions and impossible cases.
