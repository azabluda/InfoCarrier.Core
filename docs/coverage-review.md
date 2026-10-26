# Reviewing coverage evidence

Coverage is evidence about selected scenarios. It does not establish that unexecuted code is
obsolete or that a responsibility is wrong. Inspect callers, framework interfaces, registrations,
generated entry points, and ownership before proposing removal or design changes.

## Reports

The workflow collects Cobertura once. ReportGenerator supplies the downloadable `coverage-report`
artifact with full source, line, and branch detail. Octocov supplies patch coverage, affected-file
comparisons, a workflow summary, and one updated pull-request comment. Its comparison viewer uses
GitHub artifacts through `viewer: artifact`; no octocov.dev account or external report host is used.

Octocov stores main-branch report metadata in GitHub Actions artifacts. Pull requests compare
against the stored base-branch report. This may be an earlier commit than the current base; inspect
the displayed revisions. The viewer withholds base-line coverage when its revision does not match
the source diff. Missing baselines produce a current-only report, not a zero baseline. The first
successful main run seeds the comparison store; this trial's first pull request has no such baseline.
Artifacts follow GitHub retention, so an expired baseline can make comparisons unavailable again.

Changed lines exclude deletions; renames use the new path. Moved code can appear as additions.
Affected-file changes are review candidates, not proof of lost coverage on surviving lines.
Totals also change when covered code is removed. Check the source, compared revisions, and runtime
scenarios before calling a difference a regression. Missing or oversized GitHub file patches limit
patch coverage; inspect the workflow log when evidence is incomplete.

No coverage condition or threshold is configured. Reporting failure is advisory; existing build
and test failures still fail their own steps. Fork pull requests retain ReportGenerator's summary
and artifact but skip octocov publication because their token cannot write comments.

## Local review

Start with a concrete contract or failure path and inspect it through
[Roslyn CodeLens](../.claude/roslyn-codelens.md). Run focused representative scenarios before
broader coverage. Use a fresh results directory per scenario set, with `eng/coverage.runsettings`.
For each finding, record its location, observed behavior, missing scenarios, and a concrete action:
investigate, add a test, simplify, or remove with corroboration.

With octocov v0.83.3 installed, inspect an existing report without publishing anything:

```powershell
octocov ls-files --report path/to/coverage.cobertura.xml
octocov view --report path/to/coverage.cobertura.xml src/InfoCarrier.Core/InfoCarrierDatabase.cs
octocov dump --report path/to/coverage.cobertura.xml > head.json
octocov diff head.json base.json
```

Export each scenario set separately. The dump format is JSON; preserve UTF-8 when redirecting it
with Windows PowerShell. Local comparison uses exported octocov reports. Its `--patch` option also
requires GitHub pull-request context and API access. Local inspection and ordinary report comparison
do not require credentials.

## Evaluation, 2026-10-26

Work: [issue #257](https://github.com/azabluda/InfoCarrier.Core/issues/257).
The owner selected a configuration-only octocov trial after rejecting the custom reporting adapter.
The adapter and its test suite are removed. Tool behavior belongs to the community implementation.

| Capability | Octocov v0.83.3 | Decision |
|---|---|---|
| Patch coverage, baseline storage, updated comment | Built in | Replace Quality Monitor |
| Local line inspection and report comparison | Built in | Use before hosted review |
| Comparison viewer | GitHub artifact, including affected unchanged files | Trial without external hosting |
| Cobertura branch coverage | Parser records line hits only | Keep ReportGenerator for branch detail |
| Full standalone source report | Comparison viewer needs pull-request context and a baseline | Keep ReportGenerator's downloadable report |
| Obsolete code or design verdicts | Not established by coverage | Use combined source and runtime evidence |

Local checks use the fresh replacement-scenario report from this session and controlled Cobertura
inputs containing an uncovered addition, coverage lost on existing lines, deleted covered code,
and unobserved callback/supported-path placeholders. The earlier source/runtime investigation
established that framework-dispatched `CompileQuery` executes despite zero direct callers, and
that synchronous transaction creation was absent from one scenario set but present in another.
These remain reasons to investigate missing scenarios, never reasons for automatic deletion.

- [x] C1: Replace custom code with pinned octocov configuration and compare local reports.
- [ ] C2: Inspect hosted comment updates, artifact links, and missing-baseline output after continuous integration.
- [ ] C3: Verify the comparison viewer after a main-branch baseline exists. Keep the issue open until acceptance is reconciled.

Reference: [released octocov documentation](https://github.com/k1LoW/octocov/blob/v0.83.3/README.md).
