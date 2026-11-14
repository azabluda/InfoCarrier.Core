# Firebird function-query coverage restoration

Issue: [#281](https://github.com/azabluda/InfoCarrier.Core/issues/281).
Branch: `restore/firebird-ef11-281`, based on verified `main` commit
`995560bed11c1c51a517f83191e23a83978ec960`.

## Provider and engine

On November 13, 2026, NuGet's package index listed `14.0.0-rc1` as the newest
`FirebirdSql.EntityFrameworkCore.Firebird` package. This is a release candidate, not a stable
provider release. Its downloaded package targets `net11.0` and declares
`Microsoft.EntityFrameworkCore.Relational >= 11.0.0` and `FirebirdSql.Data.FirebirdClient >= 10.3.4`.
The restored assets resolve the EF Core family exclusively to 11.0.0.

The embedded engine uses the existing Windows and Linux x64 native-assets packages at 2.0.6.
No direct `FirebirdDb.Embedded.NativeAssetManager` reference is needed: the native-assets packages
already bring version 1.0.6 transitively. The harness resolves native libraries from
`AppContext.BaseDirectory`, including the Linux path correction.
No database provider or native-engine dependency is added to a shipped project.

## Published generator and retained expectations

The package records repository commit `c34a3efcb2864d970781622eb5d3483b4c450b9e` on `ef11`.
GitHub did not resolve that commit during verification. Therefore, this evidence does not claim
verified ancestry from upstream [pull request #1279](https://github.com/FirebirdSQL/NETProvider/pull/1279),
merged as `9f78da81fc80c1334d11860ffb55328ba7ce1926`.

Instead, Roslyn CodeLens inspected the actual published binary. `VisitCrossApply` emits `JOIN`,
`VisitOuterApply` emits `LEFT JOIN`, and both delegate source generation to `GenerateApplySource`.
Both append `ON TRUE`. Complete Firebird execution then passed with the provider's unmodified
generator, including correlated function calls and standalone function roots. This establishes
that the published package supplies the behavior required to remove our correction.
`FirebirdLateralQuerySqlGenerator`, its factory, and their service replacement are removed.
The historical source under `subrepos/firebird` remains untouched and describes provider 13.

The single retained function-test override calls the inherited body. It declares an extra server
reader step when a client projection throws. The complete direct-versus-wire comparison confirms
that this statement/reader difference still exists. No new store limitation or InfoCarrier defect
reason is introduced. The inherited `QF_Select_Direct_In_Anonymous_distinct` skip still cites
[EF Core issue #26078](https://github.com/dotnet/efcore/issues/26078).

## Test execution and integration

Firebird source compilation, provider/native references, and `UdfDbFunctionTestBase` compliance
coverage are restored together. `Measure.cs` already runs the containing functional project, so
no duplicate project entry is needed. The normal build, release validation, and coverage workflows
already run this project. The optional slow-comparison workflow again offers SQLite, Firebird,
or both on separate runners.

Separate local functional processes ran sequentially. Firebird still uses working-directory
`.fdb` names and best-effort startup cleanup; closed files have no cross-process ownership lease.
Keep those processes sequential even after SQLite isolation is merged. Test collections within
each process remain parallel.

Initial Windows validation with the unmodified generator and restored compliance:
Passed: 113, Failed: 0, Total: 114. One inherited case was skipped.
Complete Firebird live comparison: Passed: 110, Failed: 0, Total: 111.
All specification function cases use the direct/wire comparer, which checks normalized statements,
command outcomes, reader counts, and test outcomes. The four smoke tests execute once through the
wire. The focused normal run's timestamps show three overlapping passed cases, including compliance.
Transport validation: Passed: 395, Failed: 0, Total: 395.
Engineering validation: Passed: 61, Failed: 0, Total: 61.
The complete `Measure.cs` run in its default Debug configuration reports
Passed: 30346, Failed: 0, Total: 30579, with 233 skipped, in 43.7502 minutes.
The nonincremental Release build under CI warning rules passes with the five documented
framework Razor trim warnings. Documentation links and whitespace checks pass.
Structural wire compatibility passes against the verified base, with contract 4 retained.

The first Release coverage baseline reported Passed: 30235, Failed: 1, Total: 30468.
`AnonymousShapeCapacityTest` exceeded its two-minute child-process deadline. The complete normal
run passed this test. The isolated run with the same coverage collector then reported
Passed: 1, Failed: 0, Total: 1. Its child completed all capacity assertions in 5.379 seconds;
the outer test process took 34.6225 seconds including discovery and child startup. This does not
explain or fix the timeout under full-suite load. The full coverage run remains a failed run,
not a successful gate. No capacity-test assertion or deadline was changed in this Firebird branch.

Firebird-only Release coverage reported Passed: 110, Failed: 0, Total: 111.
Linux native loading and execution require continuous-integration confirmation. The unrelated
capacity-test deadline remains a validation limit for review. MongoDB restoration and package
publication remain separate work.

## Coverage method

Measure with `eng/coverage.runsettings`, Release configuration, and the same final product binaries.
Collect the functional suite excluding the Firebird namespace, plus transport and engineering tests.
Merge these reports, plus the successful isolated capacity-test report, for a local baseline.
Collect the Firebird namespace separately, then add only that report to the baseline inputs.
Compare covered line records with an identical denominator. Both reports include the same
capacity-test supplement, so it cannot contribute to the measured Firebird difference.

### Results, November 14, 2026

| Input set | Covered product lines | Coverable product lines | Displayed line coverage |
|---|---:|---:|---:|
| Functional tests excluding Firebird, transport, engineering, and capacity supplement | 9557 | 10342 | 92.4% |
| The same inputs plus Firebird only | 9567 | 10342 | 92.5% |

Firebird recovers **10 product lines**, or **0.09669 percentage points**. Both merged reports
contain three product assemblies, 217 classes, and 166 files. Branch coverage is unchanged at
4305 of 4710 branches. The recovered line records belong to `ModelDbFunctions.cs`,
`ProjectionRewriter.cs`, `QuerySplitter.cs`, and `ServerContextExpression.cs`.

The collector is 10.1.0, `dotnet-coverage` is 18.12.0, and ReportGenerator is 5.6.0, matching the
repository's pinned workflow tools. Inputs and merged reports are retained under
`artifacts/firebird-281/coverage/`; console evidence is under `artifacts/firebird-281/`.
These artifacts are local measurement output and are not committed SQL baselines.

The adoption report's 92.4% is a dated Linux continuous-integration baseline. This Windows pair
isolates Firebird's marginal contribution under identical local settings and reproduces 92.4%
at the displayed precision. It does not establish an exact cross-platform percentage-point difference.
The earlier 93.5% also included MongoDB,
so the complete gap cannot be assigned to Firebird.
