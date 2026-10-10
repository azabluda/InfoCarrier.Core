# Client logging implementation plan

Goal: make client failures operationally useful without exposing request contents or changing
query, exception, cancellation, or transaction behavior.

Requirements: [client logging audit](client-logging-audit.md), including the owner's chained
deployment and simplicity decisions. Work serves
[client logging #215](https://github.com/azabluda/InfoCarrier.Core/issues/215).

## Scope and owner decisions

- Preserve public constructor signatures. Add overloads; do not change serializer, transport,
  or fault interfaces.
- Preserve projection behavior. [Projection #218](https://github.com/azabluda/InfoCarrier.Core/issues/218)
  remains separate. No retry or automatic client evaluation is added.
- Use existing injected logging dependencies. No mutable static diagnostic state, service lookup,
  custom scopes, exception markers, deduplication cache, suppression counters, or classifier service.
- Chained deployments use ordinary host loggers and scopes. Duplicate observations are acceptable
  when eliminating them would require artificial coordination.
- Preserve explicit constructors without loggers. Explicit logger acquisition errors propagate;
  new event-delivery errors cannot replace the operation failure.
- New events contain only bounded operation, observed phase, bounded outcome, and runtime exception
  type. No exception object, message, stack, request values, credentials, or query contents.
- Existing Entity Framework diagnostics, forwarding grants, sensitive-forwarding gates, and metrics
  retain their contracts. Existing channels can contain exception details; new events do not sanitize them.
- Work uses the existing checkout and an ordinary branch, without a worktree or second clone.

On 2026-10-10 the owner deferred the proposed separate server cleanup commit to
[static-state and server logging audit #216](https://github.com/azabluda/InfoCarrier.Core/issues/216),
marked likely breaking and assigned to milestone 11.0.0. Preserve the shipped server forwarding
public API and behavior throughout 10.x.

## Provider conventions, revised 2026-10-10

The original plan used the underlying query/update `ILogger` directly. The owner requested a
comparison with Microsoft providers before delivery. That revealed an integration gap: direct
calls omit `LogTo`, `DiagnosticSource`, and `ConfigureWarnings` behavior.

The revised design uses Entity Framework's existing diagnostic infrastructure for query/save
provider events. It keeps ordinary injected class logging for `TransportInfoCarrierClient`, which
can run independently of a context. This does not add a shared diagnostics service.

Reference source, pinned at `a6217e3438ca1fb430079f2626056c1a11581927`:

- [Microsoft InMemory query enumeration](https://github.com/dotnet/efcore/blob/a6217e3438ca1fb430079f2626056c1a11581927/src/EFCore.InMemory/Query/Internal/InMemoryShapedQueryCompilingExpressionVisitor.QueryingEnumerable.cs)
  obtains the query diagnostic logger from the execution context, logs enumeration failures, and rethrows.
- [Microsoft relational query enumeration](https://github.com/dotnet/efcore/blob/a6217e3438ca1fb430079f2626056c1a11581927/src/EFCore.Relational/Query/Internal/SingleQueryingEnumerable.cs)
  uses the same failure/cancellation ownership pattern.
- [Core diagnostic extensions](https://github.com/dotnet/efcore/blob/a6217e3438ca1fb430079f2626056c1a11581927/src/EFCore/Diagnostics/CoreLoggerExtensions.cs)
  pair `ShouldLog` with `NeedsEventData` and `DispatchEventData`.
- [Diagnostic logger construction](https://github.com/dotnet/efcore/blob/a6217e3438ca1fb430079f2626056c1a11581927/src/EFCore/Diagnostics/Internal/DiagnosticsLogger.cs)
  receives factories, options, and dispatch dependencies through injection.

Use the already registered `InfoCarrierLoggingDefinitions` for event definitions, with bounded
instance caches per event and severity. Immutable event identifiers and stateless extension methods
follow the existing provider convention; they hold no ambient sink or hidden service provider.
`ClientFailureEventData` exposes only the safe fields, without retaining an exception or context.
Warning configuration controls provider logging as usual. `DiagnosticSource` enablement remains
independent, following Entity Framework's own contract. A warning configured to throw or a failing
subscriber must not replace the original operation exception, because these events report an
operation that already failed.

## Event ownership

| Owner | Event | Observed phases |
|---|---|---|
| Transport class logger | 35100 | Request payload serialization; transport exchange; returned server fault; response payload deserialization |
| Context query diagnostics | `ClientQueryFailure` (35101) | Query preparation; request construction; result materialization; client projection; server log replay |
| Injected update diagnostics | `ClientSaveFailure` (35102) | Change request construction; server log replay; generated value application after response |

The transport owns exchange observations for all nine operations. Query/save wrappers do not add
another new event around that call. Existing Entity Framework request-failure and interception
events remain. A custom `IInfoCarrierClient` owns its exchange diagnostics.

Query logging uses `QueryContext.QueryLogger` from the current invocation, never a logger captured
in a compiled-query delegate. Save logging uses the already injected update diagnostics logger.
Immediate residual evaluation and later enumeration have distinct catches: `ApplyResidual` runs
before `Guarded` begins, so one propagated projection exception cannot traverse both catches.
Application logging and existing Entity Framework events can still overlap with these observations.

| Observation | Default level | Outcome |
|---|---|---|
| Cancellation exception with the supplied token cancelled | Debug | CallerCancellation |
| Other cancellation exception | Error | UnrelatedCancellation |
| Transport concurrency fault | Information | ConcurrencyConflict |
| Other returned server fault | Information | ServerFaultReceived |
| Query preparation failure, including translation refusal | Information | PreparationFailed |
| Other local failure | Error | UnclassifiedFailure |

Do not infer timeout, configuration failure, or a defect from exception names or message text.
HTTP send and envelope processing remain one exchange phase. Processing failures after a response
make no rollback or retry claim.

Successful split queries retain `QuerySplit`, its operator/key diagnostics, and warning configuration.
No additional success event is added. Unsupported client filtering retains its translation refusal.

## Implementation and acceptance

The following tasks form one atomic client logging commit. The deferred server change has no
implementation in this branch.

- [x] CL1: preserve the two-argument transport constructor and add an explicit typed-logger overload.
  Observe failures once in `SendAsync`, without changing `RoundTripAsync` metrics.
- [x] CL1: test every operation, phase, cancellation classification, concurrency fault, disabled
  logging, throwing enablement/delivery, explicit null logger, structured fields, privacy, and success.
  Test one metric counter/histogram pair for serialization, decoding, returned-fault, and cancellation failures.
- [x] CL2: observe query preparation, request construction, materialization, immediate/deferred
  projection, replay, change mapping, and generated-value application without changing behavior.
- [x] CL2: use provider diagnostic event dispatch. Test `ILogger`, `LogTo`, `DiagnosticSource`,
  warning configuration, safe typed payloads, and preservation of the original failure.
- [x] CL2: test shared compiled-query logger/scope isolation and throwing delivery. Exercise synchronous
  and asynchronous processing. Preserve existing cancellation/concurrency interception contracts.
- [x] CL3: test actual A-to-B-to-C store contexts, separate host factories, concurrent scopes,
  downstream faults/transport failures/cancellation, and a committed write followed by failed decoding.
- [x] CL3: test the four forwarding-grant combinations and sensitive forwarding denial. Preserve
  existing replay composition without requiring globally unique events.
- [x] CL3: wire the console with a standard factory and typed logger, and Blazor with its existing
  dependency injection logger. The console logging package is sample-only, not a product dependency.
- [x] CL3: complete normal console and Blazor query, paging, loading, save, commit, and failure-rollback checks.
  The sample has no cancellation control; cancellation is exercised through automated transport/chained tests.
- [x] Complete final review and all delivery gates against the revised provider implementation.

## Verification and delivery

Run the full Release transport project and `eng/measure.sh` across both specification projects.
Inspect failing names and reasons, not only counts. Rebuild Release with `CI=true` so the modified
product projects compile under strict settings. Run `eng/trim-ratchet.sh`, package validation,
document-link validation, `git diff --check`, and the file-hygiene hook for every changed file.
Keep known Razor and trim warnings visible; do not suppress new warnings.

Review privacy, exception preservation, context/scope ownership, diagnostic channel behavior, and
absence of deduplication mechanisms. The original private-logger implementation had an independent
review with no confirmed product defect. The diagnostic-channel revision receives targeted review
and complete regression validation before delivery.

Create one pull request with `Assisted by OpenAI Codex.` and dated validation evidence. Stop while
continuous integration runs. Multiple commits require a merge commit. Do not create a release.
After an owner-authorized merge, delete the local branch and fetch with prune.
