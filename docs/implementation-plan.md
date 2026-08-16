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
