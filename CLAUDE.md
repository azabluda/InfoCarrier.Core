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
| `eng/ratchet.sh <results.trx> <baseline-file>` | **CI only.** The suite is legitimately red during build-out and tests must not be skipped to force it green, so CI gates on the *direction* of the failure count. It guards the **total** as well: a crashed host reports fewer failures because fewer tests ran, which once came within one measurement of looking like an improvement. Nothing invokes it today — CI is broken (see *Current state*), and fixing CI means wiring this up. |
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

## Current state

Query, projection split and SaveChanges all work end-to-end. The suite stands at
**`Total tests: 20560, Passed: 20188, Failed: 173, Skipped: 199`** (2026-08-04) across the
Northwind query bases and `GraphUpdatesTestBase`, `PropertyValuesTestBase`, `FindTestBase`,
`LoadTestBase`, `ManyToManyTrackingTestBase`, `FieldMappingTestBase`, `WithConstructorsTestBase`,
`CompositeKeyEndToEndTestBase`, `NotificationEntitiesTestBase`, `ComplexTypesTrackingTestBase`,
`ComplexNavigationsQueryTestBase`, `GearsOfWarQueryTestBase`, all sixteen
`Query.Translations` bases, the nine `ModelBuilding.ModelBuilderTest` bases and the five
`AdHoc*Query` bases, `OwnedQueryTestBase` and the shared-type query bases on Tier A, plus
`OptimisticConcurrencyTestBase` on Tier B. `PropertyValues`, `Find`, `ManyToManyTracking`,
`CompositeKeyEndToEnd`, `NotificationEntities`, `FieldsOnlyLoad`,
`OverzealousInitialization`, `FieldMapping`, `Load` and both `ManyToMany*Load` bases are clear.
**Every failure is classified — A54 in `docs/implementation-plan.md` for the 44 that predate A59,
the A59/A61/A62/A63/A65 tables for the 75 those batches added**, read out of `artifacts/measure/`. Only **4** are wrong answers
(`Correlated_collection_with_distinct_3_levels`, `Comparison_with_value_converted_subclass`) and
**6** are undiagnosed exceptions; **12** are the A28 shape — a spec test asserting a materialization
limitation this provider does not have, whose query body is inline in a `protected static` assert
helper so the assertion cannot be inverted from a derived class. The rest are a deliberate allowlist
refusal (`Regex_IsMatch`, A46) or a known singleton.

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
- **The remaining spec bases** — 47 the compliance test still reports unadopted. Phase A in
  `docs/implementation-plan.md`; adopt in batches and classify what turns red. A49 built
  `NonSharedModelInfoCarrierHarness`, so every remaining `NonSharedModelTestBase` suite
  (`SharedTypeQuery`, `OwnedEntityQuery`, `AdHocComplexTypeQuery`, `AdHocJsonQuery`,
  `NonSharedModel*`, `Scaffolding`) is now adoptable the same way — two forwarded members per
  class.
- **`Query.Associations.*` is 34 of the 80** and has no InMemory counterpart — a roadmap
  question (Tier B, or out of scope), not a plan one. Do not start it without asking.
- **CI is broken** — `.github/workflows/build.yml` restores `InfoCarrier.Core.sln` (repo has
  `.slnx`), and its `~InMemory` / `~SqlServer` filters match no current test class.

The Tier B store is **file-backed** (`<StoreName>.db` in the test output directory), as EF
Core's own `SqliteTestStore` is. Do not move it back to `Mode=Memory;Cache=Shared`: that makes
the database's lifetime a connection's, which makes test-class disposal order load-bearing and
has already produced a 698-test phantom failure. For the same reason **the store must not delete
its file on disposal, and must not release its `Created` entry either** — either one
reintroduces the coupling. The second half survived S3c-5 and produced a nine-test intermittent
failure once the suite passed ten thousand tests: a shared store's disposal re-armed the guard
and let a later class re-seed the file a live one was still using. `DisposeAsync` now releases
nothing. Stale files are swept once at startup instead.

**The runtime culture on this machine is `en-SE`, whose decimal separator is a comma.** Four
`JsonTypes` decimal parameterizations fail here purely because xUnit cannot convert their
`InlineData` strings, and EF's own suite fails them the same way (A64). The suite total is
therefore **locale-dependent** — a machine with a `.` separator reports two fewer failures with no
code change. Grep a run for *"cannot be converted to type"* before treating that as movement.

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
