# Client logging audit

Audit for [issue #215](https://github.com/azabluda/InfoCarrier.Core/issues/215), dated
2026-10-10, against main commit `57e14d3214c7134be3ad5325bf0761d81873bab4`.
This records observed boundaries and proposed requirements, not an implementation approval.
Projection behavior and registration asymmetry remain with
[issue #218](https://github.com/azabluda/InfoCarrier.Core/issues/218).

## Existing boundaries

| Path | Existing diagnostics | Missing information and operational impact |
|---|---|---|
| Request payload serialization | `TransportInfoCarrierClient.RoundTripAsync` measures failures from `SendAsync`, including serialization before transport invocation. | Operation and exception type do not distinguish serialization from network failure. A counted attempt need not have sent a request. |
| HTTP envelope serialization, send, body read, envelope decode | `HttpInfoCarrierTransport.SendAsync` propagates network exceptions, reports unsuccessful status with URI and response body, and wraps malformed successful responses in `InfoCarrierTransportException`. | No dedicated safe event or local phase. HTTP status exists in exception text, not a structured property. Logging that exception can expose response bodies and addresses. |
| Returned server fault | `TransportInfoCarrierClient.SendAsync` rehydrates the fault before reading its payload. Metrics record the resulting client exception type. | The metric cannot distinguish a returned fault from a local exception of the same type. Rehydration can substitute a base type or `InfoCarrierServerException`; the runtime type is not necessarily the original server type. |
| Response payload decode | The same round-trip measurement includes payload deserialization. | The server can have completed a write before this fails. Reporting a failed exchange must not assert rollback or make retry appear safe. |
| Asynchronous query request construction and exchange | `QueryExecutor.ExecuteAsync` catches failures around the cancellation check, `BuildRequest`, and `QueryDataAsync`. It raises Entity Framework Core `QueryCanceled` or `QueryIterationFailed` and rethrows. | The failure event attaches the exception. It has no InfoCarrier phase, and construction failures occur before transport metrics begin. |
| Synchronous query fetching | `QueryExecutor.Fetch` constructs requests and waits for the client without an equivalent failure-reporting catch. | Query diagnostics differ between synchronous and asynchronous execution. |
| Result materialization and client projection | Both query paths materialize results after fetching. `ApplyResidual`, `Guarded`, and deferred attachment execute outside the asynchronous request catch. | A successful exchange can be followed by a client failure without a corresponding provider query-failure event. Metrics correctly describe only the exchange. |
| Query splitting and expected refusals | `QuerySplit` logs Information through both logger and Entity Framework event dispatch. It reports query count, retained operators, and relevant unregistered key types. Constructor validation and splitting can throw before execution. | A successful split already has a diagnostic. Refusal does not imply an unexpected defect. Constructor failures are outside request measurement and the asynchronous request catch. |
| Save changes | `InfoCarrierDatabase.SaveAsync` builds the change request before calling the client. It reports optimistic concurrency through the existing update logger, translates update exception entries, replays granted server logs, and applies generated values. | Request mapping and generated-value application are outside exchange metrics. Do not add a second concurrency report without considering the existing event and interception behavior. |
| Transactions and savepoints | The supplied client handles exchanges. `InfoCarrierTransactionManager` rejects missing or nested transactions locally. | A missing transaction is a local usage refusal with no exchange. Direct client operations lack the query logger boundary. |

Sources: [transport client](../../../src/InfoCarrier.Core/TransportInfoCarrierClient.cs),
[HTTP transport](../../../src/InfoCarrier.Core/HttpInfoCarrierTransport.cs),
[query executor](../../../src/InfoCarrier.Core/QueryExecutor.cs),
[database](../../../src/InfoCarrier.Core/InfoCarrierDatabase.cs),
[transaction manager](../../../src/InfoCarrier.Core/InfoCarrierTransactionManager.cs),
[fault mapper](../../../src/InfoCarrier.Core/InfoCarrierFaultMapper.cs), and
[query logging](../../../src/InfoCarrier.Core/InfoCarrierLoggerExtensions.cs).

## Metrics and cancellation

`InfoCarrierMetrics` publishes `infocarrier.client.round_trips` and
`infocarrier.client.round_trip.duration`. Both carry `infocarrier.operation`; failures also carry
`error.type`. Measurement requires an active listener at entry and completion. All nine operations
share this boundary. Custom `IInfoCarrierClient` implementations are outside it.

The boundary includes request payload serialization and response payload decoding, but excludes
query expression serialization, change mapping, result materialization, and residual projection.
It counts completed attempts, including failures before a transport send. Existing tests cover
successful operations and a failed transport with its exception-type tag. They do not establish
phase classification, cancellation attribution, or serialization-failure counting.

Cancellation propagates through the client and transport. Envelope decoding deliberately excludes
`OperationCanceledException` from malformed-envelope wrapping. The asynchronous query path uses
Entity Framework's exception detector for its cancellation event. Round-trip metrics record
cancellation as an exception type without caller-cancellation or timeout attribution. A timeout
must not be inferred from an exception name alone.

Sources: [metrics](../../../src/InfoCarrier.Core/InfoCarrierMetrics.cs),
[metric tests](../../../test/InfoCarrier.Core.TransportTests/RoundTripMetricsTest.cs), and
[transport tests](../../../test/InfoCarrier.Core.TransportTests/HttpInfoCarrierTransportTest.cs).

## Server overlap and privacy

The current dispatcher already logs mapped faults with operation, observed server phase, runtime
exception type, and a bounded reason. Concurrency uses Information; request cancellation uses
Debug; unrelated cancellation uses Error. The HTTP adapter covers its own body and response
boundaries. A client event must describe the client's observation, not repeat server execution
diagnostics or claim server state from a missing response.

`ServerLogReplay` replays server-granted messages through the context logger. Forwarding is off
by default and is a separate channel. New client events must not copy forwarded messages.
Existing Entity Framework failure events attach exceptions. The fault contract also carries
messages, inner exceptions, and server stacks. A safe new event policy does not sanitize these
existing channels or the application's own logging configuration.

Sources: [server policy](server-failure-diagnostics.md),
[dispatcher](../../../src/InfoCarrier.Core/InfoCarrierEnvelopeServer.cs), and
[log replay](../../../src/InfoCarrier.Core/ServerLogReplay.cs).

## Samples

The console sample constructs its client explicitly and supplies no logger to it. Its outer
handler prints `HttpRequestException.Message`. Its counting handler measures HTTP sends rather
than the broader transport-client attempt boundary.

The Blazor sample also constructs the client explicitly, inside dependency injection. Its
`InspectingTransport` records non-cancellation transport failures and returned fault envelopes.
It omits cancellation, failures before the decorator is called, response payload decode failures,
and subsequent projection failures. Its sizing serialization occurs before its failure catch;
response sizing and recording occur afterward. The inspector displays payloads and exception
messages intentionally, and is not a privacy-safe production logging template.

These are source observations, not a new interactive sample validation. No sample or product
behavior was changed during this audit.

Sources: [console](../../../samples/Northwind.Demo/Program.cs),
[Blazor registration](../../../samples/Northwind.Client/Program.cs),
[inspecting transport](../../../samples/Northwind.Client/Wire/InspectingTransport.cs), and
[wire log](../../../samples/Northwind.Client/Wire/WireLog.cs).

## Safe logging requirements for design review

1. Identify the local operation and phase using bounded, source-controlled values. Distinguish
   preparation, exchange, returned fault, decoding, materialization, and projection where observed.
   Do not infer a more specific cause from arbitrary exception messages.
2. Distinguish known refusals, configuration errors, caller cancellation, concurrency, and
   unclassified failures only where control flow or an explicit source signal establishes them.
   Exception type alone does not classify an `InvalidOperationException` as a defect.
3. Do not attach exception objects, messages, stacks, inner chains, Data contents, payloads,
   expressions, SQL, addresses, headers, credentials, transaction tokens, savepoint names,
   entity values, or caller-supplied correlation values to new safe events.
   Runtime exception type is acceptable under the existing server policy. Additional fields,
   including structured HTTP status, require an explicit source and privacy checks.
4. Preserve existing exceptions, cancellation, interception, fault contracts, and projection
   behavior. Identify response-processing failure after a possibly completed write without
   claiming rollback or authorizing automatic retry.
5. Preserve current metrics and their compatibility. Document their attempt boundary accurately;
   do not replace them with per-success logs or treat them as projection outcome measurements.
6. Use ordinary injected class loggers and existing typed Entity Framework loggers where suitable.
   Explicitly constructed clients need explicit wiring. Surface broken logger acquisition or
   configuration. Logging delivery failures must not replace an existing operation failure.
7. Establish ownership at each boundary before adding events. Avoid duplicate client reports from
   nested catches where ordinary local control flow makes this straightforward, and preserve
   existing Entity Framework events without copying server events. Duplicate elimination is not
   an absolute requirement; do not add coordination machinery solely to suppress duplicates.
   Expected cancellation should not become an Error event. Successful operations remain quiet
   except for existing split diagnostics and enabled metrics.
8. Validate synchronous and asynchronous query failures, serialization before send, returned
   faults, malformed responses, cancellation, projection enumeration, concurrency, transactions,
   logger configuration, logger delivery failure, privacy, and unchanged metric behavior.
   After relevant implementation changes, exercise console and Blazor queries, projection,
   loading, saves, commit, cancellation, and failure rollback with normal configuration.

## Chained deployments

The owner added this required case on 2026-10-10: a server's Entity Framework context may itself
use InfoCarrier as its provider. For example, client A calls server B, whose store context is
another InfoCarrier client calling server C. B is both a server for A and a client of C.
This is a required validation topology, not a claim that this audit has exercised it.

Logging must compose through ordinary injected loggers, categories, and existing host scopes
and tracing. Do not assume a process has only one role, use process-wide client/server mode,
or suppress a downstream client event merely because an upstream server dispatch is active.
Keep observed phase and operation local to each invocation, including concurrent and nested calls.
Do not introduce a custom cross-hop identifier or copy caller metadata into safe event fields.

If C rejects a request, C can report its server failure, B can report receiving a downstream
fault, and B can report failure of its upstream dispatch. Those describe distinct boundaries;
they are not duplicates solely because they originate from the same cause. Within each boundary,
nested catches should avoid repeating the same observation when ordinary local control flow suffices.
On 2026-10-10 the owner explicitly preferred tolerating duplicate logs in chained deployments over
inventing unnatural structures to eliminate them. Do not add cross-hop deduplication state,
exception markers, ambient suppression scopes, or a shared diagnostics subsystem solely for that
purpose. Existing host context should let operators interpret these events together without falsely
attributing C's execution phase to B.

Server-log forwarding remains separately granted at each hop. A forwarding grant at C must not
implicitly authorize forwarding from B to A. Check the existing capture and replay behavior before
designing changes, including whether replayed events enter another capture. Do not turn new safe
client events into repeated forwarded server diagnostics or relax any forwarding grant.

Validation must include A-to-B-to-C success, a downstream fault, a downstream transport failure,
propagated cancellation, concurrent requests, and forwarding enabled and disabled at each hop.
Check local event ownership, host scope preservation, bounded event volume per boundary, metric
attribution to each local exchange, and unchanged failure propagation. A failed downstream write
response must not become evidence that the write was rolled back at either upstream layer.

Source verification for planning: `BeginLogCapture` requires the current server's forwarding grant.
`CollectedLog` applies that server context's sensitive-logging gate. `ServerLogReplay` writes through
the current context's logger factory, so replayed downstream events can enter an active upstream
capture. Preserve this composition; no deduplication mechanism is required.

The first design decision is event ownership across exchange and query boundaries. The strongest
case against adding events is duplication: asynchronous queries already report request failures,
server faults already have server events, and metrics already count attempts. Any implementation
must show the additional local information and operator action each event provides.

## Audit verification

The Release transport suite reported Passed: 65, Failed: 0, Total: 65 on 2026-10-10.
This includes current server logging, cancellation, HTTP transport, and round-trip metric tests.
The initial sandboxed run reported Passed: 27, Failed: 38, Total: 65 with Windows Event Log
access failures. Running the same built tests outside the sandbox passed without code changes.
The final output is retained locally in `artifacts/client-logging-audit-transport.log`.
Document link validation passed. No new behavior, privacy, or interactive sample tests were added;
the gaps above are source findings, not newly reproduced regressions.

## Implementation follow-through, 2026-10-10

The owner approved implementation after the audit. The
[implementation plan](client-logging-plan.md) records the subsequent comparison with Microsoft's
providers and the revised channel contract: standalone transport logging uses an explicit class
logger; query/save events use the existing injected Entity Framework diagnostics infrastructure.
New event payloads contain safe bounded fields and no exception object. Existing split-query
diagnostics remain available for successful client evaluation. Catch-and-rethrow follows Microsoft's
enumeration pattern; immediate residual evaluation and deferred enumeration have distinct ownership.
Application logs and existing Entity Framework request-failure events may overlap with these local observations.

Server forwarding cleanup is deferred to
[issue #216](https://github.com/azabluda/InfoCarrier.Core/issues/216), milestone 11.0.0, as likely breaking.
The client work preserves the shipped forwarding API and behavior.
