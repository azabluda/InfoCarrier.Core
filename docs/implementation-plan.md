# Implementation plan — M8 (productization)

Rolling checkbox detail for the **current** milestone only. Closed milestones are archived and
never edited again:

| Milestone | Plan |
|---|---|
| M6 — spec-base adoption (Phases A–C) | [`archive/implementation-plan-m6-phase-c.md`](archive/implementation-plan-m6-phase-c.md) |
| M9 — provider neutrality (Phase J) | [`archive/implementation-plan-m9-phase-j.md`](archive/implementation-plan-m9-phase-j.md) |

Milestone-level scope lives in [`roadmap.md`](roadmap.md). Do not put scope here.

**M9 closed 2026-08-17 and M8 did not**, which is why this file went back to holding one milestone
by *removing* the newer one. Phase J is archived; Phases H and I below are M8's and stay.

The suite stands at `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` (`j21`). All nine
are classified in the archived M9 plan, and stated for consumers in
[`limitations.md`](limitations.md). None is a blocker for M8.

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
  C50). M8-7a `<this commit>` removed that sentence, but its replacement added two claims sourced
  nowhere in `docs/`: "none of them blocks M8", and a coined "the other three are single-shape
  gaps" breakdown that a careful reader could misread against C96's own table (two of the three
  singletons it would name are A28 verdicts — permanent, not gaps). Closed in
  M8-7b `<this commit>`: reverted to the brief's own step-2 text verbatim — the measured figure,
  where the classification lives, and how many are permanent, nothing more. No code change; no
  test run needed.
- [x] **M8-8. The final fix wave — a tautological mechanism assertion, the security review's own
  trigger, and cleanup.** `<this commit>`
  `A_projection_crosses_as_columns_rather_than_as_entities` searched the raw HTTP response body
  for a plaintext ISO date, but the recorded body is `InfoCarrierEnvelope`, whose `Payload` is a
  `byte[]` System.Text.Json renders as base64 — an alphabet with no `-`, so the assertion could not
  fail regardless of what the payload carried. Deserializing `Payload` alone was still not enough:
  a Query response's payload is itself `QueryDataResult`, whose `SerializedResults` is *again* a
  `byte[]` — the row data lives two base64 layers past the recorded bytes, not one. The test now
  decodes both. Proved failable: projecting `OrderDate` alongside the two existing columns (a
  temporary, reverted change) failed with
  `Assert.DoesNotContain() Failure: Sub-string found ... Found: "2026-01-05"` against the corrected
  assertion; the original one-layer fix passed unchanged under the same broken query, which is why
  the second layer had to be found rather than assumed. `docs/security-review.md` §8 amended
  (2026-08-12): the network transport its final paragraph named as the trigger for revisiting §2
  and §6 has now shipped, and two properties recorded there changed character as a result —
  `InProcessInfoCarrierServer._transactions`, an unbounded, unexpiring registry now reachable by a
  caller that can vanish mid-transaction, and `InfoCarrierEndpointExtensions.MapInfoCarrier`'s
  unbounded body read, which sits below the product's own `MaxRequestBytes` and makes that limit
  unreachable behind a default Kestrel host. Both are also recorded above, in this same list.
  `CLAUDE.md`'s two `dotnet test` commands re-pointed from `InfoCarrier.Core.slnx` to
  `test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj`, matching the
  scope `eng/measure.sh` already uses, with a note explaining why — the solution now also contains
  `InfoCarrier.Core.TransportTests`, and running both together reports a `Total` that
  `test/known-failures.txt` was not written against. A stale `FirstOrDefaultAsync` in a comment
  corrected to `SingleOrDefaultAsync`, matching the code beside it. License headers added to the
  seventeen new `.cs` files under `samples/` and `test/InfoCarrier.Core.TransportTests/` that
  shipped without them. Passed: 17, Failed: 0, Total: 17.

- [x] **M8-9. A console demo, and the first query over a real TCP socket.** `<this commit>`
  `samples/Northwind.Demo`, plus a launch profile pinning the server to `http://localhost:5199` so
  both halves run with a bare `dotnet run`. It exercises the six things Phase 1 built — a filtered
  query, a projection, an aggregate, lazy loading, one save for two edits, and a rolled-back
  transaction — and prints the **round-trip count** beside each, which is the part worth seeing.
  **It closes a gap in the assurance, not only in the demo:** the 17 transport tests use
  `WebApplicationFactory`, whose `TestServer` is an **in-memory** pipeline, so they exercise the
  envelope, routing, serialization and the endpoint but no query had ever crossed a real TCP
  socket. This does, and the observed behaviour matches the tests exactly — 14 round trips, a lazy
  load costing one each when the navigation is touched, two edits costing one save. **The byte
  counter was removed rather than made to look right**: the first end-to-end run printed
  `0 bytes received`, because the endpoint writes to the response stream without setting
  `Content-Length`, and measuring it would mean reading the body out from under the transport
  about to read it. A demo printing a wrong number is worse than one printing no number.
  `samples/README.md` documents both commands, the expected output, and which two files must stay
  free of Northwind types. No product code changed and no spec test was added or removed, so the
  suite is untouched at `Failed: 13, Total: 22453` — and `eng/measure.sh` needed no attention
  despite a fourth project joining the solution, which is M8-1 paying for itself.
  Passed: 17, Failed: 0, Total: 17.

### What Phase 1 left open

Recorded by M8-7 and M8-8 as findings rather than absorbed. Each names where it is picked up.

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
- **`InProcessInfoCarrierServer._transactions` is an unbounded registry with no expiry.** In
  process, an abandoned entry died with the test host. Behind HTTP, a client that vanishes
  mid-transaction pins a service scope, a `DbContext` and a store connection indefinitely — there
  is no eviction, no timeout, and no binding of a transaction token to the caller who created it.
  The one SQLite file triplet leaked per test run, previously filed only as a test-cleanup
  nuisance, is the observable symptom of exactly this. Recorded in `docs/security-review.md` §8
  (2026-08-12 amendment).
- **`InfoCarrierEndpointExtensions.MapInfoCarrier` reads the request body with an unbounded
  `CopyToAsync` and copies it again with `ToArray()` before `InfoCarrierPayloadLimits.Guard` ever
  sees a length.** Kestrel's 30 MB default bounds it in a real host, but that is accidental, and it
  sits below the product's own 64 MiB `MaxRequestBytes` — so the product's deliberate limit and its
  deliberate message are unreachable behind a default Kestrel. Recorded in
  `docs/security-review.md` §8 (2026-08-12 amendment).

## Phase I — the browser (spec: `superpowers/specs/2026-08-11-blazor-wasm-sample-design.md` §10 phase 2)

Detailed steps are in
[`superpowers/plans/2026-08-16-northwind-blazor-wasm.md`](superpowers/plans/2026-08-16-northwind-blazor-wasm.md).
**That document is the "how"; this one is the record of what landed and what it measured.**

Phase 1 proved the wire, so a failure in this phase is a failure of the browser rather than of the
protocol — which is the reason the spec split the phases here. Only two tasks touch anything the
spec suite can see (M8-11 and M8-16); the rest are `samples/` and cannot move it.

- [x] **M8-10. The Phase 2 plan is open.** `<this commit>`
  Two environment facts were probed before planning rather than assumed, because each would have
  changed the plan's shape. `dotnet new blazorwasm` exists on this SDK; and a trimmed publish
  **does not need the `wasm-tools` workload**, which is not installed here — ILLink runs and the
  publish succeeds, the workload buying AOT and native relinking, which spec §3.2 explicitly does
  not want. That second fact is load-bearing: §3.2 chose `UseLazyLoadingProxies()` over
  `ILazyLoader` on the reasoning that trimming and AOT are separate axes and a Blazor release
  publish trims without AOT, and the toolchain agrees — the publish's
  `Publishing without optimizations … recommend wasm-tools` line is about AOT, while ILLink's own
  `Optimizing assemblies for size` line shows trimming ran.

- [x] **M8-11. The envelope is source-generated, and `ReferenceHandler.Preserve` turned out to be
  dead weight.** `<this commit>`
  `InfoCarrierJsonContext` covers the envelope and every operation payload, so with
  `ExpressionJsonContext` beside it nothing this provider puts on the wire is serialized
  reflectively. **The type set was read off the call sites rather than guessed** — every generic
  argument `IInfoCarrierSerializer` is instantiated with — and **the spec suite is what proves the
  set is closed**: once a resolver is set, System.Text.Json does not silently fall back to
  reflection for an undeclared type, it throws `"JsonTypeInfo metadata for type 'X' was not
  provided"` and names it. 22,453 tests drive this serializer and none of them hit that. Suite
  unchanged: `Total tests: 22453, Passed: 22219, Failed: 13, Skipped: 221`, with **identical name
  lists and identical reasons** against `c96`. `InfoCarrier.Core.TransportTests`: Passed: 17,
  Failed: 0, Total: 17.
  **Two things the enumeration got wrong, and both were found by running it rather than by reading
  it.** First, `BeginTransactionAsync` sent `new { }` — an *anonymous type*, declared as `object`
  and therefore serialized by its **runtime** type, which no source-generated context can have
  metadata for (`'<>f__AnonymousType0' was not provided`). It is now `null`, which is what every
  other void operation already puts there and which the server never reads either way.
  Second, and larger: **`ReferenceHandler.Preserve` cannot be carried onto a source-generated
  context here, and did not need to be.** A generator cannot call an `init` accessor after
  construction, so it sets `required`/`init` members through an object initializer, which
  System.Text.Json treats as parameterized construction — and reference handling is unsupported
  there. **The refusal is structural, not data-dependent**, which a probe settled in one run: an
  envelope carrying a fault serializes to `"fault":{"$id":"2",…}` with **no `$ref` anywhere in the
  document**, and then fails to read back with `"Reference metadata is not supported when
  deserializing constructor parameters … Path: $.fault.$ref"`. Reading that message as a statement
  about the data would have sent the fix in the wrong direction.
  **Dropping it costs nothing because nothing at this layer was using it**, and that is checkable
  rather than hopeful: every nested graph — expression tree, dynamic values, query results — is
  serialized through `ExpressionJsonContext.Default.<TypeInfo>`, whose own options set no reference
  handler, and arrives here already reduced to a `byte[]` (`SerializedQuery`, `SerializedResults`,
  `SerializedValues`). What this serializer sees is flat records with no repeated instance in them.
  The node model handles its own repeats with its own `Ref` mechanism.
  **It is still a wire change** — `$id` no longer appears on an envelope — and both ends change
  together, so protocol version 1 is unaffected.
  A stale claim was corrected on the way: `ExpressionJsonContext`'s `MaxDepth` comment credited
  *"the transport serializer already sets `ReferenceHandler.Preserve`"* for turning a repeated
  instance into a `$ref`. That was wrong for exactly the reason the same comment gives two
  paragraphs later — a context carries its own options, and these nodes never serialized through
  the transport serializer's.

- [x] **M8-12. The browser runs, and `UseLazyLoadingProxies()` works in WebAssembly.** `<this commit>`
  `samples/Northwind.Client`: a Blazor WASM app whose `DbContext` has no database, served by
  `Northwind.Server` from the same origin (`UseBlazorFrameworkFiles` + `MapFallbackToFile`), so
  `dotnet run --project samples/Northwind.Server` is the whole story and there is no CORS. The
  wire inspector is an **`IInfoCarrierTransport` decorator** (`InspectingTransport`), not a change
  to `HttpInfoCarrierTransport` — that file must stay free of sample types for its promotion to
  remain a file move (spec §4.1) — and it doubles as a demonstration of what the seam is for.
  **Spec §3.2's open question is answered, in a browser, with evidence.** It had recorded automatic
  lazy loading as *"an experiment with a known answer if it fails"*. The Customers page prints the
  runtime type of the rows it loaded and it reads **`CustomerProxy`**: Castle DynamicProxy emits
  types under the Mono interpreter, so the `ILazyLoader` fallback is not needed and the model stays
  clean. That leaves `RunAOTCompilation` as the only configuration where it would not hold, which
  a Blazor release publish does not use.
  **Verified by executing the app, not by serving it.** `dotnet run` plus headless Edge
  (`--headless=new --virtual-time-budget --dump-dom`) renders the real WASM app and dumps the DOM,
  which is a materially stronger check than curling `index.html` — the 17 transport tests already
  cover the protocol, and what was unproven here was the *browser*. The dump shows the rows
  (`ALFKI`, `Alfreds Futterkiste`), the runtime type, one round trip, and no error bar.
  **It earned its keep immediately by catching a defect of mine.** The first run rendered an error
  bar reading `InvalidOperationException: NodeAlreadyHasParent` and **no data at all**: `WireDecoder`
  assigned an already-parented `JsonNode` back into its own slot, and because the decode runs inside
  the transport decorator, *the inspector broke the query it was observing*. Two changes, and the
  second is not a substitute for the first: the walk now returns a **replacement or null** so an
  in-place expansion is never reassigned; and `Describe` catches broadly and renders the failure as
  text, because a debugging aid must not be able to fail the operation it observes.
  One packaging note: `Components.WebAssembly` floors
  `Microsoft.Extensions.DependencyInjection` at 10.0.9, above the 10.0.0 that
  `CentralPackageTransitivePinningEnabled` pins repo-wide (NU1109). Resolved at the time with a
  `VersionOverride` **in the sample**, on the stated grounds that the central entry was "the
  product's dependency floor". **That reasoning was wrong and M8-16 corrected it** — no project
  declares a `PackageReference` to that package, so the entry is a *transitive pin* and raising it
  changes no declared dependency of `InfoCarrier.Core`, which references only
  `Microsoft.EntityFrameworkCore` and `.Relational`. The override is gone and the central version
  is 10.0.9.
  No product code changed, so the spec suite is untouched. No `FluentIcon` anywhere: Fluent UI
  ships its icon set as a separate large package, and a sample about the wire should not pull one
  in to decorate a nav menu.

- [x] **M8-13. The Customers page, and the projection split is visible rather than asserted.**
  `<this commit>`
  Country filter, sort selector and pager become `Where`, `OrderBy`, `Skip` and `Take` on one
  `IQueryable`; the grid's four columns become a `Select` into a **client-only** `CustomerRow`
  record. **The panel shows the split doing its work**, and the contrast is on screen in the same
  session: the projection's response payload carries `System.ValueTuple`4` with `Item1`…`Item4`
  (`"ALFKI"`, `"Alfreds Futterkiste"`, `"Berlin"`, `"Germany"`) and **zero `Customer` entities**,
  while the *whole-entity* probe beside it does carry one. `CustomerRow` is never named on the wire
  at all — ADR-010 splits the `Select` into a server-side tuple projection plus a client-side
  reassembly, which is why a client-only type in a projection needs nothing from
  `TypeAllowlist.ForModel`'s unused `registeredTypes` parameter (spec §5.1's observation, now
  confirmed by running it). The pager's total is its own `CountAsync`, so the panel also shows the
  server answering with a number rather than rows.
  **The browser found a second real defect, and the fix was a design correction rather than a
  guard.** The page first held one `DbContext` for its lifetime and died on
  `InvalidOperationException: A second operation was started on this context instance before a
  previous operation completed` — Blazor renders between the awaits of `OnInitializedAsync`, so the
  grid's provider started while the proxy probe was still in flight. A `DbContext` is not
  thread-safe and a grid is entitled to ask again before the previous answer lands. This page is
  read-only, so it has no unit of work to hold: it now takes a context per load from the factory,
  which is what `IDbContextFactory` exists for. The Order page keeps a page-scoped context because
  it genuinely needs one.
  Verified in headless Edge: no error bar, 4 round trips (all `Query`), page one showing
  ALFKI/ANATR/AROUT with BERGS correctly on page two, and `CustomerProxy` still reported.

- [x] **M8-14. The Order page — and automatic lazy loading does not work in a browser, for a
  reason nobody had written down.** `<this commit>`
  Unit of work end to end, observed with real clicks: **1** round trip loads the order, **2** after
  asking for its `Customer` (*"Alfreds Futterkiste — Berlin"*), **3** after its lines, and **4**
  after saving — where the panel shows a single `#4 SaveChanges` carrying **both** edited
  quantities, and the page reports *"Last save: 2 entries, in 1 SaveChanges envelope."* The pending
  count is read off `ChangeTracker.Entries()` rather than counted by hand, so setting a value back
  to its original correctly un-counts it.
  **THE FINDING. Spec §3.2 is refuted, and its recorded fallback would not have helped.** §3.2
  decided on `UseLazyLoadingProxies()` and named one risk: Castle DynamicProxy needs
  `Reflection.Emit`, which AOT removes. **That risk does not apply** — proxies are created fine
  here, which is why the Customers page can report `CustomerProxy`. The actual blocker is
  unrelated: **a navigation property getter is synchronous, so a lazy load must BLOCK on the HTTP
  round trip, and a single-threaded WebAssembly runtime cannot block.** `order.Customer` throws
  `PlatformNotSupportedException: Cannot wait on monitors on this runtime` — and throws *after* the
  request has already gone out, so the panel shows the round trip while the value never arrives,
  which is what makes it confusing to diagnose. **`ILazyLoader.Load()` is synchronous too**, so
  §3.2's "cheap by construction" fallback fails identically; it was cheap for the wrong reason,
  because the cost was never in the model. What works is `Entry(x).Reference(…).LoadAsync()` and
  `Entry(x).Collection(…).LoadAsync()`, and the demonstration is unharmed: the navigation is still
  not fetched by the original query and still costs exactly one round trip. **The constraint is the
  browser's, not this provider's** — any EF provider reaching its store over a network has it, and
  a desktop client of this provider lazy-loads normally. `UseLazyLoadingProxies()` stays on both
  halves for model parity (A49). Spec §3.2 carries a dated amendment; the Customers page note,
  which had claimed proxies were "what lets a navigation load itself on demand", is corrected.
  **Two verification lessons, both cheap and both reusable.** `--dump-dom` renders a page but
  **cannot click anything**, and the first run of this page therefore reported nothing at all — the
  check moved to the **DevTools protocol** (`--remote-debugging-port` plus `Runtime.evaluate`),
  which is what made the finding above observable rather than theoretical. And the page now loads
  order 1 on arrival instead of waiting for a button, which is better design independently of the
  test: a page that shows nothing until you press something hides what it is there to show.
  A naming collision worth remembering: the component had to be `OrderPage`, not `Order` — a
  component named `Order` shadows `Northwind.Shared.Model.Order` inside its own markup and every
  reference to the entity fails to compile. The route is unaffected.
  No product code changed, so the spec suite is untouched.

- [x] **M8-15. The Transfer page — a rollback that is checked against the store, not against the
  change tracker.** `<this commit>`
  Two saves inside one `BeginTransactionAsync`: move order 1 to another customer, and take a unit
  off a product's stock. A checkbox gives the second save a customer id no row has, so **the
  server's** database refuses it. Both paths driven in headless Edge.
  **Happy path:** committed; order 1 moved to `ANATR` and stock fell 38 → 37.
  **Forced failure:** the fault arrives with its chain intact —
  `DbUpdateException ← DbUpdateException ← InfoCarrierServerException: SQLite Error 19:
  'FOREIGN KEY constraint failed'` — and **the store is untouched**: order 1 is still `ANATR`
  rather than the requested `AROUT`, and stock is still 37, so the rollback undid the stock
  decrement as well as the customer change. That is W5 and M4 in one action, and it is C83's
  degradation rule visible in a sample: a browser has never heard of SQLite, so the server's
  `SqliteException` arrives as the nearest type the client can actually construct, carrying the
  message rather than losing it.
  **Every assertion here is read back through a freshly created context**, so a client that merely
  echoed its own change tracker could not satisfy any of them — the same discipline M8-6 used for
  the write tests, for the same reason.
  The panel shows the whole shape, which is what spec §5.1 asked of this page:
  `#3 BeginTransaction` → `#4/#5 Query` → `#6 SaveChanges` → `#7 CommitTransaction`, then two more
  queries for the read-back.
  No product code changed, so the spec suite is untouched.

- [x] **M8-16. The compiled model — and a second thing WebAssembly will not do.** `<this commit>`
  **SUPERSEDED BY M8-18 (2026-08-16): the compiled model is REMOVED, and this entry's central
  claim — that it was generated against the client's configuration — is false.** It was the
  server's. Read M8-18 before anything here.
  `dotnet ef dbcontext optimize` output in `samples/Northwind.Client/CompiledModel/`, wired with
  `options.UseModel(...)`, so the browser builds no model by reflection at start-up (spec §7
  step 2). `dotnet-ef` is pinned in `.config/dotnet-tools.json`; the exact command is in
  `samples/README.md`.
  **THE FINDING, and it is EF's rather than ours.** A compiled model **cannot be used in Blazor
  WebAssembly as generated**: EF's generated `NorthwindContextModel` initializes itself on a
  `new Thread(…, 10 * 1024 * 1024)` — a deliberately large stack, because a big model can overflow
  the default one (EF issue 31751) — and WebAssembly has no threads. Merely *reading*
  `Instance` throws `TypeInitializationException` wrapping
  `PlatformNotSupportedException: Arg_PlatformNotSupported` from `Thread.Start`, and **the app
  never renders at all**. EF ships the escape hatch in the generated file itself:
  `AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true)` initializes inline
  instead, and the large stack buys nothing for five entity types. Set as the first line of
  `Program.cs`. **This is the second WASM threading constraint this phase has found** — M8-14's was
  the synchronous lazy load — and they are the same shape: something in the stack assumes it may
  block or fork, and a browser may do neither.
  **Two things about generating it that the plan had wrong.** The plan said "generate against the
  **server's** configuration"; that is backwards — the compiled model is the *client's*, because the
  two halves build their models with different providers and only the client's is an InfoCarrier
  model. What the server provides is the `--startup-project`, and it has to, because **the SDK emits
  no `deps.json` for a Blazor WebAssembly project** and `dotnet ef` cannot load one. The server
  already references the client (it hosts it), so the client's assembly and its
  `IDesignTimeDbContextFactory` are reachable from the server's output. `UseModel` is called
  explicitly rather than relying on auto-discovery, because that keys off an assembly attribute in
  the *DbContext's own* assembly and `NorthwindContext` lives in `Northwind.Shared` — `dotnet ef`
  warns about exactly this and offers `UseModel` as the answer.
  `Microsoft.EntityFrameworkCore.Design` is referenced by the **server**, `PrivateAssets="all"`. That
  is the `dotnet ef` *tool's* requirement for a startup project; C90's finding stands, and the
  product still needs no Design dependency.
  **A pre-existing break was found and fixed on the way, and it was not caused by this work.**
  The solution stopped restoring with NU1109: `Microsoft.Extensions.DependencyInjection` pinned at
  10.0.0 against a transitive floor of 10.0.9 reached through
  `EFCore.Sqlite → Microsoft.Extensions.Logging 10.0.9`. Verified pre-existing by building
  `InfoCarrier.Core.TransportTests` against **HEAD's own** `Directory.Packages.props`, where it fails
  identically — so CI would have hit it regardless. Raised to 10.0.9, which changes no declared
  dependency because **no project references that package** except the Blazor sample: with
  `CentralPackageTransitivePinningEnabled`, the entry is a transitive pin. M8-12's `VersionOverride`
  and its stated reasoning are removed; that entry above is corrected.
  Verified in headless Edge with the compiled model in place: Customers renders rows and still
  reports `CustomerProxy`, the Order page goes 1 → 2 → 3 round trips across its two loads, and the
  Transfer page commits. Spec suite unchanged.

- [x] **M8-17. The trimmed publish gate — and spec §9's fourth criterion is NOT met.** `<this commit>`
  **The app publishes trimmed and works.** `PublishTrimmed=true`, driven end to end against the
  *published* output in headless Edge: Customers renders rows and reports `CustomerProxy`, the Order
  page goes 1 → 2 → 3 round trips across both async loads, and the Transfer page commits. That is
  spec §9's criteria 1, 2 and 3.
  **Criterion 4 — "no IL warning attributable to `InfoCarrier.Core`" — is not met. There are 86**,
  out of 1129 across all assemblies (EF Core 864, Castle.Core 127, the rest small). Spec §7 said a
  residue of ours "is not acceptable" and that a large result must be "reported with evidence
  rather than absorbed silently", so it is reported. **They are the provider's premise showing
  through, not sloppiness**: the wire carries a type's *name* and the far end resolves it, so
  `Assembly.GetType(string)`, `MakeGenericMethod`, `MakeGenericType` and a reflective object walk
  are what this provider is made of. `[DynamicallyAccessedMembers]` cannot say *"whatever type the
  caller's model happens to name"*. By cause: 45 are DAM mismatches (mostly `Type.GetInterfaces()`
  on a model-named type), 15 `MakeGeneric*`, 17 `IL2026` calls into APIs already marked
  `RequiresUnreferencedCode`. By type, the top four are `DynamicValueMapper` 12,
  `NodeToExpressionTranslator` 7, `TransparentIdentifierRewriter` 6, `PrimitiveCoercion` 5 — which
  is spec §7's own prediction, confirmed. **The honest fix is `[RequiresUnreferencedCode]` on the
  public query API**, telling callers the truth; that is a product decision and a milestone of its
  own, not a sample's to take.
  **So the gate is a ratchet, which is this repo's established answer to legitimately-not-green**
  (`eng/ratchet.sh` for the spec suite, now `eng/trim-ratchet.sh` for this). Ours must not rise;
  everyone else's is reported and not gated. Baseline and the full reasoning in
  `eng/trim-baseline.txt`. Wired into the fast gate. Nothing is suppressed:
  `SuppressTrimAnalysisWarnings=false` and `TrimmerSingleWarn=false` are set precisely so the
  warnings are *visible*, which the Blazor SDK otherwise hides by default.
  **Two traps the script itself fell into, both now guarded, and both instances of "establish that
  the code ran".** It reported **`OURS: 0`** on its first run — an *incremental* publish does not
  re-run ILLink, so the log had no diagnostics at all and the gate would have passed forever while
  regressions sailed through. It now deletes `obj/Release` and `bin/Release` first **and** refuses
  to believe a log with no ILLink banner in it. Separately, the first triage attributed **all 1129**
  warnings to this product, because ILLink appends `[C:\…\Northwind.Client.csproj]` to every line
  and **this repository's own path contains the string "InfoCarrier"** — classification is by
  *declaring member*, never by the path. Both mistakes inverted the conclusion, in opposite
  directions, and both were caught by looking at the actual lines.
  Proved failable: with the baseline lowered by one, the script exits 1 and names the rise; at
  baseline it exits 0.

### What Phase 2 left open

- **Spec §9 criterion 4 is not met: 86 IL warnings are ours.** Full reasoning in M8-17 above and in
  `eng/trim-baseline.txt`. The honest remedy is `[RequiresUnreferencedCode]` on the public query
  API — it tells a caller the truth instead of implying a guarantee this provider cannot give — and
  a source-generated type registry for the cases where the model *is* known ahead of time. That is
  a milestone, not a follow-up.
- **WebAssembly cannot block, and that fact reaches further than lazy loading.** Two independent
  instances turned up in this phase — the synchronous navigation getter (M8-14) and EF's
  thread-based compiled-model initializer (M8-16). Any other place this stack blocks or forks will
  fail the same way. **Neither is this provider's defect**, and neither affects a desktop or server
  client.
- **The sample has no automated test of its own.** The three pages were verified by driving a real
  browser over the DevTools protocol, and that harness lives in a scratch directory rather than in
  the repository. The 17 transport tests cover the protocol; nothing in CI would notice a page
  breaking. A Playwright project is the obvious shape, and the trim ratchet already proves the
  publish still *builds*.
- **The two transport files are still in `samples/`**, so M8's HTTP-transport criterion remains
  formally open, exactly as M8-7 recorded. Both remain free of sample types — the inspector was
  built as a decorator specifically to keep that true — so promoting them is still a file move.
- **`InProcessInfoCarrierServer._transactions` is still unbounded**, and the Transfer page is now a
  second way to reach it. **Scoped 2026-08-16 and it is the LIBRARY's, not the sample's** — see
  `roadmap.md` §M8, which splits it into an idle timeout (a sample can demonstrate this through the
  existing `IInfoCarrierServer` seam, but every consumer inherits the unbounded registry, so the
  library should own it), token ownership (needs the protocol to carry caller identity — impossible
  outside the library), and multi-instance survival (the registry is process-local).
  **The consequence was measured rather than reasoned about**: against an abandoned transaction
  that had already written, a second client still read correctly — isolation holds, it saw the
  pre-write value — but its **write blocked until the probe's own timeout**, because the abandoned
  transaction holds SQLite's write lock. One abandoned browser tab wedges writes for the whole
  server until the process restarts. Full detail in `docs/security-review.md` §8.

- [x] **M8-18. Remove what a browser cannot use — and the compiled model was the server's all
  along.** `<this commit>`
  Asked to keep the sample as clean as possible now that WebAssembly's limits are known. Two
  removals, and the second turned out to close a real defect rather than only tidy up.
  **1. Lazy-loading proxies are gone from the client** (`Program.cs`), because automatic lazy
  loading cannot work there at all (M8-14). Configured-but-unusable was the worst of both: it made
  an unloaded navigation throw `Cannot wait on monitors on this runtime` from inside a proxy
  instead of simply being `null`. The **server keeps them** — it is not a browser — and the
  asymmetry was checked rather than assumed: all three pages run green against a proxied server.
  `Order.Customer` stays `virtual`, which costs a client nothing and is what the server needs.
  **2. The compiled model is gone, and the reason is a defect, not tidiness.**
  `dotnet ef dbcontext optimize` **never called `NorthwindDesignTimeFactory`.** A Blazor WASM
  project emits no `deps.json`, so the server had to be the `--startup-project` — and EF's tooling
  then takes its configuration from the **startup application's own service provider**, silently
  ignoring an `IDesignTimeDbContextFactory` in the target project. **The generated model was the
  server's**: `Relational:TableName = "Customers"`, `Relational:Schema`, `Relational:SqlQuery`,
  `Proxies:LazyLoading = true`. Since M8-16 the browser had been running on a **relational, proxied
  model**, and it worked — which is the dangerous shape, because divergence between the two models
  is silent and yields wrong answers rather than errors (A49, B4, B12).
  **The evidence that named it was the removal of proxies.** Dropping
  `UseLazyLoadingProxies()` from the client broke every page with
  `ArgumentException: PropertyNotDefinedForType … ILazyLoader LazyLoader … Order` — the compiled
  model declared an `IProxyLazyLoader.LazyLoader` service property that a client without the
  Proxies extension cannot bind. **M8-16's regeneration had also been a no-op**: its diff was one
  GUID, which should have been the tell that the factory was not being consulted, and was read at
  the time as "proxies do not affect the model".
  Removed with it: `NorthwindDesignTimeFactory`, the eight generated files, the `Issue31751`
  `AppContext` switch (needed only to load a compiled model), `Microsoft.EntityFrameworkCore.Design`
  from the server, and `.config/dotnet-tools.json`. **The EF-issue-31751 finding stands and is kept
  in `samples/README.md`** — a compiled model *can* be used in WebAssembly with that switch; this
  sample simply has no correct way to generate one, short of a console project existing purely to
  be a startup project.
  Verified in headless Edge after both removals: Customers renders rows, the Order page goes
  1 → 2 → 3 round trips and then saves two edits in **one** `SaveChanges` envelope (4 total), and
  the Transfer page commits. Trim ratchet unchanged at **86 ours / 1129 total** — a compiled model
  never affected static trim analysis. `samples/README.md` rewritten around both constraints.
  M8-16's entry is marked superseded rather than edited, since its measurements were real and only
  its conclusion was wrong. No product code changed, so the spec suite is untouched.

- [x] **M8-19. The browser UI, and one line of `index.html` that silently disabled every layout
  style.** `<this commit>`
  Reported as "looks very unprofessional": bare links, misaligned controls, the inspector stacked
  under the content instead of docked.
  **Root cause was a missing `<link>`, not styling.** `index.html` never referenced
  **`Northwind.Client.styles.css`**, Blazor's scoped-CSS bundle — which carries every `b-xxxxxxx`
  rule belonging to this app *and* to referenced Razor class libraries. Without it the Fluent web
  components still render (they carry their own shadow-DOM styling from JavaScript, so buttons and
  fields looked fine) but **nothing lays out**: `FluentStack`'s `.stack-horizontal` has no
  definition, so every stack fell back to block flow. Confirmed by `document.styleSheets` listing
  only `reboot.css` and `app.css`.
  The shell is now a **CSS grid** (`header` / `nav` / `main` / `wire`) rather than nested stacks —
  three regions that must hold their proportions cannot collapse into block flow. The inspector is
  docked right, full height, with its header fixed and only its list scrolling. Pages gained a
  toolbar + card vocabulary, and `FluentDesignTheme` supplies light/dark from the OS.
  **Two alignment defects, and the geometry named both.** A labelled Fluent field is taller than a
  bare button, so `align-items: end` is needed to share a baseline; and Fluent renders a field's
  `Label` as a **sibling** of the input, so each label became its own column in the flex row — a
  `.field` wrapper makes label+input one item. Measured before/after: controls now share
  `bottom=299`. A later pass added the vertical rhythm between stacked panels (toolbar → cards was
  a 0px seam; now 16px) — and **that pass introduced a third defect and had to be corrected**: it
  gave the message bar `display: block`, which overrode the component's own grid layout and threw
  its intent icon above the text instead of beside it. **Position a Fluent component; never restyle
  its box.** Margin only now, and the success message dropped the ✅ it carried, which had been a
  second tick beside the one the component draws itself.
  Emoji instead of `FluentIcon`, which keeps the separate multi-megabyte icon package out.
  **A verification lesson worth more than the fix.** The headless browser served a **cached**
  `index.html` and `app.css` after both had been rewritten, which looks exactly like "the CSS does
  not work" and produced one round of confident wrong diagnosis. The harness now sends
  `Network.setCacheDisabled` before navigating. Separately, headless Edge **would not rasterize**
  this app for `Page.captureScreenshot` — a trivial page captured fine, so the pipeline worked and
  the app did not — and the layout was therefore verified by **measuring geometry** rather than by
  looking at pixels. The user confirmed the result visually.
  No product code changed, so the spec suite is untouched.

- [x] **M8-20. A store worth paging through.** `<this commit>`
  The seed was four customers and five orders — enough to prove a wire, not enough to look like an
  application. Now **65 customers, 240 orders, 476 order lines, 30 products, 8 categories**, so the
  Customers grid pages twelve at a time across six pages and every page change is visibly its own
  query (observed: round trips 2 → 4 on **Next**, page one holding ALFKI and page two CHOPS).
  **Generated from row indices, not from `Random`.** A seeded `Random` is reproducible in practice
  but ties exact-count assertions to a runtime implementation detail; index arithmetic is stable by
  construction. The multipliers are coprime with the row counts so the spread looks unpatterned,
  and the second and third line of an order step by 11 and 22 — distinct modulo 30, so no order can
  name a product twice and violate the `(OrderId, ProductId)` primary key.
  **The anchors did not move, and that was the constraint that shaped the change.** The transport
  tests address rows by identity — order 1 belongs to ALFKI and has two lines of 12 and 3, product 1
  is Chai at 18.00 — so customers ALFKI/ANATR/AROUT/BERGS, orders 1–5, their seven lines and
  products 1–6 are byte-identical to before; everything generated starts after them. Only the
  count-based expectations moved, and each was **measured** rather than estimated, with a throwaway
  probe that seeded a scratch SQLite file and printed them: customers 4 → 65, `Quantity >= 10`
  3 → 330, and the Germany filter from `["ALFKI"]` to the eight ids the seed now defines. That last
  one stayed an **exact set** rather than becoming a count, because a `Where` that silently matched
  everything would still produce a plausible number.
  Pages adjusted to suit: 12 rows per page, the order-id spinner bounded by
  `NorthwindSeed.OrderCount` instead of a hard-coded 5, and the Transfer page's target list read
  from the store rather than hard-coded so it cannot drift from the seed again.
  `InfoCarrier.Core.TransportTests`: Passed: 17, Failed: 0, Total: 17. No product code changed, so
  the spec suite is untouched.

---

## Phase K — packaging and release (M8 exit criteria)

- [x] **M8-19. The two products pack, and only the two products.** `<this commit>`
      `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` (`j23` against `j21`):
      **0 fixed, 0 broken, `REASONS: unchanged`.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      Both gates run because `Directory.Build.props` reaches `src/`, and both are neutral — which
      is what packaging metadata should be, and is worth having measured rather than assumed.

      **Version: `10.0.0-preview.1`, decided 2026-08-17.** `10.0.0` matches EF Core's major, which
      is the convention every EF provider follows and the fastest way for a reader to know which EF
      this targets. **`-preview.1` should stay until M8's exit criteria are met**: nine limitations
      are known, there is no gRPC binding, and results do not stream — and the last two may touch
      `IInfoCarrierTransport`. A stable `10.0.0` is a promise not to break that surface.

      **`IsPackable` is `false` in `Directory.Build.props` and `true` in the two `src/` projects.**
      The default is `true`, so `dotnet pack` on the solution would otherwise have produced a
      package for every sample and every test project. Verified by counting the output: **four
      files**, the two products and their symbol packages.

      **The nuspec was read rather than trusted**, and it is right: MIT as a licence *expression*,
      the README embedded, `<repository>` carrying branch and commit (so SourceLink works), the XML
      documentation shipped, and `InfoCarrier.Core` depending on `InfoCarrier.Core.Abstractions` at
      the same version — which is why the push order matters and why M8-20 says so.

      **Two findings, both recorded where the next reader will meet them:**

      - **The package declares a dependency nobody wrote.**
        `Microsoft.Extensions.DependencyInjection >= 10.0.9` is in the nuspec although no `.csproj`
        references it: `CentralPackageTransitivePinningEnabled` promotes a *pin* into the package.
        `Directory.Packages.props` claimed "raising it changes no declared dependency of
        InfoCarrier.Core" — true of the projects, false of the package, and now corrected there.
        Left as it is: the floor is real, and removing transitive pinning for the packable projects
        reintroduces the NU1109 the pin exists to prevent.
      - **The package README is `docs/nuget-readme.md`, not the repository README.** nuget.org
        renders a README but resolves images only against **absolute** URLs, so the banner would
        show as a broken image, and a dozen relative documentation links would be dead inside a
        package. A short package-focused readme avoids both. It is the one file that can drift from
        the repository README, so it is deliberately short and mostly pointers.

- [x] **M8-20. `release.yml` — pack on a tag, gate it, attach it; publish by hand.** `<this commit>`
      **Chosen 2026-08-17 over pushing automatically, and the reason is irreversibility.** A pushed
      NuGet version is immutable: it can be deprecated or unlisted, never truly withdrawn. So the
      irreversible step stays with a human holding the packages, and **no `NUGET_API_KEY` lives in
      this repository's secrets** — which also means an accidental tag cannot publish anything.

      The workflow runs **the same two gates `build.yml` runs**, because a release must clear at
      least what a push clears; packs with `ContinuousIntegrationBuild=true` (deterministic, and it
      normalises the paths SourceLink embeds); and attaches the four files to a GitHub Release,
      marked pre-release when the tag contains a hyphen.

      **It verifies the tag against the packaged version.** A `v10.0.0-preview.2` tag against a
      props file still saying `preview.1` would otherwise publish the wrong version under the right
      name, and nothing downstream would notice. The step fails loudly instead.

      `fetch-depth: 0`, because SourceLink stamps the commit and a shallow clone gives it nothing
      to stamp. The release body carries the two push commands **with exact filenames rather than
      globs** — `InfoCarrier.Core.*.nupkg` also matches the Abstractions package — and in the order
      nuget.org requires.

      **Not verified by execution.** This workflow has never run; it cannot be, without a tag. The
      YAML structure matches `build.yml`'s, the shell steps are straightforward, and a BOM was
      stripped so the file starts as `build.yml` does — but **the first tag is the test**, and that
      is worth knowing before relying on it.

- [x] **M8-21. Three cleanups, each of which was checked before it was believed.** `<this commit>`
      `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` (`j24` against `j23`):
      **0 fixed, 0 broken, `REASONS: unchanged`.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.

      **1. `InProcessInfoCarrierTransport` moved out of the product into the test project.**
      Referenced by three test files and by nothing in `src/` or `samples/` for its whole life, and
      its own docstring says what it is: v1's `SimulateNetworkTransferJson`, which **double-
      serializes on purpose** — request *and* response — so that wire-serializability failures
      surface in tests. That is right for a harness and wrong for any deployment. **A real
      in-process deployment needs no transport at all**: `InfoCarrierEnvelopeServer.DispatchAsync`
      is already a delegate a caller can hand to `IInfoCarrierTransport` directly, which is exactly
      what `InfoCarrierBackendTestStore` does — and its comment explains that it avoids this type
      because double-serializing the suite's largest payload would cost about 750 MB of JSON.

      **2. `AssemblyMarker` deleted — the only genuinely dead type in `src/`.** An `internal static
      class` with an empty body, zero references anywhere, and a docstring citing *"the build order
      in ADR-003"*. Scaffolding from the first commit.

      **The audit that found it is worth keeping, and so is its first wrong answer.** A sweep of
      every `src/` file for "referenced anywhere outside its own file" returned **five** candidates.
      Three were false: `InfoCarrierDatabaseFacadeExtensions`, `InfoCarrierServiceCollectionExtensions`
      and their methods are used by 11, 11 and 3 files respectively — **a static extension class is
      referenced through its methods, never through its type name**, so a name-based sweep cannot
      see it. The fifth, `Design/InfoCarrierDesignTimeServices`, is referenced by nothing **and must
      stay**: it carries `[assembly: DesignTimeProviderServices(…)]` and `dotnet ef` finds it by
      name at run time. **An unreferenced type is a question, not a verdict** — two of the five
      would have been deleted wrongly.

      **3. The nuspecs no longer declare a dependency nobody chose.**
      `CentralPackageTransitivePinningEnabled` is now **off for the two shipped projects only**, so
      `Microsoft.Extensions.DependencyInjection >= 10.0.9` is gone from both packages. Read back
      rather than assumed:

      ```
      InfoCarrier.Core              -> InfoCarrier.Core.Abstractions 10.0.0-preview.1
                                       Microsoft.EntityFrameworkCore 10.0.0
      InfoCarrier.Core.Abstractions -> Microsoft.EntityFrameworkCore 10.0.0
      ```

      The pin stays everywhere else, because the NU1109 it prevents is in the **test and sample**
      restore graph (EF Core's SQLite package floors `Microsoft.Extensions.Logging` at 10.0.9) and
      those projects still need it. A package declares what it references; a repository pins what it
      restores. Conflating the two put a constraint on every future consumer.

- [x] **M8-22. Two packages, split on the only line that costs anything.** `<this commit>`
      `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` (`j25` against `j24`):
      **0 fixed, 0 broken, `REASONS: unchanged`.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      `InfoCarrier.Core.TransportTests`: **17 of 17**.

      | Before | After |
      |---|---|
      | `InfoCarrier.Core` + `InfoCarrier.Core.Abstractions` | **`InfoCarrier.Core`** |
      | `samples/Northwind.Client.Transport` | folded into `InfoCarrier.Core` |
      | `samples/Northwind.Server/Transport/` | **`InfoCarrier.Core.AspNetCore`** |

      **`Abstractions` was merged away because the case for it does not survive checking.** A
      contracts package earns its keep when someone can reference it *without* the heavy
      dependency. This one referenced `Microsoft.EntityFrameworkCore` — `IInfoCarrierClient`
      takes a `DbContext` — so it dragged all of EF Core anyway, and **only `InfoCarrier.Core`
      ever referenced it**. `decisions.md` names it once, in a build-order list, with no rationale.
      Namespaces are unchanged (`InfoCarrier.Core`, `InfoCarrier.Core.Common`), so no consumer code
      moves; nothing is published, so this was the last free moment.

      **The transport split is asymmetric, and that is the finding.** The two halves look alike and
      are not:

      | Half | Needs | Verdict |
      |---|---|---|
      | `HttpInfoCarrierTransport` | `System.Net.Http` — **in the shared framework** | costs nothing → ships **in** `InfoCarrier.Core`, and stays WebAssembly-safe |
      | `MapInfoCarrier` | `Microsoft.AspNetCore.Builder`/`Http`/`Routing` | a **`FrameworkReference` to `Microsoft.AspNetCore.App`** → separate package |

      Folding the endpoint into the core package would make every WPF, MAUI and Blazor WebAssembly
      client an ASP.NET Core app in order to restore its data-access library. **A `FrameworkReference`
      is the right shape for the server half** — it adds no package dependency and no files,
      because the host already has the framework.

      **The promotion was a file move, exactly as predicted.** Both sample transport projects were
      written free of Northwind types *so that this would be true*, and their own comments said so
      — `Northwind.Client.Transport.csproj`'s description said "written to be promoted into an
      InfoCarrier.Core.Http package, at which point the move is a file move". It was: two `git mv`s
      and a namespace line each.

      **Verified by reading the nuspecs, which is the only thing that proves the split landed
      where it was claimed:**

      ```
      InfoCarrier.Core             -> Microsoft.EntityFrameworkCore 10.0.0
      InfoCarrier.Core.AspNetCore  -> InfoCarrier.Core 10.0.0-preview.1
                                      frameworkReference: Microsoft.AspNetCore.App
      ```

      One dependency on the package a client installs. **The trim breakdown corroborates it**:
      `InfoCarrier.Core` stayed at 88 and `Northwind` at 8, so the promoted transport brings no
      warnings of its own — a rise in one with a fall in the other would have meant it did.

      **The push order reversed and `release.yml` was updated with it.** It was "Abstractions
      first"; it is now "`InfoCarrier.Core` first, then `InfoCarrier.Core.AspNetCore`", which
      depends on it. A release workflow that names packages goes stale the moment the layout moves,
      which is why it changed in this commit rather than the next one.

---


## Phase L — streaming the wire (architecture.md §6a D7, half A)

- [x] **M8-23. The server stops holding the result set, and the change tracker is why it can only
      half stop.** `<this commit>`
      `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` (`m8-23b` against `j25`):
      **0 fixed, 0 broken, `REASONS: unchanged`.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.

      D7's buffering point 1 — `var results = new ArrayList()` in `ServerQueryExecutor` — is
      priced there as **low, internal**. Half of it is. The other half is a wire-visible behaviour
      change, and **the suite named it in one run: 0 fixed, 20 broken (`m8-23`), every one a skip
      navigation or an `IsLoaded` assertion in `ManyToMany*`.**

      **The obstacle is the change tracker, not the query.** `SerializeRows`' probes — `IsLoaded`,
      `IsTracked`, `ReadShadowValue`, `ReadJoinEntities`, `FindEntityType` — all interrogate the
      server's `IStateManager`, and under any behaviour that resolves identity the state manager is
      only complete once the query has been enumerated to the end: a later row can add to an
      earlier row's collection, and a join row's far side can have its inverse navigation marked
      loaded after the near side has already been written. Serializing row N before row N+1 has
      been materialized asks the tracker a question it cannot yet answer — and it **answers
      wrongly rather than refusing**, which is the B12/B4 shape. The tell was
      `Load_collection_using_Query_with_Include_for_same_collection` reporting `IsLoaded: false`
      for a navigation the server was holding.

      So the point splits, and only one half is unconditional:

      | Change | Applies | What it removes |
      |---|---|---|
      | Map and write each row straight into a `Utf8JsonWriter` instead of building a `List<DynamicValueNode>` first | **always** | the node graph — the largest of the three copies |
      | Pull rows from EF instead of draining them into an `ArrayList` | **`NoTracking` only** | the row buffer as well |

      **`NoTracking` is sound for a reason that is checkable rather than hopeful.** It creates no
      state entries at all, so every probe above already falls through to reading the navigation
      value on the instance, and EF returns a fresh instance per row with its includes
      materialized. Nothing a later row does can change an earlier one.
      `NoTrackingWithIdentityResolution` is **not** in scope, for the same reason `TrackAll` is
      not: it resolves identity, which is exactly the property that makes an earlier row's graph
      still mutable.

      **This is the behaviour a large read-only result set uses**, which is the case D7 wanted:
      C37's 560 MB and 111 MB Northwind results are no-tracking projections.

      **A leading run of null rows is counted rather than written.** The element type is the first
      non-null row's, and it is what a null row's node is typed by, so a null seen before any real
      row cannot be typed yet. Deferring one renumbers nothing — `DynamicValueMapper.MapValue`
      allocates a wire reference id only for a non-null, non-primitive value — and if every row is
      null the type stays unresolved, which is the answer the buffered version reached too.

      **The general lesson, and it is one this repo keeps relearning:** a buffer whose contents
      look inert can be load-bearing for something that reads *around* it. That `ArrayList` was not
      holding rows for its own sake — it was holding the query open until the tracker was complete,
      and nothing in the code said so.

---

- [x] **M8-24. The query response leaves the envelope, and the rows become a stream.** `<this commit>`
      `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` (`m8-24b` against `j25`):
      **0 fixed, 0 broken, `REASONS: unchanged`.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      `InfoCarrier.Core.TransportTests`: **17 of 17**.

      D7's buffering points 2 and 3, in one commit because they cannot disagree: the wire contract
      and both bindings.

      **What was in the way was not the `byte[]` but the nesting.** A query response was
      `SerializedResults` (a `byte[]`) inside `QueryDataResult` inside `InfoCarrierEnvelope.Payload`
      (another `byte[]`) — **two base64 layers**, each of which has to be complete before the one
      outside it can be written, and which between them inflate the payload by about 1.78×. No
      arrangement of flushes streams through that. So the query response stopped being an envelope
      payload at all.

      | | Before | After |
      |---|---|---|
      | Query response body | `InfoCarrierEnvelope` JSON, rows base64 two layers in | a top-level JSON array of `QueryStreamItem` |
      | `QueryDataResult.SerializedResults` | `byte[]`, the whole result set | `Rows`, an `IAsyncEnumerable<DynamicValueNode>` |
      | Transport seam | `SendAsync` only | plus `SendQueryAsync` |
      | Server seam | `DispatchAsync` for all nine | plus `DispatchQueryAsync`, and `Query` refused by the other |

      **The item is tagged rather than the array being an object with a `rows` member, and that is
      forced by the reader.** `JsonSerializer.DeserializeAsyncEnumerable` reads a *top-level* array
      incrementally and nothing else; a nested array would need a hand-written `Utf8JsonReader` loop
      over a buffered stream on the client. The tag costs about ten bytes a row and buys a
      source-generated reader and a source-generated writer.

      **A fault is now a trailing item, because a streamed failure has nowhere else to go.** The
      status line and the first rows are long gone by the time an EF translation failure or a store
      error arrives, so it cannot become an HTTP 500 or an envelope `Fault`. It is safe today
      *because* point 4 is deliberate: `ClientResultMaterializer` decodes to completion, so the
      trailing fault is always reached before any row reaches the caller. **Half (B) has to answer
      this again**, and that is now recorded on the type itself.

      **Three things measured or found rather than reasoned:**

      - **`JsonSerializer.Serialize(Utf8JsonWriter, …)` flushes the writer synchronously**, and a
        synchronous write to an ASP.NET Core response body throws *"Synchronous operations are
        disallowed"*. The endpoint was written the obvious way first — one `Utf8JsonWriter` over
        `Response.Body`, `WriteStartArray`, one `Serialize` per item — and **8 of the 17 transport
        tests said so in one run**. There is no `SerializeAsync` overload taking a writer, so the
        three punctuation bytes are written by hand and each item goes to the stream through
        `SerializeAsync`.
      - **The 22 656-test suite was getting its result-wire-format coverage for free, from a line
        that no longer exists.** `EnvelopeTransport` hands the envelope straight to the server and
        never serialized anything itself — `InfoCarrierEnvelopeServer` serialized the payload, and
        every result row in the suite crossed real JSON as a side effect of that. Moving the query
        response out of the envelope would have silently ended it, leaving 22 656 tests running
        against live `DynamicValueNode` objects. Both in-process test transports now round-trip
        every `QueryStreamItem` through `ExpressionJsonContext` explicitly, **per item, because a
        simulation that buffered them to serialize them together would be testing the opposite of
        the thing under test**.
      - **`DispatchAsync` refuses `Query` outside its own try/catch**, beside the version check.
        Inside, the refusal came back as a well-formed fault envelope — an error report about the
        server describing a defect in the caller's wiring — and a transport still wired the old way
        would have looked like a server that cannot answer queries.
      - **The trim gate went to 89, and the cause was a conditional rather than a new capability.**
        ILLink reports an unannotated `Type` flowing into `IModel.FindEntityType` once **per
        origin**, so `singleResult ? query.Type : GetElementType(query.Type)` written at the call
        site produced *two* warnings for *one* call. Behind a named method there is one origin and
        one warning, and the baseline stays at 88. Worth recording because the count moved for a
        reason that has nothing to do with how much reflection the code does — which is exactly the
        reading `eng/trim-ratchet.sh`'s own header warns against.

      **`MaxResponseBytes` now means something.** It could not be applied to a stream by the
      existing `Guard(payload.Length, …)`, which needs the bytes already in hand, so the HTTP client
      counts them as they are read and refuses at the point they pass. The default stays `null`;
      what changed is that setting it works on the path that made it matter. `IInfoCarrierSerializer`
      gained a `Limits` member for this, because the transport could otherwise only have reached it
      by testing for a concrete serializer type.

      **The element type in the header is the query's declared one, not the first row's runtime
      type** — a streamed header goes out before a row exists. It is also the better of the two: a
      lazy-loading proxy's CLR type is not in the model, so the old rule reported
      `IsEntityResult: false` for a result that was entirely entities. Nothing routes on either
      member; both are diagnostics.

      **The in-process server's lease now outlives its method.** Rows are mapped through the
      server's own `IStateManager`, so the `DbContext` has to stay open until the sequence is
      exhausted — the `finally` that used to release it would have disposed the context before the
      first row was asked for. An abandoned enumeration therefore pins a context, which is D7's own
      note; `await foreach` releases it, and nothing in this provider takes a `QueryDataResult`
      without draining it.

---

- [x] **M8-25. The proof that it streams, and the first version of it was worthless.** `<this commit>`
      `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` (`m8-25b` against `j25`):
      **0 fixed, 0 broken, `REASONS: unchanged`.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      `InfoCarrier.Core.TransportTests`: **18 of 18**.

      D7 says a green suite cannot tell streaming from buffering, and that is exactly what happened
      to the test written to tell them apart.

      **`StreamingOverHttpTest` asserts the one state buffering cannot reach**: the client holds the
      response header while the server has produced **zero** rows. A gated `IInfoCarrierServer`
      decorator holds the rows; the assertion is a count with no other explanation, not a duration.

      **The first version passed against a deliberately re-buffered wire.** It timed a
      `DelegatingHandler`, and `HttpCompletionOption` is applied by `HttpClient` *after* the handler
      pipeline returns — so a handler sees the response headers at the same moment whichever option
      is in force. The instrument was watching a boundary at which the two are identical. It now
      watches `IInfoCarrierTransport.SendQueryAsync` returning, which is where they differ.

      **Both directions were executed:** with `ResponseHeadersRead` the test passes; with
      `ResponseContentRead` put back it **fails on its deadline**. That is the whole value of the
      test, and it is the standing "establish that the code *ran*" rule in its other form — *a probe
      that passes is evidence only once it is known to be able to fail*. A streaming test that
      passes against a buffering implementation is worse than no test, because it also certifies the
      regression it exists to catch.

      **The instrument had to avoid the repo's own instrument.** `RecordingHandler` calls
      `ReadAsByteArrayAsync` on every response, so every transport test that uses it reads a fully
      buffered body no matter what the wire did. Those tests prove correctness and can say nothing
      about streaming.

      **`WebAssemblyEnableStreamingResponse` is now verified rather than remembered**, and the
      near-miss is worth recording because a wrong key would be **silent** — `HttpRequestOptions` is
      a dictionary and accepts anything. The literal is present in the **user-string heap** of both
      `Microsoft.AspNetCore.Components.WebAssembly.dll` (which writes it, from
      `SetBrowserResponseStreamingEnabled`) and the browser build of `System.Net.Http.dll` (whose
      `BrowserHttpHandler` reads it), at 10.0.11. **Reading the wrong heap gave the wrong answer
      first**: `strings` shows UTF-8 *metadata names*, and .NET string *literals* are UTF-16, so the
      first pass found only `System.Net.Http.WasmEnableStreamingResponse` — which is real, is in the
      same assembly, and is the **global** AppContext switch behind
      `DOTNET_WASM_ENABLE_STREAMING_RESPONSE` rather than the per-request option.

      **Docs updated in this commit, and one of them corrected an overclaim of its own.**
      `architecture.md` **D7** now records what was built; the §6a wire-layering entry records that
      the two base64 layers are gone for queries and still present for the other eight operations;
      `security-review.md` weakness 7 is narrowed. The correction: `MaxResponseBytes` was **already
      enforced**, by `Guard<QueryDataResult>(payload.Length, …)` on the buffered payload. That shape
      cannot work on a stream, so the honest statement is that the bound was **carried across rather
      than lost** — nothing new was added, but had nothing replaced it streaming would have
      *removed* an existing control. The quantity counted did change: the raw body rather than the
      base64-inflated payload, so an unchanged setting admits roughly **1.78×** as much row data.

      **`docs/limitations.md` is deliberately unchanged.** It claims to be a complete list of
      scenarios in EF's suite that behave unlike a normal provider, and streaming produces no such
      scenario — same API, same results, exception type and message preserved, which the unchanged
      run is the evidence for.

---

- [x] **M8-26. The browser proof, which corrected D7 rather than confirming it.** `<this commit>`
      `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` (`m8-26` against `j25`):
      **0 fixed, 0 broken, `REASONS: unchanged`.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      `InfoCarrier.Core.TransportTests`: **18 of 18**.

      D7 expected the per-request option to be load-bearing: *"Blazor WebAssembly buffers the whole
      response by default; streaming needs `SetBrowserResponseStreamingEnabled(true)` per request."*
      **Measured in a real headless browser, that is not what decides it on .NET 10.**

      | Request | Response stream, unwrapped |
      |---|---|
      | `ResponseHeadersRead` **+ option** | `BrowserHttpReadStream` — live |
      | `ResponseHeadersRead`, **no option** | `BrowserHttpReadStream` — live |
      | `ResponseContentRead`, no option | `MemoryStream` — buffered |

      **`HttpCompletionOption.ResponseHeadersRead` is the belt; the option key is the braces.** The
      option stays — it is the documented switch, it costs nothing, and a wrong key would be silent
      — but the comment on it now says which of the two does the work.

      **It took three discriminators, and the first two failed the way the streaming test did.**

      1. **Outer stream type.** `StreamContent+ReadOnlyStream` either way. Reported *"NOT STREAMING
         — the option made no difference"*: a confident wrong answer, when it had shown nothing.
      2. **`CanSeek`.** The wrapper delegates it, so it is identical either way. Reported
         *"INCONCLUSIVE"* — at least honest.
      3. **Unwrap to the inner stream, plus a third request that must buffer.** The third arm is
         what makes the other two mean anything: the probe has to demonstrate it can produce a
         different answer before agreement between the first two is evidence at all.

      **The static half was checked too, and the wrong heap gave the wrong answer first** (M8-25):
      `WebAssemblyEnableStreamingResponse` is in the **user-string** heap of both the Blazor
      assembly that writes it and the browser `System.Net.Http` that reads it. `strings` shows UTF-8
      *metadata names*, so a first pass found only `System.Net.Http.WasmEnableStreamingResponse` —
      real, adjacent, and the **global** AppContext switch rather than the per-request option.

      **The sample carries `BrowserStreamingProbe`** so the question is re-answered in whatever
      browser and runtime the sample is actually run on, rather than trusting a comment. **The
      sample's own trim count rose 8 → 9** — the probe reflects over a private BCL field, which is
      genuinely unprovable to the trimmer. That is the sample's number and is reported but not
      gated; `OURS` is unchanged at 88.

      **Driven over the DevTools protocol**, as M8-17 established: `--dump-dom` renders a page but
      cannot press a button, and the verdict is behind one. Two things cost a run each and are worth
      recording: Edge refuses a CDP WebSocket without `--remote-allow-origins`, and a cold Blazor
      WASM boot in headless Edge takes longer than a 120-second wait allows.

---

- [x] **M8-27. The version rationale and the roadmap, brought back to what is true.** `<this commit>`
      `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` (`m8-27` against `j25`):
      **0 fixed, 0 broken, `REASONS: unchanged`.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      Both gates run because `Directory.Build.props` reaches `src/` (M8-19's own note).

      **`-preview.1` keeps its suffix and loses one of its three reasons.** The note said the
      version should stay pre-release because nine limitations are known, there is no gRPC binding,
      and *"results do not stream yet — and the last two may touch `IInfoCarrierTransport`"*.
      Streaming landed and **it did touch that seam**: `SendQueryAsync`, `IInfoCarrierSerializer.Limits`,
      and `QueryDataResult` leaving the envelope. **That is the suffix being made good rather than
      an argument for dropping it** — the surface moved exactly as the note predicted, which is why
      nothing had promised not to move it. gRPC may move it again; half (B) will move
      `ClientResultMaterializer`.

      **`roadmap.md`'s exit criterion (A) is struck, with the two estimates that did not survive
      named there rather than only here**, because the roadmap is what a reader consults for scope
      and D7's original pricing is what they would otherwise carry away.

---

## Phase L — what half (A) left behind

**D7 half (B) is unchanged in scope but better specified**, and the addition is not the hook
lifetime it was already about. A streamed failure travels as a **trailing item**, and that is safe
today *only because* `ClientResultMaterializer` decodes to completion — the fault is always reached
before any row reaches the caller. **Yielding rows lazily removes that guarantee**, so (B) has to
answer fault delivery as well as the DI-scoped materializer hook. It is recorded on
`QueryStreamItem` itself so the next reader meets it where the decision lives.

**The server still pulls rows out of EF synchronously** — a `foreach` over `IQueryable` inside
`SerializeRows`. Making that `IAsyncEnumerable` end to end is independent of (B), cheaper than it,
and is the last buffered hop nobody has priced.

**The falsification of `StreamingOverHttpTest` is not in CI.** That the test *can* fail was
established by hand, by putting `ResponseContentRead` back, and the evidence lives in a commit
message and in D7. A second test asserting the buffered path deadlocks is writable but costs its
10-second deadline on every run. A judgement call, recorded rather than left implicit.

**Two ways to pin a server context now exist and they are adjacent.**
`InProcessInfoCarrierServer._transactions` is still unbounded (M8's own note), and an **abandoned
enumeration** holds a `DbContext` — and, inside a transaction, a store connection — until its
enumerator is disposed. Nothing in this provider abandons one, because the materializer drains; a
caller using `IInfoCarrierClient` directly can. Both are the library's to answer and they want
answering together.

---
