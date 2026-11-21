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
| Emulator | Official Linux vNext `vnext-latest`, selected as in EF Core's test environment |
| Container module | `Testcontainers.CosmosDb` 4.17.0, using `CosmosDbBuilder` defaults |
| Framework reference | EF Core 11.0.0, commit `9e685c7a52d1a344d133a3d370e458db97417b3f` |
| Local Windows | Windows `dotnet test` process connecting to Podman 6.1.3 |
| Linux | Linux test process connecting to the same image; Docker in GitHub Actions |

This follows EF Core's Linux-emulator test path, including its known emulator skips, on both hosts.
It does not reproduce their separate native Windows emulator path. Testcontainers 4.17.0 starts
one fresh container for each test assembly execution and removes it after all fixtures finish.
The assembly fixture preserves class parallelism. The image tag can change between runs.
See the pinned [upstream environment](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/test/EFCore.Cosmos.FunctionalTests/TestUtilities/CosmosTestEnvironment.cs)
and [test jobs](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/eng/helix.proj).

On this Windows machine:

```powershell
podman machine start infocarrier-cosmos
dotnet test test/InfoCarrier.Core.CosmosTests/InfoCarrier.Core.CosmosTests.csproj --configuration Release
```

Docker Desktop with Linux containers uses the same test command. Testcontainers discovers the
container API; it does not invoke a hardcoded docker or podman executable. Windows/Podman has
been exercised without additional environment settings. Windows/Docker Desktop has not been
validated on this machine. Linux/Docker validation runs on GitHub.

The module assigns a dynamic port and uses its standard startup-log readiness check. The managed
endpoint uses HTTP, as in the official EF setup. A three-minute overall startup deadline remains.
The assembly fixture records container identity and captures emulator output under
`artifacts/cosmos-emulator/<process>/`, including startup-failure diagnostics. Both GitHub workflows
upload those logs. Testcontainers' resource reaper remains enabled; no cleanup suppression or
extra runtime settings are introduced for Podman. The earlier PostgreSQL package exclusion has
been removed; the managed container receives no custom package configuration.

An explicit `INFOCARRIER_COSMOS_ENDPOINT` opts into an external emulator. That mode retains the
manual `eng/CosmosEmulator.cs -- start|stop podman|docker` command and loopback HTTPS restriction.
The fixture never removes an externally supplied container. Missing-container errors distinguish
automatic startup from this external mode and name `podman machine start`. A received HTTP
response leaves authentication and service validation to the provider. Cancellation stays cancellation.

Each fixture still owns a generated database. Explicit cleaning and database deletion remain
observable operations; Testcontainers does not prove that the earlier missing-table or PostgreSQL
file errors are resolved. Stability review remains open; the November 21 evidence below records
failures after lifecycle migration.

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

- [x] Review every remaining official class and record a specific applicability decision.
- [x] Adopt applicable Northwind, association, complex/JSON, primitive-collection, and other query tests.
- [x] Adopt remaining scalar translations, full-text/vector behavior, and application-level extension tests.
- [x] Adopt applicable tracking, keys, conversions, concurrency, updates, and failure tests.
- [x] Review direct Cosmos service, model-building, design-time, logging, session, and transaction tests
  individually. Provider-internal assertions may be excluded; ordinary application failures may not.
- [ ] Validate the complete expanded suite on Windows/Podman and Linux/Docker, including coverage.

Do not count missing families as unsupported tests or describe these expansions as the complete
official suite. The dated results below identify their tested scope.

## CI parallelization and remaining follow-up

The owner moved CI parallelization ahead of the next adoption batch. The earlier plan placed
Testcontainers after adoption; on November 20 its implementation moved earlier to give batches fresh containers.

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
The parallelism-only change did not implement Testcontainers. The November 20 lifecycle change does.

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

The subsequently reverted implementation retried HTTP 500 at most three times, with bounded delays and diagnostics.
A final failure still propagates. Not-found remains idempotent success; other failures and
cancellation are not retried. That helper covered the standalone document fixture and association
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

At initial adoption, the fixture preserved the official metadata-probe retry after container recreation.
That retry logged the first exception and propagated a second failure. It was removed on November 20. This is not a database
cleanup retry. Database deletion remains a single attempt with ownership and exception diagnostics.

Focused Cosmos family and override audit: Passed: 15, Failed: 0, Total: 46, with 31 upstream skips.
The affected SQLite family: Passed: 34, Failed: 0, Total: 46, with 12 existing skips.
Both checks compiled in Release with warnings as errors. Full suites remain delegated to GitHub.
The preceding converted-key commit d25062c9 passed both Build & Test and Coverage workflows.

The earlier plan proposed Testcontainers after adoption and considered deferring database deletion.
The November 20 migration instead keeps explicit deletion visible and removes the whole container
after each assembly run. It does not claim that the original cleanup cause is established.


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

## Generated document-owner values, November 20, 2026

The official CosmosConcurrencyTest exposed a general acceptance defect: when only an owned member
changed, InfoCarrier sent the unchanged owner and returned its generated revision, but EF's save
list omitted that owner. Its generated-value sidecar therefore did not reach the entity or the
next original-value snapshot. Direct Cosmos controls passed while all six remoted nested-update
cases failed before correction.

The client now includes expanded unchanged entries in EF's acceptance list. An unchanged entry
which receives a generated value also remains in the changed set until acceptance, covering
SaveChanges(false) followed by ChangeTracker.AcceptAllChanges(). The serialized request retains
its original states. No Cosmos provider names, annotations, or entity-tag special cases occur in
this change. Existing correlation identifiers and generated-value response fields are unchanged.

A provider-neutral InMemory regression uses generated owner revisions and independent store reads.
All four synchronous/asynchronous and immediate/deferred acceptance combinations fail on the old
production code. With the correction, those four cases and the four existing provider-neutral
repair cases pass: Passed: 8, Failed: 0, Total: 8. This establishes behavior beyond Cosmos; it does
not establish compatibility with the disabled MongoDB provider. Full active suites remain CI work.


## Additional applicability review, November 20, 2026

At the pinned EF revision, AdHocFullTextSearchCosmosTest's composite-index, non-string-property,
and enable/disable cases assert model validation errors. Its collection-navigation case asserts
schema-creation rejection. The remaining four language-configuration cases require
IsNotLinuxEmulator and cite emulator issue 162. None executes an application query or write.
AdHocVectorSearchCosmosTest similarly contains only composite-index model validation and
collection-navigation schema-creation rejection. These ten cases are reviewed exclusions, with
the four Linux exclusions distinguished from the six provider model/schema checks.
Sources: AddHocFullTextSearchCosmosTest.cs and AdHocVectorSearchCosmosTest.cs in the pinned
EFCore.Cosmos.FunctionalTests tree. Runtime query/write families remain subject to adoption.

Final focused Cosmos concurrency batch and override audit: Passed: 22, Failed: 0, Total: 22.
This includes nine official cases, six matching direct-store controls, six deferred-acceptance
controls, and the override audit. Token comparisons against independent reads retain the official
quote normalization. Focused protocol compatibility and protocol-3 execution: Passed: 22,
Failed: 0, Total: 22. Existing sample trimming diagnostics appeared during that build.

## Automatic container lifecycle and bulk concurrency, November 20, 2026

Testcontainers 4.17.0 now owns one fresh pinned Linux emulator per assembly execution. Random
loopback ports avoid collisions with manually running emulators. Readiness is bounded, normal
teardown captures logs before removal, and startup failures preserve the underlying exception.
The resource reaper remains enabled. Local Windows/Podman required no Docker executable or
additional environment settings. The initial availability controls passed: Passed: 6, Failed: 0,
Total: 6. The managed emulator and resource-reaper containers were absent after process exit.

The bulk-concurrency adapter inherits all nine official entity-tag cases, plus the direct-store
and deferred-acceptance controls. Bulk execution is configured on the actual backend. Transaction
mode Never is configured explicitly on both sides; this is fixture configuration, not a claim
that client transaction mode crosses the wire. Focused batch and override audit on a fresh
automatic container: Passed: 22, Failed: 0, Total: 22.

The previous bulk attempt against the long-running manual container failed 21 cases with emulator
PostgreSQL E58P01 errors. That failure is retained as infrastructure evidence, not classified as
an InfoCarrier failure or silently excluded. The fresh-container result does not establish that
all emulator cleanup races are solved. Per the owner's direction, revisit stability after the
lifecycle migration rather than repeatedly running identical suites.

The unavailable-runtime probe forced an unreachable named-pipe endpoint and produced the intended
startup failure: "Cannot start the Cosmos test container. Start Docker with Linux containers, or
run 'podman machine start'." The original connection exception is retained. Setting DOCKER_HOST
alone was insufficient to force failure because Testcontainers discovered the available default
runtime; no false unavailable-runtime result is claimed from that successful discovery check.

An intentional one-test failure also removed the managed emulator and preserved its logs. That
temporary probe was removed immediately afterward and is not part of the suite. The expected
negative controls are recorded separately from green behavior-batch counts.

## Setup simplification after Testcontainers, November 20, 2026

The administrative HTTP-500 cleanup retry was already removed before the migration. The remaining
upstream F1 metadata-probe retry is now removed too: that workaround addressed database recreation
through a reused client, whereas this fixture now uses a fresh database and client per case.
Managed runs also omit per-fixture reachability probes because the assembly fixture already awaits
container readiness. Explicit external endpoints retain the actionable diagnostic check.

Per-case database isolation and ownership/error logs remain intentional. Explicit database cleanup
still makes one attempt and propagates failures. No cleanup serialization, backoff, query-parser
bypass, fixture-wide parallelism reduction, or suppressed store errors was introduced. The manual
launcher remains only for the explicit external-endpoint mode.

The next official ad-hoc query family passed through InfoCarrier: Passed: 20, Failed: 0, Total: 20,
including the override audit. It covers missing scalar fields, enum partition keys/discriminators,
nullable aggregates, null-converting conditions, and quoted scalar/key/partition values. Malformed
JSON creation is backend-only setup; subsequent application reads cross InfoCarrier. SQL baselines
are omitted. No production changes were required.

After removing the seed retry and managed per-fixture probes, the former failing concurrency
case, fixture-isolation regression, and availability controls passed: Passed: 8, Failed: 0,
Total: 8. Full validation remains on GitHub.

## Read-item and partition-key query families, November 20, 2026

Adopted all four official variants: no discriminator in IDs, concrete discriminator in IDs,
root discriminator in IDs, and the non-inheritance model. Shared application bases, complete
models, expected data, sorters, and result comparers are retained. SQL-only overrides and the four
SQL-override-completeness guards are omitted. Twenty-one exact Linux-emulator issue-329 cases
remain explicit source-backed skips; no application assertion is replaced with a baseline check.

Focused complete batch and override audit: Passed: 307, Failed: 0, Total: 328, with 21 official
skips. It exercises composite/hierarchical and single partition keys, special characters,
conflicting predicates, inheritance discriminator variants, tracking modes, and missing items.
The client allows Cosmos query extensions explicitly on both wire halves. No production changes
or extra emulator environment settings were required.


## End-to-end application scenarios and dictionary interfaces, November 20, 2026

Adopted the complete official EndToEndCosmosTest family, retaining its two issue-33402 skips.
Provider-specific shadow-ID assertions receive the exact backend model explicitly on the client.
All application reads and writes still cross InfoCarrier. TransactionalBatch settings are explicit
backend fixture configuration; logger assertions inspect the actual backend logger rather than
claiming Cosmos logs originate on the client. Redundant EnsureCreated setup remains in the backend
harness. The pinned custom JSON-ID factory and definition are retained for its key-generator case.

The first complete family and audit reported Passed: 33, Failed: 6, Total: 41, with two official
skips. Two failures were missing CosmosQueryableExtensions registrations in this new fixture;
the partition hint was consequently evaluated on the client. That fixture now explicitly grants
the extension type on both halves. Four failures exposed dictionary-interface reconstruction:
a list of KeyValuePair values did not implement IDictionary or IReadOnlyDictionary.

The general value mapper now reconstructs a compatible Dictionary for those interfaces. This
contains no Cosmos-specific type, annotation, or branch. Four provider-neutral round-trip cases
fail on the old implementation and pass with the correction, including nullable values, nested
dictionaries, and shared references. They use the production serializer. Together with existing
expression round trips: Passed: 13, Failed: 0, Total: 13. Focused protocol compatibility/execution:
Passed: 22, Failed: 0, Total: 22. No wire fields or public signatures changed.

Only the six failed application cases and audit were rerun locally after correction:
Passed: 7, Failed: 0, Total: 7. The earlier 33 passing cases are left to full GitHub validation;
this is not presented as a second complete local-family run. Normal, slow, and coverage gates
remain GitHub work. MongoDB remains disabled and was not exercised by this correction.

The end-to-end fixture also preserves the source's context-pooling option. Focused pooled
insert/read/update/delete and audit checks passed: Passed: 3, Failed: 0, Total: 3.

## Bulk end-to-end variants, November 20, 2026

Adopted CosmosBulkEndToEndTest and CosmosBulkEndToEndTestNoBatching through the same application
scenarios. Bulk execution is applied to actual backend contexts. The first variant retains the
source's bulk/transaction warning policy. The second uses Never on both sides and retains its
exact true-arm exclusion. Context pooling follows the source option; test parallelism is unchanged.
Focused complete batch and audit: Passed: 63, Failed: 0, Total: 81, with 18 official skips.
No additional production change was needed.


## Raw Cosmos queries and registered root adapters, November 21, 2026

The complete official FromSqlQueryCosmosTest family now crosses InfoCarrier, including its
SQL inputs and parameter cases. Golden SQL baselines remain omitted. The two Linux-emulator
issue-335 projection exclusions retain exact upstream references. The fixture registers an
adapter for the official Cosmos query-root type through the existing public service seam,
and explicitly grants arbitrary SQL execution on the backend.

QueryExecutor previously ignored a registered root adapter and always selected the default
relational adapter. It now resolves that existing service, retaining the default fallback.
Two provider-neutral InMemory regressions fail before this change and pass afterward, covering
synchronous and asynchronous execution through the production serializer. No Cosmos type or
wire field was added to production code. MongoDB remains disabled and was not tested.

Focused raw-query family and audit: Passed: 49, Failed: 0, Total: 51, with two official skips.
Final registered-service regressions: Passed: 2, Failed: 0, Total: 2. Focused transport service
replacement and protocol controls: Passed: 52, Failed: 0, Total: 52.

## GitHub validation review, November 21, 2026

Commit 86383505 passed every job in both [Build & Test](https://github.com/azabluda/InfoCarrier.Core/actions/runs/44819904048)
and [Coverage](https://github.com/azabluda/InfoCarrier.Core/actions/runs/44819904027).
The preceding [Build & Test run](https://github.com/azabluda/InfoCarrier.Core/actions/runs/44818602181)
at 27d752f1 failed two ComplexTypesTrackingTest cases during SaveChanges: changing a deleted
complex-field-record collection to Modified, and tracking complex readonly structs as Added.
The emulator reported missing PostgreSQL document relations. These failures happened after
Testcontainers adoption. The later successful run does not establish their cause or resolution.
Keep the lifecycle and database-owner diagnostics; continue adoption while monitoring subsequent
runs, as directed by the owner, rather than repeatedly launching identical suites.


## Embedded documents and generated collection keys, November 21, 2026

Adopted the complete EmbeddedDocumentsTest family with its two official skips. Each test owns
one database. Mapping variants within Old_still_works intentionally share that test's documents;
other tests and classes cannot delete its database. Application operations use the production
serializer and InfoCarrier transport. Explicit backend models retain the official ownership mappings.

The first complete family and audit reported Passed: 15, Failed: 2, Total: 19, with two official
skips. Both collection-manipulation variants exposed a client identity-map conflict after a
successful store write. Diagnosis found three related losses: generated existing keys were
suppressed, returned key exchanges were applied without temporary values, and reconstructed
collections did not retain application order. The server also deferred new entries on their
own generated keys, rather than only on actual foreign-key dependencies.

The client now sends document members in current navigation order and prepares changed generated
keys with the registered framework temporary-value generator. The server waits on foreign keys
and returns existing generated keys only when their values actually changed during the save.
These changes use ownership metadata, key comparers, and framework generators; they contain no
Cosmos types, annotations, property names, or provider-name checks. Relational request ordering
is unchanged. MongoDB remains disabled; applicability to MongoDB is architectural inference,
not execution evidence. The existing blanket key-suppression workaround is removed.

Four serializer-backed InMemory key-exchange regressions fail with the original client application
logic and pass with the correction, covering both execution modes and deferred acceptance.
Two additional cases inspect actual backend collection reconstruction after client tracking order
differs from navigation order. InMemory does not persist an ordinal, so those cases do not claim
persistent collection ordering. Independent Cosmos reads retain the official order assertions.
Focused generated-owner, key, order, and repair controls: Passed: 14, Failed: 0, Total: 14.
The failed collection variants, their matching existing control, and audit then passed:
Passed: 4, Failed: 0, Total: 4. Earlier passing family cases are left to full GitHub validation.
Focused protocol, historical execution, and wire-guard controls: Passed: 52, Failed: 0, Total: 52.

A stopped Podman machine produced the explicit startup diagnostic before any behavior case ran.
The local machine is named infocarrier-cosmos, so starting it required that name; the command
without a name looked for a nonexistent default machine. This is startup evidence, separate from
application failure counts, and identifies a remaining diagnostic refinement.


## Official bulk-execution behavior, November 21, 2026

Adopted all six CosmosBulkExecutionTest cases through InfoCarrier. Bulk execution, transaction
mode, session-token setup, and trigger mappings are actual backend fixture configuration. The
mixed-partition case checks the backend's two single writes and two transactional batches.
The remaining cases preserve successful 100-item insertion and the exact bulk/transaction,
bulk/session, and bulk/trigger refusals.

The initial complete family and audit reported Passed: 5, Failed: 2, Total: 7. Both failures
were direct-store inner-exception assertions encountering the client-entry exception wrapper.
The assertions now inspect the reconstructed server DbUpdateException and its exact original
InvalidOperationException cause, with source-backed mechanical deviations. No failure is skipped
and no production code changed. Focused failed cases and audit: Passed: 3, Failed: 0, Total: 3.
The earlier passing cases are left to GitHub's full suite.


## Transactional batches, November 21, 2026

Adopted the complete CosmosTransactionalBatchTest family through InfoCarrier. Tests own separate
databases and use actual backend transaction modes, execution strategies, concurrency suppression,
trigger mappings, and command logs. The five official Linux exclusions retain their exact source
references and reasons. Their disabled trigger-admin setup is not presented as an application test.

The complete batch and audit reported Passed: 15, Failed: 1, Total: 21, with five official skips.
The single failure was the oversized-document input exceeding the default 64 MiB transport request
limit after envelope encoding. That one test now explicitly permits 128 MiB so the unchanged
50-million-character source input reaches Cosmos. Production defaults and other tests retain their
limits. The provider refusal and independent read verify that the smaller preceding document was
saved. Focused oversized-input case and audit: Passed: 2, Failed: 0, Total: 2.

The earlier passing cases cover same-partition atomicity, partial progress across partitions and
nontransactional writes, 100/101-operation behavior, cross-partition Always refusals, trigger
atomicity refusal, execution-strategy propagation, suppressed concurrency, successful large batches,
and create/update/delete backend logging. No production correction was needed for this batch.


## Session-token application coverage, November 21, 2026

Adopted the applicable application cases from CosmosSessionTokensTest and its nonshared class.
Reads, shaped queries, read-item queries, paging, writes, conflict handling, and not-found behavior
cross InfoCarrier. The fixtures configure the actual backend token-storage interface. Each test
owns its database and recording storage; each logical context in the nonshared scenarios has
separate real provider token storage. There is no shared static recording field. This explicit
backend setup does not add a Cosmos session facade or token-management protocol to the client.

The first outer family and audit reported Passed: 30, Failed: 6, Total: 36. Successful paging
needed explicit grants for the page, task, and cancellation-token types on both halves. The
existing generic asynchronous-terminal path then executed it on Cosmos. An independent populated
three-document paging test compares client and direct-store results, one-item page limits,
continuation completion, and no-tracking behavior: Passed: 2, Failed: 0, Total: 2, including audit.

The other outer failures exposed the expected incomplete-document repair read before a default
container update. Those cases now explicitly assert the additional read's nonempty token and
retain every write-token assertion. An invalid token refuses that repair read before reaching
the write pipeline, so that exact default-container case asserts the transmitted Cosmos type and
message with an ADR-009 RefusedEarlier reason. Other-container updates retain the original
DbUpdateException path. Repair-read tokens are not assumed equal to preceding write tokens.

The nonshared family, successful paging, and audit initially reported Passed: 9, Failed: 1,
Total: 11, with one official issue-331 theory skip. Its conflict failure was a fixture mistake:
transaction mode Always had also reached a second context that upstream leaves WhenNeeded.
Explicit per-logical-context backend configuration fixes this without changing the expectation.
The combined correction subset reported Passed: 10, Failed: 2, Total: 12, with only the unsupported
repair-token equality assumptions remaining. Their focused correction and audit then reported
Passed: 4, Failed: 0, Total: 4. Negative paging, availability diagnostics, and audit finally passed:
Passed: 8, Failed: 0, Total: 8. No production change was needed for session-token adoption.

Individually reviewed exclusions from the same pinned source:

| Method | Why it is outside application-through-InfoCarrier scope |
|---|---|
| AppendSessionToken_uses_AppendDefaultContainerSessionToken | Asserts direct Cosmos DatabaseFacade forwarding to a recording service; no query or write. |
| AppendSessionTokens_uses_AppendSessionTokens | Asserts direct Cosmos facade dictionary forwarding; no query or write. |
| UseSessionToken_uses_SetDefaultContainerSessionToken | Asserts direct Cosmos facade forwarding; no query or write. |
| UseSessionTokens_uses_SetSessionTokens | Asserts direct Cosmos facade forwarding; no query or write. |
| GetSessionTokens_uses_GetTrackedSessionTokens | Asserts the Cosmos facade returns the provider service dictionary; no query or write. |
| Different_contexts_do_not_share_DefaultContainer_name | Compares provider context configuration and resolved default-container service metadata. |
| New_context_does_not_use_same_SessionTokenStorage | Asserts identity of directly resolved CosmosDatabaseWrapper services. |
| Pooled_context_uses_same_SessionTokenStorage | Asserts reuse of the same Cosmos context and provider service instance. |
| Pooled_context_clears_SessionTokenStorage | Asserts a direct provider pool reset calls its recording storage service. |

These exclusions do not exclude application reads, writes, or failures. Their source is
[CosmosSessionTokensTest.cs](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/test/EFCore.Cosmos.FunctionalTests/CosmosSessionTokensTest.cs).
The official non-Linux concurrency-token theory remains a referenced Linux exclusion, not a new
InfoCarrier skip. New application code has no ignored errors or reduced collection parallelism.

## Emulator failure evidence and internal logs, November 21, 2026

The [transaction-batch build](https://github.com/azabluda/InfoCarrier.Core/actions/runs/44885262305)
failed 302 Cosmos cases. Its earliest reported failure was database deletion returning PostgreSQL
E58P01. The same error then affected unrelated databases and classes. The captured gateway log
confirms those service errors; the health probe continued reporting PostgreSQL and Gateway OK.
This is not evidence that the generated-key correction caused the failure, nor proof that every
reported case has only an infrastructure cause. The original job and result artifact remain the
authoritative failure record.

The pinned image runs background system-package updates during startup. Its update-completion
message precedes the first observed service error by seconds, but timing alone does not establish
causality. The existing console artifact lacks PostgreSQL's internal error detail. Teardown now
also captures /logs/pglog.log, package-updates.log, and start_postgres_server.log before removing
the container. The focused local run verified all three files were saved. No emulator setting,
cleanup retry, query bypass, or concurrency reduction was added. Stability remains unresolved.

Startup diagnostics now include podman machine list and podman machine start <name>, because this
machine has a named VM and no default VM. The focused availability controls verify both commands.


## Final production neutrality review, November 21, 2026

The production changes are general provider/transport corrections. None references a Cosmos
package, provider name, JSON field spelling, Cosmos session interface, or emulator behavior.
The Cosmos SQL-root adapter, session setup, transaction configuration, and provider-specific
models remain in the test project. MongoDB source and project configuration remain disabled.

| Production area | General rule and evidence | Remaining limit |
|---|---|---|
| Incomplete-document repair | Merge omitted owned values using model keys, ownership, and existing context services. InMemory/SQLite regressions and the earlier official MongoDB 10 portability experiment support it. | That historical MongoDB experiment does not validate later changes or an EF Core 11 MongoDB provider. |
| Nonrelational projected terminal limits | Leave count representation to a nonrelational provider instead of forcing the relational EF.Constant marker. Direct Cosmos controls verify bounded results. | Other document providers must still translate the resulting query. |
| Generated owner acceptance | Include implicitly written owners in EF's acceptance set, including deferred acceptance. InMemory and direct Cosmos controls cover it. | A provider must return its generated values through EF update entries. |
| Dictionary interfaces | Rebuild a compatible Dictionary for dictionary interfaces. Serializer-backed independent regressions cover nullable, nested, and shared values. | This is value reconstruction, not evidence for arbitrary provider mappings. |
| Registered query roots | Resolve the registered public adapter rather than selecting the relational default unconditionally. InMemory regressions prove service replacement. | Cosmos-specific root recognition remains an explicitly registered test adapter. |
| Document order and generated key exchanges | Preserve owned-navigation order, wait on real foreign keys, return actually changed generated keys, and prepare exchanges with temporary generators. InMemory reconstruction/key controls and independent Cosmos document reads cover it. | The temporary prepass applies where the registered generator produces temporary values. Arbitrary generated key types and MongoDB 11 remain unverified. |

Confidence is high that the implementation boundaries are provider-neutral. Runtime confidence
outside the exercised providers remains limited by those explicit gaps. Source independence is
not a promise that every document provider exposes identical conventions or capabilities.

All classes in the pinned discovered Cosmos inventory now have an adoption or applicability
decision. Final full normal, coverage, and SQLite/Firebird slow validation remain outstanding.
The newly captured emulator failures prevent declaring the expanded milestone validated.


## CI regression corrections, November 21, 2026

Final-commit validation exposed two separate failures. The normal and coverage functional runs
both reported Passed: 29589, Failed: 776, Total: 30602. Their graph-update failures originated
in the new temporary-key preparation, not in Cosmos. Keys already marked temporary were needlessly
regenerated; inherited-type generator caches could start at a value already tracked by another
entry. The prepass now leaves existing temporary values alone and still prepares exchanges of
explicit existing keys. The unchanged failing alternate-key family first reported Passed: 0,
Failed: 98, Total: 98. With the correction and generated-document controls it reported
Passed: 108, Failed: 0, Total: 108. The additional failing slow-comparison graph case passed:
Passed: 1, Failed: 0, Total: 1. Focused protocol controls: Passed: 22, Failed: 0, Total: 22.

The newly captured Cosmos PostgreSQL log identifies the E58P01 cause precisely:
`could not load library "/usr/lib/postgresql/plpgsql.so"` with
`undefined symbol: heap_datum_update_type`. The package-update artifact records background
replacement of postgresql and postgresql-libs 16.15 with 16.16 while the image's original custom
server is running. Thus the pinned container digest did not keep its database libraries pinned.
The normal Cosmos job reported 212 failures and its coverage counterpart 263; fixture failures
account for many dependent test failures. These are distinct from the relational key regression.

Managed Testcontainers runs now supply the original /etc/tdnf/tdnf.conf settings plus
`excludepkgs=postgresql*`. This keeps the image's original database packages while retaining
other system-package updates. The actual package-manager noninstalling transaction preview
excludes PostgreSQL from the upgrade plan. A first comma-separated draft did not exclude either
package and was corrected before commit; its green focused run is not evidence for the final pin.
The documented package exclusion mechanism is described in the
[tdnf configuration reference](https://vmware.github.io/photon/docs-v5/administration-guide/managing-packages-with-tdnf/configuration_options/).

This is an explicit test-container package pin, not a query-parser setting, suppressed failure,
cleanup retry, or reduced parallelism. It applies to managed containers; explicit external
emulators remain the caller's responsibility. Teardown also records installed PostgreSQL package
versions. The earlier missing-relation errors are not declared resolved by this separate library
mismatch diagnosis. Full normal, coverage, and final-commit slow comparisons must be reviewed again.


Final wildcard-pin, document key-exchange, availability, and audit checks reported Passed: 10,
Failed: 0, Total: 10. The captured installed-package record shows postgresql and postgresql-libs
still at 16.15-1.azl3. The transaction preview aborts without installing, as intended; its upgrade
plan omits both PostgreSQL packages. Those focused checks do not replace a complete GitHub run.

## Official container module and live query comparison, November 21, 2026

The owner approved removing unnecessary setup differences. The managed fixture now uses
Testcontainers.CosmosDb 4.17.0 and CosmosDbBuilder with vnext-latest, like the pinned official
CosmosTestEnvironment. Module defaults select the protocol, port mapping, disabled explorer and
telemetry, and startup-log readiness check. The package exclusion, custom HTTPS command, health
port, loopback binding override, and immutable image selection are removed. Explicit external
mode retains its separate HTTPS restriction and manual helper; it is not the managed test path.

Remaining lifecycle differences are documented here and in fixture comments:
one fresh assembly-owned container, explicit external opt-in instead of automatic localhost reuse,
diagnostic capture, and a three-minute overall startup deadline. These do not alter query execution.
Gateway mode matches official provider configuration. The separate official test retry strategy
and twenty-minute request timeout have not been adopted by this container-only change.

The existing optional INFOCARRIER_LIVE_COMPARE mode now covers Cosmos query and association
families, read-item families, and the JSON, owned, inheritance, ad-hoc, and raw-query classes.
Direct controls are not replayed as wire tests. Each side owns a separate database. An EF
DiagnosticListener captures the actual executing queries and point reads without replacing
fixture logging. Query text, containers, partitions, operation order, and test outcomes are
compared. Parameter names are canonicalized outside quoted text; JSON keys and literals remain
significant. Parameter values, result-row read counts, request charges, and Cosmos writes are not
captured by this mechanism. Application assertions validate results independently.

The first focused comparison reported Passed: 25, Failed: 1, Total: 26. It detected positional
projection carrier fields Item1/Item2 replacing e/Title in Where_simple_shadow_projection_mixed.
The projected entity, scalar, and predicate remain identical. The override now records that
specific difference with InfoCarrierDesign(10), SqlDiffers, and an explanation; the normalizer
does not erase it. Two always-on direct/wire controls compare filtering, scalar and nested
projection, ordering, and limits, and require nonempty captured queries on both paths.

The optional Slow comparison workflow accepts cosmos or all. Its Cosmos job uses a Linux runner
and starts the emulator through the same assembly fixture. Captured differences stay in failure
messages and workflow artifacts; no generated query baseline is committed. Full validation remains
open. The prior commit's normal jobs had two functional failures and two Cosmos session failures;
switching container modules is not a claim that those unrelated assertions are fixed.

Focused final comparison, partition-key, inheritance, raw-query, and override-audit validation
reported Passed: 35, Failed: 0, Total: 35. The Release build passed with warnings treated as errors.
This is focused Windows/Podman evidence with the official module and unmodified current image,
not a complete-suite result or a Linux/Docker claim.

## Inherited key-generator correction, November 21, 2026

The two remaining functional failures reproduced in isolation: Passed: 0, Failed: 2, Total: 2.
Ignore_before_save_property_is_still_generated_graph supplies explicit keys across a mapped
inheritance hierarchy. The generated-key exchange prepass selected a temporary generator using
the entry's concrete type, creating separate generators for one inherited key and colliding
temporary values. Official ValueGenerationManager.TryFindValueGenerator selects using
property.DeclaringType. The prepass now makes that same call. This is an EF metadata correction,
with no document-provider condition or Cosmos-specific value handling.

The unchanged failing graph cases, their scalar siblings, and generated-document owner/key
exchange controls then reported Passed: 14, Failed: 0, Total: 14. A nonincremental Release build
passed with warnings treated as errors. Reference:
[ValueGenerationManager](https://github.com/dotnet/efcore/blob/9e685c7a52d1a344d133a3d370e458db97417b3f/src/EFCore/ChangeTracking/Internal/ValueGenerationManager.cs#L188-L222).
Focused protocol compatibility and protocol 3 execution checks reported Passed: 22, Failed: 0,
Total: 22. No payload format or field meaning changed.

## Current official-image conflict failure and first full comparison, November 21, 2026

The current vnext-latest image identifies itself as EN20261019. Its original PostgreSQL and
library packages are both 16.16-1.azl3, before running its startup script. Consequently the
previous image's 16.15-to-16.16 update diagnosis does not explain this new observation, and no
package exclusion is restored. Local image inspection recorded image ID
fb41c9f127ccd798bf5c0e14d9d2ba8b457b71fa8c2a32d3bbcc88f1384273a5.

The previously failing paging scenario passed against the official module. The conflict-token
scenario still failed in both transaction modes. An independent direct-provider execution using
the same model, storage setup, and operations reproduced both failures without InfoCarrier:
Passed: 0, Failed: 4, Total: 4 across the direct and wire controls. Duplicate insertion returns
500 InternalServerError, not the intended conflict, with PostgreSQL reporting
`cannot drop portal "<unnamed portal ...>" while it is being set up`. No session token arrives.
The PostgreSQL log independently records the error while executing cosmos_api.create_document.
The upstream token-equality requirement stays unchanged and failing. Diagnostic assertions now
include the underlying exception, rather than hiding the 500 behind a token mismatch. No retry,
skip, package mutation, or weaker token assertion is introduced. This local failure is not proof
of the cause of the earlier Linux token difference.

The first complete optional Cosmos comparison on c760e842 reported Passed: 4184, Failed: 83,
Total: 4648, with 381 skipped, in
[run 44907219437](https://github.com/azabluda/InfoCarrier.Core/actions/runs/44907219437).
The failures include projection carrier names, literal-versus-parameter query limits, and
direct/wire outcome differences. They are an investigation queue, not 83 established production
defects and not 83 accepted deviations. The comparison remains red until each difference is
explained or corrected. The exact-commit normal, coverage, and all-store slow runs on 340a307b
were still running when these findings were recorded.

Initial triage found that some outcome differences were assertion-harness errors: copied wire
assertions expected InfoCarrierServerException even during a direct-provider replay, which
correctly threw CosmosException. CosmosExceptionAssert now requires the exact native exception
on the direct path and the exact transmitted wrapper on the wire path. Only after asserting that
type does it expose a common type-name/message view for existing assertions. It does not wrap the
executed query, change provider behavior, accept arbitrary exceptions, or change expected results.
Existing StoreExceptionAsData metadata continues to describe this mechanical wire difference.
Focused failing-replay cases and the complete override audit then reported Passed: 4, Failed: 0,
Total: 5, with one existing official synchronous-operation skip. The conflict-token controls
remain red by design of this investigation: they still require the upstream successful behavior,
and do not accept the observed emulator error as a passing result.

## Projection comparison triage, November 21, 2026

Reviewed each pure projection-envelope difference in the first full comparison. The selected
field expressions, predicates, row ordering, and limits match; InfoCarrier reconstructs the
application projection from positional carrier members instead of the direct provider's JSON
envelope/member names. Those methods now carry InfoCarrierDesign(10) with SqlDiffers and an
explanation. Their original result assertions and tracking modes remain unchanged. Association
overrides delegate to the inherited scenario, including optional values and primitive collections.
The comparison continues to preserve these JSON names and validate the declared difference.

Four point-read differences were generated identifiers in independently seeded databases, not
different query operations. Point-read capture now compares operation, container, and partition,
without the item identifier value. Query literals remain significant. This follows the existing
comparison policy of excluding varying input values; application assertions still validate the
returned entities. It does not normalize query projection names, SQL structure, or query literals.

Focused direct/wire projection, generated-identifier, and audit validation reported Passed: 54,
Failed: 0, Total: 59, with five existing official skips. The strict Release build passed.
Literal-versus-parameter limits, compiled raw-query parameterization, null-entity membership,
alias naming, cancellation request counts, and any remaining outcome differences are separate
triage items. This batch does not declare those differences harmless or add blanket reasons.

The complete functional GitHub job on 340a307b reported Passed: 30365, Failed: 0, Total: 30602,
with 237 skipped, confirming the inherited-generator correction beyond its focused controls.
Its Cosmos job still failed conflict-token checks and the one-item paging assertion. The local
passing paging control therefore does not establish stable Linux behavior.

## Small query-shape differences, November 21, 2026

The next reviewed methods retain their inherited assertions and now explain four measured
differences: positional enum-projection members and a parameterized one-row Take bound; a renamed
bound collection variable; removal of a null entity candidate before key membership; and earlier
cancellation before a backend query-attempt event. The cancellation reference is the wire protocol's
cancellation-before-dispatch section. These reasons do not cover unrelated shapes or outcomes.
The six independent terminal-projection controls also verify that unrelated rows do not cross the
wire. Focused comparison, bounded-payload, and audit checks reported Passed: 13, Failed: 0, Total: 13.

The complete comparison after the exception-assertion correction, before projection triage,
reported Passed: 4223, Failed: 44, Total: 4648, with 381 skipped in
[run 44908402798](https://github.com/azabluda/InfoCarrier.Core/actions/runs/44908402798).
Remaining direct association assertion errors are being separated from actual provider behavior.
Refreshing vnext-latest from the registry retained the same local image ID, so the independently
reproduced duplicate-insert failure was not explained by a stale locally cached tag.

## Association direct-replay assertions, November 21, 2026

The remaining association replay failures were assertion setup errors. Two collection cases
selected the native exception type only for dedicated DirectAssociationFixture instances, not
for the direct replay of a wire fixture. They now recognize both direct paths and retain the
same error-message assertions. Three subquery cases inherit a common tracked-owned assertion
whose expected message differs from Cosmos's earlier translation refusal. The official Cosmos
overrides omit that tracked arm. Our existing direct controls already exercise the written query
and assert native translation refusal in both tracking modes. That implementation is now shared
with direct replay instead of copied in a second class. The wire's existing tracked-owned
refusal and all nontracking checks remain. Updated metadata explains the extra tracked coverage.

The focused replay, dedicated direct controls, and override audit reported Passed: 17, Failed: 0,
Total: 17. The Release build passed with warnings treated as errors. The complete comparison on
55bd2841 reported Passed: 4253, Failed: 14, Total: 4648, with 381 official skips, before this
association correction and the small query-shape annotations.

## Final captured differences and stronger read controls, November 21, 2026

Re-read the official indexed-JSON and coalescing Cosmos overrides before classifying the four
remaining projection differences. They preserve the same predicates, ordering, limits, and native
Cosmos refusal; positional carrier member names differ. Their custom attributes now explain that
specific difference. The two indexed-JSON skip records now describe only the synchronous arms,
matching the upstream condition; their asynchronous error assertions were already active and remain.

The compiled raw-query case is different: the official compiled Cosmos query inlines its constant
CONSH argument. The compiled InfoCarrier client ships the raw expression and values for ordinary
backend translation, as ADR-006 specifies; that backend execution keeps the argument parameterized.
An ADR-006 SqlDiffers reason records this distinction. Both executions now also assert CustomerID
equals CONSH, beyond the unchanged single-row assertion. This is not claimed to produce identical
query plans. No compile-mode wire field or provider-specific production branch was added.
Focused comparison and override-audit checks reported Passed: 7, Failed: 0, Total: 11, with four
existing official synchronous skips. The strict Release build passed.

Point-read comparison excludes varying generated identifiers, while a separate always-on control
now validates exact SDK diagnostic item and partition keys against three stored customers. It
requests two different keys through both native Cosmos and InfoCarrier and checks the corresponding
names. Its scoped observer does not add duplicate comparison records in other parallel tests.
Point-key, existing query comparison, and audit controls reported Passed: 6, Failed: 0, Total: 6.

The independent paging control now runs in both orders, with native or wire insertion respectively,
and identifies the failing path in a page-size assertion. It retains pageSize=1, continuation
completion, all three expected identifiers, and no-tracking assertions. Both local cases passed:
Passed: 2, Failed: 0, Total: 2. This adds diagnostics for the earlier Linux failure; it does not
declare that intermittent behavior resolved or change the official paging contract.

## Official paging exclusion and conflict investigation, November 21, 2026

The additional ordered, one-document-page control missed the official Linux exclusion already
adopted by NorthwindMiscellaneousTest.ToPageAsync. It now carries the same StoreDefect reference
to pinned EF Core 11 NorthwindMiscellaneousQueryCosmosTest.cs lines 4848-4904 and the same
"MaxItemCount not respected" skip reason. Its body remains for a future emulator correction.
Both local host operating systems use the Linux emulator, so both receive this exclusion.
The supported exact-size paging control remains active. Focused paging and override-audit checks
reported Passed: 2, Failed: 0, Total: 3, with one skipped theory.

The conflict assertion remains unchanged. A standalone reproduction uses the official EF Core
11.0.0 package, its real session storage, the upstream customer model, two ordinary contexts,
SemiAutomatic session management, and both upstream transaction settings. It has no InfoCarrier
references or replacement storage service. Against the default CosmosDbBuilder vnext-latest image,
both settings reproduce a null conflict token after a successful first insert. The underlying
duplicate insert returns HTTP 500 with "cannot drop portal ... while it is being set up".
The official 20-minute request timeout does not change this result. SDK-only reproductions on
Windows and in a Linux client container also fail without Entity Framework Core.

Independent upstream evidence now confirms that the assertion is not reliably green there.
Microsoft's [Linux Cosmos job](https://github.com/dotnet/efcore/actions/runs/44859366342/job/136708933780)
fails Add_conflict_updates_session_token for both Always and Never with expected nonnull tokens
and actual null. Its Windows Cosmos job succeeds. This upstream run is on the current development
branch targeting .NET 12, not the pinned EF Core 11 release. The standalone EF Core 11 reproduction
separately establishes the version-11 result. The upstream log does not expose the underlying
HTTP error, so matching token failures alone do not prove an identical internal cause.

The evidence identifies an emulator failure independent of InfoCarrier, rather than an extra
InfoCarrier requirement. There is still no official skip for the conflict cases. No production
workaround, token fabrication, retry, or changed conflict expectation was introduced. Diagnostic
sources and logs remain under artifacts/CosmosOfficialConflictRepro.cs,
artifacts/cosmos-official-conflict-repro.log, and artifacts/official-cosmos-linux-44859366342.log.

All three slow comparison jobs on 3f99e065 completed successfully in
[run 44911082036](https://github.com/azabluda/InfoCarrier.Core/actions/runs/44911082036).
Normal and coverage Cosmos validation on that commit retained six failures before this paging
exclusion. The four conflict cases remain unresolved; a green complete Cosmos suite is not claimed.

## Official emulator correction, November 21, 2026

The remaining conflict failure matches confirmed
[emulator issue 391](https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/391).
Its vnext-latest hotfix returns HTTP 500 instead of the required 409 on duplicate creation.
The maintainer confirms that the official vnext-EN20261117pre image avoids this defect and plans
a correction to the default image. The test fixture temporarily selects that dated preview on
both hosts, with the reason recorded here and in fixture comments. CosmosDbBuilder, protocol, readiness,
session storage, parallelism, and all conflict assertions stay unchanged. No packages are changed
inside the image. Return to upstream's vnext-latest after verifying its correction.

The standalone official EF Core 11 reproduction passes both transaction settings against the
preview: duplicate insertion returns 409 and the conflict token equals the first insert token.
The four original direct/wire conflict cases, supported exact-size paging, and override audit
reported Passed: 6, Failed: 0, Total: 6 locally. Logs and the tested image identity remain under
artifacts/cosmos-nov17-conflict/ and artifacts/cosmos-nov17-image.txt. Complete normal, coverage,
and query-comparison validation of this image remains delegated to GitHub Actions.

## Focused token interference and attribute audit, November 21, 2026

The complete run on d12a3d33 fixes the missing-token HTTP 500 failure, but exposes a different
failure: a valid 409 response sometimes carries a newer token than the first insert. The normal
Cosmos summary is Passed: 5394, Failed: 3, Total: 6037; coverage is Passed: 5395, Failed: 2,
Total: 6037. The full optional Cosmos query comparison succeeds. The four conflict cases alone
then pass locally: Passed: 4, Failed: 0, Total: 4. Focused success does not establish full-suite
stability.

A controlled reproduction preserves the pinned official EF Core 11 conflict scenario, ordinary
contexts, real session storage, both transaction settings, and the corrected preview image.
There are no InfoCarrier references. With no intervening activity, each conflict returns the
first insert's token. After that insert, a background task writes one document to an independent
database and container before the second context attempts the duplicate insertion. For Never,
the original token is 0:0#4, the independent write returns 0:0#5, and the conflict returns 0:0#5.
Always similarly changes from 0:0#7 to 0:0#8. Both duplicates correctly return 409. The independent
database is created before the scenario and deleted afterward; no other test shares its documents.

This establishes that unrelated database writes within the same emulator can invalidate the
official equality assertion. Separate databases do not isolate these token values. The controlled
interleaving explains why the complete parallel suite can fail while the focused case passes;
it does not establish the behavior of a real Azure account. The script and output remain at
artifacts/CosmosOfficialConflictRepro.cs and artifacts/cosmos-conflict-independent-write.log.
No assertion relaxation, token fabrication, global serialization, or production fix was added.

The semantic attribute audit found one further misplaced reason: StoreLimit on the protected
TrackAndSaveTest helper in ComplexTypesTrackingTest. Its upstream synchronous exclusion stays;
the helper's source reference is now a comment. Together with the removed InitializeAsync
InfoCarrierDesign, these are two misplaced attributes found during this review. No additional
InfoCarrierDesign misuse was found. Fixture explanations stay in comments and this document.

## Token-sensitive container isolation, November 21, 2026

SessionTokenFailureDocumentTest now owns one class-scoped emulator container. Other tests retain
the assembly-scoped shared container and their parallel scheduling. Both containers use the same
official CosmosDbBuilder, image, protocol, readiness, and diagnostic capture. The isolated fixture
does not publish or clear the shared endpoint. DocumentApplicationStore accepts an explicit
connection configuration, keeping its default shared connection for every other application class.
The token-sensitive class always uses its own managed container, even when an external shared
endpoint is configured. Testcontainers disposes it after the class. No production code changes.

The pinned official scenario and exact equality assertions remain. This is container-level
isolation beyond the official suite's database isolation, justified by the independently reproduced
cross-database token interference. All original token-failure application cases and the audit
reported Passed: 13, Failed: 0, Total: 14 locally, with one existing official skip.

An additional direct/wire regression deliberately writes and verifies a document in the shared
emulator between insertion and duplicate insertion in the isolated emulator. Temporarily restoring
the session store's shared connection makes both regression arms fail with a newer conflict token:
Passed: 0, Failed: 2, Total: 2. The isolated connection was then restored. This check detects the
original interference instead of relying on scheduler timing. The final focused original-conflict,
interference-regression, and audit run reported Passed: 7, Failed: 0, Total: 7. Logs are under
artifacts/cosmos-isolated-sessions/, artifacts/cosmos-isolation-shared-control/, and
artifacts/cosmos-isolation-final/. Complete normal and coverage verification remains in GitHub.

## Default key conventions and annotation scope, November 21, 2026

The explicit ValueGeneratedNever setting in the same-key-and-partition-key integer test compensates
for a real model-convention difference. Removing it makes the official missing-key scenario fail:
the InfoCarrier save succeeds instead of raising the expected update exception. That diagnostic
run reported Passed: 0, Failed: 1, Total: 1, under artifacts/cosmos-implicit-key/.

The retained test now also builds convention-only client and native Cosmos models without the
compensating setting. It asserts client ValueGenerated.OnAdd and native ValueGenerated.Never,
then retains the original explicitly aligned missing-key and supplied-key checks. The explicit
setting and its InfoCarrierDesign reason remain. This establishes configured behavior, not
automatic parity between provider conventions. It does not change production conventions or
apply Cosmos defaults to every nonrelational backend. Other document providers can have different
key-generation rules and require their own verification; MongoDB remains disabled.

The Add_conflict_updates_session_token InfoCarrierDesign record described backend observation,
not a changed token expectation. Its explanation is now a comment. Both direct and wire token
assertions remain. Focused convention, conflict, and override-audit validation reported Passed: 6,
Failed: 0, Total: 6, under artifacts/cosmos-key-conventions/.

The remaining recorded differences stay. Projection rewriting, parameter transport, cancellation,
and incomplete-document repair are shared InfoCarrier mechanisms. Their exact generated query
forms depend on the backend provider. Cosmos session-token behavior and emulator cross-database
token interference are Cosmos-specific observations; no equivalent MongoDB behavior is claimed.

## Rejecting temporary keys unsupported by the backend, November 21, 2026

The unconfigured-key regression confirmed an actual persisted placeholder: the missing-key insert
succeeded and an independent backend query returned Id=-2147482647. Before the correction, the
new rejection regression reported Passed: 0, Failed: 1, Total: 1. It checks the stored row count
and keys, not only the client model or SaveChanges return value.

ServerSaveChangesExecutor now rejects an Added entry's temporary key when the backend marks it
ValueGenerated.Never and it is not a borrowed foreign key. The existing TemporaryProperties field
distinguishes placeholders from explicit values. A configuration error identifies the property and
requires matching generation rules before any database write. The positive control saves an
explicit negative key and independently reads it back. This is a provider-neutral safety fix;
it neither infers Cosmos conventions nor adds wire fields or a protocol version.

The original explicitly aligned official key test remains, together with its convention controls.
Rejecting an inconsistent request is not full convention parity: the guard reports a configuration
error, rather than reproducing Cosmos's provider-specific missing-key warning. Permanent generated
values remain outside this temporary-value protection. The architecture now documents the
configuration requirement and this protection.

The affected key class and audit reported Passed: 14, Failed: 0, Total: 14. Focused document writes,
nested values, repair, and end-to-end cases reported Passed: 101, Failed: 0, Total: 105, with four
existing official skips. Logs are under artifacts/cosmos-temporary-key-before/,
artifacts/cosmos-temporary-key-after/, and artifacts/cosmos-temporary-key-documents/.
Shared generated-value and foreign-key controls on InMemory and SQLite reported Passed: 12,
Failed: 0, Total: 12. Historical payload and protocol controls reported Passed: 33, Failed: 0,
Total: 33. The production Release rebuild passed with source warnings treated as errors; its
existing NU1900 vulnerability-service connectivity warning was excluded from that local error policy.

Historical scope was checked against v10.3.0: LiveComparison selected only the SQLite and Firebird
namespaces. MongoDB retained separate direct controls rather than this full query-comparison path.
Current evidence does not date every deviation or show that MongoDB had identical manifestations.

## Correcting the temporary-key guard, November 21, 2026

Complete normal and coverage validation of 282ee821 each reported Passed: 30363, Failed: 2,
Total: 30602. Both failures were SQLite table-per-concrete-type inheritance cases. The guard
incorrectly equated a non-generated backend key with an invalid default key. The official SQLite
write scenario legitimately saves a zero key. Both failures reproduced locally before correction:
Passed: 0, Failed: 2, Total: 2.

Replay now restores the backend sentinel instead of rejecting the request itself. Native Cosmos
can raise its own missing-key warning, while SQLite can accept zero. Accepted keys are returned
to the client through the existing correlated response. The placeholder map redirects foreign
keys to the restored value. Explicit negative keys are untouched. This replaces the blanket
rejection described above; there is no provider-name test or relational bypass.

Both inherited SQLite cases and existing generated-key controls reported Passed: 14, Failed: 0,
Total: 14. An independent SQLite control first confirms that native storage accepts zero, then
checks the remoted principal, the client key, and a dependent foreign key after SaveChanges and
through a separate backend read: Passed: 1, Failed: 0, Total: 1. The Cosmos control now requires
the native missing-key DbUpdateException and exact warning, rather than the guard's custom
configuration error. Logs are under artifacts/tpc-guard-before/, artifacts/tpc-guard-after/,
artifacts/default-key-fixup/, and artifacts/restored-key-cosmos/.
The corrected Cosmos key class and audit reported Passed: 14, Failed: 0, Total: 14. Historical
payload and protocol checks reported Passed: 33, Failed: 0, Total: 33. The production Release
rebuild passed with source warnings treated as errors, retaining the local NU1900 exception noted above.
