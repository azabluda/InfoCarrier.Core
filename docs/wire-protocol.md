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

### Structural compatibility check

Run the check from the repository root:

    python eng/wire-contract.py

The local default reads the description at `HEAD`, so checking out an older commit uses its own
reference. This checks uncommitted work. Before pushing, include all feature-branch commits by
comparing with the target branch:

    python eng/wire-contract.py --base-ref issue-50-wire-versioning

The C# tool in tools/InfoCarrier.WireContract inspects effective System.Text.Json metadata and
writes the candidate description temporarily. The reference is read directly from
[the committed description](wire-contracts/current.json) at the selected Git revision. No old source
build, package restore, or old runtime is needed. The JSON describes effective contracts, rather
than a general JSON Schema language. `FormatVersion` identifies the description format separately
from the wire version.

After an accepted contract change, run:

    python eng/wire-contract.py --base-ref issue-50-wire-versioning --record

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
| New variant | Older requests remain valid; older response readers are not assumed to understand it |
| Unsupported encoding, options change, or incomplete inventory | Fail as incomplete analysis; a version increase does not resolve it |

Numeric widening and equivalent CLR type replacements are not inferred. They can require explicit
classification even when a reader would accept them. Failures identify the path, reference revision,
direction, and both descriptions. Restore accidental changes, complete coverage or classification,
or deliberately allocate a fresh version for a known break. Then record the candidate description;
never replace the protected reference to dismiss a failure.

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
