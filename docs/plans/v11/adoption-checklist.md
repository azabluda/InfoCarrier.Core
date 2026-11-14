# Adoption checklist

What moves when EF Core 11 is adopted. **Issue
[#99](https://github.com/azabluda/InfoCarrier.Core/issues/99) lists most of it**, and was written on
2026-09-09. This file re-reads it against the repository on 2026-09-21 and adds what it misses. Read
each line against the repository again when the work starts.

## Adoption acceptance scope

Owner additions, November 12, 2026. Complete these checks before declaring adoption complete.

- [x] Adopt .NET 11 and EF Core 11 throughout SDK selection, all dependencies, tests, samples,
  documentation, and CI. Keep the active graph free of EF Core 10 dependencies. Suspend Firebird
  and MongoDB tiers until compatible providers exist; retain a clear restoration path.
- [x] Remove obsolete xUnit utilities after moving to xUnit v3. Remove redundant production code
  and tests only after retained coverage verifies equivalent behavior.
- [x] Review every existing active `StoreLimit`, `StoreIssue`, `StoreDefect`, and `InfoCarrierXxx`
  override against the final EF Core 11 source and measured behavior. Remove obsolete overrides
  and correct categories. Investigate thoroughly before introducing an InfoCarrier reason.
- [x] Run previously skipped active tests again. Record why each remaining skip still applies.
- [x] Review copied upstream test bodies and compatibility shims. Prefer inherited tests when
  upstream changes make local copies unnecessary.
- [x] Compare the new upstream test inventory and features with adopted coverage. Investigate
  gaps rather than replacing new failures with broad exclusions.
- [x] Run the complete functional suite and slow live comparison suite. Measure cold and warm
  runs, and recheck the execution times recorded in
  [#279](https://github.com/azabluda/InfoCarrier.Core/issues/279). Distinguish harness overhead,
  production behavior, and upstream execution costs. Do not inspect the parked failed adoption.
- [x] Preserve parallel test execution, as in version 10. Verify actual overlap, not only runner defaults.
- [x] Check structural and behavioral wire compatibility, historical payloads, and decoding.
  State whether the migration requires a wire protocol version change, with supporting evidence.
- [x] Verify complete sample applications, tutorial instructions, runtime-sensitive documentation,
  CI workflows, packaging, trimming, and the final dependency graph.

## Documentation follow-up

Added November 10, 2026, for the version 11 documentation overhaul. Complete this review during
adoption. Package publication and site publication are separate steps; adopting EF Core 11 does
not make either one complete.

- [x] Replace the pending-adoption section and drafting comment in
  `website/docs/release-notes/11.0.md` with features verified through InfoCarrier and links to their guides.
  Put each feature's usage instructions in the relevant application guide, not only in release notes.
- [x] Confirm runtime, SDK, and dependency requirements. Update `website/docs/getting-started/installation.md`
  and `first-app.md`, including `net10.0` and `10.*` commands where required. Do not infer target
  framework requirements from the library's version number alone.
- [x] Update framework statements in `README.md`, the Core and AspNetCore `src/*/PACKAGE.md` files,
  `website/docs/upgrades/from-10-to-11.md`, and the announcement in `website/overrides/main.html`.
  Review the Abstractions package readme too, even though it has no EF dependency.
- [x] Search the active guides and entry points for `Entity Framework Core 10`, `EF Core 10`,
  `net10.0`, `10.*`, and `adoption`. Review each result. Keep historical release notes and the
  version 3-to-10 upgrade guide tied to their own framework versions.
- [x] Compile and run the complete tutorial and affected examples with the adopted dependencies.
  Recheck provider limits, translation, streaming, browser behavior, and security claims affected
  by adoption. Review the 10.3-to-11 upgrade instructions against those results.
- [x] Before separately authorized publication, review unpublished-package notices in installation,
  the first tutorial, README, and the upgrade index. Review the draft release-note introduction,
  the `11.0 (draft)` navigation label, and `extra.development_docs`. Follow `docs/versioning.md`.
  Keep development notices until the corresponding publication state actually changes.

These are manual review items. The strict site build and link checks do not detect an outdated
framework statement or an unverified capability.

## Adoption investigation, November 12, 2026

The work is on `adopt-efcore-11`. Adoption checks completed on November 12, 2026, including both
complete suites and the owner's manual browser smoke check. Local logs and test results are under `artifacts/ef11-*`.
They are investigation output, not committed SQL baselines.

The active solution has 13 projects targeting .NET 11. Its restored assets contain no EF Core 10
packages. At adoption, Firebird source was excluded from compilation; the MongoDB project is outside the solution
and explicitly refuses direct builds. Restore either tier only with an EF Core 11 provider, then
restore its compliance coverage, measurement entry, and workflow together.

The override and skip rechecks cover the active InMemory and SQLite tiers. Suspended Firebird and
MongoDB source retains its previous expectations for restoration; it does not claim EF Core 11
validation. Recheck those providers and their retained overrides when compatible versions exist.
Firebird was restored on November 13 with provider 14.0.0-rc1; the
[restoration evidence](firebird-restoration.md) records its dependency, override, comparison, and coverage checks.

The override investigation temporarily removed 560 unique attributed methods and ran their inherited
expectations. Of 954 cases, 26 passed and 928 failed. The passing methods were then compared directly
with SQLite: 40 cases produced two passes and 38 failures. Both passes belonged to
`Update_with_two_inner_joins`; its obsolete skipped override was removed. The other apparent passes
did not establish equivalent direct-store behavior. This includes upstream skips where SQLite still
refuses `APPLY`, and compiled-query cases whose row-read counts still differ. Their reasons remain.
No new InfoCarrier design or defect reason was added to handle adoption failures. The final audit
also uses exact declaring-type references for the temporal and miscellaneous scalar tests; matching
only a repeated method name had pointed several temporal tests at the byte-array override.

`Select_expression_datetime_add_ticks` was reclassified from a limitation to the wrong-answer defect
in [section 1.16](../../upstream-defects.md#116-sqlite-loses-subsecond-precision-in-the-addticks-translation).
SQLite's newly supported byte-array join uses the positive `StoreSupport` category, with the exact
upstream successful test as evidence. A successful answer is not classified as a refusal.

The xUnit v2 executor and obsolete display-name/theory-serialization tests were removed. Normal
current-test identity uses xUnit v3's `TestContext`; the custom executor runs only for live comparison.
Folded upstream ad-hoc classes now use their surviving shared adapters. Redundant bulk inheritance
adapters, fixed TimeOnly overrides, and version 10 compatibility constructors were removed.

The complete Release functional run passed 30,236 cases, failed zero, and reported 30,468 total
with 232 skipped in 16 minutes 25 seconds. Transport tests passed 395 of 395; engineering tests
passed 61 of 61. The forced Release CI build passed with the five documented Razor trim warnings.
The clean trim check passed at 116 InfoCarrier warnings, and all three version 11 packages packed.
The documentation build and all 112 Markdown link checks passed.

The tutorial's three literal C# blocks compiled and produced `Before: Example Shop` and
`After: Updated Shop`. Because version 11 packages are unpublished, this local validation used
project references for InfoCarrier and the adopted central EF dependencies. The console sample
completed all operations in 12 round trips. The new full-join and frozen-set guide snippets also
compiled and executed against the tutorial server. Browser automation cannot navigate through the
browser-control integration, including after a restart and reboot. The owner manually checked the
web sample on November 12 and reported that it works. This is manual smoke evidence, not an
automated browser trace or a claim about every published browser application's trimming behavior.

Wire contract 4 is retained. The structural guard and separate-process preserved protocol 3 and
protocol 4 client checks passed. The expanded protocol 3 application passed its behavioral checks,
including independently read database effects. The [wire assessment](../../wire-protocol.md#contract-evolution)
records the added complex shadow data and why existing field meanings remain compatible.

Timing runs and the slow suite must run sequentially in a shared working directory. An overlapping
timing probe reproduced startup cleanup deleting another test process's database files. That slow
run was stopped and discarded; its failures are not evidence of a production regression. The
replacement slow run uses the directory exclusively.

Test collections remain parallel. Result timestamps show a peak of 12 overlapping test cases in
both the complete functional run and the slow comparison. No assembly-wide parallelism disable
was added. The custom comparison executor forwards xUnit's parallel mode, scheduler, and each
test case's existing parallelism setting. Separate suite processes are serialized only when
they share a working directory and its database cleanup.

The final `Measure.cs` run completed before the machine reboot: zero failures in 30,468 cases,
with 30,236 passed and 232 skipped. The last full slow run found seven SQL differences in
28,369 cases. Six required explicit declarations of the new conditional anonymous projections;
the seventh required preserving EF's unknown discriminator state to avoid an extra update column.
All 13 focused direct/wire comparison cases passed after these corrections. The final complete
slow run then passed 28,203 cases, failed zero, and reported 28,369 total with 166 skipped in
18 minutes 28 seconds. Its timestamp intervals confirm 12 overlapping test cases. No new
InfoCarrier reason was needed to make the comparison pass.

### Test timing recheck after reboot

Two sequential fresh-process runs on November 12 used SDK 11.0.100, .NET 11.0.0, Release binaries,
no build or restore, and the exact filter
`InfoCarrier.Core.FunctionalTests.Sqlite.Query.NorthwindWhereQuerySqliteInfoCarrierTest.Where_simple`.
Each run passed both synchronous and asynchronous cases. No other functional suite ran concurrently.

| Measurement | First run after reboot | Next fresh process |
|---|---:|---:|
| Process wall time | 42.332 s | 39.820 s |
| xUnit discovery completed | 13.09 s | 12.10 s |
| xUnit execution started | 13.41 s | 12.35 s |
| xUnit execution finished | 40.10 s | 37.96 s |
| Asynchronous case | 731 ms | 808 ms |
| Synchronous case | 31 ms | 35 ms |

The second process benefits from filesystem caches, but builds fresh model and fixture state.
Within each process the second case reuses the fixture; it also uses a different execution mode,
so the case figures are not a controlled async-versus-sync benchmark. Runner timestamps and case
durations show that discovery and fixture initialization dominate the selected test's wall time.
They do not isolate reflection, model construction, seeding, or backfill individually.

The long isolated-run time described in [issue 279](https://github.com/azabluda/InfoCarrier.Core/issues/279)
is therefore still observable with the adopted harness. This is not evidence of a query taking
twenty seconds, nor a controlled EF Core 10/11 production comparison. The original issue does not
name its selected method, so its 23.5-second figure cannot be compared as an identical workload.
Detailed component profiling and production benchmarks remain that issue's separate performance work.
The parked adoption attempt was not inspected. Logs and TRX are in `artifacts/ef11-timing` and
`artifacts/ef11-timing-post-reboot-*.log`.

### Reproduce the upstream source audit

The EF Core reference is the official `v11.0.0` commit
[`9e685c7a52d1a344d133a3d370e458db97417b3f`](https://github.com/dotnet/efcore/tree/9e685c7a52d1a344d133a3d370e458db97417b3f).
The existing `subrepos/efcore` checkout remains untouched. Download the
[source archive](https://github.com/dotnet/efcore/archive/9e685c7a52d1a344d133a3d370e458db97417b3f.zip)
and extract its contents into `artifacts/efcore-v11`, without the enclosing archive directory.
Create `artifacts/efcore-v11/.source-revision` containing the full commit identifier above.
The audit checks that revision before checking reference lines. Without local reference source,
it reports unchecked references; it does not claim those line checks passed.

## Firebird restoration, November 14, 2026

- [x] Restore the EF Core 11 Firebird provider, embedded dependencies, source compilation,
  compliance coverage, measurement inputs, and workflows together.
- [x] Recheck retained expectations and execute complete Firebird normal and direct/wire tests.
  Remove the obsolete SQL-generator correction after the published generator passes those queries.
- [x] Measure Firebird's marginal product coverage using matched local inputs. The
  [restoration evidence](firebird-restoration.md#results-november-14-2026) records the measured
  recovery, exact denominator, and remaining limits without attributing MongoDB's gap to Firebird.
- [ ] Confirm Linux continuous-integration results and owner review. The full local coverage run
  exceeded an unrelated capacity-test child deadline; its isolated coverage run passed. That
  limit remains explicit in the restoration evidence. MongoDB remains suspended.

## Provisional Cosmos document tier, November 14, 2026

- [x] Add a separate official Cosmos 11.0.0 test project without changing or enabling retained MongoDB source.
- [x] Add complete document tests, six inherited query families, direct controls, and pinned Podman/Docker lifecycle commands.
- [x] Complete local suite, portability, transport, trimming, dependency, and wire validation.
- [x] Run the vNext tier from Windows and Linux test hosts against the same Podman Linux emulator, with upstream's aggregate skips.
- [x] Verify the earlier default-query classic-emulator suite on Linux Docker in GitHub Actions.
- [x] Verify the aligned vNext tier and upstream skips on Linux Docker in GitHub Actions (`0b2a553e`).
- [ ] Owner decision: retain Cosmos temporarily or permanently. This decision remains separate from implementation acceptance.

See [the validation record](cosmos-document-tier.md) for scope and the production change review.

### Expanded official Cosmos suite, November 14, 2026

The owner's later request is all applicable official Cosmos tests through InfoCarrier. The original
six-family validation above does not complete this larger scope.

- [x] Discover the complete pinned upstream runner inventory without executing its tests directly.
- [x] Adopt arithmetic, comparison, logical, GUID, enum, and all scalar storage families with official mappings and referenced expectations.
- [x] Reproduce and fix the nonrelational terminal-projection failure found by the wider tests, with independent document regressions.
- [x] Validate the first expansion locally with the full functional/Cosmos run, transport, trimming, strict builds, and documentation checks.
- [x] Verify the first expansion on Linux Docker, both normally and with coverage (`4b85adb6`).
- [x] Adopt all six complex-property query families, including the seven additional Cosmos-specific scenarios; compare discovered case names with the official runner.
- [x] Validate the complex-property expansion locally with the complete functional/Cosmos measurement, strict builds, and remote exception checks.
- [x] Report an unavailable emulator with its endpoint and explicit Podman/Docker startup commands; verify stopped-container failures and healthy startup across every initialization path.
- [ ] Complete the remaining class-by-class applicability review and test adoption listed in the
  [expanded scope](cosmos-document-tier.md#expanded-official-test-scope).
- [ ] Validate the fully expanded suite on Windows/Podman and Linux/Docker, including coverage.

## What #99 says that is no longer true

The remaining sections preserve the September 21, 2026 pre-adoption review. Their package versions,
checkout descriptions, and provider constraints are historical. The acceptance evidence above
records the current .NET 11 and EF Core 11 state.

- **"Expect one large, deliberate re-baseline of `test/known-failures.txt`".** The ratchet and both
  of its files were deleted on 2026-09-15 (`b611193`). The suite is green and CI fails on any failing
  test, so every test EF 11 turns red ends as a fix, or as an override with a typed reason that
  `OverrideAudit` accepts ([`docs/test-policy.md`](../../test-policy.md)). There is nothing to
  re-baseline.
- **`Microsoft.EntityFrameworkCore` "at `[10.0.0,11.0.0)`"** is `[10.0.1,11.0.0)`, and so is
  `Microsoft.EntityFrameworkCore.Relational` since 2026-09-28: both reach the nuspec, so both ranges
  move to `[11.0.x,12.0.0)` together. The seven other EF packages are pinned at `10.0.1`, not
  `10.0.0`.
- **`subrepos/efcore` "at `release/10.0`, `v10.0.0`"** is at `v10.0.1`, the version of the
  specification packages.

## What #99 does not name

- **Tier D is the second dependency outside Microsoft, with the same schedule risk as Tier C.**
  `test/InfoCarrier.Core.DocumentStoreTests` runs `MongoDB.EntityFrameworkCore` `10.0.3`, which needs
  EF Core `>= 10.0.11`, so that project carries `VersionOverride="10.0.11"` on five packages. It
  cannot move until MongoDB ships a provider for EF Core 11. #99's question for Tier C (pin to EF 10,
  drop for a release, or block on upstream) is the same question here.
- **Every upstream reference is pinned by line, and they all move.** `UpstreamRepository.EfCore`
  names `dotnet/efcore` at `v10.0.1`, and `OverrideAudit` checks that each reference's lines declare
  the overriding test at that commit. Moving `subrepos/efcore` to the EF 11 tag fails the audit
  locally for each reference whose file changed. **CI cannot see this**, because a runner has no
  `subrepos/` and only counts what it could not check. The work is mechanical and large: a script
  that finds each test by name at the new tag and rewrites the line pair is worth writing before
  doing it by hand. `OverrideAuditTest`'s output lists every reference.
- **The same holds for `UpstreamRepository.MongoEfCore`**, pinned at `v10.0.3`, when Tier D moves.
- **The two enum members say their tags in their XML comments** (`OverrideReasonAttributes.cs`), and
  change with the pins.
- **The slow run has no tag to move.** It compares with plain EF Core at the version the suite
  runs. `eng/ef-sql-compare.sh`, which fetched EF's tag to read its `AssertSql` text, was deleted on
  2026-09-28. Its figures, now in `docs/plans/v10/findings.md`, are dated records of EF 10.
- **`subrepos/firebird` is at `EFCore-13.0.0.0`**, the tag of the package Tier C runs, and moves
  with whatever Tier C decides.
