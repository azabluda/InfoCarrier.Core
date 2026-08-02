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

- [x] **A2.** `Query/ServerBoundaryAnalyzer` — bottom-up `ServerOk` over `TypeAllowlist`;
      frontier extraction; free-parameter check for shippability (§3.1, §3.5). Shippable =
      server-ok **and** contains a query root **and** closed; a subtree that is server-ok but open
      is a correlated subquery under a client projection, reported separately for phase B rather
      than cut. ✅ `<this commit>`

- [x] **A3+A4.** `Query/QuerySplitter` + `SplitQuery` — shipped queries plus a residual lambda,
      one parameter per shipped query, applied by `EnumerableQuery<T>` so the residual keeps its
      `Queryable` shape. Markers stripped, `EF.Property` and open fragments rejected by name
      (§3.4, §6). Navigation demand → `Include` augmentation (§3.6), rooted paths keyed by entity
      type; a read no shipped query can carry is an error, never a quiet `0`. Landed together
      because A3 alone *is* the silent-`0` bug. ✅ `<this commit>`

- [x] **A5.** Wire the splitter into `QueryExecutor<TElement>`: materialize each shipped query by
      its *boundary* element type, then run the residual to produce `TElement`. Recompute
      `ReturnsSingleResult` **for the shipped query** — `…Select(c => new {…}).First()` ships a
      sequence and the residual takes the first (§5). Rows drain eagerly per round trip: the wire
      reference scope belongs to one exchange, and a lazy sequence would decode against the next
      request's scope. **1421 → 669.** ✅ `<this commit>`

- [x] **A5a.** A shippable subtree must be an *executable query*, not merely a serializable one
      containing a root. A quoted lambda is server-ok and closed but is a function; shipping one
      made the server return the lambda object as a result row. **669 → 526.** ✅ `<this commit>`

- [x] **A5b.** Residual parameters bind by interface (`IQueryable<T>`), not by the shipped
      expression's exact type — a subtree ending in `Include(…)` is typed `IIncludableQueryable`,
      which the materialized `EnumerableQuery` does not implement. **526 → 484.** ✅ `<this commit>`

- [ ] **A6.** Tracking semantics (§4): boundary rows materialize with identity resolution and
      without tracking; entities present in the residual's *result* are attached afterwards per
      `QueryTrackingBehavior`.

- [ ] **A7.** Verification pass (§3.1): serializing a chosen shipped query must clear the server
      allowlist; on failure move the boundary one operator inward and retry, bounded. Assert in a
      test that the retry never fires across the spec suite.

## Phase M2-B — projection rewrite (this is W1)

- [x] **B1+B2+B3.** `Query/ProjectionRewriter` + `TupleCarrier` — maximal server-evaluable
      fragments of a projection body travel as a `ValueTuple` (nesting above arity 7) and the
      client rebuilds its own types from the slots (§3.2). Applied to `Queryable.Select` sitting
      on a server-executable source, innermost first. Closes correlated subqueries, navigation
      reads **and** the `GroupBy` decomposition the cut itself caused. W1 asserted: the shipped
      query for `Select(a => new { a.Name })` carries one `string`, not an `Author`.
      **484 → 270.** ✅ `<this commit>`

## Phase M2-C — recursion and multi-source

- [x] **C2+C3.** Result selectors recognised **structurally**, not by name: an operator whose
      last argument returns its last generic argument *and* whose element type is that argument.
      Covers `Select` / `SelectMany` / `Join` / `GroupJoin` / `GroupBy` / `Zip` in one rule, and
      keeps `OrderBy` out — it has the same lambda shape but returns a sequence of `TSource`, so
      rewriting it would replace rows with their sort keys. **270 → 220.** ✅ `<this commit>`
- [x] **C4.** Falls out of C2: a `Join` with a client result selector stays **one** server query
      instead of two shipped sources re-joined on the client. ✅ `<this commit>`
- [x] **C5.** Client evaluation outside the final projection is a **translation failure**, in
      EF's own wording (`CoreStrings.TranslationFailedWithDetails` + `QueryUnableToTranslateMethod`).
      A residual operator forced by the *type boundary* is fine; one whose lambda calls a method
      the server cannot run is not — answering it locally means fetching the whole table silently.
      Three tests overridden mirroring EF's relational/SqlServer overrides of the same tests.
      **220 → 194.** ✅ `<this commit>`
- [x] **C6.** `Include` is placed at the **query root** it is read from, not wrapped around the
      whole shipped query — the projection rewrite usually leaves the entity in a tuple slot, so
      the rows are not that entity type and the outer wrap had nowhere to go. **194 → 154.**
      Suite run time rose 2m36s → 4m43s: the over-fetch is real, and is what B's fragment
      evaluation removes wherever it can reach. ✅ `<this commit>`
- [x] **C1.** Nested projections rewrite too. A collection navigation read inside a projection
      (`c.Orders.Select(o => new { … })`) is an **`Enumerable`** call over an `ICollection`, not a
      `Queryable` call over a root — so neither the declaring-type test nor the queryable-only
      element-type helper could see it, and the whole nested projection fell to the client with
      its navigations unloaded. Recognising `Enumerable` too, and rebuilding lambdas unquoted
      when the original was, makes the rewrite recursive by construction. **134 → 118.**
      ✅ `<this commit>`

## Phase M2-D — adopt and drive green

- [x] **D1.** `NorthwindSelectQueryTestBase` is adopted as `NorthwindSelectQueryInfoCarrierTest`
      in `NorthwindQueryInfoCarrierTests.cs` — the plan recorded it as missing because the class
      has no file of its own. Verified present, not re-added.
- [ ] **D2.** Drive `NorthwindSelectQueryInfoCarrierTest` and `NorthwindJoinQueryInfoCarrierTest`
      green.
- [ ] **D3.** Regression guard: `Select(c => new { c.City, Count = c.Orders.Count() })` returns
      non-zero counts. This is the one M2 failure mode that would not announce itself.

## Phase M2-E — re-check the store-limitation overrides

- [x] **E1.** First pass: 14 overrides deleted because the split genuinely handles what they
      assert is unsupported — 8 `Final_GroupBy_*` (their source is a client-typed projection, so
      the `GroupBy` runs on the client and never reaches InMemory's non-composed-GroupBy limit),
      2 set-operation, 4 `SelectMany_with_client_eval`. Plus the client-evaluation guard made
      per-*argument*: a result selector runs after the rows are chosen, so client code there is
      legal, but a `Join` key selector decides which rows match and is not. **154 → 134.**
      ✅ `<this commit>`

      `Final_GroupBy_nominal_type_entity` now fails on values (expected 69, got 91) — the
      override was masking a real defect. Left red deliberately; see the open items below.

- [x] **E2a.** The client-evaluation guard reaches **every** client-side operator, not only one
      reading straight from a shipped query — a `SelectMany` two links above the boundary was
      invisible to it. And the projection exemption recurses: client code inside a nested
      projection is legal (`c.Orders.Select(o => new { ClientMethod(o) })`), client code applied
      to the sequence itself is not (`ClientDefaultIfEmpty(grouping)` decides which rows exist).
      **118 → 112.** ✅ `<this commit>`

- [x] **E2b.** `Include` validates its lambda is a property path (allowing a derived-type cast
      and a composed `Where`/`OrderBy`/`Skip`/`Take` filter, as EF does). EF checks this during
      translation, which this provider replaces — so `Include(o => new { o.Customer, o.OrderDetails })`
      reached the splitter and was quietly read as a projection boundary rather than the mistake
      it is. **112 → 100.** ✅ `<this commit>`

- [x] **E3.** `EF.Property` on the client side is **evaluated**, not refused: the value is on the
      materialized entity and the model knows how to read it, which is what EF would have
      compiled the call to anyway. A shadow property still cannot be — its value is in the change
      tracker — and says so by name. Also fixed `SplitQuery.Apply` swallowing the real exception
      inside `TargetInvocationException`. **100 → 91.** ✅ `<this commit>`

- [ ] **E2c.** Remaining override failures: `Throws_on_concurrent_query_*` (the concurrency
      detector is released before the caller enumerates), `Select_GroupBy_SelectMany`.

---

## The residual, classified (2026-08-02 — measured at 118, now 90)

Measured from `artifacts/test-results/c1.trx`. Nothing here is masked; each line is a real
failure with a named cause.

| Count | Family | Verdict |
|---|---|---|
| 28 | Spec tests asserting a throw that no longer happens — `No_orderby_added_for_client_side_GroupJoin_*`, `Include_property_expression_invalid`, `Throws_on_concurrent_query_*`, `Select_GroupBy_SelectMany`, `Where_query_composition6` | E2: re-check one by one, as E1 did |
| 12 | Navigation read on the client that no shipped query can carry | needs dataflow the syntactic scan does not have |
| 10 | `NullReferenceException` inside a client-side `SelectMany` over a transparent identifier | real defect, uninvestigated |
| 10 | `EF.Property` on the client side of the boundary | **done in E3** — mapped properties read through the model; only shadow state remains out of reach |
| 8 | Client evaluation correctly refused (`translation-failure`) but the test expects success | E2 |
| 8 | `First/Single/Last_over_custom_projection_compared_to_null` | **known limitation, see below** |
| 6 | Correlated subquery under a client projection the rewrite cannot reach | C-phase tail |
| ~36 | Value mismatches, one to four tests each | individually triaged |

### The custom-projection-compared-to-null limitation

`Where(c => c.Orders.Select(o => new { o.OrderID }).First() == null)` constructs an anonymous
type **inside a predicate**, where the value never crosses the wire — but the server would still
have to *construct* it, and it has no such type. EF's InMemory provider manages only because it
shares an `AppDomain`; no network transport could. Rewriting the construction into a
`ValueTuple` does not save it either, because the predicate compares the result to `null` and a
tuple is a struct.

This is a genuine limit of the type boundary rather than a gap in the implementation, and it is
recorded here rather than papered over.

---

## Phase T — SQLite Tier B (ADR-009)

Landed out of milestone order, deliberately: several of the residual failures are InMemory's
limits rather than this provider's, and only a backend that actually translates can tell them
apart. It is an M3 exit criterion regardless, and M4 cannot start without it.

- [x] **T1.** `SqliteInfoCarrierBackendTestStore` + `InfoCarrierTestStoreFactory.Sqlite`, and
      `SqliteSmokeTest` proving the slice end to end on a relational backend. The held-open
      connection is asserted, not assumed — mutation-tested by removing `Open()`, which fails all
      three tests. The projection split answers correctly against real SQL, which also settles
      the open question of whether the `ValueTuple` carrier translates outside InMemory.
      ✅ `<this commit>`

- [x] **T2.** Northwind on Tier B: `NorthwindInfoCarrierSqliteServerContext` (keyless types via
      `ToSqlQuery`), `NorthwindQueryInfoCarrierSqliteFixture`, and
      `NorthwindWhereQuerySqliteInfoCarrierTest` — the same base as Tier A, run against a backend
      that genuinely translates. 406 tests, **38 → 24** after adding only the overrides the run
      actually justified. ✅ `<this commit>`

      Three things it measured that Tier A could not:

      1. The `Where_compare_*` family fails as a **translation failure**, exactly as EF's own
         SQLite class asserts — confirming the prediction recorded in the Tier A class rather
         than leaving it an assumption. Six of the eight; see (3).
      2. The `*_over_custom_projection_compared_to_null` family fails on **both** tiers, so it is
         ours, not InMemory's. That settles the classification of the largest documented
         limitation by measurement.
      3. **Two new defects, both invisible on Tier A:**
         `Where_compare_constructed_equal` and `_multi_value_equal` return **zero rows where six
         are expected** — a silent wrong answer, worse than the translation failure EF gives.
         And `Generic_Ilist_contains_translates_to_server` fails because parameter substitution
         (research-findings §6) turns a local collection into a `ConstantExpression`, which
         relational EF cannot translate — InMemory never noticed because it client-evaluates.

- [x] **T3a.** The silent wrong answer is gone. Reference equality between two client-only
      reference types is now a translation failure: an anonymous type overrides `Equals`
      structurally but **not** `==`, so evaluating `new { x = c.City } == new { x = "London" }`
      on the client compares two freshly allocated objects by reference — always false, no error,
      six rows silently becoming zero. **115 → 110, nothing broken.** ✅ `<this commit>`

      Two refinements the measurements forced. `x == null` is a null *test*, not structural
      equality, and refusing it condemned every `FirstOrDefault() == null` (4 tests). And the six
      `Tuple` variants are **not** covered: `Tuple<>` is a type the server knows, so the
      comparison ships rather than being refused, and InMemory then client-evaluates it to the
      same silent false — Tier A keeps its no-ops there, Tier B asserts the translation failure
      its server actually reports. Eight Tier A no-ops became real assertions in the process.

- [x] **T3b.** Collection parameters are written out element by element as a `NewArrayExpression`
      — the shape `QueryRootProcessor` turns into an `InlineQueryRootExpression`. A single
      constant holding a `List<T>` is not that shape, which is why `cities.Contains(c.City)`
      failed to translate. research-findings §6 amended: the scalar rule stands, collections are
      the exception. **110 → 108, nothing broken.** ✅ `<this commit>`

- [x] **T4.** `Select` and `Join` adopted on Tier B — the two bases M2's exit criteria name.
      5,209 tests total: **Tier A 90, Tier B 74**. Tier A is unchanged, so nothing the relational
      tier surfaced is a regression. ✅ `<this commit>`

      Three bugs in the Tier B harness itself, all invisible with a single class:
      **534 failures** of `table "CustomerQueries" already exists` — each backend builds its own
      service provider, so the `TestStoreIndex` that normally makes shared initialisation run
      once is not shared, and every class re-ran `EnsureCreated`. A **data race** on the
      per-name guard I added to fix that (a plain `HashSet` under a *per-name* semaphore). And
      **cross-test contamination**: `Cache=Shared` is process-wide, and both smoke tests shared
      one `SmokeContext` type, so a model built for one provider reached the other — the
      *InMemory* smoke test failed with "no such table: Blogs".

- [ ] **T5.** Tier B's 74: 36 are SQLite's lack of `APPLY` (EF's own SQLite classes override
      these), the rest overlap the Tier A families already classified. Mirror the store-limitation
      overrides, then extend to the remaining bases.

---

## Attempted and reverted: deferring the reassembly (2026-08-02)

The largest remaining group of failures traces to one cause. `from c in cs join o in os … from o
in grouping.DefaultIfEmpty() select …` compiles to a `GroupJoin` whose result selector builds a
**transparent identifier** — an anonymous type. EF handles those internally; this provider must
treat one as a type boundary, so every operator above the join runs on the client. That is not
just slow: `DefaultIfEmpty` yields `null`, SQL propagates nulls through a projection, and
LINQ-to-Objects throws `NullReferenceException` instead. All ten of the NRE failures are this.

The fix in principle is to push the reassembly outward past operators that only read members of
it, rewriting `p.Member` into the tuple slot the member came from — the "operator pushdown" the
spec deferred (§7).

**Tried, measured 91 → 383, reverted.** The substitution builds trees that are type-correct
enough for `Expression.Call` to accept and then untranslatable, or subtly wrong: 67 `SelectMany`
and 40 `Join` translation failures, plus arity mismatches where a `ValueTuple<int>` was
substituted for a sequence. Suite time dropped from 4m37s to 1m29s, which is the tell — the
queries were failing before doing any work.

It is the right next step and it needs its own design pass, not an afternoon: the substitution
has to preserve what each operator expects of its source, and a transparent identifier can nest.

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
| 2026-08-02 | 3611 | 669 | 4289 | after A5 (splitter wired into the client executor) |
| 2026-08-02 | 3756 | 526 | 4291 | after A5a (only executable queries are shipped) |
| 2026-08-02 | 3798 | 484 | 4291 | after A5b (residual parameters bind by interface) |
| 2026-08-02 | 4013 | 270 | 4292 | after B1-B3 (projection rewrite — the tuple carrier) |
| 2026-08-02 | 4063 | 220 | 4292 | after C2-C4 (all result selectors, structurally recognised) |
| 2026-08-02 | 4092 | 194 | 4295 | after C5 (client evaluation is a translation failure) |
| 2026-08-02 | 4132 | 154 | 4295 | after C6 (Include placed at the root it is read from) |
| 2026-08-02 | 4152 | 134 | 4295 | after E1 (stale overrides deleted; guard made per-argument) |
| 2026-08-02 | 4168 | 118 | 4295 | after C1 (nested projections rewrite too) |
