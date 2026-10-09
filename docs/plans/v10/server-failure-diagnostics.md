# Safe server failure diagnostics

Implementation of [issue #214](https://github.com/azabluda/InfoCarrier.Core/issues/214), approved
on 2026-10-08. The fault envelope, HTTP statuses, exception rehydration, cancellation propagation,
and log-forwarding grants retain their existing contracts. No release work is included.

## Registration and lifetime

Register the host meter factory and one diagnostics instance per host:

~~~csharp
services.AddMetrics().AddInfoCarrierServerDiagnostics();
~~~

The sample server does this. Existing applications opt in explicitly. The original dispatcher
constructor remains available and does not enable diagnostics. A non-HTTP envelope host passes
its injected diagnostics instance and current scoped logger factory to the additive constructor:

~~~csharp
var dispatcher = new InfoCarrierEnvelopeServer(server, serializer, diagnostics, loggerFactory);
await dispatcher.DispatchAsync(envelope, cancellationToken);
~~~

The HTTP endpoint opens a diagnostic scope before reading the body and retains it through the
response write. The dispatcher joins that scope; it does not emit a duplicate application event.
A non-HTTP dispatch opens its own scope. A fresh server-generated request identifier and a bounded
operation name identify each request. Envelope correlation strings are not logged.

State belongs to the injected instance: request context, suppression windows, counters, and caller
hash key. Register a custom TimeProvider before diagnostics registration to control time in tests.
IMeterFactory owns the meter lifetime. No assembly friendship is required.

Request logger factories come from the current request scope. The server creates a separate cleanup
diagnostic scope during construction, before container shutdown can prevent scope creation. It
retains that scope until shutdown has awaited both registry cleanup and pending eviction cleanup.
This is a server-owned cleanup scope, not a copy of the transaction scope's services. Missing or broken
logging services do not replace the original fault or stop cleanup. Diagnostic-scope teardown is
best effort, like event delivery; a broken logging backend cannot report its own failure reliably.

## Events, reasons, and levels

The logger category and meter are both InfoCarrier.Server.

| Event | Meaning |
|---|---|
| 35001 RequestFailed | One application failure outcome per request, before rate limiting |
| 35002 CleanupOutcome | Eviction intent, successful cleanup, or a failed cleanup stage |
| 35003 FailuresSuppressed | Suppressed count for one fixed reason in the preceding window |

Information: invalid payload, payload limit, invalid descriptor, permission refusal, unknown
operation, concurrency conflict, expected cancellation, and successful cleanup.

Warning: missing shape registration, protocol mismatch, missing transaction, wrong server instance,
caller mismatch, and idle eviction intent.

Error: unexpected execution/rebinding/result-mapping failure, database failure, response serialization
or write failure, unexpected cancellation, and rollback/transaction-disposal/scope-disposal failure.

Exception messages are never parsed. Known refusal sites attach a typed reason which fault mapping
ignores. Unknown failures retain a generic reason and their observed phase. In particular, an
arbitrary InvalidOperationException during query execution may be a translation failure, interceptor
failure, or another execution failure; diagnostics do not pretend to distinguish these from its type.

ResultSerialization follows operation completion. FaultSerialization identifies a secondary failure
while constructing an error response. EnvelopeSerialization identifies the final response envelope.
These phases do not claim that a completed mutation was rolled back. Request-token cancellation
is informational; an unrelated OperationCanceledException remains an error and still propagates.
Transport input/write IOException with a cancelled request token is expected cancellation.

Cleanup attempts rollback, transaction disposal, and resource-scope disposal independently. Each
failed stage is observed even when earlier stages fail. CleanupCompleted requires every stage to
succeed. TransactionEvicted means removal from the registry, not successful rollback. Hosts without
diagnostics registration retain a safe eviction warning, without the old bearer-token disclosure.
A harmless rollback after commit remains silent.

## Privacy and repeated events

Structured fields contain fixed reason, phase, and operation values; generated request identifiers;
and an optional caller pseudonym. No exception object, stack, message, inner message, Data contents,
credentials, transaction/savepoint identifier, payload, expression, SQL, key, or entity value is added.
Existing Entity Framework and host providers retain their own logging policies.

Caller pseudonyms require AddInfoCarrierServerDiagnostics(includeCallerIdentity: true). They use
only IInfoCarrierServerCallerIdentity supplied by trusted hosting code. The keyed hash is stable
within one diagnostics instance and changes across hosts/restarts. Empty or overlong identities are
omitted. Hashing is a privacy policy choice, not anonymization; the default is no caller metadata.

Each reason has its own fixed window: five detailed events per minute of monotonic time. The next
event for that reason after the window emits the suppressed count and resumes details. Summaries
are traffic-driven; idle reasons need no timer. Refusal traffic cannot spend another reason's budget.
The fixed reason array bounds aggregation memory; there are no caller/token/payload dictionaries.

The infocarrier.server.failures counter includes all request and cleanup failures, even when logs
are suppressed. Successful cleanup and eviction intent do not count as failures. The
infocarrier.server.suppressed counter counts all limited events. Both use only the fixed reason tag;
request and caller identifiers never become metric dimensions. Normal host log filters still apply.

## Validation

Owned tests cover real HTTP refusals, shape registration, malformed inner/query data, descriptor/type/
method/operator refusals, real query translation and database paths, SaveChanges concurrency,
transaction routing and caller ownership, non-HTTP faults, cancellation, response serialization,
logger/listener failure, scoped factories, independent hosts, privacy, overlapping requests,
burst counters, suppression recovery, refusal/error isolation, background eviction, and shutdown
cleanup failures. Injected clocks drive both suppression and actual timer callbacks without sleeps.

The review reproduced incorrect operator severity, missing legacy eviction warnings, and cleanup
outcomes lost after scoped logger disposal. A container-owned shutdown regression also failed
before cleanup logging was established during server construction. Tests cover singleton and
scoped factories and a pending eviction that shutdown must await.

Validation on 2026-10-09: the final specification measurement reported zero failures across
30,120 tests, with no fixed/broken names or changed failure reasons against structural-registration.
Transport reported Passed: 77, Failed: 0, Total: 77. The nonincremental strict Release build,
package compatibility, documentation build, links, and word budgets passed. Trimming remained
at 107 product warnings. The Release build retained five documented Razor warnings.
