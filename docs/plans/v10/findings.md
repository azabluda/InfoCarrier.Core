# Findings, v10

How each part of the provider was made to work, and what it cost to learn. This is the long form
of what `CLAUDE.md` states as rules. Nothing here is an instruction; the instructions are in
`CLAUDE.md` and they are short on purpose.

Most of what follows is closed. It is kept because the same mistakes are available again: a
classification that was never re-checked, a count that did not move, a price paid for the wrong
obstacle. The plan entries that produced these findings are in `implementation-plan.md` and
`archive/`.

## When a syntactic guard is safe, and when it is not (R162/R164/R165, 2026-09-04)

Three guards were attempted in one session against the same background: this provider answers
queries every relational provider refuses. Two shipped and one was reverted, and the difference
between them is the transferable part.

**The two that shipped refuse something PROVABLY dead or PROVABLY impossible.**
`RejectUnshippableOrderingKey` refuses an ordering whose key type the allowlist does not admit.
The allowlist admits every primitive and every mapped type, so a key it rejects is one no store
could sort by. `RejectDeadCoalesce` refuses `new X() ?? y`, and `new` never returns null, so the
operator is dead code. Neither can refuse a query that does anything. Both measured 4 fixed and 0
broken.

**The one that was reverted refused something that merely LOOKED wrong.** Whether EF accepts a
`Distinct` inside a projected collection depends on identifier propagation through the `Distinct`
and every projection above it, computed during relational translation. Three syntactic
approximations were built; two measured 8 fixed and 8 BROKEN at an unchanged count of 55.

**And the shipped rule does not generalise, which is worth knowing before the next attempt.**
`GroupBy` and `Join` are blind to their key type in exactly the way `OrderBy` was. Extending
R164's rule to them would refuse **84** composite grouping keys and **12** composite join keys in
EF's own suites, because those keys are anonymous types the allowlist rejects by design. The rule
worked for ordering only because an ordering key is a scalar in every query that works.

**The question to ask first**, then, is not "does EF refuse this?" but **"can I state what this
guard refuses in a sentence that is true by construction?"** A `new` is never null. A type the
allowlist rejects is one no store can sort by. Neither sentence needs a measurement to be
believed; the measurement only confirms nothing else was caught. When the sentence needs a
qualifier -- "unless the identifier survives", "unless the parent keeps its columns" -- the guard
is approximating someone else's algorithm, and R162 is what that costs.

**The asymmetry that decides the close calls.** A missed refusal leaves the status quo. A false
refusal breaks a query that works everywhere, which makes this provider less capable than EF
rather than more portable. So "no test in the suite contradicts it" is not validation, and the
absence of a test for a shape is a reason NOT to guard it.

## A guard that cannot be written on the client (R162, 2026-09-04)

R160 refuses `Distinct` and the set operations applied ABOVE a projection that carries a
collection, and it works because that is a syntactic fact about the caller's own LINQ. The
obvious next step was the same operator one position lower: a `Distinct` INSIDE the query, on a
projection a collection join then has to be stitched back together against. Eight tests assert
that refusal. **Three hypotheses were built and measured, all three were refuted, and the third
refutation says the guard cannot be written here at all.**

**Hypothesis 1: the projection must carry the source's primary key.** That is what the test names
appear to say. Measured 8 fixed, 8 broken, `FAILING: 55` on both sides. The broken eight were the
sibling tests, and EF ships the pair that kills it:
`Correlated_collection_with_distinct_not_projecting_identifier_column` projects
`new { w.Name, w.IsAutomatic }` and TRANSLATES, while
`..._also_projecting_complex_expressions` projects
`new { w.Name, w.IsAutomatic, w.OwnerFullName.Length }` and THROWS. Neither carries `Weapon.Id`.
The test named "not projecting identifier column" is the GREEN one.

**Hypothesis 2: the projection must be bare columns, because columns are the identity when the key
is absent.** Fits that pair exactly. Measured 4 fixed, 0 broken -- but only four, because the gate
was wrong: it asked `ProjectionRewriter` whether it had produced a collection reassembly, and a
probe on `Correlated_collection_after_distinct_3_levels_without_original_identifiers` printed
`reassemblies=0` for a query whose projection carries two nested collections. **"The split found a
collection" and "the query projects one" read as the same question and are not.**

**Hypothesis 2 with a syntactic gate.** Measured 8 fixed, 8 broken, `FAILING: 55` again. The new
broken eight name the flaw:
`Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier`
has a computed member (`o.OrderDate.Value.Month`), no identifier, and a projected collection, and
every relational provider TRANSLATES it. Meanwhile `..._3_levels_without_original_identifiers`
THROWS although its inner `Distinct` projects only bare columns: what fails there is that the
projection ABOVE the `Distinct` keeps `xx.HasSoulPatch` and drops `xx.CityOfBirthName`, so the
collection's parent rows stop being distinguishable.

**So the fact being tested is not a property of the LINQ.** It is EF's identifier propagation
through `Distinct` and through every projection above it, decided inside
`RelationalQueryableMethodTranslatingExpressionVisitor` -- and the client does not translate. The
server does, and the server would raise it, except that these projections are client-typed and the
split never sends them whole. **Reverted.** The eight stay red and stay classified.

**And the asymmetry is what settles it, not the difficulty.** A missed refusal leaves the status
quo: this provider answers a query other providers reject. A FALSE refusal breaks a query that
works everywhere, which makes this provider less capable than EF rather than more portable. A rule
validated only by "no test in the suite contradicts it" is not validated: nothing in the suite
projects `new { o.Id, Year = o.Date.Year }` inside a collection's `Distinct`, which is an entirely
ordinary thing to write and which hypothesis 2 refuses.

**Two rules came out of it that are cheap to apply.**

  * **A `--filter` on a test name silently includes tests whose names EXTEND it and excludes
    nothing** -- but a filter on the LONG name misses the short one. Filtering on
    `..._also_projecting_complex_expressions` and
    `..._without_original_identifiers` ran neither
    `Correlated_collection_with_distinct_not_projecting_identifier_column` nor
    `Correlated_collection_after_distinct_3_levels`, which are exactly the tests that broke.
    **Filter by CLASS when a change could touch a family**, not by the test name you aimed at.
  * **Read both halves of a matched pair before believing what a test name says.** Three of these
    tests are named after the condition that does NOT cause the failure.

## The 140 remaining failures, triaged (R147, 2026-09-04)

The last whole-tail re-derivation predates about twenty-five tests of movement. This one is read
from the reasons diff and the run log of the 140-failure state, and it is **partly verified and
partly inferred** -- each row says which.

| Count | Family | Reading |
|---|---|---|
| 32 | `TPT`/`TPC` `GearsOfWar`, eight query shapes across two tiers | **This provider ANSWERS what EF refuses.** EF cannot attribute rows after a `Distinct` that drops the identifier columns, so it raises a translation failure; this provider reassembles the projection on the client and returns data. **The answers are CORRECT, and the existing failure message already said so.** EF's relational override wraps the CORE base call -- which is an `AssertQuery` that checks every row -- inside `Assert.ThrowsAsync<InvalidOperationException>`. "No exception was thrown" therefore means that call **ran to completion**, so its data assertions passed. A wrong answer surfaces differently and does so elsewhere in this same tail: `Correlated_collection_with_distinct_3_levels` reports `Assert.Equal() Failure: Values differ` from EF's `AssertResults`. Fully verified |
| 33 | `UdfDbFunctionInfoCarrierTest` | **User-defined SQL functions, one feature area, two symptoms.** 10 fail with `NotImplementedException` thrown from EF's own `UDFSqlContext.CustomerOrderCountInstance`, which is a body that exists to prove the call was translated rather than run -- so **this client ran it**, inside a projection, where client evaluation is legal and nothing refuses it. The other 23 are refused instead, as `The LINQ expression 'DbSet<Customer>()...' could not be translated`. The blocker for both is that an INSTANCE function's `Object` is the client's own `DbContext`, which this provider refuses to ship for the reason `ServerBoundaryAnalyzer.CarriesTheClientsContext` records. Verified from a stack |
| 12 | `Assert.Equal() Failure: Strings differ` | Message-text differences on queries that already refuse. Inferred from the reason |
| 8 | `Filtered_include_skip_navigation_order_by_skip_take_then_include_skip_navigation_where_split`, four classes | **`AsSplitQuery` is stripped, so the query needs `APPLY` and SQLite refuses it.** EF never overrides these on SQLite, because a genuine split query needs no `APPLY`. The stripping is not a mistake: it was measured as worth 456 tests, because the hint otherwise lands on the client where EF's own method is a no-op, and on a nested query root it forces the cut below that root. **Honouring split queries means carrying the hint to the SERVER**, which is a protocol change. Verified |
| 7 | `Assert.Throws() Failure: Exception type was not an exact match` | Four `OwnedJsonStructuralEquality` and two `Correlated_collection_with_distinct_3_levels`. The second pair is the known pair whose assertion no correct answer satisfies. Inferred |
| 4 | `this test store exposes no DbConnection` | By design. The client has no database |
| 4 | `Relational-specific methods can only be used when the context is using a relational database` | Same boundary |
| 4 | `Unable to cast InfoCarrierTypeMapping to RelationalTypeMapping` | The level-3 boundary: a relational model on the client needs store knowledge it cannot honestly have |
| 4 | `No part of the query can be executed on the server` | Inferred |
| ~32 | singles and pairs | Not re-read here |

**THE BIGGEST FAMILY IS NOT A DEFECT, AND IT WAS ANSWERED BY READING RATHER THAN BY MEASURING.**
The 32 `GearsOfWar` tests are this provider being MORE capable than the reference one: EF cannot
attribute rows after a `Distinct` that drops the identifier columns, so it refuses; this provider
reassembles the projection on the client and returns the right rows. **The evidence was already in
the failure message and I nearly spent a run to re-obtain it.** The rule that transfers: **an
assertion's failure message tells you what the code under it did, and "no exception was thrown"
around a data-checking call means the data checked out.**

**So the wrong-answer count is unchanged at two**, and both are the pair whose assertion no correct
answer can satisfy.

## An intermittent closed by reading the source it came from (R143/R146, 2026-09-04)

`AdHocMiscellaneousQuerySqliteInfoCarrierTest.Bool_discriminator_column_works(async: False)` failed
once in a full run with `ObjectDisposedException: Cannot access a disposed object`.

**What was checked, in order.**

1. **Could the change have caused it?** No. The commit under measurement converged
   `WithConstructorsInfoCarrierTest`, which is ADR-009 Tier A over EF's InMemory provider. The
   failing test is Tier B over SQLite, in an unrelated class, with no shared fixture.
2. **Does the class fail alone?** No. 71 passed, 1 skipped, and the failing case was green.
3. **Does the suite fail again at the same code state?** No. The re-run came back at 141 failures
   with nothing broken and the reasons unchanged.

So it is an intermittent, by the definition `CLAUDE.md` gives: the same code produced two different
answers.

**THE CAUSE, AND THE SUSPECT ABOVE WAS ONLY HALF RIGHT.** The stack says it plainly:
`SqliteConnection.Open()` failed at `sqlite3_create_collation` with
`ObjectDisposedException: SQLitePCL.sqlite3`. The native handle had already been disposed. EF's
`SqliteDatabaseCreator.Delete` answers a file-backed database with
**`SqliteConnection.ClearAllPools()`, which is process-wide and not per connection string**, and this
harness calls `EnsureDeletedAsync` at the start of every store's initialization. So one store's
delete disposes a pooled native handle that a concurrently initializing store is in the middle of
opening.

**Fixed by `Pooling=false` in the test connection string.** With no pool, `ClearAllPools` has nothing
to dispose and the exception cannot occur. **That is proof by construction, and it is the right kind
of proof here**: the failure appeared once in about ten full runs, so the three-run bar this
repository uses elsewhere would have shown nothing either way. The suite measured 140 with the change
and 140 without it.

**THE RULE THIS PRODUCES.** Two earlier intermittents here were closed by instrumenting one into the
open and by reproducing the other's signature. This one was closed by **reading the framework source
the failing stack named**, which cost minutes rather than runs. Try that first: a stack that ends
inside somebody else's code is a question about their code.

**What was suspected first, and why it was not enough.** Before R136 the two tiers were two test
projects and therefore two processes. They are one assembly now. xUnit runs test collections in
parallel within an assembly, each class is its own collection by default, and this repository
declares no `CollectionBehavior` anywhere. **A Tier A class and a Tier B class can now run at the
same time, which was impossible before the merge.** `ObjectDisposedException` is consistent with a
disposal race, and this repository has traced two earlier intermittents to shared-store disposal.

**It is not established, and the next step is reproduction rather than a fix.** One occurrence in
several full runs is a thin base. The cheap experiment is to declare a single test collection for the
whole assembly, or to cap the parallel thread count, and see whether the failure stops appearing --
but a failure that appears once cannot be shown to have stopped. The honest measurement is frequency
over many runs, which is expensive here.

**What must not happen is that this is forgotten.** `CLAUDE.md` said there was no known intermittent,
and that sentence is now wrong. It has been corrected rather than left standing.

## The boundary analyzer does not consult the client model for member mappability (R138, 2026-09-03)

**Found by an experiment that was reverted**, which is the only reason it is visible at all.

ADR-009 Tier B's Northwind store is built from the server model, where EF's own SQLite suite uses a
prebuilt `northwind.db`. The core `NorthwindContext` **ignores** ten `Order` properties that the real
Northwind schema has, so this tier's store had no such columns and ten `SqlQueryTestBase` tests
failed with `no such column: m.Freight`.

Mapping the ten on the *server* context fixes those ten and **breaks eight**:

| Test | What it asserts |
|---|---|
| `Average_with_unmapped_property_access_throws_meaningful_exception` | `Average(o => o.ShipVia)` raises `QueryUnableToTranslateMember` |
| `Collection_select_nav_prop_all_client`, `Collection_where_nav_prop_all_client` | the same for `ShipCity` |
| `SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity` | the same, across a correlated subquery |

They stop throwing and **return data**. The client's model still ignores `ShipVia`; the server's now
maps it; and the member access crossed the wire anyway. **The boundary analyzer never asked the
client model whether the member is mapped.**

**Why it has been invisible.** The two models are built from the same `OnModelCreating`, so they
agree about what is mapped in every test this suite runs. The experiment is the only thing that has
ever made them disagree on this axis. It is R120's shape once more — a fact two components read
independently — except here the two readers are two *models*, and the disagreement widens what the
client is allowed to do rather than narrowing it.

**PINNED, THEN REMOVED BY A SCOPE DECISION (2026-09-04).** The pin created a split model on
purpose, and the owner then removed split models from the harness entirely: one context class per
fixture, both halves from one `OnModelCreating`, which is what version 1 of this provider did. A
test that can only fail by building a configuration the project has declared out of scope does not
earn its place, so it is gone and this account is what remains. **The condition that reopens it: if
this provider ever offers a narrower client model as a feature, the guard becomes a prerequisite,
and its price is already measured below.**

**What the pin was, while it existed.**
`UnmappedMemberBoundaryTest` builds the disagreement deliberately: `SqliteSmokeContext` ignores
`Shipment.Note` for both sides and the store's own model customizer maps it on the server alone.
Three of its four tests are controls -- the client model really lacks the property, the server model
really has it and its column holds a value, and the mapped member beside it still works -- so the
fourth cannot pass by accident. R138's own fix put the defect back out of sight; this brings it
back into the count.

**THE FIX IS WRITTEN, MEASURED AND NOT SHIPPED.** Two readers have to learn the client model:
`ServerBoundaryAnalyzer`, so the subtree is not shipped, and `QuerySplitter`'s client-code finder,
so it is refused rather than evaluated locally. **Both are needed** -- the analyzer's verdict alone
refuses nothing, because the finder is what raises, and the finder never examines a node the
analyzer marked shippable. With both, the pin passes and the refusal carries EF's own
`QueryUnableToTranslateMember` wording.

**It costs sixteen spec tests, and every one is a message difference on a query that already
refused.** Measured at `failed` 166 against 150.

| Family | What EF answers instead |
|---|---|
| `ComplexNavigationsCollectionsSharedType` and its split variant, `Multiple_complex_includes_self_ref` and `Complex_query_issue_21665` (8) | "The expression '...' is invalid inside an 'Include' operation" |
| `NorthwindBulkUpdates.Update_unmapped_property_throws` (2) | `ExecuteUpdate`'s own message for setting an unmapped property |
| `TPT`/`TPC` `GearsOfWar.Client_member_and_unsupported_string_Equals_in_the_same_query` (4) | the untranslatable `string.Equals` overload, named ahead of the member |
| `NorthwindSelect.SelectMany_..._references_non_mapped_properties_...` (2) | its own |

**Three narrowings were tried and none of them helps**, which is what makes this a trade rather than
a bug in the attempt. Giving the member check the lowest priority within one argument does not help,
because for most of these the refusal comes from the *analyzer* and never reaches the finder. Asking
every entity type that shares a CLR type, rather than the one `FindEntityType` returns, is necessary
for shared-type entity types but changes none of these. Excluding the `Include` and `ExecuteUpdate`
argument positions does not help either, for the same reason as the first.

**So the question is one for the owner**: sixteen message-text differences, against one query shape
that silently answers where every other provider refuses. `website/docs/limitations.md` already
records two message-text differences, so the category exists; sixteen more is a different order.

**The rule that transfers: a guard that is never exercised is not evidence that it exists.** Eight
tests asserted an unmapped-member refusal and passed, for two milestones, without the code under
test ever being reached — the store simply had no column, so the query failed earlier and for a
different reason.

## What was built, and what each thing taught

Not yet implemented, in rough priority order:
- **The HTTP transport works and is tested (M8 Phase 1).** A `DbContext` with no database answers
  queries, saves and runs transactions against a SQLite-backed ASP.NET Core server over a real
  HTTP hop — `test/InfoCarrier.Core.TransportTests`, 17 of 17. **That project is deliberately not
  `InfoCarrier.Core.FunctionalTests`**: the ratchet counts the latter and its number must keep
  meaning "inherited spec tests failing". `eng/measure.sh` was scoped to one project in the same
  phase, because it parses the *last* `Total tests:` block and a second test project in the
  solution would have silently corrupted every measurement.
- **The Blazor WebAssembly client works too (M8 Phase 2, M8-10…M8-17).** `dotnet run --project
  samples/Northwind.Server` serves a browser client whose `DbContext` has no database: three pages,
  a wire inspector that decodes the expression tree out of its base64 layers, a compiled model, and
  a trimmed publish that runs. **Verified by executing it** — headless Edge over the **DevTools
  protocol**, because `--dump-dom` renders a page but cannot press a button, and two of the three
  pages are about what happens when you do.
  **Three things that phase established, all of them about the browser rather than this provider:**
  - **WebAssembly cannot block, and it bit twice.** Automatic lazy loading is impossible there — a
    navigation getter is *synchronous*, so it must block on the round trip, and `order.Customer`
    throws `Cannot wait on monitors on this runtime` **after the request has gone out**.
    `ILazyLoader.Load()` is synchronous too, so the spec's recorded fallback fails identically; use
    `Entry(x).Reference(…).LoadAsync()`. Separately, **a compiled model cannot even be loaded**
    without `AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true)`, because EF
    initializes it on a 10 MB-stack `Thread`. Proxies themselves are fine — Castle DynamicProxy
    emits types there — but the client no longer enables them (M8-18), because
    configured-but-unusable turns an unloaded navigation into an exception instead of a `null`.
  - **`dotnet ef dbcontext optimize` uses the STARTUP app's DbContext configuration and silently
    ignores an `IDesignTimeDbContextFactory` in the target project (M8-18).** A Blazor WASM project
    emits no `deps.json` and cannot be a startup project, so the server was one — and the
    "client's" compiled model came out annotated `Relational:TableName` and
    `Proxies:LazyLoading`. The browser ran on the **server's** model for two steps and looked fine,
    which is the silent-divergence shape A49/B4/B12 warn about. **A one-GUID regeneration diff is
    the tell that the factory was never consulted**, and it was misread once as "proxies do not
    affect the model". The compiled model is removed; the sample builds its model at start-up.
  - **Trimming: 88 IL warnings are ours and spec §9's "none of ours" criterion is NOT met.** They
    are the premise showing through — the wire carries a type's *name* — so
    `[DynamicallyAccessedMembers]` cannot express them. Gated by direction in `eng/trim-ratchet.sh`
    against `eng/trim-baseline.txt`, exactly as `eng/ratchet.sh` gates the spec suite. **The app
    runs trimmed regardless**; the warnings mean unprovable, not broken.
  - **`eng/trim-ratchet.sh` publishes CLEAN on purpose.** An incremental publish does not re-run
    ILLink, and the script's first version reported `OURS: 0` — a gate that would have passed
    forever. It now wipes `obj/Release` and refuses a log with no ILLink banner. **And classify
    trim warnings by declaring member, never by file path: this repository's own path contains the
    string "InfoCarrier", so a naive grep attributes every warning in the log to this product.**
    **No count is quoted here on purpose — read `ours` and `total` out of `eng/trim-baseline.txt`.**
    They move independently and for unrelated reasons: `ours` went 86 → 88 for a deliberate
    `WireGrouping` fix, while `total` fell 1129 → 853 because EF CORE's own count dropped, which is
    not an improvement of ours and reads like one. The baseline file records every measurement and
    why it moved; a number copied into prose only records when it was copied.
- **Complex types work** (A32) — `ComplexTypesTrackingTestBase` is **249 of 251**, and the two
  left are one shape of one feature: a property-bag complex *collection* on an `Added` entity.
  A complex value cannot ride in the value dictionary an entity is built from — `CreateEntry` and
  `ShadowValuesFactory` are name-keyed and complex leaves collide (`Culture.Species` and
  `Milk.Species` are both `"Species"`) — so both sides set it through its CLR member instead.
  **J22 traced the residual two to an UPSTREAM defect on a path only this provider takes, and
  corrected two wrong readings on the way.** EF's `StructuralTypeMaterializerSource.CreateMemberAssignment`
  calls `Expression.Property(instance, member)` where `member` is the `Item[string]` **indexer** of
  a property-bag complex type, with no index argument — the same hazard
  `ServerSaveChangesExecutor.SetOnEntity` already guards one level down. **The property bag is the
  complex type, not the entity** (`List<Dictionary<string, object>> Teams`), so a fix gated on
  `IEntityType.IsPropertyBag` is inert — measured, and reverted. EF's own InMemory suite passes the
  test **because EF never materializes the entity from a value buffer**; EF's SQL Server suite
  disables it outright (issue #36175). The route — construct the entity without
  `GetOrCreateMaterializer` — has to reproduce constructor binding, so it is priced in J22 and not
  taken. The
  `Query.Associations.ComplexProperties` family is **not** adoptable on Tier A (A77): EF's InMemory
  provider does not translate a complex property access at all, which is why EF ships no InMemory
  complex-type query test. Complex-type *queries* need Tier B.
- **The query residual** — 2, and **neither is a gap**. A40 closed
  `SelectMany_correlated_subquery_hard`, the correlated subquery under a client-side projection
  that **milestone M2-B existed for**, and A43 closed `Select_GroupBy_SelectMany`. The 2 left are
  spec tests asserting a limitation this provider does not have — they run and return the right
  answer, and the query bodies are `private` to the spec base, so the assertion cannot be inverted
  from a derived class.
- **`GraphUpdates` is CLOSED — 1787 of 1787 (C76) — and the residual was never what it was filed
  as.** For phases it read *"tracked-entry count off by one (26 vs 27)"*. Two probes in C42's order
  — what the metadata says, then **read the row the store actually holds** — showed the principals
  were byte-identical between a passing and the failing parameterization and only the *dependents'
  foreign keys* differed: `Optional2MoreDerived#7->6`, and no `Optional1` has key 6. **A wrong
  value written to the store.** The cause is that **a client placeholder is not unique across
  entity types** — EF's temporary generator counts down from `int.MinValue` *per key property*, so
  `Optional1.Id` and `Optional2.Id` issue the same numbers in one request, and the server's
  placeholder map was keyed by the value alone. It is now keyed by `(key property, value)` and
  resolved through `foreignKey.PrincipalKey`. **C34's rule in the SaveChanges direction**: a key
  resolved by value rather than by what declares it. Order-dependence, not rarity, is why it was
  1 of 36 parameterizations.
  **It remains the tripwire for over-returning on SaveChanges**: C42 measured a rule that sent
  every propagated foreign key back to the client and two more parameterizations of this same test
  went red. An ordinary FK is the client's own business; only a key it cannot recover by fixup may
  be asserted at it.
  **C76 filed an open finding here and C79 closed it: there was no gap, and the test was wrong.**
  `alpha.Id` reads `0` right after `Add` because **EF keeps a temporary key on the entry, not on
  the instance** (`Entry(x).Property(p).CurrentValue` is the placeholder) — so the test wrote `0`
  into a required foreign key and earned its `FOREIGN KEY constraint failed`. It failed with and
  without the fix, which is true and which was read as "pre-existing defect" when it meant
  "unrelated". **The pin C76 wanted now exists on Tier A**, and needing Tier A is the reusable
  part: on Tier B every placeholder maps to *itself*, so three separate mutations leave a Tier B
  test green. A collision is only observable where the store issues keys at `Add` **and** the two
  key sequences have drifted apart — both InMemory counters start at 1, so an unseeded store hides
  it too.
- **The remaining spec bases — 0. `InfoCarrierComplianceTest` is GREEN (C82).** Every base EF
  ships that this provider can host is adopted, `AdHocJsonQuery` last and **61 of 61**. B3d/C10
  had priced it at *"626 + 322 lines of relational mirror and seven abstract seeds"*, and both
  halves were priced against the wrong obstacle: the mirror was the cost of **not referencing**
  `EFCore.Relational.Specification.Tests` (ADR-013 now does), and the seeds are ten raw-SQL
  `INSERT`s copied byte-for-byte from `AdHocJsonQuerySqliteTest` and run against the **backend**,
  because the client has no database. **Copying only the seven the compiler demanded cost a run** —
  `Seed21006`, `Seed29219` and `Seed34960` are `virtual`, EF's SQLite class overrides them too, and
  34 of 61 became 56 of 61 once all ten were taken. *"Which does the compiler require"* is not
  *"which does this store need"*.
- **`Scaffolding.CompiledModel` is CLOSED — 4 of 4 (C93)**, and its 42 baselines live in
  `test/InfoCarrier.Core.FunctionalTests/Scaffolding/Baselines/`, written by EF's own
  `EF_TEST_REWRITE_BASELINES=1`. They need `<Compile Remove="Scaffolding\Baselines\**\*" />` and
  its `<None Include>` partner — the same `TestNamespace.DbContextModel` is generated once per
  test, so building them gives **125** duplicate-definition errors. They are the only assertion in
  the suite over the *source* this provider contributes to a compiled model.
- **This provider has design-time services now (C90), and the standing price for them was for a
  package that was never needed.** C8 filed `Scaffolding.CompiledModel` as *"needs
  `Microsoft.EntityFrameworkCore.Design` on the product"* — but `IDesignTimeServices`,
  `DesignTimeProviderServicesAttribute` and `EntityFrameworkDesignServicesBuilder` all live in
  `Microsoft.EntityFrameworkCore` itself. **The namespace is not the assembly.** What shipped is
  one attribute and one class; the tests are about `dotnet ef dbcontext optimize`, not schema, and
  registering nothing schema-related keeps migrate and scaffold-from-database unavailable without
  having to refuse them. Four further obstacles came out behind it, each named by its own error
  and each real: a `Default` property on `InfoCarrierTypeMapping` (a compiled model *clones* a
  mapping, it does not construct one), `<PreserveCompilationContext>`, the product assembly in
  `AddReferences`, and the server's model — `CompiledModelTestBase.Test` builds the context factory
  **twice** and the second call carries no model customization, so the harness carries the last one
  forward. **Two genuine defects are left and both are filed with probe evidence**: C91, where
  `property.GetValueConverter()` is `null` under a compiled model because EF's generator puts the
  converter on the *type mapping* for a converter configured by instance; and C92, where a complex
  value travels by reflective object shape and so carries a member the model says `Ignore` to. The
  baselines are the third, and they are not a defect — C8 read `AssertBaseline` as returning early
  when the **baselines** directory is missing, and it returns early when the **test source**
  directory is missing. It *creates* the baselines directory. C0's "112 generated files" was right
  in kind; `EF_TEST_REWRITE_BASELINES=1` is how EF writes them.
- **`property.GetValueConverter()` is not the effective converter, and a compiled model is where
  the two part company (C91).** EF's generator emits a `valueConverter:` argument only when it can
  name a converter *type*; under `ForNativeAot` — what `dotnet ef dbcontext optimize` produces —
  it puts the converter on the property's **type mapping**, so a converter configured by
  *instance* (`HasConversion(new BoolToStringConverter("A", "B"))`) vanishes from
  `GetValueConverter()` while one configured by *type* survives. `PrimitiveCoercion` falls back to
  `(FindTypeMapping() as InfoCarrierTypeMapping)?.Converter` — the client's own mapping, where a
  converter can only have come from the model. **The first version of that fallback measured 23
  disagreements where it meant to close 3**, and the instrument that found them is the one to
  reach for whenever the two models might disagree: print, from `WireType`, what *each side*
  computes for *every* property — tagged by whether the mapping is an `InfoCarrierTypeMapping`,
  which is the only side marker needed — and diff the two by name. Twenty-one were primitive
  collections (the mapping's `CollectionToJsonStringConverter` is `JsonForm`'s business, B4) and
  two were `HasConversion<string>()` on an enum (a provider CLR type means the model asked for a
  *target*, not for a converter). The guard is `GetProviderClrType() is null && JsonForm(…) is
  null`: fall back only where the existing rule had no answer at all.
- **A complex value travels by reflective object shape, and the shape is not the model (C92).**
  `OwnedType` declares `public DbContext? Context` and its model says `Ignore(e => e.Context)`; the
  walk sent it anyway. `ToComplexValue` hands the `IComplexType` down so the walk can drop what the
  model does not map — **forward only**, because `RehydrateObject` sets exactly what arrived. The
  hard part is that a complex type **descends through three shapes that are not it**, and the first
  version measured **31, five worse than it started**: the items of a complex collection (right), a
  `KeyValuePair` inside a **property bag** (a bag *is* a `Dictionary<string, object>`, so it takes
  the collection branch), and `Nullable<T>` for an **optional** complex property (`ValueNestedType?`
  presents `HasValue`/`Value`, which no complex type declares). Filter only where
  `ClrType.IsInstanceOfType(value)`, and treat `Nullable<>` as transparent. **The probe was one
  line** — print `DROP <complexType>.<member> kept=[…]` at the point of refusal — and it named the
  `Nullable<>` case in one filtered run where reading the five test names had suggested only
  "something about value types".
- **Spatial works, and the shape of how is worth keeping.** Three pieces, landed and measured
  separately because C9's combined attempt aborted the host: the NetTopologySuite branch in
  `InfoCarrierTypeMappingSource` (C15, worth 19 on its own — the long-standing "needs SpatiaLite"
  note was wrong, and the provider that could not map a `Point` was *the client*); **ADR-012's
  value-mapper seam** (C17); and a **WKT** geometry mapper registered **test-side** (C18), which is
  why the product assembly still does not reference NetTopologySuite. Not GeoJSON — it carries no Z
  or M, which is the v1 defect requirements §2.8 records.
- **`SpatialQuery.Item` is CLOSED (C53), and the three diagnoses before it were all wrong in the
  same way.** It was not null semantics in the residual (C43), not a native dependency (C51), not
  a tier question (C52) — it was **a member declared on a base class the model never names**.
  `MultiLineString`'s indexer lives on `GeometryCollection`; the allowlist admitted a property's
  own CLR type and nothing above it, so the analyzer refused the call and the rewriter shipped the
  whole geometry and indexed it client-side, where `null[0]` throws. `AddPropertyBaseTypes` walks
  the base chain — **base classes only, never a category**, because C23 measured `ValueType`/`Enum`
  widening at 145 → 186. **The rule to carry forward: when a projection lands on the client for no
  obvious reason, probe the boundary verdict before theorising about semantics.** Two probes —
  what the split produced, then which type was refused — replaced three sessions of plausible
  reasoning.
- **Spatial stays Tier A, and moving it is measured-worse (C52): 12 failing on Tier B against 2.**
  `mod_spatialite` does arrive from NuGet with no manual install, and EF's fixture pieces port
  cleanly server-side, so the move is *possible* — it is just worse. If it is ever attempted again,
  the two `Intersects_*` overrides become wrong (SQLite passes the base) and six `JsonException`s
  on geometry conversion are the price to diagnose.
- **The seam is the general answer to "a CLR type the wire cannot walk", and it now has three
  consumers.** A geometry's members recurse (C18), `IPAddress.ScopeId` throws for an IPv4 address
  (C23), and `Uri.AbsolutePath` throws for a relative URI (C34). Three unrelated CLR types, one
  mechanism, all reached by the same reflective object-shape walk.
  **DECIDED 2026-08-11 (C89).** `IPAddress` and `Uri` now ship in the product and are registered
  by `AddInfoCarrierStandardValueMappers()`, which `AddEntityFrameworkInfoCarrier` calls for the
  client and which is **public because the server must call it too** — a value mapped on one side
  only is worse than one mapped on neither. Both are BCL types whose members throw for ordinary
  instances, so an application storing one has opted into nothing. **The geometry mapper stays
  test-side**: shipping it would put a NetTopologySuite dependency in this package, which v1 also
  refused (C12). An application registers its own beside the standard two, and the test project's
  `InfoCarrierNetTopologySuiteValueMapper` is the worked example. ADR-012 carries a dated
  amendment.
- **"The wire cannot handle this type" has two answers and they are not interchangeable** (C34).
  The seam decides how a value is *written*; `ExpressionJsonContext` decides whether the wire can
  carry the result at all — a key value lands in `EntityKeyNode.KeyValues`, declared `object` and
  resolved by runtime type, which the seam never sees. A converted key exercises both, and fixing
  only the first moves the failure rather than closing it.
- **C18's `GeometryCollection` gap turned out not to need the type-level probe** it proposed (C24).
  `ProjectionRewriter` was substituting a `List<T>` for a declared type a `List<T>` does not
  satisfy; one clause fixed it and ADR-012 needed no amendment.
- **`MaterializationInterception` is CLEAR (C71), and the route in is the reusable part.** B16
  answered the design question in 2026-08-09 and C58 priced the optional remedy at "a hand-rebuilt
  `CoreOptionsExtension` plus a per-fixture DI change, reaching at most half". Both of C58's facts
  were true and both were about *intercepting* the forwarding; the answer was to **not forward** —
  one argument in the test class's own `CreateContextFactory`, safe because
  `SingletonInterceptorsTestBase.CreateContext` is the family's only entry point and the
  `onConfiguring`/`addServices` it sets carry nothing but interceptors. 26 fixed, 0 broken, product
  untouched, and `PropertyValues` still green because it registers its server-side interceptor
  itself. **When a remedy is priced as expensive, check whether the price is for the route rather
  than the goal.** **The 27th member closed in C72, and it needed the opposite question.**
  `OptimisticConcurrency`'s fixture registers `F1MaterializationInterceptor` on the server *on
  purpose*: every F1 entity's private constructor ends in `Assert.IsType<…Proxy>(this)`, so the
  model cannot be materialized without one — dropping it measured **21 of 33 failing**, all
  server-side. Only `InitializingInstance`'s `Sponsor.Name` rewrite is non-idempotent, so the
  server gets the **construction half** and not the caller's transform. **When the payload cannot
  be dropped, ask which part of it the server is entitled to.** The design answer below is
  unchanged and still the reason the product forwards nothing: This
  provider is **two EF instances**, and a real deployment must be free to define materialization
  hooks on either side or both — so the three routes B16 measured, each of which suppresses one
  side, may none of them be taken. Nothing in `src/` forwards an interceptor: the server sees the
  user's only because `InfoCarrierBackendTestStore.AddProviderOptions` forwards the client's
  `onConfiguring` for model parity (A49) and it rides along. Each side is individually correct —
  `"Intercepted: Intercepted:"` proves two invocations, `Assert.Same` proves they carry different
  contexts, and B15's fix landing on the *client's* materializer proves the client raises it. **The
  A28 family, one level up**: A28's spec tests assert a materialization limitation this provider
  does not have, these assert a *topology* it does not have. Red and classified.
  **C58 attempted the optional harness remedy and priced it.** Two facts came out of it and both
  are load-bearing: `DbContextOptions.WithExtension` keys the map on `extension.GetType()`, so
  **no subclass of `CoreOptionsExtension` can ever replace it** — B16's hand-rebuild from the
  public `With*` setters is the only route, not one of several. And the family arrives on *two*
  channels, because `SingletonInterceptorsTestBase` passes `useServiceProvider: inject`: half
  through options and half through the server's service collection, which the options route does
  not touch at all. A71's ten `AddInterceptors` failures are the **server's** and the same defect
  as the twelve `Assert.Same`, not a separate one.
- **JSON-mapped owned collections work (B12, TAKEN in C80).** A JSON document carries no key for
  its array elements, so every store synthesizes an ordinal. The client kept the CLR `Id` instead —
  a property the document does not contain — so it was `0` for every element and EF's fixup gave
  each of them to all of them. **Wrong data, no exception.** `InfoCarrierKeyDiscoveryConvention`
  now gives the client the same synthesized-ordinal key, which is `RelationalKeyDiscoveryConvention`'s
  JSON branch over the same public core base. **36 fixed, 0 broken**; `JsonQuery` went 40 → 4.
  The rule it states, and it is narrower than "the client is relational": *where a key shape is
  decided by the caller's own model configuration rather than by the store, the client has to reach
  the same answer as the server.* Nothing relational is resolved from the container, and the
  product already referenced `Microsoft.EntityFrameworkCore.Relational`. **A Cosmos backend would
  need its own clause** — Cosmos recognises an ordinal key by the property's *shape*, not by this
  name. **Reads only** — `JsonQueryTestBase` has zero `SaveChanges`. C81 answered the write half as far
  as it can be answered: `ComplexCollectionJsonUpdateTestBase` is adopted and **18 of 18**, so a
  JSON-mapped collection does survive being written; but `JsonUpdateTestBase`, the base that covers
  **owned** JSON collections, is **unreachable** — its `UseTransaction` is `public void` rather than
  virtual and calls `GetDbTransaction()`, so all **142** of its tests fail on *"Relational-specific
  methods can only be used when the context is using a relational database provider"* before
  reaching anything about JSON.
  **C86 covered it directly, C87 fixed half and C95 closed it — `JsonOwnedCollectionUpdate` is
  5 of 5.** `InfoCarrierDatabase.Expand` now yields, for a JSON-mapped entry, both the **ownership
  chain** (C87 — EF writes a JSON column by partial update of the owning row, whose entry is
  `Unchanged` and never travelled) and **the rest of that owner's document** (C95), as `Unchanged`,
  read off the client's change tracker rather than off the owner's navigations because a *removed*
  element is no longer in its collection.
  **C87's account of what was left was wrong, and the way it was wrong is the lesson.** It read the
  message *"another instance with the key value '{OwnerId: 1, __synthesizedOrdinal: 1}' is already
  being tracked"* as the **server** holding a query-tracked element, and raised "should a server
  context carry query-tracked state into a replay at all" as a design question about context
  lifetime. Two probes refuted it in one filtered run: the server's tracker is **empty** on entry,
  and the conflict is raised on the **client**, in `ApplyGeneratedValues`, applying what the server
  sent back. **"Something already holds this key" names a collision, not a side** — and the stack
  trace had said which side all along. The real defect: `__synthesizedOrdinal` is **positional**,
  `ChangeEntryMapper` sends no navigations, so the owner arrived with an empty collection and EF
  numbered the appended element `1` instead of `3`. **The identity conflict was the symptom; a
  wrong ordinal written into the document was the defect** — C76's shape one level along. This is
  not the "send the whole graph" C37 and C42 each paid for: a JSON column is written as one
  document, and the scan is gated on `GetContainerColumnName()`, so a model with no `ToJson()` pays
  nothing. 0 broken across 22,453.
- **`Query.Associations.*` + `BulkUpdates.*` are adopted and green — 322 of 336 and 251 of 257.**
  The standing "no InMemory counterpart, therefore out of scope" note was the A79 mistake again;
  they are Tier B (C0–C4), and C19/C20 took them the rest of the way. What was left is 14 + 6, all
  classified in C20 — and **both went further afterwards**: `Query.Associations` is **336 of 336**,
  and `BulkUpdates` fell from 6 to 2 when C94 adopted EF's own #28886 skip.
- ~~**CI is broken**~~ — **it is not, and had not been since Phase N** (C39, 2026-08-10). The
  `.sln`-vs-`.slnx` and `~InMemory`/`~SqlServer` claims that stood here described the file as it
  was before `51f4684`; the workflow has restored `.slnx`, run two jobs and invoked
  `eng/ratchet.sh` ever since. What *was* broken was `test/known-failures.txt`, eight months stale
  at `111/5215` against an actual `145/22312` — the gate would have failed on the failure count
  while the total quadrupled. **Read the file before repeating a note about it.**

**`ExecuteUpdate` is the cautionary tale of this phase, and it is about pricing rather than code.**
Three plan entries (C0, C3, C4) recorded it as a wire or boundary change and priced it at 136 —
on the strength of reading `UnreachableException: Can't call this overload directly` as proof that
the split evaluated the call on the client. It *was* evaluated on the client, and the cause was one
missing name on the ADR-008 allowlist: `ExecuteUpdate`'s rewritten call names
`IReadOnlyList<ITuple>`, `Tuple<,>` and `IReadOnlyList<>` were both already admitted, and `ITuple`
was not. **A probe in `QuerySplitter.Split` printing the boundary verdict and `Diagnose(query)`
named it in one filtered run** (C19, 153 closed), and the same probe established in the run before
that `ExecuteDelete` had never been broken at all. The standing probe rule is "establish that the
code *ran*"; point it one step earlier — *where is this being cut* — before pricing a gap.

**Native AOT was measured for the first time on 2026-08-24, and the measurement says the two axes
are not one axis.** The trim ratchet has gated `samples/Northwind.Client` since M8-17 and reports 88
diagnostics of ours. A `dotnet publish -r win-x64 -p:PublishAot=true` of the console sample
(`samples/Northwind.Demo`) reports **155 unique IL diagnostics, 153 of them ours**, and the split is
the finding: **59 are IL3050**, `RequiresDynamicCode`, a code the trim analyzer never emits because
trimming does not ask whether code can be *generated*. So more than a third of the AOT picture is
invisible to the gate this repository already runs, and "trimming is measured" was never evidence
about AOT.

**The publish did not finish, and the reason is the machine rather than the code.** It failed at the
native link step with `Platform linker not found`: Native AOT on Windows needs the Desktop
Development for C++ workload, which this machine does not have. ILCompiler's analysis had already
run by then, which is why there is a number at all. **Nothing here proves a native binary would
work**, and nobody should write that it does. `ubuntu-latest` ships clang, so CI could complete the
link if this is ever worth gating.

By file, ours concentrate exactly where the trim warnings do: `ProjectionRewriter` 26,
`DynamicValueMapper` 18, `QuerySplitter` 16, `QueryExecutor` 14, `NodeToExpressionTranslator` 13.
That is the provider's premise again, now with the second half named: the wire carries a type's
name, the far end resolves it, **and then builds and compiles an expression tree over it**.
`Expression.Lambda(...).Compile()` and `MakeGenericType` are what a remoting LINQ provider is made
of, and Native AOT is the one runtime that cannot do either.

**The two counts are not comparable and must not be subtracted.** They come from different samples
(Blazor client versus console client), different tools (ILLink versus ILCompiler) and different
unique-keys (`eng/trim-ratchet.sh` keys on code plus declaring member; the AOT tally keys on code
plus file, line and column). 153 minus 88 is not a number that means anything.

**What the annotation did, and did not do (2026-08-24).** `UseInfoCarrier` now carries
`[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, which is what EF Core itself puts on
`DbContext`'s constructors, checked in `subrepos/efcore` before it was copied. **It did not lower
the 88, and the expectation that it would was wrong.** Those warnings sit in methods EF reaches
through its own interfaces, not in anything reachable from `UseInfoCarrier`'s body, and the
attribute exempts only the annotated method's own body. The ratchet measured 88 before and 88
after. **What the attribute buys is a consumer being told at their own call site**, which is the
thing that was missing: before it, a consumer publishing trimmed learned about this provider's
reflection from EF Core's generic warning or from nothing at all.

### The client is missing what `EFCore.Relational` replaces, and one of those is a query service (R78, R80)

**M9 removed `InfoCarrier.Core`'s reference to `Microsoft.EntityFrameworkCore.Relational`, and the
cost of that is not only the annotations named by string.** EF registers a *different set of
services* when a provider is relational, and this client gets the core set. Most of those
differences are downstream of ADR-006's capture point and so cannot matter here. **One is not.**
`IEvaluatableExpressionFilter` governs EF's parameter extraction, which runs *before*
`IDatabase.CompileQuery`, and the relational implementation exists for exactly one reason: to stop
EF evaluating an `EF.Functions` marker whose body only throws. Without it,
`c.ContactName == EF.Functions.Collate("maria anders", collation)` was executed on the client and
raised *"the query has switched to client-evaluation"*, while
`EF.Functions.Collate(c.ContactName, collation) == "maria anders"` worked. The difference is not the
feature. It is whether the operand is a column, because a constant operand is what makes the whole
call evaluatable.

**The generalisation, and it is the useful part: a marker needs BOTH halves.** R78 admitted
`RelationalDbFunctionsExtensions` to `TypeAllowlist` so the call could be *serialized*; R80 added
the filter so a call is *left there* to serialize. Either alone looks like a partial fix and reads
like a separate defect, which is why R79's six reds were triaged into two unrelated families and
both readings were wrong.

**The measured gap this opened up, and it is bigger than either step.** A probe written and run
rather than reasoned about: `EF.Functions.Glob(c.ContactName, "*M*")` on the SQLite tier is
**refused by `QuerySplitter.RejectClientEvaluation`**, because `SqliteDbFunctionsExtensions` is not
on the allowlist and cannot be — it lives in the server's provider assembly. EF's own
`NorthwindDbFunctionsQuerySqliteTest` shows the server translates it to `GLOB` without difficulty.
**So no store-specific `EF.Functions` family crosses this wire at all**: SQLite's `Glob`/`Hex`/
`Substr`, SQL Server's `DateDiff*`/`Contains`/`FreeText`, and every third-party provider's. `Like`
works only because it is declared on the *core* `DbFunctionsExtensions`, and reading "`Like` and
`Glob` both work" off one green `Like` test is how that was missed.

**Why it was not fixed in the same session.** The allowlist is also the server's deserialization
defence (`security-review.md` §2), and admitting a class this repository cannot enumerate, in an
assembly it does not reference, is a change to the trust boundary rather than a widening of a list.
R78's two entries were nameable constants in a known assembly; "any provider's `DbFunctions`
extensions" is not. **The reproduction is two lines and belongs in the decision, not in a survey**:
add a `Glob` theory to `NorthwindDbFunctionsQueryInfoCarrierTest` mirroring EF's SQLite class, and
it fails at `QuerySplitter.cs` with `Translation of method
'Microsoft.EntityFrameworkCore.SqliteDbFunctionsExtensions.Glob' failed`.

## Three closed investigations

**The runtime culture is pinned to invariant, and that was a ratchet fix rather than a test fix
(C50).** The machine is `en-SE`, whose decimal separator is a comma, and it cost **nine**
failures — seven in the `_as_GeoJson` family, where EF's own `JsonGeoJsonReaderWriter` re-emits a
number with the culture-sensitive `StringBuilder.Append(reader.GetDecimal())` so `[2.0,4.0]` reads
back as `POINT (2 0)`, and two decimal `InlineData` parameterizations xUnit could not convert.
None was this provider's; EF's own suite failed them identically. **The reason to fix it was that
the suite total was a property of the machine** — the `test/known-failures.txt` baseline CI gates
on was true only here. A `[ModuleInitializer]` now pins `CultureInfo.DefaultThreadCurrentCulture`
before xUnit creates a thread. Nothing is skipped and no assertion is inverted.

**The one intermittent is CLOSED (C38, 2026-08-10), and how it was closed is the reusable part.**
`SqliteSmokeTest.A_store_generated_key_comes_back_on_the_client_entity` failed roughly one run in
four and passed 12-of-12 in isolation. It was **instrumented rather than chased** — C27 makes
`ServerSaveChangesExecutor` rethrow an identity conflict with the whole request appended, and
writes nothing on the happy path, which is the design that matters: the previous attempt wrote a
line per tracked entry and cost 194 extra failures through file I/O under parallel collections.
Sightings 3 and 4 arrived already diagnosed, exactly as intended, and two dumps were enough.

**The cause, and note that the standing hypothesis had the right evidence and the wrong
mechanism.** The range coincidence was real — client placeholders and EF's server-side temporaries
both count down from `int.MinValue` — but nothing was ever misidentified as a borrowed placeholder
(`borrowedReferences=[]` in both dumps). The server was letting **EF's own temporary generator**
run for a key it was about to overwrite with the client's placeholder. Entry 0 takes EF's
`-2147482647` and is forced to the client's `-2147482646`; entry 1 then takes EF's *next* value,
`-2147482646`, and the identity map refuses it. It needs the client's counter to be exactly one
ahead of the server's, which is why it was rare, order-dependent and never reproducible in
isolation. The fix is to not run the generator at all where the value is going to be replaced —
`IValueGeneratorSelector.GeneratesTemporaryValues` answers "does this store issue at save time"
*before* anything is tracked. Full reading in C11, fix in C38.

**The lesson worth keeping: an evidenced hypothesis can be right about the evidence and wrong
about the mechanism.** "The ranges coincide" was correct and load-bearing. "Therefore a stored key
is being mistaken for a borrowed placeholder" was one step too far, and the dump that confirmed the
first half refuted the second in the same four lines. Read what the instrument prints, not what it
was expected to print.

**The second intermittent is CLOSED (R76, 2026-09-01), and what closed it was reproducing the
signature rather than catching the flake.** `TPTFiltersInheritanceBulkUpdatesInfoCarrierTest` failed
18 of the 27 tests it runs in one full run and passed in the next eight, every failure
`SQLite Error 1: 'no such table: Countries'` or `'Animals'`. The class passes in isolation and its
whole namespace passes together, so the interaction was with full-suite scheduling.

**Five instrumented full runs never reproduced it, and that is the part worth generalising.** The
store was made to log every initialization -- the resolved path, whether it won the creation guard,
the table count it saw, the outcome of every `EnsureDeleted`/`EnsureCreated`/seed -- plus every
`.db` create and delete in the directory through a `FileSystemWatcher`. All five runs came back
byte-identical and clean. Sampling a one-in-nine event at eight minutes a sample was not going to
close it, and it is worth noticing that early rather than after the tenth run.

**Two facts pinned the mechanism without catching it.** First, the six tests that *passed* in the
failing run are exactly the ones refused before they reach the store, so every test that touched
the database failed: the database was empty for the whole class, not part of it. Second, EF's
`SqliteDatabaseCreator.Create` runs `PRAGMA journal_mode = 'wal'`, so every store here is a WAL
database whose content can live entirely in `<name>.db-wal` while the `.db` is a 4 KB shell -- and
a killed process's database recovers *completely* on reopen as long as that `.db` survives. An
empty database therefore means the `.db` itself did not survive. **Deleting exactly that one file
in the window between the two classes reproduced the failure to the reason**: 18 failures, 8
`Countries`, 4 `Animals`, 4 downstream `Assert.Contains`, 2 `Assert.Throws` -- the original tally.

**The mechanism, as a chain.** Six classes share three files, because the three `...Filters...`
fixtures override `EnableFilters` and nothing else and so inherit their parent's `StoreName` --
exactly as EF's own suite does. EF is safe there only because its `SqliteTestStore` uses the
*global* `TestStoreIndex`; each backend store here builds its own service provider and therefore
its own, which is why `Created` exists at all. The first class's store wins `Created.TryAdd`,
creates and seeds the file, runs, and is disposed; the second class's store is constructed **two
minutes later** and returns from the guard without looking. In between, the file stops being
protected: EF's `SqliteDatabaseCreator.Delete` calls `SqliteConnection.ClearAllPools()`
**process-globally** on every store's initialization -- some 646 times in a full run -- and about
fifteen seconds after the first class ends, one of those drops the last handle. Anything that then
deletes the file is noticed by nobody, because `Created` records that initialization was
*started*, not that its result still exists.

**The deleter in the original run was never identified, and the fix deliberately does not depend on
it.** All five instrumented runs show exactly one sweep and no deletion of any shared `.db`; both
in-process deleters are logged and neither fired. So it came from outside the process -- and
`SweepStaleFiles` is precisely that shape, deleting every `*.db` it finds and swallowing the
`IOException` on the ones a live handle protects, which is "delete every idle database" run by any
concurrent `dotnet test` in the same output directory. CI, which runs one thing at a time, has
never seen it.

**Two fixes, and the second is the one that matters.** The sweep now takes `-wal` and `-shm` as
well as `.db`, because a database here is three files and not one: **14,971 `-wal` and 14,946
`-shm` orphans had accumulated against 76 `.db`**, collected by nothing, and a fresh `.db` opened
beside a stale pair answers `no such table` -- the flake's own error text, reproducible in five
lines of Python. That leak is real but it self-heals at initialization, so it is *not* the flake's
mechanism and was not allowed to be reported as one. The fix that closes the flake is that the
creation guard is now **verified rather than trusted**: a store that did not create the database
asks whether it is still there and rebuilds it when it is not, at the cost of one `sqlite_master`
count per skip -- 71 in a full run. It cannot destroy anyone's data, because it fires only on a
database with no tables at all, which no store here legitimately has once seeded.

**The rule: a guard that records that work *started* is not evidence its result still exists.**

## How the rules were learned

`CLAUDE.md` states nine rules in nine lines. Each of these paragraphs is where one of them came
from, and they lived in `CLAUDE.md` next to the rules themselves until N31 — six of them ended by
stating, word for word, a rule printed a dozen lines below. The stories are worth keeping and the
duplication was not.

### Read the reasons diff, not the count

**The four `Scaffolding.CompiledModel` tests are the standing example of why the count is the wrong
instrument**: C90, C91 and C92 each closed a real defect and each measured **26 → 26**, because
every fix moved the same four tests one stage further in, and only C93 turned them green.
**Read the reasons diff, not the count.**

### A classification is not evidence, and age is not evidence

**There are no unexplained wrong answers, and after C85 there are almost no wrong answers at all.**
Group a run's `[FAIL]` lines by their first message line: `Assert.Equal() Failure: Values differ` is
**2**, and both are C64's `Correlated_collection_with_distinct_3_levels`, whose assertion no correct
answer can satisfy — re-derived in C96, which also confirmed EF's InMemory suite refuses the query
outright. **Re-derive it; do not restate it.** It has been wrong in both directions twice —
C65 found a green test counted as a wrong answer, and C85 found two that were EF's own documented
SQLite limitation (#33522) counted into B12 because they shared a message line. **Grep
`EFCore.Sqlite.FunctionalTests` for the name before calling a Tier B `Values differ` ours — and
apply that to old failures, not only to newly-red ones. Age is not evidence.**

### Read the row the store actually holds

**The undiagnosed exceptions are down to those inside `JsonQuery`** — C42 closed one, C61 and C62
closed the last two outside it, and all three used the same two probes in the same order: what the
metadata and the client say, then **read the row the store actually holds**. That second probe is
what turned `Array_of_TimeOnly` from "ours" into EF issue #30730 in one run. The rest are a
deliberate allowlist refusal (`Regex_IsMatch`, A46) or a known singleton. **`JsonTypes` is clear**
— the nine that stood here were A64's locale, and C50 removed them by pinning the culture.

### Before calling a family of failures a design question, check whether a sibling is green

**"The A28 family" was hiding a third instance of C40's mechanism, and the tell was in the failing
list rather than in any test (C56).** Twelve `ComplexNavigations` failures were filed under
`AssertInvalidMaterializationType` and called a decision — the assert helper is `protected static`,
so the only route seemed to be duplicating EF's query bodies. But `NorthwindMiscellaneous` asserts
*the same refusal six times and passes every one*. The difference is only where the boundary falls:
EF raises it in `QueryableMethodNormalizingExpressionVisitor`, downstream of ADR-006's capture
point, so a **wholly shippable query gets the refusal from the server and always has**, and the
twelve are the ones the split leaves on the client. **Before calling a family of failures a design
question, check whether a sibling of it is green.**

### When a rule breaks a named family of tests, read the family

**EF's prose was not implementable and EF's own tests were (C59 → C68).** "Client evaluation is
legal only in the final projection" measured **6 fixed, 18 broken** — and every one of the eighteen
named `client_eval` or `client_projection`, which made them the specification rather than the
obstacle. `Union`, `Count` and `FirstOrDefault` over a client projection are **allowed**; a
**join key** over one is **refused**. So the line is not whether an operator composes over the
projection but whether it *reads* it — `RowDecidingArguments` applied one level up. C59 missed it
by two words: it walked **lambda bodies** as well as sources (so an outer `Where` "consumed" a
subquery inside its own predicate — twelve of the eighteen), and it counted a constructed client
**type** as client code. **When a rule breaks a named family of tests, read the family: it is
usually stating the rule you actually want.**

### Ask what an assertion assumes about the topology

**A28 has a second face, and it is the one to check first when a spec test looks like a design
question.** A28 proper is a spec test asserting a *materialization* limitation this provider does
not have. B16 turned out to be the same shape asserting a *topology* it does not have: EF's test
bases are written for one `DbContext`, this provider is two, and `Assert.Same(context, …)` has no
answer under two. Three "routes" were measured against that test before anyone asked whether the
assertion was reachable at all — and every one of them would have suppressed a hook a real
deployment is entitled to define. **Ask what the assertion assumes about the topology before
treating it as a statement about the provider.**

### Two failures of the same shape are one defect until measured otherwise

**Two failures of the same shape are one defect until measured otherwise, and the shape is
usually "which of the two models was asked".** Four consecutive steps closed 152 failures with
four small changes, and every one of them was a question the client's model could not answer:
which properties may be store-generated (B6), which had no value set (B9), which navigations are
loaded (B10), and whether a no-tracking row carries complex values at all (B11). Read the
*assertion*, not the count — `AssertOwnedBranch` dereferencing a null and `AssertAddress`
comparing `expected is null` to `actual is null` each named their defect outright.

### Establish that the code ran — the stale-binary form

**A probe that prints nothing is evidence only once the build is known green — check the error
count, never the elapsed time.** M9's J9 read three successive "nothing logged" results as
clearances. All three were a **stale binary**: the probe named a property that does not exist
(`InfoCarrierFault.ExceptionType`; it is `TypeName`), so every build after it failed and every run
used the previous assembly. `dotnet build ... | Select-Object -Last 2` shows `Time Elapsed` and
hides `1 Error(s)`, which is how it survived three attempts. It produced two confident false
clearances before the real cause — an upstream bug in EF's own test type — was found. This is the
standing "establish that the code *ran*" rule, in the one form it had not yet been broken in.

### Before pricing a gap, check whether a sibling of it already works

The rule in `CLAUDE.md` about `ExecuteWithStrategyInTransactionAsync` used to end by declaring two
bases permanently unreachable. It was wrong, and this is the correction it carried for two
milestones.

J3 moved `ProxyGraphUpdates` on the strength of 13 mirrored skips and measured **733 failures, 717
of them that class, 471 of them `SQLite Error 5: 'database is locked'`** — each waiting out a
30-second lock timeout, which is why that run took hours rather than minutes.

**CORRECTED 2026-08-17 (M9), and the correction is the operative half.** This entry used to end
*"a base that uses it cannot move until the client can join an open server transaction by its wire
token — a product feature that does not exist"*, and named `GraphUpdates` and `ProxyGraphUpdates`
as permanently Tier A. **Nothing was missing.**
`InfoCarrierDatabaseFacadeExtensions.UseInfoCarrierTransaction` and
`InfoCarrierTransactionManager.UseTransaction(token)` have shipped since M4, with the non-owning
semantics the question worried about. What was missing was the **test class's own
`UseTransaction` override**, which `ConferencePlannerInfoCarrierTest` and
`OptimisticConcurrencyInfoCarrierTest` already carried — one grep away the whole time. Both bases
are now on **Tier B and green**: `ProxyGraphUpdates` in J11 (167 fixed, 0 broken) and
`GraphUpdates` in J12a, with **zero** "database is locked" because the override landed with the
store switch. Full reading in `architecture.md` §6a **D6**, closed. **The general lesson is the one
`CLAUDE.md` states elsewhere and this entry broke: before pricing a gap, check whether a sibling of
it already works.**
A relational suite normally enlists with `transaction.GetDbTransaction()`, which ADR-013 does put
permanently out of this client's reach — that part was always true, and it is why the override is
needed rather than why the move is impossible.

### What the wire drops is invisible until a store enforces it

Three defects, one mechanism, and it took an audit from the other end to close the class.

EF's `CommandBatchPreparer` orders a write batch from **original** values: a dependent's original
foreign key says which principal it is releasing, and a unique value's original says which row is
giving it up. A single-context EF never loses an original — it is the value the row was loaded
with. **A wire loses every one of them by construction**: the server rebuilds an entity from the
*current* values it was sent, attaches it and sets its state, and EF then snapshots originals from
the entity it was just handed. So on the server every original equals its current unless the client
explicitly sent otherwise, the dependency graph has no edges to build, and the batch goes out in an
order the store refuses.

**Tier A cannot show any of it, because InMemory enforces no constraint.** Each of the three
surfaced when a base moved to a store that does.

| | What was missing | What the store said | Found by |
|---|---|---|---|
| J11 | foreign-key originals, at all | `FOREIGN KEY constraint failed`, **165 of them** | `ProxyGraphUpdates` reaching Tier B |
| R40 | foreign-key originals on `Deleted` entries | `FOREIGN KEY constraint failed` | `ManyToManyTracking` reaching Tier B |
| R42 | originals for unique-index properties | `UNIQUE constraint failed: Products.Name, Products.Price` | `Updates` reaching Tier B |

**R40 is the one with a rule in it.** The condition had carried a comment asserting that *"a
`Deleted` entry needs no ordering hint, because the row it releases is the one being deleted"* —
which is true for every deleted row that is only ever a dependent, and false the moment **one
deleted row is a dependent of another**. `Can_delete_with_many_to_many` deletes an `EntityOne` and
an `EntityTwo` whose `CollectionInverseId` points at it; EF's own `ClientSetNull` fixup nulls that
foreign key on the client before the entry is sent, so the *current* value carries no edge either,
and the server had nothing at all to order by.

> **A design comment that says a case "needs no X" is a claim about every instance of that case.**
> Read it as a quantifier, and look for the instance that breaks it.

**R42 widened the mechanism rather than repeating it.** `CommandBatchPreparer` orders by *value*
dependencies as well as row ones: two rows swapping a unique index value have to be sequenced, and
neither property is a foreign key or a concurrency token, so the condition R40 had just corrected
still did not carry them.

**R44 is what should have happened after R40, and it is the transferable half.** Two defects of one
shape found by accident three hours apart is a signal to stop finding them one at a time. The audit
enumerated every value `CommandBatchPreparer` reads from originals and diffed that list against
what `ChangeEntryMapper.ToChangeEntry` sends, and came back **negative**: the two remaining rows are
principal keys and unique constraints, both of which are *key* properties, and a key property's
original can never differ from its current because EF fixes its `AfterSaveBehavior` at `Throw` and
refuses to let a model configure anything else. A documented negative closes the class; a third
accident would only have moved it.

> **Anything the wire drops that a single-context EF never loses is invisible until a store
> enforces it.** Original values are the standing example, and the way to close such a class is to
> enumerate what the far end reads, not to wait for the next store to complain.

### A base belongs to exactly one tier

The evidence behind the tier rule, which `CLAUDE.md` now states without it. Two bases were reverted
on the "EF ships no InMemory test, therefore out of scope" mistake, and both pass on Tier B, first
run, no overrides (A79). A80 deleted a workaround for a store capability by moving the class that
needed it rather than by writing around it, which is where the "check the tier before writing the
workaround" tell comes from. And three Northwind bases ran on both tiers, green on both — 906 tests
of pure duplication (A81), which is what "exactly one tier" is protecting against.
