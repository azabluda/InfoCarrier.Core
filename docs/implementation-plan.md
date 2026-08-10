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

## Phase C — the last 41 bases (roadmap M6)

**Why this phase exists.** Roadmap M6 says every spec base ends up either implemented or in
`IgnoredTestBases` **with a stated reason** — nothing silently forgotten. The compliance test
reports **41** unimplemented, and the standing note deferred 32 of them (`Associations.*` +
`BulkUpdates.*`) pending a scope call. **Scope call given 2026-08-09: adopt them all.** This phase
is that work, and the tier verdict for each base is the part that has to be got right — ADR-009,
A79 and A81: a base belongs to **exactly one** tier, the tier is decided by which store EF itself
ships a test on, and where both exist the tier that *translates* is the one whose green means more.

- [x] **C0. The tier audit.** No code change; the table is the deliverable. ✅ `<this commit>`

      **The single most decisive fact, checked first:** `EFCore.InMemory.FunctionalTests` contains
      **no `Associations` directory and no `BulkUpdates` file at all** — 0 against SQLite's 40-odd
      and 18. So all 32 of the deferred bases are **Tier B**, unambiguously, with no "could go
      either way" to adjudicate. The standing note's premise ("no InMemory counterpart, therefore
      out of scope") was right about the fact and wrong about the conclusion, exactly as A79 found
      for `FunkyDataQuery`.

      **27 bases are 20 classes.** `ComplianceTestBase.Implements` walks base types transitively, and
      the three concrete families each derive from the shared `Associations*TestBase` — e.g.
      `NavigationsCollectionTestBase<T> : AssociationsCollectionTestBase<T>`. So adopting the leaves
      satisfies the seven shared bases for free; there is nothing to write for them.

      **The fixtures are in the core assembly**, which is what makes this affordable and is the
      opposite of B3d's finding. `AssociationsQueryFixtureBase`, `AssociationsData`,
      `AssociationsModel`, `NavigationsFixtureBase`, `OwnedNavigationsFixtureBase` and
      `ComplexPropertiesFixtureBase` all live in `EFCore.Specification.Tests`. The relational
      assembly adds only *mapping-strategy* variants — `ComplexJson`, `OwnedJson`,
      `ComplexTableSplitting`, `OwnedTableSplitting` — which the compliance test does not ask for
      and which would each need a hand-mirrored `ToJson()`/table-splitting model. **Not adopting
      those is a deliberate line**, and it is where the ~630-line mirror B3d priced would reappear.

      | # | Base(s) | EF InMemory | EF SQLite | Tier | Batch |
      |---|---|---|---|---|---|
      | 7 | `Query.Associations.Navigations.*` | — | ✔ | **B** | C1 |
      | 6 | `Query.Associations.OwnedNavigations.*` | — | ✔ | **B** | C2 |
      | 7 | `Query.Associations.ComplexProperties.*` | — | ✔ | **B** | C3 |
      | 7 | `Query.Associations.Associations*` (shared) | — | ✔ | **B** | *covered transitively by C1–C3* |
      | 5 | `BulkUpdates.*` | — | ✔ | **B** | C4 |
      | 1 | `LoggingTestBase` | ✔ | — | **A** | C5 |
      | 1 | `ModelBuilding101TestBase` | ✔ | — | **A** | C5 |
      | 1 | `EntityFrameworkServiceCollectionExtensionsTestBase` | ✔ | — | **A** | C5 |
      | 1 | `ApiConsistencyTestBase` | ✔ | ✔ | **neither** | C6 |
      | 1 | `SeedingTestBase` | ✔ | ✔ | A65 | C7 |
      | 1 | `Scaffolding.CompiledModelTestBase` | ✔ | ✔ | **A** | C8 |
      | 2 | `SpatialTestBase`, `Query.SpatialQueryTestBase` | ✔ / ✔ | ✔ / — | **A** | C9 (M7) |
      | 1 | `Query.AdHocJsonQueryTestBase` | — | — (relational only) | **B** | C10 (behind B12) |

      **Four entries in that table are not the mechanical answer, and each is the reason to write a
      table rather than a rule:**

      - **`ApiConsistencyTestBase` has no tier**, and it is the only base here that does not. It
        asserts things about `InfoCarrier.Core.dll`'s own public surface — async suffixes, virtual
        members, `IReadOnly`/`IMutable` metadata pairs — and never touches a store. Both providers
        ship one because both are providers, not because either is a backing store. Putting it on a
        tier at all would be a category error.
      - **`BulkUpdates` looked like a feature and is probably an adoption.** `ExecuteDelete` is
        `source.Provider.Execute<int>(Expression.Call(ExecuteDeleteMethodInfo, source.Expression))`
        — an ordinary query tree through the ordinary pipeline, so it reaches ADR-006's capture
        point and ships. `ExecuteUpdate` builds its setters *before* calling the provider
        (`setterBuilder.BuildSettersExpression()`), so the `Action<UpdateSettersBuilder<T>>` never
        enters the tree either. The client being non-relational is therefore not obviously a
        blocker: the server is SQLite and it supports both. **Nothing in the roadmap mentions
        `ExecuteUpdate`/`ExecuteDelete`**, so if this turns out to need product work it is new
        scope and stops for a decision rather than being absorbed.
      - **`ComplexProperties` is the one A77 already answered.** EF's InMemory provider does not
        translate a complex property access at all, which is why no InMemory test exists; A77 read
        that as "not adoptable" and B-phase's rule corrects it to "Tier B". This batch is that
        correction being cashed.
      - **`SpatialTestBase` is Tier A, not Tier B**, which is the opposite of where instinct puts
        it. EF ships **both** an InMemory and a SQLite spatial test — SQLite's needs the SpatiaLite
        native library, InMemory's needs only the NetTopologySuite types. Under A81's "exactly one
        tier" and the cheaper-green rule, the InMemory one is the one to take, and it does not need
        a native dependency at all. That may close part of the 32 spatial failures for a managed
        package reference; it stays in M7 because `SpatialQueryTestBase` still wants the store.

- [x] **C1. `Query.Associations.Navigations.*` on Tier B — 7 classes, and 27 bases become 20.**
      **`Failed: 4, Passed: 105, Skipped: 0, Total: 109`.** Test-side only, so the targeted run is
      the honest number. ✅ `<this commit>`

      109 new tests, **105 green on adoption**, and the two things that got them there are both the
      "mirror the relational assembly by hand, with the reason stated" rule:

      **`AutoInclude` was 63 of the first run's 79 failures — one line six times.**
      `NavigationsRelationalFixtureBase` marks all six navigations `AutoInclude()`, and that
      assembly is not referenced. It is not decoration: every query in these seven classes returns
      bare `RootEntity` rows and **no test writes an `Include`**, while
      `AssociationsQueryFixtureBase.AssertRootEntity` walks the whole association graph against the
      fully-populated `AssociationsData`. Without it the associates are simply never loaded —
      `Expected: Root5_RequiredAssociate, Actual: null`, sixty-three times, out of one asserter.
      Safe to mirror because `AutoInclude` is a **core** modelling API: both models make the
      statement for themselves from the same `OnModelCreating`, so it is not something one side
      computes and the other has to agree with (the B4/B6/B12 hazard).

      **Fifteen of the remaining nineteen are EF's own overrides**, six from
      `Navigations*SqliteTest` and nine from `Navigations*RelationalTestBase`, each matched by
      *reason* before being taken (A63) — `SqliteStrings.ApplyNotSupported`,
      `RelationalStrings.DistinctOnCollectionNotSupported`,
      `RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin`, and EF's own
      note that a traditional relational collection navigation cannot be compared reliably.

      **The four left are ours, and three of them are the A28 shape.**
      `AssociationsCollectionTestBase.AssertOrderedCollectionQuery` expects an
      `InvalidOperationException` when `AreCollectionsOrdered` is false — indexing an unordered
      collection is not translatable. `Index_constant`, `Index_parameter` and `Index_out_of_bounds`
      **throw nothing here**, because the indexing lands on the client where a `List` indexes
      perfectly well: a spec test asserting a limitation this provider does not have.
      `Index_column` is the fourth and is the §6 trade again —
      `SqliteException: no such column: r.Id`, the correlated-subquery `OFFSET` shape B14 and B20
      both navigate, and B22 prices.

- [x] **C2. `Query.Associations.OwnedNavigations.*` on Tier B — 6 classes.**
      **`Failed: 13, Passed: 78, Skipped: 0, Total: 91`.** Test-side only. ✅ `<this commit>`

      **The model did not validate at all** on the first run — *"The table
      'RootEntity_NestedCollection' cannot be used for entity type …"*, **69 of 81 failures**, one
      cause: three different owners each have a `NestedCollection` and the default table-splitting
      convention gives all three the same table name. The `ToTable` calls that fix it live in
      `OwnedTableSplittingRelationalFixtureBase` and `OwnedNavigationsRelationalFixtureBase`, ~110
      lines between them, mirrored by hand on B3c's `ToJson()` precedent. **A physical table name is
      the backing store's business and the client has no store — but both sides run this one
      `OnModelCreating`, so if it is not stated here it is not stated at all, and the server's model
      is the one that has to validate.**

      `AreCollectionsOrdered` is mirrored too, and for a sharper reason than it looks: the core
      fixture leaves the base's `true` standing because a *document* store preserves an owned
      collection's order, and the relational fixture sets it false because a relational one does
      not. The backing store here is SQLite, so false is the true answer. No auto-includes were
      needed — an owned dependent comes with its owner's row by definition, which is the fact B10
      turned on.

      Nine of the remaining 21 are EF's own overrides, matched by reason: two
      `DistinctOnCollectionNotSupported`, one `ApplyNotSupported`, two set-operation limits (one of
      them `SetOperationOverDifferentStructuralTypes` — an owned navigation models each property as
      its own structural type even when the CLR type is shared), and four `Contains_with_*` where
      EF asserts only the exception type because an owned collection under a relational store
      carries a synthesized ordinal key and shadow FKs that `Contains` cannot read.

      **The 13 left, classified:**

      | # | Tests | Reading |
      |---|---|---|
      | 6 | `OwnedNavigationsProjection*` | **EF does not run this facet on SQLite at all.** `OwnedNavigationsProjectionSqliteTest.cs` is an empty file whose only content is the comment *"All tests … currently fail because of #26708 (Stop generating composite keys for owned collections on SQLite)"*. Not ours, and not a base to override — there is no override to borrow. |
      | 4 | `Index_*` | C1's, verbatim: three are the A28 shape (no exception where the base expects one, because the indexing lands on the client) and `Index_column` is the §6 trade. |
      | 2 | `Over_associate_collection_projected` | EF's override taken and still red — the exception arrives but not from the same place. Undiagnosed; the one entry here that is genuinely open. |
      | 1 | `Distinct_projected(TrackAll)` | EF's `ApplyNotSupported` override fires for `NoTracking` and not for `TrackAll`. Undiagnosed. |

- [x] **C3. `Query.Associations.ComplexProperties.*` on Tier B — 7 classes. A77 cashed.**
      **`Failed: 47, Passed: 89, Skipped: 0, Total: 136`.** Test-side only. ✅ `<this commit>`

      A77 tried this family on Tier A, found EF's InMemory provider does not translate a complex
      property access at all, and concluded "not adoptable". Phase B's rule says that is the
      definition of Tier B, and it is — 89 green on adoption.

      **Two fixture mirrors, both load-bearing, both found by the failure being uniform:**

      - **`ToJson()` — 134 of the first run's 136.** *"The complex collection property
        'RootEntity.AssociateCollection' must be mapped to a JSON column."* A relational store has
        no other way to hold a complex collection, so on Tier B this mapping is what the family
        **is**. ~20 lines from `ComplexJsonRelationalFixtureBase`. This is the one place C0's "core
        family, not the mapping-strategy variants" line bends, and the distinction that keeps it
        honest is that what is mirrored is the *mapping*, not the `ComplexJson*TestBase` classes —
        those assert SQL and stay unadopted.
      - **`UseTransaction` — all 31 `ComplexPropertiesBulkUpdate` failures, and not one of them was
        about bulk updates.** `AssociationsQueryFixtureBase.UseTransaction` throws
        `NotSupportedException` and every relational fixture overrides it; the stack named the
        fixture one frame in. `facade.UseInfoCarrierTransaction(transaction)`, the same hook
        `StoreGenerated`, `ConferencePlanner` and `OptimisticConcurrency` already use.

      **And that uncovered C0's open question, answered.** With the transaction enlisted, the bulk
      tests reach their real failure: `UnreachableException : Can't call this overload directly`, out
      of `EntityFrameworkQueryableExtensions.ExecuteUpdate<TSource>(IQueryable, IReadOnlyList<...>)`.
      C0 was right that both bulk operations arrive as ordinary query trees and wrong that this
      makes them a pure adoption: **the split evaluates them on the client**, where that overload is
      a marker EF never means anyone to invoke. Shipping `ExecuteUpdate`/`ExecuteDelete` to the
      server instead is product work and **new scope** — the roadmap mentions neither — so it is
      recorded here and not absorbed. See C4.

      The other 16 are relational translation limits — 10 `ApplyNotSupported`, 4 collection-subquery
      projections, 1 `DistinctOnCollectionNotSupported`, 1 set-operation-over-different-types — and
      EF carries overrides for these on its `ComplexJson*` classes. **Not taken in this batch**: they
      are a per-test reason match (A63) rather than a fixture fact, and the batch discipline is to
      adopt, classify, and work failures separately.

- [x] **C4. `BulkUpdates.*` on Tier B — 4 classes covering 5 bases.**
      **`Failed: 158, Passed: 90, Skipped: 9, Total: 257`.** Test-side only. ✅ `<this commit>`

      Adopted knowing they largely fail, which is the batch discipline: a base's failures are left
      red and classified rather than worked. `BulkUpdatesTestBase` is covered transitively by the
      other three; `NonSharedModelBulkUpdates` goes through the same harness
      `NonSharedPrimitiveCollectionsQuery` uses.

      **C0's open question, closed with a number: 136 of the 158 are one thing**, and it is the one
      C3 surfaced — `UnreachableException : Can't call this overload directly`, out of
      `EntityFrameworkQueryableExtensions.ExecuteUpdate<TSource>(IQueryable, IReadOnlyList<…>)`.
      C0 was right that both operations reach a provider as ordinary query trees, and wrong that
      this makes them a pure adoption. **The projection split evaluates the call on the client**,
      and that overload is a marker EF never means anyone to invoke; the operator has to be
      *shipped* instead.

      **That is product work and it is new scope** — neither `ExecuteUpdate` nor `ExecuteDelete`
      appears anywhere in `roadmap.md` — so it is recorded and not absorbed. The shape of the fix is
      already legible from the phase-B work: `ServerBoundaryAnalyzer.IsExecutableQuery` recognises
      terminal operators by declaring type, and `EntityFrameworkQueryableExtensions` is already in
      that list, so the gap is likely narrower than "implement bulk updates" — but it is a wire and
      boundary change and wants its own step.

      **90 pass**, which is the more interesting half: those are the tests whose assertions are
      about the *query* the bulk operation filters on rather than the mutation. The remaining 22
      are ordinary Tier B translation limits — 12 `ApplyNotSupported`, 6 untranslatable LINQ, 2
      `Nullable object must have a value`, 2 the owned-without-owner refusal — and EF carries
      overrides for most of them in `NorthwindBulkUpdatesSqliteTest`, not taken in this batch for
      the same reason as C3's sixteen.

- [x] **C5. The four infrastructure bases — `Logging`, `ModelBuilding101`,
      `EntityFrameworkServiceCollectionExtensions` on Tier A, and `ApiConsistency` on no tier.**
      **`Failed: 6, Passed: 144, Skipped: 0, Total: 150`.** Test-side only. ✅ `<this commit>`

      C0 split these into C5 and C6; they land in one file and one theme, so they are one step.
      None of them runs a query. For the first three "tier" means only which of EF's two suites
      states the base, and for all three it is the InMemory one.

      **`ApiConsistency` is the one base in C0's table with no tier at all**, and it earns its own
      sentence because the mechanical rule would have got it wrong: EF ships one on InMemory *and*
      one on SQLite, which looks like A81's "could go either way". It is neither. The base asserts
      things about `InfoCarrier.Core.dll`'s own public surface — async suffixes, virtual members,
      the `IReadOnly`/`IMutable`/`IConvention` metadata triples, fluent-API return types — and never
      touches a store. Both providers ship one because both are providers. **18 of 19 green**, which
      is a real statement about this provider's API hygiene that nothing else in the suite was
      making.

      The six failures are all provider-plumbing detail, and all worth keeping red:

      | Test | Reading |
      |---|---|
      | `ServiceCollectionExtensions.Required_services_are_registered_with_expected_lifetimes` | a registered service's lifetime differs from EF's expectation |
      | `ServiceCollectionExtensions.Repeated_calls_to_add_do_not_modify_collection` | `AddEntityFrameworkInfoCarrier()` is not idempotent — *Expected 121, Actual 126* |
      | `Logging.Logs_context_initialization_no_tracking`, `…_sensitive_data_logging` | the initialization log line does not compose the way EF's does |
      | `Logging.InvalidIncludePathError_throws_by_default` | an include-path diagnostic this provider does not raise |
      | `ApiConsistency` ×1 | one public-surface convention |

- [x] **C7. `SeedingTestBase` on Tier A — A65's blocker was a route, not a wall.**
      **`Failed: 0, Passed: 4, Skipped: 0, Total: 4`.** ✅ `<this commit>`

      A65 filed this base as blocked and it stayed blocked through Phase B: *"its `SeedingContext`
      takes a `string testId` and has no `DbContextOptions` constructor, so the backend cannot build
      the server's copy."* Every word of that is true, and it is not the whole picture.

      **The base hands *client* context construction to the derived class.**
      `CreateContextWithEmptyDatabase(testId)` is abstract — EF's own InMemory variant just does
      `new SeedingInMemoryContext(testId)` and configures it in `OnConfiguring`. So the awkward
      constructor is only ever the *client's*, and only the **server's** copy needs the ordinary
      one — which is exactly what `serverContextType` has existed for since the Northwind and
      `Inheritance` fixtures. A separate server context with a `DbContextOptions` constructor and
      the same `HasData` closes it.

      And the second half falls out of the first: the client's `EnsureCreated` is a no-op —
      `InfoCarrierDatabaseCreator` reports success because there is no store to create — so the two
      seeded rows the test queries for can only come from the **backend's** database, created from
      the same seeded model. They do. **Green on adoption, no overrides.**

      Worth noting as a rule rather than a one-off: *"the backend cannot build this context"* is a
      statement about **one** of the two contexts, and this provider has a supported way to make
      them different types. A65 concluded from the client's shape that the server was blocked.

- [x] **C8. `Scaffolding.CompiledModelTestBase` on Tier A — and C0's price for it was wrong.**
      **`Failed: 4, Passed: 0, Skipped: 0, Total: 4`.** ✅ `<this commit>`

      C0 called this expensive on the strength of the **112 generated baseline files** EF ships
      beside its own InMemory variant, and that was the wrong thing to look at.
      `CompiledModelTestBase.AssertBaseline` returns early when the baseline directory does not
      exist — *"cannot look for the baseline"* — so the baselines are **opt-in**, and the base's
      real contract is **one abstract member**, `TestHelpers`, which this project has had since
      `F1FixtureBase` needed it. The adoption is 40 lines.

      **All four fail on one thing, and it is a real gap this base exists to find:**

          Unable to find expected assembly attribute [DesignTimeProviderServices] in provider
          assembly 'InfoCarrier.Core'.

      Every EF provider ships an `IDesignTimeServices` implementation and names it in an assembly
      attribute; `InMemoryDesignTimeServices` is ~15 lines. This provider ships neither, so nothing
      can scaffold its model into source — which for a provider whose entire job is moving models
      across a wire is a pointed omission, and one no other base in the suite was asking about.

      **Not fixed here, because it is a dependency decision.** `IDesignTimeServices` lives in
      `Microsoft.EntityFrameworkCore.Design`, and `InfoCarrier.Core.csproj` references only
      `Microsoft.EntityFrameworkCore` and `…Relational`. Adding a third package reference to the
      **product** assembly belongs with M8's productization work, not smuggled into a test adoption.

- [ ] **C9. The two spatial bases: attempted, measured, reverted. The blocker is the wire, not the
      package.** No code change; this is the finding. `<this commit>`

      C0 put these on **Tier A** and that part holds: EF ships an InMemory spatial suite as well as
      a SQLite one, `SpatialFixtureBase` and `SpatialQueryFixtureBase` are in the core assembly this
      project already references, and EF's own fixtures for them are four lines each. No SpatiaLite,
      no native library. The adoption itself was 70 lines and built first time.

      **Then 173 of 173 failed identically**, before any query ran:

          No suitable constructor was found for the type 'LineString'.
          Cannot bind 'points' in 'LineString(Coordinate[] points)'

      `InfoCarrierTypeMappingSource` maps value types, `string` and `byte[]`; a geometry is a
      *reference* type, so it falls through, the mapping source says "not a scalar", and EF's
      convention concludes the only other thing available — that `LineString` is an **entity type**.
      `InMemoryTypeMappingSource`, which this class was copied from, carries an explicit
      NetTopologySuite branch that ours is missing. Mirroring it is ~20 lines and is obviously
      right.

      **And that is where it stops, hard.** With geometries mapped as scalars they travel, and
      `DynamicValueMapper`'s reflective object-shape walk meets `Geometry.Boundary` and
      `Geometry.Envelope` — properties that return geometries. It recurses until the stack overflows
      and **the test host aborts**. That is categorically worse than any number of failures: CLAUDE.md
      already records that a crashed host reports fewer failures because fewer tests ran, and once
      came within one measurement of looking like an improvement. Both changes reverted whole.

      **The real price, now known.** A geometry needs a *wire form* — WKB or WKT — the way B4's
      collections needed a store-independent JSON form. The roadmap already has this parked in the
      right place: **M7, Q7 "spatial Z/M via WKT"**, with requirements §2.8 demanding Z and M
      ordinates survive, which v1 lost. So the sequence is: give the wire a geometry form, then the
      type-mapping branch, then these two bases fall in. They stay reported by the compliance test
      rather than going into `IgnoredTestBases`, because that list is for bases *conceptually
      inapplicable* to a remoting provider and these are merely not built yet — which is exactly the
      distinction that file's own doc comment draws.

- [ ] **C12. The spatial route, read out of ICC v1 — which solved this, in two halves.** No code
      change; this is the finding, and it supersedes C9's "blocked until M7". `<this commit>`

      v1 shipped `SpatialInfoCarrierTest` and `SpatialQueryInfoCarrierTest`, on its InMemory tier —
      so C0's **Tier A** call was right, and so was the reverted type-mapping branch:
      `subrepos/infocarrier-v1/src/InfoCarrier.Core/Client/Storage/Internal/InfoCarrierTypeMappingSource.cs`
      carries the NTS branch **verbatim**, `GeometryValueComparer<>` and all. C9 got that half right
      and stopped one step early.

      **The half C9 was missing is a value-mapper seam, and v1 had one.**
      `IInfoCarrierValueMapper` (v1, `Common/ValueMapping/`) is a chain the result mapper walks
      before its own reflective handling — v1 ships standard members of it for arrays and byte
      arrays. `InfoCarrierNetTopologySuiteValueMapper` is one more, and it is the thing that stops
      the `Geometry.Boundary` / `Geometry.Envelope` recursion dead: a geometry is written as a
      single string and read back with one call.

      **And the seam is why v1 needed no NetTopologySuite dependency in its product assembly.** The
      NTS mapper lives in v1's **test** utilities and is registered by the test store factory —
      `.AddSingleton<IInfoCarrierValueMapper, InfoCarrierNetTopologySuiteValueMapper>()`. An
      application that wants geometries supplies the mapper; the provider stays ignorant of NTS.
      That is a better answer than putting spatial support in the product, and **v2 has no such
      seam at all**: `IDynamicValueMapper` is the whole mapper, not a chain, so there is nowhere to
      register one.

      **Do not copy v1's format.** v1 used `GeoJsonWriter`/`GeoJsonReader`, and GeoJSON has no Z or
      M ordinate — which is exactly the defect requirements §2.8 records as *"v1 lost them"* and
      roadmap M7 Q7 answers with **WKT**. So the route is: add the value-mapper seam (product,
      small, useful well beyond spatial), then a WKT/WKB mapper test-side, then the type-mapping
      branch, then the two bases fall in. **The seam is the piece worth building regardless of
      spatial** — it is the general answer to "a CLR type the wire cannot walk", which is also what
      B23's `IPAddress` was.

- [ ] **C10. `AdHocJsonQueryTestBase`: B3d's price re-checked and it holds.** No code change.
      `<this commit>`

      C3 made this worth re-checking — B3d priced the `ToJson()` mirror at ~630 lines and C3's
      turned out to be ~20 — but the two are not the same shape. Measured:
      `AdHocJsonQueryRelationalTestBase` is **626 lines** and `AdHocJsonQuerySqliteTest` **322**, and
      the core base declares **seven abstract `Seed*` methods** (`Seed30028`, `Seed33046`,
      `SeedJunkInJson`, `SeedTrickyBuffering`, `SeedShadowProperties`, `SeedNotICollection`,
      `SeedBadJsonProperties`) that only those two classes implement — with raw SQL inserting JSON
      documents. C3's twenty lines were a *mapping*; this is a mapping **and** the entire corpus's
      seed data.

      B3d's other half also holds: the corpus is owned JSON collections throughout, so most of what
      it would add lands on **B12**, which is still open. Worth doing after B12 is decided, and hard
      to justify before. Left reported.

- [x] **C11. Phase C measured whole — and it surfaced an intermittent. Diagnosed in full; fixed in C38.**
      **`Total tests: 22102, Passed: 21503, Failed: 382, Skipped: 217`** (`c10b`). No code change;
      this entry is the measurement and the flake. `<this commit>`

      **The adoption, whole.** 150 → 382 failures on 21351 → **22102** tests: **751 new tests, 233
      new failures, and not one previously-failing test changed state** — `comm` between `b21b` and
      `c10` is empty in the FIXED direction and every prior failure is still present. Nothing was
      masked and nothing regressed. Where the 233 land:

      | # | Class | Reading |
      |---|---|---|
      | 158 | `BulkUpdates` | 136 are C4's one defect (`ExecuteUpdate` evaluated client-side); the rest Tier B translation limits |
      | 64 | `Query.Associations` | C1 ×4, C2 ×13, C3 ×47 — classified in those entries |
      | 4 | `Scaffolding.CompiledModel` | C8's missing `[DesignTimeProviderServices]` |
      | 6 | infrastructure | C5's six |
      | 1 | `SqliteSmokeTest` | **the flake, below** |

      **The flake.** `SqliteSmokeTest.A_store_generated_key_comes_back_on_the_client_entity` failed
      in `c10` and passed in `c10b` — **same commit, two runs, one test**. That is exactly the signal
      CLAUDE.md says to stop for, and it is why the honest count is 382 with one known intermittent
      rather than a flat 383.

      What is already known, so the next session does not repeat it:

      - It has **never** failed before — absent from `b18`, `b20`, `b21b`.
      - It **passes in isolation** (12 of 12) and in every pairing tried: with `Seeding`, with
        `CompiledModel`, with all of `BulkUpdates`, with all of `Query.Associations`, with the
        infrastructure four. Only the full run reproduces it, which points at timing rather than at
        any one class.
      - The failure is an identity conflict on a **temporary** key —
        *"another instance with the key value '{Id: -2147482646}' is already being tracked"* — at
        `StateManager.StartTracking`. Two entities in one context ended up with the same temporary.
      - **The correlation id is not the culprit**, though the test's own comment points at it: it is
        an index into the request's `entries` list (`InfoCarrierDatabase` line 373), per-request and
        not shared.
      - `-2147482646` is `int.MinValue + 1002`, so the temporary-value generator is being shared
        across contexts — expected, since EF caches a service provider per equivalent options — and
        the next question is whether anything in this provider's SaveChanges path resets or
        re-reads it non-atomically.

      **Decision 2026-08-09: record this one incident and watch.** Not chased — a single occurrence
      with no second data point costs more to hunt than it returns, and everything cheap has already
      been tried. **A recurrence makes it a defect**: note each sighting here, and act on the
      second, holding any fix to the three-run bar.

      **Second sighting 2026-08-10, in `c24`. It is now a defect.** Eight full runs have been made
      on it: it failed in `c10` and `c24`, and passed in `c10b`, `c15`, `c17`, `c18`, `c19`, `c20`
      and `c23c`. **Roughly one run in four.** It still passes in isolation (12 of 12, checked
      again at `c24`).

      **Not caused by the change `c24` measured.** That change is in
      `ProjectionRewriter.UnbuildableNavigationElement`, which decides how a *collection navigation*
      enters a projection slot. This test runs `SaveChanges` and projects nothing, and the failure
      predates the change by fourteen commits.

      **What the second stack adds.** The first sighting recorded only `StateManager.StartTracking`.
      This one names the caller — `ServerSaveChangesExecutor.TrackOne`, at `entry.State = state` —
      and the entity type is `Blog`, of which **this test creates exactly two in one
      `SaveChanges`**:

          var first = new Blog { Title = "alpha" };
          var second = new Blog { Title = "beta" };
          client.AddRange(first, second);

      So the two colliding instances are the test's own two rows, tracked one after the other in
      **one server context**, and they received the same temporary key.

      **The sharp question, for whoever takes it.** `Blog.Id` is store-generated, so the client
      holds temporary values and deliberately does **not** send them — that is what the correlation
      id exists for, and `A_new_dependent_of_a_new_principal_gets_the_generated_foreign_key`
      documents it. So the server sees `Id = 0` twice, sets `State = Added` twice, and EF generates
      a temporary each time from a generator whose counter is `Interlocked`-decremented. **Two
      calls to that generator cannot return the same number.** Therefore either the second entity
      never reached the generator — it was tracked under a value it already carried — or the two
      entries are not both going through it. `-2147482646` is `int.MinValue + 1002`, so the
      generator is process-wide and about a thousand temporaries have been issued by that point in
      a full run, which is consistent with the failure needing a full run to appear.

      **Strategy set 2026-08-10: do not chase it. Instrument it and carry on.** Dedicated
      reproduction is poor value at one run in four, and the obvious instrument does not work — a
      probe writing a line per tracked entry took the suite from 154 failures to **348** under
      xUnit's parallel collections, so that run measured nothing. C27 is the instrument that does
      work: nothing is written unless the conflict happens, and then the whole request is dumped.
      The next sighting should arrive already diagnosed.

      **Two candidates ruled out by reading, so nobody re-checks them:**

      - **Store isolation.** `SqliteSmokeTest.CreateStore()` uses `Guid.NewGuid().ToString()` as
        the store name with `shared: false`, so no two tests share a `.db` file or a service
        provider. Not a file-naming collision.
      - **Parallelism alone.** The conflict is inside **one** identity map, and EF's temporary
        value generator decrements under `Interlocked`. Two calls cannot return the same number,
        and two contexts have separate identity maps. Concurrency can change *timing* here but
        cannot by itself produce the collision.

      **The candidate that survives, and it is now evidenced.** C27's diagnostic, printed from a
      forced conflict on a passing run, shows the client's placeholders for the two blogs are
      `-2147482647` and `-2147482646` — **exactly the numbers the server's own generator issues**,
      and in the same order. Client placeholder values and server temporary values are drawn from
      two independent counters that both start near `int.MinValue`, so **they occupy the same
      numeric range**, and this executor compares them **by value**:

          var expected = new HashSet<object>(pending.SelectMany(p => p.GeneratedKeys)
              .Select(g => g.ClientValue));

      The comment above that line already states the hazard — *"a false positive needs a stored key
      that equals one of them in the same request"* — and treats it as improbable. It is not
      improbable when the two ranges coincide; it is a question of how far each counter has
      advanced, which is exactly what varies between a full run and an isolated one, and exactly
      what would make the failure rare and order-dependent. **A server-generated key that happens
      to equal a client placeholder in the same request is indistinguishable from a borrowed
      placeholder, and the redirect then points a second row at the first row's key.**

      **What the next sighting must show, to confirm or kill it.** In the dump, compare
      `placeholder values expected in this request` against the keys in
      `entries already tracked` — if a server-issued key appears in the expected set, the
      hypothesis is confirmed and the fix is to stop identifying placeholders by bare value
      (tag them, or namespace the client's range away from the server's). If they are disjoint,
      the cause is elsewhere and the dump still names every entry, its temporary flags and the
      key it landed on. **Hold any fix to the three-run bar.**

      ---

      **Sightings 3 and 4, 2026-08-10 (`c37` and `c37b`), and the instrument did its job.** Both
      dumps are byte-identical and they close the question:

          failing entry: Blog correlationId=1 state=Added temporaryProperties=[Id]
          placeholders resolved so far: -2147482646->-2147482646(tmp=True)
          placeholder values expected in this request: -2147482646, -2147482645
          every entry in this request:
            Blog correlationId=0 state=Added temporaryProperties=[Id] generatedKeys=[Id=-2147482646] borrowedReferences=[] keyValuesSent=[]
            Blog correlationId=1 state=Added temporaryProperties=[Id] generatedKeys=[Id=-2147482645] borrowedReferences=[] keyValuesSent=[]
          entries already tracked, with the keys they landed on:
            Blog correlationId=0 state=Added key=[Id=-2147482646(tmp=True)]

      **The range hypothesis is confirmed. The mechanism named alongside it is not, and the
      difference is the whole fix.** `borrowedReferences=[]` on both entries: nothing was ever
      misidentified as a borrowed placeholder, so *"compares them by value"* is not what fires.
      The two counters coinciding is real; what it collides with is one step earlier.

      **The sequence, read straight off the dump.** Entry 0 is tracked; `entry.State = Added` runs
      EF's own temporary generator, which hands out `-2147482647`; `TrackOne` then overwrites the
      key with the client's placeholder `-2147482646`. Entry 1 is tracked; `entry.State = Added`
      runs the same generator, whose next value is **`-2147482646`** — the value entry 0 is now
      sitting on. The identity map refuses it, and it throws *before* the line that would have
      replaced it with `-2147482645`.

      So the collision is between **EF's freshly generated temporary** and **a client placeholder
      already forced onto a sibling entry**, and it needs the client's counter to be exactly one
      ahead of the server's. A passing run has them level: CLAUDE.md's recorded passing dump shows
      client placeholders `-2147482647`/`-2147482646`, and with those every forced value equals the
      generated one and nothing moves. That is why it is roughly one run in four, why it always
      passes in isolation — both counters start fresh and stay level — and why it needed ten
      thousand tests of unrelated work to appear.

      Fixed in **C38**.

- [ ] **C14. "The spatial failures need SpatiaLite" is wrong, and has been for a while.** No code
      change; this is the correction. `<this commit>`

      CLAUDE.md has said since A64 that the spatial block *"needs the SpatiaLite package"* and the
      roadmap parks it in M7 on that basis. Reading the actual failure says otherwise:

          The 'Point' property 'PointType.Point' could not be mapped because the database provider
          does not support this type.
            at ModelValidator.ValidatePropertyMapping

      **The model does not validate.** No store is involved, and the two classes say why:
      `JsonTypesInfoCarrierTest` is on **Tier A** — its backend is InMemory, which carries the NTS
      branch and maps geometry perfectly well — and `BadDataJsonDeserializationTestBase` has **no
      store at all**; it builds a model and tests JSON deserialization. The provider that "does not
      support this type" is **the client**, and it is the same missing branch in
      `InfoCarrierTypeMappingSource` that C9 found and C12 traced to v1.

      **So the ~20-line branch is worth up to 34 currently-red tests on its own** — 26 `JsonTypes`
      and 8 `BadDataJsonDeserialization` by keyword count in `c10b` — before any spatial base is
      adopted and before any wire form exists.

      **Measure it alone.** C9's stack overflow came from `SpatialQueryTestBase` running queries
      that return geometries through `DynamicValueMapper`'s reflective walk. These 34 do not cross
      the wire that way, so the branch should be safe by itself — but "should be" is exactly the
      kind of claim this repo measures, and the failure mode is a host abort, which is the one
      outcome worse than a red test. **Land the branch on its own, measure, and only then take the
      seam and the spatial bases (C12).**

- [ ] **C13. A non-relational Tier D — raised 2026-08-09, recorded, not started.** A roadmap
      question; ADR-009 owns the tier list. `<this commit>`

      **First, a correction of what prompted it.** C10's *"seven abstract `Seed*` methods only the
      relational classes implement"* is a statement about **EF's own code**, not about a store this
      project lacks: `AdHocJsonQueryTestBase` declares the seeds abstract, and the only
      implementations EF ships are in `AdHocJsonQueryRelationalTestBase` / `AdHocJsonQuerySqliteTest`,
      which insert JSON documents with raw SQL. The cost is that **we would write those seeds by
      hand**. Nothing there argues for another backend.

      **The idea stands on its own evidence, though, and it is better than the thing that prompted
      it.** Both existing tiers are compromised in opposite directions, and Phase C measured both:

      - **Tier A (InMemory) does not translate.** That is why A77 called complex-property queries
        unadoptable, why EF ships no InMemory test for `Associations`, `BulkUpdates`,
        `PrimitiveCollections` or `JsonQuery`, and why all of those had to go to Tier B.
      - **Tier B (SQLite) is relational**, so adopting anything there means hand-mirroring
        relational modelling the client provider is not: C1's `AutoInclude`, C2's `ToTable`
        (69 failures until it was there), C3's `ToJson` (134). Each was found by the failure being
        uniform, and each is a statement the *store* makes that the client has to be told.

      **B12 is the sharpest argument.** The whole 38-failure block is that a JSON-mapped owned
      collection is keyed by its **ordinal in the array** on the server and by the CLR `Id` on the
      client — and *"only the convention that rewrites such a key is relational"*. On a document
      store there is no such convention, so the two models would agree by construction. B12 is not a
      defect of this provider; it is a seam between a non-relational client and a relational server,
      and a non-relational server closes it rather than working around it.

      **The spec suite is already written for one.** The fixtures adopted in C1–C3 name it:
      *"Don't use database value generation since e.g. Cosmos doesn't support it"* twice, and
      *"Cosmos (and possibly others) don't support navigations"*. EF ships `EFCore.Cosmos` in-box
      alongside InMemory, Sqlite and SqlServer.

      **Measured 2026-08-09, and it changes the answer: there is very little to retarget *to*.**
      The proposal is not "add a tier" but "move the classes that fit a document store better" —
      which is right in principle, and ADR-009's own rule then applies in reverse. A base can only
      live on a tier EF ships a test for, and EF's own document provider ships very few:

      | Family | Cosmos | SQLite | InMemory |
      |---|---|---|---|
      | `Associations` | 7 | 41 | 0 |
      | `JsonQuery` | 3 | 3 | 0 |
      | `Northwind` | 9 | 29 | 23 |
      | `BulkUpdates` | **0** | 18 | 0 |
      | `GearsOfWar`, `ComplexNavigations`, `ManyToMany`, `PropertyValues`, `GraphUpdates` | **0** | 41 | 20 |

      **And `EFCore.Cosmos.FunctionalTests` has no `ComplianceTest` at all** — only the two
      *relational* providers do. EF does not hold its document provider to the specification suite,
      so nobody is even tracking that gap.

      So the retarget ceiling is roughly **B12's 38 `JsonQuery` plus some part of the 64
      `Associations`** — call it 40–70, and speculative, since whether a document store keys an
      owned collection the way the client does is the very thing B12 asks. What **cannot** move is
      the biggest block: `BulkUpdates` has no Cosmos counterpart, and its 158 are 136 of our own
      `ExecuteUpdate` defect anyway, which no store fixes. `GraphUpdates`, `ManyToMany`,
      `ComplexNavigations` and `GearsOfWar` cannot move either.

      **MongoDB's provider does not shortcut this.** It supports EF Core 10 on .NET 10, so
      compatibility is not the blocker — but it is *working towards* specification-suite coverage
      rather than having it, so there is no ready-made proof that the suite fits a document store.
      An ephemeral/embedded Mongo would be lighter than the Cosmos emulator; it would not be
      cheaper in adoption work.

      **The honest cost, and the open question.** Cosmos is the in-box candidate and it needs the
      emulator — which makes it **Tier C-shaped** (nightly, Docker, like SQL Server in M7) rather
      than the *lightweight* store the idea asks for. Whether a lightweight embedded document
      provider for EF Core 10 exists is **not something this entry establishes** and should be
      checked before the tier is designed: if there is one, this is cheap and closes B12; if Cosmos
      is the only option, it is an M7-sized commitment and competes with SQL Server for that slot.

- [x] **C15. The NetTopologySuite branch in `InfoCarrierTypeMappingSource`, landed alone.**
      **`Total tests: 22102, Passed: 21522, Failed: 363, Skipped: 217`** (`c15`) — **382 → 363, 19
      fixed, 0 broken, total unchanged.** ✅ `<this commit>`

      C14 said the ~20-line branch was worth up to 34 red tests and that it had to be measured by
      itself, because C9's combined attempt aborted the host. Landed alone: **no abort, nothing
      broken, and the 19 it fixed are exactly the two classes C14 named** — 8 of
      `BadDataJsonDeserialization`'s `Throws_for_bad_point_as_GeoJson` and 11 `JsonTypes`.

      The change is `InMemoryTypeMappingSource`'s branch, which is also v1's verbatim: match
      `NetTopologySuite.Geometries.Geometry` by `FullName` up the base-type chain, build a
      `GeometryValueComparer<>` through `Activator`, pass it as comparer *and* key comparer.
      `InfoCarrierTypeMapping` grew the two comparer parameters `InMemoryTypeMapping` has.
      **No package reference**: `GeometryValueComparer<>` is in EFCore proper and reflects for
      `EqualsExact`/`Copy`, and the type match is on a string, so the product assembly still does
      not know NetTopologySuite exists.

      **The 15 spatial failures left are two blocks, and neither is this provider:**

      | # | Tests | Reading |
      |---|---|---|
      | 8 | `Can_read_write_{point,point_with_M,point_with_Z,point_with_Z_and_M,line_string,multi_line_string,polygon,polygon_typed_as_geometry}` | `NullReferenceException` — no `JsonValueReaderWriter` exists for a geometry, so the JSON round-trip dereferences null. **EF's `JsonTypesInMemoryTest` overrides these exact eight with `Assert.ThrowsAsync<NullReferenceException>`**, and now that the model validates the reason matches ours character for character. A63 says take them; C16 does. |
      | 7 | the `_as_GeoJson` variants of the same eight, less `line_string` | **A64, the `en-SE` locale.** `JsonGeoJsonReaderWriter` (EF's own, in `JsonTypesTestBase`) writes a number with `StringBuilder.Append(reader.GetDecimal())`, which is culture-sensitive: `[2.0,4.0]` is re-emitted as `[2,0,4,0]` and read back as `POINT (2 0)`. `line_string_as_GeoJson` passes by luck — its ordinates are 0 and 1, so the doubled array still starts with the right pair. |

      The nullable variants all pass now, and EF does not override them either — the same tell that
      the eight are a genuine InMemory-shaped limitation rather than an accident of ours.

- [x] **C16. EF's eight spatial `JsonTypes` overrides, now that the reason matches.**
      **`Failed: 9, Passed: 591, Skipped: 0, Total: 600`** across `JsonTypes` +
      `BadDataJsonDeserialization` — **17 → 9**, test-side only so the targeted number is the
      honest one. ✅ `<this commit>`

      The class's own doc comment recorded why these were *not* taken: EF's
      `JsonTypesInMemoryTest` asserts `NullReferenceException` and this provider raised
      `InvalidOperationException` one step earlier, because it mapped no spatial type at all — so
      copying the override would have asserted a symptom this provider did not have (A39). **C15
      removed that difference.** The model now validates, the failure is InMemory's own null
      reader/writer dereference, and A63's bar is met.

      Worth stating as the general rule, because it is the mirror of A63's other half: **an
      override EF has that we could not take is a note to re-check after the thing that made us
      different is fixed**, exactly as an override of ours EF does not have is a workaround to
      delete once the limitation goes.

      The nine left: 7 `_as_GeoJson` (A64, the `en-SE` locale — see C15's table) and 2 decimal
      parameterizations xUnit cannot convert (A64 proper).

- [x] **C17. The value-mapper seam — new public API, ADR-012.**
      **`Total tests: 22102, Passed: 21530, Failed: 355, Skipped: 217`** (`c17`) — **363 → 355, 8
      fixed, 0 broken**, and the 8 are C16's overrides arriving in a full run. The seam itself
      moved nothing, which is what it promised. ✅ `<this commit>`

      C12 read the route out of v1 and named the missing half: `IDynamicValueMapper` is the whole
      mapper, not a chain, so an application had nowhere to say "this CLR type travels as one
      string". `InfoCarrier.Core.ValueMapping.IInfoCarrierValueMapper` is that chain —
      `TryMapToWire` / `TryMapFromWire`, both `bool`, both able to decline. Consulted in exactly
      two places: forward in `MapToNode` after the primitive branch and before the collection and
      object-shape branches, reverse in `Materialize` before the scalar branch.

      **Why it cannot regress anything.** With no mapper registered both hooks are `foreach` loops
      over an empty list. The measurement is the claim: 0 broken, and the only reason-histogram
      movement is C16's ten `NullReferenceException`s becoming two.

      **No wire-format change.** A claimed value rides as one wire primitive under a `TypeNode`
      naming its *original* CLR type — which is also what keeps ADR-008 constraint 2 intact, since
      a mapped property type is already on `TypeAllowlist.ForModel`. Nothing was widened.

      Registration is the application's on **both** halves, and the two are found differently: the
      client's chain comes out of EF's internal service provider (DI, alongside the rest of the
      serialization pipeline), the server's out of the provider `InProcessInfoCarrierServer` was
      built with, because `ExpressionSerializer.CreateForModel` is called by hand there rather
      than resolved.

      **This is the piece worth building regardless of spatial**, as C12 said: it is the general
      answer to "a CLR type the wire cannot walk", and B23's `IPAddress` is the other instance —
      its diagnosis is complete and the converter route it rejected cost 381.

- [x] **C18. The two spatial bases adopted — Tier A, 169 of 173, and C9's host abort did not
      recur.** ✅ `<this commit>`

      **`Failed: 4, Passed: 169, Skipped: 0, Total: 173`** across `SpatialInfoCarrierTest` (5 of 5)
      and `Query.SpatialQueryInfoCarrierTest` (164 of 168).

      **The mapper is test-side, and that is the point.** `InfoCarrierNetTopologySuiteValueMapper`
      lives in `TestUtilities` and is registered by the test store factory (client) and the backend
      store (server) — v1's arrangement, and the reason neither v1's product assembly nor this one
      has ever referenced NetTopologySuite. The only spatial code in `src/` is C15's branch, which
      matches a type name as a *string*.

      **WKT with XYZM, not v1's GeoJSON**, which has no Z or M ordinate — requirements §2.8's
      recorded v1 defect, roadmap M7 Q7's answer. SRID rides as an EWKT `SRID=n;` prefix, written
      and read by this mapper's two halves (NTS 2.6's `WKTWriter` has no SRID switch).
      **The spatial suites could not have caught a regression here**: their model is XY at SRID 0,
      so a mapper silently dropping Z, M and SRID passes all 173. `GeometryWireFormatTest` asserts
      the ordinates directly, which is where losing them is visible.

      **Two product defects fell out, both real and neither spatial:**

      - **The wire could not carry `NaN`.** `Point.Z` on a point with no Z ordinate is `NaN`, JSON
        has no literal for it, and System.Text.Json's default is to refuse the *entire payload*.
        `AllowNamedFloatingPointLiterals` on `ExpressionJsonContext` — **not** on
        `SystemTextJsonInfoCarrierSerializer`, where it does nothing, exactly as the `MaxDepth`
        comment beside it already recorded. And the writing half alone turns "cannot be written"
        into "cannot be read": the named form is a JSON *string*, so `PrimitiveCoercion.Coerce`
        needed the matching read for `double` and `float`.
      - **A `GeometryCollection` is a sequence to the projection split, and must not be.** The 4
        left are `Item` ×2 and `IGeometryCollection_Count` ×2: `MultiLineString` implements
        `IEnumerable<Geometry>`, so the splitter puts it in a slot as `List<Geometry>` and
        `e.MultiLineString[0]` / `.Count` then fail to bind — *"Method `get_Item` declared on
        `GeometryCollection` cannot be called with instance of type `List<Geometry>`"*, out of
        `ProjectionRewriter.SlotSubstitutingVisitor`. **The fix is not a special case for
        geometry**: it is that a type the value-mapper chain claims travels *whole*, and the
        boundary analyzer has no way to ask, because `TryMapToWire` takes an instance rather than
        a type. Adding a type-level probe to `IInfoCarrierValueMapper` is the route; it is an
        addition to a just-locked ADR-012 and worth 4, so it is recorded rather than taken.

      Four of EF's `SpatialQueryInMemoryTest` overrides were checked by reason and all four
      matched (A63): both `Intersects_*_to_null` raise the same `NullReferenceException` from
      `Geometry.Intersects` inside the InMemory backend's own lambda;
      `GetGeometryN_with_null_argument` fails with literally EF's comment, *"Sequence contains no
      elements"*; and `Distance_constant_lhs`, whose EF override is a bare no-op with no stated
      reason, fails here with `ApplicationException: null geometries are not supported` raised by
      NetTopologySuite **server-side**, before anything reaches the wire. The override carries the
      measured reason rather than EF's silence.

- [x] **C19. `ExecuteUpdate` was never a boundary problem. It was one name on the allowlist.**
      **`Total tests: 22278, Passed: 21855, Failed: 206, Skipped: 217`** (`c19`) — **359 → 206,
      153 fixed, 0 broken.** ✅ `<this commit>`

      C0 guessed this would be a boundary or wire change and flagged it as new scope; C3 and C4
      priced it at 136 and recorded it as product work. **The probe answered it in one run**, and
      the answer was neither:

          DIAG: … names the type 'System.Collections.Generic.IReadOnlyList`1[
                System.Runtime.CompilerServices.ITuple]', which is not on the type allowlist.

      `ExecuteUpdate`'s public overload builds its setters and rewrites the call into the private
      `ExecuteUpdate<TSource>(IQueryable, IReadOnlyList<ITuple>)` marker before the provider ever
      sees it. `IReadOnlyList<>` and `Tuple<,>` were both already admitted; **`ITuple` was the one
      name missing**, so the call was refused as unshippable, evaluated on the client, and the
      marker did the only thing it does — `UnreachableException: Can't call this overload
      directly`, 164 times. An interface constructs nothing, so admitting it widens nothing
      (ADR-008 constraint 2 is intact, and this is the same argument `AddSupertypes` and
      `AddDeclaredType` already make).

      **`ExecuteDelete` was never broken at all.** The probe established that first, which is why
      it cost nothing to find out: `WHOLLY: True`, shipped, green. C0's reading of it was right
      and the two operators were only ever failing together because C4 tallied them together.

      **This is the probe rule paying for itself.** CLAUDE.md's standing instruction is to
      establish that the code *ran* before concluding anything about the problem; here the same
      instrument was pointed one step earlier — *where is the call being cut* — and it named a
      one-line cause under a heading three plan entries had filed as a wire change. The cost of
      the guess was two entries of speculative pricing; the cost of the probe was one filtered
      test run.

      **The 30 left are all `NorthwindBulkUpdates`** and are ordinary Tier B translation limits —
      set operations (`Union`/`Except`/`Intersect`/`Concat`), `Distinct`, `GroupBy…First`, the
      owned-without-owner refusal — most of which EF carries overrides for in
      `NorthwindBulkUpdatesSqliteTest`. See C20. The two `UnreachableException`s that remain are
      `Update_with_invalid_lambda_in_set_property_throws`, a test that asserts what an *invalid*
      setter lambda does, which is a different question.

- [x] **C20. The borrowed overrides C3 and C4 deferred — the second pass.**
      **`Total tests: 22278, Passed: 21904, Failed: 157, Skipped: 217`** (`c20`) — **206 → 157,
      49 fixed, 0 broken.** Test-side only. ✅ `<this commit>`

      C3 and C4 adopted their bases with no overrides at all, on purpose: adopt, classify, then
      work the failures separately. This is that second pass, and it had to wait for C19 —
      **until `ITuple` was allowlisted, every bulk-update failure read `UnreachableException`
      and no reason could be matched against anything.**

      | # | Taken from | Reason matched |
      |---|---|---|
      | 12 | `NorthwindBulkUpdatesSqliteTest` | `SqliteStrings.ApplyNotSupported`, verbatim |
      | 12 | `NorthwindBulkUpdatesRelationalTestBase` | `ExecuteDeleteOnNonEntityType`, `NoSetPropertyInvocation`, `MultipleTablesInExecuteUpdate`, `InvalidPropertyInSetProperty` — the server raises each one; only the assertion was missing |
      | 10 | `ComplexJsonProjectionSqliteTest` | `ApplyNotSupported` again |
      | 5 | `ComplexJsonBulkUpdateRelationalTestBase` | EF issues #36678, #36336, #36679, #36722 |
      | 3 | `ComplexJsonCollectionRelationalTestBase` | #36421, and `DistinctOnCollectionNotSupported` |
      | 3 | `ComplexJsonSetOperationsRelationalTestBase` | `InsufficientInformationToIdentifyElementOfCollectionJoin`, `SetOperationOverDifferentStructuralTypes` |
      | 4 | `OwnedNavigationsProjectionRelationalTestBase` | `AssertOwnedTrackingQuery`, and the null-vs-empty collection statement below |

      **The `ComplexJson*` classes are where a relational limit on a JSON-mapped complex type is
      stated**, and C3's mirror is why their reasons are ours: C3 mirrored `ToJson()` because a
      relational store has no other way to hold a complex collection, so the model this suite runs
      is a JSON one and EF's JSON overrides are the matching ones. The classes themselves stay
      unadopted — they assert SQL — which is the same line C3 drew.

      **The one that is a test replacement rather than an assertion is worth reading**:
      `Select_nested_collection_on_optional_associate` fails here with `Assert.Null() Failure:
      Value is not null`, and EF's relational override says why — *"traditional relational
      collection navigations projected from null instances are returned as empty collections
      rather than null … in contrast to client evaluation behavior"*. That is our failure stated
      from the other side.

      **A63 cuts both ways, and three places show it.** EF no-ops the `TrackAll` half of
      `Select_subquery_*_related_FirstOrDefault` because on SQLite it hits the APPLY refusal
      before the base's owned-tracking assertion; here `TrackAll` fails on a *string comparison*,
      which is a different statement, so only the `NoTracking` half is taken and `TrackAll` stays
      red. Likewise EF's `[ConditionalTheory(Skip = "Issue#28886")]` on two
      `NorthwindBulkUpdates` tests is **not** adopted: the reason matches exactly
      (`SQLite Error 1: 'no such column'`) but a skip hides a count, and this repo records an EF
      issue red — as `PrimitiveCollectionsQuery`'s #30730 already is.

      **The 14 left in `Query.Associations`** are 8 `Index_*` (C1/C2's A28 shape plus the §6
      trade), 2 `Over_associate_collection_projected` (C2's undiagnosed pair), 2
      `Select_subquery_*(TrackAll)`, 1 `Distinct_projected(TrackAll)` and 1
      `Contains_with_nested_and_composed_operators`. **The 6 left in `BulkUpdates`** are 4 EF
      issue #28886 and 2 `Update_with_invalid_lambda_in_set_property_throws` — the latter still
      `UnreachableException`, because an unshippable *setter* (the test's own `MaybeScalar`
      extension is not allowlisted, correctly) refuses the whole call and lets EF's marker throw
      instead of raising a diagnostic. That is a small real defect and it is the one C19 did not
      cover.

- [x] **C21. `AdHocJsonQuery` not taken, and the `Skipped` figure corrected.** No code change.
      ✅ `<this commit>`

      **`AdHocJsonQuery` is left unadopted, deliberately, and is now the only base the compliance
      test reports.** C10's price re-check holds and nothing since has changed it: 626 + 322 lines
      of relational mirror, seven abstract `Seed*` methods that only EF's relational classes
      implement (raw SQL inserting JSON documents), and a corpus that is owned JSON collections
      throughout — so most of what it would add lands on **B12**, still an open decision. Adopting
      it would buy a large, hand-written mirror whose output is a block of red tests attributable
      to a design question nobody has answered. **Worth doing after B12 is decided; hard to justify
      before.**

      **The `Skipped` figure recorded against `c10b` was wrong, and it propagated.** `c10b.log`
      says `Passed: 21503, Failed: 382, Skipped: 217`; C11 recorded `21512 / 382 / 208`. The `208`
      was `b21b`'s, still right there — Phase C's own adoptions brought nine of EF's skips with
      them at `c10` — and `Passed` was then derived by subtraction rather than read. C11 is
      corrected in place, and C15/C17/C19/C20 above are corrected too, having inherited it.

      **`Failed` and `Total` were right throughout**, which is why nothing was judged wrongly: those
      are the two `eng/measure.sh` parses and the two `eng/ratchet.sh` guards. But the rule the
      repo already states for counts applies to all four — **read them out of the run's summary
      block; none of them is arithmetic.**

- [x] **C23. B23 closed through the seam — and the widening rule cost two measured reverts to
      confine.** **`Total tests: 22278, Passed: 21906, Failed: 155, Skipped: 217`** (`c23c`) —
      **157 → 155, 2 fixed, 0 broken.** ✅ `<this commit>`

      `Comparison_with_value_converted_subclass` was one of only four wrong answers in the suite:
      `Where(f => f.ServerAddress == IPAddress.Loopback)` returned 0 rows instead of 1, silently.
      B23 diagnosed it as three stacked defects and found no route to the third. **C17's seam is
      that route**, and this is ADR-012's second consumer — the one that shows the seam is not a
      spatial feature. `IPAddress` fails the reflective walk the same way a geometry does: its
      `ScopeId` getter throws `SocketException` for an IPv4 address.

      **Defect 1, confirmed by probe before anything was written**, exactly as B23 recorded it:

          DENY System.Net.IPAddress+ReadOnlyIPAddress | visible=False | base=System.Net.IPAddress

      `IPAddress.Loopback` is a private nested subclass, and EF's funcletizer types a constant by
      the value it holds. So the constant named a type no allowlist can admit, the whole `Where`
      went client-side, and LINQ-to-Objects answered it wrongly.

      **Defect 2 is the widening rule, and getting it confined took two full runs.** Both are kept:

      | Attempt | Where applied | Result |
      |---|---|---|
      | `c23` | `TypeNodeMapper.ToTypeNode`, i.e. every node kind | **157 → 530.** 107 `Load`, 77 `GraphUpdates`, 52 `OwnedQuery`. A lazy-loading **proxy** is also non-public with a public base, and `DynamicValueMapper` already strips proxies deliberately *through the model*; a second unrelated rewrite upstream fought it. Kept as `c23-widening-reverted`. |
      | `c23b` | three constant-only sites, base ≠ `object` | **157 → 186.** An `internal enum` widened to `System.Enum` — public, and useless — *"The JSON value could not be converted to System.Enum"* ×27, plus a compiler-generated array widened to `System.Array`. |
      | `c23c` | the same three sites, base ∉ {`object`, `ValueType`, `Enum`, `Array`, `Delegate`, `MulticastDelegate`} | **157 → 155, 0 broken.** |

      **The rule that survives: widen only to a base that is a real type, never to a category.**
      C9 had already measured `object` at 92 and stated the exclusion as `BaseType != typeof(object)`;
      that was right about the shape and one item long. `ValueType`, `Enum`, `Array` and the two
      delegate roots are the same mistake one level out.

      **And the three sites are exact**, because a constant is the one expression node whose `Type`
      is a *runtime* type rather than a declared one: `ExpressionToNodeTranslator.VisitConstant`
      (what is written), `WireTypeCollector`'s constant branch (what the analyzer asks the
      allowlist about — **these two must agree or a subtree is refused over a name that is never
      sent**), and `DynamicValueMapper.MapToNode`'s value-mapper branch, which every entity, proxy,
      primitive and `Type` value has already returned before.

      **Defect 3 is the seam, and the mapper is test-side** — `InfoCarrierIPAddressValueMapper`,
      registered beside the geometry one. So ADR-012's statement holds unchanged: the provider
      knows nothing about which CLR types an application carries. **Whether the product should ship
      a default set of standard mappers is a real question and is deliberately not answered here.**
      v1 had `StandardValueMappers` in its product assembly, `IPAddress` is BCL rather than a
      package, and an application hitting this today gets a wrong answer rather than an error. But
      a default-registered mapper changes what travels for every existing caller, and ADR-012 says
      registration is the application's. **That is a decision, not a patch.**

- [x] **C24. A `List<T>` may only stand in where a `List<T>` fits.**
      **`Total tests: 22278, Passed: 21907, Failed: 154, Skipped: 217`** (`c24`) — **155 → 154,
      2 fixed, 1 "broken" which is C11's intermittent and not this change.** ✅ `<this commit>`

      C18 left four spatial failures and called them the `GeometryCollection`-is-a-sequence
      problem, with a type-level probe on ADR-012 as the route. **The probe is not needed, and the
      defect was more general than spatial.** `ProjectionRewriter.UnbuildableNavigationElement`
      wraps a collection-navigation fragment in `ToList()` when the declared type is enumerable and
      is not an `ICollection<>`. It never asked whether a `List<T>` is a *legal value* for that
      declared type. `MultiLineString` implements `IEnumerable<Geometry>` and passes every test in
      that method, but it is a domain type that happens to be enumerable, not a collection — so the
      slot held a `List<Geometry>` and `e.MultiLineString[0]` and `.Count` could not bind.

      One clause: rewrite only when `type.IsAssignableFrom(typeof(List<>).MakeGenericType(element))`.
      The types the method exists for are untouched — a `List<Name>` satisfies
      `IReadOnlyList<Name>`, which is its whole point.

      **`IGeometryCollection_Count` ×2 fixed. `Item` ×2 moved to a different defect** and stay red:
      the base's actual query is `Select(e => e.MultiLineString[0])` with **no null guard** — the
      expected query adds one — so it relies on the store to propagate null through the index. The
      index now lands on the client, where a null reference is a `NullReferenceException`. That is
      null-propagation semantics, not the collection-type family.

      **The second instance of the family is still open**, and it is the one that showed the family
      exists: `Join_with_result_selector_returning_queryable_throws_validation_error` fails with
      *"Unable to cast `List<Level3>` to `IQueryable<Level3>`"*. Same shape from the other side —
      `DynamicValueMapper`'s collection branch cannot rebuild an `IQueryable<T>`, and
      `ConstructCollection` declines because it is an interface. `list.AsQueryable()` is the
      obvious candidate and was **not** taken here: the test asserts that an invalid result selector
      *throws*, so the question is which exception is correct, not whether the cast can be made to
      work. Worth one measured attempt, separately.

- [ ] **C25. M5's method allowlist cannot be a visibility rule. Measured and reverted.** No code
      change; this is the finding. `<this commit>`

      M5 is the release blocker and its open half is the **method** allowlist: ADR-008 constraint 2
      specifies default-deny with opt-in for "Queryable / Enumerable / `EF.Functions` /
      model-bound members", and `NodeToExpressionTranslator.ResolveMethod` instead binds **any**
      method on any type the *type* allowlist admits — with `BindingFlags.NonPublic` set. Since the
      type allowlist admits every entity type, every mapped property type and every declaring type
      in the model, the reachable set is large and includes methods no caller could have written.

      The cheapest possible narrowing is one token: drop `NonPublic`. Measured —
      **154 → 697, 544 broken** (`c25-publiconly-reverted`) — and the two causes are the whole
      lesson:

      | # | Method | What it is |
      |---|---|---|
      | 384 | `EntityFrameworkQueryableExtensions.NotQuiteInclude` | `internal`. EF's own rewrite target for a string-based `Include`. |
      | 157 | `EntityFrameworkQueryableExtensions.ExecuteUpdate` | `private`. The marker overload C19 spent a whole step on. |

      **EF's public query API rewrites itself into non-public marker methods, and those markers
      have to cross this wire.** `Include("Orders")` is public and becomes `NotQuiteInclude`;
      `ExecuteUpdate(Action<UpdateSettersBuilder<T>>)` is public and becomes the private
      `IReadOnlyList<ITuple>` overload before the provider ever sees the tree. A remoting provider
      captures the tree **after** those rewrites — that is ADR-006's capture point — so it
      necessarily transports them.

      **So the policy must name methods, not describe their visibility**, exactly as ADR-008
      already words it. Visibility is not a proxy for "the caller could have written this", because
      EF rewrites what the caller wrote into something the caller could not have. The allowlist
      wants a set built from: `Queryable`, `Enumerable`, `EF`/`DbFunctions`, the model's own mapped
      members, **plus an explicit, named set of EF's rewrite markers** — of which this run has
      found two and there are certainly more. That set is discoverable the same way: deny by
      default, run, and read the names out of the failures.

      **Not attempted here.** Building that set is M5's work and wants its own plan; this entry
      establishes the shape it must have and rules out the cheap version, which is what the
      measurement was for.

- [x] **C27. A standing diagnostic for the C11 intermittent — written only when it fires.**
      **`Total tests: 22278, Passed: 21908, Failed: 153, Skipped: 217`** (`c27`) — 154 → 153,
      **0 broken**; the one test that moved is the intermittent itself, absent this run. ✅ `<this commit>`

      The instrument, not the hunt. `ServerSaveChangesExecutor` now catches the
      `InvalidOperationException` from `entry.State = state` and rethrows **the same type, with
      EF's original message first** and the whole request appended: every entry's correlation id,
      state, temporary-property names, generated keys, borrowed references and sent key values;
      the placeholders resolved so far; and every already-tracked entry with the key it landed on.
      The original is the inner exception. No spec test asserts EF's identity-conflict wording, so
      putting it first keeps any `Assert.Contains` match intact.

      **Zero cost until it fires**, which is the whole design: the previous attempt wrote a line
      per tracked entry and cost 194 extra failures through file I/O under parallel collections.
      Nothing is written here on the happy path — the report is built from state already in hand.

      **The diagnostic cannot replace the fault it describes.** `DiagnoseCore` runs inside a
      `try`/`catch` that degrades to one line, because it walks arbitrary application values and
      this suite ships a type whose members throw on purpose (`MyDiscriminator`), which the
      placeholder scan twenty lines above already has to guard against.

      Verified by forcing a conflict behind a temporary switch, reading the rendered report, and
      removing the switch — the output is quoted in C11, and it is what produced C11's surviving
      hypothesis.

- [x] **C29. Three of C5's six infrastructure failures were real product defects.**
      **`Total tests: 22278, Passed: 21911, Failed: 150, Skipped: 217`** (`c29`) — **153 → 150,
      3 fixed, 0 broken.** ✅ `<this commit>`

      C5 adopted the infrastructure bases and left six failures classified as "provider-plumbing
      detail, all worth keeping red". Three of them were defects, and none needed a decision.

      **1. `AddEntityFrameworkInfoCarrier()` was not idempotent** — *Expected 121, Actual 126*, and
      the difference is exactly the five `AddScoped` calls for the serialization pipeline.
      Everything above them goes through `EntityFrameworkServicesBuilder`, which is idempotent
      already; those five bypassed it because they are this provider's own services rather than EF
      contracts. `TryAddScoped` fixes it. **Not cosmetic**: the last registration wins for a single
      resolve, so behaviour looked fine — but all five are scoped, and an `IEnumerable<T>` resolve
      returns a duplicated service, which is exactly how ADR-012's value-mapper chain is consumed.

      **2. Fourteen public members were not `virtual`.** `ApiConsistencyTestBase` requires it of a
      provider's inheritable surface, and EF's own providers comply. Found in four rounds because
      the failure reports a batch at a time; the bulk pass was scripted over the non-sealed public
      classes in `src/`, then two `protected` members finished it
      (`InfoCarrierDatabase.Client`, `ExpressionToNodeTranslator.ToMethodNode`).

      **3. Two required services were simply not registered**, and this is the interesting one.
      `EntityFrameworkServicesBuilder.CoreServices` requires every provider to supply
      `IQueryableMethodTranslatingExpressionVisitorFactory` and
      `IShapedQueryCompilingExpressionVisitorFactory`. **This provider cannot implement either, by
      design** — ADR-006 captures the raw query at `IDatabase.CompileQuery` and the *server's*
      provider does the translating, so there is no client-side visitor to build and no shaper to
      compile. They are now registered as factories that throw with that sentence in the message.
      Nothing here resolves them; the point is that "not registered" was the wrong way to say
      "deliberately not implemented" — anyone who resolved one got EF's generic *"no service has
      been registered"* rather than the reason.

      Found by a throwaway override of `LifetimeTest` that listed the missing services instead of
      asserting; EF's own assertion is `Assert.Single`, which says *"the collection was empty"* and
      names nothing.

      **The three left are genuinely not ours to fix**, and C5's reading of them holds: two
      `Logging` tests where the initialization log line does not compose the way EF's does, and
      `InvalidIncludePathError_throws_by_default`, which fails inside `InfoCarrierTestHelpers`
      (*"builds models; it has no server"*) rather than in the provider — a harness limitation, not
      a missing diagnostic.

- [x] **C30. The method allowlist, closed to public + two named markers. M5's largest open half.**
      **`Total tests: 22278, Passed: 21911, Failed: 150, Skipped: 217`** (`c30`) — **150 → 150,
      0 fixed, 0 broken, reasons unchanged.** ✅ `<this commit>`

      C25 ruled out the cheap version and said the policy must **name methods**. This is that
      policy, and the zero in the diff is the result: the deserializer's reachable method surface
      is now a small, stated set, and nothing the suite legitimately does was using the rest.

      **The rule.** `ResolveMethod` still *looks up* non-public methods — the markers have to be
      findable — and then `Admit` refuses any that is not public, unless it is one of:

          EntityFrameworkQueryableExtensions::NotQuiteInclude
          EntityFrameworkQueryableExtensions::ExecuteUpdate

      Both were produced by C25's measurement, not guessed: without them, 384 and 157 failures.
      They exist because **EF's public API rewrites itself into non-public markers** —
      `Include("Orders")` becomes the first, `ExecuteUpdate(Action<…>)` becomes the second — and
      ADR-006 captures the tree *after* those rewrites, so this wire necessarily carries them.

      **The policy was designed from an inventory, not from intuition.** A gated, in-memory
      recorder (one hash insert per resolve, written once at `ProcessExit` — never per-resolve
      file I/O, which is what cost 194 failures in C11) collected every method the deserializer is
      asked to bind across a full run: **362 distinct methods over 84 declaring types.** The
      distribution is worth keeping, because it says ADR-008's wording was right:

      | Declaring type | # | ADR-008's category |
      |---|---|---|
      | `Queryable` / `Enumerable` | 44 / 43 | named outright |
      | `Math`, `MathF`, `String`, `DateTime`, `DateTimeOffset`, `Decimal`, `TimeOnly`, `Convert`, `DateOnly`, `TimeSpan`, … | ~130 | translatable BCL scalar functions |
      | `NetTopologySuite.Geometries.Geometry` | 28 | a mapped property type — admitted by the *type* allowlist, and new since C18 |
      | `EntityFrameworkQueryableExtensions`, `EF` | 11 / 3 | named outright |
      | `List<T>` instantiations | ~14 | `Contains` / `get_Item` over local collections |
      | model entity types | the rest | model-bound members |

      **What is still open in M5**, so the next session does not read this as done: payload
      depth/size limits, `InfoCarrierEnvelope` + `ProtocolVersion` actually being exercised (the
      backend test store implements `IInfoCarrierClient` directly and bypasses both), exception
      fidelity (W5), cancellation (W6), and the security review. **The type allowlist and now the
      method allowlist are the two that were specified as default-deny, and both are in.**

- [x] **C32. The context-initialized log line put the provider's options before the core's.**
      Measured with C33 in one run (`c32`, **150 → 146, 4 fixed, 0 broken**); the FIXED list
      attributes these two. ✅ `<this commit>`

      `Logs_context_initialization_no_tracking` and `…_sensitive_data_logging` expected
      `"NoTracking using InfoCarrier"` and got `"using InfoCarrier NoTracking"`. C5 filed both as
      *"the initialization log line does not compose the way EF's does"*, which was the right
      observation and stopped one step short of the cause.

      `DbContextOptions.Extensions` yields extensions **by insertion ordinal**, and
      `BuildOptionsFragment` concatenates their `LogFragment`s in that order. `UseInfoCarrier`
      called `AddOrUpdateExtension` first and `ConfigureWarnings` second — **every EF provider does
      it the other way round**, because configuring warnings is what first creates
      `CoreOptionsExtension`, so doing it first puts core options ahead of the provider's.
      `UseInMemoryDatabase` has the two calls in exactly that order and for exactly this reason.

      Two lines swapped. Worth recording because the fix is invisible from the failure: nothing
      about *"strings differ at position 153"* points at the order of two statements in an
      extension method, and the answer was in reading how EF's own provider spells the same method.

- [x] **C33. A key behind an enum or a value converter had no client-side value generator.**
      Measured with C32 in `c32`; the FIXED list attributes these two. ✅ `<this commit>`

      `Insert_update_and_delete_with_enum_key` and `…_with_wrapped_int_key` failed at
      `context.Add` — **before the wire**, so no server work could have helped:

          The property 'EnumPrincipal.Id' does not have a value set and no value generator is
          available for properties of type 'KeyEnum'.

      `InfoCarrierValueGeneratorSelector` guarded on `property.ClrType` against a list of numeric
      types. `TemporaryNumberValueGeneratorFactory` — the factory the guard is deciding whether to
      call — is broader in two ways it did not mirror: it unwraps an **enum** to its underlying
      type, and it looks through a **value converter** to `ProviderClrType`. So the generator
      existed in both cases and only the guard refused to ask for it. It now asks the question the
      same way, through the type mapping rather than the CLR type.

      **The converter case needed a second half**, and the first attempt found it: with the guard
      widened, the factory produced a generator of the *provider* type and EF stored its output as
      the model type — *"Unable to cast object of type 'System.Int32' to type
      'WrappedIntKeyClass'"*, inside `ValueComparer.Snapshot`. EF's core selector wraps such a
      generator with `WithConverter`; so does this one now.

      **These were two of the three CLAUDE.md attributed to B6 route (a), and they were not.**
      B6 route (a) is about a client convention reading *relational annotations* to learn which
      properties are store-generated. These two never got that far: the client already knew the
      property was generated — `ValueGenerated != Never` is what triggered the lookup — and simply
      had no generator to offer. The remaining `StoreGenerated` two were unrelated to each other,
      and **both are closed now** — the second by C42:
      `…_with_wrapped_Uri_key` (*"This operation is not supported for a relative URI"*) and
      `Store_generated_values_are_propagated_with_composite_key_cycles`, which S3c already records
      as undiagnosed.

- [x] **C34. A wrapped `Uri` key needed both halves of the wire's answer to "not a primitive".**
      **`Total tests: 22278, Passed: 21916, Failed: 145, Skipped: 217`** (`c34`) — **146 → 145,
      1 fixed, 0 broken.** ✅ `<this commit>`

      `Insert_update_and_delete_with_wrapped_Uri_key` failed with *"This operation is not supported
      for a relative URI"*, thrown from `Uri.get_AbsolutePath` through
      `RuntimeMethodInfo.Invoke` — **ADR-012's case exactly, and its third distinct instance**: a
      geometry's members recurse, `IPAddress.ScopeId` throws for IPv4, and `Uri.AbsolutePath`
      throws for a relative URI. One seam, three unrelated CLR types, all reached by the same
      reflective object-shape walk.

      `InfoCarrierUriValueMapper` (test-side, beside the other two) stopped the walk — and
      uncovered the second half, which is a different mechanism on a different path:

          JsonTypeInfo metadata for type 'System.Uri' was not provided by TypeInfoResolver
          ... Path: $.EntityKey.KeyValues

      The seam is consulted in `MapToNode`, which covers **property** values. A **key** value goes
      through `PrimitiveCoercion.ToWireValue` into `EntityKeyNode.KeyValues`, which is declared
      `object` and so resolved by *runtime* type by the source-generated serializer. `Uri` was not
      registered there. **The precedent is already in the file**: `byte[]` is registered with
      exactly this reasoning — not in `IsPrimitive`'s set, but what a converter over a binary key
      produces — so `Uri` is registered the same way, with the same comment shape.

      **Worth stating as a rule.** "The wire cannot handle this type" has *two* answers and they
      are not interchangeable: the seam decides how a value is **written**, and the serializer
      context decides whether the wire can carry the result at all. A converted key exercises both,
      and fixing only the first moves the failure rather than closing it.

- [x] **C36. M5's node-kind allowlist. Two of the three parts were already closed; the third was
      the operator, and it was wide open.**
      **`Total tests: 22300, Passed: 21938, Failed: 145, Skipped: 217`** (`c36`) — **145 → 145,
      0 fixed, 0 broken, reasons unchanged.** Total is up 22 because this step adds 22 tests.
      ✅ `<this commit>`

      ADR-008 constraint 2 wants default-deny allowlists for node kinds, types and methods. Types
      closed 2026-08-01, methods in C30. This is the last third, and it turned out to be three
      separate questions with three different answers.

      **The node kind is closed by construction, and the premise that it was not is wrong.**
      `NodeToExpressionTranslator.TranslateNode` switches on the node's *CLR type*, not on
      `node.Kind`, and `ExpressionNode.Kind` is `[JsonIgnore]` and abstract — every derived record
      answers it with a literal. So `Kind` is **never wire-supplied**. What the wire carries is
      System.Text.Json's `$kind` polymorphic discriminator, which selects among the fifteen
      `[JsonDerivedType]`s registered on `ExpressionNode` and throws `JsonException` for anything
      else, before a node object exists. The trailing `_ => throw new NotSupportedException` is
      therefore **unreachable from a payload**; it guards a locally-constructed subclass and is
      worth keeping, but it is not the control. Proved by three tests: an unregistered
      discriminator is refused, a supplied `"kind"` member is ignored in favour of the CLR type,
      and registration is checked against `NodeKind` in both directions so the two cannot drift.

      **The operator was the real gap.** `BinaryNode.Operator`, `UnaryNode.Operator` and
      `TypeBinaryNode.Operator` are free strings, parsed with
      `Enum.TryParse(name, out ExpressionType)` — which admits far more than this wire emits:

      | Admitted before | Why it matters |
      |---|---|
      | all 85 `ExpressionType` names | `Assign` and the twenty-six other assignment forms reach `Expression.MakeBinary`; `Throw` reaches `MakeUnary`. Both build a **mutation or a throw into a tree the server is about to compile and run**. |
      | bare numeric strings — `"999"` | `TryParse` returns an undefined enum value; the failure is then a raw `ArgumentException` from `Expression`, not a stated refusal. |
      | comma lists — `"Add, Not"` | `TryParse` parses these as flag combinations **whether or not the enum is `[Flags]`**. |

      And `TranslateTypeBinary` read *"TypeEqual, else TypeIs"*, so all eighty-three other names
      silently became a type test.

      **The allowlist is derivable, not guessed** — which matters, because C30's lesson was that a
      method allowlist could not be. The forward translator emits `node.NodeType.ToString()` off a
      live `BinaryExpression`/`UnaryExpression`/`TypeBinaryExpression`, so the emitted set is
      bounded by what a client can have *built*, and **a C# expression-tree lambda cannot contain
      an assignment, a throw or a block** — the compiler refuses. So the admitted set is the pure
      subset of each factory's domain (24 binary, 15 unary, 2 type-binary) and the excluded set is
      exactly the mutating and control-flow forms. The zero in the diff is the confirmation: the
      suite's every operator is in the pure subset, as the argument says it must be.

      The DTO doc comments have claimed since they were written that the operator is *"an explicit
      map, never an int-cast (expression-serialization §3.7)"*. It was an `Enum.TryParse` over the
      whole enum. This is where that sentence becomes true.

- [x] **C37. The payload size bound — and the measurement moved it to the other direction.**
      **`Total tests: 22312, Passed: 21949, Failed: 146, Skipped: 217`** (`c37b`) — **145 → 146,
      0 fixed, 1 broken, and the one broken is the C11 intermittent**, whose third and fourth
      sightings this step produced. Nothing broke on the bound. ✅ `<this commit>`

      M5 lists *"payload depth/size limits (v1 needed a 10 MB stack for >1 MB payloads)"*. The
      **depth** half has been in and load-bearing for some time — `ExpressionJsonContext` sets
      `MaxDepth = 256`. The **size** half did not exist, and depth does not imply it: a flat array
      of a hundred million constants is depth 3.

      `InfoCarrierPayloadLimits` is the bound, checked **before** the parse — the allocation a
      parse costs is what it bounds, so a node count, knowable only by parsing, would be too late.
      `SystemTextJsonInfoCarrierSerializer` takes one and applies it; `ServerQueryExecutor`
      applies the default to the serialized query tree, which is the one wholly caller-controlled
      deserialization on the server.

      **The first attempt was one bound for both directions at 64 MiB, and the run refused it.**

      | Broken | Payload | What it was |
      |---|---|---|
      | `Handle_materialization_properly_when_more_than_two_query_sources_are_involved` ×2 | **560,839,164 bytes** | a `QueryDataResult` |
      | `Take_with_single_select_many` ×2 | **111,089,698 bytes** | a `QueryDataResult` |

      Every one a **result**, and no request came near the limit (`c37`, 145 → 150). Half a
      gigabyte from a triple cross-join is what the caller asked its own server for. **A control
      that has to be widened past half a gigabyte so the suite can pass is bounding the wrong
      direction** — roadmap M5 states the threat as "accepting serialized expression trees from
      remote clients", which is server-inbound, and a page-size policy on results is a different
      question this library has no basis to answer.

      So the bound is two numbers, and the direction is declared rather than inferred:
      `IInfoCarrierRequest` marks `QueryDataRequest`, `SaveChangesRequest`, `SavepointRequest` and
      `InfoCarrierEnvelope`; `MaxRequestBytes` defaults to **64 MiB** and `MaxResponseBytes` to
      **null**. Opting out is an explicit `null`, never a very large number. `c37b` re-measured it
      at zero cost.

      **The envelope caveat, stated rather than left to be discovered.** `ServerQueryExecutor` uses
      `InfoCarrierPayloadLimits.Default` because it is constructed from
      `(DbContext, IExpressionSerializer)` and has no options seam — the expression payload travels
      through `ExpressionJsonContext` directly rather than through the configured
      `IInfoCarrierSerializer`. That is the same gap M5's envelope criterion already names, not a
      new one. An unconfigurable bound is worth more than no bound in the meantime.

- [x] **C38. The C11 intermittent, closed. It was never about identifying placeholders by value.**
      **`Total tests: 22312, Passed: 21950, Failed: 145, Skipped: 217`** — three consecutive
      identical runs (`c38`, `c38b`, `c38c`), **0 fixed, 0 broken, reasons unchanged** against
      `c36`. That is the three-run bar, and it is what this fix needed rather than what routine
      work needs. ✅ `<this commit>`

      C11 has the two dumps and the full reading. In one line: the server let **EF's own temporary
      value generator** run for a key it was about to overwrite with the client's placeholder, and
      both generators count down from `int.MinValue`. When the client's counter is one entry ahead,
      the value EF hands the *second* row of a request is the value the *first* row was already
      forced onto, and the identity map refuses it.

      **The fix is to stop running the generator at all** for a key whose value is going to be
      replaced. Where the store issues the key at save time — every relational one — the client's
      placeholder now goes onto the entity **before** it is tracked, so EF's value generation never
      runs for that property, there is no second number, and there is nothing for it to collide
      with. `TrackOne` still flags it temporary afterwards, which is what makes the store replace
      it and propagate.

      **What told the two stores apart was the thing to change.** The old code left the property
      unset and inferred the store's behaviour from what EF had produced *after* tracking —
      necessarily after generation, which is the run it needed not to make. EF's own
      `IValueGeneratorSelector` answers the same question before anything is tracked:
      `GeneratesTemporaryValues` is `false` exactly for the Add-time case the original comment
      named, `InMemoryIntegerValueGenerator`. That branch is unchanged — an InMemory store has a
      real value to offer and it is still better than the placeholder — and its values are small
      positive integers, so it was never in the colliding range.

      **A guard is kept for the store that answers wrongly.** If the selector claims a real value
      but the property comes back holding its sentinel or a temporary, the old path still runs. It
      costs one comparison and it is the branch that would otherwise silently lose a key.

      **The 22278 → 22312 growth across C36–C38 is 34 new tests, not movement.** The failing count
      has been 145 throughout.

- [x] **C39. CI was not broken. Its baseline was — by a factor of four.** No suite change; the
      counts below are `c38c`'s, re-read out of a TRX. ✅ `<this commit>`

      CLAUDE.md has carried *"CI is broken — `build.yml` restores `InfoCarrier.Core.sln`, and its
      `~InMemory`/`~SqlServer` filters match no current test class"* for a long time, and this step
      began by going to fix it. **The workflow already says `.slnx`, already runs two jobs, and
      already invokes `eng/ratchet.sh`.** Commit `51f4684` — *"Step N1–N4: CI that can actually
      run, then park it"* — did all of that; the note described the file as it was *before* that
      commit and was never retired. **Read the file before repeating a note about it.**

      **The real defect was one the note never mentioned.** `test/known-failures.txt` still read

          failed=111
          total=5215

      dated 2026-08-02, from before Phase A, B and C adopted forty-one spec bases. Against an
      actual `145/22312` the gate errors on the failure count — and the total, the guard that
      exists to catch a crashed host reporting fewer failures because fewer tests ran, had
      quadrupled underneath it. **A ratchet whose baseline is not maintained is a broken build
      waiting, not a safety net.**

      **The baseline is now read out of the TRX**, which is what the script parses, and that is
      recorded in the file because the two sources do not agree in the obvious way: the TRX's
      `total` (22312) counts the 217 skips that neither its `passed` (21950) nor its `failed`
      (145) does, so `passed + failed` is 22095. Deriving one of these figures from the others is
      the mistake that cost this repo three commits (see CLAUDE.md on `Skipped`).

      **Verified locally in five directions, since GitHub Actions cannot be run from here** —
      and *the workflow itself is therefore unverified*; what is verified is the script it calls:

      | Case | Result |
      |---|---|
      | current TRX vs current baseline | `exit=0` |
      | failures rose (`144` → 145) | `exit=1`, `::error::Failures rose 144 -> 145` |
      | total shrank (`22400` → 22312) | `exit=1`, `::error::Total dropped 22400 -> 22312` |
      | failures fell (`200` → 145) | `exit=0`, `::notice::` to lower the baseline |
      | TRX missing entirely | `exit=1`, refuses rather than passing vacuously |

      **Two small repairs to the workflow while here.** It triggered on `main`/`develop` only,
      and every commit in this project is on `v10-claude` — *a gate that never runs on the branch
      it is gating is not a gate*. And the full-suite step got `timeout-minutes: 90`; it is ~15
      minutes locally at 22312 tests, and a runner slow enough to be killed at the default would
      report a partial TRX, which the ratchet would then read as a shrinking total.

- [x] **C40. A string `Include` naming nothing was never validated — and where the check went is
      the finding.**
      **`Total tests: 22312, Passed: 21951, Failed: 144, Skipped: 217`** (`c40b`) — **145 → 144,
      1 fixed, 0 broken.** ✅ `<this commit>`

      `Logging.InvalidIncludePathError_throws_by_default` did `Set<Animal>().Include("Wheels")`
      against a model with no such navigation and got *"InfoCarrierTestHelpers builds models; it
      has no server to talk to"* — the path had shipped. EF raises
      `CoreEventId.InvalidIncludePathError` in `NavigationExpandingExpressionVisitor.ProcessInclude`,
      a warning-as-error by default, and that visitor is precisely what ADR-006's capture point
      means this provider never runs. `RejectInvalidIncludes` already covered the *lambda* forms;
      a string names no member, so nothing saw it.

      `StringIncludeValidator` mirrors EF's walk — the breadth-first queue over navigations,
      `FindNavigations`' four cases (navigation, derived declared navigation, skip navigation,
      derived declared skip navigation), and the decision to keep reporting after a failed
      segment. **The message is not reproduced**: `_queryLogger.InvalidIncludePathError(chain,
      name)` is EF's own extension, so the `WarningAsErrorTemplate` wrapper the test asserts comes
      from EF's plumbing and cannot drift from it. `QuerySplitter` gained an optional
      `IDiagnosticsLogger<DbLoggerCategory.Query>`, which `QueryExecutor` passes from
      `queryContext.QueryLogger`.

      **The first attempt failed and it is the more useful half of this entry.** The new check
      went into `RejectInvalidIncludes`, and that method sits *after* `Split`'s
      `IsWhollyServerExecutable` early return — so it never ran for the query in question, which
      is wholly shippable. **A probe established that in one filtered run**: nothing was written
      at all, which said the method was not reached rather than that the matcher had missed.

      Moving the whole of `RejectInvalidIncludes` above the early return then measured
      **1 fixed, 18 broken** (`c40`), and every one of the 18 is *"Cannot apply the 'Include'
      operation with argument …"* on a **legitimate** include:

          Include(e => EF.Property<X>(e, "OneToOne_Optional_FK1"))
          ThenInclude(g => ((Officer)g).Reports)

      **Attributed to `IsPropertyPath` here, and that was wrong — see C47, which read the code and
      probed it.** `IsPropertyPath` handles both shapes. The refusals came from the finder's other
      branch, `IncludeOnNonEntity`, because a `ThenInclude` after a collection navigation is rooted
      at `ICollection<T>` and `IsEntity` did not look through it. The observation that stands is
      the one that matters: **a check that has never run has never been tested**, and this is how
      that surfaced. Only the string half moved up in this step; C47 moved the rest.

- [x] **C41. `ComplexTypesTracking.Can_track_entity_with_complex_property_bag_collections` ×2 is
      EF's, and the stack says so outright.** No code change; this is the classification. ✅
      `<this commit>`

      The message — *"Incorrect number of arguments supplied for call to method
      'System.Object get_Item(System.String)'"* — reads like expression construction, which would
      make it ours. The frame above it says otherwise:

          at System.Linq.Expressions.Expression.Property(Expression, PropertyInfo)
          at StructuralTypeMaterializerSource.<AddInitializeExpression>g__CreateMemberAssignment|10_0
          at StructuralTypeMaterializerSource.AddInitializeExpression
          …
          at RuntimeEntityType.GetOrCreateMaterializer
          at ServerSaveChangesExecutor.Materialize          <- ours, and only as the caller

      A **property-bag** entity type's members are the indexer `get_Item(string)`. EF's own
      `CreateMemberAssignment` hands that `PropertyInfo` to `Expression.Property(expression,
      property)`, the overload for a parameterless property, and an indexer needs an argument.
      Everything between our call and the throw is EF's, and the only thing this provider
      contributes is asking for a materializer for a type EF cannot build one for.

      **Confirms A32 as written** and closes the queue's doubt: CLAUDE.md said it fails inside
      EF's `StructuralTypeMaterializerSource` and it does. Red, classified, not ours.

- [x] **C42. A key cycle broke "a key this store issues", and the client's placeholder was
      written to the store as a real key.**
      **`Total tests: 22312, Passed: 21952, Failed: 143, Skipped: 217`** (`c42b`) — **144 → 143,
      1 fixed, 0 broken.** ✅ `<this commit>`

      `Store_generated_values_are_propagated_with_composite_key_cycles` failed with
      *"Sequence contains no elements"* inside the server's read-back — the dependent's key never
      received the value the store generated for its principal. S3c recorded it as undiagnosed.
      **Two probes settled it, and the second was the one that mattered.**

      The model is a genuine cycle:

          CompositePrincipal.Id      HasKey + ValueGeneratedOnAdd, and half of the FK {Id, CurrentNumber}
          CompositeDependent         HasKey {PrincipalId, Number}, PrincipalId FK -> CompositePrincipal.Id

      The first probe dumped every property's metadata and value after `SaveChanges`:

          CompositeDependent  PrincipalId  valueGenerated=Never  isKey=True isFk=True  current=-2147482646  principalGenerated=[OnAdd]
          CompositePrincipal  Id           valueGenerated=OnAdd  isKey=True isFk=True  current=-2147482646  principalGenerated=[Never]

      `-2147482646` is a *client* placeholder, and it survived the save. The second probe read the
      row out of SQLite: `DB principal Id=-2147482646`. **The placeholder had been inserted as an
      explicit key.** No amount of work on the return path would have helped; the store had
      never generated anything.

      **Cause: `!property.IsForeignKey()` was standing in for "a key this store issues".** The two
      agree in every acyclic model, which is why the proxy held — but `CompositePrincipal.Id` is
      store-generated *and* half a foreign key, so the guard sent it down the ordinary-value path.
      The question was always `ValueGenerated`, and asking it directly costs nothing: a borrowed
      placeholder is by definition not generated here, so `CompositeDependent.PrincipalId`
      (`Never`) stays a reference exactly as before.

      **The second half, and C34's rule again** — *"the wire cannot handle this" has two answers
      and fixing one moves the failure.* With the store now generating, the client still had to
      learn the dependent's `PrincipalId`, and `ReadGenerated` returns only `ValueGenerated`
      properties. A propagated FK is `Never`-generated and yet holds a number only the store
      knows.

      **The wide version of that is measured and wrong: 1 fixed, 2 broken** (`c42`), two
      `Save_optional_many_to_one_dependents` parameterizations, on
      *"Assert.Contains() Failure: Item not found in set"*. An ordinary FK is the client's own
      business — EF's fixup reaches it from the principal key that does come back — and asserting
      it re-imposes a relationship the client may have deliberately changed. So the rule is
      narrowed with `property.IsKey()`: **a row cannot disagree with the store about its own
      identity**, and that is the only case where the client cannot recover the value itself.

- [x] **C43. `SpatialQuery.Item` ×2 is null propagation the base borrows from a relational store,
      and this class has already classified the same shape twice.** No code change; this is the
      classification, and it names what a fix would cost. ✅ `<this commit>`

      After C24 these fail with `NullReferenceException` rather than the old cast error. The base:

          actual:   ss.Set<MultiLineStringEntity>().Select(e => new { e.Id, Item0 = e.MultiLineString[0] })
          expected: ss.Set<MultiLineStringEntity>().Select(e => new { e.Id,
                        Item0 = e.MultiLineString == null ? null : e.MultiLineString[0] })

      **The guard is in the expected query and deliberately not in the actual one** — the base is
      asserting that the *store* propagates null through the index. EF's SQLite test confirms it
      by passing with no override at all: `GeometryN("m"."MultiLineString", 0 + 1)` over `NULL` is
      `NULL`. Compare the `Length` test three methods below, where EF writes the guard into *both*
      queries; it does that exactly when the store cannot be relied on.

      **Neither side of this provider is a relational store on Tier A.** These bases run on the
      InMemory backend (C18), which evaluates NetTopologySuite in a compiled lambda and throws.
      `SpatialQueryInfoCarrierTest` already carries two overrides of precisely this shape —
      `Intersects_equal_to_null` and `Intersects_not_equal_to_null`, both asserting
      `NullReferenceException` with the note *"the store's, not the wire's"*. `Item` is the third,
      differing only in that C24 moved the index onto the **client residual**, so the throw is now
      in `QueryExecutor.Guarded` instead of in the backend's lambda. The answer is the same either
      way: nothing in this configuration propagates null through an index.

      **Left red rather than overridden, and that is the difference from the other two.** For
      `Intersects_*` the throw was *measured* coming out of the backend, which is what justified
      asserting it. Here the split means the server never sees the index, so the same claim about
      the store cannot be demonstrated without shipping it — and asserting an exception one cannot
      attribute is how a suppressed test starts.

      **What a fix would actually be, so it is not mistaken for a small one.** Making the client
      residual propagate null through a moved member or index access means giving the residual
      *relational* null semantics — for every client-side projection, not just this one — in a
      provider whose Tier A backend does not have them and whose own spec suite contains tests
      asserting the throw. **That is a semantic decision about what the split guarantees, not a
      bug fix**, and it is the same question in a different coat as "what does the boundary
      preserve". ~~The cheaper route is spatial on Tier B, where the store answers it.~~
      **Underpriced — see C51.** Tier B has no SpatiaLite and cannot map a `Point`; that route is
      a native dependency and belongs to M7.

- [x] **C45. The envelope had a client half and no server half. Now the whole suite goes through
      it.**
      **`Total tests: 22321, Passed: 21961, Failed: 143, Skipped: 217`** (`c45`) — **143 → 143,
      0 fixed, 0 broken, reasons unchanged.** Total up 9 for the new tests. ✅ `<this commit>`

      M5's criterion read *"`InfoCarrierEnvelope` + `ProtocolVersion` actually exercised by tests —
      currently the backend test store implements `IInfoCarrierClient` directly and bypasses
      both"*. The store was only half the problem. **`TransportInfoCarrierClient` has wrapped every
      request in an envelope since it was written, and nothing in `src/` ever unwrapped one.** The
      only dispatcher in the repo was inline in `InMemorySmokeTest`, handled `Query`, and threw for
      the other eight operations. A network transport author had a client to call and no server to
      answer it; the envelope and the version were write-only fields.

      **`InfoCarrierEnvelopeServer` is the missing half**, and it belongs in the product for the
      same reason `TransportInfoCarrierClient` does. It checks `ProtocolVersion` **before**
      running anything — a version field nobody reads is documentation, not a compatibility
      mechanism — and refuses an unknown operation by name rather than treating it as one it
      knows. The smoke test's hand-rolled dispatcher is deleted in favour of it.

      The backend test store now talks through `TransportInfoCarrierClient`, so all **22321**
      tests cross a real envelope, including the transaction operations. Those are the ones with
      no payload to speak of, which is exactly why they had never exercised dispatch: a wrong
      discriminator arm for `ReleaseSavepoint` would have been invisible.

      **The transport is a test-side one rather than `InProcessInfoCarrierTransport`, and that is
      a measurement, not a preference.** That transport re-serializes the whole envelope, and an
      envelope's payload is *already serialized bytes* — so the payload would be base64'd into a
      second JSON document on every hop. C37 measured this suite's largest result at
      **560,839,164 bytes**; base64 makes that ~750 MB of extra JSON, twice per query, for
      coverage already had. The payload round-trips regardless — the client serializes it and
      `InfoCarrierEnvelopeServer` deserializes it, which is where every wire-serializability
      failure this suite has caught was caught. `InMemorySmokeTest` keeps the real
      `InProcessInfoCarrierTransport` on small payloads, so the envelope's own serializability
      stays covered.

      **This retro-validates C37.** The payload size bound sits on `IInfoCarrierSerializer`, and
      C37 had to note that the suite did not use that path. It does now: every request payload in
      the suite passes the request bound, and `IInfoCarrierRequest`'s direction split is exercised
      rather than argued.

      Nine tests cover what 22321 green runs cannot — the refusals. A suite that only ever sends
      the current version never learns what happens to a different one, and
      `Every_declared_operation_dispatches_to_its_own_server_method` asserts against
      `Enum.GetValues<InfoCarrierOperation>().Length`, so a tenth operation added without a
      dispatch arm fails there rather than at a caller's first use.

- [x] **C46. W5 — a server-side failure now crosses as data, and three things fell out of it.**
      **`Total tests: 22327, Passed: 21967, Failed: 143, Skipped: 217`** (`c46c`) — **143 → 143,
      0 fixed, 0 broken.** ✅ `<this commit>`

      In-process a server exception reaches the caller by propagating — the same object on the
      same stack. **No network transport can do that**, so a suite that only ever runs in-process
      is not testing the error behaviour it appears to test. Roadmap M2's re-scoping note is the
      precedent: the type allowlist was introduced to break exactly this kind of illusion.

      `InfoCarrierFault` carries type name, message, stack and the inner chain;
      `InfoCarrierEnvelopeServer` catches and returns it in the response;
      `TransportInfoCarrierClient` checks it **before** the payload and rethrows. Fidelity is
      defined by what callers depend on — type, message, inner chain — because that is what EF's
      spec tests assert, and thousands of them now cross this path.

      **A version mismatch still escapes rather than becoming a fault.** The two ends disagree
      about what an envelope *is*, so answering with one assumes the thing in dispute. An unknown
      *operation* does become a fault: there the ends agree on the envelope.

      **Three findings, each of which cost a run or would have.**

      1. **`ArgumentNullException(string)` takes a *paramName*, not a message.** Rebuilding through
         the one-string constructor nested the message inside itself —
         `Value cannot be null. (Parameter 'Value cannot be null. (Parameter 'value')')` — and
         eight `GearsOfWarQuery` tests said so in one run. `ArgumentOutOfRangeException` and
         `ObjectDisposedException` are the same trap. The fix is to prefer
         `(string message, Exception? inner)` **even when there is no inner**, because that
         overload is unambiguous for every one of them.

      2. **`DbUpdateException.Entries` was surviving on the in-process leak.** They are update
         entries *of the server's context* and cannot cross a wire under any encoding.
         `InfoCarrierDatabase` already re-raised `DbUpdateConcurrencyException` with translated
         client entries for this exact reason; W5 made the plain `DbUpdateException` case need the
         same treatment, one level up the hierarchy.

      3. **A store's own exception type is not reconstructible, and that is correct.**
         `SqliteException` has no message-and-inner constructor, and more fundamentally a client
         has no reason to reference the backend's driver. Our three
         `Inline_collection_*index_Column` overrides asserted it directly and passed only on the
         illusion. They now assert `InfoCarrierServerException` **plus** that
         `ServerExceptionTypeName` is SQLite's and the message is the engine's — *stronger* than
         what they replaced, because it proves both survived the wire. The base test still runs,
         still reaches SQLite, still fails there. **These are our own overrides, not EF spec
         tests**; nothing is suppressed.

      **The reasons histogram went blind, and that is the finding worth keeping.** `measure.sh`
      filtered reason lines to those beginning `System|Microsoft|Assert`, so **42 lines now
      beginning `InfoCarrier.Core.InfoCarrierServerException` vanished from it** — a diff showing
      removals with no additions, which is precisely the "names identical, reasons moved" case the
      third level of `measure.sh` exists to catch. The filter now includes `InfoCarrier` and
      `Xunit`, and `c46c.reasons.txt` was regenerated from its own log. Both sides total **142**
      reason lines and every one is accounted for. `.reasons.txt` files older than this predate
      the filter and are not comparable to it.

      Twelve of those 42 are `MaterializationInterception`'s `Assert.Same` — a user interceptor
      running **server-side** whose assertion throws there. Its `XunitException` used to propagate;
      it now crosses as a fault with the message intact. Same tests, same substance, and a neat
      proof that the fault path is engaged inside user code on the far side.

- [x] **C47. C40's 18 broken includes were not `IsPropertyPath`. They were a collection type.**
      **`Total tests: 22329, Passed: 21969, Failed: 143, Skipped: 217`** (`c47b`) — **143 → 143,
      0 fixed, 0 broken, reasons unchanged.** Total up 2 for the new tests. ✅ `<this commit>`

      **C40's attribution was wrong and this corrects it.** It recorded that
      `IsPropertyPath` "refuses neither an `EF.Property` call nor a cast". Reading it says
      otherwise — it has a `Convert or TypeAs` arm and a `MethodCallExpression` arm, and both
      shapes pass. The failures were the *other* branch of the same finder, `IncludeOnNonEntity`,
      whose message differs from `InvalidIncludeExpression` and which nobody compared.

      A probe named the cause in one filtered run:

          rootType=System.Collections.Generic.ICollection`1[[…ComplexNavigationsModel.Level2…]]
              findEntityType=<null> assignableFrom=
          INVALID onNonEntity=True expr=e => Property(e, "OneToOne_Optional_FK2")
          INVALID onNonEntity=True expr=g => Convert(g, Officer).Reports

      **A `ThenInclude` after a collection navigation is rooted at the collection.** `EF.Property`
      gives overload resolution nothing to infer from, so EF picks the reference `ThenInclude` and
      the lambda's parameter is `ICollection<Gear>`, not `Gear`. `IsEntity` asked the model about
      the collection type, got a flat no, and reported a perfectly ordinary include as being on a
      non-entity. It now looks through a sequence type to its element — `string` needs no special
      case, because `char` is not an entity either.

      With that fixed, `RejectInvalidIncludes` moves above the `IsWhollyServerExecutable` early
      return, where a validity check on the caller's query belongs, and the placement stops being
      load-bearing.

      **Two attempts at a regression test, and the first one was worthless — worth recording
      because it is the failure mode `measure.sh`'s third level exists to catch.** A wholly-
      shippable query with an invalid include turned out to be hard to construct: an invalid
      *lambda* include almost always drags in an anonymous type, which makes the query non-
      shippable, so the check reached it at the old position anyway. **Verified by putting the
      move back and watching the test still pass.** So the honest statement about the move is that
      it is safe and removes a fragile ordering dependency — *not* that it catches something new.
      The string half (C40) remains the case that genuinely needed the earlier position.

      The test that was kept pins the real defect: a `ThenInclude` rooted at a collection, checked
      by removing the lookthrough and watching it fail. **A regression test nobody has seen fail is
      a regression test nobody has tested.**

- [x] **C48. The security review — and the type allowlist's safety turns out to be a conjunction.**
      **`Total tests: 22347, Passed: 21987, Failed: 143, Skipped: 217`** (`c48`) — **143 → 143,
      0 fixed, 0 broken.** Total up 18 for the adversarial tests.
      [`docs/security-review.md`](security-review.md). ✅ `<this commit>`

      M5's last criterion. The review walks the ten stages from bytes to execution and says which
      control governs each; the useful part is what reading `TypeAllowlist` adversarially turned
      up.

      **`System.Type` is admitted, and so is every enum** (`return type.IsEnum` is the closing
      line). So a payload may legitimately call `Type.GetType("System.Diagnostics.Process")` — a
      *public* method on an *admitted* type — and hold, **at run time on the server, after every
      deserialization-time check has passed**, a type the allowlist never saw.

      **It is not a hole, and the reason is the finding:** a `Type` obtained that way has nothing
      to call. `Type.InvokeMember` takes a `System.Reflection.Binder`; `MethodInfo.Invoke` and
      `ConstructorInfo.Invoke` live on declaring types that are not admitted; `Activator`,
      `Assembly` and `AppDomain` are not admitted at all. And `ResolveMethod` resolves a method's
      **parameter** types through the same allowlist, so an unadmitted parameter type fails the
      signature lookup before `Admit` is reached.

      **So stage 6's safety is a conjunction across several clauses, not one check** — and it
      would be broken by adding any of `Binder`, `MethodBase`, `MethodInfo`, `ConstructorInfo`,
      `PropertyInfo`, `Activator`, `Assembly` or `AppDomain`, none of which looks dangerous on its
      own. That is precisely why it is now **asserted rather than written down**:
      `DeserializationHardeningTest` builds the pivot end to end — `Type.GetType(…)` resolves,
      `.InvokeMember(…)` does not — and pins each blocked type individually.

      **One prediction the tests corrected.** `BindingFlags` was expected to be refused and is
      admitted, because every enum is. Sound — an enum constructs nothing, it only completes a
      *signature* — but the review says so explicitly, so that a later hardening pass does not
      spend effort on enums when the bound is the `Binder` beside them.

      Also recorded: five weaknesses accepted with reasons, what is **out of scope** (authn/authz,
      transport security, and DoS beyond payload size — stated so nobody assumes otherwise), and
      **a security consequence of W6 before it is built**: `CorrelationId` becomes a handle by
      which one caller can affect another caller's in-flight request, so it must be unguessable
      and connection-scoped.

- [x] **C50. The suite's failure count was a property of this machine. Now it is not.**
      **`Total tests: 22347, Passed: 21996, Failed: 134, Skipped: 217`** (`c50`) — **143 → 134,
      9 fixed, 0 broken.** ✅ `<this commit>`

      A64 established that nine failures were the `en-SE` locale and none of them this provider's:
      seven `_as_GeoJson`, where EF's own `JsonGeoJsonReaderWriter` re-emits a number with the
      culture-sensitive `StringBuilder.Append(reader.GetDecimal())` so `[2.0,4.0]` returns as
      `[2,0,4,0]`, and two decimal `InlineData` parameterizations xUnit cannot convert. EF's own
      suite fails them identically on this machine. They were left alone as "not ours".

      **Leaving them was the wrong call, and the reason is the ratchet rather than the nine.**
      CLAUDE.md had already written the tell without drawing the conclusion: *"the suite total is
      therefore locale-dependent — a machine with a `.` separator reports nine fewer failures with
      no code change."* C39 then committed `failed=143` to `test/known-failures.txt` and wired CI
      to gate on it. **That baseline was only true on this box.** A ratchet whose baseline depends
      on the runner's locale is not a ratchet, and the CI runner is not `en-SE`.

      A `[ModuleInitializer]` pins `CultureInfo.DefaultThreadCurrentCulture` to invariant before
      xUnit creates a test thread. Not a fixture: threads inherit the default unless they set
      their own, which is every thread in a parallel run, and a test that deliberately sets a
      culture still overrides it.

      **Not a suppression, and the distinction is worth keeping.** Nothing is skipped and no
      assertion is inverted; an environmental variable EF does not handle is removed, and the
      tests then run in the configuration EF supports. The nine were passing on other people's
      machines all along. What actually changed is that the number now means the same thing
      everywhere.

- [x] **C51. Correction: spatial on Tier B is not the cheap route C43 called it.** No code change.
      ✅ `<this commit>`

      C43 closed `SpatialQuery.Item` ×2 by classifying them, and ended *"the cheaper route is
      spatial on Tier B, where the store answers it"*. Priced properly, that is wrong, and it is
      the second underpriced claim of this phase after C40's.

      What checks out: **82 of EF's 83 `SpatialQuerySqliteTest` overrides just add `AssertSql`**
      and are behaviourally identical to the base, so SQLite really would answer the null-through-
      index question the base assumes. Only one changes behaviour.

      What does not: **there is no SpatiaLite in this repo at all** — no
      `Microsoft.EntityFrameworkCore.Sqlite.NetTopologySuite` package, no `UseNetTopologySuite`,
      no `mod_spatialite`. The Tier B store cannot map a `Point`. Moving the class means adding a
      NuGet dependency **and a native library** that has to load on this Windows box and on CI's
      ubuntu runner.

      ~~**That is not a tier move, it is a dependency decision, and it belongs to M7.**~~
      **Retracted by C52**, which added the package and found `mod_spatialite.dll` arrives from
      NuGet with no manual install. The dependency was never the blocker. The real answer is in
      C52 and it is better: the tier does not matter, because the index is evaluated on the
      client. Note the distinction from the old "needs SpatiaLite" note C15 demolished: that note
      was wrong about the *client's* type mapping, which needed only an NTS branch. A SQLite
      *backend* storing geometry does need SpatiaLite — it just comes for free.

- [x] **C52. Spatial on Tier B: attempted, measured, reverted — and `Item` is not a tier question
      at all.** **This class on Tier B: 12 failed of 168. On Tier A: 2 of 168.** No code change
      survives; this is the negative result. ✅ `<this commit>`

      C43 said Tier B was the cheap route for `SpatialQuery.Item` ×2. C51 said it was blocked by a
      native dependency. **Both were wrong, and the experiment settled it in one filtered run.**

      **C51's blocker does not exist.** `Microsoft.EntityFrameworkCore.Sqlite.NetTopologySuite`
      depends on the `mod_spatialite` NuGet package, which ships the native library —
      `runtimes/win-x64/native/mod_spatialite.dll` appeared in the output directory from a package
      reference alone, exactly as it does for EF's own `EFCore.Sqlite.FunctionalTests`. Nothing has
      to be installed. That claim is retracted.

      The fixture ported cleanly too: EF's three pieces —
      `AddEntityFrameworkSqliteNetTopologySuite()`, a `GeoPoint` type-mapping replacement, and a
      `Distance` DbFunction — are all **server-side**, and the factory's `onAddServices` /
      `onModelCreating` hooks reach the backend without touching the client model, which knows
      nothing relational.

      **And then the run said no.**

      | | Tier A | Tier B |
      |---|---|---|
      | failing | **2** | **12** |

      | Outcome | Tests | What it means |
      |---|---|---|
      | **`Item` unchanged** | 2 | **The decisive one.** Identical `NullReferenceException`, in the same client-side lambda. |
      | `Intersects_*` overrides become wrong | 4 | *"No exception was thrown"* — SQLite handles the null and the base **passes**. These two overrides are Tier-A workarounds. |
      | `SimpleSelect`, `Normalized`, `XY_with_collection_join` | 6 | New: `JsonException` converting to a NetTopologySuite geometry. The wire form against SpatiaLite's representation. Undiagnosed. |

      **`Item` is the finding, and C43 had already written it down before contradicting itself.**
      C24 moved the index onto the **client residual**, so the store never evaluates
      `MultiLineString[0]` under *any* backend. C43's own analysis says exactly that — and its
      closing sentence recommended changing the backend anyway. **The error was not the analysis;
      it was a conclusion inconsistent with the analysis two paragraphs above it.**

      **Do not repeat this.** Spatial stays Tier A. If it ever moves for another reason, the
      `Intersects_equal_to_null` / `Intersects_not_equal_to_null` overrides must be deleted, and
      the six `JsonException`s are the price to diagnose first.

      **Noticed while restoring, and unrelated:** `dotnet restore` already reports two known
      high-severity vulnerabilities in the test project's transitive graph —
      `System.Security.Cryptography.Xml` 9.0.0 and `SQLitePCLRaw.lib.e_sqlite3` 2.1.11. Both
      predate this step and are test-only, but nothing in the repo mentions them.

- [x] **C53. `SpatialQuery.Item` was never about null semantics or tiers. A member declared on a
      base class the model never names.**
      **`Total tests: 22347, Passed: 21998, Failed: 132, Skipped: 217`** (`c53`) — **134 → 132,
      2 fixed, 0 broken.** ✅ `<this commit>`

      **Three wrong diagnoses preceded this one, and the difference each time was a probe.** C43
      called it null-propagation semantics in the client residual and a decision about what the
      split guarantees. C51 called it blocked by a native dependency. C52 proved the tier is
      irrelevant but still framed the remedy as a projection-split question. All three reasoned
      from "the index is on the client" without asking **why**.

      Two probes answered it. The first printed the split:

          CAPTURED:  Select(e => new AnonType(Id = e.Id, Item0 = e.MultiLineString.get_Item(0)))
          REWRITTEN: Select(e => new ValueTuple(Item1 = e.Id, Item2 = e.MultiLineString))   <- ships
                    .Select(row => new AnonType(Id = row.Item1, Item0 = row.Item2.get_Item(0)))

      `CollectFragments` takes the **maximal** server-evaluable subexpression, so it stopping at
      `e.MultiLineString` meant the analyzer had refused the call. The second probe said why:

          REFUSED node: e.MultiLineString.get_Item(0)
             disallowed type: NetTopologySuite.Geometries.GeometryCollection

      **`MultiLineString`'s indexer is declared on `GeometryCollection`** — an intermediate class
      between it and `Geometry`, which the model never mentions. The allowlist admits a property's
      own CLR type and nothing above it, so the call named a type it had never heard of, the node
      was not server-ok, and the rewriter shipped the whole geometry and indexed it on the client
      — where `null[0]` is a `NullReferenceException`, because C# has no null propagation unless
      the expression asks for it.

      **The fix is four lines, and the argument was already in this file.** `AddSupertypes` admits
      an entity type's supertypes with the reasoning *"a supertype of an entity type is reachable
      only through an instance the model itself produced, so naming one widens nothing"*. That is
      exactly as true of a mapped **property** type, and had never been applied to one.
      `AddPropertyBaseTypes` walks the base-class chain, **base classes only and never a
      category** — interfaces would drag the whole generic-math surface in behind an `int`, and
      C23 measured widening to `ValueType`/`Enum` at **145 → 186** on a neighbouring mechanism.

      **The prediction was wrong in the good direction, which is worth recording too.** C52
      reasoned that pushing the index server-side would merely relocate the failure, because
      InMemory would evaluate `get_Item(0)` on a null in its own compiled lambda. It does not —
      EF's InMemory pipeline null-guards it, and both tests pass on **Tier A**. No SpatiaLite, no
      tier move, no semantic decision about the split.

      **And it is a W1 win independent of the two tests**: the wire now carries one indexed
      geometry per row instead of an entire `MultiLineString`.

- [x] **C55. The eight `Index_*` are the server's warning, not a missing client-side one — and
      forwarding the fixture's warning configuration is measured at 132 → 750.**
      **`Total tests: 22355, Passed: 21388, Failed: 750, Skipped: 217`** (`c55`) — **8 fixed, 626
      broken.** Reverted; no code change survives. ✅ `<this commit>`

      `Index_constant` / `Index_column` / `Index_parameter` / `Index_out_of_bounds` on
      `NavigationsCollectionQuery` and `OwnedNavigationsCollectionQuery` fail
      *"Assert.Throws() Failure: No exception was thrown"*. The base is
      `AssociationsCollectionTestBase.AssertOrderedCollectionQuery`, which expects an
      `InvalidOperationException` when `Fixture.AreCollectionsOrdered` is false —
      `NavigationsFixtureBase` and `OwnedNavigationsRelationalFixtureBase` both override it to
      `false`, and EF's own comment names what should throw:
      `RowLimitingOperationWithoutOrderByWarning`.

      **The standing plan was to raise it client-side, as C40 does for `InvalidIncludePathError`.
      That is wrong, and one probe said so.** C40's diagnostic comes out of *core*'s
      `NavigationExpandingExpressionVisitor`, which ADR-006 means nobody runs. This one comes out
      of `RelationalQueryableMethodTranslatingExpressionVisitor` — the **backend's** translator —
      and the probe in `QuerySplitter.Split` shows the query reaching it untouched:

          CAPTURED:  [EntityQueryRootExpression].Where(e => (e.AssociateCollection.get_Item(0).Int == 8))
          REWRITTEN: <identical>
          wholly=True shippable=1

      Nothing is missing on the client. **The server raises the warning and does not throw on it**,
      because `InfoCarrierBackendTestStore.AddProviderOptions` forwards
      `EnableSensitiveDataLogging` and deliberately not the rest of `FixtureBase.AddOptions` —
      its `ConfigureWarnings(Default(Throw))`. The comment there already said why: *"the server
      runs a tree this provider generated."*

      **Measured, because the comment was a reason rather than a number, and now it is a number.**
      Forwarding `Default(Throw).Log(SensitiveDataLoggingEnabled).Log(PossibleUnintendedReferenceComparison)`
      — the fixture's own configuration, verbatim — fixes all eight and breaks **626**:

      | New reason | Count |
      |---|---|
      | `Model.MappedComplexProperties` warning-as-error | 196 |
      | `Model.Validation.ShadowForeignKey` | 124 |
      | `Query.MultipleCollectionIncludeWarning` | 100 |
      | `Model.ConflictingKeylessAndKeyAttributes` | 95 |
      | `Model.MappedEntityTypeIgnoredMember` | 40 |
      | `Query.RowLimitingOperationWithoutOrderBy` (**tests other than these eight**) | 26 |
      | eight more query and model warnings | 45 |

      **The finding is in the distribution, not the total.** Most of the 626 are **model**
      warnings, and the server's model is not the caller's — it is built by
      `TestModelSource` against the backing store, so it validates differently from the client's.
      A warning about the model is a statement about a thing the caller never wrote. The query
      warnings are the same argument one level along: 100 `MultipleCollectionIncludeWarning`s land
      on trees this provider's `ProjectionRewriter` and `GroupJoinFlattener` produced.

      **So the honest classification, and it is a harness knob rather than a provider gap.** These
      eight tests assert a diagnostic that this provider *does* raise, in the half of itself that
      knows about it, and that the harness does not configure as an error there. Making them green
      means either forwarding the whole configuration (626) or naming
      `RowLimitingOperationWithoutOrderByWarning` on its own, which is choosing one event because
      six tests assert it — test-tuning wearing a configuration's clothes. **Left red and
      classified.** If a real deployment configures warnings-as-error on its server, its callers
      get the exception these tests expect; that is the arrangement being reproduced, not a
      missing feature.

      **And the ordering question the classification turned on is settled with it:** this
      provider's collections are **not** ordered. They inherit the backing store's order, which is
      exactly what EF's two fixtures say by overriding `AreCollectionsOrdered` to `false` for this
      model on this store. Overriding it back to `true` on our fixtures would assert an ordering
      nothing guarantees, so that route is closed rather than untried.

- [x] **C56. A projection that is still a query — the third diagnostic ADR-006 skips, and the one
      the "A28 family" was hiding.**
      **`Total tests: 22356, Passed: 22019, Failed: 120, Skipped: 217`** (`c56`) — **132 → 120,
      12 fixed, 0 broken.** ✅ `<this commit>`

      Twelve of the twenty `ComplexNavigations` failures were filed under the
      `AssertInvalidMaterializationType` family — the standing note called that family a decision,
      because the assert helper is `protected static` and the only route seemed to be overriding
      each test and duplicating EF's query body. **It was not a decision. It was C40's mechanism a
      third time, and the tell was in the failing list rather than in any of the tests.**

      `NorthwindMiscellaneous` has **six** tests asserting the same refusal —
      `Select_correlated_subquery_filtered_returning_queryable_throws` and friends — and every one
      of them **passes**. A refusal this provider supposedly does not have, asserted six times, all
      green. The difference is only where the boundary falls: EF raises it in
      `QueryableMethodNormalizingExpressionVisitor.VerifyReturnType`, the opening stage of
      `QueryTranslationPreprocessor`, which is downstream of ADR-006's capture point. **A wholly
      shippable query gets the refusal from the server and always has.** The twelve are the ones
      where the split leaves the projection on the client, and then nobody asks.

      Four shapes, four different wrong answers, all the same cause:

      | Test | What this provider did instead |
      |---|---|
      | `Complex_query_with_let_collection_SelectMany` ×4 | ran, no exception |
      | `Select_projecting_queryable_in_anonymous_projection_followed_by_Join` ×4 | ran, no exception |
      | `Queryable_in_subquery_works_when_final_projection_is_List` ×4 | `ArgumentNullException: source` — `FirstOrDefault()` gave null and `ToList()` took it |

      `QueryableProjectionValidator` mirrors EF's walk exactly: `Queryable.Select` only, recursing
      through `New` and `MemberInit` so a queryable hidden in an anonymous type is found, EF's own
      `CoreStrings.QueryInvalidMaterializationType`. It runs beside C40's include checks, before
      the `IsWhollyServerExecutable` early return, in EF's own order.

      **Adopting the refusal rather than the answer is right here, and this provider has a reason
      EF does not: an `IQueryable<T>` cannot cross the wire.** What comes back for such a
      projection is a materialized list, so the declared element type is a promise the provider
      cannot keep — which is exactly what the fourth shape,
      `Join_with_result_selector_returning_queryable_throws_validation_error` ×4, has been saying
      all along as *"Unable to cast object of type `List<Level3>` to type `IQueryable<Level3>`"*.
      **Those four stay red**, and faithfully so: EF's own check is `Select`-only and that query's
      queryable comes out of a `Join` result selector, so EF misses it too and fails later, in
      InMemory with `ArgumentException` and in SQLite with `ApplyNotSupported`. Widening the walk
      past what EF does would be inventing a rule, not mirroring one.

      **The unit test was seen to fail.** `A_collection_the_projection_returns_is_left_for_EF_to_refuse`
      asserted the opposite — that the queryable ships verbatim — and its name was the whole
      mistake in four words: *left for EF to refuse* is true only of a query that ships whole. It
      is now `A_collection_the_projection_returns_is_refused`, checked by commenting the call out
      and watching it go red, and joined by one asserting that an ordinary materialized collection
      still is not refused.

- [x] **C57. Five `OwnedNavigations` overrides had gone stale, and C20 declined three of them for
      a reason the two strings refute.**
      **`Total tests: 22356, Passed: 22024, Failed: 115, Skipped: 217`** (`c57`) —
      **120 → 115, 5 fixed, 0 broken.** ✅ `<this commit>`

      Test-only. All five are ours, none is an EF spec test, and each is replaced by what EF's own
      SQLite suite says — A63, applied in the direction C46 and C52 established: **an override of
      ours can go stale, and the check is whether its stated reason is still the reason.**

      | Ours | Actual | EF's |
      |---|---|---|
      | `Distinct_projected(TrackAll)` asserts `ApplyNotSupported` | `EqualException` — the base compared *"A tracking query is attempting to project…"* against `ApplyNotSupported` | `OwnedNavigationsCollectionSqliteTest`: `TrackAll` → no-op, *"Base test expects 'can't track owned entities' exception, but with SQLite we get 'no CROSS APPLY'"* |
      | `Select_subquery_{optional,required}_related_FirstOrDefault(TrackAll)` run the base | the same two strings | `OwnedJsonProjectionSqliteTest`: the same no-op, the same comment |
      | `Over_associate_collection_projected` ×2 asserts `InsufficientInformationToIdentifyElementOfCollectionJoin` | `ApplyNotSupported` | `OwnedNavigationsSetOperationsSqliteTest`: *"SQL APPLY not supported in SQLite — different exception message from the one expected in the base class"* |

      **C20's decline is the part worth recording.** It read: *"here `TrackAll` fails on a string
      comparison instead, which is a different statement — so it is left red rather than silenced
      under a reason that is not ours (A63 cuts both ways)."* The caution was right and the
      conclusion was not: the string comparison **is** EF's statement, and the two strings say so
      outright. `Expected: "A tracking query is attempting to project"` /
      `Actual: "Translating this query requires the SQL APPLY"` is EF's comment in evidence form.
      Corrected in place, in the override's own remarks, as C40's and C43's attributions were.

      **The third one is a translation between inheritance chains rather than a copy.** EF's
      `Over_associate_collection_projected` override wraps the base in
      `Assert.ThrowsAsync<EqualException>` — because *its* base is the **relational** one, which
      makes the message assertion EF then expects to fail. Ours derives from the **core** base, so
      the same fact is stated directly as `ApplyNotSupported`. The tell that it was SQLite's limit
      and not the relational base's: **the identical test passes on `Navigations`**, where the
      query does not reach APPLY.

- [x] **C58. B16's optional remedy, attempted: the cheap route into `CoreOptionsExtension` does not
      exist, and half the family was never on that channel anyway.** No code change survives; this
      is the mechanism, established by two probes. ✅ `<this commit>`

      B16 left the `MaterializationInterception` family red-and-classified with an optional harness
      remedy: stop forwarding the client's *interceptors* to the server while still forwarding
      everything else `onConfiguring` carries. It named one obstacle — *"`WithInterceptors`
      concatenates, `Clone` is protected, so the extension would have to be replaced wholesale with
      one rebuilt by hand from its public `With*` setters"*. **There appeared to be a way around
      that, and there is not.**

      The apparent way: `CoreOptionsExtension`'s copy constructor is `protected` and reads the
      `Interceptors` and `SingletonInterceptors` **properties**, both `virtual`. So a subclass
      overriding them to null, copy-constructed twice, yields an extension with both the properties
      and the backing fields empty — which covers `Validate` (property) and `ApplyServices` (field)
      alike. It builds, it runs, and the probe says it strips nothing:

          SERVER opts:  core=True interceptors=4 singleton=4
          AFTER STRIP:  type=CoreOptionsExtension interceptors=4 singleton=4

      **`DbContextOptions<TContext>.WithExtension` keys the map on `extension.GetType()`, the
      runtime type.** A subclass is therefore filed under its *own* key and the original
      `CoreOptionsExtension` stays exactly where it was — still found by `FindExtension`, still
      validated. **Any subclass route is closed**, whatever it overrides. B16's "rebuilt by hand
      from its public `With*` setters" is not one option among several; it is the only one.

      **And the second probe halves the prize.** The 26 arrive on two different channels, because
      `SingletonInterceptorsTestBase.CreateContext` passes `useServiceProvider: inject`:

      | `inject` | How the interceptor reaches the server | What stripping options would do |
      |---|---|---|
      | `False` | the client's `onConfiguring` → `AddInterceptors` → `CoreOptionsExtension` | removes it |
      | `True` | `addServices` → the server's own `IServiceCollection` | **nothing** |

      So the options rebuild reaches at most half, and the other half is the service-collection
      filter B16 already measured at **1629** (all `ISingletonInterceptor` — `AddEntityFrameworkProxies`
      registers one) or **246** (`IMaterializationInterceptor` only — `PropertyValuesFixtureBase`
      wants its server-side). Closing that half means changing `PropertyValues`' fixture to
      register on the backend additively, per fixture, by hand.

      **Corrected while here: A71 is not what it was filed as.** The 10
      *"A call was made to `AddInterceptors`, but Entity Framework is not building its own internal
      service provider"* failures are the **server's**, not a client-side wiring gap — the server
      always calls `UseInternalServiceProvider`, and it is the forwarded `onConfiguring` that then
      calls `AddInterceptors`. EF's own client never hits it, because `useServiceProvider: inject`
      means the internal provider and the options-level interceptor are never both present there.
      They are the same defect as the twelve `Assert.Same`, not a separate one.

      **Still optional, and still B16's answer**: the product forwards no interceptor, a real
      deployment may hook either side or both, and what these tests assert is a one-context
      topology. Priced properly the remedy is a hand-rebuilt `CoreOptionsExtension` — sixteen
      `With*` properties to mirror, silently wrong if one is missed, and re-checked at every EF
      upgrade — plus a per-fixture DI change, for at most half a family that is already classified.
      **Not taken.**

- [x] **C59. "Client evaluation is legal only in the final projection" is not a rule you can
      implement. Measured: 6 fixed, 18 broken.**
      **`Total tests: 22358, Passed: 22014, Failed: 127, Skipped: 217`** (`c59`) — **115 → 127.**
      Reverted; no code change survives. ✅ `<this commit>`

      Six failures across two families shared one shape, and a probe in `RejectClientEvaluation`
      printed it rather than leaving it to be inferred:

          Join(root, root.Select(o => (o, o.Customer)).Select(row => ClientProjection(…)), …)
          …Select(row => ClientLevel1(…)).Take(2).LeftJoin(root, …)

      Both put a **join on the client** over a sequence the client computed — fetching both sides
      entire, the plan ADR-010 exists to refuse — and EF refuses both queries outright. The
      client code sits in a `ProjectionRewriter` **reassembly**, and `ClientEvaluationFinder`
      exempts reassemblies wholesale, so the finder returned `<none>`.

      The rule tried: a reassembly is exempt only while it is *final* — that is, while no query
      operator composes over it, walking up through the operators that pick a row rather than
      change the projection (cardinality, markers, `Take`/`Skip`). Restricted to client **methods**,
      because refusing a constructed client *type* in that position is the boundary this milestone
      is about and cost 235 tests once. It fixes the six, and:

      | Broken | What it is |
      |---|---|
      | `GroupJoin_in_subquery_with_client_projection` + `_nested1` + `_nested2` ×2 classes ×2 | 12 |
      | `Count_on_projection_with_client_eval` ×2, `SelectMany_with_client_eval_with_collection_shaper_ignored` ×2, `Client_eval_Union_FirstOrDefault` ×2 | 6 |

      **Every one of the eighteen has `client_eval` or `client_projection` in its name, and EF
      passes them all.** So the rule is wrong, and specifically: **EF permits a query operator over
      a client-evaluated projection in at least five named shapes** — a `GroupJoin` in a subquery,
      a `Count`, a `SelectMany` collection shaper, and a `Union` under `FirstOrDefault`. What EF
      actually refuses in the six is narrower than "the projection is not final", and reading its
      test names is what says so.

      **Left as a negative result rather than narrowed by guessing.** Narrowing it to "a `Join`
      whose source is a client-computed sequence" is contradicted by
      `GroupJoin_in_subquery_with_client_projection` in the same run. Finding the real line means
      reading what `NavigationExpandingExpressionVisitor` does with a client-evaluated shaper, not
      another rule fitted to six tests. The six stay red and classified: **this provider answers a
      query EF refuses**, which is the A28 shape with the sign flipped.

- [x] **C60. The singles and pairs, examined one at a time — one is A63, the rest are classified
      with evidence.**
      **`Total tests: 22356, Passed: 22025, Failed: 114, Skipped: 217`** (`c60`) —
      **115 → 114, 1 fixed, 0 broken.** ✅ `<this commit>`

      Nine failures across seven classes had never been looked at individually. Each was read out
      of `c56.log`/`c57.log` — the assertion and the stack, not the count.

      **One is a missing override, and it is A63's usual shape.**
      `ComplexPropertiesStructuralEquality.Contains_with_nested_and_composed_operators` fails with
      the server's own *"Translation of `EF.Property<int?>(CollectionResultExpression: …
      RootEntity.AssociateCollection#AssociateType.NestedCollection …, "Id")` failed"*. EF states
      the limit on `ComplexTableSplittingStructuralEqualityRelationalTestBase`: *"Collections are
      not supported with table splitting, only JSON. Note that the exception is correct, since the
      collections in the test data are null for table splitting."* **A complex property on a
      relational store *is* table splitting**, so that base describes this fixture even though it
      is not in the class's chain — the same reasoning C57 used to translate an override between
      inheritance chains.

      **The rest are classified, not fixed.**

      | Failure | Reading |
      |---|---|
      | `ProxyGraphUpdates+LazyLoading` / `+LazyLoadingAndChangeTracking.Save_two_entity_cycle_with_lazy_loading` ×2 | The base branches on `context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory"` and expects a `CircularDependency` throw otherwise. **Under this provider that name is the client's and the behaviour is the backend's** — the InMemory backend permits the cycle, so the test takes the branch its own store contradicts. B16's topology shape, in a new place. Tier B would make the branch true and costs moving a 1700-test base; not attempted. |
      | `AdHocAdvancedMappingsQuery.Casts_are_removed_from_expression_tree_when_redundant` | Asserts `CoreStrings.TranslationFailed` against an **exactly printed** expression (`DbSet<MockEntity>()    .Cast<IDummyEntity>()    .Where(…)`). `IDummyEntity` is a type `MockEntity` does not implement, the cast lands on the client, and `Enumerable.Cast` throws `InvalidCastException`. Green would require reproducing EF's printer output byte for byte over a tree this provider has rewritten. |
      | `OptimisticConcurrency.Nullable_client_side_concurrency_token_can_be_used` | `"Intercepted: Intercepted: New name"`. B16's two invocations, priced in C58. |
      | `GearsOfWarQuery.Query_with_complex_let_containing_ordering_and_filter_projecting_firstOrDefault_element_of_let` ×2 | `NullReferenceException` in the client residual on `automaticWeapons.FirstOrDefault().Name` — a store propagates null through that and C# does not. C43's shape; **C53's lesson says probe the boundary verdict before calling it semantics**, and that probe has not been run on this one. |
      | `CustomConverters.Composition_over_collection_of_complex_mapped_as_scalar` | EF refuses (`TranslationFailed`, again an exactly printed lambda) and this provider answers. **C59's family**, and C59 measured what a general rule for it costs. |
      | `NonSharedPrimitiveCollectionsQuerySqlite.Array_of_TimeOnly` | *"Sequence contains no elements"* — the server matched no row. **Undiagnosed, and the clue is in its siblings**: `Array_of_TimeOnly_with_milliseconds` and `_with_microseconds` both pass, so whatever it is, it is specific to a `TimeOnly` with no sub-second part rather than to `TimeOnly` as such. |
      | `Query.StringTranslations.Regex_IsMatch` ×2 | A46, a deliberate allowlist refusal. Unchanged. |

- [x] **C61. The `GearsOfWar` `let` pair, probed as C53 prescribes — it is the sequence-slot guard,
      and that guard is already priced at 107.** No code change; this is the diagnosis C60 said was
      owed. ✅ `<this commit>`

      C60 left `Query_with_complex_let_containing_ordering_and_filter_projecting_firstOrDefault_element_of_let`
      ×2 as *"C43's shape, and C53's lesson says probe the boundary verdict before calling it
      semantics — that probe has not been run"*. It has now, and the answer is not semantics.

      **Three probes, each one step further in.** The split:

          CAPTURED:  …Select(g => new AnonType456(g = g, automaticWeapons = g.Weapons.OrderByDescending(…).Where(…)))
                     …Select(ti => new AnonType457(Nickname = ti.g.Nickname,
                                                   WeaponName = ti.automaticWeapons.FirstOrDefault().Name))
          REWRITTEN: …Select(g => new ValueTuple(Item1 = g, Item2 = g.Weapons.…))          <- ships
                     …Select(row => new AnonType456(g = row.Item1, automaticWeapons = row.Item2))
                     …Select(ti => new AnonType457(… ti.automaticWeapons.FirstOrDefault().Name))
          wholly=False shippable=1

      The **final** projection is on the client, and there `FirstOrDefault()` over a gear with no
      automatic weapon is `null`, so `.Name` is a `NullReferenceException`. The refusal probe named
      the type: `AnonType456<Gear, IEnumerable<Weapon>>` — the `let`'s transparent identifier,
      which is exactly what ADR-011's re-carry exists to remove.

      **So why did the re-carry not remove it?** A third probe, in `ReCarryInternalTypes`:

          RECARRY: changed=True  kept=False

      It produced a candidate and `RewriteVerifier` rejected it as `NoGain` — **correctly**, because
      the candidate had tupled only `AnonType457`, the *result* type, which `RebuildAtRoot` then
      rebuilds anyway. `AnonType456` was never a candidate at all.

      **And that is a guard with a number on it.** `CarrierFinder.Register`:

          if (construction.Arguments.Any(a => IsSequence(a.Type) && !IsNull(a)))
          {
              _disqualified.Add(construction.Type);

      *"A slot holding a sequence asks SQL to navigate out of a projected tuple back into a
      correlated collection — `t.Item2.DefaultIfEmpty()` — and 107 translation failures followed."*
      `automaticWeapons` is a sequence, so the carrier is disqualified by design, and the consumer
      here does the very thing the guard is about: `t.Item2.FirstOrDefault().Name`.

      **Reclassified, and this is the point of the entry.** These two are not C43's null-propagation
      question and not an unexamined gap — they are **the measured cost of the sequence-slot guard,
      visible from the other side.** Anyone tempted to relax that guard should read spec §4 first
      and expect to pay 107; the two tests here are what one would buy.

- [x] **C62. `Array_of_TimeOnly` is EF issue #30730, and EF skips it in its own SQLite suite.** No
      code change; this is the diagnosis C60 said was owed, and it closes the last undiagnosed
      exception outside `JsonQuery`. ✅ `<this commit>`

      C60 recorded it as *"undiagnosed, and the clue is in its siblings"* — `_with_milliseconds`
      and `_with_microseconds` both pass, so it is specific to a `TimeOnly` with no sub-second
      part. **Two probes, C42's order: what the client holds, then the row the store actually
      holds.**

          ROW 1: [12:30:00.0000000, 12:30:00.0000000]      <- read back through the wire
          CONSTANT value1 = 12:30:00.0000000
          DB …db Id=1 SomeArray=["12:30:00.0000000","12:30:00.0000000"]

      **Everything this provider touches is correct.** The seed crossed the wire, the server wrote
      it, SQLite holds a seven-digit fraction, and the client reads the same value back. The
      constant is right too. So the failure — *"Sequence contains no elements"* out of
      `SingleAsync` — is in the SQL comparison, and EF's own suite says exactly what it is:

          [ConditionalFact(Skip = "Issue #30730: TODO: SQLite is not matching elements here.")]
          public override async Task Array_of_TimeOnly()
              …  WHERE "s"."value" = '12:30:00') = 2

      **`'12:30:00'` against a stored `"12:30:00.0000000"`.** SQLite's `TimeOnly` literal drops a
      trailing zero fraction and its JSON element keeps all seven digits; the two agree for
      `.1230000` and `.1234560`, which is why the two siblings pass, and disagree for a whole
      second. Not this provider's — the same defect, the same store, the same SQL.

      **Left red rather than skipped, and that is a deliberate reading of the two rules.** A63
      encourages adopting EF's own override when the reason matches, and the reason matches
      exactly — but EF's override here *is* a `[Skip]`, which the guardrail names by name. Leaving
      it red with a stated, verified reason costs one number and keeps the guardrail unambiguous.
      **The option is recorded rather than taken**: adopting EF's attribute verbatim would move it
      from `Failed` to `Skipped`, and it is a one-line change if that is preferred.

## Phase B — the tier audit, and the rework it found

**Why this phase exists.** A70 and A77 each reverted a spec base after establishing that EF's
InMemory provider could not host it, and stopped there. A79 showed that was the wrong conclusion
twice over: ADR-009 defines Tier B for exactly that case, and both bases pass on it — `FunkyDataQuery`
**38 of 38** and `AdHocComplexTypeQuery` **4 of 4**, first run, no overrides. The signal I had been
using ("EF's own suite does not derive from this base") was only ever checked against
`EFCore.InMemory.FunctionalTests`.

**The rule, corrected.** *"EF ships no InMemory test for this base"* means **move it to Tier B**.
Only *"EF ships no test for it on any store we have"* justifies leaving a base unadopted. Adopting
on the wrong tier is not a neutral choice: it produces failures that describe the backing store
rather than this provider, and it invites workarounds for problems the right tier does not have.

**The audit.** Every class holding `InfoCarrierTestStoreFactory.InMemory` was matched against the
spec bases it inherits, and each base against whether EF ships an `…InMemoryTest` and an
`…SqliteTest`. 48 bases; **46 have an InMemory counterpart and are on the right tier.** Two did not:

| Base | EF InMemory | EF SQLite | Verdict |
|---|---|---|---|
| `ConferencePlannerTestBase` | **no** | yes | **Wrong tier. Moved in A80.** |
| `MonsterFixupTestBase` | yes (`MonsterFixupSnapshot…`) | no | Correct — the audit glob missed the infix. |

- [x] **A80.** `ConferencePlanner` moved to Tier B; the audit above. ✅ `<this commit>`

      **24 of 24 on Tier B, and the A76 workaround deleted rather than carried.** That workaround is
      the part worth recording: A76 added a reseed-after-every-test override because the base wraps
      each test in a transaction and Tier A has none. The evidence that the tier was wrong was
      therefore already in hand, and was read as a fixture quirk instead. On Tier B the transaction
      is real, `UseTransaction` enlists the second context in it (the same hook
      `OptimisticConcurrencyInfoCarrierTest` needs), and nothing has to be put back by hand.

      **The tell, for next time: if adopting a base means writing a workaround for a store
      capability the base assumes, check the tier before writing the workaround.**

- [x] **A81. Every base is adopted exactly once.** **`Total tests: 20560, Passed: 20188,
      Failed: 173, Skipped: 199`** — **910 tests removed, failure count unchanged, unadopted bases
      unchanged at 47.** ✅ `<this commit>`

      Three bases were adopted on **both** tiers: `NorthwindJoinQueryTestBase`,
      `NorthwindSelectQueryTestBase` and `NorthwindWhereQueryTestBase`. All three pairs were green
      on both, with identical counts — 906 tests of pure duplication, about 4% of the suite's
      runtime for no additional information. The Tier A copies are removed and the Tier B ones kept.

      **Why Tier B is the one to keep for a query base.** Tier A client-evaluates nearly everything,
      so a green there is weaker evidence than a green on a tier that actually translates — which is
      the reason `NorthwindQueryInfoCarrierSqliteFixture` was written in the first place. Keeping
      the weaker copy and deleting the stronger one would be the wrong way round.

      That the failure count did not move, and that no base became unadopted, is the check that this
      removed duplication rather than coverage.

- [ ] **B1. One tier per base — decide where the rest of the query corpus belongs.**
      **Supersedes the "dual-tier the query bases" item this replaces, which was wrong:** a base
      belongs on exactly one tier (A81), so the question is *which*, not *both*.

      Nineteen Northwind bases plus the complex-navigations, gears-of-war and many-to-many families
      still run on Tier A alone, and EF ships SQLite counterparts for all of them. By the argument
      A81 settles, a query base is better judged on the tier that translates — so the default answer
      is "move them" — but three things have to be weighed first, and none of them is free:

      1. **The 12 failures classified as "asserts a limitation this provider does not have" have
         never been run on a tier that has the limitation.** That classification is an assumption.
         Moving those bases is the measurement that settles it, and it may turn some of them green
         and others into real defects.
      2. **Tier A is not worthless for a query base.** It is where the InMemory-limitation overrides
         were derived, and it is the tier most of the change-tracking suite runs on; moving the
         query corpus wholesale leaves the two halves of the suite on different stores.
      3. **The Tier B store is file-backed and slower.** 906 duplicated tests cost ~4% of runtime;
         moving twenty-odd bases changes the whole suite's shape.

      Do it a base at a time, measuring, and record which tier each one ends on and why. Do **not**
      do it as one sweep.

      **Reason 1 is now answered, and it was the one that could be settled by experiment.** B8 ran
      `ComplexNavigations` on Tier B: not one of its ten A28-shape failures moved, and
      `No exception was thrown` means on a translating store exactly what it meant on a
      client-evaluating one. **The classification was right; there is nothing there to find.**

      What is left of B1 is reasons 2 and 3, and neither is a measurement — B8 priced the move at
      **~32 genuinely new reds per base after the `APPLY` overrides are mirrored**, and there are
      twenty-odd bases. **That is a scope decision. Put the number to the roadmap before moving any
      more query bases.**

- [ ] **B2. Re-judge the deferred reds on the tier that can answer them.** Not before B1. In
      particular:

      | Family | Count | Where it should be judged |
      |---|---:|---|
      | `ComplexTypeQuery` `Values differ` / `Strings differ` | 62 | Already Tier B (A79). Real provider work; undiagnosed on purpose. |
      | `JsonTypes` spatial | 26 | Neither tier: SQLite needs the SpatiaLite package, which is not referenced. A roadmap question, not a plan one. |
      | `MaterializationInterception` | 26 | Tier A is right (EF ships an InMemory test). 16 are the real gap, 10 are A71's blocked wiring. |
      | the A28 "asserts a limitation we do not have" set | 12 | **Tier B, after B1.** |
      | `JsonTypes` decimal | 4 | Neither — machine locale (A64). |

### B3 — the bases the corrected rule makes adoptable

The audit that produced A80 asked one question of each *adopted* class. This asks the other
question of each *unadopted* base: not "does EF ship an InMemory test", but "does EF ship a test
on **any** store we have". Six of the 46 turn out to have a SQLite counterpart and no InMemory
one, which under the corrected rule is a Tier B adoption, not a reason to leave them out:

| Base | EF InMemory | EF SQLite | Verdict |
|---|---|---|---|
| `Query.NonSharedPrimitiveCollectionsQueryTestBase` | no | `NonSharedPrimitiveCollectionsQuerySqliteTest` | **Tier B — B3a** |
| `Query.PrimitiveCollectionsQueryTestBase` | no | `PrimitiveCollectionsQuerySqliteTest` | **Tier B — B3b** |
| `Query.JsonQueryTestBase` | no | `JsonQuerySqliteTest` | **Tier B — B3c** |
| `Query.AdHocJsonQueryTestBase` | no | `AdHocJsonQuerySqliteTest` | **Tier B — B3d** |
| `Types.TypeTestBase` | no | `SqliteMiscellaneousTypeTest` &c. | **Tier B — B3e** |
| `StoreGeneratedTestBase` | no | `StoreGeneratedSqliteTest` | **Tier B — B3f** |

The nine that stay unadopted are unchanged and stay for their own reasons: six are infrastructure
(`ApiConsistency`, `EntityFrameworkServiceCollectionExtensions`, `Logging`, `ModelBuilding101`,
`Scaffolding.CompiledModel`, and `ComplianceTestBase` itself), two are spatial (SQLite needs the
SpatiaLite package, which is not referenced — a roadmap question), and `SeedingTestBase` is
blocked by A65: its `SeedingContext` takes a `string testId` and has no `DbContextOptions`
constructor, so the backend store cannot build the server's copy.

- [x] **B3a. `NonSharedPrimitiveCollectionsQuery` on Tier B.** **`Failed: 7, Passed: 18,
      Skipped: 1, Total: 26`**, first run, no overrides. ✅ `<this commit>`

      Two forwarded members on the A49 harness, the SQLite backend behind the wire, and the core
      base rather than `NonSharedPrimitiveCollectionsQueryRelationalTestBase`, which asserts SQL.

      **The 7 reds are one defect and one loose end, and both are new information — this is the
      first base that puts a primitive *collection* on the wire.**

      **Six of them (`Array_of_DateTime`, `…_with_milliseconds`, `…_with_microseconds`,
      `Array_of_DateTimeOffset`, `Array_of_TimeOnly`, `Array_of_decimal`) are one bug: the wire
      form of a primitive collection is provider-specific, so the two sides disagree about it.**
      `PrimitiveCoercion.JsonForm` reads the reader/writer off `property.FindTypeMapping()`, and
      that mapping is the *backing store's* on the server and this provider's on the client.
      SQLite's `DateTime` element writes `2023-01-01 12:30:00`; core's `JsonDateTimeReaderWriter`
      reads ISO-8601 and throws `FormatException: The JSON value is not in a supported DateTime
      format`. `decimal` is the same shape — SQLite writes `'1.0'` as a JSON string, core's reader
      wants a number. Both directions are affected, since `SaveChanges` runs the mirror image.
      Fixed in B4.

      The seventh, `Array_of_byte`, is unrelated and left red: the server fails to translate
      `a => a == 1` over the collection, which EF's own SQLite suite does not have to override.

- [x] **B3b. `PrimitiveCollectionsQuery` on Tier B.** **`Failed: 132, Passed: 31, Skipped: 2,
      Total: 165`**, first run, no overrides. ✅ `<this commit>`

      The shared-model half of the same corpus, and it makes B3a's finding much larger than it
      looked: **99 of the 132 are the one `FormatException` B4 is about**, plus one
      `DateTimeOffset` and one `Cannot get the value of a token type 'String' as a number` — the
      decimal shape — for **101 of 132 on a single cause**. The base's entity carries a
      `DateTime[]`, so nearly every test in it reads one back.

      Of the remaining 31: **13 are `Translating this query requires the SQL APPLY operation`**,
      which is the A79 shape — a limit of the backing store that EF's own SQLite suite overrides,
      so they are convergence with the reference provider rather than defects, and the overrides
      go in once B4 has cleared the noise around them. Six are `no such column: p.Int`, three are
      unsupported LINQ, and the rest are singletons.

- [x] **B4. A primitive collection's wire form must not be the backing store's.**
      **`Total tests: 20725, Passed: 20320, Failed: 204, Skipped: 201`** — **106 fixed** across the
      two bases B3a/B3b adopted, nothing broken anywhere else. ✅ `<this commit>`

      `PrimitiveCollectionsQuery` **132 → 31**, `NonSharedPrimitiveCollectionsQuery` **7 → 2**.
      Measured against `a81`, so the run's `BROKEN` list is the two new bases' residual and its
      `FIXED` list is empty by construction; what matters is that the residual is 33 where the
      adoptions reported 139, and that no reason outside those two classes moved.

      **The defect.** `PrimitiveCoercion.JsonForm` read the JSON reader/writer off
      `property.FindTypeMapping()`. That mapping is the *backing store's* on the server and this
      provider's on the client, and the two do not agree: SQLite writes a `DateTime` element as
      `2023-01-01 12:30:00`, EF's core `JsonDateTimeReaderWriter` reads ISO-8601 and threw
      `FormatException: The JSON value is not in a supported DateTime format`. `decimal` was the
      same shape — SQLite writes the JSON string `'1.0'` where the core reader wants a number.
      Both directions were affected; `SaveChanges` runs the mirror image.

      **Why it only surfaced now.** For a *scalar* it cannot happen: every store agrees on the wire
      primitives and `IsWirePrimitive` short-circuits them before a mapping is consulted. A
      *collection* of them is not itself a wire primitive, so it fell through to the mapping — and a
      mapping is exactly the thing the two ends are entitled to disagree about. Tier A never showed
      it because EF's InMemory provider does not map primitive collections at all, which is why
      these two bases were unadopted until B3.

      **The fix, and the one thing to keep hold of.** The collection's JSON form is now derived from
      the **CLR type alone**, through EF's own core `JsonValueReaderWriterSource` (which no provider
      replaces) and its own collection wrappers, with `ConcreteCollectionType` copied from
      `TypeMappingSourceBase.TryFindJsonCollectionMapping`. Deriving it from the *model* instead —
      `IProperty.GetElementType()` — would have reintroduced the asymmetry, because that is a
      modelling answer each side computes for itself with its own mapping source. An element the
      core source does not know, which includes every element behind a value converter, falls
      through to the old path unchanged.

      **The rule: anything the wire computes from a type mapping is computed twice, by two
      providers, and is only sound if the two agree.**

      What is left in the two bases is unrelated and genuine: 13 `SQL APPLY` (the A79 shape — EF's
      own SQLite suite overrides them), 6 `no such column: p.Int`, and a tail of translation
      failures including `Array_of_byte`.

- [x] **B5. The server gets the fixture's `ConfigureConventions` too.**
      **`Total tests: 20725, Passed: 20320, Failed: 204, Skipped: 201`** — **byte-identical to B4**:
      no test fixed, none broken, reasons unchanged. Kept anyway. ✅ `<this commit>`

      **What was missing.** A fixture states its model in two places, and the harness carried one.
      `SharedTestStoreProperties` had `OnModelCreating` and nothing for `ConfigureConventions`, so
      the server model was built without it — every `NorthwindQueryFixtureBase` routes its model
      customizer through it, `LazyLoadProxyTestBase` declares five complex types there, and
      `StoreGeneratedFixtureBase` registers three dozen value converters. `TestModelSource.GetFactory`
      has taken a `configureConventions` argument since EF wrote it; it was simply never passed.

      Now plumbed through `SharedTestStoreProperties.ConfigureConventions`, `Create`'s new optional
      `configureConventions`, and `NonSharedModelInfoCarrierHarness.Prepare`, and **passed at all 51
      call sites** rather than at the ones judged to need it — "the server builds the same model as
      the client" is the harness's contract, and a per-fixture judgement about half of it is exactly
      the drift that produces a wrong classification later.

      **Why a null result is kept.** It is inert *today* because no adopted fixture's
      `ConfigureConventions` changes what its server model does — that is a fact about the bases
      adopted so far, not about the harness. B3f cannot be adopted without it: `StoreGenerated`'s
      converters live there and nowhere else.

      **And the null result was verified rather than assumed** (the A57 rule): a probe on the model-
      source registration prints `store=Northwind conventions=True`, so the delegate arrives and is
      passed. An unmeasurable change that is never shown to run is indistinguishable from one that
      does nothing.

- [x] **B3f. `StoreGenerated` on Tier B.** **`Failed: 12, Passed: 199, Total: 211`**, one override,
      which is EF's own. ✅ `<this commit>`

      A base that was on the do-not-adopt list for the superseded reason, and it is **199 of 211
      first run** — the strongest single argument yet for the corrected rule. A store-generated
      value is produced on the far side of the wire and has to come back, so this is as central to
      the provider as a base gets, and it very nearly works.

      One override, `Fields_used_correctly_for_store_generated_values`, mirrored from
      `StoreGeneratedSqliteTest` with its reason: SQLite has no computed columns. `UseTransaction`
      enlists, as `ConferencePlanner` and `OptimisticConcurrency` do. The fixture's
      `OnModelCreating` is EF's SQLite one — a store-generated value has to come from a column
      default, which is the backing store's to state, not something this provider could invent.

      **The 12 are three findings, and the first is the interesting one.**

      **Eight are `ValueGenerated` asymmetry, and it is B4's shape again**: *"The property
      `WithNoBackingFields.FalseDefault` cannot be assigned a value generated by the database"*, out
      of EF's `InternalEntryBase.SetStoreGeneratedValue`. `HasDefaultValue` is a relational
      annotation, and what turns it into `ValueGenerated.OnAdd` is
      `RelationalValueGenerationConvention` — which the *server* runs and the *client* does not. So
      the server returns a generated value for a property the client's model says is not generated,
      and the client refuses it.

      Unlike B4 this one cannot be fixed by computing the wire form differently, because it is not
      a wire form — it is the model. **That is a design question, B6, not a bug to patch.**

      Two are a missing client-side value generator (`WrappedIntClassPrincipal.Id`,
      `EnumPrincipal.Id`: *"does not have a value set and no value generator is available"*), one is
      `This operation is not supported for a relative URI` on a wrapped-`Uri` key, and one is
      `Sequence contains no elements` in the composite-key-cycle propagation test.

- [x] **B3e. `Types.TypeTest` on Tier B.** **`Failed: 0, Passed: 16, Total: 16`** — clean, first
      run, no overrides. ✅ `<this commit>`

      Sixteen classes, one per CLR type EF's SQLite suite covers, over the core `TypeTestBase`:
      one entity, one property, one value, one query. It is a thin base and that is why it is worth
      having — **B4 was a family of per-type wire defects, and nothing in the suite asked the
      question one type at a time.** All sixteen pass, which is a real statement about the wire and
      not a formality.

      The core base rather than `RelationalTypeTestBase`, which is in
      `EFCore.Relational.Specification.Tests` — not referenced here (A79 mirrors that assembly's
      overrides by hand for the same reason) — and whose extra tests assert JSON columns and
      `ExecuteUpdate`.

      **One deliberate divergence: a store name per type.** EF names every one of these fixtures
      `TypeTest` and keeps them apart with `[Collection("Type tests")]`. The Tier B store is
      file-backed, and a shared file whose contents depend on class ordering is the exact coupling
      that produced the 698-test phantom failure.

- [x] **B3c. `JsonQuery` on Tier B.** **`Failed: 162, Passed: 230, Skipped: 7, Total: 399`**,
      first run, no overrides. ✅ `<this commit>`

      A deep owned graph — reference and collection, two levels down, with inheritance, custom
      naming, converters and a type-per-property entity — stored in JSON columns. EF ships
      `JsonQuerySqliteTest` and no InMemory counterpart, for the obvious reason: there is no JSON
      column in a store that keeps live objects.

      The core base, not `JsonQueryRelationalTestBase`, which asserts SQL. **The `ToJson()` mapping
      is mirrored from `JsonQueryRelationalFixture` and the collection-of-collections ignores from
      `JsonQuerySqliteFixture`** — a JSON column is what the base is named for, so adopting it
      without one would take the base's name and leave its subject behind.

      **230 passing is the finding; the 162 are a new surface, deliberately undiagnosed**, exactly
      as A79 left `ComplexTypeQuery`'s 62. They are four blocks: **72 `Values differ`**, **48
      `NullReferenceException`**, **36 `SQL APPLY`** (the A79 shape — EF's own SQLite suite
      overrides these, so they are convergence, and they go in with `PrimitiveCollectionsQuery`'s 13
      in one sweep) and **6** `a tracking query is attempting to project an owned entity without a
      corresponding owner`.

- [x] **B7. The `SQL APPLY` overrides, and the B3 batch measured whole.**
      **`Total tests: 21351, Passed: 20812, Failed: 331, Skipped: 208`** ✅ `<this commit>`

      **FIXED: 13, all of them `PrimitiveCollectionsQuery`'s `APPLY` failures. BROKEN: 140 — 128
      `JsonQuery`, 12 `StoreGenerated`, and nothing outside the classes B3 adopted.** That is the
      check that five new bases and a harness change disturbed nothing: 21351 − 20725 = 626 new
      tests = 211 + 16 + 399, and 331 − 204 = 127 = 140 − 13.

      Thirty overrides in two classes, all EF's own: 13 from `PrimitiveCollectionsQuerySqliteTest`
      and 17 from `JsonQuerySqliteTest`, each a query that now reaches SQL and asks SQLite for
      `APPLY`. `JsonQuery` **162 → 128**, `PrimitiveCollectionsQuery` **31 → 18**.

      **What was deliberately not taken matters as much as what was.** EF overrides 14 and 24; the
      counts here are 13 and 17. Only the intersection with what actually fails that way was
      adopted — A63 borrowed eight spatial overrides on the strength of EF having them and all
      eight failed with *"Exception type was not an exact match"*, hiding the real reason behind a
      borrowed one. One goes the other way and is left red:
      `Json_nested_collection_anonymous_projection_of_primitives_in_projection_NoTrackingWithIdentityResolution`
      raises `APPLY` here and EF does not override it, so there is no override to borrow.

      **Unadopted bases: 46 → 41.** Of those, **32 are `Query.Associations.*` (27) and
      `BulkUpdates.*` (5)**, which is a roadmap question; **8 are genuinely not adoptable** (six
      infrastructure, two spatial); and **exactly one adoptable base is left — `AdHocJsonQuery`,
      B3d.**

- [ ] **B3d. `AdHocJsonQuery` on Tier B.** The last adoptable base outside `Associations`/
      `BulkUpdates`, deferred rather than blocked, and the shape of the work is known.

      Seven `protected abstract Task Seed*` methods, whose SQLite implementations in
      `AdHocJsonQuerySqliteTest` are ~200 lines of `ExecuteSqlAsync` with JSON literals. They
      cannot be copied as-is: **`Database.ExecuteSqlAsync` is a relational API and the context the
      base hands the seed is the *client's*, which has no database.** Each must run against
      `((InfoCarrierTestStore)TestStore).Backend.CreateDbContext()` instead — the same rule A74/A75/
      A76 found, and the reason `MusicStore` and `GraphUpdates` reseed through the backend.

- [x] **B8. B1's premise, tested on `ComplexNavigations` — and the code reverted.**
      ✅ `<this commit>`

      B1 says the twelve failures classified as *"asserts a limitation this provider does not
      have"* have never been run on a tier that **has** the limitation, that the classification is
      therefore an assumption, and that moving those bases is the measurement that settles it.
      `ComplexNavigationsQuery` and `ComplexNavigationsCollectionsQuery` carry 20 of the 331
      between them, so they are where to test it. The fixture was switched to the SQLite backend,
      the seven InMemory `NonComposedGroupByNotSupported` overrides removed, and the pair run.

      **`Failed: 84, Passed: 844, Total: 928`, against 10 failures on Tier A — and not one of the
      ten moved.** The measurement settles the question it was asked, in the negative:

      | Test | Tier A | Tier B |
      |---|---|---|
      | `Complex_query_with_let_collection_SelectMany` | `Assert.Throws: No exception was thrown` | **the same** |
      | `Select_projecting_queryable_in_anonymous_projection_followed_by_Join` | same shape | **the same** |
      | `GroupJoin_on_a_subquery_containing_another_GroupJoin_projecting_outer_with_client_method` | same shape | **the same** |
      | `Queryable_in_subquery_works_when_final_projection_is_List` | `Assert.Contains: Sub-string not found` | **the same** |
      | `Join_with_result_selector_returning_queryable_throws_validation_error` | `ArgumentException` expected (EF InMemory's override) | `ApplyNotSupported` expected — **an override swap, not a defect** |

      **The A28 classification is confirmed, not refuted.** `No exception was thrown` on a tier that
      translates means the same thing it meant on the tier that does not: the query runs and returns
      the right answer, and the spec base asserts a limitation this provider genuinely does not
      have — on either backing store. There is nothing here to fix.

      **And the tier question is not settled by it.** Tier B costs 84 where Tier A costs 10. Of the
      84, **52 are `SQL APPLY`** — convergence, closable by mirroring
      `ComplexNavigationsQuerySqliteTest`'s and `ComplexNavigationsCollectionsQuerySqliteTest`'s
      overrides, about 64 methods across the two classes and their shared-type twins. That still
      leaves **~32 genuinely new reds** (14 `Type 'GroupBySingleQuery…'`, 8 untranslatable LINQ, and
      a tail). Those are real information — a translating store finds real defects — but **32 new
      reds per base, across the twenty-odd remaining query bases, is a scope decision and not a
      plan one.**

      Reverted, per the rule that a measured negative result is committed as a finding and dead
      neutral code is not. **B1 now needs an answer from the roadmap, not another experiment** — see
      the note under B1.

- [x] **B6. `ValueGenerated` is part of the shared model and the client does not compute it.**
      **`Total tests: 21351, Passed: 20818, Failed: 325, Skipped: 208`** — 331 → 325, **FIXED 6,
      BROKEN none**. ✅ `<this commit>`

      The client provider is not relational, so `RelationalValueGenerationConvention` never runs:
      `HasDefaultValue` and `HasComputedColumnSql` leave `ValueGenerated` at `Never` on the client
      while the server has `OnAdd`/`OnAddOrUpdate`. A property with no `ValueGenerated` gets no
      store-generated slot, and `SetStoreGeneratedValue` refuses the value the server returns:
      *"The property 'WithNoBackingFields.FalseDefault' cannot be assigned a value generated by the
      database."* Eight occurrences across six `StoreGenerated` tests.

      Three routes were on the table, and the one taken is the third — **the client accepts whatever
      the server actually generated rather than deciding in advance what it may generate**. Where
      there is no store-generated slot, `ApplyGeneratedValues` writes the current value instead.
      Nothing is lost: the sidecar exists so `AcceptAllChanges` can promote a generated value over
      an explicit one, and a property the client believes is never generated has no such conflict to
      resolve. The other two routes — teach the client to read the relational annotations, or accept
      the asymmetry and leave the tests red — both needed a decision about what the client's model is
      *for*; this one needed none, and changes no model.

      This is the type-mapping guardrail one level up. **Which properties may receive a generated
      value is computed twice**, by two providers, from a convention rather than from anything the
      model builder said — so the two answers differ, and the client's is the one that has no way to
      know better. The fix is the same in shape as B4's: stop asking the client's model a question
      only the server's can answer.

      Six of the eight, not eight. The two left are a *different* defect the exception had been
      hiding, now visible as `Assert.True() Failure` and `System.Exception : Bang!` — see B9.

- [x] **B9. "No value was set" does not survive the wire.**
      **`Total tests: 21351, Passed: 20820, Failed: 323, Skipped: 208`** — 325 → 323, **FIXED 2,
      BROKEN none**. ✅ `<this commit>`

      Uncovered by B6, which removed the exception that had been hiding it. EF distinguishes a
      property a user set to `0` from one nobody set at all, and it does *not* do so by the value:
      it compares against `IProperty.Sentinel` — the default of the CLR **member** EF reads the
      property through. That is why the spec's `WithNullableBackingFields` backs a `bool` property
      with a `bool?` field. The field's `null` is the sentinel, `HasExplicitValue` is false, and
      that is what leaves the column out of the `INSERT` so the store's default applies.

      The wire carries values, and that distinction is not one. `GetCurrentValue` reads the field
      into the property's CLR type, so an unset `bool?` arrives as `false`; the server materializes
      it into a real `false`, `HasExplicitValue` is now true, and the `INSERT` states the value the
      default was supposed to supply. `Nullable_fields_get_defaults_when_not_set` read the row back
      as `false`, and `Object_fields_get_defaults_when_not_set` — whose getter throws `Bang!` when
      the field is unset — proved it from the other side, on the client, by never being told
      anything.

      So the client names them, as it already names `ModifiedProperties` and `TemporaryProperties`
      for the same reason, and the server puts each one back to **its own** `property.Sentinel`
      through the backing field — the only member that can hold a `null` sentinel for a `bool`.

      **The interesting half is which properties it does *not* name**, and the first attempt got it
      wrong: naming every property with no explicit value broke
      `Properties_get_set_values_when_not_set_to_sentinel_values`. The sentinel is computed twice
      like everything else the two models each derive, and it *diverges* —
      `HasDefaultValue(true)` makes a `bool`'s sentinel `true` on the server and leaves it `false`
      here. `TrueDefault = false` reads as unset on the client and as deliberate on the server, and
      the server is right: it holds both the value and its own sentinel, and comparing them is
      exactly what EF does. The client therefore speaks only where the *value* cannot: when the
      current value is not equal to the sentinel it stands for, so the server has no way to
      reconstruct it. B4's rule, again — derive what you can from what both sides can see, and send
      only what neither side can compute alone.

- [x] **B10. A JSON-mapped owned collection never left the server.**
      **`Total tests: 21351, Passed: 20902, Failed: 241, Skipped: 208`** — 323 → 241, **FIXED 82,
      BROKEN none**. ✅ `<this commit>`

      B3c's 128 `JsonQuery` failures were 72 `Values differ` and 48 `NullReferenceException`, and
      the NRE said where to look: it was thrown from EF's own `AssertOwnedBranch`, dereferencing an
      `actual` that was null. `JsonOwnedRoot.OwnedCollectionBranch` arrived empty.

      A navigation travels only if EF says it is *loaded*, and for a tracked entity the change
      tracker answers. **"Loaded" is a flag something sets when it does the loading, and nothing
      loads a collection that is already inside its owner's row.** EF's JSON materializer builds
      `OwnedCollectionBranch` straight out of the document and never flags it, so a tracked entry
      reports `IsLoaded: false` for a collection it is holding two elements of. A probe on the
      server's mapper said so exactly: `OwnedCollectionBranch loaded=False clr=count 2`.

      Owned *references* were flagged and did travel — which is why every document arrived half
      built rather than empty, and why the answer is not to distrust the tracker in general. An
      owned dependent is the one thing whose loadedness is not a question: it came with the row or
      it does not exist. So an ownership navigation the tracker calls unloaded falls through to the
      same value test an untracked entity already used, and everything else is unchanged.

      82 of the 323, from four lines. `JsonQuery` 128 → 46, and the whole `NullReferenceException`
      block — 51 across the suite — is down to 3.

- [x] **B11. A no-tracking result carried no complex values.**
      **`Total tests: 21351, Passed: 20964, Failed: 179, Skipped: 208`** — 241 → 179, **FIXED 62,
      BROKEN none**. ✅ `<this commit>`

      A79's undiagnosed `ComplexTypeQuery` residual, all 62 of it, and one line. The failures read
      `Assert.Equal(expected is null, actual is null)` — Expected `False`, Actual `True` — from
      inside EF's `AssertAddress`: `Customer.ShippingAddress` was null on the client.

      A probe on the server said it was not null there (`CX Customer . ShippingAddress clr=set`),
      and a second probe on the client said `ApplyComplexValues` never ran. It is called from both
      branches of the *tracked* path and from neither of the untracked one:
      `MaterializeUntracked` handled scalars and navigations, and a complex value is neither. Every
      complex member of every no-tracking result was therefore dropped — and `ComplexTypeQuery`'s
      fixture is no-tracking throughout, which is why the whole class failed the same way and why
      `ComplexTypesTracking`, which tracks, has been at 249 of 251 for months.

      **The class is now 150 of 151** — the one skip is EF's. Nothing outside it moved, which is
      the expected shape: the tracked path already did this, so no tracked test could have been
      relying on the omission.

- [ ] **B12. A JSON-mapped owned collection is keyed differently on the two sides.** Diagnosed,
      **not fixed — it needs a decision about what the client's model is, and that is not mine to
      make.** It is the whole of `JsonQuery`'s remaining **38 `Values differ`**, and B10 is what
      made it visible: the collections could not be wrong before they travelled at all.

      The failure is `Assert.Equal(expected.OwnedCollectionBranch.Count, actual…Count)` — *Expected
      2, Actual 4*. Two owned roots, two branches each, and one root ends up holding all four. A
      probe on the client's tracking, printing each entry's key beside the key the wire carried:

          TRACK JsonOwnedRoot #6594565  key=[OwnerId:1,Id:0]  wirekey=[1,1]
          TRACK JsonOwnedRoot #66212867 key=[OwnerId:1,Id:0]  wirekey=[1,2]

      **The server keys an element by its ordinal in the JSON array; the client keys it by the CLR
      `Id` property, which the document does not contain and which is `0` for every element.** So
      every element of an owned collection has the same client-side key, and EF's own fixup — doing
      exactly what it should with the keys it was given — hands every branch to every root.

      Both sides run the same `OnModelCreating`, `ToJson()` included. What differs is which
      *conventions* act on it: replacing a JSON-mapped owned collection's key with a synthesized
      ordinal is relational, and this provider is not. **The same shape as B6**, and this time
      there is no route (c): the client cannot derive the ordinal, and writing the server's key
      value into the client's `Id` would corrupt a property a query can legitimately project.

      Two routes, and picking one is the same question B6 asked and answered without needing an
      answer:
      - Teach the client provider to build the key a JSON-mapped owned collection actually has.
        This is "the client honours relational annotations" again, narrowed to keys.
      - Accept that the two models key these types differently, and leave the 38 red — which also
        means owned JSON collections stay unusable under tracking.

- [x] **B13. The rest of the 179, classified.** No code change; read out of `artifacts/measure/b12`.
      ✅ `<this commit>`

      | # | Where | Reading |
      |---|---|---|
      | 38 | `JsonQuery` | **B12** — the owned-collection key. One defect, awaiting a decision. |
      | 8 | `JsonQuery` | *"a tracking query is attempting to project an owned entity without a corresponding owner"* — EF's own refusal, and 6 of the 8 are the three `Project_json_*_in_tracking_query_fails` tests, which **want** it. Check the exception type before treating these as failures. |
      | 26 | `JsonTypes` | **26 of the 28 are spatial** and need the SpatiaLite package (roadmap); the other 2 are A64's locale, not a failure of anything. Nothing here is ours. |
      | 26 | `MaterializationInterception` | Unchanged: 16 a real gap (the client builds rows from the wire, so `IMaterializationInterceptor` never runs — A68's query-side twin is the template), 10 blocked by A71. |
      | 6 | `PrimitiveCollectionsQuery` | `SQLite Error 1: 'no such column: p.Int'` — the server's SQL correlates a column that is not in scope. One cause, six tests, undiagnosed. |
      | 4 | `PrimitiveCollectionsQuery` | **`EF.Constant`.** The server's funcletizer evaluates the `EF.Constant(…)` call itself and the method body throws *"may only be used within Entity Framework LINQ queries"*. EF handles it in `ExpressionTreeFuncletizer.VisitMethodCall`, forcing the argument to parameterize; the likely difference is ADR-006's substitution of captured variables as **plain constants**, which changes the argument's evaluatability state before the server ever sees it. Worth a probe: it is the first place that substitution has been shown to matter. |
      | 6 | `PrimitiveCollectionsQuery` | Untranslatable LINQ and one `Values differ` — singletons. |
      | 8 | `ComplexNavigations` ×2 | A28 shape, closed by B8. |
      | 8 | `BadDataJsonDeserialization` | Spatial GeoJson. |
      | ~49 | the rest | The A54/A59/A61–A65 tables and the known singletons. |

- [ ] **B3d. `AdHocJsonQuery` is not the seven-seed job it was filed as.** Re-examined, not started.
      The plan assumed the work was `AdHocJsonQuerySqliteTest`'s ~200 lines of seed SQL rerouted
      through `((InfoCarrierTestStore)TestStore).Backend`, which is right as far as it goes. What it
      missed is that **the core base's `OnModelCreating*` methods do not map anything to JSON** —
      every `ToJson()` in this corpus lives in `AdHocJsonQueryRelationalTestBase`, which this
      project does not reference. Mirroring it by hand is ~630 lines of model configuration on top
      of the seeds, and without it the raw-SQL seeds do not match the schema they insert into.

      That is still only transcription, but the payoff is now gated: the corpus is owned JSON
      collections throughout, so most of what it would add lands on **B12**. Worth doing after B12
      is decided, and hard to justify before.

- [x] **B14. `EF.Constant` and its argument's shape, plus four SQLite overrides.**
      **`Total tests: 21351, Passed: 20972, Failed: 171, Skipped: 208`** — 179 → 171, **FIXED 8,
      BROKEN none**. ✅ `<this commit>`

      Two rules, each right on its own, meeting badly. §6 substitution spells a collection parameter
      out as a `NewArrayExpression` rather than one constant, because that is the shape
      `QueryRootProcessor` turns into an inline collection and a single `Constant` holding a
      `List<T>` translates as nothing. EF's funcletizer, meanwhile, insists on *parameterizing*
      `EF.Constant`'s argument — "even EF.Constant will be parameter here", so the query caches —
      **except** for a `NewArrayExpression`, which it deliberately refuses to parameterize so that
      `new[] { x, y }` can reach SQL as `IN (x, y)`. It bubbles the argument's evaluatable state up
      instead, and the caller then evaluates the `EF.Constant` call itself, whose body throws *"may
      only be used within Entity Framework LINQ queries"*.

      A probe printed the server's rebound tree and settled it in one line —
      `Constant(new [] {2, 999, 1000}).Contains(c.Id)`, with `isEF=True`: the method was found, the
      branch was entered, and the *argument's shape* is what defeated it. Inside a call on `EF` the
      collection now stays whole. Nothing downstream wants the spelled-out form there — `EF.Constant`
      exists to say "inline this", and EF does that itself once translation reaches it. **4 tests.**

      The other four are EF's own overrides adopted as convergence. Three are
      `Assert.ThrowsAsync<SqliteException>` for indexing an inline collection by a column, which puts
      that column in a correlated subquery's `OFFSET` and SQLite refuses; one is
      `Assert.ThrowsAsync<EqualException>` for EF issue #32561. Each matches ours exactly in type and
      origin. **EF overrides two more `Parameter_collection_index_Column_*` by calling `base` — they
      pass there and fail here**, because a real parameter reaches SQL as a JSON string indexed with
      `->>` while our substitution makes it a subquery. Those stay red: the reason is ours, not
      SQLite's, and it is the same §6 trade the first half of this step navigated. **The remaining 6
      in this class are worth reading together** — five of them are the substitution's shape, and the
      real fix is to stop substituting and ship the parameter *values* instead, which is a change to
      the wire, not to a visitor.

- [x] **B15. The client's materializer now names the tracking behaviour it is materializing for.**
      **`Total tests: 21351, Passed: 20972, Failed: 171, Skipped: 208`** — 171 → 171, **FIXED none,
      BROKEN none, reasons byte-identical.** ✅ `<this commit>`

      Kept anyway, on B5's precedent and with better evidence than B5 had. `MaterializerFor` compiled
      a behaviour-specific materializer only for
      `NoTrackingWithIdentityResolution` and used EF's cached one everywhere else, "so nothing else
      pays for it". Both halves of that were wrong: the compiled delegate is cached here too, so
      nothing pays anything, and the cached materializer bakes in
      `QueryTrackingBehavior = null` — *"not from a query"* — which is also what
      `MaterializationInterceptionData.QueryTrackingBehavior` reports to a user interceptor. Every
      row this class builds came from a query, so the behaviour is never genuinely unknown.

      **It was observed, not assumed.** Twelve `Assert.Equal() Failure: Expected TrackAll, Actual
      null` appeared the moment a taller failure in front of them was removed, out of EF's own
      `ValidatingMaterializationInterceptor` — the value reaching user code, read back. They are red
      again now for the reason below, which is why this step measures neutral.

- [ ] **B16. A user's `IMaterializationInterceptor` sees the server's context too, and both fixtures
      that care want opposite things.** Diagnosed, **not fixed — it is a decision.** Worth 12 in
      `MaterializationInterception`; 4 more there are the same question about
      `IInstantiationBindingInterceptor`, and the remaining 10 are A71.

      `MaterializationInterceptionTestBase`'s interceptor asserts that the context it is handed is
      the one it was registered on. It is handed the **server's**, because the server materializes:
      first from `ServerSaveChangesExecutor.Materialize`, and — once that is fixed — from EF's own
      shaper inside `InMemoryShapedQueryCompilingExpressionVisitor.QueryingEnumerable`. Twelve
      `Assert.Same` failures, both sides printing the same type name.

      Three things were tried and all three are recorded here rather than in the code:

      - **Filtering every `ISingletonInterceptor` out of the server's services.** Fixes the twelve,
        and costs **1629**: `AddEntityFrameworkProxies` registers `ProxyBindingInterceptor` as one,
        and without it the proxy *conventions* still run, so the server's model binds a `LazyLoader`
        member the plain CLR type does not have.
      - **Filtering only `IMaterializationInterceptor`.** Fixes the twelve and costs **246**:
        `PropertyValuesFixtureBase` uses a materialization interceptor to set `CreatedCalled` and
        friends, and its seed — which runs on the server, as every seed here does — asserts them.
        *"The given key 'CreatedCalled' was not present in the dictionary."* That fixture wants the
        interceptor server-side as much as the other wants it client-only.
      - **Suppressing interception in `ServerSaveChangesExecutor.Materialize` alone**, by building
        EF's materializer source with the materialization interceptors removed and the binding ones
        kept. Defensible on its own — reconstructing the client's entity is not a materialization,
        and EF raises no event for `new Blog { … }` + `Attach` — but it **pays nothing**, because
        the server's *query* path materializes through EF's compiled shaper, which has no such lever.
        Reverted.

      So the question is not "where should this provider raise the event" but **whose hook is it**:
      the client's, the server's, or both. Every route above answers it, and two of the three
      answers are contradicted by an existing fixture.

      **Answered 2026-08-09, and the answer dissolves the question: both, and the product already
      does that.** The premise above — that a side has to be chosen — is wrong. A real deployment
      must be free to define materialization hooks on the client, on the server, or on both, and
      each of the three routes recorded above suppresses one side, so **none of them may be taken.**

      The premise came from reading a two-instance system through a one-instance test base.
      `MaterializationInterceptionTestBase` asserts `Assert.Same(context, …)` because in EF there
      *is* a context; under this provider there are two, and both materialize. Nothing in `src/`
      forwards an interceptor from one to the other — the product assembly contains no interceptor
      plumbing at all. **The server sees the user's interceptor only because
      `InfoCarrierBackendTestStore.AddProviderOptions` forwards the client's `onConfiguring`**, which
      A49 does so that the two models match; the interceptor rides along as collateral. One object,
      two instances.

      Three independent confirmations that each side is individually correct, all already in hand:

      | Evidence | What it proves |
      |---|---|
      | `"Intercepted: Intercepted: New name"` (B23) | **two** invocations — a mutating interceptor registered on both sides applies twice, which is what that registration asks for |
      | `Assert.Same` ×12 | one invocation carried a context that is not the one the test registered on — i.e. the other instance's |
      | B15's twelve `QueryTrackingBehavior` failures, which appeared and were fixed on the **client's** materializer | the client raises the event properly, with its own context |

      **So this is the A28 family, one level up.** A28 is a spec test asserting a *materialization*
      limitation this provider does not have; these assert a *topology* — one EF instance — that this
      provider does not have either. Left red and classified, which is what the guardrail prescribes:
      27 tests (12 `Assert.Same`, 4 the same for `IInstantiationBindingInterceptor`, 10 A71's
      wiring, 1 `OptimisticConcurrency.Nullable_client_side_concurrency_token_can_be_used`).

      **What remains is a harness question, not a product one, and it is optional.** The 27 would go
      green if the harness stopped forwarding the *interceptors* while still forwarding everything
      else `onConfiguring` carries — then the client's context is the only one registered on and
      `Assert.Same` holds. Two obstacles, both real: A71's wall means they cannot be *subtracted*
      (`CoreOptionsExtension.WithInterceptors` concatenates, `Clone` is protected), so the server's
      `CoreOptionsExtension` would have to be **replaced wholesale** with one rebuilt by hand from
      its public `With*` setters; and `PropertyValuesFixtureBase`'s seed genuinely wants an
      interceptor server-side, which under "both sides are allowed" is answered by registering it on
      the backend *additively* rather than by forwarding. Neither is required for correctness.

- [x] **B17. Three overrides EF leaves to the provider, and the keyless type A1 left open.**
      **`Total tests: 21351, Passed: 20981, Failed: 162, Skipped: 208`** — 171 → 162, **FIXED 9,
      BROKEN none**. ✅ `<this commit>`

      Three separate things, none of them a provider change.

      **`JsonQuery`'s three `Project_json_*_in_tracking_query_fails`** are tests the core base
      deliberately hands to the provider — it says so on the test itself, *"verify exception on the
      provider level, relational and core throw different exceptions"* — and then projects an owned
      entity out of a tracking query without its owner. `JsonQueryRelationalTestBase` asserts
      `CoreStrings.OwnedEntitiesCannotBeTrackedWithoutTheirOwner`; this provider raises exactly that,
      from the same place, because the query it raises on is the one the server ran. Mirrored, as
      that assembly always must be. **Not** taken for the fourth test that fails the same way:
      `OwnsMany_correlated_projection` raises it here and passes for EF, so it is ours and stays red.

      **`Updates`' two concurrency-token messages** were asserted against
      `InMemoryStrings.UpdateConcurrencyTokenException` — EF's *insensitive* variant — while the
      backend runs with `EnableSensitiveDataLogging`, which
      `InfoCarrierBackendTestStore.AddProviderOptions` sets deliberately so that a server-side
      exception reads the way the fixture asked for. The store composes the message, so the store's
      options decide which of the two it is; EF ships both variants in two classes for exactly this
      reason. *"entity type 'Product' **with the key value**…"* against *"entity type 'Product' **on
      the concurrency token**…"* is where they first diverge, at character 62. `Updates` is now
      **28 of 28**.

      **`WithConstructors.Query_with_keyless_type`** is the last of A1's nineteen, and A1 named the
      fix without applying it: the base maps `BlogQuery` as keyless and stops, because where its rows
      come from is the store's business. A defining query cannot live in the client's model — there
      is no store to run it against — so it goes on the server's copy through `serverContextType`,
      as Northwind and `Inheritance` already do for theirs. **`WithConstructors` is 41 of 41**, and
      A1's batch is now entirely clear except for the classifications it got wrong at the time.

- [x] **B18. A rewritten projection's lambda is typed by the operator, not by its body.**
      **`Total tests: 21351, Passed: 20983, Failed: 160, Skipped: 208`** — 162 → 160, **FIXED 2,
      BROKEN none**. ✅ `<this commit>`

      `ProjectionRewriter` rebuilt the client-side half of a split projection with
      `Expression.Lambda(clientBody, row)`, letting the delegate type be inferred from the body. The
      two can legitimately differ: C# lets `Select(x => new { … })` instantiated as
      `Select<T, object>` carry a body whose own type is the anonymous one, and
      `LambdaExpression.ReturnType` is what the *operator* was instantiated with, not what the body
      evaluates to. Inferring produced `Func<row, <>f__AnonymousType>` where `Select<row, object>`
      wanted `Func<row, object>`, and `Expression.Call` rejected it before the query reached the wire
      at all. Built with the explicit delegate type now — which is how the tree arrived.

- [x] **B19. The allowlist knew only the types the model *registered*, not the types it *names*.**
      **`Total tests: 21351, Passed: 20986, Failed: 157, Skipped: 208`** — 160 → 157, **FIXED 3,
      BROKEN none**. ✅ `<this commit>`

      B19's diagnosis stood: for

          Parents.Include(p => p.ChildCollection).ThenInclude(c => c.SelfReferenceCollection)

      the server receives `[EntityQueryRootExpression].Include("ChildCollection")` — the **string**
      overload with one segment, which is not the user's `Include` at all but the one
      `AugmentWithNavigations` synthesizes for a navigation the *residual* reads. The chain was
      judged unshippable, went client-side, and only its first segment came back.

      A probe on the two candidate rules named the second one in one line each. It is
      `TypeAllowlist`, twice, for the same reason:

          DENY …Context3409+IChild on Parameter :: c
          DENY ICollection`1[…Context3409+IChild] on MemberAccess :: p.ChildCollection
          DENY …Context17794+IOffer on Convert :: Convert(v, IOffer)

      **`ForModel` admitted the types the model registered and the types it declares members
      *through*, but not the types it declares members *as*.** Two edits, each a sentence:

      - **A navigation's CLR type.** `HasMany(p => (ICollection<Child>)p.ChildCollection)` maps a
        navigation declared as `ICollection<IChild>`, so every node of the resulting chain is
        spelled with the interface. Admitted now, with its generic arguments.
      - **An entity CLR type's supertypes.** `HasAction17794<T>() where T : IOffer` builds its
        predicate against the interface, so the captured tree reads `Convert(v, IOffer).OfferActions`
        — a cast target that is not an entity CLR type. `InvalidIncludeFinder.IsEntity` asks exactly
        this with `type.IsAssignableFrom(e.ClrType)`; this is that answer, precomputed. Neither
        widens anything: both are reachable only through an instance the model itself produced.

      **The third test is the one the first two uncovered**, and it is why this measures 3 rather
      than 2. `Customer_collections_materialize_properly` had been *passing by refusal*: a projection
      of `MyGenericCollection<Order>` was denied, ran on the client, and got the right answer. Once
      it ships, the wire says `MyGenericCollection<Order>` — EF's `CollectionTypeFactory`
      instantiates a concrete collection with a public parameterless constructor verbatim — and
      nothing in `DynamicValueMapper` could rebuild one: no single-argument constructor, no set
      interface, no static factory. The `List<T>` fell through and the cast failed one frame later.
      `AddToNew` mirrors EF's own first rule — construct it empty, `ICollection<T>.Add` each element
      — and refuses the same types EF refuses, so the two sides still say the same thing about
      `MyInvalidCollection(int)`. That also closed `Collection_without_setter_materialized_correctly`,
      which had been a §3.6 refusal and became this the moment the navigation could travel.

      **An include chain silently degrading to its first segment is a wrong answer, not an error**,
      and nothing else in the suite would have caught it.

- [x] **B20. Five of the ten primitive-collection failures were stated on the relational base.**
      **`Failed: 5, Passed: 158, Skipped: 2, Total: 165`** across the two classes — 10 → 5,
      **FIXED 5, BROKEN none**. Test-side only, so the targeted run is the honest number.
      ✅ `<this commit>`

      CLAUDE.md's rule, collected: *"**Grep `EFCore.Relational.Specification.Tests` too** — a limit
      every relational provider has is overridden on the relational base, not in SQLite's own
      suite."* Both classes derive from the **core** base — the relational one asserts SQL a client
      with no database does not have — so five overrides that were sitting one file away had every
      one of their tests classified as a failure of this provider.

      | Test | EF states | Ours |
      |---|---|---|
      | `Inline_collection_Count_with_zero_values` | `RelationalStrings.EmptyCollectionNotSupportedAsInlineQueryRoot` | the same message, from `QuerySqlGenerator.GenerateValues` |
      | `Project_inline_collection_with_Concat` | `RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin` | the same message, from `SelectExpression.ApplyProjection` |
      | `Column_collection_Where_equality_inline_collection` | `AssertTranslationFailed` (EF's TODO: comparing a relational rowset to a primitive collection, #33792) | translation failed |
      | `Column_collection_Concat_parameter_collection_equality_inline_collection` | `AssertTranslationFailed` | translation failed |
      | `NonShared…Array_of_byte` | `AssertTranslationFailed` — *"byte[] gets mapped to a special binary data type, which isn't queryable as a regular primitive collection"* | translation failed |

      A63's rule held throughout — the reason has to match, not the name. Three of the relational
      base's overrides were **not** taken, because this provider does not have the limitation they
      assert: `Column_collection_equality_inline_collection_with_parameters`,
      `Parameter_collection_in_subquery_and_Convert_as_compiled_query` and
      `Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query` all
      *pass* here, and §6's substitution is why — the inline form EF cannot type-map is the one this
      provider ships.

      **Five left, and they are two things.** Four are §6's trade, priced in B21 below. The fifth is
      `Array_of_TimeOnly`, and it is **not ours**: EF's own SQLite suite carries
      `[ConditionalFact(Skip = "Issue #30730: TODO: SQLite is not matching elements here.")]` on it,
      and our failure is `Sequence contains no elements` — the same non-match, one layer up. It is
      left red rather than skipped, because CLAUDE.md forbids introducing a `[Skip]` and the
      classification carries the same information. Note the shape: `_with_milliseconds` and
      `_with_microseconds` pass and only the whole-second value fails, which is #30730 exactly.

- [x] **B21. A carrier row is not the query the caller wrote.**
      **`Total tests: 21351, Passed: 20993, Failed: 150, Skipped: 208`** — 152 → 150, **FIXED 2,
      BROKEN none**. ✅ `<this commit>`

      `OwnsMany_correlated_projection`, which B17 left red for the right reason — EF overrides
      nothing here, so the failure was ours. Two defects in a row, and the first attempt at each
      was too broad by exactly the same amount.

      **The projection.** `Contacts.Select(c => new ContactDto { Id = c.Id, Names = c.Names.Select(n => new NameDto()).ToArray() })`
      splits, because `ContactDto` is a client type. A probe on the server's rebound tree:

          TREE track=TrackAll :: [EntityQueryRootExpression].Select(contact => new ValueTuple`2(
              Item1 = contact.Id, Item2 = contact.Names))

      **Defect one: the server refused to track it.** `Name` is owned by `Contact`, the carrier
      holds `contact.Names` beside a scalar, and EF's
      `OwnedEntitiesCannotBeTrackedWithoutTheirOwner` fires before a row is read. EF is right —
      an owned dependent has no identity apart from its owner — and **a user query that asks for
      this must still be refused**: B17 adopted three `Project_json_*_in_tracking_query_fails`
      that assert exactly it. But a `TupleCarrier` row is not a user query; it is what
      `ProjectionRewriter` generated, for a user query EF itself translates. The server's change
      tracker is a serialization scratchpad — the client rebuilds every row from the wire and
      tracks what the *residual* yields — so for a carrier the tracking is dropped and
      `NoTrackingWithIdentityResolution` kept, which is A55's back-reference behaviour exactly and
      the entries EF will not make, nothing else.

      Stated without the carrier condition it cost **4**: the three `Project_json_*` overrides and
      one more, each a spec test *wanting* the refusal.

      **Defect two: the slot's declared type could not be built.** `Contact.Names` is
      `IReadOnlyList<Name>` — the spec maps it with a `protected` setter — and InMemory's
      `MaterializeCollection<TElement, TCollection>` constrains
      `TCollection : class, ICollection<TElement>`, so `MakeGenericMethod` throws
      `VerificationException` before a row is read. EF never meets this in a user query, because a
      projection returning a collection ends in `ToList`/`ToArray` — its documented requirement.
      **This rewrite is what put the bare navigation in a slot, so this rewrite owes the
      materialization**, and a `List<T>` satisfies every collection interface the client body could
      have been written against.

      Stated over any sequence type it cost **23 more**, and the two shapes say why: an
      `IGrouping<K, T>` is an `IEnumerable<T>` that is not an `ICollection<T>`, and `ToList`-ing one
      throws its key away (20 of the 23 were `GroupBy`); and `b.Posts1.OrderBy(p => p.Id)` in a
      final projection is a refusal `Collection_without_setter_materialized_correctly` asserts.
      Both are *composed* sequences. The rule is a **member read** and nothing else — the same
      distinction the tracking half needed, one layer down.

- [ ] **B22. §6 substitution priced: it does not need a wire change, and that is the finding.**
      Priced, **not started** — it reverses a stated decision (research-findings §6), so it needs
      an answer rather than a patch. No code change; this entry is the price.

      **What is left after B20.** Four in `PrimitiveCollectionsQuery`, all one trade:

      | Test | Why |
      |---|---|
      | `Parameter_collection_index_Column_equal_Column` | EF calls `base` — it passes there |
      | `Parameter_collection_index_Column_equal_constant` | EF calls `base` — it passes there |
      | `Parameter_collection_with_type_inference_for_JsonScalarExpression` | EF calls `base` |
      | `Parameter_collection_null_Contains` | EF calls `base`; ours reads `null.Contains(p.Int)` |

      A real parameter reaches SQLite as a JSON string indexed with `->>`. §6 substitution spells
      the collection out as a `NewArrayExpression`, which becomes an inline collection and therefore
      a correlated subquery, and the column is out of scope in its `OFFSET`. The last one is the
      same trade with a different face: a `null` collection parameter substitutes as a literal
      `null` constant, and `null.Contains(x)` is not a thing to translate.

      **The queue priced this as a wire change — "send `QueryContext.Parameters` alongside the tree
      and have the server rebuild `QueryParameterExpression`s against its own `QueryContext`". That
      route does not exist, and a cheaper one does.** The server executes through EF's own
      `EntityQueryProvider.Execute`, which builds the `QueryContext` itself from the tree it is
      given; nothing public injects values into it. But `ExpressionTreeFuncletizer.VisitMember` says
      in its own comment: *"any evaluatable `MemberExpression` is treated as a captured variable"* —
      `State.CreateEvaluatable(typeof(MemberExpression), containsCapturedVariable: true)` — and
      `ProcessEvaluatableRoot` parameterizes anything with a captured variable in it. **So the
      client does not have to send parameters beside the tree. It has to send them in a shape the
      server's own funcletizer will lift back out**: `Field(Constant(box), "Value")`, which is
      precisely what a captured variable looks like to EF.

      **The change, then:**

      | Where | What | Size |
      |---|---|---|
      | a public `ParameterBox<T>` holder | one field, one ctor | ~15 lines |
      | `TypeAllowlist.BuiltInGenericDefinitions` | add `typeof(ParameterBox<>)` | 1 line |
      | `QueryExecutor.SubstituteParametersExpressionVisitor.Substitute` | box instead of `Constant`/`NewArrayInit` | ~10 lines |
      | the serializer | **believed** unchanged — a member read over a constant is an existing node pair, and `RehydrateObject` rebuilds a public-property holder. Not verified. | 0, or a day if wrong |

      **The cost is not the code. It is that §6 chose the opposite on purpose.** research-findings
      §6 says plain constants, *"never wrapped in custom generic structs (the v1 `ValueWrapper<T>`
      trap)"* — and a `ParameterBox<T>` is exactly a custom generic wrapper. Reversing that needs
      the v1 trap re-read to establish which failure it actually was, and a dated supersession, not
      a code change that quietly contradicts it.

      **What else moves, both directions:**

      - **Three tests would newly fail, and each one has EF's override waiting.** B20 deliberately
        did *not* adopt `Column_collection_equality_inline_collection_with_parameters`,
        `Parameter_collection_in_subquery_and_Convert_as_compiled_query` and
        `Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query`,
        because this provider *passes* them and EF does not — and §6's substitution is exactly why.
        Ship real parameters and they fail as EF's do, so they become three overrides to adopt
        rather than three regressions. **Net 4, not 7.**
      - **Tier A should not move at all.** §6 already records why: *"EF's InMemory provider
        client-evaluates the `Contains` either way."*
      - **The server's compiled-query cache is a side benefit.** Today every distinct parameter
        value ships as a distinct constant, so the server's plan cache misses on every call. A
        funcletized parameter restores the cache key EF designed.

- [x] **B23. The singleton tail, read one by one — and one attempt measured and reverted.**
      No code change. Read out of `artifacts/measure/b21b`. ✅ `<this commit>`

      **The reverted attempt first, because it is the useful half.**
      `Comparison_with_value_converted_subclass` — `Where(f => f.ServerAddress == IPAddress.Loopback)`,
      expecting 1 row and getting 0 — is **three defects stacked**, and the third has no cheap fix.

      1. A probe on the boundary analyzer named the first in one line:
         `DENY System.Net.IPAddress+ReadOnlyIPAddress on Constant :: 127.0.0.1`. **`IPAddress.Loopback`
         is not an `IPAddress`** — it is a private nested subclass, and EF's funcletizer types the
         constant by the value it holds. So the constant named a type no allowlist can admit and no
         transport could resolve, the whole `Where` went client-side, and the answer was silently
         wrong rather than an error.
      2. Widening a non-visible type to its nearest visible base makes it ship. **But the base must
         not be `object`**: an anonymous type is also invisible, and widening one to `object` cost
         **92 in `GearsOfWarQuery` alone** — it is not a private implementation of anything, it *is*
         the projection. `!type.IsVisible && BaseType != typeof(object)` is the exact rule.
      3. Then the value has nowhere to go. `IPAddress` is not a wire primitive; EF's core
         `JsonValueReaderWriterSource` — the CLR-type-only service `CollectionForm` already uses —
         **does not know it**; and the mapper's reflective walk is what is left, where `ScopeId`
         throws `SocketException` for an IPv4 address, exactly the signature `PrimitiveCoercion`
         was written for. `PrimitiveCoercion.JsonForm` handles this **for a value reached through
         its `IProperty`**, which is why a *result row* carrying `Faction.ServerAddress` is fine. A
         constant in a query tree has no property to be read through.

      Routing it through EF's core `ValueConverterSelector` instead — which *does* know `IPAddress`,
      from the CLR type alone, through a service no provider replaces — **fixes the test and costs
      381**: `Total 21351, Failed 529` against 150, FIXED 2, BROKEN 381. The reason is where it had
      to be applied: `PrimitiveCoercion.Coerce` is on every scalar path in the provider, so a
      converter branch at its head hijacks shadow properties, keys and enums —
      *"'Order.ClientId' is a shadow property"*, *"Object must implement IConvertible"*. Reverted
      whole; its run is kept as `artifacts/measure/b23-reverted.*` so the label cannot be mistaken
      for the current state. **A fix would have to reach only the constant path, and that is a design question
      about where the wire's scalar form is decided, not a patch.**

      **The rest of the tail, classified.** Four are the A28 shape — a spec test asserting a
      limitation this provider does not have — and they read identically,
      `Assert.Throws() Failure: No exception was thrown`:
      `ProxyGraphUpdates.Save_two_entity_cycle_with_lazy_loading` (×2),
      `NorthwindNavigations.Join_with_nav_projected_in_subquery_when_client_eval` (×2),
      `CustomConverters.Composition_over_collection_of_complex_mapped_as_scalar`.
      `AdHocAdvancedMappings.Casts_are_removed_from_expression_tree_when_redundant` is the same
      family with the exception present but different: `Cast<IDummyEntity>()` names an interface no
      entity implements, so it stays client-side and `Enumerable.Cast` throws `InvalidCastException`
      where EF's translation would have thrown `InvalidOperationException`.

      **One is not a singleton at all: it is B16's thirteenth test, with a second symptom.**
      `OptimisticConcurrency.Nullable_client_side_concurrency_token_can_be_used` reads
      *Expected "Intercepted: New name", Actual "Intercepted: **Intercepted:** New name"*.
      `F1MaterializationInterceptor` is a user `IMaterializationInterceptor` that prepends a prefix,
      and it runs **twice** — once on the server materializing the row, once on the client
      materializing the wire row. Every other B16 failure is an `Assert.Same` on the wrong context;
      this one is the same defect doing visible damage to a value. It belongs in B16's count.

      **Still undiagnosed, one:** `StoreGenerated.Store_generated_values_are_propagated_with_composite_key_cycles`
      — `SaveChanges` succeeds and the read-back `SingleAsync(e => e.PrincipalId == id)` finds
      nothing, so the dependent's FK does not carry the value the store generated for its principal.
      B6's shape one level on, in a composite-key cycle. The other three `StoreGenerated` are B6
      route (a) and fail at `context.Add`, before the wire.

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

- [x] **A17.** Fourth batch: four query bases, 257 tests. **`Total tests: 13576, Passed: 13497,
      Failed: 38, Skipped: 41`** — 3 new failures, **none of them a regression**.
      ✅ `<this commit>`

      | Base | Tests | Failing |
      |---|---|---|
      | `Query.InheritanceQueryTestBase` | 97 | **0** |
      | `Query.FiltersInheritanceQueryTestBase` | 22 | **0** |
      | `Query.Ef6GroupByTestBase` | 110 | 2 |
      | `Query.QueryFilterFuncletizationTestBase` | 28 | 1 |

      **`QueryFilterFuncletization` came up 18 of 28 red and is now 1**, and the diagnosis is the
      general statement of something this repo already had one instance of. A query filter is part
      of the **model**, so *both* sides build it and both sides apply it: the client funcletizes
      its own value into the shipped tree, and the server then applies its own filter again with
      whatever `Field`, `Property` or `Tenant` its instance happens to hold. Every test in that
      base mutates exactly those members between two queries, so the server kept filtering on the
      initial value and the second query answered like the first. `CopyDbContextParameters` —
      which existed for Northwind's `TenantPrefix` — is the mechanism, and this is the general
      case of it. Worth stating as a rule: **context state a query filter closes over is part of
      the request, not part of the client.**

      `Ef6GroupBy` at 108 of 110 is the useful negative result of the batch: GroupBy is named in
      the query residual as one of its three causes, and a second, independent corpus over a
      different model says that residual is **Northwind-specific**, not a GroupBy gap.

      `Can_query_all_animal_views` takes EF's own `InheritanceQueryInMemoryTest` override: the
      keyless view's defining query calls a CLR method no provider can translate, the query
      reaches the server whole, and the server's InMemory provider refuses it in the same words.
      Convergence with the reference provider, adopted as such.

      The 3 left:

      | # | Test | Reading |
      |---|---|---|
      | 2 | `Ef6GroupBy.Whats_new_2021_sample_7` | `NavigationBaseIncludeIgnored` raised as an error from EF's own `NavigationExpandingExpressionVisitor` — an `Include` walking back up the tree. Unclassified; it is EF's expansion, on the server. |
      | 1 | `QueryFilterFuncletization.Local_variable_from_OnModelCreating_can_throw_exception` | The message differs by two words: "evaluate the LINQ query parameter" vs "evaluate a LINQ query parameter". The exception is raised where it should be; only its wording is EF's rather than ours. |

- [x] **A18.** Fifth batch: three bases, 168 tests — and two provider defects each worth more
      than the batch. **`Total tests: 13744, Passed: 13646, Failed: 50, Skipped: 48`** — 12 new
      failures, **none of them a regression**. ✅ `<this commit>`

      | Base | Tests | On adoption | After the two fixes |
      |---|---|---|---|
      | `Query.InheritanceRelationshipsQueryTestBase` | 94 | 90 | **2** |
      | `KeysWithConvertersTestBase` | 47 (7 skipped) | 40 | **7** |
      | `ValueConvertersEndToEndTestBase` | 27 | 3 | 3 |

      **A shadow key property cannot be read either — the third site.** 88 of
      `InheritanceRelationships`'s 90 were one exception: "No backing field could be found for
      property `BaseInheritanceRelationshipEntity.OwnedReferenceOnBase#OwnedEntity.…Id`", from
      `ServerQueryExecutor.HasKey`. That is the same defect L18 fixed in `MapRowMembers` and A3
      fixed in `IsLoaded`, at a third call site, reached first by an **owned** entity type — whose
      key *is* its owner's foreign key and has no CLR member at all. `HasKey` exists to tell a row
      that came from the store from a constructor-set placeholder, and only a CLR-visible key can
      distinguish those, so a shadow key property is skipped rather than read.

      **A converted key travels as its provider value.** All 40 of `KeysWithConverters` failed
      before any assertion: `EntityKeyNode.KeyValues` is declared `object`, the source-generated
      serializer resolves `JsonTypeInfo` by runtime type, and a key like `ComparableBytesStructKey`
      has none — "JsonTypeInfo metadata for type … was not provided". The converter's provider
      value is what the store keys on, is by construction one of the registered primitives, and
      both sides compute it from the model, so that is what `KeyValues` now carries
      (`PrimitiveCoercion.ToWireKey`/`FromWireKey`). `byte[]` joins the serializer context for the
      binary keys, and `Coerce` learned to read one back from base64.

      The 12 left, all in the new classes:

      | # | Test | Reading |
      |---|---|---|
      | 7 | `KeysWithConverters.Can_query_and_update_owned_entity_with_*` | `NullReferenceException` in the test body — an owned entity behind a converted key comes back unpopulated. |
      | 3 | `ValueConvertersEndToEnd.Can_insert_and_read_back_with_conversions` | `IPAddress.get_ScopeId` throws `SocketException` for an IPv4 address, from the mapper's reflective member walk. The general form of the key fix would avoid it: a property with a converter should travel as its **provider** value, not as an object shape. |
      | 2 | `InheritanceRelationships.Nested_include_collection_reference_on_non_entity_base` | `ArgumentException: Expression of type 'IQueryable<…>'` — a residual shape, so it belongs with the query residual. |

- [x] **A19.** A converted *property* travels as its provider value too.
      **`Total tests: 13744, Passed: 13649, Failed: 47, Skipped: 48`** — FIXED 3, BROKEN none.
      **`ValueConvertersEndToEndTestBase` is 27 of 27.** ✅ `<this commit>`

      A18 did this for keys. The general rule is the same and the second failure mode is
      different: an ordinary converted property falls through to the mapper's **reflective member
      walk**, which reads every public getter — and `ValueConvertersEndToEndTestBase` stores an
      `IPAddress`, whose `ScopeId` throws `SocketException` for an IPv4 address.

      **The first attempt was measured byte-identical and that was information, not noise.** It
      changed only `MapRowMembers` — the query-result path — and a probe on `ToWireValue` showed
      it **never ran** for the failing test. The failure is on the **SaveChanges** path:
      `ChangeEntryMapper.ToChangeEntry` sends `entry.GetCurrentValue(property)` as the CLR value,
      and the entity never survives to be queried back. Without the probe the honest reading would
      have been "converters make no difference", which is the mistake CLAUDE.md warns about.

      So the rule now holds on every edge that carries a mapped value: `MapRowMembers` and
      `ClientResultMaterializer.ReadPrimitives` for a result row, `ChangeEntryMapper` and
      `ServerSaveChangesExecutor` for a change entry, and the store-generated values coming back
      through `InfoCarrierDatabase`. It is what ADR-008 constraint 1 means by reading a scalar
      through its `IProperty`: honouring the converter, not merely going through the accessor.

- [x] **A20.** A constant is mapped by what it *is*, not by what the expression declares.
      **`Total tests: 13744, Passed: 13656, Failed: 40, Skipped: 48`** — FIXED 7, BROKEN none.
      **`KeysWithConvertersTestBase` is 40 of 40 (7 skipped, EF's own).** ✅ `<this commit>`

      The seven `Can_query_and_update_owned_entity_with_*` failures were not about owned entities
      at all. Each ends with `FindAsync(new IntStructKey(1))`, and `EntityFinder` builds
      `Equals(EF.Property(e, "Id"), <constant declared object>)`. `VisitConstant` mapped that
      constant with `node.Type` — `object` — so the mapper walked `object`'s members, of which
      there are none, and the server rebuilt a bare `new object()`. The predicate matched nothing,
      `Find` returned null, and the test dereferenced it.

      Four probes to get there, and the useful one was the last: the *first* round trip's rows,
      navigations and change entries were all correct, so the interesting question was what the
      **third** query actually was. Printing the rebound tree on the server gave it in one line —
      `FirstOrDefault(e => Equals(Property(e, "Id"), value(System.Object)))`, and
      `ConstantExpression.ToString` renders `value(T)` only when the value's `ToString()` *is* its
      type name, i.e. when it is a plain `object`.

      Only the **value node** widens to the runtime type; `ConstantNode.Type` still carries the
      declared type, so the rebuilt `Expression.Constant` keeps the type its parent expects. The
      deliberate truncation one level down — a *member* declared `object` holding a `DbContext`,
      which `MapToNode`'s member walk must keep truncating or it stack-overflows — is untouched.

- [x] **A21.** `Left_join_with_skip_navigation` (8) — **attempted, measured neutral, reverted.**
      Suite unchanged at **`Total tests: 13744, Passed: 13656, Failed: 40, Skipped: 48`** — FIXED
      none, BROKEN none, **reasons unchanged**. Not committed as code; this is the finding.
      ✅ `<this commit>`

      The 8 fail with `NullReferenceException` from `EnumerableSorter` inside
      `QueryExecutor.Attaching`: the query is
      `… from s in grouping.DefaultIfEmpty() orderby t.Key1, s.Key1, … select new { t, s }`, so
      `s` is null for an unmatched row and the `orderby` dereferences it **client-side**. On the
      reference provider the ordering reaches the store, where null simply sorts.

      **`GroupJoinFlattener` was the obvious suspect and is only half the cause.** It declines this
      shape by design — substituting the group-join result selector reconstructs the transparent
      identifier *including* its grouping member, and `select new { t, s }` keeps the identifier
      whole, so `flattened` strands `g`. The attempt taught the flattener to replace a stranded
      grouping with `null` when nothing outside the pair reads that member (counted over the whole
      tree against the collection selector that consumes it, which is sound because only this
      `GroupJoin` can name that identifier type).

      **It worked, and it was still not enough.** A probe on the decision (`open=True dead=True`,
      four times) and on the split showed the shipped query change from

          GroupJoin(…, (t, grouping) => ValueTuple(t, grouping))

      to

          LeftJoin(…, (t, s) => ValueTuple(Item1 = t, Item2 = s))

      — the join now runs whole on the server. But `PASSTHROUGH=False` still, and the residual
      still holds the `OrderBy`, still typed over the anonymous identifier
      (`lambda_method(Closure, <>f__AnonymousType576`2)` in the stack). `ProjectionRewriter`
      rewrote the join's client-typed result selector into a tuple and cut the boundary right
      there; `ReCarryInternalTypes`, which exists to keep the operators *above* a carrier on the
      server, did not carry the two nested identifiers through the `OrderBy` chain.

      So the fix is two halves and only pays as one: the flattener rewrite above, **plus** a
      re-carry that survives a nested transparent identifier so the `orderby` ships. Reverted
      rather than kept, because half of it measures neutral and a `null` in a carrier slot is not
      worth carrying on unmeasured merit. Redo both together and measure once.

- [x] **A22.** An augmenting `Include` never walks back the way it came.
      **`Total tests: 13744, Passed: 13658, Failed: 38, Skipped: 48`** — FIXED 2, BROKEN none.
      **`Ef6GroupByTestBase` is 110 of 110.** ✅ `<this commit>`

      `Whats_new_2021_sample_7` has **no `Include` in it at all** — the one EF complained about was
      ours. `AugmentWithNavigations` exists because a navigation the residual reads must actually
      be on the wire (§3.6), and the residual here reads `p.Feet.Person.LastName`, so the path
      `Feet.Person` became `Include("Feet.Person")`. `Feet.Person` is the **inverse** of
      `Person.Feet`, and EF refuses that outright: *"The navigation 'Feet.Person' was ignored from
      'Include' … Walking back include tree is not allowed"* — raised as an error by the spec
      fixtures' warning configuration.

      The rest of that message is the reason it is also unnecessary: *"since the fix-up will
      automatically populate it"*. So a path is now cut at the first step that is the inverse of
      the step before it, and a path that cuts to nothing is dropped. That asks for exactly the
      rows the residual needs and nothing EF will reject.

- [x] **A23.** A navigation is reached through the one navigation that gets to it.
      **`Total tests: 13745, Passed: 13665, Failed: 32, Skipped: 48`** — FIXED 6, BROKEN none.
      ✅ `<this commit>`

      §3.6 puts an `Include` on the shipped query that returns the rows the residual reads a
      navigation off, and refused outright when no shipped query returns rows *of that entity
      type*. That is too literal. `Select(c => new CustomerViewModel(…, c.Orders.SelectMany(o =>
      o.OrderDetails…)))` ships `Customer` rows carrying the orders in a tuple slot, and the
      residual reads `o.OrderDetails` — owner `Order`, and no shipped query returns `Order` rows.
      The `Include` still belongs on the query; it just needs the step that got there:
      `Orders.OrderDetails` at the `Customer` root.

      **Sound exactly when there is one such step**, which is why the fallback prefixes only a
      single unambiguous navigation from a shipped *root* to the read's owner: no other navigation
      could have produced those rows, and the loops before it have already established that no
      shipped query returns them directly. Ambiguous, and it still refuses — guessing wrong would
      put the `Include` on the wrong relationship and answer an empty value, which is the entire
      point of §3.6.

      `QuerySplitterTest.A_navigation_no_shipped_query_can_carry_is_rejected` went green on the
      change, because the two-entity model it used has a unique navigation in both directions and
      the case it described is now *carried* rather than refused. Rather than lose the guardrail's
      coverage, the read it makes is now the one that is genuinely carried — asserted by **value**,
      as this file's own preamble argues for — and the rejection moved to a small second model
      with two collections of the same type.

- [x] **A24.** A dead grouping is nulled, and a nested transparent identifier is re-carried.
      **`Total tests: 13745, Passed: 13669, Failed: 28, Skipped: 48`** — FIXED 4, BROKEN none.
      ✅ `<this commit>`

      A21's two halves, done together, as it said they had to be. Half one is A21's rewrite
      unchanged in substance: when substituting the group-join result selector strands the
      grouping parameter, and that member is read **exactly once in the whole query** — by the
      collection selector this rewrite is about to consume — it is replaced with `null` and the
      pair flattens into a `LeftJoin`. Counting over the whole tree is sound because the
      identifier is an anonymous type only this one `GroupJoin` can construct.

      Half two is what A21 was missing, and it is one line of principle in `CarrierFinder`:
      **a transparent identifier can hold another one**. `from … join … into g from … select`
      produces `new { new { t, g }, s }`, and the inner construction is not the body of any result
      selector, so registering only the outermost left an anonymous type sitting inside the tuple
      — the whole chain still client-typed, which is exactly the "PASSTHROUGH=False, the `OrderBy`
      is still in the residual" A21 measured. Carriers are now registered recursively, and the
      sequence-slot guard makes an exception for a **literal null**, which asks SQL to navigate out
      of nothing and is precisely what half one leaves behind.

      A nested carrier may only be retyped together with its parent — half a rewrite hands
      `Expression.New` a tuple where the constructor declares the original type, which throws and
      discards the pass for a query that was doing nothing wrong. So removal cascades: whatever
      drops a parent drops its children, to a fixed point.

      **The four `ManyToManyNoTracking` parameterizations still fail**, with the same
      `NullReferenceException`. Same query, same split; the difference is on the reading side, so
      it is a separate defect and is A25.

- [x] **A25.** A marker does not get to constrain the query it marks.
      **`Total tests: 13745, Passed: 13673, Failed: 24, Skipped: 48`** — FIXED 4, BROKEN none.
      **`Left_join_with_skip_navigation` is clear, all eight.** ✅ `<this commit>`

      A24 fixed the tracking four and left the no-tracking four failing on the same query with the
      same stack. A probe on the split named the difference immediately: the tracking query shipped
      the whole `LeftJoin` + `OrderBy` chain as tuples, and the no-tracking one shipped only the
      `LeftJoin` and rebuilt the anonymous identifiers on the client — **the re-carry had been
      discarded outright**, not merely declined.

      `ManyToManyNoTrackingQueryTestBase.RewriteServerQueryExpression` wraps the query in
      `AsNoTracking()`, and every one of EF's queryable markers is declared
      `where TEntity : class` — `AsTracking`, `AsNoTracking`, `AsNoTrackingWithIdentityResolution`,
      `IgnoreQueryFilters`, `IgnoreAutoIncludes`, `AsSplitQuery`, `AsSingleQuery`. Retyping the
      element carrier to a `ValueTuple` therefore made `MakeGenericMethod` throw, and the
      `ArgumentException` catch in `Rewrite` — which exists for nodes the pass cannot retype —
      swallowed the entire rewrite.

      A carrier handed to a `class`-constrained method now gets the reference-typed `Tuple<>`
      family, exactly as one compared to `null` already did. `_nullCompared` is renamed
      `_referenceTyped`, because it now has three triggers and only one of them is about null.
      The marker keeps its meaning: tracking behaviour is read off the *original* tree by
      `TrackingBehaviorFinder` and travels in the request, and `IgnoreQueryFilters` and friends
      stay in the shipped subtree where the server honours them.

- [x] **A26.** A mapped member is named by the type that declares it.
      **`Total tests: 13745, Passed: 13675, Failed: 22, Skipped: 48`** — FIXED 2, BROKEN none.
      ✅ `<this commit>`

      `Nested_include_collection_reference_on_non_entity_base` failed with `Expression of type
      'IQueryable<ReferencedEntity>' cannot be used for parameter of type
      'IIncludableQueryable<ReferencedEntity, IEnumerable<PrincipalEntity>>'` — the boundary cut
      **inside** an `Include`/`ThenInclude` chain, replacing the `Include` with the deliberately
      widened `serverN` parameter and leaving the `ThenInclude` above it in the residual, where it
      no longer type-checks.

      Not an `Include` problem. The query is
      `Set<ReferencedEntity>().Include(e => e.Principals).ThenInclude(e => e.Reference)`, and
      `Reference` is declared on `NonEntityBase`, which `PrincipalEntity` derives from and the
      model does not know. `WireTypeCollector` reports a member read's **declaring** type; the
      allowlist is built from entity CLR types and mapped property types only; so the read was not
      server-ok, the `ThenInclude` was not shippable, and the cut fell exactly where it did. The
      test's own name says this — *on non entity base*.

      The allowlist now also admits the declaring type of every mapped member. That is not a
      widening of reach: the type is a base of an entity type the list already admits, named only
      for a member the model maps. Nothing else in the suite moved.

- [x] **A27.** The store's own limits, asserted as EF asserts them.
      **`Total tests: 13745, Passed: 13683, Failed: 14, Skipped: 48`** — FIXED 8, BROKEN none.
      ✅ `<this commit>`

      Four residual methods turned out to be **backing-store limitations that EF Core overrides in
      its own suites**, reached only because the split now ships the whole query. Each override is
      EF's, adopted with the reason stated:

      | Test | Tier | EF's own override |
      |---|---|---|
      | `SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_…` | A | `NorthwindSelectQueryInMemoryTest` — `Assert.ThrowsAsync<NotImplementedException>`; `InMemoryQueryExpression.AddJoin` is literally unimplemented for this shape |
      | the same | B | `NorthwindSelectQuerySqliteTest` — `AssertUnableToTranslateEFProperty` |
      | `Reverse_without_explicit_ordering` | B | `NorthwindSelectQueryRelationalTestBase` — `MissingOrderingInSelectExpression`; **every** relational provider fails it, which is why the override lives on the relational base rather than SQLite's |
      | `Final_GroupBy_nominal_type_entity` | A | `NorthwindGroupByQueryInMemoryTest` — translation failure |

      `Reverse_without_explicit_ordering` was previously classified as a real failure on the
      grounds that `EFCore.Sqlite.FunctionalTests` does not override it. It does not — its
      *relational base* does, and that base cannot be derived from here because it also swaps in a
      `RelationalQueryAsserter` that needs relational test infrastructure. **Grepping the SQLite
      suite alone is not enough; the relational specification base is part of the answer.**

      `Final_GroupBy_nominal_type_entity` gets the store-independent half of EF's assertion.
      EF fails it with `NonComposedGroupByNotSupported`; this provider refuses one step earlier,
      because `GroupBy(c => new RandomClass { … })` keys the grouping on a type the server cannot
      name, so the query never reaches the store to be told the store's reason. Both say the
      query does not translate, which is what the test is for.

- [x] **A28.** What is left of the query residual, and why each one stays.
      No code change; classification only, read out of `artifacts/measure/a28.txt`. ✅ `<this commit>`

      Nine query failures remain, and they fall into two kinds.

      **A real gap (2).** `SelectMany_correlated_subquery_hard` — a correlated subquery under a
      client-side projection, refused by `RejectOpenFragments` with the message that names
      milestone M2-B. This is the one remaining *designed* hole in the split.

      **The base asserts a limitation this provider does not have (4).**
      `Select_GroupBy_SelectMany` and `Join_with_nav_projected_in_subquery_when_client_eval` are
      both written as `AssertTranslationFailed(() => AssertQuery(…))`, and both now run and
      **return the right answer** — "no exception was thrown" is the whole failure. That is not a
      defect to fix. It is also not one that can be overridden away: the query bodies name
      `ProjectedType` and `ClientProjection`, both `private` to the spec base, so a derived class
      cannot restate the query to assert success instead. EF's own SqlServer suite overrides both
      by calling `base` — i.e. it fails there too. Left red deliberately.

      **Still to diagnose (3).** `Contains_over_keyless_entity_throws` (2) answers `False` where
      every provider answers `True`, which is about identity for a type that has none, and
      `QueryFilterFuncletization.Local_variable_from_OnModelCreating_can_throw_exception` (1),
      where the exception is right and raised from a different EF path, so the message differs by
      two words.

- [x] **A29.** The server reports an error the way the client would.
      **`Total tests: 13745, Passed: 13684, Failed: 13, Skipped: 48`** — FIXED 1, BROKEN none.
      **`QueryFilterFuncletizationTestBase` is 28 of 28.** ✅ `<this commit>`

      `Local_variable_from_OnModelCreating_can_throw_exception` differed from EF's expected message
      by two words — `CoreStrings.ExpressionParameterization**Exception**` where EF expects
      `…ExceptionSensitive`, which also names the expression that failed. Right exception, right
      place; the sensitive variant is simply what EF emits when
      `EnableSensitiveDataLogging` is on.

      **Which told us where it was raised.** The spec fixtures set that option on the *client*
      (`FixtureBase.AddOptions`), so a client-side throw would already have had the sensitive
      wording. It did not, so the query filter's closure was being evaluated on the **server**,
      whose options this test store builds itself and which had no such setting. The store now
      sets it, and the message matches.

      Deliberately only that one option, not the rest of `AddOptions`: its
      `ConfigureWarnings(Default(Throw))` is a statement about the query the *test author* wrote,
      and the server runs a tree this provider generated from it — A22 is what that distinction
      costs when it is missed.

- [x] **A30.** A keyless entity has no identity, so its values are its identity.
      **`Total tests: 13745, Passed: 13686, Failed: 11, Skipped: 48`** — FIXED 2, BROKEN none.
      ✅ `<this commit>`

      `Contains_over_keyless_entity_throws` answered `False` where every EF provider answers
      `True`. Two probes settled it in one run: the split is **passthrough** and EF issues two
      queries — `First()`, then `Contains(<the instance it just returned>)` — and the constant
      arrived on the server as
      `{"CompanyName":null,"ContactName":null,"ContactTitle":null,"Address":null,"City":null}`.

      An entity in a *query tree* travels as identity only (research-findings §7), which is right
      and is what makes `Contains(customer)` a key comparison rather than a graph copy. A keyless
      entity type has no key, so "identity only" carries **literally nothing** and the server
      rebuilt an empty shell. `CustomerQuery` overrides `Equals` on `CompanyName`, so the store
      compared a real row against a blank one and said no.

      Now: no primary key means the mapped scalars travel instead, and the reference form
      rehydrates through the ordinary object-shape path. Scalars only — the navigation half of
      `MapRowMembers` needs row-mapping callbacks a constant does not have, and `_isNavigationLoaded!`
      would have thrown. That split is why `MapScalars` now exists.

- [x] **A31.** Half an override set is not an override set.
      **`Total tests: 13745, Passed: 13687, Failed: 10, Skipped: 48`** — FIXED 1, BROKEN none.
      **Lazy loading is 825 of 825.** ✅ `<this commit>`

      `Can_serialize_proxies_to_JSON` had been recorded — in this plan and in CLAUDE.md — as the
      one **complex-type** failure: `Culture.Species` arrived null, and `MapRowMembers` walks
      `GetProperties()`, which excludes complex properties. The diagnosis was wrong, and a probe
      on the metadata is what said so: `Blog`'s complex-property list is **empty**. This fixture
      does not map `Culture` or `Milk` at all — our own `InfoCarrierFixture` `Ignore`s them,
      one for one with EF's `LazyLoadProxyInMemoryTest`, because the InMemory store has no
      complex types.

      EF's InMemory test therefore also overrides `SerializedBlogs1` and `SerializedBlogs2`, whose
      expected JSON has `"Species": null` where the relational base has `"S1"`. We had adopted the
      `Ignore` calls and not the strings, so the test was comparing against an expectation for a
      model nobody built. Both strings adopted verbatim.

      **The lesson generalises past this test.** An override set is adopted as a set: the pieces
      of EF's `*InMemoryTest` are consequences of each other, and taking the model configuration
      without the expectations it implies leaves a failure that reads like a provider defect.

- [x] **A32.** Complex types travel, and the base that proves it.
      **`Total tests: 13996, Passed: 13896, Failed: 12, Skipped: 88`** — **251 tests added, 249 of
      them passing**; FIXED none, BROKEN 2, both of them new. Unadopted bases **110 → 109**.
      ✅ `<this commit>`

      A31 removed the only failure complex types had been blamed for, which left the feature with
      **no test at all** — so it was written and adopted in the same step:
      `ComplexTypesTrackingTestBase`, EF's own corpus, mirroring `ComplexTypesTrackingInMemoryTest`
      down to the `TransactionIgnoredWarning` and the `PreferProperty` access mode.

      Four defects, in the order the corpus surfaced them — **211 red, then 28, then 12, then 2**:

      1. **A primitive collection needs a JSON reader/writer to be mapped at all.**
         `InfoCarrierTypeMappingSource` built its mapping without one, so `List<string>` on a
         complex type was left unmapped and a constructor taking it failed to bind — 211 failures,
         all of them at *model building*, before a single byte moved. EF's
         `InMemoryTypeMappingSource` passes `Dependencies.JsonValueReaderWriterSource.FindReaderWriter`
         for exactly this; so does ours now.
      2. **Complex properties are not in `GetProperties()`.** Added to both directions — the query
         row (`MapScalars`, which is the half of `MapRowMembers` a query-tree constant can also
         use) and the change entry (`ChangeEntryMapper`, replayed by `ServerSaveChangesExecutor`).
         A complex value is *owned* — no identity, no navigations, no sharing — so the mapper's
         ordinary object shape is the whole of it, nesting included.
      3. **The allowlist has to walk complex types by hand.**
         `GetFlattenedComplexProperties()` stops at a complex *collection* — "including those on
         non-collection complex types" is its own summary — so a third-level complex property
         reached through one was refused on deserialization.
      4. **A sequence does not always yield its first generic argument.** A property-bag complex
         type is a `Dictionary<string, object>`, whose elements are `KeyValuePair<string, object>`.
         `GetElementType` took the first argument, built a `List<string>` and refused every
         element.

      Neither side can put a complex value in the value dictionary the entity is built from:
      `CreateEntry` and `ShadowValuesFactory` are keyed by property **name**, and
      `GetFlattenedProperties()` gives each complex leaf its own — `Culture.Species` and
      `Milk.Species` are both `"Species"`. Both sides therefore set the whole complex value through
      its CLR member, through the backing field where there is one (L6), before the row is tracked.

      **Left red: `Can_track_entity_with_complex_property_bag_collections(state: Added)`**, 2 of
      251. The failure is inside EF's `StructuralTypeMaterializerSource` — *Incorrect number of
      arguments supplied for call to method 'System.Object get_Item(System.String)'* — raised
      while building a materializer for a property-bag complex collection. That is one shape of
      one feature, and the diagnosis belongs with the materializer rather than with the wire.

- [x] **A33.** Three more query bases: complex navigations, their collections, and Gears of War.
      **`Total tests: 16099, Passed: 15897, Failed: 110, Skipped: 92`** — **2103 tests added, 2005
      of them passing**; FIXED none, BROKEN 98, **every one of them inside the three new classes**.
      Unadopted bases **109 → 106**. ✅ `<this commit>`

      `ComplexNavigationsQueryTestBase` and `ComplexNavigationsCollectionsQueryTestBase` share one
      fixture, as EF's do. `GearsOfWarQueryTestBase` is EF's largest single corpus and leans on
      optional navigations, TPH inheritance and null semantics at once. Every override in all three
      is EF's own `*InMemoryTest`, adopted as a **set** (A31) rather than picked from.

      The 98, by cause — this is the classification, not a to-do list, and it is read out of
      `artifacts/measure/a33.reasons.txt`:

      | Count | Reason | Reading |
      |---:|---|---|
      | 30 | `SocketException : The attempted operation is not supported for the type of object referenced` | **The `IPAddress.ScopeId` signature.** A19 named this exactly: a value with a converter reached the mapper's *reflective member walk* instead of travelling as its provider value. A19 fixed the edges it could see; this corpus has found another. Highest-value single fix left. |
      | 26 | `Nullable object must have a value` | Diagnosed in **A35**: `CollectFragments` lifts a fragment out of the conditional branch that guards it. Not the carrier rule this row first guessed at. |
      | 14 | `Assert.Throws(): No exception was thrown` | Same shape as A28's four: the base asserts a limitation, and the query runs. Each needs reading before it is called anything. |
      | 10 | `Assert.Throws(): Exception type was not an exact match` | We throw, but not what EF throws. |
      | 10 | `NullReferenceException` | Undiagnosed. |
      | 6 | `The LINQ expression '…' could not be translated` | Our own `RejectClientEvaluation`. |
      | 4 | `'Level*.OneToMany_Optional_Self_Inverse*Id' is a shadow property, so its value is held…` | **The fifth shadow-property site**, predicted in the handoff and now found. `GetGetter()` on a shadow property throws rather than returning null; L18, A3, A10 and A18 are the first four. |
      | 2 | `Argument type 'List<string>' does not match` | |
      | 2 | `Queryable in subquery` / `Distinct` shapes | |

- [x] **A34.** A value EF knows how to write as JSON travels as its JSON.
      **`Total tests: 16099, Passed: 15925, Failed: 82, Skipped: 92`** — FIXED 28, BROKEN none.
      **The `SocketException` family is gone, 30 → 0.** ✅ `<this commit>`

      A19 established that a value behind a converter travels as its *provider* value, and named
      the exact signature of getting it wrong: the mapper's reflective member walk reads every
      public getter, and `IPAddress.ScopeId` throws `SocketException` for an IPv4 address. A33's
      corpus produced 30 of them — from a property with **no converter at all**.
      `Faction.ServerAddress` is an `IPAddress`, and the InMemory store keeps it as one.

      A converter is not the only way a mapped value can be an arbitrary CLR type. The model
      already answers what to do with such a value: EF gives the property a
      `JsonValueReaderWriter` precisely because it knows how to write it. So a property with no
      converter, whose CLR type is none of the wire primitives, now travels as its JSON string and
      is rebuilt by the same reader on the far side.

      **The first attempt measured byte-identical**, and the reason is worth keeping:
      `IReadOnlyProperty.GetJsonValueReaderWriter()` answers only what the model was *explicitly
      annotated* with — `CreateFromType(this[CoreAnnotationNames.JsonValueReaderWriterType])` — which
      is null in the ordinary case. The one that exists is on the **type mapping**:
      `property.FindTypeMapping()?.JsonValueReaderWriter`. The code ran; the condition was never
      true. Exactly the failure mode CLAUDE.md warns about, and the second reading is what found it.

      Of the 30, **28 now pass and 2 fail on a value comparison** — past the crash and into an
      ordinary disagreement, which is a different problem and is left classified rather than
      guessed at.

- [x] **A35.** The `Nullable object must have a value` family, diagnosed. No code change.
      ✅ `<this commit>`

      26 of A33's remaining failures, one cause, and it is **not** the carrier rule A33's table
      guessed at. Every one is a `Projecting_property_converted_to_nullable_*` variant of

      ```csharp
      ss.Set<CogTag>().Select(x => new
      {
          x.Note,
          Nullable = x.GearNickName != null
              ? new { x.Gear.Nickname, x.Gear.SquadId, x.Gear.HasSoulPatch }
              : null,
      })
      ```

      and every one throws **on the server**, inside the InMemory shaper.

      `ProjectionRewriter.CollectFragments` walks a client-typed projection body for its maximal
      server-evaluable subexpressions and puts each in a tuple slot. Here the body's client-typed
      part is the whole `cond ? new { … } : null`, so the walk descends *through the conditional*
      and collects `x.Gear.Nickname`, `x.Gear.SquadId` and `x.Gear.HasSoulPatch` as three
      independent fragments — **outside the test that was guarding them**. `x.Gear` is null for a
      tag with no gear, and `x.Gear.SquadId` is exactly the dereference the `!= null` existed to
      prevent.

      So the rule the splitter is missing is: **a fragment may not be lifted out of the branch of a
      conditional whose test guards it.** Either the conditional travels whole, or each fragment
      taken from a branch travels wrapped in the same test. The test itself
      (`x.GearNickName != null`) is server-ok and already becomes a fragment, so the material for
      the second option is there. Not attempted here — it is a change to the shape of what ships,
      and this plan's rule is one experiment per measurement.

- [x] **A36.** A fragment lifted out of a conditional branch travels wrapped in its test.
      **`Total tests: 16099, Passed: 15927, Failed: 80, Skipped: 92`** — FIXED 2, BROKEN none.
      **The `Nullable object must have a value` family is gone, 26 → 0.** ✅ `<this commit>`

      A35's rule, implemented. `CollectFragments` now recognises `ConditionalExpression` instead of
      descending into it blindly: a fragment taken from a branch records the test that guarded it
      (negated for the false branch, `AndAlso`-chained through nested conditionals), and `Guarded`
      wraps the slot as `test ? fragment : default` on its way into the tuple. The client body is
      untouched — it still holds the conditional and only reads the slot down the branch it belongs
      to, so the default is never observed.

      **Two things this cost, both found by measuring rather than by reading:**

      1. `Expression.Default(type)` for the else-arm made it **worse**: 82 → 86. A
         `DefaultExpression` is not one of `IsSerializableKind`'s node kinds (research-findings §5),
         so every guarded fragment became unshippable and its enclosing call fell to the residual —
         where the navigation it read had no shipped query to carry it. Six tests that pass today
         broke on `The client-side part of the query reads navigation …`. An
         `Expression.Constant(default, type)` is the same value and *is* a serializable kind.
      2. The first cut also **refused to lift** from a branch whose test was not server-ok, on the
         principle that an unreproducible guard means no lift. Measured byte-identical to the
         `Default` version — the six were never that arm — and it is a needless risk, so the
         unguardable case keeps the prior unguarded descent. Every conditional in this corpus whose
         test the analyzer will not ship compares an **entity** to null.

      **Left red: 24 of the 26, now failing as `NullReferenceException`** — and somewhere else
      entirely. The stack is `Enumerable.IEnumerableWhereIterator.MoveNext` inside
      `QueryExecutor.Guarded`: the server now returns the right rows, and the *client residual*
      dereferences them. `Select(x => new { …, Nullable = cond ? new { … } : null }).Where(x =>
      x.Nullable.SquadId == 1)` leaves the `Where` on the client, where `x.Nullable` is null for a
      tag with no gear and C# throws. EF expects SQL null semantics, under which the row is simply
      filtered out — for two of these variants EF even supplies a second expected lambda
      (`x.Nullable != null && …`) acknowledging the difference. **The client residual would need
      null-propagating semantics**, which is a feature and not a fix; classified here, not
      attempted.

- [x] **A37.** A join key is a carrier too.
      **`Total tests: 16099, Passed: 15931, Failed: 76, Skipped: 92`** — FIXED 4, BROKEN none.
      **The shadow-property family is gone, 4 → 0.** ✅ `<this commit>`

      A33's table called this "the fifth `GetGetter()`-on-a-shadow-property site", following L18,
      A3, A10 and A18. It is not one. `ClientPropertyReader` raises that message **deliberately**
      (projection-split.md §7) — the shadow read was a symptom, and the question was why the read
      was on the client at all.

      `join l2 in ss.Set<Level2>() on new { A = EF.Property<int?>(l1, "…"), B = … } equals new { A =
      …, B = … }` builds an anonymous type for the **key**. That type is created inside the query
      and never reaches its result, which is precisely the structural property this pass exists for
      — but `CarrierFinder` only ever called `Register` on the body of a *result* selector, and a
      key selector is not one. So the key stayed a client type, ADR-010 made it a type boundary,
      the whole join fell to the client, and the key selectors then read the shadow FKs the keys
      are made of. `Join` and `GroupJoin` now register both key-selector bodies as candidates.

      Everything downstream already handled it: the key type has no `class` constraint (A25), so
      the `ValueTuple` retype builds, and tuple equality is structural in the same way anonymous
      equality is.

- [x] **A38.** A reflected call lets its own exception out.
      **`Total tests: 16099, Passed: 15931, Failed: 76, Skipped: 92`** — FIXED none, BROKEN none.
      **Kept anyway**, and the reasoning is the point. ✅ `<this commit>`

      A query boundary's element type is only known at run time, so `QueryExecutor.Materialize`
      goes through `MethodBase.Invoke` — which wraps whatever the callee threw in
      `TargetInvocationException`. That is an implementation detail of this provider changing the
      exception a caller sees, and it was actively hiding a diagnosis: the whole of
      `Join_with_result_selector_returning_queryable_throws_validation_error` read as "we throw the
      wrong type", when the wrong type was a wrapper. Unwrapped through
      `ExceptionDispatchInfo.Capture(e.InnerException).Throw()`, so the stack survives.

      **This is the exception to "revert what does not pay".** It moved no test — but a caller
      seeing `TargetInvocationException` from an internal reflective call is a defect whether or
      not a spec test scores it, and the two `Include_on_GroupJoin_SelectMany_DefaultIfEmpty_*`
      overrides confirm EF's *own* `TargetInvocationException` still arrives intact, so the unwrap
      is not swallowing anything real.

      **What it exposed, left red:** the real exception is `InvalidCastException` at
      `ClientResultMaterializer.Materialize` — `TElement` is `IQueryable<Level3>` and the wire
      decoded a `List<Level3>`. EF's core check for this is
      `CoreStrings.QueryInvalidMaterializationType` / `InvalidOperationException`, which this
      provider *does* raise for the two shapes `AssertInvalidMaterializationType` covers; for this
      shape EF's core check does not fire either, and InMemory happens to fail first with an
      `ArgumentException` its shaper builder raises. Matching that would mean synthesising a
      backend's incidental error, which is inventing an expectation rather than mirroring one.
      Classified, not faked.

- [x] **A39.** Four `Distinct` overrides for a limitation this provider does not have.
      **`Total tests: 16099, Passed: 15939, Failed: 68, Skipped: 92`** — FIXED 8, BROKEN none.
      ✅ `<this commit>`

      A33 adopted EF's `GearsOfWarQueryInMemoryTest` overrides as a set (A31), which was right:
      most of them are limitations of the store this provider runs on. Four are not.
      `Projecting_entity_as_well_as_correlated_collection_followed_by_Distinct`, its `complex_` and
      `of_scalars_` siblings, and `Projecting_some_properties_as_well_as_…` each wrap the base in
      `Assert.ThrowsAsync<InvalidOperationException>` expecting
      `InMemoryStrings.DistinctOnSubqueryNotSupported` (EF issue #24325) — and the base ran to
      completion, which means the base's own `AssertQuery` **passed**. The `Distinct` in those
      shapes lands in the residual, so the store never sees the subquery it refuses.

      **This is the reverse case CLAUDE.md names**: an override of *ours* for a limitation the
      provider does not have is a workaround, and deleting it restores spec coverage rather than
      suppressing it (ADR-004). Adopting a set is about not cherry-picking a model configuration
      away from the expectations it implies; it is not a reason to keep asserting a failure that
      does not happen. The comment left in its place says which four and why, so the next reader
      does not "restore" them.

      **Not deleted, deliberately:** `Projecting_correlated_collection_followed_by_Distinct` still
      reaches the store and still throws, so its override is real; and
      `Correlated_collection_with_distinct_3_levels` runs but answers **wrong** (`EqualException`
      through the `Assert.Throws`), which is a defect of ours and stays red.

      **The `No exception was thrown` bucket is 14 → 6**, and the remaining six are all the A28
      shape: `Select_projecting_queryable_in_anonymous_projection_followed_by_Join` (asserts
      `CoreStrings.QueryInvalidMaterializationType`; the `Subquery` member never reaches the result
      past the `Join`, so nothing invalid is materialized), plus `Select_GroupBy_SelectMany` and
      `Join_with_nav_projected_in_subquery_when_client_eval`. All three build the query inline in a
      `protected static` assert helper, so the expectation cannot be inverted from a derived class.

- [x] **A40.** A carrier can live in a conditional branch.
      **`Total tests: 16099, Passed: 15961, Failed: 46, Skipped: 92`** — FIXED 22, BROKEN none.
      **68 → 46, the largest single step of this phase**, and it closes
      `SelectMany_correlated_subquery_hard` — the gap A28 called the only real one left, which
      milestone M2-B existed for. ✅ `<this commit>`

      A36 shipped A35's rule and left 24 of the 26 failing one step further on, as a
      `NullReferenceException` in the *client residual*. A36's note said that needed
      null-propagating semantics on the residual — a feature. It does not. **It needs the residual
      not to exist**: hand EF the whole query and EF's own null semantics apply, which is why these
      tests pass on EF's InMemory provider unmodified.

      What stopped the whole query shipping was `new { Note, Nullable = cond ? new { … } : null }`,
      and it took **four** defects, each hiding the next — a good argument for the probe over
      reading:

      1. `CarrierFinder.Register` never descended into a `ConditionalExpression`, so the inner
         carrier was the argument of no construction and the body of no result selector, and was
         never a candidate at all.
      2. Nothing marked it **reference-typed**. `cond ? new { … } : null` has no `ValueTuple` form,
         and `Expression.Condition` refusing the mismatch discards the entire pass through
         `Rewrite`'s catch. Fourth trigger, after the null comparison, the absence-producing
         operator (`FirstOrDefault`) and the `class` constraint (A25).
      3. `Find` excluded it for being **reachable from the result type** — true, but reachable only
         *through* the element carrier, which `RebuildAtRoot` rebuilds recursively. The probe
         showed this one exactly: the outer carrier became a `ValueTuple` and the inner stayed
         anonymous, because it is a *generic argument* of the outer. A tuple with an anonymous type
         in it ships no further than the anonymous type did.
      4. `ExpressionVisitor.VisitConditional` rebuilds through `node.Update`, which keeps the
         **original** node type — so both branches retyped and the conditional still declaring the
         anonymous type gave `Argument types do not match`, and the catch threw the pass away.

      Each of 1–3 alone measures as nothing (the first attempt was FIXED 0 / BROKEN 2), and the
      shape of "nothing" was identical to "the target does not exist". The probe named all four in
      three runs of one test.

      **The `NullReferenceException` bucket is 34 → 14** and the residual `Nullable object must
      have a value` family is fully closed.

- [x] **A41.** A query that ends in `OrderBy` still has an element carrier.
      **`Total tests: 16099, Passed: 15963, Failed: 44, Skipped: 92`** — FIXED 2, BROKEN none.
      ✅ `<this commit>`

      `Projecting_property_converted_to_nullable_and_use_it_in_order_by` is A40's query with
      `.OrderBy(x => x.Nullable.SquadId).ThenBy(x => x.Note)` on the end, and it stayed on the
      client where every sibling had moved to the server. Two places assume the exact
      `IQueryable<T>`:

      - `CarrierFinder.Find` tested `query.Type.GetGenericTypeDefinition() == typeof(IQueryable<>)`
        for the element carrier. A query ending in `OrderBy` is an `IOrderedQueryable<T>`, so it
        found none — and the carrier was then struck out for being *reachable from the result
        type*, which is the very exclusion A40 had to qualify. Now any queryable, through
        `ServerBoundaryAnalyzer.ElementTypeOf`.
      - `RewriteVerifier` rejected the result as `TypeChanged`, because `RebuildAtRoot` appends a
        `Select` and that answers `IQueryable<T>`. The guard now compares the **element** type when
        both roots are queryable. Nothing composes above the root of a captured query, so the one
        thing `IOrderedQueryable<T>` buys there — a `ThenBy` on top — has no caller.
        `A_rewrite_that_changes_the_result_type_is_discarded` still covers the guard: it changes
        the element type, which is still refused.

      **The first half alone measured byte-identical** — the re-carry built the right tree and the
      verifier threw it away — and the probe distinguished those in one run. Two identical-looking
      nothings in two consecutive steps; the probe is the only thing that tells them apart.

- [x] **A42.** A `GroupBy` key is a carrier too.
      **`Total tests: 16099, Passed: 15971, Failed: 36, Skipped: 92`** — FIXED 8, BROKEN none.
      ✅ `<this commit>`

      A37's rule, applied where it also holds. `GroupBy(t => new { t.Gear.HasSoulPatch,
      t.Gear.Squad.Name })` builds an anonymous type for the key; so does
      `GroupBy(l1 => …, l1 => new { Id = (int?)l1.OneToOne_Required_PK1.Id ?? 0 })` for the element.
      Neither is the body of a result selector, so neither was a candidate, so the whole grouping
      stayed on the client — where the key selector dereferenced the optional navigation it is made
      of, four `NullReferenceException`s deep in `Enumerable.Lookup.Create`. Every lambda argument
      of a `GroupBy` is registered, because the 3rd and 4th are overloaded between an element
      selector and a result selector.

      One consequence had to come with it: `g.Key` is a member whose **declaring** type merely
      mentions a carrier (`IGrouping<TKey, …>`), and `ExpressionVisitor` rebuilds a
      `MemberExpression` through `node.Update`, which keeps the original `MemberInfo` — not
      declared on the mapped type, so `Expression` refuses it and the catch discards the pass.
      Re-resolved by name, which is exact because the mapped type is the same generic definition.

      **The `NullReferenceException` bucket is 12 → 4**, and the remaining four are two shapes:
      `Member_over_null_check_ternary_and_nested_dto_type` (a `MemberInitExpression` carrier — the
      anonymous-type sibling passes since A40, and `Register` handles only `NewExpression`) and
      `GroupJoin_on_a_subquery_containing_another_GroupJoin_projecting_outer_with_client_method`.

- [x] **A43.** An object initializer is a carrier like any other.
      **`Total tests: 16100, Passed: 15976, Failed: 32, Skipped: 92`** — FIXED 4, BROKEN none.
      ✅ `<this commit>`

      `new Level1Dto { Id = l1.Id, Level2 = cond ? null : new Level2Dto { … } }` creates a type
      inside the query and never lets it reach the result — the whole structural test this pass
      applies — but it arrives as a `MemberInitExpression`, and `Register` handled only
      `NewExpression`. That is why `Member_over_null_check_ternary_and_nested_anonymous_type`
      passed from A40 and its `_dto_type` sibling did not. It also closed
      `Select_GroupBy_SelectMany`, one of the four A28 had classified as unreachable.

      Slot order comes from the recorded member list, not from each initializer's binding order:
      two sites can initialize the same DTO's members in different orders, and a tuple whose slots
      disagree would be a **wrong answer** rather than a refused rewrite. A site missing a member
      throws into `Rewrite`'s catch, which keeps the original tree.

      The rebuild has to construct what the query constructed: an anonymous type through the
      constructor that takes every member, a DTO through a parameterless constructor and
      assignments. The absence of a parameterless constructor is the discriminator, because
      `NewExpression.Members` is only ever populated for the former.

      **One of our own unit tests changed meaning, and was rewritten rather than deleted (A23).**
      `A_join_key_the_client_cannot_compare_is_a_translation_failure` used a `BookSummary` object
      initializer as a join key; that key is now a tuple, the join ships, and the server compares
      it structurally — which is what the query said. The guard is not weaker, so the test keeps
      its name and its assertion and moves to `ClientRow`, a **constructor**-built carrier the
      re-carry deliberately leaves alone, where the client really does compare by reference. A new
      `A_dto_join_key_is_re_carried_and_ships` asserts the new behaviour on the old query.

- [x] **A44.** Client code in a row-deciding argument, mirrored from EF's relational base.
      **`Total tests: 16100, Passed: 15982, Failed: 26, Skipped: 92`** — FIXED 6, BROKEN none.
      **The `could not be translated` bucket is gone, 6 → 0.** ✅ `<this commit>`

      The six were `RejectClientEvaluation` refusing `ClientMethod(...)` in a `Where` predicate or
      an `OrderBy` key — the line ADR-010 draws, which is EF's own line and every relational
      provider's. **A27 exactly:** all three tests are overridden with `AssertTranslationFailed` on
      `ComplexNavigationsQueryRelationalTestBase` / `GearsOfWarQueryRelationalTestBase`, and
      `GroupJoin_client_method_in_OrderBy` with `AssertTranslationFailedWithDetails` in EF's SQLite
      suite. This provider has the same limit for the same reason — running a predicate here means
      fetching every row first — so the overrides are mirrored, not invented, and the reason is
      stated at each.

      The details clause needed EF's *display* name for the declaring type
      (`ComplexNavigationsQueryTestBase<…Fixture>`) and not `Type.FullName`, which is the CLR's
      backtick form. `QuerySplitter.DisplayName` produces the former; the first version of the
      override compared against the latter and failed on a substring that looked like a missing
      details clause.

- [x] **A45.** An `Include` is validated against the query as the caller wrote it.
      **`Total tests: 16100, Passed: 15984, Failed: 24, Skipped: 92`** — FIXED 2, BROKEN none.
      ✅ `<this commit>`

      `ss.Set<Faction>().Select(f => new { f }).Include(x => x.f.Capital)` was refused, correctly,
      but by **EF on the server** — and by then the carrier re-carry had renamed the member, so the
      message said `x.Item1.Capital` where the caller wrote `x.f.Capital`. An internal carrier
      leaking into a user-facing message is a defect on its own terms; the spec test happens to
      assert it exactly.

      Two halves, and the first alone measured byte-identical: `RejectInvalidIncludes` now reads
      the query **as captured**, before flattening and re-carry, *and* it now performs EF's other
      include check. `x => x.f.Capital` is a perfectly well-formed property path, so
      `IsPropertyPath` accepted it; what EF rejects is that its **root is not an entity** —
      `CoreStrings.IncludeOnNonEntity`, which names the whole lambda rather than its body.
      Shared-type and owned entity types are not reachable by CLR type through
      `FindEntityType`, so the entity test falls back to scanning `GetEntityTypes()`.

- [x] **A46.** All sixteen `Query.Translations` bases.
      **`Total tests: 16433, Passed: 16315, Failed: 26, Skipped: 92`** — **333 tests added, 331 of
      them passing**; FIXED none, BROKEN 2. Unadopted bases **90**. ✅ `<this commit>`

      EF Core 10 replaced the sprawling `*FunctionsQuery` base with one class per CLR type or
      operator family over a single shared model: `ByteArray`, `Enum`, `Guid`, `Math`,
      `Miscellaneous`, `String`, the five `Operators.*` and the five `Temporal.*`. They are the
      densest scalar coverage EF has, and this provider had none of it — every value in them
      crosses the wire as a constant, a parameter or a projected column, which is exactly what
      `PrimitiveCoercion` and the allowlist decide (A19, A34). **331 of 333 passed on adoption**,
      which is what A34 bought.

      Overrides, both mirrored with the reason stated:

      - The three `*_with_StringComparison_unsupported` are EF's own `StringTranslationsInMemoryTest`:
        the culture-sensitive comparisons no real provider supports and the InMemory one does, so
        the base asserts a throw this backing store will not produce.
      - The six `Random_*` are EF's own `MiscellaneousTranslationsRelationalTestBase` (A27).
        `Random.Next()` in a `Where` is client code in a row-deciding argument, and it is worse
        here than for a relational provider: a random number drawn on the client would decide which
        rows are fetched, once, and then be gone.

      **Left red: `Regex_IsMatch` and `Regex_IsMatch_constant_input`.** Not a defect and not an
      oversight — `System.Text.RegularExpressions.Regex` is not on the allowlist, and the allowlist
      is ADR-008's constraint 2, the thing that stops a serialized tree from naming arbitrary
      types. SQLite and SQL Server translate `Regex.IsMatch` to SQL; the InMemory store runs it
      as .NET. **Whether this provider should allowlist it is a design decision for
      `docs/roadmap.md`, not a fix**, and it is deliberately not made here.

- [x] **A47.** The nine `ModelBuilding.ModelBuilderTest` bases.
      **`Total tests: 17136, Passed: 16952, Failed: 26, Skipped: 158`** — **703 tests added, every
      one of them passing**; FIXED none, BROKEN none. Unadopted bases **80**. ✅ `<this commit>`

      Nothing here touches a store. What it exercises is the one thing the client model has that no
      other test reaches directly: this provider's conventions and type-mapping source, applied by
      `ModelBuilder` to every shape EF supports — inheritance, owned types, complex types and
      collections, and each relationship cardinality. A client `DbContext` has no database, so its
      model is the whole of what it knows, and it must agree with the server's (ADR-008).

      Structured as EF's `InMemoryModelBuilderTest`: abstract classes per spec base, then one
      concrete set through `GenericTestModelBuilder`. EF's InMemory suite adds three more concrete
      sets — non-generic, string-named, unqualified-string — which cover the *builder API's*
      surface rather than the provider's and remain available. The provider-specific
      `[ConditionalFact]`s EF adds to its own variants are InMemory's tests, not spec base members,
      and are not carried.

      **`ForeignKeysHaveIndexes` is left at EF's default `true`, unlike InMemory's fixture, and
      that is the whole content of the adoption.** The first cut copied InMemory's `false` and
      produced **136 `Assert.Empty() Failure: Collection was not empty`** — one cause, and not a
      defect: this provider keeps `ForeignKeyIndexConvention` where the InMemory provider drops it.
      The fixture flag is a statement of what the provider *does*, not a preference, so it was
      corrected rather than the model. An index on a client model is metadata about a store the
      client does not have; it costs nothing and travels nowhere. Whether to drop the convention
      anyway is a provider question and is not answered here.

- [x] **A48.** The whole residual, classified. No code change. ✅ `<this commit>`

      A33's 98 are down to 16 and the suite to **26 failures over 17136 tests**. This is the map,
      read out of `artifacts/measure/a47b.txt` — nothing below is undiagnosed, and none of it is
      a to-do list.

      | Count | Test | Reading |
      |---:|---|---|
      | 2 | `Correlated_collection_with_distinct_3_levels` | **A real defect.** The override asserts EF's `DistinctOnSubqueryNotSupported`; the query runs instead (A39) and answers **wrong** — the base's own assertion fails inside the `Assert.Throws`. The only wrong *answer* left in the suite. |
      | 2 | `Comparison_with_value_converted_subclass` | **A real defect**, `Expected: 1, Actual: 0`. A value-converted key compared against a constant of a *subclass*; A20's rule (a constant is mapped by what it is) and A19's (a converted value travels as its provider value) meet here. |
      | 2 | `Complex_query_with_let_collection_projection_FirstOrDefault` | `Argument type 'List<string>' does not match … 'IQueryable<string>'`, raised **on the server** inside `InMemoryProjectionBindingExpressionVisitor.VisitNew`. The shipped tree has an anonymous type whose member is declared `IQueryable<string>` holding a `ToList`; `ProjectionRewriter.Materialized` is the only thing that introduces one. |
      | 2 | `Queryable_in_subquery_works_when_final_projection_is_List` | Same family — `ArgumentException` where the base expects `InvalidOperationException`. |
      | 2 | `Join_with_result_selector_returning_queryable_throws_validation_error` | Classified in **A38**: `InvalidCastException` where InMemory's shaper builder happens to raise `ArgumentException`. Matching it means synthesising a backend's incidental error. |
      | 2 | `Select_projecting_queryable_in_anonymous_projection_followed_by_Join` | The A28 shape: the base asserts `QueryInvalidMaterializationType`, and the `Subquery` member never reaches the result past the `Join`, so nothing invalid is materialized. The query body is inline in a `protected static` assert helper. |
      | 2 | `Join_with_nav_projected_in_subquery_when_client_eval` | The A28 shape, unchanged. |
      | 2 | `GroupJoin_on_a_subquery_containing_another_GroupJoin_projecting_outer_with_client_method` | `NullReferenceException` where the base expects a translation failure. Undiagnosed — the last one. |
      | 2 | `Query_with_complex_let_containing_ordering_and_filter_projecting_firstOrDefault_element_of_let` | `NullReferenceException` in the residual. Undiagnosed. |
      | 2 | `Regex_IsMatch`, `Regex_IsMatch_constant_input` | **Deliberate** (A46): `Regex` is not on the allowlist, and the allowlist is ADR-008. A roadmap decision, not a fix. |
      | 2 | `Can_track_entity_with_complex_property_bag_collections(Added)` | A32's residual: fails inside EF's own `StructuralTypeMaterializerSource`. |
      | 1 | `Query_with_keyless_type` | Needs the `serverContextType` split; `InheritanceQueryInfoCarrierTest` is the worked example. |
      | 1 | `Save_optional_many_to_one_dependents` | 1 of 1787, from S3c-9. |
      | 1 | `Nullable_client_side_concurrency_token_can_be_used` | Rooted in `IMaterializationInterceptor` never running on the client; adopting `MaterializationInterceptionTestBase` is the way in. |
      | 1 | the compliance report | Not a defect; it moves as bases are adopted. **80 left.** |

      **What the remaining 80 need is a harness, not a query fix.** The next batches —
      `AdHoc*QueryTestBase` ×5, `SharedTypeQuery`, `OwnedEntityQuery`,
      `NonSharedModelBulkUpdates`, `NonSharedPrimitiveCollectionsQuery` — all derive from
      `NonSharedModelTestBase`, which builds a **different context type per test** through
      `InitializeAsync<TContext>`. `InfoCarrierTestStoreFactory` captures one `ContextType` up
      front in `SharedTestStoreProperties`, and the backend store resolves the server context from
      it. Making the server context type per-test is the piece of work that unlocks that whole
      group, and it is infrastructure rather than adoption.

- [x] **A49.** A harness for non-shared-model suites, and the five `AdHoc*` query bases.
      **`Total tests: 17281, Passed: 17092, Failed: 30, Skipped: 159`** — **145 tests added, 141 of
      them passing**; FIXED none, BROKEN 4. Unadopted bases **75**. ✅ `<this commit>`

      The infrastructure A48 named. Every other fixture has one `DbContext` type for its lifetime,
      and `InfoCarrierTestStoreFactory` captures it up front because `ITestStoreFactory`'s members
      take only a store name; a `NonSharedModelTestBase` builds a different one per test, and the
      backend store builds its **server** provider eagerly from it.
      `InfoCarrierTestStoreFactory.CreateDeferred` reads the properties at store-creation time and
      `NonSharedModelInfoCarrierHarness` supplies them from `CreateContextFactory<TContext>`, which
      EF calls before `CreateTestStore` and is where `TContext` first exists.

      A **mixin, not a base class** — the spec bases already derive from `NonSharedModelTestBase`,
      so an adopting class holds one and forwards two members. Three things it has to do that a
      shared fixture does by hand:

      - **Clear `Fixture`.** `NonSharedFixture` caches one store for the whole test class, which is
        sound for a provider whose store is a database name and wrong here, because this store
        carries a server provider built for one context type.
      - **Pass the test's `onConfiguring` to the server.** Unlike the fixture-wide `AddOptions`
        A29 deliberately withholds, this is written by the test for the one context it is about to
        build, and the server builds that same context.
        `Can_ignore_invalid_include_path_error` suppresses a warning there and asserts the query
        then runs.
      - **Copy the client context's own state per request.** A query filter may close over a
        property of the context — `MultiContext_query_filter_test` writes `context.Tenant = 1` and
        expects `e.SomeValue == Tenant` to follow. A shared fixture names those properties in its
        `CopyDbContextParameters`; with no fixture to name them in, every writable public instance
        property declared *below* `DbContext` is copied, `DbSet`s excluded.

      **Two provider defects the adoption found:**

      1. A45's entity test refused an `Include` **rooted at an interface** the entity implements,
         which EF allows. Assignability, not identity: the question is whether the root *can* be an
         entity, not whether it is spelled as one.
      2. A C# **collection expression** is a constant whose runtime type is the compiler's
         `<>z__ReadOnlyArray<T>`, which the allowlist rightly refuses (A20 reads a constant by what
         it is). So `IgnoreQueryFilters(["ActiveFilter", "NameFilter"])` was unshippable *whole*,
         only the query root travelled, and the marker sat on the client doing nothing — the server
         applied the very filters the caller had excluded, and the test read 1 row of 2. Normalized
         to a plain array before the split.

         Two attempts before it worked, both worth keeping. Testing for
         `[CompilerGenerated]` never fired — the type does not carry it. Testing only "not on the
         allowlist" fired **too often**: `OrderedEnumerable<T>` is not on it either, and turning
         one into an array threw away the ordering
         `Contains_with_local_ordered_enumerable_inline` is about. The condition that holds is an
         **unspeakable name** — the caller cannot have named it, so no round trip can be expected
         to preserve it.

      **Left red: 4.** `ThenInclude_with_interface_navigations` (a `NullReferenceException` once
      past the include check), `Collection_without_setter_materialized_correctly` (our
      `AugmentWithNavigations` cannot place `Post.Comments`),
      `Casts_are_removed_from_expression_tree_when_redundant` (`InvalidCastException` where the
      base expects `InvalidOperationException`) and
      `Double_convert_interface_created_expression_tree` (`ArgumentNullException`). Three of the
      four involve an interface-typed navigation, which is the thread to pull.

- [x] **A50.** Five more bases: owned queries, shared-type queries, shared-type complex navigations.
      **`Total tests: 18420, Passed: 18163, Failed: 98, Skipped: 159`** — **1139 tests added, 1071
      of them passing**; FIXED none, BROKEN 68, **every one of them inside the five new classes**.
      Unadopted bases **70**. ✅ `<this commit>`

      `OwnedQueryTestBase` and `ComplexNavigations*SharedTypeQueryTestBase` share fixtures the
      ordinary way; `SharedTypeQueryTestBase` and `OwnedEntityQueryTestBase` go through A49's
      harness. Every override is EF's own `*InMemoryTest`, adopted as a set (A31); the extra
      `[ConditionalTheory]`s EF adds to its InMemory classes are that store's tests, not spec base
      members, and are not carried (A47).

      Two capabilities this reaches for the first time, and one of them is largely red:

      - **A shared-type entity type** is keyed by *name*, not by CLR type: several are the same
        `Dictionary<string, object>`. Reading a value by its CLR member is not enough, and the
        model has to be consulted — which is most of what this provider's mapper does.
        `ComplexNavigations*SharedType` is **14 of 236** red.
      - **An owned entity type** has no identity of its own; it is addressed through its owner.
        `OwnedQueryTestBase` is **50 of 656** red and `OwnedEntityQuery` 4 of 12.

      **The 50 are one symptom**: the owned reference comes back `null` — `Expected: 804 S.
      Lakeshore Road, Actual: null`. The owner arrives, its owned navigation does not. That is a
      single question about whether an owned navigation counts as *loaded* on the way out
      (`ServerQueryExecutor.IsLoaded` → `HasKey`, whose key for an owned type is the owner's and
      is usually shadow), and it is A51's.

- [x] **A51.** The owned-navigation family, diagnosed. One attempt, measured, reverted.
      **`Total tests: 18420, Passed: 18163, Failed: 98, Skipped: 159`** — unchanged from A50.
      ✅ `<this commit>`

      Three probes, and the answer is neither of the two things A50 guessed at.

      **The server sends the owned reference.** `ServerQueryExecutor.IsLoaded` answers *true* for
      `OwnedPerson.PersonAddress`: the owner is untracked server-side, so the CLR path decides, the
      value is a real `OwnedAddress`, and `HasKey` already tolerates the shadow key an owned type
      has. **The client receives it**, too — a probe over `row.Properties` shows
      `PersonAddress*` present with a value on every `OwnedPerson`, `Branch`, `LeafA` and `LeafB`
      row.

      **What is missing is a row for the owned entity itself.** The same probe, placed at the top
      of the client's row walk, never prints `ROW OwnedAddress` — the owned node is decoded as a
      plain object rather than materialized as an entity. It cannot be: an **owned entity type is
      not addressable by its CLR type**, exactly as a shared-type entity is not (A45 met the same
      rule on the include check). The wire node can carry an entity-type *name* — `RebindQueryRoot`
      resolves query roots that way — and it is written from
      `ServerQueryExecutor.FindEntityType`, which is `stateManager.TryGetEntry(entity)?.EntityType`.
      **The server does not track**, so that returns null and the node carries only a CLR type the
      client cannot resolve. The two halves of the diagnosis are the same fact seen twice.

      **The attempt, and why it is reverted.** The probe turned up a second, real defect on the
      way: `IsLoaded` reads *every* navigation through `GetGetter()`, and that getter is typed for
      the navigation's target — so an owned collection reached from a derived type
      (`Branch.Orders`, declared on `OwnedPerson`) throws `InvalidCastException: Unable to cast
      HashSet<Order> to Order`. Reading the backing field instead is **measured much worse**: 98 →
      far more, including **102 `Assert.False()` failures** and a family of *"the navigation cannot
      have 'IsLoaded' set to false because the reference is set"* across the lazy-loading bases. A
      backing-field read bypasses a lazy-loading proxy, which is precisely what `GetGetter()` is
      for. Reverted; the cast still needs fixing, but through the model's own accessor.

- [x] **A52.** A navigation names the entity type its value is.
      **`Total tests: 18420, Passed: 18209, Failed: 52, Skipped: 159`** — FIXED 46, BROKEN none.
      **`OwnedQueryTestBase` is 190 of 194**, from 144. ✅ `<this commit>`

      A51's diagnosis, implemented. `DynamicValueMapper.MapToNode` resolves an entity type two
      ways, and neither can see an owned one: `FindRuntimeEntityType(clrType)` because EF names an
      owned type for the navigation that owns it (`OwnedPerson.PersonAddress#OwnedAddress`, and
      four of this model's owned types are the same `OwnedAddress`), and `_findEntityType` because
      that reads the change tracker and the server does not track a query's rows. So the owned
      value was decoded as a plain object and the navigation arrived null.

      Whoever maps a navigation's value *does* know the navigation, and therefore its target entity
      type. `ToDynamicValue` gained an internal overload carrying it, threaded through collections
      so a collection navigation's **items** get it, and consulted last — after the CLR type and
      after the tracker, so nothing that could already name itself is overridden.

      **The guard is the whole of the second attempt.** Applied unconditionally, the target entity
      type also landed on the *collection object* a collection navigation hands down first: a
      `HashSet<Order>` became an `Order`, the entity walk read `Order`'s properties off it, and
      `OwnedQuery` went 50 red to **78**. It applies only when `declared.ClrType.IsInstanceOfType(value)`.

      **Left red: 4 of 194**, and they are four different things —
      `Union_over_owned_collection`, `Skip_Take_over_owned_collection`,
      `Preserve_includes_when_applying_skip_take_after_anonymous_type_select` and
      `A tracking query is attempting to project an owned entity without a corresponding owner`.

- [x] **A53.** A44's two overrides, on the shared-type twin.
      **`Total tests: 18420, Passed: 18213, Failed: 48, Skipped: 159`** — FIXED 4, BROKEN none.
      ✅ `<this commit>`

      `ComplexNavigationsSharedTypeQueryTestBase` runs the same query bodies over a shared-type
      model, so it needs the same two client-evaluation overrides A44 mirrored onto the ordinary
      one — and EF carries them in the same two places, on
      `ComplexNavigationsSharedTypeQueryRelationalTestBase` and in its SQLite shared-type suite.
      The details clause names `ComplexNavigationsQueryTestBase<…SharedTypeFixture>`: the shared
      -type base derives from the ordinary one, and `ClientMethodNullableInt` is declared up there.

- [x] **A55.** Why the server does not track. **It does. The premise was wrong.** No code change;
      this is the finding. ✅ `<this commit>`

      A51 recorded `tracked=False` for every row on the server and read it as a defect. One probe
      in `ServerQueryExecutor.ExecuteAsync` — logging `request.TrackingBehavior`, the server
      context's `ChangeTracker.QueryTrackingBehavior` and `stateManager.Entries.Count()` — settles
      it in two runs:

      | Suite | Probe |
      |---|---|
      | `Project_multiple_owned_navigations`, `Project_owned_reference_navigation_which_owns_additional` | `req=NoTracking ctx=NoTracking entries=0` |
      | `NorthwindAsTrackingQuery` | `req=TrackAll ctx=TrackAll entries=6 / 7 / 91 / 919` |

      **The server tracks exactly when the client asks it to.** `QueryExecutor.TrackingBehaviorFinder`
      reads the marker off the query, `ExecuteAsync` sets it on the server context, and EF picks it
      up — `QueryCompilationContextDependencies.QueryTrackingBehavior` is
      `_currentContext.Context.ChangeTracker.QueryTrackingBehavior`, so there is nothing between
      the two. The owned-projection tests are `NoTracking` because **that is what the fixture asks
      for**: `OwnedQueryTestBase` asserts through `AssertQuery`, and the spec query fixtures set
      `QueryTrackingBehavior.NoTracking` on the context they hand out (`ComplexNavigationsQueryFixtureBase.CreateContext`
      is the explicit one). A51 probed a no-tracking query and found no tracking.

      **So the tracker is not a second source, and cannot be made into one.** Forcing the server to
      track regardless was the alternative on the table and it is wrong twice over: under `TrackAll`
      EF returns the *same* instance for two rows that reference one entity, which the wire then
      sends as a back-reference and the client rebuilds as one object — identity resolution leaking
      into a query that asked not to have it — and the shipped tree still carries the client's
      `AsNoTracking()` marker, so EF on the server would override the setting anyway.

      **What the second source has to be, from the same probes.** For
      `Select(p => p.PersonAddress)` the row is an `OwnedAddress` with
      `runtimeET=∅ candidates=4` — the model itself cannot disambiguate, because
      `OwnedPerson.PersonAddress`, `Branch.BranchAddress`, `LeafA.LeafAAddress` and
      `LeafB.LeafBAddress` are all that CLR type. The only thing on the server that *can* name it is
      the query, and the server has the query:

          [EntityQueryRootExpression].OrderBy(o => o.Id).Select(p => p.PersonAddress)
          [EntityQueryRootExpression].OrderBy(p => p.Id).Select(p => new ValueTuple`3(
              Item1 = p.Orders, Item2 = p.PersonAddress, Item3 = p.PersonAddress.Country.Planet))

      `p` is the root's entity type; `p.PersonAddress` is a navigation and a navigation names its
      target. That is A52's rule, applied one level higher — to the projection instead of to a
      value already in hand. A56 implements it.

- [x] **A56.** The query names what it projects.
      **`Total tests: 18420, Passed: 18217, Failed: 44, Skipped: 159`** — FIXED 4, BROKEN none.
      **`OwnedQueryTestBase` is 192 of 194.** ✅ `<this commit>`

      A55's finding, implemented. `ProjectionShape.Of(query)` reads the rebound tree on the server
      and answers, for the row it is about to map, which entity type it is — and for a constructed
      row, which entity type each member is. A query root is its entity type; a member access on an
      entity type that names a navigation is that navigation's target; `Select`/`SelectMany`/`Join`
      bind their parameters and re-shape; every other operator that keeps the element type keeps the
      shape. The result is threaded as `declared` exactly where A52 put the navigation's target
      entity type, so the two sources meet at one site and are consulted in the same place, last.

          [EntityQueryRootExpression].OrderBy(o => o.Id).Select(p => p.PersonAddress)
          [EntityQueryRootExpression].OrderBy(p => p.Id).Select(p => new ValueTuple`3(
              Item1 = p.Orders, Item2 = p.PersonAddress, Item3 = p.PersonAddress.Country.Planet))

      Both shapes above are what the two fixed tests ship. The first is a bare row; the second is a
      **re-carried** projection, so the members are named `Item1…` on a `ValueTuple` whose
      constructor parameters are `item1…` — the member map is `OrdinalIgnoreCase` for that reason,
      which is also how `RehydrateObject` reads them back.

      **Resolution is partial on purpose.** Anything unrecognised yields `null`, which leaves the
      mapper exactly where it was, and a resolved answer still only applies when the value really is
      an instance of the named type (A52's guard). So the failure mode of a wrong shape is the
      previous behaviour, not a mislabelled row — which matters, because the alternative source
      considered here was making the server track regardless of what the client asked, and *that*
      fails loudly and far away (A55).

- [x] **A57.** A `let` holding a subquery is an intermediate, so it travels materialized.
      **`Total tests: 18420, Passed: 18217, Failed: 44, Skipped: 159`** — FIXED 4, BROKEN 4.
      **The count did not move and the change is still right; the argument is below.**
      ✅ `<this commit>`

      **The defect.** `Complex_query_with_let_collection_projection_FirstOrDefault` died on the
      server with `ArgumentException: Argument type 'List<string>' does not match the corresponding
      member type 'IQueryable<string>'`, raised inside
      `InMemoryProjectionBindingExpressionVisitor.VisitNew`. EF's InMemory suite passes that test —
      only SQLite overrides it, and for `ApplyNotSupported` — so this is a gap, not a limitation.

      Two probes, because the first guess was wrong. The carrier is **not** the transparent
      identifier rewrite's: a probe in `TransparentIdentifierRewriter.Rewrite` prints
      `carriers=[] element=` for this query. `ProjectionRewriter` builds it, and a second probe over
      its fragments prints the whole answer:

          frags=[Level2:False:l2 | IQueryable`1:False:<subquery>]   ← IsQueryableCollection = False

      `Materialized` is gated on `consumed.Contains(fragment)` and the `let`'s value goes *straight
      into* the transparent identifier `new { l2, innerL1s }` — no operator touches it in that
      lambda. The operator that reads it (`ti => ti.innerL1s.ToList()`) is one level **up**, and
      this pass runs innermost-first, so at the moment of decision it has not been seen.

      **The fix is to ask the whole tree.** `MemberReadCollector` collects every `(DeclaringType,
      Name)` the query reads, once, before any rewriting; a slot of a constructed row counts as
      consumed when its member is read anywhere. By declaring type and name rather than by
      `MemberInfo`, because a `NewExpression`'s recorded member and the `MemberExpression` reading it
      back need not be the same reflection object.

      **The trade, stated plainly.** Four tests turn red — `Complex_query_with_let_collection_SelectMany`
      on both models — and every one of them is an `AssertInvalidMaterializationType` test: the base
      asserts that EF *refuses* the query, and with this change we answer it. The measurement is
      unambiguous that we answer it **correctly**: the failure is `Assert.Throws() Failure: No
      exception was thrown`, which is only reachable after the inner `AssertQuery` has compared every
      row. So the suite total is unchanged and the capability is strictly larger — we no longer
      refuse a query EF answers, and we additionally answer four EF refuses. Those four join the A28
      family already in the table.

      **What was not done, and why.** A discriminator does exist that would have kept all eight
      green: materialize only when the element type is not an entity type. It is arbitrary — there is
      no reason a collection of `string` should behave differently from a collection of `Level1` —
      and inventing a rule to move a number is the thing this plan keeps a *Do not repeat* list for.

- [x] **A58.** `Comparison_with_value_converted_subclass`, diagnosed end to end. No code change;
      this is the finding. ✅ `<this commit>`

      One of the four wrong answers, and now fully understood. Three probes — the captured tree,
      the residual, and every `ConstantExpression`'s declared *and* runtime type:

          CAPTURED  [EntityQueryRootExpression].Where(f => (f.ServerAddress == Convert(127.0.0.1, IPAddress)))
          RESIDUAL  server0 => server0.Where(f => (f.ServerAddress == Convert(127.0.0.1, IPAddress)))
          TREE      [EntityQueryRootExpression]
          CONSTS    decl=ReadOnlyIPAddress rt=ReadOnlyIPAddress v=127.0.0.1

      **The whole `Where` falls to the client and nothing says so.** `IPAddress.Loopback` is not an
      `IPAddress`: it is `System.Net.IPAddress+ReadOnlyIPAddress`, an internal subclass — which is
      what the test's name has been saying all along. The model maps `Faction.ServerAddress` as
      `IPAddress`, so `IPAddress` is on the allowlist and `ReadOnlyIPAddress` is not; the constant
      cannot be named on the wire, the predicate is unshippable, and the split leaves it in the
      residual. On the client `==` between two `IPAddress` references is **reference equality** —
      `IPAddress` overrides `Equals` but declares no `operator ==` — so nothing matches and the
      answer is `Expected: 1, Actual: 0`.

      EF's InMemory suite passes this test (only SQLite overrides it, and only to assert SQL), so it
      is a gap.

      **The fix is two independent halves, and neither alone is enough.**

      1. *Naming.* `Convert(Constant(v, ReadOnlyIPAddress), IPAddress)` should travel as
         `Constant(v, IPAddress)`. A20's "a constant is mapped by what it *is*" needs the corollary
         that what a value *is* may be a type no one can name, and the tree already says which base
         it stands for. That is a small normalizer.
      2. *Serializing.* Naming it is useless on its own. `DynamicValueMapper` would then reach an
         `IPAddress` with no `IProperty` in hand, fall through to the reflective object shape, and
         (A19's finding) `IPAddress.ScopeId` throws `SocketException` for an IPv4 address — and the
         reverse path has no constructor to match, since `IPAddress`'s take `long`/`byte[]`. A34's
         rule says such a value travels as its JSON form **off the type mapping**, and the mapping
         hangs off the property. The property is right there structurally — it is the other side of
         the comparison, which is how EF infers a constant's type mapping too — but the mapper has
         no expression context, so somebody has to carry it in.

      **What is not the fix.** Widening `ClientCodeFinder.VisitBinary` — which today refuses
      reference equality only between types the allowlist does not know — to refuse it for any type
      lacking `operator ==` would turn this wrong answer into an honest translation failure. That is
      strictly better than answering `0`, and it is ADR-010's own line. It is *not* done here because
      it does not fix the test, its blast radius covers every client-side entity comparison, and it
      would be a wash on the count at best. Recorded so the option is not re-derived.

- [x] **A59.** Four more bases: `Updates`, `MusicStore`, and the two `ConcurrencyDetector` halves.
      **`Total tests: 18498, Passed: 18269, Failed: 70, Skipped: 159`** — **+78 tests, +52 passing**,
      26 new red, nothing previously green broken. Unadopted bases **70 → 66**. ✅ `<this commit>`

      Every fixture is EF's own InMemory one with the store factory swapped, including
      `MusicStoreInMemoryTest`'s `EnsureDeleted` transaction shim, `UpdatesInMemoryTestBase`'s
      reseed-after-transaction and its `#29875` override, and
      `ConcurrencyDetectorDisabledInMemoryTest`'s `EnableThreadSafetyChecks(false)` that *replaces*
      rather than extends the base options.

      **The 26, classified. Three unrelated causes, and two of them are real provider defects.**

      | Count | Family | Reading |
      |---:|---|---|
      | ~~14~~ 0 | every `ConcurrencyDetectorDisabledInfoCarrierTest` method | **A provider defect, fixed in A60.** *"A second operation was started on this context instance"* — thrown by `ConcurrencyDetector.EnterCriticalSection`, which `QueryExecutor` calls unconditionally. `QueryContext.ConcurrencyDetector` is never null in EF 10; every provider gates the call on `ICoreSingletonOptions.AreThreadSafetyChecksEnabled` instead, and this one did not. |
      | ~~7~~ 2 | `UpdatesInfoCarrierTest` concurrency-token and partial-update methods | **Four fixed by A72** (a `byte[]` token's two values are the same array and the definition was written into the payload decoded *second*) and **one by A73** (a partial update wrote every column). The 2 left both raise `UpdateConcurrencyException` where the base wants `UpdateConcurrencyTokenException`. |
      | ~~5~~ 0 | `MusicStoreInfoCarrierTest` cart and catalogue counts | **Fixed in A75.** Not a provider defect — a fixture one. EF's shim ends a "transaction" with `context.Database.EnsureDeleted()`, and on this provider that is the *client* context, which has no database. The backing store keeps every cart item from the previous test and the counts accumulate. Remoting `EnsureDeleted` is a roadmap question (there is no DDL on the wire), not a plan one. |

- [x] **A60.** `EnableThreadSafetyChecks(false)` is answered by the provider, not by the detector.
      **`Total tests: 18498, Passed: 18283, Failed: 56, Skipped: 159`** — FIXED 14, BROKEN none.
      **`ConcurrencyDetectorDisabledTestBase` is 16 of 16.** ✅ `<this commit>`

      A59's largest family, and a one-line reading of EF's source settles it.
      `QueryContext.ConcurrencyDetector` is `Dependencies.ConcurrencyDetector` and is **never
      null** — it throws whenever it is re-entered, whatever the option says. Nothing about the
      option reaches it. What reads the option is the *provider*:
      `InMemoryShapedQueryCompilingExpressionVisitor` and
      `RelationalShapedQueryCompilingExpressionVisitor` each hold
      `dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled` and emit the
      `EnterCriticalSection` call only when it is set.

      So `QueryExecutor` reads the same flag once, in its constructor, and all three critical
      sections — the synchronous round trip, the asynchronous one, and Z1's per-row section over the
      residual — go through one `CriticalSection()` returning `IDisposable?`. A `using` over `null`
      is a no-op, so the shape of the code is unchanged.

      Worth noting what this was *not*: not a missing service registration, and not something
      `ConcurrencyDetectorEnabledTestBase` could ever have caught — that half was 16 of 16 from the
      moment it was adopted, because a provider that ignores the option looks exactly right until
      somebody turns it off.

- [x] **A61.** `DataAnnotation` and `MaterializationInterception`.
      **`Total tests: 18621, Passed: 18380, Failed: 82, Skipped: 159`** — **+123 tests, +97
      passing**, 26 new red, nothing previously green broken. Unadopted bases **66 → 63**.
      ✅ `<this commit>`

      `DataAnnotationInfoCarrierTest` is **95 of 95**. It is mostly a model-building suite, so it is
      a direct check that the client model this provider builds is EF's — conventions run on the
      client. Its six overrides are EF's own `DataAnnotationInMemoryTest`'s, each replacing a
      store-enforced constraint with the metadata assertion, because the backing store is InMemory
      and enforces none of them. **One flag differs from EF's and deliberately:**
      `HasForeignKeyIndexes` is `true` here where InMemory answers `false` — this provider keeps
      `ForeignKeyIndexConvention`, and A47 measured what happens when the flag lies (136 failures).

      **`MaterializationInterceptionInfoCarrierTest` is 2 of 28, and that is the point of adopting
      it.** The `OptimisticConcurrency` singleton this residual has carried since A31 was one test
      of a family; here the family is visible, and it splits in two:

      | Count | Symptom | Reading |
      |---:|---|---|
      | 16 | `Assert.Same() Failure: Values are not the same instance` and `Assert.All()` over the instances a query returned | **The gap itself.** This provider does not materialize through EF's shaper — `ClientResultMaterializer` builds rows from the wire — so `IMaterializationInterceptor` never runs on the client. Nothing about it is accidental; closing it means giving the materializer the interceptor pipeline EF's shaper has. |
      | 10 | *"A call was made to 'AddInterceptors', but Entity Framework is not building its own internal service provider"* | **Wiring, not capability — but not the cheap half either.** Diagnosed in **A71**: the client is clean (the base passes `useServiceProvider: false` for exactly this case); the context that throws is the **server's**, whose options pin `UseInternalServiceProvider` and then apply the forwarded `onConfiguring`. The fix EF's message prescribes is blocked by `CoreOptionsExtension.WithInterceptors` concatenating rather than replacing. |

- [x] **A62.** The three data-type bases: `BuiltInDataTypes`, `ConvertToProviderTypes`,
      `CustomConverters`.
      **`Total tests: 18743, Passed: 18497, Failed: 83, Skipped: 163`** — **+122 tests, +117
      passing**, **1** new red. Unadopted bases **63 → 60**. ✅ `<this commit>`

      The best return of any batch so far, and the least eventful. `BuiltInDataTypes` is **30 of
      30**, `ConvertToProviderTypes` **32 of 32**, `CustomConverters` **55 of 56** with EF's own
      four `Issue#17050` skips. Every capability flag and every override is EF's
      `…InMemoryTest`'s, because all of them describe the *backing store*: strict equality, no
      ANSI, no binary keys, a case-sensitive string comparison, and the non-composed `GroupBy`.

      Worth saying why this matters more than the count. `BuiltInDataTypes` writes and reads back
      every primitive the CLR has, which is `PrimitiveCoercion`'s whole subject;
      `ConvertToProviderTypes` does it again with a converter on every property, which is A19's
      rule applied to all of them at once rather than to the one that exposed it. That the two
      came up clean on the first run is the strongest evidence so far that the wire format is
      right, and it is evidence this suite did not previously have.

      The one failure, `Composition_over_collection_of_complex_mapped_as_scalar`, is the A28 shape
      again: the base asserts `CoreStrings.TranslationFailed` for an anonymous-type projection over
      a collection mapped as a scalar, and the projection split answers it instead.

- [x] **A63.** `JsonTypesTestBase` — the largest single base adopted so far.
      **`Total tests: 19317, Passed: 19043, Failed: 111, Skipped: 163`** — **+574 tests, +546
      passing**, 28 new red. Unadopted bases **60 → 59**. ✅ `<this commit>`

      Every mapped type written and read back through its `JsonValueReaderWriter`, which is the
      exact mechanism A34 made this provider's fallback for a value the wire has no primitive for.
      546 of 574 first time.

      | Count | Family | Reading |
      |---:|---|---|
      | 26 | every spatial type, `Point` through `Polygon`, plain and `_as_GeoJson` | **No spatial support.** `InfoCarrierTypeMappingSource` maps no `NetTopologySuite` type, so the model does not build: *"The 'Point' property 'PointType.Point' could not be mapped…"*. Consistent with `SpatialQueryTestBase` and `SpatialTestBase` being on the do-not-adopt list. |
      | 2 | `Can_read_write_decimal_JSON_values(0.0)` and `(1.1)` | ~~A real wire defect~~ — **wrong, corrected in A64: this is a machine-locale artifact and not a defect at all.** |

      **EF's eight spatial overrides are deliberately not adopted, and that is the point worth
      recording.** They assert `NullReferenceException` — InMemory maps the spatial type and then
      fails writing it as JSON. This provider raises `InvalidOperationException` one step earlier,
      because it never maps the type at all. Copying the override made all eight fail with
      *"Exception type was not an exact match"*, which is a worse answer than not overriding: it
      hides the real reason behind a borrowed one. A39's rule runs in this direction too — an
      override is only worth having when the reason behind it is *ours*.

- [x] **A64.** The two decimal failures are the *machine's locale*, not a defect. A63's table
      corrected. No code change. ✅ `<this commit>`

      A63 classified `Can_read_write_decimal_JSON_values("0.0")` and `("1.1")` as "a real wire
      defect — a `decimal` written through its JSON form comes back as the string it was written
      as". That was wrong, and the stack said so if read properly: **there is not one InfoCarrier
      frame in it.** It is `MethodBase.Invoke` refusing to bind a `String` argument to a `decimal`
      parameter — xUnit's own theory-data conversion, before any provider code runs.

      Why only those two of the four parameterizations, when
      `-79228162514264337593543950335` passes: those two are the only ones containing a `.`. A
      three-line program settles it —

          culture=en-SE  sep=','
          Convert.ChangeType("1.1", typeof(decimal)) → FormatException: The input string '1.1' was not in a correct format.

      **The .NET runtime culture on this machine is `en-SE`, whose decimal separator is a comma.**
      xUnit converts `InlineData` strings with the current culture, fails, and passes the raw
      string through. EF's own suite fails these two here for exactly the same reason. The clue was
      in plain sight all session: `dotnet` prints `Restored … (in 5,26 sec)`.

      **What this means for every measurement in this repo.** These are the only four occurrences
      of the signature in the full run — grep `artifacts/measure/<label>.log` for
      *"cannot be converted to type"* to check — so nothing else is affected today. But the suite
      total is **locale-dependent**, and a run on a machine with a `.` decimal separator will report
      **two fewer failures** with no code change. That is the one shape of difference that is not
      flakiness and must not be chased as such.

      Recorded as its own step rather than edited quietly into A63, because the mistake is the
      useful part: a failure whose stack contains no code of yours is not your failure, and the
      count that "looks about right" is the one worth reading the stack for.

- [x] **A65.** `MonsterFixup` and `BadDataJsonDeserialization`.
      **`Total tests: 19355, Passed: 19073, Failed: 119, Skipped: 163`** — **+38 tests, +30
      passing**, 8 new red. Unadopted bases **59 → 57**. ✅ `<this commit>`

      **`MonsterFixupInfoCarrierTest` is 12 of 12, and it is the result worth reporting.** The
      monster model is EF's largest — every relationship kind at once, seeded three separate ways
      (by foreign key, by navigation, and by both) and then verified from every end. This provider
      reassembles that graph from the wire rather than through EF's shaper, so a relationship that
      fixes up in one direction and not the other is exactly the defect a smaller base cannot see.
      It came up clean on the first run.

      The context type is named explicitly rather than read from the fixture:
      `MonsterFixupFixtureBase` derives from `ServiceProviderFixtureBase`, which has no
      `ContextType` and states its context through `CreateContext(DbContextOptions)` instead. The
      three `ValueGeneratedOnAdd` calls are EF's own InMemory ones and describe the backing store
      (S3c-8: InMemory generates those keys at `Add` time).

      `BadDataJsonDeserializationInfoCarrierTest` is **18 of 26**, and all 8 failures are
      `Throws_for_bad_point_as_GeoJson` — **the same absent spatial mapping A63 found**, arriving
      as *"The 'Point' property … could not be mapped"* before any JSON is read. No new
      information, and no store is involved: the base builds a model and reads JSON directly, so
      the client is configured through `InfoCarrierTestHelpers.UseProviderOptions`, whose client
      throws if anything ever reaches it.

      **`SeedingTestBase` was attempted in this batch and is not adoptable as it stands.** Its
      `SeedingContext` takes a `string testId` and has no `DbContextOptions` constructor, so the
      backend store — which registers the context type with `AddDbContext` — cannot build the
      server's copy. Adopting it needs either a second constructor on our side or a store that can
      be handed a context *factory*; it is not two forwarded members.

- [x] **A66.** The F1 model on Tier A: `Serialization` and `DataBinding`.
      **`Total tests: 19419, Passed: 19137, Failed: 119, Skipped: 163`** — **+64 tests, +64
      passing, nothing red.** Unadopted bases **57 → 55**. ✅ `<this commit>`

      `SerializationInfoCarrierTest` **6 of 6**, `DataBindingInfoCarrierTest` **58 of 58**, first
      run, no overrides of any kind.

      Both needed a Tier A `F1InfoCarrierFixture`, which did not exist. `F1FixtureBase` builds its
      model *externally* and applies it with `UseModel` — deliberately, as EF's regression coverage
      for doing so — and `F1Context` has no `OnModelCreating`, so the `OnModelCreating` route every
      other store here takes would leave the server with a bare convention model. The server is
      handed its own copy, built over `InMemoryConventionSetBuilder`'s conventions rather than this
      provider's, plus the `UseSeeding`/`UseAsyncSeeding` pair and
      `F1MaterializationInterceptor`. It is the Tier A twin of
      `OptimisticConcurrencyInfoCarrierTest.InfoCarrierFixture` and much shorter, because an
      InMemory server needs none of the table mappings SQLite's needs.

      Worth noting what came up clean. `Serialization` walks a *tracked graph* with
      `System.Text.Json` and Newtonsoft — a navigation still pointing at a half-built instance
      surfaces there as a cycle, and `ClientResultMaterializer` builds every one of those entities
      itself. `DataBinding` reads `Local` and the binding lists straight off the change tracker,
      which this provider populates by hand. Neither had been exercised before.

- [x] **A67.** The two interception bases: `SaveChangesInterception` and `QueryExpressionInterception`.
      **`Total tests: 19547, Passed: 19253, Failed: 131, Skipped: 163`** — **+128 tests, +116
      passing**, 12 new red. Unadopted bases **55 → 52** (`InterceptionTestBase` comes with them).
      ✅ `<this commit>`

      **`SaveChangesInterception` is 112 of 112.** `ISaveChangesInterceptor` runs on the *client*
      context, whose `SaveChanges` is a wire call rather than a store write, so this is a direct
      check that remoting the save did not move it out from under EF's own interception points. It
      did not — every one of the 112, across both the diagnostic-listener and plain fixtures.

      **`QueryExpressionInterception` is 4 of 16, and the 12 are one real gap.**
      `Assert.Same() … Actual: null` out of `AssertNormalOutcome`: the interceptor's `Context` is
      never set, i.e. **`IQueryExpressionInterceptor` does not run on the client**. That is the
      query-side twin of the `IMaterializationInterceptor` gap A61 exposed, and it is the more
      surprising of the two — the client's query compiler is EF's own, and ADR-006 captures at
      `IDatabase.CompileQuery`, which is downstream of where EF raises
      `QueryCompilationStarting`. Undiagnosed; it is the next thing to look at in this family.

      **One fixture problem was real and is worth recording, because the first fix was wrong.**
      `InterceptionTestBase` seeds through `SeedAsync` on *every* `CreateContextAsync`, and its
      tests insert rows with fixed keys. That is sound for every other provider because
      `Fixture.CreateOptions` builds a **fresh internal service provider per call** and an InMemory
      database is rooted in that provider — each test genuinely gets an empty store. Here the
      client's provider is fresh but the *server* is the fixture's one store, which persists, so
      the second test collided: *"An item with the same key has already been added. Key: 77"*, 62 of
      112 and 12 of 16.

      Cleaning the store first fixed `SaveChanges` outright. It did **not** fix
      `QueryExpression` — a probe printing the row count either side of
      `Fixture.TestStore.CleanAsync` shows `before=2 after=2`, so that path does not empty this
      store, and had the probe not been written the 12 would have been read as the same collision
      they started as. Seeding idempotently instead is what made the real failure visible. **The
      store-clean path not clearing is a loose end of its own**, unrelated to interception.

- [x] **A68.** The capture point raises EF's query-compilation events.
      **`Total tests: 19547, Passed: 19265, Failed: 119, Skipped: 163`** — FIXED 12, BROKEN none.
      **`QueryExpressionInterceptionTestBase` is 16 of 16.** ✅ `<this commit>`

      A67's gap, and the cause is structural rather than a bug.
      `CoreEventId.QueryCompilationStarting` — and with it every `IQueryExpressionInterceptor` — is
      raised by EF from `QueryCompilationContext.CreateQueryExecutorExpression`, which **this
      provider never reaches**: ADR-006 takes the tree at `IDatabase.CompileQuery`, upstream of
      EF's whole translation pipeline. So nothing raised it, and no interceptor ran.

      `CompileQuery` now raises it, and it is **not** merely a log line: the interceptor's return
      value *is* the query, which is what `Intercept_to_change_query_expression` asserts. It is
      raised before the element-type and single-result questions below it, so an interceptor that
      replaces the tree is answered about its own tree rather than the caller's.

      `CoreEventId.QueryExecutionPlanned` follows it, because the pair is observable: the
      diagnostic-listener fixture asserts the two arrive **in order**, and only that fixture's four
      tests fail without it. EF raises it from the same method's `finally` over the executor
      expression it built. There is no executor expression here, and the query itself is the honest
      answer to what was planned — this provider's plan *is* the tree it is about to ship.

      Two events, twelve tests, and a whole base green.

- [x] **A69.** `ProxyGraphUpdatesTestBase`, all three proxy flavours.
      **`Total tests: 21257, Passed: 20927, Failed: 128, Skipped: 202`** — **+1710 tests, +1662
      passing**, 9 new red. Unadopted bases **52 → 51**. ✅ `<this commit>`

      The largest adoption of the session by an order of magnitude, and **1662 of 1671 pass on the
      first run that got past the fixture.** It is the `GraphUpdates` corpus — reparenting,
      severing, cascading a whole graph — over entities that are *proxies*, in the lazy-loading,
      change-tracking and both-at-once flavours. `LazyLoadProxyInfoCarrierTest` covers the loading
      half and `GraphUpdatesInfoCarrierTest` the saving half; this is the only place they meet.
      Structure and the 39 skips are EF's own `ProxyGraphUpdatesInMemoryTest`'s (issues #2166,
      #3924).

      **The fixture needed one thing no previous one did, and the first cut got 1671 of 1671
      wrong.** The seed builds its graph out of `context.CreateProxy<Root>()`, and it runs on the
      **server's** context — `InfoCarrierTestStore.InitializeAsync` deliberately ignores the
      fixture's context factory, because the backend owns the real store. So proxies had to be
      enabled on *both* sides, `UseLazyLoadingProxies`/`UseChangeTrackingProxies` through
      `onAddOptions` and `AddEntityFrameworkProxies` through `onAddServices`, or the seed died with
      "Unable to create proxy for 'Root' because proxies are not enabled" before a single test ran.
      Each flavour states its proxies once, in `AddProxyOptions`, and the base applies them to
      client and server alike. **This is the first fixture here whose *seed* needs a client-side
      feature**, and the shape is worth remembering for the next one.

      **The 2 left are a category nothing had named before: a spec test that branches on
      `context.Database.ProviderName`.** `Save_two_entity_cycle_with_lazy_loading` reads

          if (context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory") { … }
          else { Assert.Throws<InvalidOperationException>(…CircularDependency…); }

      and this provider's name is **not its backing store's** — it is
      `InfoCarrier.Core`, whatever it is running over. So a store-specific branch always takes the
      *other* path: the test asks for a circular-dependency refusal that only a store with real
      insert ordering raises, while the actual store is the InMemory one that resolves the cycle.
      The assertion is correct about InMemory and cannot see that InMemory is what it is talking to.

      Not overridable without copying the thirty-line body (A28's rule), and not fixable by
      renaming the provider. **Every base that branches on `ProviderName` will do this**, which is
      worth knowing before the next adoption that comes up short by exactly one or two tests.

      (The other 7 were `Optional_many_to_one_dependents_are_orphaned_starting_detached`, fixed in
      A74 — a reseed, not a defect.)

- [x] **A70.** `ConferencePlanner` and `AdHocComplexTypeQuery`; **`FunkyDataQuery` attempted and
      reverted.**
      **`Total tests: 21285, Passed: 20946, Failed: 137, Skipped: 202`** — +28 tests, +19 passing,
      9 new red. Unadopted bases **51 → 49**. ✅ `<this commit>`

      `ConferencePlannerInfoCarrierTest` is **19 of 24**. The second application-shaped suite after
      `MusicStore` and the more useful one: every test is a controller action — load, project into
      a DTO, mutate, save — against a context per operation, which is the shape a real caller of
      this provider writes. The 5 left are `Sequence contains no elements` (×2) and three
      `Assert.All`/`Assert.Equal` count mismatches, all in the `SessionsController` family.

      `AdHocComplexTypeQueryInfoCarrierTest` was **0 of 4** — ~~complex-type equality in a predicate
      does not work~~ **wrong, corrected in A77: the InMemory provider does not translate complex
      property access at all, and the class is reverted.**

      **`FunkyDataQueryTestBase` is not adoptable on Tier A, and this is the evidence.** Adopted, it
      came up **2 of 38**, with 34 of the failures reading
      `ArgumentNullException: Value cannot be null. (Parameter 'value')` from
      `String.EndsWith(null, StringComparison)` — thrown **inside EF's own InMemory provider**,
      three frames below `ServerQueryExecutor`. The corpus is built out of nulls and wildcards
      precisely to break predicate translation, and the InMemory provider client-evaluates those
      operators without EF's null-guarding. That is why **EF ships no InMemory counterpart for this
      base** — the same signal that keeps `StoreGeneratedTestBase` off the list. Reverted rather
      than committed: 36 red tests that report the backing store's behaviour say nothing about this
      provider, and the guardrail about leaving spec tests red is about tests that *tell* you
      something.

- [x] **A71.** `MaterializationInterception`'s ten wiring failures, diagnosed. **Attempted,
      measured, reverted.** No code change; this is the finding. ✅ `<this commit>`

      A61 called these "wiring, and the cheaper half to fix". The first half of that is right and
      the second is not.

      **What they are.** `SingletonInterceptorsTestBase.CreateContext` passes
      `useServiceProvider: inject` — so when the test supplies interceptors through
      `AddInterceptors` (`inject: false`) the *client* deliberately has **no** internal service
      provider, and `NonSharedModelTestBase.ConfigureOptions` calls
      `EnableServiceProviderCaching(false)` instead. The client is therefore clean. The context that
      throws is the **server's**: `InfoCarrierBackendTestStore.AddProviderOptions` pins
      `UseInternalServiceProvider(ServiceProvider)` and *then* applies the forwarded `onConfiguring`
      (A49 forwards it on purpose), and EF refuses that pairing by name.

      **The attempt, and the trap that killed it.** EF's own message names the fix — "build the
      `ISingletonInterceptor` services to use into the service provider before passing it" — so the
      attempt read the interceptors off a throwaway builder, registered them as singletons in the
      server's collection, and removed them from the options with
      `CoreOptionsExtension.WithInterceptors(kept)`. A probe confirms the first two steps work:
      `onAddOptions=True singletons=7`. The count did not move.

      **`WithInterceptors` concatenates, it does not replace** —
      `clone._interceptors = _interceptors == null ? interceptors : _interceptors.Concat(interceptors)`.
      So the "strip" appended, and there is **no public API on `CoreOptionsExtension` that removes
      an interceptor**; `Clone` is protected. Without that, the only routes left are dropping
      `UseInternalServiceProvider` for the server — which would give every server context a fresh
      InMemory root and lose the store between requests — or not forwarding `onConfiguring` at all,
      which A49 added for a reason.

      Reverted rather than committed. The 10 stay red next to the 16 that are the real
      `IMaterializationInterceptor` gap.

- [x] **A72.** A wire reference may only point backwards in *decode* order.
      **`Total tests: 21285, Passed: 20950, Failed: 133, Skipped: 202`** — FIXED 4, BROKEN none.
      **`UpdatesTestBase` is 25 of 28**, from 21. ✅ `<this commit>`

      A59's *"Dangling wire reference 1: no value with that id has been materialized"*, and it is a
      two-line reordering once seen.

      A `byte[]` concurrency token is **not a wire primitive**, so it travels as a referenceable
      object: the first mapping of an instance defines it, every later one is a back-reference.
      When the token has not been changed the current and original values are the **same array** —
      which is precisely what `..._original_value_matches_does_not_throw` sets up — so exactly one
      of the two is a `Ref`.

      `ChangeEntryMapper.ToChangeEntry` mapped the original **first**, so the definition landed in
      `SerializedOriginalValues` and the reference in `SerializedValues`. Those are two separate
      payloads, decoded independently, and the server decodes `SerializedValues` at the top of
      `ExecuteAsync` and `SerializedOriginalValues` three hundred lines later, after the state is
      set — deliberately, because setting the state re-snapshots originals. So the current values
      arrived holding a reference to a value nobody had materialized yet.

      Mapping the current value first puts the definition where it is read first. **The rule this
      states, and which nothing wrote down before: a wire reference may only point backwards in
      the order the payloads are *decoded*, which is not the order they are written in a
      `ChangeEntry`.**

      It also fixed the two `..._mismatch_throws` siblings, which had a different array and so a
      different symptom — the token check ran against a value that never arrived.

- [x] **A73.** A partial update writes only the properties the client changed.
      **`Total tests: 21285, Passed: 20951, Failed: 132, Skipped: 202`** — FIXED 1, BROKEN none.
      **`UpdatesTestBase` is 26 of 28.** ✅ `<this commit>`

      One test, and a real hole in the wire protocol. A partial update is ordinary EF: attach a
      stub carrying only the key, set one property, mark that one modified, save.
      `Save_partial_update` does exactly that and expects `Name` to still read "Apple Cider"
      afterwards; it read `null`.

      **Which properties are modified is change-tracker state and does not follow from the
      values** — every property's value is on the wire either way — so nothing carried it. The
      server set `State = Modified`, EF marked *every* property modified, and the untouched columns
      were written from the stub.

      `ChangeEntry` gained `ModifiedProperties`, filled from `IUpdateEntry.IsModified` for a
      `Modified` entry only (on an `Added` one EF reports all of them and on a `Deleted` one none,
      neither of which is information). The server applies it **after** setting the state, because
      that is what marked them all, and skips key properties — EF refuses to be told a key is
      modified on a tracked entry, and `State = Modified` leaves keys alone anyway.

      Measured across the whole `GraphUpdates`/`ProxyGraphUpdates` corpus (3,500 saves) with
      nothing broken, which is the point of measuring a change in this path at all.

      **Left red: 2**, `…_on_concurrency_token_original_value_mismatch_throws` for `Save_partial`
      and `Remove_partial`. Both raise EF's `UpdateConcurrencyException` ("with the key value")
      where the base wants `UpdateConcurrencyTokenException` ("on the concurrency token") — the
      store cannot find the row rather than finding it with a stale token. Undiagnosed.

- [x] **A74.** `ProxyGraphUpdates` reseeds through the backend.
      **`Total tests: 21285, Passed: 20958, Failed: 125, Skipped: 202`** — FIXED 7, BROKEN none.
      **`ProxyGraphUpdatesTestBase` is 1669 of 1671.** ✅ `<this commit>`

      A69's seven-parameterization family, and not a provider defect at all. The test opens with
      `Assert.Equal(2, root.OptionalChildren.Count())` and read **4** — the seed had run twice.

      `SharedStoreFixtureBase.ReseedAsync` cleans and reseeds through a *client* context.
      `GraphUpdatesInfoCarrierTest` has overridden that since S3c to go through the **backend**,
      because the initial seed runs server-side and a reseed that went through the client would
      make every test's setup depend on remoted `SaveChanges` — the thing under test. A69 adopted
      the proxy variant without carrying that override, so the clean did not reach the store and
      the seed accumulated.

      Same override, same five lines. **Every new fixture over a mutable store needs it**, and the
      symptom is arithmetic: a count that is an exact multiple of the seeded one.

- [x] **A75.** `MusicStore` empties the backend, not the client.
      **`Total tests: 21285, Passed: 20963, Failed: 120, Skipped: 202`** — FIXED 5, BROKEN none.
      **`MusicStoreTestBase` is 18 of 18.** ✅ `<this commit>`

      A59's five, and A74's rule again from the other direction. EF's
      `MusicStoreInMemoryTest` ends a "transaction" with `context.Database.EnsureDeleted()`; on
      this provider that is the **client** context, which has no database, so it deleted nothing
      and returned. Every test's cart survived into the next and the counts accumulated.

      The backend owns the store, so the backend is what gets emptied. Together with A74 this is
      now a rule with two witnesses: **anything a fixture does to the store — clean, delete,
      reseed — has to go through `((InfoCarrierTestStore)TestStore).Backend`, because the client
      side of those APIs is a no-op by construction.**

      `ConferencePlanner`'s 5 are *not* this — its counts come out **low**, not high
      (`Expected: 21, Actual: 20`), plus two `Sequence contains no elements` and one speaker whose
      skip-navigation came back empty. Left classified.

- [x] **A76.** `ConferencePlanner` puts the data back after each test.
      **`Total tests: 21285, Passed: 20968, Failed: 115, Skipped: 202`** — FIXED 5, BROKEN none.
      **`ConferencePlannerTestBase` is 24 of 24.** ✅ `<this commit>`

      The third witness for the same rule, and A75 was wrong to say these were "not this". They
      were the same thing seen from the other end: the base wraps every test in
      `ExecuteWithStrategyInTransactionAsync` and relies on a **real transaction rolling it back**.
      Tier A's store has no transaction, so `SessionsController_Put` left its session renamed and
      the next test looking for it got "Sequence contains no elements"; `AttendeesController_AddSession`
      counted 20 where it wanted 21 because a prior test had removed one. Counts came out *low*
      rather than high, which is why it did not look like accumulation — but the cause is identical:
      **nothing put the store back.**

      Two overrides, both already established here: reseed after the transaction helper (as
      `GraphUpdates`, `Updates` and `ProxyGraphUpdates` do) and reseed through the **backend** (A74,
      A75).

      **The rule, now with four witnesses.** A fixture over a mutable store on this provider needs
      *both*: something that restores the data between tests, and that something must go through
      `((InfoCarrierTestStore)TestStore).Backend`. Neither is optional and neither is inferable
      from the base — a base written against a store with transactions simply does not say it.

- [x] **A77.** `AdHocComplexTypeQuery` is not adoptable on Tier A either. Reverted.
      **`Total tests: 21281, Passed: 20968, Failed: 111, Skipped: 202`** — 4 red removed with the
      class; nothing else moved. Unadopted bases **49 → 50**. ✅ `<this commit>`

      A70 adopted it and classified its 0-of-4 as "complex-type equality in a predicate does not
      work". Reading the errors properly says something else: **all four are raised by EF's own
      translator on the server**, out of `QueryableMethodTranslatingExpressionVisitor.Translate`,
      and three say *"Translation of member 'ComplexContainer' on entity type 'EntityType' failed.
      This commonly occurs when the specified member is unmapped."*

      That is the InMemory provider declining to translate a complex property access at all — not
      this provider losing one. Two independent confirmations: EF's InMemory suite contains **no
      complex-type query test of any kind**, and the *two* spec bases about querying complex types
      (`AdHocComplexTypeQueryTestBase` and `Query.ComplexTypeQueryTestBase`) both lack an InMemory
      counterpart. Same signal as `FunkyDataQuery` (A70) and `StoreGeneratedTestBase`.

      **This also corrects A62's aside** that `Query.Associations.ComplexProperties` "is now
      adoptable and is not adopted". It is not, on Tier A. `ComplexTypesTrackingTestBase` is 249 of
      251 because that base *tracks* complex values; querying against them is a store capability
      this backend does not have, and no amount of work on this provider changes that. Complex-type
      queries need Tier B.

- [x] **A79.** The bases InMemory cannot host belong on **Tier B**, not in the bin.
      **`Total tests: 21470, Passed: 21094, Failed: 173, Skipped: 203`** — **+189 tests, +126
      passing**, 62 new red, nothing previously green broken. Unadopted bases **50 → 47**.
      ✅ `<this commit>`

      **A70 and A77 drew the wrong conclusion, and this step is the correction.** Both reverted a
      base after establishing that EF's InMemory provider could not host it, and both stopped
      there. ADR-009 defines a second tier *for exactly that case*, and the signal I used to
      justify the reverts — "EF's own suite does not derive from this base" — was only ever checked
      against `EFCore.InMemory.FunctionalTests`. Checked against `EFCore.Sqlite.FunctionalTests`,
      **every one of them has a counterpart**:

      | Base | Tier A | Tier B |
      |---|---:|---:|
      | `FunkyDataQueryTestBase` | 2 of 38 | **38 of 38** |
      | `AdHocComplexTypeQueryTestBase` | 0 of 4 | **4 of 4** |
      | `Query.ComplexTypeQueryTestBase` | not attempted | 88 of 151 |

      The first two are *first-run, no overrides*. `FunkyDataQuery` is a corpus about predicate
      translation and Tier A does not translate; `AdHocComplexTypeQuery` needs complex property
      access translated at all. Neither was ever about this provider — putting them on the tier
      that translates simply answers them.

      `ComplexTypeQuery` is new coverage rather than a restoration: 88 of 151, with 8 of the
      failures closed by mirroring EF's own overrides — six from
      `ComplexTypeQueryRelationalTestBase` (a subquery over a complex type, a set operation between
      two different ones) and two `ApplyNotSupported` from `ComplexTypeQuerySqliteTest`. That is
      CLAUDE.md's "grep the relational base as well as SQLite's own suite" rule paying off on a
      tier where it applies for the first time. **The remaining 62 are left red and undiagnosed on
      purpose** — two uniform families, `Values differ` (32) and `Strings differ` (30) — and are
      provider work on a capability surface that did not exist before this step.

      **The rule this replaces the reverts with:** *"EF ships no InMemory test for this base"* is a
      reason to move it to Tier B, **not** a reason to drop it. Only *"EF ships no test for it on
      any store we have"* justifies leaving a base unadopted.

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

**This section had drifted four milestones — it described M2 until 2026-08-10.** That is the exact
failure the roadmap/plan split exists to prevent, and it is worth one sentence about why it went
unnoticed: nothing reads this section on the way past. The checkboxes above are what each session
works from, so a stale heading below them costs nothing until a milestone boundary arrives, and
then it is wrong at the one moment it is consulted. **Rewrite it in the same commit that opens a
new milestone, not in the one that closes the old one.**

M6 closes when all of:

1. Relationships, owned types, table splitting, TPH/TPT inheritance covered (requirements §2.7). ✅
2. The compliance inventory fully classified — every failure attributable to a plan entry. ✅
3. `InfoCarrierComplianceTest.All_test_bases_must_be_implemented` green.

**Criterion 3 has one base left, `AdHocJsonQuery`, and only one route to it.** The roadmap's
alternative — "every remaining base in `IgnoredTestBases` with its reason" — does not apply:
that list is for bases *conceptually inapplicable* to a remoting provider, and C9 already refused
it for the spatial bases on the ground that "not built yet" does not qualify. The same refusal
binds here. **So M6 is blocked on the B12 decision** (C21), not on available work.

Then archive this doc as `docs/archive/2026-08-m6-coverage-expansion-plan.md` and rewrite it for
**M7 — SQL Server (Tier C)**, whose spatial half is already done and must not be re-planned
(C15/C17/C18).

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

- [x] **A54.** The 48, in one table. No code change. ✅ `<this commit>`

      A48's map is six entries out of date. This replaces it as the single place to look, read out
      of `artifacts/measure/a53.txt`. **48 failures over 18420 tests; 70 spec bases unadopted.**

      | Count | Family | Reading |
      |---:|---|---|
      | ~~8~~ 12 | `Select_projecting_queryable_in_anonymous_projection_followed_by_Join`, `Join_with_result_selector_returning_queryable_throws_validation_error`, **`Complex_query_with_let_collection_SelectMany`** (×2 models each) | Classified in A38/A39. The base asserts a materialization error this provider does not raise, or raises differently; the query bodies are inline in `protected static` assert helpers. The third joined them in **A57**, which chose answering a query EF refuses over refusing one EF answers. |
      | ~~4~~ 2 | ~~`Complex_query_with_let_collection_projection_FirstOrDefault`~~, `Queryable_in_subquery_works_when_final_projection_is_List` (×2 models) | **Half fixed by A57**: a `let` whose value is read anywhere in the query is an intermediate, so it travels materialized. What is left is the same A28 shape as the row above — the base asserts `QueryInvalidMaterializationType` and we raise `ArgumentException`. |
      | 4 | `GroupJoin_on_a_subquery_containing_another_GroupJoin_projecting_outer_with_client_method` (×2 models) | `NullReferenceException` where the base expects a translation failure. Undiagnosed. |
      | ~~4~~ 0 | `Project_multiple_owned_navigations`, `Project_owned_reference_navigation_which_owns_additional` | **Fixed by A56.** A52's fix reaches an owned value through the navigation that owns it; these project one directly. A55 established the tracker can never help (they are `NoTracking` by fixture) and A56 read the entity type off the query instead. |
      | 4 | `OwnsMany_correlated_projection`, `Multiple_single_result_in_projection_containing_owned_types` | EF's own guard (*a tracking query is attempting to project an owned entity without a corresponding owner*), and a tuple-carrier lambda typed `Func<…, Anonymous>` handed to `Select<…, object>`. |
      | 4 | `ThenInclude_with_interface_navigations`, `Collection_without_setter_materialized_correctly`, `Casts_are_removed_from_expression_tree_when_redundant`, `Double_convert_interface_created_expression_tree` | A49's residual. Three of the four involve an **interface-typed navigation**. |
      | 2 | `Correlated_collection_with_distinct_3_levels` | **A wrong answer** — the only one left of that kind, with `Comparison_with_value_converted_subclass`. |
      | 2 | `Comparison_with_value_converted_subclass` | **A wrong answer**, fully diagnosed in **A58**: `IPAddress.Loopback` is an internal `ReadOnlyIPAddress`, which the allowlist cannot name, so the whole `Where` stays on the client — where `==` on `IPAddress` is reference equality. Needs both halves A58 names. |
      | 2 | `Query_with_complex_let_containing_ordering_and_filter_projecting_firstOrDefault_element_of_let` | `NullReferenceException` in the residual. Undiagnosed. |
      | 2 | `Join_with_nav_projected_in_subquery_when_client_eval` | The A28 shape, unchanged since. |
      | 2 | `Regex_IsMatch`, `Regex_IsMatch_constant_input` | **Deliberate** (A46). `Regex` is not on the allowlist and the allowlist is ADR-008. A roadmap decision. |
      | 2 | `Can_track_entity_with_complex_property_bag_collections(Added)` | A32's residual: fails inside EF's own `StructuralTypeMaterializerSource`. |
      | 1 | `Query_with_keyless_type` | Needs the `serverContextType` split; `InheritanceQueryInfoCarrierTest` is the worked example. |
      | 1 | `Save_optional_many_to_one_dependents` | 1 of 1787, from S3c-9. |
      | 1 | `Nullable_client_side_concurrency_token_can_be_used` | `IMaterializationInterceptor` never runs on the client; adopting `MaterializationInterceptionTestBase` is the way in. |
      | 1 | the compliance report | Not a defect. **70 bases left.** |

      **What the 70 need.** A49's harness makes every remaining `NonSharedModelTestBase` suite
      adoptable two-members-per-class; A46 and A47 show the shared-fixture batches are mechanical.
      The ones that are *not* mechanical, and why:

      - **`Query.Associations.*` is 34 of the 70** and has no InMemory counterpart at all — a
        roadmap question (a relational tier, or explicitly out of scope), not a plan one. Complex
        types work since A32, so its `ComplexProperties` third is no longer blocked on capability.
      - **`BulkUpdates.*` (5)** — the provider implements nothing of `ExecuteUpdate`/`ExecuteDelete`.
        Same roadmap question.
      - **Six infrastructure bases** (`ApiConsistency`, `Logging`, `Scaffolding.CompiledModel`,
        `ServiceCollectionExtensions`, `ModelBuilding101`, `Spatial*`) are not worth adopting;
        `StoreGeneratedTestBase` is not adoptable at all (EF's own InMemory test does not derive
        from it).
      - Everything else — `Updates`, `MonsterFixup`, `Seeding`, `MusicStore`, `CustomConverters`,
        `ConvertToProviderTypes`, `BuiltInDataTypes`, `JsonTypes`, `DataAnnotation`, `DataBinding`,
        `Serialization`, `ProxyGraphUpdates`, the three interception bases — is an ordinary batch.
        `MaterializationInterception` is worth taking for its own sake: it is the root of the
        `OptimisticConcurrency` singleton above.
