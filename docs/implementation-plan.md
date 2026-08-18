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


## Phase L — the sample reads like an ordinary data app

The Blazor sample looked good and behaved like nothing a reviewer has used: sorting and filtering
sat in a toolbar beside a **Run** button, an entity was chosen by typing its primary key, and a
dropdown offered raw customer ids. The demo exists to make this provider look like ordinary EF
Core, so a strange UI makes the *provider* look strange.

**Which gate.** `samples/` is neither `src/` nor `test/`, so `eng/measure.sh` says nothing about it
and is not run. `eng/trim-ratchet.sh` **publishes `samples/Northwind.Client`**, so every step here
runs it and `OURS` must not rise above 88.

**How these steps are verified.** By driving a real browser over the DevTools protocol —
`--headless=new --remote-debugging-port` plus `Runtime.evaluate`, and `--remote-allow-origins=*`,
without which Edge refuses the CDP WebSocket. `--dump-dom` renders a page but cannot press a
button, and every claim below is about what happens when you press one.

- [x] **M8-23. Customers: the controls move into the grid, and one Fluent parameter turned out to
      render nothing.** `<this commit>`
      `eng/trim-ratchet.sh`: **`OURS: 88 <= 88`**, total 853. No spec-suite run: no `src/` or
      `test/` code changed.

      The toolbar is gone — country field, sort dropdown and **Run** button. Every column is
      `Sortable`, and Company and Country each carry a `ColumnOptions` popup holding a
      `FluentSearch`. Fluent renders a `col-sort-button` on each header and a `col-options-button`
      labelled *"Filter this column"*, which is the shape a reviewer already knows.

      **The sort is composed on `IQueryable<Customer>`, before `Skip`/`Take`, and deliberately NOT
      through `request.ApplySorting`.** That helper sorts an `IQueryable<CustomerRow>`, and
      `CustomerRow` is a client-only record: `ProjectionRewriter` cuts the query at the `Select`
      (ADR-010), so an `OrderBy` composed after it lands on the **client** side of the boundary.
      The page would then sort the twelve rows it had already been given and look entirely
      correct — server-side paging with client-side sorting, wrong on every page but the first.

      **Verified in the browser, not reasoned about.** Driving the real headers produced, in the
      decoded tree in the wire panel and in the server's own SQL:

      | Action | Tree | SQL reaching SQLite |
      |---|---|---|
      | Company header, 1st click | `Select`,`Take`,`Skip`,`ThenBy`,`OrderBy` | `ORDER BY "c"."CompanyName", "c"."Id"` |
      | Company header, 2nd click | …`OrderByDescending` | `ORDER BY "c"."CompanyName" DESC, "c"."Id"` |
      | Country filter `Germany` | …`Where`,`Contains`, literal on the wire | 65 items → **8** |
      | Next page | `Skip`/`Take`, sort preserved | Page 2 of 6 |

      A filter change costs **one** refresh, not two — wire entries went `#6` → `#8`, a `Count` and
      a rows query. Moving off page one already re-runs the provider, so calling `RefreshDataAsync`
      as well would buy a second round trip for the same answer, which the panel would show and a
      reader would rightly ask about.

      **`ColumnBase.Filtered` renders NOTHING in Fluent UI 4.14.4, and that was measured rather
      than assumed.** The design said the header would show the filter was on. It does not: the
      `col-options-button` markup is **byte-identical** with a filter set and without one — same
      class, same `aria-label`, same `svg`. So an active filter would have been invisible, and the
      item count its only evidence, which is how a reviewer ends up believing the store is nearly
      empty. The page now renders **a chip per active filter** in the grid footer, each clearing
      its own filter: *Company contains "Ma" ✕* beside *5 items*, and clicking it returns 65.
      `Filtered` is left set, because it is the correct API and costs nothing if a later version
      honours it.

      Also `color-scheme: light dark` on `html, body`, one declaration: the page already followed
      the OS theme through `DesignThemeModes.System`, but a **scrollbar is painted by the browser**
      rather than by CSS, so a dark page kept light scrollbars.

      **Two operational facts about driving this sample, both of which cost a run.** A Blazor WASM
      rebuild changes every fingerprinted `.wasm` name, so (1) the **server must be restarted**
      after a client change, and (2) the browser must reload with **`Network.setCacheDisabled`** —
      a cached boot manifest names files that no longer exist, every request answers `304`, and the
      app hangs on *"Starting the client…"* for ever with no error. Worse, building
      `Northwind.Client` **alone** leaves the server's static-asset manifest stale, which shows as a
      `404` plus an SRI `integrity` failure on `Northwind.Shared.*.wasm`. **Build through
      `samples/Northwind.Server`**, which builds the client as a project reference and keeps the two
      manifests in step. Three boot failures were diagnosed by streaming `Runtime.exceptionThrown`
      and `Log.entryAdded` over CDP; none of them was visible in the page's own error UI.

- [x] **M8-24. Order: master-detail, and the id field is gone.** `<this commit>`
      `eng/trim-ratchet.sh`: **`OURS: 88 <= 88`**, total 853. No spec-suite run: no `src/` or
      `test/` code changed.

      An order was chosen by typing its primary key into a `FluentNumberField` bounded
      `Min="1" Max="NorthwindSeed.OrderCount"`. Nobody picks a record that way. It is now a paged,
      sortable grid of orders on the left and the detail on the right, driven by selection.

      **Two `DbContext`s on one page, for two different reasons, and the page says so.** The grid
      takes a fresh one per provider call, because a grid provider may ask again before the last
      answer lands and a `DbContext` is not thread-safe — the fault that produced EF's *"A second
      operation was started on this context instance"* on Customers. The detail keeps a page-scoped
      one per selected order, because **the unit of work is the demonstration**: several edits
      accumulate in one change tracker and leave as one `SaveChanges`.

      **The master list is a projection over a join**, `o.Customer!.CompanyName`, so the grid shows
      *Alfreds Futterkiste* rather than `ALFKI` and the server does the joining — 240 orders do not
      arrive so that the browser can label twelve of them.

      **Driven in a real browser, and every claim below is a reading rather than an expectation:**

      | Action | Observed |
      |---|---|
      | Page load | `240 items · Page 1 of 20`, order 1 auto-selected and row-highlighted |
      | Click row 3 | selection moves, detail shows Order 3 / `ANATR`, 1 round trip |
      | *load it* (customer) | `Ana Trujillo Emparedados — México D.F.`, 1 round trip |
      | *load them* (lines) | 2 lines for order 1, edit fields appear |
      | Sort by **Customer** | tree `OrderBy`+`ThenBy`; SQLite got `ORDER BY "c"."CompanyName", "o"."Id"` |
      | Two quantity edits | button reads **Save 2 changes**, off the change tracker |
      | Save | **one** wire entry, `💾 SaveChanges`, *"Last save: 2 entries, in 1 SaveChanges envelope"*, two `UPDATE "OrderDetails"` at the store |

      The sort is composed on `IQueryable<Order>` before `Skip`/`Take` for the reason M8-23 gives
      in full: `request.ApplySorting` would sort the client-only `OrderRow` and leave the `OrderBy`
      on the client side of the projection split.

- [x] **M8-25. Transfer: a customer, not a primary key.** `<this commit>`
      `eng/trim-ratchet.sh`: **`OURS: 88 <= 88`**, total 853. No spec-suite run: no `src/` or
      `test/` code changed.

      The dropdown offered `ALFKI`. It now offers *Alfreds Futterkiste*, with `OptionValue` keeping
      the id as the bound value because that is what the foreign key needs. It also offers **all
      65** ordered by company name, rather than the first twelve **by key** — two string columns
      for 65 rows is nothing on the wire, and a list silently holding twelve of sixty-five was its
      own small dishonesty about what the store contains.

      **Driven in a real browser, both paths:**

      | | Observed |
      |---|---|
      | Dropdown | 65 options, `Alfreds Futterkiste [ALFKI]` … `Wolski Zajazd`, ordered by name |
      | Commit | picked *Berglunds snabbköp* **by name** → order 1 belongs to `BERGS`, stock 38 → 37 |
      | Forced failure | store **unchanged** (`BERGS`, 37) — the rollback held |
      | Wire shape | `🔓 BeginTransaction` → `Query` → `Query` → `💾 SaveChanges` → `💾 SaveChanges ⚠️ fault` → `↩️ RollbackTransaction` |
      | W5 | the bar carries the server's chain, ending `FOREIGN KEY constraint failed` |

      **A probe of mine was wrong before the page was**, which is the M8-23 lesson repeating:
      `document.querySelector('fluent-message-bar')` returned `null` and briefly looked like a
      page that had stopped reporting failures. `FluentMessageBar` renders as
      **`div.fluent-messagebar`**, not as a custom element. The page was correct throughout; the
      instrument was not. *Read what the instrument prints — and check the instrument before the
      subject.*

- [x] **M8-26. `AsNoTracking` where it belongs, and only there.** `<this commit>`
      `eng/trim-ratchet.sh`: **`OURS: 88 <= 88`**, total 853. No spec-suite run: samples only.

      Every query in all four sample projects was classified. **Two qualified**; the rest either
      cannot take it or would gain nothing.

      | Site | Verdict |
      |---|---|
      | `Northwind.Demo` — German customers | **`AsNoTracking`** — entities printed and dropped |
      | `Transfer.ReadBackAsync` — order + product | **`AsNoTracking`** — read-only, and it is part of the card's claim |
      | `Northwind.Demo` — lazy-loading step | tracked; a loader only rides on a tracked entity |
      | `Northwind.Demo` — unit-of-work lines | tracked; edited and saved |
      | `OrderPage` detail | tracked; edited, saved, and `Entry(…).LoadAsync()` needs an entry |
      | `Transfer.TransferAsync` — order + product | tracked; modified inside the transaction |
      | `Customers`/`OrderPage` grids, `Transfer` dropdown | projections to client-only records — **no entity is tracked in the first place**, so it would be noise |
      | `CountAsync` calls | scalar |

      **It is not a client-side nicety in this provider.** `QueryExecutor.TrackingBehaviorFinder`
      lifts the marker out of the tree and it travels in `QueryDataRequest.TrackingBehavior`, so
      the *server* marks the rows untracked too and `ClientResultMaterializer` skips identity
      resolution. `QuerySplitter` already carries `AsNoTracking` in its `QueryMarkers` set.

      **The dangerous one is the lazy-loading step, and it is dangerous because it does not
      throw.** `AsNoTracking` there would leave `order.Customer` `null` and print an empty name —
      a silent wrong answer, which is the shape this repository keeps paying for.

      Verified by running both clients: the console demo prints the same 8 German customers and
      still resolves `order.Customer` in one extra round trip, 14 round trips total and unchanged;
      the Transfer page read `ALFKI`/34 → committed `BERGS`/33 → forced a failure and stayed
      `BERGS`/33.

## Phase M — taking control of compiler warnings

**Why this is worth a phase.** There were **18 distinct warning texts** in
`dotnet build InfoCarrier.Core.slnx`, and **three of them nobody had ever read** were high-severity
security advisories. That is the whole argument: `EF1001` is expected and allowed, and 244 of it
buried the two that were not.

Occurrence counts at the start (each warning is printed twice, once at compile and once in the
summary, so these are double the distinct sites):

| Code | Count | What |
|---|---|---|
| `EF1001` | 244 | EF internals — **expected and allowed**, `CLAUDE.md` |
| `NU1903` | 44 | **known vulnerabilities** |
| `CS8618` | 24 | non-nullable field uninitialised |
| `CS1573`/`CS1591`/`CS1574`/`CS1570` | 46 | XML doc |
| `CS8604`/`CS8602`/`CS8765`/`CS8625`/`CS8619` | 22 | nullability |

**44 distinct CS sites, every one in a file this repository owns** — 22 in `src/InfoCarrier.Core`,
1 in `src/InfoCarrier.Core.AspNetCore`, 21 in `test/`. **None is in an EF spec base class**, which
is what makes a repo-wide `TreatWarningsAsErrors` reachable rather than aspirational.

**Neither ratchet is affected.** `eng/ratchet.sh` gates the spec-test failure count and
`eng/trim-ratchet.sh` gates ILLink `IL2xxx`, which the C# compiler never emits. There was no
warning ratchet to replace.

- [x] **M8-27. The two advisories are closed by a version floor, not by a suppression.**
      `<this commit>`
      `eng/measure.sh m8-27 j25`: `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` —
      **0 fixed, 0 broken, `REASONS: unchanged`**. `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      `InfoCarrier.Core.TransportTests`: **17 of 17** (run because the SQLite native package moved).
      `NU1903` occurrences **44 → 0**.

      **The brief said three advisories; the build emits nine**, all high — one SQLite and **eight**
      against `System.Security.Cryptography.Xml`.

      **Neither reaches a shipped product, and that was measured rather than assumed.**
      `dotnet list package --vulnerable --include-transitive` reports `InfoCarrier.Core` and
      `InfoCarrier.Core.AspNetCore` **clean**. `SQLitePCLRaw.lib.e_sqlite3` reaches
      `Northwind.Server` and both test projects through `Microsoft.Data.Sqlite.Core`;
      `System.Security.Cryptography.Xml` reaches **only** `InfoCarrier.Core.FunctionalTests`,
      through `EFCore.SqlServer` → `Microsoft.Data.SqlClient`. That lowers the severity for a
      consumer; it does not make the finding go away, and a fixed version exists for both.

      Both are transitive-only, and `CentralPackageTransitivePinningEnabled` is already on, so a
      `PackageVersion` floor is the whole fix. **The four `SQLitePCLRaw` packages are pinned as a
      family** — `core`, `bundle_e_sqlite3`, `lib.e_sqlite3`, `provider.e_sqlite3` ship in lockstep,
      and pinning the one the advisory names would leave the other three a version behind it.

      **A double hyphen inside an XML comment cost one restore.** Writing *"a version floor rather
      than a suppression -- nothing here is being accepted as a risk"* is invalid XML; it did not
      report a malformed comment but `NU1010` against **every** `PackageReference` in the
      repository, because the whole `Directory.Packages.props` stopped parsing. An error naming
      thirty packages meant one punctuation mark.

- [x] **M8-28. The XML-doc warnings, fixed rather than silenced.** `<this commit>`
      `eng/measure.sh m8-28 m8-27`: `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` —
      **0 fixed, 0 broken, `REASONS: unchanged`**. `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      `CS1570`/`CS1573`/`CS1574`/`CS1591` **46 → 0**.

      All 22 sites are in `src/`, and each was a real defect in a repository whose comments carry
      this much of its reasoning:

      | Code | What was actually wrong |
      |---|---|
      | `CS1570` ×2 | `ObservableCollection<T>` and `Collection<T>` written raw inside backticks in a `<remarks>` — **the `<T>` opened an XML tag**, so the whole remark was malformed and the doc file dropped it |
      | `CS1574` ×4 | crefs that cannot resolve: `IModel` and `DynamicDependencyAttribute` had no `using`, `OpenFragments` is a member of `BoundaryAnalysis`, and **`RelationalTypeBaseExtensions` is not referenceable at all** — M9 removed the product's `EFCore.Relational` reference and the comment outlived it |
      | `CS1573` ×11 | missing `<param>` on primary constructors and one positional record; `Replay` documented two of its seven parameters |
      | `CS1591` ×5 | **undocumented public members of a shipping package** — `MapInfoCarrier`, `HttpInfoCarrierTransport.SendAsync`, both `InfoCarrierTransportException` constructors, `DynamicValueMapper.FromDynamicValue` |

      The `CS1574` on `RelationalTypeBaseExtensions` is the one worth keeping: a stale cref is how
      a comment tells you it is describing a dependency that no longer exists.

- [x] **M8-29. The nullability warnings, answered per site.** `<this commit>`
      `eng/measure.sh m8-29 m8-28`: `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` —
      **0 fixed, 0 broken, `REASONS: unchanged`**. `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      **`EF1001` is now the only warning the build emits.**

      | Sites | Answer |
      |---|---|
      | 12 × `CS8618` `_testStoreFactory` | declared `ITestStoreFactory?` — the field is lazily built with `??=`, so nullable is the **true** annotation. Most of the suite's other 50-odd copies were already `?`; these twelve had drifted. |
      | 2 × `CS8602` in `ProjectionRewriter` (**product**) | **one fix, not two**: `IsResultSelectorOperator`'s `out` parameter is non-null whenever it returns `true`, and that contract was never expressed. `[NotNullWhen(true)]` states it, closes both warnings, and let **three** `!` suppressions be deleted. |
      | 2 × `CS8604` in `InfoCarrierBackendTestStore` | `ServiceProvider` is EF's own nullable `TestStore` member, assigned in this store's constructor |
      | 2 × `CS8602`/`CS8604` in `QuerySplitterTest` | expression trees the splitter *reads* — `FirstOrDefault()!.Title` is a node, never a dereference |
      | 1 × `CS8619` in `GearsOfWarQuery` | the expected-results lambda made the anonymous member `string?` while the actual made it `string` |

      **The product fix is the one worth keeping.** Two warnings that looked like two sites were one
      missing annotation on a contract, and the `!` operators around them were hiding it rather than
      documenting it. `= null!` was not used anywhere.

- [x] **M8-30. `EF1001`: EF's own answer, checked before it was copied. The build is CLEAN.**
      `<this commit>`
      `eng/measure.sh m8-30 m8-29`: `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` —
      **0 fixed, 0 broken, `REASONS: unchanged`**. `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      **`dotnet build InfoCarrier.Core.slnx` now reports `0 Warning(s), 0 Error(s)`.**

      **What other providers do, read out of `subrepos/efcore` rather than recalled.** EF Core's own
      providers use **`#pragma warning disable EF1001`** at the point of use, under the comment
      *"Internal EF Core API usage."*: **51 files across eight projects** — `EFCore.Relational` 21,
      `EFCore.SqlServer` 10, `EFCore.Cosmos` 7, `EFCore.Sqlite.Core` 6, `EFCore.InMemory` 3,
      `EFCore` 2, plus `Sqlite.NTS` and `Proxies`. Both granularities appear: a single file-scoped
      `disable` where a file's whole job is internal work, narrow `disable`/`restore` pairs for
      isolated sites. **There is no `NoWarn` for `EF1001` anywhere in EF Core's repository.**

      This repository had **122 sites in 19 files**, and the dense ones are dense —
      `ClientResultMaterializer` 29, `ServerQueryExecutor` 16, `InfoCarrierDatabase` 8 — so the
      file-scoped form is the one that matches. Each of the 19 gets one pragma under a two-line
      comment naming the reason.

      **Per file rather than `NoWarn`, and the difference is the whole point.** A `NoWarn` in
      `Directory.Build.props` would be one line and would also silence the *next* file that reaches
      for an internal API. The pragma leaves that tripwire armed, which is what keeps `CLAUDE.md`'s
      "prefer public API where one exists" enforceable rather than aspirational.

      **`CLAUDE.md`'s guardrail is rewritten in this commit**, because the old wording —
      *"Do not suppress them repo-wide"* — read against a build that now shows none of them would
      have looked like a rule that had been quietly broken.

- [x] **M8-31. Unused usings become a warning, and two blunt instruments were replaced by scoped
      ones.** `<this commit>`
      `eng/measure.sh m8-31 m8-30`: `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` —
      **0 fixed, 0 broken, `REASONS: unchanged`**. `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      `InfoCarrier.Core.TransportTests`: **17 of 17**. The Blazor sample boots (driven).
      **36 unused `using` directives removed; the build stays `0 Warning(s), 0 Error(s)`.**

      `IDE0005` is on at `warning` in a root `.editorconfig`, with `EnforceCodeStyleInBuild` in
      `Directory.Build.props` so it runs in the build rather than only in an editor.

      **It was silently inert in six of eight projects, and the build said so.** `IDE0005` needs
      `GenerateDocumentationFile`, and the samples and both test projects set it **false** — so
      the rule covered `src/` only while looking repository-wide. The tell was a warning nobody
      reads, `EnableGenerateDocumentationFile`, naming the property. It was found by counting
      warnings after the fix rather than by trusting that the fix applied. Removing 15 usings in
      `src/` and stopping there would have been a confident false clearance.

      **Removing a using exposes another**, so this had to iterate: 15 in `src/` over three passes,
      then 21 more once the other six projects were covered, converging in three rounds.

      **Two things this repository asked for, and both replaced something blunt with something
      scoped:**

      | Was | Now | Why |
      |---|---|---|
      | `dotnet_analyzer_diagnostic.category-Style.severity = none` at root | **deleted** | It was guarding a problem that does not exist. Almost every `IDE####` rule defaults to *suggestion*, which is not a build warning — **measured: the build is `0 Warning(s)` without it**. All it would ever have done is silently pre-empt every style rule anyone later wanted to adopt. |
      | `<NoWarn>CS1591;CS1573;CS1574;CS1570</NoWarn>` in six `.csproj` | **`test/.editorconfig` and `samples/.editorconfig`** | A `NoWarn` is invisible from the file that trips it, is repeated per project, and widens by accident. A folder-scoped `.editorconfig` states the boundary once, in the directory the boundary is about, and a new project under `test/` inherits it with no build-file edit. |

      **The scoped files are load-bearing, and that was probed rather than assumed**: with
      `test/.editorconfig` removed, `InfoCarrier.Core.FunctionalTests` alone emits **1238 `CS1591`,
      12 `CS1574` and 4 `CS1570`** — 1254 warnings, which is exactly why those projects had
      documentation generation switched off in the first place. In `src/` the same four rules stay
      **on**, and M8-28 answered all 22 sites there.

- [x] **M8-32. The gate: warnings are errors in CI, and only in CI.** `<this commit>`
      `eng/measure.sh m8-32 m8-31`: `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` —
      **0 fixed, 0 broken, `REASONS: unchanged`**. `eng/trim-ratchet.sh` **under `CI=true`**:
      `OURS: 88 <= 88`. `InfoCarrier.Core.TransportTests`: **17 of 17**.

      ```xml
      <TreatWarningsAsErrors Condition="'$(CI)' == 'true'">true</TreatWarningsAsErrors>
      ```

      Local development keeps warnings as warnings, because half-finished work with an unused
      variable in it is a normal state and a gate that punishes experimentation gets switched off
      by the people it is for. `CI` is set by GitHub Actions and MSBuild reads environment
      variables as properties, so nothing is passed on a command line; `build.yml` states
      `CI: true` explicitly anyway, so the gate is visible in the workflow rather than inherited
      from a runner. Policy is written up in **`docs/build-warnings.md`**, registered in
      `CLAUDE.md`'s authority table.

      **There is no `WarningsNotAsErrors`, and that is a consequence of M8-30 rather than an
      omission.** `EF1001` was the one code the story expected to exempt; since it is pragma'd per
      file it emits nothing, so there is nothing to exempt. A *new* file reaching for an internal
      API warns locally and **fails CI**, which is the tripwire working.

      **The gate was driven in both directions, because one that has only been seen to pass is not
      known to work.** With a deliberately unused `using` in `TypeNode.cs`: plain `dotnet build`
      exits **0** with `warning IDE0005`; `CI=true dotnet build` exits **1** with
      `error IDE0005`.

      **The trap the story flagged was real, and the fix the story proposed does not work.**
      `<ILLinkTreatWarningsAsErrors>false</ILLinkTreatWarningsAsErrors>` was set first and the
      trimmed publish **still failed** under `CI=true`, with five `IL2110`/`IL2111` errors. That
      property feeds the ILLink **task**; most `IL2xxx` findings come from the trim **Roslyn
      analyzer** during compilation — they carry a source line — so plain `TreatWarningsAsErrors`
      turns those into errors and the property never sees them. **The gate that exists to measure
      trim warnings was the one thing that could not tolerate them.**

      `NoWarn` would be worse than useless: `eng/trim-ratchet.sh` **counts** them, so silencing
      them reports `OURS: 0` and passes for ever — the failure its own clean-publish rule exists to
      prevent. The two axes are separated instead: the script passes
      `-p:TreatWarningsAsErrors=false` on its own publish and says why. The property stays for the
      ILLink task, with a comment correcting what it does.

      **Two instruments were wrong before the code was, again.** `ratchet exit=0` on a failed
      publish was `tail`'s exit status, not the script's — the script fails correctly. And a build
      failure read as XML damage was `NETSDK1124` from a stale `obj/Release` the trim publish left
      behind. Both were resolved by reading the actual output rather than the expected one.

### Phase M closed

`dotnet build InfoCarrier.Core.slnx` → **`0 Warning(s), 0 Error(s)`**, from 18 distinct warning
texts. Nine security advisories closed by a version floor, 22 XML-doc defects fixed, 23 nullability
sites answered, 36 unused usings removed, `EF1001` handled the way EF Core's own providers handle
it, and a new warning now fails CI.

## Phase N — the documentation a consumer reads

Two audiences, and the whole phase exists because they had been served by one set of files. The
three READMEs are the *repository's* front doors; the site under [`../website/`](../website/) is
for a C# developer who has never seen this repository and does not want to. **Nothing internal
goes on the site** — no ADR numbers, no phase labels, no test tiers, no wire internals beyond what
a consumer must set.

**Gates.** Everything in this phase is text plus a workflow. `eng/measure.sh` and
`eng/trim-ratchet.sh` say nothing about a Markdown file, and CLAUDE.md's table is explicit that
`docs/`/`eng/` text changes need neither. Each step below states what it ran and what it did not,
rather than staying silent about it.

- [x] **N1. The repository README, checked claim by claim rather than edited in place.**
      `<this commit>`

      Four claims were checked against the repository and three of them had moved.

      **"Not yet done: NuGet packaging"** was the largest. Phase K did it: `Directory.Build.props`
      carries the package identity, `IsPackable` opts the two `src/` projects in individually, and
      `release.yml` packs on a `v*` tag and attaches the `.nupkg` and `.snupkg` to a GitHub
      Release. What is *not* done is publishing — deliberately, and `release.yml`'s own header says
      why. The README now separates the two, because "no packaging" and "packaged but not pushed"
      send a reader to different places.

      **"The HTTP binding lives in the samples"** was M8-22's before-picture. It is in
      `InfoCarrier.Core`, which the table three paragraphs below already said — the two halves of
      the same section disagreed.

      **The three sample pages** were redesigned in M8-23…M8-26 and the README still described
      them as an undifferentiated "three pages". Named now, one sentence each, because what each
      page demonstrates is the reason to run it.

      **`docs/build-warnings.md` was linked from nowhere.** It is in the documentation table, and
      `CI=true dotnet build` is in *Build and test* — a contributor who does not know that warnings
      are fatal on the server finds out from a red run otherwise.

      Verified unchanged: the suite figures (`22658 / 22472 / 9 / 177`) against
      `artifacts/measure/m8-32`, the badge's `22,472`, both package descriptions, and every
      relative link in the documentation table resolving to a file that exists.

      **Gates: none, and that is the rule rather than an omission.** One Markdown file changed; no
      `src/`, no `test/`. CLAUDE.md's table says `docs/` text needs neither `eng/measure.sh` nor
      `eng/trim-ratchet.sh`.

- [x] **N2. `samples/README.md`: three stale numbers and one section that contradicted itself.**
      `<this commit>`

      **The trim numbers were all three wrong**, and they were wrong in different directions:
      `ours` 86 → **88** (J8's `WireGrouping` fix, a deliberate rise), `total` 1129 → **853** and
      EF Core's share 864 → **585** (the package/SDK movement, none of it ours). Read out of
      `eng/trim-baseline.txt`, which records each measurement rather than only the current value —
      which is why the direction of each could be stated rather than guessed.

      **The file contradicted itself about the transports.** *The projects* says they were promoted
      in M8-22 and that no sample-owned transport is left; *What is not here yet* still listed
      "the two transport files still live here". The stale bullet is deleted rather than rewritten:
      the promotion is already described where it belongs, and a "this is done now" entry under a
      heading about what is missing is worse than no entry.

      **The projects table was missing `Northwind.Client` altogether**, and described the server as
      having no UI — *"a `GET /` is a 404"*. It serves the Blazor client (`UseBlazorFrameworkFiles`
      plus `MapFallbackToFile`), which is the whole reason there is one origin and no CORS, and the
      same table's own first sentence about the browser says so.

      **The console transcript is not stale — its caveat was in the wrong place.** The numbers
      (`1:ALFKI`, 330 lines of quantity ≥ 10) are a *fresh* store's, and
      `InfoCarrier.Core.TransportTests` asserts both against the same seed. The note saying so was
      below the transcript; a reader comparing output line by line meets it after concluding the
      sample is broken. It now leads the transcript, and the note itself says what a clicked-through
      store prints instead and why that is the demonstration working.

      Verified unchanged by counting the seed rather than trusting it: **65 customers, 240 orders,
      476 order lines, 30 products, 8 categories** — the orders and lines arithmetic re-run over
      `NorthwindSeed`'s own generator, the others counted out of its literal arrays.

      **Gates: none.** Markdown only.

- [x] **N3. `docs/nuget-readme.md`: honest about where the packages actually are.** `<this commit>`

      **Every absolute link in this file was broken, and the reason is worth stating once.** It
      links absolutely on purpose — a package README is rendered outside the repository, where a
      relative path means nothing — and all three pointed at `blob/master/…` or at the repository
      root. **`master` is v1.** `git ls-tree -r --name-only origin/master` has no `docs/` at all:
      it is `InfoCarrier.Core.sln`, `BuildScript/`, `sample/ServiceStackSample.Client/`. So the
      two links a reader is *told* to follow before adopting — limitations and the security review
      — were 404s, and the third landed on the previous product. They point at `v10-claude`, which
      is where the files are; `git ls-tree` confirms both, on the remote rather than locally.

      **The absolute-link rule was recorded and the consequence of the branch was not.** The
      `PackageReadmeFile` comment in `Directory.Build.props` explains why this file exists at all
      (nuget.org resolves images only against absolute URLs). Nothing said that "absolute" also
      means "names a branch", and the default branch being the previous major made that silent.

      **A *Getting the packages* section, because the honest answer is not `dotnet add package`.**
      Nothing is on nuget.org; a package README that omits this leaves a reader to discover it from
      a failed restore. Two routes that work today: the `.nupkg` and `.snupkg` attached to each
      GitHub Release with a local feed, or `dotnet pack InfoCarrier.Core.slnx` from a clone —
      **`-b v10-claude`**, for the same reason the links needed fixing.

      Verified unchanged: `10.0.0-preview.1` against `Directory.Build.props`, the suite figures,
      both package descriptions, and that `dotnet pack` on the solution yields exactly the two
      products (`IsPackable` is false in `Directory.Build.props` and opted into per project).

      **Gates: none.** Markdown only.

- [x] **N4. The site: Material for MkDocs in `website/`, seventeen pages, every snippet executed.**
      `<this commit>`

      **Where, and why not `docs/`.** `docs/` is this repository's internal source of truth and a
      static-site generator pointed at it would publish the ADR log, the roadmap and the rolling
      plan as if they were promises to a consumer. The site is `website/mkdocs.yml` plus
      `website/docs/`, which is MkDocs' own default layout relative to its config file, so nothing
      had to be reconfigured to keep the two apart. `website/site/` is git-ignored.

      **Seventeen pages**: home; getting started (installation, first client and server, samples);
      using it (querying, saving changes, transactions, loading related data, errors);
      configuration (client, server, value mappers, custom transports); reference (Blazor
      WebAssembly, security, limitations, public API).

      **Every code sample was compiled and run**, not written from memory. A console harness in the
      scratchpad references `src/InfoCarrier.Core`, stands a SQLite-backed
      `InProcessInfoCarrierServer` behind a serializing loopback transport, and executes the
      snippets as assertions: query with `Include`/`Where`/`OrderBy`/`Take`, projection and
      aggregates, `AsNoTracking`, a two-entity unit of work, a store-generated key coming back,
      explicit reference and collection loads, savepoint plus rollback, two contexts sharing one
      transaction through `UseInfoCarrierTransaction`, `ExecuteUpdate`/`ExecuteDelete`, a
      server-side failure, a transport failure, payload limits, and a custom value mapper.
      **Seventeen of seventeen pass.**

      **Both of those counts said "sixteen" when this entry was first written, and neither had been
      counted.** They were estimated from the nav and from the snippet list, and `find | wc -l` and
      `grep -c '^PASS '` disagreed with both. Corrected here, and worth recording because it is the
      cheapest possible instance of the rule this file states everywhere else: **a number that was
      not read is not a measurement**, however small the thing being counted.

      **The value-mapper snippet is the reason the harness exists rather than a review.** The first
      version passed and proved nothing: the mapper was registered, the query ran, the assertion
      was green — and a call counter showed `ToWire=0 FromWire=0`. `o.Freight > money.Amount` folds
      to a decimal constant on the client, so the `Money` never reached the wire. The mapper only
      fires for a value the wire must actually carry; with `Money` as a **mapped property** and the
      constant compared against it, the counter reads `ToWire=1 FromWire=1`. **A probe that passes
      is evidence only once it has been shown able to fail**, and this one failed first.

      **Two facts came out of the harness that no reading would have produced.** The exception
      chain from a foreign-key violation is
      `DbUpdateException → DbUpdateException → InfoCarrierServerException`, the last naming
      `Microsoft.Data.Sqlite.SqliteException` in `ServerExceptionTypeName`, with the server's stack
      trace present under `Exception.Data["InfoCarrier.ServerStackTrace"]` on the inner two and
      absent on the outer. That is the errors page, verbatim. And **`FromSqlRaw` executed and
      returned rows** when the client project adds a `Microsoft.EntityFrameworkCore.Relational`
      reference — undocumented, outside the stated surface, and a package the product deliberately
      dropped in M9, so the site does **not** advertise it; it says relational APIs are not part of
      what this provider offers, which is true and is the sentence a consumer needs. Flagged for a
      decision rather than silently documented.

      **`--strict`, and the gate was proved able to fail.** `mkdocs build --strict` turns a broken
      internal link or a missing nav target into a failed build. A deliberately broken link was
      added to `index.md`, the build aborted naming it, and the link was removed —
      `Aborted with 1 warnings in strict mode!` is the failure mode this gate is for.

      **The built site fetches nothing.** `theme.font: false`, because Material's default pulls
      Roboto from `fonts.googleapis.com` on every page view and sends every visitor's IP to a third
      party for a typeface. Mermaid ships inside the theme bundle, so diagrams cost no request
      either. What is left in the output is SVG namespaces, an icon-licence URL and ordinary
      hyperlinks.

      **Versions are pinned** in `website/requirements.txt` (`mkdocs-material==9.7.7`), because a
      documentation build that resolves a different theme on a different day is not reproducible
      and a fresh runner is the only place anyone would notice.

      **Gates: `mkdocs build --strict`, green. Not `eng/measure.sh` and not
      `eng/trim-ratchet.sh`** — no file under `src/` or `test/` changed. The verification harness is
      in the scratchpad and is deliberately not committed: it would be a fifth project in the
      solution, and `eng/measure.sh` parses the last `Total tests:` block.

- [x] **N5. `docs.yml` — build on every push, deploy to Pages, disturb nothing.** `<this commit>`

      **A third workflow rather than a job in `build.yml`, and the reason is what each gate is
      for.** `build.yml` sets `CI: true`, which makes compiler warnings fatal, and its
      *spec-ratchet* job is allowed ninety minutes; neither has anything to say about a Markdown
      file, and a documentation typo must not queue behind a 22,000-test suite. `docs.yml` touches
      no project, no ratchet and no baseline — `git diff` against `build.yml` and `release.yml` is
      empty, checked rather than asserted, and all three parse as YAML.

      **`--strict` runs on pull requests too, where nothing is deployed.** The deploy job is gated
      on `github.event_name != 'pull_request'`, so a fork's branch cannot publish to this site
      while its content is still verified. `concurrency: pages` with `cancel-in-progress: false`,
      because cancelling a half-finished deployment is worse than waiting for one.

      **One manual step remains and this workflow cannot do it**: *Settings → Pages → Source →
      GitHub Actions*. Until then `build` passes and `deploy` fails on `configure-pages`, which is
      the right way round — the content is still checked. The workflow's own header says so.

      **The three READMEs now point at the site**, each in the register of its own audience: the
      repository README separates "here to use it" from the internal documents it develops against,
      `samples/README.md` opens with one line for a reader who took a wrong turn, and
      `docs/nuget-readme.md` sends *limitations* to the site rather than to a file inside a
      repository. **Those URLs 404 until Pages is switched on** — noted here so the next reader
      knows it is a pending switch rather than a wrong link.

      **Gates: none of the repository's.** A workflow file and three Markdown files; nothing under
      `src/` or `test/`. `mkdocs build --strict` was re-run and is green.

- [x] **N7. The documents describe a published package.** `<this commit>`

      Requested directly: write every document as though both packages are on nuget.org. Six files
      carried the old claim, and they are not six copies of one sentence — each states it in the
      register of its own audience, so each needed its own answer.

      | File | Was | Is |
      |---|---|---|
      | `README.md` | *"Packaged, not published … neither is on nuget.org"* | two version badges and the two `dotnet add package` lines |
      | `docs/nuget-readme.md` | *Getting the packages* — a local feed over a downloaded `.nupkg` | *Installing* — the ordinary two lines |
      | `website/docs/getting-started/installation.md` | a **warning** admonition, then two workarounds | CLI / `PackageReference` / Package Manager tabs, with *from source* kept as an alternative |
      | `website/docs/index.md` | *"neither package is on nuget.org yet"* | the install line, preview caveat kept |
      | `docs/roadmap.md` | *"Nothing has been published yet"* | published at `-preview.1`, and **why the suffix still stays** |
      | `docs/ci-cd.md` | *"`dotnet nuget push` to NuGet.org (needs `NUGET_API_KEY` secret)"* | what `release.yml` actually does |

      **`docs/ci-cd.md` was already wrong before this change, and only reading it to edit it found
      that.** It described `release.yml` as pushing to nuget.org with a `NUGET_API_KEY` secret.
      `release.yml` does no such thing and says so in its own header — the push is manual and no
      such secret exists. The line predates M8-20 and had survived every edit since, because
      nothing that grepped for publication status ever *read* the file it hit. It now records pack,
      the tag/version check and the Release attachment, and says the push is a human's.

      **The one substantive addition is `--prerelease`, and it is the claim most likely to be
      tested first.** `dotnet add package InfoCarrier.Core` with no version does **not** resolve
      `10.0.0-preview.1`: NuGet looks for a stable release, and there is not one. Every install
      instruction therefore names the version, and each page also gives the `--prerelease`
      alternative. An install line that fails on a reader's first attempt is worse than no install
      line.

      **What was deliberately left alone.** `release.yml` is unchanged and still correct — it packs
      and attaches, a human pushes, no API key lives here — so nothing about having published makes
      it stale. `Directory.Build.props`'s note that `-preview.1` should stay is likewise still true;
      a published preview is still a preview. And N1–N6 above are **not** rewritten: they are the
      record of what was true when each landed, and back-dating them would make this file a
      description of the present rather than a log.

      **Gates: none of the repository's.** Markdown only; nothing under `src/` or `test/`.
      `mkdocs build --strict` re-run and green, and the renamed heading was checked for inbound
      anchor links first — there were none.

- [x] **N8. The version is the git tag, the gate that policed it was broken, and the human moves
      rather than disappears.** `<this commit>`

      **One commit, because it is one change to `release.yml`.** Splitting the version source from
      the publishing route would have put two halves of the same file in two commits, and neither
      half is coherent alone: the step that had to go was the version gate, and what fills its place
      is the approval-gated push.

      ### The version source

      **The gate failed, and it was run rather than read.** `release.yml`'s *"Verify the tag matches
      the package version"* step did:

      ```bash
      pkg=$(ls artifacts/pack/InfoCarrier.Core.*.nupkg | grep -v Abstractions | sed -E '…')
      ```

      Both products were packed locally and the step's own shell was run against the four real
      files:

      ```
      tag=[10.0.0-preview.1]
      packaged=[10.0.0-preview.1
      AspNetCore.10.0.0-preview.1]
      >>> GATE FAILS
      ```

      `InfoCarrier.Core.Abstractions` was merged away in **M8-22** and `InfoCarrier.Core.AspNetCore`
      arrived in the same step. The glob matches both packages; the filter excludes one that no
      longer exists. **Every tagged release would have stopped there** — and the same file's Release
      body carries a comment warning about this exact glob, written while the step above it kept
      the bug. A warning in prose is not a gate.

      **MinVer, so there is nothing left to compare.** The number lived in `Directory.Build.props`
      *and* in the tag; the step existed only because two sources can disagree. `MinVer 7.0.0`
      derives it from the tag, `VersionPrefix`/`VersionSuffix` are gone, and the step is replaced by
      one the old design could not make: that all four expected files exist under the exact names
      the push step will use. That replacement was also run, and made to fail by deleting a file.

      **Measured, not assumed, in this order:**

      | | |
      |---|---|
      | untagged pack | `InfoCarrier.Core.10.0.0-alpha.0.510.nupkg` — the 10.0 line held by `MinVerMinimumMajorMinor` |
      | `git tag -a v10.0.0-preview.1` then pack | `10.0.0-preview.1`, **byte-identical to what the hand-maintained property produced** |
      | inter-package dependency | `<dependency id="InfoCarrier.Core" version="10.0.0-preview.1" />` — lock-step survives the change |
      | built assembly | `AssemblyVersion 10.0.0.0`, `FileVersion 10.0.0.0`, `InformationalVersion 10.0.0-preview.1+901bc81…` |

      `AssemblyVersion` is **pinned to `10.0.0.0` and deliberately not derived**. Left to follow the
      package version it would become `10.0.1.0` on the first patch and force every consumer to
      rebuild, for a change that is compatible by definition. EF Core and ASP.NET Core both pin it.

      **`fetch-depth: 0` is now load-bearing in `build.yml`.** MinVer reads tags; a shallow clone
      has none, and it falls back to a default **without failing** — the build succeeds and quietly
      produces the wrong number. `release.yml` already had it, for SourceLink.

      **`v10.0.0-preview.1` was created locally and NOT pushed.** It exists so a local `dotnet pack`
      reproduces the version the documents claim is published. Pushing it fires `release.yml`.

      **Gates, all three, because `Directory.Build.props` governs what `src/` compiles to:**
      `CI=true dotnet build` → `0 Warning(s), 0 Error(s)`; `eng/trim-ratchet.sh` → `OURS: 88
      TOTAL: 853`, `OK (88 <= 88)`; `eng/measure.sh n8-minver` →
      **`Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177`**, read out of the run's own
      summary block, and the failing *names* diff **byte-identical** against `m8-32`. A version
      number should change nothing about behaviour, and this is the measurement that says so rather
      than the assumption.

      ### The two feeds

      **`packages.yml`** publishes to GitHub Packages on every code push to `main`/`v10-claude`.
      Documentation-only pushes are skipped, because the assembly would be identical. It needs no
      configured secret — the run's own `GITHUB_TOKEN` with `packages: write` is enough — and it
      builds with `CI=true`, so an internal build still has to compile clean. **GitHub Packages
      cannot be a public feed**: consuming a NuGet package from it needs a PAT with
      `read:packages` even when the package is public. What it gives in exchange is deletable
      versions, which nuget.org never allows and which is exactly what a feed of throwaway builds
      needs.

      **`release.yml` gains `publish-nuget`, in a protected environment.** M8-20's rule is intact —
      a pushed version can be unlisted but never withdrawn, so a person decides. **What changed is
      where the person stands.** They used to run `dotnet nuget push` from their own machine with
      their own key; they now approve the `nuget-org` environment, and the workflow pushes. Same
      gate, but the key is an environment secret rather than a personal one, the step is
      repeatable, and the push is recorded against the run. Dated amendment in `roadmap.md`.

      **Exact filenames in the push steps, never a glob** — `InfoCarrier.Core.*.nupkg` also matches
      `InfoCarrier.Core.AspNetCore`, which is the shape of the bug N8 just removed. Spending the
      lesson once is cheaper than learning it twice. Symbols are a separate, `continue-on-error`
      push: a failed symbol upload must not leave a release half-published.

      **New: `docs/versioning.md`**, which is where the whole of this now lives — what each part of
      the version means, why the three assembly fields differ, why lock-step, and the release
      sequence. `README.md`, `roadmap.md` and `ci-cd.md` point at it.

      **Two one-time setups are the user's and cannot be done from here**: the `nuget-org`
      environment with required reviewers and its `NUGET_API_KEY`, and (still outstanding from N5)
      Pages. Both fail closed.

      **Gates: none of the repository's** — two workflow files and four Markdown files, nothing
      under `src/` or `test/`. All four workflows parse as YAML, checked.

- [x] **N9. The publish job fails usefully while there is no key.** `<this commit>`

      The `nuget-org` environment and its required reviewers now exist; **`NUGET_API_KEY` does not
      yet**, because the first upload is being done by hand. That is a supported state and the
      workflow now says so rather than discovering it at `dotnet nuget push`.

      **A `Require a key` step, and it fails rather than skips.** A green *Publish to nuget.org*
      that pushed nothing is the false-clearance shape this repository has paid for before — a gate
      that passes for ever because it silently does nothing. `eng/trim-ratchet.sh`'s first version
      was exactly that. So the missing key is an **error**, with the four `dotnet nuget push`
      commands written into `$GITHUB_STEP_SUMMARY` filled in with the version being released, and
      the same list restored to the Release body. Nothing about the release is lost: `release` has
      packed, gated and published the GitHub Release before this job is even offered for approval.

      **The guard was run both ways**, its `run:` block extracted from the parsed YAML so what was
      tested is what the workflow will execute:

      | | |
      |---|---|
      | `NUGET_API_KEY=abc123` | `nuget-org carries a key; publishing.` → exit **0** |
      | `NUGET_API_KEY=""` | `::error::…` plus the filled-in commands in the summary → exit **1** |

      A guard that cannot fail is not a guard, and this one was made to do both.

      **The key now travels as an environment variable** on each push step rather than interpolated
      into the command line, so it cannot appear in an echoed command.

      **Pages is enabled**, so `docs.yml` will deploy on the first push. Nothing in the repository
      changes for that — the workflow was already correct and waiting on the switch.

      **Gates: none of the repository's.** One workflow file and three Markdown files.

- [x] **N10. A version claim in N8's own new document was wrong, and packing twice found it.**
      `<this commit>`

      `versioning.md` said an untagged build is *"the next patch plus its height — `10.0.1-alpha.0.7`"*.
      That is what MinVer does after a **stable** tag. After a **prerelease** tag it appends the
      height to the prerelease identifiers instead, and the patch does not move. Measured on this
      repository with the tag two commits back:

      | Last tag | Produced |
      |---|---|
      | `v10.0.0-preview.1` | `10.0.0-preview.1.2` |
      | none | `10.0.0-alpha.0.512` |

      Both were read off a real `dotnet pack`, the second by deleting the tag, packing, and putting
      it back. The section is now a table of all three cases with the sort order spelled out, since
      ordering is the only property the feed actually depends on.

      **The same two packs exposed something larger: a tag that is not pushed makes CI disagree with
      the developer's machine.** `v10.0.0-preview.1` exists locally and not on the remote, so this
      machine builds `10.0.0-preview.1.2` and a runner builds `10.0.0-alpha.0.512` **from the same
      commit**. Neither fails and nothing warns. It is now a `danger` admonition in `versioning.md`,
      because it is the one way "the tag is the version" can bite.

      **Gates: none.** Markdown only.

- [x] **N11. `main` becomes the only trunk, and 14 references had to move first.** `<this commit>`

      **The branch layout is v1's and v2 does not need it.** `master` and `develop` are the *same
      commit* (`9e7831a`) with `release/2.2`, `release/3.1` and `release/5.0` beside them — a
      GitFlow layout where `develop` integrates and a release branch stabilises. Since N8 the
      **tag** is the release: MinVer derives the version from it and `release.yml` fires on it, so a
      release branch holds nothing and a `develop`/`master` split decides nothing. One trunk plus
      tags.

      **`main`, not `develop`.** The name `develop` conventionally means *"not the released
      branch"*, which is the opposite of what a sole trunk is. `main` was also already named in all
      three workflow triggers and in `build.yml`'s `pull_request` filter, so those needed no change
      beyond dropping the branches that are leaving.

      **v1 is untouched, by decision.** `master`, `develop`, the three `release/*` branches and the
      thirty-odd tags from `1.0.0` to `3.1.1` all stay exactly as they are. Nothing is renamed and
      nothing is deleted, so every existing link to v1 keeps resolving. That also means v1's history
      was never at risk from this: it is held by tags and release branches regardless of what any
      branch ref does.

      **Fourteen references named the working branch, and each would have 404'd the moment it went
      away.** Found by grep before anything moved, not after:

      | | |
      |---|---|
      | `website/mkdocs.yml` `edit_uri` | *Edit this page* on **every** site page |
      | `installation.md`, `samples.md` | `git clone -b v10-claude` |
      | `docs/nuget-readme.md` | `blob/v10-claude/…security-review.md` and `tree/v10-claude`, **inside the published package** |
      | `build.yml`, `docs.yml`, `packages.yml` | trigger lists |
      | `ci-cd.md`, `versioning.md` | prose |

      The two `git clone` instructions lose their `-b` and their *"note the branch"* note, and
      `nuget-readme.md`'s source link becomes the bare repository URL — **both of which are only
      true once `main` is the default branch**, which is a repository setting and not something a
      commit can do.

      **The timing was the lucky part rather than the clever part.** Nothing is on nuget.org yet, so
      the package README carrying `blob/v10-claude/…` has not shipped — and a package README is
      immutable per version, so a link fixed after publishing stays broken for that version for
      ever.

      **Not rewritten: the historical entries above.** N3 and N8 still say `v10-claude`, because
      that is what was true when each landed. This entry supersedes N8's *"every code push to
      `main`/`v10-claude`"*.

      **Gates: none of the repository's** — three workflow files and six Markdown files, nothing
      under `src/` or `test/`. `mkdocs build --strict` green, and all four workflows re-parsed to
      confirm the triggers are `[main]` and nothing else.

- [x] **N12. CI had been red since M8-32, and the documented way to check could not see it.**
      `<this commit>`

      **Ten commits, every one of them reporting a green gate, every one of them red on the
      server.** `gh run list` was the first thing run after the CLI was authenticated, and the
      pattern was immediate: `Step M8-30` succeeded in 7m49s, and from `Step M8-32` onward every
      *Build & Test* run failed in **under a minute** — a build failure, not a test failure. M8-32
      is the commit that turned `TreatWarningsAsErrors` on for CI. **This predates Phase N; N1–N11
      were committed on top of it, and each of their "gates green" claims was made with a command
      that could not fail this way.**

      **The failure is five `IL2110`/`IL2111` errors in `samples/Northwind.Client`, in Razor
      *generated* code** — `Router.NotFoundPage`, `LayoutView.Layout` in `App_razor.g.cs`. Not this
      repository's code, and not fixable here.

      **The mechanism was already written down in `Directory.Build.props`, one step short of the
      conclusion.** That file explains that `ILLinkTreatWarningsAsErrors` covers the ILLink *task*
      and not the trim *analyzer*, and that `eng/trim-ratchet.sh` therefore passes
      `-p:TreatWarningsAsErrors=false` on its own publish. What nobody carried further: the
      **ordinary build** compiles that same project, `SuppressTrimAnalysisWarnings=false` turns the
      analyzer on for it in Release, and M8-32 then made every one of those an error.

      **Why it went unseen is the part worth keeping.** CLAUDE.md's reproduction command is
      `CI=true dotnet build InfoCarrier.Core.slnx` — **no configuration**, so it builds Debug. The
      Blazor sample only trims in Release. Run side by side:

      | Command | Result |
      |---|---|
      | `CI=true dotnet build InfoCarrier.Core.slnx` (documented) | `Build succeeded. 0 Warning(s), 0 Error(s)` |
      | `CI=true dotnet build InfoCarrier.Core.slnx --configuration Release` (what CI runs) | `Build FAILED. 5 Error(s)` |

      **A gate that cannot fail is not a gate**, and this one could not — the whole class of
      diagnostic was invisible to it. The command is corrected in CLAUDE.md,
      `Directory.Build.props`, `README.md`, `docs/build-warnings.md` and `docs/versioning.md`, each
      saying why the configuration is part of it.

      **The fix downgrades, it does not silence.** `WarningsNotAsErrors` for `IL2110;IL2111`, in the
      sample's Release property group only. `NoWarn` would have been worse than useless:
      `eng/trim-ratchet.sh` **counts** these, so silencing them is how a ratchet starts reporting
      zero and passes for ever — the failure its own clean-publish rule exists to prevent. Two codes
      rather than the family, so a *new* trim diagnostic still fails CI and forces a look. Measured
      after: build `5 Warning(s), 0 Error(s)`; ratchet `OURS: 88`, `Northwind 8`, `OK (88 <= 88)` —
      the counted set is untouched.

      **Two more things the CLI found in the same pass, neither of them a code defect:**

      - **`Docs` failed on `configure-pages`** for every run before Pages was switched on — which
        is the fail-closed behaviour N5 designed, working. After it was switched on it failed
        again, differently: *"Branch `main` is not allowed to deploy to github-pages due to
        environment protection rules."* The `github-pages` environment's branch policy allowed
        **`develop`** only — a v1 branch that cannot build this site at all. `main` added, the
        stale `develop` policy deleted, re-dispatched: **green in 38s, and the site now answers.**
        Confirmed by fetching `/limitations/` and reading its heading back.
      - **`Packages` failed for the same build reason**, so nothing has reached GitHub Packages yet.
        It will on this push.

      **Gates: `CI=true dotnet build … --configuration Release` → `0 Error(s)`;
      `eng/trim-ratchet.sh` → `OK (88 <= 88)`.** Not `eng/measure.sh`: nothing under `src/` or
      `test/` changed, and the release pipeline runs the full suite regardless.

- [x] **N13. N12 broke the build with the comment explaining N12, and did not re-run the gate.**
      `<this commit>`

      **The PR's CI failed at `Restore`, in 17 seconds**, with
      `NETSDK1124: Trimming assemblies requires .NET Core 3.0 or higher` — an error that names
      trimming and has nothing to do with it. `Directory.Build.props` was **unparseable**, so there
      was no `TargetFramework` for the trimming check to be satisfied by, and that is what it says
      when it finds none.

      **A double hyphen cannot appear inside an XML comment.** N12's own comment wrote the
      corrected gate command in full — `dotnet build InfoCarrier.Core.slnx --configuration
      Release` — inside `<!-- -->`. The comment explaining how to keep CI green is what turned CI
      red. It is `-c Release` now, with a note saying why the short form is required rather than
      preferred.

      **The real defect is the order of operations, and it is the one N12 had just finished
      documenting.** The sequence was: edit the sample csproj → build Release, green → start the
      trim ratchet → *then* edit `Directory.Build.props` → commit. **Nothing was built after the
      last edit.** N12's own gate line was true of a tree that no longer existed by the time it was
      written. One commit after writing *"a gate that cannot fail is not a gate"*, the failure was
      not running the gate at all.

      **`eng/measure.sh`'s rule about reading output applies to the instrument's timing too.** The
      trim ratchet reported `OK (88 <= 88)` and was believed — but it had been launched *before*
      the props edit, so its publish read the good file. A green result from a run that started
      before the change is not a result about the change.

      **This error had been seen before and diagnosed the other way round.** M8-32's entry records
      *"a build failure read as XML damage was `NETSDK1124` from a stale `obj/Release` the trim
      publish left behind"*. Same error, opposite cause, and this time it really was XML damage —
      on a fresh CI runner with no `obj/` to be stale. **`NETSDK1124` means "no usable
      TargetFramework"; it does not tell you why.** Parse the props files before theorising.

      **Gates, run after the last edit this time**, and a check that did not exist before: every
      `.props` and `.csproj` in the repository parsed as XML — 10 files, 0 invalid. `dotnet restore`
      clean; `CI=true dotnet build InfoCarrier.Core.slnx -c Release` → **`5 Warning(s),
      0 Error(s)`**.

- [x] **N14. `edit_uri` was inert, and finding that out is also the proof two workflows work.**
      `<this commit>`

      **N11 corrected `edit_uri` from `v10-claude` to `main` and called it *"Edit this page on every
      site page"*. There was no edit link on any page.** Material renders that control only when
      `content.action.edit` is among the theme features, and it was not — so the setting had been
      doing nothing since the site was created, and pointing it at a wrong branch would have cost
      nothing either. Found by `curl`-ing the live page and grepping for any `edit` control at all:
      empty.

      `content.action.edit` and `content.action.view` are on now, and the built page carries
      `href="https://github.com/azabluda/InfoCarrier.Core/edit/main/website/docs/limitations.md"`.

      **The N11 entry is not rewritten** — it says what was believed then, and the belief was the
      defect. This entry is the correction.

      **It is also the only honest way to prove two things the merge of #6 could not.** That merge
      touched no `website/**` file, so `docs.yml`'s path filter correctly skipped it — which left
      *"Docs fires from a push"* and *"the site serves new content"* both unverified, and the only
      green `Docs` run to date was one dispatched by hand. A path-filtered workflow that has only
      ever run manually is not a working workflow. This change touches `website/mkdocs.yml`, so:

      | Claim | How this proves it |
      |---|---|
      | `docs.yml` fires from a **push** | it must run on this merge, without a dispatch |
      | Pages serves the **new** build | an edit control appears on the live site, and there was none before |

      A verification that needed a manufactured commit would have been noise. This one was a real
      defect that happened to be in the right file.

      **Gates: `mkdocs build --strict` green.** No `src/`, no `test/`; one line of theme
      configuration.

- [x] **N15. Trusted Publishing: no publishing secret exists at all.** `<this commit>`

      nuget.org's own account page now says it plainly: *"API keys are strongly discouraged for
      automated publishing and should be replaced with Trusted Publishing."* N9 had priced that as
      *"prefer it if available, verify before relying on it"* — it is available, so it is taken, and
      the API-key route is gone rather than kept as a fallback. One path is easier to keep true
      than two.

      **What replaces the secret.** The job asks GitHub for an OIDC token; nuget.org validates it
      against a policy naming owner, repository, workflow file and environment, and returns an API
      key valid for **one hour**. Nothing long-lived is stored, so nothing long-lived can leak, be
      rotated late, or sit forgotten on a laptop. `grep 'secrets.'` over `release.yml` now returns
      **nothing**.

      **Read from the vendor rather than remembered**, because the mechanism is newer than any
      recollection worth trusting: `NuGet/login@v1` takes `user` (the nuget.org *profile name*, not
      an email) and emits `NUGET_API_KEY` as a step output; the job needs `id-token: write`. Both
      confirmed against the action's own repository and Microsoft's documentation before the
      workflow was written.

      **The exchange sits one step above the pushes, and that placement is load-bearing.** Each
      token buys exactly one key, the key lives an hour, and asking early then pushing late is the
      documented way to have it expire mid-release — with the first package published and the
      second not.

      **THE APPROVAL GATE DID NOT EXIST, and this is the finding of the step.** The `nuget-org`
      environment was created, but the API says:

      ```
      protection_rules  : []
      branch policy     : null
      secrets           : 0
      ```

      **Required reviewers had been ticked but never saved.** With no key that was harmless — the
      job stopped at its own guard. The moment a credential existed, a tag would have published to
      nuget.org **unattended**, which is the single thing M8-20's rule exists to prevent. Found by
      reading the environment over the API instead of trusting the UI, and the same lesson as
      everything else today: *the setting you believe you made is not evidence.*

      **Setup is now two halves that each fail closed**, written up in `versioning.md` with the
      four policy fields and the exact `gh api` call that proves the reviewer stuck. Also recorded:
      the policy covers **every package owned by the account**, so neither package has to exist on
      nuget.org first — which is the question that would otherwise have been asked at the worst
      moment, since neither does.

      **Gates: `mkdocs build --strict` green; `release.yml` re-parsed** — `permissions
      {contents: read, id-token: write}`, environment `nuget-org`, six steps in the intended order.
      No `src/`, no `test/`.
