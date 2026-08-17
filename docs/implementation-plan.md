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

## Phase J — provider neutrality and store coverage (M9)

Scope lives in [`roadmap.md`](roadmap.md) §M9. **M8 is not closed**, so this plan holds two
milestones at once for the first time; Phase J is appended rather than replacing Phases H and I,
and the whole file is rewritten when M8 closes.

The audit that opened this phase is in [`architecture.md`](architecture.md) §6a, D3 (amended) and
D4 (new). Two things it established are worth restating here because they shape the order below:
the package reference is a **symptom**, and the **fixed query-boundary allowlist** is the
assumption nobody had recorded.

### J1–J3 — the tier moves

CLAUDE.md's A79/A80 rule: a base belongs to exactly one tier, and *the tier that translates is the
one whose green means more*. Three bases sit on Tier A only because that is where they were first
adopted, and each carries skips that are **EF's InMemory limits, not this provider's**. EF's own
SQLite suite skips **zero** of them, which is the whole argument:

| Base | Skips today | EF InMemory | EF SQLite | EF SqlServer |
|---|---|---|---|---|
| `KeysWithConverters` (#26238) | 7 | 8 | **0** | **0** |
| `BuiltInDataTypes` / `CustomConverters` (#17050) | 4 | 4 | **0** | **0** |
| `ProxyGraphUpdates` (#2166, #3924) | 13 | 13 | **0** | — |

Measured one at a time, because a combined move cannot tell which base moved the number.

- [x] **J1. `KeysWithConverters` to Tier B.** `<this commit>`
      `Total tests: 22453, Passed: 22224, Failed: 15, Skipped: 214` (`j1b`), against
      `22453 / 22219 / 13 / 221` (`m8-17`). **All four figures are read out of the run's own summary
      block; none is arithmetic.** Seven skips gone, five of them now passing, and
      **`failed` rises 13 → 15 deliberately** — the two that fail describe *this provider* where
      before they described EF's InMemory store, which is the whole point of the move.

      **The move needed one thing EF's SQLite fixture gets for free, and finding it cost a run.**
      Deleting the seven skips and the three `Ignore<EnumerableClassKey*>()` calls put the class at
      **47 failures**, every one `CollectionWithoutComparer`: `EnumerableClassKey.Id` is an
      `IEnumerable` behind a value converter with no value comparer, and model validation warns.
      `KeysWithConvertersSqliteFixture` does not hit it because its `AddOptions` is
      `builder.UseSqlite(…)` and **never chains to base**, so `FixtureBase`'s
      `ConfigureWarnings(Default(Throw))` never runs. This client cannot take that route —
      `UseSqlite` is precisely what it does not do — so it ignores the one event id instead, on
      **both** halves, because the model is validated twice (A49).

      **The two residual failures are both new information, and both are the wire's rather than the
      store's.** Neither existed as a failure before; both were hidden inside a skip that was about
      InMemory.

      | Test | What it says |
      |---|---|
      | `Can_insert_and_read_back_with_enumerable_class_key_and_optional_dependents` | `NotImplementedException` from `EnumerableClassKey.GetEnumerator()`, reached from `DynamicValueMapper.MapToNode`. The mapper sees `IEnumerable` and takes the **collection** branch on a value that is a *key*, not a collection. **ADR-012's family exactly** — a CLR type whose member throws for an ordinary instance, like `IPAddress.ScopeId` (C23) and `Uri.AbsolutePath` (C34) — except the value arrives as a `ConstantExpression` in a query, where there is no property to read a converter from. |
      | `Can_query_and_update_owned_entity_with_value_converter` | `MissingMethodException: Cannot dynamically create an instance of type '…+Key[…]'. Reason: No parameterless constructor defined.` Raised deserializing the round trip, so it is `RehydrateObject` on a nested generic type with no parameterless constructor. |

      Left red and classified, as CLAUDE.md requires. Neither is a reason to go back to Tier A:
      on Tier A they were unreported.
- [x] **J2. `CustomConverters` to Tier B.** `<this commit>`
      `Total tests: 22453, Passed: 22225, Failed: 18, Skipped: 210` (`j2b`), against
      `22453 / 22224 / 15 / 214` (`j1b`). Four skips gone, `failed` rises 15 → 18 deliberately.

      **The four #17050 skips are all in `CustomConverters`, not in `BuiltInDataTypes`**, which is
      why this step is named for the class rather than for the file. `BuiltInDataTypes` and
      `ConvertToProviderTypes` share the file and carry no skips at all; they are J2b below.

      Three of the four now pass — `Value_conversion_with_property_named_value`,
      `Collection_property_as_scalar_Any`, `Collection_property_as_scalar_Count_member` — and every
      one of them is a **collection property behind a value converter**, which is B4's subject and
      the shape this wire has paid most for. They had never once been executed.

      **Two InMemory statements went with the skips**, and both turned out to be real coverage:
      a non-composed `GroupBy` is no longer refused by the store, and
      `Optional_datetime_reading_null_from_database` is no longer a silent `Task.CompletedTask` —
      SQLite has a null to read.

      **An override adopted from EF was disproved by measurement and deleted.**
      `CustomConvertersSqliteTest` overrides `Value_conversion_on_enum_collection_contains` to
      assert a translation failure; taking it measured `Assert.Throws() Failure: No exception was
      thrown`, because the query this provider ships is *answered*. Kept out, with the reason in
      the class. This is CLAUDE.md's rule read in the other direction: an override of ours that EF
      does not need is a workaround, and so is one of EF's that we do not need.

      **Three residual failures, all new information and none of them the store's:**

      | Test | What it says |
      |---|---|
      | `GroupBy_converted_enum` | `GroupBySingleQueryingEnumerable+InternalGrouping<…>` **is not on the deserialization allowlist**. An EF-internal grouping type reaches the wire. |
      | `Value_conversion_is_appropriately_used_for_join_condition` | The `Join` over two converted columns is not translated. |
      | `Collection_enum_as_string_Contains` | `Assert.Throws() Failure: No exception was thrown` — and the base's body is `Assert.Throws<InvalidOperationException>(…)` around the query. **A28 family**: the spec test asserts a limitation this provider does not have, and it returns the right answer. |

- [x] **J12a. `GraphUpdates` to Tier B — the store switch and the enlisting hook.** `<this commit>`
      `Total tests: 22654, Passed: 22461, Failed: 16, Skipped: 177` (`j12a`) against
      `22672 / 22487 / 14 / 171` (`j9`). **127 tests moved to a store that enforces constraints and
      it cost 2 failures.** J11 did the heavy lifting; this is what was left.

      **Zero `database is locked`.** The `UseTransaction` override landed in the *same* change as
      the store switch, which is the whole lesson of J3's first attempt — omitting it there cost two
      hours of 30-second timeouts. Applied first time here.

      `total` falls 18 and `skipped` rises 6: EF's six `GraphUpdatesSqliteTestBase` skips, mirrored
      one for one, and xUnit reports a skipped theory as one test rather than as its
      parameterizations — the same accounting `known-failures.txt` records for C94.

      **The two new failures are one message:** `SQLite Error 19: 'NOT NULL constraint failed:
      OwnedOptional1.Id'`, on `Save_changed_owned_one_to_one` and `Save_changed_owned_one_to_many`.
      An owned dependent's key arrives null where the store requires it. Filed as J13.

      **Still to do, and deliberately a separate step:** the class carries **28** silent
      `Task.CompletedTask` overrides — 28 tests that do nothing at all, most of them commented
      *"FK uniqueness not enforced in in-memory database"* or about cascade delete *in store*. Those
      are precisely what a real store tests, and they are J12b. Splitting them off keeps this
      measurement interpretable.

- [x] **J12b. Deleted `GraphUpdates`' 28 silent no-op overrides.** `<this commit>`
      `Total tests: 22654, Passed: 22451, Failed: 26, Skipped: 177` (`j12b`). **A deliberate rise of
      10, and the honest trade is 18 for 10**: of 28 tests that did nothing at all, 18 now pass and
      10 fail. 140 lines deleted.

      **`total` is unchanged, and that is the finding.** These were never skips — a
      `=> Task.CompletedTask` override *counts as a passing test*. So **28 green ticks in every
      previous run were an empty method body**. That is worse than a skip, which at least announces
      itself in the `Skipped` column, and it is why deleting them is progress even at +10.

      **The 10 are five tests × async, in three groups, classified rather than counted:**

      | Group | Count | Message |
      |---|---|---|
      | `Cruiser` / `CruiserWithSentinel` not in the model | 4 | *"The entity type 'Cruiser' was not found. Ensure that the entity type has been added…"* — a **model** fault, which cannot be a store limitation |
      | Unknown foreign-key value at save | 2 | *"The value of 'SomethingOfCategoryB.CategoryId' is unknown when attempting to save…"* |
      | `DbUpdateException` | 5 | a store constraint; overlaps J13's shape |

      Every one of the five test names is `Can_insert_when_…_has_default_value` or
      `…_has_sentinel_value`, so **this is the sentinel/default-value family** — and
      `ChangeEntryMapper`'s `SentinelProperties` comment is where to start, because it already
      describes a value the wire cannot distinguish from unset. Filed as J14.

- [ ] **J14. The sentinel/default-value family on a real store** — 10 failures, 5 tests × async.
      Three groups, classified in J12b. **Start with the four `Cruiser`-not-in-model ones**: an
      entity type missing from the model is a fixture or convention fault, cannot be a store
      limitation, is the cheapest of the three to settle, and may explain the others.

- [ ] **J13. `NOT NULL constraint failed: OwnedOptional1.Id`** — an owned dependent's key arrives
      null. Two tests, one message.

- [x] ~~**J12. `GraphUpdates` to Tier B — assessed 2026-08-17.**~~ Split into J12a/J12b above.
      **Original assessment:**
      1787 tests, and **the reason not to move it has just gone**. The assessment, not a guess:

      | Fact | Value |
      |---|---|
      | Skips in `GraphUpdatesInfoCarrierTest` today | **0** — so nothing is being retired; this is coverage, not cleanup |
      | Uses `ExecuteWithStrategyInTransactionAsync` | **yes** — so it needs J3's `UseTransaction` override, which is now a known one-liner |
      | Skips in EF's `GraphUpdatesSqliteTestBase` | **6** — mirror them, and read each one first |
      | EF's SQLite `UseTransaction` | `facade.UseTransaction(transaction.GetDbTransaction())` — ADR-013's call, so ours is `facade.UseInfoCarrierTransaction(transaction)` as in J3 |

      **Why it is worth doing and why it was not before.** `GraphUpdates` is the same corpus as
      `ProxyGraphUpdates` without proxies, and on Tier A it has never met a store that enforces a
      foreign key — which is exactly the blind spot J11 was hiding in. J11's defect was live for
      every one of those 1787 tests and none of them could see it. **The move is now expected to be
      largely green rather than reckless**, because the one mechanism that made J3 explode is fixed.

      **Do it in this order, and do not shortcut it:** the `UseTransaction` override *first* and in
      the same change as the store switch — J3 proved that omitting it costs two hours of 30-second
      lock timeouts rather than a fast failure. Then adopt EF's six skips, each checked against
      `subrepos/efcore` rather than assumed. Then measure; expect a rise, and classify it before
      committing.

      **The `ReseedAsync` override should be kept** (it reseeds through the backend, not the client)
      and the `ExecuteWithStrategyInTransactionAsync` override should be **deleted** — that is the
      ConferencePlanner precedent J3 followed, and it held there.

- [x] **J2b. `BuiltInDataTypes` and `ConvertToProviderTypes` to Tier B — measured in halves, both at ZERO cost.** `<this commit>`
      `26 / 22654` unchanged across both, **0 fixed and 0 broken each time**. The file was split so
      each half could be measured alone, which is what makes "zero cost" a fact rather than a hope.

      Both fixtures now carry `BuiltInDataTypesSqliteFixture`'s eight capability flags. **Four change
      value and none is cosmetic** — `StrictEquality`, `SupportsDecimalComparisons` and
      `PreservesDateTimeKind` become `false`, `SupportsBinaryKeys` becomes `true` — and each turns
      assertions on or off inside the base. That they change and nothing breaks is the result.

      **What the move was for**: each class had a silent
      `Optional_datetime_reading_null_from_database() => Task.CompletedTask`, because the InMemory
      store has no null to read. SQLite has one, so both now run, and both pass. Same accounting as
      J12b — an empty override already counted as a passing test, so `total` does not move; the
      difference is that the tick now means something.

      **The earlier price of "2201 lines of EF SQLite surface" was for the wrong thing.** That file is
      overwhelmingly `AssertSql`, which this provider cannot use and does not need. What was actually
      required was eight flag values and two deletions.

- [x] ~~**J2b (original entry).**~~ Superseded above.
      No skips to retire, so this is not J2's argument. What it *would* retire is the silent
      `Optional_datetime_reading_null_from_database() => Task.CompletedTask` in each — a test that
      does nothing at all, because the InMemory store has no null to read. **Priced before
      starting:** `BuiltInDataTypesSqliteTest` is **2201 lines**, so the adoption surface is large
      even though most of it is `AssertSql` this provider cannot use. Worth doing, worth doing on
      its own, and worth measuring in halves.
- [x] **J3. `ProxyGraphUpdates` to Tier B — DONE on the second attempt.** `<this commit>`
      `Total tests: 22672, Passed: 22319, Failed: 182, Skipped: 171` (`j3b`) against
      `22456 / 22229 / 17 / 210` (`j10`). **A deliberate rise of 165, and the largest this file has
      recorded since L1** — which is the right comparison, because it is the same kind: skipped
      tests becoming real ones.

      **The first attempt's diagnosis was wrong, and finding out cost nothing but a grep.** It
      concluded a product feature was missing. **Nothing was missing.**
      `UseInfoCarrierTransaction` and the non-owning `UseTransaction(token)` have shipped since M4.
      What was missing was this class's **`UseTransaction` override** — which
      `ConferencePlannerInfoCarrierTest` and `OptimisticConcurrencyInfoCarrierTest` already carry,
      and whose comment on the first names the exact symptom: *"Without enlisting, the second runs
      on its own SQLite connection and gets 'database is locked'."* **Before pricing a gap, check
      whether a sibling of it already works** — two classes in this same suite did.

      With the hook in place the deadlock is gone completely: **0 `database is locked`**, and the
      run takes **5.6 minutes** instead of the two hours the first attempt was still short of.

      **What the move bought and what it cost.** `skipped` 210 → 171 (the 13 skips × 3 flavours),
      `total` 22456 → 22672 (those 39 becoming 216 real parameterizations), `passed` +90.
      **165 fail, and they are one defect with 165 faces**: every single one is
      `SQLite Error 19: 'FOREIGN KEY constraint failed'`, spread 56 / 56 / 55 across the three
      proxy flavours. That is precisely what the deleted skips were about — EF's #2166 (FK
      constraint checking) and #3924 (cascade delete) are InMemory *not enforcing* either. On a
      store that enforces both, this provider's `SaveChanges` replay does not order or propagate
      deletes the way a relational store requires.

      **This is a large, previously invisible area, not a regression.** `GraphUpdatesInfoCarrierTest`
      — the non-proxy corpus, 1787 tests — is still Tier A, so the whole `GraphUpdates` family has
      never once run against a store that enforces foreign keys. Filed as J11.

      **If this rise is judged too large to hold, reverting is three edits** (store factory, the
      thirteen skips, the `UseTransaction` override) and the base returns to Tier A with EF's own
      mirrored skips — which is an adoption choice, not the test-suppression CLAUDE.md forbids.

- [ ] **J11. A foreign key that references an ALTERNATE key does not survive the replay.**
      **Narrowed 2026-08-17, before any code, and the narrowing is the point.** J3 filed the 165 as
      "cascade delete and foreign-key ordering", which was a guess from one error message. Grouping
      the failing *names* instead says something much sharper:

      | | count |
      |---|---|
      | `ProxyGraphUpdates` failures | 167 |
      | …whose name contains `alternate_key` / `_AK_` | **162** |
      | …that do not | **5** |

      Every large family is `Optional_one_to_one_with_alternate_key_*` or
      `Optional_many_to_one_dependents_with_alternate_key_*`, each at 9 parameterizations (the
      three cascade timings squared). So this is **not** a statement about cascade delete or about
      ordering — both of which apply equally to the primary-key variants, and those **pass**.
      It is: *a foreign key that points at an alternate key rather than at the primary key is not
      resolved correctly on the server.*

      **That is an existing family, not a new one.** C34 and C76 are both "a key resolved by value
      rather than by what declares it", and C76's fix keyed the placeholder map by
      `(key property, value)` and resolved through `foreignKey.PrincipalKey`. An alternate key is
      exactly where `PrincipalKey` stops being the primary key — so the first thing to read is
      whether every path that resolves a reference uses `foreignKey.PrincipalKey`, or whether some
      still assume the primary key.

      **A hypothesis that ordering explains it is already weak** and should not be spent time on
      first: deletes are tracked before everything else and *not* in dependency order
      (`ServerSaveChangesExecutor`, the `Deleted` pass), but EF sorts modification commands
      topologically itself, so tracking order is not what reaches the store.

      **AND THE `PrincipalKey` HYPOTHESIS ABOVE WAS CHECKED AND DOES NOT HOLD. Read this before
      re-deriving it.** Every reference-resolution path in `ServerSaveChangesExecutor` already
      resolves through `foreignKey.PrincipalKey`, not through the primary key:

      | Path | What it does |
      |---|---|
      | `PrincipalPropertyOf` | matches by **position** into `foreignKey.PrincipalKey.Properties` |
      | the reference redirect | keys `qualifiedPlaceholders` on `(foreignKey.PrincipalKey.Properties[index], clientValue)` |
      | the generated-key read-back | asks `PrincipalPropertyOf(fk, property)` for `ValueGenerated` |

      The only `FindPrimaryKey()` in the file is inside an identity-conflict **diagnostic**, which
      cannot cause a store error. So C76's fix is not incomplete here, and the defect is somewhere
      else.

      **PROBED 2026-08-17. The sentinel theory is refuted, and the real shape is now visible.**
      A temporary instrument in `ChangeEntryMapper.ToChangeEntry` printed every key and foreign-key
      property of every entry leaving the client, for
      `Optional_many_to_one_dependents_with_alternate_key_are_orphaned` (27 of 27 failing):

      ```
      CLIENT OptionalAk1        state=Deleted   Id=1  AlternateId=3e3db6de…  ParentId=a2276653…
      CLIENT OptionalAk2        state=Modified  Id=1  ParentId=<null> modified=True explicit=False
      CLIENT OptionalComposite2 state=Modified  Id=1  ParentId=<null>          modified=True
                                                      ParentAlternateId=3e3db6de… modified=True explicit=True
      ```

      **Two things this settles outright.**

      1. **A nulled foreign key travels correctly.** `OptionalAk2.ParentId` leaves as `<null>` and
         is flagged `modified=True`. It is *not* dropped as "unset", so
         `SentinelProperties`/`HasExplicitValue` is **not** the mechanism. Do not re-derive this.
      2. **The row the store rejects is identified.** `OptionalAk1` — the principal — is `Deleted`,
         and its alternate key is `3e3db6de…`. `OptionalComposite2.ParentAlternateId` **still holds
         `3e3db6de…`** while its sibling `ParentId` on the same entry has been nulled. A foreign key
         still pointing at a row being deleted is exactly what SQLite refuses, and it explains the
         `alternate_key` correlation precisely: the primary-key FK is nulled, the alternate-key FK
         is not.

      **The "offending row" reading above was itself wrong, and the model says so.**
      `OptionalComposite2.ParentAlternateId` is a **non-nullable `Guid`**, and its foreign key to
      `OptionalAk1` is **composite** — `(ParentAlternateId, ParentId)`. A composite foreign key with
      any NULL component is not enforced, so leaving `ParentAlternateId` set while nulling
      `ParentId` is *correct*, and the client is right. Read the model before blaming a value.

      ## ROOT CAUSE — established 2026-08-17, three probes, no theory left

      **1. EF names the entry it blames.** Catching the failure inside
      `ServerSaveChangesExecutor` and printing `DbUpdateException.Entries`:

      ```
      SAVE-FAILED DbUpdateException   BLAMED OptionalAk1 state=Deleted
      TRACKER: OptionalAk2 Modified ×2, OptionalComposite2 Modified ×2, OptionalAk1 Deleted
      ```

      So the `DELETE` of the principal is what the store refuses, while its dependents are present
      and correctly nulled. The replay is **faithful** — the server tracks exactly the five entries
      the client sent, with the same states and the same values.

      **2. The server's ORIGINAL values are the defect.**

      ```
      SERVER OptionalAk2 state=Modified | Id=1 orig=1 | ParentId=<null> orig=<null> mod=True
      ```

      `ParentId` is `<null>` **and its original is `<null>` too** — while the row in the store holds
      `1`, pointing at `OptionalAk1`. **The server believes this foreign key was always null.**

      **3. Why, and it is written in the code as a deliberate fact.** `ChangeEntryMapper` sends
      original values for **concurrency tokens only** — `if (carriesOriginals && property.IsConcurrencyToken)` —
      and its comment states the reasoning: *"the server rebuilds the entity from the current
      values, attaches it and sets `Modified`, so every original it has equals its current one by
      construction"*. That is true, and for the concurrency check it is right. For **command
      ordering it is wrong**: EF's `CommandBatchPreparer` builds its dependency graph from
      *original* foreign-key values, because that is what tells it a dependent is *releasing* a
      principal. With `original == current == null` there is no edge from `OptionalAk2` to
      `OptionalAk1`, EF has no reason to order the `UPDATE` before the `DELETE`, and the `DELETE`
      meets a row that still points at it.

      **Why this could only ever surface here.** Tier A cannot show it — InMemory enforces no
      foreign keys, which is exactly what EF's #2166 and #3924 skips say. A single-context EF has
      the true originals from the moment it loaded the row. **Only a two-context provider against a
      store that enforces constraints can lose them**, so 1787 `GraphUpdates` tests have never
      exercised this and neither had anything else.

      ## THE WAY FORWARD

      **Send original values for foreign-key properties of a `Modified` entry**, alongside the
      concurrency tokens already sent. The channel exists on both halves —
      `ChangeEntry.SerializedOriginalValues`, applied by `ServerSaveChangesExecutor` to
      `entry.Property(...).OriginalValue` — so this widens *what* is put in it, and adds no wire
      shape and no protocol change.

      **Three constraints on the implementation, all already documented in the code it touches:**

      - **Order matters within the payload.** The originals are mapped *after* the current values,
        because a `byte[]` travels as a referenceable object and the definition must precede the
        back-reference (*"Dangling wire reference 1"*). Keep that order.
      - **Apply originals last on the server.** Setting the state re-snapshots originals from the
        entity, so anything written earlier is undone. The existing block is already last.
      - **Do not widen beyond foreign keys.** C42 measured "send every propagated foreign key back"
        at 1 fixed / 2 broken; the symmetric temptation here is to send every original. Send
        exactly the foreign-key properties, and only for `Modified` entries.

      ## DONE `<this commit>` — 167 fixed, 0 broken, `182 -> 15`

      `Total tests: 22672, Passed: 22486, Failed: 15, Skipped: 171` (`j11`). **`ProxyGraphUpdates`
      is GREEN**, and J3's deliberate rise of 165 is repaid with two to spare. One line in
      `ChangeEntryMapper`: a `Modified` entry now carries the originals of its **foreign-key**
      properties as well as its concurrency tokens.

      **The alternate-key correlation was a red herring, and the measurement says so.** All five
      non-alternate-key failures — `Avoid_nulling_shared_FK_property_when_deleting` ×3 and
      `Save_two_entity_cycle_with_lazy_loading` ×2 — closed too. So it was **one cause with 167
      faces**, not 162 plus 5, and the name-grouping that narrowed the search also over-narrowed the
      conclusion. Grouping by name is how to find a defect; only the fix says how far it reached.

      **The residual question below is now answered as far as it needs to be.** Why the
      primary-key variants passed *before* is still not fully explained — but `BROKEN: 0` across
      22,672 tests establishes the fix is general and costs nothing, which is what the question was
      guarding against.

      **      **The one thing still unexplained, and it is the check on any fix**: why the *primary-key*
      variants of these same tests pass. The mechanism above is not specific to alternate keys, so
      either those pass for an unrelated reason — a plausible candidate is that EF declares
      `ON DELETE SET NULL` for the simple optional FK and SQLite then repairs it whatever the order
      — or something narrows it. **Confirm that before believing the fix is complete**: a fix that
      closes 162 and leaves the mechanism half-understood is how a wrong revert starts.

      **The 5 that are not alternate-key are separate and small**:
      `Avoid_nulling_shared_FK_property_when_deleting` (×3) and
      `Save_two_entity_cycle_with_lazy_loading` (×2). Do not fold them in.

      The move itself was the same three lines as J1 and J2: store factory to `Sqlite`, delete the
      thirteen skips, keep the by-hand reseed. The run was stopped at **21,289 of 22,453** after
      about two hours, with **733 distinct failures, 717 of them this class**. Reasons, tallied:

      | Count | Reason |
      |---|---|
      | 471 | `InfoCarrierServerException : SQLite Error 5: 'database is locked'` |
      | 246 | `DbUpdateException` (the same lock, one frame out) |

      **The cause is one mechanism, not 717 findings.**
      `TestHelpers.ExecuteWithStrategyInTransactionAsync` opens **one** transaction on an outer
      context and then hands every inner context to `useTransaction(innerContext.Database,
      transaction)`. `ProxyGraphUpdatesSqliteTestBase` satisfies that with
      `facade.UseTransaction(transaction.GetDbTransaction())` — **ADR-013's call**, which a client
      that is never a relational context cannot make. Our `UseTransaction` is therefore a no-op:
      the inner contexts run *outside* the transaction while the outer one holds SQLite's write
      lock, and each one waits out a **30-second** lock timeout before failing. That timeout is
      why a normally six-minute suite ran for hours, and it is the same "an abandoned transaction
      wedges writes" behaviour [`roadmap.md`](roadmap.md) §M8 records from the other direction.

      **On Tier A none of this is visible**, because the transaction is ignored outright and
      `ReseedAsync` puts the data back by hand. The InMemory skips were a true statement about the
      InMemory store *and* an accidental screen over a second, larger dependency.

      **What would unblock it: a client-side way to join an open server transaction by its wire
      token.** `IInfoCarrierClient.BeginTransactionAsync` already returns that token and every
      request record already carries `TransactionId`, so the missing piece is one client API —
      plus the authorization question §M8 raises, because today any caller holding a token can join.
      Filed as J7 below rather than folded in here: it is product work with a security question
      attached, and it deserves its own measurement.

      **The check that would have caught this in a grep**, now also in `CLAUDE.md`: before moving a
      base to Tier B, grep it for `ExecuteWithStrategyInTransactionAsync`. `GraphUpdates` and
      `ProxyGraphUpdates` are on Tier A because of it, not by accident.

- [ ] **J7. Let a client context join an open server transaction by token.**
      What J3 needs. One client-side API over the existing wire — no protocol change, since the
      token is already returned and already carried.
      **The blocking objection recorded here on 2026-08-16 does not survive checking, and the
      correction is the useful part.** It said a token is a bearer credential and that "who may
      join" had to be answered before shipping. The first half is true; the second does not follow.
      `InProcessInfoCarrierServer.Acquire` already runs **any** request naming a token on that
      transaction's context, and every request record already carries `TransactionId` — so the
      exposure is a property of the wire protocol as it stands, and **this API widens nothing**.
      Binding a token to its creator stays worth doing and stays M8's item.
      What does need deciding is ownership: a joined transaction must not commit or roll back on
      dispose. [`architecture.md`](architecture.md) §6a **D6**.

### J4 — the test project organised by backend store

v1's layout (`InMemory/`, `SqlServer/`, `TestUtilities/`, root for store-independent tests), which
makes a store's coverage countable by looking at the tree. A **pure move**: `eng/measure.sh` must
return the same failure count and total with empty FIXED, BROKEN and REASONS diffs.
`test/known-failures.txt` holds fully-qualified names, so it moves in the same commit.

The census that sizes it, taken by resolving each file's fixture to its backend store rather than
by grepping names (42 `Scaffolding/Baselines/**` files are excluded from compilation and are not
counted):

| Backend | Files |
|---|---|
| InMemory (Tier A) | 61 |
| SQLite (Tier B) | 24 |
| Store-independent (`Expressions/`, `ProjectionSplit/`, compliance, infrastructure) | 26 |
| Shared harness (`TestUtilities/`, used by both) | 4 |

- [x] **J4. Reorganise `test/InfoCarrier.Core.FunctionalTests` by backend store.** `<this commit>`
      `Total tests: 22453, Passed: 22225, Failed: 18, Skipped: 210` (`j4`) — **all four figures
      identical to `j2b`**, and `REASONS: unchanged`.

      **The neutrality proof needed one extra step, because the failing test *names* necessarily
      move.** `measure.sh` snapshots fully-qualified names, so a namespace change makes FIXED and
      BROKEN both non-empty by construction — 18 names leave, 18 arrive. Stripping the inserted
      store segment and diffing the two snapshots gives **no differences at all**: the same 18
      tests, failing for the same reasons, before and after.

      The layout, after v1's (`InMemory/`, `SqlServer/`, `TestUtilities/`, root):

      | Location | Files | What |
      |---|---|---|
      | `InMemory/` | 57 | Tier A test classes, sub-structure kept (`Query/`, `Update/`, `Scaffolding/`) |
      | `InMemory/Scaffolding/Baselines/` | 42 | not compiled; travels with its test — see below |
      | `Sqlite/` | 25 | Tier B test classes (`Query/`, `Query/Associations/`, `Update/`, `Types/`, `BulkUpdates/`) |
      | `TestUtilities/` | 16 | the harness, **kept whole** — it defines both store factories, so it is shared by construction |
      | root, `Expressions/`, `ProjectionSplit/`, `ModelBuilding/` | 18 | store-independent: wire format, boundary analysis, compliance, infrastructure |

      **Four things this turned up that a rename alone would not have:**

      1. **`test/known-failures.txt` needed no change.** It holds `failed=`, `total=` and prose —
         **no test names at all** — so namespaces cannot move it. The expectation that it would was
         wrong, and checking took one grep.
      2. **`Scaffolding/Baselines/` has to travel with its test.** `CompiledModelTestBase`
         locates it from `[CallerFilePath]` on `AddReferences`, which our
         `CompiledModelInfoCarrierTest` overrides — so the baselines follow that *file*, not the
         project. The two `csproj` lines moved with it, and getting that wrong reproduced CLAUDE.md's
         documented **125 duplicate-definition errors** immediately, which is a good tripwire.
      3. **One file was split.** `BuiltInDataTypesInfoCarrierTest.cs` held three classes across two
         tiers after J2. `CustomConverters` is now `Sqlite/CustomConvertersInfoCarrierTest.cs`.
      4. **A shared helper crossed a tier and my grep missed it.** `AssociationsWarnings` is
         `internal static class`, which the cross-reference script's pattern did not match, and it
         is used by two Tier B classes. **The compiler is the reliable cross-reference checker**;
         the grep is only for planning. Every other cross-tier "reference" the script did find —
         eight of them — turned out to be prose in `<c>` tags.

### J1/J2 residual — all five classified

The two tier moves raised `failed` 13 → 18. Diffing the current run against the M9 baseline gives
**exactly five new, none gone**, and every one is this provider's rather than the backing store's.
Classified here so the count keeps meaning something.

**One is not a gap at all.**

| Test | Verdict |
|---|---|
| `CustomConverters.Collection_enum_as_string_Contains` | **A28 family. Red forever, correctly.** The base's whole body is `Assert.Throws<InvalidOperationException>` around the query; this provider answers it. **Probed rather than assumed**, because "no exception" and "wrong answer" look identical from the count: with one seeded row, `Seller` returns `server=1, client=1` and `Customer` returns `server=0, client=0`. A filter that matched everything would have shown `Customer: server=1`. The answer is right. |

**Two are one defect with two faces — a captured constant whose CLR type the wire cannot
round-trip.** Both are *query constants*, not stored values, and both are a **key behind a value
converter** that travels by reflective object shape instead of as its provider value:

| Test | Which face |
|---|---|
| `KeysWithConverters.Can_insert_and_read_back_with_enumerable_class_key_and_optional_dependents` | **Outbound.** `EnumerableClassKey` implements `IEnumerable`, so `DynamicValueMapper.MapToNode` takes its **collection** branch and calls `GetEnumerator()`, which EF's test type does not implement. Reached from `ExpressionToNodeTranslator.VisitConstant`. |
| `KeysWithConverters.Can_query_and_update_owned_entity_with_value_converter` | **Inbound.** `protected class Key(string id) { public string Value { get; } }` — no parameterless constructor, one get-only member — so the **server** cannot rebuild it: `MissingMethodException: Cannot dynamically create an instance … No parameterless constructor defined`, surfaced through `RoundTripAsync`. |

**ADR-012's seam is the shipped answer and it does not fit here**, which is the finding. Its two
standard mappers are *BCL* types whose members throw for ordinary instances (`IPAddress.ScopeId`,
`Uri.AbsolutePath`) — an application storing one "has opted into nothing". These two are the
**application's own** types, and the model already says exactly how they become primitives: a value
converter. **The open question is whether a constant whose CLR type matches a mapped property type
should travel as its provider value**, the way `ChangeEntryMapper` already sends property values
(A19). That is a design question, not a bug fix, and it is filed as J9 below.

**One is a code path that had never run before.**

| Test | Verdict |
|---|---|
| `CustomConverters.GroupBy_converted_enum` | **The first non-composed `GroupBy` this provider has ever been asked to carry.** `context.Set<Entity>().GroupBy(e => e.SomeEnum).ToList()` returns EF's `GroupBySingleQueryingEnumerable+InternalGrouping<,>`, which the deserialization allowlist refuses. It could not have surfaced before: `NorthwindGroupByQuery` is **Tier A**, and InMemory refuses a non-composed `GroupBy` outright — which is precisely the override J2 deleted. Not a regression; a gap that Tier A was standing in front of. |

**One needs one more probe, and the theorising stops here.**

| Test | What is known |
|---|---|
| `CustomConverters.Value_conversion_is_appropriately_used_for_join_condition` | The test joins on **anonymous types**. The tree that reaches SQLite has, for *both* key selectors, `(object)new ValueTuple<int?, bool, int>(…)` — a **boxed** tuple, which relational translation refuses. So the anonymous key was correctly re-carried as a `ValueTuple` and then boxed. **The boxing is evidently not ours**: neither `TransparentIdentifierRewriter` nor `ProjectionRewriter` contains a single `Expression.Convert`, and `TupleCarrier` contains no `typeof(object)`. Where it comes from is unresolved. **The probe is the standing one** — print the boundary verdict and the shipped tree in `QuerySplitter.Split` and compare it with the raw captured tree, which answers "ours or already in the input" in one filtered run. |

- [ ] **J8. Close `GroupBy_converted_enum`.** Designed 2026-08-17, not implemented.
      The server returns `GroupBySingleQueryingEnumerable+InternalGrouping<,>` and the allowlist
      refuses it — correctly, since it is an EF internal type. **Three answers, and the third is
      the one to try first:**

      | # | Answer | Note |
      |---|---|---|
      | a | Carry `IGrouping<K,V>` as a wire shape | The most work, and it puts an EF-shaped concept in the protocol. |
      | b | Refuse at the boundary with a sentence naming the reason | Honest, cheap, and leaves the test red — a worse answer than (c) if (c) works. |
      | c | **Cut below the `GroupBy` and let the client group** | The rows are shippable; only the *grouping* is not. The client already applies a residual, and grouping in memory over shipped rows is exactly what a residual is for. |

      **The trap in (c), and why it needs care rather than a one-liner:** the refusal must apply
      only when the **final result element** is a grouping. Marking `IGrouping<,>` non-shippable
      per *node* would cut every aggregate `GroupBy` too — the ones that must stay on the server —
      and those are a large, currently-green family. The check belongs on the query root, not in
      `ServerBoundaryAnalyzer`'s per-node verdict. Measure before believing either way.

- [~] **J9. A query constant now travels as its provider value — 1 of 2 closed.** `<this commit>`
      `Total tests: 22672, Passed: 22487, Failed: 14, Skipped: 171` (`j9`): **1 fixed, 0 broken**.
      ADR-012 carries the dated amendment the user approved, and
      `ValueMapping.ModelConverterValueMapper` is the mapper it permits — built inside
      `DynamicValueMapper` from the model it was given, so **symmetry is structural**: each half
      derives it from its own model and it cannot be present on one side only.

      **Closed:** `Can_query_and_update_owned_entity_with_value_converter`. The inbound face —
      `class Key(string id)`, no parameterless constructor — now arrives as the string the model
      always said it was.

      **Still open, and it is a NEW failure rather than the old one:**
      `Can_insert_and_read_back_with_enumerable_class_key_and_optional_dependents` no longer throws
      `NotImplementedException` from `GetEnumerator()`. It now throws

      ```
      InvalidCastException : Unable to cast object of type
        'EnumerableClassKey[…InfoCarrierFixture]' to type 'IntClassKey[…InfoCarrierFixture]'
      ```

      **The mapper is selecting the wrong converter.** `_byClrType` is keyed by
      `converter.ModelClrType`, and something makes an `EnumerableClassKey` match `IntClassKey`'s
      entry. The likely cause — **unverified, do not act on it without checking** — is that these EF
      test key types share a base and a converter's `ModelClrType` is the base rather than the leaf,
      so one dictionary entry answers for several types. C53 is the precedent to read first: it is
      the same shape one level up (*"a member declared on a base class the model never names"*), and
      its rule was **base classes only, never a category**.

      **A guard was tried and measured INERT, which is itself the finding.** Declining when
      `!converter.ModelClrType.IsInstanceOfType(value)` measured **0 fixed, 0 broken, REASONS
      unchanged** — so `IsInstanceOfType` *passes*: the value genuinely is an instance of the
      converter's model type, and the `InvalidCastException` is raised **inside**
      `ConvertToProvider` by the converter's own body. The guard was reverted rather than kept:
      inert code carrying an unverified explanation is worse than no code.

      **So the base-type suspicion is half right and the conclusion drawn from it was wrong.** The
      declared type and the converter agree; what disagrees is the converter's *internal* cast. That
      points at a converter declared for one key type whose body targets another — read EF's
      `KeysWithConvertersFixtureBase` configuration for these two types before touching the mapper
      again.

      **CORRECTED again after reading EF's configuration.** `EnumerableClassKey.Converter` is
      `ValueConverter<EnumerableClassKey, int>` and `IntClassKey` has its own — the two are distinct
      dictionary keys, and `EnumerableClassKey` does not derive from `IntClassKey`. So the mapper is
      **not** selecting the wrong converter, which is why the guard was inert.

      **The likely reading now: the mapper worked, and the test simply got further.** Its body is
      `RunQueries`, which runs many queries; the original `NotImplementedException` came from the
      first. With that closed, a later one fails for an unrelated reason. That reframes this from "my
      mapper is wrong" to "one more defect exists behind it", and it is a **single test** — much
      lower value than J12's 1787.

      **Next probe:** print every `(ModelClrType → ProviderClrType)` pair the constructor records for
      this fixture's model, and the `declaredType` each lookup is made with. One filtered run.
      If it is the base-type collision, the fix is to key on the **exact** type and require
      `converter.ModelClrType == declaredType` rather than an assignability match.
      Closes both `KeysWithConverters` failures, which are one defect with two faces (above).

      **The natural mechanism already exists and the contract forbids using it.** ADR-012's
      `IInfoCarrierValueMapper` maps "a CLR type the wire cannot walk" to a primitive and back,
      in both directions, on both halves — precisely what these two constants need. But ADR-012 is
      **LOCKED**, and states the contract *"in terms of the CLR type alone: neither side may
      consult a type mapping to decide"*. Deriving mappers from the model's value converters
      consults exactly that.

      **The distinction that might justify an amendment, and it is the same one B12/C80 and J5
      already rest on.** ADR-012's clause was written against B23, where sending a scalar through
      EF's core `ValueConverterSelector` inside `PrimitiveCoercion.Coerce` cost **381** — a
      *store* type mapping, which the two providers genuinely compute differently. A converter
      configured in `OnModelCreating` is not that: it is **shared model configuration**, identical
      on both sides by construction, and J5's seam exists because *"where a key shape is decided by
      the caller's own model configuration rather than by the store, the client has to reach the
      same answer as the server"*. If that reading holds, the amendment is narrow: *a converter the
      model declares is not a type mapping.*

      **Do not proceed without deciding that explicitly.** CLAUDE.md: reversing or amending a
      LOCKED ADR requires a dated supersession edit in `decisions.md`, never a code change that
      quietly contradicts it. Options, if it is taken: (i) derive mappers from the model's
      converters and register them automatically on both halves; (ii) leave ADR-012 alone and give
      the *constant* path its own model lookup, which duplicates the idea in a second place;
      (iii) do nothing and classify both tests as permanent. **(i) is the recommendation** — it
      reuses the seam, needs no wire change, and keeps one mechanism rather than two.
- [x] **J10. The join key was never boxed, and the fix is one line.** `<this commit>`
      `Total tests: 22456, Passed: 22229, Failed: 17, Skipped: 210` (`j10`): **1 fixed, 0 broken**.

      **Every theory in the entry this replaces was wrong, and the probe said so in four lines.**
      The tree is clean at *all four* stages — captured at `Split`, after `ReCarryInternalTypes`,
      after `ProjectionRewriter`, and rebound on the server — each printing
      `new ValueTuple\`3(Item1 = …, Item2 = …, Item3 = …)` with no `Convert` anywhere. **The
      `(object)` in EF's message is EF's own rendering of a key it could not decompose**, not
      something anyone boxed. Reading it as boxing cost this entry two rounds of confident
      reasoning about where the cast came from.

      **The real cause, proved with no InfoCarrier in the probe at all.** A plain SQLite context,
      the same join written three ways:

      | Join key shape | EF's own SQLite provider |
      |---|---|
      | anonymous type | **TRANSLATED** |
      | `ValueTuple<int?, bool, int>` (with `NewExpression.Members` supplied) | **refused** — *"could not be translated"* |
      | `Tuple<int?, bool, int>` | **TRANSLATED** |

      So the limitation is EF's, and ours was only in picking the shape that trips it: the re-carry
      that keeps a join *on* the server was simultaneously making it untranslatable. Supplying
      `Members` — the fix that was worth 214 tests elsewhere — is **not** enough for a join key.

      The change adds the join key's type to `_referenceTyped`, the mechanism that already exists
      for a carrier that must stay a reference type. Deliberately **not** applied to a `GroupBy`
      key: nothing has been measured there.

### J5–J6 — D3 answer (c), in two steps

- [x] **J5. The document seam, and the package reference leaves with it.** `<this commit>`
      `Total tests: 22456, Passed: 22228, Failed: 18, Skipped: 210` (`j5c`) against
      `22453 / 22225 / 18 / 210` (`j4`): **FIXED and BROKEN both empty, REASONS unchanged**, and
      `total` rises by exactly the three new pin tests, all passing.
      **`InfoCarrier.Core.csproj` no longer references `Microsoft.EntityFrameworkCore.Relational`.**

      `Metadata.IInfoCarrierDocumentMapping` asks the one question — *is this type stored inside
      one document belonging to something else?* — plus the two things that vary with the answer:
      which annotations can change it, and what the store calls the synthesized ordinal. The
      default, `AnnotationDocumentMapping`, reads the relational annotation **by string name**
      (D3 answer (c), string-default variant, chosen 2026-08-16).

      **The three D3 pins were checked positively, not inferred from a stable count**, because
      B12's symptom was wrong data with no exception: `JsonQuerySqlite` **393 passed / 0 failed**,
      `JsonOwnedCollectionUpdate` **5 / 0**, `ComplexCollectionJsonUpdate` **18 / 0**, and
      `The_two_models_agree_on_the_key_of_every_JSON_mapped_owned_collection` passed.

      **Four things this cost, and three of them were only findable by running it:**

      1. **`GetContainerColumnName()` is not an annotation read — it is a *walk*.** It falls back
         through the ownership chain for an entity type and through the declaring type for a
         complex type, so a nested owned type inherits its container. Reading the annotation on
         the type alone answers `null` for every nested type, which is B12 one level down.
      2. **`RelationalKeyDiscoveryConvention.SynthesizedOrdinalPropertyName` had to go too.** It is
         a `const`, so it is inlined at runtime — but naming the type still needs the assembly at
         compile time. It is now on the seam, which is where it belongs: CLAUDE.md already records
         that Cosmos recognises the ordinal by the property's *shape* rather than by this name.
      3. **EF refuses a provider's own service through `EntityFrameworkServicesBuilder`.** Its
         `TryAdd` validates against EF's list of service contracts, and routing this one there put
         *"The database provider attempted to register an implementation of the
         'IInfoCarrierDocumentMapping' service"* on **21,991** tests in a single run. It registers
         on the plain collection instead, exactly as ADR-012's value mappers do.
      4. **A `const` became a property, and `ApiConsistencyTest` caught it.**
         `InfoCarrierKeyDiscoveryConvention.SynthesizedOrdinalPropertyName` is now `virtual`;
         without that, `Public_inheritable_apis_should_be_virtual` failed and was the entire
         difference between 19 and 18.

      **`DocumentMappingPinTest` is the price of naming the string, and it was watched failing.**
      Two assertions compare the strings to EF's constants; the third walks a real `ToJson()` model
      and compares `FindContainerName` with `GetContainerColumnName()` for **every** entity and
      complex type. Deliberately removing the ownership-chain fallback made it fail with
      `Expected: "Items", Actual: null` — D1's rule, that the assertion you never watched fail is
      the one to distrust. The test asserts non-vacuity directly too: one type outside a container,
      two inside, and the nested one reachable only through the walk.
      A provider-neutral *"is this type mapped to one document?"* question, answered by the
      relational implementation behind it. `ServerSaveChangesExecutor.IssuedAtSave` is the shape to
      copy: it asks the backend a capability question rather than testing for a store family.
      **A green build is not evidence here.** B12's symptom was wrong data with no exception, so
      the pins are `JsonQuery` at 0 failures, `JsonOwnedCollectionUpdate` at 5 of 5, and
      `The_two_models_agree_on_the_key_of_every_JSON_mapped_owned_collection`.

- [ ] **J6. Ask the backend what it can evaluate — a second axis, beside the allowlist.**
      **Rescoped 2026-08-17; the earlier wording here said "replacing the fixed boundary allowlist"
      and that would have been a security regression.** `TypeAllowlist` is ADR-008 constraint 2 —
      an RCE control whose own summary describes the alternative as *"a remote-code-execution vector
      the moment a network transport exists"*, and whose safety `security-review.md` §2 calls a
      conjunction. A backend must never widen it. The missing axis is separate and only ever
      **narrows** what is shipped: *can the thing at the other end evaluate this?*
      Four candidate answers, the difficulty of the automatic one, and why nothing is blocked on it
      today are in [`architecture.md`](architecture.md) §6a **D5**. **Design first, as D3 was.**

### Recorded, not scheduled

- **D4's two chained-InfoCarrier defects.** The probe stays out of the suite so the baseline keeps
  meaning "inherited spec tests failing"; it lives outside the repo, and D4 records what it printed.
- **A third store.** Cosmos is the recommended candidate — first-party EF Core 10 provider
  (`src/EFCore.Cosmos` is in the EF tree), an emulator in one container, and 155 test files in
  `EFCore.Cosmos.FunctionalTests` to check our overrides against, which is the method CLAUDE.md
  depends on and which no other candidate offers. MongoDB is cheaper to run and has no EF suite at
  all. Adopting one is its own milestone, and J5/J6 are what make it cheap.
