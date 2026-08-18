# CLAUDE.md

EF Core 10 database provider that remotes LINQ queries and change-tracking over a wire
protocol. Client `DbContext` has no database; the server executes against a real provider.

## Commands

```powershell
dotnet build InfoCarrier.Core.slnx                       # note: .slnx, not .sln
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj   # full suite
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --filter "FullyQualifiedName~NorthwindWhere"
```

Point test runs at the `.csproj`, not the `.slnx` — the solution also contains
`test/InfoCarrier.Core.TransportTests` (17 tests), and running both together inflates `Total`
past what `test/known-failures.txt` was written against. `eng/measure.sh` was scoped to the
`.csproj` for the same reason; a hand run must be scoped the same way or its count is not
comparable. The transport tests are a separate project, run separately:
`dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`.

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
- **Which gate to run before which commit.** `eng/measure.sh` says nothing about trimming, and the
  trim gate says nothing about behaviour — they are separate axes and M9's J8 was committed green on
  one while failing the other in CI.
  | Change touches | Run |
  |---|---|
  | `src/` | **both** `eng/measure.sh` and `eng/trim-ratchet.sh` |
  | `test/` only | `eng/measure.sh` |
  | `docs/`, `eng/` text only | neither |

  The trim ratchet is a clean publish: ~41 s in CI, about a minute locally. That is cheap enough
  that "product code changed" is the whole trigger — do not try to judge whether a change *looks*
  reflective, because `WireGrouping` did not look like five warnings.

  **And the build itself is a gate now.** Warnings are errors when `CI=true`, so before any commit
  that touches code: `CI=true dotnet build InfoCarrier.Core.slnx --configuration Release`.
  **`--configuration Release` is not optional and leaving it off is how CI went red for ten
  commits without anyone noticing (N12).** The Blazor sample turns the trim analyzer on in Release
  only, so the Debug build cannot produce the diagnostic that fails the server. It is seconds, it
  is what the server runs, and a Debug build will not show you the failure. `docs/build-warnings.md` says what
  is suppressed and why.
- **A probe that prints nothing is evidence only once the build is known green — check the error
  count, never the elapsed time.** M9's J9 read three successive "nothing logged" results as
  clearances. All three were a **stale binary**: the probe named a property that does not exist
  (`InfoCarrierFault.ExceptionType`; it is `TypeName`), so every build after it failed and every run
  used the previous assembly. `dotnet build ... | Select-Object -Last 2` shows `Time Elapsed` and
  hides `1 Error(s)`, which is how it survived three attempts. It produced two confident false
  clearances before the real cause — an upstream bug in EF's own test type — was found. This is the
  standing "establish that the code *ran*" rule, in the one form it had not yet been broken in.
- **Before moving a base to Tier B, grep it for `ExecuteWithStrategyInTransactionAsync` — and if it
  uses one, write the `UseTransaction` override in the same commit as the store switch.** That
  helper opens **one** transaction and then requires **every other context** to enlist in it. On
  Tier A the transaction is ignored and nothing shows; on Tier B it is real, and without the
  override the inner contexts stay outside it while the outer one holds the store's write lock. J3
  moved `ProxyGraphUpdates` on the strength of 13 mirrored skips and measured **733 failures, 717 of
  them that class, 471 of them `SQLite Error 5: 'database is locked'`** — each waiting out a
  30-second lock timeout, which is why the run took hours rather than minutes. **The tell is not in
  the skips and not in the fixture: it is the base's own transaction strategy.**
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
  store switch. Full reading in `architecture.md` §6a **D6**, closed. **The general lesson is the
  one this file states elsewhere and this entry broke: before pricing a gap, check whether a sibling
  of it already works.**
  A relational suite normally enlists with `transaction.GetDbTransaction()`, which ADR-013 does put
  permanently out of this client's reach — that part was always true, and it is why the override is
  needed rather than why the move is impossible.
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
| `docs/decisions.md` **ADR-013** | The test project may reference `EFCore.Relational.Specification.Tests`. **Before adopting a relational spec base, check whether it assumes the *client* is relational** — a non-virtual `UseTransaction` calling `GetDbTransaction()` makes a base unreachable here, and cost 142 tests to discover. |
| `docs/security-review.md` | **M5's review of the deserialization path** (C48). Read §2 before adding anything to `TypeAllowlist`: its safety is a conjunction across several clauses, and `Binder`/`MethodInfo`/`Activator` each break it alone. |
| `docs/build-warnings.md` | **Which warning codes are fatal, which are suppressed, where, and why.** The build is clean (`0 Warning(s)`) and warnings are errors **in CI only** — `CI=true dotnet build InfoCarrier.Core.slnx --configuration Release` reproduces it — the configuration matters. Read before adding any `NoWarn`. |

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

**EF1001 usage is expected and allowed; the warning is suppressed per file, EF's own way.** This
provider legitimately depends on EF Core internals (`IStateManager`, `EntityQueryable<>`,
`InternalEntityEntry`). Do not refactor to avoid them — but do prefer public API where one exists.

The 19 files that use internals carry a **file-scoped** `#pragma warning disable EF1001` under a
two-line comment naming the reason. That is what EF Core's own providers do and it was checked
before it was copied: `subrepos/efcore` has **51 files** with `#pragma warning disable EF1001`
across eight projects (21 in `EFCore.Relational` alone), some file-scoped and some in narrow
pairs, and **no `NoWarn` for EF1001 anywhere in the repository**.

**Do not add `NoWarn=EF1001`** to a project or to `Directory.Build.props`. The pragma is per file
on purpose: a *new* file that reaches for an internal API still warns, which is the tripwire that
keeps "prefer public API where one exists" enforceable. A `NoWarn` would remove it silently.

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

**M9 is CLOSED (2026-08-17), M8 is NOT.** M9's four exit criteria are met — the document-mapping
seam (so `InfoCarrier.Core` no longer references `EFCore.Relational`), the test project organised
by backend store, four bases moved to the tier that translates, and the capability axis
*identified, decided and recorded* rather than built (`architecture.md` §6a **D5**, answer (c);
the original wording required a second backend, which M9 excluded). Task detail is archived at
`docs/archive/implementation-plan-m9-phase-j.md` and is never edited again;
`docs/implementation-plan.md` holds M8's Phases H and I only.
**The nine remaining failures now have a consumer-facing statement — `docs/limitations.md`** — and
that is the document to keep true, because it is the only one written for someone outside this
repository. It says one unsupported scenario, one query to treat with caution, two message-text
differences, and two queries this provider *answers* that other EF providers reject.
**Six standing classifications were found wrong when checked against EF's own suites in the closing
session alone**, one of which had read "SQLite-tier, a store limitation" for two milestones and was
ours, one line (J19). *A classification is not evidence, and age is not evidence.*

**M6 is CLOSED (2026-08-11).** Every spec base EF ships that this provider can host is adopted —
`InfoCarrierComplianceTest.All_test_bases_must_be_implemented` is green, `AdHocJsonQuery` last and
61 of 61. It closed in four steps once B12 was decided: C80 took B12 (36 fixed), C81 added ADR-013
and the JSON *write* coverage, C82 adopted the last base, C83 fixed the exception-fidelity gap C82
found. **The standing price for all of it — "626 + 322 lines of relational mirror and seven
abstract seeds" — was a price for a route nobody was going to take.**

Query, projection split and SaveChanges all work end-to-end. The suite stands at
**`Total tests: 22656, Passed: 22466, Failed: 13, Skipped: 177`** (2026-08-17, `j15`) across the
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
Tier B adoptions added, and Phase C's C1–C96 for the rest — **C96 re-derives the whole tail**, and
**Phase J's "The residual 13, examined properly" re-derives it again against M9's run** — read out of `artifacts/measure/`,
currently `j15`. C55–C96 took 132 to 13, and none of the 13 is C86's own new tests — those are 5 of 5; `Query.Associations` is 336 of 336, and
`MaterializationInterception`, `OptimisticConcurrency` and `ComplexNavigations` are all clear.
**There is no largest block any more: the 13 sit in eight different classes — five hold two each,
three hold one each** (grouped from `artifacts/measure/j15.txt`; the earlier "six pairs and one
singleton, in seven classes" was C96's composition and M9's tier moves changed it). **After J15 not
one of them is of unknown standing**, which had not been true before: J15 seeded the `Dashboard`
set EF leaves empty and showed that `Composition_over_collection_of_complex_mapped_as_scalar`
returns the **right answer**, so it is A28 proper rather than A28 by assertion.
`JsonQuery` fell from 40 to 4 when C80 took B12 and to **0** when C96 took the
eighteenth of EF's own APPLY overrides, `PrimitiveCollectionsQuery` from 4 to 1 when C88 took B22,
`Scaffolding.CompiledModel` from 4 to **0** across C90–C93, and `BulkUpdates` from 6 to 2 when C94
took EF's own #28886 skip. **All 13 are re-derived one at a time in C96** — grouped by class *and*
by message, with EF's own suites grepped for every name.
**`total` fell to 22453 in C94 and that was deliberate** — xUnit reports a *skipped theory* as one
test rather than as its parameterizations, so two skipped theories turn 4 tests into 2. It is the
only lowering `test/known-failures.txt` has, and it is written up there. A shrinking total with no
such note is a crashed host.
**The four `Scaffolding.CompiledModel` tests are the standing example of why the count is the wrong
instrument**: C90, C91 and C92 each closed a real defect and each measured **26 → 26**, because
every fix moved the same four tests one stage further in, and only C93 turned them green.
**Read the reasons diff, not the count.**

**There are no unexplained wrong answers, and after C85 there are almost no wrong answers at all.**
Group a run's `[FAIL]` lines by their first message line: `Assert.Equal() Failure: Values differ` is
**2**, and both are C64's `Correlated_collection_with_distinct_3_levels`, whose assertion no correct
answer can satisfy — re-derived in C96, which also confirmed EF's InMemory suite refuses the query
outright. **Re-derive it; do not restate it.** It has been wrong in both directions twice —
C65 found a green test counted as a wrong answer, and C85 found two that were EF's own documented
SQLite limitation (#33522) counted into B12 because they shared a message line. **Grep
`EFCore.Sqlite.FunctionalTests` for the name before calling a Tier B `Values differ` ours — and
apply that to old failures, not only to newly-red ones. Age is not evidence.**

**The undiagnosed exceptions are down to those inside `JsonQuery`** — C42 closed one, C61 and C62
closed the last two outside it, and all three used the same two probes in the same order: what the
metadata and the client say, then **read the row the store actually holds**. That second probe is
what turned `Array_of_TimeOnly` from "ours" into EF issue #30730 in one run. The rest are a
deliberate allowlist refusal (`Regex_IsMatch`, A46) or a known singleton. **`JsonTypes` is clear**
— the nine that stood here were A64's locale, and C50 removed them by pinning the culture.

**"The A28 family" was hiding a third instance of C40's mechanism, and the tell was in the failing
list rather than in any test (C56).** Twelve `ComplexNavigations` failures were filed under
`AssertInvalidMaterializationType` and called a decision — the assert helper is `protected static`,
so the only route seemed to be duplicating EF's query bodies. But `NorthwindMiscellaneous` asserts
*the same refusal six times and passes every one*. The difference is only where the boundary falls:
EF raises it in `QueryableMethodNormalizingExpressionVisitor`, downstream of ADR-006's capture
point, so a **wholly shippable query gets the refusal from the server and always has**, and the
twelve are the ones the split leaves on the client. **Before calling a family of failures a design
question, check whether a sibling of it is green.**

**`Skipped` is 217, and was 217 in `c10b` too.** The number recorded against `c10b` was `208` —
carried over from `b21b`, where it was right — and `Passed` was then derived from it rather than
read. Phase C's adoptions brought nine of EF's own skips with them. `Failed` and `Total` were
correct throughout, so nothing was judged wrongly, but **all four figures come out of the run's
own summary block; none of them is arithmetic.**

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
