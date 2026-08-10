# Roadmap

Status: **M6 in progress** · Milestone-level plan for the whole project.

This doc is **stable** — it lists milestones, their exit criteria, and their order. It changes
only when scope changes.

The fine-grained checkbox plan for the *current* milestone lives in
[`implementation-plan.md`](implementation-plan.md), which is **rewritten each milestone**.
Do not put per-milestone task detail here; do not put roadmap-level scope there.

Authority: [`infocarrier-core-requirements.md`](infocarrier-core-requirements.md) ·
[`decisions.md`](decisions.md) · [`research-findings.md`](research-findings.md)

---

## Where we are

The query pipeline is complete end to end — capture → serialize → transport → server rebind →
EF execute → client materialization with identity resolution. The **projection split (M2)**,
**SaveChanges (M3)** and **transactions (M4)** are implemented, with the type boundary enforced
rather than hidden by the in-process harness.

Measured 2026-08-10 (`artifacts/measure/c30`): **`Total tests: 22278, Passed: 21911,
Failed: 150, Skipped: 217`**. The suite inherits Microsoft's spec tests (ADR-004), so coverage
scales by adopting bases, not by writing tests — M6 took the unadopted count from 41 to 1.

The 150 are classified in [`implementation-plan.md`](implementation-plan.md); none is masked.
Most are already answered rather than open: 40 wait on the B12 decision, 26 are the
`MaterializationInterception` topology B24 settled, 9 are a locale defect in EF's own test code,
and the majority of the rest are spec tests asserting a limitation this provider does not have.

---

## Milestones

### M1 — Query pipeline correctness + working signal ✅ *(query work done; N5/N6 doc tail open)*

Clear the mechanical failures masking the real state, and get a CI signal that can gate.

**Exit criteria**
- `QueryParameterExpression` substitution and STJ primitive registration fixed; failure count
  recorded and reduced (expected ≈ 141 → ≈ 350 of 413).
- CI builds the correct solution file and runs a **failure-count ratchet** (below) that fails
  the build when failures increase.
- `InfoCarrierComplianceTest` (F8) landed and **red on purpose**, publishing the authoritative
  inventory of unimplemented spec bases.
- Doc integrity closed: ADR-008/ADR-009 recorded ✅, subrepo revisions pinned.

### M2 — Projection split (requirements §3) ✅ **complete**

The one genuinely unsolved design problem. **Spec written 2026-08-01:**
[`projection-split.md`](projection-split.md), recorded as [ADR-010](decisions.md#adr-010).

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
>   [`transparent-identifiers.md`](transparent-identifiers.md) / [ADR-011](decisions.md#adr-011). `from … join … select new { a, b }`
>   makes an anonymous type EF handles internally and we must treat as a boundary, so everything
>   downstream lands on the client. Deferring the reassembly and threading tuple slots through
>   downstream operators is the next real gain, and is the "operator pushdown" the spec deferred.

**Exit criteria**
- **Result wire format** — spec written: [`result-wire-format.md`](result-wire-format.md).
  1,047 of 1,440 failures (73%). Do this first: it is independent of the type-boundary work,
  it unblocks SaveChanges, and until it lands most other failures are masked behind it.
- ✅ Server-side type allowlist enforced, so client-only types cannot be materialized
  server-side even in-process. Projection tests fail again before they are fixed.
- ✅ Design spec + ADR — [`projection-split.md`](projection-split.md), [ADR-010](decisions.md#adr-010).
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

### M5 — Wire hardening 🔒 **release blocker**

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

**The rest of M5 is still open**, and is now the whole of it: payload limits, the envelope,
exception fidelity, cancellation, and the review.

**Exit criteria**
- Allowlists for node kinds, resolvable types, and invocable methods — **default deny**,
  opt-in registration for model entities and declared projection types.
  *Types* ✅ (`TypeAllowlist`, 2026-08-01) · *methods* ✅ (C30, 2026-08-10) · **node kinds still open**.
- Payload depth/size limits (v1 needed a 10 MB stack for >1 MB payloads).
- `InfoCarrierEnvelope` + `ProtocolVersion` actually exercised by tests — currently the
  backend test store implements `IInfoCarrierClient` directly and bypasses both.
- Exception fidelity across the wire (W5) and cancellation (W6).
- Security review of the deserialization path.

### M6 — Coverage expansion ← **in progress** (plan Phase C)

Work the M1 compliance inventory down. Every spec base ends up either implemented or in
`IgnoredTestBases` **with a stated reason** — nothing silently forgotten.

**Scope confirmed 2026-08-09: all 41 remaining bases are in.** `Query.Associations.*` (27) and
`BulkUpdates.*` (5) had been held back pending this call; they are **Tier B**, decided by the fact
that `EFCore.InMemory.FunctionalTests` ships neither family at all. The per-base tier verdict is
plan item C0 and is not re-derived per batch.

**State 2026-08-10: 41 adopted down to 1.** `AdHocJsonQueryTestBase` is the only base the
compliance test still reports.

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

**And that adoption sits behind [B12](implementation-plan.md).** The corpus is owned JSON
collections throughout, so most of what it adds lands on the undecided question of how a
JSON-mapped owned collection is keyed. Plan item C21 records the price and the decision not to pay
it yet: 626 + 322 lines of relational mirror plus seven abstract seeds that only EF's relational
classes implement. **M6 is therefore blocked on a decision, not on work.**

### M7 — SQL Server (Tier C) + spatial *(spatial half already complete)*

**Exit criteria**
- Docker SQL Server backend store; nightly CI job (ADR-009 Tier C).
- `rowversion` concurrency, computed columns, sequences, TPT/TPC.
- ~~NetTopologySuite with **Z/M ordinates preserved** (requirements §2.8) — v1 lost them.~~
  ✅ **Done 2026-08-10, in M6. Do not plan this again.**

**The spatial criterion closed early, and it needed no SQL Server.** It arrived in three pieces,
each landed and measured on its own because plan item C9 had attempted two of them together and
aborted the test host:

| Piece | Plan | What it is |
|---|---|---|
| Type mapping | C15 | The NetTopologySuite branch every provider carries, in `InfoCarrierTypeMappingSource`. Worth 19 tests by itself: the client — not SpatiaLite — was what could not map a `Point`. |
| Value-mapper seam | C17, [ADR-012](decisions.md#adr-012) | Product API. A geometry travels as one wire primitive instead of being walked reflectively, which is what overflowed the stack in C9. |
| WKT mapper | C18 | **Test-side**, so `InfoCarrier.Core` still does not reference NetTopologySuite — v1's arrangement, kept. |

**Z and M survive because the format is WKT, not v1's GeoJSON**, which carries neither. That is
the defect requirements §2.8 records, and Q7 below is the entry that named WKT as the answer.
`GeometryWireFormatTest` asserts the ordinates and the SRID directly: the two spatial spec suites
model XY at SRID 0 and would pass all 173 tests against a mapper that silently dropped them.

Both spatial bases are adopted on **Tier A**, 169 of 173.

### M8 — Productization

**Exit criteria**
- HTTP and gRPC transport bindings (only in-process exists today).
- Streaming results as `IAsyncEnumerable<T>` (requirements §4.4, wire-protocol W4) — the
  server currently buffers into an `ArrayList`.
- Compiled-query cache keyed by canonical serialization (ADR-008 constraint 6, Q5).
- AOT/trimming verification (requirements §4.5).
- Sample apps, NuGet packaging, `release.yml`.

---

## CI strategy

Two jobs, because the spec suite is legitimately red during build-out and
[`CLAUDE.md`](../CLAUDE.md) forbids skipping tests to force green.

**Job 1 — fast gate (must be green).** Build + `ExpressionRoundTripTest` + `InMemorySmokeTest`.
Any failure blocks.

**Job 2 — spec ratchet.** Run the full suite, compare the failure count against a committed
baseline in `test/known-failures.txt`. **Fail only if failures increase.** Nothing is skipped
or hidden, progress is monotonic, and the baseline drops as milestones land.

Tier C (Docker SQL Server) runs nightly from M7, never on the per-commit path.

**Known defects to fix in M1:** `build.yml` restores `InfoCarrier.Core.sln` but the repo has
`InfoCarrier.Core.slnx`; its `~InMemory` / `~SqlServer` filters match no current test class, so
it would run zero tests even after the restore is fixed.

---

## Deferred, tracked, not forgotten

From wire-protocol §5 and research-findings §10 — resolved in the milestone that needs them:

| Item | Milestone |
|---|---|
| W1 minimal column payload | M2 |
| W2 store-generated value keying (resolved §9, needs implementing) | M3 |
| W3 transaction token | M4 |
| W5 exception fidelity · W6 cancellation | M5 |
| Q5 canonical form + compiled-query cache · Q10 server delegate cache | M8 |
| Q6/W4 streaming vs identity resolution | M8 |
| ~~Q7 spatial Z/M via WKT~~ ✅ **done 2026-08-10** (C15/C17/C18, landed in M6) | ~~M7~~ |
