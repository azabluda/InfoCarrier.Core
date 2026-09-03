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
      | Needs a relational **client** API (#60) | ~18 | The five `FromSql`/`SqlQuery`/`SqlExecutor`/`ToSqlQuery` bases and the six `*SplitQuery*`/`*SplitInclude*` ones, plus `UdfDbFunction`. **Out of scope by the owner's decision**; do not design around it. |
      | Asserts the **client's relational model** | ~3 | The nine `RelationalModelBuilderTest+*` nested bases, `ModelBuilding101RelationalTestBase`, `Scaffolding.CompiledModelRelationalTestBase`, and `JsonTypesRelationalTestBase` — the last measured in R23 at 104 red of 576 and reverted. This provider does not build a relational model on the client (M9), so these are the ADR-013 "blocked wholesale" shape rather than the "costs one test" shape. |
      | **Re-parents of families already running** | ~11 | `Query.Translations.String*`/`Miscellaneous*`, `Update.UpdatesRelational`, `Query.ComplexTypeQueryRelational`, `Query.JsonQueryRelational`, `Query.PrimitiveCollectionsQueryRelational`, `Query.OwnedQueryRelational`, `Query.SpatialQueryRelational`, `Query.NorthwindMiscellaneousQueryRelational`, the two `BulkUpdates.*Relational`, `ManyToManyTrackingRelational` (R16 examined and deferred). **This is the R25/R26/R30 shape and is where the cheap wins are.** Several also close a `ITestSqlLoggerFactory` fixture entry as a side effect, as R25 and R30 each did. |
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

- [x] **R32. `ComplexTypeQuery` re-parented — free, and it retires the same wrong remark a third
      time.** The class moves onto `ComplexTypeQueryRelationalTestBase` and the fixture onto
      `ComplexTypeQueryRelationalFixtureBase`. **The remark that stood on this file said the
      relational base "asserts SQL, which a client with no database has none of". It does not**:
      its six overrides each assert an *exception message* and then call an empty `AssertSql()`
      meaning "nothing was executed". That is the identical C0-era misreading R30 corrected for the
      `ComplexJson` bases, and here it had cost six hand-written copies of overrides this file
      could have inherited — the two `Subquery_over_*` and the four
      `Concat_`/`Union_two_different_*`. All six deleted.
      `Passed: 150, Failed: 0, Total: 151`, identical before and after, so `failed` and `total`
      both stand. **Two compliance entries close for one commit**: missing bases 59 → 58 and
      missing fixtures 20 → 19, the second because the relational fixture base supplies
      `ITestSqlLoggerFactory`.
      What stays is EF's `ComplexTypeQuerySqliteTest` pair, the two `ApplyNotSupported` ones the
      relational base does not carry.

- [x] **R33. `NonSharedModelBulkUpdates` re-parented — twelve new tests and all of them green.**
      `NonSharedModelBulkUpdatesRelationalTestBase` takes the same `NonSharedFixture` this class
      already used and adds six tests, which is twelve with the async arm. 22 → 34 tests,
      `Passed: 34, Failed: 0, Total: 34`. `failed` unchanged, `total` +12. Compliance missing bases
      58 → 57.
      **What separates it from its Northwind sibling is worth stating**, because the two sit next
      to each other in the same file: this base carries no `FromSql`, no `AsSplitQuery` and no
      `RelationalTestStore` cast, and `NorthwindBulkUpdatesRelationalTestBase` carries all three.

- [ ] **R34. Four of R30b's "cheap re-parent" candidates are actually #60-gated, and the probe is
      what found it.** The inventory put them in the group where the cheap wins are; reading the
      bases moved them:

      | Base | What gates it |
      |---|---|
      | `Query.JsonQueryRelationalTestBase` | adds **seven** `FromSqlRaw` tests |
      | `Query.OwnedQueryRelationalTestBase` | adds eight `AsSplitQuery` tests, one `FromSqlRaw`, **and** its fixture declares `public new RelationalTestStore TestStore => (RelationalTestStore)base.TestStore` |
      | `Query.NorthwindMiscellaneousQueryRelationalTestBase` | adds two `AsSplitQuery` tests |
      | `BulkUpdates.NorthwindBulkUpdatesRelationalTestBase` | adds two `FromSqlRaw` tests, and its fixture carries the same `RelationalTestStore` cast |

      **Not adopted**, per the standing instruction not to adopt a base that needs a relational
      client API. `InfoCarrierTestStore` derives from `TestStore` and not from
      `RelationalTestStore`, so that cast is an `InvalidCastException` rather than a missing
      feature — the shape ADR-013's amendment calls blocked when every route runs through it.
      **The correction to make in the R4 notes: the "re-parents of families already running" group
      is ~11, not ~15, and the #60 group is ~18, not ~14.**

- [x] **R35. `ManyToManyTracking` moved to Tier B — and it found a real defect.** R16 examined this
      move and deferred it. What makes it worth taking is R13a's lesson: **the move is what makes
      the tests real.** 200/0/200 → 200/1/201.
      **The `UseTransaction` override is written in the same commit as the store switch**, and this
      is the case CLAUDE.md D6 is about rather than a formality: the core base routes **47** call
      sites through `ExecuteWithStrategyInTransactionAsync`, each opening one transaction every
      other context must enlist in. On Tier A it was ignored; here it is real, and the run
      completes in six seconds instead of producing D6's lock-timeout shape.
      **Two things the Tier A class asserted about itself are deleted, because the move makes them
      false.** The `ExecuteWithStrategyInTransactionAsync` reseed override said *"without a real
      transaction there is no rollback to undo the test's mutations"* — true of InMemory, false
      here. And `SupportsDatabaseDefaults => false` said *"the backend is the InMemory store, which
      has no database default values"*; the fixture now declares the six `HasDefaultValue` /
      `HasDefaultValueSql` statements EF's own SQLite fixture declares.
      The relational base's one added test, `Many_to_many_delete_behaviors_are_set`, **passes**.
      **`Can_delete_with_many_to_many` breaks, and it is a real defect of this provider.** It
      deletes an `EntityOne` and an `EntityTwo` whose `JoinOneToTwo` rows are cascade-deleted, and
      the server reports `SQLite Error 19: 'FOREIGN KEY constraint failed'`. The join rows must be
      deleted before their principals — a relational provider's `CommandBatchPreparer`
      topologically sorts for exactly this — and something in the wire round trip does not preserve
      that order. **InMemory enforces no foreign key, so on Tier A this passed while saying
      nothing.**
      **Not convergence, and that was checked rather than assumed**: `ManyToManyTrackingSqliteTest`
      declares zero test overrides, so EF's own SQLite run passes it. Left failing per ADR-004.
      **Worth an issue of its own** — same class of find as Phase U, which R19 turned up the same
      way.
      `failed` 78 → 79, `total` +1. Compliance missing bases 57 → 56, fixtures 19 → 18.

- [x] **R36. All nine `RelationalModelBuilderTest` bases adopted — and the R30b inventory was
      wrong about them.** It put these in the "asserts the client's relational model, therefore
      blocked wholesale" group, on the strength of their names. **Reading them is what corrected
      that**, and the correction is large: nine compliance entries close in one commit, missing
      bases 56 → 47.
      **Three of the nine are declared with an empty body** — `RelationalOneToManyTestBase`,
      `RelationalManyToOneTestBase`, `RelationalOneToOneTestBase` add no test at all — and
      `RelationalModelBuilderFixture` is `: ModelBuilderFixtureBase;` and nothing else, so the
      fixture move is free too.
      637/0/703 → 678/4/748: **+45 tests, 41 of them green.**
      **The four reds are two kinds.** Three are the M9 boundary proper, where this provider does
      not build a relational model on the client and an assertion about table splitting,
      owned-type identity under it, or stored-procedure mapping has nothing to read:
      `OwnedTypes.Can_use_table_splitting_with_owned_reference`,
      `OwnedTypes.Can_configure_owned_type` and
      `OwnedTypes.Can_use_sproc_mapping_with_owned_reference`. **That is the "costs a few tests"
      side of ADR-013's amendment rather than the "blocked wholesale" side** — the same
      distinction R24 drew for one test and R23 drew against `JsonTypesRelationalTestBase`'s 104.
      The fourth, `ComplexType.Complex_properties_can_be_configured_by_type`, **fails because it
      passes**: *"Assert.Throws() Failure: No exception was thrown"*. That is now the fourth place
      in this phase where a spec base asserts a failure this provider does not have.
      `failed` 79 → 83, `total` +45.
      **The lesson, and it is R4's lesson restated: a group classified from its name is not
      classified.** Two of the four groups in the R30b inventory have now moved when read — four
      candidates out of the cheap group into #60 (R34), and nine out of the blocked group into
      adopted (this step).

- [x] **R37. `limitations.md` gains the two scenarios this phase found.** The page's promise is
      that it names *every* scenario in the suite that does not behave as a normal provider does,
      so a phase that adopts new spec bases can oblige it to grow. Two did:
      **comparing an owned JSON entity against a null parameter** (R29, EF issue #36401 — EF
      returns the wrong rows, this provider the right ones) and **comparing a column collection
      against an inline collection of parameters** (R31, which EF's relational providers leave
      without a type mapping). The section goes from three scenarios to five.
      **Its heading changed with it**, and that is not cosmetic: it said "that other providers
      reject", and one of the two new cases is a provider answering *wrongly* rather than
      refusing. It now reads "that other providers do not".
      **The page's word budget is raised 700 → 750**, in `docs/doc-style.md` and in
      `eng/doc-words.py`, which the script's own header says to keep in step. The page reads 730.
      This follows the precedent set for `security` and `guide/errors` on 2026-08-24: a dated
      entry with the reason, not a silent bump. **The reason is specific to this page** — its
      length is a function of what the suite covers rather than of how it is written.
      **Left alone deliberately: the `Total tests: … Failed: 9` block**, which is measured against
      the released `10.0.0` and is not this branch's to move. The behaviours described are 10.0.0's
      behaviours; what changed is only that adopted bases now cover them.
      Humanizer pass run on the result, per the standing rule for `website/`. `eng/doc-links.py`
      and `eng/doc-words.py` both pass.

- [ ] **R38. The "plausibly new or standalone" group probed — and most of it is blocked for
      reasons that only reading it shows.** No code in this step; it is the classification R4 asks
      for, on the group R30b sized at ~19. Each was read for the three checks R25–R30 produced.

      | Base | Verdict |
      |---|---|
      | `Query.WarningsTestBase` | **The one worth trying.** 11 tests, and the base itself carries no blocker. What stands in the way is its `TFixture : NorthwindQueryRelationalFixture<NoopModelCustomizer>` constraint, and that fixture declares `public new RelationalTestStore TestStore => (RelationalTestStore)base.TestStore`. **ADR-013's amendment says such a cast blocks only when every route runs through it, and `WarningsTestBase` never touches `Fixture.TestStore`** — so this is an experiment with a real chance, not a closed door. |
      | `ConcurrencyDetectorEnabledRelationalTestBase`, `…DisabledRelationalTestBase` | **#60.** Each adds exactly one test and that test is `FromSqlRaw`. Everything else about them is clean: 22 lines, the constraint is the *core* fixture, and the `RelationalTestStore` use is a safe `as` cast with a fallback rather than a hard one. |
      | `Query.NonSharedPrimitiveCollectionsQueryRelationalTestBase` | **Blocked, and it is the #60 shape rather than a new one.** It declares `protected abstract DbContextOptionsBuilder SetParameterizedCollectionMode(…)` and uses it **ten** times; SQLite implements it as `new SqliteDbContextOptionsBuilder(optionsBuilder).UseParameterizedCollectionMode(…)`, a relational option on the *client's* builder. `UseInfoCarrier` has none. Routing it to the backend harness instead is design work, not an adoption. |
      | `TwoDatabasesTestBase` | **Blocked by the client having no database**, which is the honest category rather than a defect: it declares `protected abstract string DummyConnectionString` and `CreateBackingContext(string databaseName)`. |
      | `LoggingRelationalTestBase<TBuilder, TExtension>` | **Blocked by design.** Every test configures `MaxBatchSize`, `CommandTimeout`, `UseRelationalNulls`, `MigrationsAssembly` or `MigrationsHistoryTable`, and `TExtension` is a `RelationalOptionsExtension`. This provider's options extension is not one, and M9 is why. |
      | `TransactionTestBase`, `NullSemanticsQueryTestBase`, `OwnedEntityQueryRelationalTestBase`, `QueryNoClientEvalTestBase`, `SharedTypeQueryRelationalTestBase` | Not read line by line. Each has between one and seven hits for `FromSql`/`AsSplitQuery`/`RelationalTestStore`/`GetDbTransaction`, so each needs the same per-base read rather than a group verdict — which is the whole lesson of R34 and R36. |

      **What the three probes together say about the R30b inventory.** It has now been corrected
      twice by reading and once more here: four candidates left the cheap group for #60 (R34), nine
      left the blocked group for adopted (R36), and this group turns out to be mostly blocked
      rather than mostly open. **A name is not a classification, and the cost of finding out is
      about twenty minutes a base.**

- [x] **R39. `WarningsTestBase` adopted — R38's one candidate, and it landed green.** 11 tests,
      `Passed: 11, Failed: 0, Total: 11`. `failed` unchanged, `total` +11, compliance missing bases
      47 → 46.
      **The obstacle was the fixture, not the base, and ADR-013's amendment is what decided it.**
      `WarningsTestBase` constrains `TFixture` to `NorthwindQueryRelationalFixture`, which declares
      `public new RelationalTestStore TestStore => (RelationalTestStore)base.TestStore`, and
      `InfoCarrierTestStore` is a `TestStore` rather than a `RelationalTestStore`. The amendment
      says such a cast blocks a base only when *every route* runs through it. No test in this class
      reads `Fixture.TestStore`.
      **So `NorthwindQueryInfoCarrierSqliteFixture` is re-parented onto
      `NorthwindQueryRelationalFixture`, and that was measured before the new class was written
      rather than assumed**: `Passed: 2460, Failed: 2, Total: 2470` for the whole
      `Sqlite.Query.Northwind` filter before and after, with a **byte-identical failure set**. The
      `new` property is never read, and the base's `AddOptions` — which adds `ConfigureWarnings`
      and `EnableDetailedErrors` — changed nothing. Two of our own members are deleted as
      redundant: the fixture's `TestSqlLoggerFactory` implementation and its `ShouldLogCategory`
      override, both of which the relational base supplies.
      **What the base covers is worth naming**: the diagnostics a query raises rather than its
      answer. Those warnings come from the query pipeline, which on this provider runs on the
      *server*, so this is a direct check that a server-side diagnostic still reaches a client with
      no database.
      **And it re-opens the R34 verdicts as a question.** Two of the four bases R34 set aside —
      `NorthwindBulkUpdatesRelationalTestBase` and `OwnedQueryRelationalTestBase` — were set aside
      partly for this same cast. #60 still gates them on their `FromSql`/`AsSplitQuery` tests, so
      the verdict stands, but the *cast* is no longer part of the reason.

- [x] **R40. The defect R35 found, fixed — and it is one line.** `Can_delete_with_many_to_many`
      passes; the class is `Passed: 201, Failed: 0, Total: 201`. Full local run
      `Passed: 27050, Failed: 82, Total: 27367`, **FIXED 1, BROKEN 0**, diffed by name against
      `known-failures.names.txt` rather than read off the count.
      **The cause was a comment that was right about every case it had been tested on.**
      `ChangeEntryMapper.ToChangeEntry` sent original foreign-key values for `Modified` entries
      only, on the stated ground that *"a `Deleted` entry needs no ordering hint, because the row
      it releases is the one being deleted"*. **That is true while a deleted row is only ever a
      dependent, and false the moment one deleted row is a dependent of another.** The test deletes
      an `EntityOne` and an `EntityTwo` in one call and `EntityTwo.CollectionInverseId` points at
      that `EntityOne`; EF's own `ClientSetNull` fixup nulls the FK on the client *before* the
      entry is sent, so the current value carried no edge either, and
      `CommandBatchPreparer` on the server had nothing to order by. It emitted
      `DELETE FROM "EntityOnes"` first and SQLite refused it.
      **Traced rather than guessed, in four steps**: the server SQL log named the failing statement
      once `RelationalEventId.CommandError` was added to it; replaying the server's exact
      statements against a copy of the store reproduced the error and showed `EntityTwos.Id=1` as
      the only row still referencing `EntityOne 1`; a probe on the server's change tracker showed
      `CollectionInverseId` current `null` **and** original `null`, where a single-context EF holds
      `1`; and the one-line widening of the condition fixed it.
      **This is J11's mechanism one case wider.** J11 measured **165** `FOREIGN KEY constraint
      failed` for the same missing original on `Modified` entries, when `ProxyGraphUpdates` first
      reached a store that enforces them. Neither case can appear on Tier A, because InMemory
      enforces no foreign key — which is the argument for the tier moves, stated as a measurement
      rather than a preference.
      **`src/`, so both gates ran**: trim ratchet OK (89 ≤ 89, unchanged) and
      `CI=true … --configuration Release` clean.
      `failed` 83 → 82, `total` unchanged.
      **A second, smaller thing came out of the trace and is kept**: `ServerSqlLog` now subscribes
      to `RelationalEventId.CommandError` as well as `CommandExecuted`. A statement that *failed*
      is the one a diagnostic most needs and the one `CommandExecuted` never carries, so the log
      used to stop at the last statement that worked and stay silent about the one that did not.

- [x] **R41. The last seven bases read one by one — and every one is blocked, for a reason worth
      writing down.** No code. This finishes R38's open list and the two R30b left over, and it is
      the point at which the *unblocked* part of the remaining inventory is exhausted.

      | Base | Verdict |
      |---|---|
      | `Query.NullSemanticsQueryTestBase` | **Probed properly and backed out**, and it is the one that nearly landed: 168 test methods, a *core* fixture constraint, and only one `FromSqlRaw` in 2,325 lines. **The blocker is not in its body.** It declares `protected abstract NullSemanticsContext CreateContext(bool useRelationalNulls = false)`, EF's SQLite class implements it with `new SqliteDbContextOptionsBuilder(options).UseRelationalNulls()`, and **20 of the base's 23 `CreateContext` call sites pass `useRelationalNulls: true`**. That is a relational option on the *client's* builder, which `UseInfoCarrier` has none of. A class was written, failed to compile on the abstract member, and was deleted. |
      | `TransactionTestBase` | **Blocked wholesale**, and this is the shape ADR-013 calls that: `GetDbConnection()`, `GetDbTransaction()`, `UseTransaction(DbTransaction)` and `protected RelationalTestStore TestStore => (RelationalTestStore)Fixture.TestStore` run through the whole of its 44 tests. The client has no database and no connection. |
      | `Query.QueryNoClientEvalTestBase` | #60: two `FromSqlRaw` tests. Its fixture constraint is now satisfied by R39's re-parent, so #60 is the only thing left. |
      | `Query.SharedTypeQueryRelationalTestBase` | #60: one `FromSqlRaw` test, plus one `(RelationalTestStore)TestStore` cast in the same area. |
      | `Query.OwnedEntityQueryRelationalTestBase` | #60: two `AsSplitQuery` tests out of twelve. |
      | `ModelBuilding101RelationalTestBase` | **Blocked wholesale.** Its whole contribution is `GetModelMetadata`, overridden as `new RelationalModelMetadata(context.Model, context.Database.GenerateCreateScript())`. `GenerateCreateScript` is relational-only and *every* test routes through it. |
      | `Scaffolding.CompiledModelRelationalTestBase` | **Blocked, and it is the M9 boundary rather than #60.** Its eleven tests build models with `ToTable`, `SplitToTable`, sprocs, sequences and check constraints, then assert `GetTableName()` on the compiled model. The compiled model here is the *client's*, and this provider does not build a relational one. |

      **The contrast with R36 is the useful part.** Those nine `RelationalModelBuilderTest` bases
      had relational names and turned out adoptable, because most of their content is not
      relational-model assertion. These two have the same kind of name and are genuinely blocked,
      because theirs is. **Neither the name nor the namespace decides it; only reading the base
      does** — and the cost of reading one is minutes.

      **What the #60 rule is now withholding, stated so it can be overruled with numbers rather
      than argued.** Ten bases stand aside for it, and most are cheap in reds:
      `JsonQuery` (7 `FromSql` tests), `OwnedQuery` (8 `AsSplitQuery` + 1 `FromSql`),
      `NorthwindBulkUpdates` (2), `NorthwindMiscellaneousQuery` (2),
      `ConcurrencyDetectorEnabled`/`Disabled` (1 each), `QueryNoClientEval` (2),
      `SharedTypeQuery` (1), `OwnedEntityQuery` (2), and `NullSemanticsQuery` (21, and 168 test
      methods behind them). **The standing instruction is not to adopt a base that needs a
      relational client API, and that is what R34, R38 and this step have all applied** — but the
      trade in every case is a handful of permanently red tests against a much larger body of
      green, and `NullSemanticsQuery` is the one where the ratio is worth the owner's attention.

- [x] **R42. `Updates` moved to Tier B — 28 tests become 36, all green, and the move found a second
      defect of R40's family.** The class re-parents onto `UpdatesRelationalTestBase` and the
      fixture onto its nested `UpdatesRelationalFixture`. `Passed: 36, Failed: 0, Total: 36`; full
      run `Passed: 27058, Failed: 82, Total: 27375`, FIXED none BROKEN none by name. Compliance
      missing bases 46 → 45.
      **Three Tier A overrides are deleted because the move makes them false** — both concurrency
      messages (they were `InMemoryStrings`', and the relational base states them itself), the
      reseed override whose remark said the InMemory store *"has no transaction to roll back"*, and
      EF issue #29875's, which was InMemory's alone. `UseTransaction` is written in the same commit,
      per D6.
      **The defect, and it is R40's mechanism one case wider again.**
      `Swap_filtered_unique_index_values` and `Swap_computed_unique_index_values` swap the values of
      a unique index between two rows, and the store answered
      `SQLite Error 19: 'UNIQUE constraint failed: Products.Name, Products.Price'`.
      **`CommandBatchPreparer` orders by *value* dependencies as well as row ones**: one row must
      release a unique value before another may take it, and the only thing that says which row is
      releasing what is the **original**. `ChangeEntryMapper` sent originals for concurrency tokens
      and foreign keys; neither `Name` nor `Price` is either. The condition now also admits a
      property contained in a unique index, and both tests pass.
      **Not convergence, checked rather than assumed**: `UpdatesSqliteTest` overrides only
      `Save_with_shared_foreign_key` and `Identifiers_are_generated_correctly`, so EF's own SQLite
      run passes both swap tests.
      **One expectation was wrong and the measurement corrected it.**
      `Identifiers_are_generated_correctly` asserts `GetTableName()` on the *client's* model and was
      predicted to hit the M9 boundary. It passes: EF keeps the table name as a core annotation, so
      a client that builds no relational model still has it.
      `src/`, so both gates ran: trim ratchet OK (89 ≤ 89) and Release build clean.

- [ ] **R43. `Translations` priced and NOT moved — 217 overrides, and the reason to hand it over.**
      The last of the tier moves, and the one that should not be taken on a whim. EF's SQLite
      `Translations` suite carries **217 `public override`s across six classes** —
      `StringTranslations` 104, `MathTranslations` 66, `EnumTranslations` 18,
      `MiscellaneousTranslations` 18, `ByteArray` 7, `Guid` 4 — plus `Operators/` and `Temporal/`
      subdirectories. Nearly all of them are SQLite lacking a function, which says something about
      **the store** and nothing about this provider.
      Against that, the Tier A family is 333 tests green today, and what it exercises *is* this
      provider's concern: every scalar type crossing the wire as a constant, a parameter or a
      projected column, which is what `PrimitiveCoercion` and the type allowlist decide.
      **Both relational bases constrain `TFixture` to the *core* `BasicTypesQueryFixtureBase`**, so
      nothing technical blocks it; the cost is the 217 overrides and the judgement is whose green
      means more. A81 says the translating tier — but A81 is about a base that could go either way,
      and this is the one case in the phase where the answer turns on how much store-limitation
      bookkeeping the suite should carry. **Left for the owner, priced rather than argued.**

- [x] **R44. The originals audit — done from the other end, and it comes back NEGATIVE.**
      R40 and R42 each found the same defect shape by accident, three hours apart: EF's
      `CommandBatchPreparer` orders a write batch by values the wire had dropped, and the store
      answered with a constraint failure. **Rather than wait for a third**, this enumerates every
      value the preparer reads from *originals* and diffs that list against what
      `ChangeEntryMapper.ToChangeEntry` sends. The answer is that the list is now complete.

      | What `CommandBatchPreparer` reads from originals | For which state | Sent today? |
      |---|---|---|
      | Dependent foreign-key columns (`AddForeignKeyEdges`, `CreateDependent…`) | Modified, Deleted | **yes** — `IsForeignKey()`, since J11 and R40 |
      | Unique **index** columns (`AddUniqueValueEdges`, first loop) | Modified, Deleted | **yes** — `GetContainingIndexes().Any(IsUnique)`, since R42 |
      | Concurrency tokens (the `UPDATE`/`DELETE` `WHERE` clause, `ModificationCommand.HandleColumn`) | Modified, Deleted | **yes** — `IsConcurrencyToken`, since M4 |
      | **Principal key** columns (`CreatePrincipalEquatableKeyValue/Key`) | Modified, Deleted | no — and it does not matter, below |
      | **Unique constraint** columns, i.e. primary key + alternate keys (`AddUniqueValueEdges`, second loop) | Deleted | no — and it does not matter, below |
      | Whether a column changed (`IsModified`) | Modified | **yes** — `ModifiedProperties` is the primary answer; the original-vs-current compare is only its fallback |

      **The two rows that are not sent are exactly the key properties, and a key property's
      original can never differ from its current.** `Property.GetAfterSaveBehavior()` returns
      `PropertySaveBehavior.Throw` for any property where `IsKey()` — primary *and* alternate —
      and `Property.CheckAfterSaveBehavior` **refuses to configure any other value**, answering
      `KeyPropertyMustBeReadOnly`: *"Key properties are always read-only once an entity has been
      saved for the first time."* `ChangeDetector.ThrowIfKeyChanged` and
      `InternalEntryBase.SetPropertyModified` both raise `KeyReadOnly` on the attempt. So on every
      `Modified` or `Deleted` entry the client could send, original **is** current for every key
      property, and there is nothing for the wire to lose. **Alternate keys were the named suspect
      and they are clear.**

      **A second, independent reason closes the unique-constraint row.** `AddSameTableEdges` already
      orders every `Deleted` command on a table before every `Added` one, reading no value at all.
      `AddUniqueValueEdges`' contribution there is only that its edge is *non-breakable* in a cycle.

      **Two things outside the ordering graph were checked in the same sweep and are recorded
      rather than fixed:**

      - **`ColumnValuePropagator`** (shared-table column maps: table splitting, and a `Deleted` +
        `Added` pair sharing one row identity) compares a `Modified` or `Deleted` entry's
        *original* provider value against a later `Added` entry's current one to decide whether to
        write the column. The wire's original equals the current there, so the two could disagree —
        but only for an entity whose non-key properties were **changed and then deleted in the same
        `SaveChanges`**, which discards the change on every other path. No spec test does it, and
        the effect would be a column written or skipped, not a constraint failure. **Left open and
        named**, not fixed: the fix is "send every original", and C42 already measured the cost of
        widening this condition speculatively at 1 fixed, 2 broken.
      - **Stored-procedure parameters with `ForOriginalValue: true`** take *any* property's
        original, not just a key's or a token's. That is the one place the enumeration above would
        not hold — and it is unreachable, because stored-procedure mapping is not supported here.
        See R47, which classifies `StoredProcedureUpdateTestBase`.

      **The pin.** `SqliteSmokeTest.A_deleted_row_releases_its_alternate_key_before_a_new_row_takes_it`
      and a `Coded` entity with `HasAlternateKey` on the SQLite smoke model. It is the first thing
      on Tier B to send a delete and a colliding insert in one request. **Its own comment says what
      it does not prove** — `AddSameTableEdges` makes it pass either way — because a pin that
      overstates itself is worse than none.
      `test/` only, so `eng/measure.sh` and not the trim ratchet. Measured together with R45;
      figures there.

- [x] **R45. Six provably-inapplicable bases into `IgnoredTestBases` — missing 45 → 39.**
      `InfoCarrierComplianceTest`'s own remarks state the bar: the list is for bases *conceptually
      inapplicable to a remoting provider*, and **"a base that is merely not built yet must stay
      out of the list."** R41 read all six; this records the reading where the gate can use it.
      Each entry carries its reason beside it, in the style of R2's twelve.

      | Base | Why it can never be adopted here |
      |---|---|
      | `TransactionTestBase<>` | `GetDbConnection()`, `GetDbTransaction()`, `UseTransaction(DbTransaction)` and a `(RelationalTestStore)Fixture.TestStore` cast run through all 44 tests. Same reason as the already-listed `TransactionInterceptionTestBase`. |
      | `TwoDatabasesTestBase` | Declares `protected abstract string DummyConnectionString` and `CreateBackingContext(string databaseName)`; its three tests swap one connection string for another inside a `DbConnection` interceptor. |
      | `LoggingRelationalTestBase<,>` | **Cannot be closed at all**: `TExtension` is constrained to `RelationalOptionsExtension` and `TBuilder` to `RelationalDbContextOptionsBuilder<,>`. This provider's options extension is neither, and all nine tests configure `MaxBatchSize` / `CommandTimeout` / `UseRelationalNulls` / `MigrationsAssembly` / `MigrationsHistoryTable` through them. |
      | `ModelBuilding101RelationalTestBase` | Its whole contribution over the core base is `GetModelMetadata`, overridden as `new RelationalModelMetadata(context.Model, context.Database.GenerateCreateScript())`. Every test routes through it. |
      | `Scaffolding.CompiledModelRelationalTestBase` | Asserts `GetTableName()` on the compiled model, which here is the **client's**. M9 removed the relational model. |
      | `JsonTypesRelationalTestBase` | Same boundary: `AssertElementFacets` asserts `FindRelationalTypeMapping()`, `IsFixedLength()` and `GetStoreType()` on the client's model. R23 measured 104 red of 576 on that assumption and reverted. |

      **Nothing moves in `failed` or `total`, and that flat count is the expected result rather
      than a null one.** `All_test_bases_must_be_implemented` is red before and red after — the
      other 39 keep it red — so it is the same one failing test either way and neither baseline
      file changes. What moves is the number the test prints, measured: **45 → 39**.
      `test/` only. Measured together with R44; figures below.

- [x] **R46. `AdHocManyToManyQuery` and `AdHocQueryFilters` re-parented onto their relational bases
      and moved to Tier B — missing 39 → 37, and not one test moves.**
      Both relational bases are **eighteen lines with no tests of their own**: their whole
      contribution over the core base is a `TestSqlLoggerFactory`, a `ClearLog` and an `AssertSql`.
      So this is a re-parent and a store switch and nothing else, and EF's own
      `AdHocManyToManyQuerySqliteTest` and `AdHocQueryFiltersQuerySqliteTest` are twelve lines each
      with **no overrides at all**, so the store asks for nothing either.
      **Moved, not added** — a base belongs to exactly one tier — so the two classes leave
      `InMemory/Query/AdHocQueryInfoCarrierTest.cs` for a new
      `Sqlite/Query/AdHocRelationalQuerySqliteInfoCarrierTest.cs`.
      Tier A before: `Passed: 26, Failed: 0, Total: 26`. Tier B after: `Passed: 26, Failed: 0,
      Total: 26`. Neither core base uses `ExecuteWithStrategyInTransactionAsync`, so D6's
      `UseTransaction` override is not needed and none is written.
      `test/` only.

- [x] **R47. `AdHocAdvancedMappings` moved to Tier B — seven tests added, all green, and two of
      them are worth more than their green.** Missing 37 → 36.
      `Passed: 38, Failed: 1, Total: 39` on Tier A becomes `Passed: 45, Failed: 1, Total: 46` here.
      The one failure is the same pre-existing `Casts_are_removed_from_expression_tree_when_redundant`
      on both tiers — **it is in the baseline and only its namespace changed**, so
      `known-failures.names.txt` is edited in this commit and `failed` does not move.
      EF's own `AdHocAdvancedMappingsQuerySqliteTest` is twelve lines with no overrides, so the
      store asks for nothing.

      **Four of the seven are the only TPT and TPC coverage anywhere in this repository.**
      `CLAUDE.md` names TPT/TPC as the one real gap left by M7's dropped SQL Server tier — *"it
      changes the model, and this provider builds a model on the client too, and no TPT or TPC test
      class exists here at any tier"*. One now does. **What that establishes is bounded and the
      bound is the point**: EF's `Context28196` pair are regression tests for a crash (#28196), so
      they run `Animals.OfType<Pet>().Where(a => a.Species.StartsWith("F"))` against a
      `UseTpcMappingStrategy` and a `UseTptMappingStrategy` model and **assert nothing about the
      result**. So the client builds such a model and the server answers the query without
      throwing. **It does not say TPT or TPC is correct, and no user-facing document may say it
      does** — CLAUDE.md's standing rule about the four withdrawn features applies unchanged.

      **Two more use `AsSplitQuery()`, and this is the finding for #60.** They pass — but they pass
      because the marker is **silently ignored**, not because splitting works. Established rather
      than assumed, as CLAUDE.md requires: `INFOCARRIER_SERVER_SQL=1` on
      `Two_similar_complex_properties_projected_with_split_query1` shows the server executing
      **one** `SELECT` with a `LEFT JOIN`, where a split query is two. A single query gives the
      same answers, so the assertion holds.

      **This narrows #60 rather than obeying it.** The standing instruction is not to adopt a base
      that needs a relational client API, and it was written for `FromSql`, which *throws*.
      `AsSplitQuery` does not throw and does not produce a red — so the four bases withheld only
      for `AsSplitQuery` (`OwnedQuery`'s 8, `OwnedEntityQuery`'s 2, `AdHocNavigations`' 2, and this
      one's 2) are withheld for a cost that has now been measured at **zero red tests**.
      **What it does cost is a silent wrong-shaped answer**: a consumer calling `AsSplitQuery` here
      gets correct results from an unsplit query and no diagnostic at all. That is an owner's
      decision on two counts — whether to adopt those bases, and whether
      [`website/docs/limitations.md`](../../../website/docs/limitations.md) should name the silent
      ignore — and **neither is taken here**.
      `test/` only.

- [x] **R48. `AdHocNavigations` moved to Tier B — four tests added, and the two it breaks are
      convergence.** Missing 36 → 35. `Passed: 21, Failed: 0, Total: 21` on Tier A becomes
      `Passed: 25, Failed: 0, Total: 25` here, after two overrides.
      **R47 is what made this adoptable.** The relational base's one theory has four
      parameterizations and two of them call `AsSplitQuery()`, which is why R41's rule would have
      withheld it. R47 measured that cost at zero red tests, and all four pass.

      **The two newly-red tests are the store, and EF's own SQLite class is the check that says
      so.** `Projection_with_multiple_includes_and_subquery_with_set_operation` and
      `Let_multiple_references_with_reference_to_outer` fail with
      `Translating this query requires the SQL APPLY operation, which is not supported on SQLite`
      — `SqliteStrings.ApplyNotSupported` character for character, which is exactly what
      `AdHocNavigationsQuerySqliteTest` overrides them with. On Tier A the query never reached SQL.
      EF's overrides are adopted, as CLAUDE.md requires.

      **EF overrides a third and this does not, deliberately.**
      `SelectMany_and_collection_in_projection_in_FirstOrDefault` is `ApplyNotSupported` in EF's
      SQLite suite and **passes here**, so adopting that override would turn a green test red. It
      joins the set of queries this provider answers that other EF providers reject — the same
      shape `limitations.md` already names two of. **Checked by running it, not assumed from the
      sibling two.**
      `test/` only.

- [x] **R49. `Types.RelationalTypeTestBase` adopted — 16 tests become 112, and the reason it had
      been passed over was stale on both halves.** Missing 34 of 45. `Passed: 16, Failed: 0,
      Total: 16` becomes `Passed: 112, Failed: 0, Total: 112`: **+96, every one green.** The
      largest single gain in the phase, and it cost a fixture re-parent and nine overrides.

      **The stale reason is the finding.** `TypeInfoCarrierTest`'s own remarks said the core base
      was used because `RelationalTypeTestBase` *"lives in `EFCore.Relational.Specification.Tests`,
      which this project does not reference … and its extra tests assert JSON columns and
      `ExecuteUpdate` — neither of which a client with no database has."* **R1 made the project
      reference that assembly**, and **both features shipped**: `ExecuteUpdate` runs on Tier B in
      `NorthwindBulkUpdates` and JSON columns in `JsonQuerySqliteInfoCarrierTest`. This is
      `CLAUDE.md`'s "before pricing a gap, check whether a sibling of it already works" again, and
      the sibling had been green for milestones.

      **Two things were needed and one of them is D6.** The fixture re-parents onto
      `RelationalTypeFixtureBase<T>`, whose `UseTransaction` is
      `facade.UseTransaction(transaction.GetDbTransaction())`; five of the base's six tests route
      through `ExecuteWithStrategyInTransactionAsync`, and without the override all of them answer
      *"Relational-specific methods can only be used when the context is using a relational
      database provider"* — **82 red of 112, measured before the override and 9 after**. It is
      `public virtual`, which is exactly what ADR-013's 2026-08-30 amendment says still adopts.

      **The nine remaining are convergence, name for name.** Seven
      `ExecuteUpdate_within_json_to_nonjson_column` (EF #36688: SQLite cannot do it for a type
      other than string, numeric or bool) and two `Query_property_within_json` (EF #36749: a
      string-representation discrepancy between EF's JSON and `Microsoft.Data.Sqlite`'s). **The
      nine this provider failed and the nine EF's own `SqliteMiscellaneousTypeTest` /
      `SqliteTemporalTypeTest` override are the same nine**, checked one by one rather than
      inferred from the family. EF's overrides are adopted verbatim.
      `test/` only.

- [x] **R50. `SpatialQueryRelationalTestBase` adopted — no SpatiaLite, no store change, and the
      fixture gate moves for the first time.** Missing bases 34 → 33, **and
      `All_query_test_fixtures_must_implement_ITestSqlLoggerFactory` 18 → 17.**
      `Passed: 168, Failed: 0, Total: 168`, unchanged, because the base adds no tests.

      **It was priced on its name.** The handoff had this last and time-boxed, on the assumption it
      needs a package reference and a native library. **Reading it costs less than that assumption
      did**: `SpatialQueryRelationalTestBase<TFixture>` is **fourteen lines**, constrained on the
      *core* `SpatialQueryFixtureBase`, and its only member is
      `CreateQueryAsserter => new RelationalQueryAsserter(...)`. `RelationalQueryAsserter` differs
      from the core one by calling `TestSqlLoggerFactory.OutputSql()` when an assertion fails — a
      diagnostic. Nothing spatial, nothing relational about the store.
      The whole cost is `ITestSqlLoggerFactory` on the fixture, which
      `InfoCarrierTestStoreFactory.CreateListLoggerFactory` has satisfied since R3.

      **Which is why the second compliance gate moved.** That test lists 18 query fixtures with no
      `ITestSqlLoggerFactory`; this is the first one to gain it, and the same one-property change
      is available to the other 17. **Not done here**: 17 fixtures is a step of its own, and this
      one was written because the base required it rather than to lower a count.
      `test/` only.

- [x] **R51. The five of the eleven that do not adopt, classified — and none is classified on its
      name.** No code. This finishes the unread list the phase opened with: **six adopted (R46–R50),
      five blocked, none left unread.**

      | Base | Verdict, and the member that decides it |
      |---|---|
      | `Update.StoredProcedureUpdateTestBase` | **Blocked wholesale.** It declares **28 `public abstract Task`** members — one per test — and each provider implements them by mapping a real stored procedure (`InsertUsingStoredProcedure` and friends). Stored-procedure mapping is not supported here. **This is also the one hole R44's audit named**: a sproc parameter with `ForOriginalValue: true` takes *any* property's original, which is the single place `ToChangeEntry`'s condition would not hold — and it is unreachable for exactly this reason. |
      | `Update.JsonUpdateTestBase<>` | **Blocked by ADR-013, and re-confirmed against EF 10 rather than taken from the record.** `public void UseTransaction(DatabaseFacade, IDbContextTransaction) => facade.UseTransaction(transaction.GetDbTransaction())` at line 3674 — **non-virtual**, and **every one of its 136 tests** routes through `ExecuteWithStrategyInTransactionAsync(CreateContext, UseTransaction, …)`. C81 measured it at 142 of 142 failing and `JsonOwnedCollectionUpdateInfoCarrierTest` is the hand-written substitute that exists because of it. Contrast R49: `RelationalTypeFixtureBase`'s is `public virtual`, and that one adopts. |
      | `Update.StoreValueGenerationTestBase<>` | **Blocked three ways, independently.** (1) The fixture's `OnModelCreating` calls `context.GetService<ISqlGenerationHelper>()` — a relational service this client has not registered since M9. (2) It configures `HasComputedColumnSql` on the client's model. (3) **Every test asserts the backing store's command shape through the client's logger**: `Assert.Equal(ShouldExecuteInNumberOfCommands(…), Fixture.ListLoggerFactory.Log.Count(l => l.Id == RelationalEventId.CommandExecuted))`, plus `TransactionStarted`/`TransactionCommitted`. The client executes no `DbCommand` and starts no store transaction; both happen on the server, on its own context and its own logger. **What this base tests is the relational update pipeline's batching, which is EF's concern on the server and never crosses this wire.** |
      | `Query.AdHocMiscellaneousQueryRelationalTestBase` | **#60, and the blocker is an abstract member rather than a test body** — `NullSemanticsQueryTestBase`'s shape exactly (R41). It declares `protected abstract DbContextOptionsBuilder SetParameterizedCollectionMode(DbContextOptionsBuilder, ParameterTranslationMode)`, which EF's SQLite class implements as `new SqliteDbContextOptionsBuilder(optionsBuilder).UseParameterizedCollectionMode(…)` — a relational option on the **client's** builder. A second abstract member, `Seed2951`, is `context.Database.ExecuteSqlRawAsync(...)` on the client. **Stays visible, not ignored**: #60 is undecided, and R2's rule is that an undecided base must keep being reported. |
      | `Query.NorthwindDbFunctionsQueryRelationalTestBase<>` | **Not blocked — gated on a step of its own, and worth stating as such.** Its only real requirement is `where TFixture : NorthwindQueryRelationalFixture<NoopModelCustomizer>`, and that fixture declares `public new RelationalTestStore TestStore => (RelationalTestStore)base.TestStore`. Under ADR-013's 2026-08-30 amendment a cast like that blocks a base **only if a route runs through it**, which is not established here — but adopting means re-parenting `NorthwindQueryInfoCarrierFixture` onto a relational fixture base, and **every Northwind class in the suite hangs off it**. That is a change to price on its own, not a substep. Its two abstract members are trivial (`CaseInsensitiveCollation` / `CaseSensitiveCollation`, `"NOCASE"` and `"BINARY"` on SQLite). |

      **Two of the five look to me like `IgnoredTestBases` candidates and neither is added here.**
      `JsonUpdateTestBase` is the same ADR-013 shape as the already-listed `TransactionTestBase`,
      and `StoreValueGenerationTestBase` asserts a store's command batching through a client that
      issues no commands. Both would meet the "conceptually inapplicable" bar. **Adding to that
      list is a permanent change to what the gate reports**, the owner named exactly six for R45,
      and neither of these was among them — so they are classified and left visible.

- [x] **R54. The fixture gate closed — 17 → 0, and `failed` 82 → 81.**
      `All_query_test_fixtures_must_implement_ITestSqlLoggerFactory` is **green**, which is the
      first of the two compliance tests to go green at all. Full run
      `Passed: 27167, Failed: 81, Total: 27483`; **FIXED 1** (that test), **BROKEN none**, `total`
      unchanged. R50 found the change is one member and did it for one fixture because a base
      required it; this does the other seventeen. Fifteen declarations edited, two inherit it.

      **The member is real, not a suppression, and that was checked rather than assumed.**
      `InfoCarrierTestStoreFactory.CreateListLoggerFactory` returns `new TestSqlLoggerFactory(...)`,
      so the cast holds for every fixture using an InfoCarrier store factory — which all 17 do —
      and **the same cast is already exercised at runtime** by R50's Spatial fixture and by the
      `Associations` families, whose relational bases read it through `RelationalQueryAsserter`.

      **What it is worth is smaller than green suggests, and each fixture says so in its own
      remarks.** The property observes the **client's** log, and this client has no database and
      emits no SQL; `ServerSqlLog` is where the server's statements can be read. That is the same
      honesty `NavigationsQueryInfoCarrierTests` already carries about `AssertSql`. The gate asks
      whether a fixture can produce a `TestSqlLoggerFactory`, and the answer here is truthfully
      yes.
      `test/` only.

- [x] **R55. `NorthwindDbFunctions` probed and NOT adopted — and R51's reason for withholding it
      was wrong twice over.** No code kept. `Passed: 10, Failed: 0, Total: 10` on Tier A before,
      and unchanged after the revert.

      **R51 said it was "not blocked, gated on a step of its own": re-parenting
      `NorthwindQueryInfoCarrierFixture`, which every Northwind class hangs off. Both halves were
      wrong.**

      1. **The fixture was never the blocker.** R20 had already built a second, relational Northwind
         fixture — `NorthwindQueryInfoCarrierSqliteFixture` — and the Tier B Northwind classes have
         been using it since. The cost was one file, not a suite-wide re-parent. *(A Tier A
         re-parent was probed first, on R51's reading, and measured clean at
         `Passed: 4398, Failed: 4, Total: 4416` with all four failures pre-existing — so the
         `(RelationalTestStore)base.TestStore` cast in `NorthwindQueryRelationalFixture` is
         confirmed not to be on any Northwind route. That probe was reverted as unnecessary.)*
      2. **And it IS blocked, by something R51 never looked at.** With the class moved to Tier B on
         the relational base: **`Passed: 10, Failed: 20, Total: 30`.** The relational base declares
         **exactly ten test methods** — `Collate` ×4, `Greatest` ×3, `Least` ×3 — and **every one
         of the twenty parameterizations fails.** The ten that pass are the core base's, untouched.

      **The mechanism, and it is the useful part.** The thrower is
      `QuerySplitter.RejectClientEvaluation` — **the client, before the wire**:
      `Translation of method 'Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions.Collate'
      failed`. The client's model registers no translation for `RelationalDbFunctionsExtensions`,
      so the query is rejected at the boundary and the server never sees it. The two collations the
      base declares abstract (`"NOCASE"`, `"BINARY"`) are beside the point — nothing gets far
      enough to use them.

      **So this is #60, in its purest form yet.** Not one or two `FromSql` tests inside a useful
      base — a base whose *entire* contribution is relational `EF.Functions`. Twenty permanent reds
      for nothing. **The standing rule applies unchanged and the class is not kept.**
      **What is new is that #60 now has a measured third member**: `FromSql` throws,
      `AsSplitQuery` is silently ignored (R47), and `RelationalDbFunctionsExtensions` is rejected
      at the client boundary with EF's own translation-failure message. Those are three different
      behaviours, and a decision on #60 should name which of them it is fixing.
      No code; `test/` only while probing.

- [x] **R56. `NullSemanticsQuery` ADOPTED — 322 tests, 304 green, and `failed` rises 81 → 99 on
      purpose.** Missing bases 33 → 32; `total` 27483 → 27805.
      **The baseline rise is deliberate and authorised**, which `known-failures.txt` records at
      length. R41 called this the base *"where the ratio is worth the owner's attention"* and
      estimated about 21 permanent reds; measuring gives **18**. FIXED none, BROKEN 18, and the
      BROKEN list is exactly those 18 — nothing else in the suite moved.

      **The abstract member R41 tripped on is implemented by dropping the flag.**
      `protected abstract NullSemanticsContext CreateContext(bool useRelationalNulls = false)`;
      EF's SQLite class writes `new SqliteDbContextOptionsBuilder(o).UseRelationalNulls()`, a
      relational option on the *client's* builder. Ours takes the flag and ignores it, so a test
      that asks for the store's null semantics gets C#'s. **That is the cost, and counting it was
      the point of the step.**

      **The 18 split into two causes and neither is a defect this repository can fix alone.**

      - **12 are #60.** Ten say so in their own names (`..._for_relational_null_semantics`); the
        other two are `Switching_null_semantics_produces_different_cache_entry` and
        `From_sql_composed_with_relational_null_comparison`, the latter also #60's `FromSql` half.
      - **6 are the projection-split boundary working exactly as `CLAUDE.md` says it must**, and
        this was checked rather than assumed. The fixture registers two user-defined functions
        with a **relational** translation —
        `modelBuilder.HasDbFunction(… Cases …).HasTranslation(args => new CaseExpression(…))`, and
        the same for `BoolSwitch`. The client has no relational query pipeline, cannot apply that
        translation, and `QuerySplitter.RejectClientEvaluation` refuses the query rather than
        fetching the table. **The tell that this is the boundary and not a translation bug**: the
        `select`/`projection` siblings all **pass**, because a projection is reassembled
        client-side; only the `filter`/`predicate` ones are refused.

      **Not convergence, and EF's own suite was read before that was ruled out.** EF *does*
      override all six `Case*` tests in `NullSemanticsQuerySqliteTest` — but every override is
      `await base.X(async)` followed by `AssertSql(…)`, so the base assertion **passes** on SQLite
      and the override only adds a golden-SQL check. EF's overrides therefore say these should
      pass. **None is adopted and all six stay red**, which is what `CLAUDE.md` requires of a red
      that is information.

      **This makes #60 the largest single item left in the inventory by test count.** Twelve of
      these 18, plus R55's 20, plus the bases still standing aside for it.
      `test/` only.

- [x] **R57. `Translations` moved to Tier B — the last tier move, and R43's price was off by a
      factor of three.** Missing bases 32 → 30, `total` 27805 → 27809, `failed` **unchanged at
      99**, FIXED none, BROKEN none. 333 tests become 337, **green on both sides of the move**.

      **R43 priced this at 217 overrides and left it for the owner. The measured cost is 65.**
      The 217 is the size of EF's *SQLite* `Translations` suite, and most of those overrides exist
      only to assert golden SQL over a base call that already passes. What a provider actually has
      to write is one override per test that **fails**, and on this store that is 65. **The gap
      between 217 and 65 is the whole argument for measuring a price instead of counting the
      reference implementation's lines** — and it is the same mistake in the same shape as R50,
      where a base was priced as needing a native library on the strength of its name.

      **All 65 are the store and not this provider, established twice over rather than assumed.**
      Every one failed with `The LINQ expression … could not be translated`, naming a member SQLite
      has no function for — `TimeOnly.FromDateTime`, `Math.Round` on `decimal`, `Convert.To*`,
      `Guid.NewGuid`, `DateTimeOffset.ToUnixTimeSeconds`. EF's own SQLite classes override each
      with `AssertTranslationFailed`, **sampled across five classes before any were adopted**. The
      adoption is the second proof: all 65 overrides assert a translation failure and all 65 pass,
      which they could not do if any test had been failing for another reason.

      **Nine overrides were deleted by the move, and that is the tier rule paying out.** Three were
      EF's `StringTranslationsInMemoryTest` — the base asserts `StringComparison.CurrentCulture`
      and `InvariantCulture` are unsupported, which is true of real providers and false of the
      InMemory store that used to sit behind this wire, so the assertion had to be neutered. SQLite
      does not support them, so the base's own expectation is now the right one. The other six are
      A27's hand-transcribed copy of `MiscellaneousTranslationsRelationalTestBase`'s `Random.Next`
      expectations, written because that base was out of reach; the move puts it in reach and the
      base supplies them.
      `test/` only.

- [x] **R59. `AsSplitQuery` is now actually ignored — and the "silent ignore" was never what was
      happening.** `failed` 99 → 95, `total` **unchanged** at 27809. FIXED 4, BROKEN none.
      `Passed: 27478, Failed: 95, Total: 27809`. A `src/` change; both gates ran.

      **The defect is one clause, and it had never executed.** `QuerySplitter.QueryMarkers` has
      listed `"AsSplitQuery"` and `"AsSingleQuery"` since the set was written. The only thing that
      reads that set is `MarkerStrippingVisitor`, whose first test is
      `DeclaringType == typeof(EntityFrameworkQueryableExtensions)` — and both hints are declared
      on `RelationalQueryableExtensions`. **Neither entry could ever match.** The strings look like
      a decision and are a dead branch.

      **What actually happened is not "the marker vanishes", and that mattered.** The marker stayed
      in the tree; `ServerBoundaryAnalyzer` met a call it did not know and cut **below** it. The
      server ran what was under the hint, and the client applied `AsSplitQuery` to a materialized
      `EnumerableQuery` — where EF's own method returns its source untouched, because the provider
      is not an `EntityQueryProvider`. **At the top of a chain that is invisible**: the whole query
      is under the hint and the answer is right, which is exactly what R47 measured and read as a
      silent ignore. **At a nested query root it is not**: the cut is forced below that root, so an
      `Include` or a navigation above it has no server query to read from.

      **Four baseline failures were this, and both places that classified them said something
      else.** `Include_on_derived_type_with_queryable_Cast_split` on TPT and TPC is recorded in
      `known-failures.txt` as returning an **over-included graph**, cited as *"evidence for #60's
      option 3, a marker the server must recognise so an unknown one fails loudly"*. It was not
      evidence for that; it was this. All four are green with the hint removed, and the entry is
      corrected in place. **That also takes the repository's wrong-answer count back to 2** —
      `CLAUDE.md` has said 2 for two milestones while these four were quietly a third case.

      **The other eight of that twelve stay red and stay correctly classified**: they throw
      `ApplyNotSupported`, which a single query genuinely needs and a split query genuinely avoids.
      That half is #60 and this does not touch it.

      **This is not #60 work and adds no relational client API.** The hint is an EF extension
      method that already compiled and already reached this provider; the change is that the
      provider now does what its own `QueryMarkers` list says it does. **What it does not change is
      the consumer-facing question** the owner reserved: a caller who writes `AsSplitQuery` still
      gets correct results from one statement and no diagnostic, and
      [`limitations.md`](../../../website/docs/limitations.md) still does not say so.
      Gates: trim ratchet OK (89 ≤ 89), Release build `5 Warning(s), 0 Error(s)`.

- [x] **R60. Five of the six Split bases ADOPTED — 1110 tests, every one green.** Missing bases
      30 → 25, `total` 27809 → 28919, `failed` **unchanged at 95**, FIXED none, BROKEN none. The
      largest all-green addition in the phase, and it cost R59 plus 47 overrides.

      `NorthwindSplitIncludeQueryTestBase`, `NorthwindSplitIncludeNoTrackingQueryTestBase`,
      `ComplexNavigationsCollectionsSplitQueryRelationalTestBase`,
      `ComplexNavigationsCollectionsSplitSharedTypeQueryRelationalTestBase`,
      `CompositeKeysSplitQueryRelationalTestBase`. All five were withheld by R41's #60 rule.

      **The family splits in two, and the probe is what showed it.** The two Northwind bases append
      `AsSplitQuery` **once, at the top of the chain**; the other three insert it **at every query
      root**, through a `SplitQueryRewritingExpressionVisitor` over `EntityQueryRootExpression`.
      Bare, the first pair measured `Passed: 452, Failed: 20, Total: 472` — eight `ApplyNotSupported`
      that EF's own SQLite classes override and four that were the ignore itself. The second group
      measured **456 red of 638**, with 106 of this provider's own *"reads navigation X, but no
      query sent to the server returned it"* and the rest **wrong answers**. Two orders of magnitude
      apart on the same hint, which is what said the top-of-chain reading was incomplete and sent
      this to R59.

      **With R59 in, all five adopt green.** The remaining 86 reds were every one
      `SqliteStrings.ApplyNotSupported`, and 47 overrides close them.

      **Where not splitting shows is the override count, not the failure count, and that is the
      honest price.** EF's `ComplexNavigationsCollectionsSplitQuerySqliteTest` overrides 20 and ours
      overrides 23. The four extra —
      `Filtered_include_after_different_filtered_include_different_level`, both
      `Filtered_include_complex_three_level_with_middle_having_filter*`, and
      `Skip_Take_on_grouping_element_with_collection_include` — are tests a real split query answers
      in a second statement and a single statement cannot, because SQLite has no `APPLY`. **Each of
      the four is already overridden identically in the unsplit sibling class**, taken from EF's own
      unsplit SQLite class, so the assertion is EF's own about the same query and the same store.
      The shared-type class shows the same four, 17 of EF's becoming 20.
      **One of EF's is deliberately absent from both**, as in the unsplit siblings:
      `Projecting_collection_after_optional_reference_correlated_with_parent` passes here.

      **The sixth, `AdHocQuerySplittingQueryTestBase`, does not adopt** and it is not the hint that
      stops it — see R61.
      `test/` only.

- [x] **R61. `NorthwindMiscellaneousQuery` and `OwnedEntityQuery` adopted — and between them they
      need zero overrides that are not EF's own.** Missing bases 25 → 23, `total` 28919 → 28945,
      `failed` **unchanged at 95**, FIXED none, BROKEN none.
      `Passed: 28614, Failed: 95, Total: 28945`.

      **Both are R41 entries, and R41's reason for both was `AsSplitQuery`.** R59 removes it.

      **`NorthwindMiscellaneousQueryRelationalTestBase`, moved to Tier B: `Passed: 935, Failed: 0,
      Total: 936`.** The largest query base in the suite and the last big one on Tier A. The
      relational base adds exactly two tests, both `AsSplitQuery`, and both pass.

      **Ten overrides were deleted by the move and nine arrived**, which is the tier rule paying
      out twice in one class. Seven of the ten asserted *"Sequence contains no elements"* — true of
      the InMemory store that used to sit behind this wire, which throws where a relational store
      returns an empty sequence, so the base's own expectation could not hold and had to be
      neutered. The other three were EF's own InMemory suppressions of
      `Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_*`, which InMemory
      cannot compose and SQLite can; all three now run and all three pass.
      The nine that arrive are all EF's own `NorthwindMiscellaneousQuerySqliteTest` — five `APPLY`,
      two date-arithmetic, one untranslatable date component, one client-evaluation message whose
      only difference is the fixture named in it. **EF overrides 36 tests and this class overrides
      nine, because the other 27 pass.** An override is written where a test fails, not where the
      reference provider happens to have one.
      **One of the nine is deliberately not EF's**: EF disables
      `SelectMany_correlated_subquery_hard` outright by returning a null `Task`. It fails here for
      the same reason its four siblings do, and `AssertApplyNotSupported` says that where a skip
      would say nothing.

      **`OwnedEntityQueryRelationalTestBase`, moved to Tier B: 37 tests, 37 green, and no overrides
      at all.** R41 withheld it for two `AsSplitQuery` tests and there is nothing left to withhold
      it for. It declares no `UseTransaction` and calls the transaction helper zero times, both
      checked rather than assumed.

      **The Release build caught three errors Debug did not**, which is #90's lesson holding on a
      `test/`-only change for the second time: two `CS8603` on an element sorter returning a
      `DateTime?` and one `IDE0005`.
      `test/` only.

- [x] **R62. The remaining 23 classified, four of them measured and reverted — and the inventory is
      now read end to end.** No code. Every base the compliance gate lists has a verdict and the
      member or the number that decides it, and **not one is classified on its name**.

      **Four were probed and backed out.** Each left no code and each replaced an estimate with a
      measurement, which is R55's model. They are marked *(probed)* below.

      | Base | Verdict, and what decides it |
      |---|---|
      | `Query.FromSqlQueryTestBase` | **#60, and the blocker is an abstract member as much as the bodies.** It declares `protected abstract DbParameter CreateDbParameter(string, object)` — an **ADO.NET** parameter object. This client has no ADO.NET provider, so there is nothing to return. 1,346 lines, every test `FromSqlRaw` or `SqlQueryRaw`. |
      | `Query.SqlQueryTestBase` | **#60, same abstract member.** 1,301 lines of `context.Database.SqlQueryRaw`. |
      | `Query.NorthwindSqlQueryTestBase` | **#60, same abstract member.** Every one of its tests is `Database.SqlQueryRaw` or `Database.SqlQuery`. |
      | `Query.SqlExecutorTestBase` | **#60, same abstract member, plus three more**: `TenMostExpensiveProductsSproc`, `CustomerOrderHistorySproc` and `CustomerOrderHistoryWithGeneratedParameterSproc`, each a real stored procedure the store must define. |
      | `Query.FromSqlSprocQueryTestBase` | **#60 and stored procedures.** Two abstract sproc names, and every test `FromSqlRaw(TenMostExpensiveProductsSproc, …)`. |
      | `Query.GearsOfWarFromSqlQueryTestBase` | **#60 in its purest form: 45 lines, two tests, no abstract member at all.** The only blocker is `FromSqlRaw` — and `NormalizeDelimitersInRawString` reaching `Fixture.TestStore` as a `RelationalTestStore` on both routes. Two tests, both permanently red; nothing else in the base. |
      | `Query.ToSqlQueryTestBase` | **Blocked twice over, and neither is #60.** Its model calls `builder.ToSqlQuery("SELECT * FROM PostStats")` on the **client's** model — a relational mapping this provider does not build (M9) — and it declares `public void UseTransaction(DatabaseFacade, IDbContextTransaction) => facade.UseTransaction(transaction.GetDbTransaction())`, **non-virtual**, which is ADR-013's blocking shape exactly. |
      | `Query.UdfDbFunctionTestBase` | **Blocked twice, and the second is the stronger reason.** Its model registers about thirty `HasDbFunction`, several with `HasTranslation(args => new SqlFragmentExpression(…))`, `new InExpression(…)` and `new SqlFunctionExpression(…)` — relational `SqlExpression` types on the **client's** model, which `QuerySplitter.RejectClientEvaluation` refuses before the wire. That is R55's and R56's mechanism for the third time. **And EF ships no `UdfDbFunctionSqliteTest` and no InMemory one either**, which is `CLAUDE.md`'s stated bar for leaving a base unadopted rather than moving its tier: the functions have to exist in the store, and SQLite has no `CREATE FUNCTION`. |
      | `Query.NonSharedPrimitiveCollectionsQueryRelationalTestBase` | **#60, and it is the same abstract member as `AdHocMiscellaneous` (R51)**: `protected abstract DbContextOptionsBuilder SetParameterizedCollectionMode(DbContextOptionsBuilder, ParameterTranslationMode)`, which EF's SQLite writes as `new SqliteDbContextOptionsBuilder(o).UseParameterizedCollectionMode(…)` — a relational option on the **client's** builder. |
      | `Query.AdHocQuerySplittingQueryTestBase` *(probed)* | **7 green of 13, 6 red, and not adopted.** Its abstract pair is `SetQuerySplittingBehavior` / `ClearQuerySplittingBehavior`, the same client-builder shape again; implementing them as no-ops (R56's route) leaves **six reds in three causes**: three `InvalidCastException` to `RelationalTestStore`, one `Unconfigured_query_splitting_behavior_throws_a_warning` that never throws, and two `NoTracking_split_query_creates_only_required_instances` measuring **1 instance where 2 are created** — the first place a test asserts the *consequence* of splitting rather than the hint. |
      | `Query.SharedTypeQueryRelationalTestBase` | **#60, and R41's "1" is really 4.** 72 lines, three test methods, four parameterizations, all four blocked: a query filter built on `FromSqlRaw`, a `Database.SqlQueryRaw` with a hard `(RelationalTestStore)TestStore` cast, and a third expecting `ClashingSharedType` from `SqlQueryRaw`. |
      | `ConcurrencyDetectorEnabledRelationalTestBase` | **#60, and there is nothing else in it.** 22 lines; the whole contribution is one `FromSql` theory. **Adopting adds two tests and both are red — zero new green.** R41 said "1"; a theory is two. |
      | `ConcurrencyDetectorDisabledRelationalTestBase` | **Identical, line for line.** Two tests, both red, no new green. |
      | `Query.MappingQueryTestBase` | **Blocked on the store, not on any API, and deliberately NOT probed.** Its nested `MappingQueryFixtureBase` supplies a *model* — a cut-down Northwind remapped with `SetTableName`/`SetColumnName`/`SetSchema` — and **no seed at all**, because it expects a prebuilt `Northwind` database. `StoreName` is `"Northwind"`, the same name `NorthwindQueryFixtureBase` uses, and this tier's store is **built from whichever model reaches it first** (`NorthwindInfoCarrierSqliteServerContext`) rather than being a curated file as EF's `SqliteNorthwindTestStoreFactory` is. Probing it could initialize the shared `Northwind.db` from a three-table model and break every Northwind class in the suite — a store-lifetime coupling `CLAUDE.md` explicitly forbids reintroducing. **Adopting it needs a second, separately named Northwind store seeded outside the model**, and that is the price. |
      | `Query.QueryNoClientEvalTestBase` *(probed)* | **11 green of 14, 3 red, and not adopted.** R41 said 2. Two are #60 — one `FromSqlRaw` and one `Doesnt_throw_when_from_sql_not_composed` that dies on `(RelationalTestStore)` first. **The third looked like this provider's own and is not** — see R67, which read the message instead of the assertion failure. |
      | `Query.OwnedQueryRelationalTestBase` *(probed)* | **202 green of 212, 10 red, and NOT adopted — and R41's estimate was wrong in the opposite direction to R43's and R50's.** R41 priced it at "8 `AsSplitQuery` + 1 `FromSql`". **All eight split tests pass** (R59), the `FromSql` theory is two reds, **and there are eight more R41 never saw because it never ran the base**: six `ElementAt`/`ElementAtOrDefault`/`Skip_Take_over_owned_collection` where the relational base expects a row-limiting throw, and **two `Left_join_on_entity_with_owned_navigations` that return the wrong answer**. **EF's own `OwnedQuerySqliteTest` is nineteen lines and overrides nothing**, so not one of the eight is the store. This is the most informative "not yet" in the phase: there is a defect behind it, and it should be diagnosed before the base is adopted. |
      | `Query.JsonQueryRelationalTestBase` *(probed)* | **Not adopted, and the probe does not even compile.** The 7 `FromSql_on_entity_with_json_*` theories are 14 #60 reds as R41 said. What R41 could not see is that `JsonQueryRelationalFixture` declares `public new RelationalTestStore TestStore => (RelationalTestStore)base.TestStore`, which **shadows** the property: `JsonQuerySqliteInfoCarrierTest`'s own model-agreement test reads the backend through `Fixture.TestStore` and stops compiling, and no cast recovers it, because every read of the shadowed property throws on a store that is not relational. **This is a new ADR-013 shape** — the amendment asks whether a route runs through the cast, and here it fails at *compile* time in a derived fixture. Adopting needs 14 reds accepted **and** that test rewired. |
      | `BulkUpdates.NorthwindBulkUpdatesRelationalTestBase` | **#60, and R41's "2" is 4 parameterizations.** Two new theories, `Delete_FromSql_converted_to_subquery` and `Update_FromSql_set_constant`, both `FromSqlRaw`. It also needs `NorthwindBulkUpdatesRelationalFixture` and, per **D6**, a `UseTransaction` override in the same commit: both new theories call `TestHelpers.ExecuteWithStrategyInTransactionAsync(…, Fixture.UseTransaction, …)`. |
      | `Query.AdHocMiscellaneousQueryRelationalTestBase` | R51: #60, abstract `SetParameterizedCollectionMode`. Unchanged. |
      | `Query.NorthwindDbFunctionsQueryRelationalTestBase` | R55: #60, measured at 20 reds for 0 new green. Unchanged. |
      | `Update.JsonUpdateTestBase` | R51: ADR-013, non-virtual `UseTransaction` on every one of 136 tests. Unchanged. |
      | `Update.StoreValueGenerationTestBase` | R51: blocked three ways. Unchanged. |
      | `Update.StoredProcedureUpdateTestBase` | R51: 28 abstract members, one per test, each a real stored procedure. Unchanged. |

      **#60 now has a fourth measured behaviour, and it is an abstract member rather than a call.**
      R55 named three — `FromSql` throws, `AsSplitQuery` is ignored, `RelationalDbFunctions` is
      refused at the client boundary. The fourth is **a relational option on the client's
      `DbContextOptionsBuilder`**, and it is not a stray: `NullSemanticsQueryTestBase`
      (`useRelationalNulls`, R56), `AdHocMiscellaneousQueryRelationalTestBase` and
      `NonSharedPrimitiveCollectionsQueryRelationalTestBase` (`SetParameterizedCollectionMode`) and
      `AdHocQuerySplittingQueryTestBase` (`SetQuerySplittingBehavior`) are **four bases blocked by
      one member shape**. A fifth, `DbParameter CreateDbParameter`, blocks four of the `Sql`-named
      bases and is ADO.NET rather than EF. **A decision on #60 should say which of the five it
      addresses**, because they are not the same problem and only the first two are about queries.

      **On the direction of a mis-priced estimate.** R50 and R43 were both over-priced by about
      three, and the handoff asked whether the direction repeats. **It does not.** R41's per-base
      counts are **under**-priced, because they were read off the bases' new test methods and never
      run: `SharedTypeQuery` 1 → 4, both `ConcurrencyDetector`s 1 → 2, `NorthwindBulkUpdates` 2 → 4,
      `QueryNoClientEval` 2 → 3, `OwnedQuery` 9 → 10 with a different *composition* and two wrong
      answers inside it. The two failure modes are the same mistake — **counting instead of
      running** — and they point opposite ways depending on whether the count is of the reference
      provider's overrides or of the base's own new tests.

- [x] **R63. J22's `ComplexTypesTracking` pair re-checked — the price still stands, but the
      mechanism it was priced against no longer matches EF's source.** No code. **Left open**,
      because what is needed next is one diagnostic rather than a decision.

      **The symptom is unchanged.** Both parameterizations of
      `Can_track_entity_with_complex_property_bag_collections` on `Added` still fail with
      `System.ArgumentException : Incorrect number of arguments supplied for call to method
      'System.Object get_Item(System.String)'`, raised on the server during `SaveChanges`.

      **What has changed is EF's code.** J22 named the site exactly: *"`CreateMemberAssignment`
      calls `Expression.Property(instance, member)` where `member` is the `Item[string]` indexer …
      supplying no index argument"*. `StructuralTypeMaterializerSource.AddInitializeExpression`'s
      local `CreateMemberAssignment` now ends:

      ```csharp
      return property.IsIndexerProperty()
          ? Assign(MakeIndex(parameter, (PropertyInfo)memberInfo, [Constant(property.Name)]), value)
          : MakeMemberAccess(parameter, memberInfo).Assign(value);
      ```

      **The guard J22 said was missing is there.** Either the reference clone moved since J22 or
      J22 read the wrong one of the method's several assignment sites — and **two of them are still
      unguarded**: both `MakeMemberAccess` calls inside the `IsPrimitiveCollection` branch, which
      `IsIndexerProperty()` does not cover. A third candidate is the complex-*collection* path,
      which this method does not write at all (`IComplexProperty { IsCollection: true } =>
      Default(clrType)`, *"populated separately"*), and the failing model's property is exactly a
      complex collection of bags.

      **So the standing classification is not safe to repeat.** J22's conclusion — upstream, on a
      path only this provider takes, priced at reproducing constructor binding — may still be
      right, and its *price* is unaffected either way, but the sentence naming the defect is now
      false about the source it names. **A classification is not evidence, and neither is its
      age**, which is the rule that produced six corrections in M9's closing session.

      **The next step is one diagnostic, not an investigation.** The server's own stack is what
      settles it. **Done in R65, and the diagnostic turned out to already exist** — see there.

- [x] **R64. `LeftJoin` was missing from `ProjectionShape`, and it cost a silent wrong answer.**
      `failed` **unchanged at 95**, `total` 28945 → 28946 for the one new test. FIXED none, BROKEN
      none. `Passed: 28615, Failed: 95, Total: 28946`. A `src/` change; both gates ran.

      **This is R62's `OwnedQuery` finding run to ground.** That step measured
      `OwnedQueryRelationalTestBase` at 202 green of 212 and said two of the ten were wrong answers
      with a defect behind them. This is the defect.

      **`ProjectionShape.Operator` listed `Select`, `SelectMany` and `Join`, and not `LeftJoin`.**
      A left join's result selector was therefore never entered, and every owned entity type it
      projected came back unresolved. Nothing else can resolve those, and the class's own remarks
      say why: the CLR type cannot, because four of `OwnedQueryTestBase`'s owned types are the same
      `OwnedAddress`; the change tracker cannot, because the server tracks only when asked.
      `Companion` named `Join` alone for the same reason. `RightJoin` is added to both — the three
      have identical argument shapes.

      **Two consequences, and the quiet one is worse.**
      1. The mapper falls back to round-tripping the value by its **public CLR members**, so
         `PlaceType` and `Country` came back and `AddressLine` and `ZipCode` — private fields
         behind an indexer — did not. **A wrong answer, and silent.**
      2. The tracking downgrade `ServerQueryExecutor.TrackingBehaviorFor` makes for an ownerless
         owned type never fires, so the server refuses the query instead.

      **Ten probes, and the discriminator is the operator and nothing else.** Correct: an inner
      `Join`; `SelectMany` + `Where` + `DefaultIfEmpty`, which is the same left join written
      differently; a plain `Select(p => p.PersonAddress)`; and every one of those with
      `AsNoTracking`. Wrong: `GroupJoin` + `DefaultIfEmpty`, and a hand-written
      `Queryable.LeftJoin`. **The same `LeftJoin` run directly on the server context is correct**,
      which is what rules out EF, the shaper and the SQL — and the SQL was replayed against a copy
      of the `.db` file and returns the address.
      **`GroupJoinFlattener` was the first suspect and is exonerated**: it emits
      `Queryable.LeftJoin`, and a caller who writes `LeftJoin` by hand fails identically.
      **One hypothesis was evidenced and wrong**: that the loss was "no entry, so no shadow state".
      `AsNoTracking` on the working shapes does not reproduce it.

      **The suite measures none of this**, so
      `SqliteSmokeTest.A_left_join_keeps_an_owned_value_that_has_no_public_member` is added, with a
      `Located` entity whose owned address holds one value behind an indexer. Measured **red before
      the fix and green after**. It pins consequence (2): a model with one owned type per CLR type
      cannot reproduce (1), and the base that does — `OwnedQueryRelationalTestBase` — is not
      adopted (R62). **That is stated in the test rather than left implied**, because a pin that
      covers half a defect should say which half.
      Gates: trim ratchet OK (89 ≤ 89), Release build `0 Warning(s), 0 Error(s)`, which caught one
      `CS8602` Debug did not — the third time on this branch.

- [x] **R65. J22's pair settled from the server's own stack — J22 was right, R63's doubt was
      right about the source and wrong about the conclusion, and the diagnostic already
      existed.** No code kept; the probe was reverted. Docs only, no gate.

      **Nothing had to be built.** R63 said the server frames "need to reach the test output before
      the site can be named". They already do: `InfoCarrierFaultMapper` has carried the server
      stack since C48 and `Rehydrate` puts it on `exception.Data["InfoCarrier.ServerStackTrace"]`,
      deliberately not spliced into the message — `InfoCarrierFault`'s own remarks say why, and
      say it would break every message assertion. A four-line `catch` in the test class printed it.
      **The lesson is R50's and R43's in a third shape: the thing was priced without being looked
      at.**

      **The stack, and it names the frame J22 named:**

      ```
      Expression.Property(Expression, PropertyInfo)
        StructuralTypeMaterializerSource.<AddInitializeExpression>g__CreateMemberAssignment|10_0
        StructuralTypeMaterializerSource.AddInitializeExpression        <- the complex type's property
        StructuralTypeMaterializerSource.CreateMaterializeExpression    <- the complex type
        StructuralTypeMaterializerSource.AddInitializeExpression        <- the entity's complex property
        StructuralTypeMaterializerSource.CreateMaterializeExpression    <- the entity
        RuntimeEntityType.GetOrCreateMaterializer
        InfoCarrier.Core.ServerSaveChangesExecutor.Materialize
      ```

      **And the branch is the one R63 guessed.** `CreateMemberAssignment` guards its final
      `return` with `property.IsIndexerProperty()` and builds a `MakeIndex`. Its **primitive
      collection** branch, taken earlier, does not: it calls
      `MakeMemberAccess(parameter, property.GetMemberInfo(…))`, and `MakeMemberAccess` on a
      `PropertyInfo` is `Expression.Property` — which is the throwing frame. So R63's "two other
      `MakeMemberAccess` calls in the same method are still unguarded" was the right suspicion and
      this is the confirmation.

      **The model shows exactly why that branch is reached**, and the value is one line of EF's
      own seed: `["Members"] = new List<string> { "Boris", "David", "Theresa" }` — a **primitive
      collection**, non-array, inside a **property-bag** complex type whose properties are indexer
      properties. `IsPrimitiveCollection: true, ClrType.IsArray: false` selects the unguarded
      branch; the indexer then reaches `Expression.Property` with no index argument.

      **So J22's verdict stands, its site name stands, and its price stands.** It is EF's defect,
      on a path only this provider takes — EF's own suites construct the object and track it, and
      never materialize it from a value buffer. The route around it still has to avoid
      `GetOrCreateMaterializer` and reproduce constructor binding, for two tests. **Not taken, and
      now priced against a mechanism that has been read rather than inferred.**

      **What is newly actionable is the upstream report**: the fix is one branch of one method
      applying the guard the same method already applies eight lines later.

- [x] **R66. `NorthwindBulkUpdates` re-parented onto its relational base — and the re-parent
      deletes seven things and adds two.** Missing bases 23 → 22, `total` 28946 → 28950,
      `failed` **95 → 99 on the owner's instruction**. FIXED none, BROKEN four.
      `Passed: 28615, Failed: 99, Total: 28950`.

      **The rise is the price R62 measured and the owner accepted.** The base adds two theories,
      `Delete_FromSql_converted_to_subquery` and `Update_FromSql_set_constant`, both `FromSqlRaw`;
      four parameterizations, all permanent until #60 is decided. **The four broken are exactly
      those four**, and nothing else in the suite moved. They fail on the `(RelationalTestStore)`
      cast inside `NormalizeDelimitersInRawString` before `FromSql` is reached, so the message
      names the cast rather than the SQL — the reason is #60 either way.

      **Seven things go away, and that is R1 paying out a second time.** This class carried **six
      overrides hand-mirrored** from the relational base — the three `Delete_non_entity_projection`,
      `Update_without_property_to_set_throws`, `Update_multiple_tables_throws` and
      `Update_unmapped_property_throws` — plus a hand-mirrored `AssertTranslationFailed` helper.
      Its own remark explained why: *"`EFCore.Relational.Specification.Tests` is not referenced here
      — so they are mirrored by hand, each matched by reason against a measured failure first
      (A63)"*. R1 referenced it. The fixture also loses a duplicated `TestSqlLoggerFactory` property
      and its `ITestSqlLoggerFactory` declaration, both of which the relational fixture supplies.

      **D6 is satisfied and was checked rather than assumed.**
      `NorthwindBulkUpdatesRelationalFixture.UseTransaction` is `public override` calling
      `GetDbTransaction()` — virtual, so it adopts under ADR-013's amendment — and this fixture
      already overrode it with `UseInfoCarrierTransaction`. Nothing new was needed, which is why
      this step has no `database is locked` run behind it.

      **C20's pair did not move, and that is worth recording.** The relational base overrides
      `Update_with_invalid_lambda_in_set_property_throws` itself, and both parameterizations fail
      exactly as before. They were already in the baseline, they are not among the four, and
      adopting the base did not close them — so C20's reading of them stands unchanged.
      `test/` only; the Release build was run anyway and caught one `IDE0005` Debug did not, the
      fourth on this branch.

- [x] **R67. R62's one "real gap" in `QueryNoClientEval` is not a gap — the details clause was
      never missing.** No code kept; the probe was reverted. Docs only, no gate.

      **R62 said `Throws_when_orderby_multiple` gets `TranslationFailed` where the base expects
      `TranslationFailedWithDetails`, so "the details clause is missing on that shape". That was
      read off an assertion failure, and xUnit had truncated both strings.** Printing the thrown
      message instead says something different:

      | Query | Who refuses it, and with what details |
      |---|---|
      | `OrderBy(c => c.IsLondon)` | **The server.** `Translation of member 'IsLondon' on entity type 'Customer' failed`. The test passes. |
      | `OrderBy(c => c.IsLondon).ThenBy(c => ClientMethod(c))` | **The client**, first. `Translation of method '…ClientMethod' failed`. |
      | `OrderBy(c => ClientMethod(c))` | The client, same message. |
      | `Where(c => c.IsLondon)` | The server, member message. The test passes. |

      **The details are there. They name a different reason, and both reasons are true.** The query
      has two untranslatable operators; EF translates bottom-up and reports the inner one, and this
      provider refuses at the boundary and reports the outer one.

      **It is also not fixable in the direction EF goes, and that is the useful part.** Reporting
      `IsLondon` would mean shipping a query the client has already established it cannot ship —
      the member is not client code by this provider's test (`Customer` is allowlisted and
      `IsLondon` is an ordinary member read), so only the server can name it, and the server never
      sees this query. Visiting children before parents in `ClientEvaluationFinder` would not help
      for the same reason: there is nothing in the inner operator for it to find.

      **So this is a message-text difference, the category `limitations.md` already names two of,
      and not a defect.** `QueryNoClientEvalTestBase`'s price is therefore **11 green, 3 red, and
      none of the three is a defect of this provider** — two are #60's `FromSql` and one is this.
      That is a better trade than R62 recorded, and it is the owner's call, not taken here.

- [x] **R68. `QueryNoClientEvalTestBase` ADOPTED on Tier B — 14 tests, and all three reds were
      already classified.** `failed` 99 -> 102, `total` 28950 -> 28964. The class is nineteen lines
      over `NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>`, which already satisfies
      the base's `NorthwindQueryRelationalFixture<NoopModelCustomizer>, new()` constraint. **EF's
      own `QueryNoClientEvalSqliteTest` is the same shape and overrides nothing**, so no red here is
      the store's.

      **Ten pass, one is skipped by EF itself** (`Throws_when_group_by`, EF issue #18923), **three
      are red and none is a defect of this provider** — which is the whole reason the rise is taken
      rather than the base left unadopted:

      | Test | Cause |
      |---|---|
      | `Doesnt_throw_when_from_sql_not_composed` | #60. Dies on `(RelationalTestStore)TestStore` inside `NormalizeDelimitersInRawString` before `FromSql` is reached. |
      | `Throws_when_from_sql_composed` | #60. `FromSqlRaw`. |
      | `Throws_when_orderby_multiple` | **A message-text difference (R67), not a gap.** Both messages carry the details clause; they name different operators, and both reasons are true. |

      The base asserts that an untranslatable operator is *refused* rather than run on the client,
      which is this provider's own rule (`QuerySplitter.RejectClientEvaluation`) stated by someone
      else's tests — so the ten greens are the point of adopting it. Measured `r68-noclienteval`
      against `r67-base`: FIXED none, BROKEN exactly the three, reasons diff one new line per red.

- [x] **R69. `OwnedQueryRelationalTestBase` ADOPTED as a TIER MOVE — 212 tests, 210 green, and the
      six row-limiting reds closed with a knob that already existed.** `failed` 102 -> 104,
      `total` 28964 -> 28982 (212 on Tier B less the 194 Tier A tests the move deletes).

      **The harness question the handoff posed is answered, and the answer is "the existing
      decision stands".** `InfoCarrierBackendTestStore.AddProviderOptions` deliberately does not
      copy the fixture's `ConfigureWarnings(Default(Throw))` to the server, on the grounds that it
      is a statement about what the test author wrote while the server runs a tree this provider
      generated. **That remark is correct and the global change was not re-measured, because C55
      already measured it: 8 fixed, 626 broken.** Most of the 626 are model warnings about a model
      `TestModelSource` built for the backing store.

      **What closed the six is C69's per-fixture mechanism, reused rather than rebuilt.**
      `AssociationsWarnings.ThrowOnUnorderedRowLimiting` forwards exactly
      `RowLimitingOperationWithoutOrderByWarning` — the event
      `RelationalQueryableMethodTranslatingExpressionVisitor` raises on the *server* — and four
      `Associations` fixtures already call it. This fixture is the fifth. The base names the event
      itself, in a comment on each of the three overrides, which is the same justification C69
      recorded. **No override asserting "no throw" was written**: that would have hidden a real
      difference between this provider and every relational one.

      | Outcome | Count |
      |---|---|
      | Green | 210 |
      | Red — `Using_from_sql_on_owner_generates_join_with_table_for_owned_shared_dependents`, two parameterizations | 2 |

      Both reds are #60, dying on `RelationalOwnedQueryFixture`'s
      `public new RelationalTestStore TestStore` cast inside `NormalizeDelimitersInRawString`. That
      cast is on no other route in the base, so ADR-013's 2026-08-30 amendment adopts it.

      **The re-parent deletes more than it adds — R1 paying out for the second time this phase.**
      The Tier A class's five overrides were all InMemory limitations copied from EF's
      `OwnedQueryInMemoryTest`; on a store that composes, all five simply answer. The fixture also
      loses a duplicated `TestSqlLoggerFactory` and its `ITestSqlLoggerFactory` declaration, both
      supplied by `RelationalOwnedQueryFixture`. What is left of the old Tier A file is
      `SharedTypeQueryInfoCarrierTest`, and the file is renamed to match.

      **D6 checked rather than assumed**: neither owned-query base uses
      `ExecuteWithStrategyInTransactionAsync` and the relational fixture declares no
      `UseTransaction`. It is a read-only query base. Measured `r69-ownedquery` against
      `r68-noclienteval`: FIXED none, BROKEN exactly the two, one reason moved (`InvalidCastException`
      9 -> 11).

- [x] **R70. `JsonQueryRelationalTestBase` ADOPTED — the compile blocker is solved, and the probe's
      price was under by eight.** `failed` 104 -> 118, `total` 28982 -> 29028. 425 of 446 pass,
      7 skipped, 14 red — **all fourteen the `FromSql_on_entity_with_json_*` theories, which is
      #60 and exactly what R62 priced.**

      **The handoff's open question is answered yes, with the compiler rather than a guess.**
      `JsonQueryRelationalFixture` declares `public new RelationalTestStore TestStore`, which
      *shadows*; our `The_two_models_agree_on_the_key_of_every_JSON_mapped_owned_collection`
      stopped **compiling**, not failing. The `OwnedQueryFixtureBase` workaround transfers, with a
      different type argument — `JsonQueryFixtureBase : SharedStoreFixtureBase<JsonQueryContext>`,
      not `SharedStoreFixtureBase<PoolableDbContext>`:

      ```csharp
      public InfoCarrierTestStore InfoCarrierTestStore
          => (InfoCarrierTestStore)((SharedStoreFixtureBase<JsonQueryContext>)this).TestStore;
      ```

      **R41's failure mode repeated, and this time on our side of the ledger.** R62 priced 14 by
      counting the base's `FromSql` methods. The base also adds ~16
      `*AsNoTrackingWithIdentityResolution` theories nobody had counted; twelve pass and **four
      fail identically**, SQLite raising `ApplyNotSupported` before the query reaches the check the
      base is testing. **EF's own `JsonQuerySqliteTest` overrides all four** — *"Sqlit throws APPLY
      error, but base expects different exception"* — so they are convergence with the reference
      provider and EF's overrides are adopted.

      **A63's shape was reproduced, by measuring rather than reasoning about it.** The eighteen
      APPLY overrides already in this file wrap `base` in `AssertApplyNotSupported`, which asserts
      the refusal rather than swallowing it, and that was tried first. It cannot work here: **these
      four base methods catch the `InvalidOperationException` themselves** and compare its message,
      so what escapes `base` is an `Xunit.Sdk.EqualException` and the wrapper fails with *"Exception
      type was not an exact match"* — the exact words this file's own remarks warn about. EF's
      `=> Task.CompletedTask` is taken instead, with the reason recorded on it.

      **R1 pays out a third time.** Deleted: ~40 lines of hand-copied `ToJson()` mapping, three
      hand-mirrored `Project_json_*_tracking_query_fails` overrides, the `AssertOwnedWithoutOwner`
      helper, a duplicated `TestSqlLoggerFactory` and an `ITestSqlLoggerFactory` declaration. Kept:
      `JsonQuerySqliteFixture`'s ignores, which are the *store's* statement rather than the base's.
      Measured `r70-jsonquery` against `r69-ownedquery`: FIXED none, BROKEN exactly the fourteen,
      one reason moved (`InvalidCastException` 11 -> 25).

- [x] **R71. The remaining 19 measured rather than estimated - and `FromSqlRaw` is SILENTLY
      IGNORED, which no estimate had seen.** No code; every probe adopted bare on Tier B, run with
      `--filter`, and reverted. `failed` and `total` unmoved at 118 / 29028. **This step adopts
      nothing: every base below raises `failed`, and that is the owner's call, not this step's.**

      **The finding that outranks the table.** `FromSqlRaw` on a client `DbSet` does not throw,
      does not translate, and does not refuse - **the `FromSqlQueryRootExpression` is discarded and
      the query runs as if `FromSqlRaw` had never been called.** Read out of the server's own log
      (`INFOCARRIER_SERVER_SQL=1`) for
      `FromSqlQueryTestBase.FromSqlRaw_queryable_with_parameters`, whose raw SQL is
      `SELECT * FROM "Customers" WHERE "City" = {0}`:

      ```text
      SELECT "c"."CustomerID", "c"."Address", ... FROM "Customers" AS "c"
      WHERE "c"."ContactTitle" = @Value
      ```

      The raw text and its `London` parameter are **gone**; only the composed LINQ `Where`
      survives. The test expects 3 rows and gets **91** - the whole table. **82 of the 148 tests in
      that base fail as a wrong answer of exactly this shape**, and 40 more "pass" only because
      their raw SQL happened to be equivalent to an unfiltered `SELECT *`.

      **This is `AsSplitQuery` before R59, with the stakes of a `WHERE` clause.** A user who writes
      `FromSqlRaw` with a filter gets every row of the table and no diagnostic. It is
      consumer-visible, it is undocumented, and **it is not reached by the suite today** - no
      adopted base calls `FromSqlRaw` - which is why 22472 green never saw it. It belongs in
      [`limitations.md`](../../../website/docs/limitations.md), and it is the sharpest single input
      to #60.

      **The `RelationalTestStore` cast was hiding the question, and lifting it is what answered
      it.** 277 of the reds below were `InvalidCastException: InfoCarrierTestStore ->
      RelationalTestStore`, raised by the spec bases' own `NormalizeDelimitersInRawString` helper -
      **test infrastructure, ADR-013, not #60**, and it stops the test before the provider is ever
      asked anything. A probe store deriving from `RelationalTestStore` (the base needs a
      `DbConnection` only for `ConnectionString`; the normalizer is pure string work) lifted it and
      turned the wall into the two clean numbers below. **Counting those 277 as #60 evidence, which
      is what R62's table did, was wrong.**

      **The table. Every number is read out of the run's own summary; the delta columns subtract
      what the core base already runs.**

      | Base | Class run P/F/S/T | New tests | New green | New red | Dominant cause |
      |---|---|---|---|---|---|
      | `ConcurrencyDetectorEnabledRelationalTestBase` | 18/0/0/18 | +2 | **+2** | 0 | none - the `FromSql` theory passes |
      | `ConcurrencyDetectorDisabledRelationalTestBase` | 18/0/0/18 | +2 | **+2** | 0 | none |
      | `Query.ToSqlQueryTestBase` | 2/0/0/2 | +2 | **+2** | 0 | none |
      | `Query.AdHocMiscellaneousQueryRelationalTestBase` | 65/6/1/72 | +13 | **+7** | 6 | cache-entry and dbcontext-leak asserts |
      | `Query.NonSharedPrimitiveCollectionsQueryRelationalTestBase` | 44/7/1/52 | +26 | **+20** | 6 (+1 override) | `ParameterTranslationMode` not honoured |
      | `Query.UdfDbFunctionTestBase` | 25/80/1/106 | +106 | **+25** | 80 | ~~SQLite has no `CREATE FUNCTION`~~ **wrong; R73 measured only 2 of 80 as the store** |
      | `Query.SharedTypeQueryRelationalTestBase` | 2/4/0/6 | +4 | 0 | 4 | `SqlQueryRaw` plus the cast |
      | `Query.GearsOfWarFromSqlQueryTestBase` | 0/1/0/1 | +1 | 0 | 1 | the cast (**one** test, not two) |
      | `Query.NorthwindSqlQueryTestBase` | 0/8/0/8 | +8 | 0 | 8 | `Database.SqlQuery*` relational-only |
      | `Query.SqlQueryTestBase` | 0/119/0/119 | +119 | 0 | 119 | `Database.SqlQueryRaw` relational-only |
      | `Query.FromSqlQueryTestBase` | 0/148/0/148 | +148 | 0 | 148 | the cast; **40 green once lifted** |
      | `Query.SqlExecutorTestBase` | 0/28/0/28 | +28 | 0 | 28 | `Database.ExecuteSql*` relational-only |
      | `Query.FromSqlSprocQueryTestBase` | 0/48/0/48 | +48 | 0 | 48 | sproc result types absent from Northwind |
      | `Update.JsonUpdateTestBase` | 0/142/0/142 | +142 | 0 | 142 | `UseTransaction` reaching `GetDbTransaction()` |
      | `Update.StoreValueGenerationTestBase` | 0/38/0/38 | +38 | 0 | 38 | `ISqlGenerationHelper` unresolvable |
      | `Update.StoredProcedureUpdateTestBase` | 0/56/0/56 | +56 | 0 | 56 | *"SQLite does not support stored procedures"* |
      | `Query.AdHocQuerySplittingQueryTestBase` (R62) | 7 green / 6 red / 13 | +13 | **+7** | 6 | unchanged |
      | `Query.NorthwindDbFunctionsQueryRelationalTestBase` (R55) | 0 green / 20 red | +20 | 0 | 20 | unchanged |
      | `Query.MappingQueryTestBase` | see R72 | | | | the store name, not any API |

      **How much green #60 is withholding: none. The withheld green is 65 tests, and R62's
      estimates are what withheld it.** Adopting the six rows above with a bold figure adds
      **65 new green** and 18 new red, and not one of those greens needs a line of #60 work. What
      #60 and its neighbours withhold is **red**: 623 tests that cannot go green whatever is
      written here.

      **The two blockers are different problems and the table now separates them.**
      `Database.SqlQueryRaw` and `Database.ExecuteSql*` raise *"Relational-specific methods can only
      be used when the context is using a relational database provider"* - **249 tests, a hard
      guard no test-side work reaches**, and the cleanest #60 evidence in the phase. Against that,
      `IQueryable.FromSqlRaw` raises nothing at all, which is the defect above.

      **R62's estimates were wrong in seven rows, and this time the direction is mixed.**

      | Base | R62 said | Measured |
      |---|---|---|
      | both `ConcurrencyDetector*Relational` | *"2 tests, both red, zero new green"* | 2 tests, **both green** |
      | `Query.ToSqlQueryTestBase` | *"blocked twice over"* - client `ToSqlQuery` **and** a non-virtual `UseTransaction` | **2 tests, both green.** Neither blocker fires |
      | `Query.AdHocMiscellaneousQueryRelationalTestBase` | *"#60, abstract `SetParameterizedCollectionMode`"* | a **no-op** implementation is enough; 7 new green |
      | `Query.NonSharedPrimitiveCollectionsQueryRelationalTestBase` | same member, same verdict | same; **20 new green**, and the re-parent deletes the hand-mirrored `Array_of_byte` |
      | `Query.UdfDbFunctionTestBase` | *"`HasDbFunction` ... refused before the wire"* | the model builds; **25 green**. ~~The 80 reds are SQLite's missing functions~~ — **R62 was RIGHT and this cell was wrong.** R73 read the reasons instead of the base's name: 56 of 80 are exactly that refusal, 11 are wrong answers, 2 are the store |
      | `Query.GearsOfWarFromSqlQueryTestBase` | *"2 tests"* | **1** - a `ConditionalFact`, not a theory |
      | `Update.JsonUpdateTestBase` | *"136 tests"* | **142**, one cause, and the cause is the `UseTransaction` R62 named |

      **The `CreateDbParameter` hypothesis the handoff asked to test is CONFIRMED, and that member
      was never the blocker.** `protected abstract DbParameter CreateDbParameter(string, object)`
      compiles and runs as `new SqliteParameter { ParameterName = name, Value = value }` - the test
      project already has Microsoft.Data.Sqlite through the Tier B backend. R62 called it a hard
      blocker on four bases on the ground that *"this client has no ADO.NET provider, so there is
      nothing to return"*; the client does not need one, because the **test** project has one. Not
      one of the four bases is blocked by that member.

      **One adoption cost worth recording.** `StoredProcedureUpdateTestBase` declares
      `public abstract Task X(bool)` beside `protected Task X(bool, string)`, and overriding the
      first trips **xUnit1024** in this repository, which EF's own suite does not enforce. Adopting
      it would need a file-scoped `#pragma warning disable xUnit1024`.

- [x] **R72. `MappingQueryTestBase` ADOPTED — the store-name blocker cost a seed, and the one red
      it uncovered was a defect in the type allowlist.** `failed` 118 -> 117, `total`
      29028 -> 29032. **Four tests, four green.**

      **R62 was right not to probe it, and right about why.** The base's fixture supplies a model
      and no seed, because EF's providers hand it a prebuilt `northwind.db` through
      `SqliteNorthwindTestStoreFactory`. Its inherited `StoreName` is `"Northwind"` — on this tier
      the same file the Northwind query fixtures share — and this tier builds each store from
      whichever model reaches it first. Probing it as-is would have initialized the shared
      `Northwind.db` from a three-table model and broken every Northwind class in the suite.

      **The price, and it is the whole price.** `StoreName` is `protected override`, so the store
      is renamed to `"MappingQuery"` and seeded here from `NorthwindData`'s own `Create*` arrays —
      the real 91 customers, 9 employees and 830 orders the base asserts. Only the three properties
      this model keeps are written, because the base `Ignore`s everything else and there is no
      column to write to. **Renaming alone is necessary and not sufficient**, exactly as the
      handoff said: it yields the right table shape and no rows.

      **EF's four `MappingQuerySqliteTest` overrides are NOT adopted.** Each asserts a SQL string
      against `Fixture.TestSqlLoggerFactory.Sql`, which observes the *client's* log — a client with
      no database, which emits no SQL (R54). #56's "SQL plumbing only" group. The core base's four
      tests assert results, and those are the adoptable part.

      **The probe's one red was ours, and it was a general defect rather than this base's.**
      `Project_nullable_enum` was refused by `TypeAllowlist` with *"Type
      'MappingQueryTestBase`1+ShipVia[...]' is not on the deserialization allowlist"* — which
      contradicts the rule that file states and `security-review.md` §2 repeats, that **every enum
      is admitted**. The cause: **a type nested in a generic type is itself a constructed generic
      type**, even when it declares no type parameter of its own. `Evaluate` therefore reached its
      `IsConstructedGenericType` branch first, asked whether the open definition
      `MappingQueryTestBase<>+ShipVia` was listed — which no enum ever is — and denied it. The
      closing `return type.IsEnum` was unreachable for the whole family.

      **This is the exact shape the same method already warns about one branch higher**, where the
      exact-match check carries a comment that "an entity type can perfectly well *be* a
      constructed generic — the EF specification suites nest their models inside generic test
      bases". Entity types were rescued by being in the set verbatim; enums are admitted **by rule**
      rather than by the set, and nothing rescued them.

      **The fix is to ask `IsEnum` before the decomposition, and it widens nothing.** An enclosing
      type's generic arguments say nothing about an enum's value, so there is nothing to decompose;
      and `security-review.md` §2's conjunction is measured over `Binder`, `MethodBase`,
      `MethodInfo`, `ConstructorInfo`, `PropertyInfo`, `Activator`, `Assembly` and `AppDomain`, of
      which an enum is none. Every non-nested enum was already admitted by the closing line, so no
      new *kind* of thing crosses. §2 is amended to say so; `DeserializationHardeningTest` (27) and
      `TypeAllowlistBoundaryTest` (2) both stay green.

      `src/` changed, so **both** gates: `CI=true dotnet build --configuration Release` reports
      `0 Error(s), 5 Warning(s)` (the documented five), and `eng/trim-ratchet.sh` holds.
      Measured `r72-mappingquery` against `r70-jsonquery`: **FIXED 1, BROKEN none**, one reason moved (41 -> 40 "No exception was thrown"). The one fixed test is `CustomConvertersInfoCarrierTest.Collection_enum_as_string_Contains`, which asserts the refusal this provider now raises; its sibling `Value_conversion_on_enum_collection_contains` had been **passing for the same wrong reason** and takes EF's own SQLite override, recorded in `test/known-failures.txt` and in the class itself.

- [x] **R73. Five of R71's six adoptable bases TAKEN, on the owner's instruction — +37 green for
      +8 red, and every red was named before the run.** `failed` 117 -> 125, `total`
      29032 -> 29077. The compliance gate's missing list falls **18 -> 13**.

      | Base | Was | Now | New | Green | Red |
      |---|---|---|---|---|---|
      | `ConcurrencyDetectorEnabledRelationalTestBase` | Tier A, core base, 16 | 18/0/0/18 | +2 | +2 | 0 |
      | `ConcurrencyDetectorDisabledRelationalTestBase` | Tier A, core base, 16 | 18/0/0/18 | +2 | +2 | 0 |
      | `Query.ToSqlQueryTestBase` | not adopted | 2/0/0/2 | +2 | +2 | 0 |
      | `Query.AdHocMiscellaneousQueryRelationalTestBase` | Tier A, core base, 59 | 69/2/1/72 | +13 | +11 | 2 |
      | `Query.NonSharedPrimitiveCollectionsQueryRelationalTestBase` | Tier B, core base, 26 | 44/6/2/52 | +26 | +20 | 6 |

      **Three of the five are tier MOVES, not additions** — a base belongs to exactly one tier, so
      each class was re-parented onto the relational base rather than run alongside the core one.
      `NonSharedPrimitiveCollections` was already Tier B and is a pure re-parent.

      **R1 pays out again.** The re-parent deletes `NonSharedPrimitiveCollections`'
      hand-mirrored `Array_of_byte` override, which existed only because this project did not
      reference the relational base that declares it. EF's own skip of `Array_of_TimeOnly` stays:
      that one is the store's statement, not the base's.

      **The trap on the `AdHocMiscellaneous` move, and it would have read as a regression.** Four
      of the six reds R71 measured are on the **core** base and pass today at Tier A —
      `Explicitly_compiled_query_does_not_add_cache_entry`, `Inlined_dbcontext_is_not_leaking`,
      `Variable_from_closure_is_parametrized` and
      `Relational_command_cache_creates_new_entry_when_parameter_nullability_changes`. The Tier A
      class carried EF's own `AdHocMiscellaneousQueryInMemoryTest` overrides for all four, and
      **those overrides had to move with it**: they assert the size of EF's relational command
      cache, and it is the *client's* cache they read. This client is not a relational provider on
      **either** tier, so the reason is untouched by the backing store; EF's SQLite class omits
      them only because a real relational client has that cache. Carrying them turns R71's
      "+7 green / 6 red" into the true **+11 green / 2 red**.

      **The eight reds, both families pre-classified.** Two are R71's `FromSqlRaw` defect
      (`Multiple_different_entity_type_from_different_namespaces`, whose `FromSqlRaw` is discarded
      so the exception it exists to provoke never arrives) — left red on purpose as the cheapest
      standing witness to that defect in the suite. Six are **#60's fourth shape**, a relational
      option on the *client's* `DbContextOptionsBuilder`: `SetParameterizedCollectionMode` is a
      no-op here, so `ParameterTranslationMode` is never applied. The query is right; the knob to
      request it is missing.

      **R62's blocker on both `SetParameterizedCollectionMode` bases does not exist.** A no-op
      implementation is enough, because only tests that ask for a *non-default* mode consult it —
      none in `AdHocMiscellaneous`, six in `NonSharedPrimitiveCollections`. R51 read the member and
      called both bases blocked; R71 ran them.

      **`ToSqlQuery` was priced as blocked twice over and is blocked by neither.** `ToSqlQuery` is
      model *metadata* — the client records it, the server builds the same model and turns it into
      SQL — so nothing relational need exist on the client. And no test in the base routes through
      its non-virtual `UseTransaction`, which is exactly the distinction ADR-013's 2026-08-30
      amendment draws.

      **EF's SQLite overrides not taken, in every case for the same reason**: `ToSqlQuerySqliteTest`'s
      `AssertSql` and `Check_all_tests_overridden`, and `AdHocMiscellaneousQuerySqliteTest`'s
      `Average_with_cast` and `Check_inlined_constants_redacting`. The first two pin generated SQL,
      which a client emitting none cannot observe (R54); the last two **passed unmodified when
      measured**, and an override adopted ahead of a measurement is a workaround for a limitation
      this arrangement may not have.

      `test/` only, so `eng/measure.sh` alone. Measured `r73-five-bases` against
      `r72-mappingquery`: FIXED none, BROKEN exactly the eight named above.

      **`Query.UdfDbFunctionTestBase` is the sixth and is NOT here.** R71 classified its 80 reds as
      SQLite's missing functions; **that was wrong, and reading the reasons rather than the base's
      name is what showed it.** Only **2 of 80** are the store. 56 are this provider refusing or
      trying to *evaluate* a `HasDbFunction` call before the wire, and **11 run and get it wrong**
      — 6 wrong answers and 5 empty results where rows are expected. That is a second
      silent-wrong-answer family after R71's `FromSqlRaw`, and the owner's instruction is to
      diagnose those 11 before the base is adopted (R74).

- [x] **R74. `UdfDbFunctionTestBase` ADOPTED — and diagnosing it first is what stopped a whole
      wrong classification from being committed.** `failed` 125 -> 200, `total`
      29077 -> 29183. 106 tests: **30 green, 75 red, 1 skipped by EF itself.** The compliance
      gate's missing list falls **13 -> 12**.

      **The owner's instruction was to adopt but to diagnose R73's "11 wrong answers" first. There
      are none.** The 11 were an artefact of the R71 probe's own fixture, and the root cause is a
      deliberate hole in EF's base: **`UdfFixtureBase.SeedAsync` only STAGES its entities.** Its
      last four statements are `AddRange` calls and it never persists them — verified with the
      compiler over the whole body, lines 371-528. Every provider fixture is expected to override
      it, create its own SQL functions and call `SaveChanges`, which is exactly what EF's
      `UdfDbFunctionSqlServerTests.SqlServer` fixture does. The probe did neither, so the store was
      **empty**, and eleven tests reported "wrong answer" and "sequence contains no elements"
      while reading zero rows.

      **The evidence that settled it, in order.** A `Probe_seed_state` test writing to a file (xUnit
      swallows stdout) read `Customers = 0, Products = 0, Orders = 0`. The store file on disk had
      every table created and every table empty, so `EnsureCreated` had run and the seed had not
      taken. Instrumenting `SeedAsync` showed it **was** called and **did** return without
      throwing — which is what pointed at the base's body rather than at the wiring.

      **With the seed corrected: 30 green, 75 red, and NOT ONE wrong answer.**

      | Count | Cause |
      |---|---|
      | 36 | the client refuses the `HasDbFunction` call before the wire |
      | 22 | the funcletizer tries to **evaluate** the UDF call locally |
      | 7 | a different exception or message than the base asserts |
      | 4 | `NotImplementedException` — EF's UDF stub bodies, reached because the call was evaluated client-side |
      | 3 | EF's own translator marker exception, likewise client-side |
      | **2** | **the store** — SQLite has no such user-defined function |
      | 1 | the client-side part of the query |

      **One mechanism, and it is the safe failure mode.** This provider does not support
      `HasDbFunction`: it refuses at the client boundary or tries to evaluate the call, and either
      way the caller gets an exception rather than a plausible result. That is **#60's third
      shape** (R55, R56). **Unlike `FromSqlRaw` (R71) it needs no consumer warning about silent
      data loss**, and the contrast between the two is the most useful thing this base contributes.

      **Two corrections land here.** R71 recorded the 80 reds as "SQLite's missing functions" —
      only 2 are, and R73 corrected that by reading the reasons rather than the base's name. Then
      R73 recorded 11 of them as a second silent-wrong-answer family — they do not exist, and this
      step corrects that by fixing the fixture rather than trusting the summary. **Both errors were
      the same mistake: classifying from a label instead of from evidence**, which is the failure
      mode `CLAUDE.md` names twice over.

      **On the bar for leaving a base unadopted.** EF ships no SQLite and no InMemory class for
      this base, which is `CLAUDE.md`'s stated bar. The bar's *reason*, though, is that such a base
      reports on the backing store instead of on the provider — and **73 of 75 reds here report on
      this provider**. Adopted on the owner's decision for that reason, with the letter of the rule
      noted as not fitting its purpose in this one case.

      **The functions are not created, and that is priced rather than overlooked.** SQLite has no
      `CREATE FUNCTION`; `Microsoft.Data.Sqlite` registers one per *connection* through
      `SqliteConnection.CreateFunction`, which is not a schema object and would need a connection
      interceptor on the server. It would buy the two store-side reds and none of the other 73.

      `test/` only, so `eng/measure.sh` alone. Measured `r74-udf` against `r73-five-bases`: **FIXED none, BROKEN exactly the 75**, all inside the new class, and the reasons diff shows the seven causes above and nothing else.

- [x] **R75. `FromSql` is REFUSED instead of silently discarded, and the mechanism was a subclass
      falling into a base-class branch.** `failed` 200 -> 198, `total` unchanged at 29183. The silent
      wrong answer R71 found is closed.

      **The defect, exactly.** `FromSqlQueryRootExpression` derives from
      `EntityQueryRootExpression` and adds two members, `string Sql` and `Expression Argument`.
      `ServerBoundaryAnalyzer.IsSerializableKind` matched the **base** (`QueryRootExpression =>
      true`), and `QueryRootStubNode` has fields for an element type and nothing else, so the SQL
      and its parameters were read by nobody. The server rebuilt a plain `DbSet<T>`, and a
      `FromSqlRaw` carrying a `WHERE` returned the whole table.

      **The shape that generalises, and it is worth naming.** An *unknown* extension node already
      threw — `ExpressionToNodeTranslator.VisitExtension` ends in
      `throw new NotSupportedException($"Unsupported extension expression: {node.GetType()}.")`.
      What degraded silently was a **known base class with an unrepresented subclass**. A type test
      written as `is SomeBase` accepts subclasses carrying state the branch cannot see, and there
      is no compiler help for it. The fix matches the **exact** type instead, so any future EF
      subclass of a query root is refused rather than quietly flattened.

      **Refused through the existing path, not a new throw site.** With the node no longer
      serializable the subtree is not shippable and `QuerySplitter.RejectClientEvaluation` fires,
      which is where every other refusal in this provider is raised. The message names the
      construct: *"No part of the query can be executed on the server:
      '[…FromSqlQueryRootExpression]'"*. Named by shape rather than by type because the class lives
      in `EFCore.Relational`, which `InfoCarrier.Core` does not reference (M9).

      **Three overrides, and one of them could not assert.** `ConcurrencyDetectorDisabled.FromSql`
      and `AdHocMiscellaneous.Multiple_different_entity_type_from_different_namespaces` now assert
      the refusal through `FromSqlAssertions`, which is the tripwire: **if `FromSql` is ever
      supported, both fail** and the decision has to be taken again. The first of those *used to
      pass by accident* — the base asserts nothing, so a discarded query root and a table scan
      looked like success. The second used to fail with a `NullReferenceException` out of this
      provider's materializer, three layers from its cause.

      **`ConcurrencyDetectorEnabled.FromSql` takes EF's `Task.CompletedTask` form, and this is
      A63's shape for the third time** (R70 recorded it for `JsonQuery`'s four APPLY tests).
      `ConcurrencyDetectorTest` catches the `InvalidOperationException` *itself* and compares its
      message, so what escapes `base` is an `Xunit.Sdk.EqualException` and any
      `Assert.Throws<InvalidOperationException>` around it fails on "Exception type was not an
      exact match". The refusal is not left unasserted: the disabled sibling pins it on the same
      query, where the base adds no assertion to collide with. **The refusal also arrives *before*
      the concurrency detector**, because it happens while the query is still being compiled.

      **Two user-facing corrections, both found by reading the page against the suite.**
      `website/docs/limitations.md` already carried a row saying relational-only APIs "are not part
      of this provider's surface"; the row was true and the code disagreed with it, and it now says
      what calling one does. And the page's "queries this provider answers that other providers do
      not" listed **five** scenarios, one of which was `Contains` over a collection of enums stored
      as a string. **R72's allowlist fix made this provider refuse that query**, so the claim had
      become false; the entry is removed and the count is four. 737 words against a 750 budget, no
      budget change needed.

      `src/` changed, so **both** gates: `CI=true dotnet build --configuration Release` reports
      `0 Error(s), 5 Warning(s)` (the documented five) and `eng/trim-ratchet.sh` holds at
      `ours 89 <= 89`. Measured `r75-repro` against `r74-udf`: **FIXED 2, BROKEN none.** The first run of this change reported 216, with 18 extra reds in `TPTFiltersInheritanceBulkUpdates` (`no such table`); a second run with identical code reported 198 and none of them. **That is a flake, it is not this change, and it is now the top priority** -- `test/known-failures.txt` carries the evidence and the standing hypothesis.

      **What is NOT done, deliberately.** Supporting `FromSql` is a security decision rather than
      an engineering one, and it is the owner's. The wire would carry client-authored SQL for the
      server to execute, which escapes the containment every other node has: a LINQ tree is
      translated *through the server's model*, and raw SQL is not, so it reaches tables the model
      never maps. That is a larger grant than `IgnoreQueryFilters`, which only bypasses a filter
      *inside* the model, and this repository ships a browser client. If it is ever wanted it
      belongs behind a server-side opt-in that is off by default, documented the way
      `IgnoreQueryFilters` is.

- [x] **R76. The shared test store loses its database and nothing notices — this repository's only
      known intermittent, CLOSED.** `failed` unchanged at 198, `total` unchanged at 29183.
      **Not #56 work**, and committed straight to `main` as `299a1dd` because it is independent of
      this branch: the flake was found while measuring R75 and `CLAUDE.md` makes it outrank
      whatever else is in hand.

      **R75's standing hypothesis was wrong, and the correction is the useful part.** The shared
      `StoreName` is real — `TPTFilters…`, `TPHFilters…` and `TPCFilters…` override `EnableFilters`
      only, so each inherits its parent's — but **that is EF's own design and EF's own suite shares
      the store the same way.** EF is safe because its `SqliteTestStore` uses the *global*
      `TestStoreIndex`; each backend store here builds its own service provider and therefore its
      own, which is why `Created` exists. Renaming the three pairs would have papered over them and
      left `Northwind` (14 skips a run) and `BasicTypesTest` (15) exactly as exposed.

      **The mechanism.** `Created` records that initialization was *started*, and every later store
      for the same file trusted it forever without looking — 71 such skips in a full run, across 19
      store names. The first class creates and seeds the file, runs, and is disposed; the second is
      constructed **two minutes later** and returns from the guard blind. In between the file stops
      being protected: EF's `SqliteDatabaseCreator.Delete` calls `SqliteConnection.ClearAllPools()`
      **process-globally** on every store's initialization, ~646 times in a full run, and ~15s
      after the first class ends one of those drops the last handle.

      **How it was closed, since five instrumented full runs never caught it.** Two facts pinned it
      without a sighting. The six tests that *passed* in the failing run are exactly the ones
      refused before they reach the store, so the database was empty for the whole class. And a WAL
      database recovers completely on reopen **as long as its `.db` survives** — so an empty one
      means the file did not. That turned the hunt into an experiment: delete exactly that file in
      the window and compare signatures. **Matched pair, same trigger.** Pre-fix, deleted 15s after
      disposal: 18 failures, 8 `Countries`, 4 `Animals`, 4 `Assert.Contains`, 2 `Assert.Throws` —
      R75's tally to the reason. Post-fix, deleted 11s after disposal: the rebuild branch fires and
      the run is clean.

      **Two changes.** `SweepStaleFiles` now matches `*.db*`, because EF's `Create` sets
      `journal_mode = wal` and a database here is three files — **14,971 `-wal` and 14,946 `-shm`
      orphans against 76 `.db`**, collected by nothing. That leak self-heals at initialization, so
      it is **not** the flake's mechanism and was not allowed to be reported as one. The fix that
      closes the flake is that the guard is **verified rather than trusted**: a store that did not
      create the database checks it is still there and rebuilds it when it is not.

      **The deleter in the original run was never identified, and the fix does not depend on it.**
      Both in-process deleters are logged and neither fired in five runs, so it came from outside
      the process — and `SweepStaleFiles` is exactly that shape, deleting every `*.db` it finds and
      swallowing the `IOException` on the ones a live handle protects. CI, which runs one thing at a
      time, has never seen it.

      Three-run bar: **198 / 198 / 198**, FIXED none, BROKEN none, reasons unchanged. Full account
      in [`findings.md`](findings.md); `CLAUDE.md`'s "no known intermittent" paragraph is updated
      rather than restored.

- [x] **R78. `EF.Functions` and the `EF.Constant`/`Parameter`/`MultipleParameters` markers cross the
      wire — `TypeAllowlist` was missing the classes that declare them.** `failed` **198 -> 192**,
      `total` unchanged at 29183. FIXED 6, BROKEN none. `src/` change, so **both** gates:
      `eng/trim-ratchet.sh` holds at `ours 89 <= 89` and the Release build reports the documented
      `5 Warning(s), 0 Error(s)`.

      **The defect.** The allowlist admitted `EF`, `DbFunctions` and the *core*
      `DbFunctionsExtensions` — but the markers a caller actually writes are declared on
      `RelationalDbFunctionsExtensions` (`Collate`, `Least`, `Greatest`) and `EFExtensions`
      (`EF.Constant`, `EF.Parameter`, `EF.MultipleParameters`). Neither was admitted, so every one
      was refused at the client boundary by `QuerySplitter.RejectClientEvaluation` — **while the
      server, an ordinary relational provider, could translate all six.** That is the shape M9 J20
      reversed for `Regex`: a refusal that made this provider disagree with every reference
      implementation.

      **Why they could not simply be added.** Both live in `EFCore.Relational`, which
      `InfoCarrier.Core` does not reference (M9), so neither can be written as `typeof`. They are
      matched by **full name and assembly name** instead — the by-name route
      `ServerBoundaryAnalyzer` already takes for `FromSqlQueryRootExpression`, and the assembly
      check makes it tighter than a name alone.

      **`security-review.md` §4b** records why §2's conjunction survives: neither class is on the
      reflection *invocation* surface (`Binder`, `MethodBase`, `MethodInfo`, `ConstructorInfo`,
      `PropertyInfo`, `Activator`, `Assembly`, `AppDomain`), and the generic markers are bounded
      **by §2's own mechanism rather than by luck** — `ResolveMethod` resolves every parameter type
      through this same allowlist, so a `T` bound to an unadmitted type fails the signature lookup
      before the method is found. Naming a host permits the type to be *named*; a method still has
      to resolve by signature.

      **How it was found, because the route is the lesson.** Nothing named `EF.Functions` existed in
      this suite, so a whole family of refusals was invisible. Adopting
      `NorthwindDbFunctionsQueryRelationalTestBase` gave the first such coverage and its reds were
      all one mechanism; the six fixed here were already failing for that same mechanism in
      `NonSharedPrimitiveCollectionsQuerySqliteInfoCarrierTest` and nobody had connected them.
      **A gap with no test naming it is a gap nobody is looking at.**

      That base is not part of this step; it is adopted in R79. **The claim made here when this
      entry was written — that it needs a relational client test store to be adoptable — is wrong,
      and R79 records the measurement that disproves it.** This step depended on none of it either
      way.

- [x] **R79. `NorthwindDbFunctionsQueryRelationalTestBase` ADOPTED — the first `EF.Functions`
      coverage this repository has ever had, and it needs no relational client store.**
      `failed` 192 -> 198, `total` 29183 -> 29213. **A deliberate rise**: 30 tests, **24 green**,
      6 red, FIXED none, BROKEN exactly the 6 and every one inside the new class. Ratio **24:6**,
      against the 30:75 accepted for `UdfDbFunctionTestBase` in R74.

      **R77 was not needed, and believing it was cost a whole mechanism.** The base constrains its
      fixture to `NorthwindQueryRelationalFixture`, which declares
      `public new RelationalTestStore TestStore => (RelationalTestStore)base.TestStore;`. The
      inference — constraint therefore forces the cast, therefore the client must be relational —
      is simply wrong. **A property is evaluated when something reads it, and no test in this base
      reads it.** Measured both ways and byte-identical: 30 tests, 24 green, 6 red, with the
      relational shell and without it.

      **The rule that generalises: a type constraint names what a fixture must BE, not what a test
      will TOUCH.** R77 was built, measured, committed, parked and reverted on the strength of the
      opposite assumption, and one filtered run against the plain client store would have settled
      it at any point. The cost was not the mechanism, which broke nothing — it was that the
      question was never asked.

      **What R77 did leave behind is real and is already banked in R78.** Adopting this base put
      the words `EF.Functions` into the suite for the first time, its reds turned out to be one
      allowlist gap, and six *existing* reds elsewhere were failing for that same gap with nobody
      connecting them. **A gap with no test naming it is a gap nobody is looking at** — that is the
      finding, and it belongs to the base, not to the mechanism.

      **The 6 reds, four of which are one shape.** `Least_with_parameter_array_is_not_supported` and
      `Greatest_with_parameter_array_is_not_supported` (sync + async) assert a translation failure
      and get a differently worded one, because the refusal happens on the *server* and arrives
      wrapped — A63's shape, now recorded for the third and fourth time after R70 and R75.
      `Collate_case_sensitive_constant` (sync + async) is **genuine and not yet triaged**: the other
      three `Collate_*` tests pass, so it is one expression shape rather than the feature.

      No golden strings: EF's own `NorthwindDbFunctionsQuerySqliteTest` overrides most of these to
      assert SQL and adds `Glob`; that is the provider's dialect, and this client emits none.
      `test/` only, so `eng/measure.sh` and not the trim ratchet.

- [x] **R80. The client had no relational `IEvaluatableExpressionFilter`, so `EF.Functions.Collate`
      over a constant was executed instead of translated — and R79's six reds were ONE mechanism,
      not the two families they were triaged as.**
      `failed` 198 -> 192, `total` 29213 -> 29214, FIXED 6, BROKEN none. The +1 total is the new pin
      test. **`NorthwindDbFunctionsQueryInfoCarrierTest` is now 30 of 30.**

      **The defect.** EF's parameter extraction evaluates every maximal subtree that does not touch
      the query root, and `RelationalDbFunctionsExtensions.Collate` — like every `EF.Functions`
      marker — has a body that exists only to throw. Relational providers are protected by
      `RelationalEvaluatableExpressionFilter`, which refuses to evaluate anything that class
      declares. **This client is not a relational provider**: M9 removed the reference to
      `Microsoft.EntityFrameworkCore.Relational`, so EF registers the plain core
      `EvaluatableExpressionFilter`, which knows the *core* `DbFunctionsExtensions` and nothing
      about the relational host. `InfoCarrierEvaluatableExpressionFilter` ports the one clause that
      applies, naming the type by string as M9 J5 decided and pinning it in `DocumentMappingPinTest`
      beside the annotation names. EF's other clause — `model.FindDbFunction` — is deliberately not
      ported: it is a relational model extension for `HasDbFunction`, which this provider does not
      support at all, so the clause could never fire.

      **This is the other half of R78, and neither half works alone.** The allowlist lets the call
      be *serialized*; the filter is what leaves a call there to serialize. R78 landed first and
      fixed six reds elsewhere, which is why the remaining six looked like a separate problem.

      **BOTH HANDED-OVER TRIAGES WERE WRONG, AND THEY WERE WRONG THE SAME WAY.**
      `Collate_case_sensitive_constant` was called "genuine and not yet triaged — one expression
      shape rather than the feature", which was right about the symptom and wrong about the cause.
      The four `Least_with_parameter_array_is_not_supported` /
      `Greatest_with_parameter_array_is_not_supported` were filed under **A63** — "the refusal
      happens on the server and arrives wrapped" — and that was never read out of the message.
      The log said `String: "An exception was thrown while attempting "···`, which is EF's
      *client-evaluation* wrapper: the call never reached the wire, so there was no server refusal
      to be wrapped. **A63 was assumed from the shape of the assertion, not from the string the
      assertion printed.**

      **The rule that generalises, and it is the cheap half of an existing one.** CLAUDE.md already
      says a classification is not evidence and age is not evidence. R80 adds the narrower case:
      **when a family is filed under a known class, check that the recorded symptom is the observed
      one.** Six reds sat in two buckets for a day because nobody compared "arrives wrapped from the
      server" against a message that names client evaluation in its first eight words. The
      distinguishing tell was in the *passing* siblings all along — every green `Collate_*`,
      `Least` and `Greatest` takes a **column**, and all six reds take a constant or a captured
      array, which is exactly what makes a subtree evaluatable.

      `src/` changed, so both gates: `eng/trim-ratchet.sh` holds at `ours 89 <= 89` (`total` 855),
      and `CI=true dotnet build --configuration Release` reports the documented
      `5 Warning(s), 0 Error(s)`. Both halves of the baseline moved together.

      **Triage banked on the way past, at the cost of one tally: `TPCGearsOfWarQueryInfoCarrierTest`
      and `TPTGearsOfWarQueryInfoCarrierTest`'s 36 reds are not 36 things and were not untriaged.**
      They are **nine test names, each in both classes, sync and async**. 32 fail with
      `Assert.Throws() Failure: No exception was thrown` from `AssertTranslationFailed` — the base
      asserts a relational translator must refuse a correlated collection with `Distinct`, and this
      provider answers it, which is `website/docs/limitations.md`'s "queries this provider answers
      where other providers refuse". The other 4 are C64's
      `Correlated_collection_with_distinct_3_levels`, whose assertion no correct answer can satisfy.
      **Both classes' own doc comments already said exactly this**; the tally only confirmed it.
      **Nothing here is a defect and nothing here is work.**


- [x] **R82. `UseRelationalNulls` reaches the server, and what was missing was a service lifetime
      rather than a client option.**
      `failed` 192 -> 181, `total` unchanged at 29214, FIXED 11, BROKEN none.
      `NullSemanticsQueryInfoCarrierTest` goes from 18 red to 7. `test/` only, so `eng/measure.sh`
      and not the trim ratchet.

      **The flag never had to cross the wire.** `NullSemanticsQueryTestBase` declares
      `CreateContext(bool useRelationalNulls)`, and EF's SQLite class implements it on the
      *client's* options builder, which `UseInfoCarrier` has none of. R56 therefore accepted the
      flag and dropped it, and every test that passed `true` got C# null semantics where it asked
      for the store's. But the owner's rule already settled where the flag belongs: **ambient
      provider configuration is the server's, and only per-query hints in the expression tree may
      cross.** `UseRelationalNulls` decides SQL, and the SQL is the server's.

      **What was actually missing was a lifetime.** The server's `DbContextOptions` were registered
      `Singleton`, so one server served every test in the class and no individual request could
      configure it — and this class asks for relational nulls in some tests and C# nulls in others,
      which one options instance cannot answer. `SharedTestStoreProperties.ServerOptionsLifetime`
      is the new seam. This fixture is the only one that asks for `Transient`; every other fixture
      keeps `Singleton` and pays nothing, because transient options are rebuilt on every server
      context resolution. The request carries the value in an `AsyncLocal` that `CreateContext`
      writes — a synchronous method, so the write is visible to the test that called it — and the
      fixture's `onAddOptions` reads. **`CopyDbContextParameters` is the other per-request seam and
      could not be used**: it runs after the server context exists, and an options extension has to
      be in place before one is built.

      **The reading that was wrong was "the flag cannot cross".** Nothing about the product had to
      change, and no product code did. **The rule that generalises: when a test asks for
      configuration this client cannot express, ask whether the SERVER can express it before
      recording the test as a limitation.** The harness owns the server.

      **The remaining 7 in that class are not null semantics, and one of them re-prices a whole
      family.** Six are `BoolSwitch` and `Cases`, which `NullSemanticsQueryFixtureBase` declares
      with `HasDbFunction`. That is the same mechanism as the 75 in `UdfDbFunctionInfoCarrierTest`,
      so **the `HasDbFunction` mechanism is 81 tests, not 75**, and nobody had connected the two
      because the six sat inside a class named for null semantics. The seventh is
      `From_sql_composed_with_relational_null_comparison`, which is `FromSql` and is #60.

      **`HasDbFunction` was also measured, and it is not "unsupported".** A probe admitted
      `NullSemanticsQueryFixtureBase` to `TypeAllowlist` by name and **all six went green**. The
      refusal is at the client type boundary: the class declaring the mapped method is not on the
      allowlist, so `QuerySplitter.RejectClientEvaluation` raises EF's `TranslationFailed` while the
      server — an ordinary relational provider with the same model — translates it. R74's
      "this provider does not support `HasDbFunction`" describes a symptom, not a cause. The 75 in
      `UdfDbFunctionInfoCarrierTest` split 32 of that shape and 22 of R80's shape (a function call
      with only constant arguments, client-evaluated because
      `RelationalEvaluatableExpressionFilter`'s `model.FindDbFunction` clause is not ported). **The
      fix is model-derived and therefore not a trust-boundary change** — admit the declaring type of
      every `DbFunction` the model itself declares, exactly as `ForModel` already admits every
      entity and property type — but reading that annotation without a reference to
      `EFCore.Relational` needs an M9 J5 style seam, and that is a step of its own.


- [x] **R83. `SharedTypeQueryRelationalTestBase` ADOPTED, by moving its non-relational base off
      Tier A rather than running it on both.**
      `failed` 181 -> 185, `total` 29214 -> 29218. **A deliberate rise**: 6 tests, **2 green**,
      4 red, FIXED none, BROKEN exactly the 4 and every one inside the new class. Compliance missing
      list **11 -> 10**. `test/` only, so `eng/measure.sh` and not the trim ratchet.

      **Moved, not added, and that was the whole design decision.**
      `SharedTypeQueryRelationalTestBase` derives from `SharedTypeQueryTestBase`, which this
      repository already ran on Tier A. Adopting the relational base beside it would have run the
      shared base's tests on both tiers, which CLAUDE.md calls duplication rather than coverage. So
      the class moved to Tier B whole. **Its two inherited tests still pass there, so the move
      itself cost nothing** — all four new reds are the relational base's own.

      **The four reds are two mechanisms, and one of them was not known.** Three of them —
      `Can_use_shared_type_entity_type_in_query_filter_with_from_sql` (sync + async) and
      `Ad_hoc_query_for_default_shared_type_entity_type_throws` — fail with the *same*
      `System.ArgumentException` raised inside EF's own `QueryFilterRewritingConvention`, **while
      building the client's model, before any query runs**: *"Expression of type
      `IQueryable<Dictionary<string, object>>` cannot be used for parameter of type
      `DbSet<Dictionary<string, object>>` of method `FromSqlRaw`"*. A `HasQueryFilter` whose body
      calls `FromSqlRaw` cannot be rewritten for this client. **That is not #60's runtime `FromSql`
      gap — it is a model-build failure**, it happens on the client alone, and no test in this suite
      named it before. Whether #60 closes it is unproven and should not be assumed.

      **The fourth is R77's cast, and R77 still buys nothing.**
      `Ad_hoc_query_for_shared_type_entity_type_works` casts the test store to `RelationalTestStore`
      and then calls `SqlQueryRaw`. Making the client store relational would remove the cast and not
      the red, because the call behind it needs raw SQL on a client that has no database. **This is
      the second base measured against R77's premise and the second to refuse it.**

      **What the handoff predicted and what the run showed.** The handoff read this base as
      "3 tests, all reaching `SqlQueryRaw`/`FromSqlRaw`". The count was right and the conclusion was
      half right: all four reds do involve raw SQL in the source, but three of them never reach it,
      failing in model building instead. **Reading which API a test calls does not tell you where it
      fails.**


- [x] **R84. `HasDbFunction` works. It was never unsupported — two boundaries refused it, and they
      pulled in opposite directions.**
      `failed` **unchanged at 185**, `total` 29218 -> 29220 (two new pin tests). **FIXED 12,
      BROKEN 12.** This is precisely the case `eng/measure.sh` exists for and the count cannot see,
      so the verdict below is read out of the reasons diff. `src/` changed, so both gates:
      `eng/trim-ratchet.sh` holds at `ours 89 <= 89`, and `CI=true dotnet build --configuration
      Release` reports the documented `5 Warning(s), 0 Error(s)` — **from a clean sample build**,
      because a second Release build in one session reports `0 Warning(s)` by incremental skip.

      **The two boundaries.** `TypeAllowlist` would not let a mapped function be *named*: the class
      declaring it is a `DbContext` subclass or a static helper, never an entity type, so
      `QuerySplitter` raised EF's `TranslationFailed`. And EF's parameter extraction *evaluated* the
      call whenever its arguments were all constants, which runs a body that exists only to throw.
      Fixing one without the other only moves a test from the first failure to the second.
      `Metadata.ModelDbFunctions` reads the model's own `Relational:DbFunctions` annotation by
      string (M9 J5's route), `TypeAllowlist.ForModel` admits each declaring type, and
      `InfoCarrierEvaluatableExpressionFilter` ports the `model.FindDbFunction` clause **R80
      deliberately left out on the belief that it could never fire**. It fires for 22 tests.

      **Model-derived, so `security-review.md` §2's conjunction is untouched.** Nothing static is
      widened. The methods come from the application's own `OnModelCreating`, exactly as the entity
      and property types the allowlist already admits do, and §2a's C53 argument applies word for
      word — including its guard, so a declaring type on the reflection invocation surface is
      refused rather than trusted.

      **The 12 fixed are the functions that need no store function**: six `BoolSwitch` and `Cases`
      tests in `NullSemanticsQueryInfoCarrierTest`, six in `UdfDbFunctionInfoCarrierTest`, every one
      mapped with `HasTranslation` so the server builds SQL from the tree and asks the store for
      nothing.

      **The 12 broken are all `Scalar_Nested_Function_*_Instance` and all say "No exception was
      thrown".** The base asserts that a relational provider must *refuse* a query mixing client and
      server calls; this provider answers it, because the projection split reassembles on the
      client. That is `website/docs/limitations.md`'s "queries this provider answers where other
      providers refuse", the same family as the 32 TPC/TPT GearsOfWar reds. **Not one is a wrong
      answer** — the assertion is over the exception, not over a value.

      **The rest of that class converged with the reference provider, which is why a flat count is
      progress.** Its reds used to read *"the client refuses"* (38) and *"the client evaluated it"*
      (22). They now read `SQLite Error 1: no such function: CustomerOrderCount` and eight siblings.
      The query reaches SQL, and SQLite has no `CREATE FUNCTION`. CLAUDE.md names exactly this as
      convergence rather than regression.

      **What this suite structurally cannot show, and it is the point of the change.** On a store
      that *has* the function — SQL Server, which EF ships the only provider class for — a mapped
      function now reaches the store and works, where before it could not leave the client. ADR-009
      Tier B is SQLite and M7's SQL Server tier is dropped, so no test here can demonstrate it.
      **`Microsoft.Data.Sqlite` can register a function per connection**, which needs a connection
      interceptor on the harness server. R74 priced that at "two store-side reds and none of the
      other 73" and declined it; after this step it is worth about **fourteen**, and the pricing
      should be redone rather than inherited.

      **How the reader was found wrong, and the pin test is what found it.** The first
      implementation read a public `MethodInfo` property off the concrete class. A finalized model
      holds `RuntimeDbFunction`, which implements `IReadOnlyDbFunction.MethodInfo` **explicitly**,
      so the lookup answered "this model maps no functions" — on every model this provider ever
      sees. `DocumentMappingPinTest` caught it because it compares against EF's own
      `GetDbFunctions()` rather than asserting a count. **A pin test that asserted a number would
      have passed.**


- [x] **R85. A caller could not use their own store's `EF.Functions`. The fix is a registration the
      application makes on both sides, not a list inside this package.**
      `failed` unchanged at 185, `total` 29220 -> 29223 — three new `SqliteSmokeTest` cases, all
      three green. FIXED none, BROKEN none, REASONS unchanged. `src/` changed, so both gates:
      `eng/trim-ratchet.sh` holds at `ours 89 <= 89` and a **clean** Release build reports the
      documented `5 Warning(s), 0 Error(s)`.

      **The gap, measured rather than reasoned about.** `EF.Functions.Like` works everywhere because
      it is declared on EF Core's *core* `DbFunctionsExtensions`. `Glob` is declared on
      `SqliteDbFunctionsExtensions`, `DateDiffDay` on `SqlServerDbFunctionsExtensions`, and
      `InfoCarrier.Core` references no provider, so it can name neither. A probe run against the
      SQLite tier was refused by `QuerySplitter.RejectClientEvaluation` while the server translated
      the identical call to `GLOB`. **One green `Like` test is what hid this**, which is R78's lesson
      again: a gap with no test naming it is a gap nobody is looking at.

      **Why not a list of names in this package.** It cannot enumerate providers it does not
      reference, so it would be wrong for every third-party store; and a *pattern* — "any class
      called `*DbFunctionsExtensions`" — cannot be reviewed at all, because `security-review.md`
      §2's argument is a per-class conjunction and a pattern admits classes nobody has seen.

      **The shape, and the two halves do different jobs.** `UseInfoCarrier(client, o =>
      o.AllowTypes(…))` on the client; `services.AddInfoCarrierAllowedTypes(…)` on the server. The
      client's list decides what this application's own code may **send** and is not a security
      boundary. The server's decides what a **payload** may name and is. §2 already ended with the
      sentence this implements — *"only ever by an application registering one explicitly, which is
      its own decision"* — and it had no API behind it until now. §4c records the reading.

      **EF's nested-options-builder idiom**, not a `params Type[]` overload of `UseInfoCarrier`:
      `InfoCarrierDbContextOptionsBuilder` is the shape every EF provider uses
      (`UseSqlite(conn, o => o.CommandTimeout(30))`), so the next option needs no new overload.
      `WithAllowedTypes` is **additive rather than replacing**, unlike every other `With…` on an EF
      options extension, because it names a set and a caller configuring options in two places would
      otherwise silently lose the first list.

      **Three tests, and the third is the one that earns the two registrations.** The call is refused
      with nothing registered; it works with both halves; and **registering on the client alone still
      fails on the server**, with the deserializer's own rejection message. Without that third case
      the two registrations read as duplication. It is ADR-012's value-mapper rule restated for
      types: admitted on one side only is worse than admitted on neither.


- [x] **R86. The harness server defines its own SQLite functions, and R74's price for that was
      stale by fourteen.**
      `failed` **185 -> 171**, `total` unchanged at 29223. FIXED 14, BROKEN none. The REASONS diff
      is **removals only** — all seven `no such function` classes gone, nothing added.
      `test/` only, so `eng/measure.sh` is the gate and the trim ratchet is not.

      **The price was counted, not estimated.** `UdfDbFunctionInfoCarrierTest` is 81 of the 185, and
      **14** of them named a missing scalar function in `artifacts/measure/r85.log`. Exactly those
      14 are the FIXED list. R74 priced this at *"two store-side reds and none of the other 73"* and
      declined it; what invalidated that was **R84**, which made `HasDbFunction` work and so moved
      60 reds from *"the client refuses the mapped call"* to *"the store has no such function"*.
      R85 flagged the pricing as stale and it was.

      **SQLite has no `create function`, which is the whole difficulty.** EF's SqlServer fixture
      writes its definitions into the database in `SeedAsync`; `Microsoft.Data.Sqlite` attaches a
      delegate to **one open connection** through `SqliteConnection.CreateFunction` and writes
      nothing to the file. So the definitions have to be reapplied on every connection, which is
      what `SqliteFunctionInterceptor` (a `DbConnectionInterceptor`, in the test utilities) does.

      **No product code changed and no new plumbing was added.**
      `SharedTestStoreProperties.OnAddOptions` already existed and
      `InfoCarrierBackendTestStore.AddProviderOptions` is its only reader, so it configures the
      **server** context and only the server context — the client has no database and no
      connection to intercept. The fixture passes its own function definitions through it.

      **The two functions that read the database do so on the connection they were called on.**
      SQLite permits a function callback to read through its own connection; a second connection
      would be a second transaction, and a UDF called inside one would answer from the wrong
      snapshot. `CustomerOrderCount` alone is 7 of the 14, so that half had to work.

      **Five more functions were written, measured, and deleted.** `StringLength`, the three
      `IdentityString` variants and `AddValues` are all in EF's SqlServer fixture and every one
      bought nothing here: the class ran 81 -> 67 with them and 81 -> 67 without. `DollarValue` is
      the one kept without a red of its own, because it is `StarValue`'s twin and shares its
      implementation.

      **Four reds in that class are still the store's and are not worked around.** Two are the
      table-valued functions, which `Microsoft.Data.Sqlite` cannot express at all. Two are
      `IdentityString`, mapped `[DbFunction(Schema = "dbo")]`, so the server emits a
      schema-qualified call and SQLite answers `near "(": syntax error` — a schema is not
      something a connection-scoped function can carry.

- [x] **R87. What the client loses by not being relational, listed once instead of a defect at a
      time.** Documents only — `docs/architecture.md` **§6a D7**. No gate runs; nothing executable
      changed.

      **Three defects in one session were one shape**, and none was found by looking: R80
      (`EF.Functions.Collate` over a constant, executed on the client), R84 (a `HasDbFunction` call
      over constant arguments, the same), and the `FromSql` query filter that still fails while the
      **client's** model is built. EF registers a different set of services and conventions when a
      provider is relational; this client gets the core set; the difference had never been written
      down.

      **The cut is ADR-006 and it does most of the work.** `EntityFrameworkRelationalServicesBuilder
      .TryAddCoreServices` makes **61** `TryAdd` calls; **50** of them are downstream of
      `IDatabase.CompileQuery` and belong to the server's provider — not missing here, not wanted.
      D7 names the eleven that are not, with a verdict on each, and four of the eleven are verdicts
      of *"nothing to lose"* reached by reading EF's class rather than by guessing from its name
      (`RelationalModelCustomizer` is an empty subclass; the two execution-strategy factories return
      the same strategy).

      **The live defect the audit names is `QueryFilterRewritingConvention`**, which is the one of
      `RelationalConventionSetBuilder`'s four replacements this client does not make. R88 fixes it.
      Three more rows are **open and unverified** and no failure is attributed to any of them:
      `IStructuralTypeMaterializerSource` (JSON-mapped complex types), `IAdHocMapper` (bound up with
      #60), `TableSharingConcurrencyTokenConvention` (a shadow property that would change what
      `SaveChanges` sends) and `RelationalDbFunctionAttributeConvention` (a function declared by
      **attribute** is not in this client's model at all, which R84's reader cannot see).

      **The rule that transfers:** a relational service is not automatically a store service, and
      the class name does not say which it is. The question is whether it runs before
      `IDatabase.CompileQuery`.

- [x] **R88. A `FromSql` query filter broke the client's model build, and the missing piece was the
      convention replacement R87 had just named.** `src/` change, so both gates: `eng/measure.sh`
      and `eng/trim-ratchet.sh` (`ours` 89 ≤ 89, `total` 855, unchanged).
      `failed` **171 -> 169**, `total` 29223 -> **29225** — two new `DocumentMappingPinTest` pins,
      both green. FIXED 2, BROKEN none.

      **It fires while the model is finalized, before any query runs**, which is why R83 read the
      base as *"3 tests, all reaching `SqlQueryRaw`/`FromSqlRaw`"* and was right about the API and
      wrong about where it failed. Core EF's `QueryFilterRewritingConvention` rewrites a `DbSet`
      access inside a filter into an `EntityQueryRootExpression`, typed `IQueryable<T>`; the first
      parameter of `FromSqlRaw` is `DbSet<T>`, so the rewritten call cannot be constructed:
      *"Expression of type `IQueryable<Dictionary<string, object>>` cannot be used for parameter of
      type `DbSet<Dictionary<string, object>>`"*. EF Core Relational does not have the problem
      because it **replaces** the convention, and D7 had just recorded that this client makes two of
      that builder's four replacements and not this one.

      **Leaving the `FromSql*` call alone is R82's rule, not the cheap way out.** The server applies
      its own model's filter with its own provider, where `FromSql` means something; the filter in
      the *client's* model only has to be representable. The alternative was to build a
      `FromSqlQueryRootExpression` by reflection — a relational query root, on a client with no
      store.

      **The third test with that exception converged rather than passed.**
      `Ad_hoc_query_for_default_shared_type_entity_type_throws` now builds its model, reaches
      `FromSqlRaw` on a non-relational client, and gets *"Relational-specific methods can only be
      used when the context is using a relational database provider"* — ADR-013's shape, and #60.
      Its reason moved from `ArgumentException` to `Assert.Equal Strings differ`, which is the whole
      of that line in the reasons diff going 8 -> 9.

      **The second pin is derived from EF rather than written down.** The methods the convention
      must leave alone are exactly those on `RelationalQueryableExtensions` whose *first* parameter
      is a `DbSet<>` — the parameter core EF's rewriter fills with an `IQueryable` — so a new
      overload group EF adds fails this test instead of failing a caller's model build.

- [x] **R89. A predicate calling a function mapped on the client's own context was neither shipped
      nor refused, and R84 is what removed the refusal.** `src/` change, so both gates:
      `eng/trim-ratchet.sh` (`ours` 89 ≤ 89, `total` 855, unchanged) and `eng/measure.sh`.
      `failed` **169 -> 157**, `total` 29225 -> **29226** (one new pin, green). FIXED 12, BROKEN
      none — and the 12 are the *same* `Scalar_Nested_Function_*_Instance` tests R84 broke.

      **The reasons diff across R84 is where this was visible, and the count hid it.** r83 -> r84:
      **38 `TranslationFailed` refusals disappear** and become 18 client evaluations and 15 "no part
      of the query can be executed", while `failed` went 181 -> 185. CLAUDE.md's three levels exist
      for exactly this, and even the names were not enough — only the reasons showed it.

      **Being nameable on the wire is not being shippable.** R84 admits the declaring type of every
      `HasDbFunction` mapping so the *call* can be named. For a function mapped as an **instance**
      method that type is the caller's own `DbContext`, so `QuerySplitter.ClientCodeFinder` — which
      refuses a method whose declaring type is not admitted — stopped firing, while the call stayed
      unshippable for a different reason: its `Object` is a constant holding the live client
      context.

      **Both outcomes were wrong, and which one a caller got depended on nothing that matters.**
      Measured with a boundary probe on two contexts rather than reasoned about:

      | The allowlist happened to… | What happened |
      |---|---|
      | refuse the context type | `shippable=1` — the bare query root. Neither shipped nor refused: **the client fetched the whole table and ran the predicate.** |
      | admit it | the whole query shipped and the **server** tried to rebuild a `DbContext` from the payload: *"Cannot dynamically create an instance … no parameterless constructor"*. |

      **The fix is two clauses and the first is the one that matters.**
      `ServerBoundaryAnalyzer.CarriesTheClientsContext` makes a constant holding a `DbContext` never
      server-ok, which collapses both cases into "not shipped"; `ClientCodeFinder` then turns "not
      shipped" into EF's own `TranslationFailed` instead of silent client evaluation. **The type
      allowlist cannot be asked to do this job**, because R84 needs that same type admitted for the
      name.

      **What this does not do is make the call work.** An instance-mapped function would need a wire
      node resolving to the *server's* context — a new capability handed to a payload, so a
      `security-review.md` question rather than a rewrite. Refusing is what every other EF provider
      does with a call it cannot translate. Recorded as an open question in `architecture.md` §6a
      **D7**.

      **The pin throws on purpose.**
      `SqliteSmokeTest.A_predicate_calling_a_mapped_function_on_the_client_context_is_refused` maps
      `SqliteSmokeContext.TitleIsLong` with `HasDbFunction` — that mapping is what puts the context
      type on the allowlist, which is the condition — and the method throws, so a regression arrives
      named in the assertion message rather than as a green count.

- [x] **R90. `UdfDbFunctionInfoCarrierTest`'s 55 remaining reds, every one classified.** Comments
      only — the class's own XML doc. No gate runs; nothing executable changed.

      The class began this session at **81 red of 106** and stands at **55**, with 50 passing and
      one skipped by EF itself. **Not one is a wrong answer.** The classification is read out of
      `artifacts/measure/r89b.log`, not carried over: 29 are EF's own `TranslationFailed`, 10 are a
      mapped function evaluated by the client inside an anonymous-type projection, 6 are a `QF_*`
      message assertion, 4 are this provider's own refusal wording, 4 are the store's, and 2 are
      one-offs.

      **The `QF_*` family is not a lever, and that is measured rather than assumed.** Every one is a
      *table-valued* function; SQLite has none, and `Microsoft.Data.Sqlite` offers no registration
      for one the way it does for a scalar (which is what R86 used). Moving the boundary so they
      ship would only move the failure — **the two that already reach the store are the proof, and
      they say `no such table`.** Nothing in that family is this provider's and nothing in it is
      work.

      **The 10 are a real semantic gap and a small one.** A mapped function in a *final projection*
      is answered by the client's own method rather than the store's, because the projection split
      reassembles client-typed projections here — and a final projection is exactly where EF permits
      client evaluation, so this is inside EF's contract, not outside it. What differs is *whose*
      implementation runs, which matters only where a function's CLR body and its store definition
      disagree. Every stub in this base throws, so it surfaces as `NotImplementedException` and
      never as a wrong value. Closing it means hoisting a mapped call out of the residual into the
      server's tuple; **not priced, and not started.**

- [x] **R91. Two of D7's open rows settled by running them, and both answers differed from the
      prediction.** `test/` only, so `eng/measure.sh` is the gate.
      `failed` **unchanged at 157**, `total` 29226 -> **29228** — two new `SqliteSmokeTest` probes,
      both green. FIXED none, BROKEN none, REASONS unchanged.

      **`TableSharingConcurrencyTokenConvention` cannot fire on any store this suite has.**
      `GetConcurrencyTokensMap` skips a token that is not *also* `ValueGenerated.OnUpdate`, and on a
      token that means `rowversion`, which SQLite has not. So the client not running the convention
      is unobservable here **by construction rather than by luck** — which is a much stronger
      statement than the "open, unverified" it replaces. **The first version of the probe used a
      plain `IsConcurrencyToken()`, passed, and proved nothing**; the two model assertions are what
      caught that, and they are CLAUDE.md's "establish that the code ran" in its exact shape. Forced
      to fire with `ValueGeneratedOnAddOrUpdate`, the divergence is real — the server's model gains
      the synthesized property, the client's does not — and both halves of a table-split pair still
      round-trip and save, because the server applies its own model to entries the client sent by
      property *name*.

      **A `[DbFunction]`-attributed function still crosses, which was not the prediction.** The
      attribute is read by a relational convention, so the server's model maps the method and the
      client's does not — asserted both ways. But what decides the outcome is the **allowlist**, and
      the context type was on it for an unrelated reason (another function's `HasDbFunction`
      mapping). The static call ships, the server translates it against its own model, and the only
      thing that fails is the store lacking the function. Had the context declared no mapped
      function at all, the same call would have been refused.

      **The pattern the two probes exposed is the part worth keeping.** R84, R89 and R91 were each
      decided by whether the type allowlist happened to admit a type. It is documented as a
      deserialization control and is also, undocumented, what decides where the query boundary falls
      and whether an untranslatable call is refused or quietly run on the client. `architecture.md`
      §6a **D7** now says so.

      **Still open in D7:** `IStructuralTypeMaterializerSource`, `IAdHocMapper` and
      `RuntimeModelConvention`. None has a failure attributed to it and none was probed here.

- [x] **R92. `NorthwindBulkUpdates` priced, and #60 scoped rather than started.** Documents only —
      `architecture.md` §6a **D8**. No gate runs; nothing executable changed.

      **`NorthwindBulkUpdates`' 6 reds: none is cheap and none is clearly this provider's.** Four
      are `Update_FromSql_*` / `Delete_FromSql_*` — #60, and blocked twice over, since they also
      carry R77's `InvalidCastException`. The other two are
      `Update_with_invalid_lambda_in_set_property_throws`, and they are **the right refusal with the
      wrong message**: the base asserts `CoreStrings.NonQueryTranslationFailedWithDetails` whose
      details clause is `RelationalStrings.InvalidPropertyInSetProperty(…)` — **a localized
      relational resource this package cannot name**, because it does not reference
      `EFCore.Relational` (D3). Reading it by string, as `AnnotationDocumentMapping` reads an
      annotation, is a much worse trade for a user-facing message than for a metadata key. **Priced
      and not taken.**

      **#60 priced: 27 of the 157 failures and 6 of the 10 missing bases**, counted out of
      `artifacts/measure/r91.log`. The standing note that *seven* of ten wait on it is wrong; it is
      six, and the other four are unrelated. **The first pass at this said 21 and was low** — it
      matched on the test name and missed the ones that fail earlier, on R77's
      `InvalidCastException`. That cast is not a separate item but this one's first blocker, and
      **reviving R77 alone would buy no green test at all**: all 26 tests carrying it are raw-SQL
      tests that would then fail one step later.

      **The finding that decides the sequencing is not a count.** `FromSql` would be the first
      construct where the client hands the server **a string to execute**, and every argument in
      `security-review.md` is about what a payload may *name* — a payload that names nothing
      dangerous can still carry `DROP TABLE`. That is a change of posture rather than an extension
      of the allowlist, and §2's per-class conjunction cannot be stretched over it. **D8 recommends
      the security section first and on its own**, before either piece of code; the shape of both
      code pieces is already known and neither is the hard part.

- [x] **R94. What a raw SQL string is actually allowed to do, measured before the gate was named.**
      `test/` only, so `eng/measure.sh` is the gate. `failed` unchanged at 157, `total` 29228 ->
      29231; FIXED none, BROKEN none, REASONS unchanged. The three new tests are
      `Sqlite/RawSqlExecutionProbeTest` and all three are green.

      **Moved ahead of the registration on purpose, and the reason is that it decides the
      registration's name.** D8 recommended the security section first; this is the half of that
      section which is a fact rather than a position, and landing the gate before measuring it
      would have meant rewriting the prose afterwards. Two questions, both about
      `Microsoft.Data.Sqlite` and EF Core rather than about this provider, and nothing here crosses
      the wire.

      **Does one `CommandText` execute more than one statement? YES.**
      `SELECT 1; DROP TABLE Probe;` drops the table. Measured on both driver paths, because they
      are different code and only the second is the one EF takes for a query: `ExecuteNonQuery`
      runs everything, and on `ExecuteReader` the trailing statements run as the reader is advanced
      past the first result set - which disposal does, so a caller who reads one row and stops has
      still run the `DROP`. `Microsoft.Data.Sqlite` prepares and steps the statements in sequence
      by design and offers no single-statement mode.

      **Does EF hand an uncomposed `FromSqlRaw` to the store unwrapped? YES.** The caller's string
      is the whole command, character for character - the only difference is the newline
      `ToQueryString` appends, and the query is executed as well so the assertion does not rest on
      a path the execution does not take. The `FROM (<sql>) AS x` wrap does exist, and the contrast
      test pins it, but it appears only when something is composed on top: it is an artefact of
      composition, and **the caller decides whether to compose**.

      **What the two answers settle.** There is no read-only version of `FromSql` to grant.
      Enabling it enables arbitrary SQL execution on the server's connection under whatever rights
      it holds, so the gate must be named for that and not for the API it unblocks - which is what
      R95 does. The answers live in the test file's own remarks rather than in prose here, because
      a fact in prose goes stale and this one is what a review will rest on.

- [x] **R95. The raw-SQL gate, and the wire node that makes it mean something (#60).** `src/`
      change, so both gates plus the CI Release build. `failed` unchanged at 157, `total` 29231 ->
      29235; FIXED none, BROKEN none. Trim ratchet `ours` 89 <= 89, `total` 855. `CI=true
      dotnet build --configuration Release` reports the documented `5 Warning(s), 0 Error(s)`.

      **The name is the finding, and R94 is where it comes from.** `AddInfoCarrierArbitrarySqlExecution`
      on the server, `AllowArbitrarySqlExecution` on the client. Not "enable `FromSql`": one
      `CommandText` executes every statement it contains and an uncomposed `FromSqlRaw` reaches the
      store unwrapped, so what is granted is arbitrary SQL execution on the server's connection and
      there is no read-only subset of it to offer. `security-review.md` **section 5a** is the
      written form, and it also corrects section 5's first bullet: a client cannot influence the
      server's query filters, but it can now write a query they are not part of.

      **R85's two halves, doing the same two jobs.** The server's registration is the security
      boundary and is default-deny; the client's option governs only what this application's own
      code may send. Four tests in `SqliteSmokeTest` pin it in R85's three shapes plus one: refused
      when neither side grants it, works when both do, **refused by the server when only the client
      grants it**, and the arguments cross as values. The third is the one that makes the two
      registrations something other than duplication.

      **The registration and the wire node landed together on purpose, against the sprint's stated
      order.** A gate that admits a node the wire cannot carry is a switch that turns on a broken
      path, and the one-commit window between them would have been exactly that. `D8`'s own
      recommendation - the security section first and on its own - is honoured by R94, which is the
      half of that section that is a fact.

      **`FromSqlQueryRootStubNode`**, a subclass of `QueryRootStubNode` carrying `Sql` and the
      arguments node. `QueryRootStubNode` is unsealed for it and for nothing else; the wire mirrors
      EF's own hierarchy, where a root with extra state is a subclass too.
      `ServerBoundaryAnalyzer.IsSerializableKind` still matches EF's plain root by EXACT type and
      still refuses every other subclass - the new clause is one more exact shape, and only when
      the client option is set, so the refusal path is byte-identical for everyone else.

      **`RelationalQueryRootShape` names EF's type by shape**, because `InfoCarrier.Core` does not
      reference `EFCore.Relational` (D3) and R75's comment already named it that way. Two
      `GetProperty` reads on the client, one `Activator.CreateInstance` on the server, each with a
      narrow `[UnconditionalSuppressMessage]` saying why the members survive trimming. **The trim
      count still rose to 90 on the first run, and the cause is worth carrying**: an
      `[UnconditionalSuppressMessage]` covers the annotated member's own body, and a lambda
      compiles to a member of its own - `.Select(a => a.GetType(...))` reported its IL2026 against
      `<>c.<ResolveFromSqlRootType>b__5_0`, outside the suppression. Rewritten as a `foreach` it is
      89 again.

      **The arguments are bound, never interpolated.** They cross as an ordinary constant node and
      are handed back to EF, which makes `DbParameter`s of them. Not because injection is the
      threat - a client that can send this node already writes whatever SQL it likes - but because
      a value put into text has lost its type. Pinned with a value containing a quote.

      **The 27 blocked spec tests are untouched by this and stay red.** They need the harness work
      as well: R77's `RelationalTestStore` cast blocks 26 of them before the query is ever built,
      and no fixture grants the gate. That is the next step, and the gate is what unblocks it
      rather than the other way round.

- [x] **R96. The raw-SQL spec bases pointed at the gate, and R77 revived to unblock them (#60).**
      `test/` only, so `eng/measure.sh` is the gate. **`failed` 157 -> 133**, `total` unchanged at
      29235. **FIXED 24, BROKEN none.**

      **Neither half buys anything alone, which is why they land together.** R77's
      `RelationalInfoCarrierTestStore` was parked on 2026-09-01 having measured **zero** green
      tests - it turned 26 `InvalidCastException`s into 26 `FromSql` refusals, and a refusal was
      the right answer then. R95 changed that, and the cast is the *first* blocker on 26 of the 27
      raw-SQL reds. Cherry-picked as written, with the `ServiceLifetime` and relational-nulls work
      that landed in between merged around it, and ADR-013's amendment re-dated to this step.

      **The grant is per fixture, as `relationalClientStore` is, and for the same reason.**
      `SharedTestStoreProperties.ArbitrarySqlExecution` gives the server
      `AddInfoCarrierArbitrarySqlExecution()` and the client `AllowArbitrarySqlExecution()`. Seven
      fixtures opt in. **Not granted suite-wide**: the default refusal is what every other fixture
      exercises, two of them assert it directly through `FromSqlAssertions`, and a global grant
      would claim of every fixture that its deployment had made a security decision it has not.

      **The 24, by class.** 14 `JsonQuerySqlite` `FromSql_on_entity_with_json_*`, 3
      `TPHInheritanceQuery` (including `Casting_to_base_type_joining_with_query_type_works`, the
      real defect R77 recorded the cast as hiding), 2 `OwnedQuery`, 2 `QueryNoClientEval`, 2
      `NorthwindBulkUpdates.Update_FromSql_set_constant`, and 1 each of `NullSemanticsQuery` and
      `SharedTypeQuery`.

      **Where the other three stopped, read out of the reasons diff rather than the count.**

      - **`NorthwindBulkUpdates.Delete_FromSql_converted_to_subquery` (2) moved from a cast to
        `SQLite Error 1: 'no such table: Order Details'`** - a *harness* mismatch, not a wire one.
        The base is written against `NorthwindRelationalContext`, which maps `OrderDetail`
        `ToTable("Order Details")`; this tier builds its store from `NorthwindContext`'s core model,
        where the table is `OrderDetails`. `Update_FromSql_set_constant` passes because it names
        `[Customers]`, which both models agree on. Same shape as the `ProductView` and
        `Product.CategoryID` notes already in `NorthwindInfoCarrierSqliteServerContext`.
      - **`SharedTypeQuery`'s remaining 2** moved to
        `"Relational-specific methods can only be used when the context is using a relational
        database provider"`, raised by `RelationalDatabaseFacadeExtensions.SqlQueryRaw`. That is
        **D8 item 2** - `SqlQuery<T>` and `Database.ExecuteSql` are separate entry points, not query
        roots - and is untouched by this step, as D8's amendment says.

      **A test that moves from a cast to a translation failure to a real store error has moved
      twice and the count says so only once.** Two of these three are further along than they were
      and still red; the reasons diff is the only thing that shows it.

- [x] **R97. `Delete_FromSql_converted_to_subquery`'s table name: tried, measured at 369, reverted.**
      Nothing executable changed — this entry is the record, and it is here because the change
      *looked* like a one-liner and the run said otherwise.

      **The hypothesis was right about the cause.** R96 left those two failing on
      `no such table: Order Details`, because the base writes `FROM [Order Details]` —
      `NorthwindRelationalContext`'s mapping — while this tier builds its store from
      `NorthwindContext`'s core model, where the table is `OrderDetails`. Adding
      `modelBuilder.Entity<OrderDetail>().ToTable("Order Details")` to
      `NorthwindInfoCarrierSqliteServerContext` **does fix both**: run alone, the class goes to
      173 of 175, the two remaining being `Update_with_invalid_lambda_in_set_property_throws`,
      which R92 already priced as the right refusal with a message this package cannot name.

      **And it broke 236 other tests.** `failed` 133 -> **369**, every new one
      `SQLite Error 1: 'no such table: OrderDetails'` raised inside
      `SharedStoreFixtureBase.InitializeAsync` — at **seeding**, not at query. The store named
      "Northwind" is shared by many Tier B classes, only the first of them creates the schema (the
      `Created` guard in `SqliteInfoCarrierBackendTestStore`), and not all of them build their
      server context from `NorthwindInfoCarrierSqliteServerContext`. Renaming the table in one of
      those contexts therefore splits the schema from the seed.

      **Reverted, and the two stayed red until R101. The diagnosis in this entry was wrong and R101
      says why.** It read "the fix is not one line: every server context sharing the Northwind store
      must agree on the table name", which was inferred from the shape of the failure rather than
      from its stack. The stack names one line in one file. **The half of this entry that stands is
      the measurement**: a per-class run and a full run answer different questions, and only the
      second one knows about a shared store.

- [x] **R98. `FromSqlQueryTestBase` adopted — the first base #60 unblocks outright, and a
      `DbParameter` turns out to cross.** `test/` only. **`failed` 133 -> 147, a deliberate rise of
      14**, `total` 29235 -> 29383 (148 new tests, 134 of them green). The missing-bases list drops
      **10 -> 9**.

      **134 of 148, and the 14 reds are classified in the class's own remarks rather than here.**
      Six are this tier's `Product.CategoryID` harness note reappearing, four reach for the
      client's own `DbConnection` and are refused by ADR-013's design, two cast the client's type
      mapping to `RelationalTypeMapping` inside the base's own body, and two are D8 item 2.

      **The finding is not the count.** The first run was **94 of 148**, and 32 of the 54 failures
      were `Type 'Microsoft.Data.Sqlite.SqliteParameter' is not on the deserialization allowlist`.
      **A `DbParameter` crosses this wire perfectly well**: it is an ordinary object with a
      parameterless constructor and settable properties, the wire walks it, and the server rebuilds
      it with no special handling. It was refused only because ADR-008 constraint 2 refuses every
      type the model does not imply, and R85's seam is what admits it —
      `InfoCarrierBackendTestStore.StoreParameterType`, admitted on both halves alongside the
      raw-SQL grant because the two are only ever wanted together.

      **D8 item 2 is amended rather than closed.** Half its stated reason was wrong: "passes
      `DbParameter` objects, a provider type the client cannot construct" was never tested and is
      false — a test project references its own server's provider, as any such application would.
      The half that stands is that `SqlQuery<T>` and `Database.ExecuteSql` are separate entry
      points: `RelationalDatabaseFacadeExtensions.GetFacadeDependencies` refuses a non-relational
      context before a query is built at all, and that is where four tests now stop.

      **Fourth time in this issue that the type allowlist decided the behaviour** — R84, R89, R91,
      R98. D7's note that it is load-bearing far beyond deserialization safety is the general form,
      and this is the first time it decided something in the *helpful* direction.

      **`QueryNoClientEvalInfoCarrierTest`'s class doc is corrected in the same commit.** It called
      two of its three reds "permanently red until #60 is decided"; they went green in R96 and the
      paragraph had outlived them.

- [x] **R100. `GearsOfWarFromSqlQueryTestBase` adopted, and #60's scope closed out.** `test/` only.
      `failed` unchanged at 147, `total` 29383 -> 29384. **Missing bases 9 -> 8.** The one test is
      green on its first run.

      **One test, and it needed a fixture nothing else here had.** The base is constrained to
      `GearsOfWarQueryRelationalFixture`, and this suite's Gears of War fixtures are the TPT and
      TPC ones, which derive from `GearsOfWarQueryFixtureBase` - a sibling, not a parent. So the
      fixture is new and brings a store of its own, for one assertion. **Adopted on policy rather
      than on the count**: EF ships a SQLite class for the base, so the only justification CLAUDE.md
      accepts for leaving one unadopted does not apply.

      **What #60 came to, against D8's numbers.** Of the **27** raw-SQL failures D8 counted, **25
      are green** (R96's 24 and R98's 1 in `SharedTypeQuery`), and the two that are not are
      `Delete_FromSql_converted_to_subquery`, whose cause is the harness mismatch R97 measured and
      reverted. Of the **6** missing raw-SQL bases, **2 are adopted** (`FromSqlQueryTestBase`,
      `GearsOfWarFromSqlQueryTestBase`). The remaining four are one axis, not four:
      `NorthwindSqlQueryTestBase`, `SqlQueryTestBase` and `SqlExecutorTestBase` all need
      `Database.SqlQuery`/`ExecuteSql`, which D8 item 2 describes and which
      `RelationalDatabaseFacadeExtensions.GetFacadeDependencies` refuses before a query is built;
      `FromSqlSprocQueryTestBase` needs stored procedures, which SQLite has not and for which EF
      ships no SQLite class.

- [x] **R101. The `Order Details` rename, done properly, and R97's diagnosis corrected.** `test/`
      only. **`failed` 147 -> 145**, `total` unchanged at 29384. FIXED 2
      (`Delete_FromSql_converted_to_subquery`, sync and async), BROKEN none.

      **R97 reverted this for a reason that turned out to be wrong, and reading the stack is what
      settled it.** The 236 breakages were not several server contexts disagreeing about a table
      name. They were **one hand-written table name in one statement**:
      `NorthwindQueryInfoCarrierSqliteFixture.SeedAsync` runs
      `UPDATE "OrderDetails" SET "Discount" = round("Discount", 2)`, the float-widening fix that
      predates all of this. Renaming the table on the model without renaming it there threw inside
      the shared store's initialization, and every class sharing "Northwind" then failed at
      `SharedStoreFixtureBase.InitializeAsync`. The 236 were one exception, counted once per test.

      **R97's entry asserted the general cause and this entry withdraws it.** The evidence for the
      general claim was the *shape* of the failure (many classes, at seeding) and the shape was
      consistent with both explanations. One `grep -m1 -A22` at the stack would have separated them
      the same day, and did not cost anything when finally run. **An evidenced hypothesis can be
      right about the evidence and wrong about the mechanism** is already in `CLAUDE.md`; this is
      that, at its smallest.

      Both places that name the table now carry a comment pointing at the other.

- [x] **R102. `Database.SqlQuery<T>` priced, and it is blocked by D3 rather than by wire work.**
      Documents only — `architecture.md` §6a **D8 item 2**. No gate runs; nothing executable
      changed.

      **The obstacle is a type test, and it runs before anything this repository owns.**
      `SqlQueryRaw` opens with `GetFacadeDependencies`, which throws `RelationalNotInUse` unless the
      context's dependencies **are** an `IRelationalDatabaseFacadeDependencies` — an interface in
      `EFCore.Relational`, which `InfoCarrier.Core` stopped referencing in M9 J5. No wire node, no
      allowlist entry and no boundary change is reachable past it.

      **Reversing D3 is step one of four, and it is a milestone exit criterion.** The other three
      are ordinary: a facade-dependencies implementation whose relational half throws (the shape
      `RelationalInfoCarrierTestStore` already proves), a `SqlQueryRootExpression` node that is a
      direct sibling of R95's, and — for a non-scalar `TResult` — getting `AdHocMapper`'s
      client-side entity type to cross, which is a new capability rather than a new node.

      **Worth 2 missing bases and 6 failures**, and there is no partial adoption: every test in both
      bases routes through `Database.SqlQuery`.

      **`SqlExecutorTestBase` is removed from this item.** Its first three tests are
      `Executes_stored_procedure`; it is a stored-procedure base wearing a general name, and
      **EF's own `SqliteComplianceTest` ignores it** along with `FromSqlSprocQueryTestBase` and
      `StoredProcedureUpdateTestBase`. Only SQL Server implements the three. D8's original sentence
      put a store limitation and a client limitation in one bucket.

- [x] **R103. The ignored-bases list aligned with EF's own SQLite one.** `test/` only. `failed`
      unchanged at 145, `total` unchanged at 29384. **Missing bases 8 -> 5.**

      **EF's `SqliteComplianceTest` has an `IgnoredTestBases` of its own and nobody here had read
      it.** It lists nine; three of them are bases this suite reports as missing, and only SQL
      Server implements any of the three:

      | Base | EF's reason |
      |---|---|
      | `FromSqlSprocQueryTestBase<>` | stored procedures, which SQLite has not |
      | `StoredProcedureUpdateTestBase` | EF's own comment: *"SQLite doesn't support stored procedures"* |
      | `SqlExecutorTestBase<>` | also stored procedures, despite the name |

      **`SqlExecutorTestBase` is the one worth naming.** Its first three tests are
      `Executes_stored_procedure`, `_with_parameter` and `_with_generated_parameter`, every one
      running a sproc through `Database.ExecuteSqlRaw`. D8 item 2 had paired it with
      `SqlQuery<T>`, which is a **client** limitation, where this is a **store** one. R102 split
      them and this removes it from the list.

      **The compliance test's own rule needed a second category, and it now has one.** It said only
      a base *conceptually inapplicable to a remoting provider* may be ignored. SQLite is this
      suite's only relational store (ADR-009 Tier B), so a base the reference provider declares out
      of scope for SQLite has no store here to run on either — which is precisely CLAUDE.md's bar,
      *"EF ships no test for it on any store we have"*. Each entry names EF's reason rather than
      inventing one.

      **Aligning means aligning what is MISSING, not deleting what is adopted, and the other six
      entries on EF's list say why.** Five of them are implemented here.
      `TPCRelationshipsQueryTestBase` and the three `Owned*Projection*` classes are **green** — EF
      ignores the projection family for its own issue #26708 and the TPC one for a
      test-infrastructure reason, and neither reaches this provider, so removing those classes
      would delete passing coverage. `UdfDbFunctionTestBase` is implemented with **55 classified
      reds** (R90); dropping it would lower `failed` by 55 without fixing anything, which is the
      failure mode CLAUDE.md names outright. **Listing an implemented base changes nothing**, so
      none of the five is listed.

      **The five still missing, and none is a SQLite question.** `NorthwindSqlQueryTestBase` and
      `SqlQueryTestBase` are D3-blocked (R102); `AdHocQuerySplittingQueryTestBase` needs
      `CloseConnection` and ADR-013 says it must not get one; `JsonUpdateTestBase` assumes a
      relational client; `StoreValueGenerationTestBase` has not been re-read since it was
      classified.

- [x] **R104. The server never had detailed errors on, so EF's materialization messages could not
      form.** `test/` only. **`failed` 145 -> 143**, `total` unchanged at 29384. FIXED 2
      (`Bad_data_error_handling_null_projection`, sync and async), BROKEN none.

      **Found by measuring a hypothesis that was wrong, which is the part worth keeping.** R98
      classified six `Bad_data_error_handling_null*` reds as this tier's `Product.CategoryID`
      addition showing through. Removing that property to check the price gave the answer directly:
      **the six did not go green, and four other tests broke.** What surfaced underneath was a
      second, unrelated cause.

      **EF wraps a failed column read into `ErrorMaterializingPropertyNullReference` only when
      detailed errors are enabled** — `ShaperProcessingExpressionVisitor` emits the try/catch under
      `if (_detailedErrorsEnabled ...)`. On this provider the read happens on the **server**, and a
      shared fixture's own `AddOptions` deliberately does not reach the server (A29), so the server
      had them off and a raw `SqliteException: The data is NULL at ordinal 5` crossed where EF's
      message was expected. One `onAddOptions` closes it.

      **Only two of the six, and the arithmetic on the other four is why they stay.** The projection
      pair never materializes the whole entity, so EF's FromSql column check does not fire and the
      wrap is all they needed. The other four do materialize `Product`, so they still stop at
      *"The required column 'CategoryID' was not present"*. Removing the property clears that and
      **costs four `NorthwindKeylessEntities` tests**, because `ProductView`'s `ToSqlQuery` reads
      the column — measured, +4 and -4, a net zero that trades message-text coverage for view
      coverage. **Not taken.**

      **What EF's own SQLite suite does about this: nothing, because it does not have the
      problem.** `NorthwindContext` line 49 is `e.Ignore(p => p.CategoryID)`, so its `Product` maps
      exactly the six columns the base's SQL selects, and EF's SQLite Northwind store is a prebuilt
      `northwind.db` rather than one built from the model. The property exists here only because
      this tier builds its store from the model and `ProductView` needs the column — which is the
      note already standing in `NorthwindInfoCarrierSqliteServerContext`.

- [x] **R105. The last three unexamined missing bases, re-read and classified.** `test/` only.
      `failed` unchanged at 143, `total` unchanged at 29384, FIXED none, BROKEN none, REASONS
      unchanged. **Missing bases 5 -> 2**, and the two left are the D3-blocked pair.

      **All three were reported as missing with no recorded reason, which is the one state this
      gate exists to make impossible.** Each is now listed with a reason that was checked against
      EF 10 rather than carried over. All three fall in the list's FIRST category: the client is not
      relational.

      - **`JsonUpdateTestBase` — 136 of 136.** Every test runs through
        `ExecuteWithStrategyInTransactionAsync`, and the `UseTransaction` handed to it is declared
        on the test base as `public void ... => facade.UseTransaction(transaction.GetDbTransaction())`.
        **Non-virtual**, so no fixture can substitute `UseInfoCarrierTransaction`, and
        `GetDbTransaction()` needs a relational client. ADR-013's own worked example, re-counted
        rather than assumed: still 136, still non-virtual.
      - **`StoreValueGenerationTestBase`.** `StoreValueGenerationFixtureBase.OnModelCreating` opens
        with `context.GetService<ISqlGenerationHelper>()` and builds every computed column from it.
        That service is `EFCore.Relational`'s, and this harness runs a fixture's `OnModelCreating`
        on **both** sides, so the client throws before a test runs. Blocked by D3, exactly as
        `SqlQuery<T>` is.
      - **`AdHocQuerySplittingQueryTestBase` — and the reason on record was wrong.** ADR-013's R77
        amendment said it "calls `CloseConnection()` on the cast store", which is true of **one test
        of ten** and under R14's rule would cost a test rather than the base. The real blocker is
        the base's required surface: its two abstract members, `SetQuerySplittingBehavior` and
        `ClearQuerySplittingBehavior`, are implemented by every provider as configuration of a
        `RelationalOptionsExtension` on the **client's** options builder — EF's SQLite class writes
        a private field by reflection to do the second. A remoting client has no such extension. Its
        subject is moot here besides: `SplitHintStrippingVisitor` removes `AsSplitQuery` on purpose.

      **Which is the third standing classification found wrong in this session** — R97's, R98's and
      now R77's. `CLAUDE.md` already says a classification is not evidence and age is not evidence;
      three for three is the strongest form of that this repository has recorded.

- [x] **R106. `UdfDbFunction`'s 55 recorded as future scope rather than ignored.** Documents only —
      `roadmap.md`, the deferred table. Owner's decision, 2026-09-02.

      **R103 established the new fact and deliberately did not act on it.** EF's own
      `SqliteComplianceTest` ignores `UdfDbFunctionTestBase`, so the reference provider does not run
      that base on the only relational store this suite has. The 55 reds R90 classified have
      therefore never been judged against a store that hosts the base at all, and every reading of
      them so far has been against one that does not.

      **Not added to `IgnoredTestBases`, and that is the decision.** The class stays adopted and the
      reds stay visible. Dropping it would lower `failed` by 55 while answering nothing, which is
      the failure mode `CLAUDE.md` names outright — and R86 already went further than EF's SQLite
      suite here, defining the store's scalar functions on every connection for 14 tests, so this is
      not a base this repository has nothing to say about.

      **What would answer it is another tier.** Relational or not: the deferred table's
      non-relational backend entry is the same question from the other end, and neither is
      committed.

- [x] **R107. Server detailed errors for every fixture: measured at 211 and rejected.** Nothing
      executable changed — this entry and one comment at the site are the record.

      **The obvious generalization of R104, and it is a trap.** R104 turned detailed errors on for
      the Northwind SQLite fixture and gained 2. `InfoCarrierBackendTestStore.AddProviderOptions`
      already applies `EnableSensitiveDataLogging()` to **every** server context, so
      `EnableDetailedErrors()` beside it is one line and reaches all 77 fixtures without touching
      any of them.

      **It costs 68 tests.** `failed` 143 -> **211**, every new one in
      `JsonQuerySqliteInfoCarrierTest` and every one an `Assert.Equal() Failure: Values differ`
      inside `JsonQueryFixtureBase.AssertPrimitiveCollection` — `Expected: 3, Actual: 0`. Detailed
      errors change the shape of EF's column read, and the JSON primitive collections then
      materialize as defaults. The mechanism was not chased further: the number settles the
      question.

      **So the switch stays per fixture**, and the site now says so, because the next reader will
      have the same idea. **A one-line generalization of a two-test win is exactly the shape that
      does not get measured**, and this one is negative by a factor of thirty.

- [x] **R108. The third-tier question filed as an issue, and the roadmap row points at it.**
      Documents only — `roadmap.md`, the deferred table row R106 added. Owner approved the text.

      **The row said what would answer the question and named nowhere to answer it.** R106 recorded
      `UdfDbFunction`'s 55 reds as a tier question rather than a gap to ignore, and left the tier
      itself as prose in a table this repository closes rather than tracks. Filed as
      [#96](https://github.com/azabluda/InfoCarrier.Core/issues/96), "Add a third test tier for the
      bases SQLite cannot host", which states the two-tier position, `UdfDbFunctionTestBase` as the
      worked example, and that the tier may be relational or not. Nothing is decided there and no
      design exists.

      The deferred table's non-relational backend entry stays where it is: it asks the same question
      from the other end and neither entry is committed.

- [x] **R109. The 68 JSON failures are EF Core's, on the server, before the wire.** One comment
      rewritten at the site and this entry. Nothing executable changed.

      **R107 left the mechanism unchased and said so.** It measured `failed` 143 -> 211 for one
      line and stopped at the number, recording "detailed errors change how EF reads a column ...
      whatever the mechanism". The owner asked for the mechanism, because a value that changes when
      only an error *message* was supposed to change is a defect wearing the clothes of a test
      setting.

      **It is one property.** `JsonOwnedAllTypes.TestInt16Collection`, declared
      `IReadOnlyList<short>`. With `EnableDetailedErrors()` on it materializes with **zero**
      elements; with it off it holds its three. `JsonQueryFixtureBase.AssertAllTypes` walks its
      collections in declaration order and reaches this one twelfth, before anything else that
      could differ, so all 68 failures report the same `Expected: 3, Actual: 0` and all 68 are the
      same defect. Every other collection on the same object -- `string[]`,
      `ReadOnlyCollection<string>`, `int[]`, `List<long>`, the converted enums -- reads correctly
      in the same materialization.

      **Measured on the server context, where this provider is absent.**
      `Backend.CreateDbContext()` is a plain EF Core SQLite context; reading
      `JsonEntityAllTypes` through it returns `TestInt16Collection` empty before any request
      crosses the wire, and the client afterwards reports exactly what the server sent. Toggling
      the option is the only difference between the two runs: 0 with it, 3 without it, on both
      sides. **So this is not the projection split, not the wire format and not the client's
      model** -- three explanations the shape of the failure would have supported.

      **What EF does differently is a `TryCatch`.**
      `RelationalShapedQueryCompilingExpressionVisitor.ShaperProcessingExpressionVisitor
      .CreateReadJsonPropertyValueExpression` is the only place the option touches a JSON read,
      and all it does is wrap the read in one, to call `ThrowExtractJsonPropertyException`. The
      reader threaded through those reads is `Utf8JsonReaderManager`, a **`ref struct` passed by
      reference**, which is the kind of argument an expression-tree `TryExpression` forces the
      compiler to treat specially. **A minimal model does not reproduce it**: a `ToJson()` owned
      type carrying `List<int>`, `List<string>` and an `IReadOnlyList<short>` reads all three
      correctly with the option on, with and without a property initializer. So the trigger needs
      more of EF's model than the property type alone, and finding it is EF's work, not this
      repository's.

      **Nothing to fix here, and the option stays per fixture.** R104's two-test win on one
      Northwind fixture is unaffected. The site's comment now names the property, says where the
      value is lost, and says the loss is EF Core's -- which is what the next reader with R107's
      idea needs to see.

- [x] **R110. `Database.SqlQuery<T>`: options C and D evaluated, and D3 does not have to be
      reversed.** Documents only — `architecture.md` §6a **D8 item 2**, a new dated subsection.
      Nothing executable changed and no decision is taken; the owner's is still open.

      **Option C — name the relational types by string — cannot work, and the reason is a language
      rule.** `GetFacadeDependencies` asks `dependencies is IRelationalDatabaseFacadeDependencies`.
      That is a CLR type test, answered by the runtime type's interface table. The two sites in this
      repository that already name relational things by string —
      `QuerySplitter.RelationalQueryableExtensionsFullName` and `RelationalQueryRootShape` — both
      answer *what is this node called*, which a name can settle. *Does this object implement this
      interface* is not that question. **Read, not measured.**

      **Option D — a replacement registered from outside `InfoCarrier.Core` — works, and it was
      measured.** `DatabaseFacade.Dependencies` is
      `field ??= context.GetService<IDatabaseFacadeDependencies>()`, so the registration is
      replaceable. A probe registered an `IRelationalDatabaseFacadeDependencies` through
      `ReplaceService` on an ordinary InfoCarrier client: the facade resolved it and the type test
      passed. The three relational-only members threw from that class and **every call still
      completed**, because `SqlQueryRaw` reads only `QueryProvider`, `TypeMappingSource` and
      `AdHocMapper`, all on the core interface. **D3 stands as written.**

      **Two shapes, and D2 is the better product.** D1 has the application write the class — the
      same seam as R85's `AddInfoCarrierAllowedTypes` and R95's
      `AddInfoCarrierArbitrarySqlExecution` — and is what the probe used. D2 ships the same class in
      an optional `InfoCarrier.Core.Relational` package, so the reference is opt-in at the NuGet
      level rather than at the DI level. Neither is started.

      **Past the type test, both roots were measured and one of them moved.** The scalar case is the
      new work R102 predicted: `SqlQueryRootExpression` *"has no wire representation"*. The
      non-scalar case gets further than R102 expected — the **client's** `AdHocMapper` builds the
      entity type without complaint and produces R95's `FromSqlQueryRootExpression`, which already
      crosses; what refuses it is this provider's **type allowlist**, answered by an ordinary R85
      registration on both sides. With the DTO admitted the query reached the **server**, which
      raised *"Entity type 'BlogRow' not found in the server model"*.

      **So R102's point 4 is confirmed and re-stated.** The gap is not getting an ad-hoc entity type
      across the wire; it is that `ServerQueryExecutor.RebindQueryRoot` resolves through the
      server's model and the server never builds the matching ad-hoc type. Smaller than "a new
      capability", and it raises the same security question the raw-SQL gate raised, because it is
      the server constructing a type on a client's say-so.

      **What changed for the owner.** The choice is no longer *reverse D3 or record two bases as out
      of scope*. There is a third option, it was measured, and it leaves the milestone exit
      criterion intact.

- [x] **R111. Retriage batch 1: four recorded reasons re-read, one wrong.** Documents only —
      `test/known-failures.txt` gains a dated entry, `architecture.md` §6a D8 item 2 has one figure
      corrected. `failed` unchanged at 143, `total` unchanged at 29384. Nothing executable changed.

      **Why this exists.** Three standing classifications were examined last session and all three
      were wrong (R97's, R98's, R77's). This batch covers **49 of the 143**, read out of
      `artifacts/measure/r105.log` message *and first stack frame*, because the message alone is
      what let R97's wrong reason stand.

      **Wrong: the 36 TPT/TPC `GearsOfWar` reds are two mechanisms, not one.** The recorded reason
      says the base refuses a correlated collection with `Distinct` and this provider answers it.
      True for 7 of the 9 methods. The other two — `Where_coalesce_with_anonymous_types` and
      `Correlated_collection_order_by_constant_null_of_non_mapped_type`, 8 tests across the two
      hierarchies — are wrapped by `GearsOfWarQueryRelationalTestBase` in `AssertTranslationFailed`,
      not in a `DistinctOnCollectionNotSupported` assertion. They assert a *client-evaluation*
      refusal, which is a different thing entirely.

      **And one of the two carries a cost the reason does not mention, measured with
      `INFOCARRIER_SERVER_SQL=1`.** `Where_coalesce_with_anonymous_types` asks for
      `where (new { … } ?? new { … }) != null select g.Nickname`, and the server ran
      `SELECT` of **nine `Gear` columns and a discriminator**, joined to `Officers`, with no `WHERE`.
      The anonymous type in the predicate puts the whole tail — predicate *and* projection — on the
      client side of the boundary. **No rows are lost and the answer is right**: the predicate is a
      tautology, so nothing could have been filtered. What is lost is W1's minimal-column payload.
      Saying "we answer what other providers refuse" and stopping there hides that.
      `Correlated_collection_order_by_constant_null_of_non_mapped_type` is benign under the same
      probe — it orders by a constant null, and its base query has no predicate to lose.

      **Held: three reasons, each checked to the stack frame.** The 7 "Relational-specific methods"
      are one message from **two** call sites and the file already separates them — 4 at
      `DbContextTransactionExtensions.GetDbTransaction` (ADR-013's shape), 3 at
      `RelationalDatabaseFacadeExtensions.GetFacadeDependencies` (D3, D8 item 2). The 4
      `Include_*_connection*` do stop at `RelationalTestStore.CloseConnection()`. The 2
      `FromSqlRaw_queryable_simple_projection_composed` do throw inside the base method's own body.
      R84 recorded all three correctly.

      **One figure corrected.** D8 item 2's "6 current failures" counts by class; by cause it is
      **3**. See the amendment.

      **The rule this batch produced, and it is the one to carry into batch 2.** *A reason written
      per test held; the one reason written per family did not.* R84's entry names fourteen tests
      and gives each its own cause — fourteen for fourteen. The 2026-08-29 entry summarises
      thirty-six at once, and the summary flattened two mechanisms into the more interesting one.

- [x] **R112. Retriage batch 2: three more reasons re-read, all three held, and batch 1's rule
      corrected.** Documents only — `test/known-failures.txt`. `failed` unchanged at 143, `total`
      unchanged at 29384. Nothing executable changed. Running total **7 reasons, 62 of the 143, one
      wrong**.

      **The 8 APPLY reds held, and holding them meant checking EF's suites.** One method in four
      classes:
      `Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where_split`.
      It is declared on `ManyToManyQueryRelationalTestBase` and its no-tracking twin as a plain
      `AssertQuery` over `.Include(… OrderBy().Skip(1).Take(2)).ThenInclude(…).AsSplitQuery()`, and
      **neither `EFCore.Sqlite.FunctionalTests` nor the relational specification assembly overrides
      it anywhere** — so on EF's own SQLite provider it passes, because the split query avoids
      `APPLY`. Here `SplitHintStrippingVisitor` removes the marker and the single query needs
      `APPLY`, which SQLite has not. **These are ours, and the recorded reason already says so in
      those words** and prices #60 from it.

      **The 3 `PrimitiveCollectionsQuerySqlite` and the 2
      `Collection_projection_before_set_operation_fails` held too.** The first names EF's own TODO
      as the cause; the second names the passing sibling that narrowed the user-facing claim to the
      *before* shape.

      **R111's rule was too crude and this batch disproves it.** It said a reason written per test
      held and the one written per family did not. All three above are family reasons and all three
      held. The division is not how many tests a reason covers:

      > **A reason that names the mechanism held. The one that named the symptom did not.**

      *"The marker never reaches the server so they run as a single query"* can be checked against
      EF's suites, and was. *"The base asserts that a correlated collection with `Distinct` must be
      refused and this provider answers it"* can only be recognised, and recognition is what put two
      mechanisms in one bucket. The three that held share a second habit worth copying: each names
      something **green** — a passing sibling, a base that overrides nothing, EF's own TODO — so the
      reason can be falsified by re-reading rather than only agreed with.

- [x] **R113. Retriage batch 3: the "client is not relational" tail, five reasons, all five held.**
      Documents only — `test/known-failures.txt`. `failed` unchanged at 143, `total` unchanged at
      29384. Nothing executable changed. Running total **12 reasons, 71 of the 143, one wrong**.

      **The 4 `UseTransaction` reds were re-verified with the compiler rather than taken on the
      note's word.** `TableSplittingTestBase.UseTransaction` and
      `EntitySplittingTestBase.UseTransaction` are both `public void … => facade.UseTransaction(
      transaction.GetDbTransaction())` — no `virtual`, exactly as recorded, and the stack traces
      land on that frame. This is the shape `CLAUDE.md` tells you to write a `UseTransaction`
      override for, and also the one where an override cannot reach.

      **The 3 `ModelBuilderGeneric…OwnedTypes`, the 1 `…ComplexType` and the 1 `DataAnnotation` all
      held**, each to the frame: a keyless shared entity type where the owned navigation's was
      expected, a `Throws` that did not throw, and `GetTableMappings` → `EnsureRelationalModel` →
      `GetRelationalModel` on a client that builds none.

      **Batch 2's rule holds and gains a corollary.** All five name a mechanism, and three name the
      **evidence** as well — `isVirtual: false`, the exact `Assert.Same` shape, the method the
      assertion calls. Each of those three was re-checked in one tool call.

      > A reason that says how it was checked is a reason the next reader can re-check cheaply.

      **What is left for batch 4.** The 55 `UdfDbFunctionInfoCarrierTest` reds — parked behind the
      third-tier question ([#96](https://github.com/azabluda/InfoCarrier.Core/issues/96)) and
      classified by shape in R74 — and about 17 singletons. One drift in the Udf group is visible
      and is deliberately **not** called a correction: R74 counted 2 failures reaching the store,
      and `r105` shows 2 `no such table` plus 2 `near "(": syntax error`. R86 changed that group on
      purpose, so re-deriving the shape counts is batch 4's work.

- [x] **R114. Option D built: the client's relational facade dependencies, registered from outside
      the package.** `test/` only — `InfoCarrierRelationalFacadeDependencies` plus one registration
      in `InfoCarrierTestStoreFactory`. **`failed` 143 → 141**, `total` unchanged at 29384,
      FIXED 2, BROKEN none. Owner's decision, 2026-09-02.

      **D3 is not reversed and does not need to be.** `DatabaseFacade` resolves its dependencies
      with `context.GetService<IDatabaseFacadeDependencies>()`, so anything outside
      `InfoCarrier.Core` can register an implementation of the relational interface and the package
      still references nothing relational. The harness is that application, exactly as it is for
      R85's `AddInfoCarrierAllowedTypes` and R95's `AddInfoCarrierArbitrarySqlExecution`.

      **The three relational members throw and nothing on this path calls them.** `SqlQueryRaw`
      reads `QueryProvider`, `TypeMappingSource` and `AdHocMapper` — all on the core interface.

      **Gated on `ArbitrarySqlExecution` rather than on a flag of its own**, because
      `Database.SqlQuery<T>` *is* arbitrary SQL execution and the two are wanted together or not at
      all.

      **The two that moved are both `SharedTypeQueryInfoCarrierTest`**, and one of them is a
      surprise: `Ad_hoc_query_for_default_shared_type_entity_type_throws` was recorded as failing
      inside `QueryFilterRewritingConvention` while building the *client's* model. It was
      downstream of the same call.

      **Read the reasons diff, not the count.** The 7 "Relational-specific methods" become 4 — the
      remaining four are ADR-013's non-virtual `UseTransaction`, a different call site (R113) — and
      2 new "No part of the query can be executed on the server" appear:
      `Multiple_occurrences_of_FromSql_with_db_parameter_adds_two_parameters` is now past the type
      test and refused one layer later by the **type allowlist** over `UnmappedCustomer`.

      **A shipped `InfoCarrier.Core.Relational` package would hold this same class**, and that is a
      packaging step rather than a design one: it costs a new `csproj`, `release.yml`'s package list
      and three user-facing pages. Not taken here.

- [x] **R115. The scalar `SqlQuery` wire node, and `NorthwindSqlQueryTestBase` adopted green.**
      `src/` and `test/`. **`failed` unchanged at 141, `total` 29384 → 29392** for the eight tests
      the new base brings, **all eight green on the first run**. FIXED none, BROKEN none, REASONS
      unchanged. Compliance missing bases **2 → 1**.

      **`SqlQueryRootStubNode` is the sibling of R95's node and had to be a node of its own.**
      `Database.SqlQuery<T>` makes EF build `SqlQueryRootExpression` when the type-mapping source
      recognises `T`, and `FromSqlQueryRootExpression` over an ad-hoc entity type when it does not.
      **The scalar root is the one query root with no entity type**, so
      `ServerQueryExecutor.RebindQueryRoot` answers it *before* the model lookup — which would
      otherwise throw "not found in the server model" on a perfectly well-formed query — and
      rebuilds it from the CLR element type the wire carried. `RelationalQueryRootShape` reads and
      rebuilds it by shape, exactly as it already does for the entity root.

      **One grant, now one method.** `RequireArbitrarySql` serves both roots.
      `security-review.md` §5a has an addendum saying why nothing above it needs re-deriving: R94's
      two measurements are properties of the store and of EF's command path, not of which extension
      method the caller typed.

      **`NorthwindSqlQueryInfoCarrierTest` takes no override from EF's SQLite class**, which adds
      only `CreateDbParameter` and a private `AssertSql`; the baselines have no meaning on a client
      that emits no SQL.

      **The trim ratchet caught the only mistake and the count could not have.** Sharing one
      `ResolveRootType(string fullName)` between the two roots turned a `const`-folded literal into
      a **parameter**, which the trim analyzer cannot read: `IL2057`, `ours` 89 → **90**, FAIL.
      Splitting the two `Type.GetType` calls apart returned it to 89. Same lesson as the
      `foreach`-not-`Select` note in the same file, from the other direction: **a suppression covers
      a member's own body, and so does the analyzer's ability to see a constant.** `eng/measure.sh`
      was green on the failing version — the two gates are separate axes, and CLAUDE.md says so.

      **`SqlQueryTestBase` is not adopted and the reason is measured.** Its 61 methods project into
      `UnmappedProduct`/`UnmappedCustomer`, so EF takes the ad-hoc entity-type path: the client
      builds the type, R95's node carries it, the allowlist refuses the DTO until an application
      admits it, and the **server** then raises "not found in the server model". Closing that means
      the server calling its own `AdHocMapper` on a client's say-so, which is a widening and gets
      its own reading first.

- [x] **R116. Retriage batch 4: the 55 `UdfDbFunction` reds.** Documents only —
      `test/known-failures.txt`. `failed` unchanged at 141, `total` unchanged at 29392. Nothing
      executable changed. Running total **13 reasons, 126 of the 141, one wrong and one figure
      stale**.

      **R74's classification holds in substance, and its checkable half checks out.** It says the
      55 are one mechanism and that **not one is a wrong answer**. Re-derived from `r115b.log`:
      29 client-boundary refusals, 10 `NotImplementedException` from EF's own stub bodies, 6 message
      differences, 4 "no part of the query", 4 store errors, 1 client-side navigation read, 1
      `System.Exception` from the test model. **No `Values differ` and no `Collections differ`
      anywhere in the group** — every failure is an exception or a message text.

      **One figure is stale, and it is the one this entry would be quoted for.** R74 wrote "only 2
      are the store". **Today it is 4**: two `no such table: GetTopTwoSellingProducts` and two
      `near "(": syntax error`, all four `InfoCarrierServerException` raised after the server
      reached SQLite. The group was 75 when R74 counted and is 55 now — R86 closed 14 — so the
      shapes moved for a recorded reason. **It strengthens R74's point**: four failures reach the
      store and the store refuses them, which is the fail-safe direction.

      **The 6 message differences are one shape and all six are `QF_*`.** Each expects EF's
      *"Unable to translate a collection subquery"* and gets this provider's *"No part of the query
      can be executed on the server"*. Both refuse the same query; what differs is **which refusal
      fires** — the whole-query one rather than the per-subtree one `RejectClientEvaluation`
      raises. **That is the largest single lead left in this group: six tests, one message.**

      **`Scalar_Function_With_Translator_Translates_Instance` reads like a defect and is not one.**
      Its `System.Exception` comes from `UDFSqlContext.MyCustomLengthInstance`, the test model's own
      throwing stub, and the frames below are `lambda_method…` and `ListSelectIterator` — the call
      was evaluated on the client, which is what R74's funcletizer family describes. The stub is
      the evidence, not the failure.

      The group stays red and stays adopted (R106,
      [#96](https://github.com/azabluda/InfoCarrier.Core/issues/96)).

- [x] **R117. The refusal message prints the expression EF's way, and retriage batch 5 closes the
      141.** `src/` — one line in `QuerySplitter.RejectClientEvaluation` — plus
      `test/known-failures.txt`. **`failed` unchanged at 141**, `total` unchanged at 29392, FIXED
      none, BROKEN none. Both gates ran; trim ratchet `ours` 89 ≤ 89.

      **The count cannot see this and the reasons diff is the whole result.**

      ```
      -  29 The LINQ expression '[Microsoft.EntityFrameworkCore.Query.EntityQueryRootExpression].
      +  23 The LINQ expression 'DbSet<Customer>()
      +   3 The LINQ expression 'DbSet<Product>()
      +   2 The LINQ expression 'DbSet<Order>()
      +   1 The LINQ expression 'DbSet<Address>()
      ```

      `RejectClientEvaluation` rendered the offending expression with `Expression.ToString()`, which
      has no case for an extension node and prints its **type name in brackets**. EF renders every
      one of these messages with `ExpressionPrinter.Print`. **The wording was already EF's; the
      expression inside it was not**, and the comment above the throw claimed the whole message was
      EF's — half right for five milestones.

      **No test flipped, and that was checked before the change rather than after.** Five failures
      assert on this text, and each has a second difference behind the rendering: EF names a
      different operator (R67, R68), or renders a rewritten tree that ADR-006's raw capture cannot
      reproduce (J18). Those readings were right and still stand. What changes is what a consumer
      sees.

      **Batch 5 re-reads the last 20, so all 141 are now re-read.** Running total across five
      batches: **141 of 141, one reason wrong (batch 1's) and one figure stale (batch 4's)**.
      Everything else held — R84's, R29's, J22's, C64's, the topology pair, C20's, R68's, J18's,
      J15's, and the compliance test, whose missing list is now **1**.

      **The best entry in the file to copy is J15's**, found again in this pass:
      `Composition_over_collection_of_complex_mapped_as_scalar` asserts a throw over a table EF
      leaves **empty**, so the "no exception was thrown" everyone read was a **vacuous control**.
      J15 wrote a test that asserts the answer instead, and it passes.

      **Nothing in the 141 is of unknown standing, and that is now a re-derived statement rather
      than an inherited one.** The last whole-tail re-derivation was M9's archive, at thirteen.

- [x] **R118. `InfoCarrier.Core.Relational` measured and filed, not built.** Documents only —
      `architecture.md` §6a **D3 amendment**, `roadmap.md`'s deferred table, and
      [#97](https://github.com/azabluda/InfoCarrier.Core/issues/97). Owner's idea, 2026-09-02.
      Nothing executable changed and nothing is decided.

      **What J5 actually did.** It removed the `EFCore.Relational` reference and paid for it in
      string literals. Counted: **9 magic `Relational:` annotation strings** in four product files,
      pinned by a **268-line** `DocumentMappingPinTest`; `RelationalQueryRootShape.cs` at **276
      lines and 10 trim suppressions**, resolving two EF expression types by name;
      `InfoCarrierHierarchyMappingConvention.cs` at **131 lines**, a deliberately narrower
      hand-written copy of EF's `EntityTypeHierarchyMappingConvention`; and the 149-line document
      mapping seam. **Every one is a fact computed twice by two providers** — the hazard `CLAUDE.md`
      opens with. J5 did not remove it; it changed its shape from a reference to a string.

      **The constraint that decides the design, read from EF's source.**
      `EntityFrameworkRelationalServicesBuilder.TryAddCoreServices()` is **all-or-nothing and
      collides with ADR-006**: it registers `IDatabase` → `RelationalDatabase`, and this provider
      captures at `IDatabase.CompileQuery`. It also takes `IDbContextTransactionManager`,
      `IDatabaseCreator`, `ITypeMappingSource`, `IAdHocMapper` and the whole SQL-generation stack.
      **So "make the client relational" cannot mean calling that builder**, and EF publishes no seam
      that splits its metadata half from its command half.

      **Three levels, and only the third is risky.** Level 1 references the package and answers
      today's strings behind seams — most of the deletion, almost no risk. Level 2 registers EF's
      relational conventions on the client model. **Level 3 (`GetRelationalModel`) needs an
      `IRelationalTypeMappingSource` the client cannot honestly have: that is B4, and it is why D3
      was drawn where it is.**

      **Worth about 14 of the 141, and about 8 that it must not touch.** Plausible: 3
      `ModelBuilderGeneric…OwnedTypes`, 1 `Table_can_configure_TPT_with_Owned`, 2
      `FromSqlRaw_queryable_simple_projection_composed`, 8 `_split` reds that
      `SplitHintStrippingVisitor` creates. Not: the 4 `GetDbTransaction` and the 4
      `Include_*_connection*`, which want a real connection on the client.

      **Testing needs no re-parenting, and that was checked rather than assumed.** 52 test files and
      117 declaration sites already sit on relational spec bases (ADR-013), and EF's own relational
      bases derive from the core ones and override — the layering this repository copied. The
      per-fixture switch exists too: `relationalClientStore`, used by 8 fixtures. **Tier A must not
      get it**, because InMemory is not relational and a relational client over it would recreate the
      disagreement the change exists to remove. The same bases running both ways *is* the
      measurement.

      **D3 is not reversed and the amendment says so twice.** The reference lives outside
      `InfoCarrier.Core` under every level, so a non-relational backend — the other end of #96 —
      stays possible.

- [x] **R119. The public-API gate moves onto the pull request.** `.github/workflows/build.yml` and
      `CLAUDE.md`; no product or test code. The *fast-gate* job now runs
      `dotnet pack InfoCarrier.Core.slnx --no-build --configuration Release --output artifacts/pack`
      straight after its Release build, and publishes nothing.

      **Why.** `dotnet pack` is what runs package validation — `Directory.Build.props` sets
      `EnablePackageValidation` against the published `10.0.0` baseline — and until now the
      `Packages` workflow was the only job that packed. That workflow runs on `main` alone, so a
      binary break was invisible until it had already merged. #91 merged green and turned `main`
      red with six `CP0002` breaks, every one an optional parameter added to a public member:
      source-compatible and binary breaking, because the compiler emits one member and the old
      arity leaves the assembly. `UseInfoCarrier` was among them. Commit `ea2f560` restored the six
      as explicit `[EditorBrowsable(Never)]` overloads rather than suppressing them, and the two
      `UseInfoCarrier` shims needed the same `RequiresUnreferencedCode` / `RequiresDynamicCode`
      attributes as the members they forward to — without those the trim ratchet went 89 → 91.

      **Cost.** Seconds, on a Release build that has already happened. No path filtering was added
      because `build.yml` has none to follow: it already runs its build and its 90-minute spec
      ratchet on every push and pull request, documentation-only ones included. `docs.yml` and
      `packages.yml` are the workflows that filter, and neither is touched.

      **Left for the next `src/` commit, deliberately.** Five XML doc comments in
      `src/InfoCarrier.Core` — on `ExpressionSerializer.CreateForModel`, `UseInfoCarrier`,
      `QuerySplitter`, `ServerBoundaryAnalyzer` and `ServerQueryExecutor`, one per binary-
      compatibility overload — still end "the `Packages` workflow is the only job that checks it —
      it runs on `main` alone". That sentence is now false. It is not corrected here because
      `CLAUDE.md`'s gate table asks for a full suite run and a trim publish for any change under
      `src/`, and R120 widens all five of those files anyway.

      No gate to run: nothing under `src/` or `test/` changed.

- [x] **R120. `InfoCarrier.Core.Relational` ships, and the seam it needs has exactly one reader.**
      Level 1 of #97's three. `src/` and `test/` change, so both gates ran, and a public signature
      changes, so `dotnet pack` ran too.

      **What is built.** A third shipped package, `src/InfoCarrier.Core.Relational`. It holds
      `InfoCarrierRelationalQueryRoots`, which names `FromSqlQueryRootExpression` and
      `SqlQueryRootExpression` outright, and `InfoCarrierRelationalFacadeDependencies`, moved out
      of the test project. `InfoCarrier.Core` gains
      `Metadata.IInfoCarrierRelationalQueryRoots` with a no-op default that refuses rather than
      drops. **`RelationalQueryRootShape.cs` is deleted: 276 lines and 10 trim suppressions.**
      `architecture.md` §6a carries the **D3 amendment 2026-09-02 (R120)**, and **D3 as written is
      untouched** — the relational reference lives outside `InfoCarrier.Core`.

      **THE FINDING, and it cost a wrong answer rather than a red test.** The prototype gave one
      fact two carriers. `ServerBoundaryAnalyzer` read the seam from the **options**, which it must:
      `ExtensionInfo.GetServiceProviderHashCode()` is `0` and `ShouldUseSameServiceProvider` is true
      for every InfoCarrier options shape, so every client context in a process shares one internal
      service provider and anything per-context has to travel on the options.
      `ExpressionToNodeTranslator` read it from **DI**, because it is DI-scoped. A client that set
      the option but not the service therefore **admitted** a raw-SQL root at the boundary and then
      **dropped its SQL** in the translator, and the whole table came back — the defect R75 closed.
      `SqliteSmokeTest.FromSql_arguments_cross_as_values_and_are_bound_rather_than_interpolated`
      answered **2 where 1 is correct**.

      **The fix is one reader.** `InfoCarrierOptionsExtension.RelationalQueryRootsFor(context)` is
      it. `QueryExecutor` calls it once per execution and hands the same object to `QuerySplitter`
      (hence to the analyzer) and to `ExpressionSerializer.ToNode`. The translator takes it per
      translation and scopes it as it scopes parameter identity: set at depth 0, untouched in the
      recursion; the constructor parameter the prototype had is gone. `ExpressionSerializer
      .CreateForModel` is back to its 10.0.0 shape — the server never forward-translates a query
      root, so it never needed the seam, and one of the prototype's two compatibility overloads went
      with it.

      **A second half of the same mistake, found by running the tests.** The prototype resolved the
      server's implementation from the server `DbContext`'s services. A server context builds its
      own internal service provider and never sees the application's collection, so that answered
      "nothing is relational here" for a server that had registered a real one, and
      `A_FromSql_query_crosses_once_both_sides_grant_it` failed on the rebuild instead of the
      translation. `InProcessInfoCarrierServer` now resolves it from the application's provider and
      passes it to `ServerQueryExecutor`, beside the value mappers, the allowed types and the
      raw-SQL grant, which is where the other three already came from.

      **All 25 `SqliteSmokeTest` tests pass**, including the three the prototype left red.

      **Three registration entry points, and it is not tidiness.** `AddInfoCarrierRelational()` on
      both halves; `AddInfoCarrierRelationalClient()` on the **client only**, because it also
      replaces `IDatabaseFacadeDependencies` and a relational server already has EF's own over a
      live connection; and `InfoCarrierDbContextOptionsBuilder.UseRelationalQueryRoots(...)`,
      because most clients never build an `IServiceCollection` — `SqliteSmokeTest` is exactly that
      shape.

      **Gates.** `eng/measure.sh r120 r119-check`: `Passed: 29013, Failed: 141, Total: 29392,
      Skipped: 238`, **FIXED none, BROKEN none, REASONS unchanged** — the same 141 as `main`, which
      is the answer this change should give, since the three smoke tests were red only on the
      prototype. `dotnet pack` clean on all three packages, so the widened `QuerySplitter`,
      `ServerBoundaryAnalyzer` and `ServerQueryExecutor` constructors and the new
      `ExpressionSerializer.ToNode` overload are binary-compatible with 10.0.0.
      `CI=true dotnet build --configuration Release`: `5 Warning(s), 0 Error(s)`.

      **`eng/trim-ratchet.sh`: `ours` 89, `total` 855, and the baseline does NOT move.** The
      expectation that removing 276 lines of reflection would lower it was wrong, and the reason is
      worth carrying: **the ten attributes on that file were `UnconditionalSuppressMessage`, so
      those diagnostics were never in the 89.** Deleting suppressed reflection cannot lower a count
      that excluded it. Two measurements say so directly, 89 with the file and 89 without, and the
      publish log carries **no `InfoCarrier.Core.Relational` diagnostic at all** — verified, not
      assumed. `eng/trim-baseline.txt` records this so the next reader does not go looking for the
      win here: what went away is ten written arguments a trimmer could not check, replaced by two
      type names a compiler does.

      **Two costs recorded rather than rediscovered.** `EnablePackageValidation` is on for every
      packable project, and a brand-new package has no baseline to fetch: restore fails `NU1101`
      hunting an `InfoCarrier.Core.Relational 10.0.0` that cannot exist, so the new csproj turns it
      off with a comment saying to turn it on after the first release. And `release.yml` pushes by
      **exact filename, not a glob** — it now names the third package, in three places, or a
      release would have shipped nothing of it.

      Level 2 (EF's relational conventions on the client model) and level 3 (a relational model on
      the client) are untouched and stay open. `roadmap.md`'s deferred row is updated to say level 1
      is built.

      **One deviation, stated rather than hidden.** Five XML doc comments R119 left stale, on the
      binary-compatibility overloads, were corrected after `measure.sh` had already built. They are
      doc comments: no IL changes, and the trim publish and the pack both ran after them.

- [x] **R121. The three measurement tools take several test projects, against one baseline.**
      `eng/` only: `measure.sh`, `ratchet.sh`, `trx-failures.py`, plus the table rows in
      `CLAUDE.md`. **No product or test code, and the behaviour of every existing call is
      unchanged.** Written before the split it exists for, so the split can be measured the day it
      lands rather than after.

      **What changed.** `ratchet.sh` now reads `<results.trx> [more.trx ...] <baseline-file>` — the
      LAST argument is the baseline — and sums the `<Counters>` of each TRX while unioning the
      failing names into one sorted list. `trx-failures.py` takes any number of TRX. `measure.sh`
      holds a `projects` array, runs `dotnet test` once per entry, adds the figures read out of
      each run's own summary block, and concatenates the per-project logs for the name and reason
      extraction. Today that array holds one project, so every number is identical.

      **ONE BASELINE, and that is the decision rather than a detail.** `test/known-failures.txt`
      and `known-failures.names.txt` stay one pair covering the whole suite. With a pair per
      project, a test that MOVES between projects reads as a fix in one and a break in the other,
      and the name diff that makes "fixed 4, broke 4" fail the gate stops working across the
      boundary — which is exactly the boundary the split is about to create.

      **Summing is the one arithmetic allowed on these counts, and the scripts say so.** Every
      figure is still read out of a run's own summary or `<Counters>` element; what is added is the
      same figure from a second run. `passed` is still never derived from `total` and `failed`.

      **Verified directly, because these scripts ARE the gates and a broken one is invisible.**
      `ratchet.sh` against a real TRX and a baseline matching it: `Passed: 27474, Failed: 99,
      Total: 27809`, FIXED none, BROKEN none, exit 0 — the single-TRX path unchanged. The same TRX
      named twice: `Passed: 54948, Failed: 198, Total: 55618`, both counters exactly doubled, the
      name list unioned to the same 99 names, and the failure gate fires. `trx-failures.py` on one
      TRX and on the same TRX twice both print 99 names. `measure.sh` was pointed at a fast project
      listed twice and reported two per-project lines and `TOTAL: 38` for a 19-test project.

      `build.yml` is untouched: its call is two arguments, the last of which is the baseline, which
      is what the new parser reads.

- [x] **R122. The spec suite splits by backing store, and the compliance gate splits with it.**
      `test/` and `eng/` and the workflows; no product code, so `eng/measure.sh` is the gate and the
      trim ratchet has nothing to say.

      **Three test projects where there was one**, EF Core's own layout:
      `test/InfoCarrier.Core.FunctionalTests` is ADR-009 Tier A over InMemory,
      `test/InfoCarrier.Core.Relational.FunctionalTests` is Tier B over SQLite, and
      `test/InfoCarrier.Core.TestUtilities` is the store-neutral harness they share. **Tier A
      references neither `EFCore.Relational.Specification.Tests` nor `InfoCarrier.Core.Relational`,
      and neither does the shared harness**, because a reference is transitive. That is the whole
      purpose: a relational client over an InMemory backend is the disagreement the seam exists to
      prevent, and it was a per-fixture flag before, which asks politely.

      **The seam the split forced, and it is the interesting part.** The shared harness could not
      keep naming relational types, so the backend-store delegate and the `relationalClientStore`
      bool became one `InfoCarrierTier` object. A tier answers four questions for itself: which
      backend store, which client shell, which client services, which logger factory. Everything
      relational is overridden in `SqliteInfoCarrierTier`, in the Tier B assembly;
      `InMemoryInfoCarrierTier` overrides one member and takes the store-neutral defaults for the
      rest, which is the honest measure of how much of the harness was ever store-specific. The
      same shape appears twice more: `InfoCarrierBackendTestStore.AddStoreSpecificServices` (the
      server's `AddInfoCarrierRelational`) and `CreateStoreConnection`, which used to return a bare
      `SqliteConnection` from the store-neutral base and now throws there.

      **Two things Tier A was carrying that it did not need, both found by the compiler rather than
      by reading.**

      - **Seven Tier A fixtures implemented `ITestSqlLoggerFactory`**, purely to satisfy
        `RelationalComplianceTestBase`'s second assertion. Tier A is now checked by the plain
        `ComplianceTestBase`, which does not ask, and what the property returned was the *client's*
        log — on a client with no database, empty.
      - **`SpatialQueryInfoCarrierTest` sat on `SpatialQueryRelationalTestBase`, which declares no
        tests.** Read before it was decided, as the handoff required: fourteen lines whose whole
        contribution is a `RelationalQueryAsserter` that calls `TestSqlLoggerFactory.OutputSql()`
        when an assertion fails, on a client that emits no SQL. It now sits on the core
        `SpatialQueryTestBase` and **loses nothing**, because the base declared nothing to lose. It
        is listed in Tier B's `IgnoredTestBases` with that measurement: adopting it there would
        need SpatiaLite for a base that declares no tests.

      **`ModelBuilding` and `DocumentMappingPinTest` moved to Tier B**, which the compiler decided:
      the first derives from `RelationalModelBuilderTest` and a stack of `Relational…TestBase`, and
      the second calls `UseSqlite`.

      **TWO COMPLIANCE TESTS, AND BOTH ARE WHERE THEY SHOULD BE.** `InfoCarrierComplianceTest`
      scans the core specification assembly against Tier A; `RelationalInfoCarrierComplianceTest`
      overrides `GetBaseTestClasses()` to scan the relational one against Tier B, so the two do not
      both claim the core bases — without that override Tier B would report a hundred bases missing
      that are not missing at all. **Tier A's missing list is 0 and Tier B's is 1**
      (`SqlQueryTestBase`), which is the same 1 the single test reported before the split.

      **Tier A's `IgnoredTestBases` was generated from the test's own output**, not written by
      hand: 108 core bases adopted on Tier B, because InMemory cannot host them and the tier that
      translates is where they run (ADR-009). One reason covers the list and it is a true one; the
      per-base reasoning is in the archive. An entry is a claim that a subclass exists in the
      sibling assembly, and the pair of tests is what checks it. The old list, every entry of it,
      was relational and moved to Tier B — the compiler established that by refusing to resolve a
      single one of them in Tier A.

      **Gates.** `eng/measure.sh r122 r120`: **`Passed: 29014, Failed: 141, Total: 29393`**, read
      as `failing 2 of 9151` on Tier A plus `failing 139 of 20242` on Tier B. **`failed` does not
      move.** FIXED one and BROKEN one, and they are the same failure renamed:
      `InfoCarrierComplianceTest.All_test_bases_must_be_implemented` became
      `RelationalInfoCarrierComplianceTest.All_test_bases_must_be_implemented`. REASONS unchanged.
      `CI=true dotnet build --configuration Release`: `5 Warning(s), 0 Error(s)`.

      **`total` rises 29392 → 29393, a deliberate rise of one, and the cause is arithmetic rather
      than behaviour.** The compliance gate is two classes now, so
      `All_test_bases_must_be_implemented` exists twice while
      `All_query_test_fixtures_must_implement_ITestSqlLoggerFactory` still exists once. Two tests
      became three. `test/known-failures.txt` carries the note and
      `test/known-failures.names.txt` carries the rename.

      **`build.yml` runs both projects into one results directory and hands both TRX to the
      ratchet**, which R121 taught to aggregate. The fast gate gains `SqliteSmokeTest`, which moved
      to the other project and is the only place the raw-SQL seam runs end to end.

      **One thing the Release build caught that a Debug build would not have.** The culture pin is a
      `[ModuleInitializer]`, and moving it into the shared library made `CA2255` an error under
      `CI=true`: *"only intended to be used in application code"*. **The analyzer is right and the
      hazard is real** — a library's module initializer runs when that library is first loaded,
      which is not guaranteed to precede the test threads of the assembly that referenced it, and
      the whole point of the pin is that it runs before any of them. `InvariantCulture.Pin()` is now
      an ordinary method in the shared harness and each test assembly carries its own three-line
      `[ModuleInitializer]` that calls it. Re-measured after that change: `FAILING: 141 TOTAL:
      29393`, FIXED none, BROKEN none, REASONS unchanged, so the pin still does what it did.

      **Both compliance tests are green in the sense that matters**: Tier A's passes outright, and
      Tier B's carries the one missing base it is supposed to carry. The end state the split was for
      — two compliance tests, both meaningful, neither able to hide the other's gap — exists.

- [x] **R123. Level 2 scoped and filed, NOT built, and it is blocked on one decision.** Documents
      only — `architecture.md` §6a **D3 amendment 2026-09-02 (R123)**. **Nothing executable
      changed.** No gate to run.

      **What was established, and it is the half that costs time.** EF's own
      `EntityTypeHierarchyMappingConvention` **runs on a client model unchanged**: it takes
      `RelationalConventionSetBuilderDependencies` and never touches it, and `GetTableName()` needs
      no relational service either — `GetDefaultTableName` reads model metadata and
      `GetMaxIdentifierLength()` and nothing else. All three read out of EF's source. So the
      131-line hand-written copy really can go, and the dependency object it would need can throw on
      both its members, exactly as `InfoCarrierRelationalFacadeDependencies` does and for the reason
      ADR-013 records.

      **The blocker is not the conventions. It is which client gets them, and it is R120's finding
      in its model-building form.** A convention set cannot read a context's options —
      `ProviderConventionSetBuilderDependencies` exposes `ContextType`, a `Type`, and no
      `ICurrentDbContext` — so the seam must be registered in DI, and DI is one answer for every
      client context in the process. **Worse, the model is shared too**: this provider registers no
      `IModelCacheKeyFactory`, so EF's default keys the model on the context CLR type, and a
      per-context answer could not be honoured even if the seam could carry one.

      **Two ways forward and they are the owner's to choose**: level 2 is a process-wide statement
      (`AddInfoCarrierRelational()` means "every client here is relational"), or
      `IModelCacheKeyFactory` is replaced so the options participate. Starting with the first and
      needing the second later is a public-API change and a model-cache change at once, so it is
      not a thing to discover halfway.

      **And the first measurement of level 2 is not a convention.** The relational client services
      are registered today only where a fixture asked for raw SQL
      (`InfoCarrierTestStoreFactory.AddProviderServices`, gated on `ArbitrarySqlExecution`). Level 2
      wants them for every Tier B fixture, which is a large blast radius and should be measured on
      its own first.

- [x] **R124. `SqlQueryTestBase` adopted — the last specification base with no subclass anywhere,
      and Tier B's missing list goes to 0.** `test/` only, so `eng/measure.sh` is the whole gate.
      **`failed` 141 -> 242, `total` 29393 -> 29512**, both deliberate and both noted in
      `test/known-failures.txt`. FIXED 1
      (`RelationalInfoCarrierComplianceTest.All_test_bases_must_be_implemented`), BROKEN 102, every
      one of them in the new class. Read as `failing 2 of 9151` on Tier A -- unchanged -- plus
      `failing 240 of 20361` on Tier B.

      **The compliance gate is now green on both tiers.** Tier A's missing list was already 0; Tier
      B's was 1 and is 0. That pair of tests, and not a list in `CLAUDE.md`, is the answer to "which
      bases are in", and the answer is now "all of them".

      **ADR-013's three-way test was applied before a line was written, and it passes.** The base's
      1301 lines contain no `UseTransaction`, no `GetDbTransaction()` and no
      `ExecuteWithStrategyInTransactionAsync`; its one abstract member is `CreateDbParameter`, and
      every route to a context runs through `Fixture.CreateContext()`. Nothing in it requires the
      client to be relational. The fixture already carried `arbitrarySqlExecution: true`, so the
      grant needed no change.

      **The four `Bad_data_error_handling_invalid_cast*` overrides are EF's own**, taken from
      `SqlQuerySqliteTest`: SQLite is dynamically typed, so there is no invalid cast to make. EF's
      remaining overrides are `AssertSql` baselines, which have no meaning on this side of the wire,
      so those tests are left to the base exactly as `FromSqlQueryInfoCarrierTest` leaves them.

      **The harness gains one seam, and it is the product's own.** `SharedTestStoreProperties`
      gains `AllowedTypes`, wired to `AllowTypes` on the client and `AddInfoCarrierAllowedTypes` on
      the server, and `NorthwindQueryInfoCarrierSqliteFixture` declares the four `Unmapped*`
      projection types. **Not gated on `ArbitrarySqlExecution`, unlike the store's parameter type**:
      a `DbParameter` can only appear in a raw-SQL payload, and a projection DTO cannot, so gating
      it would state a dependency that is not there.

      **THE MEASUREMENT THAT MATTERS IS THE ONE THAT MOVED NO COUNT.** With the base adopted and
      nothing declared, the class ran 17 of 119 and 90 of the 102 reds were
      `not on the type allowlist`. With the four types declared, the class ran **17 of 119 again**
      -- the identical count -- and every one of those 90 had moved to
      `Entity type '...' not found in the server model`. A count that did not move and a reasons
      diff that did: this is the case `measure.sh`'s third level exists for, and reading the count
      alone would have said the declaration did nothing. It did the whole thing.

      **The control is in the same run.** `SqlQueryRaw_queryable_simple_mapped_type` passed both
      times, because `CustomerQuery` **is** in the model. The allowlist was never wrong; an ad-hoc
      entity type simply is not something a model implies.

      **All 102 are classified in `test/known-failures.txt`** -- 90 the server-model gap R125
      closes, 10 the message-text class `FromSqlQueryTestBase` already carries, 2 the
      `RelationalTypeMapping` cast ADR-013 records. None is of unknown standing.

- [x] **R125. The server builds the ad-hoc entity type a raw-SQL root names, instead of reporting
      it missing from a model it was never going to be in.** `src/` change, so both
      `eng/measure.sh` and `eng/trim-ratchet.sh`. **`failed` 242 -> 222**, `total` unchanged at
      29512. FIXED 20, BROKEN none. Trim ratchet `ours` 89 <= 89, `total` 855, unchanged.
      `CI=true dotnet build --configuration Release` reports the expected `5 Warning(s),
      0 Error(s)`. No public signature moves -- `RebindQueryRoot` is private -- so no pack gate.

      **This repository predicted the fix before it wrote it.** R110 recorded, in the plan, that
      *"the gap is not getting an ad-hoc entity type across the wire; it is that
      `ServerQueryExecutor.RebindQueryRoot` resolves through the server's model and the server never
      builds the matching ad-hoc type"*. That is exactly what this step does, and the prediction was
      re-derived independently from a stack trace before the entry was found again.

      **`IAdHocMapper` is EF's own public service and the client already uses it.** `Database.SqlQuery<T>`
      into an unmapped type is answered by an ad-hoc entity type, which never enters
      `IModel.GetEntityTypes()`; `RelationalDatabaseFacadeExtensions.SqlQuery` builds one on the
      client through this service, and the server now does the same. Nothing is invented.

      **The two gates are unchanged, and the comment in the code says why that matters.** A CLR type
      reaches `RebindQueryRoot` only if the server's own `TypeAllowlist` admitted it in
      `TypeNodeResolver`, which for a type the model does not imply means the application registered
      it with `AddInfoCarrierAllowedTypes`; and `RequireArbitrarySql` still refuses a raw-SQL root
      outright unless the server granted execution. The conjunction is *declared type* **and**
      *raw-SQL grant*, and neither half is relaxed.

      **IT CLOSED 20 OF THE 90, NOT 90, AND THAT IS THE USEFUL PART.** The other 70 moved to a
      second, distinct defect on the RETURN path: `Type '...UnmappedCustomer' is not on the
      deserialization allowlist`, raised in `TypeNodeResolver` from `ClientResultMaterializer`. The
      client refuses to materialize rows of a type its own options declared. **R120's shape a third
      time** -- the boundary reads the declared types off the options, the materializer reads its
      allowlist off DI, and the disagreement was silent because the only registered type this suite
      had before was a `DbParameter`, which is sent and never returned. R126 closes it.

- [x] **R126. The client materializes rows of a type its own options declared, and `QueryExecutor`
      is now the one reader for both directions.** `src/` change with new public members, so
      `eng/measure.sh`, `eng/trim-ratchet.sh` **and** `dotnet pack`. **`failed` 222 -> 160**,
      `total` unchanged at 29512. FIXED 62, BROKEN none. **The reasons diff removes one whole class
      and adds nothing.** Trim ratchet `ours` 89 <= 89. Pack clean, no `CP0002`: the two new members
      are additions, not arity changes. `CI=true dotnet build --configuration Release` reports
      `5 Warning(s), 0 Error(s)`.

      **R120'S SHAPE, A THIRD TIME, AND THE THIRD TIME IS THE ONE THAT GENERALISES.** A permission
      and the knowledge it guards lived on different carriers. `AllowTypes` travels on the
      **options**, which `InfoCarrierOptionsExtension.AllowedTypesFor` requires be read per
      execution and never captured, because `CompileQuery`'s result is cached across every context
      of one options shape. The result materializer resolves through a **DI-scoped**
      `TypeNodeResolver` whose allowlist is `TypeAllowlist.ForModel(model)` and knows only what the
      model implies. So the boundary admitted a declared type and the materializer refused the rows
      that came back.

      **It was silent for a reason worth naming: the only registered type this suite had before was
      a `DbParameter`, and a `DbParameter` is SENT and never RETURNED.** The two readers could not
      disagree out loud until a declared type made the round trip, which
      `Database.SqlQuery<UnmappedCustomer>` is the first thing in this repository to do. The rule
      that generalises: **when a fact is read off two carriers, the disagreement stays hidden until
      something exercises both directions.**

      **The fix keeps the per-execution rule rather than working around it.** `QueryExecutor` reads
      `AllowedTypesFor` once and hands the same list to the boundary analyzer and, through
      `ExpressionSerializer.UseExecutionAllowedTypes`, to the resolver.
      `TypeNodeResolver.UseExecutionAllowedTypes` widens and never narrows, and **its cache now
      memoizes the resolution and never the permission** — a name resolved while one execution's
      declared types were in force must not stay admitted for the next execution, whose context may
      declare nothing, so a cache hit still falls through to the allowlist check.

      **`SqlQueryInfoCarrierTest` is 97 of 119**, from 17 at adoption, across R124 -> R125 -> R126.
      The 22 still red are classified in `test/known-failures.txt`: 20 are this tier's store
      (EF's own `NorthwindContext` calls `Ignore(o => o.Freight)` and this tier builds its store
      from the model, where EF's SQLite suite reads a prebuilt `northwind.db` that has the column
      regardless — read from EF's source, not inferred), and 2 are ADR-013's `RelationalTypeMapping`
      cast, which `FromSqlQueryTestBase` already carries as an identical pair.

- [x] **R127. #97 level 2, step one: the relational client is on for the whole of Tier B, and the
      measurement moved nothing in all three levels.** `test/` only, so `eng/measure.sh` is the
      whole gate. **`failed` unchanged at 160**, `total` 29512 -> 29514, a deliberate rise of two
      and both of them green. FIXED none, BROKEN none, **`REASONS: unchanged`**.
      `architecture.md` §6a carries the **D3 amendment 2026-09-03 (R127)**.

      **The owner's decision is taken and recorded: level 2 is a process-wide statement**, answer
      (1) of the two R123 set out. `IModelCacheKeyFactory` is therefore **not** replaced, and
      nothing should add one.

      **What changed.** `SqliteInfoCarrierTier.AddClientServices` registered
      `AddInfoCarrierRelationalClient()` only where a fixture had asked for raw SQL. That gate was
      #56 option D's, and it was about `Database.SqlQuery<T>` rather than about the store: the shim
      alone traded one exception for another, so the two were wanted together. It stops holding once
      the package also decides how the client's **model** is built, which is a property of the
      backing store and not of a permission.

      **A NULL RESULT IS THE ONE CASE `CLAUDE.md` SINGLES OUT, AND IT WAS TREATED AS ONE.** A
      matcher that never fired and a change that did not help look identical from outside, so the
      code was **established to have run** rather than assumed.
      `Sqlite/RelationalClientTierPinTest` reads the registration straight out of the collection
      each tier builds, and it is **red without the change and green with it** — verified by
      reverting the one file, rebuilding and re-running, not by reasoning. Its second test pins the
      other direction: **Tier A registers nothing relational**, which is this handoff's stopping
      rule 4 in test form and is structural rather than conditional, since the override lives in the
      relational project and `InfoCarrierTier.AddClientServices` adds nothing by default.

      **The null result is itself the finding.** The facade shim and the query-root seam are inert
      for a client that never uses `Database.SqlQuery<T>` or a raw-SQL root: widening the
      registration from a handful of fixtures to all 20,363 Tier B tests changed no answer anywhere.
      So level 2's registration has a blast radius of nil, and whatever the **conventions** change
      later is attributable to the conventions alone. Measuring this step separately is what buys
      that, and it is why R123 asked for it.

      **STEP TWO IS BLOCKED ON A DIFFERENT CARRIER PROBLEM AND IS NOT STARTED.**
      `IProviderConventionSetBuilder` is registered by `AddEntityFrameworkInfoCarrier` through
      `EntityFrameworkServicesBuilder.TryAdd`, and that call is made by
      `InfoCarrierOptionsExtension.ApplyServices` into **EF's internal service provider** — not into
      the application's collection, which is what `AddInfoCarrierRelationalClient` runs on. A client
      that does not call `UseInternalServiceProvider` therefore cannot have its convention set
      replaced by a DI call at all, and `SqliteSmokeTest` is exactly that shape. Same question
      `UseRelationalQueryRoots` answered for level 1, same two honest answers, and it is the owner's
      to take before any code. The full reading is in the D3 amendment.

      **And when step two is built, `InfoCarrier.Core.Relational` must REPLACE rather than
      duplicate** (owner's instruction, 2026-09-03): a subclass of `InfoCarrierConventionSetBuilder`
      that calls the base and swaps EF's relational conventions in for the hand-written ones, with
      `InfoCarrierHierarchyMappingConvention` deleted in the same commit. Two implementations of one
      fact in two packages drift silently, which is the failure mode D3 and ADR-012 already record.

- [x] **R128. #97 level 2, step two: the relational package REPLACES the client's convention set
      builder, and the hand-written hierarchy convention is deleted.** `src/` change, so
      `eng/measure.sh`, `eng/trim-ratchet.sh` **and** `dotnet pack`. **`failed` unchanged at 160**,
      `total` 29514 -> 29515. FIXED none, BROKEN none, `REASONS: unchanged`. Trim ratchet `ours`
      89 <= 89. `architecture.md` §6a carries the **D3 amendment 2026-09-03 (R128)**.

      **The owner chose answer (C) on 2026-09-03**, from four live alternatives: replace
      `IProviderConventionSetBuilder` with a subclass, rather than `IConventionSetPlugin`,
      `ModelConfigurationBuilder.Conventions`, or requiring `UseInternalServiceProvider`.

      **R127's "blocked on DI reach" was real but narrower than it read, and this corrects it.**
      EF's `ServiceProviderFixtureBase` builds its provider from
      `TestStoreFactory.AddProviderServices(...)` and calls `UseInternalServiceProvider` — read from
      EF's source. So `AddInfoCarrierRelationalClient` already reaches the convention set for every
      spec fixture, and no new seam was needed. What is still open is the consumer that builds no
      collection at all.

      **`InfoCarrierHierarchyMappingConvention` is deleted**: 131 lines, four hand-spelled
      `Relational:` strings, and the `DocumentMappingPinTest` case that held them against EF's
      constants. That pin is not replaced by another pin — EF's own
      `EntityTypeHierarchyMappingConvention` reads EF's constants, so **a rename is a compile error
      now**, which is strictly stronger than the test was.

      **The relational dependency object is a stub whose 29 members all throw.** The convention
      holds `RelationalConventionSetBuilderDependencies` and never reads it; the two services it
      carries are command-side, and D3's charter says this package gives the client metadata and
      never anything reaching a connection. A throw is louder than a wrong answer.

      **A SECOND NULL RESULT IN A ROW, AND THE NEGATIVE CONTROL IS WHAT MADE IT EVIDENCE.** Removing
      EF's convention from the new builder, changing nothing else, turns
      `TPTInheritanceQueryInfoCarrierTest.Using_from_sql_throws` and its TPC sibling **red**;
      restoring it turns them green. So the convention runs and does work, and the copy it replaced
      was doing the same work — which is why no count moved. Two permanent pins were added to
      `RelationalClientTierPinTest`: a single `IProviderConventionSetBuilder` descriptor per tier,
      the relational subclass on Tier B and the core one on Tier A.

      **`total` moves by one and it is two movements.** Two pin tests added, one pin test deleted.

      **The Release build caught what Debug did not**, twice: a dangling `cref` to the deleted type
      (`CS1574`) and an unnecessary `using` (`IDE0005`), both errors only under `CI=true` in
      Release. This is the N12 lesson holding.

      **Pack is clean and that was verified rather than assumed.** Deleting a public type would be a
      binary break, but `InfoCarrierHierarchyMappingConvention` is **not in the published `10.0.0`**
      — checked in the baseline package's own XML — so there is nothing for validation to compare it
      against.

      **What remains of the duplication.** `InfoCarrierValueGenerationConvention` still spells two
      `Relational:` strings by hand, and `AnnotationDocumentMapping` and `ModelDbFunctions` spell
      more. One step each, one measurement each, because deleting a string deletes its pin.

- [x] **R129. EF's `RelationalValueGenerationConvention` CANNOT run on a client model. Attempted,
      measured, REVERTED.** Documents only in the end — the code change was made, measured and
      backed out, so nothing executable changed and no gate applies. The tree is byte-identical to
      R128, which `r128b` measured, so the baseline files do not move: `failed=160, total=29515`.

      **The measurement: `failed` 160 -> 720. BROKEN 560, FIXED none.** One cause dominates:
      **458 × `The property '__synthesizedOrdinal' cannot be configured as 'ValueGeneratedOnUpdate'`.**

      **What it means, and it is the boundary of level 2 rather than a bug in the change.**
      `__synthesizedOrdinal` is the ordinal property EF synthesizes for a JSON-mapped collection —
      this repository already knows the name, because `AnnotationDocumentMapping.SynthesizedOrdinal`
      pins it against `RelationalKeyDiscoveryConvention.SynthesizedOrdinalPropertyName`. EF's
      relational value-generation convention reaches into relational **model** concepts around that
      property, and the client does not build a relational model. **That is level 3 leaking into
      level 2**, and level 3 is out of scope for the reason D3's charter now states.

      **THE RULE THIS PRODUCES, AND IT IS THE PART THAT TRANSFERS.** D3 scoped level 2 as *"register
      EF's own relational conventions on the client model"*, as though the conventions were one job
      with one answer. **They are not interchangeable in cost, and the difference is not visible from
      the outside.** `EntityTypeHierarchyMappingConvention` reads annotations and model metadata only
      — it ran with no failure at all (R128). `RelationalValueGenerationConvention` derives from a
      core convention and pulls relational model machinery with it — it broke 560 tests. **Each
      convention is its own experiment and needs its own full measurement**, which is exactly what
      taking them one at a time was for.

      **`InfoCarrierValueGenerationConvention` stays, and its doc comment was already right.** It
      says *"Narrow on purpose"*, and narrow is what makes it work: only a property whose
      `ValueGenerated` is still `Never`, and only where the caller declared a default. Its two
      `Relational:` strings and their `DocumentMappingPinTest` case stay with it.

      **A partial run said this was fine and the full run said otherwise.** A filtered check over
      `DocumentMappingPinTest` and `StoreGenerated*` reported 337 of 337 green, which is true and
      says nothing: the 458 live in `AdHocJsonQuery*` and the JSON-mapped families, which that
      filter never touched. Recorded because it is `CLAUDE.md`'s "never state a verdict from partial
      output" arriving in a new disguise — the partial run was not read as a verdict, but it would
      have been easy to.

- [x] **R130. A half-configured relational client is told so, and told what to call.** `src/`
      change, so `eng/measure.sh`, `eng/trim-ratchet.sh` and `dotnet pack`. **`failed` unchanged at
      160**, `total` 29515 -> 29519, a deliberate rise of four and all four green. FIXED none,
      BROKEN none, `REASONS: unchanged`. Trim `ours` 89 <= 89. Pack clean. Release build
      `5 Warning(s), 0 Error(s)`.

      **Raised by the owner as a question — "plain ICC should return NotSupported and recommend
      ICC.R; is that correct, and should tests assert it?"** The answer is yes, and checking it
      found that **R128 had shipped a silent failure two commits earlier**.

      **Three cases, and they were not alike.** Raw-SQL roots were already right:
      `NoRelationalQueryRoots` refuses and its message names the package and the call.
      `Database.SqlQuery<T>` throws EF's own `RelationalNotInUse`, which names nothing of ours and
      still does not. **The conventions were silent**: a client that registered
      `AddInfoCarrierRelational()` but not `AddInfoCarrierRelationalClient()` built a model EF's
      relational conventions never touched, and nothing said so.

      **The consequence is behavioural rather than cosmetic, and it was read rather than assumed.**
      EF decides "non-TPH" by the absence of a discriminator, so `context.Set<Bird>().FromSqlRaw(...)`
      — which EF refuses on a TPT root — was **admitted**. That is R128's own negative control
      (`Using_from_sql_throws`) read properly.

      **IT WARNS AND DOES NOT THROW, AND TWO MEASURED FALSE POSITIVES ARE WHY.** A first version
      triggered on the configuration alone and broke
      `OptimisticConcurrencyTestBase.External_model_builder_uses_validation`, which hands over a
      model built externally with `UseModel` that no convention set can stamp — it replaced EF's own
      `EntityRequiresKey` message with this one. Narrowing the trigger to configuration **and**
      symptom fixed that and left a second: `F1FixtureBase` also builds its model externally.
      **A diagnostic that refuses a legitimate model is worse than the silence it replaces.**

      **The trigger is configuration and symptom, never the shape of the model alone.** A model
      calling `ToTable` is ordinary on a client whose store is not relational — Tier A builds many —
      so the model is not evidence by itself. What is evidence is the client's own statement plus a
      hierarchy that still carries a discriminator while a derived type names its own store object.

      **Three tests, and the third is the one that matters.** Half-configured warns and the message
      names the fix; both halves does not warn (the control); **a plain client that said nothing does
      not warn** (the negative control that keeps the guard from being too wide).

      **Two limits are stated in the code rather than left as silence.** A client that says nothing
      at all cannot be caught — finding out needs the model handshake §6a D2 describes and this
      repository has not built. And the options-carried route is invisible, because `IModelValidator`
      is a singleton and that value is per context.

      **Three `Relational:` strings come back into `InfoCarrier.Core`, and they are pinned.** They
      are a **detector's** strings rather than a fixer's: one that stops matching costs a missed
      warning and never wrong data, which is why R128 could delete the hierarchy convention's four
      outright and these three are worth carrying.

      **`total` was almost recorded as 29518**, which is the measurement that preceded the pin test.
      Two tests were added after that run and the draft note's arithmetic did not add up, which was
      the tell. The suite was re-measured rather than reasoned about. This is the "read it out of
      the run, never derive it" rule catching a live mistake.

- [x] **R131. D3 IS REVERTED (owner, 2026-09-03). One package, kept modular.** Documents only —
      `architecture.md` §6a gains the **D3 SUPERSESSION 2026-09-03 (R131)** and D3's own heading
      carries the notice. **Nothing executable changed.** No gate.

      **The decision was taken on measurements, and they are recorded in the supersession so nobody
      re-derives them.** Referencing `EFCore.Relational` costs **+0.62 MB brotli** on the Blazor
      sample (4.11 -> 4.73), the cost is **unconditional** (a build that references it and calls
      nothing ships the identical figure, because the trimmer keeps the assembly almost whole at
      2.079 MB against 2.09 MB on disk), and it drags in **no new packages**. Going without it costs
      **262 of 20,368** Tier B tests. The owner's judgement: TPT and TPC are often required, and 2 MB
      is not prohibitive here.

      **The expected gain is in test and annotation code, not in the product.** Naming EF's constants
      instead of spelling `Relational:` strings deletes the pins that hold those strings honest, and
      the seams that exist only because two packages had to agree.

      **MODULAR MONOLITH, AND THE SEAM IS THE REQUIREMENT.** The package boundary goes; the
      configuration seam stays. "My backing store is relational" is still something an application
      says. A client over a non-relational store must not get relational conventions on its model —
      ADR-009 Tier A exercises exactly that, and it is stopping rule 4 of this milestone's handoff.

      **Three measurable conditions would reverse it back**, and they are listed in the
      supersession: Blazor WASM becoming a primary target, a non-relational backing store being
      adopted, or the relational half growing past what a monolith should carry.

      **Level 3 stays out of scope and the reference does not change that.** B4's
      `IRelationalTypeMappingSource` problem is store knowledge on the far side of the wire, and
      `TryAddCoreServices()` still collides with ADR-006.

- [x] **R132. `InfoCarrier.Core.Relational` is folded into `InfoCarrier.Core`, as one module.**
      `src/` change, so `eng/measure.sh`, `eng/trim-ratchet.sh` and `dotnet pack`. **`failed`
      unchanged at 160, `total` unchanged at 29519.** FIXED none, BROKEN none, `REASONS: unchanged`
      — the move changes no behaviour, which is what a move should measure. Pack clean, and it now
      produces **two** packages instead of three.

      **The four files moved intact and git recorded all four as renames.** They live at
      `src/InfoCarrier.Core/Relational/` and keep the `InfoCarrier.Core.Relational` **namespace**, so
      a consumer's `using InfoCarrier.Core.Relational;` still compiles and **a future split is a
      folder move plus one `PackageReference` line**. That is the modular half of the owner's
      "modular monolith", and it is a requirement rather than tidiness.

      **Nothing published had to change.** The package never shipped a stable version — verified in
      the `10.0.0` baseline's own XML, which contains none of its types — so there is no break to
      manage and no `CP0002`.

      **Trim: `ours` unchanged at 89, `total` 855 -> 1134.** The rise is EF Core's own bucket going
      585 -> 864 because the Blazor publish now ships `Microsoft.EntityFrameworkCore.Relational`.
      None of those 279 diagnostics is this repository's, which is why only `ours` is gated.
      `eng/trim-baseline.txt` carries the note.

      **Also updated because they named three packages:** `release.yml` (the `for id in` loop and
      the third push step), `eng/doc-words.py` (the deleted `PACKAGE.md`), `InfoCarrier.Core.slnx`,
      the Tier B test project, and two passages in `CLAUDE.md`.

      **What did NOT change, deliberately.** The configuration seam. `AddInfoCarrierRelational`,
      `AddInfoCarrierRelationalClient` and `UseRelationalQueryRoots` keep their names and their
      meanings, and Tier A still registers none of them. One package removes the packaging choice,
      not the configuration one — a client over a non-relational store must still not get relational
      conventions on its model.

- [x] **R133. The product names EF's constants, and seven pin tests become tautologies.** `src/`
      change, so `eng/measure.sh`, `eng/trim-ratchet.sh` and `dotnet pack`. **`failed` unchanged at
      160**, `total` 29519 -> 29512, a deliberate fall of seven. FIXED none, BROKEN none,
      `REASONS: unchanged`. Trim `ours` 89 <= 89. Pack clean. Release build `5 Warning(s),
      0 Error(s)`. **The first of the two gains the owner expected from reverting D3.**

      **Ten names stop being literals.** Eight annotation names become EF's own constants
      (`RelationalAnnotationNames.ContainerColumnName`, `.DefaultValue`, `.DefaultValueSql`,
      `.DbFunctions`, `.TableName`, `.ViewName`, `.MappingStrategy`, and
      `RelationalKeyDiscoveryConvention.SynthesizedOrdinalPropertyName`), and two type names come
      from `typeof(...)` instead of being spelled out.

      **THE PUBLIC CONSTANTS KEEP THEIR NAMES, AND THAT WAS CHECKED RATHER THAN ASSUMED.**
      `AnnotationDocumentMapping` and `InfoCarrierValueGenerationConvention` **are** in the published
      `10.0.0` — verified in the baseline package's own XML — so deleting their consts would be a
      binary break. They are redefined as `= RelationalAnnotationNames.X` instead: same public
      surface, compile-time checked, no `CP0002`. The two type names sit on types that never
      shipped, so those could become `static readonly` from `typeof(...)`.

      **SEVEN TESTS DELETED, THREE KEPT, AND THE SPLIT IS THE POINT.** CLAUDE.md forbids deleting a
      test to make a suite green; that is not this. The seven were green and stayed green, and the
      compiler took over their job — a rename is a build error now, which is stronger than the
      assertion was. The three that a constant **cannot** do are kept, and the file is renamed
      `RelationalMetadataAgreementTest` to say what it actually checks:

      | Kept test | Why a constant cannot replace it |
      |---|---|
      | `The_db_function_methods_agree_with_EFs_own_GetDbFunctions` | `ModelDbFunctions` reads a `MethodInfo` property **by reflection**, so a change answers "this model maps no functions" instead of failing to compile. 81 tests, silently |
      | `Every_DbSet_taking_method_EF_declares_there_is_one_the_convention_leaves_alone` | The method set is **derived** from EF, not listed: those on `RelationalQueryableExtensions` whose first parameter is a `DbSet<>`. A new EF overload group fails this test rather than a caller's model build |
      | `The_walk_agrees_with_EF_for_every_type_including_nested_ones` | It reproduces EF's **ownership-chain walk**. A change in how EF resolves a container breaks only the walk, and only for nested types, which is what B12 was |

      **Also updated:** every doc comment that said "Pinned by `DocumentMappingPinTest`" for a
      constant the compiler now checks.

- [x] **R135. There is no relational opt-in any more: every client gets the relational half.**
      `src/` change, so `eng/measure.sh`, `eng/trim-ratchet.sh` and `dotnet pack`. **`failed`
      unchanged at 160**, `total` 29512 -> 29509, a deliberate fall of three. FIXED none, BROKEN
      none, `REASONS: unchanged`. Trim `ours` 89 <= 89. Pack clean. Release build from a forced
      clean `obj`/`bin`: `0 Error(s)`. `architecture.md` §6a carries the **D3 amendment 2026-09-03
      (R135)**.

      **The owner's correction, and it follows from R131 rather than modifying it.** R131 reverted
      D3 to one package but kept the opt-in. One package that already ships the relational half
      cannot save a consumer anything by withholding it: the 0.62 MB brotli is in the payload
      whether or not the registration is made. So the opt-in bought nothing and could only be got
      wrong, which R134's session had just measured — merging the two registration flavours into
      one broke all 25 `SqliteSmokeTest` cases, because a *server*'s collection has no
      `IInfoCarrierDocumentMapping` to activate the convention set builder with. **The recorded
      reason for two flavours was also wrong** — it claimed a server must not receive
      `InfoCarrierRelationalFacadeDependencies` because its `RelationalConnection` throws — and
      neither the right reason nor the wrong one matters once there is nothing to register.

      **Deleted.** `AddInfoCarrierRelational()`, `AddInfoCarrierRelationalClient()` and the file
      that held them; `UseRelationalQueryRoots(...)` with `WithRelationalQueryRoots` and
      `RelationalQueryRoots` behind it; `NoRelationalQueryRoots`; R130's `Validate` override,
      `HalfConfiguredMessage`, `RelationalConventionsAnnotation`, three detector constants,
      `HasUnmappedNonTphHierarchy` and the `StampConvention` that fed it; and
      `HalfConfiguredRelationalClientTest`. `RelationalClientTierPinTest` now asserts the opposite
      of what it asserted, which is the point: both tiers register one convention set builder and it
      is the relational subclass.

      **The three tests in the `total` fall are named, because CLAUDE.md forbids an unexplained
      one.** `A_client_that_registers_only_the_shared_half_is_warned_and_told_what_to_call`,
      `A_client_that_registers_the_client_half_is_not_warned`,
      `A_plain_client_that_has_said_nothing_is_not_warned`. All three tested a diagnostic that has
      no subject once there is no half-configured state.

      **THE ONE THING THAT MOVED IN 29,509 TESTS, and the first measurement did not survive it.**
      The run before this one stood at 167 with four `CompiledModelInfoCarrierTest` failures, all on
      `Difference found in DbContextModelBuilder.cs`. Deleting the stamp fixed one; the other three
      were a real difference, read out by rewriting the baselines with
      `EF_TEST_REWRITE_BASELINES=1` and diffing. It is one line per hierarchy root:
      `runtimeEntityType.AddAnnotation("Relational:MappingStrategy", "TPH")`. EF's own SQL Server
      baselines carry the same annotation with the value their model implies, so this is the client
      carrying a fact it previously did not carry at all, derived from the same `OnModelCreating`
      the server uses. Baselines updated.

      **R120's two-carrier hazard is now closed by construction.** With one implementation and no
      way to configure another there is one answer, so the boundary analyzer and the forward
      translator cannot disagree. `QueryExecutor` names `InfoCarrierRelationalQueryRoots.Instance`.

- [x] **R136. The two spec test projects become one, and the tiers become a namespace.** `test/`,
      `eng/` and CI only, so `eng/measure.sh` alone. **`failed` UNCHANGED at 160, `total` UNCHANGED
      at 29509.** FIXED none, BROKEN none, `REASONS: unchanged`, and the snapshot is
      **byte-identical** to R135's -- which is the right measurement for a pure move, because a
      fully-qualified test name does not mention its project. Release build `0 Error(s)`.
      `decisions.md` ADR-013 carries the **amendment 2026-09-03 (R136)**.

      **The split had one reason and it is gone.** R122 separated the projects to keep the
      `InfoCarrier.Core.Relational` package off Tier A's compile line: a reference is transitive, so
      naming a relational type in the shared harness would have reached Tier A, and a relational
      client over an InMemory backend was the disagreement to prevent. D3's supersession removed the
      package, and R135 made every client relational and measured that it changes no answer. Neither
      half of the reason survives, so the boundary separated nothing.

      **What moved.** 97 files, recorded by git as renames: `Sqlite/`, `TestUtilities/`,
      `ModelBuilding/`, `RelationalInfoCarrierComplianceTest` and `RelationalMetadataAgreementTest`.
      One file was deleted rather than moved -- the second `InvariantCultureInitializer`, identical
      to Tier A's but for its namespace, and a module initializer is per assembly. The merged
      `.csproj` is Tier A's plus three package references and the SQLite audit suppression.
      `eng/measure.sh` takes one project, `build.yml` runs one `dotnet test` and passes one TRX, and
      **both keep their plural shape**: `ratchet.sh` still unions several TRX into one counter set
      and one name list, because a second backing store would need it again.

      **MERGING FOUND SOMETHING THE BOUNDARY HAD HIDDEN, and this is the part worth carrying.**
      `RelationalComplianceTestBase.All_query_test_fixtures_must_implement_ITestSqlLoggerFactory`
      scans the whole target assembly. With one assembly it immediately demanded that Tier A's
      InMemory query fixtures implement `ITestSqlLoggerFactory` -- which they must not, because they
      emit no SQL and the interface would claim something false. It is overridden to scan the Tier B
      namespace only, which is the same correction `GetBaseTestClasses()` already makes in that
      class and for the same reason: **a compliance test written against one store's assembly has to
      be told which half of a merged one it answers for.** All three compliance tests are green.

      **`InfoCarrier.Core.TestUtilities` now has exactly one consumer** and its stated reason
      ("nothing relational may be referenced from here") is dead. It is kept, with the prose
      corrected to say the separation is a layering choice rather than a barrier. Folding it in is
      the next boundary that separates nothing, and it is deliberately not done in this step.

- [x] **R137. The shared harness folds into the spec project, and R136's misplaced folder is
      fixed.** `test/` only, so `eng/measure.sh` alone. **`failed` UNCHANGED at 160, `total`
      UNCHANGED at 29509**, FIXED none, BROKEN none, `REASONS: unchanged`, snapshot byte-identical.
      Release build `0 Error(s)`, the five known Razor warnings. `decisions.md` ADR-013's R136
      amendment records it.

      **`InfoCarrier.Core.TestUtilities` had one consumer after R136**, and its stated reason -- the
      harness is "neither tier's property" and nothing relational may be named in it -- is a
      statement about two projects. There are not two. Its 19 files join the 7 that came from Tier B
      in `test/InfoCarrier.Core.FunctionalTests/TestUtilities/`, 26 in one folder, and the project,
      its `.csproj` and its solution entry are deleted. The namespace does not move
      (`InfoCarrier.Core.FunctionalTests.TestUtilities`), so no test name changes and the baseline
      is untouched.

      **R136 PUT TIER B'S HARNESS ONE FOLDER TOO DEEP AND THE MEASUREMENT COULD NOT SEE IT.**
      `git mv TestUtilities ../InfoCarrier.Core.FunctionalTests/TestUtilities` moved the directory
      *into* the existing one, giving `TestUtilities/TestUtilities/`. It compiled, because the SDK
      globs `**/*.cs`, and it measured identical, because a test's fully-qualified name does not
      mention its path. **A pure-move measurement proves the moves changed no answer; it does not
      prove the files landed where they were meant to.** Flattened here.

      **What the folder still says is true.** `InfoCarrierTier` and `InMemoryInfoCarrierTier` are
      store-neutral; `SqliteInfoCarrierTier` and the SQLite backend store are the per-store answers
      beside them. That separation is a design, not a boundary, and it survives the fold.

- [x] **R138. The Tier B Northwind store gets the ten `Orders` columns its model ignores.** `test/`
      only, so `eng/measure.sh` alone. **`failed` FALLS 160 -> 150**, `total` UNCHANGED at 29509.
      **FIXED ten, BROKEN none**; every one is `SqlQueryInfoCarrierTest`, which goes 97 -> 107 of
      119. `test/known-failures.txt` and `known-failures.names.txt` both move, as a falling count
      requires. Release build `0 Error(s)`.

      **The gap.** The core `NorthwindContext` ignores `RequiredDate`, `ShippedDate`, `ShipVia`,
      `Freight`, `ShipName`, `ShipAddress`, `ShipCity`, `ShipRegion`, `ShipPostalCode` and
      `ShipCountry`, and the real Northwind schema has all ten. EF's own SQLite suite has both at
      once because its store is a prebuilt `northwind.db`; this tier builds its store from the
      model, so it could have one or the other. `SqlQueryTestBase` reads `Orders` with raw SQL into
      its own `UnmappedOrder`, which names nine of the ten, and failed with
      `no such column: m.Freight` -- `Freight` only because it is the first the parser reaches.

      **THREE ROUTES WERE MEASURED AND ONLY THE THIRD IS RIGHT.**

      | Route | Result |
      |---|---|
      | Map the ten on the server model | `failed` 160 -> **158**. FIXED ten, **BROKEN eight** |
      | `ALTER TABLE` alone | No change. The columns exist and hold null; the tests compare real values |
      | `ALTER TABLE` plus an `UPDATE` from `NorthwindData.CreateOrders()` | `failed` 160 -> **150**. FIXED ten, BROKEN none |

      **Why the first route breaks eight, and it is the finding rather than the fix.** This
      repository is two models. With the property mapped on the server, the server answers a member
      access the CLIENT's model calls unmapped, so
      `Average_with_unmapped_property_access_throws_meaningful_exception`,
      `Collection_select_nav_prop_all_client`, `Collection_where_nav_prop_all_client` and
      `SelectMany_..._references_non_mapped_properties_...` stop throwing and return data. **The
      boundary analyzer never asks the client model whether a member is mapped**, and that is
      invisible while the two models agree -- which they always do, except under that experiment.
      Recorded in [`findings.md`](findings.md), not fixed here.

      **Shadow properties do not avoid it**, which was also measured: `Property<int?>("ShipVia")`
      binds to the CLR member whose name it matches, ignored or not, so the server model maps it
      either way. Raw DDL is the only route that gives the store the column without giving either
      model the property.

      **`SqliteParameter` and not a bare value**, because a null has to arrive as `DBNull` and EF's
      raw-SQL path refuses `DBNull.Value` directly with *"no store type mapping for properties of
      type 'DBNull'"* -- which cost one measurement, read as 103 of 127 where the baseline was 105.

      **THE OTHER EIGHT FREIGHT FAILURES DID NOT VANISH, THEY MOVED**, and only the reasons diff
      says so. `SQLite Error 1: 'no such column: m0.Freight'` is now
      `Type 'System.ValueTuple``2[...UnmappedOrder...]'`: the wire type allowlist refusing the tuple
      a composed raw-SQL query projects. That is the next problem in the same tests and is
      unaddressed. This is exactly CLAUDE.md's "fixed what it aimed at and uncovered the next
      problem in the same tests" case, which a fixed/broken list alone cannot tell from a no-op.

- [x] **R139. The unmapped-member defect is pinned in the suite, red on purpose, and the fix is
      priced.** `test/` only, so `eng/measure.sh` alone. **`failed` RISES 150 -> 151 and `total`
      29509 -> 29513**, both deliberate and both noted in `test/known-failures.txt`. FIXED none,
      BROKEN one, and that one is the new pin. Release build `0 Error(s)`.

      **What is pinned.** The client's model is the authority for what is mapped, and nothing
      enforces it. `UnmappedMemberBoundaryTest` builds the only disagreement this repository can
      produce: `SqliteSmokeContext` ignores `Shipment.Note` for both sides, and the store's own
      model customizer maps it on the server alone. Three of the four tests are controls, so the red
      one cannot pass by accident.

      **THE FIX IS WRITTEN AND MEASURED AND NOT SHIPPED.** It needs BOTH readers to learn the client
      model -- `ServerBoundaryAnalyzer` so the subtree is not shipped, and `QuerySplitter`'s
      client-code finder so it is refused rather than evaluated locally -- because the analyzer's
      verdict alone refuses nothing and the finder never examines a node the analyzer called
      shippable. With both, the pin passes with EF's own `QueryUnableToTranslateMember` wording, and
      **`failed` measures 166 against 150**: sixteen spec tests, every one a message difference on a
      query that already refused. Three narrowings were tried and none helps. The account, the four
      families and the reason each narrowing fails are in [`findings.md`](findings.md).

      **This is an owner decision and it is recorded as one**, not left as a silent gap: sixteen
      message-text differences against one query shape that answers where every other provider
      refuses.

- [x] **R140. The twelve `Employees` columns the Northwind model ignores.** `test/` only, so `eng/measure.sh` alone. **`failed` FALLS 151 -> 149**, `total`
      UNCHANGED at 29513. FIXED two, BROKEN none. Both baseline files move. Release build
      `0 Error(s)`.

      R138's route applied to the other table it fits. The core `NorthwindContext` ignores
      `LastName`, `TitleOfCourtesy`, `BirthDate`, `HireDate`, `Address`, `Region`, `PostalCode`,
      `HomePhone`, `Extension`, `Photo`, `Notes` and `PhotoPath`, and `SqlQueryTestBase`'s
      `UnmappedEmployee` names all twelve. `ALTER TABLE` plus an `UPDATE` from
      `NorthwindData.CreateEmployees()`, so neither model gains a property and the store holds what
      EF's prebuilt `northwind.db` holds.

      **It surfaced with a different message from the `Orders` half and the same cause.**
      "The required column 'Address' was not present in the results of a 'FromSql' operation"
      rather than `no such column`, because the failing query selects named columns where the other
      selected everything.

      **THE STORE-SHAPE FAMILY IS NOW CLOSED, and that was checked rather than assumed.**
      `UnmappedProduct` names only `CategoryID`, which the server model has mapped since R97, and
      `Customers` ignores nothing at all.

- [x] **R141. One allowlist, so one decomposition: the response path stops keeping a second
      set.** `src/` change, so `eng/measure.sh`, `eng/trim-ratchet.sh` and `dotnet pack`.
      **`failed` FALLS 149 -> 141**, `total` UNCHANGED at 29513. FIXED eight, BROKEN none; all eight
      are `SqlQueryInfoCarrierTest`, which reaches **117 of 119**. Trim `ours` 89 <= 89. Pack clean.
      Release build from a forced clean `obj`/`bin`: `0 Error(s)`.

      **The defect.** The application's registered projection types reached the REQUEST path through
      `TypeAllowlist.ForModel(model, registeredTypes)`, and the RESPONSE path as a SECOND set that
      `TypeNodeResolver` consulted beside the allowlist. **A second set cannot be decomposed.** A
      raw-SQL join projects `ValueTuple<UnmappedCustomer, UnmappedOrder>`; the allowlist admits
      `ValueTuple<,>` and then asks whether each ARGUMENT is allowed; and the answer lived in the set
      it could not see. `TypeAllowlist.With` widens the one list instead, and the resolver consults
      only that.

      **R120's shape and ADR-012's**: a fact two components read independently, where the
      disagreement only widens what one of them may do, so nothing fails loudly. It was found by
      following R138's reasons diff, which showed eight failures MOVING from a missing column to
      this tuple rather than disappearing.

      **It widens no surface, and that was checked against the review rather than asserted.** Every
      added type is one the application registered explicitly, which `security-review.md` §4c
      records as the safe shape, and the same types already cleared the request path. The §2
      conjunction is untouched -- none of `Binder`, `MethodBase`, `MethodInfo`, `ConstructorInfo`,
      `PropertyInfo`, `Activator`, `Assembly` or `AppDomain` can arrive this way -- and
      `DeserializationHardeningTest` is green.

- [x] **R142. Tier B builds both models from ONE `OnModelCreating`, as version 1 did and as an
      application does.** `test/` only, so `eng/measure.sh` alone. **`failed` UNCHANGED at 141,
      `total` UNCHANGED at 29513.** FIXED none, BROKEN none, `REASONS: unchanged`. Release build
      `0 Error(s)`.

      **The owner asked who decided the two models should differ, and the answer is that this
      repository did.** `NorthwindInfoCarrierSqliteServerContext` gives the SERVER the store shape:
      `ToSqlQuery` for each keyless type, `ToTable("Order Details")`, and `Product.CategoryID`. The
      client got none of it. That was forced rather than chosen -- before D3's supersession the
      client had no relational half, so it could not hold `ToTable` or `ToSqlQuery` at all.

      **VERSION 1 NEVER DID THIS, and its harness is the model to copy.** `subrepos/infocarrier-v1`
      carries ONE `ContextType` and ONE `OnModelCreating` in its `SharedTestStoreProperties`, and
      its backend store resolves the server context from the same `ContextType` the client uses.
      There is no second context class anywhere in it. Its two tiers, `InMemory/` and `SqlServer/`,
      also live in one test project -- the shape R136 restored.

      **So the client now uses the server's context class on both Tier B fixtures**, the Northwind
      one and the bulk-updates one, and the client's model gains the store shape it would have in a
      real deployment: `samples/Northwind.Client` and `samples/Northwind.Server` already share
      `NorthwindContext` from `samples/Northwind.Shared`.

      **TIER A CANNOT FOLLOW, and the reason is a package rather than a preference.** Its inheritance
      fixture gives the server an InMemory *defining query* for the keyless `AnimalQuery`.
      `ToInMemoryQuery` needs `Microsoft.EntityFrameworkCore.InMemory`, which the PRODUCT does not
      reference and a real client therefore cannot call. Relational configuration is different: the
      product carries it since D3's supersession, so a client can hold it honestly. The line is
      "what a real client could hold", not "what the test project happens to reference".

      **`serverContextType` keeps its second, unrelated use.** `SeedingInfoCarrierTest` and
      `WithConstructorsInfoCarrierTest` supply a server context for its CONSTRUCTOR shape, not for
      its model. That has nothing to do with this and is untouched.

      **What is left disagreeing is one synthetic case**: `Shipment.Note` in
      `UnmappedMemberBoundaryTest`, which exists to make the two-model defect visible and says so.

- [x] **R143. Every fixture builds both models from ONE `OnModelCreating`, and it turns nothing
      green.** `test/` only, so `eng/measure.sh` alone. **`failed` UNCHANGED at 141, `total`
      UNCHANGED at 29513.** FIXED none, BROKEN none, `REASONS: unchanged`. Release build
      `0 Error(s)`.

      **The owner's question was whether identical models make failures disappear. The measured
      answer is no.** Not one test changed its answer, on either tier. The remaining four fixtures
      that supplied a separate server context are converged: Northwind Tier A, the Tier A
      inheritance fixture, and the constructors fixture, which added a keyless type with
      `ToInMemoryQuery` on the server alone. The seeding fixture needed nothing -- it already passes
      the same type as both.

      **What the separate server contexts were for.** Store shape, never a hidden property: a
      defining query for a keyless type, a table name, and one column. **Nothing in the suite hid a
      property from the client except the pin added in R139**, which was written to do exactly that.

      **A ONE-OFF FAILURE APPEARED IN THIS STEP'S RUN AND IS AN INTERMITTENT, NOT A REGRESSION.**
      `AdHocMiscellaneousQuerySqliteInfoCarrierTest.Bool_discriminator_column_works(async: False)`
      failed with `ObjectDisposedException`. It is Tier B, where the change is Tier A; its class
      passes alone at 71 of 72; and the suite re-run at the same code state came back at 141 with
      nothing broken. **The suspect is R136/R137**: the two tiers used to be two processes and are
      one assembly now, xUnit parallelises collections within an assembly, and no
      `CollectionBehavior` is declared. It is one observation and it is written up in
      [`findings.md`](findings.md); `CLAUDE.md`'s "there is no known intermittent" is corrected.

- [x] **R144. The two-model machinery is GONE. One context class per fixture, and no context class
      is named for a side.** `test/` only, so `eng/measure.sh` alone. **`failed` UNCHANGED at 141,
      `total` UNCHANGED at 29513.** FIXED none, BROKEN none, `REASONS: unchanged`. Release build
      `0 Error(s)`.

      **The owner's instruction, and the reasoning behind it.** R142 and R143 converged the fixtures
      but left the mechanism standing. This removes it. `SharedTestStoreProperties.ServerContextType`
      is deleted, `InfoCarrierTestStoreFactory.Create`'s `serverContextType` parameter is deleted,
      and `InfoCarrierBackendTestStore` reads `ContextType` for both sides. The harness now does
      what version 1's did: **one `ContextType`, one `OnModelCreating`, both halves.**

      **No context class carries `Server` or `Client` in its name any more.** Five were renamed:

      | Was | Is |
      |---|---|
      | `NorthwindInfoCarrierServerContext` | `NorthwindInfoCarrierContext` |
      | `NorthwindInfoCarrierSqliteServerContext` | `NorthwindInfoCarrierSqliteContext` |
      | `InheritanceInfoCarrierServerContext` | `InheritanceInfoCarrierContext` |
      | `WithConstructorsInfoCarrierServerContext` | `WithConstructorsInfoCarrierContext` |
      | `SeedingInfoCarrierServerContext` | `SeedingInfoCarrierOptionsContext` |

      **THE LAST ONE IS NAMED FOR A CONSTRUCTOR SHAPE, AND IT IS THE ONLY SECOND CLASS LEFT.** EF's
      `SeedingContext` is abstract, takes a `string testId`, and declares no `DbContextOptions`
      constructor, so nothing the harness registers can derive from it. Its `HasData` seed is
      therefore written twice. That is a limitation of the base rather than a design of ours, and
      the test itself catches the two copies drifting, because it asserts the rows.

      **A belief was falsified and the prose that carried it is corrected.** Three fixtures said a
      defining query "cannot be part of the client's model, which has no store to run it against".
      The client holds the annotation and never runs it. Converging every fixture changed no answer
      anywhere in 29,513 tests.

- [x] **R145. The unmapped-member pin is removed, by a scope decision rather than to make the suite
      green.** `test/` only, so `eng/measure.sh` alone. **`failed` FALLS 141 -> 140** and `total`
      FALLS 29513 -> 29509, four tests of which three were green. FIXED one, BROKEN none. Both
      baseline files move.

      **Why it goes.** It could only fail by building a split model on purpose, and the previous
      step removed split models from the harness on the owner's instruction. A test that pins a
      configuration the project has declared out of scope does not earn its place. It was added the
      day before by this same session, so nothing long-standing was removed.

      **THE DISTINCTION FROM A SUPPRESSED TEST MATTERS AND IS STATED IN
      `test/known-failures.txt`.** CLAUDE.md's rule stops a real gap being hidden. This is the
      opposite: the gap stays written down, with its price and the condition that reopens it.

      **What it pinned, kept in [`findings.md`](findings.md).** The client's model is the authority
      for what is mapped, and nothing enforces it: where the server's model maps a property the
      client ignores, the query is answered rather than refused. It needs a deliberate one-sided
      `Ignore`, which no realistic application writes. The fix is written and measured at about
      sixteen spec tests, every one a message difference on a query that already refuses.

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
