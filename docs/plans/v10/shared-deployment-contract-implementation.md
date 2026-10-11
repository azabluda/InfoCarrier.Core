# Shared Deployment Contract Implementation Plan

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one version 10.3 deployment configuration delegate that applies matching shared facts to InfoCarrier clients and servers without changing existing version 10 APIs.

**Architecture:** A new plain `InfoCarrierDeploymentOptions` snapshots four shared facts. The client entry point translates them into the existing context options extension, while the server entry point translates them into existing dependency-injection registrations. Existing low-level methods remain independent, composable and behaviorally unchanged.

**Tech Stack:** C# 14, .NET 10, Entity Framework Core 10, Microsoft.Extensions.DependencyInjection, xUnit, SQLite, ASP.NET Core transport tests.

**Spec:** `docs/plans/v10/shared-deployment-contract-design.md`

## Global Constraints

- Keep every released version 10 public type and member binary compatible.
- Keep the current anonymous catalog public signatures even though they were not released in version 10.2.
- Do not add `[Obsolete]` warnings during version 10.
- Preserve version 10.2 planning when the client has no new anonymous-shape declaration.
- Keep the server as the admission boundary for anonymous shapes, named types and arbitrary SQL.
- Never retry a refused query with client evaluation.
- Keep endpoints, serializers, payload limits, transactions, logging, caller identity and store behavior role-specific.
- Do not implement version 11 role options, removals, negotiation or mismatch diagnostics.
- Do not edit files under `subrepos/**`.
- Write new C# classes with primary constructors where a constructor only assigns fields.

## Review Focus

- Two client configurations with different mapper instances must not share one Entity Framework internal provider.
- A client configuration without custom mappers must retain the existing service-provider identity and standard mapper chain.
- A null item in any additive method must fail during configuration, before a context or server is created.
- A shared server call mixed with existing low-level registrations must compose without duplicate standard mappers.
- A client-only anonymous declaration must produce one refusal, zero database commands and no retry.

---

### Task 1: Shared options value and validation

**Files:**

- Create: `src/InfoCarrier.Core/InfoCarrierDeploymentOptions.cs`
- Create: `test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentOptionsTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/InfrastructureInfoCarrierTests.cs`

**Interfaces:**

- Produce: public sealed `InfoCarrierDeploymentOptions`.
- Produce: `void AddAnonymousShapes(params object[] shapes)`.
- Produce: `void AddAllowedTypes(params Type[] types)`.
- Produce: `void AddValueMappers(params IInfoCarrierValueMapper[] mappers)`.
- Produce: `bool AllowArbitrarySqlExecution { get; set; }`.
- Produce internally: immutable snapshots of anonymous types, allowed types and mapper instances.
- Reuse: `AnonymousShapeSet.ValidatePrototypes` for anonymous prototype validation.

- [ ] **Step 1: Write failing options tests**

Add these tests to `InfoCarrierDeploymentOptionsTest`:

- `Additive_methods_compose_and_preserve_order`
- `Inputs_are_snapshotted_before_configuration_returns`
- `Null_arrays_and_items_fail_during_configuration`
- `Anonymous_shape_validation_matches_existing_role_APIs`

Assert exact ordered snapshots for two calls. Mutate caller-owned arrays afterward and assert no change. Compare named, nested and null prototype outcomes with the existing client and server methods.

- [ ] **Step 2: Run the tests and verify the missing API failure**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter FullyQualifiedName~InfoCarrierDeploymentOptionsTest
```

Expected: compilation fails because `InfoCarrierDeploymentOptions` does not exist.

- [ ] **Step 3: Implement the shared options class**

Add the exact public members from the Interfaces block. Keep collection access internal. Snapshot every input. Preserve insertion order. Validate each item before changing state, so a failed call adds nothing.

- [ ] **Step 4: Add public API convention coverage**

Add `InfoCarrierDeploymentOptions` and `InfoCarrierDbContextOptionsBuilder` to the appropriate `InfoCarrierApiConsistencyFixture.FluentApiTypes` expectations only where their return conventions apply. Do not suppress a convention globally.

- [ ] **Step 5: Run focused tests**

Run the command from step 2.

Expected: all `InfoCarrierDeploymentOptionsTest` cases pass.

- [ ] **Step 6: Commit task 1**

```powershell
git add src/InfoCarrier.Core/InfoCarrierDeploymentOptions.cs test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentOptionsTest.cs test/InfoCarrier.Core.FunctionalTests/InfrastructureInfoCarrierTests.cs
git commit -m "feat: add shared deployment options"
```

### Task 2: Client entry point and mapper isolation

**Files:**

- Modify: `src/InfoCarrier.Core/InfoCarrierDbContextOptionsBuilder.cs`
- Modify: `src/InfoCarrier.Core/InfoCarrierOptionsExtension.cs`
- Modify: `src/InfoCarrier.Core/InfoCarrierServiceCollectionExtensions.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentOptionsTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeClientRegistrationTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Expressions/GeometryWireFormatTest.cs`

**Interfaces:**

- Consume: internal snapshots from task 1.
- Produce: `InfoCarrierDbContextOptionsBuilder.UseInfoCarrierDeployment(Action<InfoCarrierDeploymentOptions> configure)`.
- Produce internally: `InfoCarrierOptionsExtension.WithDeployment(InfoCarrierDeploymentOptions options)`.
- Produce internally: ordered custom mapper registrations during `InfoCarrierOptionsExtension.ApplyServices`.
- Preserve: service-provider hash `0` and current sharing when no custom mapper exists.
- Isolate: configurations with different mapper instance sequences through `GetServiceProviderHashCode` and `ShouldUseSameServiceProvider`.

- [ ] **Step 1: Write failing client translation tests**

Add these tests:

- `Client_shared_configuration_translates_all_four_facts`
- `Client_shared_calls_compose_with_existing_low_level_calls`
- `Client_configuration_invokes_the_delegate_once_and_rejects_null`
- `Different_mapper_instances_do_not_share_an_internal_provider`
- `No_custom_mapper_keeps_the_existing_internal_provider_identity`
- `Custom_mappers_run_before_standard_and_model_mappers`

Use two stateful mapper instances of the same runtime type for the provider-isolation case. Assert that each context uses its own instance and cannot observe the other's state.

- [ ] **Step 2: Run focused tests and verify failure**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter "FullyQualifiedName~InfoCarrierDeploymentOptionsTest|FullyQualifiedName~AnonymousShapeClientRegistrationTest|FullyQualifiedName~GeometryWireFormatTest"
```

Expected: compilation fails because `UseInfoCarrierDeployment` does not exist.

- [ ] **Step 3: Implement client translation**

Add the exact public method from the Interfaces block. Create one options object, invoke the delegate once and apply one immutable extension clone. Translate anonymous shapes, allowed types and arbitrary SQL into their existing extension state.

- [ ] **Step 4: Register custom client mappers safely**

Store the mapper snapshot on the options extension. Register each mapper instance before standard mappers in `ApplyServices`. Keep `GetServiceProviderHashCode() == 0` when the mapper snapshot is empty. For configured mappers, hash the ordered object identities and compare ordered instances by reference in `ShouldUseSameServiceProvider`.

- [ ] **Step 5: Verify focused client behavior**

Run the command from step 2.

Expected: all selected tests pass. Existing no-mapper provider-sharing assertions remain unchanged.

- [ ] **Step 6: Commit task 2**

```powershell
git add src/InfoCarrier.Core/InfoCarrierDbContextOptionsBuilder.cs src/InfoCarrier.Core/InfoCarrierOptionsExtension.cs src/InfoCarrier.Core/InfoCarrierServiceCollectionExtensions.cs test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentOptionsTest.cs test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeClientRegistrationTest.cs test/InfoCarrier.Core.FunctionalTests/Expressions/GeometryWireFormatTest.cs
git commit -m "feat: apply shared deployment options to clients"
```

### Task 3: Server entry point and shared-fact parity

**Files:**

- Modify: `src/InfoCarrier.Core/InfoCarrierServiceCollectionExtensions.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentOptionsTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeStructuralRegistrationTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Sqlite/RawSqlExecutionProbeTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Sqlite/SpatialiteServerTest.cs`

**Interfaces:**

- Consume: internal snapshots from task 1.
- Produce: `IServiceCollection AddInfoCarrierDeployment(this IServiceCollection services, Action<InfoCarrierDeploymentOptions> configure)`.
- Reuse: existing anonymous catalog, allowed-type and arbitrary-SQL registration paths.
- Register: custom mapper instances in order, before idempotent standard mappers.

- [ ] **Step 1: Write failing server translation tests**

Add these tests:

- `Server_shared_configuration_translates_all_four_facts`
- `Server_shared_calls_compose_with_existing_low_level_calls`
- `Server_configuration_invokes_the_delegate_once_and_rejects_null`
- `Repeated_server_calls_keep_custom_order_without_standard_duplicates`
- `One_application_method_configures_a_custom_mapper_on_both_roles`

Resolve the service collection to assert ordered mapper identities. Exercise one custom value end to end through client and server. Assert application mappers precede standard mappers.

- [ ] **Step 2: Run focused tests and verify failure**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter "FullyQualifiedName~InfoCarrierDeploymentOptionsTest|FullyQualifiedName~AnonymousShapeStructuralRegistrationTest|FullyQualifiedName~RawSqlExecutionProbeTest|FullyQualifiedName~SpatialiteServerTest"
```

Expected: compilation fails because `AddInfoCarrierDeployment` does not exist.

- [ ] **Step 3: Implement server translation**

Add the exact extension method from the Interfaces block. Invoke the delegate once. Apply only requested permissions. Route each setting through the same internal helper used by its existing public method where practical. Add standard mappers with the existing idempotent method.

- [ ] **Step 4: Verify shared facts and compatibility**

Run the command from step 2.

Expected: all selected tests pass. Existing low-level registration tests retain their outcomes.

- [ ] **Step 5: Commit task 3**

```powershell
git add src/InfoCarrier.Core/InfoCarrierServiceCollectionExtensions.cs test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentOptionsTest.cs test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeStructuralRegistrationTest.cs test/InfoCarrier.Core.FunctionalTests/Sqlite/RawSqlExecutionProbeTest.cs test/InfoCarrier.Core.FunctionalTests/Sqlite/SpatialiteServerTest.cs
git commit -m "feat: apply shared deployment options to servers"
```

### Task 4: Anonymous registration matrix through the shared contract

**Files:**

- Modify: `test/InfoCarrier.Core.TransportTests/NorthwindOverHttpTest.cs`
- Modify if needed: `test/InfoCarrier.Core.TransportTests/NorthwindServerFactory.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeClientRegistrationTest.cs`

**Interfaces:**

- Consume: both public entry points from tasks 2 and 3.
- Preserve: version 10.2 planning with no client declaration.
- Preserve: strict server admission and one-way refusal.

- [ ] **Step 1: Convert the permanent four-state matrix to shared entry points**

Cover absent on both roles, client only, server only and both roles. Use one application configuration method in declared cases. Keep the low-level matrix tests as compatibility coverage rather than deleting them.

- [ ] **Step 2: Assert observable boundaries**

For client-only declaration, assert one transport request, zero database commands, the existing anonymous-catalog refusal and no retry. For both-side declaration, assert backend execution and original client anonymous identity. For absent-client cases, assert the existing version 10.2 plan.

- [ ] **Step 3: Run the matrix**

Run:

```powershell
dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj -c Release --filter FullyQualifiedName~NorthwindOverHttpTest
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter FullyQualifiedName~AnonymousShapeClientRegistrationTest
```

Expected: all matrix and compatibility cases pass.

- [ ] **Step 4: Commit task 4**

```powershell
git add test/InfoCarrier.Core.TransportTests/NorthwindOverHttpTest.cs test/InfoCarrier.Core.TransportTests/NorthwindServerFactory.cs test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeClientRegistrationTest.cs
git commit -m "test: verify shared deployment registration states"
```

### Task 5: Samples, documentation and version 11 removal table

**Files:**

- Modify: `samples/Northwind.Demo/Program.cs`
- Modify: `samples/Northwind.Server/Program.cs`
- Modify if used by normal flow: `samples/Northwind.Client/Program.cs`
- Modify: `src/InfoCarrier.Core/PACKAGE.md`
- Modify: `website/docs/configuration/client.md`
- Modify: `website/docs/configuration/server.md`
- Modify: `website/docs/configuration/value-mappers.md`
- Modify if required by navigation: `mkdocs.yml`
- Update externally: [issue #104](https://github.com/azabluda/InfoCarrier.Core/issues/104)

**Interfaces:**

- Consume: the shared public entry points from tasks 2 and 3.
- Recommend: one application-owned `ConfigureInfoCarrier(InfoCarrierDeploymentOptions options)` method.
- Preserve: low-level API documentation as legacy version 10 paths without `[Obsolete]` warnings.

- [ ] **Step 1: Apply the humanizer skill**

Read and apply the humanizer skill before changing `PACKAGE.md` or `website/`. Preserve security statements and exact API names.

- [ ] **Step 2: Update normal sample configuration**

Place one shared configuration method beside model configuration. Pass it to client and server entry points. Keep role-specific setup in its current location.

- [ ] **Step 3: Update user documentation**

Recommend the shared path for anonymous structures, named types, value mappers and arbitrary SQL. Explain that matching configuration guides planning but does not grant provider translation. State that server admission remains authoritative and refusals are not retried.

- [ ] **Step 4: Update issue 104 after the additive names are final**

Replace the provisional version 11 table entries for these four facts with the exact version 10.3 names. Do not change issue state, milestone or broader version 11 scope.

- [ ] **Step 5: Validate samples and documentation**

Run:

```powershell
dotnet build samples/Northwind.Demo/Northwind.Demo.csproj --configuration Release
dotnet build samples/Northwind.Server/Northwind.Server.csproj --configuration Release
dotnet build samples/Northwind.Client/Northwind.Client.csproj --configuration Release
python eng/doc-links.py src/InfoCarrier.Core/PACKAGE.md website/docs/configuration/client.md website/docs/configuration/server.md website/docs/configuration/value-mappers.md
python eng/doc-words.py --budget
bash eng/docs-serve.sh --build
```

Expected: builds succeed and all documentation gates pass.

- [ ] **Step 6: Commit task 5**

```powershell
git add samples/Northwind.Demo/Program.cs samples/Northwind.Server/Program.cs samples/Northwind.Client/Program.cs src/InfoCarrier.Core/PACKAGE.md website/docs/configuration/client.md website/docs/configuration/server.md website/docs/configuration/value-mappers.md mkdocs.yml
git commit -m "docs: recommend shared deployment configuration"
```

### Task 6: Complete verification and delivery

**Files:**

- Modify: `docs/plans/v10/implementation-plan.md`
- Modify only other files required by failures caused by this change.

**Interfaces:**

- Verify: all product, transport, package and documentation gates.
- Record: exact results from command output without derived counts.
- Complete: phase SDC only after every required gate succeeds.

- [ ] **Step 1: Run file hygiene on every changed file**

Run `.claude/hooks/file-hygiene.py` with the complete changed-file list.

Expected: no findings.

- [ ] **Step 2: Run the complete specification measurement**

Run:

```powershell
bash eng/measure.sh shared-deployment
```

Expected: `FAILING 0`, no broken tests and unchanged reason inventory.

- [ ] **Step 3: Run transport, strict build, package and analyzer gates**

Run:

```powershell
dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj --configuration Release
dotnet build InfoCarrier.Core.slnx --configuration Release
dotnet pack InfoCarrier.Core.slnx --no-build --configuration Release --output artifacts/pack
dotnet format InfoCarrier.Core.slnx analyzers --no-restore --verify-no-changes --diagnostics IDE0290
bash eng/trim-ratchet.sh
```

Expected: zero failures and errors, existing skips only, package compatibility success, no IDE0290 findings and no trimming regression.

- [ ] **Step 4: Run documentation and sample gates**

Repeat task 5 step 5 after the final product build.

Expected: all sample builds and documentation gates pass.

- [ ] **Step 5: Review public compatibility and security boundaries**

Confirm package validation reports only additive APIs. Confirm no existing member moved, changed or gained `[Obsolete]`. Confirm client-only grants cannot execute on an unconfigured server.

- [ ] **Step 6: Record evidence and close the phase checkbox**

Update `docs/plans/v10/implementation-plan.md` phase SDC with the exact command results. Mark SDC1 complete only after all gates pass.

- [ ] **Step 7: Commit final evidence**

```powershell
git add docs/plans/v10/implementation-plan.md
git commit -m "Step SDC1: complete shared deployment contract"
```

- [ ] **Step 8: Request code review and prepare the pull request**

Use `superpowers:requesting-code-review`. Correct verified findings. Push the feature branch and open one pull request for issue 221. Add `Assisted by OpenAI Codex.` to the pull-request body. Stop while continuous integration runs.
