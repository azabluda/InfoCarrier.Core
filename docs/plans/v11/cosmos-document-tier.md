# Provisional Cosmos document tier

November 14, 2026. The owner authorized implementation and complete local and Linux CI validation.
The decision to retain Cosmos permanently or temporarily remains open. MongoDB source is unchanged
and disabled. No release or public MongoDB restoration is part of this work.

## Inputs and execution

- Provider: Microsoft's `Microsoft.EntityFrameworkCore.Cosmos` 11.0.0, targeting .NET 11.
- Emulator: `vnext-EN20261008`, pinned by digest
  `sha256:81707bcadf894a29d7dab63377685d408b5d54dca3aacfb625c19db4c6392e7c`.
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
emulator readiness endpoint, with a three-minute deadline. Ports bind to loopback. The command
only removes a container carrying its own ownership label. Tests require the emulator and fail
when it is unavailable. They do not silently skip a missing database service.

The embedded account key is Microsoft's publicly documented emulator key, not an Azure account
credential. The test client accepts the emulator's generated certificate only on loopback HTTPS
connections. No cloud account or secret is required. Every fixture has its own database name;
cleanup deletes only that database. Test collections retain parallel execution.

The build workflow runs the Cosmos project and joins its result file and override audit with the
functional suite. Coverage runs each project explicitly and sequentially. `eng/Measure.cs` includes
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

Linux Docker validation remains pending on the review branch. Local Podman results do not stand
in for that required check. The owner still decides whether Cosmos is temporary or permanent.
