# Implementation plan — M9 (provider neutrality and store coverage) — CLOSED 2026-08-17

**Archived. Never edited again.** Milestone scope and exit criteria are in
[`../roadmap.md`](../roadmap.md) §M9, which records the closure. The rolling plan for the current
milestone is [`../implementation-plan.md`](../implementation-plan.md).

**Closed at `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177`** (`j21`), from
`13 / 22655` at the milestone's last pre-close measurement. Every one of the nine is classified
below, and the consumer-facing statement of them is [`../limitations.md`](../../../limitations.md).

---

## Phase J — provider neutrality and store coverage (M9)

Scope lives in [`roadmap.md`](../roadmap.md) §M9. **M8 is not closed**, so this plan holds two
milestones at once for the first time; Phase J is appended rather than replacing Phases H and I,
and the whole file is rewritten when M8 closes.

The audit that opened this phase is in [`architecture.md`](../../../architecture.md) §6a, D3 (amended) and
D4 (new). Two things it established are worth restating here because they shape the order below:
the package reference is a **symptom**, and the **fixed query-boundary allowlist** is the
assumption nobody had recorded.

### J1–J3 — the tier moves

CLAUDE.md's A79/A80 rule: a base belongs to exactly one tier, and *the tier that translates is the
one whose green means more*. Three bases sit on Tier A only because that is where they were first
adopted, and each carries skips that are **EF's InMemory limits, not this provider's**. EF's own
SQLite suite skips **zero** of them, which is the whole argument:

| Base | Skips today | EF InMemory | EF SQLite | EF SqlServer |
|---|---|---|---|---|
| `KeysWithConverters` (#26238) | 7 | 8 | **0** | **0** |
| `BuiltInDataTypes` / `CustomConverters` (#17050) | 4 | 4 | **0** | **0** |
| `ProxyGraphUpdates` (#2166, #3924) | 13 | 13 | **0** | — |

Measured one at a time, because a combined move cannot tell which base moved the number.

- [x] **J1. `KeysWithConverters` to Tier B.** `<this commit>`
      `Total tests: 22453, Passed: 22224, Failed: 15, Skipped: 214` (`j1b`), against
      `22453 / 22219 / 13 / 221` (`m8-17`). **All four figures are read out of the run's own summary
      block; none is arithmetic.** Seven skips gone, five of them now passing, and
      **`failed` rises 13 → 15 deliberately** — the two that fail describe *this provider* where
      before they described EF's InMemory store, which is the whole point of the move.

      **The move needed one thing EF's SQLite fixture gets for free, and finding it cost a run.**
      Deleting the seven skips and the three `Ignore<EnumerableClassKey*>()` calls put the class at
      **47 failures**, every one `CollectionWithoutComparer`: `EnumerableClassKey.Id` is an
      `IEnumerable` behind a value converter with no value comparer, and model validation warns.
      `KeysWithConvertersSqliteFixture` does not hit it because its `AddOptions` is
      `builder.UseSqlite(…)` and **never chains to base**, so `FixtureBase`'s
      `ConfigureWarnings(Default(Throw))` never runs. This client cannot take that route —
      `UseSqlite` is precisely what it does not do — so it ignores the one event id instead, on
      **both** halves, because the model is validated twice (A49).

      **The two residual failures are both new information, and both are the wire's rather than the
      store's.** Neither existed as a failure before; both were hidden inside a skip that was about
      InMemory.

      | Test | What it says |
      |---|---|
      | `Can_insert_and_read_back_with_enumerable_class_key_and_optional_dependents` | `NotImplementedException` from `EnumerableClassKey.GetEnumerator()`, reached from `DynamicValueMapper.MapToNode`. The mapper sees `IEnumerable` and takes the **collection** branch on a value that is a *key*, not a collection. **ADR-012's family exactly** — a CLR type whose member throws for an ordinary instance, like `IPAddress.ScopeId` (C23) and `Uri.AbsolutePath` (C34) — except the value arrives as a `ConstantExpression` in a query, where there is no property to read a converter from. |
      | `Can_query_and_update_owned_entity_with_value_converter` | `MissingMethodException: Cannot dynamically create an instance of type '…+Key[…]'. Reason: No parameterless constructor defined.` Raised deserializing the round trip, so it is `RehydrateObject` on a nested generic type with no parameterless constructor. |

      Left red and classified, as CLAUDE.md requires. Neither is a reason to go back to Tier A:
      on Tier A they were unreported.
- [x] **J2. `CustomConverters` to Tier B.** `<this commit>`
      `Total tests: 22453, Passed: 22225, Failed: 18, Skipped: 210` (`j2b`), against
      `22453 / 22224 / 15 / 214` (`j1b`). Four skips gone, `failed` rises 15 → 18 deliberately.

      **The four #17050 skips are all in `CustomConverters`, not in `BuiltInDataTypes`**, which is
      why this step is named for the class rather than for the file. `BuiltInDataTypes` and
      `ConvertToProviderTypes` share the file and carry no skips at all; they are J2b below.

      Three of the four now pass — `Value_conversion_with_property_named_value`,
      `Collection_property_as_scalar_Any`, `Collection_property_as_scalar_Count_member` — and every
      one of them is a **collection property behind a value converter**, which is B4's subject and
      the shape this wire has paid most for. They had never once been executed.

      **Two InMemory statements went with the skips**, and both turned out to be real coverage:
      a non-composed `GroupBy` is no longer refused by the store, and
      `Optional_datetime_reading_null_from_database` is no longer a silent `Task.CompletedTask` —
      SQLite has a null to read.

      **An override adopted from EF was disproved by measurement and deleted.**
      `CustomConvertersSqliteTest` overrides `Value_conversion_on_enum_collection_contains` to
      assert a translation failure; taking it measured `Assert.Throws() Failure: No exception was
      thrown`, because the query this provider ships is *answered*. Kept out, with the reason in
      the class. This is CLAUDE.md's rule read in the other direction: an override of ours that EF
      does not need is a workaround, and so is one of EF's that we do not need.

      **Three residual failures, all new information and none of them the store's:**

      | Test | What it says |
      |---|---|
      | `GroupBy_converted_enum` | `GroupBySingleQueryingEnumerable+InternalGrouping<…>` **is not on the deserialization allowlist**. An EF-internal grouping type reaches the wire. |
      | `Value_conversion_is_appropriately_used_for_join_condition` | The `Join` over two converted columns is not translated. |
      | `Collection_enum_as_string_Contains` | `Assert.Throws() Failure: No exception was thrown` — and the base's body is `Assert.Throws<InvalidOperationException>(…)` around the query. **A28 family**: the spec test asserts a limitation this provider does not have, and it returns the right answer. |

- [x] **J12a. `GraphUpdates` to Tier B — the store switch and the enlisting hook.** `<this commit>`
      `Total tests: 22654, Passed: 22461, Failed: 16, Skipped: 177` (`j12a`) against
      `22672 / 22487 / 14 / 171` (`j9`). **127 tests moved to a store that enforces constraints and
      it cost 2 failures.** J11 did the heavy lifting; this is what was left.

      **Zero `database is locked`.** The `UseTransaction` override landed in the *same* change as
      the store switch, which is the whole lesson of J3's first attempt — omitting it there cost two
      hours of 30-second timeouts. Applied first time here.

      `total` falls 18 and `skipped` rises 6: EF's six `GraphUpdatesSqliteTestBase` skips, mirrored
      one for one, and xUnit reports a skipped theory as one test rather than as its
      parameterizations — the same accounting `known-failures.txt` records for C94.

      **The two new failures are one message:** `SQLite Error 19: 'NOT NULL constraint failed:
      OwnedOptional1.Id'`, on `Save_changed_owned_one_to_one` and `Save_changed_owned_one_to_many`.
      An owned dependent's key arrives null where the store requires it. Filed as J13.

      **Still to do, and deliberately a separate step:** the class carries **28** silent
      `Task.CompletedTask` overrides — 28 tests that do nothing at all, most of them commented
      *"FK uniqueness not enforced in in-memory database"* or about cascade delete *in store*. Those
      are precisely what a real store tests, and they are J12b. Splitting them off keeps this
      measurement interpretable.

- [x] **J12b. Deleted `GraphUpdates`' 28 silent no-op overrides.** `<this commit>`
      `Total tests: 22654, Passed: 22451, Failed: 26, Skipped: 177` (`j12b`). **A deliberate rise of
      10, and the honest trade is 18 for 10**: of 28 tests that did nothing at all, 18 now pass and
      10 fail. 140 lines deleted.

      **`total` is unchanged, and that is the finding.** These were never skips — a
      `=> Task.CompletedTask` override *counts as a passing test*. So **28 green ticks in every
      previous run were an empty method body**. That is worse than a skip, which at least announces
      itself in the `Skipped` column, and it is why deleting them is progress even at +10.

      **The 10 are five tests × async, in three groups, classified rather than counted:**

      | Group | Count | Message |
      |---|---|---|
      | `Cruiser` / `CruiserWithSentinel` not in the model | 4 | *"The entity type 'Cruiser' was not found. Ensure that the entity type has been added…"* — a **model** fault, which cannot be a store limitation |
      | Unknown foreign-key value at save | 2 | *"The value of 'SomethingOfCategoryB.CategoryId' is unknown when attempting to save…"* |
      | `DbUpdateException` | 5 | a store constraint; overlaps J13's shape |

      Every one of the five test names is `Can_insert_when_…_has_default_value` or
      `…_has_sentinel_value`, so **this is the sentinel/default-value family** — and
      `ChangeEntryMapper`'s `SentinelProperties` comment is where to start, because it already
      describes a value the wire cannot distinguish from unset. Filed as J14.

- [x] **J14 (rest). A store default now makes the CLIENT call a property store-generated.** `<this commit>`
      `Total tests: 22655, Passed: 22465, Failed: 13, Skipped: 177` (`j14b`): **2 fixed, 0 broken**,
      `total` +1 for the new pin assertion. **`GraphUpdates` is now fully green.**

      **B6's divergence, on the way out, where it is fatal rather than lossy.** `ValueGenerated` is
      inferred by a convention, and the one that reads `HasDefaultValue` is
      `RelationalValueGenerationConvention` — which the server runs and this provider does not. B6
      recorded what that costs for values coming *back*: no store-generated slot. Outbound it is
      worse: `SomethingOfCategoryB.CategoryId` has `HasDefaultValue(2)` and is half of a composite
      foreign key, so the client believed nothing would ever supply it and **EF's own change tracker
      refused the save before the wire was reached** — `InternalEntryBase.PrepareToSave`, with not one
      frame of this provider in the stack, which is how it was identified.

      `InfoCarrierValueGenerationConvention` is the same shape and the same argument as
      `InfoCarrierKeyDiscoveryConvention`: where the answer is decided by the caller's own model
      configuration rather than by the store, the client has to reach it too. The annotations are
      named **by string**, as J5 decided, and pinned beside the other two.

- [x] ~~**J14. The sentinel/default-value family on a real store**~~ — 10 failures, 5 tests × async.
      Three groups, classified in J12b. **Start with the four `Cruiser`-not-in-model ones**: an
      entity type missing from the model is a fixture or convention fault, cannot be a store
      limitation, is the cheapest of the three to settle, and may explain the others.

- [ ] **J13. One owned-collection test, and it is C76's value-only fallback biting.**
      **Half closed by J14 and the other half re-diagnosed.** `Save_changed_owned_one_to_one` is
      **fixed** — the `OwnerRoot` owned-collection keys J14 adopted were what it needed.
      `Save_changed_owned_one_to_many` remains, and it is no longer `NOT NULL`: it is now an
      identity conflict, with C11's replay diagnostic firing, which is what that instrument was
      built for.

      ```
      The instance of entity type 'OwnedOptional1' cannot be tracked because another instance
      with the key value '{Id: -2147482647}' is already being tracked.
        placeholders resolved so far: -2147482647->-2147482647(tmp=True)
        placeholder values expected in this request: -2147482647, -2147482646
      ```

      **The client is correct, and that was probed rather than assumed.** A temporary instrument in
      `ChangeEntryMapper` printed every key and foreign-key property leaving the client:

      ```
      corr=0 OwnerRoot        Id=-2147482647 shadow=False temp=True
      corr=1 OwnedOptional1   Id=-2147482647 shadow=True  temp=True | OwnerRootId=-2147482647
      corr=2 OwnedOptional1   Id=-2147482646 shadow=True  temp=True | OwnerRootId=-2147482647
      corr=3 OwnedOptional2   Id=-2147482647 shadow=True  temp=True | OwnedOptional1Id=-2147482646
      ```

      corr=1 and corr=2 carry **distinct** placeholders. The client did its job.

      **Two facts point at one line.** First, the same value `-2147482647` is used by `OwnerRoot.Id`,
      `OwnedOptional1.Id` *and* `OwnedOptional2.Id` — EF's temporary generator counts down **per key
      property**, which is C76's premise. Second, `OwnedOptional1.Id` is **both a key and a foreign
      key**, so the server resolves it as a *reference* through the placeholder map.

      C76 keyed that map on `(key property, value)` and left **value-only as a fallback**, with the
      note *"a reference whose principal key property cannot be named keeps exactly the behaviour it
      had"*. **This looks like the case where that fallback resolves to another entity type's
      registration** — corr=2's `-2147482646` finding `OwnedOptional2.Id`'s entry rather than its
      own. That would explain the conflict exactly, and it is a **hypothesis with a named suspect**,
      not a conclusion.

      ## DONE `<this commit>` — 1 fixed, 0 broken, `16 -> 15`

      **The probe confirmed the fallback, and the FIRST fix was measured worse.** Refusing the
      value-only path for every key property measured **0 fixed, 1 broken** — it broke
      `Save_changed_owned_one_to_one`, which J14 had just fixed. That refutation is what found the
      answer: an owned *single*'s key genuinely **is** its owner's, so it is a real reference and the
      fallback is rescuing it when the qualified lookup misses.

      The condition is therefore `property.IsKey() && !property.IsForeignKey()` — refuse the
      fallback only where **no foreign key names the property at all**. Then there is nothing to
      redirect it at, and leaving it alone is correct: `generatedKeys` has already put the client's
      placeholder on the entity and flagged it temporary, which is exactly what the store replaces.

      **C76's deferred question is now answered, and narrowly.** Its fallback was kept for *"a key
      borrowed other than through a foreign key"*; the case it did not foresee is a key that is
      **borrowed by nobody** — an owned collection's own shadow key, which no foreign key names, so
      the qualified lookup cannot match and the fallback hands it another entity type's
      registration:

      ```
      RESOLVE OwnerRoot.Id      client=-2147482647 -> FALLBACK -2147482647 isKey=True
      RESOLVE OwnedOptional1.Id client=-2147482647 -> FALLBACK -2147482647 isKey=True   <- OwnerRoot's
      ```

- [x] ~~**J12. `GraphUpdates` to Tier B — assessed 2026-08-17.**~~ Split into J12a/J12b above.
      **Original assessment:**
      1787 tests, and **the reason not to move it has just gone**. The assessment, not a guess:

      | Fact | Value |
      |---|---|
      | Skips in `GraphUpdatesInfoCarrierTest` today | **0** — so nothing is being retired; this is coverage, not cleanup |
      | Uses `ExecuteWithStrategyInTransactionAsync` | **yes** — so it needs J3's `UseTransaction` override, which is now a known one-liner |
      | Skips in EF's `GraphUpdatesSqliteTestBase` | **6** — mirror them, and read each one first |
      | EF's SQLite `UseTransaction` | `facade.UseTransaction(transaction.GetDbTransaction())` — ADR-013's call, so ours is `facade.UseInfoCarrierTransaction(transaction)` as in J3 |

      **Why it is worth doing and why it was not before.** `GraphUpdates` is the same corpus as
      `ProxyGraphUpdates` without proxies, and on Tier A it has never met a store that enforces a
      foreign key — which is exactly the blind spot J11 was hiding in. J11's defect was live for
      every one of those 1787 tests and none of them could see it. **The move is now expected to be
      largely green rather than reckless**, because the one mechanism that made J3 explode is fixed.

      **Do it in this order, and do not shortcut it:** the `UseTransaction` override *first* and in
      the same change as the store switch — J3 proved that omitting it costs two hours of 30-second
      lock timeouts rather than a fast failure. Then adopt EF's six skips, each checked against
      `subrepos/efcore` rather than assumed. Then measure; expect a rise, and classify it before
      committing.

      **The `ReseedAsync` override should be kept** (it reseeds through the backend, not the client)
      and the `ExecuteWithStrategyInTransactionAsync` override should be **deleted** — that is the
      ConferencePlanner precedent J3 followed, and it held there.

- [x] **J2b. `BuiltInDataTypes` and `ConvertToProviderTypes` to Tier B — measured in halves, both at ZERO cost.** `<this commit>`
      `26 / 22654` unchanged across both, **0 fixed and 0 broken each time**. The file was split so
      each half could be measured alone, which is what makes "zero cost" a fact rather than a hope.

      Both fixtures now carry `BuiltInDataTypesSqliteFixture`'s eight capability flags. **Four change
      value and none is cosmetic** — `StrictEquality`, `SupportsDecimalComparisons` and
      `PreservesDateTimeKind` become `false`, `SupportsBinaryKeys` becomes `true` — and each turns
      assertions on or off inside the base. That they change and nothing breaks is the result.

      **What the move was for**: each class had a silent
      `Optional_datetime_reading_null_from_database() => Task.CompletedTask`, because the InMemory
      store has no null to read. SQLite has one, so both now run, and both pass. Same accounting as
      J12b — an empty override already counted as a passing test, so `total` does not move; the
      difference is that the tick now means something.

      **The earlier price of "2201 lines of EF SQLite surface" was for the wrong thing.** That file is
      overwhelmingly `AssertSql`, which this provider cannot use and does not need. What was actually
      required was eight flag values and two deletions.

- [x] ~~**J2b (original entry).**~~ Superseded above.
      No skips to retire, so this is not J2's argument. What it *would* retire is the silent
      `Optional_datetime_reading_null_from_database() => Task.CompletedTask` in each — a test that
      does nothing at all, because the InMemory store has no null to read. **Priced before
      starting:** `BuiltInDataTypesSqliteTest` is **2201 lines**, so the adoption surface is large
      even though most of it is `AssertSql` this provider cannot use. Worth doing, worth doing on
      its own, and worth measuring in halves.
- [x] **J3. `ProxyGraphUpdates` to Tier B — DONE on the second attempt.** `<this commit>`
      `Total tests: 22672, Passed: 22319, Failed: 182, Skipped: 171` (`j3b`) against
      `22456 / 22229 / 17 / 210` (`j10`). **A deliberate rise of 165, and the largest this file has
      recorded since L1** — which is the right comparison, because it is the same kind: skipped
      tests becoming real ones.

      **The first attempt's diagnosis was wrong, and finding out cost nothing but a grep.** It
      concluded a product feature was missing. **Nothing was missing.**
      `UseInfoCarrierTransaction` and the non-owning `UseTransaction(token)` have shipped since M4.
      What was missing was this class's **`UseTransaction` override** — which
      `ConferencePlannerInfoCarrierTest` and `OptimisticConcurrencyInfoCarrierTest` already carry,
      and whose comment on the first names the exact symptom: *"Without enlisting, the second runs
      on its own SQLite connection and gets 'database is locked'."* **Before pricing a gap, check
      whether a sibling of it already works** — two classes in this same suite did.

      With the hook in place the deadlock is gone completely: **0 `database is locked`**, and the
      run takes **5.6 minutes** instead of the two hours the first attempt was still short of.

      **What the move bought and what it cost.** `skipped` 210 → 171 (the 13 skips × 3 flavours),
      `total` 22456 → 22672 (those 39 becoming 216 real parameterizations), `passed` +90.
      **165 fail, and they are one defect with 165 faces**: every single one is
      `SQLite Error 19: 'FOREIGN KEY constraint failed'`, spread 56 / 56 / 55 across the three
      proxy flavours. That is precisely what the deleted skips were about — EF's #2166 (FK
      constraint checking) and #3924 (cascade delete) are InMemory *not enforcing* either. On a
      store that enforces both, this provider's `SaveChanges` replay does not order or propagate
      deletes the way a relational store requires.

      **This is a large, previously invisible area, not a regression.** `GraphUpdatesInfoCarrierTest`
      — the non-proxy corpus, 1787 tests — is still Tier A, so the whole `GraphUpdates` family has
      never once run against a store that enforces foreign keys. Filed as J11.

      **If this rise is judged too large to hold, reverting is three edits** (store factory, the
      thirteen skips, the `UseTransaction` override) and the base returns to Tier A with EF's own
      mirrored skips — which is an adoption choice, not the test-suppression CLAUDE.md forbids.

- [ ] **J11. A foreign key that references an ALTERNATE key does not survive the replay.**
      **Narrowed 2026-08-17, before any code, and the narrowing is the point.** J3 filed the 165 as
      "cascade delete and foreign-key ordering", which was a guess from one error message. Grouping
      the failing *names* instead says something much sharper:

      | | count |
      |---|---|
      | `ProxyGraphUpdates` failures | 167 |
      | …whose name contains `alternate_key` / `_AK_` | **162** |
      | …that do not | **5** |

      Every large family is `Optional_one_to_one_with_alternate_key_*` or
      `Optional_many_to_one_dependents_with_alternate_key_*`, each at 9 parameterizations (the
      three cascade timings squared). So this is **not** a statement about cascade delete or about
      ordering — both of which apply equally to the primary-key variants, and those **pass**.
      It is: *a foreign key that points at an alternate key rather than at the primary key is not
      resolved correctly on the server.*

      **That is an existing family, not a new one.** C34 and C76 are both "a key resolved by value
      rather than by what declares it", and C76's fix keyed the placeholder map by
      `(key property, value)` and resolved through `foreignKey.PrincipalKey`. An alternate key is
      exactly where `PrincipalKey` stops being the primary key — so the first thing to read is
      whether every path that resolves a reference uses `foreignKey.PrincipalKey`, or whether some
      still assume the primary key.

      **A hypothesis that ordering explains it is already weak** and should not be spent time on
      first: deletes are tracked before everything else and *not* in dependency order
      (`ServerSaveChangesExecutor`, the `Deleted` pass), but EF sorts modification commands
      topologically itself, so tracking order is not what reaches the store.

      **AND THE `PrincipalKey` HYPOTHESIS ABOVE WAS CHECKED AND DOES NOT HOLD. Read this before
      re-deriving it.** Every reference-resolution path in `ServerSaveChangesExecutor` already
      resolves through `foreignKey.PrincipalKey`, not through the primary key:

      | Path | What it does |
      |---|---|
      | `PrincipalPropertyOf` | matches by **position** into `foreignKey.PrincipalKey.Properties` |
      | the reference redirect | keys `qualifiedPlaceholders` on `(foreignKey.PrincipalKey.Properties[index], clientValue)` |
      | the generated-key read-back | asks `PrincipalPropertyOf(fk, property)` for `ValueGenerated` |

      The only `FindPrimaryKey()` in the file is inside an identity-conflict **diagnostic**, which
      cannot cause a store error. So C76's fix is not incomplete here, and the defect is somewhere
      else.

      **PROBED 2026-08-17. The sentinel theory is refuted, and the real shape is now visible.**
      A temporary instrument in `ChangeEntryMapper.ToChangeEntry` printed every key and foreign-key
      property of every entry leaving the client, for
      `Optional_many_to_one_dependents_with_alternate_key_are_orphaned` (27 of 27 failing):

      ```
      CLIENT OptionalAk1        state=Deleted   Id=1  AlternateId=3e3db6de…  ParentId=a2276653…
      CLIENT OptionalAk2        state=Modified  Id=1  ParentId=<null> modified=True explicit=False
      CLIENT OptionalComposite2 state=Modified  Id=1  ParentId=<null>          modified=True
                                                      ParentAlternateId=3e3db6de… modified=True explicit=True
      ```

      **Two things this settles outright.**

      1. **A nulled foreign key travels correctly.** `OptionalAk2.ParentId` leaves as `<null>` and
         is flagged `modified=True`. It is *not* dropped as "unset", so
         `SentinelProperties`/`HasExplicitValue` is **not** the mechanism. Do not re-derive this.
      2. **The row the store rejects is identified.** `OptionalAk1` — the principal — is `Deleted`,
         and its alternate key is `3e3db6de…`. `OptionalComposite2.ParentAlternateId` **still holds
         `3e3db6de…`** while its sibling `ParentId` on the same entry has been nulled. A foreign key
         still pointing at a row being deleted is exactly what SQLite refuses, and it explains the
         `alternate_key` correlation precisely: the primary-key FK is nulled, the alternate-key FK
         is not.

      **The "offending row" reading above was itself wrong, and the model says so.**
      `OptionalComposite2.ParentAlternateId` is a **non-nullable `Guid`**, and its foreign key to
      `OptionalAk1` is **composite** — `(ParentAlternateId, ParentId)`. A composite foreign key with
      any NULL component is not enforced, so leaving `ParentAlternateId` set while nulling
      `ParentId` is *correct*, and the client is right. Read the model before blaming a value.

      ## ROOT CAUSE — established 2026-08-17, three probes, no theory left

      **1. EF names the entry it blames.** Catching the failure inside
      `ServerSaveChangesExecutor` and printing `DbUpdateException.Entries`:

      ```
      SAVE-FAILED DbUpdateException   BLAMED OptionalAk1 state=Deleted
      TRACKER: OptionalAk2 Modified ×2, OptionalComposite2 Modified ×2, OptionalAk1 Deleted
      ```

      So the `DELETE` of the principal is what the store refuses, while its dependents are present
      and correctly nulled. The replay is **faithful** — the server tracks exactly the five entries
      the client sent, with the same states and the same values.

      **2. The server's ORIGINAL values are the defect.**

      ```
      SERVER OptionalAk2 state=Modified | Id=1 orig=1 | ParentId=<null> orig=<null> mod=True
      ```

      `ParentId` is `<null>` **and its original is `<null>` too** — while the row in the store holds
      `1`, pointing at `OptionalAk1`. **The server believes this foreign key was always null.**

      **3. Why, and it is written in the code as a deliberate fact.** `ChangeEntryMapper` sends
      original values for **concurrency tokens only** — `if (carriesOriginals && property.IsConcurrencyToken)` —
      and its comment states the reasoning: *"the server rebuilds the entity from the current
      values, attaches it and sets `Modified`, so every original it has equals its current one by
      construction"*. That is true, and for the concurrency check it is right. For **command
      ordering it is wrong**: EF's `CommandBatchPreparer` builds its dependency graph from
      *original* foreign-key values, because that is what tells it a dependent is *releasing* a
      principal. With `original == current == null` there is no edge from `OptionalAk2` to
      `OptionalAk1`, EF has no reason to order the `UPDATE` before the `DELETE`, and the `DELETE`
      meets a row that still points at it.

      **Why this could only ever surface here.** Tier A cannot show it — InMemory enforces no
      foreign keys, which is exactly what EF's #2166 and #3924 skips say. A single-context EF has
      the true originals from the moment it loaded the row. **Only a two-context provider against a
      store that enforces constraints can lose them**, so 1787 `GraphUpdates` tests have never
      exercised this and neither had anything else.

      ## THE WAY FORWARD

      **Send original values for foreign-key properties of a `Modified` entry**, alongside the
      concurrency tokens already sent. The channel exists on both halves —
      `ChangeEntry.SerializedOriginalValues`, applied by `ServerSaveChangesExecutor` to
      `entry.Property(...).OriginalValue` — so this widens *what* is put in it, and adds no wire
      shape and no protocol change.

      **Three constraints on the implementation, all already documented in the code it touches:**

      - **Order matters within the payload.** The originals are mapped *after* the current values,
        because a `byte[]` travels as a referenceable object and the definition must precede the
        back-reference (*"Dangling wire reference 1"*). Keep that order.
      - **Apply originals last on the server.** Setting the state re-snapshots originals from the
        entity, so anything written earlier is undone. The existing block is already last.
      - **Do not widen beyond foreign keys.** C42 measured "send every propagated foreign key back"
        at 1 fixed / 2 broken; the symmetric temptation here is to send every original. Send
        exactly the foreign-key properties, and only for `Modified` entries.

      ## DONE `<this commit>` — 167 fixed, 0 broken, `182 -> 15`

      `Total tests: 22672, Passed: 22486, Failed: 15, Skipped: 171` (`j11`). **`ProxyGraphUpdates`
      is GREEN**, and J3's deliberate rise of 165 is repaid with two to spare. One line in
      `ChangeEntryMapper`: a `Modified` entry now carries the originals of its **foreign-key**
      properties as well as its concurrency tokens.

      **The alternate-key correlation was a red herring, and the measurement says so.** All five
      non-alternate-key failures — `Avoid_nulling_shared_FK_property_when_deleting` ×3 and
      `Save_two_entity_cycle_with_lazy_loading` ×2 — closed too. So it was **one cause with 167
      faces**, not 162 plus 5, and the name-grouping that narrowed the search also over-narrowed the
      conclusion. Grouping by name is how to find a defect; only the fix says how far it reached.

      **The residual question below is now answered as far as it needs to be.** Why the
      primary-key variants passed *before* is still not fully explained — but `BROKEN: 0` across
      22,672 tests establishes the fix is general and costs nothing, which is what the question was
      guarding against.

      **      **The one thing still unexplained, and it is the check on any fix**: why the *primary-key*
      variants of these same tests pass. The mechanism above is not specific to alternate keys, so
      either those pass for an unrelated reason — a plausible candidate is that EF declares
      `ON DELETE SET NULL` for the simple optional FK and SQLite then repairs it whatever the order
      — or something narrows it. **Confirm that before believing the fix is complete**: a fix that
      closes 162 and leaves the mechanism half-understood is how a wrong revert starts.

      **The 5 that are not alternate-key are separate and small**:
      `Avoid_nulling_shared_FK_property_when_deleting` (×3) and
      `Save_two_entity_cycle_with_lazy_loading` (×2). Do not fold them in.

      The move itself was the same three lines as J1 and J2: store factory to `Sqlite`, delete the
      thirteen skips, keep the by-hand reseed. The run was stopped at **21,289 of 22,453** after
      about two hours, with **733 distinct failures, 717 of them this class**. Reasons, tallied:

      | Count | Reason |
      |---|---|
      | 471 | `InfoCarrierServerException : SQLite Error 5: 'database is locked'` |
      | 246 | `DbUpdateException` (the same lock, one frame out) |

      **The cause is one mechanism, not 717 findings.**
      `TestHelpers.ExecuteWithStrategyInTransactionAsync` opens **one** transaction on an outer
      context and then hands every inner context to `useTransaction(innerContext.Database,
      transaction)`. `ProxyGraphUpdatesSqliteTestBase` satisfies that with
      `facade.UseTransaction(transaction.GetDbTransaction())` — **ADR-013's call**, which a client
      that is never a relational context cannot make. Our `UseTransaction` is therefore a no-op:
      the inner contexts run *outside* the transaction while the outer one holds SQLite's write
      lock, and each one waits out a **30-second** lock timeout before failing. That timeout is
      why a normally six-minute suite ran for hours, and it is the same "an abandoned transaction
      wedges writes" behaviour [`roadmap.md`](../roadmap.md) §M8 records from the other direction.

      **On Tier A none of this is visible**, because the transaction is ignored outright and
      `ReseedAsync` puts the data back by hand. The InMemory skips were a true statement about the
      InMemory store *and* an accidental screen over a second, larger dependency.

      **What would unblock it: a client-side way to join an open server transaction by its wire
      token.** `IInfoCarrierClient.BeginTransactionAsync` already returns that token and every
      request record already carries `TransactionId`, so the missing piece is one client API —
      plus the authorization question §M8 raises, because today any caller holding a token can join.
      Filed as J7 below rather than folded in here: it is product work with a security question
      attached, and it deserves its own measurement.

      **The check that would have caught this in a grep**, now also in `CLAUDE.md`: before moving a
      base to Tier B, grep it for `ExecuteWithStrategyInTransactionAsync`. `GraphUpdates` and
      `ProxyGraphUpdates` are on Tier A because of it, not by accident.

- [ ] **J7. Let a client context join an open server transaction by token.**
      What J3 needs. One client-side API over the existing wire — no protocol change, since the
      token is already returned and already carried.
      **The blocking objection recorded here on 2026-08-16 does not survive checking, and the
      correction is the useful part.** It said a token is a bearer credential and that "who may
      join" had to be answered before shipping. The first half is true; the second does not follow.
      `InProcessInfoCarrierServer.Acquire` already runs **any** request naming a token on that
      transaction's context, and every request record already carries `TransactionId` — so the
      exposure is a property of the wire protocol as it stands, and **this API widens nothing**.
      Binding a token to its creator stays worth doing and stays M8's item.
      What does need deciding is ownership: a joined transaction must not commit or roll back on
      dispose. [`architecture.md`](../../../architecture.md) §6a **D6**.

### J4 — the test project organised by backend store

v1's layout (`InMemory/`, `SqlServer/`, `TestUtilities/`, root for store-independent tests), which
makes a store's coverage countable by looking at the tree. A **pure move**: `eng/measure.sh` must
return the same failure count and total with empty FIXED, BROKEN and REASONS diffs.
`test/known-failures.txt` holds fully-qualified names, so it moves in the same commit.

The census that sizes it, taken by resolving each file's fixture to its backend store rather than
by grepping names (42 `Scaffolding/Baselines/**` files are excluded from compilation and are not
counted):

| Backend | Files |
|---|---|
| InMemory (Tier A) | 61 |
| SQLite (Tier B) | 24 |
| Store-independent (`Expressions/`, `ProjectionSplit/`, compliance, infrastructure) | 26 |
| Shared harness (`TestUtilities/`, used by both) | 4 |

- [x] **J4. Reorganise `test/InfoCarrier.Core.FunctionalTests` by backend store.** `<this commit>`
      `Total tests: 22453, Passed: 22225, Failed: 18, Skipped: 210` (`j4`) — **all four figures
      identical to `j2b`**, and `REASONS: unchanged`.

      **The neutrality proof needed one extra step, because the failing test *names* necessarily
      move.** `measure.sh` snapshots fully-qualified names, so a namespace change makes FIXED and
      BROKEN both non-empty by construction — 18 names leave, 18 arrive. Stripping the inserted
      store segment and diffing the two snapshots gives **no differences at all**: the same 18
      tests, failing for the same reasons, before and after.

      The layout, after v1's (`InMemory/`, `SqlServer/`, `TestUtilities/`, root):

      | Location | Files | What |
      |---|---|---|
      | `InMemory/` | 57 | Tier A test classes, sub-structure kept (`Query/`, `Update/`, `Scaffolding/`) |
      | `InMemory/Scaffolding/Baselines/` | 42 | not compiled; travels with its test — see below |
      | `Sqlite/` | 25 | Tier B test classes (`Query/`, `Query/Associations/`, `Update/`, `Types/`, `BulkUpdates/`) |
      | `TestUtilities/` | 16 | the harness, **kept whole** — it defines both store factories, so it is shared by construction |
      | root, `Expressions/`, `ProjectionSplit/`, `ModelBuilding/` | 18 | store-independent: wire format, boundary analysis, compliance, infrastructure |

      **Four things this turned up that a rename alone would not have:**

      1. **`test/known-failures.txt` needed no change.** It holds `failed=`, `total=` and prose —
         **no test names at all** — so namespaces cannot move it. The expectation that it would was
         wrong, and checking took one grep.
      2. **`Scaffolding/Baselines/` has to travel with its test.** `CompiledModelTestBase`
         locates it from `[CallerFilePath]` on `AddReferences`, which our
         `CompiledModelInfoCarrierTest` overrides — so the baselines follow that *file*, not the
         project. The two `csproj` lines moved with it, and getting that wrong reproduced CLAUDE.md's
         documented **125 duplicate-definition errors** immediately, which is a good tripwire.
      3. **One file was split.** `BuiltInDataTypesInfoCarrierTest.cs` held three classes across two
         tiers after J2. `CustomConverters` is now `Sqlite/CustomConvertersInfoCarrierTest.cs`.
      4. **A shared helper crossed a tier and my grep missed it.** `AssociationsWarnings` is
         `internal static class`, which the cross-reference script's pattern did not match, and it
         is used by two Tier B classes. **The compiler is the reliable cross-reference checker**;
         the grep is only for planning. Every other cross-tier "reference" the script did find —
         eight of them — turned out to be prose in `<c>` tags.

### J1/J2 residual — all five classified

The two tier moves raised `failed` 13 → 18. Diffing the current run against the M9 baseline gives
**exactly five new, none gone**, and every one is this provider's rather than the backing store's.
Classified here so the count keeps meaning something.

**One is not a gap at all.**

| Test | Verdict |
|---|---|
| `CustomConverters.Collection_enum_as_string_Contains` | **A28 family. Red forever, correctly.** The base's whole body is `Assert.Throws<InvalidOperationException>` around the query; this provider answers it. **Probed rather than assumed**, because "no exception" and "wrong answer" look identical from the count: with one seeded row, `Seller` returns `server=1, client=1` and `Customer` returns `server=0, client=0`. A filter that matched everything would have shown `Customer: server=1`. The answer is right. |

**Two are one defect with two faces — a captured constant whose CLR type the wire cannot
round-trip.** Both are *query constants*, not stored values, and both are a **key behind a value
converter** that travels by reflective object shape instead of as its provider value:

| Test | Which face |
|---|---|
| `KeysWithConverters.Can_insert_and_read_back_with_enumerable_class_key_and_optional_dependents` | **Outbound.** `EnumerableClassKey` implements `IEnumerable`, so `DynamicValueMapper.MapToNode` takes its **collection** branch and calls `GetEnumerator()`, which EF's test type does not implement. Reached from `ExpressionToNodeTranslator.VisitConstant`. |
| `KeysWithConverters.Can_query_and_update_owned_entity_with_value_converter` | **Inbound.** `protected class Key(string id) { public string Value { get; } }` — no parameterless constructor, one get-only member — so the **server** cannot rebuild it: `MissingMethodException: Cannot dynamically create an instance … No parameterless constructor defined`, surfaced through `RoundTripAsync`. |

**ADR-012's seam is the shipped answer and it does not fit here**, which is the finding. Its two
standard mappers are *BCL* types whose members throw for ordinary instances (`IPAddress.ScopeId`,
`Uri.AbsolutePath`) — an application storing one "has opted into nothing". These two are the
**application's own** types, and the model already says exactly how they become primitives: a value
converter. **The open question is whether a constant whose CLR type matches a mapped property type
should travel as its provider value**, the way `ChangeEntryMapper` already sends property values
(A19). That is a design question, not a bug fix, and it is filed as J9 below.

**One is a code path that had never run before.**

| Test | Verdict |
|---|---|
| `CustomConverters.GroupBy_converted_enum` | **The first non-composed `GroupBy` this provider has ever been asked to carry.** `context.Set<Entity>().GroupBy(e => e.SomeEnum).ToList()` returns EF's `GroupBySingleQueryingEnumerable+InternalGrouping<,>`, which the deserialization allowlist refuses. It could not have surfaced before: `NorthwindGroupByQuery` is **Tier A**, and InMemory refuses a non-composed `GroupBy` outright — which is precisely the override J2 deleted. Not a regression; a gap that Tier A was standing in front of. |

**One needs one more probe, and the theorising stops here.**

| Test | What is known |
|---|---|
| `CustomConverters.Value_conversion_is_appropriately_used_for_join_condition` | The test joins on **anonymous types**. The tree that reaches SQLite has, for *both* key selectors, `(object)new ValueTuple<int?, bool, int>(…)` — a **boxed** tuple, which relational translation refuses. So the anonymous key was correctly re-carried as a `ValueTuple` and then boxed. **The boxing is evidently not ours**: neither `TransparentIdentifierRewriter` nor `ProjectionRewriter` contains a single `Expression.Convert`, and `TupleCarrier` contains no `typeof(object)`. Where it comes from is unresolved. **The probe is the standing one** — print the boundary verdict and the shipped tree in `QuerySplitter.Split` and compare it with the raw captured tree, which answers "ours or already in the input" in one filtered run. |

- [ ] **J8. Close `GroupBy_converted_enum`.** Designed 2026-08-17, not implemented.
      The server returns `GroupBySingleQueryingEnumerable+InternalGrouping<,>` and the allowlist
      refuses it — correctly, since it is an EF internal type. **Three answers, and the third is
      the one to try first:**

      | # | Answer | Note |
      |---|---|---|
      | a | Carry `IGrouping<K,V>` as a wire shape | The most work, and it puts an EF-shaped concept in the protocol. |
      | b | Refuse at the boundary with a sentence naming the reason | Honest, cheap, and leaves the test red — a worse answer than (c) if (c) works. |
      | c | **Cut below the `GroupBy` and let the client group** | The rows are shippable; only the *grouping* is not. The client already applies a residual, and grouping in memory over shipped rows is exactly what a residual is for. |

      **The trap in (c), and why it needs care rather than a one-liner:** the refusal must apply
      only when the **final result element** is a grouping. Marking `IGrouping<,>` non-shippable
      per *node* would cut every aggregate `GroupBy` too — the ones that must stay on the server —
      and those are a large, currently-green family. The check belongs on the query root, not in
      `ServerBoundaryAnalyzer`'s per-node verdict. Measure before believing either way.

- [~] **J9. A query constant now travels as its provider value — 1 of 2 closed.** `<this commit>`
      `Total tests: 22672, Passed: 22487, Failed: 14, Skipped: 171` (`j9`): **1 fixed, 0 broken**.
      ADR-012 carries the dated amendment the user approved, and
      `ValueMapping.ModelConverterValueMapper` is the mapper it permits — built inside
      `DynamicValueMapper` from the model it was given, so **symmetry is structural**: each half
      derives it from its own model and it cannot be present on one side only.

      **Closed:** `Can_query_and_update_owned_entity_with_value_converter`. The inbound face —
      `class Key(string id)`, no parameterless constructor — now arrives as the string the model
      always said it was.

      **Still open, and it is a NEW failure rather than the old one:**
      `Can_insert_and_read_back_with_enumerable_class_key_and_optional_dependents` no longer throws
      `NotImplementedException` from `GetEnumerator()`. It now throws

      ```
      InvalidCastException : Unable to cast object of type
        'EnumerableClassKey[…InfoCarrierFixture]' to type 'IntClassKey[…InfoCarrierFixture]'
      ```

      **The mapper is selecting the wrong converter.** `_byClrType` is keyed by
      `converter.ModelClrType`, and something makes an `EnumerableClassKey` match `IntClassKey`'s
      entry. The likely cause — **unverified, do not act on it without checking** — is that these EF
      test key types share a base and a converter's `ModelClrType` is the base rather than the leaf,
      so one dictionary entry answers for several types. C53 is the precedent to read first: it is
      the same shape one level up (*"a member declared on a base class the model never names"*), and
      its rule was **base classes only, never a category**.

      **A guard was tried and measured INERT, which is itself the finding.** Declining when
      `!converter.ModelClrType.IsInstanceOfType(value)` measured **0 fixed, 0 broken, REASONS
      unchanged** — so `IsInstanceOfType` *passes*: the value genuinely is an instance of the
      converter's model type, and the `InvalidCastException` is raised **inside**
      `ConvertToProvider` by the converter's own body. The guard was reverted rather than kept:
      inert code carrying an unverified explanation is worse than no code.

      **So the base-type suspicion is half right and the conclusion drawn from it was wrong.** The
      declared type and the converter agree; what disagrees is the converter's *internal* cast. That
      points at a converter declared for one key type whose body targets another — read EF's
      `KeysWithConvertersFixtureBase` configuration for these two types before touching the mapper
      again.

      **CORRECTED again after reading EF's configuration.** `EnumerableClassKey.Converter` is
      `ValueConverter<EnumerableClassKey, int>` and `IntClassKey` has its own — the two are distinct
      dictionary keys, and `EnumerableClassKey` does not derive from `IntClassKey`. So the mapper is
      **not** selecting the wrong converter, which is why the guard was inert.

      **The likely reading now: the mapper worked, and the test simply got further.** Its body is
      `RunQueries`, which runs many queries; the original `NotImplementedException` came from the
      first. With that closed, a later one fails for an unrelated reason. That reframes this from "my
      mapper is wrong" to "one more defect exists behind it", and it is a **single test** — much
      lower value than J12's 1787.

      **CORRECTION, 2026-08-17: "the guard measured inert" was over-read, and the method matters.**
      That conclusion came from `FIXED: 0, BROKEN: 0, REASONS: unchanged`. Those three cannot
      distinguish *"the guard never fired"* from *"the guard fired, declined correctly, and something
      further along threw an `InvalidCastException` too"* — because the test fails either way and the
      reasons tally is grouped by **first message line**. **A tally is not a trace.** The guard may
      have been right; that run does not say.

      What the run *does* establish is that the failure is a **server fault on the query path**
      (`QueryDataAsync` → `RoundTripAsync` rethrow), and that it survives J13 and J14 unchanged.

      ## TRACED 2026-08-17 — two suspects cleared, and the previous "inert" reading was the lesson

      Re-run **with a trace instead of a tally**, which is what the correction above asked for.

      **1. `ModelConverterValueMapper` is not at fault, and this is now traced rather than argued.**
      Every one of its **7** calls in this test is:

      ```
      TOWIRE declared=EnumerableClassKey value=EnumerableClassKey
             converterModel=EnumerableClassKey accepts=True
      ```

      Never a mismatch, so the guard that was reverted had genuinely nothing to decline — the
      "inert" verdict was right by luck and for the wrong reason. **The tally could not tell those
      apart; the trace can.**

      **2. It is not a rehydrated server fault either.** A probe inside
      `InfoCarrierFaultMapper.Rehydrate` logged **nothing at all** across the run, so no
      `InfoCarrierFault` was ever built — despite the client stack naming
      `TransportInfoCarrierClient.cs:103`, which *is* the `throw Rehydrate(fault)` line. **A stack
      line inside an `async` method is attributed to the state machine and is not reliable**; the
      probe outranks it.

      **What is left, and it is now a small space.** The `InvalidCastException` is raised somewhere
      that applies **one entity type's converter to another's value** —
      `EnumerableClassKey` handed to `IntClassKey`'s converter — and it is neither the value-mapper
      chain nor the fault path. `PrimitiveCoercion.ToWireValue(property, value)` with a `property`
      from the wrong entity type is the remaining shape, which would make it a
      **result-serialization** defect rather than a constant-mapping one.

      ## CLOSED as an UPSTREAM DEFECT, 2026-08-17. Nothing here to fix.

      The server's own stack, once a probe inside `InfoCarrierFaultMapper.Rehydrate` finally printed
      it, names EF's test model:

      ```
      FAULT System.InvalidCastException: Unable to cast object of type 'EnumerableClassKey[…]'
                                         to type 'IntClassKey[…]'
        at KeysWithConvertersTestBase`1.EnumerableClassKey.Equals(Object obj)
        at Query.ExpressionEqualityComparer.ExpressionComparer.CompareConstant(…)
      ```

      And the type says it outright — `KeysWithConvertersTestBase.EnumerableClassKey`:

      ```csharp
      public override bool Equals(object obj)
          => obj == this
              || obj?.GetType() == GetType()
                  && Equals((IntClassKey)obj);      // <- should be (EnumerableClassKey)obj
      ```

      **A copy-paste bug in EF's own test type.** The guard `obj?.GetType() == GetType()` passes —
      both objects really are `EnumerableClassKey` — and the cast to `IntClassKey` then throws. Any
      caller that compares two of these by `Equals(object)` gets an `InvalidCastException`.

      **Why only this provider reaches it.** The caller is EF's `ExpressionEqualityComparer`, which
      builds the compiled-query cache key and compares `ConstantExpression`s by `Equals`. Under
      ADR-006 the key value survives in the tree **as a constant**; EF's own providers parameterize
      it before the cache key is taken, so they never call `Equals` and never see the bug.

      **Left red and classified**, which is what CLAUDE.md prescribes. EF ships no override for it —
      the test passes for them — so there is none to adopt, and `subrepos/` must not be edited.
      Same family as #30730 and #33522: someone else's defect, reached by a legitimate route.

      ## The method lesson, and it cost three wrong conclusions in a row

      Three successive "NOTHING LOGGED" results were read as evidence. **All three were a stale
      binary**: the fault-mapper probe named `fault.ExceptionType`, which does not exist
      (`TypeName` does), so every build after it failed — and the runs used the previous assembly.
      The build output was checked with `Select-Object -Last 2`, which showed `Time Elapsed` and hid
      `1 Error(s)`.

      That is CLAUDE.md's oldest rule — *establish that the code ran* — broken three times in one
      sitting, and it produced two confident false clearances (`Rehydrate` "never called", request
      serialization "cleared"). **A probe that prints nothing is only evidence once the build is
      known green.** Check the error count, never the elapsed time.
      Closes both `KeysWithConverters` failures, which are one defect with two faces (above).

      **The natural mechanism already exists and the contract forbids using it.** ADR-012's
      `IInfoCarrierValueMapper` maps "a CLR type the wire cannot walk" to a primitive and back,
      in both directions, on both halves — precisely what these two constants need. But ADR-012 is
      **LOCKED**, and states the contract *"in terms of the CLR type alone: neither side may
      consult a type mapping to decide"*. Deriving mappers from the model's value converters
      consults exactly that.

      **The distinction that might justify an amendment, and it is the same one B12/C80 and J5
      already rest on.** ADR-012's clause was written against B23, where sending a scalar through
      EF's core `ValueConverterSelector` inside `PrimitiveCoercion.Coerce` cost **381** — a
      *store* type mapping, which the two providers genuinely compute differently. A converter
      configured in `OnModelCreating` is not that: it is **shared model configuration**, identical
      on both sides by construction, and J5's seam exists because *"where a key shape is decided by
      the caller's own model configuration rather than by the store, the client has to reach the
      same answer as the server"*. If that reading holds, the amendment is narrow: *a converter the
      model declares is not a type mapping.*

      **Do not proceed without deciding that explicitly.** CLAUDE.md: reversing or amending a
      LOCKED ADR requires a dated supersession edit in `decisions.md`, never a code change that
      quietly contradicts it. Options, if it is taken: (i) derive mappers from the model's
      converters and register them automatically on both halves; (ii) leave ADR-012 alone and give
      the *constant* path its own model lookup, which duplicates the idea in a second place;
      (iii) do nothing and classify both tests as permanent. **(i) is the recommendation** — it
      reuses the seam, needs no wire change, and keeps one mechanism rather than two.
- [x] **J10. The join key was never boxed, and the fix is one line.** `<this commit>`
      `Total tests: 22456, Passed: 22229, Failed: 17, Skipped: 210` (`j10`): **1 fixed, 0 broken**.

      **Every theory in the entry this replaces was wrong, and the probe said so in four lines.**
      The tree is clean at *all four* stages — captured at `Split`, after `ReCarryInternalTypes`,
      after `ProjectionRewriter`, and rebound on the server — each printing
      `new ValueTuple\`3(Item1 = …, Item2 = …, Item3 = …)` with no `Convert` anywhere. **The
      `(object)` in EF's message is EF's own rendering of a key it could not decompose**, not
      something anyone boxed. Reading it as boxing cost this entry two rounds of confident
      reasoning about where the cast came from.

      **The real cause, proved with no InfoCarrier in the probe at all.** A plain SQLite context,
      the same join written three ways:

      | Join key shape | EF's own SQLite provider |
      |---|---|
      | anonymous type | **TRANSLATED** |
      | `ValueTuple<int?, bool, int>` (with `NewExpression.Members` supplied) | **refused** — *"could not be translated"* |
      | `Tuple<int?, bool, int>` | **TRANSLATED** |

      So the limitation is EF's, and ours was only in picking the shape that trips it: the re-carry
      that keeps a join *on* the server was simultaneously making it untranslatable. Supplying
      `Members` — the fix that was worth 214 tests elsewhere — is **not** enough for a join key.

      The change adds the join key's type to `_referenceTyped`, the mechanism that already exists
      for a carrier that must stay a reference type. Deliberately **not** applied to a `GroupBy`
      key: nothing has been measured there.

### J5–J6 — D3 answer (c), in two steps

- [x] **J5. The document seam, and the package reference leaves with it.** `<this commit>`
      `Total tests: 22456, Passed: 22228, Failed: 18, Skipped: 210` (`j5c`) against
      `22453 / 22225 / 18 / 210` (`j4`): **FIXED and BROKEN both empty, REASONS unchanged**, and
      `total` rises by exactly the three new pin tests, all passing.
      **`InfoCarrier.Core.csproj` no longer references `Microsoft.EntityFrameworkCore.Relational`.**

      `Metadata.IInfoCarrierDocumentMapping` asks the one question — *is this type stored inside
      one document belonging to something else?* — plus the two things that vary with the answer:
      which annotations can change it, and what the store calls the synthesized ordinal. The
      default, `AnnotationDocumentMapping`, reads the relational annotation **by string name**
      (D3 answer (c), string-default variant, chosen 2026-08-16).

      **The three D3 pins were checked positively, not inferred from a stable count**, because
      B12's symptom was wrong data with no exception: `JsonQuerySqlite` **393 passed / 0 failed**,
      `JsonOwnedCollectionUpdate` **5 / 0**, `ComplexCollectionJsonUpdate` **18 / 0**, and
      `The_two_models_agree_on_the_key_of_every_JSON_mapped_owned_collection` passed.

      **Four things this cost, and three of them were only findable by running it:**

      1. **`GetContainerColumnName()` is not an annotation read — it is a *walk*.** It falls back
         through the ownership chain for an entity type and through the declaring type for a
         complex type, so a nested owned type inherits its container. Reading the annotation on
         the type alone answers `null` for every nested type, which is B12 one level down.
      2. **`RelationalKeyDiscoveryConvention.SynthesizedOrdinalPropertyName` had to go too.** It is
         a `const`, so it is inlined at runtime — but naming the type still needs the assembly at
         compile time. It is now on the seam, which is where it belongs: CLAUDE.md already records
         that Cosmos recognises the ordinal by the property's *shape* rather than by this name.
      3. **EF refuses a provider's own service through `EntityFrameworkServicesBuilder`.** Its
         `TryAdd` validates against EF's list of service contracts, and routing this one there put
         *"The database provider attempted to register an implementation of the
         'IInfoCarrierDocumentMapping' service"* on **21,991** tests in a single run. It registers
         on the plain collection instead, exactly as ADR-012's value mappers do.
      4. **A `const` became a property, and `ApiConsistencyTest` caught it.**
         `InfoCarrierKeyDiscoveryConvention.SynthesizedOrdinalPropertyName` is now `virtual`;
         without that, `Public_inheritable_apis_should_be_virtual` failed and was the entire
         difference between 19 and 18.

      **`DocumentMappingPinTest` is the price of naming the string, and it was watched failing.**
      Two assertions compare the strings to EF's constants; the third walks a real `ToJson()` model
      and compares `FindContainerName` with `GetContainerColumnName()` for **every** entity and
      complex type. Deliberately removing the ownership-chain fallback made it fail with
      `Expected: "Items", Actual: null` — D1's rule, that the assertion you never watched fail is
      the one to distrust. The test asserts non-vacuity directly too: one type outside a container,
      two inside, and the nested one reachable only through the walk.
      A provider-neutral *"is this type mapped to one document?"* question, answered by the
      relational implementation behind it. `ServerSaveChangesExecutor.IssuedAtSave` is the shape to
      copy: it asks the backend a capability question rather than testing for a store family.
      **A green build is not evidence here.** B12's symptom was wrong data with no exception, so
      the pins are `JsonQuery` at 0 failures, `JsonOwnedCollectionUpdate` at 5 of 5, and
      `The_two_models_agree_on_the_key_of_every_JSON_mapped_owned_collection`.

- [x] **J6. Ask the backend what it can evaluate — a second axis, beside the allowlist.**
      **DECIDED 2026-08-17: answer (c), no mechanism in M9.** The decision, its argument, and the
      table of coarse-versus-fine facts that is its actual deliverable are in
      [`architecture.md`](../../../architecture.md) §6a **D5**; the exit criterion is restated in
      [`roadmap.md`](../roadmap.md). Two things are worth repeating here because they are what decided
      it: **(b) cannot express J10** — a `ValueTuple` join key refused where an anonymous type and a
      `Tuple` are accepted is a property of the *tree shape*, and no provider manifest carries that
      — and **the criterion as originally written required a second backend**, which M9 puts out of
      scope, so it could only ever be met by changing the milestone. Restated rather than dropped.
      **Rescoped 2026-08-17; the earlier wording here said "replacing the fixed boundary allowlist"
      and that would have been a security regression.** `TypeAllowlist` is ADR-008 constraint 2 —
      an RCE control whose own summary describes the alternative as *"a remote-code-execution vector
      the moment a network transport exists"*, and whose safety `security-review.md` §2 calls a
      conjunction. A backend must never widen it. The missing axis is separate and only ever
      **narrows** what is shipped: *can the thing at the other end evaluate this?*
      Four candidate answers, the difficulty of the automatic one, and why nothing is blocked on it
      today are in [`architecture.md`](../../../architecture.md) §6a **D5**. **Design first, as D3 was.**

### Recorded, not scheduled

- **D4's two chained-InfoCarrier defects.** The probe stays out of the suite so the baseline keeps
  meaning "inherited spec tests failing"; it lives outside the repo, and D4 records what it printed.
- **A third store.** Cosmos is the recommended candidate — first-party EF Core 10 provider
  (`src/EFCore.Cosmos` is in the EF tree), an emulator in one container, and 155 test files in
  `EFCore.Cosmos.FunctionalTests` to check our overrides against, which is the method CLAUDE.md
  depends on and which no other candidate offers. MongoDB is cheaper to run and has no EF suite at
  all. Adopting one is its own milestone, and J5/J6 are what make it cheap.

### The residual 13, examined properly (2026-08-17)

**A first pass at summarising these was wrong in three ways and is corrected here.** It claimed
"3 upstream", "2 A28 family" and "4 singletons". Read off the run instead:

| Test | × | First message | Standing |
|---|---|---|---|
| `Can_track_entity_with_complex_property_bag_collections` | 2 | `ArgumentException … get_Item(System.String)` | **UPSTREAM, on a path only this provider takes (J22).** EF's own `StructuralTypeMaterializerSource` builds `Expression.Property(instance, Item[string])` with no index argument. Route priced and not taken — see J22. |
| `Correlated_collection_with_distinct_3_levels` | 2 | `Assert.Equal() Values differ` | **A28 family, and the old wording overstated it (J22).** **Every** EF provider refuses this query, so the base's expected result was never validated against any answer. |
| ~~`Regex_IsMatch`, `Regex_IsMatch_constant_input`~~ | ~~2~~ | LINQ not translated | ~~A46's deliberate allowlist refusal~~ — **reversed and fixed by J20.** `Regex` is admitted; `security-review.md` §4a carries the decision. |
| ~~`Can_insert_and_read_back_with_enumerable_class_key…`~~ | ~~1~~ | `InvalidCastException` | ~~**Upstream**, and the only one~~ — **the bug is still upstream, and J21 removed our path into it.** EF's `EnumerableClassKey.Equals` still casts to `IntClassKey`; the server's query cache no longer meets it. |
| ~~`Parameter_collection_null_Contains`~~ | ~~1~~ | LINQ not translated | ~~SQLite-tier~~ — **the label was wrong and J19 fixed it.** No reference provider refuses this; it was ours, and one clause. |
| `Update_with_invalid_lambda_in_set_property_throws` | 2 | LINQ not translated | **Settled by J16, no code.** Right type, right verdict, EF's wording of a different true fact. Three siblings green. |
| `Casts_are_removed_from_expression_tree_when_redundant` | 1 | ~~`Assert.Throws: Exception type was not an exact match`~~ → `Assert.Equal: Strings differ` (J18) | **Re-diagnosed by J17; the entry below it used to carry was wrong.** Not cast elision — a type-argument boundary, guarded in J18. What is left is EF's *printed* message, which ADR-006 cannot reproduce. |
| `Collection_enum_as_string_Contains` | 1 | `Assert.Throws: No exception was thrown` | **A28, verified** by probe in J2's triage: `Seller` → 1/1, `Customer` → 0/0, so the server really filters. |
| `Composition_over_collection_of_complex_mapped_as_scalar` | 1 | `Assert.Throws: No exception was thrown` | ~~**NOT verifiable here**~~ — **A28, verified by J15.** |

- [x] **J16. `Update_with_invalid_lambda_in_set_property_throws` — settled, and the sibling check
      settled it.** `<this commit>` Docs only; no code, no gate.

      **Three siblings of the same shape are green, and one is red.** Read off `j8b`, and this is
      the whole finding:

      | Test | `j8b` |
      |---|---|
      | `Update_without_property_to_set_throws` | Passed ×2 |
      | `Update_multiple_tables_throws` | Passed ×2 |
      | `Update_unmapped_property_throws` | Passed ×2 |
      | `Update_with_invalid_lambda_in_set_property_throws` | **Failed ×2** |

      All four are `AssertTranslationFailed` over a specific relational diagnostic. The three green
      ones ship whole, and the **server** produces EF's message, which round-trips. So this is not a
      family-level design question — C56's rule, applied before pricing anything.

      **The one difference is what the invalid lambda contains.** It calls
      `TestUtilities.TestExtensions.MaybeScalar`. `ClientCodeFinder.VisitMethodCall` refuses a call
      whose **declaring type is not on the `TypeAllowlist`**, and `TestExtensions` is not on it, so
      `RejectClientEvaluation` refuses before the query can leave. The exception **type** is right,
      the **verdict** is right, and the message is `CoreStrings.TranslationFailedWithDetails` —
      EF's own wording of a different fact that is also true.

      **Deferring to the server does not work, and there are two independent reasons.**

      - `TypeNodeResolver` resolves against `TypeAllowlist.ForModel(model)` — **the same allowlist**.
        Ship the call and the server refuses the same type at deserialization: same verdict, worse
        message (*"not on the deserialization allowlist"*). Making the server accept it means
        admitting `TestExtensions` to `TypeAllowlist`, which is ADR-008 constraint 2 and which
        `security-review.md` §2 forbids on a conjunction argument.
      - The message the base asserts is `RelationalStrings.InvalidPropertyInSetProperty`. **J5
        removed the product's reference to `Microsoft.EntityFrameworkCore.Relational`**, so the
        product cannot name that string without reinstating a dependency this milestone deleted.

      **DECIDED: leave it red.** Both routes to the message cost more than the message is worth, and
      one of them is a security regression. Red and classified.

- [x] **J17. `Casts_are_removed_from_expression_tree_when_redundant` — the diagnosis above was
      wrong, and the correction is the deliverable.** `<this commit>` Docs only; no code, no gate.

      ```
      Expected: InvalidOperationException     Actual: InvalidCastException
      ---- Unable to cast object of type 'MockEntity' to type 'IDummyEntity'
      ```

      **What this entry used to say:** *"EF removes a redundant cast during translation; ADR-006
      captures the tree before that, so the cast survives, reaches the client residual, and
      executes… what is missing is not a refusal but a simplification."* The first clause is true.
      The conclusion does not explain this failure, and EF's own source says why.

      `NavigationExpandingExpressionVisitor.ProcessCastOfType` elides a cast **only** when
      `castType.IsAssignableFrom(source.PendingSelector.Type) || castType == typeof(object)`.
      The test has three blocks:

      | Block | Cast | EF | Us |
      |---|---|---|---|
      | 1 | `Cast<IDomainEntity>()` | redundant → **elided** | passes |
      | 2 | `Cast<object>()` | redundant → **elided** | passes |
      | 3 | `Cast<IDummyEntity>()` | **not** redundant → kept, then `TranslateCast` fails | **fails** |

      `MockEntity` implements `IDomainEntity` and does not implement `IDummyEntity`. So EF does not
      elide the cast that fails. **Implementing EF's elision in the residual would change nothing
      here** — it applies to exactly the two blocks that already pass.

      **The real cause is the type boundary, and it is C53's mechanism again.**
      `TypeAllowlist.AddSupertypes` admits every interface an entity CLR type implements, so
      `IDomainEntity` is allowed. Nothing implements `IDummyEntity`, and `Evaluate` has no other
      clause that would admit it, so it is refused. The `Cast` therefore cannot ship, stays in the
      residual, and `Enumerable.Cast` runs on the client. The stack trace says exactly that:
      `Enumerable.CastICollectionIterator` inside `SplitQuery.Apply`.

      **The general gap, and it is worth more than this test:
      `RejectClientEvaluation` examines an operator's *value* arguments and never its *type*
      arguments.** `Cast<T>` carries no lambda and no client code, so nothing looks at `T` at all;
      `TransparentIdentifierRewriter` is the only place in `src/` that names `Cast`/`OfType`, and it
      is about re-carry disqualification, not refusal.

      **`OfType<T>` is the reason to care.** A refused `Cast<T>` throws, which is loud. A refused
      `OfType<T>` returns **zero rows with no error** — the silent-plausible-answer shape that
      `ClientCodeFinder`'s own anonymous-`==` remark calls the one thing worse than a refusal.
      **Stated as a hypothesis with a named check, not as a fact:** write one query with an
      interface no entity type implements and read what `OfType` returns. That is J18.

      **Pricing, so nobody re-prices it later.** A guard that refuses a residual `Cast`/`OfType`
      whose type argument is off the allowlist gives the right exception **type**. The test stays
      red, because the base asserts EF's *whole printed message*, and that message is EF's rendering
      of the tree **after** EF normalizes and parameterizes it — `.Where(e => e.Id == @id)`, not the
      `FirstOrDefault(x => x.Id == id)` ADR-006 captures. **So the count will not move**, and this
      is C90–C93's situation: judge it on the reasons diff.

      **This *is* the "downstream of the capture point" family after all** — C56's shape — but the
      thing missing downstream is EF's *printing and normalization* of the query, not its cast
      elision. Getting that one word wrong pointed the remedy at code that would have measured
      neutral.

- [x] **J18. The type-argument boundary: `Cast<T>`/`OfType<T>` is refused rather than answered by
      `Enumerable`.** `<this commit>`
      `Total tests: 22657, Passed: 22467, Failed: 13, Skipped: 177` (`j18` against `j15`):
      **0 fixed, 0 broken**, `total` +1 for the new pin. `eng/trim-ratchet.sh`: **`OURS: 88 <= 88`**,
      unchanged — the guard reflects over nothing.

      **The count did not move and was never going to. The reasons diff is the result:**

      ```
      -  1 Assert.Throws() Failure: Exception type was not an exact match
      +  1 Assert.Equal() Failure: Strings differ
      ```

      `Casts_are_removed_from_expression_tree_when_redundant` now gets EF's exception **type** and
      EF's message **form**, and differs only in EF's rendering of the tree — which is J17's real
      residual and is ADR-006's by construction. C90–C93's situation, and the reason this file keeps
      saying to read the reasons rather than the count.

      **What J18's probe actually printed**, against a seeded two-blog store:

      ```
      OfType  => 0 row(s)
      Cast    => InvalidCastException: Unable to cast object of type 'Blog' to 'IUnmappedMarker'
      control => 2 row(s)      (OfType<Blog>, the non-vacuity control)
      ```

      **The severity was overstated before the probe, and the correction is the part to keep.** J17
      filed the `OfType` case as a **silent wrong answer** — the worst class this repo recognises.
      It is not one. `TypeAllowlist.AddSupertypes` runs for **every** entity type, so any interface
      or base class a mapped type implements is admitted and an `OfType` over a real hierarchy
      ships. The only type that can reach the residual is one **no entity implements**, and for that
      type LINQ-to-objects answers empty too. **No data differs; what is missing is EF's
      diagnostic.** The evidence was exactly what J17 predicted and the mechanism was one step
      stronger than the evidence supported — C38's lesson, in a new place: *read what the instrument
      prints, not what it was expected to print.*

      **The guard, and why each exemption is load-bearing.** `RejectUnshippableTypeArgument` fires
      only on a client-side `Queryable`/`Enumerable` `Cast`/`OfType`, and stands down when:

      | Exemption | Why |
      |---|---|
      | `target.IsAssignableFrom(source)` | Exactly what EF's `ProcessCastOfType` **elides**. A no-op cast is not a translation failure anywhere, and `Cast<object>` falls here too. |
      | `allowlist.IsAllowed(target)` | It could have shipped, so the boundary fell for some other reason and that reason is not this one's to report. |
      | `target.IsGenericParameter` | Not a type yet. |

      `node.Arguments[0].Type` carries the **argument's** type rather than the declared `IQueryable`,
      so `SequenceElementType` yields the real element type and the first exemption means what EF
      means by it. That is why `Cast<IDomainEntity>()` over `MockEntity` — block 1 of the same spec
      test — stays legal and still passes.

      **0 broken across 22,657 is the claim that mattered**: no carrier type the rewriters
      introduce, and no projection type, trips the guard.

      **The pin is `An_unshippable_type_argument_is_refused_rather_than_answered_by_Enumerable`**, in
      `InMemorySmokeTest`, and it carries its own control. `OfType<Blog>` over the same seed returns
      2 and `Cast<object>` returns 2, so a guard that refused *every* `Cast`/`OfType` would fail it.
      Without those two lines the test would pass for the wrong reason, which is the one way this
      particular fix could rot.

- [x] **J19. A null collection parameter is boxed like any other — and the classification that hid
      it was wrong.** `<this commit>`
      `Total tests: 22657, Passed: 22468, Failed: 12, Skipped: 177` (`j19` against `j18`):
      **1 fixed, 0 broken. 13 → 12.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.

      **The label was the defect.** `Parameter_collection_null_Contains` had stood as *"SQLite-tier;
      the 1 that survived `PrimitiveCollectionsQuery` going 4 → 1 in C88"* — a store limitation, red
      by right. **CLAUDE.md's own rule says to grep EF's suites for the name, and nobody had.**
      EF's SQLite, SQL Server and Cosmos tests all override it *only to pin SQL* and all call
      `base`; SQLite emits `WHERE 0`. **No reference provider refuses this query.** So it was ours.

      **What the message said, once read as a trace rather than as a category:**

      ```
      .Where(p => null.Contains(p.Int))
      ```

      The null array reached the server as an **inline literal**, and nothing can translate
      `null.Contains(…)`. EF's own funcletizer lifts the captured variable into a parameter, and
      then the provider answers `WHERE 0`.

      **One clause, and the shape of it is the reusable part.** `QueryExecutor.Substitute` decides
      whether to box a collection parameter (B22/C88, so the server's funcletizer lifts it back).
      Its guard is five clauses; **four already decide from `parameterType`, and the fifth read the
      runtime value** — `value is not System.Collections.IEnumerable`, which a null fails. So a null
      collection was never boxed. `value is null` now rides with the collections and every other
      clause is unchanged.

      **This is the third failure of one divergence, which is why it is worth naming.** The client
      substitutes EF's extracted parameters back in as constants (research-findings §6), where EF's
      own providers keep parameters. B22/C88 was that divergence for collections, this is it for a
      *null* collection, and J21 is it for a scalar behind a value converter. **CLAUDE.md states the
      rule already — derive it from the CLR type alone, because a value cannot say what declares
      it** — and this is C34's *"a key resolved by value rather than by what declares it"* in a
      fourth place.

      **Method note, since three sessions have now been cost by the same thing:** this was found by
      grepping EF's own suites for a test name that had carried a classification for two milestones.
      *A classification is not evidence* (C96), and **age is not evidence** either.

- [x] **J20. `Regex` is admitted to `TypeAllowlist`, reversing A46.** `<this commit>`
      `Total tests: 22658, Passed: 22471, Failed: 10, Skipped: 177` (`j20` against `j19`):
      **2 fixed, 0 broken. 12 → 10.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      **The decision and its argument live in [`security-review.md`](../../../security-review.md) §4a**, which
      is where a change to an ADR-008 control belongs; this entry records only what was measured.

      **A46 was a decision, not a finding, and it was never argued on the merits.** It recorded that
      `Regex` is off the allowlist and that the allowlist is ADR-008 — *"a roadmap decision, not a
      fix"*. What it did not do is ask whether `Regex` is dangerous. EF's own SQLite provider
      translates `Regex.IsMatch` to `REGEXP` and its InMemory provider evaluates it, so these two
      tests were **this provider disagreeing with every reference implementation** about an ordinary
      BCL type.

      **The security argument, in one line each** (full version in §4a):

      | Question | Answer |
      |---|---|
      | Does it widen §2's conjunction? | **No.** That bound is over the reflection *invocation* surface — `Binder`, `MethodBase`, `MethodInfo`, `ConstructorInfo`, `PropertyInfo`, `Activator`, `Assembly`, `AppDomain`. `Regex` is on none of it and constructs none of it. |
      | What about `Regex.CompileToAssembly`? | Unreachable, **by §2's own mechanism**: `ResolveMethod` resolves every parameter type through this allowlist, and `RegexCompilationInfo[]`, `AssemblyName` and `CustomAttributeBuilder[]` are all unadmitted. The signature lookup fails before the method is found — how `Binder` blocks `Type.InvokeMember`. |
      | `RegexOptions`? | No entry needed; §2 already records that every enum is admitted. |
      | So it is free? | **No.** ReDoS is real and is now accepted weakness **6** in §4, with the deployer's `REGEX_DEFAULT_MATCH_TIMEOUT` mitigation named. The library cannot inject a timeout into a static overload the caller wrote. |

      **Pinned, not asserted in prose.** `DeserializationHardeningTest.Regex_is_admitted_but_CompileToAssembly_cannot_be_named`
      checks the premise (`Regex` resolves; `Regex.IsMatch(string, string)` translates), **each of
      the three parameter types individually**, and the whole call. §2's own standard is that a
      review living only in prose goes stale the first time someone adds a convenience type — and
      this commit is exactly that event, so the standard applies to itself.

      **`PlatformNotSupportedException` was deliberately not used as the argument.**
      `CompileToAssembly` does throw it on modern .NET. That is a property of the runtime and could
      change; the signature argument is a property of this allowlist.

- [x] **J21. A mapped value type travels as a parameter, not as a constant — and the upstream bug
      is still upstream.** `<this commit>`
      `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177` (`j21` against `j20`):
      **1 fixed, 0 broken. 10 → 9.** `eng/trim-ratchet.sh`: `OURS: 88 <= 88`.
      **`KeysWithConvertersInfoCarrierTest` is 47 of 47.**

      **Nothing here fixes EF's defect, and the entry above is unchanged about that.**
      `KeysWithConvertersTestBase.EnumerableClassKey.Equals(object)` still casts its argument to
      `IntClassKey`. What J21 removes is **our path into it** — and J9's trace had already named
      that path exactly:

      ```
      at KeysWithConvertersTestBase`1.EnumerableClassKey.Equals(Object obj)
      at Query.ExpressionEqualityComparer.ExpressionComparer.CompareConstant(…)
      ```

      **EF reads the constants in a tree.** `CompareConstant` calls `Equals` on them to build the
      compiled-query cache key. EF's own providers never have an `EnumerableClassKey` constant
      there, because the caller's captured variable is a *parameter*. This client had one, because
      it captures at ADR-006's point — **downstream of EF's parameter extraction** — so every
      captured variable is already a `__p_0` by the time `Substitute` decides what it becomes
      again, and research-findings §6 said "a scalar becomes a plain constant".

      **§6 is right for a wire primitive and wrong for everything else**, which is now the rule:
      a value the model maps is boxed, so the server's own funcletizer lifts it back into a
      parameter. This is the **third face of one divergence** — B22/C88 for collections, J19 for a
      null collection, J21 for a mapped scalar.

      ## The probe is the part to keep, and the first version was wrong in a way reading could not catch

      Version one guarded on `value is not System.Collections.IEnumerable`. The target test did not
      move. **That is the exact situation CLAUDE.md says not to read** — a matcher that never fired
      and a fix that did not help are identical from outside — so it was probed rather than
      re-reasoned. The probe printed:

      ```
      7  CONSTANT EnumerableClassKey wirePrimitive=False
      ```

      **The branch declined the very value it was written for, seven times.** EF's
      `EnumerableClassKey` *implements `IEnumerable<byte>`* — that is what the type is named for.
      Without the probe the conclusion would have been "boxing does not help", and the change would
      have been reverted for the opposite of the true reason.

      **And the guard could not simply be dropped.** `IOrderedEnumerable<T>` would then be boxed,
      which the collection branch excludes on purpose, and the eight
      `Contains_with_local_ordered_enumerable_*` tests its comment names would go red. Checked
      before the full run, and green: 8 of 8.

      **So the discriminator is the model, and that is the principled one anyway.**
      `IsMappedPropertyType` asks whether any entity type declares a property of that CLR type —
      i.e. whether this is a **value the caller stores** rather than an incidental CLR type the
      query mentions. `IOrderedEnumerable<T>` is never a property type; neither is an anonymous
      type. Cached per model in a `ConditionalWeakTable`, since the answer depends only on the
      model and a weak table does not pin every model a process builds.

      **Two conservative exclusions, stated as conservative rather than derived**: `object`, because
      a parameter declared that way says nothing about what it holds; and an entity type, which
      travels by its own rules and which this class already resolves through the model elsewhere.

      **0 broken across 22,658 is the claim that mattered**, because this reverses a
      research-findings rule for a whole class of values.

- [x] **J22. The last three of the residual, re-derived — one attacked and reverted, two settled by
      grep.** `<this commit>` Docs only; no gate. **No code shipped, deliberately.**

      ### 1. The property-bag pair — upstream, on a path only this provider takes. ATTACKED AND REVERTED.

      **The standing note was wrong twice over, and my own first correction of it was wrong too.**
      It read *"fails inside EF's own `StructuralTypeMaterializerSource` … red forever"*. I then
      said the opposite — *"EF's `ComplexTypesTrackingInMemoryTest` overrides nothing, so EF expects
      it to pass on InMemory, so it is ours"*. **Both are half right, and the probe settled it.**

      The server's own stack, printed from `InfoCarrierFaultMapper.Rehydrate`:

      ```
      at System.Linq.Expressions.Expression.Property(Expression, PropertyInfo)
      at …StructuralTypeMaterializerSource.CreateMemberAssignment(…)
      at …StructuralTypeMaterializerSource.CreateMaterializeExpression(…)   <- complex type
      at …StructuralTypeMaterializerSource.AddInitializeExpression(…)
      at …StructuralTypeMaterializerSource.CreateMaterializeExpression(…)   <- entity
      at …RuntimeEntityType.GetOrCreateMaterializer(…)
      at InfoCarrier.Core.ServerSaveChangesExecutor.Materialize(…)
      ```

      **EF's defect, named exactly:** `CreateMemberAssignment` calls
      `Expression.Property(instance, member)` where `member` is the `Item[string]` **indexer** of a
      property-bag complex type, supplying no index argument. .NET refuses it. `ServerSaveChangesExecutor.SetOnEntity`
      already guards the identical hazard one level down — *"EF reports its properties as the
      `Item[string]` indexer; handing that to `SetValue` without an index is a parameter-count
      mismatch"* — so this repo had already met the shape and EF had not.

      **Why EF's own InMemory suite passes it, and it is not because the materializer works.**
      Nothing on EF's side materializes this entity **from a value buffer**: the test constructs the
      object and EF tracks it. This provider must materialize, because the entity reached the server
      as values. So EF never executes the broken path. EF's **SQL Server** suite does disable the
      test outright (`=> Task.CompletedTask`, *"Issue #36175: Complex types with notification change
      tracking are not supported"*), which is the corroboration. **J9/J21's shape a third time: an
      upstream bug reached only by a path this provider takes.**

      **The attack, and why it was reverted.** Two edits were made and measured on the target
      class — a `Materialize` branch and a complex-value indexer branch — both gated on
      `entityType.IsPropertyBag`. **The probe showed neither fired**, and the reason is the model:

      ```csharp
      public List<Dictionary<string, object>> Teams { get; set; } = [];      // complex COLLECTION of bags
      b.ComplexProperty(e => e.FeaturedTeam, "FeaturedTeamPropertyBag", …)   // a bag
      ```

      **The property bag is the *complex type*, not the entity.** `PubWithPropertyBagCollections`
      is an ordinary class, so `IsPropertyBag` is `false` on it and both branches were inert.
      Reverted rather than kept: J9 already paid for leaving inert code with an unverified
      explanation, and this session's own J21 nearly repeated it.

      **The route that would work, priced honestly.** Avoid `GetOrCreateMaterializer` whenever the
      entity type contains a property-bag complex type, and construct the entity ourselves. That is
      not a clause — the materializer is used *precisely because* it performs constructor binding
      (the docstring cites a positional record and *"No parameterless constructor defined"*), so the
      carve-out has to reproduce binding via `IRuntimeEntityType.ConstructorBinding`. **Real work,
      EF1001 internals, for 2 tests, against an upstream defect EF has an issue number for.** Not
      taken. Recorded so it is priced once rather than re-priced.

      ### 2. `Correlated_collection_with_distinct_3_levels` — A28 family, and the old wording overstated it

      The note said *"no correct answer satisfies the assertion"*. What is actually true, and it is
      both weaker and more useful:

      | Suite | Behaviour |
      |---|---|
      | `GearsOfWarQueryInMemoryTest` | refuses — `InMemoryStrings.DistinctOnSubqueryNotSupported` (#24325) |
      | `GearsOfWarQueryRelationalTestBase` | refuses — `RelationalStrings.DistinctOnCollectionNotSupported` |
      | SQLite, SQL Server, Temporal SQL Server | call `base`, i.e. the relational refusal |

      **Every EF provider refuses this query**, so the `AssertQuery` expectation in the base has
      never been checked against any provider's answer. This provider executes it — the split leaves
      part of it client-side, where InMemory's Distinct-on-subquery limit never applies — and gets a
      different result. So it is **A28 family**: a spec test asserting a limitation this provider
      does not have.

      **It is not the *verified* A28 that J15 and J2 produced**, and the difference matters: those
      two showed the answer is *right*. Here all that is shown is that the expected value is
      unvalidated. Verifying it would need an independent oracle for a three-level correlated
      collection under `Distinct`, which is J15-sized work for 2 tests. Stated as what it is.

      ### 3. The type-argument sweep — J18's guard is COMPLETE, not a special case

      J18 refused `Cast<T>`/`OfType<T>` **by name**, which left open whether other operators hide a
      type argument the boundary never examines. They do not, and the check is exact. A type
      argument can only name a type the arguments do not determine when the *source* is non-generic,
      and there are exactly two such methods on each declaring type:

      ```
      M:System.Linq.Queryable.Cast``1(System.Linq.IQueryable)
      M:System.Linq.Queryable.OfType``1(System.Linq.IQueryable)
      M:System.Linq.Enumerable.Cast``1(System.Collections.IEnumerable)
      M:System.Linq.Enumerable.OfType``1(System.Collections.IEnumerable)
      ```

      Every other `Queryable`/`Enumerable` method takes `IQueryable<TSource>`/`IEnumerable<TSource>`,
      so its type arguments are fixed by the source and its lambdas — both of which
      `ClientEvaluationFinder` already walks. **`RejectUnshippableTypeArgument` covers the whole
      surface**, and J18's "fixed by name, completeness unknown" is now closed. Read off the .NET 10
      reference documentation rather than reasoned about.

**`Composition_over_collection_of_complex_mapped_as_scalar` cannot be classified from this fixture,
and calling it A28 was unfounded.** A probe ran the base's query and compared it against the same
projection applied client-side:

```
rows=0 expectedRows=0    REMOTED = (empty)    EXPECTED = (empty)
```

**The `Dashboard` set is never seeded** — the base only ever asserted a throw, so EF never needed
data. So "no exception was thrown" is being observed over an empty table and says nothing about
whether the projection would be correct. **A28 requires evidence that the answer is right**, and
here there is no answer to check. Verifying it means seeding `Dashboard` in a test of our own,
which is a deliberate piece of work rather than a classification.

**The transferable point:** an `Assert.Throws` test that never needed data makes a *vacuous*
control. `Collection_enum_as_string_Contains` had one seeded row and the probe was strengthened
until a non-matching value proved the filter ran; this one has none, and no amount of reading the
failure would have revealed that.

- [x] **J15. `Composition_over_collection_of_complex_mapped_as_scalar` is A28, and now with an
      answer behind it.** `<this commit>`
      `Total tests: 22656, Passed: 22466, Failed: 13, Skipped: 177` (`j15` against `j8b`): **0 fixed,
      0 broken, `REASONS: unchanged`**, `total` +1 for the new assertion. The base test stays red,
      which is the point — seeding cannot make a query that answers start refusing.

      `CustomConvertersInfoCarrierFixture.SeedAsync` now seeds the `Dashboard` set EF's own fixture
      leaves empty, and `Composition_over_collection_of_complex_mapped_as_scalar_returns_the_right_answer`
      runs the base's query body **byte-for-byte** against it. It **passes**: the provider returns

      ```
      [{ Id = 4001, Layouts = [{ H = 11, W = 12 }, { H = 13, W = 14 }],                     Name = "Dashboard one" },
       { Id = 4002, Layouts = [{ H = 21, W = 22 }, { H = 23, W = 24 }, { H = 25, W = 26 }], Name = "Dashboard two" }]
      ```

      which is the right answer. So this is A28 proper — a spec test asserting a limitation this
      provider does not have — and it is now the *only* member of the residual 13 with a positive
      answer recorded rather than a refusal.

      **The seed is on the server, and that matters.** `InfoCarrierTestStore.InitializeAsync` hands
      the fixture's seed the **backend's** context, so the rows are written through the backing
      store's own model and its `List<Layout>`-to-string converter. The read side is then the only
      thing under test, which is what the question asked. Nothing else in `CustomConvertersTestBase`
      reads `Dashboard`, so one otherwise-unused set gains data and no other result can move —
      `REASONS: unchanged` across 22,656 is that claim measured rather than argued.

      **Non-vacuity was established by watching the assertion fail** (D1's rule), not by reading it.
      One expected pair was transposed to `(22, 21)` and the run printed the whole actual collection
      in its failure message — which is how the answer above is quoted here. Four distinct wrong
      answers are separable: a row lost, one row's layouts given to the other, a truncated list
      (the two rows deliberately hold **different numbers** of layouts), and `H`/`W` transposed (the
      serializer writes `(Height,Width)`, so a transposition is silent unless the two differ).

      **What this closes and what it costs.** The residual 13 now has **no member of unknown
      standing**. The cost is one fixture that no longer matches EF's byte-for-byte — worth stating,
      because that is the kind of divergence A49/B4 warn about; it is confined to a set EF seeds not
      at all, and the direction is *more* data rather than different data.

### J8's trim cost — caught by CI, reduced from 5 to 2 (2026-08-17)

**`eng/trim-ratchet.sh` failed in CI at 91 against a baseline of 86, and it was right to.** I did
not run it before committing J8; the gate is what noticed. `eng/measure.sh` and the transport tests
say nothing about trimming, and `WireGrouping.TryWrap` is the most reflective thing this milestone
added.

**Three of the five were avoidable, and avoiding them is the reusable part.** The first version
built the closed `WireGrouping<,>` and then *filled* it with `GetProperty` and `GetMethod`, which
cost `IL2062`, `IL2065` and `IL2075` on the fill path alone. Filling it instead through a
non-generic `IWireGroupingSink` that the closed type implements costs **nothing**: the closed type
can cast to its own `IGrouping<TKey, TElement>`, and the caller never has to.

```
91  -> GetProperty / GetMethod fill
88  -> IWireGroupingSink.Fill(object)
```

**A `dynamic` shortcut was tried and rejected on sight** — `((dynamic)value).Key` would have pulled
the whole C# binder into a WebAssembly download to save one cast.

**The remaining two are the premise, and `eng/trim-baseline.txt` already says so**:
`GetInterfaces()` on a runtime type, and `MakeGenericType` from arguments the caller's model
supplies. Baseline raised 86 → 88 with that reason, which is what the file's own header prescribes.

**One genuine correctness fix came out of it.** `Activator.CreateInstance(wrappedType)` is the only
caller of `WireGrouping<,>`'s constructor, and a trimmer cannot see a constructor reached that way —
so it was free to remove it, and a published client would have failed at run time on the first
non-composed `GroupBy`. A `[DynamicDependency]` on `TryWrap` keeps it. **That changes no warning
count**, which is exactly why it would never have been found by chasing the number.

**Also recorded so nobody misreads it:** `total` fell 1129 → 853, and none of that is ours — EF
Core's own count dropped 864 → 585 with package movement since M8-17.

Suite re-measured after the refactor: `13 / 22655`, empty FIXED and BROKEN, `REASONS: unchanged`.
