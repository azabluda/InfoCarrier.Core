# Provisional Cosmos document tier

November 14, 2026. The owner authorized implementation and complete local and Linux CI validation.
The decision to retain Cosmos permanently or temporarily remains open. MongoDB source is unchanged
and disabled. No release or public MongoDB restoration is part of this work.
The classic emulator in this revision is a CI candidate, not a completed validation result.
No tested local configuration has passed the complete Linux tier with default query execution.

## Inputs and execution

- Provider: Microsoft's `Microsoft.EntityFrameworkCore.Cosmos` 11.0.0, targeting .NET 11.
- Emulator: Microsoft's classic Linux emulator 2.14.28.0, obtained from its documented `latest`
  image and pinned by digest
  `sha256:9a4f69121f20c1d6502db84625c71ebb441ab61b01880a7b2d3d8031c6dac429`.
- Reference: EF Core `v11.0.0`, commit `9e685c7a52d1a344d133a3d370e458db97417b3f`.
- Local runtime: Podman 6.1.3 with a dedicated `infocarrier-cosmos` WSL machine.
- CI runtime: Docker on the existing Ubuntu GitHub Actions runners.

Start the installed runtime, then run. On this Windows checkout the runtime starts with
`podman machine start infocarrier-cosmos`:

```powershell
dotnet run --file eng/CosmosEmulator.cs -- start podman
dotnet test test/InfoCarrier.Core.CosmosTests/InfoCarrier.Core.CosmosTests.csproj --configuration Release
dotnet run --file eng/CosmosEmulator.cs -- stop podman
```

Use `docker` instead of `podman` for the same lifecycle command in CI. Startup waits for the
HTTPS certificate endpoint, with a five-minute deadline. Port 8081 binds to loopback. The command
only removes a container carrying its own ownership label. Tests require the emulator and fail
when it is unavailable. They do not silently skip a missing database service.

The embedded account key is Microsoft's publicly documented emulator key, not an Azure account
credential. The test client accepts the emulator's generated certificate only on loopback HTTPS
connections. No cloud account or secret is required. Every fixture has its own database name;
cleanup deletes only that database. Test collections retain parallel execution.

The client retains normal SDK query-execution defaults. Neither
`AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED` nor `AZURE_COSMOS_BYPASS_QUERY_PARSING` is set.
Windows can use native query planning; Linux obtains its query plans through the gateway.
The harness selects gateway transport and accepts the loopback emulator certificate. It does
not replace query plans, rewrite service responses, or alter result assertions. Microsoft's
[emulator instructions](https://learn.microsoft.com/azure/cosmos-db/how-to-develop-emulator)
document this classic image and its HTTPS certificate endpoint.

The build workflow runs the Cosmos project and joins its result file and override audit with the
functional suite. Coverage runs each project explicitly and sequentially, starting the emulator
after the functional workload finishes. `eng/Measure.cs` includes
Cosmos alongside the functional project; start the emulator before running it.

## Validation scope

| Area | Evidence |
|---|---|
| Nonrelational queries | All six inherited owned-navigation families, through the wire and directly against Cosmos |
| Nested data | References, arrays, nested references within references, and arrays inside references |
| Writes | Insert, scalar and nested changes, complete reference replacement, element update/removal, empty arrays, optional-reference removal, root deletion |
| Physical document shape | Independent Cosmos client reads verify nested objects and arrays inside one stored document |
| Incomplete writes | Attached stubs and clients without nonrelational configuration retain independently read nested values |
| Concurrent independent changes | Scalar updates preserve deeper changes committed by a second context |
| Concurrency | Stale entity and stale stub writes retain original entity tags and fail without changing stored values |
| Generated keys | Automatic GUID keys persist and can be read independently, with direct controls |
| Projections | Nested scalar projections and payload checks verify that unrelated document values do not cross the wire |
| Policy isolation | Named Cosmos and InMemory registrations retain separate document-repair policies in both registration orders |
| Failure behavior | Explicit transaction refusal and missing-document update failures have direct-store controls |
| Portability | InMemory and SQLite check synchronous/asynchronous repair, context-dependent query filters, and explicit SQLite transactions |

Cosmos-specific expected results are copied from the pinned Cosmos suite with exact source ranges.
No inherited query family is dropped. Upstream's filtered/unfiltered method mix-up and its swapped
required/optional nested projection bodies are corrected locally, with explicit deviation notes.
The direct controls exercise three tracked subquery refusals that upstream omits; they do not
replace those checks with empty bodies. This tier adds no skipped cases.

## Production change review

The only production change is incomplete-document repair in `ServerSaveChangesExecutor`.
The first attempted implementation used a second `DbContext`; it was rejected because it could
lose application context state or the current transaction. The final implementation retains the
original context and creates a provider query context with a separate identity-resolution tracker.
It uses EF's provider-specific query-context factory, query compiler, and cache-key generator.
Keys remain query parameters, so different document identifiers do not create one cache entry per key.

The merge uses model entity types, primary keys, ownership, modification flags, and concurrency
metadata. It contains no Cosmos package reference, annotation name, provider-name check, document
field name, or entity-tag convention. Provider-specific raw shadow values and owned ordinal keys
remain in the tracked query snapshots. Existing modified, added, or deleted entries keep their
identity and state. Unmodified scalar values come from storage without changing concurrency originals.
Temporary query tracking is cleared after merging to release its subscriptions.

This is a general document-repair mechanism, not a Cosmos-specific persistence algorithm.
The InMemory and SQLite regression checks verify that its execution does not depend on Cosmos.
They also exercise context filters and actual transactions, which a Cosmos-only test cannot prove.
The Cosmos entity-tag and deep-document cases verify preservation of provider-generated values
and nested values through the wire.

MongoDB compatibility is still a qualified expectation, not a tested claim. The retained MongoDB
project remains disabled because the official provider does not support the adopted framework.
The inspected ajcvickers EF11 source uses EF's query-context tracking methods, which is consistent
with this design, but source inspection does not replace executing a trusted compatible provider.
Recheck its shadow keys, tracking/materialization, and transaction behavior when restoration becomes
possible. No arbitrary document provider is guaranteed merely because the implementation uses EF interfaces.

## Limits

The emulator validates local persistence and provider behavior, not complete Azure service parity.
Its supported features and authentication behavior differ from the hosted service. Cosmos does
not support synchronous database operations or general relational transactions; direct controls
record applicable refusals. The extra local InMemory and SQLite tests cover the synchronous repair path.

This work does not establish MongoDB support, authenticate the unofficial MongoDB NuGet package,
or settle the owner's permanent database choice. Detailed command logs and result files are local
under `artifacts/cosmos*` and `artifacts/measure/cosmos-document-tier*`.

## Results

Local validation, November 14, 2026:

| Check | Actual result |
|---|---|
| Complete functional project | Passed: 30350, Failed: 0, Total: 30583; 233 skipped |
| Complete Cosmos project, Release | Passed: 252, Failed: 0, Total: 252; no skipped cases |
| Transport project, Release | Passed: 395, Failed: 0, Total: 395 |
| Engineering project, Release | Passed: 61, Failed: 0, Total: 61 |
| Warning-as-error Release build | Five documented framework Razor warnings, zero errors |
| Clean trimming check | 116 InfoCarrier warnings, equal to the 116 baseline |
| Wire structural guard | Protocol 4 retained; protected descriptions match |
| Preserved protocol 3 and current protocol 4 clients | Behavioral acceptance passed, including independently read writes, rollback, failure atomicity, generated keys and deletion |
| Documentation links | Zero broken links in 114 Markdown files |
| Workflow syntax | Both changed workflow files parse |
| Dependency graph | All resolved Entity Framework Core packages are 11.0.0; no unofficial MongoDB dependency |

The Cosmos result timestamps show nine overlapping test cases. The override audit checks all
26 upstream references against the pinned source, reports no violations, and reports no skipped
overrides. Its explicitly noted deviations include stronger tracked-arm direct controls and
corrections to the two upstream query-body mix-ups described above.

The combined measurement initially exposed a wrong expectation in the newly added missing-document
control. A root without an entity tag raises `DbUpdateException` containing Cosmos 404, rather than
`DbUpdateConcurrencyException`. The final control asserts that exact direct outcome, the preserved
wire cause and the client-side failed-entry identity. The complete Cosmos suite passed after that
test correction. Production code did not change after the successful full functional run.

The InMemory and SQLite portability regression passed all four synchronous/asynchronous cases.
The review therefore supports high confidence that the repair mechanism is provider-neutral for
providers using the normal EF Core 11 query and tracking contracts. It does not establish MongoDB
11 runtime compatibility or guarantee every custom provider implementation.

## Initial vNext result and rejected workaround

The first [spec run](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43577070719)
and [coverage run](https://github.com/azabluda/InfoCarrier.Core/actions/runs/43577070687)
both reported Cosmos Passed: 242, Failed: 10, Total: 252. Emulator startup and the functional
suite passed. Five direct-store cases and their five wire counterparts failed on nested aggregates.
One query raised `RewrittenAggregateProjections was not an array`; others returned one result
where seven were expected.

The SDK uses native query planning on Windows x64 but gateway planning on Linux. Forcing gateway
planning on Windows reproduced both representative failures against the same pinned image.
The emulator's gateway plan misclassifies nested aggregates, causing the SDK to select an incorrect
aggregation pipeline. Enabling optimistic direct execution avoids that plan for this single-partition
test store. The two representative controls then passed, followed by the complete Windows Release
suite: Passed: 252, Failed: 0, Total: 252, with no skips. The build treated warnings as errors.
No production code, query, assertion, or test parallelism changed for this correction.

The same assembly then ran under Linux in Microsoft's .NET SDK 11.0.100 container, using the
emulator through Podman's host network: Passed: 252, Failed: 0, Total: 252, with no skips.
The repository was mounted read-only so the override audit could inspect its evidence.
This used xUnit's in-process runner because the Windows-built app host cannot execute on Linux.
Logs and XML results are retained locally under `artifacts/cosmos-ci/` and `artifacts/cosmos-ci-*`.

That workaround narrowed validation to one execution mode and left normal Linux planning unverified.
The owner required standard query-execution defaults instead. Both environment overrides were
removed, and the emulator was changed rather than carrying that workaround forward.

## Classic emulator with default query execution

Microsoft's classic Linux emulator 2.14.28.0 passed all six direct owned-navigation query families
on a native Linux .NET 11 test host: Passed: 102, Failed: 0, Total: 102. Both query-execution
overrides were absent. This includes the five direct cases that failed against vNext's gateway
plans. However, this does not qualify the image for the complete tier.

The complete classic-emulator attempts on WSL kernel `6.18.58.1-microsoft-standard-WSL2` exposed
native emulator crashes and fixture container-creation errors. An instrumented run completed with
Passed: 226, Failed: 26, Total: 252; the failures occurred during container creation. Other runs
aborted after the emulator exited, so they have no complete test summary. Increasing the emulator's
partition capacity to 64 and then using 40 did not eliminate native crashes. No assertion, query,
test collection parallelism, production code, or dependency version was changed to accept them.

The official `vnext-EN20260602` general-availability image was also tested under native Linux .NET 11,
pinned to `sha256:9a6d2a97fd28254c28cee7e8ba120ee198fe787ad0e4fd825ccaefeedc52792c`.
Its direct controls reported Passed: 96, Failed: 6, Total: 102. It reproduces the aggregate-planning
failures and also fails an optional-associate projection, so it was not taken to the complete tier.

The next controlled comparison runs the classic image with 40 partitions on GitHub's native Linux
Docker runner. This tests a different host from local Podman on WSL without restoring SDK query
overrides. Local native builds, test summaries, TRX files, and emulator crash diagnostics remain
under `artifacts/cosmos-default-*`. Native crash causes remain unresolved. A green direct-control
run, or the earlier single-partition workaround, must not be reported as complete default-client
validation. The draft pull request remains an investigation until its complete checks pass and
the local emulator instability is resolved or explicitly accepted by the owner.

This emulator has a built-in evaluation period. On November 14, 2026, startup reported 118 days
remaining. Refresh the official image pin before that period expires; a permanently frozen digest
cannot provide indefinite CI service. The emulator still does not establish complete Azure service
parity or validate distribution across physical partitions.

Complete-tier default-client validation remains unfinished. Updated Docker CI validation is the next
comparison. The owner still decides whether Cosmos is temporary or permanent.
