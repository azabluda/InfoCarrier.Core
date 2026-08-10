# CLAUDE.md

EF Core 10 database provider that remotes LINQ queries and change-tracking over a wire
protocol. Client `DbContext` has no database; the server executes against a real provider.

## Commands

```powershell
dotnet build InfoCarrier.Core.slnx                       # note: .slnx, not .sln
dotnet test  InfoCarrier.Core.slnx                       # full suite
dotnet test  InfoCarrier.Core.slnx --filter "FullyQualifiedName~NorthwindWhere"
```

Report test results as `Passed: N, Failed: M, Total: T` from actual output — never estimate
or infer a count.

Everything in `eng/` — there is nothing else, and no script does anything a comment in it does
not explain:

| Script | What it is for |
|---|---|
| `eng/measure.sh <label> [baseline]` | The way to measure a change. See below. |
| `eng/ratchet.sh <results.trx> <baseline-file>` | **CI only**, and wired: `.github/workflows/build.yml`'s *spec-ratchet* job invokes it against `test/known-failures.txt`. The suite is legitimately red during build-out and tests must not be skipped to force it green, so CI gates on the *direction* of the failure count. It guards the **total** as well: a crashed host reports fewer failures because fewer tests ran, which once came within one measurement of looking like an improvement. **The baseline is read out of the TRX, never out of the console block and never by arithmetic** — the TRX is what this script parses, and its `total` counts the skips its `passed` and `failed` do not. |
| `eng/gate.sh` | A detached delay used to schedule an unattended session. Sleeps 7200s, then writes `artifacts/gate-open.txt`. Not part of the build; delete it if it stops being useful. |

**Measuring a change: `eng/measure.sh <label> [baseline]`** (or the `/experiment` skill, which
wraps the whole loop). It prints the count, the exact list of tests fixed and broken, *and* a
diff of the failure **reasons** — three levels, because each one hides a mistake the level below
it catches:

- the count alone cannot tell "fixed 4, broke 4" from "changed nothing";
- the fixed/broken lists alone cannot tell "changed nothing" from "fixed what it aimed at and
  uncovered the next problem in the same tests" — both leave the name list byte-identical. That
  one produced a wrong revert (plan L8) after two runs were read as neutral.

Never state a verdict from partial output. Two specific errors have each cost a wrong revert
here, and both are cheap to avoid:

- **A count that did not move does not mean the target does not exist.** A matcher that never
  fired and a rewrite that did not help look identical from outside. Establish that the code
  *ran* — a probe writing to a file, since xUnit swallows stdout — before concluding anything
  about the problem.
- **"EF ships no InMemory test for this base" means move it to Tier B, not drop it.** ADR-009 has
  two tiers precisely because InMemory cannot host everything, and a base adopted on the wrong one
  produces failures that describe the *backing store* rather than this provider. Two bases were
  reverted on that mistake and both pass on Tier B, first run, no overrides (A79). Only "EF ships no
  test for it on any store we have" justifies leaving a base unadopted. The tell: **if adopting a
  base means writing a workaround for a store capability the base assumes, check the tier before
  writing the workaround** (A80 deleted one such workaround by moving a class). And **a base belongs
  to exactly one tier** — three Northwind bases ran on both, green on both, 906 tests of pure
  duplication (A81). When a base could go either way, the tier that *translates* is the one whose
  green means more.
- **A newly-red SQLite test is not automatically a regression.** Grep
  `subrepos/efcore/test/EFCore.Sqlite.FunctionalTests` for the name first: if EF overrides it
  with `ApplyNotSupported`, the query now reaches SQL and this is convergence with the reference
  provider. Adopt EF's override. **Grep `EFCore.Relational.Specification.Tests` too** — a limit
  every relational provider has is overridden on the relational *base*, not in SQLite's own
  suite, and reading only the latter had `Reverse_without_explicit_ordering` classified as a real
  failure for two sessions. The reverse also happens — an override of ours that EF does *not*
  have is a workaround to delete once the limitation goes.

## Where authority lives

`docs/` is the source of truth. Read before changing design, and keep it current:

| Doc | Role |
|---|---|
| `docs/decisions.md` | **ADR log.** LOCKED entries are binding. |
| `docs/infocarrier-core-requirements.md` | Authoritative requirements spec |
| `docs/roadmap.md` | **Stable** milestone plan for the whole project |
| `docs/implementation-plan.md` | **Rolling** checkbox detail for the *current* milestone only |
| `docs/architecture.md` | Components, test strategy, open questions |
| `docs/research-findings.md` | EF Core 10 pipeline findings backing the ADRs |

**Roadmap vs plan — do not mix them.** Milestone-level scope, ordering, and exit criteria go
in `roadmap.md`, which changes only when scope changes. Per-task checkboxes go in
`implementation-plan.md`, which is rewritten at each milestone boundary (previous ones land in
`docs/archive/`, never edited again). Putting task detail in the roadmap, or scope changes in
the plan, is what caused the drift these two docs replaced.

**Reversing a LOCKED ADR requires a dated supersession edit in `docs/decisions.md`** — not a
code change that quietly contradicts it. ADR-001 (greenfield serializer, no Remote.Linq/Aqua
dependency), ADR-004 (inherit `EFCore.Specification.Tests`), and ADR-006 (raw capture at
`IDatabase.CompileQuery`) are the ones most likely to be violated by accident.

## Guardrails

**Never edit anything under `subrepos/`.** Those are git-ignored reference clones of
`efcore`, `rlinq`, `aqua`, and `infocarrier-v1`, kept for source-level study. `efcore` is the
authoritative EF Core 10 reference — grep it to confirm API shapes rather than guessing.
Edits there are invisible to git and will be lost.

**Never `[Skip]`, delete, or override a spec test to make the suite green.** The inherited
`EFCore.Specification.Tests` classes *are* the coverage goal (ADR-004); a red test is
information. If a test targets genuinely unimplemented functionality, leave it failing and
note it in `docs/implementation-plan.md`. Silently suppressing tests was v1's stated failure
mode.

**Update the plan checkbox in the same commit as the work.** `docs/implementation-plan.md`
drifted out of sync with git once already (F1–F7 were committed while still shown unchecked).
One substep per commit, message prefixed `Step <id>:`.

**EF1001 warnings are expected and allowed.** This provider legitimately depends on EF Core
internals (`IStateManager`, `EntityQueryable<>`, `InternalEntityEntry`). Do not suppress them
repo-wide and do not refactor to avoid them — but do prefer public API where one exists.

**Do not add a NuGet dependency on Remote.Linq or Aqua** (ADR-001). They are specification
material only.

**Anything the wire computes from a type mapping is computed twice, by two different providers,
and is only sound if the two agree.** The client's model is built by this provider and the
server's by the backing store, so `FindTypeMapping()` is not one answer but two. B4: a
`DateTime[]` was written by SQLite's JSON form (`2023-01-01 12:30:00`) and read by EF's core one
(ISO-8601), 106 failures in both directions. Scalars are safe because `PrimitiveCoercion`
short-circuits the wire primitives before any mapping is consulted; anything else must be derived
from the **CLR type alone**, through a service no provider replaces.

## Current state

Query, projection split and SaveChanges all work end-to-end. The suite stands at
**`Total tests: 22312, Passed: 21951, Failed: 144, Skipped: 217`** (2026-08-10) across the
Northwind query bases and `GraphUpdatesTestBase`, `PropertyValuesTestBase`, `FindTestBase`,
`LoadTestBase`, `ManyToManyTrackingTestBase`, `FieldMappingTestBase`, `WithConstructorsTestBase`,
`CompositeKeyEndToEndTestBase`, `NotificationEntitiesTestBase`, `ComplexTypesTrackingTestBase`,
`ComplexNavigationsQueryTestBase`, `GearsOfWarQueryTestBase`, all sixteen
`Query.Translations` bases, the nine `ModelBuilding.ModelBuilderTest` bases and the five
`AdHoc*Query` bases, `OwnedQueryTestBase` and the shared-type query bases on Tier A, plus
`OptimisticConcurrency`, `ConferencePlanner`, `FunkyDataQuery`, `ComplexTypeQuery`,
`AdHocComplexTypeQuery`, `PrimitiveCollectionsQuery`, `NonSharedPrimitiveCollectionsQuery`,
`JsonQuery`, `StoreGenerated`, the sixteen `Types.TypeTest` classes, `Query.Associations.*` and
`BulkUpdates.*` on Tier B, and the two spatial bases on Tier A.
`PropertyValues`, `Find`, `ManyToManyTracking`,
`CompositeKeyEndToEnd`, `NotificationEntities`, `FieldsOnlyLoad`,
`OverzealousInitialization`, `FieldMapping`, `Load`, `Updates`, `WithConstructors`,
`ComplexTypeQuery`, `Spatial` and both `ManyToMany*Load` bases are clear.
**Every failure is classified — A54 in `docs/implementation-plan.md` for the 44 that predate A59,
the A59/A61/A62/A63/A65 tables for the 75 those batches added, Phase B's B3a–B16 for what the
Tier B adoptions added, and Phase C's C1–C20 for the rest** — read out of `artifacts/measure/`,
currently `c40b`. The total grew from 22278 to 22312 across C36–C38: 34 tests added for the
node-kind and payload-size controls, no movement in the failing count; C40 then took 145 to 144. The largest blocks are **40 `JsonQuery`** (38 of them B12, a decision), **26
`MaterializationInterception`** (16 are B16's topology, answered and classified; 10 blocked by
A71), **20 `ComplexNavigations`**, **14 `Query.Associations`** (C20) and **9 `JsonTypes`** (7 of
them A64's locale, not this provider). Only **4** are wrong answers
(`Correlated_collection_with_distinct_3_levels`, `Comparison_with_value_converted_subclass`
— the latter diagnosed in full and reverted, B23) and
**6** are undiagnosed exceptions; **12** are the A28 shape — a spec test asserting a materialization
limitation this provider does not have, whose query body is inline in a `protected static` assert
helper so the assertion cannot be inverted from a derived class. The rest are a deliberate allowlist
refusal (`Regex_IsMatch`, A46) or a known singleton.

**`Skipped` is 217, and was 217 in `c10b` too.** The number recorded against `c10b` was `208` —
carried over from `b21b`, where it was right — and `Passed` was then derived from it rather than
read. Phase C's adoptions brought nine of EF's own skips with them. `Failed` and `Total` were
correct throughout, so nothing was judged wrongly, but **all four figures come out of the run's
own summary block; none of them is arithmetic.**

**A28 has a second face, and it is the one to check first when a spec test looks like a design
question.** A28 proper is a spec test asserting a *materialization* limitation this provider does
not have. B16 turned out to be the same shape asserting a *topology* it does not have: EF's test
bases are written for one `DbContext`, this provider is two, and `Assert.Same(context, …)` has no
answer under two. Three "routes" were measured against that test before anyone asked whether the
assertion was reachable at all — and every one of them would have suppressed a hook a real
deployment is entitled to define. **Ask what the assertion assumes about the topology before
treating it as a statement about the provider.**

**Two failures of the same shape are one defect until measured otherwise, and the shape is
usually "which of the two models was asked".** Four consecutive steps closed 152 failures with
four small changes, and every one of them was a question the client's model could not answer:
which properties may be store-generated (B6), which had no value set (B9), which navigations are
loaded (B10), and whether a no-tracking row carries complex values at all (B11). Read the
*assertion*, not the count — `AssertOwnedBranch` dereferencing a null and `AssertAddress`
comparing `expected is null` to `actual is null` each named their defect outright.

Lazy loading works (Phase L): it began at 505 of 505 failing and is **825 of 825** across
`LoadInfoCarrierTest` and `LazyLoadProxyInfoCarrierTest`.

Not yet implemented, in rough priority order:
- **Complex types work** (A32) — `ComplexTypesTrackingTestBase` is **249 of 251**, and the two
  left are one shape of one feature: a property-bag complex *collection* on an `Added` entity,
  which fails inside EF's own `StructuralTypeMaterializerSource`. A complex value cannot ride in
  the value dictionary an entity is built from — `CreateEntry` and `ShadowValuesFactory` are
  name-keyed and complex leaves collide (`Culture.Species` and `Milk.Species` are both
  `"Species"`) — so both sides set it through its CLR member instead. The
  `Query.Associations.ComplexProperties` family is **not** adoptable on Tier A (A77): EF's InMemory
  provider does not translate a complex property access at all, which is why EF ships no InMemory
  complex-type query test. Complex-type *queries* need Tier B.
- **The query residual** — 2, and **neither is a gap**. A40 closed
  `SelectMany_correlated_subquery_hard`, the correlated subquery under a client-side projection
  that **milestone M2-B existed for**, and A43 closed `Select_GroupBy_SelectMany`. The 2 left are
  spec tests asserting a limitation this provider does not have — they run and return the right
  answer, and the query bodies are `private` to the spec base, so the assertion cannot be inverted
  from a derived class.
- **The `GraphUpdates` residual** — 1 of 1787, one parameterization of
  `Save_optional_many_to_one_dependents`. Classified in `docs/implementation-plan.md` under S3c,
  which is read out of `artifacts/measure/` rather than tallied by hand — the table it replaced
  had drifted badly.
- **The remaining spec bases — 1** (Phase C, 2026-08-10: 41 adopted down to 1). Everything the
  compliance test used to list is in, including the four "infrastructure" bases, `Seeding` (C7) and
  **both spatial bases, 169 of 173** (C18). The one left is **`AdHocJsonQuery`** — B3d's price
  re-checked in C10 and it holds: 626 + 322 lines of relational mirror and seven abstract seeds only
  EF's relational classes implement, and the corpus is owned JSON collections throughout so most of
  what it adds lands on **B12**, which is undecided.
- **Spatial works, and the shape of how is worth keeping.** Three pieces, landed and measured
  separately because C9's combined attempt aborted the host: the NetTopologySuite branch in
  `InfoCarrierTypeMappingSource` (C15, worth 19 on its own — the long-standing "needs SpatiaLite"
  note was wrong, and the provider that could not map a `Point` was *the client*); **ADR-012's
  value-mapper seam** (C17); and a **WKT** geometry mapper registered **test-side** (C18), which is
  why the product assembly still does not reference NetTopologySuite. Not GeoJSON — it carries no Z
  or M, which is the v1 defect requirements §2.8 records.
- **The seam is the general answer to "a CLR type the wire cannot walk", and it now has three
  consumers.** A geometry's members recurse (C18), `IPAddress.ScopeId` throws for an IPv4 address
  (C23), and `Uri.AbsolutePath` throws for a relative URI (C34). Three unrelated CLR types, one
  mechanism, all reached by the same reflective object-shape walk.
  **A DECISION IS WAITING on the back of that**: all three mappers are registered *test-side*, so
  the suite is green and a real application hitting any of them is not. v1 shipped
  `StandardValueMappers` in its product assembly; ADR-012 says registration is the application's.
  Two of the three are BCL types. **Recorded in C23 and C34; not taken, because it is a scope
  call.**
- **"The wire cannot handle this type" has two answers and they are not interchangeable** (C34).
  The seam decides how a value is *written*; `ExpressionJsonContext` decides whether the wire can
  carry the result at all — a key value lands in `EntityKeyNode.KeyValues`, declared `object` and
  resolved by runtime type, which the seam never sees. A converted key exercises both, and fixing
  only the first moves the failure rather than closing it.
- **C18's `GeometryCollection` gap turned out not to need the type-level probe** it proposed (C24).
  `ProjectionRewriter` was substituting a `List<T>` for a declared type a `List<T>` does not
  satisfy; one clause fixed it and ADR-012 needed no amendment.
- **`MaterializationInterception` is 27 and is *not* a decision (B16, answered 2026-08-09).** This
  provider is **two EF instances**, and a real deployment must be free to define materialization
  hooks on either side or both — so the three routes B16 measured, each of which suppresses one
  side, may none of them be taken. Nothing in `src/` forwards an interceptor: the server sees the
  user's only because `InfoCarrierBackendTestStore.AddProviderOptions` forwards the client's
  `onConfiguring` for model parity (A49) and it rides along. Each side is individually correct —
  `"Intercepted: Intercepted:"` proves two invocations, `Assert.Same` proves they carry different
  contexts, and B15's fix landing on the *client's* materializer proves the client raises it. **The
  A28 family, one level up**: A28's spec tests assert a materialization limitation this provider
  does not have, these assert a *topology* it does not have. Red and classified. Making them green
  is a harness change (stop forwarding interceptors, register the backend's additively) and is
  optional.
- **JSON-mapped owned collections need a decision (B12).** The server keys an element by its
  ordinal in the array; the client keys it by the CLR `Id`, which the document does not carry and
  which is `0` for every element — so EF's fixup gives every element to every owner. Both sides
  run the same `OnModelCreating`; only the *convention* that rewrites such a key is relational.
  38 failures, and no client-side route that does not either invent the ordinal or overwrite a
  property a query can project.
- **`Query.Associations.*` + `BulkUpdates.*` are adopted and green — 322 of 336 and 251 of 257.**
  The standing "no InMemory counterpart, therefore out of scope" note was the A79 mistake again;
  they are Tier B (C0–C4), and C19/C20 took them the rest of the way. What is left is 14 + 6, all
  classified in C20.
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

The Tier B store is **file-backed** (`<StoreName>.db` in the test output directory), as EF
Core's own `SqliteTestStore` is. Do not move it back to `Mode=Memory;Cache=Shared`: that makes
the database's lifetime a connection's, which makes test-class disposal order load-bearing and
has already produced a 698-test phantom failure. For the same reason **the store must not delete
its file on disposal, and must not release its `Created` entry either** — either one
reintroduces the coupling. The second half survived S3c-5 and produced a nine-test intermittent
failure once the suite passed ten thousand tests: a shared store's disposal re-armed the guard
and let a later class re-seed the file a live one was still using. `DisposeAsync` now releases
nothing. Stale files are swept once at startup instead.

**The runtime culture on this machine is `en-SE`, whose decimal separator is a comma**, and it
costs **nine** failures, all in `JsonTypes` and none of them this provider's. Two are decimal
parameterizations xUnit cannot convert from their `InlineData` strings, and EF's own suite fails
them the same way (A64). The other seven are the `_as_GeoJson` family: EF's own
`JsonGeoJsonReaderWriter` re-emits a number with `StringBuilder.Append(reader.GetDecimal())`, which
is culture-sensitive, so `[2.0,4.0]` comes back as `[2,0,4,0]` and the point reads as
`POINT (2 0)`. `line_string_as_GeoJson` passes by luck, its ordinates being 0 and 1. The suite
total is therefore **locale-dependent** — a machine with a `.` separator reports nine fewer
failures with no code change. Grep a run for *"cannot be converted to type"* and for
`_as_GeoJson` before treating either as movement.

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

**The suite is deterministic. Run it once.** Do not re-run to "confirm" a result — `measure.sh`
already ran it, and repeating that is minutes of wall clock buying nothing. Flakiness is not the
default assumption.

**If you do notice flakiness, it becomes the top priority — before whatever you were doing.** The
signal is a run that differs from the previous snapshot with **no code change between them**;
that is the only thing that justifies suspecting it. Stop, find the cause, fix it, and only then
go back to the work. A flake left in place poisons every measurement after it, which is how this
repo lost a day to a 698-test phantom failure and later to a nine-test intermittent one. Verify
the fix with three consecutive identical runs — *that* is what the three-run bar is for, not for
routine work.
