# Provisional Cosmos document tier

November 14, 2026. The owner authorized implementation, local testing, and GitHub Actions validation.
Cosmos may be temporary or permanent; that decision remains open. MongoDB source stays unchanged,
disabled, and outside the solution. No release or MongoDB restoration is part of this work.

## Current test setup

The tests run on Windows or Linux. Cosmos always runs in a Linux container. The owner explicitly
prefers Podman over installing a native Windows Cosmos emulator.

| Component | Configuration |
|---|---|
| Provider | Microsoft's `Microsoft.EntityFrameworkCore.Cosmos` 11.0.0 |
| Emulator | Official Linux vNext `vnext-EN20261008` |
| Immutable image | `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:81707bcadf894a29d7dab63377685d408b5d54dca3aacfb625c19db4c6392e7c` |
| Framework reference | EF Core 11.0.0, commit `9e685c7a52d1a344d133a3d370e458db97417b3f` |
| Local Windows | Windows `dotnet test` process connecting to Podman 6.1.3 |
| Linux | Linux test process connecting to the same image; Docker in GitHub Actions |

This follows EF Core's Linux-emulator test path, including its known emulator skips, on both hosts.
It does not reproduce their separate native Windows emulator path. Their Testcontainers startup
is replaced by this repository's existing explicit C# lifecycle command, supporting Podman and Docker.
See the pinned [upstream environment](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/test/EFCore.Cosmos.FunctionalTests/TestUtilities/CosmosTestEnvironment.cs)
and [test jobs](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/eng/helix.proj).

On this Windows machine:

```powershell
podman machine start infocarrier-cosmos
dotnet run --file eng/CosmosEmulator.cs -- start podman
dotnet test test/InfoCarrier.Core.CosmosTests/InfoCarrier.Core.CosmosTests.csproj --configuration Release
dotnet run --file eng/CosmosEmulator.cs -- stop podman
```

Use `docker` instead of `podman` in the lifecycle command on Linux CI, or locally with Docker
Desktop running Linux containers. The runtime argument selects the executable; the operating
system resolves it through its normal executable search, including `PATH`. PowerShell aliases
are not used. Docker selects its engine through its
[active context and connection settings](https://docs.docker.com/engine/manage-resources/contexts/).
Inspect these with `docker context ls` and `docker info`. Windows tests against Docker Desktop
have not been validated on this machine; Windows/Podman and Linux/Docker have been validated.

Startup waits up to three
minutes for `/ready`. Ports 8081 and 8080 bind to loopback. Cleanup removes only the task-owned
container. Each fixture owns a separate generated database. Test collections remain parallel.
An unavailable emulator fails the tests instead of silently skipping them.

Fixtures do not start or stop containers. Before database setup, custom fixtures, inherited/direct
test stores, and standalone named-store tests check endpoint reachability with a five-second timeout.
If the endpoint cannot be reached, the error names it and gives both `start podman` and `start docker`
launcher commands. Database cleanup is not attempted after a failed startup check. A received HTTP
response leaves authentication and service validation to the provider; ordinary provider failures
are not rewritten as missing-container errors. Test cancellation keeps its cancellation exception.
Failed checks are not cached, so starting the emulator and rerunning tests in the same host works.

The stopped-container check intentionally failed five cases across all startup paths, each with
the endpoint and both launch commands. After explicit startup, the focused run reported Passed: 11,
Failed: 0, Total: 11, including six standalone diagnostic controls. Logs are under
`artifacts/cosmos-suite-audit/emulator-*`. These negative startup checks are not part of the normal
suite's failure count, and the diagnostic controls do not require an emulator.
The complete normal check then reported Cosmos Passed: 447, Failed: 0, Total: 489, with the same
42 upstream skips; functional Passed: 30350, Failed: 0, Total: 30583. The combined measurement was
FAILING: 0, TOTAL: 31072, with no broken tests and unchanged failure reasons. Evidence is under
`artifacts/measure/cosmos-emulator-diagnostics*`.

The client uses gateway transport and accepts the emulator certificate only on loopback HTTPS.
The embedded key is Microsoft's public emulator key, not an Azure credential. Neither
`AZURE_COSMOS_BYPASS_QUERY_PARSING` nor `AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED` is set.
Query results and service responses are not rewritten to accommodate the emulator.

## Upstream skips and coverage limits

For the original owned-navigation families, EF Core 11 skips four aggregate methods on the Linux
emulator. One has two tracking cases.
The direct and wire variants therefore report ten skipped test cases. The adopted overrides name
the exact upstream source ranges and retain its skip message and justification. The defect is
recorded in [the upstream-defect register](../../upstream-defects.md#21-linux-cosmos-emulator-misprocesses-aggregate-subqueries),
with the existing [emulator report](https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/330).

| Upstream method | Cases per direct or wire variant |
|---|---:|
| `Where` on owned collections | 1 |
| `Select_within_Select_within_Select_with_aggregates` | 1 |
| `Select_Sum` on primitive collections | 1 |
| `Over_assocate_collection_Select_nested_with_aggregates_projected` | 2 |

These skips depend on the database emulator, not the test host's operating system. They leave
explicit coverage gaps and do not claim that hosted Cosmos lacks these features. No extra query
family or custom document test is dropped. Corrections to upstream's filtered/unfiltered and
required/optional query-body mix-ups remain documented on the relevant override attributes.

## Validation scope

| Area | Checks |
|---|---|
| Queries | Owned-navigation families with direct controls, complex-property families, all basic scalar translations, Northwind functions, primitive collections, Find, and bounded document projections |
| Scalar storage | All 16 official scalar type families through InfoCarrier, subject to their upstream primitive-collection skips |
| Complex tracking | References and collections, state transitions, original/current values, change detection, and persistence with the official synchronous skips |
| Writes | Insert, read, scalar update, nested replacement, collection update/removal, optional-reference removal, root deletion |
| Stored shape | Independent Cosmos client reads verify nested objects, arrays, and deeper references |
| Incomplete documents | Attached stubs and scalar-only changes preserve independently read nested values |
| Concurrent changes | Repair preserves another context's nested changes and original concurrency entity tags |
| Policy isolation | Named Cosmos and InMemory registrations retain separate repair policies in both registration orders |
| Keys and projections | Generated GUID keys, nested scalar projections, and payload boundaries |
| Failures | Explicit transaction refusal and missing-document update outcomes, with direct controls |
| Other providers | InMemory and SQLite cover synchronous/asynchronous repair, context filters, and real SQLite transactions |

## Expanded official-test scope

The owner subsequently requested all applicable official Cosmos tests through InfoCarrier, rather
than every official test executing directly against Cosmos. The earlier six-family tier is not
complete against that request. ADR-009 records the expanded scope and the exception to tier deduplication.

Discovery of the pinned official Cosmos assembly found 7,374 cases across 122 concrete test classes.
That inventory includes provider infrastructure, model construction, SQL baselines, and application
behavior; it is not a count of cases already adopted or a final applicability decision. Discovery
used the official runner without executing tests or contacting the emulator. Its output remains
under `artifacts/cosmos-suite-audit/`, together with the source build and class inventory.

The first expansion inherits the arithmetic, comparison, logical, GUID, and enum specification
families with the official Cosmos basic-types mapping. It also adopts all 16 official scalar
storage families, using their exact values and mappings. Behavioral assertions remain inherited.
Cosmos-specific refusals and primitive-collection skips carry exact upstream source references.
The scalar storage families retain 13 Linux-emulator aggregate skips and three provider-design
skips: converted GUID/byte-array collection elements and byte equality inside primitive collections.

The second expansion adopts all six complex-property association families: collection, miscellaneous,
primitive collection, projection, set operations, and structural equality. It includes seven additional
Cosmos-specific scenarios, not only the inherited common bases. Comparing official discovery with
the adopted run gives identical names for all 122 behavior cases, after normalizing class names and
excluding the six SQL-baseline completeness checks. The focused run, including the override audit,
reported Passed: 107, Failed: 0, Total: 123, with 16 official skips. No production change is required.

Five skipped cases concern aggregate subqueries, eight concern structural equality, two concern
projection ordering, and one concerns an entity collection crossing document boundaries. The
[defect register](../../upstream-defects.md) distinguishes the open projection-order report from the
closed structural-equality report that the pinned official suite still cites. Retaining that upstream
skip is not a fresh claim that the selected image still fails. Required/optional nested projection
mix-ups in the upstream test bodies are corrected, with explicit deviation notes on both overrides.
No golden SQL baselines are copied. The upstream `Check_all_tests_overridden` checks exist to enforce
those baselines, so they are not adopted as application behavior tests.

The third expansion adopts the remaining eleven basic translation families: bitwise, byte array,
date, date/time, date/time offset, time, time span, math, string, and both miscellaneous families.
Their 319 behavior-case names match the pinned official inventory exactly after excluding SQL
baseline completeness checks. It also adopts all three `Find` variants, built-in data types,
complex-type tracking, Northwind database functions and general functions, and primitive collections.
The tracking and primitive-collection adapters include the additional official Cosmos-specific cases.

Synchronous calls continue through InfoCarrier's synchronous server contract. The `Find` and
Northwind tests assert the official Cosmos synchronous refusal rather than redirecting calls to
asynchronous execution. Complex tracking preserves the official synchronous skips. Its execution
helper uses separate contexts and cleans the database, matching upstream's transaction-free setup.
Two upstream `Find` theory skips now enumerate all three inherited cancellation modes instead of
one collapsed skipped case, adding twelve reported skips across the three variants. Two built-in
type theory skips similarly enumerate both inherited execution modes, adding two reported skips.
These discovery differences add no behavior claim or new skipped method.

Primitive-collection tests that build a model per test use the existing store-neutral harness,
relocated from the functional project into `TestUtilities`. The namespace and implementation stay
the same. Its Cosmos adapter explicitly enables the document policy for deferred server models.
The shared fixture registers the official custom collection type and `CosmosDbFunctionsExtensions`
on both halves. These are explicit test configuration grants, not expanded production allowlist
inference. The two official wrong-result assertions are recorded separately in the
[defect register](../../upstream-defects.md#117-cosmos-nullable-collection-minimum-differs-from-the-common-specification).
No production code or wire format changes are needed for this expansion.

Verified exclusions from the upstream inventory:

| Official class or method | Reason |
|---|---|
| `CosmosApiConsistencyTest` | Reflects over the Cosmos provider assembly, its fluent API signatures, and provider service registrations. It executes no application query or update through a context. InfoCarrier cannot change these third-party API surfaces. |
| `ConnectionSpecificationTest` | Calls the directly resolved Cosmos `IDatabaseCreator` to verify `UseCosmos` connection-string configuration and missing-connection validation. InfoCarrier clients use `UseInfoCarrier` and have no Cosmos connection. This excludes those two configuration checks, not application connection failures. |
| Query-family `Check_all_tests_overridden` methods | Require every shared method to have an override for SQL-baseline assertions. Inheriting unchanged behavioral tests is intentional here. |
| `FullTextSearchTranslationsCosmosTest`, `HybridSearchCosmosTest` | Both official classes require `IsNotEmulator`. These application features remain outside the selected emulator scope, rather than being classified as provider internals. |
| `VectorSearchTranslationsCosmosByteTest`, `VectorSearchTranslationsCosmosSByteTest`, `VectorSearchTranslationsCosmosFloatTest`, `VectorSearchTranslationsCosmosMixedTest` | Each official class requires `IsNotLinuxEmulator`. The same exclusion applies on both test hosts because both use the Linux emulator. |

These decisions follow the pinned [API consistency class](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/test/EFCore.Cosmos.FunctionalTests/CosmosApiConsistencyTest.cs)
and [connection configuration class](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/test/EFCore.Cosmos.FunctionalTests/ConnectionSpecificationTest.cs).
Remaining inventory entries still require explicit adoption or a reviewed exclusion.

The search exclusions follow the pinned [full-text class](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/test/EFCore.Cosmos.FunctionalTests/Query/Translations/FullTextSearchTranslationsCosmosTest.cs#L9),
[hybrid class](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/test/EFCore.Cosmos.FunctionalTests/Query/Translations/HybridSearchTranslationsCosmosTest.cs#L9),
and [four vector classes](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/test/EFCore.Cosmos.FunctionalTests/Query/Translations/VectorSearchTranslationsCosmosTest.cs).
They do not exclude the separate ad hoc search classes, which need their own review.

Remaining work is explicitly open:

- [ ] Review every remaining official class and record a specific applicability decision.
- [ ] Adopt applicable Northwind, association, complex/JSON, primitive-collection, and other query tests.
- [ ] Adopt remaining scalar translations, full-text/vector behavior, and application-level extension tests.
- [ ] Adopt applicable tracking, keys, conversions, concurrency, updates, and failure tests.
- [ ] Review direct Cosmos service, model-building, design-time, logging, session, and transaction tests
  individually. Provider-internal assertions may be excluded; ordinary application failures may not.
- [ ] Validate the complete expanded suite on Windows/Podman and Linux/Docker, including coverage.

Do not count missing families as unsupported tests or describe these expansions as the complete
official suite. The dated results below identify their tested scope.

## CI parallelization and remaining follow-up

The owner subsequently moved CI parallelization ahead of the next adoption batch. Testcontainers
remains a follow-up after completing the applicable suite.

1. Split functional and Cosmos validation into parallel GitHub Actions jobs on separate runners,
   with a separate job combining results. Coverage collection uses independent functional and
   Cosmos matrix runners, followed by one merge/report job. Transport and engineering coverage
   remain sequential after Cosmos on its runner. The required `Spec tests` and `Coverage report`
   names, combined test artifact, fork report, coverage report, and badge remain intact.
   `eng/RequireSuccessfulJobs.cs` rejects failed, cancelled, skipped, or missing worker results;
   aggregation must not turn an unsuccessful worker into a green required check. The owner requested
   timings from the sequential `be7e11a7` run before pushing this optimization. Compare execution
   time separately from queue/setup time, and retain the raw job and step timestamps under artifacts.
2. Explore Testcontainers for automatic emulator startup, readiness, and cleanup, following the
   official EF Core approach. Verify Windows tests against the owner's Podman machine and Linux
   tests against Docker, including cleanup after failures. The Podman machine must still be running;
   Docker-compatible endpoint discovery and cleanup need local validation before replacing the
   explicit launcher. Keep the pinned image, upstream emulator behavior, and test parallelism.

The parallel workflow validation and timing comparison completed before the next batch was pushed.
The Testcontainers option is not implemented by this change.

Both parallel workflows passed for `202e3b0f`. The successful sequential baseline is `be7e11a7`.
The specification and product code are identical across this timing comparison; the parallelism
commit adds only workflow/gate changes, engineering tests, and documentation.

| Workflow execution span | Sequential | Parallel | Observed change |
|---|---:|---:|---:|
| Build & Test | 13m 14s | 15m 28s | 2m 14s slower |
| Coverage | 21m 05s | 18m 45s | 2m 20s faster |

Execution spans run from the first job start to the last job completion. Initial queue time was
two versus three seconds for Build & Test, and three seconds in both coverage runs. Coverage
improved by about 11.1% in this comparison. Normal functional test execution increased from
10m 34s to 14m 19s, outweighing the overlap; normal-build improvement is not established by this
pair. The coverage functional step was similar, 16m 10s versus 15m 54s. The separate Cosmos jobs
finished within three minutes while functional testing continued. These are single-run observations,
not an explanation of the functional slowdown.

The combined parallel specification report retained Passed: 31866, Failed: 0, Total: 32316.
The required aggregate checks passed, and coverage reports were combined successfully. Evidence is
under `artifacts/cosmos-suite-audit/ci-sequential-timings.json` and `ci-parallel-timings.json`.
The runs are [sequential build](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43632624152),
[sequential coverage](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43632624154),
[parallel build](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43634592305), and
[parallel coverage](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43634592263).

## Local batch validation policy

On November 14, 2026, the owner replaced repeated full local measurements for this initiative with
a focused loop: adopt a batch, run its cases and relevant controls locally, push when green, and
continue the next batch while CI runs the full suites. Production fixes need focused regressions,
but the complete functional and Cosmos suites are delegated to the required GitHub Actions checks.
The `cosmos-northwind-batch` full local measurement was stopped under this instruction; its partial
logs are not a completed validation result.

Before completing the pull request, run the existing slow SQLite and Firebird comparisons against
the final commit and review their outcomes. The manual GitHub Actions workflow can run both on
separate Windows runners and retain the exact tested revision and diagnostic artifacts. It remains
an optional workflow, not an automatically required merge check. The current workflow offers no
Cosmos slow-comparison selection; Cosmos direct controls remain part of its ordinary test suite.

## Production change review

The original production change from the Cosmos implementation is incomplete-document repair in
`ServerSaveChangesExecutor`. It uses the original context and provider services with an isolated
query tracker, retaining the application's connection, transaction, and query-filter state.
Provider-owned query compilation and caching retain parameterized keys.

The merge uses model entity types, primary keys, ownership, modification state, and concurrency
metadata. It preserves modified, added, and deleted entries, shadow keys, and original concurrency
values while filling omitted owned data. It contains no Cosmos dependency, provider-name branch,
document field name, or entity-tag convention. The isolated tracker is cleared after merging.

The InMemory and SQLite regressions support provider neutrality. A separate owner-authorized local
experiment ported this one class to the last EF Core 10 checkout, adapting two framework API calls.
The official MongoDB provider 10.0.4 baseline and the port both reported Passed: 237, Failed: 0,
Total: 237, with identical test names and outcomes. Evidence remains under
`artifacts/mongo-v10-portability/`. This supports the algorithm on those MongoDB models, but does
not establish MongoDB 11 compatibility or authenticate an unofficial package.

The first wider query tests also exposed an InfoCarrier projection failure: a reconstructed
terminal row limit always used `EF.Constant`, which Cosmos does not support. `QuerySplitter` now
uses that forced-constant marker only for deployments using relational query rules. Nonrelational
deployments receive an ordinary `Take` count; the provider chooses its representation. Server-side
row limits, ordering, and client projection reconstruction remain intact. There is no Cosmos name,
package dependency, or provider-type test in the production change.

Six independent document tests compare projected `First`, `FirstOrDefault`, `Last`, `LastOrDefault`,
`Single`, and `SingleOrDefault` with direct Cosmos execution. They also inspect wire responses for
unrequested rows. Before the fix, four failed and two passed. The complete scalar/projection/audit
subset then reported Passed: 46, Failed: 0, Total: 46. This is evidence for the nonrelational path,
not proof that every document provider supports every count representation. The MongoDB 10
experiment above predates this query change and does not validate it.

The query fix changes an existing expression's count representation, with the same bounded-row
meaning. It adds no wire node, message, field, or protocol version. The transport and historical
protocol regressions pass without changing their payload expectations.

## Results

Local validation of this setup, November 14, 2026:

| Host and check | Actual result |
|---|---|
| Windows host, Podman Linux emulator, complete Cosmos tier | Passed: 242, Failed: 0, Total: 252; 10 upstream skips |
| Native Linux host, same Podman emulator, complete Cosmos tier | Passed: 242, Failed: 0, Total: 252; 10 upstream skips |
| Focused direct primitive-collection tests and override audit | Passed: 6, Failed: 0, Total: 7; one upstream skip |
| Windows and Linux Cosmos builds with warnings as errors | Zero warnings, zero errors |
| Complete Release build with warnings as errors | Five documented framework Razor warnings, zero errors |
| Engineering suite | Passed: 61, Failed: 0, Total: 61 |
| Documentation links | Zero broken links in 114 Markdown files |

The Linux comparison uses a native Linux test host built in Microsoft's .NET SDK 11.0.100 container.
The Windows run uses the host's regular `dotnet test`; its test process does not run inside Linux.
Both use normal SDK query execution. Logs and result files remain under `artifacts/cosmos-official*`.

Earlier classic-emulator CI runs passed all 252 cases, including coverage, but that image crashed
locally. This revision standardizes on vNext and adopts upstream's known skips instead of retaining
a query-execution workaround or installing a Windows emulator. The original implementation's
functional, transport, trimming, and wire checks remain recorded in the pull request. This
emulator-alignment revision changes no production code or wire contract.

First official-suite expansion, November 14, 2026, Windows host and the same Podman Linux emulator:

| Check | Actual result |
|---|---|
| Full assembled Cosmos tier | Passed: 335, Failed: 0, Total: 361; 26 referenced upstream skips |
| Full functional suite after the query fix | Passed: 30350, Failed: 0, Total: 30583; 233 existing skips |
| Combined measurement | FAILING: 0, TOTAL: 30944 |
| Transport and protocol regressions | Passed: 395, Failed: 0, Total: 395 |
| Final terminal-projection payload assertions | Passed: 6, Failed: 0, Total: 6 |
| Strict full Release build | Five documented framework Razor warnings, zero errors |
| Final strict Cosmos build | Zero warnings, zero errors |
| Clean trimming check | 116 InfoCarrier warnings, matching the baseline |
| Documentation links | Zero broken links in 114 Markdown files |

The combined run's failure-name and failure-reason files are empty. Its logs are under
`artifacts/measure/cosmos-official-expansion*`; focused evidence is under `artifacts/cosmos-suite-audit/`.
These results validate the assembled first expansion, not the outstanding full official-suite request.

GitHub Actions passed for the first official-suite expansion `4b85adb6` in
[Build & Test](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43606161151) and
[Coverage](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43606161152). Both Linux Docker
Cosmos runs reported Passed: 335, Failed: 0, Total: 361, with 26 official skips. The Linux functional
suite reported Passed: 30346, Failed: 0, Total: 30583, with 237 existing platform skips. Transport
reported Passed: 395, Failed: 0, Total: 395; engineering reported Passed: 61, Failed: 0, Total: 61.
The second, complex-property expansion `3216fc67` also passed
[Build & Test](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43614289673) and
[Coverage](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43614289709).
The startup-diagnostic change `8b0a5f1f` passed
[Build & Test](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43620003370) and
[Coverage](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43620003405).
Emulator testing does not establish full Azure service
parity, distributed-query coverage, official MongoDB support, or release approval.

Second expansion validation, November 14, 2026, Windows host and the same Podman Linux emulator:

- Full assembled Cosmos tier: Passed: 441, Failed: 0, Total: 483; 42 referenced upstream skips.
- Full functional suite: Passed: 30350, Failed: 0, Total: 30583; 233 existing skips.
- Combined measurement: FAILING: 0, TOTAL: 31066. Failure-name and failure-reason files are empty;
  the comparison with the first expansion reports no broken tests and unchanged failure reasons.
- Official complex-property case comparison: 122 upstream behavior cases, 122 adopted cases, no
  missing or additional names after class-name normalization and SQL-baseline-check exclusion.
- Strict full Release build: five documented framework Razor warnings, zero errors.
- Final remote exception-type and message checks: Passed: 2, Failed: 0, Total: 2; the final strict
  Cosmos build reported zero warnings and errors.

Logs remain under `artifacts/measure/cosmos-official-complex*` and `artifacts/cosmos-suite-audit/complex-*`.
This extension changes tests and internal documentation only. It does not complete the remaining
official-suite adoption checklist, change the wire contract, or enable MongoDB.

Third expansion validation, November 14, 2026, Windows host and the same Podman Linux emulator:

- Full assembled Cosmos tier: Passed: 1520, Failed: 0, Total: 1733; 213 official skips.
- Full functional suite after the shared harness relocation: Passed: 30350, Failed: 0,
  Total: 30583; 233 existing skips.
- Combined measurement: FAILING: 0, TOTAL: 32316. Failure-name and failure-reason files are empty;
  there are no broken tests and failure reasons are unchanged from the startup-diagnostic baseline.
- Strict Release solution build: five documented framework Razor warnings, zero errors.
- The eleven new basic translation families match all 319 official behavior-case names.
  Complex tracking matches all 289 official cases, and primitive collections match all 176 behavior
  cases after excluding the SQL-baseline completeness check. All three Find variants and built-in
  types have identical method inventories; their additional reported cases enumerate official skips.

Logs remain under `artifacts/measure/cosmos-expanded-bases*` and `artifacts/cosmos-suite-audit/`.
The dated class inventory records 52 implemented official classes, eight reviewed exclusions,
and 62 entries still needing review and possible adoption. Those entries are not all distinct
specification bases. The complete official-suite milestone remains open. Linux Docker validation
passed normally and with coverage in the sequential and parallel runs linked above.

Fourth expansion validation, November 14, 2026, under the focused local batch policy:

- Adopted Northwind filtering, selection, keyless-entity, and aggregate families, including the
  additional official value-property projection. No production changes are needed.
- Focused batch, override audit, and two direct projection controls: Passed: 1275, Failed: 0,
  Total: 1323, with 48 upstream skips or explicitly reported upstream no-op arms.
- All four method inventories match the official classes. Their 1320 behavior cases correspond
  to 1314 official cases after excluding SQL-baseline checks; six additional reported cases come
  from enumerating data on upstream skipped theories. The other three cases are the audit and
  direct controls. The official aggregate `Distinct_Scalar` CI exclusion adds two skips on GitHub.
- The copied SQL-injection scenario retains the common base's input and result assertions;
  no golden SQL strings are copied. Driver failures preserve their remote exception type.
- One projection refuses translation in both execution modes. Independent direct Cosmos queries
  verify both refusals. Its synchronous assertion is stronger than upstream's empty arm; the
  adopted method does not demand the wrong synchronous-I/O error.

The remaining Northwind miscellaneous class and the wider official inventory still require work.
Logs and parity inventories are under `artifacts/cosmos-suite-audit/northwind-*`. The stopped full
local measurement is not reported as a passing run. Full-suite validation belongs to GitHub Actions.


## Miscellaneous Northwind batch, November 15, 2026

Adopted the pinned official miscellaneous query family, without SQL baselines. The focused
batch and override audit passed: Passed: 891, Failed: 0, Total: 944 (53 upstream skips).
The two variable-page-size tests retain the official Linux-emulator exclusion; exact-size,
point-filter, and subquery-refusal paging remain active. Concurrent-query tests use the fixture's
backend database instead of asking the database-free client to create one. The upstream
`OrderBy_multiple_queries` override calls another test; the adapter executes its matching base
method, which already asserts translation refusal.

Paging exposed a generic asynchronous-terminal gap. Registered static methods taking a query
and returning `Task<T>` can now remain on the server. Planning and execution use the awaited
result type instead of treating the task as a result row. No Cosmos type or provider-name test
was added to production code. The test fixture explicitly registers the Cosmos paging method
host, exact page/task result types, and cancellation-token type; automatic permissions remain
unchanged.

Non-cancellable token constants use the existing value-type construction expression representation,
so serialization does not inspect their runtime wait handles. Boxed tokens retain their declared
expression type. Request cancellation still travels through the transport. No expression node
or wire field was added. Focused shared splitter/serializer tests passed: Passed: 71, Failed: 0,
Total: 71. Focused transport cancellation and compatibility checks passed: Passed: 47, Failed: 0,
Total: 47. GitHub supplies complete normal and slow validation under the owner's validation policy.

These changes concern query execution and transport, rather than Cosmos storage semantics.
They should apply to any provider exposing an equivalent registered asynchronous terminal,
including a future MongoDB provider. That prediction is not MongoDB validation. Existing document
repair changes remain provider-neutral; no new document-write repair was introduced in this batch.
Full official Cosmos adoption remains incomplete.


## Concurrency-detector batch, November 15, 2026

Adopted the official enabled and disabled concurrency-detector families through InfoCarrier.
Each uses a separate fixture and backend database. Disabled checks are configured on both client
and backend. The enabled family retains upstream's six synchronous no-op cases as reported skips;
both families retain its Any skip for EF Core issue 17246. Enabled SaveChanges still checks both
execution modes, as upstream does. Disabled-mode tests execute the store's normal operations
while the client detector is held, including insert/read/delete cleanup.

Focused tests and override audit: Passed: 23, Failed: 0, Total: 33 (10 upstream skips).
The two explicitly skipped theories enumerate inherited data, unlike upstream's collapsed discovery.
No production changes were needed for this batch.


Reviewed exclusion, November 15, 2026: the entire official `NumberProjectionCosmosTest`
requires `IsNotLinuxEmulator`. Its class-level condition in `Query/CosmosNumberProjectionTest.cs`,
lines 6-9, cites emulator issue 335. Its 27 discovered cases are outside this Linux-emulator
scope; this is not a translation gap hidden by a local override.


## Materialization batch, November 15, 2026

Adopted all nine behaviors from the pinned official `CosmosMaterializerTest`: shadow composite
keys, discriminator inheritance, populated and empty owned collections, ordinal collection keys,
converted primary keys, and three undefined-projection cases. The per-test model harness runs each
context through InfoCarrier. The upstream class has no reusable specification base, so its test
bodies and models are adapted directly, with test cancellation and standard assertion style.
No SQL baselines or production changes were added.

Focused materialization tests and override audit passed: Passed: 10, Failed: 0, Total: 10.
The final compilation reported no warnings or errors. Undefined projections retain upstream's
exact exception message, including its successful coalesced-projection control.


## Concurrent database cleanup, November 15, 2026

GitHub run 43656286661 reported Passed: 3705, Failed: 0, Total: 4031, but the Cosmos worker
correctly failed on an `IncompleteDocumentTest` class-cleanup error. The uploaded report records
a `TestPipelineException`; the emulator log records a PostgreSQL catalog deadlock (`E40P01`)
while deleting that fixture's database at the same time. The adapter did not include the inner
cleanup exception in its report, so the HTTP response details remain unavailable.

Database deletion now retries HTTP 500 at most three times, with bounded delays and diagnostics.
A final failure still propagates. Not-found remains idempotent success; other failures and
cancellation are not retried. The helper covers the standalone document fixture and association
backend cleanup, including database reset before seeding. Query and write operations never use
this administrative retry helper. Collection parallelism remains enabled.

Focused deletion-policy and incomplete-document checks passed: Passed: 14, Failed: 0, Total: 14.
This includes three concurrent rounds, each deleting eight independent databases and checking
that each is absent. Fault injection verifies transient recovery, retry exhaustion, non-retryable
errors, and cancellation. These focused checks do not establish that the original GitHub cleanup
failure was reproduced locally; complete GitHub validation remains required.


## Custom-converter batch, November 15, 2026

Adopted the pinned custom-converter family, preserving upstream fixture capabilities, mapping,
cleanup before the custom-equality test, explicit skips, and its optional-owned undefined-row
result difference. Pure behavior overrides that only asserted SQL remain inherited.

A direct-store control confirms that composition over a converted layout collection refuses
translation. Registering the layout CLR type explicitly in the fixture prevents a premature
boundary that otherwise attempted synchronous database I/O. With that registration, InfoCarrier
also refuses translation, with a tuple carrier in the diagnostic instead of the original anonymous
projection. The override records the existing documented exception-message limitation, executes
the same query, and asserts the refused projection and member accesses. No allowlist inference
or production code changed. Audit failures now print their full violation strings before the
existing assertion, so long method names cannot hide the reason behind xUnit truncation.

Focused converter cases, direct control, and override audit passed: Passed: 36, Failed: 0,
Total: 51 (15 upstream skips). Compilation used warnings as errors. Two upstream skipped theories
enumerate inherited data rather than collapsing each theory to one discovery case.


## Cleanup ownership review and retry removal, November 15, 2026

The owner requested ownership evidence before accepting the administrative retry. The earlier
cross-class sharing explanation was incorrect. Each standalone document-fixture instance gets
its own GUID database name. Each specification backend also gets a GUID database name and its
own server service provider/store index. Matching logical store names, including Northwind,
do not identify the same physical database. Nonshared-model tests create fresh backend stores;
named-policy cases generate independent names. Tests and contexts within one class fixture
intentionally use that fixture's database. No Cosmos collection or assembly fixtures share it
between classes.

The HTTP 500 retry introduced in 091b3bfc is removed. Each cleanup makes one attempt; the final
exception remains the original exception. Not-found is still idempotent success. The earlier
catalog-deadlock observation is correlation, not a confirmed explanation of the fixture exception.

Allocation and cleanup diagnostics include the physical database name, fixture/backend identity,
test class, process, and UTC time. Failures print the complete exception chain and stack. Normal
and coverage Cosmos jobs use detailed console logging, so the next fixture failure should not
be reduced to its outer pipeline exception. No production code or test parallelism changed.

Focused cleanup, ownership, incomplete-document, and named-policy checks passed: Passed: 16,
Failed: 0, Total: 16. Ownership checks inspect actual provider database identifiers for separate
fixture instances and backend stores with equal logical names. Failure injection verifies one
attempt for HTTP 500 and HTTP 403, exception identity, inner diagnostic details, and cancellation.
Three concurrent deletion rounds remain active without retries.

Reproduction should use repeated Cosmos jobs at the same commit, emulator image, and configuration.
Passing attempts only mean the timing-dependent failure was not observed; they do not establish
its cause or a fix. Complete normal/slow suites remain delegated to GitHub.


The reproduction version also restores the original document fixture's Entity Framework
`EnsureDeletedAsync` operation; the earlier retry mitigation had changed that fixture to direct
SDK deletion. Diagnostics wrap the original scope creation, deletion, and provider disposal,
so changing the cleanup path cannot silently remove the original symptom. Focused checks of
that restored path and diagnostic behavior passed: Passed: 9, Failed: 0, Total: 9.


## Default and generated keys batch, November 15, 2026

Adopted all twelve official `DefaultKeyValuesTest` behaviors across single/composite keys and
single/hierarchical partition keys. The fixture preserves the official warnings-as-errors setting
on the backend. Missing-key assertions verify the exact warning text through the preserved
server update exception and verify that the re-raised outer update exception names the client's
entity. This accommodates the existing update-entry remapping contract rather than discarding
or weakening the provider error.

One model setting must be explicit: the integer property which is both key and partition key
uses `ValueGeneratedNever`. The InfoCarrier client uses relational conventions, while Cosmos
infers non-generation for this property. The direct control independently verifies the unmodified
Cosmos convention before testing missing-key refusals and generated-GUID readback. This batch
validates matching generation metadata; it does not establish automatic propagation of a backend's
provider-specific conventions to the client. Application models must specify matching semantics.
The adapter records the existing client-convention decision on the affected test. No production
code changed.

Focused new-family tests, direct control, and audit passed: Passed: 14, Failed: 0, Total: 14.
The final direct-control and audit check passed: Passed: 2, Failed: 0, Total: 2.
Compilation used warnings as errors.

The normal Build & Test run 43659800758 for e1338382 completed successfully with retries removed
and the original cleanup path restored. Its Cosmos worker reported 3,758 passed and 341 skipped
out of 4,099 total cases. Allocation diagnostics identified 93 distinct databases with no conflicting
fixture-owner records, and no cleanup failure occurred. Evidence is retained in
`artifacts/cosmos-suite-audit/cleanup-ownership-ci-e133.json`. This does not establish the original
cleanup failure's cause. The owner chose continued batch adoption with normal GitHub monitoring,
rather than scheduling extra identical reproduction runs. Cleanup diagnostics remain active.


## Converted-key relationships and strict repair warnings, November 15, 2026

Adopted the complete 47-case `KeysWithConvertersTestBase` family with the official Cosmos fixture's
`UseInclude = false`, root discriminator mapping, and warning configuration. No behavior tests or
skips needed overrides.

Two owned-entity updates exposed a product issue: document repair appended unordered `Take(1)`
to its full-primary-key lookup. With the official warning policy, the provider refused that
internal query. The same warning was reproduced on SQLite in both synchronous and asynchronous
repair. The provider-neutral regression was strengthened to treat the warning as an error:
before the fix, Passed: 2, Failed: 2, Total: 4; after the fix, Passed: 4, Failed: 0, Total: 4.

Both repair paths now use the complete primary-key predicate without the redundant row limit.
Primary-key uniqueness already bounds a valid lookup to one root. Context, filter, transaction,
tracking isolation, cancellation, and merge logic remain in the existing paths. This correction
contains no provider-specific type or branch and also applies to other document providers.
No wire field, expression-node kind, or public signature changed.

The new converted-key family plus Cosmos incomplete-document, named-policy, and audit controls
passed: Passed: 55, Failed: 0, Total: 55. Focused transport contract and cancellation checks passed:
Passed: 42, Failed: 0, Total: 42. Complete normal suites and trim validation run on GitHub.


## Optimistic concurrency specification, November 15, 2026

Adopted the official optimistic-concurrency family with its F1 model, ETag mappings,
per-test reseeding, asynchronous-only reload behavior, and exact source-backed exclusions.
The Linux-emulator and provider skips remain explicit. The upstream fake transaction is
only a specification harness mechanism; it does not claim Cosmos transaction support.

The server model uses Cosmos conventions while the client retains InfoCarrier conventions.
The existing construction-only F1 materialization interceptor moved from the SQLite adapter
into shared test utilities. Its behavior is unchanged: server construction supplies the model's
required proxies, while the caller's observable name transformation executes on the client.
No production code changed.

The fixture preserves the official single metadata-probe retry after container recreation.
It logs the complete first exception and lets a second failure propagate. This is not a database
cleanup retry. Database deletion remains a single attempt with ownership and exception diagnostics.

Focused Cosmos family and override audit: Passed: 15, Failed: 0, Total: 46, with 31 upstream skips.
The affected SQLite family: Passed: 34, Failed: 0, Total: 46, with 12 existing skips.
Both checks compiled in Release with warnings as errors. Full suites remain delegated to GitHub.
The preceding converted-key commit d25062c9 passed both Build & Test and Coverage workflows.

Testcontainers remains a follow-up after complete adoption. Evaluate keeping isolated databases
until suite completion and removing their container afterward, rather than concurrent final deletion.
This might avoid cleanup failures but does not establish the original failure's cause.


## Value converter round trips, November 15, 2026

Adopted all official value-converter end-to-end cases through InfoCarrier, preserving the four
DateTimeOffset model exclusions from the official fixture. No production change or test override
was needed. Focused family and audit: Passed: 28, Failed: 0, Total: 28.


## Additional applicability review, November 15, 2026

These exclusions concern the exact official assertions, not general claims that the corresponding
application capability is unsupported. The source remains pinned to the adoption-checklist revision.

| Official class | Reviewed reason |
|---|---|
| `ConfigPatternsCosmosTest` | Direct UseCosmos region/connection-mode validation and CosmosClient identity/thread-safety. These configure the backend provider; the InfoCarrier client has no CosmosClient or region. |
| `CosmosModelBuilderGenericTest+CosmosGenericComplexCollection` | Complex-collection model metadata and convention validation. Uses GenericTestModelBuilder with CosmosModelBuilderFixture, finalizes a model, and inspects it. There is no application query or save crossing the wire. |
| `CosmosModelBuilderGenericTest+CosmosGenericComplexType` | Complex-type model metadata and convention validation. Uses GenericTestModelBuilder with CosmosModelBuilderFixture, finalizes a model, and inspects it. There is no application query or save crossing the wire. |
| `CosmosModelBuilderGenericTest+CosmosGenericInheritance` | Model inheritance metadata, discriminator mappings, and convention validation. Uses GenericTestModelBuilder with CosmosModelBuilderFixture, finalizes a model, and inspects it. There is no application query or save crossing the wire. |
| `CosmosModelBuilderGenericTest+CosmosGenericManyToMany` | Many-to-many model metadata and relationship conventions. Uses GenericTestModelBuilder with CosmosModelBuilderFixture, finalizes a model, and inspects it. There is no application query or save crossing the wire. |
| `CosmosModelBuilderGenericTest+CosmosGenericManyToOne` | Many-to-one model metadata and relationship conventions. Uses GenericTestModelBuilder with CosmosModelBuilderFixture, finalizes a model, and inspects it. There is no application query or save crossing the wire. |
| `CosmosModelBuilderGenericTest+CosmosGenericNonRelationship` | Scalar/collection model metadata and Cosmos convention validation. Uses GenericTestModelBuilder with CosmosModelBuilderFixture, finalizes a model, and inspects it. There is no application query or save crossing the wire. |
| `CosmosModelBuilderGenericTest+CosmosGenericOneToMany` | One-to-many model metadata and relationship conventions. Uses GenericTestModelBuilder with CosmosModelBuilderFixture, finalizes a model, and inspects it. There is no application query or save crossing the wire. |
| `CosmosModelBuilderGenericTest+CosmosGenericOneToOne` | One-to-one model metadata and relationship conventions. Uses GenericTestModelBuilder with CosmosModelBuilderFixture, finalizes a model, and inspects it. There is no application query or save crossing the wire. |
| `CosmosModelBuilderGenericTest+CosmosGenericOwnedTypes` | Owned-type model metadata and ownership conventions. Uses GenericTestModelBuilder with CosmosModelBuilderFixture, finalizes a model, and inspects it. There is no application query or save crossing the wire. |
| `OverzealousInitializationCosmosTest` | The only official behavior override is explicitly skipped for EF Core issue 17246. No active Cosmos behavior case remains to execute. |
| `QueryLoggingCosmosTest` | Asserts Cosmos query-pipeline logging positions, Cosmos QueryingEnumerable text, sensitive SQL parameter text, and provider diagnostic messages. These exact backend logs are not the InfoCarrier client query pipeline. |
| `QueryLoggingCosmosTestSensitive` | Sensitive counterpart of the same Cosmos internal query/SQL logging assertions. This does not exclude application query or log-forwarding coverage. |
| `ReloadTest` | The entire official behavior method unconditionally calls SkipOnLinuxEmulator for emulator issue 335. Outside the selected Linux-emulator scope. |
| `CompiledModelCosmosTest` | Cosmos design-time compiled-model code generation, generated assembly/metadata assertions, and Cosmos-specific value-generator metadata. The backend owns its compiled model; this does not test a transported application query. |
| `CosmosDatabaseCreatorTest` | Direct Cosmos IDatabaseCreator creation/deletion return values and seeder validation. Database administration is backend setup, not an InfoCarrier client operation. |

The model-builder classes use `ModelBuilding/CosmosModelBuilderGenericTest.cs`; their shared
fixture selects `CosmosTestHelpers` for model construction, not a remote client. Compiled-model
assertions are in `Scaffolding/CompiledModelCosmosTest.cs`. Database-creator assertions are in
`Storage/CosmosDatabaseCreatorTest.cs`. Configuration assertions are in `ConfigPatternsCosmosTest.cs`.
Logging assertions are in `Query/QueryLoggingCosmosTestBase.cs`, shared by both logging classes.
The complete Linux skip is in `ReloadTest.cs`; the eager-reference skip is in
`OverzealousInitializationCosmosTest.cs`. Application-level concurrency, query, update, failure,
and interception tests remain separately subject to adoption.


## Complex-type queries and synchronous refusals, November 15, 2026

Adopted both official complex-type query families, including renamed JSON properties and the two
Cosmos-specific complex-key scenarios. Fixtures use the official model types explicitly because
this project also defines an unrelated Customer model. Official group-by and Linux structural-equality
skips remain source-backed. Every active assertion matches the official behavior, including exact
translation details. No new exception-message deviation or production change was required.

The batch also adopts all twenty Cosmos synchronous query refusal tests through InfoCarrier.
Focused families and override audit: Passed: 321, Failed: 0, Total: 347, with 26 official skips.
Compilation used Release warnings as errors. Full normal and coverage validation runs on GitHub.

`JsonTypesCosmosTest` is separately excluded from remote behavior adoption. Its shared
`JsonTypesTestBase.Can_read_and_write_JSON_value` builds model metadata and directly exercises
`JsonValueReaderWriter`, `Utf8JsonWriter`, element comparers, and facets. It neither saves a
record nor executes a database query. These provider JSON codec assertions do not test the
InfoCarrier wire codec. This does not exclude the separate database-backed `JsonQueryCosmosTest`.


## Inheritance queries, November 15, 2026

Adopted the complete official inheritance query family with Cosmos containers, disabled generated
keys and complex types, asynchronous behavior, and source-backed skips. No production changes.
Focused family and audit: Passed: 85, Failed: 0, Total: 94, with nine official skips.


## Identity-resolution projections, November 15, 2026

Adopted the application-level identity-resolution projection cases and all six non-shared-model
cases. Focused tests and audit: Passed: 3, Failed: 0, Total: 9, with six official skips. No product
change was needed. Five methods in the outer official class are excluded individually:
`Owned_reference_without_owner_key_throws`, `Owned_collection_without_owner_key_throws`,
`SelectMany_owned_collection_throws`, `SelectMany_owned_collection_with_owner_key_throws`, and
`Root_entity_projection_with_owned_reference_without_owner_key_throws`. Each directly constructs
Cosmos translation preprocessor/translator/postprocessor services and asserts their ShapedQueryExpression
or compiler diagnostics without executing the query. They are not application execution tests.
This exclusion does not apply to queries which actually execute through InfoCarrier.


## Owned queries, November 15, 2026

Adopted the complete official owned-query family with its containers, indexers, seed data,
embedded discriminator name, translation refusals, and Linux-emulator exclusions. Official empty
synchronous arms are explicit runtime skips. Cosmos SDK failures retain their remote type and
BadRequest status text through the existing server-exception envelope. The optional-owned scalar
NullReferenceException remains classified as a crash, not a limitation.

Focused family and audit: Passed: 172, Failed: 0, Total: 195, with 23 source-backed skips.
One upstream override calls an unrelated sibling: Query_loads_reference_nav_automatically_in_projection
now executes its own Fink/Barton base scenario and asserts the matching cross-document refusal.
The focused corrected scenario and audit passed: Passed: 3, Failed: 0, Total: 3.
No production code or wire format changed.


## Partition-key application round trips, November 15, 2026

Adopted all three single-level and all three hierarchical partition-key scenarios. They cover
insert, implicit and explicit partition queries, ordered reads, updates, key immutability, and
deletions. The fixture grants CosmosQueryableExtensions on both wire halves; database creation
remains backend fixture setup. No production change or query-execution bypass was needed.
Focused families and audit: Passed: 7, Failed: 0, Total: 7.

The separate CosmosTriggersTest has one behavior method, conditionally excluded for the Linux
emulator because server-side scripts are unsupported. The selected emulator scope excludes that
entire class, following its exact official condition. This is an application coverage gap, not
an internal-provider classification. See CosmosTriggersTest.cs at the pinned revision.


## Database-backed JSON queries, November 15, 2026

Adopted all official JsonQueryCosmosTest behavior cases through InfoCarrier, preserving the
provider's mapping, custom result comparers, translation refusals, explicit issue skips, Linux
projection skips, and upstream CI exclusions. The fixture constructs malformed document inputs
through the backend SDK; application reads still execute through InfoCarrier. Primary_key_baseline
now actually selects tracking or nontracking according to its existing theory parameter.
The independent document JSON inspection uses the backend context, not a client Cosmos facade.

Focused complete family and override audit: Passed: 405, Failed: 0, Total: 481, with 76 official
skips on Windows/Podman. The local family took 10 minutes 36 seconds. This was the new batch only;
full normal and coverage suites remain GitHub work. NotImplementedException cases retain the
source issue classification and SDK query errors retain the explicit remote exception type.
No product code, public signature, or wire format changed.

## Emulator diagnostics and concurrency isolation, November 20, 2026

The stopped-machine check confirmed that test failures identify the emulator endpoint and launcher
commands. The message now also names `podman machine start`. The launcher checks runtime access
before container operations and gives a direct machine-start instruction when Podman is unavailable.
A deliberately unreachable Podman endpoint confirmed the launcher message without stopping the machine.

Run [43697841012](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43697841012) failed during
F1 fixture reseeding: the second seed probe received BadRequest with a missing PostgreSQL relation.
The failure occurred before the concurrency test body. Its exact cause remains unconfirmed; later
green runs do not explain it. No evidence established cross-class database sharing.

Each concurrency case now receives a new database, service provider, and SDK client. The existing
fixture and externally owned logger remain stable for the upstream constructor. This removes the
repeated delete/recreate cycle with a reused client. No cleanup retry or parallelism reduction was
added. An integration regression checks distinct database names, survival after another fixture's
disposal, and fresh seeded data after reseeding.

Focused concurrency, isolation, availability, and override checks: Passed: 22, Failed: 0, Total: 53,
with 31 existing source-backed skips. Full-suite validation remains GitHub work. Production code and
wire format are unchanged. The missing-table cause is not claimed as proven.

## Interception families, November 20, 2026

Adopted the official diagnostic query-expression interception and materialization interception
families. Model configuration reaches both halves; application interceptors remain client-side,
matching the existing SQLite harness. Embedded settings ownership is explicit because the client
uses relational conventions. Official issue exclusions and synchronous-I/O refusals are retained.
Focused batch: Passed: 16, Failed: 0, Total: 24, with eight source-backed skips.
No production code or wire behavior changed.
