# Implementation Plan — M2: Projection split

Status: **IN PROGRESS** · Milestone [M2](roadmap.md#m2--projection-split-requirements-3)

**Scope of this doc:** the current milestone only. Milestone-level scope belongs in
[`roadmap.md`](roadmap.md), not here. Design authority for this milestone is
[`projection-split.md`](projection-split.md) ([ADR-010](decisions.md#adr-010)); read it before
touching any phase below.

Previous plans: [`archive/2026-08-m1-query-correctness-plan.md`](archive/2026-08-m1-query-correctness-plan.md)
(M1 — including the standing failure taxonomy and the Phase K classification of the residual 42),
[`archive/2026-07-query-pipeline-plan.md`](archive/2026-07-query-pipeline-plan.md).
Archived plans are never edited again.

Each checkbox is one minimal, logically-complete substep, committed individually with the
checkbox ticked **in the same commit** (CLAUDE.md).

**Baseline entering M2 (2026-08-01):** `Passed: 2817, Failed: 1421, Skipped: 9, Total: 4247`.
Of the 1,421: 1,197 compiler-generated projection types, ~108 client-only DTOs, ~84
store-limitation overrides now tripping the type boundary first, 32 pre-L1 known failures.

---

## Phase N — carried from M1 · **parked**

M1's infrastructure tail. N1–N4 landed; the rest is **deferred to the end of M2** — the split
itself is the work that matters, and the local suite already gives the same signal CI would.

- [x] **N1–N4.** CI that can actually run: `.slnx` instead of the non-existent `.sln`, two jobs
      (fast gate + spec ratchet), `eng/ratchet.sh` gating on *direction* against
      `test/known-failures.txt`, SQL Server container dropped per ADR-009. The ratchet also
      guards the **total**, because a crashed test host reports fewer failures — the trap step
      K3c came one measurement from falling into. ✅ `<this commit>`

- [ ] **N5.** *(deferred)* Pin subrepo revisions in `research-infrastructure.md` — all four cells
      are `_(record tag/SHA)_`, so ADR-005's reproducibility guarantee is void. Capture SHAs from
      the existing clones **before** anything re-clones them. *(was J3)*

- [ ] **N6.** *(deferred)* Reconcile `ci-cd.md` with ADR-009 (Docker demoted to Tier C) and with
      the two-job ratchet strategy. *(was J4)*

---

## Phase M2-A — analysis and cut

The split with no projection rewrite: ship server-executable subtrees whole, run the residual
locally. Correct but coarse; A must never be *silently* wrong, which is what A4 is for.

- [x] **A1.** `Query/WireTypeCollector` — the types a node would put on the wire
      ([`projection-split.md`](projection-split.md) §3.1), enumerated from the same sources
      `ExpressionToNodeTranslator` writes `TypeNode`s from. The drift guard compares **per node**,
      not whole-tree: types repeat across a tree, so a whole-tree comparison passed even with
      `Member.DeclaringType` dropped. Six omissions mutation-tested, all caught. ✅ `<this commit>`

- [ ] **A2.** `Query/ServerBoundaryAnalyzer` — bottom-up `ServerOk` over `TypeAllowlist`;
      frontier extraction; free-parameter check for shippability (§3.1, §3.5).

- [ ] **A3.** `Query/QuerySplitter` + `SplitQuery` — orchestration returning shipped queries and
      a residual. Residual executes via `EnumerableQuery<T>`; marker calls (`AsNoTracking`,
      `Include`, `AsSplitQuery`) stripped first, `EF.Property` rejected with a named message
      (§3.4, §6).

- [ ] **A4.** Navigation demand → `Include` augmentation (§3.6). Syntactic and conservative:
      every navigation any residual operator reads on a server-known entity type is `Include`d on
      the query that supplies it. Over-fetching is accepted; a wrong answer is not. **Land with
      A3 or before it** — A3 without A4 answers `c.Orders.Count()` as `0`.

- [ ] **A5.** Wire the splitter into `QueryExecutor<TElement>`: materialize each shipped query by
      its *boundary* element type, then run the residual to produce `TElement`. Recompute
      `ReturnsSingleResult` **for the shipped query** — `…Select(c => new {…}).First()` ships a
      sequence and the residual takes the first (§5).

- [ ] **A6.** Tracking semantics (§4): boundary rows materialize with identity resolution and
      without tracking; entities present in the residual's *result* are attached afterwards per
      `QueryTrackingBehavior`.

- [ ] **A7.** Verification pass (§3.1): serializing a chosen shipped query must clear the server
      allowlist; on failure move the boundary one operator inward and retry, bounded. Assert in a
      test that the retry never fires across the spec suite.

## Phase M2-B — projection rewrite (this is W1)

- [ ] **B1.** `Query/ProjectionRewriter` — maximal `ServerOk` fragments of a projection lambda
      body; `ValueTuple` carrier synthesis (nesting above arity 7); reassembly lambda (§3.2).

- [ ] **B2.** Apply to top-level `Select`. Removes the A4 over-fetch for the shapes it covers and
      closes correlated subqueries under a client projection.

- [ ] **B3.** Minimal-column payload confirmed (wire-protocol W1): assert the shipped query for
      `Select(c => new { c.City })` carries one column, not a `Customer`.

## Phase M2-C — recursion and multi-source

- [ ] **C1.** Recursive rewrite of nested projections inside a fragment (§3.3).
- [ ] **C2.** `SelectMany` / `Join` / `GroupJoin` / `Zip` result selectors.
- [ ] **C3.** `GroupBy` element and result selectors.
- [ ] **C4.** Multiple shipped queries from one LINQ query (§3.5) — a `Join` with an anonymous
      result selector has two independent sources.

## Phase M2-D — adopt and drive green

- [ ] **D1.** Adopt `NorthwindSelectQueryTestBase` as `NorthwindSelectQueryInfoCarrierTest` (not
      currently adopted — one of the 21 bases Phase I did not reach).
- [ ] **D2.** Drive `NorthwindSelectQueryInfoCarrierTest` and `NorthwindJoinQueryInfoCarrierTest`
      green.
- [ ] **D3.** Regression guard: `Select(c => new { c.City, Count = c.Orders.Count() })` returns
      non-zero counts. This is the one M2 failure mode that would not announce itself.

## Phase M2-E — re-check the store-limitation overrides

- [ ] **E1.** The ~84 overrides that now trip the type boundary before reaching the translation
      failure they assert: each either returns to asserting its original failure, or is deleted.
      Mirroring EF's own `*InMemoryTest` override remains the objective criterion (roadmap M3
      inventory).

---

## Exit criteria

M2 closes when all of:

1. N1–N6 done (M1's infrastructure tail).
2. Boundary computed on the client; `ServerQueryExecutor` unchanged.
3. W1 minimal-column payload demonstrated (B3).
4. `NorthwindSelectQueryTestBase` and `NorthwindJoinQueryTestBase` adopted and passing.
5. Suite failures at or below the pre-L1 baseline of **32**, with the ~1,305 M2 failures cleared.

Then rewrite this doc for **M3 — SQLite backend + SaveChanges**.

## Baseline log

Continued from the M1 plan; the run population is unchanged (4,247).

| Date | Passed | Failed | Total | Note |
|---|---|---|---|---|
| 2026-08-01 | 2817 | 1421 | 4247 | M2 entry baseline — after L1 (type allowlist ON) |
