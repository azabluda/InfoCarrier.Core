# Roadmap

Status: **Every milestone is closed. M5 closed 2026-08-24, last of them; M8 closed and M7's SQL Server half dropped the same day** · Milestone-level plan for the whole project.

This doc is **stable** — it lists milestones, their exit criteria, and their order. It changes
only when scope changes.

The fine-grained checkbox plan for the *current* milestone lives in
[`implementation-plan.md`](implementation-plan.md), which is **rewritten each milestone**.
Do not put per-milestone task detail here; do not put roadmap-level scope there.

Authority: [`infocarrier-core-requirements.md`](../../infocarrier-core-requirements.md) ·
[`decisions.md`](../../decisions.md) · [`research-findings.md`](../../research-findings.md)

---

## Where we are

**Every milestone is closed.** M5 was the last, on 2026-08-24, when the remote half of cancellation
(W6) landed: the server reads the token the transport already handed it, so a caller who abandons
a request stops the query at the store rather than only stopping its own wait.

The query pipeline is complete end to end — capture → serialize → transport → server rebind →
EF execute → client materialization with identity resolution. The **projection split (M2)**,
**SaveChanges (M3)** and **transactions (M4)** are implemented, with the type boundary enforced
rather than hidden by the in-process harness.

**`Total tests: 22662, Passed: 22476, Failed: 9, Skipped: 177`.** All nine are classified, and
`test/known-failures.txt` carries the dated reading for each. What is missing is stated for
consumers in [`limitations.md`](../../../website/docs/limitations.md).

**This section has been wrong before.** It read "M5 is one criterion from done" and quoted a
2026-08-11 measurement for a day after M5 closed, which put the first screen of the stable plan
at odds with its own milestone headers. Re-read it whenever a milestone closes.

---

## Milestones

### M1 — Query pipeline correctness + working signal ✅ *(query work done; N5/N6 doc tail open)*

Clear the mechanical failures masking the real state, and get a CI signal that can gate.

**Exit criteria**
- `QueryParameterExpression` substitution and STJ primitive registration fixed; failure count
  recorded and reduced (expected ≈ 141 → ≈ 350 of 413).
- CI builds the correct solution file and runs a **failure-count ratchet** (below) that fails
  the build when failures increase. ✅ (N1–N4 for the workflow; C39 refreshed the baseline, which
  had gone eight months stale at `111/5215` against an actual `145/22312` and would have failed
  the gate on the failure count while the total quadrupled. **A ratchet whose baseline is not
  maintained is a broken build waiting, not a safety net** — lower it in the same commit as the
  fix that lowers it.)
- `InfoCarrierComplianceTest` (F8) landed and **red on purpose**, publishing the authoritative
  inventory of unimplemented spec bases.
- Doc integrity closed: ADR-008/ADR-009 recorded ✅, subrepo revisions pinned.

### M2 — Projection split (requirements §3) ✅ **complete**

The one genuinely unsolved design problem. **Spec written 2026-08-01:**
[`projection-split.md`](../../projection-split.md), recorded as [ADR-010](../../decisions.md#adr-010-projection-split-boundary-computed-on-the-client-locked-2026-08-01).

Two things changed from the research-findings §8 sketch. The boundary is computed **on the
client**, not the server — the allowlist rejects during deserialization, so the server never gets
an expression to analyze. And it is a *rewrite*, not a cut at "the last `Select` whose element
type the server's model knows": cutting there ships `Customer` entities and then answers
`c.Orders.Count()` as `0` on the client, silently. Projection lambdas are instead split into a
server-side `ValueTuple` projection plus a client-side reassembly — which is also the
minimal-column payload (W1).

> ⚠️ **Re-scoped 2026-08-01 — the tests no longer detect this problem.** Projection tests used
> to fail loudly (`Entity type '<>f__AnonymousType…' not found in the server model`). After
> M1-G4e resolved the server query provider from the query root, they pass — because the
> in-process harness runs client and server in **one AppDomain**, so the server can see
> anonymous types and client-only DTOs and materializes them itself. Over a real transport it
> cannot. The requirement is as unmet as before; only the symptom is gone.
>
> **M2 must therefore restore the failing signal first.** The mechanism already exists in the
> design: ADR-008 constraint 2 mandates an allowlist of *deserializable types* (model entities
> + registered projection types). Enforcing it makes the server reject client-only types, the
> type boundary becomes real in-process, and the §3 tests fail honestly again. The allowlist
> is not only a security control (M5) — it is what makes the boundary testable at all.
>
> ✅ **Done 2026-08-01 — and the problem is far larger than anyone estimated.** With the
> allowlist enforced the suite went **32 → 1,421 failures of 4,247**: 1,197 anonymous or other
> compiler-generated projection types, ~108 client-only DTOs. **~1,305 tests — 31% of the
> suite — are M2-blocked**, against an estimate of ~16 taken from the passing suite. A further
> ~84 store-limitation overrides now trip the boundary before reaching the translation failure
> they assert, and will need re-checking once the split lands.
>
> That ratio is the point worth remembering: the harness was concealing roughly eighty times
> more missing functionality than the visible failures suggested.

> ✅ **Implemented 2026-08-02 — 1,421 → 91 failures of 4,296.** The split is in
> `src/InfoCarrier.Core/Query/`: `WireTypeCollector` → `ServerBoundaryAnalyzer` →
> `ProjectionRewriter` → `QuerySplitter`, applied by `QueryExecutor` and consumed by nothing on
> the server, which is unchanged as the design required.
>
> Three things the spec did not anticipate, all found by measurement:
>
> - **The cut broke `GroupBy`.** Separating a `GroupBy` from the aggregate `Select` that composes
>   it leaves a bare non-composed `GroupBy` no provider can translate — 136 failures the split
>   itself created. The rewrite keeps them together.
> - **Client evaluation needed a rule of its own.** A residual operator forced by the type
>   boundary is legitimate; one whose lambda calls a method the server cannot run is a
>   translation failure, because answering it locally means fetching the whole table. Getting the
>   line in the right place took three attempts, costing 235 and then 69 tests respectively when
>   drawn too widely.
> - **Transparent identifiers are the remaining ceiling** — now specified in
>   [`transparent-identifiers.md`](../../transparent-identifiers.md) / [ADR-011](../../decisions.md#adr-011-transparent-identifiers-are-re-carried-not-reassembled-locked-2026-08-02). `from … join … select new { a, b }`
>   makes an anonymous type EF handles internally and we must treat as a boundary, so everything
>   downstream lands on the client. Deferring the reassembly and threading tuple slots through
>   downstream operators is the next real gain, and is the "operator pushdown" the spec deferred.

**Exit criteria**
- **Result wire format** — spec written: [`result-wire-format.md`](../../result-wire-format.md).
  1,047 of 1,440 failures (73%). Do this first: it is independent of the type-boundary work,
  it unblocks SaveChanges, and until it lands most other failures are masked behind it.
- ✅ Server-side type allowlist enforced, so client-only types cannot be materialized
  server-side even in-process. Projection tests fail again before they are fixed.
- ✅ Design spec + ADR — [`projection-split.md`](../../projection-split.md), [ADR-010](../../decisions.md#adr-010-projection-split-boundary-computed-on-the-client-locked-2026-08-01).
- ✅ Boundary detection **in the client**; client applies the residual projection.
  `ServerQueryExecutor` unchanged, as the design's own test required.
- ✅ Minimal-column payload (wire-protocol W1) — the same mechanism as the boundary rewrite, not
  a separate pass: `Select(a => new { a.Name })` ships one `string` per row, not an entity.
- `NorthwindSelectQueryTestBase` and `NorthwindJoinQueryTestBase` — both adopted; residual
  failures tracked in [`implementation-plan.md`](implementation-plan.md).
- The ~84 store-limitation overrides that now trip the type boundary before reaching the
  translation failure they assert are re-checked: each returns to asserting its original failure,
  or is deleted.

### M3 — SQLite backend + SaveChanges ✅ **complete**

**Exit criteria**
- `SqliteInfoCarrierBackendTestStore` (ADR-009 Tier B) — holds one connection open for the
  store lifetime.
- S1 client change-tracker capture; S2 server replay + store-generated values returned by
  correlation id (research-findings §9).
- **Many-to-many from day one** (ADR-004) — v1's worst failure mode.
- SaveChanges/change-tracking spec bases green on Tiers A and B.
- **Every InMemory-limitation override re-tested against Tier B and deleted where it passes.**
  These assert a *store* limitation, so on a relational backend most of them assert something
  that is no longer true — a passing query would fail the override's "this throws" assertion,
  which is the signal that it must go. They are inventoried below; carrying one over silently
  would turn a store limitation into permanent hidden coverage loss, which is precisely v1's
  stated failure mode.

**Inventory — overrides that assert an InMemory limitation** (as of 2026-08-01, M1). Each
class doc carries the same instruction; this is the index.

| Class | Overrides | Limitation |
|---|---|---|
| `NorthwindGroupByQueryInfoCarrierTest` | 13 | non-composed `GroupBy` |
| `NorthwindMiscellaneousQueryInfoCarrierTest` | 12 | throws on empty sequence; `ElementAtOrDefault` subquery; composite-key entity equality |
| `NorthwindAggregateOperatorsQueryInfoCarrierTest` | 8 | aggregate over empty subquery; local `IEnumerable` in `Contains` |
| `NorthwindJoinQueryInfoCarrierTest` | 6 | `RightJoin`; local-collection join; client-eval joins (EF #21200) |
| `NorthwindKeylessEntitiesQueryInfoCarrierTest` | 3 | no database views; no `Include` from a keyless type |
| `NorthwindSetOperationsQueryInfoCarrierTest` | 2 | set operation after client-evaluated projection (EF #16243) |
| `NorthwindIncludeQueryInfoCarrierTest` + `NoTracking` / `String` / `EFProperty` variants | 4 (1 each) | `RightJoin` |

Total **48** tests. Each mirrors EF Core's own `*InMemoryTest` override one for one — the
objective criterion for "store limitation, not our bug".

### M4 — Transactions ✅ **complete 2026-08-03** (plan T1–T2)

Untestable before M3: EF InMemory raises `TransactionIgnoredWarning` with
`WarningBehavior.Throw`, so Tier B is a prerequisite, not a nicety.

**Exit criteria**
- S3 begin/commit/rollback + savepoints; transaction-scope token across stateless transports
  (wire-protocol W3).
- Client disposal/rollback cleans up server-side (requirements §2.9).

### M5 — Wire hardening — **CLOSED 2026-08-24**

**No network transport may ship before this milestone completes.**

ADR-008 constraint 2 mandates strict allowlists on by default. Combined with `InvocationNode`,
an unconstrained resolver is a remote-code-execution vector in a product whose entire purpose is
accepting serialized expression trees from remote clients.

**Both allowlists are now closed (type 2026-08-01, method 2026-08-10).** A payload can no
longer name an arbitrary type for the deserializer to construct (`TypeAllowlist`), and as of
plan item C30 it can no longer name an arbitrary *method* either: `ResolveMethod` admits a
**public** method on an allowed declaring type, plus two named non-public markers EF's own query
rewrites produce (`NotQuiteInclude`, `ExecuteUpdate`).

That second list is short because it was measured, not guessed — C25 established that a plain
"public only" rule costs 154 → 697, because ADR-006 captures the tree *after* EF has rewritten
its own public API into those markers. C30's policy was then designed from an inventory of every
method the deserializer actually binds across a full run: 362 methods over 84 declaring types,
distributed almost exactly as this ADR words it.

**All three allowlists are now closed** (type 2026-08-01, method 2026-08-10, node kind 2026-08-10).
The third split into two questions with different answers, recorded in plan item C36. The **node
kind** was never wire-supplied at all: `ExpressionNode.Kind` is `[JsonIgnore]` and answered by each
record's CLR type, and what the wire carries is System.Text.Json's `$kind` discriminator over the
fifteen registered `[JsonDerivedType]`s, which refuses anything else before a node exists — so that
half was closed by construction, and C36 proves it rather than changing it. The **operator** name
inside a binary, unary or type-binary node was the part genuinely open: a free string parsed by
`Enum.TryParse`, which admits every `ExpressionType` name — `Assign` and `Throw` included — plus
bare numbers and comma lists. C36 replaced it with a per-node-kind allowlist of the pure subset.

**Payload limits are in** (C37). The depth half was already there (`ExpressionJsonContext`'s
`MaxDepth = 256`); the size half is `InfoCarrierPayloadLimits`, default-on at 64 MiB **in the
request direction only**. That asymmetry is measured, not stylistic: one bound applied to both
directions broke four Northwind spec tests on results of 560 MB and 111 MB — cross-joins the
caller had asked for — while no request came close. The threat this milestone names is
server-inbound; capping what a client gets back is a page-size policy, and a different question.

**One thing is left in M5: the remote half of cancellation (W6).** The envelope (C45), exception
fidelity (C46) and the security review (C48) all closed on 2026-08-10, and **C66 closed W6's
cooperative half**: the caller's `CancellationToken` reaches all nine server operations, and six
tests hold it there. Nothing in the product needed building — every layer already threaded the
token — but nothing had ever asserted it, because the *harness* transport dropped it and the whole
suite therefore ran with `CancellationToken.None`.

**What remains is a remote cancel signal**, and it is deliberately not built: abandoning a request
already dispatched needs a `Cancel` operation keyed by `InfoCarrierEnvelope.CorrelationId`, which
no one writes or reads today, and it cannot be exercised before a network transport exists (M8) —
over an in-process transport the token *is* the signal. `security-review.md` §6 states the
constraints it must meet when it lands.

**Exit criteria**
- Allowlists for node kinds, resolvable types, and invocable methods — **default deny**,
  opt-in registration for model entities and declared projection types.
  *Types* ✅ (`TypeAllowlist`, 2026-08-01) · *methods* ✅ (C30, 2026-08-10) · *node kinds* ✅ (C36, 2026-08-10).
- Payload depth/size limits (v1 needed a 10 MB stack for >1 MB payloads).
  *Depth* ✅ (`ExpressionJsonContext.MaxDepth`) · *size* ✅ (C37, 2026-08-10).
- `InfoCarrierEnvelope` + `ProtocolVersion` actually exercised by tests. ✅ (C45, 2026-08-10.
  The store was half the problem: the product had a `TransportInfoCarrierClient` that wrapped
  every request and **nothing that unwrapped one** — the only dispatcher was inline in a smoke
  test and handled one of nine operations. `InfoCarrierEnvelopeServer` is the missing half, and
  all 22321 tests now cross a real envelope with the version checked before dispatch.)
- Exception fidelity across the wire (W5) ✅ (C46, 2026-08-10) and cancellation (W6) ✅
  **complete 2026-08-24.** The cooperative half landed in C66; the remote half landed in Phase P,
  and it needed no new mechanism. The token already travelled from `MapInfoCarrier` to
  `ServerQueryExecutor` and was dropped at the last step. **Two decisions were taken with it,
  2026-08-24, by the owner:**
  **(1) No `Cancel` operation goes on the wire.** A tenth operation carrying a request id needs a
  server-side registry of live requests, and that registry has the shape this roadmap already
  records as a defect under M8: unbounded, process-local, keyed by something a vanished client
  never mentions again. The transport's own disconnect signal costs nothing, needs no protocol
  version, and cannot leak, because ASP.NET Core owns its lifetime. Build a wire message only if a
  transport appears that cannot report a lost client, and record the reason before building it.
  **(2) What such a transport must do is deliberately left open.** Both transports that ship can
  report a lost client: HTTP has `RequestAborted`, and the in-process transport shares the caller's
  token directly. A rule written now would be a guess about a transport nobody has designed.

  W5 is `InfoCarrierFault`: a failure travels as data and is raised again on the client, keeping
  the type, the message and the inner chain — which is what EF's spec tests assert on, so the
  whole suite verifies it. Two limits are real and stated rather than worked around: a store's own
  exception type (`SqliteException`) is not reconstructible by a client that has no reason to
  reference the backend's driver, and `DbUpdateException.Entries` are the *server's* update
  entries, so the client is given its own. Both were previously hidden by client and server
  sharing a process.
- Security review of the deserialization path. ✅ (C48, 2026-08-10 —
  [`security-review.md`](../../security-review.md).)

  The material finding is that the type allowlist's safety is a **conjunction, not a single
  check**. `System.Type` is admitted and every enum is admitted, so a payload can legitimately
  call `Type.GetType("System.Diagnostics.Process")` and hold, at run time on the server, a type
  the allowlist never saw. That is not a hole only because every reflection entry point that would
  turn it into a call is blocked by a *different* clause — `InvokeMember` needs a `Binder`,
  `MethodInfo.Invoke` needs an unadmitted declaring type, `Activator` is not admitted. Adding any
  one of those to the allowlist, none of which looks dangerous alone, breaks it. Asserted by
  `DeserializationHardeningTest` rather than left in prose.

### M6 — Coverage expansion — ✅ **DONE 2026-08-11** (plan Phase C)

Work the M1 compliance inventory down. Every spec base ends up either implemented or in
`IgnoredTestBases` **with a stated reason** — nothing silently forgotten.

**Scope confirmed 2026-08-09: all 41 remaining bases are in.** `Query.Associations.*` (27) and
`BulkUpdates.*` (5) had been held back pending this call; they are **Tier B**, decided by the fact
that `EFCore.InMemory.FunctionalTests` ships neither family at all. The per-base tier verdict is
plan item C0 and is not re-derived per batch.

**State 2026-08-10: 41 adopted down to 1.** `AdHocJsonQueryTestBase` was the only base the
compliance test still reported. **Closed 2026-08-11 (plan C82): it is adopted, 61 of 61, and
`InfoCarrierComplianceTest.All_test_bases_must_be_implemented` is green.**

**Exit criteria**
- Relationships, owned types, table splitting, TPH/TPT inheritance (requirements §2.7). ✅
- Compliance inventory fully classified. ✅
- `InfoCarrierComplianceTest.All_test_bases_must_be_implemented` green, or every remaining base in
  `IgnoredTestBases` with its reason in the plan.

**Only the first of those two routes is open, and that is a scope fact rather than a
preference.** `IgnoredTestBases` is for bases *conceptually inapplicable* to a remoting provider —
that is the distinction its own doc comment draws, and plan item C9 applied it when it kept the
spatial bases reported rather than ignored, on the ground that "not built yet" does not qualify.
`AdHocJsonQuery` is also merely not built yet. **So M6 closes by adopting it, and by nothing
else.**

~~**And that adoption sits behind [B12](implementation-plan.md)** … 626 + 322 lines of relational
mirror plus seven abstract seeds … **M6 is therefore blocked on a decision, not on work.**~~

**Both halves of that were true and neither survived contact.** B12 *was* the blocker, it was
decided on 2026-08-11, and taking it cost **two files and one registration line** — the client
model already carried the annotation and the price quoted in C78 was 36 fixed, 0 broken (C80). The
626 + 322 lines were the cost of **not referencing**
`Microsoft.EntityFrameworkCore.Relational.Specification.Tests`; ADR-013 now does, so the base is
inherited rather than mirrored, and the seven abstract seeds are ten raw-SQL `INSERT`s copied
verbatim and executed against the backend (C82).

**What the milestone actually cost, for the next time a price like that is quoted:** C80 + C81 +
C82 + C83, four steps, ~40 fixed and one new ADR. **A price is only ever a price for the route
that was in mind at the time.**

### M7 — SQL Server (Tier C) + spatial — **SPATIAL DONE; THE SQL SERVER HALF IS DROPPED 2026-08-24**

**The SQL Server tier is dropped, by the owner's decision, 2026-08-24.** It does not ship in the
10.x line and it is not deferred to a later one: it is withdrawn.

**What is dropped is a TEST TIER, not support for a store.** The server side of this product is an
ordinary EF Core application, so it runs against whatever provider the server references, SQL Server
included. Requirements §5 names SQL Server among the server backends and that is unaffected. What
was planned here and is now not planned is a *third* backend for this repository's own suite, beside
InMemory (Tier A) and SQLite (Tier B), with a Docker store and a nightly job. ADR-009's tier model
keeps its two tiers; Tier C is simply never built.

**What the drop costs, separated properly. A first version of this paragraph said all four features
had "no evidence either way", and that was too broad for three of them.** The question a remoting
provider has to ask is not "does the store do this" but "does anything on the wire depend on it",
and for three of the four the answer is a mechanism that is tested.

| Feature | The mechanism this provider owns | Evidence |
|---|---|---|
| Computed columns | A value the server generates and returns. In the model that is `ValueGenerated.OnAddOrUpdate`; `HasComputedColumnSql` is only how a store produces it. | `StoreGeneratedTestBase` is adopted and green, and it parameterizes `OnAddOrUpdate` heavily. No `StoreGenerated` entry in `test/known-failures.txt`. |
| Sequences / hi-lo | The same: a key the server issues, carried back and matched to the client's placeholder. | The store-generated key path is the most heavily exercised write path here (C76 and the correlation-id mechanism). |
| `rowversion` | A store-generated concurrency token, compared server-side against original values the wire carries. | `ConcurrencyTokenTest`, `OptimisticConcurrencyInfoCarrierTest` and `ConcurrencyDetectorInfoCarrierTest`: 67 pass, 0 fail. |
| **TPT / TPC** | **This one is different, and it is the real gap, though narrower than this row used to claim.** It changes the *model*, and this provider builds a model on the client as well as on the server. | **CORRECTED 2026-08-25.** This read "None. No TPT or TPC test class exists here at any tier" and that was wrong: `Sqlite/OptimisticConcurrencyInfoCarrierTest` builds `FanTpt` with `UseTptMappingStrategy()` and `FanTpc` with `UseTpcMappingStrategy()`, and runs 33 passed of 45 with 12 skipped, so both strategies cross the wire and are green. What is missing is query-translation breadth: EF ships **17** TPT/TPC bases in `EFCore.Relational.Specification.Tests` and none is adopted. Issue #56 carries the list and why the compliance test never flagged them. The adopted inheritance base, `InheritanceQueryInfoCarrierTest`, is still TPH. |

So the honest statement is: **three of the four rest on mechanisms with direct green coverage, and
what is untested about them is the store's own behaviour, which is an ordinary EF Core concern on
the server and never crosses this wire.** TPT and TPC are the one open question, because a mapping
strategy is a model concern and the client has a model too.

**No user-facing document may claim any of the four works**, which is unchanged: green coverage of
a mechanism is not a claim about a store this suite never runs.

**The direction the owner would rather spend it on is a non-relational backend**, recorded in the
deferred table below. That is future scope and nothing is committed.

~~**Exit criteria**~~
- ~~Docker SQL Server backend store; nightly CI job (ADR-009 Tier C).~~ **DROPPED 2026-08-24.**
- ~~`rowversion` concurrency, computed columns, sequences, TPT/TPC.~~ **DROPPED 2026-08-24**, and
  untested by anything that remains.
- ~~NetTopologySuite with **Z/M ordinates preserved** (requirements §2.8) — v1 lost them.~~
  ✅ **Done 2026-08-10, in M6. Do not plan this again.**

**The spatial criterion closed early, and it needed no SQL Server.** It arrived in three pieces,
each landed and measured on its own because plan item C9 had attempted two of them together and
aborted the test host:

| Piece | Plan | What it is |
|---|---|---|
| Type mapping | C15 | The NetTopologySuite branch every provider carries, in `InfoCarrierTypeMappingSource`. Worth 19 tests by itself: the client — not SpatiaLite — was what could not map a `Point`. |
| Value-mapper seam | C17, [ADR-012](../../decisions.md#adr-012-a-value-mapper-seam-for-clr-types-the-wire-cannot-walk-locked-2026-08-09) | Product API. A geometry travels as one wire primitive instead of being walked reflectively, which is what overflowed the stack in C9. |
| WKT mapper | C18 | **Test-side**, so `InfoCarrier.Core` still does not reference NetTopologySuite — v1's arrangement, kept. |

**Z and M survive because the format is WKT, not v1's GeoJSON**, which carries neither. That is
the defect requirements §2.8 records, and Q7 below is the entry that named WKT as the answer.
`GeometryWireFormatTest` asserts the ordinates and the SRID directly: the two spatial spec suites
model XY at SRID 0 and would pass all 173 tests against a mapper that silently dropped them.

Both spatial bases are adopted on **Tier A**, 169 of 173.

### M8 — Productization — **CLOSED 2026-08-24**

**Server-held transaction lifetime — OPEN, and it is the library's, not the sample's.**
Recorded 2026-08-16 after the Blazor sample made it reachable. `InProcessInfoCarrierServer`
holds an open transaction — a DI scope, a `DbContext` and a store connection — in an unbounded
`ConcurrentDictionary` keyed by the wire-protocol W3 token, with **no timeout, no eviction and no
binding of a token to the caller who created it**. A client that opens a transaction and then
vanishes (tab closed, network dropped, process crashed) pins all three until the server process
exits. The client's own `DisposeAsync` rolls back and covers every ordinary path, including
exceptions; it cannot cover a client that never runs again.

**Measured, not assumed:** with an abandoned transaction that had already written, a second client
still *read* correctly (isolation holds — it saw the pre-write value), but its **write blocked
until the test's own timeout**. Once such a transaction has written, it holds SQLite's write lock,
so one abandoned browser tab wedges writes for the whole server. Full detail in
[`security-review.md`](../../security-review.md) §8.

**DEPRIORITIZED 2026-08-16, and the reason is that the pattern it protects is not the recommended
one.** Holding a store transaction open across client round trips is pessimistic locking over a
network, which is discouraged for exactly the failure this gap exhibits: the lock's lifetime becomes
the client's, and clients vanish. The recommended shape is **optimistic concurrency** — a
concurrency token, a short server-side transaction inside a single `SaveChanges`, and a
`DbUpdateConcurrencyException` the caller resolves and retries.

**Optimistic concurrency is supported, and that was checked rather than assumed** before
deprioritizing:

- `OptimisticConcurrencyInfoCarrierTest`, `ConcurrencyDetectorInfoCarrierTest` and
  `ConcurrencyTokenTest` are adopted and **67 pass, 0 fail**; none appears among the suite's 13
  known failures.
- The mechanism is genuinely end-to-end: `ChangeEntry.SerializedOriginalValues` carries the
  original values the store compares against, and `InfoCarrierDatabase` rebuilds a
  `DbUpdateConcurrencyException` **with its conflicting entries** on the client.
- The 11 skipped `..._can_be_resolved_with_...` tests are **EF's own SQLite skips**
  (`Optimistic Offline Lock #2195`), mirrored one for one from
  `OptimisticConcurrencySqliteTestBase` — verified against `subrepos/efcore`, not inferred from
  our own comment. Any EF application on SQLite has the same gap, and M7's SQL Server tier would
  run them.

**The risk does not vanish, it shrinks.** A transaction is still the right tool for atomicity
across several `SaveChanges` calls — the sample's Transfer page uses one for that, not for locking
— and such a transaction still leaks if the client dies mid-flight. The exposure window is just
seconds rather than unbounded, which is what makes this a hardening item rather than a blocker.

Three separable pieces, and only the first can be done outside the library:

| Piece | Scope | Note |
|---|---|---|
| Idle timeout that rolls back and disposes | **sample can demonstrate it; the library should own it** | Doable today as a decorator over `IInfoCarrierServer`: the interface exposes the token from `BeginTransactionAsync` and accepts it in `RollbackTransactionAsync`, and the request records expose `TransactionId` to refresh liveness. But every consumer of `InProcessInfoCarrierServer` inherits the unbounded registry, so a sample-only fix leaves the defect shipped. |
| Binding a token to its creator | **library + protocol** | The envelope carries no caller identity, so today any caller who knows a token can join the transaction. Nothing outside the library can enforce this. |
| Surviving more than one server instance | **library / deployment** | The registry is process-local, so a token only resolves on the instance that created it — horizontal scaling needs affinity or a shared registry. The sample is single-instance and does not hit this. |

**Exit criteria**
- ~~HTTP transport binding~~ **DONE (M8-3, M8-4, promoted to the products in Phase K)** — only
  in-process existed when this was written. `HttpInfoCarrierTransport` is the client half, in
  `InfoCarrier.Core`; `MapInfoCarrier` is the server half, in `InfoCarrier.Core.AspNetCore`.
  Both ship in the two packages, so nothing is left in `samples/`. **The Phase I note that said
  this criterion "remains formally open" was true when it was written and is stale:** it
  described the two files while they still lived beside the sample.
- ~~gRPC transport binding~~ and ~~streaming results as `IAsyncEnumerable<T>`~~ **OUT OF SCOPE FOR
  v10, 2026-08-23, by the owner's decision.** Neither ships in the 10.x line. `IInfoCarrierTransport`
  is one method, so a gRPC binding stays a consumer-side class; the server continues to buffer a
  result set into an `ArrayList` and a consumer pages large results. The requirement they came from
  (requirements §4.4, wire-protocol W4) is deferred past this generation, not withdrawn.
  **Consequence for documentation:** neither may be named as a plan in any user-facing document
  (`docs/doc-style.md` rule 6), and neither is the reason the version says `-preview`.
- ~~Compiled-query cache keyed by canonical serialization (ADR-008 constraint 6, Q5)~~ **OUT OF
  SCOPE FOR v10, 2026-08-24, by the owner's decision.** It does not ship in the 10.x line.
  **It was never a correctness item, and that is why it can move.** The client already avoids
  repeating EF's compile work: EF's own `ICompiledQueryCache` caches what `IDatabase.CompileQuery`
  returns, which is why `InfoCarrierDatabase` resolves the client per execution rather than
  capturing it (`InfoCarrierDatabase.cs:93`). What repeats without this item is the serialize,
  deserialize and translate work on each request, and that repetition gives the same answer every
  time. The suite measures answers, so it cannot see this at all.
  ADR-008 constraint 6 (canonical, deterministic serialization) stays as written and stays
  unexercised; nothing in the code contradicts it. Q10, the server-side delegate cache, moves with
  it, for the same reason and by the same decision.
  **Consequence for documentation:** it may not be named as a plan in any user-facing document
  (`docs/doc-style.md` rule 6). No user-facing document mentions caching today, so nothing needs
  removing.
- **AOT/trimming verification (requirements §4.5): trimming is verified, Native AOT is measured and
  not supported.** §4.5 says "where feasible", and the two halves have different answers.
  **Trimming: done.** The Blazor client publishes with `PublishTrimmed=true` and runs; M8-17 drove
  all three pages against the published output. 88 IL diagnostics are ours and gated by direction
  in `eng/trim-ratchet.sh`. The §4.5 clause about `System.Text.Json` source generators is met: the
  envelope is source-generated (M8-11).
  **Native AOT: measured 2026-08-24, and it is not a supported configuration for v10.** A
  `PublishAot` publish of the console sample reports **155 unique IL diagnostics, 153 ours, of which
  59 are IL3050** (`RequiresDynamicCode`) — a class the trim gate never sees. The provider builds
  and compiles expression trees at run time from the payload, which is the one thing Native AOT
  cannot do. **The publish also did not finish**: it failed at the native link step for a missing
  platform linker on the measuring machine, so nothing proves a native binary would run either way.
  Full account in [`findings.md`](findings.md).
  **The honest annotation shipped instead of a fix.** `UseInfoCarrier` carries
  `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, matching what EF Core puts on
  `DbContext`'s own constructors. It did not lower the 88 and was not expected to once the mechanism
  was understood; it tells a consumer at their call site.
- ~~Sample apps~~ **DONE** (Phases H/I) — a Blazor WebAssembly client and a console client, both
  against a SQLite-backed ASP.NET Core server.
- ~~NuGet packaging, `release.yml`~~ **DONE (M8-19, M8-20, M8-22)** — **`InfoCarrier.Core`** and
  **`InfoCarrier.Core.AspNetCore`** at **`10.0.0-preview.1`**, with MIT metadata, SourceLink, symbol
  packages and a package README. `InfoCarrier.Core.Abstractions` was merged away (M8-22): it
  referenced `Microsoft.EntityFrameworkCore`, so it was never the lightweight contracts package
  that would justify a separate NuGet. The split that remains is the one that costs something —
  the ASP.NET Core endpoint carries a framework reference a client must not. `release.yml` packs on a `v*` tag, runs both gates, checks the tag
  against the packaged version, and attaches the files to a GitHub Release. **Publishing to
  nuget.org is deliberately manual** — a pushed version is immutable, so no API key lives in this
  repository. **Both packages are published at `-preview.1`, and `-preview` stays until the
  public surface is settled**: the suffix is the promise not yet made about
  `IInfoCarrierTransport` and the rest of the shipped API, and a stable `10.0.0` is the commitment
  not to break it. **No user-facing document gives a reason for the suffix** (2026-08-23): the
  version number carries the information, as it does for every EF Core preview.

  **AMENDED 2026-08-18 (N8), and the amendment is about *where the human stands*, not whether
  there is one.** The rule above is unchanged: a pushed NuGet version can be unlisted but never
  withdrawn, so a person decides. What it used to cost was a key on somebody's laptop and a step
  nobody could repeat. `release.yml` now carries a `publish-nuget` job in a **protected
  environment with required reviewers** — it waits for an approval and then pushes, so the key is
  an environment secret rather than a personal one and every push is recorded against its run.
  Two further changes landed with it: **the version is now the git tag** (MinVer; there is no
  version number in any file, so the tag-versus-props check `release.yml` carried is gone — it was
  also broken, see [`versioning.md`](../../versioning.md)), and **every code push publishes to GitHub
  Packages** as an internal feed. Full account in [`versioning.md`](../../versioning.md).

**M8 CLOSED 2026-08-24, and this is the reading that closed it.** Every one of the six exit criteria
above has a resolution rather than a status: three done (HTTP transport, sample apps, NuGet packaging and
`release.yml`), two out of scope for v10 by the owner's decision (gRPC and streaming; the
compiled-query cache), and §4.5 answered in two halves (trimming verified, Native AOT measured and
not supported). **Nothing on that list is waiting on work.**

Two things are open and neither is an M8 exit criterion, which is why reading the list as a set was
worth doing:

- **The server-held transaction lifetime**, described at the top of this section. Deprioritized
  2026-08-16 because the pattern it protects is not the recommended one, and it is a hardening item
  rather than a blocker. It is stated for consumers in `guide/transactions.md`.
- **M5's remaining criterion: the remote cancel signal** (W6). The cooperative half shipped in C66;
  a client that abandons a request cannot tell the server to stop working on it. **This belongs to
  M5 and not to M8**, and the header line of this document has said "M5 has one open criterion"
  since before M8 began.

**The owner closed it on that reading, 2026-08-24.** The two items above are open and neither
belongs to M8: the transaction lifetime is a hardening item that is documented for consumers, and
the remote cancel signal is M5's. Task detail moved to
[`archive/implementation-plan-m8-phases-h-n.md`](archive/implementation-plan-m8-phases-h-n.md) and
is never edited again. [`implementation-plan.md`](implementation-plan.md) now holds M5's remaining
criterion.

**Native AOT is deliberately not gated in CI** (2026-08-24, owner's decision). `ubuntu-latest` ships
clang, so a `PublishAot` job could complete the native link that the local measurement could not,
and the answer would be a real one. It is not worth a job: Native AOT is not a supported
configuration, so nothing would act on the result, and a gate nobody acts on is a gate that goes
stale and then gets believed. The measurement in [`findings.md`](findings.md) is the record, and it
is dated.

### M9 — Provider neutrality and store coverage — **CLOSED 2026-08-17**

**Opened 2026-08-16, closed 2026-08-17.** M8's remaining exit criteria stay open; this milestone
ran beside it, because its subject is orthogonal — M8 is about shipping the provider, M9 is about
what the provider assumes of the store behind it.

**All four exit criteria are met, one of them by restatement** (the capability axis; the reason is
recorded with the criterion below and in [`architecture.md`](../../architecture.md) §6a D5). Task detail
is archived at
[`archive/implementation-plan-m9-phase-j.md`](archive/implementation-plan-m9-phase-j.md).

**Closing state:** `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` — from
`145 / 22312` when the ratchet was last refreshed, and `13 / 22655` at the start of the closing
session. **Every one of the nine is classified**, and stated for consumers in
[`limitations.md`](../../limitations.md) — the first time this project has had a limitations statement
aimed at someone outside it.

**What it delivered beyond the criteria**, because the tier moves exposed real defects rather than
just relocating tests:

| | |
|---|---|
| `ProxyGraphUpdates` | 165 failures → **green** (J11): original foreign-key values never reached the server, so EF's command ordering could not see a dependent releasing a principal |
| `GraphUpdates` | moved to a store that enforces foreign keys, at a cost of 2 |
| 28 silent no-op overrides | **deleted** (J12b) — an empty override counts as a *passing* test, which is worse than a skip |
| Wire boundary | a non-composed `GroupBy` crosses (J8); `Cast`/`OfType` type arguments are checked (J18); `Regex` is admitted with the security argument written down (J20) |
| Parameter substitution | three supersessions of one rule — a null collection (J19) and a mapped scalar (J21) join C88's collections |

**The methodological result is worth more than the count.** Six standing classifications were
found to be wrong when checked against EF's own suites — including one that had read
"SQLite-tier, a store limitation" for two milestones and was ours, one line. *A classification is
not evidence, and age is not evidence.*

**The premise.** This is a non-relational provider whose client is, by construction, never a
relational context (ADR-013). It nevertheless references `Microsoft.EntityFrameworkCore.Relational`
and its whole test suite runs on two stores, one of which is a real relational database. Nothing
here is broken; the question is what would break under a store that is neither InMemory nor SQLite.

**What the opening audit established** (evidence in [`architecture.md`](../../architecture.md) §6a D3):

- The package reference is used for **exactly one question** at four call sites, and is a symptom
  rather than the disease. `InfoCarrierTypeMappingSource`, `InfoCarrierValueGeneratorSelector`,
  `ServerSaveChangesExecutor.IssuedAtSave` and the transaction path are all store-neutral, and
  `IssuedAtSave` is neutral on purpose — it asks the backend's own `IValueGeneratorSelector`
  instead of testing for SQL.
- **Two components genuinely assume a relational store**: `InfoCarrierKeyDiscoveryConvention`'s
  JSON ordinal key and `InfoCarrierDatabase.Expand`'s JSON document scan.
- **A third assumes something stronger and is not recorded anywhere**: `TypeAllowlist` and
  `ServerBoundaryAnalyzer` decide the query boundary from a **fixed list**, never asking the
  backend what it can translate. That is safe against SQLite because SQLite translates a great
  deal. Against a store that translates less, the client ships queries the server cannot run.
- **Chained InfoCarrier — a server whose own `DbContext` is an InfoCarrier client — very nearly
  works**, and was measured rather than reasoned about. See D4 below.

**Exit criteria — all met**
- **MET (J5).** A provider-neutral *"is this type mapped to one document?"* seam, with the relational
  implementation behind it, so `InfoCarrier.Core` no longer references
  `Microsoft.EntityFrameworkCore.Relational` (D3 answer **(c)**, chosen 2026-08-16).
  `IInfoCarrierDocumentMapping` reads the container annotation by its string name; three
  `DocumentMappingPinTest` assertions pin the strings and the ownership-chain walk against EF's own
  constants.
- **MET by restatement (J6).** ~~A query boundary that also asks **what the backend can evaluate**~~
  — **RESTATED 2026-08-17: the capability axis is *identified, decided and recorded*.** It is a second, independent axis and
  it must not touch `TypeAllowlist` — that allowlist is ADR-008 constraint 2, a remote-code-execution
  control whose safety `security-review.md` §2 calls a conjunction, so a backend must never be able
  to widen it by answering a question. The capability axis only ever *narrows* what is shipped.
  **Decided: answer (c)** — one declaration, read by both halves, D2's shape — with **no mechanism
  built in M9**, because (b) cannot express a fact like J10's `ValueTuple` join key and because a
  mechanism should follow a second backend rather than precede it. The full decision, and the table
  of coarse-versus-fine facts that is its deliverable, is [`architecture.md`](../../architecture.md)
  §6a **D5**.
  **Why restated rather than met as written:** the original wording cannot be satisfied inside M9 —
  there is no second backend to ask, and adding one is explicitly out of this milestone's scope. A
  criterion that requires the thing the milestone excludes is a criterion that can only be met by
  changing the milestone. Recorded here rather than quietly dropped.
- **MET (J4).** The test project organised **by backend store**, as v1's was, so a store's coverage
  is countable by looking at it: `InMemory/` 57, `Sqlite/` 25, `TestUtilities/` 16, and 18
  store-independent.
- **MET (J1, J2, J3+J11, J12a).** Every base that runs on Tier A only because it was adopted there
  first is moved to the tier that translates (CLAUDE.md's A79/A80 rule). The audit found three,
  worth 24 mirrored skips; a fourth (`GraphUpdates`) moved once J11 made it possible.
  **The moves were not bookkeeping** — `ProxyGraphUpdates` arrived on a store that enforces foreign
  keys with 165 failures, and closing them found a real defect in what `ChangeEntryMapper` sends.
  **And CLAUDE.md's own rule about this was wrong and is corrected**: it said a base using
  `ExecuteWithStrategyInTransactionAsync` could not move until a product feature existed. The
  feature had shipped in M4; what was missing was the test class's `UseTransaction` override, which
  two other classes already carried.

**Deliberately *not* exit criteria**
- Adding a third store. Cosmos is the recommended candidate and the only one with both a
  first-party EF Core 10 provider and a reference suite to check overrides against, but adopting
  it is its own milestone. The seam above is what makes it cheap; doing it first would be building
  the seam blind.
- Fixing the chained-InfoCarrier defects. They are recorded, not scheduled.

---

## CI strategy

Two jobs, because the spec suite is legitimately red during build-out and
[`CLAUDE.md`](../../../CLAUDE.md) forbids skipping tests to force green.

**Job 1 — fast gate (must be green).** Build + `ExpressionRoundTripTest` + `InMemorySmokeTest`.
Any failure blocks.

**Job 2 — spec ratchet.** Run the full suite, compare the failure count against a committed
baseline in `test/known-failures.txt`. **Fail only if failures increase.** Nothing is skipped
or hidden, progress is monotonic, and the baseline drops as milestones land.

**The reports.** The ratchet writes a delta to the run's job summary — the counts against the
baseline, plus the tests fixed and broken by name. A `dorny/test-reporter` step turns the same TRX
into a `Spec suite` check run, so a pull request carries the counters in a check title. That check
reports and never gates: `fail-on-error: false` keeps it green, so the count rides on the check
title while `eng/ratchet.sh`'s exit status stays the verdict. It **must never be a required check**,
because a check that is green unconditionally requires nothing. It was red for one pull request and
reversed the same day — a pull request list shows one rollup icon per row and takes the worst check,
so every row would have worn a cross meaning "nine tests fail, as they always have". A fork's pull request gets the check
from `test-report.yml` instead, because a fork's token cannot create one.

**Job 3 — documentation gates.** `eng/doc-links.py` and `eng/doc-words.py --all --budget`, on
every push. `mkdocs build --strict` checks that a page exists and not that an `#anchor` does, so
without this a renamed heading breaks inbound links and the build stays green.

**Enforcement, since 2026-08-26.** Until that date none of this was enforced: `main` had no
protection and the one ruleset was disabled and targeted no refs, so the ratchet computed the right
verdict and nothing acted on it. A repository ruleset on the default branch now requires a pull
request with one approving review, requires `Docs gates`, `Fast gate` and `Spec ratchet` to pass,
and blocks force-pushes and deletion of `main`. **`Spec suite` is deliberately not required**, for
the reason above: it is green unconditionally, so requiring it would require nothing.

Two choices in it are deliberate and would otherwise read as mistakes. **Repository admin is on the
bypass list in `Always` mode**, which exempts the owner from every rule including direct and force
pushes; that is correct while this is a single-maintainer repository, and it becomes
`For pull requests only` once collaborators are established — the owner then goes through pull
requests but can still merge without waiting on the approval or the checks. And **branches must be
up to date before merging**, which costs a full re-run of the eight-minute suite immediately before
each merge; that price is paid on purpose, because the alternative is merging a pull request whose
green was measured against an older `main`.

**There is no third tier and no nightly job.** This said "Tier C (Docker SQL Server) runs nightly
from M7" until 2026-08-25. That tier was dropped on 2026-08-24 (M7 above), and no scheduled
workflow ever existed: the repository has five workflows and none runs on a cron — the fifth, `test-report.yml`, is triggered by `workflow_run` and not by a schedule.

---

## Deferred, tracked, not forgotten

From wire-protocol §5 and research-findings §10 — resolved in the milestone that needs them:

| Item | Milestone |
|---|---|
| W1 minimal column payload | M2 |
| W2 store-generated value keying (resolved §9, needs implementing) | M3 |
| W3 transaction token | M4 |
| W5 exception fidelity · W6 cancellation | M5 |
| Q5 canonical form + compiled-query cache · Q10 server delegate cache. **Out of scope for v10, 2026-08-24**, owner's decision; it is a latency item and never a correctness one (M8 above) | post-v10 |
| Q6/W4 streaming vs identity resolution | M8 |
| Server-held transaction lifetime: idle timeout, token ownership, multi-instance (see M8 above) | M8 |
| ~~Q7 spatial Z/M via WKT~~ ✅ **done 2026-08-10** (C15/C17/C18, landed in M6) | ~~M7~~ |
| **`IgnoreQueryFilters` crosses the wire and the server honours it**, so a global query filter is not an authorization boundary. Three strategies, one preferred, in [`cold-read-findings.md`](cold-read-findings.md) §1. **Deferred deliberately 2026-08-24**, owner's decision: v10 ships with this gap open, and the reasoning is below | post-v10 |
| **Idempotency for a retried unit of work.** A transport failure leaves the outcome unknown and there is no request id to ask with. Same file, §2 | post-v10 |
| **Nothing observable at runtime**: no logger category, no round-trip counter. Same file, §2 | post-v10 |
| **A non-relational backend tier, for example an ephemeral MongoDB.** Raised 2026-08-24 as the direction preferred over the dropped SQL Server tier. Every store this repository tests today is relational or InMemory, so nothing exercises a backend whose query translation refuses different things. **Nothing is committed and no design exists.** M9 already made the provider store-neutral (`InfoCarrier.Core` does not reference `EFCore.Relational`), which is what would make this cheap to attempt | future scope |
| **Client/server version-skew policy.** The envelope carries a `ProtocolVersion`; nothing states what a fleet on mixed builds should expect. Same file, §2 | post-v10 |
| **`InfoCarrier.Core.Relational`, a companion package on BOTH halves.** Raised 2026-09-02 by the owner. `InfoCarrier.Core` references nothing relational (§6a D3), so every relational fact it needs is a magic string or a reflection read: 9 `Relational:` annotation strings pinned by a 268-line test, `RelationalQueryRootShape`'s 276 lines and 10 trim suppressions, a hand-written narrower copy of EF's `EntityTypeHierarchyMappingConvention`. A second package referenced by client and server deletes them **without reversing D3**, because the reference lives outside `InfoCarrier.Core`. Measured, priced in three levels and tracked as [#97](https://github.com/azabluda/InfoCarrier.Core/issues/97). **LEVEL 1 IS BUILT (R120, 2026-09-02)**: the package ships, `RelationalQueryRootShape` is gone, and §6a D3's R120 amendment records the seam and the one-reader constraint it uncovered. Level 2 (EF's relational conventions on the client model) and level 3 (a relational model on the client, which needs a type-mapping source the client cannot honestly have) are still open | level 1 done; 2 and 3 future scope |
| **`UdfDbFunction`'s 55 reds are a TIER question, not a gap to ignore.** Raised 2026-09-02 (R103/R106), owner's decision. EF's own `SqliteComplianceTest` ignores `UdfDbFunctionTestBase` outright, so the reference provider does not run this base on the only relational store this suite has — which means the 55 have never been judged against a store that hosts the base. **They are deliberately NOT added to `IgnoredTestBases`**: the class stays adopted and the reds stay visible, because dropping it would lower `failed` by 55 while answering nothing. What would answer it is another tier, relational or not; the non-relational entry above is the same question from the other end. Tracked as [#96](https://github.com/azabluda/InfoCarrier.Core/issues/96) | future scope |

### `IgnoreQueryFilters`: why v10 ships with it open

**Decided 2026-08-24 by the owner. v10 ships without a query filter that a hostile client cannot
switch off.** Recording the reasoning here because a security-relevant gap that is deferred without
one gets re-litigated, or worse, quietly forgotten.

**It is deferred, not unmitigated.** The documented control is a query interceptor on the server,
which re-applies the tenant predicate whatever the incoming tree says, and a client cannot reach it.
Beside it sits the coarse control of the shared model: a type that must never leave the server goes
on a context only the server has. `multi-tenancy.md` works both through and `security.md` carries the
threat model, so a reader who follows the pages gets a boundary that holds. What v10 does not give is
a boundary that holds for a reader who *ignores* the pages and reaches for a query filter, which is
the natural thing to reach for.

**The deferral has a scope, and it is only the honest-client case that is safe.** Two operations
are affected, not one: `IgnoreQueryFilters()` disables the filter for reads, and it disables it for
`ExecuteUpdate` and `ExecuteDelete` too, because those translate from a query
(cold-read-findings §1). A `SaveChanges` never sees a filter at all. So the gap is read and bulk
write, and the interceptor covers all three.

**What deciding it later costs, checked before deferring.** Strategy (c), the preferred one, refuses
an ignore-request for a filter the server added on its own while allowing one the client's model also
declares. That needs a deny gate in `NodeToExpressionTranslator.Admit` and a per-filter decision the
gate cannot make today, because it sees a method name and its arguments rather than the model. None
of that is a wire-format change, and the envelope does not have to grow a field. So the decision does
not get more expensive by waiting, which is the property that makes deferring it reasonable rather
than convenient.

**Two things that must not drift while it waits.** No user-facing document may claim a query filter
is an authorization boundary, and none does today. And no document may name the fix as a plan
(`docs/doc-style.md` rule 6).

**[`cold-read-findings.md`](cold-read-findings.md) is the full record**, from seven readers who were
each given a slice of the user-facing documentation, a persona and a task, and told to read only
their own files. It separates what was fixed at once, what is product work, what is a
documentation gap, and four false positives recorded so they are not raised again.
