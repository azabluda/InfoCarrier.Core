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

- [x] **A6.** Tracking semantics (§4). A split query materializes every row the server sent, but
      only the entities the **residual yields** belong in the change tracker. A join shipping 919
      rows to answer a projection over 7 entities tracked all 919; a query projecting no entities
      at all still filled the tracker. Entries are now left `Detached` — invisible to
      `ChangeTracker.Entries()` — and attached as the residual yields them. **128 → 119, nothing
      broken**, and the 9 fixed are exactly the tracking-count assertions predicted from the
      classification. ✅ `<this commit>`

      Applied only to split queries under `TrackAll`, so the pass-through path is untouched.
      Two consequences the implementation had to answer: the state manager's identity map only
      holds *tracked* entries, so deferring needs a local one; and an identity hit must still
      walk the row, because its nested nodes carry wire ids later rows reference — returning
      early stranded them as "dangling wire reference".

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

## Phase S — SaveChanges (M3)

- [x] **S1+S2.** The provider can **write**. Client capture (`ChangeEntryMapper`), server replay
      (`ServerSaveChangesExecutor`), store-generated values returned by correlation id
      (research-findings §9). Insert / update / delete round-trip and store-generated keys are
      proven on Tier B, where a real store actually generates them. ✅ `<this commit>`

      Two things EF refuses that the design had to accommodate:
      a key cannot be assigned through a *tracked* entry ("the property 'Blog.Id' is part of a
      key and so cannot be modified"), so the object is populated before it reaches the change
      tracker; and store-generated values must be filtered by **state** — returning an inserted
      row's key for a `Modified` entry made the client try to re-key a tracked row.

- [x] **S3a+S3b.** Relationships **and** many-to-many, by one mechanism rather than two.
      A temporary key value travels and is **flagged** as temporary; the server marks it
      temporary on its own entries, and EF then does what it does on the client — a principal and
      its dependents share the value, and every occurrence is replaced by the real key.
      ✅ `<this commit>`

      I built the correlation-id navigation-link mechanism first (§9's literal reading) and it
      worked. Then the temporary-value approach turned out to subsume it: probing by disabling
      the links left every test green, so the links were deleted. One mechanism, and it is EF's
      own rather than a parallel one.

      Two things a join entity needs that nothing else does: its properties are the
      `Item[string]` **indexer**, so `SetValue` without an index is a parameter-count mismatch;
      and `DbContext.Entry` resolves by CLR type, which cannot tell two shared-type entities
      apart. Both covered, with M2M between two *new* entities — where both of the join row's
      foreign keys are temporary — and between two existing ones, where the join entity is the
      only changed entry in the request.

- [x] **S3c.** Concurrency tokens (`SerializedOriginalValues` is on the wire and unused), and the
      SaveChanges / change-tracking spec bases. ✅ `<this commit>`

      Concurrency tokens travel and are checked (S3c-13). Six change-tracking bases are adopted:
      `GraphUpdatesTestBase`, `PropertyValuesTestBase`, `FindTestBase`, `LoadTestBase` and
      `ManyToManyTrackingTestBase` on Tier A, `OptimisticConcurrencyTestBase` on Tier B. The
      suite went from `Total tests: 5237, Failed: 29` at the start of Phase S to
      **`Total tests: 11024, Passed: 10310, Failed: 685, Skipped: 29`** — 5787 tests added, and
      every remaining failure classified in the tables below.

      Two things this milestone found that outgrew it, both of which want their own place in
      `roadmap.md` rather than another S3c substep: **lazy loading**, unimplemented and
      accounting for 505 of the 685 failures on its own, and **Tier B beyond the query bases**,
      where S3c-17 showed the recorded blocker was the wrong one.

- [x] **S3c-1.** Transactions are *ignored*, not refused. ✅ `<this commit>`

      The plan was to adopt `GraphUpdatesTestBase` first and read the red wave. Counting the
      dependency first changed the order: **every one** of GraphUpdates' 127 tests, all 46 of
      `ManyToManyTrackingTestBase`, all 118 of `StoreGeneratedFixupTestBase`, all 60 of
      `StoreGeneratedTestBase` and all 21 of `UpdatesTestBase` drive their mutations through
      `TestHelpers.ExecuteWithStrategyInTransactionAsync`, whose first statement is
      `Database.BeginTransactionAsync()`. Against a manager that threw, adopting GraphUpdates
      would have produced 127 identical failures measuring one thing, and SaveChanges — which
      works — would have been hidden behind a feature scheduled for M4.

      So `InfoCarrierTransactionManager` now does what EF Core's own `InMemoryTransactionManager`
      does: returns a stub and raises `InfoCarrierEventId.TransactionIgnoredWarning`, which
      `UseInfoCarrier` defaults to `WarningBehavior.Throw`. The default is still a hard failure;
      what changed is that a caller can *opt into* the no-op. This is not M4 — M4 is remoting
      begin/commit/rollback with the W3 token — it is the statement that the provider ignores
      transactions today, which is true.

      **29 → 29, nothing fixed, nothing broken** (`Passed: 5195, Failed: 29, Skipped: 13,
      Total: 5237`), because nothing in the suite began a transaction yet. A count that does not
      move cannot tell a working stub from an uncalled one, so `TransactionIgnoredTest` asserts
      the behaviour directly: throws by default, returns a stub when opted in, never reports
      itself as `CurrentTransaction`, and never contacts the server. **4 of 4 pass.**

- [x] **S3c-2.** `GraphUpdatesTestBase` adopted on Tier A. ✅ `<this commit>`

      **+1787 tests. `Passed: 6638, Failed: 377, Skipped: 13, Total: 7028`** — the 29 pre-existing
      failures are the same 29, nothing outside the new class broke, and 1439 of the 1787 new
      tests pass. The store-limitation overrides are EF Core's own `GraphUpdatesInMemoryTestBase`
      overrides mirrored one for one; re-test each against Tier B and delete it there where it
      passes.

      The class opened at **1550 failing of 1787** and three provider defects — none of them in
      SaveChanges — accounted for almost all of it:

      1. **`TypeAllowlist` denied every model entity type that is a constructed generic.**
         `ForModel` adds each entity's CLR type verbatim, but `Evaluate` decomposed a constructed
         generic into definition-plus-arguments and asked whether the *open* definition was
         listed, which it never is. The EF specification suites nest their models inside a
         generic test base, so `Root` is really `GraphUpdatesTestBase<TFixture>+Root` and the
         query root itself was unshippable. An exact match now wins before decomposition.
         **1550 → 1461.**
      2. **`TypeNodeResolver` judged each generic argument on its own.** `Root[InfoCarrierFixture]`
         names the fixture, and the fixture is on no allowlist. A generic argument is part of a
         name and nothing is ever constructed from one, so it is now judged as part of the type
         it appears in; the constructed type still has to clear the list.
      3. **Shadow properties could not be read at all.** `MapRowMembers` went through
         `property.GetGetter()`, which for a shadow property does not return null — it throws
         "no backing field could be found ... and the property does not have a getter". Every TPH
         discriminator in the model hit it. The value lives in the entry, so the server now reads
         it from the state manager. **1461 → 348.**

      The first two were invisible until something asked: the whole suite's models are
      non-generic and shadow-property-free. The diagnosis cost a run of its own because the
      splitter's "no part of the query can be executed" message *guessed* at the cause ("this
      usually means the query root names a type the server does not know") and guessed wrong;
      it now names the offending node and type.

      **Recorded, not fixed:** the first full run reported 1075 failures with both SQLite
      Northwind classes wholly red; rerun unchanged, they pass, and in isolation they are at
      baseline. The shared-name SQLite backend store is not safe under the parallel load 1787
      more tests create. Until that is fixed, a single `measure.sh` run can invent 698 failures.

- [x] **S3c-3.** A TPH row travels as what it *is*, not as what its navigation declares.
      ✅ `<this commit>`

      A result row was mapped by `value.GetType()`, but an entity reached through a navigation
      was mapped by `navigation.ClrType`. In a TPH hierarchy those differ: `Root.OptionalSingle`
      is declared `OptionalSingle1`, so a loaded `OptionalSingle1Derived` went onto the wire
      named as its own base, and the client materialized the base — "Unable to cast object of
      type 'OptionalSingle2' to type 'OptionalSingle2Derived'". `MapToNode` now prefers the
      instance's own entity type whenever the model knows it and it is assignable to the
      declared one, which covers rows, references and collection elements by one rule.

      **GraphUpdates 348 → 200. Suite `Passed: 6786, Failed: 229, Skipped: 13, Total: 7028`.**
      The 29 pre-existing failures are unchanged and nothing outside the class moved.

- [x] **S3c-5.** The Tier B store is file-backed, as EF Core's own is. ✅ `<this commit>`

      S3c-2 recorded a full run that reported 1075 failures with both SQLite Northwind classes
      wholly red, passing on rerun and in isolation. The mechanism, once EF's `SqliteTestStore`
      was read rather than guessed at:

      `NorthwindWhereQuerySqliteInfoCarrierTest` and `NorthwindSelectQuerySqliteInfoCarrierTest`
      take the same fixture **type**, so xUnit builds one fixture **instance per class** and both
      ask for the store named `Northwind`. Ours opened `Mode=Memory;Cache=Shared` and held one
      connection open for the store's lifetime, because an in-memory SQLite database is destroyed
      when its last connection closes. The first store created and seeded it; the second skipped
      creation (the `Created` guard) and then queried a database the first had already destroyed
      by disposing. Adding 1787 GraphUpdates tests changed the scheduling enough to expose it.

      EF avoids all of this three ways, and none of them is a parallelism setting — its SQLite
      suites run fully parallel with no `xunit.runner.json`:

      1. `SqliteTestStore` uses `DataSource = name + ".db"` with `Cache=Private`. A file's
         lifetime is its own; closing a connection destroys nothing.
      2. `SqliteNorthwindTestStoreFactory.GetOrCreate` **ignores the requested name** and returns
         `SqliteTestStore.GetExisting("northwind")`, whose `seed: false` makes `InitializeAsync`
         return on its first line. `northwind.db` is a 946 KB file checked into the repo.
      3. The query suites never write, and concurrent readers on a private-cache file are safe.

      Adopted (1) and, by consequence, (3). Not (2) — a checked-in binary would have to be
      rebuilt whenever the model changes, and the once-per-file guard already gives us
      seed-once. The held connection is gone with the reason for it, so contexts no longer share
      a single `SqliteConnection` either; unshared stores get a file of their own and delete it
      on disposal, which the smoke tests need.

      **Verified by three consecutive full runs with identical counts and identical failure
      sets: `Passed: 6786, Failed: 229, Skipped: 13, Total: 7028`.** `Northwind.db` in the
      output directory and zero leftover per-test files confirm the new path actually ran.

- [x] **S3c-10.** `PropertyValuesTestBase` adopted on Tier A. ✅ `<this commit>`

      **+196 tests, 143 of them passing immediately. `Total tests: 7228, Passed: 7084,
      Failed: 127, Skipped: 17`** — nothing outside the new class moved. The store-limitation
      overrides are EF Core's own `PropertyValuesInMemoryTest` overrides, mirrored one for one
      (complex types and complex collections, which the InMemory backend does not support).

      Chosen ahead of `OptimisticConcurrencyTestBase` even though the latter is the obvious
      harness for concurrency tokens: **EF's own InMemory suite `[Skip]`s 16 of that base's
      tests** — "Optimistic Offline Lock #2195", "#23569" — because the InMemory store cannot
      detect a concurrency conflict at all. Adopting it on Tier A would prove nothing about
      tokens; it needs Tier B, and its F1 fixture needs lazy-loading proxies, an externally
      built model and interceptors. `PropertyValuesTestBase` reads exactly what
      `SerializedOriginalValues` has to carry, needs no transactions, and cost one afternoon.

      One infrastructure gap surfaced immediately and is fixed here: a fixture's `AddServices`
      configures the **client** provider, and `PropertyValuesFixtureBase` registers a
      materialization interceptor and then asserts *in its seed* that the interceptor ran — but
      the seed executes against the **server**. All 196 tests failed on
      `KeyNotFoundException: 'CreatedCalled'` before the server provider could be given the same
      services (`SharedTestStoreProperties.OnAddServices`).

      **The 53 remaining are one family**: `Store_values_*` and
      `Values_can_be_reloaded_from_database_*` — `GetDatabaseValues()` and `Reload()`, which
      read the row as the store currently holds it. Nothing in the provider answers that yet.

- [x] **S3c-11.** A constant declared as `object` keeps the type of what it holds.
      **127 → 78 failures; 49 fixed, 0 broken.** ✅ `<this commit>`

      `GetDatabaseValues()` and `Reload()` returned `null` for every entity whose key is a
      **non-numeric value type**, which is the whole `Store_values_*` /
      `Values_can_be_reloaded_from_database_*` family left by S3c-10.

      `EntityFinder.GetDatabaseValuesQuery` is an ordinary LINQ query, and
      `ExpressionExtensions.BuildPredicate` builds its `Where` two different ways. A numeric or
      `bool` or enum key compares typed. Anything else — a `Guid` here — becomes
      `Equals(EF.Property<object>(e, "BuildingId"), keyValues[0])`, where the index expression
      into the `ValueBuffer` is typed **`object`**. Parameter extraction turns that into
      `Expression.Constant(guid, typeof(object))`.

      `ConstantNode` recorded only the declared type, and the compact primitive form carries no
      type of its own, so the server rebuilt a `string` and the comparison was **quietly false**:
      no row, `FirstOrDefault` → `null`, and the tests failed on a `NullReferenceException` far
      from the cause rather than on an error. The node now carries the value's own type
      alongside the declared one; the declared type still governs the constant, because
      `object.Equals(object, object)` rejects a `Guid`-typed operand.

      Found by dumping the shipped query — the smoke test that already covered
      `GetDatabaseValues` uses an `int` key and takes the other branch, so nothing in the suite
      could have caught this. `Constant_boxed_in_object_keeps_the_value_type` now does.

- [x] **S3c-12.** A harness for concurrency tokens, on Tier B. **`Total tests: 7231,
      Passed: 7135, Failed: 79, Skipped: 17`** — one new failure, which is the point. ✅ `<this commit>`

      `ConcurrencyTokenTest` states the contract in both directions against SQLite, the only
      tier that can show it (EF's InMemory provider performs no concurrency check at all, which
      is why EF's own `OptimisticConcurrencyInMemoryTest` skips sixteen tests):

      - **`A_stale_write_is_refused` passes today.** A client that leaves the token alone sends
        the right value as its original, so the check is made correctly by accident.
      - **`A_client_that_bumps_the_concurrency_token_is_not_a_conflict` fails today** with a
        *spurious* `DbUpdateConcurrencyException` — "expected to affect 1 row(s), but actually
        affected 0". Bumping the token is the whole point of an application-managed one, and the
        server sends the new value as its own original, so the `WHERE` matches nothing.

      That is the gap `SaveChangesRequest.SerializedOriginalValues` exists to close: the server
      rebuilds each entity from current values, attaches it and sets `Modified`, and an entry
      attached that way has `OriginalValues == CurrentValues` by construction.

      **The store had to be fixed first, and the harness is what found it.** S3c-5 made the Tier
      B database a file so that disposal order would stop being load-bearing — and then deleted
      that file on disposal, which put the coupling straight back: the test that ran next got
      "no such table" despite having created and seeded a file of its own. Nothing is deleted on
      disposal now. Files from previous runs are swept once at startup, where every file present
      is stale by construction, and each store still deletes and recreates its own at
      initialization.

      Worth recording how that was diagnosed, because two intermediate readings were wrong: the
      store probe showed both files created with a `Widgets` table, which looked like it
      exonerated the store; and when cleanup was disabled the run still reported one failure, so
      it looked unchanged. It was not — the *reason* had changed from "no such table" to the
      concurrency exception. Counting failures rather than reading them cost a round trip.

- [x] **S3c-13.** Concurrency tokens travel. **`Total tests: 7231, Passed: 7136, Failed: 78,
      Skipped: 17`** — FIXED 1 (`A_client_that_bumps_the_concurrency_token_is_not_a_conflict`),
      BROKEN none. ✅ `<this commit>`

      `SaveChangesRequest.SerializedOriginalValues` is now written and read, which is the named
      S3c deliverable. The client sends the original value of every concurrency token on a
      `Modified` or `Deleted` entry — an `Added` one has nothing to conflict with, and EF answers
      `GetOriginalValue` with the current value there anyway. The server applies them through
      `entry.Property(name).OriginalValue` as the *last* thing it does to an entry, because
      setting the state re-snapshots originals from the entity and would undo an earlier write.

      Only concurrency tokens travel. Every other original is either unused by the store or
      equal to the current value by construction, and sending the lot would cost a second full
      copy of every entry on the wire.

      **A second defect had to be fixed first, and it is the more serious of the two.**
      `InfoCarrierDatabase.CompileQuery` captured `_client` in the delegate it returned, and EF
      caches that delegate in `ICompiledQueryCache` — a singleton of the *internal* service
      provider, which is shared by every context with the same options shape. Ours has
      `GetServiceProviderHashCode() => 0`, but so does `RelationalOptionsExtension`, and EF's
      InMemory extension hashes only a nullability flag: no EF provider distinguishes its service
      provider by which store it talks to, because no EF provider bakes the connection into the
      cached delegate. Ours did. The consequence is that the first context to compile a given
      query pins it to *its* server, and every later context running that query ships it there
      while its own SaveChanges goes elsewhere — two clients against two servers being the
      ordinary case this provider exists for. The client is now resolved from
      `queryContext.Context` at execution time.

      It surfaced as the two concurrency tests passing alone and failing together, which read
      like the store flakiness of S3c-5 and was not. The probe settled it in one run: the bump
      test was sending `Version.orig=99` — the *other* test's data — while its server held
      `Version=1`. A client reading one database and writing another is not a store problem, and
      no amount of staring at the store would have found it.

- [x] **S3c-14.** `OptimisticConcurrencyTestBase` adopted on Tier B. **`Total tests: 7276,
      Passed: 7159, Failed: 88, Skipped: 29`** — 45 new tests, of which 23 pass, 12 are EF's own
      skips and 10 fail. BROKEN outside the new class: none. ✅ `<this commit>`

      Tier B only, and deliberately: EF's own `OptimisticConcurrencyInMemoryTest` skips sixteen
      of these because InMemory performs no concurrency check, so running them on Tier A would
      assert InMemory's limits rather than this provider's. The eleven skips adopted here are
      EF's `OptimisticConcurrencySqliteTestBase` skips, mirrored one for one, because the backend
      *is* SQLite.

      Three pieces of infrastructure were needed, each worth naming:

      - **`InfoCarrierTestHelpers`.** `F1FixtureBase` builds its model *externally* — on purpose,
        as EF's regression coverage for building a model away from a context — via
        `TestHelpers.CreateConventionBuilder()`. Every provider supplies its own; this is ours.
      - **The server gets its own copy of the model.** `F1Context` has no `OnModelCreating` at
        all, so the usual route would have left the server with a bare convention model and none
        of the concurrency tokens. The server model is built over SQLite's convention set so it
        carries the relational annotations the client's must not have. The relational
        configuration is EF's `F1RelationalFixture.BuildModelExternal`, duplicated rather than
        inherited because that class ships in `Relational.Specification.Tests`, which a
        non-relational provider has no business referencing.
      - **The server gets its own seed.** `F1FixtureBase` seeds through `UseSeeding` on the
        *client* options, which the server never sees. Without it every row was missing.

      **The seed fix is the clearest case yet for reading reasons and not counts.** Adding it
      left the failure count at exactly 21 — and changed every single failure. Before: 19 ×
      "sequence contains no elements", an empty database. After: 21 × "type
      `Driver+DriverProxy` is not on the deserialization allowlist", which is a different defect
      entirely and the one that mattered.

      That defect: `DynamicValueMapper.MapToNode` asked the model for `FindEntityType(type)`, and
      the CLR type in hand may be a **proxy**. A materialization interceptor produces one, and
      the collection branch maps each item by `item.GetType()`, so `Driver+DriverProxy` travelled
      as the declared type and was refused by the allowlist (ADR-008). It now asks
      `FindRuntimeEntityType`, which walks base types — EF's own helper, for exactly this reason
      — and the entity's own CLR type is what goes on the wire. 21 → 10 in the class, nothing
      broken anywhere else.

      A per-test reseed (xUnit's `IAsyncLifetime`) stands in for the rollback this provider
      cannot do: every test here wraps its work in a transaction, transactions are ignored
      (roadmap M4), so without it the first test to delete a row left it deleted.
      `GraphUpdatesInfoCarrierTest` reseeds for the same reason.

      **The 10 remaining, classified:**

      | # | Family | Diagnosis |
      |---|---|---|
      | 4 | `Calling_Reload_on_owned_entity_works`, `Calling_GetDatabaseValues_on_owned_entity_works` | "The property `SponsorDetails.TitleSponsorId` is part of a key and so cannot be modified" — the **owned-type key family**, the same root cause as the 14-test family in the `GraphUpdates` residual below. One fix serves both. |
      | 4 | `Deleting_the_same_entity_twice`, `Deleting_then_updating_the_same_entity` (×2), `Concurrency_issue_where_the_FK_is_the_concurrency_token_can_be_handled` | The behaviour is right — the `DbUpdateConcurrencyException` is thrown and caught. What fails is `Fixture.ListLoggerFactory.Log.Single(l => l.Id == CoreEventId.OptimisticConcurrencyException)`: the exception is raised inside the *server's* `SaveChanges`, so the **server's** logger records the event and the client's never does. The client would have to log it on receiving a transported concurrency failure. |
      | 1 | `Nullable_client_side_concurrency_token_can_be_used` | `Assert.IsType<Sponsor.SponsorDoubleProxy>` — the client materializes rows itself and does not run `IMaterializationInterceptor`, so it produces a plain `Sponsor`. Now that the wire strips proxies (above), the client is the only place one could be re-created. |
      | 1 | `Attempting_to_delete_same_relationship_twice_for_many_to_many` | No exception thrown where `DbUpdateConcurrencyException` is expected. Independent-association concurrency on a join row; unclassified. |

- [x] **S3c-15.** `FindTestBase`, `LoadTestBase` and `ManyToManyTrackingTestBase` adopted on
      Tier A. **`Total tests: 11024, Passed: 10289, Failed: 706, Skipped: 29`** — 3748 new tests,
      3130 of them passing. BROKEN outside the three new classes: none. ✅ `<this commit>`

      Three thin fixtures, each mirroring EF's own InMemory adoption; all three compiled and ran
      without a single provider change. One checkbox, one commit: the three are one queue item
      and no intermediate state between them means anything.

      - **`FindTestBase` — 411 of 411 pass.** Worth having anyway: `Find` is the one read that
        may never reach the server, since a tracked entity is answered from the client's change
        tracker and only a miss becomes a query. Both halves work.
      - **`LoadTestBase` — 2630 of 3137 pass.**
      - **`ManyToManyTrackingTestBase` — 89 of 200 pass.** Reseeds after each test and sets
        `SupportsDatabaseDefaults => false`, both exactly as EF's InMemory version does.

      **Lazy loading is not implemented, and that is the whole `Load` residual.** 505 of the 507
      failures are `Lazy_load_*`, and *no* lazy test passes — the navigation is simply never
      populated (`Assert.NotNull(parent.Children)` on a null collection). The other two are
      `Fixup_reference_after_FK_change_without_DetectChanges` and its one-to-one twin. A single
      named feature, not a long tail: it is the largest unimplemented thing this suite now knows
      about, and it deserves its own milestone rather than a slot in S3c.

      **The `ManyToManyTracking` residual — 111 of 200**, classified:

      | # | Symptom | Diagnosis |
      |---|---|---|
| 60 | `ArgumentException: An item with the same key has already been added`, thrown from `InMemoryTable.Create` | The server inserts a row whose key already exists. Every one is a `Can_insert_many_to_many*` test, and those tests deliberately add **the same relationship from both ends** — EF's own source marks the second one `// 21 - 11 (Dupe)` — which EF is expected to collapse into a single join row. The keys are the tests' own explicit sentinels (7711/7721), not generated, so this is not the stable-value-generator story an earlier version of this table guessed at. Start by counting the entries the client sends for one of these saves against the rows the server inserts. |
      | 21 | `Assert.Equal` values differ | Unclassified. |
      | 6 | "No backing field could be found for property `UnidirectionalEntityTwo.…`" | Unidirectional skip navigations, whose join key lives in shadow state with no CLR member. |
      | 5 | `JsonException: A possible object cycle was detected` | **A serializer defect, and the only one here that is squarely ours.** A cyclic entity graph reaches `System.Text.Json` without the reference handling the rest of the wire format uses. |
      | 4 | `Sequence contains no matching element` | Unclassified. |
      | 2 | "Unable to track an entity of type `EntityCompositeKeyEntityRoot (Dictionary<string, object>)`" | A shared-type join entity with a composite key. |
      | 2 | `ProxyableSharedType` | Shared-type entity behind a proxy; adjacent to the `FindRuntimeEntityType` fix in S3c-14. |

- [x] **S3c-16.** Shadow state goes on before the state does. **`Total tests: 11024,
      Passed: 10310, Failed: 685, Skipped: 29`** — FIXED 21, BROKEN none. ✅ `<this commit>`

      One reorder in `ServerSaveChangesExecutor.TrackOne`: apply the entry's shadow values while
      it is still `Detached`, then set the state. S3c-9 had already established that a key must
      be written before EF can call it a key — "the property `SponsorDetails.TitleSponsorId` is
      part of a key and so cannot be modified" — and moved every write that goes onto the CLR
      object ahead of tracking. Shadow state had no CLR member to receive, so it stayed behind,
      and an *owned* dependent's key is exactly that: its owner's key, in shadow.

      It paid three times over. The 17 `GraphUpdates` owned tests it was aimed at, the 4
      `OptimisticConcurrency` owned tests S3c-14 had classified as the same root cause — one fix
      serving both, as predicted — and the two `Discriminator_values_are_not_marked_as_unknown` /
      `Saving_unknown_key_value_marks_it_as_unmodified` pairs, which the plan had recorded as an
      unrelated "shadow-state round trip" family and which were the same bug seen from the other
      end.

## Phase L — lazy loading

- [x] **L1.** Entities are built by EF's materializer, so they have an `ILazyLoader`.
      **`Total tests: 11024, Passed: 10406, Failed: 589, Skipped: 29`** — FIXED 347, BROKEN 251,
      of which 250 are other parameterizations of the `Lazy_load_*` family that was entirely red
      before. ✅ `<this commit>`

      `ClientResultMaterializer` built every entity with
      `Activator.CreateInstance(entityType.ClrType, nonPublic: true)`, which bypasses EF's entity
      materializer — so constructor binding and service-property injection never ran and **every
      service property on every entity was null**. A probe over the `Load` model, before any
      change:

          Parent                             count=1 Loader:ILazyLoader=NULL
          ParentFullLoaderByConstructor      count=1 _loader:ILazyLoader=NULL
          ParentDelegateLoaderByProperty     count=1 LazyLoader:Action`2=NULL

      With no loader, `parent.Children` returns null and nothing ever loads. That is why *zero*
      lazy tests passed rather than some fraction.

      Both paths now go through the materializer, differently and deliberately:

      - **Tracked**: `IStateManager.CreateEntry(values, entityType)`, which is
        `entityType.GetOrCreateMaterializer(...)` plus an entry. This is what v1 did; see
        `InfoCarrierQueryResultMapper.TryMapEntity`.
      - **Untracked**: the materializer *without* an entry, reproducing EF's own no-tracking
        path. Using `CreateEntry` here instead was measured and is wrong: it registers the entry
        in the state manager's reference map, and a second untracked instance then collided with
        the first the moment anything attached one — 109 identity conflicts.

      **One further fix was needed and it was not obvious.** Assigning a navigation on a tracked
      entity wakes EF's `NavigationFixer`, which attaches the whole graph reachable from what was
      assigned; hand it a second instance of an already-tracked row and it throws. That path had
      never been reachable, because lazy loading had never worked. `ResolveAgainstTracker` now
      swaps a freshly materialized navigation target for the tracked instance with the same key.
      That single change took the suite from 720 to 589.

      **The measurement sequence, because the count was misleading at every step but the last:**

      | Attempt | Failed | What it showed |
      |---|---|---|
      | tracked path only | 1040 | Worse than baseline — but lazy loading was now *attempting* to load, which it never had |
      | + untracked via `CreateEntry` | 736 | Better, and revealed the identity-map collision |
      | + untracked without an entry | 720 | Collision from untracked entries gone |
      | + `ResolveAgainstTracker` | 589 | Below the 685 baseline |

      Judged on the count alone, the first attempt would have been reverted and the whole line of
      work abandoned.

      **Residual: 424 `Lazy_load_*` still fail** (from 505), and there is exactly **one genuine
      regression outside the family** — `OptimisticConcurrencyInfoCarrierTest.Attempting_to_add_
      same_relationship_twice_for_many_to_many_results_in_independent_association_exception`.
      Non-lazy `Load` failures are unchanged at 2. `PropertyValues` went 16 → 0, which was not a
      goal: those tests read current, original and store values through a property dictionary,
      and an entry built by EF's materializer simply has them.

      **The 80 "Object of type `Parent` cannot be converted to type `SinglePkToPk`" failures are
      fixed by L2 below.** The trail is kept because the wrong turns in it are instructive:

          MISMATCH owner=Parent nav=Single       wants=Single       got=Parent nodeRef=1
          MISMATCH owner=Parent nav=SinglePkToPk wants=SinglePkToPk got=Parent nodeRef=1
          REF 1 -> Parent (tableSize=1)

      Every one is `Ref = 1` — the *principal's own wire id*. The server is emitting a
      one-to-one navigation as a **back-reference to the entity that holds it**, so the client
      dutifully assigns the `Parent` to `Parent.Single`. The bug is in outbound mapping in
      `DynamicValueMapper.ToDynamicValue`, not in decoding: `_toIds` is
      `ReferenceEqualityComparer`-keyed, so a shared key value cannot explain it and that lead
      is dead.

      Three other candidates were measured and are all neutral — do not re-run them: reading
      node-valued properties before the wire id is registered; a type guard on
      `ResolveAgainstTracker`; and materializing the whole result before yielding (the
      hypothesis that a lazy load resets the reference scope mid-decode — it does not apply,
      because `Set<Parent>().Single()` is fully enumerated before the test touches a
      navigation).

- [x] **L2.** A query can nest inside another one. **`Total tests: 11024, Passed: 10509,
      Failed: 486, Skipped: 29`** — FIXED 103, BROKEN none. ✅ `<this commit>`

      `ClientResultMaterializer.Materialize` assigned `mapper.EntityMaterializer` and never put
      it back, and the mapper is DI-scoped and shared. That was harmless while a query was the
      only kind of exchange — but a lazy load is a whole exchange issued *while an outer result
      is still being decoded*, and it replaced the outer materializer with its own for the rest
      of the outer decode. The inner one carries a different tracking behaviour, a different
      deferred-identity map and a different reference scope, which is how a `Parent` ended up
      assigned to `Parent.Single`.

      The hook is now saved and restored, and rows are decoded to completion before any is
      handed out — with `yield return`, the caller could start a nested exchange part-way
      through this one, and by then the hook had already been swapped. Nothing extra is
      buffered: the payload is deserialized into a list of nodes up front regardless.

      This removed **all 80** of the type-mismatch failures.

      **How it was found, since three earlier attempts on the same symptom were neutral.** The
      wire was dumped and searched: across 4664 payloads, *zero* had a navigation encoded as a
      top-level back-reference — so the serializer was exonerated outright and the fault had to
      be in decoding. Probing the failing assignment then gave `rowId=2 rowType=Parent
      nav=Single memberRef=1`, a nested `Parent` inside a response rooted elsewhere whose
      back-reference to wire id 1 resolved to the wrong object. Only a decode running against
      another exchange's state can do that.

- [x] **L3.** An untracked entity records its own loaded navigations.
      **`Total tests: 11024, Passed: 10649, Failed: 346, Skipped: 29`** — FIXED 140,
      BROKEN none. ✅ `<this commit>`

      `PopulateNavigations` recorded loaded state with `entry?.SetIsLoaded(navigation)`, and an
      untracked entity has no entry — so every navigation the server had already sent looked
      unloaded. The loader then fetched it again, and the tests that ask directly were simply
      told `false`; that was the 142 `Assert.True` and 91 `Assert.False` failures.

      An untracked entity does have somewhere to keep it: the `ILazyLoader` injected into it,
      which L1 made possible by materializing through EF. `ILazyLoader.SetLoaded` is what v1
      used, in its `SetIsLoadedNoTracking`.

- [x] **L4.** The store is reseeded after a *failing* test too. **`Total tests: 11024,
      Passed: 10650, Failed: 345, Skipped: 29`** — FIXED 1, BROKEN none. ✅ `<this commit>`

      A near-flat count that changed the meaning of 59 failures, so read the reasons and not the
      number: `ArgumentException: An item with the same key has already been added` went from 72
      occurrences to 22, and `Assert.Equal: Values differ` from 42 to 101. The same tests fail;
      they now report **their own** problem instead of the previous test's leftovers.

      `GraphUpdatesInfoCarrierTest` and `ManyToManyTrackingInfoCarrierTest` reseed after
      `ExecuteWithStrategyInTransactionAsync`, because this provider ignores transactions and
      nothing rolls back. That reseed was not in a `finally`, so a test that *failed* skipped it
      and left its rows for the next parameterization — which then failed on a duplicate key it
      had nothing to do with.

      **What that exposed about the ManyToMany residual, and it is not what S3c-15 recorded.**
      The real failure is `Can_insert_many_to_many_with_navs` asserting `Expected: 11, Actual: 6`
      when a *fresh context* re-queries: 3 left entities + 3 right = 6 arrive, and the **5 join
      rows do not**. Skip-navigation join entities are not being persisted. The duplicate-key
      errors were downstream pollution from that, not a story about key generation.

- [x] **L5.** `LazyLoadProxyTestBase` adopted on Tier A. **`Total tests: 11344, Passed: 10891,
      Failed: 424, Skipped: 29`** — 320 new tests, 241 passing. BROKEN outside the new class:
      none. ✅ `<this commit>`

      Lazy loading through Castle proxies rather than through an injected `ILazyLoader` — a
      different mechanism from the one L1–L3 fixed, and the one v1 covered with this same base.
      `Microsoft.EntityFrameworkCore.Proxies` and `Castle.Core` arrive transitively with the
      specification-tests package, so this adds no dependency and does not touch ADR-001. The
      `Ignore` calls in the fixture are EF's own InMemory fixture one for one: the backend *is*
      the InMemory store, which has no complex types.

      **It also fixed a latent defect in the backend store.** `InfoCarrierBackendTestStore`
      registered `AddScoped<DbContext>(...)` unconditionally, which is right when the fixture's
      context is a subclass and wrong when it *is* `DbContext` — as
      `LazyLoadProxyTestBase.LoadFixtureBase` is. It re-registered `DbContext` as scoped over the
      transient registration `AddDbContext` had just made, and every test in the class failed
      identically, before any of them ran, with "cannot resolve scoped service 'DbContext' from
      root provider". Registered only for a subclass now.

      **The 79 residual** is the same shape as `LoadInfoCarrierTest`'s and should be worked with
      it, not separately: `Lazy_load_*_reference_to_principal` dominates, plus the shadow-FK
      family ("relationships using shadow values can only be loaded for tracked entities") which
      is EF's own designed refusal.

- [x] **L6.** A navigation is read through its backing field, never its property.
      **`Total tests: 11344, Passed: 11127, Failed: 188, Skipped: 29`** — FIXED 236,
      BROKEN none. ✅ `<this commit>`

      `ClearPlaceholderReferencesBlockingFixup` read each reference navigation with
      `PropertyInfo.GetValue` to decide whether a constructor-set placeholder was blocking fixup.
      On a lazy-loading entity **that read is the lazy load**: it fired a query during
      materialization and left the navigation marked loaded, so a test that had queried only the
      dependent was told its principal reference was already loaded. It then nulled the value it
      had just caused to be fetched.

      Harmless until L1, because a reflection-constructed entity has no loader and its getter is
      an ordinary property read. Once entities are built by EF's materializer every lazy-loading
      shape reaches this code, and only a backing-field read is free of side effects.

      This was the whole of the `Assert.False(IsLoaded)` family and most of what surrounded it:
      **`LoadInfoCarrierTest` 183 → 12, `LazyLoadProxyInfoCarrierTest` 79 → 15.** Lazy loading
      began this session at 505 of 505 failing and is now 27 of 825.

      Found by bisecting the materialization stages with a probe — `beforeClear=[]`,
      `afterClear=[Parent]` — which named the exact statement. Guessing had already cost three
      neutral attempts on the same symptom.

- [x] **L7.** A loaded skip navigation tracks its join rows. **`Total tests: 11344,
      Passed: 11142, Failed: 173, Skipped: 29`** — FIXED 15, BROKEN none. ✅ `<this commit>`

      **This corrects the diagnosis L4 recorded, which was wrong.** L4 said skip-navigation join
      rows were not being persisted. They are: a probe on both sides of the wire shows the client
      sending 11 entries — 6 endpoints and 5 `JoinTwoToThree` — and the server saving 11. What
      fails is the *re-query*: EF expects a loaded skip navigation to leave its join rows in the
      change tracker, and nothing here created them, because a join row is not part of the
      projection and never reaches the wire. Hence eleven expected, six found.

      They are now reconstructed on the client, from the two entities the row links.

      **Only for a join type that is nothing but its two foreign keys.** A join entity with a
      **payload** carries data that exists only in the store, and the first attempt invented an
      entry for those too — which broke three `PropertyValues` tests that had been passing, from
      "no entry" to "an entry whose `Payload` is null". Worse than having none. Those need the
      join row on the wire, which is a change to the result format and is not this step.

- [x] **L8 (superseded by L9).** Two guard relaxations were recorded here as "measured neutral,
      both reverted". **That conclusion was wrong**, and how it was reached matters more than the
      finding: the failing-test *names* were byte-identical before and after, so I compared only
      the names and called the change neutral. The *reasons* had changed completely —
      `Assert.Equal: Expected 11, Actual 6` had become `Assert.True` in
      `VerifyRelationshipSnapshots`. The relaxation had fixed what it was aimed at and exposed
      the next problem behind it. This is the same trap recorded twice already in this file, and
      comparing snapshots of test names is not enough to avoid it — the reasons have to be
      diffed too.

- [x] **L9.** Join rows are tracked, and a materialized navigation updates its relationship
      snapshot. **`Total tests: 11344, Passed: 11186, Failed: 129, Skipped: 29`** — FIXED 44,
      BROKEN none. ✅ `<this commit>`

      Two halves of one thing:

      - **The join-row guard now refuses only a payload with a CLR member.** L7 refused any
        property outside the two foreign keys, which caught `JoinOneToTwoExtraId` — a
        convention-created *shadow* foreign key on the join table, not something a user set.
        `JoinOneToTwo` declares only `OneId`/`TwoId`. A shadow property has no CLR member for
        anyone to have assigned, so leaving it at its sentinel is right; a payload with a CLR
        member still cannot be invented and is still refused.
      - **A navigation populated here now updates the relationship snapshot.** That snapshot is
        what `DetectChanges` compares against, and assigning a navigation through its CLR
        property — which is how a materialized graph is wired — left it empty, so EF saw every
        loaded relationship as newly added. `AddToCollectionSnapshot` per item for a collection,
        `SetRelationshipSnapshotValue` for a reference.

      `ManyToManyTracking` 96 → 52.

- [x] **L10.** `eng/measure.sh` diffs the failure *reasons*, not just the test names.
      ✅ `<this commit>`

      Tooling for the mistake L8 recorded: a change can fix exactly what it targeted and expose
      the next problem in the *same* tests, leaving the name list byte-identical. Two runs were
      read as neutral on that basis and reverted; the reasons had gone from
      `Assert.Equal: Expected 11, Actual 6` to `Assert.True` in `VerifyRelationshipSnapshots`.
      A tallied reason list is now written beside each snapshot and diffed against the baseline,
      and it is printed **even when the fixed and broken lists are both empty** — which is
      precisely the case where it is the only evidence anything happened.

- [x] **L11.** A skip navigation's join rows travel on the wire. **`Total tests: 11344,
      Passed: 11215, Failed: 100, Skipped: 29`** — FIXED 29, BROKEN none. ✅ `<this commit>`

      L10 recorded this as needing a wire-format change and called it a design problem. It is a
      wire-format *addition*, and a small one: `DynamicPropertyValue.IsJoinRows`, one member per
      loaded skip navigation carrying the join rows as ordinary entity rows. The server does not
      have to find them from a navigation — a principal has none to its join rows, `EntityOne`
      declares only `TwoSkip` — so it scans its tracked entries for the join type whose foreign
      key points at the entity. They are there: EF materialized them to build the skip collection.

      That reaches everything reconstruction could not — a CLR payload, and the
      `JoinOneToTwoExtraId` of the `*_suspected_dangling_join` family — because the row itself
      arrives rather than being inferred. `ManyToManyTracking` 52 → 23.

      **Two things the measurement caught that a count would not have.**

      A shared-type join entity is a `Dictionary<string, object>` and cannot travel as a row: it
      decodes as a shape and the dictionary rejects the wire's pairs — *"the value [LeftsId, 1]
      is not of type System.String"*. The first attempt sent every join type and measured
      129 → 127, which looks like progress and was 29 fixed against 27 broken. The server now
      sends rows only for a join type with a CLR class; shared ones are still rebuilt on the
      client, and `TrackJoinEntity` stays for exactly that case.

      Then the two paths collided. Reconstruction runs first — the navigation member precedes
      the join-row member — so it created a stub without the payload, and identity resolution
      kept the stub in preference to the real row arriving behind it. That cost the whole
      `*_suspected_dangling_join` family silently: 116 instead of 100, with the family simply
      absent from the FIXED list rather than present in BROKEN. The two paths are now mutually
      exclusive by name.

- [x] **L12.** A request carries the context it is running against. **`Total tests: 11344,
      Passed: 11221, Failed: 94, Skipped: 29`** — FIXED 6, BROKEN none. ✅ `<this commit>`

      `NorthwindQueryFiltersQueryTestBase` sets `context.TenantPrefix = "F"` and expects eight
      customers; we returned seven. A query filter closes over a **context property**, and
      nothing carried it across: ADR-006 captures the client's tree *before* EF applies query
      filters, so the filter is applied on the far side, by the server's model, reading the
      server's context. The client's value never left the client.

      `SharedTestStoreProperties.CopyDbContextParameters` was written for exactly this and had
      **never been invoked** — declared, assigned by both Northwind fixtures, and dead. Nothing
      read it. The three tests had been failing since the fixture was written.

      `IInfoCarrierClient.QueryDataAsync` and `SaveChangesAsync` now take the client
      `DbContext`, which is what v1's interface did and for the same reason. A transport needs it
      for anything the wire format does not carry but the server must know; the test store uses
      it to copy per-request parameters onto the server context as that context is resolved.

- [x] **L13.** A row replaced in place travels with the row it replaces. **`Total tests: 11344,
      Passed: 11226, Failed: 89, Skipped: 29`** — FIXED 5, BROKEN none. ✅ `<this commit>`

      `Update_root_by_collection_replacement_of_*` failed with "an item with the same key has
      already been added" from `InMemoryTable.Create`. The tests replace a root's collection with
      instances carrying the keys it already held, so an `Added` entry and a `Deleted` one share
      a key.

      EF pairs those through `IUpdateEntry.SharedIdentityEntry` and hands `IDatabase` **only the
      `Added` half** — confirmed by probe: the client's tracker held eight entries including
      `FirstLaw:Deleted[11]`, and `SaveChanges` was given four, all `Added`. The store is
      expected to notice the pair and replace rather than insert; EF's InMemory provider deletes
      `SharedIdentityEntry`'s row first (`InMemoryStore.ExecuteTransaction`).

      None of that pairing reaches the wire, so the server saw a bare `Added` for a key the store
      already had. The deleted half is now sent alongside, which restores the behaviour with **no
      new wire concept**: the server already replays `Deleted` before `Added` (S3c-6), and its own
      state manager re-pairs them on the key.

      Correlation ids index the sent list, so the expansion is materialized once and the same
      list is used to apply the generated values back — indexing the original would have written
      each store-generated key onto the wrong entry.

- [x] **L14 (cause found in L15).** The last six `Update_root_by_collection_replacement_of_*` —
      **two attempts measured, both negative, both reverted.** Recorded so they are not repeated.

      After L13 these no longer throw a duplicate key; they assert
      `Expected: 2, Actual: 0` — the replacement rows are gone from the store. **The reasons
      moved even though the names did not**, which is what L10's tooling now shows.

      **The server's shared-identity pairing is correct.** A probe of the server's tracker
      immediately before `SaveChanges` shows every relevant entry marked `*paired`:

          FirstLaw:Added[11]*paired   FirstLaw:Deleted[11]*paired
          SecondLaw:Added[111]*paired SecondLaw:Deleted[111]*paired
          SecondLaw:Added[112]

      So whatever is wrong is not the reconstruction L13 added.

      The hypothesis was EF's **cascade**: it walks navigations first and falls back to matching
      foreign-key values, and this side has no navigations — so a replaced principal's *new*
      children carry the same foreign key as the old ones and looked like cascade targets. Two
      ways of suppressing it were measured, and **both are worse than leaving it alone**:

      1. `CascadeDeleteTiming.Never` **and** `DeleteOrphansTiming.Never` — 89 → 91, and eight new
         "the association between entity types 'SecondLaw' and 'ThirdLaw' has been severed, but
         the relationship is either marked as required or is implicitly required". Orphan
         deletion is load-bearing.
      2. `CascadeDeleteTiming.Never` alone — also 91, the same two broken, nothing fixed. It
         un-fixed `Update_root_by_collection_replacement_of_inserted_first_level`, which L13 had
         just fixed.

      So the cascade is not the cause either. What is left to check is the *order* the server
      hands its entries to the store, and whether `SecondLaw:Added[112]` — the only unpaired new
      row — survives the save at all.

      **Both readings above were wrong, and in the same way.** The cascade *is* what deletes the
      replacements — but suppressing it is the wrong lever, and "this side has no navigations" was
      an assumption never checked. L15 checked it.

- [x] **L15.** A replaced row's new dependents are fixed up to the row that survives.
      **`Total tests: 11344, Passed: 11232, Failed: 83, Skipped: 29`** — FIXED 6, BROKEN none.
      ✅ `<this commit>`

      The server tracks every `Deleted` entry before any other, which `IdentityMap.Add` requires
      (L13). Tracking a dependent runs EF's fixup, and fixup finds the principal **by key in the
      identity map** — so for the window in which the `Deleted` half of a replaced row is the only
      entry under that key, a new dependent is wired onto the row about to be *deleted*.
      `StateManager.CascadeDelete` reads exactly that navigation, and an `Added` cascade target is
      `Detached` rather than deleted, so the replacement was silently dropped before the store ever
      saw it.

      A probe of the deleted principal's navigation is what settled it, and it contradicted the
      assumption L14 reasoned from:

          BEFORE | SecondLaw:Added[111]*paired FirstLaw:Added[11]*paired
                 | SecondLaw:Deleted[111]*paired FirstLaw:Deleted[11]*paired ThirdLaw:Deleted[1111]
            fk FirstLaw -> SecondLaw nav=SecondLaw value=count 1 behavior=Cascade
            -> SecondLaw[111] Added => Detached   (StateManager.CascadeDelete)
          AFTER  | FirstLaw:Unchanged[11]

      `count 1` is the whole finding: the deleted principal *did* have a navigation, holding the
      replacement. Reading `GetDependentsFromNavigation` and concluding from the model that the
      collection was empty — these entities initialize theirs to an `ObservableHashSet` — was a
      third wrong reading, avoided only because the probe ran first.

      The fix is an ordering, not a suppression: the non-deleted group is now seeded principal-first
      by depth in the model's foreign key graph, so the `Added` principal is in the identity map
      before its dependents arrive. That is the same direction the placeholder rule already
      required, so it constrains nothing that was not already constrained.

- [x] **L16.** An `Include`d dependent is attached with the principal that carried it.
      **`Total tests: 11344, Passed: 11234, Failed: 81, Skipped: 29`** — FIXED 5, BROKEN 3,
      and the 3 are convergence rather than regression (below). ✅ `<this commit>`

      `Set<QuizTask>().Include(e => e.Choices)` tracked the `QuizTask` and left its `TaskChoice`
      **detached**, so `Set<TaskChoice>()` in the same context built a second instance of a row
      already in hand and `Assert.Same` failed. A probe showed it exactly:

          QuizTask   key=1 defer=True  tracked=MISS | sm:
          TaskChoice key=1 defer=True  tracked=MISS | sm:
          TaskChoice key=1 defer=False tracked=MISS | sm: QuizTask#39021180:Unchanged

      The residual walk that attaches deferred entities (projection-split §4) stopped at every
      entity, on the reasoning that navigations lead back into rows the residual discarded. That
      does not hold for a walk starting from what the residual *kept*: anything reachable from a
      yielded entity travelled as part of that entity's graph. It now descends through
      navigations, read through backing fields so a lazy-loading getter does not turn the walk
      into a query per navigation.

      **Two aborted runs before the clean one, both from the same walk, and both worth keeping.**
      Each reported *fewer* failures than the baseline while doing so — the exact shape
      `eng/measure.sh` guards the total against, and the reason the guard exists:

      1. `seen` compared with default equality, so it called `GetHashCode` on everything it
         reached. `Northwind.Customer.GetHashCode` throws on a null key — 138 new
         `NullReferenceException`s, `FAILING: 219`. Reference identity is what that set wants.
      2. Reference identity then removed the *other* thing default equality had been doing:
         deduplicating boxed structs. A struct is a new object every read, so
         `DateTime.Date` — a `DateTime` that has a `Date` — recursed until the host's stack
         overflowed. `FAILING: 11, TOTAL: <empty>`, from a run that executed 1117 of 11344 tests.

      The walk now stops at any value type that is not a `ValueTuple` or `KeyValuePair`, and
      descends from an entity only into what the model also calls an entity.

      **The 3 newly-red tests were passing vacuously.**
      `PropertyValues*_for_join_entity_can_be_copied_into_an_object` iterates
      `ChangeTracker.Entries<Dictionary<string, object>>()`, which returned **nothing** while the
      join rows behind an `Include` went untracked — a zero-iteration loop asserts nothing. The
      body now runs and reaches the shared-type join entity limitation this plan already tracks:
      "Entity type 'System.Collections.Generic.Dictionary`2<System.String,System.Object>' not
      found in the server model", from `ServerQueryExecutor.RebindQueryRoot`. Confirmed by
      running the class against both trees in isolation: 0 failures before, 3 after, no exception
      possible outside the loop body. Left failing, per the guardrail — they belong to the
      `ManyToManyTracking` residual below, not here.

- [x] **L17.** A shared-type entity can be a query root. **`Total tests: 11344, Passed: 11236,
      Failed: 79, Skipped: 29`** — FIXED 2, BROKEN none. ✅ `<this commit>`

      Two layers, each resolving a shared-type entity by its CLR type, which is the one thing
      that cannot identify one: every many-to-many join entity is a
      `Dictionary<string, object>` and several of them are that same type, told apart only by
      name.

      1. **The wire.** `TypeNodeMapper` fills `TypeNode.EntityTypeName` from
         `IModel.FindEntityType(Type)`, which returns null for a shared type, so a query root
         travelled with no name and the server's `RebindQueryRoot` — which already handles the
         named case — had nothing to use. At a query root the expression itself carries the
         entity type (`EntityQueryRootExpression.EntityType`), so the name is now taken from
         there rather than inferred.
      2. **The provider.** `GetQueryProvider` reflected `DbContext.Set<T>()` over the root's CLR
         type: "cannot create a DbSet for 'Dictionary<string, object>' … access the entity type
         via the 'Set' method overload that accepts an entity type name". There was never
         anything to look up — `InternalDbSet<T>` builds its queryable from
         `context.GetDependencies().QueryProvider`, one per context — so it now resolves
         `IAsyncQueryProvider` directly and `QueryRootFinder` is gone with the reflection.

      **The first fix alone measured as "no change" in the affected classes, and was not.** The
      count held at 26 while the failure moved from the server's model lookup to the provider
      one layer down — the same trap L8 fell into, caught this time by reading the reason rather
      than the count.

      The remaining `ManyToMany` failures are past this point now: the "not found in the server
      model" and `ProxyableShared…` model-lookup errors are gone from the reasons entirely,
      replaced by payload-level ones ("the value [OneId, 20] is not of type System.String").
      `Original_values_for_join_entity_can_be_copied_into_an_object` still fails — original
      values are a separate gap, sent only for concurrency tokens since S3c-13.

- [x] **L18.** A shadow navigation has no value to send. **`Total tests: 11344, Passed: 11243,
      Failed: 72, Skipped: 29`** — FIXED 7, BROKEN none. ✅ `<this commit>`

      `MapRowMembers` read every loaded navigation through `navigation.GetGetter()`, which does
      not return null for a navigation with no CLR member — it throws: "No backing field could be
      found for property 'UnidirectionalEntityTwo.UnidirectionalEntityThree' and the property does
      not have a getter."

      A **unidirectional** many-to-many is where those come from. EF still declares the inverse
      skip navigation in the model; only the CLR property is absent. So `_isNavigationLoaded`
      answers yes, the walk reaches it, and the request dies before anything is sent.

      The navigation is skipped, not the loop iteration: there is no value to send and nowhere on
      the client to put one, but the **join rows** below it are the payload the navigation exists
      to carry, and they were being lost with it. That is what the seven fixed tests were missing
      — all seven are `*_unidirectional` or reached through one.

      This reason is now absent from the whole suite, and `ManyToManyTracking` is 23 → 16.

- [x] **L19.** An entity graph is deeper than 64 JSON levels. **`Total tests: 11344,
      Passed: 11248, Failed: 67, Skipped: 29`** — FIXED 5, BROKEN none. ✅ `<this commit>`

      Five `ManyToManyTracking` tests failed with "a possible object cycle was detected … or if
      the object depth is larger than the maximum allowed depth of 64".

      **Depth, not a cycle**, despite what the message offers as the likelier of the two:
      `SystemTextJsonInfoCarrierSerializer` has set `ReferenceHandler.Preserve` — exactly the fix
      the message suggests — since it was written, so a repeated instance already becomes a
      `$ref` rather than recursion. What is left is the longest path of *distinct* entities, and
      the node model spends roughly four JSON levels on every hop between them: the value node,
      its member list, the member, and that member's own value node. Sixty-four levels is about
      sixteen hops, which a many-to-many exhausts.

      **It was set in the wrong place first, and the error said so.** `MaxDepth = 256` on
      `SystemTextJsonInfoCarrierSerializer` changed nothing: the count held at 16 *and* the
      message still read "depth of 64". These nodes serialize through the source-generated
      `ExpressionJsonContext`, and a `JsonSerializerContext` carries its own options —
      `JsonSourceGenerationOptionsAttribute.MaxDepth` is where it belongs.

      The inert setting was then removed and the suite re-measured rather than left in: the
      transport envelope carries `byte[]` payloads and never a node graph, so it could not have
      mattered, and 256 on it was config that did nothing. Both runs report 67.

- [x] **L20.** A shared-type entity is named by what holds it, not by its CLR type.
      **`Total tests: 11344, Passed: 11254, Failed: 61, Skipped: 29`** — FIXED 6, BROKEN none.
      ✅ `<this commit>`

      `MapToNode` decided "is this an entity" with `IModel.FindRuntimeEntityType(Type)`, and no
      CLR type can identify a **shared-type** entity: every many-to-many join entity is a
      `Dictionary<string, object>` and several of them are that same type, told apart only by
      name. So the lookup returned null, the value fell through to the *collection* branch — a
      dictionary is enumerable — and rebuilding it as one produced "the value [OneId, 1] is not
      of type System.String and cannot be used in this generic collection".

      On the server every value mapped here came out of the change tracker, and the tracker knows
      which shared type an instance is. `ToRowValue` therefore takes a `findEntityType` hook
      alongside the existing `isTracked` / `readShadowValue` / `readJoinEntities` ones, backed by
      `stateManager.TryGetEntry(entity)?.EntityType`, and `MapToNode` falls back to it. It
      answers null for everything that is not a tracked entity, which is most of what passes
      through, and its lookup is reference-keyed — so a value whose `GetHashCode` throws is safe,
      which `GraphUpdatesTestBase.MyDiscriminator` requires.

      **Three of the six fixed are not many-to-many at all.** An *owned* type is shared-typed too,
      so `Lazy_loading_finds_correct_entity_type_with_*` had the same defect, reported as "Type
      'LazyLoadProxyTestBase+Culture' …" rather than as a dictionary. That reason is now gone from
      the suite, along with both `[OneId, …] is not of type System.String` ones.

      `ManyToManyTracking` is 23 → 8 across L18–L20. What remains is the *payload*: join rows for
      a shared-type join entity are still gated out of the wire in `MapRowMembers`
      (`!skip.JoinEntityType.HasSharedClrType`, L11) and rebuilt client-side, which cannot carry
      one. That gate is now the only thing left in the way, and this step removed the reason it
      was put there.

- [x] **L21.** A shared-type join entity's rows travel on the wire. **`Total tests: 11344,
      Passed: 11262, Failed: 53, Skipped: 29`** — FIXED 8, BROKEN none.
      **`ManyToManyTracking` is 0 of 200.** ✅ `<this commit>`

      L11 gated shared-type join rows off the wire (`!skip.JoinEntityType.HasSharedClrType`)
      because they could not be decoded on the far side — sending every join type then measured
      29 fixed against 27 broken. L20 removed that reason, so the gate came out and the payload
      travels: eight `*_with_payload` and `*_self_shared_*` tests, which the client's own
      reconstruction could never have satisfied because a rebuilt join row has only the two
      foreign keys.

      The client-side reconstruction (`TrackJoinEntity` / `ReadJoinKey`) stays as the fallback for
      a navigation no rows were sent for; L11's `sentJoinRows` keeps the two paths mutually
      exclusive by navigation name. **Whether anything still reaches it is now an open question**
      — it was written for exactly the case this step removed. It is not dead code by
      inspection, and proving it either way needs a probe, so it stays until measured.

      This closes queue item 2. The `ManyToMany` residual went 111 → 23 (L7–L11) → 0 (L18–L21),
      and `PropertyValues` is back to 0 with it.

- [x] **L22.** A concurrency failure is translated at the client boundary. **`Total tests: 11344,
      Passed: 11267, Failed: 48, Skipped: 29`** — FIXED 5, BROKEN none.
      `OptimisticConcurrency` is 6 → 1. ✅ `<this commit>`

      The conflict is detected by the *store*, which is on the far side, so the server's
      `DbUpdateConcurrencyException` simply propagated through the client untouched. Two things
      were wrong with that, and they are halves of one defect — nothing translates the failure
      where it crosses.

      1. **Nobody logged it here.** `OptimisticConcurrencyTestBase` asserts
         `Fixture.ListLoggerFactory.Log.Single(l => l.Id == CoreEventId.OptimisticConcurrencyException)`
         on the *client's* logger, and the server's had recorded it instead. Every EF provider
         logs this from exactly this position — InMemory from `InMemoryTable`, the relational
         ones from `AffectedCountModificationCommandBatch` — and this provider is the store as
         far as the client context is concerned.
      2. **`Entries` pointed at a dead context.** They are the server's entries, and the request
         scope is disposed as the exception unwinds, so the resolver's first touch gave "cannot
         access a disposed context instance". The exception is now re-raised carrying the
         client's own entries, matched on entity type name and primary key.

      **Part 1 alone moved the count by one and fixed four tests' worth of assertion.** The four
      got past the log check and failed in the resolver instead — visible only in the reasons,
      which is the third time in this phase that reading the count alone would have been wrong.

      The server's key values are read off the **entity instance**, never through its entry: the
      entry APIs are exactly what throws on a disposed context, which is the failure being fixed.
      A shadow key has no instance to read and does not match; an unmatched server entry is
      dropped rather than guessed at, and if that leaves nothing the whole batch stands in.

      One left in the class: `Nullable_client_side_concurrency_token_can_be_used`, which is
      unrelated — the client materializes rows itself and so never runs
      `IMaterializationInterceptor`, producing a plain `Sponsor` where the test wants a
      `SponsorDoubleProxy`.

- [x] **L23.** An untracked entity is materialized knowing what tracking the query asked for.
      **`Total tests: 11344, Passed: 11278, Failed: 37, Skipped: 29`** — FIXED 11, BROKEN none.
      The lazy residual is 22 → 11. ✅ `<this commit>`

      Every failure was `state: Detached, queryTrackingBehavior: NoTrackingWithIdentityResolution`
      and every one was a duplicate: `Lazy_load_collection_already_partially_loaded` counted 3
      where 2 were expected, `..._already_loaded_delegate_loader_*` counted 4 where 2 were. The
      same tests under plain `NoTracking` passed — which is the whole diagnosis, because it says
      we were behaving as `NoTracking` when asked for identity resolution.

      The chain is entirely EF's, and it turns on a constant baked into a compiled expression:

      - `LazyLoader.Load` asks for `LoadOptions.ForceIdentityResolution` only when its
        `_queryTrackingBehavior` is `NoTrackingWithIdentityResolution`;
      - that field is set by `ILazyLoader.Injected`, which the materializer calls with
        `Constant(bindingInfo.QueryTrackingBehavior)` — see
        `StructuralTypeMaterializerSource.AddAttachServiceExpressions`;
      - `EntityFinder.LoadAsync` uses that option to build a stand-alone `StateManager`, track
        what the navigation already holds, and skip a loaded row already in it.

      `IReadOnlyTypeBase.GetOrCreateMaterializer` — which L1 adopted, and which is right for
      everything else — is the *cached* materializer, and `GetMaterializer` builds it with
      `QueryTrackingBehavior = null`, meaning "not from a query". So the loader was told nothing
      and defaulted to `LoadOptions.None`.

      `MaterializeUntracked` now compiles its own materializer through
      `CreateMaterializeExpression` with the real behavior, exactly as EF's query pipeline does,
      and only under `NoTrackingWithIdentityResolution` — everywhere else keeps the cached one, so
      nothing else pays for it. Cached per entity type, which lives as long as the model.

- [x] **L24.** `Lazy_load_one_to_one_reference_with_recursive_property` (4) — **cause found,
      one fix attempted and measured catastrophic, reverted.** Suite unchanged at
      **`Total tests: 11344, Passed: 11278, Failed: 37, Skipped: 29`**. ✅ `<this commit>`

      The test asserts `ChangeTracker.Entries().Count() == 2`; we hold 1. The parent is loaded —
      `Assert.NotNull(child.Parent)` passes — but never tracked.

      **The cause is settled, by probe.** `WithRecursiveProperty.IdLoadedFromParent` is a *mapped*
      property whose getter reads `Parent`, so attaching the child runs it. The probe caught the
      nested query firing between "before-track" and "after-track":

          row WithRecursiveProperty isTracked=True  behavior=TrackAll
          before-track WithRecursiveProperty state=Detached tracked=0
          row Mother                isTracked=False behavior=NoTracking     <- inside SetEntityState
          after-track  WithRecursiveProperty state=Unchanged tracked=WithRecursiveProperty:Unchanged

      `entry.SetEntityState(Unchanged)` snapshots by *reading the entity's properties*, the getter
      issues a lazy load, and at that instant the child is still `Detached` — so
      `EntityFinder.Query` picks `AsNoTracking()` (its `entry.EntityState == Detached` branch) and
      the parent is materialized untracked. EF's own shaper does not hit this because it tracks
      from a snapshot the query already built, and never reads the entity back.

      **Attempt: adopt that idiom — `_stateManager.StartTracking(entry)` +
      `entry.MarkUnchangedFromQuery()`, which is verbatim `EntityFinder.StartTracking`. Measured
      `Load` + `LazyLoadProxy` 11 → 1126.** `MarkUnchangedFromQuery` is the *from-query* path and
      assumes the caller supplied the snapshot, as `StateManager.StartTrackingFromQuery(entityType,
      entity, snapshot)` does; without one the entries have no original values and nearly every
      test in both classes fails. Reverted.

      Doing this properly means building the snapshot from the wire row and going through
      `StartTrackingFromQuery`, which is a real piece of work, not a tweak. Note that
      `StartTracking(entry)` alone cannot help: it does not set `EntityState`, so the load would
      still see `Detached`.

- [x] **L25.** The client-side join reconstruction is dead; deleted. **`Total tests: 11344,
      Passed: 11278, Failed: 37, Skipped: 29`** — FIXED none, BROKEN none, **reasons byte-identical**.
      ✅ `<this commit>`

      L21 left this as an open question rather than a claim: `TrackJoinEntity` was written for the
      shared-type join entities that could not travel as rows, and L20/L21 made them travel. It
      was documented as the remaining fallback for "a navigation no rows were sent for", which
      was inspection, not evidence.

      A probe on the full suite settles it: **it never ran once across 11344 tests.** So the
      method, its `ReadJoinKey` helper, and the `sentJoinRows` set that existed only to stop the
      two paths colliding are all gone — 108 lines. The measurement is the point of the entry: a
      deletion that changes no test and no failure reason is the only kind worth making without
      further argument.

- [x] **L26.** An untracked row's lazy loader is found on the type the row *is*.
      **`Total tests: 12878, Passed: 12816, Failed: 33, Skipped: 29`** — FIXED 4, BROKEN none.
      ✅ `<this commit>`

      `SetIsLoadedUntracked` looked for the `ILazyLoader` service property on
      `navigation.DeclaringEntityType`. `UseLazyLoadingProxies` adds that property only to types
      it can proxy, so an **abstract** base that declares a navigation never gets one —
      `LazyLoadProxyTestBase.Parent` is abstract and declares `Children`, and every row is a
      `Mother` or a `Father`.

      A probe said so in one line, and is worth quoting because the two cases sit side by side:

          SETLOADED type=Child  nav=Parent   prop=LazyLoader loader=LazyLoader instance=ChildProxy
          SETLOADED type=Parent nav=Children prop=<none>     loader=<null>     instance=MotherProxy

      `Child` is concrete and answers; `Parent` is abstract and does not, so a no-tracking
      `Include` came back reporting the collection unloaded. Looked up on the row's own entity
      type instead, which is what EF's `InternalEntityEntry.GetLazyLoader` does — it reads the
      service property off *this* entry's type, never off a declaring type.

      Lazy is 7 of 825: 4 `Lazy_load_one_to_one_reference_with_recursive_property` (cause settled
      in L24, the naive fix measured 1126 and was reverted), 1 `Can_serialize_proxies_to_JSON`,
      and 2 `Fixup_*_reference_after_FK_change_without_DetectChanges`.

- [x] **L27.** A query result is tracked *as coming from a query*.
      **`Total tests: 12878, Passed: 12822, Failed: 27, Skipped: 29`** — FIXED 6, BROKEN none.
      **`LoadTestBase` is 0 of 715 failing; lazy is 1 of 825.** ✅ `<this commit>`

      This is the work L24 asked for and measured the naive version of. `SetEntityState(Unchanged)`
      is the *attach* path, and it differs from EF's shaper in two ways that each cost tests:

      - it **snapshots by reading the entity's properties**, so a mapped property whose getter
        touches a navigation issues a lazy load while the entry is still `Detached` —
        `EntityFinder.Query` then takes its `Detached` branch, picks `AsNoTracking()`, and the
        principal is never tracked. That is L24's four
        `Lazy_load_one_to_one_reference_with_recursive_property`.
      - `NavigationFixer.InitialFixup` guards its dependent-side writes with
        `!fromQuery || CanOverrideCurrentValue(…)`. Only the from-query path leaves a navigation
        the caller has already pointed at another **tracked** entity alone, which is exactly what
        `Fixup_reference_after_FK_change_without_DetectChanges` (EF issue #27497) asserts.

      **Why L24's attempt measured 1126 and this does not.** L24 kept the entry
      `StateManager.CreateEntry(values, …)` had built and called `MarkUnchangedFromQuery()` on it.
      That is half of `StartTrackingFromQuery`: the other half constructs a *fresh*
      `InternalEntityEntry` around the entity and a shadow snapshot, and registers it in every
      identity map. So the entity is now built directly — `Materialize(entityType, values)`, the
      helper that was already there — and handed to
      `IStateManager.StartTrackingFromQuery(entityType, instance, ShadowValuesFactory(values))`,
      which is the call EF's own `ShapedQueryCompilingExpressionVisitor` emits.

      One consequence worth stating: the scalars that travel as nodes rather than primitives now
      join the value dictionary *before* the entity is built, instead of being written onto the
      entry afterwards. On a from-query entry the later write is a modification to a row that had
      only just been read.

      `ClearPlaceholderReferencesBlockingFixup` takes the instance and the values rather than an
      entry, since there is no entry until tracking. It may well be obsolete now — from-query
      fixup overrides an untracked value on its own — but that is a separate measurement.

## Phase T — transactions (roadmap M4)

- [x] **T1.** Transactions are remoted; the W3 token is the scope. **`Total tests: 11347,
      Passed: 11281, Failed: 37, Skipped: 29`** — FIXED none, BROKEN none, **reasons unchanged**,
      3 net new tests. ✅ `<this commit>`

      The client no longer decides on the store's behalf. `InfoCarrierTransactionManager` used to
      return a stub and raise `InfoCarrierEventId.TransactionIgnoredWarning` itself; it now asks
      the server, and whatever the server's store does is what happens.

      **W3 is answered by a server-held scope.** `InProcessInfoCarrierServer.BeginTransactionAsync`
      creates a DI scope, resolves a context, begins a store transaction and keeps all three under
      a token. `QueryDataRequest` and `SaveChangesRequest` gained `TransactionId`, and a request
      naming one runs on that context instead of a fresh scope. An *unknown* token is refused
      rather than run outside the transaction — falling back would commit work the caller believes
      is provisional, which is the one thing a transaction exists to prevent.

      Three things this turned up, none of them guessable from the design:

      1. **The InMemory backend had to opt into its own ignored-transaction warning.** EF defaults
         it to `WarningBehavior.Throw`, and now that the *client* asks the store, Tier A would have
         failed at `BeginTransaction` in every base that runs inside
         `ExecuteWithStrategyInTransactionAsync`. The client fixtures already opted in the same
         way; the backend store now does too. The outcome on Tier A is unchanged — a transaction
         that does nothing — but the refusal comes from the component that actually refuses.
      2. **A second context must be able to join.** `OptimisticConcurrencyTestBase` opens a
         transaction on one context and has another observe the same uncommitted state; that is
         the shape of a concurrency test. With a real transaction on Tier B the unenlisted second
         context ran on its own SQLite connection and got "database is locked" — 11 tests.
         `UseInfoCarrierTransaction` enlists it by token, and the enlisted transaction is **not
         owned**: ending it detaches that context and leaves the transaction to whoever began it,
         because two contexts able to commit one transaction makes the outcome depend on disposal
         order.
      3. **A pinned context must still clear its change tracker.** A transaction pins the
         *connection*, not the tracker, and every request is self-contained. Reusing the context
         without clearing let one request's tracked entities meet the next request's copy of the
         same rows — "the instance of entity type 'Driver' cannot be tracked because another
         instance with the same key value is already being tracked", 6 more tests.

      `TransactionIgnoredTest` is replaced by `TransactionRemotingTest`, which asserts the token
      flows, that a save inside a transaction carries it and one outside does not, that commit and
      rollback round-trip, that disposal rolls back an uncommitted transaction (requirements §2.9)
      and that a second `BeginTransaction` on one context is refused. Direct assertions for the
      reason the replaced file gave: on Tier A the remoted transaction still does nothing, so no
      suite count distinguishes "the token flows" from "the token is never sent".

      **Still open after T1:** savepoints (T2), and `Database.UseTransaction` as a public client
      API rather than the test-only extension.

- [x] **T2.** Savepoints, and proof that a transaction is real. **`Total tests: 11351,
      Passed: 11285, Failed: 37, Skipped: 29`** — FIXED none, BROKEN none, **reasons unchanged**,
      4 net new tests. ✅ `<this commit>`

      `IDbContextTransaction`'s savepoint members default to throwing `SavepointsNotSupported`, so
      leaving them was a real gap rather than a formality. Four wire operations
      (`CreateSavepoint`, `RollbackToSavepoint`, `ReleaseSavepoint`, `SupportsSavepoints`) and a
      `SavepointRequest` carrying `{ TransactionId, Name }`.

      **A savepoint has no token of its own.** It is not a scope — EF uses savepoints *instead* of
      nested transactions — so the W3 token plus a name is the whole address, and the server
      delegates each call to the store transaction it is already holding. `SupportsSavepoints` is
      a round trip because only the server's store knows the answer, cached after the first ask
      since EF consults it before every savepoint operation.

      **The interesting part is what now proves any of this works.** T1 changed no test count and
      no failure reason, which is the correct outcome and also the weakest possible evidence — on
      Tier A a remoted transaction still does nothing, so nothing in a suite run distinguishes
      "the token flows" from "the token is never sent". Three end-to-end tests on Tier B, where
      SQLite has genuine transactions and genuine savepoints, close that:

      - a rolled-back transaction leaves the store untouched, *and* its work is visible from
        inside it — which only holds if the query carried the token and ran on the pinned server
        context;
      - a committed transaction keeps its work;
      - a savepoint rolls back part of a transaction and the rest still commits.

      That third one is the whole feature in one assertion. **M4's exit criteria are met**: begin,
      commit, rollback, savepoints, the W3 token across a stateless transport, and client disposal
      cleaning up the server side.

## Phase A — adopting the remaining spec bases

The compliance test reports **131** unadopted bases. That is a far larger unknown than the
failure count, which is the argument for working it before the query long tail: every base
adopted turns guesswork into a number. Adopted in batches, measured per batch, and a base's
failures are left red and classified rather than worked immediately.

- [x] **A1.** First batch: four bases, 213 tests. **`Total tests: 11564, Passed: 11479,
      Failed: 56, Skipped: 29`** — 19 new failures, **none of them a regression**: every
      previously passing test still passes. ✅ `<this commit>`

      | Base | Tests | Failing |
      |---|---|---|
      | `FieldMappingTestBase` | 167 | 16 |
      | `WithConstructorsTestBase` | 41 | 3 |
      | `CompositeKeyEndToEndTestBase` | 3 | 0 |
      | `NotificationEntitiesTestBase` | 2 | 0 |

      Chosen because each aims directly at something this provider learned indirectly:
      field-backed state (L6, L18), constructor binding and service injection (L1), multi-property
      keys (used by every identity path but exercised almost nowhere), and a notification model —
      the one case where nothing re-derives a change the tracker was not told about.

      **`CompositeKeyEndToEnd` and `NotificationEntities` are green on adoption**, which is the
      more useful half of the result: composite keys and change-notification tracking work, and
      neither needed a line of provider code.

      The 19 failures, classified:

      | # | Symptom | Reading |
      |---|---|---|
      | 4 | `Assert.NotSame` — values are the same instance | A no-tracking query returning shared instances; identity resolution applied where it must not be. |
      | 4 | `ArgumentNullException (source)` | A collection navigation left null where the test expects an empty one. |
      | 4 | `Operation is not valid due to the current state of the object` | Unclassified. |
      | 4 | `AmbiguousMatchException` on a hidden property | **This classification was wrong** — see A8. Recorded here as "raised by the test base's own reflection, before it reaches the provider"; it is raised by `NodeToExpressionTranslator.TranslateMember`, which is ours. Read from the message alone, without opening the stack. |
      | 2 | `MissingMethodException` — cannot dynamically create an immutable record | The client builds entities through EF's materializer since L1, but something on this path still reflects a parameterless constructor. |
      | 1 | `Assert.Single` — collection empty | `Query_with_keyless_type`: the keyless type's defining query is `ToInMemoryQuery`, which the *client* model cannot carry. Needs the `serverContextType` split the Northwind fixtures use. |

      **`StoreGeneratedTestBase` was considered and not adopted**: EF's own
      `StoreGeneratedInMemoryTest` does not derive from it — it is a standalone class — so the
      base is relational-only in practice and adopting it would assert relational behaviour, not
      ours.

- [x] **A2.** Second batch: five loading and fixup bases, 1314 tests. **`Total tests: 12878,
      Passed: 12455, Failed: 394, Skipped: 29`** — 338 new failures, **all of them inside the
      five new classes**; no previously passing test moved. ✅ `<this commit>`

      | Base | Tests | Failing |
      |---|---|---|
      | `FieldsOnlyLoadTestBase` | 713 | **0** |
      | `ManyToManyLoadTestBase` | 358 | 184 |
      | `ManyToManyFieldsLoadTestBase` | 124 | 56 |
      | `StoreGeneratedFixupTestBase` | 118 | 98 |
      | `OverzealousInitializationTestBase` | 1 | **0** |

      **`FieldsOnlyLoadTestBase` passing 713 of 713 on adoption is the result of this batch.** It
      is explicit and lazy loading over a model with no properties at all — every scalar and every
      navigation is a field — which is precisely what phase L rebuilt, and it needed no provider
      code. `OverzealousInitialization` is likewise green: a model whose constructors eagerly
      populate their own navigations, which is what `ClearPlaceholderReferencesBlockingFixup`
      (L6) exists for.

      **The 338 are three causes, not 338 problems**, and the count should be read that way:

      | # | Cause | Where |
      |---|---|---|
      | 96 | `An item with the same key has already been added` from `InMemoryTable.Create` | Server-side. Almost all of `StoreGeneratedFixup`: rows whose keys the store generates collide on insert. One defect. |
      | 20 | `No backing field could be found for property 'UnidirectionalEntity…'` | `ServerQueryExecutor.IsLoaded` line 227 — **the same defect L18 fixed in `MapRowMembers`, at a second site.** Fixed in A3. |
      | ~220 | `Assert.Equal`/`Assert.True` on loaded skip navigations | The `ManyToMany*Load` residual proper: these bases *load* skip navigations, where `ManyToManyTracking` saves and re-reads them. |

      Adopting a base that comes up 98 of 118 red is deliberate, not an accident of batching. A
      red spec test is information and the guardrail against suppressing them is the whole reason
      this repo inherits the bases at all; selecting only the ones that pass would be the same
      mistake in a politer form. What the count needs is the table above, not a smaller number.

- [x] **A3.** A shadow navigation cannot report itself loaded either. **`Total tests: 12878,
      Passed: 12463, Failed: 386, Skipped: 29`** — FIXED 8, BROKEN none, and the reason is gone
      from the whole suite. ✅ `<this commit>`

      L18 fixed `DynamicValueMapper.MapRowMembers`, which read a loaded navigation's value through
      `GetGetter()` and threw on one with no CLR member. `ServerQueryExecutor.IsLoaded` has the
      same call on its *untracked* fallback path and was missed, because nothing adopted at the
      time reached it — `ManyToManyLoadTestBase` does.

      An untracked entity with a shadow navigation has nowhere for the value to be, so the answer
      is "not loaded" rather than an exception. The tracked path above it is unaffected: an entry
      knows what it loaded without reading the entity.

      **Twenty tests stopped throwing and eight of them pass.** The other twelve now fail on
      `Assert.Equal` — they got past the exception and into the `ManyToMany*Load` residual, which
      is where they belonged all along. Worth stating plainly because the count moved by 8 while
      the fix did what it was aimed at for all 20.

- [x] **A4.** `StoreGeneratedFixup` needs the store emptied between tests. **`Total tests: 12878,
      Passed: 12561, Failed: 288, Skipped: 29`** — FIXED 98, BROKEN none.
      **`StoreGeneratedFixupTestBase` is 118 of 118.** ✅ `<this commit>`

      The 96 duplicate-key failures A2 recorded were **not a provider defect**. The base wraps
      each test in a transaction and relies on the rollback; the backend is EF's InMemory
      provider, which does not do transactions, so nothing is undone. This model's keys are
      composite and therefore *not* store-generated by convention — every test creates a
      `Category` keyed `(0, Guid.Empty)`, and the second one to save collides with the first.

      **Settled by running a failing test on its own, where it passes.** That is the whole
      diagnosis: the failure belonged to the previous test's leftovers, not to the test that
      reported it. Reading the 96 as one server-side insert bug — which is what the stack looks
      like, `InMemoryTable.Create` throwing from inside `ServerSaveChangesExecutor` — would have
      sent the next session hunting in the wrong file.

      `GraphUpdatesInfoCarrierTest` and `OptimisticConcurrencyInfoCarrierTest` already clean for
      exactly this reason, so this is the third instance of one pattern: **on a backend that
      ignores transactions, a shared store has to be emptied by the test class.** `finally`,
      because a failing test dirties the store the same way a passing one does.

- [x] **A5 (cause; fixed in A6).** The `ManyToMany*Load` residual — **240 of 482, one cause.**
      Suite unchanged at **`Total tests: 12878, Passed: 12561, Failed: 288, Skipped: 29`**.

      `ManyToManyLoad` 176 of 358 and `ManyToManyFieldsLoad` 56 of 124 — together **83% of every
      failure left in the suite**. Explicitly loading a skip navigation
      (`context.Entry(left).Collection(e => e.TwoSkip).Load()`) leaves the collection **empty**.
      All 24 parameterizations of `Load_collection` fail, across every state and tracking
      behaviour, so this is one gap and not a set of edge cases.

      **Not a store-isolation problem** — unlike A4, a failing test still fails on its own.

      Two probes narrowed it to the client, and the second is the one that matters:

      1. The query ships **whole** (`IsPassThrough=true`) and reaches the server. EF builds
         `Set<EntityOne>().Where(e => e.Id == 1).SelectMany(e => e.TwoSkip).NotQuiteInclude(e =>
         e.OneSkip.Where(...))` — see `ManyToManyLoader.Query` — and the serializer carries it,
         `NotQuiteInclude` and all.
      2. **The server returns the right rows.** Logging the materialized count per round trip
         gave `ROWS=7` for the dominant case, which is exactly what the test then asserts it
         cannot find.

      So the rows arrive and the navigation stays empty: **the failure is fixup on the client, not
      translation or transport.** A skip navigation is wired through its *join* rows, and the
      `NotQuiteInclude` half of that query exists precisely to bring the inverse navigation — and
      with it, since L21, the join rows — back. What has not been established is whether those
      join rows reach the client at all, or arrive and fail to connect. That is the next probe,
      and it is a small one.

      Left failing rather than half-fixed. Recorded here because the count alone
      (`Failed: 288`) reads as a long tail and is nothing of the sort.

- [x] **A6.** A skip navigation's join rows travel whether or not it is loaded.
      **`Total tests: 12878, Passed: 12737, Failed: 112, Skipped: 29`** — FIXED 194, BROKEN 18.
      ✅ `<this commit>`

      The last probe A5 asked for: `ReadJoinEntities` is called for `OneSkip` **only** under
      `NoTracking`, never under `TrackAll`. It was never reached, because `MapRowMembers` gated
      the join-row block behind `if (!_isNavigationLoaded(…)) continue;` — and **EF deliberately
      leaves a filtered include unloaded.** `ManyToManyLoadTestBase.Load_collection` asserts
      exactly that (`Assert.False(context.Entry(entityTwo).Collection(e => e.OneSkip).IsLoaded)`),
      and `ManyToManyLoader.Query` builds precisely that shape:
      `…SelectMany(e => e.TwoSkip).NotQuiteInclude(e => e.OneSkip.Where(…))`.

      So the rows arrived and nothing joined them. The *value* of an unloaded navigation still
      must not be sent — that was never the question — but its join rows are what connect the two
      sides and the client cannot rebuild them. They now travel on their own merit.

      **18 broken, and they are a real ordering defect, not convergence.** All 18 are
      `Load_collection_using_Query_already_loaded[_untyped]` on `ManyToManyFieldsLoad`, whose
      navigation is a `List` — so order is observable, and `Assert.Equal(children,
      left.TwoSkip.ToList())` compares it. The count is right (the preceding
      `Assert.Equal(7, …)` passes); one entity moves to the front once join rows arrive for a
      navigation the client had already loaded. `ManyToManyLoad`, whose collections are unordered,
      does not see it.

      Kept rather than reverted: 194 fixed against 18, the 18 are one narrower defect in one model
      shape, and reverting would restore a gap that made explicit skip loading useless. Fixed in
      A7.

- [x] **A7.** One side owns a join row: whichever loaded the navigation.
      **`Total tests: 12878, Passed: 12753, Failed: 96, Skipped: 29`** — FIXED 18, BROKEN 2.
      ✅ `<this commit>`

      A6's 18, diagnosed by capturing the *same* test's payload with and without A6's line:

          pre-A6   EntityOne[3] TwoSkip:nav TwoSkip:JOIN
                   EntityTwo[19] · EntityTwo[1] · … · JoinOneToTwo[3,1] · JoinOneToTwo[3,4] · …
          post-A6  EntityOne[3] TwoSkip:nav TwoSkip:JOIN
                   EntityTwo[19] OneSkip:JOIN · JoinOneToTwo[3,19] · EntityTwo[1] OneSkip:JOIN · …

      Both ends of a many-to-many name the same join rows. Before A6 they arrived as **one batch**
      from the loaded side, and that batch order is the order the client rebuilds the navigation
      in. A6 let the other end send them too, so they arrived **interleaved, one per entity** —
      `JoinOneToTwo[3,19]` first instead of in set order — and a `List`-backed navigation came out
      reordered. The count was never wrong; only the order was, which is why only
      `ManyToManyFieldsLoad` saw it.

      So exactly one side sends each join row, and the owner is **whichever side EF actually
      loaded the navigation on**: it sends the whole set in one run. When neither side is loaded —
      the case A6 exists for — the near side sends them, which is what makes explicit loading work.
      Implemented server-side in `ReadJoinEntities`, where the far side's entity and its loaded
      state are both reachable.

      **A first attempt was wrong and measured inert.** Deduplicating against the mapper's
      `_toIds` assumed the loaded side is serialized first; the probe showed the opposite — in the
      query that matters the `EntityTwo` rows precede `EntityOne[3]`. Reverted rather than kept as
      a no-op.

      **2 broken**, both `Load_collection_using_Query_with_Include_for_same_collection`
      (`Expected: 7, Actual: 4`) — corrected in A11.

- [x] **A8.** A `new`-hidden member resolves to the most derived declaration.
      **`Total tests: 12878, Passed: 12757, Failed: 92, Skipped: 29`** — FIXED 4, BROKEN none.
      ✅ `<this commit>`

      `NodeToExpressionTranslator.TranslateMember` resolved a member with
      `Type.GetProperty(name, flags)`, which **throws** when a derived type hides a base member
      rather than preferring the nearer one: "ambiguous match found for '… BlogHiding … Posts'".
      A model whose derived type hides a base property is legitimate —
      `FieldMappingTestBase`'s `*Hiding` entities are exactly that — and the most-derived
      declaration is the one the client wrote and the wire named. Now walked one level at a time
      with `DeclaredOnly`, most-derived first, for properties and fields alike.

      **A1 classified these 4 as the test base's own reflection and it was wrong.** That reading
      came from the exception message naming `FieldMappingTestBase+BlogHiding`, without opening
      the stack — where the top frame is this provider's. Corrected in the A1 table.

- [x] **A9.** A navigation is *written* through its backing field too.
      **`Total tests: 12878, Passed: 12766, Failed: 83, Skipped: 29`** — FIXED 9, BROKEN none.
      ✅ `<this commit>`

      L6 established that a navigation must be *read* through its backing field, because a getter
      on a lazy-loading entity is itself a load. The write side is the same rule for a different
      reason: **a setter can refuse.** `FieldMappingTestBase.PostFull.Blog` throws
      `InvalidOperationException` outright unless the model is seeding, which is that base's way
      of saying "materialize through the field". EF's own materializer obeys the navigation's
      `PropertyAccessMode`, whose default prefers the field.

      **Three variants measured, and the middle one was the trap:**

      | Variant | Failures | |
      |---|---|---|
      | field always | 86 | FIXED 10, BROKEN 4 |
      | field for *references* only | 91 | FIXED 1 — gives up almost the whole gain |
      | field unless a collection already exists | **83** | FIXED 9, BROKEN none |

      A collection navigation's field usually already holds the instance the entity was
      constructed with, and `Include_collection_read_only_props` exposes no setter at all — EF's
      fixup was filling that collection perfectly well, and replacing it with a fresh list is a
      different thing from filling it. But a *null* field is the case with nothing to fill, and
      there the field is the only way in. Refusing collections outright (the second row) throws
      away the tests that need exactly that.

- [x] **A10.** A shadow navigation can still be *loaded*.
      **`Total tests: 12878, Passed: 12788, Failed: 61, Skipped: 29`** — FIXED 22, BROKEN none.
      ✅ `<this commit>`

      L18 stopped sending the *value* of a shadow navigation, because `GetGetter` on one throws
      rather than returning null. Correct, and it threw away something real with it: **"loaded" is
      state the client cannot learn any other way.** A unidirectional many-to-many's inverse skip
      navigation is exactly this — declared in the model with no property behind it, which is why
      `Load_collection_already_loaded_untyped_*` has to name it with a string — and
      `Assert.True(navigationEntry.IsLoaded)` was failing on it right after an `Include`.

      So a loaded shadow navigation now travels as a **value-less loaded member**: present, flagged
      loaded, carrying nothing. The client marks the navigation loaded and assigns nothing, which
      is the only thing it could do and exactly what is wanted. What populates the navigation is
      the join rows sent alongside it (A6/A7).

      An absent member still means "not loaded", so the flag stays unambiguous — nothing else had
      to change to tell the two apart.

- [x] **A11.** Only an *unloaded* side defers to the other.
      **`Total tests: 12878, Passed: 12790, Failed: 59, Skipped: 29`** — FIXED 2, BROKEN none.
      ✅ `<this commit>`

      A7 made the loaded side own a join row, but applied the far-side check unconditionally. When
      **both** sides are loaded — a query that `Include`s the very collection it is loading — each
      deferred to the other and nobody sent: `Expected: 7, Actual: 4`.

      The rule is now stated the way it was always meant: a **loaded side always sends**; an
      unloaded side sends only when the far side is not loaded either. `ReadJoinEntities` takes
      the near side's loaded flag to say so, rather than inferring it.

- [x] **A12.** A join row is attached outright, never deferred.
      **`Total tests: 12878, Passed: 12797, Failed: 52, Skipped: 29`** — FIXED 7, BROKEN none.
      ✅ `<this commit>`

      Deferred tracking exists so a split query tracks only the entities its *residual* yields —
      a join over 919 rows answering a projection over 7 (projection-split §4). A join row is not
      one of those. It is relationship state for an entity that is already in the result, and no
      residual ever yields one, so deferring it meant never attaching it at all:
      `Load_collection_using_Query_with_join` counts the tracker and found 4 where 7 belong.

      `ManyToManyLoad` is 4 of 358 and `ManyToManyFieldsLoad` 4 of 124; across A5–A12 the pair
      went from 240 failures to 8.

- [x] **A13.** A join row is tracked in *result* order, not in walk order.
      **`Total tests: 12878, Passed: 12805, Failed: 44, Skipped: 29`** — FIXED 8, BROKEN none.
      **`ManyToManyLoad` and `ManyToManyFieldsLoad` are 482 of 482.** ✅ `<this commit>`

      A7 and A11 settled *which side* sends a join row. This is the third question in the same
      family and the last one: **when** the client tracks it. EF's fixup appends to the navigation
      as each join row is tracked, so tracking order is a `List`-backed navigation's order, which
      is what `Assert.Equal(children, left.TwoSkipShared.ToList())` compares.

      A probe on both ends of the wire gave the cause outright — the serialization order is not
      the result order:

          SEND owner=EntityTwo[10] skip=OneSkipShared  join=[3,10]
          SEND owner=EntityTwo[16] skip=OneSkipShared  join=[3,16]     ← row 3, sent inside row 1
          SEND owner=EntityTwo[11] skip=OneSkipShared  join=[3,11]

      `Load_collection_using_Query_with_Include` returns EntityTwo 10, 11, 16, and EntityTwo 16 is
      reachable from row 10's *own* include — `10 → ThreeSkipFull → EntityThree 3 → TwoSkipFull →
      16`. The serializer's walk is depth-first and an entity travels whole at its **first**
      occurrence, so 16's join rows were emitted inside row 10 and the navigation came out
      10, 16, 11.

      So a join row is now held back and tracked once the result is known: a result element's join
      rows are tracked at the element's position, and rows sent for an entity that is not a result
      element — the loaded far side of A7, which sends the whole set in one run — keep the order
      they arrived in, which is the run order that matters there.

      **Two wrong shapes measured first, and each named the constraint it broke:**

      | Attempt | `ManyToMany*Load` | What it hit |
      |---|---|---|
      | hold the *node*, materialize at flush | 218 of 482 | `Dangling wire reference 10` — a wire id is only resolvable inside its own message, and a lazy load fired mid-decode resets that scope |
      | hold the entity, flag left set for the subtree | 150 of 482 | A join row carries navigations to **both** its sides; the far entity was deferred with it and nothing ever attached that |

      Hence the shape that works: the row is *built* where it arrives, so references resolve, and
      only tracking is held; and the hold is consumed at the top of `MaterializeEntity`, so it
      applies to that row and nothing nested under it.

- [x] **A14.** A collection navigation is *filled*, through the model's own accessor.
      **`Total tests: 12878, Passed: 12810, Failed: 39, Skipped: 29`** — FIXED 5, BROKEN none.
      **`FieldMappingTestBase` is 167 of 167.** ✅ `<this commit>`

      A9 built a `List<T>` and assigned it, guarded by "unless the collection already exists".
      Both halves of that were wrong, and `FieldMappingTestBase` names each one:

      | Entity | Member | What assigning a list does |
      |---|---|---|
      | `BlogReadOnly` | `ObservableCollection<PostReadOnly> _posts` | `ArgumentException` — a `List<T>` is not one |
      | `BlogReadOnlyExplicit` | `Collection<PostReadOnlyExplicit> _myposts` | the same |
      | `BlogFull` | `List<PostFull> _posts`, setter throws unless seeding | A9's guard sent a non-empty field to the *property*, and the setter refused |

      `INavigationBase.GetCollectionAccessor()` answers all three at once, because it is what EF's
      own materializer uses: it knows the concrete type to instantiate, the member to reach it
      through — field or property, per the navigation's `PropertyAccessMode` — and that an
      existing collection is to be **filled** rather than replaced. `GetOrCreate(instance,
      forMaterialization: true)` then `Add(instance, item, forMaterialization: true)` per item,
      and nothing is assigned to the navigation at all.

      A9's three-variant table is superseded: the question it was answering — replace the
      collection or leave it alone — does not arise once the items go in one at a time.

- [x] **A15.** The server reconstitutes an entity through EF's materializer, not `Activator`.
      **`Total tests: 12878, Passed: 12812, Failed: 37, Skipped: 29`** — FIXED 2, BROKEN none.
      ✅ `<this commit>`

      `ServerSaveChangesExecutor` built the entity to replay with
      `Activator.CreateInstance(entityType.ClrType)`, which is enough only while every entity has
      a parameterless constructor. `WithConstructorsTestBase` is the model that says otherwise —
      a constructor-bound `Blog(int id, string title, int? monthlyRevenue)` and a positional
      record `BlogAsImmutableRecord` — and both came back **"No parameterless constructor
      defined"** from inside `SaveChanges`.

      L1 settled this on the client: the materializer is what performs constructor binding, and
      reflection-constructing an instance skips it. The server had the same defect and nothing
      adopted until A1 reached it. So the values are read first, laid into a value buffer indexed
      by `IProperty.GetIndex()`, and handed to
      `IRuntimeEntityType.GetOrCreateMaterializer(…)` — the same shape as
      `ClientResultMaterializer.Materialize`. The pass that follows writes the values onto the
      object again, which is what carries the ones no constructor parameter claimed.

      `WithConstructors` is down to `Query_with_keyless_type`, which is a fixture question rather
      than a provider one: the keyless type's defining query is `ToInMemoryQuery`, which the
      *client* model cannot carry, so it needs the `serverContextType` split the Northwind
      fixtures use.

- [x] **A16.** Third batch: five query bases, 441 tests. **`Total tests: 13319, Passed: 13247,
      Failed: 35, Skipped: 37`** — 8 new failures, **none of them a regression**: every previously
      passing test still passes. ✅ `<this commit>`

      | Base | Tests | Failing |
      |---|---|---|
      | `Query.CompositeKeysQueryTestBase` | 14 | **0** |
      | `Query.NullKeysTestBase` | 5 | **0** |
      | `Query.IncludeOneToOneTestBase` | 12 | **0** |
      | `Query.ManyToManyQueryTestBase` | 204 | 4 |
      | `Query.ManyToManyNoTrackingQueryTestBase` | 206 | 4 |

      Chosen as the query-side counterparts of what the tracking and loading bases already cover:
      A5–A13 spent nine steps on many-to-many *loading* and nothing had exercised the same model
      through `Include` and projection; `CompositeKeyEndToEnd` is green on the tracking side and
      the query side was untested; `IncludeOneToOne` is the shape L27 changed most.

      **Three of the five are green on adoption**, and the fourth and fifth fail on **one method,
      in two parameterizations each**: `Left_join_with_skip_navigation[_unidirectional]`, all 8
      with the same `NullReferenceException` from the same place —

          at lambda_method(Closure, <>f__AnonymousType`2)
          at System.Linq.Enumerable.EnumerableSorter`2.ComputeKeys
          at InfoCarrier.Core.QueryExecutor`1.Attaching(…)

      The query is `… from s in grouping.DefaultIfEmpty() orderby t.Key1, s.Key1, …`, so `s` is
      null for an unmatched row, and the `orderby` is running **in the residual** — client-side,
      in process — where a null dereferences. On the reference provider the ordering reaches the
      store and null simply sorts. So this is the splitter placing an `OrderBy` on the client that
      belongs on the server, which puts it with the query residual rather than with many-to-many.

- [x] **S3c-18.** A shared SQLite store is initialized once per *process*, not once per live
      store. **`Total tests: 11024, Passed: 10310, Failed: 685, Skipped: 29`**, three consecutive
      identical runs. ✅ `<this commit>`

      **The flakiness was not gone; it was only quiet.** The final verification run of S3c-17
      reported 694 failures where the run before it, with no code change between them, reported
      685 — nine `NorthwindWhereQuerySqliteInfoCarrierTest` tests. Reproduced as nondeterministic
      immediately: the next run was byte-identical to the earlier snapshot.

      S3c-5 removed one half of the disposal-order coupling (deleting the `.db` file on disposal)
      and left the other. Several classes share the store named "Northwind" and therefore one
      file; each backend store builds its own service provider, so the `TestStoreIndex` that
      normally makes shared initialization run once is not shared between them; and the static
      `Created` set was the only remaining guard. `DisposeAsync` removed its entry — so one class
      finishing while another still held the file let a third pass the guard and run
      `EnsureDeleted` + `EnsureCreated` + seed, deleting the database out from under the class
      still using it. It needed the suite to pass ten thousand tests before the scheduling
      exposed it, which is exactly how the 698-test phantom failure behaved in S3c-2.

      `DisposeAsync` now releases nothing at all. A shared file initialized once stays
      initialized for the lifetime of the process, which is what "shared" was always supposed to
      mean; an unshared store has a path of its own and is unaffected.

- [ ] **S3c-17.** `GraphUpdatesTestBase` on Tier B — **attempted, measured, reverted.** Not
      committed as code; this is the finding.

      **The recorded blocker was wrong.** It said Tier B was blocked because EF's
      `GraphUpdatesSqliteFixtureBase` uses relational-only `HasDefaultValue` and `UseTransaction`
      on a model the non-relational client must also build. That is not an obstacle: the *core*
      `GraphUpdatesFixtureBase` model is relational-agnostic, SQLite creates a schema from it
      directly, and a fixture that changes nothing but the store factory compiles and runs. EF's
      SQLite variant is an optimisation of that model, not a precondition for it.

      **What actually happens: `Failed: 1787, Passed: 0, Total: 1787` in 6 m 31 s.** Not a long
      tail — nothing passes. 897 `NullReferenceException` thrown from the first line of the test
      body, 134 "sequence contains no elements", 426 `DbUpdateException`: the store comes up
      **empty**. The same shape S3c-14 hit on the F1 fixture, where the cause was that the
      fixture seeds through the *client* options and the server never sees them.

      Reverted rather than committed. 1787 red tests and six and a half minutes of run time for
      no signal is worse than not having the class, and the guardrail against suppressing spec
      tests is about tests that *tell* you something.

      **The next step is a one-hour question, not a redesign:** find out why
      `GraphUpdatesFixtureBase.SeedAsync` — which does populate the InMemory backend on Tier A
      through the same `TestStore.InitializeAsync` path — leaves the SQLite one empty. Fix that
      first and re-measure before reading any other failure in this class.

### The `GraphUpdates` residual — 1 of 1787 (2026-08-03, Tier A)

**The table this replaces was wrong, and worth saying how.** It claimed 45 failures split
14 / 12 / 10 / 4 / 3 / 2, and the decomposition of the suite total that went with it read
"45 GraphUpdates + 29 query + 4 PropertyValues". The measured split at that same commit was
**33 GraphUpdates + 16 PropertyValues + 28 query + 1 compliance**, and the 12-test
`Mark_explicitly_set_*_stable_*` family did not exist at all — it had been fixed and the table
never updated. Both numbers had been carried forward by hand across several steps. Anything
below is now read out of `artifacts/measure/<label>.txt`, which is the actual run.

| # | Family | Symptom | Diagnosis |
|---|---|---|---|
| 1 | `Save_optional_many_to_one_dependents` | tracked-entry count off by one (26 vs 27) | **Introduced by S3c-9**, now in 1 of that method's 12 parameterizations. |

Three families have left this table. `Update_root_by_collection_replacement_of_*` (was 10, then
6): L13 sent the replaced row, L15 stopped its replacement being fixed up to it.
`Can_add_*_dependent_when_multiple_possible_principal_sides` (3) was never a `SaveChanges`
failure at all — L16 fixed it in the *query* path. `Save_changed_owned_one_to_one` is likewise
gone.

### The `PropertyValues` residual — 0 of 196, and `ManyToManyTracking` 0 of 200 (2026-08-03, Tier A)

Both are clear. The 16-test `Scalar_store_values_*` / `Scalar_original_values_*` shape this
section used to describe is fixed, and so are the 3
`*_for_join_entity_can_be_copied_into_an_object` tests L16 un-hid — those had been passing
vacuously on an empty `ChangeTracker.Entries<Dictionary<string, object>>()`, and L17/L20/L21
carried them through the shared-type join entity work rather than around it.

The remaining **22** failures outside these classes are M2's query residual, unchanged since Z7,
plus 1 in `InfoCarrierComplianceTest` — which is the compliance report itself, and moves as
bases are adopted.
- [x] **S3c-9.** Generate the key on a store that generates at `Add` time. **GraphUpdates
      156 → 45; suite `Passed: 6941, Failed: 74, Skipped: 13, Total: 7028`** — 113 fixed, 2
      broken, identical across two consecutive full runs. ✅ `<this commit>`

      The server now leaves an `Added` entity's placeholder key unset so value generation runs,
      then redirects every reference to whatever the key became. Three things the measurements
      forced, each of which had broken an earlier attempt — the diagnosis under S3c-8 below was
      right about the cause and wrong about all three:

      1. **A borrowed placeholder is recognised by its value, not by the client's temporary
         flag.** EF only flags what it generated; a reparent that assigns the FK itself
         (`old1.RootId = newRoot.Id`) produces an ordinary `int`. Flag-based detection left all
         112 `Reparent_*` tests pointing at a key that was about to stop existing — which is why
         attempts 1 and 2 measured *worse* than doing nothing.
      2. **Only a key, and only of a type some placeholder actually is, is a candidate.**
         `GraphUpdatesTestBase.MyDiscriminator` throws from `GetHashCode` on purpose, so an
         unguarded set lookup over every value in the request fails on a property that could
         never have been a key.
      3. **If no *real* value appears, put the client's placeholder back and behave exactly as
         before.** Adopting EF's own temporary value instead is a second placeholder doing the
         first one's job, and the join row of a many-to-many between two new entities came out
         pointing at neither. Deciding this by asking the value generator — the obvious way —
         got it wrong on SQLite and wrote a foreign key of `0`.

      Ordering changed with it: `Deleted` first (S3c-6's rule only ever concerned `Deleted` vs
      `Added`), then everything else in availability order, so a principal is tracked before
      whoever borrows its key — the borrower's own key may *be* that value, and by then EF will
      not let it change.

### S3c-8 — store-generated keys are not generated on Tier A

**Diagnosis; superseded by S3c-9 above.** Kept because the two reverted attempts below are the
reason S3c-9 looks the way it does.

A client `Added` entity carries a placeholder key (`int.MinValue + n`) flagged in
`ChangeEntry.TemporaryProperties`. The server sets that value on the object, tracks it, sets
`State = Added`, then flags the property temporary — and expects the store to replace it. On
Tier B it does. **On Tier A nothing ever does**, so the placeholder is stored and returned to
the client as though it were a real key, which is why `new1.Id` is still negative after
SaveChanges. Two facts in EF's source settle it:

- `InMemoryIntegerValueGenerator.GeneratesTemporaryValues` is **`false`** — EF's InMemory
  provider assigns the real key at `Add` time via a *value generator*, not at save time, and
  `InMemoryTable` only ever `Bump`s that generator from inserted rows. Nothing fills a temporary
  value at save.
- `PropertyEntry.IsTemporary`'s setter is `InternalEntry[Metadata] = CurrentValue;
  MarkAsTemporary(Metadata, value);` — it *keeps* the current value and flags it. It does not
  ask anyone to replace it.

So S1+S2's "store-generated values returned by correlation id" was only ever proven where the
store generates at save time. The plan already said so ("proven on Tier B, where a real store
actually generates them"); what is new is that Tier A silently returns a placeholder rather
than failing.

**Attempt 1 (reverted).** Leave a temporary *non-foreign-key* property unset so value
generation actually runs — EF only generates for a property still holding its sentinel — then
remap every temporary *foreign* key from a `placeholder → server value` map in a second pass.
The client already flags both (`OptionalSingle2 temp=[Id,BackId]`), so the information is
there. **Measured 156 → 187**, and the new failures were 264 `NullReferenceException`s *inside
the test bodies* — navigations coming back null, i.e. the graph reassembled wrong. Reverted.

The idea is not necessarily wrong, but it needs the case the measurement exposed: an entity
whose primary key *is* its foreign key (`RequiredSingle1.Id`, every owned dependent) has no
key of its own to generate, and the two-pass remap writes a key property through a tracked
entry — which is the same refusal the 24 owned-collection failures already show. A fix has to
distinguish those before it can pay.

---

- [x] **E1.** A query that ends in `ToList` / `ToArray` / `AsEnumerable` is no longer shipped.
      Those operators do not translate — they are the point at which a query *stops* being a
      query — and shipping a subtree ending in one asked the server to execute it as a terminal
      operator, which answered "could not be translated" for a query it would have run happily
      one call earlier. Descending past them ships the query and leaves the materialization on
      the client. **119 → 111, nothing broken.** ✅ `<this commit>`

      Worth recording that the family was **misdiagnosed** in the classification as the
      client-evaluation guard being over-broad. It was not the guard at all: the messages with no
      "Additional information" clause came from the *server*, and reading them properly is what
      pointed at the terminal operator.

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
| 8 | `First/Single/Last_over_custom_projection_compared_to_null` | ~~known limitation~~ — **overturned in X4** |
| 6 | Correlated subquery under a client projection the rewrite cannot reach | C-phase tail |
| ~36 | Value mismatches, one to four tests each | individually triaged |

### ~~The custom-projection-compared-to-null limitation~~ — **not a limitation (X4)**

> ⚠️ **Overturned 2026-08-02.** Kept here in full because it was wrong in an instructive way,
> and because "genuine limit, not a gap" is the sort of claim that stops anyone looking again.

The original entry read:

> `Where(c => c.Orders.Select(o => new { o.OrderID }).First() == null)` constructs an anonymous
> type **inside a predicate**, where the value never crosses the wire — but the server would
> still have to *construct* it, and it has no such type. EF's InMemory provider manages only
> because it shares an `AppDomain`; no network transport could. Rewriting the construction into
> a `ValueTuple` does not save it either, because the predicate compares the result to `null`
> and a tuple is a struct.

Two things were wrong.

**The diagnosis named the wrong symptom.** These queries were not failing because the server
could not construct the type. They were failing with *"Sequence contains no elements"* — the
predicate ran on the client, where LINQ-to-Objects applies `First` strictly, while SQL answers
an empty subquery with `null`. The tell was there all along: the `*OrDefault` variants
**passed**, and only the throwing operators — `First`, `Single`, `SingleOrDefault`, `ElementAt`
— failed. A limitation of the type boundary would not have cared which operator was used.

**The dismissal was half an argument.** "A tuple is a struct" is true, and it is a reason not to
use `ValueTuple`, not a reason the problem is unsolvable. `Tuple<>` is a reference type, is
already on the allowlist, and makes `== null` mean what it says.

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

- [x] **T5.** SQLite's lack of `APPLY` mirrored from EF's own `Northwind*QuerySqliteTest`
      (18 tests × 2), asserting the provider's own `SqliteStrings.ApplyNotSupported`. Tier B
      **74 → 38**, suite **164 → 128**. ✅ `<this commit>`

      Tier B's residual has now converged onto the *same taxonomy as Tier A* — 16
      custom-projection-compared-to-null, 8 navigation reads, 6 null-reference, 2 correlated
      subquery. That agreement is the finding: the relational tier confirms the residual is this
      provider's, not InMemory's.

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

> ⚠️ **Corrected 2026-08-02 (X1).** "All ten of the NRE failures are this" was true when written
> and is not true now — A6 and E1 changed the residual underneath it. Measured against the
> current 111, the `NullReferenceException`s are
> `Select_GetValueOrDefault_on_DateTime_with_null_values`, `Reverse_in_join_inner`(`_with_skip`)
> and `Entity_equality_contains_with_list_of_null`; **none** is a `GroupJoin` transparent
> identifier. The transparent-identifier family shows up as *navigation-read refusals*
> (`Multiple_select_many_with_predicate`, `Navs_query`) instead. The stale count was carried into
> `transparent-identifiers.md` §1 and became X1's stated target, which is how a phase came to be
> aimed at a family that no longer existed.

---

## Phase X — transparent identifiers ([`transparent-identifiers.md`](transparent-identifiers.md), ADR-011)

- [x] **X0.** Design session: read EF's `TryFlattenGroupJoinSelectMany` at source, wrote the
      spec, recorded [ADR-011](decisions.md#adr-011). ✅ `db5dcdd`

- [x] **X1.** Mirror `TryFlattenGroupJoinSelectMany` on the client, before the boundary analysis.
      **First attempt measured `111 → 111` and was reverted. The revert was a mistake** — see X6,
      which restores it for 63 → 49. ✅ `db5dcdd` (attempt) / `<this commit>` (corrected)

      The attempt found one real thing: **EF's rewrite is not separable from EF's pipeline.** The
      substitution reconstructs the transparent identifier including its grouping member, so the
      emitted join names a parameter it does not bind. EF's projection binding drops the dead
      member before anything compiles; this provider compiles the residual and gets
      *"variable 'orders' … referenced from scope ''"*. That broke 10 passing tests, and the
      free-parameter guard added to fix it is still there and still load-bearing.

      It also produced a wrong conclusion, recorded here because it is the more instructive half.
      With the guard the phase measured exactly neutral, and I read "my change did nothing" as
      "the target family does not exist" — writing into two documents that no `NullReferenceException`
      was a `join … into … from … DefaultIfEmpty` shape. **They all were.**
      `Select_GetValueOrDefault_on_DateTime_with_null_values` and `Reverse_in_join_inner`(`_with_skip`)
      are that shape verbatim. The same evidence equally supported "my change did nothing
      *because it never ran*", which is what had happened, and that hypothesis costs one probe to
      separate from the other. See X6.

- [x] **X2.** Verification harness — `RewriteVerifier` rewrites, re-analyzes, and keeps a
      candidate only when it is well formed *and* ships strictly more.
      **111 → 111 of 5222, as designed**: it guards a rewrite that does not exist yet, so any
      test movement would have meant it was doing something it should not. ✅ `<this commit>`

      Ordered *before* X3 rather than between the two rewrites, since there is no X1 to sit
      between. X1 is the argument for it: it grew an ad-hoc free-parameter check mid-flight,
      which is this step arriving late and in the wrong shape. That check is now
      `RewriteRejection.OpenTree`, with X1's actual broken tree as its regression test —
      a `Join` whose result selector reconstructs the transparent identifier while the join
      binds only the outer and inner elements.

      The measure is **query operators left on the client**, not node count: replacing an
      anonymous type with a `ValueTuple` changes a tree's size without moving anything across
      the boundary, whereas operators only ever change sides under these rewrites.

      Four rejection reasons, each mutation-tested by disabling it — `TypeChanged`, `OpenTree`,
      `CorrelatedFragment`, and the strictness of the comparison itself (`<` weakened to `<=`).
      Every one costs at least one test, as does the counter's skip over already-shipped
      subtrees.

- [x] **X3.** `ValueTuple` re-carry for transparent identifiers, under both guards of
      `transparent-identifiers.md` §4. **111 → 101 of 5227, nothing broken**, and the target
      family moved in *both* tiers (`Multiple_select_many_with_predicate` on InMemory and on
      SQLite), which §7 required. ✅ `<this commit>`

      The condition implemented is not "is it a transparent identifier" — that is a fact about
      the C# compiler, not about the tree — but the property that matters: **the type is created
      inside the query and never reaches its result.** No caller can observe it, so nothing is
      owed a reassembly.

      Three corrections, each found only by running the suite:

      | | |
      |---|---|
      | `Expression.New(ctor, args)` builds a tuple EF cannot see through | **111 → 323.** EF collapses `new { c, o }.c` via `NewExpression.Members`; without members `t.Item1` is an opaque field read and every navigation out of a slot stops translating. Supplying the fields as members: 323 → 116. |
      | `.Cast<object>()` hides the carrier from the result type while the value still escapes | `Take_with_single_select_many` returned a boxed tuple. The conversion has to be caught where it happens. |
      | `Expression.Lambda` re-inferring the delegate type | `SelectMany`'s collection selector is `Func<T, IEnumerable<X>>` but its body is an `ICollection<X>`; inference narrows it, the rebuilt call stops matching, and the rewrite is discarded **silently**. 107 → 101, and until it was fixed the target family did not move at all. |

      Four SQLite results moved from passing to `ApplyNotSupported` —
      `SelectMany_with_selecting_outer_element` and
      `Select_nested_collection_deep_distinct_no_identifiers`. **EF's own SQLite suite has
      overridden both all along**; this provider was passing them only because the split
      client-evaluated them instead of translating. Adopting EF's overrides verbatim is
      convergence with the reference provider, not suppression — and it is worth noticing that
      the previous "pass" was the weaker result.

      Three `QuerySplitterTest` cases changed shape rather than expectation. The two `EF.Property`
      ones now ship to the server, which is right — a real server translates `EF.Property`, and
      the spec suite's `NorthwindEFPropertyIncludeQuery*` tests confirm it — but the unit tests'
      LINQ-to-Objects stand-in cannot. They were rewritten against `ClientRow`, a
      constructor-built carrier the re-carry deliberately does not touch (the compiler fills
      `NewExpression.Members` only for anonymous types), so the client-side path they exist to
      cover stays reachable. The navigation-refusal test kept its guard the same way, and gained
      a sibling asserting that the anonymous-carrier version of the same query now answers
      **1** on the server rather than needing the refusal at all.

      Guards mutation-tested: the sequence-in-a-slot check, the `Cast` escape and the
      result-type exclusion each cost a unit test when disabled. The delegate mapping and the
      tuple members are covered by the suite rather than by unit tests — 107 → 101 and
      116 → 323 respectively — which is recorded here rather than papered over with a test that
      would not have caught them.

- [x] **X4.** A carrier compared to `null` is carried by a **reference-typed** `Tuple<>` rather
      than a `ValueTuple`. **101 → 67 of 5227, nothing broken** — the whole
      `First/Single/SingleOrDefault/Last/ElementAt_over_custom_projection_compared_to_null`
      family, on both tiers. ✅ `<this commit>`

      This overturns the "genuine limit of the type boundary" recorded above; see that section
      for what the original diagnosis got wrong.

      One implementation trap, and it cost a full suite run of zero movement: the null has to be
      recognised **through the comparison**, not through its own type. The C# compiler emits
      `anonymous == null` with the null constant typed `object`, so keying off the constant marks
      `object` as null-compared and never the carrier. The rewrite then produced
      `ValueTuple<int> == object`, `Expression.Equal` refused it — *"Reference equality is not
      defined for the types …"* — and the catch in `Rewrite` discarded the whole thing in
      silence. That silence is the cost of the catch, and is why the probe went in rather than
      another guess.

      `TupleCarrier` now has both families and picks members accordingly: fields on a
      `ValueTuple`, properties on a `Tuple`. Mutation-tested — forcing the value family back
      costs the new unit test.

- [x] **X5.** Correlated subquery under a client projection — the "operator pushdown"
      `projection-split.md` §7 deferred. **67 → 63 of 5230, nothing broken**, both tiers.
      ✅ `<this commit>`

      The shape is `from c in cs from g in (from o in os where c.CustomerID == o.CustomerID
      select new { o.OrderDate, c.City }) select g`. `ProjectionRewriter` rewrites the *inner*
      projection in place, which leaves a server-ok subtree still referencing `c` — an open
      fragment — and `RejectOpenFragments` throws. The fix is to carry the **returned** type as a
      tuple as well and rebuild it in one `Select` at the root, so the enclosing `SelectMany`
      ships whole.

      > ⚠️ **This was measured at 67 → 72 and reverted first, on a verdict that was wrong twice
      > over.** Both misreadings are worth more than the fix.
      >
      > **"Only Tier A moved."** The Tier B result was `ApplyNotSupported` — the rewrite reached
      > SQL and *SQLite* declined, which is where EF's own SQLite suite has had an override for
      > this test all along. Both tiers moved; one of them moved to the reference provider's
      > behaviour. Reading a store limitation as a non-move is the second time this milestone
      > that a "regression" was really convergence with EF, and the check is cheap: grep
      > `subrepos/efcore/test/EFCore.Sqlite.FunctionalTests` before believing a SQLite delta.
      >
      > **"It decomposes a `GroupBy`."** It does not. The shipped query was
      > `Select(Select(GroupBy(…)))` rather than `Select(GroupBy(…))` — the aggregate still
      > travels with its `GroupBy`, one operator further in. The real fault was **redundancy**:
      > the root rebuild and `ProjectionRewriter` build the same thing, so the second rewrote the
      > first's output into a pointless tuple-to-tuple hop. Naming that "the failure mode that
      > cost 136 tests" was alarm, not diagnosis.

      Three things it needed beyond the reverted attempt, each a real defect rather than a
      tuning knob:

      | | |
      |---|---|
      | the rebuild is handed to `ProjectionRewriter` as already-reassembled | otherwise both passes rewrite the same projection |
      | a carrier reached by `FirstOrDefault`/`SingleOrDefault`/`LastOrDefault`/`ElementAtOrDefault`/`DefaultIfEmpty` must be reference-typed | `FirstOrDefault` over a `ValueTuple<string,int>` yields `(null, 0)` — a row that looks real — where the anonymous type yielded `null`. Same rule as X4's null comparison, different trigger. |
      | the rebuild must pass absence through | reading slots out of that `null` turns the answer it was meant to give into a `NullReferenceException` |

      Two SQLite tests reached `ApplyNotSupported` for the first time and took EF's own
      overrides. All four decisions mutation-tested against unit tests.

      **Still open in this family:** `SelectMany_correlated_subquery_hard` (2) and
      `SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_…`
      (4). The first is a three-level correlation with `Take` at each level; the second reads
      unmapped CLR properties. Both are genuinely harder than the shape fixed here.

- [x] **X6.** Revive X1's flattener, matching `Enumerable` as well as `Queryable`.
      **63 → 49 of 5232, nothing broken**, both tiers. ✅ `<this commit>`

      X1 fired on nothing because it matched only the `Queryable` overloads. EF normalizes
      `Enumerable` into `Queryable` (`TryConvertEnumerableToQueryable`) *before* its matcher runs,
      so by then everything is `Queryable`; nothing does that here, and
      `from o in grouping.DefaultIfEmpty()` binds to **`Enumerable.DefaultIfEmpty`** because the
      grouping is an `IEnumerable<T>`. `StripDefaultIfEmpty` therefore never stripped it,
      `IsCorrelated` then saw a method call it did not recognise on the spine, and the rewrite
      declined — silently, on every query it was written for.

      Fixed 16: `Select_GetValueOrDefault_on_DateTime_with_null_values`, `Reverse_in_join_inner`,
      `Reverse_in_join_inner_with_skip` (all both tiers), `Join_GroupBy_Aggregate_with_left_join`,
      and `Left_join_with_tautology_predicate_doesnt_convert_to_cross_join` — the last by
      **deleting** our SQLite `ApplyNotSupported` override, which EF's suite does not have and
      which was papering over the limitation this removes.

      The free-parameter guard from X1 is kept and is load-bearing. EF's correlated-collection
      guard is mirrored too and is **inert on this suite** — disabling it changes no test. Kept
      because it is EF's and declining is the safe direction, recorded as untested rather than
      claimed as covered.

---

## Phase Z — residual sweep

Small families with unrelated causes, taken one at a time. Each is measured on its own.

- [x] **Z1.** Hold the concurrency critical section across the residual, not only the round trip.
      **49 → 45 of 5232, nothing broken.** ✅ `<this commit>`

      The round trip was already guarded. The residual was not — and the residual is where a
      client-side projection actually runs, which is precisely what
      `Throws_on_concurrent_query_first`/`_list` stage: a second use of the context *while a
      projection is executing*. This provider answered where every other one throws.

      Held per row rather than per query, as EF's own enumerators do (`InMemory`'s
      `QueryingEnumerable.MoveNext`), so a caller legitimately interleaving two queries is not
      punished for it. EF's detector is re-entrant per thread, so it nests harmlessly inside the
      round-trip section.

- [ ] **Z2.** Query filters that close over context state — `Materialized_query_parameter`,
      `Materialized_query_parameter_new_context`, `Projection_query_parameter` (6).

      **Diagnosed, not a bug in the split.** `HasQueryFilter(c => c.CompanyName.StartsWith(TenantPrefix))`
      captures a property of the *context*. EF applies query filters during translation, which in
      this architecture happens on the **server** — so the filter is parameterized from the
      *server's* context instance. The measured answer is 7 rows in every case, which is the
      server default `TenantPrefix = "B"`; the client's `"F"` never crosses the wire.

      This is an architectural consequence of [ADR-006](decisions.md)'s capture point, not an
      oversight, and it needs a decision rather than a patch. The options are for the client to
      apply filters itself before shipping (duplicating EF's filter machinery, which handles
      inheritance and navigations) or to ship filter parameter values with the request (which
      requires knowing them before filters have been applied). Wants an ADR.

- [x] **Z3.** A projection inside a *collection selector* — reassembly hoisted above the
      `SelectMany`. **41 → 33 of 5235, nothing broken**, both tiers. ✅ `<this commit>`

      ```csharp
      Customers.SelectMany(c => c.Orders.Select(o => new {
          OrderProperty = ClientMethod(o), o.OrderDetails, CustomerProperty = c.ContactName }))
      ```

      Rewritten in place, the client reassembly sits *inside* the collection selector, which makes
      the whole `SelectMany` client-side: the source ships alone and the residual reads
      `Order.OrderDetails` off rows the server never sent. Hoisting it above the `SelectMany`
      lets the join ship.

      One thing beyond a move: the fragments must be collected against the **outer** parameter as
      well as the inner one. After the hoist `c` is out of scope, so `c.ContactName` has to travel
      in a slot of its own — the in-place rewrite never needed to carry it, because `c` was still
      in scope where it landed. Mutation-tested: dropping the outer parameter costs the test.

      Matched narrowly, at the two-argument `SelectMany` — which `IsResultSelectorOperator` does
      not consider at all, since its lambda returns `IEnumerable<TResult>` rather than `TResult`.
      That is why this family never entered the rewrite path.

      **All eight fixes are convergence with the reference providers, not new answers.** EF's
      InMemory suite has had `Assert.ThrowsAsync<NotImplementedException>` on these tests all
      along (issue #21200 — joins between sources with client evaluation), and EF's SQLite suite
      has `ApplyNotSupported`. This provider previously refused the query *itself* and never
      reached either limitation; now it reaches both, and the overrides are EF's, copied.

      > ⚠️ **The plan previously said all four tests in this family "have the same shape". They do
      > not — they share a *message*.** Two are the `SelectMany` shape fixed here; the other two
      > are not, and are unaffected:
      >
      > - `SelectMany_with_client_eval_with_constructor` — a nested `SelectMany(…).ToArray()`
      >   inside a DTO **constructor argument**, not a collection selector.
      > - `GroupBy_Count_in_projection` — a *filtered collection* carried in an anonymous carrier
      >   and read by the next projection. That is an X3 carrier case the "no sequence in a slot"
      >   guard declines. Worth revisiting now that Z5 materializes collections: the guard exists
      >   because a **queryable** in a slot asks SQL to navigate back out of a projected tuple, and
      >   a materialized `List<T>` may not have that problem. Untested — the guard cost 107
      >   failures once, so it gets its own experiment.

- [x] **Z6.** A client-only key with no value equality is a translation failure.
      **33 → 31 of 5237, nothing broken** — `OrderBy_multiple_queries`. ✅ `<this commit>`

      ```csharp
      join o in os on new Foo { Bar = c.CustomerID } equals new Foo { Bar = o.CustomerID }
      ```

      `Foo` is a type the server cannot name, so the join lands on the client — where the keys are
      compared with `EqualityComparer<Foo>.Default`, and `Foo` does not override `Equals`. That is
      reference equality between two freshly allocated objects: every row fails to match and the
      query answers **nothing**. No exception, no log line, an empty result that looks like data.

      The guard tests the type's **equality**, not its origin. An anonymous type in the same
      position stays allowed, because the compiler gives it structural `Equals` and the client
      comparison then means what the query said. Refusing client-only construction outright is
      the type boundary — this milestone's whole subject — and cost 235 tests when tried in C5.
      Both sides unit-tested; the guard and the `Equals` check are each mutation-tested.

      Worth recording what this was *not*. The obvious reading was `c.IsLondon`, the `[NotMapped]`
      property in the same query, for which EF even has a dedicated
      `QueryUnableToTranslateMember`. But the spec tests that read it **already pass**: the query
      ships and the *server's* EF raises that error for us. Fixing the unmapped property would
      have changed nothing here.

- [ ] **Z4.** Client evaluation *forced by the type boundary*, where EF refuses outright —
      `Select_GroupBy_SelectMany` and `Where_query_composition6`-style tests that assert a
      translation failure this provider does not produce.

      Not a bug so much as an unmade decision. The client-evaluation guard (phase C5) fires on
      client **code** — a method the server cannot run. It deliberately does not fire when an
      operator lands on the client because its type crossed the boundary, since that is this
      milestone's whole subject and treating it as a failure cost 235 tests. But EF's contract is
      that anything outside the final projection must translate or throw, and where EF throws and
      this provider quietly answers, the two have diverged in a way a caller can observe.

      Deciding this needs a policy, not a patch: either the boundary-forced residual is legitimate
      (and these spec tests are permanently red, recorded in an ADR), or it is not (and the guard
      widens, at a cost that has to be measured). Both were measured once — see C5 — and the
      current line was chosen with evidence; what is missing is the ADR saying so.

---

## The residual, re-classified (2026-08-02 — measured at 45 of 5232)

Produced by `eng/measure.sh` plus a run that captured every failure's message, then clustered by
cause rather than by test class. Two clusters below were previously counted as unrelated singles;
finding them is the whole reason to re-cluster rather than work down the list.

| # | Cause | Where |
|---|---|---|
| **14** | Projection inside a **collection selector** — reassembly lands in the wrong place, navigation read from rows never sent | **Z3** |
| **6** | Query filter closing over context state — parameterized from the *server's* context | **Z2** |
| **6** | Correlated subquery, hard forms — three-level with `Take`, and unmapped CLR properties | X5 tail |
| **4** | **Collection-valued fragment shipped as `IQueryable<T>`** — EF requires a materialized collection in a final projection | **Z5 (new)** |
| **4** | Spec asserts a translation failure this provider does not produce | **Z4** |
| **11** | Singles and the compliance test | — |

### Z5 — a collection fragment must be materialized before it ships ✅ **done, 45 → 41**

`AsQueryable_in_query_server_evals` and `Complex_query_with_group_by_in_subquery5` were filed as
unrelated. They fail with the **same** EF message, `CoreStrings` —

> The query contains a projection '…' of type '…'. Collections in the final projection must be an
> `IEnumerable<T>` type such as `List<T>`. Consider using `ToList` …

`ProjectionRewriter` picks the largest server-evaluable fragment of a projection body, and for
`Select(c => c.Orders.…Take(1).Select(o => new { o.OrderDate }).ToList())` that fragment is the
`Take(1)` subquery — typed `IQueryable<Order>`. It then goes into a tuple slot verbatim, so the
shipped projection returns an `IQueryable<T>` and EF refuses it before running anything.

The fix is local: a fragment whose type is a queryable must be materialized (`ToList`) on the way
into the slot, and the client-side reassembly must read it back as a sequence — the slot is then
a `List<T>` where the body expected an `IQueryable<T>`, so the substitution needs an
`AsQueryable()` to keep the residual's operators bound.

Worth noting against **E1**, which taught the splitter to descend past `ToList` because shipping a
query that ends in one asked the server to translate a materialization. That rule is right at the
*end of a query* and wrong *inside a projection*, where EF requires the `ToList` to be there. Same
operator, opposite meaning, decided by position.

**Implemented, 45 → 41 of 5234, nothing broken** — `Complex_query_with_group_by_in_subquery5` and
`AsQueryable_in_query_server_evals`, both tiers.

The first attempt materialized every queryable fragment and **broke three tests that assert this
exact error**: `Select_correlated_subquery_ordered_returning_queryable_in_DTO_throws` calls
`AssertInvalidMaterializationType`, and `Mixed_sync_async_query` expects the same
`InvalidOperationException`. Their projections declare an `IQueryable<T>` *member* — the queryable
is the answer the caller asked for, and EF is right to refuse it. Materializing suppressed an
error this provider is supposed to raise.

The discriminator is whether the projection **composes over** the fragment: `frag.Select(…)` is an
intermediate and gets materialized, `new Dto { Orders = frag }` is the answer and is left for EF
to reject. Read off the body's own operators, so no type analysis is needed.

Both sides are unit-tested, and all three decisions — materialize, re-queryable, and the
discriminator — are mutation-tested. The shipped shape turned out to be
`ValueTuple<List<ValueTuple<string>>>` rather than `ValueTuple<List<Book>>`: the inner projection
is rewritten first, so only the projected column travels, not whole rows. That is W1 holding
through a nested collection, and the test asserts it.

### Two cheap checks that reclassify a failure

Both are now in CLAUDE.md, because each has cost a wrong verdict:

- A newly-red **SQLite** test — grep `subrepos/efcore/test/EFCore.Sqlite.FunctionalTests` first.
  If EF overrides it, the query now reaches SQL and this is convergence. (`Reverse_without_explicit_ordering`
  was checked this way and is **not** overridden by EF, so it stays a real failure.)
- A count that **did not move** — establish that the code ran before concluding the target does
  not exist.

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
| 2026-08-02 | 5098 | 111 | 5222 | after X2 (rewrite verifier — no movement, by design) |
| 2026-08-02 | 5113 | 101 | 5227 | after X3 (carriers re-carried as tuples; both tiers moved) |
| 2026-08-02 | 5147 |  67 | 5227 | after X4 (reference-typed carrier when compared to null) |
| 2026-08-02 | 5154 |  63 | 5230 | after X5 (returned carrier re-carried, rebuilt once at the root) |
| 2026-08-02 | 5170 |  49 | 5232 | after X6 (GroupJoin flattening, matching Enumerable too) |
| 2026-08-02 | 5174 |  45 | 5232 | after Z1 (concurrency section held across the residual) |
| 2026-08-02 | 5180 |  41 | 5234 | after Z5 (collection fragment materialized before it ships) |
| 2026-08-02 | 5189 |  33 | 5235 | after Z3 (collection projection reassembled above the SelectMany) |
| 2026-08-02 | 5193 |  31 | 5237 | after Z6 (client-only key without value equality is refused) |
