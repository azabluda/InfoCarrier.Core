# Server failure logging

Implementation of [issue #214](https://github.com/azabluda/InfoCarrier.Core/issues/214).
On 2026-10-09 the owner approved replacing the proposed shared diagnostics subsystem with
ordinary injected class loggers. The fault envelope, HTTP statuses, exception rehydration,
cancellation propagation, and log-forwarding grants retain their existing contracts.

## Ownership and registration

InfoCarrierEnvelopeServer accepts ILogger<InfoCarrierEnvelopeServer> in an additive constructor.
Its original two-argument constructor remains available without logging. Non-HTTP hosts can
construct the dispatcher with their own logger or use ActivatorUtilities. Each invocation keeps
its phase in local variables, so concurrent and nested dispatches do not share diagnostic state.

InProcessInfoCarrierServer accepts ILogger<InProcessInfoCarrierServer> in an additive constructor.
Normal host dependency injection selects this constructor when logging is registered. The logger
has the server's lifetime and remains available during transaction resource disposal. A singleton
server requires a logger suitable for singleton consumption; it does not retain a scoped factory.
The original one-argument constructor remains usable without a logger.

The ASP.NET Core adapter obtains its endpoint logger from the current request's logger factory.
It passes the request's typed dispatcher logger to InfoCarrierEnvelopeServer. It reports malformed
outer envelopes and failures in body reading, final envelope serialization, or response writing.
It does not repeat failures already observed by the dispatcher. Existing host logging scopes and
tracing provide request correlation; no extra request identifier or caller metadata is generated.

## Messages and severity

| Boundary | Level | Event |
|---|---|---|
| Dispatch failure mapped to a fault | Error | 35001 |
| Concurrency conflict mapped to a fault | Information | 35001 |
| Request-token cancellation | Debug | 35001 or 35011 |
| Unrelated cancellation | Error | 35001 or 35011 |
| Unsupported protocol version | Warning | 35002 |
| Outer envelope cannot be read | Warning | 35010 |
| HTTP body or response boundary failure | Error | 35011 |
| Idle transaction removed before cleanup | Warning | 35020 |
| Cleanup rollback, transaction disposal, or scope disposal failure | Error | 35021–35023 |

Messages identify the failed action. Dispatch fields contain only a bounded operation and the
locally observed phase. Result serialization explicitly follows operation completion; a failure
there does not imply that a completed write was rolled back. A fault-response serialization
failure is observed before it escapes. No exception messages are parsed to infer a cause.
On 2026-10-10 the owner approved a bounded diagnostic addition after exercising the samples.
A missing anonymous shape registration logs Reason=AnonymousShapeNotRegistered and a fixed
explanation. The catalog marks this exception with a server-only Data entry keyed by its Type;
the dispatcher reads only that entry, never message text. The exception remains an
InvalidOperationException, and the fault mapper does not transmit the marker. Other mapped faults
use Reason=UnclassifiedFailure, which does not label every refusal as unexpected. Each mapped
fault also logs ExceptionType, using the exception's runtime type name. Operation, phase, severity,
and fault responses retain their existing behavior. No classifier service or taxonomy is added.
Host category filters control
verbosity. Normal successful cleanup is silent; eviction reports intent rather than claiming
rollback completion. Cleanup attempts every resource stage and shutdown awaits pending evictions.

## Privacy and limits

Application events do not attach exception objects, messages, stacks, Data contents, payloads, expressions,
SQL, transaction/savepoint tokens, client correlation values, or caller identities. Existing
Entity Framework and host providers keep their own policies. SQL command logging remains available
under Microsoft.EntityFrameworkCore.Database.Command when enabled by host filters.

There is no built-in rate limiter, suppression counter, custom meter, caller hash, or ambient
request context. Every failed dispatch remains observable. Hosts needing output limits or
aggregation must configure their logging pipeline; category filtering alone is not rate limiting.
This narrows the original issue proposal according to the owner's design review.
Logger registration, resolution, and creation failures propagate to the host. The adapter requires
its logging services and does not substitute a null logger for broken configuration. Exceptions
from event delivery are still caught so they cannot replace an existing fault or interrupt cleanup.
The original explicit constructors without logging remain available for compatibility.

For unexpected mapped failures, the safe default is the operation, phase, runtime exception type,
and UnclassifiedFailure reason. Raw exception diagnostics remain outside these application events;
the host's other logging categories retain their own policies. Additional known reasons require
an explicit source signal and privacy tests, rather than interpreting arbitrary exception text.

## Verification

Owned tests cover injected loggers, safe fault contents, nested and concurrent dispatches,
serialization after completed operations, cancellation severity, logger backend failure,
malformed envelopes, query validation and execution failures, transaction refusals, concurrency,
SQL log preservation, and idle/shutdown resource cleanup failures. A fake clock drives eviction
and container-shutdown tests without sleeps. The direct-injection regression failed before the
implementation because the previous dispatcher did not consume the host's typed logger.

Validation on 2026-10-09: final measurement reported zero failures across 30,120 specification
tests, with unchanged failing names and reasons against server-failure-diagnostics-final.
Release transport reported Passed: 64, Failed: 0, Total: 64. Strict Release rebuilding, binary
package compatibility, documentation, links, word budgets, and file hygiene passed. Trimming
remained at 107 product warnings; the strict build retained five documented Razor warnings.
The subsequent owner review rejected silent logger-acquisition fallbacks. Both acquisition
regressions now require the original configuration exception to propagate instead of HTTP 400.

The configuration correction was measured as visible-logger-configuration: zero failures across
30,120 specification tests, with unchanged failures and reasons against simple-class-logging.
All 64 transport tests passed; strict Release rebuilding and trimming passed with unchanged
warning counts. Isolated published samples passed console queries, anonymous projection, saves,
and rollback; Blazor passed customer paging, explicit loads, saving, transaction commit, and
deliberate database failure with rollback. The dispatcher emitted event 35001 for that failure,
and server SQL commands remained visible. The owned test server was stopped afterward.

Validation on 2026-10-10: safe-failure-reasons reported zero failures across 30,120 specification
cases, with the same 234 skips and unchanged failing names/reasons against visible-logger-configuration.
All 65 transport tests passed. The reason and exception-type tests failed before implementation;
they also verify the unchanged exception/message, a marker-free wire fault, and rejection of
message-based or string-key-based classification. Strict Release rebuilding passed with the five
known Razor warnings; trimming retained 107 product warnings. Independent review found no material
issue. Normal isolated console and Blazor samples passed queries, projection, paging, explicit
loads, saves, commit, and failure rollback. The database failure logged its exception type without
its message. The owned validation server was stopped, and the owner's sample failure probes were
restored byte-for-byte and left uncommitted.
