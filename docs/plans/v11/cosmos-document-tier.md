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

Use `docker` instead of `podman` in the lifecycle command on Linux CI. Startup waits up to three
minutes for `/ready`. Ports 8081 and 8080 bind to loopback. Cleanup removes only the task-owned
container. Each fixture owns a separate generated database. Test collections remain parallel.
An unavailable emulator fails the tests instead of silently skipping them.

The client uses gateway transport and accepts the emulator certificate only on loopback HTTPS.
The embedded key is Microsoft's public emulator key, not an Azure credential. Neither
`AZURE_COSMOS_BYPASS_QUERY_PARSING` nor `AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED` is set.
Query results and service responses are not rewritten to accommodate the emulator.

## Upstream skips and coverage limits

EF Core 11 skips four aggregate methods on the Linux emulator. One has two tracking cases.
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
| Queries | Six inherited owned-navigation families, direct and through the wire, subject to the upstream skips |
| Writes | Insert, read, scalar update, nested replacement, collection update/removal, optional-reference removal, root deletion |
| Stored shape | Independent Cosmos client reads verify nested objects, arrays, and deeper references |
| Incomplete documents | Attached stubs and scalar-only changes preserve independently read nested values |
| Concurrent changes | Repair preserves another context's nested changes and original concurrency entity tags |
| Policy isolation | Named Cosmos and InMemory registrations retain separate repair policies in both registration orders |
| Keys and projections | Generated GUID keys, nested scalar projections, and payload boundaries |
| Failures | Explicit transaction refusal and missing-document update outcomes, with direct controls |
| Other providers | InMemory and SQLite cover synchronous/asynchronous repair, context filters, and real SQLite transactions |

## Production change review

The only production change from the Cosmos implementation is incomplete-document repair in
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

Updated GitHub Actions checks are pending. Emulator testing does not establish full Azure service
parity, distributed-query coverage, official MongoDB support, or release approval.
