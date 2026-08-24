# Build warnings — what is fatal, what is not, and why

The build reports **0 errors in both configurations**, and the warning count is not the same in
each. Measured on a full rebuild:

| Command | Result |
|---|---|
| `dotnet build InfoCarrier.Core.slnx` (Debug) | `0 Warning(s), 0 Error(s)` |
| `CI=true dotnet build InfoCarrier.Core.slnx --configuration Release` | `5 Warning(s), 0 Error(s)` |

**The five are green, not tolerated debt.** They are `IL2110` and `IL2111` from the framework's
own Razor output in `samples/Northwind.Client`, and that project downgrades exactly those two
codes from error to warning so that `eng/trim-ratchet.sh` can keep counting them. The row in the
table below is the full reasoning. Every other warning is still an error under `CI=true`.

**Do not restate this as "the build is clean".** That sentence stood here for five milestones,
measured in Debug, in a document whose own next paragraph says Debug cannot show the diagnostics
that matter. An incremental build hides them too, because nothing recompiles.

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

> **`--configuration Release` is part of the command, not decoration.** Omit it and you build
> Debug, where `samples/Northwind.Client` does not run the trim analyzer — so an entire class of
> diagnostic that fails the server cannot appear. That omission is exactly how the CI build stayed
> red from M8-32 to N12 while a local `CI=true` build answered `0 Warning(s), 0 Error(s)` every
> single time.

## What is suppressed, where, and why

There is **no repo-wide `NoWarn`**, and exactly **one** `WarningsNotAsErrors`, in one project
file. Every suppression is scoped to the smallest place that makes sense.

| Code(s) | Where | Why |
|---|---|---|
| `EF1001` | file-scoped `#pragma warning disable EF1001` in the **19 files** that use EF Core internals | This provider is built on `IStateManager`, `EntityQueryable<>` and `InternalEntityEntry` by design. **This is what EF Core's own providers do** — `subrepos/efcore` has 51 such files across eight projects (21 in `EFCore.Relational`) and **no `NoWarn` for EF1001 anywhere**. Per file, so a **new** file reaching for an internal API still warns. |
| `CS1591`, `CS1573`, `CS1574`, `CS1570` | `test/.editorconfig` and `samples/.editorconfig` | Nothing under `test/` or `samples/` is a public API surface anyone reads. Without this, the functional test project alone emits **1254** of them. In `src/` these stay **on**. |
| `IL2xxx` | `-p:TreatWarningsAsErrors=false` on `eng/trim-ratchet.sh`'s own publish | The 88 trim warnings are unfixable by design and are gated by **direction**, not by zero. See below. |
| `IL2110`, `IL2111` | `samples/Northwind.Client.csproj`, Release only | **Downgraded from error to warning, not silenced.** They come from the *framework's own* Razor output (`Router.NotFoundPage`, `LayoutView.Layout` in `App_razor.g.cs`) — not this repository's code, and not fixable here. `eng/trim-ratchet.sh` still counts them; `NoWarn` would make it count zero and pass for ever. Only these two codes: any other trim diagnostic still fails CI, which forces a look before a new one is tolerated. |

## Two traps, both of which cost a run

**1. `IDE0005` needs `GenerateDocumentationFile`, and was silently inert in six of eight
projects.** The samples and both test projects had documentation generation off, so the
unnecessary-using rule covered `src/` only while appearing to be repository-wide. The tell is a
warning almost nobody reads — `EnableGenerateDocumentationFile`, which names the property outright.
Those six projects now generate the file and switch the doc-comment rules off in a folder-scoped
`.editorconfig` instead.

**2. `ILLinkTreatWarningsAsErrors` does not do what its name suggests.** It feeds the ILLink
*task*. Most `IL2xxx` findings come from the trim **Roslyn analyzer** during compilation — they
carry a source line — so plain `TreatWarningsAsErrors` turns *those* into errors and the property
never sees them. Under `CI=true` the trimmed publish failed outright, which meant **the gate that
exists to measure trim warnings was the one thing that could not tolerate them**.

`NoWarn` would have been worse than useless: `eng/trim-ratchet.sh` **counts** these warnings, so
silencing them reports `OURS: 0` and passes for ever — the exact failure its clean-publish rule
already exists to prevent. The two axes are kept separate instead: warnings-as-errors gates the
ordinary build, and the *direction* of the trim count gates the publish.

## Verified, in both directions

A gate that has only ever been seen to pass is not known to work. This one was driven both ways
with a deliberately unused `using` in `src/InfoCarrier.Core/Expressions/TypeNode.cs`:

| | Result |
|---|---|
| `dotnet build` (no `CI`) | exit **0**, reported as `warning IDE0005` |
| `CI=true dotnet build` | exit **1**, `error IDE0005: Using directive is unnecessary.` |
| `CI=true bash eng/trim-ratchet.sh` | exit **0**, `OURS: 88 <= 88` |

## Related gates, which this does not replace

| Gate | Measures | Unaffected by this document |
|---|---|---|
| `eng/ratchet.sh` | spec-test failure **count**, against `test/known-failures.txt` | yes |
| `eng/trim-ratchet.sh` | ILLink `IL2xxx` **direction**, against `eng/trim-baseline.txt` | yes — the C# compiler never emits `IL2xxx`, and those 88 are unfixable by design |
