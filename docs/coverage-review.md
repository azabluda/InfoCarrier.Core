# Reviewing coverage evidence

Coverage is an observation of selected scenarios. Use it to choose an investigation or a test,
not to decide automatically that a method is obsolete or a responsibility is wrong.

## Start locally

1. Select a concrete changed contract, failure path, or ownership boundary. Use Roslyn CodeLens
   to inspect references, callers, interfaces, overrides, dependency-injection registration,
   generated entry points, and relevant ownership paths. A zero caller count is not a runtime result.
2. Run the smallest representative scenarios. For example, from the repository root:

   ```powershell
   dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj --configuration Release --filter FullyQualifiedName~ServiceReplacementTest --collect:"XPlat Code Coverage" --settings eng/coverage.runsettings --results-directory artifacts/coverage/local-review
   dotnet reportgenerator '-reports:artifacts/coverage/local-review/**/coverage.cobertura.xml' '-targetdir:artifacts/coverage/local-review-report' '-reporttypes:Html'
   ```

   Use a new results directory for each scenario set. Do not merge stale reports or treat a
   focused run's aggregate percentage as a whole-suite comparison. Open the generated `index.html`.
3. Record the code location, observed input and result, missing scenarios, and review action.
   Add a test when supported behavior lacks evidence. Investigate framework dispatch and public
   callers before removal. Inspect coupling and ownership before proposing a design change.
   Use targeted instrumentation when coverage cannot distinguish the relevant lifecycle or inputs.

For C# symbol questions, use the tools described in
[the repository navigation instructions](../.claude/roslyn-codelens.md). Stop if that connection closes.
The detailed [multiple-registration audit](plans/v11/multiple-registrations.md#post-implementation-obsolete-code-audit-2026-10-25)
shows why generated materializers, framework services, and supported public factories were retained.

## Pull-request report

The coverage workflow preserves ReportGenerator HTML and the workflow summary. Its
`coverage-report` artifact contains `index.html` and the merged Cobertura input.
Quality Monitor provides changed-line/branch metrics and source annotations in its GitHub Checks
Result. The pull request receives one updated comment, with a report link and revision context.
There is no percentage threshold or coverage merge gate. Reporting failures are advisory;
build and test failures still fail their existing workflow steps.

The tested revision is GitHub's synthetic merge, with the pull-request base and head as its two
parents. The comment identifies that merge, the head, and the merge base used for the source diff.
Project-total differences use only a
successful `coverage.yml` push on `main` at exactly that base commit. The reference run and base
commit are linked. No older successful run is substituted. An expired, missing, unreadable, or
malformed artifact disables baseline differences and is identified as unavailable.

The adapter downloads only `merged.cobertura.xml` from that artifact. It places the file under
`reference-reports/artifacts/coverage/report/`, matching the current report's configured path.
`actions: read` permits this download; `checks: write` and `pull-requests: write` permit publication.
Fork pull requests retain the summary and artifact but skip comments because their token cannot write.
No privileged workflow executes fork code for reporting.

Changed-code coverage is separate from project-total differences. It uses added or replaced lines
from the pull-request file patches, with renamed files keyed by their new path. It excludes deleted
lines. Moves can look like additions. Before enabling changed-code metrics, the adapter checks the
complete file list and the added-line counts in production C# patches. A missing/truncated patch
disables those metrics and annotations. No production additions means those metrics are omitted.
Changes containing only non-executable source can still produce an empty instrumented diff,
which this tool displays as 100%; this is not proof that changed behavior was tested.

Annotations are capped at 30. Use the detailed report for the remaining evidence. Paths are resolved
by the pinned action's repository/suffix matcher; generated files excluded by the collector cannot
produce coverage findings. The comment links to the actual tested source through the Checks Result,
and links the procedure at the pull-request head revision.

Project-total changes include added and removed code. They cannot establish that a surviving line
lost or gained coverage. This implementation does not claim line-mapped existing-code comparisons.
In particular, a removed covered method is not reported as lost test coverage. Branch denominators
can change with collector output, as the [earlier coverage audit](plans/v10/coverage-audit.md#scope-and-method)
demonstrated. Investigate the source and scenarios rather than interpreting a percentage movement
as a regression. Coverage never labels a method dead or produces an automatic design verdict.

## Tool choice and evaluation, 2026-10-26

Work: [issue #257](https://github.com/azabluda/InfoCarrier.Core/issues/257).
Baseline source: `451a842198024732f1e8b00b71c3f52f4b85af43`.

The strongest argument against more reporting infrastructure is maintenance without better
decisions: another percentage generator would duplicate ReportGenerator. A custom source mapper
would also need to own rename, deletion, instrumentation, and partial-evidence semantics. Retain
the existing pinned Quality Monitor for its changed-line metrics and missed-line/partial-branch
annotations. The adapter only selects verified artifacts, configures existing scopes, and adds
revision/uncertainty context. It does not calculate coverage or infer obsolete code.
No production code or ordinary application flow changes.

Verified pinned implementation:

- [Action inputs and download](https://github.com/uhafner/quality-monitor/blob/0100da6973a1498aad89af676852a8c788d397f8/action.yml):
  automatic deltas download `quality-reports` from `quality-monitor-build.yml`, not this repository's
  workflow. Keep that download disabled and supply the exact reference directory ourselves.
- [Runner and comment update](https://github.com/uhafner/quality-monitor/blob/0100da6973a1498aad89af676852a8c788d397f8/src/main/java/edu/hm/hafner/grading/github/QualityMonitor.java):
  a supplied reference directory is used independently of `enable-delta`; `UPDATE` edits the
  canonical comment. Any nonempty `skip-annotations` string, including `false`, suppresses annotations.
- [Diff provider](https://github.com/uhafner/quality-monitor/blob/0100da6973a1498aad89af676852a8c788d397f8/src/main/java/edu/hm/hafner/grading/github/GitHubDiffProvider.java):
  uses added/replaced new-file lines and skips removed files. The action fetches live pull-request
  patches. Revision checks and cancellation reduce stale publication; they cannot make its separate
  API calls atomic. A new synchronization run replaces earlier output.
- [Scope mapping](https://github.com/uhafner/autograding-model/blob/v17.1.0/src/main/java/edu/hm/hafner/grading/Scope.java)
  and [delta builder](https://github.com/uhafner/autograding-model/blob/v17.1.0/src/main/java/edu/hm/hafner/grading/ScoreBuilder.java):
  `new` means modified lines; `changed` means modified files. Deltas apply only to whole-project
  scores, not surviving source lines.
- [Coverage score](https://github.com/uhafner/autograding-model/blob/v17.1.0/src/main/java/edu/hm/hafner/grading/CoverageScore.java)
  and [annotation builder](https://github.com/uhafner/autograding-model/blob/v17.1.0/src/main/java/edu/hm/hafner/grading/CommentBuilder.java):
  empty instrumented results display 100%. Annotations identify missed lines and partially covered
  branches; they do not determine caller reachability or classify dead code.

Concrete evaluation findings:

| Location | Observed evidence | Missing evidence | Review action |
|---|---|---|---|
| `ServiceReplacementTest.Pipeline.cs`, `Selected_server_limits_also_guard_decoded_query_payloads_for_custom_adapters` | The prior reconciliation records a failing custom-adapter limit probe, corrected in buffered and streamed dispatch. The focused replacement scenarios pass now. | Percentages cannot prove that the selected limit, rather than a global limit, was used. | Keep the input-boundary assertion and unlimited control; use a focused test, not a coverage target. |
| Removed `InfoCarrierOptionsExtension.WithAllowedTypes` and other adapters in the obsolete-code audit | The recorded audit combined reference analysis, replacement paths, supported callers, and the version boundary. | Present coverage cannot examine deleted methods. A denominator decrease does not restore this evidence. | Use the historical audit for corroboration; exclude deletions from changed-code findings. Do not reopen that cleanup. |
| `ServiceReplacementTest.Pipeline.cs`, `Failed_context_resolution_disposes_the_operation_scope` | The prior deterministic failure probe reproduced an undisposed scope. The present focused run exercises synchronous, asynchronous, and streamed failures. | Executing a disposal line does not prove exactly-once ownership. | Retain the disposal-count assertion and inspect the lease boundary. A coverage annotation alone cannot detect the lifetime defect. |
| `src/InfoCarrier.Core/InfoCarrierDatabase.cs`, `CompileQuery`, line 79 | Roslyn CodeLens reports zero direct callers. The focused replacement run covers all its instrumented lines and 60% of its branches. Its declaration implements the framework database contract. | The remaining branches and other application input shapes are not established by this run. | Retain this framework entry point; investigate missing branch scenarios. Never infer dead code from its caller count. |
| `src/InfoCarrier.Core/InfoCarrierTransactionManager.cs`, `BeginTransaction`, line 50 | Zero line coverage in the replacement scenarios becomes full method line coverage in the transaction-remoting scenarios. | These two focused sets are not complete application coverage and must not be treated as a historical regression. | Expand representative scenarios before deletion; keep the supported synchronous transaction path. |
| `src/InfoCarrier.Core/InfoCarrierTransactionManager.cs`, `ResetState`, line 121 | Zero direct callers and zero coverage in both focused sets. It implements `IResettableService`. | Pool reset with an active transaction remains unobserved in these sets. | Investigate that framework scenario; no deletion claim is justified. |

Local evidence on 2026-10-26:

- Replacement scenarios: Passed: 30, Failed: 0, Total: 30, collected with the committed runsettings.
- Transaction-remoting scenarios: Passed: 9, Failed: 0, Total: 9, with separate coverage collection.
- ReportGenerator rendered the replacement report from those fresh results. All three production
  assemblies were present. Existing package-advisory and generated Razor warnings were emitted.
- The exact-baseline adapter downloaded and validated the real
  [main coverage artifact](https://github.com/azabluda/InfoCarrier.Core/actions/runs/40303337085).
- Adapter checks cover missing/expired/malformed artifacts, revision selection, incomplete patches,
  moved/deleted file inputs, stale publication, and canonical-comment replacement. They do not run
  the action's Java engine or substitute for inspecting actual GitHub annotations.
- A separate temporary Java evaluation ran the pinned `GitHubDiffProvider` and
  `GitHubAnnotationsBuilder` against autograding-model 17.1.0, the pinned action's dependency.
  Controlled Cobertura inputs contained an uncovered addition, a partially covered branch,
  coverage lost on existing lines, deleted covered code, and uncovered callback/supported-path
  placeholders. The added line and partial branch produced source findings. Existing/deleted
  lines did not become changed-code findings or dead-code claims. A rename produced the new path.
  Whole-project changes included the other differences and did not locate existing-line loss.
- That evaluation caught and corrected two configuration errors: each coverage group needs one
  scope, and the filesystem matcher needs a leading `**/` for both current and reference paths.
  The final generated configuration also parsed the fresh local report and downloaded baseline,
  with both scopes and exact mirrored paths. The focused/full-suite difference was intentionally
  not interpreted as a regression. Temporary Java/Maven files and reports remain ignored artifacts.

Integration acceptance remains a separate observation: inspect the first pull-request report for
baseline/revision links and a single updated comment. A reporting-only pull request has no added
production lines. The local pinned-engine probes establish rendering and path behavior, not a live
GitHub annotation publication. Inspect annotations on the next production-changing pull request before
claiming that integration result. Existing-code loss remains explicitly unsupported; aggregate
differences must not be described as locating it. Do not close the issue based only on adapter tests.

Delivery checklist:

- [x] C1: Verify the pinned tools, evaluate historical and controlled findings, implement the
  smallest adapter, and validate it locally without production changes.
- [ ] C2: Inspect the pull-request workflow, exact revision links, and the single updated comment
  after continuous integration. Keep live source-annotation publication explicitly unverified until observed.
