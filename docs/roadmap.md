# Roadmap

Status: **M1 in progress** · Milestone-level plan for the whole project.

This doc is **stable** — it lists milestones, their exit criteria, and their order. It changes
only when scope changes.

The fine-grained checkbox plan for the *current* milestone lives in
[`implementation-plan.md`](implementation-plan.md), which is **rewritten each milestone**.
Do not put per-milestone task detail here; do not put roadmap-level scope there.

Authority: [`infocarrier-core-requirements.md`](infocarrier-core-requirements.md) ·
[`decisions.md`](decisions.md) · [`research-findings.md`](research-findings.md)

---

## Where we are

Phases A–E (query pipeline) and F1–F7 (spec-test fixture) are complete. The vertical slice is
green end to end: capture → serialize → transport → server rebind → EF execute → client
materialization with identity resolution.

Measured 2026-08-01: **141 passed / 272 failed / 413 total**, from **1 of 21**
`Northwind*QueryTestBase` classes. The suite inherits Microsoft's spec tests (ADR-004), so
coverage scales by adopting bases, not by writing tests.

---

## Milestones

### M1 — Query pipeline correctness + working signal ← **current**

Clear the mechanical failures masking the real state, and get a CI signal that can gate.

**Exit criteria**
- `QueryParameterExpression` substitution and STJ primitive registration fixed; failure count
  recorded and reduced (expected ≈ 141 → ≈ 350 of 413).
- CI builds the correct solution file and runs a **failure-count ratchet** (below) that fails
  the build when failures increase.
- `InfoCarrierComplianceTest` (F8) landed and **red on purpose**, publishing the authoritative
  inventory of unimplemented spec bases.
- Doc integrity closed: ADR-008/ADR-009 recorded ✅, subrepo revisions pinned.

### M2 — Projection split (requirements §3)

The one genuinely unsolved design problem. Approach is sketched in research-findings §8 — *the
boundary is the last `Select` whose element type the server's model knows* — but nothing
implements it. **Needs its own design session and spec before code.**

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

**Exit criteria**
- **Result wire format** — spec written: [`result-wire-format.md`](result-wire-format.md).
  1,047 of 1,440 failures (73%). Do this first: it is independent of the type-boundary work,
  it unblocks SaveChanges, and until it lands most other failures are masked behind it.
- ✅ Server-side type allowlist enforced, so client-only types cannot be materialized
  server-side even in-process. Projection tests fail again before they are fixed.
- Boundary detection in the server executor; client applies the residual projection.
- Minimal-column payload (wire-protocol W1) — the server returns only what the client
  projection needs, per requirements §3.3.
- `NorthwindSelectQueryTestBase` and `NorthwindJoinQueryTestBase` adopted and passing.

### M3 — SQLite backend + SaveChanges

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

### M4 — Transactions

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

**Partly closed 2026-08-01.** The *type* allowlist is implemented and on by default
(`TypeAllowlist`), so a payload can no longer name an arbitrary type for the deserializer to
construct. **The method allowlist is still open**: `ResolveMethod` binds any method on an
allowed declaring type, rather than the "Queryable / Enumerable / `EF.Functions` / model-bound
members" restriction ADR-008 specifies. Narrower than before, not yet sufficient.

**Exit criteria**
- Allowlists for node kinds, resolvable types, and invocable methods — **default deny**,
  opt-in registration for model entities and declared projection types.
- Payload depth/size limits (v1 needed a 10 MB stack for >1 MB payloads).
- `InfoCarrierEnvelope` + `ProtocolVersion` actually exercised by tests — currently the
  backend test store implements `IInfoCarrierClient` directly and bypasses both.
- Exception fidelity across the wire (W5) and cancellation (W6).
- Security review of the deserialization path.

### M6 — Coverage expansion

Work the M1 compliance inventory down. Every spec base ends up either implemented or in
`IgnoredTestBases` **with a stated reason** — nothing silently forgotten.

**Exit criteria**
- Relationships, owned types, table splitting, TPH/TPT inheritance (requirements §2.7).
- Compliance inventory fully classified.

### M7 — SQL Server (Tier C) + spatial

**Exit criteria**
- Docker SQL Server backend store; nightly CI job (ADR-009 Tier C).
- `rowversion` concurrency, computed columns, sequences, TPT/TPC.
- NetTopologySuite with **Z/M ordinates preserved** (requirements §2.8) — v1 lost them.

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
| Q7 spatial Z/M via WKT | M7 |
