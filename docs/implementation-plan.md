# Implementation plan — M8 (productization)

Rolling checkbox detail for the **current** milestone only. M6's plan (Phases A–C) is in
[`archive/implementation-plan-m6-phase-c.md`](archive/implementation-plan-m6-phase-c.md) and is
never edited again.

Milestone-level scope lives in [`roadmap.md`](roadmap.md). Do not put scope here.

The suite stands at `Total tests: 22453, Passed: 22219, Failed: 13, Skipped: 221` (`c96`). All 13
are classified in C96 of the archived plan; none is a blocker for M8.

## Phase H — the HTTP transport (spec: `superpowers/specs/2026-08-11-blazor-wasm-sample-design.md` §10 phase 1)

Detailed steps are in
[`superpowers/plans/2026-08-11-northwind-http-transport.md`](superpowers/plans/2026-08-11-northwind-http-transport.md).
**That document is the "how"; this one is the record of what landed and what it measured.**

- [x] **M8-1. The spec measurement is scoped to one project, and the M8 plan is open.** `<this commit>`
- [x] **M8-2. The shared Northwind model, and a test project of its own.** `<this commit>`
- [x] **M8-3. `HttpInfoCarrierTransport`, the client half of the wire.** `<this commit>`
  Review found the malformed-body path unhandled: `DeserializeAsync` throwing on a non-envelope
  200 body (e.g. a proxy/captive-portal HTML page) surfaced a raw `JsonException` instead of
  `InfoCarrierTransportException`. Closed in M8-3a `<this commit>`: the deserialize call is now
  wrapped and rethrown via the exception type's two-argument constructor, `OperationCanceledException`
  passes through untouched, and a fourth test covers it.
- [x] **M8-4. The server endpoint, and the first real HTTP hop in this repo.** `<this commit>`
  One route (`MapInfoCarrier`), all nine operations — `InfoCarrierEnvelopeServer` already checks
  the protocol version, dispatches, and turns a server-side failure into a fault carried in the
  response, so the endpoint adds no policy of its own. `Program.cs` registers `DbContext` as well
  as `NorthwindContext` (`InProcessInfoCarrierServer` resolves the base type) and calls
  `AddInfoCarrierStandardValueMappers`, which a server must do for itself (C89). No sample types
  in the endpoint file: promoting it to `InfoCarrier.Core.AspNetCore` is a file move.
  `NorthwindServerFactory`'s given `Dispose(bool)` raced the host's async shutdown on Windows —
  deleting the temp SQLite file before every native handle was released threw `IOException`
  (`dotnet test` exit code 1 despite `Passed: 9, Failed: 0`); Linux tolerates deleting an open
  file, which is why CI (`ubuntu-latest`) would never have seen it. Moved to an async
  `DisposeAsync` override that awaits the base class first, clears
  `Microsoft.Data.Sqlite`'s connection pool, and swallows a residual `IOException` best-effort: the
  first test in this file opens a transaction and never ends it, which is intentional (Task 6
  needs `InProcessInfoCarrierServer` to hold it open across requests), and that server is a
  singleton that is not itself `IDisposable`, so nothing ends the transaction at host shutdown.
  Passed: 9, Failed: 0, Total: 9.
  Review found the endpoint itself catching nothing: a malformed request body and the protocol
  version refusal (`NotSupportedException`, deliberately outside `DispatchAsync`'s own fault
  handling — the two ends disagree about what an envelope is, so answering with one would be
  optimistic) both fell through to ASP.NET Core's default handling, which under Development
  leaked a stack trace and under Production returned no message at all — so
  `An_unsupported_protocol_version_is_refused_by_number` passed only because the Development
  stack trace happened to contain "999", not because the endpoint said so. Closed in
  M8-4a `<this commit>`: the endpoint now catches both paths and answers a deliberate HTTP 400
  whose body is only the exception's own message, `NorthwindServerFactory` pins
  `UseEnvironment("Production")` so the test no longer depends on ambient hosting configuration,
  and the client's existing non-success path turns that 400 into `InfoCarrierTransportException`
  by design. Same review found `NorthwindServerFactory.DisposeAsync` deleting only the main
  `.db` and leaking its `-wal`/`-shm` (and occasionally `-journal`) sidecar files on every run;
  those three paths are now deleted alongside the main file with the same best-effort semantics.
  Passed: 9, Failed: 0, Total: 9 — confirmed again under
  `ASPNETCORE_ENVIRONMENT=Production DOTNET_ENVIRONMENT=Production`, filtered to the
  protocol-version test alone: Passed: 1, Failed: 0, Total: 1.
- [x] **M8-5. A query over HTTP, end to end — the premise of the product, asserted.** `<this commit>`
  `NorthwindOverHttpTest`: a filtered entity query, a projection that crosses as columns rather
  than as entities, an aggregate answered by the server, and a navigation lazy-loaded via
  `UseLazyLoadingProxies()` over a second HTTP round trip. All four passed on the first run — a
  legitimate result, since the transport is thin by design and Tasks 2–4 already proved the two
  models agree. Filtered: Passed: 4, Failed: 0, Total: 4. Whole project: Passed: 13, Failed: 0,
  Total: 13 (Task 3's review round, M8-3a, added a fourth test to that task after the plan's
  arithmetic was written, so every running total downstream of it is one higher than printed
  there).
  Review found three of the four tests asserting values but not the mechanism their names claim —
  a defect could pass every one of them. Closed in M8-5a `<this commit>`: a new `RecordingHandler`
  (a small reusable `DelegatingHandler`, threaded through a `CreateClientContext(factory, out
  RecordingHandler)` overload that leaves the original overload working for Task 6) now lets the
  projection test assert the excluded `OrderDate` column never crosses the wire, the aggregate test
  assert one request and a sub-700-byte response (measured: 448 bytes for the real scalar), and the
  lazy-loading test — the one that matters most — assert the request count is 1 right after the
  initial query and increases once after each navigation is touched. Deliberately broken and
  restored to confirm that last assertion can fail: asserting the count stayed at 1 after touching
  `order.Customer` failed with `Assert.Equal() Failure: Expected: 1, Actual: 2` against the
  correct implementation. Total unchanged at 13 — three tests strengthened, none added.
- [x] **M8-6. `SaveChanges` and a transaction over HTTP — the write half.** `<this commit>`
  `NorthwindWritesOverHttpTest`: several edits crossing as one save (2 `OrderDetail.Quantity`
  changes, one `SaveChangesAsync`), an insert getting its store-generated key back by correlation
  id, a rolled-back transaction leaving the store untouched, and a committed one visible to a
  later context. `IInfoCarrierServer` was already a singleton (M8-4), so the transaction pair
  passed against the existing registration with no product change. Every test verifies through a
  **second, freshly constructed `NorthwindContext`** rather than re-reading the first context's own
  change tracker, so a client that merely echoed its local state back to itself could not satisfy
  any of the four. All four passed on the first run: Passed: 4, Failed: 0, Total: 4. Whole project:
  Passed: 17, Failed: 0, Total: 17 (the M8-5 arithmetic note applies again — M8-3a's review round
  added a fourth test to Task 3 after the plan's running totals were written, so this project's
  true count has been one higher than the plan's printed figures since that round; 16 was the
  plan's stale figure, 17 is correct).
  Review found three of the four tests asserting values but not the mechanism their names claim —
  the same shape as M8-5a's review, and this task's own report had already flagged one of the
  three. Closed in M8-6a `<this commit>`: `Several_edits_cross_as_one_save` now uses
  `CreateClientContext(factory, out RecordingHandler recorder)` and asserts the request count rose
  by exactly one across `SaveChangesAsync`, so two edits shipped as two separate round trips can no
  longer pass under the name "one save". `An_insert_gets_its_store_generated_key_back` now re-reads
  the inserted row by its returned id through a fresh context and asserts its `Name`, closing the
  gap where any positive integer — a row count, a stale id from a broken correlation-id lookup —
  would have satisfied the old `> 0` check alone. `A_rolled_back_transaction_leaves_the_store_untouched`
  now asserts `before + 1` on the same context immediately after `SaveChangesAsync` and before
  `RollbackAsync`, so a `SaveChangesAsync` that was a silent no-op inside an open transaction can no
  longer pass under this test's name — deliberately broken (asserted `before` instead) and observed
  to fail with `Assert.Equal() Failure: Values differ / Expected: 6 / Actual: 7` before being
  restored. Every transaction still ends on every path (rollback or commit) inside its own `using`
  block. No test added: Passed: 4, Failed: 0, Total: 4 filtered; whole project unchanged at
  Passed: 17, Failed: 0, Total: 17.
- [x] **M8-7. CI, and the honest record of what Phase 1 did not close.** `<this commit>`
  `InfoCarrier.Core.TransportTests` now runs as a second step in the fast-gate job, kept separate
  from `InfoCarrier.Core.FunctionalTests` because the spec ratchet's number means "inherited spec
  tests failing" and must not absorb transport tests. Two stale figures in `roadmap.md` corrected
  to the current measurement (`c96`: 22453 total, 13 failing) and a stale CI note describing the
  workflow before `51f4684` deleted. Passed: 17, Failed: 0, Total: 17 (`InfoCarrier.Core.TransportTests`);
  spec suite unchanged at `FAILING: 13  TOTAL: 22453`.
  Review found the correction left `roadmap.md`'s "Where we are" paragraph contradicting itself:
  the new `Failed: 13` sentence was immediately followed by a leftover breakdown — "40 wait on the
  B12 decision, 26 are the `MaterializationInterception` topology B24 settled, 9 are a locale
  defect" — that summed to 75 and named three categories CLAUDE.md already records as resolved
  (B12 taken in C80, `MaterializationInterception` clear since C71, the locale defect fixed by
  C50). Closed in M8-7a `<this commit>`: that sentence is gone, folded into one paragraph that
  states only the current count and its classification. No code change; no test run needed.

- **`SystemTextJsonInfoCarrierSerializer` uses reflection-based `JsonSerializer`.** Its
  `JsonSerializerOptions` sets no `TypeInfoResolver`, so the envelope and the request/response
  records are serialized reflectively. That is fine untrimmed and **will fail in a trimmed Blazor
  WASM build**, where the SDK sets `JsonSerializerIsReflectionEnabledByDefault=false`. Note that
  the *expression tree* is already safe — it goes through the source-generated
  `ExpressionJsonContext`. So the fix is bounded: a source-generated context for the envelope and
  the nine operation payloads. **This is Phase 2's first task.**
- **The response direction is bounded by `MaxRequestBytes`.** `InfoCarrierEnvelope` implements
  `IInfoCarrierRequest`, and `InfoCarrierPayloadLimits.Guard<T>` picks its bound from that
  interface — so a client deserializing a *response* envelope applies the 64 MiB **request**
  bound. The envelope's own doc comment already says the two legs are not distinguished and that
  fixing it is part of M5's envelope criterion. Harmless for Northwind; a large result would fail
  confusingly.
- **M8's HTTP transport criterion is formally still open**, because the two transport files live
  in `samples/` rather than in packages. Both are free of sample types, so the promotion is a
  file move; see the spec §4.1.
