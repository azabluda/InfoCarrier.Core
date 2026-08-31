# Implementation plan — post-10.0 work, no milestone open

Milestone-level scope lives in [`roadmap.md`](roadmap.md). Do not put scope here.

**Every milestone closed on 2026-08-24.** M5 was the last, and this file was rewritten because of
it. What follows Phase Q is issue-driven rather than milestone-driven: each section names the GitHub
issue it serves, and **which release the work lands in is not decided here** — the issues carry that,
and it may be a 10.0.x patch or a later minor. Closed milestones are archived and never edited again:

| Milestone | Plan |
|---|---|
| M5 — wire hardening (Phase P) | [`archive/implementation-plan-m5-phase-p.md`](archive/implementation-plan-m5-phase-p.md) |
| M6 — spec-base adoption (Phases A–C) | [`archive/implementation-plan-m6-phase-c.md`](archive/implementation-plan-m6-phase-c.md) |
| M8 — productization (Phases H–N) | [`archive/implementation-plan-m8-phases-h-n.md`](archive/implementation-plan-m8-phases-h-n.md) |
| M9 — provider neutrality (Phase J) | [`archive/implementation-plan-m9-phase-j.md`](archive/implementation-plan-m9-phase-j.md) |

The suite stands at `Total tests: 22662, Passed: 22476, Failed: 9, Skipped: 177`. All nine are
classified in the archived M9 plan and stated for consumers in
[`limitations.md`](../../../website/docs/limitations.md).

## Phase Q — verifying the cancellation path over HTTP

**This is verification of work that already shipped, not a milestone.** Phase P made the server use
the token it was already handed, and proved it with
`InMemorySmokeTest.The_server_stops_a_query_when_the_caller_cancels`. That test replays a request
into the in-process server. **It says nothing about HTTP, which is the only transport a user gets.**

**And a user-facing page already makes the claim.** `guide/errors.md` tells a reader that the token
reaches the server, so cancelling stops the query there. That sentence must not stand on a path
nobody has watched.

**The existing HTTP tests do not use a real web server**, which is the thing worth knowing before
planning any of this. `NorthwindServerFactory` derives from `WebApplicationFactory`, so
`CreateClient()` runs the pipeline in memory: no socket, no port, and Kestrel never runs. Anything
built on that factory tests this repository's wiring and not the web server's behaviour.

- [x] **Q1. Our wiring, on the in-memory host.** `<commit d7f3083 red, 9dc4931 green>` Prove that `MapInfoCarrier` hands
      `HttpContext.RequestAborted` down the chain rather than dropping it, and that
      `HttpInfoCarrierTransport` hands the caller's token to `HttpClient`. Both are this
      repository's own lines, and both are deterministic to assert.
- [x] **Q2. A real Kestrel host: NOT DONE, and not to be done.** Decided 2026-08-24 by the owner.
      The question it would have answered is Microsoft's rather than ours: for a POST request
      Kestrel does not always learn that the client has gone until it writes the response. Answering
      it needs a real socket and a real port, which no test here starts, and it would be the first
      timing-sensitive test in a repository that treats a flaky test as a stop-everything defect.
      **What stands instead is what Q1 measures**: the token reaches the store on the cancellable
      path, which is this repository's whole half of the problem. If Kestrel is ever slow to report
      a lost client, the effect is that cancelling frees the server later than it could, not that
      anything is wrong here.

## Phase R — the compliance gate, and the relational spec inventory (#56)

**Not a milestone.** #56 found that `InfoCarrierComplianceTest`, the test CLAUDE.md calls the current
answer to "which bases are in", could not see a single base in `EFCore.Relational.Specification.Tests`.
`ComplianceTestBase.GetBaseTestClasses` reads its own assembly, which is the core one. The gate was
answering a smaller question than it claimed, and the TPT and TPC gap that prompted the issue was
only a fraction of what it hid.

- [x] **R1. Let the gate see the relational assembly.** `<commit 935adfa>` Inherit EF's
      `RelationalComplianceTestBase`, which ships exactly that override and is what EF's own
      relational providers use. A hand-written override was tried first and deleted as a duplicate.
      `failed` 9 -> 11 and `total` 22667 -> 22668, both recorded in `test/known-failures.txt`:
      `All_test_bases_must_be_implemented` turns red with the true inventory, and
      `All_query_test_fixtures_must_implement_ITestSqlLoggerFactory` arrives as a new test naming 27
      fixtures.
- [x] **R2. Ignore what is server-only, with a reason on each entry.** `IgnoredTestBases` was empty.
      Twelve entries added, each verified against the base's own source rather than its name:
      migrations and design-time scaffolding (4), the two SQL generators, the three ADO.NET
      interception bases, the relational DI registrations, and the three precompiled-query bases,
      which pregenerate a provider's SQL at build time on a client that compiles no SQL. 160 listed
      bases become 148. **The other two "do not adopt" groups in #56 are deliberately still listed**:
      the `RelationalModelBuilderTest` nested bases and the `FromSql`/`ToQueryString` family need a
      relational *client* API, which is #60's decision, and a base that is merely undecided must stay
      visible. Measured: no test moved. 22668 / 22480 / 11 / 177, FIXED none, BROKEN none, REASONS
      unchanged (`issue56-ignored`).
- [ ] **R3. `ITestSqlLoggerFactory` on the query fixtures — and it is NOT a prerequisite for R4.**
      **Corrected 2026-08-29, after R5 measured it.** This step used to claim two things and both
      were wrong for the family R4 starts with. *It is already satisfied*:
      `InfoCarrierTestStoreFactory.CreateListLoggerFactory` returns a `TestSqlLoggerFactory` rather
      than a bare `ListLoggerFactory`, precisely so the non-virtual `TestSqlLoggerFactory` property
      several spec fixtures expose can cast to it, and `TPTInheritanceQueryFixture` is one of them.
      R5 proves it: 98 tests were collected and run, where a failed cast would have thrown in the
      fixture's constructor and run none. *And there are no golden strings here*:
      `TPTInheritanceQueryTestBase` contains **zero** `AssertSql` calls.
      What remains of this step is real but smaller — the bases whose content genuinely *is*
      `AssertSql` pin the backend's dialect, and #56's "SQL plumbing only" group has to be answered
      rather than skipped. **R4 does not wait on it.**
- [ ] **R4. Classify the remaining 148.** Each is adopt-on-Tier-B, or an ignore entry with a reason.

      **By-product of R25–R30, 2026-08-31: the list is 60, and here is how it splits.** This is an
      inventory, not the classification R4 asks for — each group still needs the 20-minute probe
      that R25–R30 used (adopt bare, measure, read the reasons diff). Recorded so the next session
      does not re-derive it.

      | Group | Count | What is known now |
      |---|---|---|
      | Needs a relational **client** API (#60) | ~14 | The five `FromSql`/`SqlQuery`/`SqlExecutor`/`ToSqlQuery` bases and the six `*SplitQuery*`/`*SplitInclude*` ones, plus `UdfDbFunction`. **Out of scope by the owner's decision**; do not design around it. |
      | Asserts the **client's relational model** | ~12 | The nine `RelationalModelBuilderTest+*` nested bases, `ModelBuilding101RelationalTestBase`, `Scaffolding.CompiledModelRelationalTestBase`, and `JsonTypesRelationalTestBase` — the last measured in R23 at 104 red of 576 and reverted. This provider does not build a relational model on the client (M9), so these are the ADR-013 "blocked wholesale" shape rather than the "costs one test" shape. |
      | **Re-parents of families already running** | ~15 | `Query.Translations.String*`/`Miscellaneous*`, `Update.UpdatesRelational`, `Query.ComplexTypeQueryRelational`, `Query.JsonQueryRelational`, `Query.PrimitiveCollectionsQueryRelational`, `Query.OwnedQueryRelational`, `Query.SpatialQueryRelational`, `Query.NorthwindMiscellaneousQueryRelational`, the two `BulkUpdates.*Relational`, `ManyToManyTrackingRelational` (R16 examined and deferred). **This is the R25/R26/R30 shape and is where the cheap wins are.** Several also close a `ITestSqlLoggerFactory` fixture entry as a side effect, as R25 and R30 each did. |
      | Plausibly new families or standalone | ~19 | The five `Query.AdHoc*QueryRelational`, `TransactionTestBase`, `TwoDatabasesTestBase`, `LoggingRelational`, the two `ConcurrencyDetector*Relational`, `Query.WarningsTestBase`, `Query.QueryNoClientEvalTestBase`, `Query.NullSemanticsQueryTestBase`, `Query.SharedTypeQueryRelational`, `Query.OwnedEntityQueryRelational`, `Query.NonSharedPrimitiveCollectionsQueryRelational`, `Types.RelationalTypeTestBase`, `Update.JsonUpdateTestBase`, `Update.StoreValueGenerationTestBase`. `Update.StoredProcedureUpdateTestBase` needs stored procedures and is the one here most likely to be a genuine ignore entry. |

      **The three checks R25–R30 showed are worth running on every candidate, in this order**, because
      each is cheap and each caught something:
      1. `grep` the **relational fixture base** (not the test bases) for `UseTransaction` —
         in all six families of that block the trap was there and nowhere else.
      2. Count `AssertSql` calls **with an argument** in the relational bases. In the whole
         `Associations` block there were none; in EF's *SQLite* classes there were many, and those
         must not be adopted.
      3. Check whether EF's SQLite class for the facet exists at all before concluding there is no
         override to adopt — #26708 has two of them commented out, and the fix was to borrow the
         sibling family's.
      #56 carries the hand classification; this step replaces it with an enforced one. The TPT and
      TPC bases that prompted the issue are the first ones worth taking.
- [x] **R5. `TPTInheritanceQueryTestBase` on Tier B, and the client validator it needed.** The first
      TPT coverage in this repository, and the probe that prices the other nine TPT and TPC bases.
      **The probe began at 84 of 98 failing with a single cause**, which is the whole value of
      running it before pricing anything: EF's core `ModelValidator` requires a discriminator on
      every hierarchy, `RelationalModelValidator` overrides that method to allow one without, and
      this provider registered no validator at all. `InfoCarrierModelValidator` lifts the rule,
      with `InMemoryModelValidator` — a non-relational provider subclassing the core validator — as
      the precedent. **The first design keyed on the TPT/TPC mapping-strategy annotation and would
      never have fired**: EF's fixture expresses TPT with `ToTable` per type and never sets that
      annotation. The absence of a discriminator is the signal, as it is in EF's own relational
      validator. 93 of 98 green; the one new red is a real client/server model divergence
      (`Using_from_sql_throws`), left failing per ADR-004 and written up in
      `test/known-failures.txt`. `failed` 11 -> 12, `total` 22682 -> 22780
      (`tpt-model-validator`).
- [x] **R6. `TPCInheritanceQueryTestBase` on Tier B, and what running the sibling proved.** No
      product change: the test class and its fixture are the whole diff, and the figures came back
      **identical to TPT's** — 1 failed, 93 passed, 4 skipped, 98 total, the same test and the same
      reason. That identity is why it was worth adopting before writing anything else. One base
      cannot distinguish a fix for the TPT *mapping* from a fix for TPT's *fixture*; two bases
      reaching the same numbers by the same route can. It also gives the remaining divergence a
      second witness, which is what makes it a defect rather than a curiosity.
      `UseGeneratedKeys` is `false`, copied from EF's own `TPCInheritanceQuerySqliteFixture`:
      TPC needs a key generator shared across tables and SQLite has neither sequences nor HiLo.
      `failed` 12 -> 13, `total` 22780 -> 22878 (`tpc-adopt`).
- [x] **R7. The client keeps the discriminator the server drops.** Core EF gives every hierarchy a
      discriminator; the convention that takes it back for TPT and TPC ships in
      `EFCore.Relational`, which this client does not have. `InfoCarrierHierarchyMappingConvention`
      closes that, registered beside `InfoCarrierValueGenerationConvention`, which exists for the
      identical reason. **Deliberately not a seam**: `IInfoCarrierDocumentMapping` is an interface
      because Cosmos answers *its* question differently, and inheritance is not like that — Cosmos
      always uses a discriminator and has no TPT or TPC, so this convention finds no annotations
      there and is already correct. **The narrowing was the risk**, not the fix: EF compares
      `GetTableName()`, which falls back to the `DbSet` name, so a naive annotation comparison would
      strip the discriminator from plain TPH. BROKEN none across 22879 tests is what says the
      boundary holds. `failed` 13 -> 11, `total` 22878 -> 22879 (`hierarchy-convention`), trim
      ratchet 89 <= 89. **The count is back where it stood before R5**, with 197 more tests.
- [x] **R8. TPH, and the filtered variants of TPT and TPC.** 144 new tests, 141 green. **The two
      filters bases passed on the first run**, which is R7's dividend rather than luck: a global
      filter over a hierarchy must reach every derived type, and under TPT and TPC those live in
      different store objects. **TPH is adopted for the opposite reason** — it is the mapping the
      convention must leave alone, so it asserts the half of the narrowing that must not change.
      Not a duplicate of the Tier A inheritance test, which adopts the *core* base; EF hosts this
      one on SQLite itself. **No TPH filters class**: EF's derives from the core
      `FiltersInheritanceQueryTestBase`, already adopted on Tier A, and one base gets one tier.
      The three reds are ADR-013 — `NormalizeDelimitersInRawString` casts the store to
      `RelationalTestStore` — and all three are raw SQL, so they join #60's blocked set.
      `failed` 11 -> 14, `total` 22879 -> 23023 (`inheritance-family`).
- [x] **R9. Relationships and many-to-many, under TPT and TPC.** Six bases, 1248 new tests, 1236
      green. These navigate *between* hierarchies, so one query touches a derived type, its base and
      the derived types across a navigation. Eight failures were EF's own `ApplyNotSupported`
      convergence and are now green, adopted **after** measuring rather than copied in advance.
      **A filtered probe got this wrong and the full gate caught it**: `~ManyToManyQueryInfoCarrier`
      does not match `ManyToManyNoTrackingQueryInfoCarrier`, so a third of the classes never ran and
      the probe reported 8 where the run found 20. A `~` filter is partial output too.
      **The twelve that remain price #60 for the first time.** All are `_split`; the marker never
      reaches the server, so they run as one query. Eight throw `ApplyNotSupported`; **four do not
      throw at all** and return an over-included graph. #60 argues `AsSplitQuery` changes the plan
      and not the result set — that does not hold here, because the marker vanishes rather than
      being refused, which is evidence for its option 3.
      `failed` 14 -> 26, `total` 23023 -> 24271 (`relationships-manytomany-v2`).
- [x] **R10. Bulk updates over the three inheritance mappings.** Six classes closing **seven**
      entries, 208 new tests, **every one green** — FIXED none, BROKEN none, REASONS unchanged.
      These write, where every inheritance base before them only read.
      **The `UseTransaction` override is on the FIXTURE here, and CLAUDE.md's stated tell would
      have missed it.** `BulkUpdatesAsserter` calls `ExecuteWithStrategyInTransactionAsync` on every
      assertion, but the hook is `InheritanceBulkUpdatesFixtureBase.UseTransaction`, declared
      *abstract on the fixture* — so EF's own SQLite classes override nothing, which reads as
      evidence that no transaction is involved. Inheriting EF's relational implementation would
      have hit `GetDbTransaction()` and the documented 471 `database is locked`. The guardrail
      needs widening: the override may be on the fixture.
      Eight first-probe failures were EF's own `#31402` defect and are adopted as overrides through
      the existing `AssertStoreRefuses` idiom. **No golden strings came with any of it, which
      settles R3 by demonstration**: EF's `AssertSql` lives in the provider subclass, not the base.
      `failed` unchanged at 26, `total` 24271 -> 24479 (`bulkupdates-inheritance`).
- [x] **R11. Table splitting under TPT — the last of the family.** One class closing two entries,
      29 new tests, 23 green. Table splitting merges two entity types into one store object while
      TPT spreads a hierarchy across several, and this base is where they meet. It takes the
      non-shared-model shape, so it uses `NonSharedModelInfoCarrierHarness`.
      **Two reds are ADR-013 in its textbook form and no override can reach them**:
      `TableSplittingTestBase.UseTransaction` calls `GetDbTransaction()` and is *non-virtual*
      (`isVirtual: false`, checked). Only one pair of tests routes through it, so the base costs
      two tests rather than being unreachable — which is why it is adopted rather than skipped.
      **Two reds are topology**: the warning is raised by the relational update pipeline on the
      *server* and asserted against the *client's* logger. Two contexts; the assertion assumes one.
      `failed` 26 -> 30, `total` 24479 -> 24508 (`tpt-tablesplitting`).
      **This closes every TPT and TPC base except the two GearsOfWar ones.**
- [x] **R12. Gears of War under TPT and TPC — the last two, and the largest.** 2354 new tests, 2318
      green. **Every TPT and TPC base EF ships is now adopted.**
      **3419 of 3529 passed before a single override was written.** The 24 distinct failing methods
      were intersected against EF's own 23 SQLite overrides: 15 EF also overrides, 9 it does not.
      **Fifteen were adopted, not twenty-three** — the other eight were never measured red here,
      and copying them would import workarounds for limitations this wire does not reach. 98 fell
      to 36.
      **The 36 that remain are one family, already known.** The base asserts a correlated
      collection with `Distinct` must be *refused*; this provider answers it, because the
      projection split reassembles on the client. That is `limitations.md`'s "answers where others
      refuse" section, which names three today — and one of the nine is **C64** itself, reproduced
      under TPT and TPC. **Whether that page should widen from three named queries to this family
      is a user-facing-docs decision and is not taken here.**
      `failed` 30 -> 66, `total` 24508 -> 26862 (`gearsofwar-tpt-tpc`).
- [ ] **R13. Group C — move the relational bases from Tier A to Tier B.** 83 bases whose *core*
      parent this suite already runs on InMemory, so adopting the relational subclass alongside it
      would be duplication (ADR-009). The owner's policy, 2026-08-29, is to **move**, smallest
      first: 27 add no methods at all, 30 add one to three, 25 add four or more and are held until
      the defects the earlier moves surface are fixed.
- [x] **R13a. PropertyValues, the first move — and it vindicated the policy immediately.**
      `failed` 66 -> 116, **`total` unchanged at 26862**. The same tests existed before; about
      twenty returned `Task.CompletedTask` and the fixture ignored `School` and two `Building`
      properties outright, all because InMemory cannot host complex types. The move added no tests,
      it **made existing ones real**, and 50 fail. **45 are one product defect**: `EF.Property<T>`
      does not survive the wire on the store-values and `Reload` path, thrown on the server.
      `Scalar_store_values_can_be_accessed_as_a_property_dictionary` passed on InMemory and fails
      against a real database — **Tier A was green on a path it never executed.**
      Note for pacing: this base adds *zero* methods, so it is among the cheapest to write.
      Cheap to write is not cheap in reds.
- [x] **R14. Group D classified, and three of its twelve adopted.** The 17 bases outside Group C
      were classified against *measured* properties rather than names. **One is not adoptable**:
      `Update.JsonUpdateTestBase` is the base ADR-013 names — non-virtual `UseTransaction` used by
      136 of its own tests — and the ADR already records the decision. **Four are issue 60**: the
      two `NorthwindSplitInclude` bases, `QueryNoClientEvalTestBase` and `WarningsTestBase`. The
      other twelve are adoptable.
      Three adopted, all green on the first run: `OperatorsQueryTestBase`,
      `OperatorsProceduralQueryTestBase`, `OptionalDependentQueryTestBase`. `failed` unchanged at
      116, `total` 26862 -> 26896 (`groupd-operators`).
      **Three more were written, measured and backed out**: the entity-splitting family returned
      120 failures of 121, 114 of them queries returning zero rows. That is an unseeded store — a
      gap in `NonSharedModelInfoCarrierHarness` for bases that seed data — and ADR-013's rule is
      that wholesale harness failures are not information. They wait on harness work.
      **The distinction that makes ADR-013 usable**: it is not that a non-virtual `UseTransaction`
      disqualifies a base, but that a base failing *wholesale* yields nothing. One use costs a
      test; 136 costs the base.
- [x] **R15. Two more Group C moves — and the policy's second face.** `failed` and `total` both
      **unchanged**, FIXED none, BROKEN none: the signature of a clean tier move.
      `CompositeKeysQueryRelationalTestBase` is the cheapest shape — a one-liner both sides, no
      `UseTransaction`, zero helper uses, both checked. 14 tests.
      **`StoreGeneratedFixupRelationalTestBase` deletes two workarounds rather than adding one.**
      Tier A declared `EnforcesFKs => false` and hand-emptied the store because InMemory has neither
      foreign keys nor rollback. On SQLite both reverse. The base calls the transaction helper
      **118 times and the run is 118 tests, 0 failures** — every one now inside a real transaction
      with foreign keys enforced. `UseTransaction` is `protected virtual` with an empty body,
      verified before writing.
      **Three moves, three different answers**: PropertyValues exposed a 45-test defect (#69),
      StoreGeneratedFixup turned dead paths live and passed, CompositeKeys changed nothing. Only
      the move tells you which — which is the argument for the policy.
- [x] **R16. ComplexTypesTracking moved; ManyToManyTracking examined and deferred.** `failed` and
      `total` unchanged, REASONS unchanged, and the FIXED/BROKEN lists are **the same two tests
      changing namespace** — a rename, not a behaviour change. The two are J22's, unchanged by the
      store. The move deletes two more InMemory accommodations: the post-test reseed and the
      `TransactionIgnoredWarning` log, both meaningless once the transaction is real. Its
      `UseTransaction` override is what makes it real, for the base's three helper uses.
      **`ManyToManyTrackingRelationalTestBase` is deferred with a reason**: its SQLite fixture needs
      store-specific model configuration — `HasDefaultValueSql("CURRENT_TIMESTAMP")`, indexer
      defaults — for `SupportsDatabaseDefaults`. That is server-model work, not a fixture swap.
- [x] **R17. LazyLoadProxy moved — the cheapest move so far, and it found a product defect.**
      `failed` and `total` unchanged at 116 / 26896, FIXED and BROKEN both empty, REASONS unchanged.
      **The move deletes 700 lines and adds 68.** The Tier A class ignored `Milk` and `Culture` on
      twenty-nine entity types, because InMemory has no complex types, and then carried two 680-line
      JSON strings restating the model that was left; the relational base maps both complex
      properties itself, so all of it goes.
      **EF's own SQLite class differs from the core base by one token across both strings** —
      `"Charge": 1.00` becomes `1.0`, because SQLite has no decimal type and the scale that comes
      back is the scale the store wrote. Written as the substitution rather than as 1360 lines of
      duplicated JSON: a base that stops carrying the token fails the assertion rather than silently
      passing, and `Can_serialize_proxies_to_JSON` passing is the proof the scale survives the wire.
      **Four tests failed with `InvalidCastException: Double to Int32`, and it is one defect.**
      `ClientResultMaterializer.Materialize` fills the value buffer by bare property name, and a
      complex leaf's `Name` is bare — `Host.Culture.Rating` is `"Rating"`. `Host.Rating` is a
      `double`; the four `Rating` properties under `Culture` and `Milk` are `int`, and all four
      slots took the double. The file's own remark already warned that a complex value must not ride
      in that dictionary because names collide; **the leak in the other direction, a top-level value
      bleeding *into* complex slots, was not considered.** One condition fixes it: only a property
      whose `DeclaringType is IEntityType` reads from the dictionary, and `ApplyComplexValues`
      writes the complex members afterwards through their CLR member. Trim ratchet 89 <= 89.
      **This is EF's own `InternalEntityEntry` values constructor, copied.** EF only ever calls it
      with dictionaries it controls, so the collision is latent upstream rather than a defect there.

- [x] **R18. Three Northwind bases moved — and the store itself was missing a view.**
      `failed` 116 -> 118, `total` 26896 -> 26900. FIXED none, BROKEN 2, REASONS: the one family,
      34 -> 36. **The four new tests are the relational bases' own and all four pass.**
      `NorthwindSetOperations`, `NorthwindInclude` and `NorthwindKeylessEntities`. The probe went 14
      red to 6, and every step of that reduction is a rule this repository already had:
      **Eight were APPLY.** EF's own `NorthwindIncludeQuerySqliteTest` overrides exactly four
      methods and no others, and the four measured red here are the same four. A query that reaches
      the store and is refused by it is convergence with the reference provider.
      **One was EF issue #21627**, `KeylessEntity_with_nav_defining_query`, where EF's SQLite class
      asserts a `SqliteException`; the store refuses it identically here, arriving wrapped.
      **`KeylessEntity_by_database_view` was OURS, and it was a store gap rather than a query one.**
      `ProductView` maps `ToView("Alphabetical list of products")`, and EF's SQLite suite passes
      only because its Northwind store is a prebuilt `northwind.db` holding the real schema. This
      tier builds its store from the model, and `NorthwindContext` **ignores `Product.CategoryID`**,
      so there was nothing for a view to read. Mapping the column on the server context and writing
      the view as a defining query took 4 red to 40 green. **The client model still ignores the
      column**; a property the client does not know is skipped when the row is read back.
      **The two that remain are the family**, `Collection_projection_before_set_operation_fails`.
      Its sibling `..._after_set_operation_fails_if_distinct` passes, so this provider refuses that
      shape exactly as a relational one does. **That pair is what sharpened the docs wording**: the
      claim is not "collection projections with set operations", it is the *before* shape only.
      The fixture gained `ITestSqlLoggerFactory`, which `RelationalQueryAsserter` casts to on the
      failure path; without it a failing assertion would surface as `InvalidCastException` and hide
      its own reason. Nothing new is constructed for it.
      **`InheritanceRelationshipsQueryRelationalTestBase` was examined and HELD.** Every test it
      adds is `AsSplitQuery`, which is #60's territory and an owner decision.

- [x] **R19. The harness seeding gap, found and fixed — and it was not a seeding gap.**
      `failed` 118 -> 122, `total` 26900 -> 27021. **121 new tests, 117 green.**
      **The earlier reading was wrong.** The note said `NonSharedModelInfoCarrierHarness` does not
      seed. It does. What it did not do is carry the *seeder*:
      `NonSharedModelTestBase.ConfigureOptions` applies `AddOptions` to the **client** context only,
      and `EntitySplittingQueryTestBase` seeds through `AddOptions(...).UseSeeding(...)`. A seeder
      runs inside `EnsureCreated`, and `EnsureCreated` runs on the **server**, so it never fired.
      **Proved before it was fixed**, per CLAUDE.md: the base was adopted, one test run, and it
      answered `Expected: 5, Actual: 0`. After the fix that class is **114 of 114**.
      **Only the seeders cross, not all of `AddOptions`.** The distinction is what the option acts
      on: a seeder acts on the *store*, which is the server's, while warning behavior and
      sensitive-data logging describe how a context behaves and each side owns its own. A29 stands.
      They are read off a throwaway builder because `UseSeeding` has no getter. Passing `AddOptions`
      at all eleven call sites changed nothing else: FIXED empty, and BROKEN is only the four below.
      **Three bases adopted, splitting three ways on ADR-013.**
      `EntitySplittingQueryTestBase`: 114 green, no overrides.
      `NonSharedModelUpdatesTestBase`: **reachable despite the non-virtual `UseTransaction`.** It
      calls `GetDbTransaction()`, but the method that *calls* it,
      `ExecuteWithStrategyInTransactionAsync`, is `protected virtual`; overriding that hands
      `TestHelpers` a different enlistment and never touches the unreachable member.
      **ADR-013's rule is about a base whose ONLY route runs through the non-virtual member**, and
      this refines it. `Principal_and_dependent_roundtrips_with_cycle_breaking` passes because of it.
      `EntitySplittingTestBase`: `Can_roundtrip` green; `ExecuteDelete_throws_for_entity_splitting`
      unreachable, because there the call is inline in the test body with no hook in between.
      **One new finding, filed as #70.** `DbUpdateException.Entries` names every entry the client
      sent rather than the one the store rejected. W5 already documents why the server's entries
      cannot cross; what is missing is the wire carrying *which* failed. Matching by key would not
      fix this case, since all three rows are `Added` with store-generated keys and the server has
      no key for the failing one either. The ordinal in the sent list is what identifies it, which
      makes it a protocol change and so filed rather than taken.

- [x] **R20. The last four Northwind query bases moved to Tier B.** `NorthwindFunctions`,
      `NorthwindNavigations`, `NorthwindAggregateOperators` and `NorthwindGroupBy` — the Northwind
      query bases R18 did not take. None of the four relational bases adds an `AsSplitQuery` test,
      a `UseTransaction` route or a `RelationalTestStore` cast, so none is gated on #60 or ADR-013:
      each just swaps in `RelationalQueryAsserter` and adds at most a few message-shape overrides.
      The move deletes the InMemory-limitation override sets the Tier A classes carried — one on
      `NorthwindNavigations` (now inherited from the relational base), eight on
      `NorthwindAggregateOperators`, six on `NorthwindGroupBy` — each of which the Tier A class had
      already flagged for deletion once a relational backend landed. `test/` only, so the gate is
      `eng/measure.sh`; measured on CI's Spec ratchet because a local full run OOMs this box.
      **Adopted bare first**: 34 tests red, none fixed. **28 are convergence with EF's own SQLite
      classes** and are adopted as overrides in EF's shape — Functions gets four
      `AssertTranslationFailed` (`Math.Round`/`Math.Truncate` in a `Sum` projection has no SQLite
      translation); AggregateOperators gets an `ApplyNotSupported` and a local-tuple-array
      `Contains` `AssertTranslationFailed`; GroupBy gets seven `SqliteStrings.ApplyNotSupported`
      plus `Final_GroupBy_nominal_type_entity` (a `GroupBy` key of a client-only type, ADR-010,
      refused before the wire — the Tier A override that survives the move because its reason is
      store-independent, and one EF's SQLite class does *not* have). `Navigations` needs no
      overrides — the one it had is now inherited. **The 6 that stay red are the finding**: three
      AggregateOperators members (`Average_over_max_subquery`, `Average_over_nested_subquery`,
      `Type_casting_inside_sum`, sync+async) return an aggregate that differs from EF's expected in
      the trailing digits. The `(decimal)` cast over an `int`/`float` `Average`/`Sum` picks a
      different translation on each side of the wire — EF's expected is the `double` computation,
      the server's is SQLite's decimal accumulator, and `AssertEqual` compares exactly. B4 family;
      left failing per ADR-004, recorded in `test/known-failures.txt`, tracked as issue #75.
      `failed` 72 → 78, `total` unchanged 27021. (Issue #75 later closed those six — SQLite
      version and store seeding, not the wire; see `test/known-failures.txt` `#77` block.)

- [x] **R21. Three more Northwind query bases onto their relational bases.** `NorthwindWhere`,
      `NorthwindJoin` and `NorthwindSelect` — the Northwind query Sqlite classes R18 and R20 did
      not take. None has a Tier A counterpart, so this is not a tier move: each already ran on
      Tier B against the *core* base and is re-parented onto the `*RelationalTestBase`, which swaps
      in `RelationalQueryAsserter` (the fixture has implemented `ITestSqlLoggerFactory` since R18)
      and folds in that base's expected-answer corrections. None of the three relational bases adds
      an `AsSplitQuery` route, a `UseTransaction` route or a `RelationalTestStore` cast, so none is
      gated on #60 or ADR-013. `test/` only, so the gate is `eng/measure.sh`.
      **Join**: the relational base adds no test and no override — a pure asserter swap. 132 → 132,
      all green.
      **Where**: the relational base adds one theory,
      `EF_MultipleParameters_with_non_evaluatable_argument_throws` (+2, sync and async), which
      passes, and turns `Where_bool_client_side_negated` into `AssertTranslationFailed` — this
      provider fails that translation identically, so the hand-written override that used to
      restate it is deleted and inherited instead. 406 → 408, all green.
      **Select**: `Reverse_without_explicit_ordering` was restated by hand here with a comment
      that the class "cannot derive from" the relational base; it now can, so the restatement is
      deleted and inherited. The base also turns
      `Select_bool_closure_with_order_by_property_with_cast_to_nullable` into
      `AssertTranslationFailed` — and **this provider answers that query** (its split evaluates the
      `OrderBy` over a client constant projection and the server runs the rest), so the inherited
      assertion is overridden back to the core answer-check, which passes. Same category as
      `limitations.md`'s "queries this provider answers that other EF providers refuse". 372 → 372,
      all green.
      `failed` unchanged at 72, `total` 27021 → 27023, FIXED none, BROKEN none. The three relational
      bases leave `InfoCarrierComplianceTest`'s missing list at 101 (was 104). Measured with local
      per-class runs (`--filter`); a full run OOMs this box, so the CI Spec ratchet confirms the
      figures.

- [x] **R22. The four ComplexNavigations bases moved to Tier B.** `ComplexNavigationsQuery`,
      `ComplexNavigationsCollectionsQuery` and their two shared-type siblings — the deepest
      navigation corpus EF ships, and the cheapest Group C move left to write: all four relational
      bases are 11 to 19 lines and add no test methods of their own. None declares
      `UseTransaction` or calls `ExecuteWithStrategyInTransactionAsync`, at either level of the
      chain, both checked rather than assumed. The two relational *fixture* bases implement
      `ITestSqlLoggerFactory`, so the move also clears two entries from the compliance test's
      second assertion. `test/` only, so the gate is `eng/measure.sh`.
      **Adopted bare first**, per the measure-first rule. The bare run was 118 red of 1856, and
      **the classification came out cleaner than any move so far: every one of the 118 is a test
      EF's own SQLite suite overrides, and not one is this provider's.** 110 are
      `SqliteStrings.ApplyNotSupported` — SQLite has no `APPLY` — and are adopted in EF's shape,
      through a per-file `AssertApplyNotSupported` helper as six other Tier B classes here already
      do. The other eight are two families of four, both of them the Tier A overrides that survive
      the move because their reason is store-independent: `GroupJoin_client_method_in_OrderBy`
      (client code in an `OrderBy` key, refused with the same details clause EF's SQLite class
      asserts) and `Join_with_result_selector_returning_queryable_throws_validation_error` (C73's
      refusal on the result element type, raised before the wire — **the one place these classes
      do not follow EF's SQLite suite**, which expects `ApplyNotSupported` because on SQLite the
      query gets as far as the translator).
      **And two of EF's SQLite overrides are deliberately not adopted**:
      `Projecting_collection_after_optional_reference_correlated_with_parent`, in both collections
      classes, *passes* here — the projection split reassembles that collection on the client, so
      SQLite is never asked for `APPLY`. Same category as R21's
      `Select_bool_closure_with_order_by_property_with_cast_to_nullable` and
      `limitations.md`'s "queries this provider answers that other EF providers refuse".
      1856 of 1856 green. The compliance missing list goes 101 → 97, and its
      `ITestSqlLoggerFactory` list 25 → 23. Measured with local per-class runs (`--filter`); the
      CI Spec ratchet confirms `failed` and `total`.

- [x] **R23. `OptimisticConcurrency` onto its relational base — and two of the four "cheap"
      Group C candidates turned out not to be cheap at all.** The batch was picked by size:
      `ConcurrencyDetectorDisabled`/`EnabledRelationalTestBase` (22 lines, one method each),
      `OptimisticConcurrencyRelationalTestBase` (35 lines, one method) and
      `JsonTypesRelationalTestBase` (226 lines, two methods, no transaction helper). **Reading the
      bases rather than their line counts disqualified three of the five.**
      **`OptimisticConcurrency` is adopted, and it is the one that worked.** It already ran on
      Tier B against the *core* base, so this is a re-parent rather than a tier move. The
      relational base adds `Property_entry_original_value_is_set` and requires the fixture to be an
      `F1RelationalFixture<TRowVersion>`; both are fine. The new test passes — the concurrency
      check is the server's and `RelationalStrings.UpdateConcurrencyException` is the message it
      raises, so the assertion holds across the wire unchanged. 45 → 46, 34 passed, 12 skipped,
      0 failed.
      **Deriving from `F1RelationalFixture` also deletes a duplicate.** This fixture restated
      `F1RelationalFixture.BuildModelExternal` by hand, on a comment saying a non-relational
      provider has no business referencing `EFCore.Relational.Specification.Tests` — which is what
      ADR-013 settled the other way for the test project. Inheriting it removes about thirty lines
      and picks up the six TPT and TPC circuit types the copy had omitted. It also puts EF's
      relational configuration on the **client's** model, because `F1FixtureBase.AddOptions` feeds
      `BuildModelExternal` a builder from this provider's convention set: measured, `ToTable`,
      `HasColumnName` and the TPT/TPC mapping strategies are inert there and every test stays
      green.
      **`ConcurrencyDetectorDisabledRelationalTestBase` and `ConcurrencyDetectorEnabledRelationalTestBase`
      are #60, not Group C.** The single method each one adds is `FromSql`, over
      `Products.FromSqlRaw(...)`. They belong with the `FromSql`/`ToQueryString` family R2 left
      visible pending #60's decision, and the earlier reading of them as "one method, therefore
      cheap" was a line count standing in for a look at the method. Not adopted.
      **`JsonTypesRelationalTestBase` is an ADR-013 exclusion: it assumes the *client* is
      relational.** Adopted bare and measured before being judged — 104 red of 576 — and **92 of
      the 104 are one cast.** `AssertElementFacets` calls `element.FindRelationalTypeMapping()` on
      the model under test, which here is the client's, and that throws
      `InvalidCastException: InfoCarrier.Core.InfoCarrierTypeMapping → RelationalTypeMapping`
      (69 directly; another 23 through `NoNestedCollections`, whose `Assert.Throws` catches the
      cast instead of the relational message it wants). `AssertElementFacets` *is* `protected
      virtual`, so ADR-013's amendment would allow an override — but the override would have to
      reimplement the grandparent's body, because `base` reaches the relational one, and what it
      would delete is every relational facet assertion in the base. That is the whole of what this
      base contributes. Not adopted; `JsonTypes` stays on Tier A against the core base, where it is
      green. Recorded here for R4 rather than added to `IgnoredTestBases`, as `Update.JsonUpdateTestBase`
      was in R14.
      `test/` only. Measured with local per-class runs (`--filter`); the CI Spec ratchet confirms
      `failed` unchanged and `total` +1.

- [x] **R24. `DataAnnotation` moved to Tier B — the first Group C move whose *value* is the store
      rather than the base.** The relational base adds only two tests, but the Tier A class carried
      **six** overrides that each replaced a round trip with a metadata assertion because InMemory
      enforces no store constraint. On a real database three of those six run for real and pass:
      `ConcurrencyCheckAttribute_throws_if_value_in_database_changed` and the two
      `RequiredAttribute` ones. That is R13a's lesson again — the move adds few tests and makes
      existing ones real — except that here they pass.
      **The `UseTransaction` override is written in the same commit as the store switch**, per
      CLAUDE.md: the base routes five tests through `ExecuteWithStrategyInTransactionAsync`, four
      in the core base and one in the relational one, and the tell is the base's own transaction
      strategy rather than anything in the fixture.
      The other three of the six are replaced by EF's own `DataAnnotationSqliteTest` overrides —
      same bodies, same reasons, different store: SQLite enforces no column length
      (`MaxLengthAttribute`, `StringLengthAttribute`) and has no `rowversion` (`TimestampAttribute`,
      EF issue #2195, the same one this repo's `OptimisticConcurrencyInfoCarrierTest` skips eleven
      tests for).
      **One test is left failing, and it is R23's finding again at one-hundredth the size.** The
      relational base's `Table_can_configure_TPT_with_Owned` asserts over
      `context.Model.GetTableMappings()`, which needs the relational model, and the model under
      assertion is the **client's** — which this provider does not build relationally (M9). Where
      `JsonTypesRelationalTestBase` failed 92 tests on that assumption and is therefore not
      adoptable, this base fails one, so it is adopted and the test is left failing per ADR-004.
      That is exactly the distinction ADR-013's 2026-08-30 amendment draws.
      95 / 0 / 95 → 97 / 96 / 1. `failed` 70 → 71, `total` +2. `test/` only.

### R25–R30 — the `Query.Associations` relational family (35 bases)

The 35 `Query.Associations.*RelationalTestBase` classes are a third of the 95 the compliance
test still lists. EF ships a complete SQLite counterpart for every one of them, fixtures
included, and **none of them carries `FromSql`, `ToQueryString`, or a `RelationalTestStore`
cast** — the four things that blocked bases earlier in Phase R. **Nor is there any golden SQL:
across all 35 there are zero `AssertSql("…")` calls with an argument. Every use is either the
`protected void AssertSql(params string[] expected)` declaration or an empty `AssertSql()`
meaning "nothing was executed".** That corrects a C0-era remark in
`ComplexPropertiesQueryInfoCarrierTests.cs` which said these bases "assert SQL and stay
unadopted"; they do not.

**What an empty `AssertSql()` is worth here, stated honestly.** It reads the *client's*
`TestSqlLoggerFactory`, and this client has no database and emits no SQL, so the assertion
passes trivially. Weaker than it is on SQLite, not false. `ServerSqlLog` is where the server's
statements can be read.

**The `UseTransaction` trap is on the FIXTURE here, not the base.** Grepping these test bases
for `ExecuteWithStrategyInTransactionAsync` finds nothing, and that is not clearance: every
`*RelationalFixtureBase` in the family declares
`UseTransaction(facade, t) => facade.UseTransaction(t.GetDbTransaction())`, which ADR-013 makes
unreachable on a client with no database. Each of our fixtures overrides it with
`facade.UseInfoCarrierTransaction(transaction)`, in the same commit as the fixture.

- [x] **R25. The seven `Navigations` bases — a pure re-parent, and it cost nothing.** The
      fixture moves from the core `NavigationsFixtureBase` to `NavigationsRelationalFixtureBase`
      and the seven classes from `Navigations*TestBase` to `Navigations*RelationalTestBase`.
      **Six hand-written overrides are deleted, because the re-parent now inherits them
      verbatim**: the two `Distinct_over_projected_*` (C2 copied them out of
      `NavigationsCollectionRelationalTestBase`), `Select_nested_collection_on_optional_associate`,
      `Over_associate_collection_projected`, and the three `Nested_collection_*`. The fixture's
      hand-mirrored six `AutoInclude()` calls go the same way — they were
      `NavigationsRelationalFixtureBase.OnModelCreating`, mirrored in C0 because the test project
      did not then reference `EFCore.Relational.Specification.Tests`. What stays is EF's own
      `Navigations*SqliteTest` overrides, three `SqliteStrings.ApplyNotSupported` assertions the
      relational bases do not carry.
      The relational bases add no test of their own, so this is measurable in one figure:
      `Passed: 336, Failed: 0, Total: 336` across all three Associations families before and
      after, and `Passed: 109, Failed: 0, Total: 109` for `Navigations` alone. `failed` and
      `total` both unchanged; compliance missing 95 → 88, fixtures 23 → 22. `test/` only.

- [x] **R26. The six `OwnedNavigations` bases, adopted bare — 8 red of 91, and all 8 are one
      reason.** The fixture re-parents onto `OwnedNavigationsRelationalFixtureBase` and the six
      classes onto `OwnedNavigations*RelationalTestBase`, with **every override removed** so that
      the failures are measured before anything is written to answer them.
      **The hand copy C0 left behind was not complete, which is the argument for the re-parent
      rather than for maintaining it.** C0 mirrored `OwnedTableSplittingRelationalFixtureBase`'s
      and `OwnedNavigationsRelationalFixtureBase`'s `ToTable` calls and `AreCollectionsOrdered`;
      it did not mirror the base's `ValueGeneratedNever()` on every owned key, nor its
      `Navigation(…).IsRequired()` statements. The re-parent brings both in.
      `Passed: 83, Failed: 8, Total: 91`, measured locally with a `--filter` run. **The eight are
      four tests times two `QueryTrackingBehavior` arms, and the reason is the same in all eight:
      SQLite has no `APPLY`.** Two shapes, which is the reasons diff and not the count:
      `NoTracking` raises `SqliteStrings.ApplyNotSupported` bare, while `TrackAll` reaches an
      assertion expecting a different message — *"A tracking query is attempting to project"* for
      the `Projection` and `Collection` ones, *"Unable to translate a collection subquery"* for
      `SetOperations`. R26a is the override subset.

- [x] **R26a. The `OwnedNavigations` override subset — 8 of 8 answered, all from EF's own SQLite
      suite.** `Passed: 336, Failed: 0, Total: 336` across all three Associations families,
      identical to the figure before R25 and after it, so `failed` and `total` are both unchanged
      and the baseline files do not move. Compliance missing 88 → 82, fixtures 22 → 21.
      Adopted, and why each matched by reason first (A63):
      • `OwnedNavigationsCollectionSqliteTest.Distinct_projected`, whole, including the
      `TrackAll` arm EF short-circuits with *"Base test expects 'can't track owned entities'
      exception, but with SQLite we get 'no CROSS APPLY'"* — which is exactly what R26 measured.
      • `OwnedNavigationsSetOperationsSqliteTest.Over_associate_collection_projected`, **verbatim,
      and this is the clearest single thing the re-parent bought.** EF writes it as
      `Assert.ThrowsAsync<EqualException>` because the relational base asserts
      `InsufficientInformationToIdentifyElementOfCollectionJoin` and SQLite raises APPLY instead,
      so the nested assertion failure *is* the statement. C57 could not write it that way — the
      class then sat on the core base, which makes no assertion to fail — and asserted the APPLY
      message directly with a paragraph explaining the divergence. That paragraph is now deleted.
      • The two `Projection.Select_subquery_*_related_FirstOrDefault` from
      `OwnedJsonProjectionSqliteTest`, character for character. **EF ships no
      `OwnedNavigationsProjectionSqliteTest` at all** — the whole class is commented out upstream
      for EF issue #26708 — so there is nothing to adopt there and `OwnedJson`'s is the nearest
      statement of the same limit, as C20 and C57 already found.
      **Deliberately not adopted:** `OwnedNavigationsStructuralEqualitySqliteTest` overrides
      several tests purely to assert golden SQL. Those pass here on the relational base's own
      assertion, and the golden SQL is the *backing store's* statement text, which this client
      never emits — taking them would assert nothing and would couple the file to SQLite's
      formatting. **This is the one place the family does carry golden SQL, and it is in EF's
      SQLite classes, never in the 35 relational bases** (verified: every `AssertSql` in those 35
      is the declaration or an empty call).

- [x] **R27. `OwnedTableSplitting` — the first genuinely new family, and it lands at 4 red of 70.**
      A new fixture on `OwnedTableSplittingRelationalFixtureBase` and four classes adopted bare.
      **Four and not seven: EF ships no `BulkUpdate`, `Collection` or `SetOperations` class for
      this family.** The mapping is the one `OwnedNavigations` switches *off* — an owned reference
      lives in its owner's table and only an owned collection gets a table of its own — which is
      why `OwnedNavigationsRelationalFixtureBase` derives from this one and overrides precisely
      that. Nothing is mirrored by hand, `AreCollectionsOrdered` included.
      `Passed: 66, Failed: 4, Total: 70`. **All four are two tests times two arms, and the reason
      is the one R26 already priced: SQLite has no `APPLY`.**
      `Projection.Select_subquery_required_related_FirstOrDefault` and
      `…_optional_related_FirstOrDefault`, `NoTracking` raising the APPLY message bare and
      `TrackAll` reaching `AssertOwnedTrackingQuery` expecting *"A tracking query is attempting to
      project"*.
      **EF's `OwnedTableSplittingProjectionSqliteTest` is commented out in full** — the same EF
      issue #26708 that disables `OwnedNavigationsProjectionSqliteTest` — so once again there is
      no upstream override to adopt and `OwnedJsonProjectionSqliteTest` is the nearest statement of
      the same limit. Two families now, same gap, same substitute.
      **One thing of EF's is deliberately not adopted and it is worth naming: their SQLite fixture
      adds `ConfigureWarnings(b => b.Ignore(SqliteEventId.CompositeKeyWithValueGeneration))`.**
      Not needed here and measured rather than assumed — 66 of 70 pass with no such failure, and
      an unnecessary warning-ignore in a fixture is the kind of thing that later hides a real one.
      `OwnedTableSplittingStructuralEqualitySqliteTest` is not adopted either: its overrides are
      golden SQL only, those tests pass here on the relational base's own assertion, and the SQL is
      the backing store's text, which this client never emits.

- [x] **R27a. The `OwnedTableSplitting` override subset — 4 of 4 answered, and the family is
      green.** `Passed: 406, Failed: 0, Total: 406` for the whole `Query.Associations` tree, which
      is R26a's 336 plus this family's 70. Both overrides are `OwnedJsonProjectionSqliteTest`'s,
      character for character, because #26708 leaves EF with no `OwnedTableSplitting` projection
      class to take them from. **Baseline moves for the first time this block: `failed` unchanged
      at 71, `total` 27026 → 27096**, and `known-failures.names.txt` is untouched because no
      failure is added or removed. The two `InfoCarrierComplianceTest` entries stay red by design;
      what falls is what their assertion prints (95 → 82 bases, 23 → 21 fixtures), not their
      status.

- [x] **R28. `ComplexTableSplitting` — 4 red of 115, same reason again, and EF has the override
      this time.** A new fixture on `ComplexTableSplittingRelationalFixtureBase` and five classes
      adopted bare. **Five and not seven: EF ships no `Collection` or `SetOperations` class, and
      that follows from the model rather than from EF's convenience — a complex collection cannot
      be table-split at all**, so the fixture both `Ignore`s every collection and nulls it out of
      the seed data, and there is nothing for those two facets to run against.
      `Passed: 111, Failed: 4, Total: 115`. The four are the same two
      `Projection.Select_subquery_*_related_FirstOrDefault` tests in both arms, all four raising
      `SqliteStrings.ApplyNotSupported` bare — **no `TrackAll` wrapper this time**, because a
      complex type is not tracked as an entity and there is no owned-tracking assertion in the way.
      **`ComplexTableSplittingProjectionSqliteTest` exists and carries exactly those two
      overrides**, so unlike R26a and R27a this one is adopted from the family's own SQLite class
      rather than a sibling's. R28a takes them verbatim.
      **This family is the other half of a question C0 answered once.**
      `ComplexPropertiesQueryInfoCarrierFixture` mirrors `ComplexJsonRelationalFixtureBase`'s
      `ToJson()` because a relational store has no other way to hold a complex collection; this
      family is the other answer to the same question — do not hold collections at all. Both are
      now adopted as EF states them.
      Nothing of EF's is left unadopted here: the rest of its SQLite suite for this family is bare,
      with **no golden SQL anywhere in it**.

- [x] **R28a. The `ComplexTableSplitting` override subset — one override, 4 of 4 answered.**
      `ComplexTableSplittingProjectionSqliteTest`'s two methods, verbatim.
      `Passed: 521, Failed: 0, Total: 521` for the whole `Query.Associations` tree, which is
      R27a's 406 plus this family's 115. `failed` unchanged at 71, `total` 27096 → 27211,
      `known-failures.names.txt` untouched. Compliance missing bases 78 → 73, fixtures unchanged
      at 21.

- [x] **R29. `OwnedJson` — 16 red of 87, and for the first time in this block they are not all one
      reason.** A new fixture on `OwnedJsonRelationalFixtureBase` and six classes adopted bare.
      **Six and not seven, and the missing one is not ours:**
      `OwnedJsonSetOperationsRelationalTestBase` is commented out upstream in full, EF's note being
      that every set operation over an owned JSON collection throws `KeyNotFoundException` on the
      synthesized ordinal key. The compliance test asks for six for that reason.
      `Passed: 71, Failed: 16, Total: 87`. **Three groups, and the reasons diff is the whole point
      of reading them separately:**
      • **Group A, twelve of the sixteen: SQLite has no `APPLY`.** Six tests times two arms —
      `Collection.Distinct_projected`, the two `Projection.Select_subquery_*_related_FirstOrDefault`
      and the three `Projection.SelectMany_*`. EF has an override for every one, in
      `OwnedJsonCollectionSqliteTest` and `OwnedJsonProjectionSqliteTest`. R29a adopts all six.
      • **Group B, three: an exception-type difference on a path that is unsupported either way.**
      `Contains_with_parameter`, `Contains_with_operators_composed_on_the_collection` and
      `Contains_with_nested_and_composed_operators` — the relational base asserts
      `KeyNotFoundException`, this provider raises `InvalidOperationException` *"No backing field
      could be found for property … and the property does not have a getter"*. Both are the owned
      JSON collection's synthetic key machinery failing to be read; the difference is which point
      it fails at first. **EF's SQLite class has no override for these — it just calls `base` — so
      there is nothing to adopt, and writing one of our own would be overriding a spec test to make
      the suite green.** Left failing per ADR-004. Note that `Contains_with_inline`, which the base
      asserts as `InvalidOperationException`, passes.
      • **Group C, one, and it is the good kind: `Associate_with_parameter_null` fails because it
      passes.** The relational base wraps it in `Assert.ThrowsAsync<EqualException>` for EF issue
      #36401 — EF expects a *wrong answer* here — and this provider returns the right one, so no
      `EqualException` is thrown and the wrapper fails. This is R21's and R22's category again:
      **a query this provider answers that other EF providers get wrong.** Not overridden, left
      failing, and a candidate for `website/docs/limitations.md`'s section on exactly that (that
      file is governed by `doc-style.md` and needs a humanizer pass, so it is not touched here).

- [x] **R29a. The `OwnedJson` override subset — 12 of 16 answered, 4 left failing on purpose, and
      this is the first commit in the block that raises `failed`.** Six methods adopted whole:
      `OwnedJsonCollectionSqliteTest.Distinct_projected` and all five of
      `OwnedJsonProjectionSqliteTest`. **That second class is the one R26a and R27a have been
      borrowing from**, because #26708 leaves the two other owned families without a projection
      class; here it is finally used where it was written.
      `Passed: 604, Failed: 4, Total: 608` for the whole `Query.Associations` tree, which is
      R28a's 521 plus this family's 87 — 83 of the 87 green.
      **`failed` 71 → 75, `total` 27211 → 27298, and `known-failures.names.txt` gains exactly the
      four names.** They are new tests arriving red, not a regression: FIXED none, BROKEN none.
      Compliance missing bases 73 → 67, fixtures unchanged at 21.
      Nothing of EF's SQLite suite is left unadopted that states a *reason*:
      `OwnedJsonStructuralEqualitySqliteTest` overrides every test in the class, but only ever to
      assert golden SQL, calling `base` for the behaviour — so there is nothing there for the four
      reds, and both the exception-type difference and the "fails because it passes" case stand as
      measured.

- [x] **R30. `ComplexJson` — the last family, a re-parent, and the hand copy is cashed in.** 10 red
      of 136, all one reason. `ComplexPropertiesQueryInfoCarrierFixture` becomes
      `ComplexJsonQueryInfoCarrierFixture` on `ComplexJsonRelationalFixtureBase`, and the seven
      classes move from `ComplexProperties*TestBase` to `ComplexJson*RelationalTestBase`.
      **The ~20 lines of `ToJson()` C0 mirrored by hand are deleted, and the copy was diffed
      against the original before it went: byte-identical apart from the wording of one comment.**
      **This is a re-parent and not an addition, and running both would be duplication rather than
      coverage** (CLAUDE.md). The `ComplexJson*RelationalTestBase` classes derive from the
      `ComplexProperties*TestBase` ones, so compliance resolves both transitively, and the
      *non*-JSON complex mapping is not lost: R28 adopted it as `ComplexTableSplitting`. **Two
      complex mappings, one family each, and no model mirrored by hand anywhere in the block any
      more.**
      `Passed: 126, Failed: 10, Total: 136` — and 136 is exactly the count the family had before
      the re-parent, so no test is gained or lost. All ten are five
      `ComplexJsonProjection` tests times two arms, all raising `SqliteStrings.ApplyNotSupported`
      **bare in both arms** (a complex type is not tracked as an entity, so no owned-tracking
      assertion intervenes — the same distinction R28 measured).
      `ComplexJsonProjectionSqliteTest` has exactly those five and R30a adopts them. **No other
      class in EF's SQLite suite for this family carries a single override**, golden SQL included.
      **It also corrects a C0-era remark that stood on this file**, which said the `ComplexJson*`
      bases "assert SQL and stay unadopted". They do not, and the whole block is the evidence.
      **And it settles the #62 note the old file carried.** That note deleted an override of
      `Contains_with_nested_and_composed_operators` — borrowed from
      `ComplexTableSplittingStructuralEqualityRelationalTestBase` and applied to a JSON-mapped
      model — once the query started translating, and called the result "a query this provider
      answers that other EF providers refuse". The sharper reading now available: EF's *JSON*
      structural-equality base asserts nothing at all, so passing there is agreement rather than
      divergence, and R28 runs the table-splitting base where EF *does* assert the throw and this
      provider throws. **The difference was the mapping, not the provider**, and the borrowed
      override never should have applied to a JSON model. Deleting it was right for a reason
      better than the one recorded.

- [x] **R30a. The `ComplexJson` override subset — one class, and the block is finished.**
      `ComplexJsonProjectionSqliteTest`'s five, adopted whole. The re-parent also deleted nine
      overrides this file used to restate by hand — five in `BulkUpdate`, two in `Collection`, two
      in `SetOperations` — every one copied out of a `ComplexJson*RelationalTestBase` in C20 and
      now inherited verbatim.
      `Passed: 604, Failed: 4, Total: 608` for the whole `Query.Associations` tree, **unchanged
      from R29a, as a re-parent should be**. `failed` stays 75 and `total` stays 27298, so neither
      baseline file moves.

### R25–R30 closed: all 35 `Query.Associations` relational bases are adopted

**The compliance test's missing list holds no `Query.Associations` entry at all**: 95 → **60**
bases, 23 → **20** fixtures. `Passed: 604, Failed: 4, Total: 608` across the six families, with
`failed` 71 → 75 and `total` 27026 → 27298 for the whole suite.

What the block cost and what it bought, in the order it is worth remembering:

- **Two of the six families were re-parents of code already running** (`Navigations`,
  `ComplexJson`) and added no test at all; two more (`OwnedNavigations`, and `ComplexJson` again)
  deleted hand-mirrored model code. **Four new families brought 272 tests, 268 of them green.**
- **The handoff's "no golden SQL" claim was verified rather than trusted, and it is true of the
  35 relational bases and false of EF's SQLite classes.** Every `AssertSql` in the 35 is either
  the helper declaration or an empty call; several `*StructuralEqualitySqliteTest` classes are
  nothing but golden SQL. None of those was adopted, and the file in each family says why: the SQL
  is the *backing store's* text, which this client never emits.
- **The `UseTransaction` trap was on the fixture, not the base, in all six families.** Grepping
  the test bases for `ExecuteWithStrategyInTransactionAsync` finds nothing; what needs the
  override is each `*RelationalFixtureBase.UseTransaction` calling `GetDbTransaction()`. Written
  in the same commit as the fixture every time, per CLAUDE.md.
- **EF issue #26708 costs EF two whole SQLite projection classes** (`OwnedNavigations`,
  `OwnedTableSplitting`), and this provider runs both with two tests red in each — answered from
  `OwnedJsonProjectionSqliteTest`, the nearest statement of the same limit.
- **`SqliteStrings.ApplyNotSupported` is 38 of the 42 failures across the whole block.** The one
  distinction worth keeping: the *owned* families need a `TrackAll` short-circuit because
  `AssertOwnedTrackingQuery` intervenes, and the *complex* families do not, because a complex type
  is not tracked as an entity. Measured in each family rather than inferred from the shape.
- **Four are left failing on purpose, all in `OwnedJson`**, and they are two different things —
  three exception-type differences on an already-unsupported path, and one test that fails
  *because it passes*. Both are recorded in `known-failures.txt` and in the file.

### R31 onward — the rest of the relational spec bases

The R4 inventory above splits the remaining 60 four ways. This section works the third group, the
re-parents of families already running, because R25–R30 showed that is where the cheap wins are.

- [x] **R31. `PrimitiveCollectionsQuery` re-parented — and it broke three tests by answering
      them.** The class moves from the core `PrimitiveCollectionsQueryTestBase` onto
      `PrimitiveCollectionsQueryRelationalTestBase`. **The fixture does not move**, which is the
      cheapest shape this project has seen: that base constrains `TFixture` to the *core*
      `PrimitiveCollectionsQueryFixtureBase` and calls no `AssertSql`, so there is nothing for a
      relational fixture to supply.
      **Four hand-mirrored overrides are deleted, and the remark on them was the tell**: it said
      they were mirrored "because this project does not reference" the relational specification
      assembly — which stopped being true at ADR-013. Same stale-by-a-milestone shape as R25's
      `AutoInclude` copies and R30's `ToJson` one.
      165 total before and after; `Passed: 159, Failed: 3` against `Passed: 162, Failed: 0`.
      **All three broke because they pass.** They are the base's three overrides this file never
      carried, each asserting that translation *must* fail, and each now reporting
      *"Assert.Throws() Failure: No exception was thrown"*:
      `Parameter_collection_in_subquery_and_Convert_as_compiled_query`,
      `Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query`,
      `Column_collection_equality_inline_collection_with_parameters`.
      **They are one defect on EF's side, and EF's own TODO states it**: indexing an array becomes
      a subquery with a `CAST` over it, the type-mapping inference from the other side does not
      propagate inside, and the parameter is left without a mapping. This provider does not reach
      that state, and the base tests' own result assertions hold — so the answers are right rather
      than merely un-thrown, measured rather than inferred.
      Not overridden: there is no grandparent to call, and asserting the correct behaviour to turn
      the red green would be overriding a spec test to make the suite green.
      `failed` 75 → 78, `total` unchanged. Compliance missing bases 60 → 59.
      **With R29's `OwnedJson.Associate_with_parameter_null` this makes four standing failures of
      the "a query this provider answers that other EF providers refuse" kind — enough to be worth
      a `website/docs/limitations.md` entry**, which is tracked as its own step because that file
      needs the humanizer pass.

## Phase S — the query parameters still inlined as SQL literals (#62)

**Not a milestone.** #59 fixed two shapes of one defect and a sweep counted what survived: 379
inlined substitutions in four categories, of which #62 says "None has been checked against the
server's SQL. Each is a hypothesis."

**No product diagnostic was needed to check them, and #49 was closed for saying otherwise.**
`Sqlite/ServerParameterizationTest.cs` already runs a query twice — over the wire and directly on
the server context — and compares the two statements, with parameter names normalized. The
provenance problem that would justify a counter inside `Substitute` does not exist inside a test,
because the test author wrote the query.

- [x] **S1. Check categories 2 and 3.** `<commit 62527cc>` Four cases. Both hypotheses correct:
      a `HashSet`, an `ImmutableArray` and a `ReadOnlyCollection` reached the store as
      `IN ('alpha', 'gamma')` where the direct query got `IN (@p, @p)`, and `Where(b => b == blog)`
      reached it as `"b"."Id" = 2` where the direct query got `= @p`. Committed red, as Q1 did.
      `failed` 11 -> 15, `total` 22668 -> 22672.
- [x] **S2. Fix category 3, the collection types.** The client's guard asked whether a `List<T>`
      satisfies the declared type. The far side can do more than that: `ConstructCollection` also
      reaches a single-argument constructor, a set interface, a static `CreateRange` and an
      add-to-new loop. The fix asks the rebuilder itself, through a new
      `DynamicValueMapper.CanRebuildCollection`, so the two sides cannot drift — and
      `IOrderedEnumerable<T>` is still refused, because nothing in `ConstructCollection` produces
      one. `failed` 15 -> 12, FIXED the three collection cases, BROKEN none. 22672 / 22483 / 12 /
      177 (`issue62-fix-collections`). Trim ratchet 89 <= 89, unchanged.
- [x] **S3. Fix category 2, the entity constant.** `Substitute` excludes an entity-typed parameter
      because EF expands entity equality into a key comparison itself. The measurement shows the
      expansion then carries a *literal* key: EF reads the key off a `ConstantExpression` eagerly,
      where a member read would have been parameterized. The wire already sends an entity by
      reference and rebuilds it with its key (`MaterializeEntityReference`), which is what makes
      boxing one plausible. It was the change most likely to cost something elsewhere and it cost
      nothing: `failed` 12 -> 11, FIXED the entity case, BROKEN none. 22672 / 22484 / 11 / 177
      (`issue62-fix-entity`). Trim ratchet 89 <= 89, unchanged.
- [x] **S4. Check categories 1 and 4.** Both hypotheses correct, so **all four of #62's categories
      are now checked and all four are real**. A converted struct key reaches the store as
      `"s"."Id" = 7`, and a complex value as `"a"."Address_City" = 'Oslo'`, where the direct query
      gets `@p` in both places. Two entities added to `SqliteSmokeContext` to carry them, as
      `GuidKeyed` was added for #59, and adding them broke nothing. Committed red. `failed` 11 ->
      13, `total` 22672 -> 22674.
- [x] **S5. Fix category 1, the converted struct key.** This is the one #59 tried and backed out
      of: boxing on the declared type alone broke 21 `KeysWithConvertersInfoCarrierTest` tests with
      "Object must implement IConvertible", because the box's value must round-trip and a converted
      key is not a wire primitive. #62's question is whether the value can cross in its *converted*
      form. **Read `known-failures.txt`'s #59 entries before starting**: the first attempt at this
      is written up there and the reason it failed is not obvious from the code. **Done, and
      reproducing #59's failure first is what located the fault**: the box is the problem, not the
      value. `ParameterBox<object>` loses the runtime type, `ParameterBox<BytesStructKey>` does not,
      and that second shape is already green wherever the declared type is the struct. So the key is
      boxed on its runtime type and an `Expression.Convert` restores the node type. `failed` 13 ->
      12, all 21 `KeysWithConverters` tests green. 22674 / 22485 / 12 / 177
      (`issue62-fix-structkey`). Trim ratchet 89 <= 89.
- [x] **S6. Fix category 4, the complex value.** EF splits a complex value into one parameter per
      property; this side sends one constant. Unlike S5 there is no failed attempt on record, and
      the entity fix in S3 is the nearest precedent. **Done. A complex property is not in
      `GetProperties()` but in `GetComplexProperties()`, and the predicate read only the first.**
      The fix broke `Contains_with_nested_and_composed_operators`, which asserted a throw; two
      narrowings failed to restore the throw, and running the base directly showed its own assertion
      passes. So that override was a workaround whose limitation has gone, and it is deleted —
      `website/docs/limitations.md` needs the query added to its "answers where others refuse"
      section. `failed` 12 -> 11. 22674 / 22486 / 11 / 177 (`issue62-fix-complex-full`). Trim
      ratchet 89 <= 89.
- [x] **S7. `limitations.md`: one more query this provider answers and other providers refuse.**
      User-facing, so the humanizer skill ran on the result. The section says three scenarios rather
      than two. The page was ten words over its 700-word budget once the third was added, so the
      pass had to earn them back; it measures 698 and `doc-links.py` passes with anchors.
- [x] **S8. Eight cases pinning where the wire could change the statement's shape.** #62's four
      categories were all found by counting substitutions, and a count only finds a value that was
      already inlined. These come from the other direction: four on the comparison (a null string, a
      `StartsWith` argument, an empty `Contains` list, a value in the projection) and four on
      structure (`Include`, `GroupBy`, `Any` over a navigation, a nullable value type). **All eight
      passed on the first run**, which is the result rather than a disappointment: eight assumptions
      became eight assertions. `failed` unchanged at 11, `total` 22674 -> 22682.

## Phase T — the client model's missing struct complex types (#69)

**Not a milestone.** R13a's move of `PropertyValues` to Tier B made 50 tests real and 45 of them
failed with `EF.Property<T> may only be used within Entity Framework LINQ queries`, raised on the
server. #69 called it "does not survive the wire on the store-values and `Reload` path". It was
not a wire defect: it was a **model** defect on the client, and the wire was faithful.

- [x] **T1. The client type-mapping source claimed every value type as a scalar.**
      `<commit red PENDING, green PENDING>` `InfoCarrierTypeMappingSource.FindMapping` returned a
      mapping for `clrType.IsValueType` unconditionally, so `PropertyDiscoveryConvention` claimed
      the **struct** complex types (`Culture.License`, `Manufacturer.Tog`, `License.Tag`, …) as
      primitive properties before `ComplexPropertyDiscoveryConvention` could see them. The client
      model then lost every nested complex property whose CLR type is a struct while the SQLite
      server's model — whose mapping source returns null for those structs — kept them. On that
      divergence `EntityFinder.BuildProjection`, which `GetDatabaseValues()` and `Reload()` run
      against the **client** model, emitted `EF.Property<TStruct>(complex, "License")` for a value
      the server can only read as a complex type; the server materialised the whole entity and
      client-evaluated the projection into `EF.Property` at shaping. Server SQL confirmed it:
      `SELECT <all 30 Building columns> … WHERE "BuildingId" = @Value` where a working projection
      gives `SELECT "b"."Name"`. **Established before the fix**, as CLAUDE.md requires — the client
      and server complex-type trees were dumped side by side, and the client's was missing `License`
      and `Tog` at every level. The fix maps a value type as a scalar only when EF recognises it as
      one: it has a `JsonValueReaderWriter` (every BCL primitive, `Guid`/`DateTime`/`decimal`/…, and
      every enum) — a plain `struct` does not. Converted struct keys are unaffected:
      `KeysWithConvertersTestBase` configures every one with an explicit
      `Property(e => e.Id).HasConversion(...)`, so it never depends on this source to be classified
      as a property. `failed` 122 -> 72, **`total` unchanged at 27021**. FIXED 50 — the 45 #69
      failures and the 5 sibling `PropertyValues` failures on the current/original-values path (two
      `complex property 'Building.Culture#Culture.License' could not be found`, three
      `Collections differ`) that the same divergence caused. BROKEN none. REASONS: three whole
      classes removed (`45× EF.Property`, `3× Collections differ`, `2× complex property not found`),
      nothing added. Trim ratchet `ours` 89 <= 89, `total` 855, unchanged.
      **Measurement note.** The dev box (8 GB, swap exhausted, an unrelated `ubuntu-desktop-installer`
      snap resident) OOM-killed the test host on every full-suite run, so the suite was run in
      memory-sized `--filter` chunks — a clean partition of the `.csproj`, every chunk completing —
      and the `[FAIL]` names aggregated and diffed against `groupd-entitysplitting`. The count is
      cross-checked two ways (122 baseline minus the 50 fixed; observed fails minus the two
      environment casualties) and both give the same 72 names. The two casualties are
      `NorthwindMiscellaneousQueryInfoCarrierTest.Handle_materialization_properly_when_more_than_two_query_sources_are_involved`
      (sync and async), which throw `System.OutOfMemoryException` serialising this suite's
      known-largest result on a starved box; they pass under normal memory and are not in the
      baseline or the new names file.

## Phase U — `DbUpdateException.Entries` names every entry sent, not the one rejected (#70)

**Not a milestone.** Found by R19's adoption of `NonSharedModelUpdatesTestBase` on Tier B.
`DbUpdateException_Entries_is_correct_with_multiple_inserts` (sync + async) failed: on a server
`SaveChanges` failure the client's `DbUpdateException.Entries` carried **every** entry it sent
rather than the one the store rejected. W5 already documents why the server's own update entries
cannot cross — they belong to a context disposed with the request scope — and matching by key
does not help when the rejected row is `Added` with a store-generated key, so neither side has a
key for it. What identifies it is its **ordinal in the sent batch**, which the server knows.

- [x] **U1. Carry the rejected entries' ordinals over the wire, and prefer them in the re-raise.**
      `src/` change. `ServerSaveChangesExecutor` now wraps `SaveChangesAsync` in a
      `catch (DbUpdateException)` that maps `exception.Entries` back to the client's
      `ChangeEntry.CorrelationId`s (matched on the entity instance, since EF rebuilds the
      `EntityEntry` wrappers) and stashes them on `Exception.Data` under
      `InfoCarrierFaultMapper.FailedCorrelationIdsKey`. `InfoCarrierFault` gains
      `FailedCorrelationIds`; `InfoCarrierFaultMapper.Capture`/`Rehydrate` lift it onto `Data` and
      back, so the in-process path (same object) and the wire path agree.
      `InfoCarrierDatabase.SaveChangesAsync`'s re-raise — for `DbUpdateException` and, as a strict
      improvement, `DbUpdateConcurrencyException` — now does `FailedByOrdinal(exception, sent) ??
      Translate(...)`: the ordinal indexes `sent` directly (that is how `CorrelationId` is
      assigned), an out-of-range ordinal is skipped rather than trusted, and only where the tag is
      absent does the old whole-batch fallback stand. The fault path is server → client, the
      trusted direction (`InfoCarrierFaultMapper`'s own reasoning), and the payload is an `int[]`
      of positions — no type resolution, no deserialization surface.
      New HTTP-wire coverage: `NorthwindWritesOverHttpTest
      .A_failed_batch_insert_names_only_the_rejected_row_over_http` (three `Customer` inserts, the
      middle a duplicate primary key; asserts a single `Entries` naming that one, in one request).
      TransportTests is a separate project and not in the spec baseline.
      `failed` 72 → 70, `total` unchanged at 27023, FIXED 2, BROKEN none. `test/measure.sh` gate
      and `eng/trim-ratchet.sh` (`ours` 89 ≤ 89). Local runs: the two pinned tests pass, the 19
      TransportTests pass, and `OptimisticConcurrency` / `StoreGeneratedFixup` / `GraphUpdates`
      (1763/1769) are unchanged; a full run OOMs this box, so the CI Spec ratchet confirms.
