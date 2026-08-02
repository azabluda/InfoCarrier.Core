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

**Measuring a change: `eng/measure.sh <label> [baseline]`** (or the `/experiment` skill, which
wraps the whole loop). It prints the count *and* the exact list of tests fixed and broken,
because the count alone cannot tell "fixed 4, broke 4" from "changed nothing".

Never state a verdict from partial output. Two specific errors have each cost a wrong revert
here, and both are cheap to avoid:

- **A count that did not move does not mean the target does not exist.** A matcher that never
  fired and a rewrite that did not help look identical from outside. Establish that the code
  *ran* — a probe writing to a file, since xUnit swallows stdout — before concluding anything
  about the problem.
- **A newly-red SQLite test is not automatically a regression.** Grep
  `subrepos/efcore/test/EFCore.Sqlite.FunctionalTests` for the name first: if EF overrides it
  with `ApplyNotSupported`, the query now reaches SQL and this is convergence with the reference
  provider. Adopt EF's override. The reverse also happens — an override of ours that EF does
  *not* have is a workaround to delete once the limitation goes.

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
**`Passed: 6786, Failed: 229, Skipped: 13, Total: 7028`** (2026-08-02) across the Northwind
query bases and `GraphUpdatesTestBase` on Tier A.

Not yet implemented, in rough priority order:
- **The `GraphUpdates` residual** — 200 of 1787, concentrated in seven `Save_*` methods
  (alternate keys, one-to-one changed by reference, owned collections). Classified in
  `docs/implementation-plan.md` under S3c.
- **Concurrency tokens** — `SaveChangesRequest.SerializedOriginalValues` is on the wire and
  never written or read, so the server cannot make an optimistic-concurrency check. Plan S3c.
- **The remaining change-tracking spec bases** — `ManyToManyTrackingTestBase`,
  `PropertyValuesTestBase`, `OptimisticConcurrencyTestBase`, `FindTestBase`, `LoadTestBase`
  and the rest of the 138 the compliance test reports unadopted.
- **Transactions** (roadmap M4) — `InfoCarrierTransactionManager` *ignores* them and raises
  `InfoCarrierEventId.TransactionIgnoredWarning` (warning-as-error by default), as EF's
  InMemory provider does. `InProcessInfoCarrierServer`'s three transaction methods throw.
  Needs the wire-protocol W3 transaction token.
- **CI is broken** — `.github/workflows/build.yml` restores `InfoCarrier.Core.sln` (repo has
  `.slnx`), and its `~InMemory` / `~SqlServer` filters match no current test class.
- **The SQLite backend store is not parallel-safe** — a single full run has produced 698
  phantom failures in the two SQLite Northwind classes that pass on rerun and in isolation.
  Confirm any SQLite regression with a second run before believing it.
