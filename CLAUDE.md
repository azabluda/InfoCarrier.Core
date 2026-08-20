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
| `docs/plans/v10/roadmap.md` | **Stable** milestone plan for the whole project |
| `docs/plans/v10/implementation-plan.md` | **Rolling** checkbox detail for the *current* milestone only |
| `docs/architecture.md` | Components, test strategy, open questions |
| `docs/research-findings.md` | EF Core 10 pipeline findings backing the ADRs |
| `docs/decisions.md` **ADR-013** | The test project may reference `EFCore.Relational.Specification.Tests`. **Before adopting a relational spec base, check whether it assumes the *client* is relational** — a non-virtual `UseTransaction` calling `GetDbTransaction()` makes a base unreachable here, and cost 142 tests to discover. |
| `docs/security-review.md` | **M5's review of the deserialization path** (C48). Read §2 before adding anything to `TypeAllowlist`: its safety is a conjunction across several clauses, and `Binder`/`MethodInfo`/`Activator` each break it alone. |
| `docs/build-warnings.md` | **Which warning codes are fatal, which are suppressed, where, and why.** The build is clean (`0 Warning(s)`) and warnings are errors **in CI only** — `CI=true dotnet build InfoCarrier.Core.slnx --configuration Release` reproduces it — the configuration matters. Read before adding any `NoWarn`. |

**Roadmap vs plan — do not mix them.** Milestone-level scope, ordering, and exit criteria go
in `roadmap.md`, which changes only when scope changes. Per-task checkboxes go in
`implementation-plan.md`, which is rewritten at each milestone boundary (previous ones land in
`docs/plans/v10/archive/`, never edited again). Putting task detail in the roadmap, or scope changes in
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
note it in `docs/plans/v10/implementation-plan.md`. Silently suppressing tests was v1's stated failure
mode.

**Update the plan checkbox in the same commit as the work.** `docs/plans/v10/implementation-plan.md`
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

**M9 is CLOSED (2026-08-17). M8 is NOT.** M9 met its four exit criteria: the document-mapping seam
(so `InfoCarrier.Core` no longer references `EFCore.Relational`), the test project organised by
backend store, four bases moved to the tier that translates, and the capability axis identified,
decided and recorded rather than built (`architecture.md` §6a **D5**, answer (c)). Task detail is
archived in `docs/plans/v10/archive/implementation-plan-m9-phase-j.md` and is never edited again;
`docs/plans/v10/implementation-plan.md` holds M8's Phases H and I only.

**M6 is CLOSED (2026-08-11).** Every spec base EF ships that this provider can host is adopted, and
`InfoCarrierComplianceTest.All_test_bases_must_be_implemented` is what enforces it. That test, not a
list in this file, is the current answer to "which bases are in".

Query, projection split and SaveChanges work end-to-end. Lazy loading works: Phase L began at 505 of
505 failing and stands at **825 of 825**.

**`Total tests: 22656, Passed: 22466, Failed: 13, Skipped: 177`** (2026-08-17, `j15`).
**All four figures come out of the run's own summary block, and none of them is arithmetic** — a
`c10b` entry once carried `Skipped` over from an earlier run and derived `Passed` from it. **A
falling `total` with no note explaining it is a crashed host**: `test/known-failures.txt` records
the one deliberate lowering, in C94, where two skipped theories turned 4 tests into 2.

**All 13 failures are classified and not one is of unknown standing.** They sit in eight classes,
five holding two and three holding one. The tables are in `docs/plans/v10/implementation-plan.md`
— A54, A59, A61–A65, B3a–B16 and C1–C96 — and Phase J's "The residual 13, examined properly"
re-derives the whole tail against M9's run. The run itself is in `artifacts/measure/`, currently
`j15`. `Query.Associations` is 336 of 336, and `MaterializationInterception`,
`OptimisticConcurrency` and `ComplexNavigations` are clear. Wrong answers are down to **2**, both
C64's `Correlated_collection_with_distinct_3_levels`, whose assertion no correct answer can
satisfy.

**The consumer-facing statement of what is missing is
[`website/docs/limitations.md`](website/docs/limitations.md)**, and that is the document to keep
true, because it is the only one here written for someone outside this repository. It names one
unsupported scenario, one query to treat with caution, two message-text differences, and two queries
this provider *answers* that other EF providers reject.

**What is not implemented.** Everything else that this section used to list has closed.

- A shipped gRPC binding, and streaming results as `IAsyncEnumerable`. Both may change
  `IInfoCarrierTransport`, which is why the version still says `preview`.
- Two `ComplexTypesTracking` parameterizations: a property-bag complex *collection* on an `Added`
  entity. J22 traced it to an upstream defect on a path only this provider takes, and the route
  around it has to reproduce constructor binding, so it is priced and not taken.
- The residual spec failures, every one of them classified.

**The long form is [`docs/plans/v10/findings.md`](docs/plans/v10/findings.md)**: how the HTTP
transport, the Blazor client, complex types, JSON-mapped owned collections, spatial, `GraphUpdates`,
the compiled model and the design-time services were made to work, what each cost, and the two
investigations that ran longest. The rules those findings produced are the part that transfers, and
they are these:

- **Read the reasons diff, not the count.** A count that did not move cannot tell "fixed four, broke
  four" from "changed nothing", and four `Scaffolding.CompiledModel` fixes each measured 26 to 26.
- **Establish that the code ran** before concluding anything from a count that did not move. A
  matcher that never fired and a rewrite that did not help look identical from outside.
- **Before pricing a gap, check whether a sibling of it already works.** Two bases were called
  permanently unreachable while the feature they needed had shipped five milestones earlier.
- **Before calling a family of failures a design question, check whether a sibling is green.**
- **A classification is not evidence, and age is not evidence.** Grep EF's own suites for the test
  name before calling a failure this provider's. Six standing classifications were found wrong that
  way in M9's closing session alone, one of which had read "SQLite-tier, a store limitation" for two
  milestones and was ours, one line (J19).
- **Ask what an assertion assumes about the topology** before treating it as a statement about the
  provider. This repository is two `DbContext` instances; `Assert.Same(context, …)` has no answer.
- **When a rule breaks a named family of tests, read the family.** It is usually stating the rule
  you actually wanted.
- **Two failures of the same shape are one defect until measured otherwise.**
- **An evidenced hypothesis can be right about the evidence and wrong about the mechanism.**

The Tier B store is **file-backed** (`<StoreName>.db` in the test output directory), as EF
Core's own `SqliteTestStore` is. Do not move it back to `Mode=Memory;Cache=Shared`: that makes
the database's lifetime a connection's, which makes test-class disposal order load-bearing and
has already produced a 698-test phantom failure. For the same reason **the store must not delete
its file on disposal, and must not release its `Created` entry either** — either one
reintroduces the coupling. The second half survived S3c-5 and produced a nine-test intermittent
failure once the suite passed ten thousand tests: a shared store's disposal re-armed the guard
and let a later class re-seed the file a live one was still using. `DisposeAsync` now releases
nothing. Stale files are swept once at startup instead.

**The runtime culture is pinned to invariant** by a `[ModuleInitializer]`, and that is a ratchet fix
rather than a test fix. On an `en-SE` machine nine spec tests fail on the decimal separator, none of
them this provider's, which made the suite total a property of the machine. Do not remove it.

**There is no known intermittent.** The last one closed in C38: `ServerSaveChangesExecutor` rethrows
an identity conflict with the whole request appended, which turned a one-run-in-four failure into
two dumps that arrived already diagnosed. Both accounts are in
[`docs/plans/v10/findings.md`](docs/plans/v10/findings.md).

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
