# Engineering commands

[Issue #264](https://github.com/azabluda/InfoCarrier.Core/issues/264) moves repository-owned
engineering automation to .NET file-based C# applications. Use the existing build jobs and root
build properties. A separate scripting runtime, per-command project, or platform matrix is not needed.

## Commands

Run from the repository root unless noted otherwise. Put application arguments after `--`.

| Entry file | Arguments | Behavior |
|---|---|---|
| `eng/WireContract.cs` | `[--base-ref revision] [--record] [--release-ref revision ...]` | Inspect the candidate and compare protected Git descriptions. |
| `eng/DocLinks.cs` | `[file ...]` | Validate local Markdown pages and heading anchors; exit 1 for broken links. |
| `eng/DocWords.cs` | `[--all] [--budget] [file ...]` | Count prose with existing budgets; `--budget` fails if exceeded. |
| `eng/TrxFailures.cs` | `<results.trx> [more.trx ...]` | Stream XML, union failed names, and sort by Unicode code point. |
| `eng/SpecParity.cs` | `<reasons.tsv ...> -- <results.trx ...>` | Write badge values to stdout and the parity table to stderr. |
| `eng/SuiteSummary.cs` | `<results.trx> [more.trx ...]` | Write counters, failed names, parity, and the workflow summary beside the first TRX. |
| `eng/Measure.cs` | `<label> [baseline-label]` | Run both specification projects and compare complete failure-name and reason snapshots. |
| `eng/TrimRatchet.cs` | `[baseline-file]` | Clean-publish the Blazor client and enforce the existing trim-warning limit. |
| `eng/DocsServe.cs` | `[--build] [MkDocs serve arguments ...]` | Create the documentation environment and preview it, or build strictly. |
| `eng/MakeIcons.cs` | `[--output-root directory]` | Generate the five PNGs and three-frame ICO from source artwork. |

```text
dotnet run --file eng/Measure.cs -- change-label baseline-label
dotnet run --file eng/DocWords.cs -- --all --budget
dotnet run --file eng/WireContract.cs -- --base-ref origin/main
dotnet run --file eng/DocsServe.cs -- --build
dotnet test test/InfoCarrier.EngineeringTests/InfoCarrier.EngineeringTests.csproj --configuration Release
```

Measurement, trimming, documentation serving, and wire checking find the repository from source
location. Documentation checks and icon generation retain current-directory input behavior.
TRX and reason paths are caller-supplied paths. Measurement labels are filenames within
`artifacts/measure`, not arbitrary paths. Test failures remain measurement data; missing summaries
and failed builds are errors. The suite summary reports results; `dotnet test` remains the gate.

## Source layout and future changes

Write all new engineering automation in C#, with PascalCase filenames and top-level entry files.
Keep script-specific implementations in `eng/commands/`. Use `eng/shared/` only for code called by
multiple commands. `ScriptHost` owns safe argument passing, concurrent pipe draining, log streaming,
and cancellation of child process trees. Commands retain their argument parsing, outputs, and error decisions.

The entry files use `#:include` to compile those sources. `global.json` therefore requires SDK
10.0.300 with `latestMinor` roll-forward. The engineering test project compiles the same source
files without the top-level entry statements. Keep comments on arguments, outputs, failures, and
non-obvious safeguards current. Do not introduce new Python or Bash orchestration.

The root `Directory.Build.props` supplies nullable checking, implicit usings, analyzers, MinVer,
and `IsPackable=false`. The icon command declares its codec packages; no product project is
referenced by command builds. The wire command explicitly builds the existing inspector,
and measurement explicitly builds the solution. The SDK restores and caches file-based builds;
use `--no-cache` when investigating a stale build. Shared-source changes must invalidate the corresponding cache.

## Preserved behavior and verification

The wire runner retains protected baseline selection, historical local defaults, merge identity
checks, recording restrictions, and strict release comparisons. The inspector, allocation ledger,
protocol, and committed descriptions are unchanged. See the [wire contract](wire-protocol.md).

Prose budgets, test project lists, TRX counters, badge calculations, and trimming limits remain
unchanged. Counters are read independently; passed cases are never derived from total and failed.
Parity excludes skipped cases and wire-free controls and rounds down. XML parsing retains nested
failures and theory names containing `>`. SDK diagnostics stay outside machine-readable summary
files. Cancellation and cleanup failures remain visible.

SkiaSharp 4.153.1 supplies PNG codecs under the
[MIT license](https://www.nuget.org/packages/SkiaSharp/4.153.1). Its
[Linux native-assets package](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/4.153.1)
avoids a separate graphics-system installation. The renderer retains alpha-floor trimming, square
tolerance, margins, premultiplied Lanczos resizing, and mask composition. During migration, all
five PNGs and all three ICO frames matched the former renderer pixel for pixel. Encoded bytes
can differ because the PNG codec differs. Shipped artwork is unchanged; `--output-root` permits verification in artifacts.

Active contributor instructions, workflow invocations, and experiment skills use the new commands.
The user-facing review includes README, package readmes, and website instructions. Source-build
instructions name SDK 10.0.300; MkDocs comments name the new wrapper. Dated execution records retain
commands that actually ran.

## External dependencies and remaining automation

Git remains necessary for Git operations. MkDocs still requires Python and builds the existing
website. `DocsServe.cs` locates that interpreter only for MkDocs, then uses its virtual environment.
The documentation workflow retains interpreter setup and pinned website requirements. Replacing
MkDocs is outside this migration.

Automation hooks outside `eng/` remain separate: `.claude/hooks/file-hygiene.py`,
`.claude/hooks/cs-search-reminder.py`, their `.codex/hooks/` counterparts, and
`.codex/tests/test_hooks.py`. Historical plans also mention earlier interpreter commands. The final
repository audit must report these uses rather than describe them as removed.
