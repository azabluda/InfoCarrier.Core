# Shared Deployment Contract Implementation Plan

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a minimal abstractions package and one shared builder that configures matching version 10.3 client and server facts.

**Architecture:** `InfoCarrier.Core.Abstractions` owns only the shared builder and value-mapper interface. The existing client builder derives from the shared builder. A new server `AddInfoCarrier` entry point consumes the same application extension. Core reads an internal immutable snapshot through production-only friendship.

**Tech Stack:** C# 14, .NET 10, Entity Framework Core 10, Microsoft.Extensions.DependencyInjection, xUnit, SQLite, ASP.NET Core transport tests.

**Spec:** `docs/plans/v10/shared-deployment-contract-design.md`

## Global Constraints

- Keep `InfoCarrier.Core.Abstractions` limited to `InfoCarrierDeploymentBuilder` and `IInfoCarrierValueMapper` as public contracts.
- Give the abstractions package only Base Class Library dependencies; do not reference Entity Framework Core or dependency injection.
- Keep `IInfoCarrierValueMapper` in `InfoCarrier.Core.ValueMapping` and type-forward it from `InfoCarrier.Core`.
- Grant `InternalsVisibleTo` only from `InfoCarrier.Core.Abstractions` to `InfoCarrier.Core`; grant no test friendship.
- Keep the existing `UseInfoCarrier` overload set unchanged.
- Preserve version 10.2 planning when the client has no anonymous-shape declaration.
- Keep the server as the admission boundary, and never retry a refused query with client evaluation.
- Start each application mapper list empty; install standard mappers automatically after application mappers.
- Remove the unreleased anonymous-shape role methods instead of retaining internal versions with the same names.
- Add warning-level obsolete attributes only to the five approved version 11 removals.
- Do not implement version 11 role options, negotiation, agreement checks, or removals.
- Do not edit files under `subrepos/**`.
- Write new C# classes with primary constructors where a constructor only assigns fields.

## Review Focus

- A failing additive call must leave all earlier builder state unchanged. Task 3 observes the final server registrations.
- A legacy mapper compiled against version 10.2 must load through the type forwarder. Task 1 runs a separate binary fixture.
- Two client configurations with different mapper instances must not share an Entity Framework internal provider. Task 2 tests reference identity.
- Repeated server calls must preserve mapper order without duplicating standard mappers. Task 3 compares the final service sequence.
- A client-only anonymous declaration must cause one refusal, zero database commands, and no retry. Task 3 retains this transport assertion.

---

### Task 1: Minimal abstractions package and binary compatibility

**Files:**

- Create: `src/InfoCarrier.Core.Abstractions/InfoCarrier.Core.Abstractions.csproj`
- Create: `src/InfoCarrier.Core.Abstractions/InfoCarrierDeploymentBuilder.cs`
- Create: `src/InfoCarrier.Core.Abstractions/ValueMapping/IInfoCarrierValueMapper.cs`
- Create: `src/InfoCarrier.Core.Abstractions/Properties/InternalsVisibleTo.cs`
- Create: `src/InfoCarrier.Core.Abstractions/PACKAGE.md`
- Create: `src/InfoCarrier.Core/Properties/TypeForwards.cs`
- Create: `test/InfoCarrier.Core.BinaryCompatibility/LegacyMapperConsumer/LegacyMapperConsumer.csproj`
- Create: `test/InfoCarrier.Core.BinaryCompatibility/LegacyMapperConsumer/LegacyMapper.cs`
- Create: `test/InfoCarrier.Core.BinaryCompatibility/TypeForwardHost/TypeForwardHost.csproj`
- Create: `test/InfoCarrier.Core.BinaryCompatibility/TypeForwardHost/Program.cs`
- Create: `eng/verify-type-forwarding.ps1`
- Modify: `InfoCarrier.Core.slnx`
- Modify: `src/InfoCarrier.Core/InfoCarrier.Core.csproj`
- Delete: `src/InfoCarrier.Core/ValueMapping/IInfoCarrierValueMapper.cs`
- Delete: `src/InfoCarrier.Core/Properties/InternalsVisibleTo.cs`
- Test: `test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentBuilderTest.cs`

**Interfaces:**

- Produce: public `InfoCarrier.Core.InfoCarrierDeploymentBuilder`.
- Produce: `void AddAnonymousShapes(params object[] shapes)`.
- Produce: `void AddAllowedTypes(params Type[] types)`.
- Produce: `void AddValueMappers(params IInfoCarrierValueMapper[] mappers)`.
- Produce: `bool AllowArbitrarySqlExecution { get; set; }`.
- Produce internally: immutable ordered snapshots for Core and one shared anonymous-prototype validator.
- Preserve: `InfoCarrier.Core.ValueMapping.IInfoCarrierValueMapper` with its existing two methods.

- [ ] **Step 1: Write failing builder and package-boundary tests**

Add `InfoCarrierDeploymentBuilderTest`. Test these cases:

- `Null_arrays_and_items_fail_during_configuration`
- `Anonymous_prototypes_accept_nested_anonymous_types_and_reject_named_types`
- `Abstractions_assembly_exposes_only_the_two_approved_contracts`
- `Abstractions_assembly_references_only_framework_assemblies`

Use reflection only for the assembly surface and dependency assertions. Defer ordered-state assertions until public role entry points consume the builder.

- [ ] **Step 2: Run the focused tests and verify the missing-type failure**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter FullyQualifiedName~InfoCarrierDeploymentBuilderTest
```

Expected: compilation fails because `InfoCarrierDeploymentBuilder` and the abstractions assembly do not exist.

- [ ] **Step 3: Add the abstractions package**

Apply the humanizer skill before creating `PACKAGE.md`. Add the project to `InfoCarrier.Core.slnx`. Set `IsPackable` to `true`. Disable package validation only for this first package version, with a comment that no published baseline exists. Pack its own `PACKAGE.md` and the repository icon.

Implement the exact public surface from the Interfaces block. Keep mutable lists private. Validate every input before appending. Return immutable copied snapshots through internal members.

Move `IInfoCarrierValueMapper` without changing its namespace, accessibility, or signatures. Add `[assembly: InternalsVisibleTo("InfoCarrier.Core")]` in the abstractions assembly.

- [ ] **Step 4: Add Core forwarding and consume the shared internals**

Reference `InfoCarrier.Core.Abstractions` from `InfoCarrier.Core`. Add `[assembly: TypeForwardedTo(typeof(IInfoCarrierValueMapper))]` in Core. Remove the Core interface source and test friendship.

Move anonymous-prototype validation behind an internal abstractions member. Make Core's existing anonymous helpers consume that member instead of keeping a second validator.

- [ ] **Step 5: Add the compiled version 10.2 compatibility fixture**

Build `LegacyMapperConsumer` only against the published `InfoCarrier.Core` version 10.2.0 package. It implements `IInfoCarrierValueMapper`.

Build `TypeForwardHost` against the current projects. Load the legacy assembly, instantiate its mapper, and cast it to the current interface. `eng/verify-type-forwarding.ps1` builds the two projects in separate restore graphs and runs the host.

- [ ] **Step 6: Verify the package boundary and forwarding behavior**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter FullyQualifiedName~InfoCarrierDeploymentBuilderTest
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-type-forwarding.ps1
dotnet pack src/InfoCarrier.Core.Abstractions/InfoCarrier.Core.Abstractions.csproj -c Release --output artifacts/pack
```

Expected: focused tests pass, the legacy cast succeeds, and the package contains no non-framework dependency.

- [ ] **Step 7: Commit task 1**

```powershell
git add InfoCarrier.Core.slnx src/InfoCarrier.Core.Abstractions src/InfoCarrier.Core test/InfoCarrier.Core.BinaryCompatibility eng/verify-type-forwarding.ps1 test/InfoCarrier.Core.FunctionalTests/Deployment
git commit -m "feat: add minimal deployment abstractions"
```

### Task 2: Client builder hierarchy and mapper isolation

**Files:**

- Modify: `src/InfoCarrier.Core/InfoCarrierDbContextOptionsBuilder.cs`
- Modify: `src/InfoCarrier.Core/InfoCarrierOptionsExtension.cs`
- Modify: `src/InfoCarrier.Core/InfoCarrierServiceCollectionExtensions.cs`
- Delete: `src/InfoCarrier.Core/InfoCarrierDeploymentOptions.cs`
- Delete: `test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentOptionsTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentBuilderTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeClientRegistrationTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Expressions/GeometryWireFormatTest.cs`
- Create: `test/InfoCarrier.Core.CompileTests/ClientOnlyAfterShared/ClientOnlyAfterShared.csproj`
- Create: `test/InfoCarrier.Core.CompileTests/ClientOnlyAfterShared/Program.cs`
- Create: `eng/verify-client-fluent-syntax.ps1`

**Interfaces:**

- Consume: internal shared snapshot from task 1.
- Produce: `InfoCarrierDbContextOptionsBuilder : InfoCarrierDeploymentBuilder`.
- Preserve: all existing `UseInfoCarrier(..., Action<InfoCarrierDbContextOptionsBuilder>?)` signatures.
- Remove: `InfoCarrierDbContextOptionsBuilder.UseInfoCarrierDeployment`.
- Remove: `InfoCarrierDbContextOptionsBuilder.UseRegisteredAnonymousShapes`.
- Remove: unreleased `InfoCarrierServiceCollectionExtensions.AddInfoCarrierDeployment` before deleting its options type.
- Preserve internally: ordered application mapper registrations and existing zero hash without application mappers.

- [ ] **Step 1: Write failing client hierarchy tests**

Add these tests:

- `Existing_UseInfoCarrier_callback_accepts_shared_extension`
- `Client_specific_calls_chain_before_shared_extension`
- `Client_specific_calls_do_not_compile_after_shared_extension`
- `Client_callback_invokes_shared_configuration_once`
- `Different_mapper_instances_do_not_share_an_internal_provider`
- `No_application_mapper_keeps_zero_service_provider_hash`
- `Application_mappers_run_before_standard_and_model_mappers`

Build the intentional negative fixture separately from the solution. `eng/verify-client-fluent-syntax.ps1` must require compiler diagnostic `CS1061` for `UseNonRelationalServerStore` after `ConfigureInfoCarrier`.

- [ ] **Step 2: Run focused client tests and verify failure**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter "FullyQualifiedName~InfoCarrierDeploymentBuilderTest|FullyQualifiedName~AnonymousShapeClientRegistrationTest|FullyQualifiedName~GeometryWireFormatTest"
```

Expected: the hierarchy and direct shared extension tests fail against the old nested deployment method.

- [ ] **Step 3: Derive the client builder from the shared builder**

Remove `UseInfoCarrierDeployment`. During the existing `UseInfoCarrier` callback, use the same derived builder for client-only and shared methods. After the callback, translate its inherited snapshot into `InfoCarrierOptionsExtension`.

Keep `UseNonRelationalServerStore` returning `InfoCarrierDbContextOptionsBuilder`. Remove both unreleased deployment entry points and the old options type. Remove the unreleased `UseRegisteredAnonymousShapes` public method. Retain only smaller private or internal translation helpers.

- [ ] **Step 4: Preserve client mapper ordering and provider isolation**

Store the ordered application mapper snapshot on the options extension. Register those instances before standard mappers in `ApplyServices`.

Keep `GetServiceProviderHashCode() == 0` for an empty application mapper list. Otherwise hash ordered object identities. Compare ordered mapper instances by reference in `ShouldUseSameServiceProvider`.

- [ ] **Step 5: Verify focused client behavior**

Run the command from step 2.

Then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-client-fluent-syntax.ps1
```

Expected: all selected tests pass. The compile fixture rejects client-only calls after the shared extension.

- [ ] **Step 6: Commit task 2**

```powershell
git add src/InfoCarrier.Core test/InfoCarrier.Core.FunctionalTests test/InfoCarrier.Core.CompileTests eng/verify-client-fluent-syntax.ps1
git commit -m "feat: configure shared facts through the client builder"
```

### Task 3: Server entry point, defaults, and anonymous registration matrix

**Files:**

- Modify: `src/InfoCarrier.Core/InfoCarrierServiceCollectionExtensions.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentBuilderTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeStructuralRegistrationTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Sqlite/RawSqlExecutionProbeTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Sqlite/SpatialiteServerTest.cs`
- Modify: `test/InfoCarrier.Core.TransportTests/NorthwindOverHttpTest.cs`
- Modify if required: `test/InfoCarrier.Core.TransportTests/NorthwindServerFactory.cs`

**Interfaces:**

- Produce: `IServiceCollection AddInfoCarrier(this IServiceCollection services, Action<InfoCarrierDeploymentBuilder> configure)`.
- Remove: `InfoCarrierServiceCollectionExtensions.AddInfoCarrierAnonymousShapes`.
- Consume: shared snapshot from task 1.
- Preserve: first-claim-wins application mapper order.
- Install: standard `IPAddress` and `Uri` mappers once, after application mappers.

- [ ] **Step 1: Write failing server translation tests**

Add these tests:

- `Server_AddInfoCarrier_translates_all_four_shared_facts`
- `Server_AddInfoCarrier_invokes_configuration_once_and_rejects_null`
- `Repeated_server_calls_compose_application_mappers_in_order`
- `Standard_mappers_are_automatic_and_not_duplicated`
- `Application_mapper_lists_start_empty_on_both_roles`
- `One_application_extension_configures_both_roles_without_shared_state`
- `Additive_calls_compose_and_preserve_order`
- `Failed_additive_call_does_not_change_existing_state`
- `Caller_owned_arrays_are_snapshotted_before_configuration_returns`

Assert the exact mapper instance order after building the service provider. Invoke the application extension twice and assert separate mapper instances.

- [ ] **Step 2: Run focused server tests and verify failure**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter "FullyQualifiedName~InfoCarrierDeploymentBuilderTest|FullyQualifiedName~AnonymousShapeStructuralRegistrationTest|FullyQualifiedName~RawSqlExecutionProbeTest|FullyQualifiedName~SpatialiteServerTest"
```

Expected: compilation fails because the new `AddInfoCarrier` overload does not exist.

- [ ] **Step 3: Implement server translation and automatic defaults**

Create one builder, invoke the callback once, and translate its snapshot. Apply only requested permissions. Route each fact through one private helper shared with its released low-level method.

Register application mapper instances in order. Install standard mappers idempotently afterward. Remove the unreleased public anonymous-shape server method.

- [ ] **Step 4: Move the four-state matrix to the shared public surface**

Cover absent on both roles, client only, server only, and both roles. For client-only declaration, assert one transport request, zero database commands, the existing refusal, and no retry.

For both-side declaration, assert backend execution and the original client anonymous identity. For absent-client cases, assert version 10.2 planning.

- [ ] **Step 5: Verify focused server and transport behavior**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter "FullyQualifiedName~InfoCarrierDeploymentBuilderTest|FullyQualifiedName~AnonymousShapeStructuralRegistrationTest|FullyQualifiedName~RawSqlExecutionProbeTest|FullyQualifiedName~SpatialiteServerTest"
dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj -c Release --filter FullyQualifiedName~NorthwindOverHttpTest
```

Expected: all selected tests pass, and the client-only case records no database command.

- [ ] **Step 6: Commit task 3**

```powershell
git add src/InfoCarrier.Core/InfoCarrierServiceCollectionExtensions.cs test/InfoCarrier.Core.FunctionalTests test/InfoCarrier.Core.TransportTests
git commit -m "feat: add shared server configuration"
```

### Task 4: Version 10.3 obsolete warnings and public-surface checks

**Files:**

- Modify: `src/InfoCarrier.Core/InfoCarrierDbContextOptionsBuilder.cs`
- Modify: `src/InfoCarrier.Core/InfoCarrierServiceCollectionExtensions.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Deployment/InfoCarrierDeploymentBuilderTest.cs`
- Modify: package compatibility fixtures selected by existing build failures.

**Interfaces:**

- Mark warning `ICC1001`: `InfoCarrierDbContextOptionsBuilder.AllowTypes`.
- Mark warning `ICC1002`: `InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution`.
- Mark warning `ICC1003`: `AddInfoCarrierAllowedTypes`.
- Mark warning `ICC1004`: `AddInfoCarrierArbitrarySqlExecution`.
- Mark warning `ICC1005`: `AddInfoCarrierStandardValueMappers`.
- Use: `https://github.com/azabluda/InfoCarrier.Core/issues/221` as each obsolete warning URL.

- [ ] **Step 1: Write failing obsolete-metadata tests**

Assert each approved method has a warning-level `ObsoleteAttribute`, its assigned diagnostic identifier, its URL, and a replacement message.

Assert no other released public member gains `ObsoleteAttribute`. Assert both unreleased anonymous methods are absent from the public API.

- [ ] **Step 2: Run the metadata tests and verify failure**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter FullyQualifiedName~InfoCarrierDeploymentBuilderTest
```

Expected: the five approved methods have no obsolete metadata.

- [ ] **Step 3: Add exact warning metadata**

Add warning-level `[Obsolete]` attributes with identifiers `ICC1001` through `ICC1005`. Name the shared-builder replacement in each message. Set `UrlFormat` to the issue URL from the Interfaces block.

Refactor product internals to call private helpers instead of obsolete public methods. Keep consumer migration warnings without creating warnings in the product build.

- [ ] **Step 4: Verify the public surface**

Run the command from step 2, then run:

```powershell
$env:CI='true'; dotnet build InfoCarrier.Core.slnx -c Release; Remove-Item Env:CI
dotnet pack InfoCarrier.Core.slnx --no-build -c Release --output artifacts/pack
```

Expected: metadata tests pass, the strict build has zero warnings, and package validation accepts only approved changes.

- [ ] **Step 5: Commit task 4**

```powershell
git add src/InfoCarrier.Core test/InfoCarrier.Core.FunctionalTests
git commit -m "feat: announce version 11 configuration removals"
```

### Task 5: Shared application examples and migration documentation

**Files:**

- Modify: `samples/Northwind.Shared/Northwind.Shared.csproj`
- Modify: `samples/Northwind.Shared/NorthwindDeployment.cs`
- Modify: `samples/Northwind.Client/Program.cs`
- Modify: `samples/Northwind.Demo/Program.cs`
- Modify: `samples/Northwind.Server/Program.cs`
- Modify: `src/InfoCarrier.Core/PACKAGE.md`
- Modify: `src/InfoCarrier.Core.Abstractions/PACKAGE.md`
- Modify: `website/docs/configuration/client.md`
- Modify: `website/docs/configuration/server.md`
- Modify: `website/docs/configuration/value-mappers.md`
- Modify if required: `mkdocs.yml`

**Interfaces:**

- Recommend: `InfoCarrierDeploymentBuilder ConfigureInfoCarrier(this InfoCarrierDeploymentBuilder builder)`.
- Demonstrate client order: client-specific calls first, then `ConfigureInfoCarrier()`.
- Demonstrate server use: `services.AddInfoCarrier(builder => builder.ConfigureInfoCarrier())`.
- Reference: `InfoCarrier.Core.Abstractions` from the shared sample project instead of `InfoCarrier.Core`.

- [ ] **Step 1: Apply the humanizer skill**

Read and apply the humanizer skill before changing either `PACKAGE.md` file or `website/`. Preserve exact API names and security statements.

- [ ] **Step 2: Update the Northwind shared extension and role calls**

Return the base builder from the non-generic extension. Put `UseNonRelationalServerStore()` before it where required. Use a lambda on both roles because the return-valued method group cannot bind to `Action<T>`.

Remove redundant standard mapper registration. Keep application mapper lists empty unless the sample has an application mapper.

- [ ] **Step 3: Document supported project layouts and migration**

Recommend reusing an existing shared model project. Describe a separate deployment project when the model must remain unaware of InfoCarrier. Describe linked source as a small-repository alternative.

State that the abstractions package itself has no Entity Framework Core dependency. State that a consumer project may reference both packages. Explain that server `AddInfoCarrier` configures only shared facts and does not replace context, serializer, server, or endpoint setup.

List all five obsolete methods and replacements. Explain that the two unreleased anonymous methods were removed before version 10.3.

- [ ] **Step 4: Validate samples and documentation**

Run:

```powershell
dotnet build samples/Northwind.Demo/Northwind.Demo.csproj -c Release
dotnet build samples/Northwind.Server/Northwind.Server.csproj -c Release
dotnet build samples/Northwind.Client/Northwind.Client.csproj -c Release
python eng/doc-links.py src/InfoCarrier.Core/PACKAGE.md src/InfoCarrier.Core.Abstractions/PACKAGE.md website/docs/configuration/client.md website/docs/configuration/server.md website/docs/configuration/value-mappers.md
python eng/doc-words.py --budget
bash eng/docs-serve.sh --build
```

Expected: all sample builds and documentation gates pass.

- [ ] **Step 5: Commit task 5**

```powershell
git add samples src/InfoCarrier.Core/PACKAGE.md src/InfoCarrier.Core.Abstractions/PACKAGE.md website mkdocs.yml
git commit -m "docs: recommend shared deployment configuration"
```

### Task 6: Complete verification and pull-request update

**Files:**

- Modify: `docs/plans/v10/implementation-plan.md`
- Modify only other files required by failures caused by this change.

**Interfaces:**

- Verify: product, transport, binary compatibility, package, sample, trimming, and documentation gates.
- Record: exact results from command output without derived counts.
- Complete: the shared deployment phase only after every required gate succeeds.

- [ ] **Step 1: Run file hygiene on every changed file**

Run `.claude/hooks/file-hygiene.py` with the complete changed-file list.

Expected: no findings.

- [ ] **Step 2: Run complete product verification**

Run:

```powershell
bash eng/measure.sh shared-deployment
dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-type-forwarding.ps1
$env:CI='true'; dotnet build InfoCarrier.Core.slnx -c Release; Remove-Item Env:CI
dotnet pack InfoCarrier.Core.slnx --no-build -c Release --output artifacts/pack
dotnet format InfoCarrier.Core.slnx analyzers --no-restore --verify-no-changes --diagnostics IDE0290
bash eng/trim-ratchet.sh
```

Expected: `FAILING 0`, zero transport failures, a successful legacy cast, zero build errors, package compatibility success, no IDE0290 findings, and no trimming regression.

- [ ] **Step 3: Repeat final sample and documentation gates**

Repeat task 5 step 4 after the final product build.

Expected: all sample builds and documentation gates pass.

- [ ] **Step 4: Record evidence and close the phase**

Update `docs/plans/v10/implementation-plan.md` with the exact command results. Mark the shared deployment phase complete only after all gates pass.

- [ ] **Step 5: Commit final evidence**

```powershell
git add docs/plans/v10/implementation-plan.md
git commit -m "Step SDC1: complete shared deployment contract"
```

- [ ] **Step 6: Request review and update the pull request**

Use `superpowers:requesting-code-review`. Correct verified findings. Push the branch and update [pull request #223](https://github.com/azabluda/InfoCarrier.Core/pull/223). Keep `Assisted by OpenAI Codex.` in its body. Reply to review comments with the implemented decisions, resolve completed threads, and stop while continuous integration runs.
