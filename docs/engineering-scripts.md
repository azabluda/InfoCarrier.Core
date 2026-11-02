# Engineering script migration

[Issue #264](https://github.com/azabluda/InfoCarrier.Core/issues/264) replaces repository-owned
Python and Bash orchestration with .NET 10 file-based C# applications. The first conversion is the
wire-contract runner. The other commands retain their existing implementation until their behavior
has been verified separately.

## Commands and prerequisites

Run from the repository root:

```text
dotnet run --file eng/wire-contract.cs
dotnet run --file eng/wire-contract.cs -- --base-ref origin/main
dotnet test test/InfoCarrier.EngineeringTests/InfoCarrier.EngineeringTests.csproj --configuration Release
```

The runner resolves its repository from its source path, so an absolute `--file` path also works
from another working directory. Arguments after `--` belong to the application. `--record` and
repeatable `--release-ref` retain their existing meaning; see the
[wire check](wire-protocol.md#structural-compatibility-check).

Git remains required. `global.json` declares SDK 10.0.201 with `latestMinor` roll-forward.
The engineering workflow checks that exact minimum and the current 10.0 SDK on Windows and Linux,
using checkout paths with spaces and invoking the real runner from the documentation directory.
Each matrix job uses an isolated SDK installation, because merely installing 10.0.201 beside a
newer SDK would allow roll-forward to select the newer one. Local development on 2026-11-02
resolved SDK 10.0.401. Both local SDKs passed the focused tests and the actual wire comparison.
The implementation does not use `#:include`, which requires SDK 10.0.300.

The runner needs no Python or Bash. Website builds and preview still require Python and MkDocs;
replacing the website engine is outside this migration.

## Build behavior

`eng/Directory.Build.props` deliberately does not import the product build properties. Engineering
applications have nullable checking, implicit usings, documentation generation, and the repository
code-style analyzers, but no MinVer dependency, product package identity, or package validation.
Native AOT publishing is disabled. No project file is committed for the runner, and no product
project is referenced by its file-based build. Only the runner's explicit child command builds the
existing `tools/InfoCarrier.WireContract` project.

The SDK restores and caches file-based builds automatically. The workflow exercises initial,
`--no-restore`, and `--no-build` launches. After changing inherited build settings, use
`dotnet run --file eng/wire-contract.cs --no-cache -- --help` to force a build. The test project
links the same runner source into one engineering harness; it is separate from specification-suite
counters, badges, and transport coverage. Child arguments use `ProcessStartInfo.ArgumentList`.
Cancellation kills the active child process tree before temporary-directory cleanup.

Tests cover protected Git selection, historical local defaults, stale merge candidates, pushes,
merge groups, legacy descriptions, provenance-only changes, recording order, release comparisons,
paths with spaces, missing inputs, child failures, cancellation, and cleanup rejection/failure.
The actual inspector still checks allocation history and wire compatibility; its implementation
and committed descriptions are unchanged.

## Documentation and remaining inventory

Active wire commands are in `CLAUDE.md`, [wire-protocol.md](wire-protocol.md),
and [versioning.md](versioning.md). They move with this runner. Repository experiment skills refer
to `measure.sh`, so their command instructions change when that script moves. Historical plans
retain their original command spellings.

The user-facing documentation review covered README, `src/*/PACKAGE.md`, and `website/`.
None invokes this runner or its former Python test. The website's source-build instructions still
use `dotnet pack`; this bounded conversion does not change those instructions or prerequisites.
Recheck user-facing instructions as the remaining scripts move, especially documentation preview
and icon generation.

`CONTRIBUTING.md` still describes the existing suite and website commands. It gains the engineering
test command and this migration reference with the runner conversion.

Remaining scripts in `eng/` are `doc-links.py`, `doc-words.py`, `trx-failures.py`, `spec-parity.py`,
`make-icons.py`, `measure.sh`, `trim-ratchet.sh`, `suite-summary.sh`, and `docs-serve.sh`.
The documentation gates still use Python. Suite measurement, trimming, summaries, and website
preview still use Bash.

Python automation outside `eng/` remains separate: `.claude/hooks/file-hygiene.py`,
`.claude/hooks/cs-search-reminder.py`, their `.codex/hooks/` counterparts, and
`.codex/tests/test_hooks.py`. These are not converted by the first runner change. The repository
does not become Python-free or Bash-free with this step.
