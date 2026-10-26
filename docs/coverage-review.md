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
the source diff. Missing baselines produce a current-only report, not a zero baseline. Main runs
update the comparison store automatically. This trial initially imported the verified main report
from commit `451a842198024732f1e8b00b71c3f52f4b85af43` through a temporary seed job, then removed that job.
There is no fixed workflow run number or seed step in the final configuration.
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

Hosted acceptance used the same stored main baseline in both controlled runs:

| Run | Project coverage | Covered lines | Probe / patch coverage |
|---|---:|---:|---:|
| [Uncovered addition](https://github.com/azabluda/InfoCarrier.Core/actions/runs/40325410026) | 92.7% | 8,906 of 9,600 | 0%, 0 of 66 lines |
| [Tests added](https://github.com/azabluda/InfoCarrier.Core/actions/runs/40327416579) | 93.4% | 8,972 of 9,600 | 100%, 66 of 66 lines |

The main baseline was 93.4%, with 8,906 of 9,534 lines covered. The tested report's difference
against main displayed `+0.0%` because the tool rounds to one decimal; its exact covered-line
increase was 66. Compare counts and patch coverage as well as the rounded percentage.

Both trial workflows omitted the seed job. Octocov found metadata artifact
`octocov-metadata-octocov-report@refs_heads_main`, which recorded the main commit and report artifact
`12650592102`. The seed ran on the feature branch, so branch-name fallback could not supply this
baseline. Both comments identified the correct baseline and tested merge revisions.
The [same comment](https://github.com/azabluda/InfoCarrier.Core/pull/258#issuecomment-6414804434)
was updated, not duplicated. Inspection of the HTML viewers confirmed 66 uncovered rows in the
first run and 66 covered rows, with no uncovered probe rows, in the second run. The comparison
artifacts remained on GitHub. Artifact links expire with their normal retention.

The temporary production probe, its tests, and the seed job were removed by resetting the trial
commits after both reports were captured. No temporary source or bootstrap setting remains in
the reviewed implementation. The owner authorized hosted checks for the remaining temporary probes;
the first local specification gate and the focused transport checks had already passed.

- [x] C1: Replace custom code with pinned octocov configuration and compare local reports.
- [x] C2: Inspect hosted comment updates, artifact links, and explicit missing-baseline output.
- [x] C3: Verify automatic baseline lookup, downward/upward deltas, and the hosted comparison viewer.

Keep the issue open for final review and separate owner merge approval.

Reference: [released octocov documentation](https://github.com/k1LoW/octocov/blob/v0.83.3/README.md).
