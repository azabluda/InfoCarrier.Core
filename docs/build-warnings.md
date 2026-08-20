# Build warnings — what is fatal, what is not, and why

The build is **clean**: `dotnet build InfoCarrier.Core.slnx` reports `0 Warning(s), 0 Error(s)`.
This document exists so that the next person does not have to re-derive how it got that way, or
guess which suppressions are deliberate.

Landed in M8-27 … M8-32. The starting point was **18 distinct warning texts**, and the argument
for doing any of it is that **three of them were high-severity security advisories nobody had
ever read** — 244 `EF1001` occurrences were burying them.

## The rule

**Warnings are errors in CI, and only in CI.**

```xml
<TreatWarningsAsErrors Condition="'$(CI)' == 'true'">true</TreatWarningsAsErrors>
```

`CI` is set to `true` by GitHub Actions (and by Azure Pipelines, GitLab and Travis), and MSBuild
reads environment variables as properties, so nothing is passed on the command line. Locally a
warning stays a warning — half-finished work with an unused variable in it is a normal state to be
in, and a gate that punishes experimentation is a gate people switch off.

**To reproduce a CI failure locally:**

```bash
CI=true dotnet build InfoCarrier.Core.slnx --configuration Release
```

> **`--configuration Release` is part of the command, not decoration**, because Release is what the
> server builds. Omitting it is exactly how the CI build stayed red from M8-32 to N12 while a local
> `CI=true` build answered `0 Warning(s), 0 Error(s)` every single time: `samples/Northwind.Client`
> ran the trim analyzer in Release only, so Debug could not produce the diagnostic that was failing.
> N30 removed that analyzer from the ordinary build, so that particular trap is closed. The rule
> outlived its reason and stays.

## What is suppressed, where, and why

There is **no repo-wide `NoWarn`** and, since N30, **no `WarningsNotAsErrors` anywhere**. Every
suppression is scoped to the smallest place that makes sense.

| Code(s) | Where | Why |
|---|---|---|
| `EF1001` | file-scoped `#pragma warning disable EF1001` in the **19 files** that use EF Core internals | This provider is built on `IStateManager`, `EntityQueryable<>` and `InternalEntityEntry` by design. **This is what EF Core's own providers do** — `subrepos/efcore` has 51 such files across eight projects (21 in `EFCore.Relational`) and **no `NoWarn` for EF1001 anywhere**. Per file, so a **new** file reaching for an internal API still warns. |
| `CS1591`, `CS1573`, `CS1574`, `CS1570` | `test/.editorconfig` and `samples/.editorconfig` | Nothing under `test/` or `samples/` is a public API surface anyone reads. Without this, the functional test project alone emits **1254** of them. In `src/` these stay **on**. |


## Trim warnings are not suppressed. They are switched on.

`IL2xxx` is the one family that works the other way round, and it is worth reading as a separate
mechanism rather than as a suppression.

The Blazor SDK turns trim warnings off by default, so nothing in an ordinary build or publish
reports them. `eng/trim-ratchet.sh` publishes with **`-p:TrimReport=true`**, which is the only
place in the repository that sets it, and which turns on three properties declared together in one
`PropertyGroup` in `samples/Northwind.Client.csproj`: `SuppressTrimAnalysisWarnings=false`,
`TrimmerSingleWarn=false` and `TreatWarningsAsErrors=false`. The publish then reports every
`IL2xxx` finding individually and the script gates two owners by direction against
`eng/trim-baseline.txt`.

**This used to be on for every Release build, and that cost two suppressions to survive `CI=true`:**
a `WarningsNotAsErrors=IL2110;IL2111` in the sample's project file, and a
`-p:TreatWarningsAsErrors=false` on the ratchet's own command line. Both are gone. So is
`ILLinkTreatWarningsAsErrors` in `Directory.Build.props`, which was redundant once
`TreatWarningsAsErrors` went false inside the switch: it defaults to `$(TreatWarningsAsErrors)`, and
outside the switch ILLink emits nothing to promote.

**Driven both ways, because a switch only seen in one position is not known to work.**
`CI=true dotnet build InfoCarrier.Core.slnx -c Release --no-incremental` reports `0 Warning(s)`; the
same command with `-p:TrimReport=true` reports `5 Warning(s)`, all `IL2110`/`IL2111`, and still
exits 0 because the switch turns warnings-as-errors off along with them. `bash eng/trim-ratchet.sh`
reports `ours OK (88 <= 88)` and `northwind OK (8 <= 8)`; run against a baseline saying
`northwind=7` it exits **1**.

## Two traps, both of which cost a run

**1. `IDE0005` needs `GenerateDocumentationFile`, and was silently inert in six of eight
projects.** The samples and both test projects had documentation generation off, so the
unnecessary-using rule covered `src/` only while appearing to be repository-wide. The tell is a
warning almost nobody reads — `EnableGenerateDocumentationFile`, which names the property outright.
Those six projects now generate the file and switch the doc-comment rules off in a folder-scoped
`.editorconfig` instead.

**2. `ILLinkTreatWarningsAsErrors` does not do what its name suggests, and the conclusion drawn
from that was wrong for two milestones.** The property feeds the ILLink *task*. Some `IL2xxx`
findings instead come from the trim **Roslyn analyzer** at compile time, so plain
`TreatWarningsAsErrors` turns *those* into errors and the property never sees them. Under `CI=true`
the trimmed publish failed outright, which meant **the gate that exists to measure trim warnings was
the one thing that could not tolerate them**.

That much is true. What this document used to say next is not: *"Most `IL2xxx` findings come from
the trim Roslyn analyzer during compilation."* Measured in N30 against `artifacts/trim/publish.log`:

| Emitter | Warning lines |
|---|---|
| ILLink task (`Trim analysis warning IL…`) | 1113 |
| Roslyn analyzer (plain `warning IL…`) | **6** |

Five of the six are the framework's own Razor output, one is EF Core's `ExecuteUpdateAsync`, and
**none of InfoCarrier.Core's 88 came from the analyzer**. A `CI=true dotnet build -c Release` of the
sample reported those five and nothing else, every time, for as long as the analyzer was on.

So the analyzer was carrying no signal about this product and all of the cost, and N30 took it out
of the ordinary build. The one signal it did carry — a new trim diagnostic in the *sample's* own
code — moved to `eng/trim-baseline.txt` as a second gated owner, `northwind`, which is measured
across the whole publish rather than across one compile.

`NoWarn` was never an option and still is not: `eng/trim-ratchet.sh` **counts** these warnings, so
silencing them reports `OURS: 0` and passes for ever — the exact failure its clean-publish rule
already exists to prevent.

## Verified, in both directions

A gate that has only ever been seen to pass is not known to work. This one was driven both ways
with a deliberately unused `using` in `src/InfoCarrier.Core/Expressions/TypeNode.cs`:

| | Result |
|---|---|
| `dotnet build` (no `CI`) | exit **0**, reported as `warning IDE0005` |
| `CI=true dotnet build` | exit **1**, `error IDE0005: Using directive is unnecessary.` |
| `CI=true bash eng/trim-ratchet.sh` | exit **0**, `ours OK (88 <= 88)` and `northwind OK (8 <= 8)` |

## Related gates, which this does not replace

| Gate | Measures | Unaffected by this document |
|---|---|---|
| `eng/ratchet.sh` | spec-test failure **count**, against `test/known-failures.txt` | yes |
| `eng/trim-ratchet.sh` | ILLink `IL2xxx` **direction** for two owners, `ours` and `northwind`, against `eng/trim-baseline.txt` | yes — those 88 are unfixable by design, and since N30 no ordinary build emits `IL2xxx` at all |
