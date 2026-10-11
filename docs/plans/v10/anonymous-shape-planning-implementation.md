# Anonymous-shape planning implementation plan

> For agentic workers: use `superpowers:executing-plans` to implement this plan task by task. Steps use checkbox syntax for tracking.

**Goal:** Preserve version 10.2 planning for undeclared anonymous shapes, including existing exact type grants, and add explicit client declarations for the trusted catalog path.

**Architecture:** Add a composable client declaration to the existing context options extension. Represent declared shapes structurally and apply one narrowed planning allowlist throughout carrier rewriting, boundary analysis, and client-evaluation guards. Preserve existing safe rewrites for undeclared anonymous types. Keep server admission independent and unchanged.

**Tech stack:** C# 14, .NET 10, Entity Framework Core 10, xUnit, SQLite, ASP.NET Core transport tests.

**Spec:** `docs/plans/v10/anonymous-shape-planning-design.md`

## Global constraints

- Keep every released version 10 public type and member binary compatible.
- Do not add `[Obsolete]` warnings during version 10.
- Preserve strict server admission and component-type permission checks.
- Never retry a refused query with client evaluation.
- Preserve safe internal carrier rewrites that keep database execution.
- Do not implement the shared contract from issue #221 in this change.
- Do not change the stricter version 11 policy from issue #220.

## Review focus

- A client-only declaration must execute no database command after server refusal.
- A server-only declaration must preserve the version 10.2 client plan.
- Nested anonymous structures must use structural descriptors and restore original client identities.
- A declared shape must not authorize a denied component type.
- Existing public `QuerySplitter` and `ServerBoundaryAnalyzer` constructors must remain binary compatible.

---

### Task 1: Client declaration and immutable shape set

**Files:**

- Create: `src/InfoCarrier.Core/Expressions/AnonymousShapeSet.cs`
- Modify: `src/InfoCarrier.Core/InfoCarrierDbContextOptionsBuilder.cs`
- Modify: `src/InfoCarrier.Core/InfoCarrierOptionsExtension.cs`
- Create: `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeClientRegistrationTest.cs`
- Reuse validation from: `src/InfoCarrier.Core/InfoCarrierServiceCollectionExtensions.cs`

**Interfaces:**

- Produce: `InfoCarrierDbContextOptionsBuilder.UseRegisteredAnonymousShapes(params object[] shapes)`.
- Produce: immutable structural membership for ordered member names and recursively described component types.
- Produce: `InfoCarrierOptionsExtension.RegisteredAnonymousShapesFor(DbContext)` for execution-time reading.
- Preserve: existing service-provider hash value `0`.

- [x] **Step 1: Write failing public API tests**

Cover validation in `AnonymousShapeClientRegistrationTest`, planning closure in `QuerySplitterTest`, and execution-time option scope over HTTP in `NorthwindOverHttpTest`:

- `Client_shape_calls_compose_snapshot_inputs_and_remain_context_scoped`
- `Client_shape_registration_rejects_named_and_null_prototypes`
- `Registration_includes_anonymous_shapes_inside_containers`
- `Matching_nested_shape_declarations_restore_both_client_types`

Assert that repeated calls retain both shape sets, later input mutation cannot change them, and validation matches the server registration API.

- [x] **Step 2: Verify the tests fail for the missing API**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter FullyQualifiedName~AnonymousShapeClientRegistrationTest
```

Expected: compilation fails because `UseRegisteredAnonymousShapes` does not exist.

- [x] **Step 3: Implement the smallest registration surface**

Create an immutable internal shape set that snapshots prototype types and compares structural descriptors. Move prototype validation into one internal helper used by both client and server entry points. Add the builder method, options clone method, and per-context reader. Do not add these descriptors to `ExtensionInfo.GetServiceProviderHashCode()`.

- [x] **Step 4: Verify the focused tests pass**

Run the command from step 2.

Expected: all `AnonymousShapeClientRegistrationTest` cases pass.

- [x] **Step 5: Check public compatibility**

Run the repository package-compatibility target used by continuous integration.

```powershell
dotnet build InfoCarrier.Core.slnx --configuration Release
dotnet pack InfoCarrier.Core.slnx --no-build --configuration Release --output artifacts/pack
```

Expected: no removed or changed version 10 API.

### Task 2: Planning boundary and registration matrix

**Files:**

- Modify: `src/InfoCarrier.Core/Expressions/TypeAllowlist.cs`
- Modify: `src/InfoCarrier.Core/Query/ServerBoundaryAnalyzer.cs`
- Modify: `src/InfoCarrier.Core/Query/QuerySplitter.cs`
- Modify: `src/InfoCarrier.Core/QueryExecutor.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/ServerBoundaryAnalyzerTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/QuerySplitterTest.cs`
- Modify: `test/InfoCarrier.Core.TransportTests/NorthwindOverHttpTest.cs`
- Modify if needed: `test/InfoCarrier.Core.TransportTests/NorthwindServerFactory.cs`

**Interfaces:**

- Consume: client shape membership from task 1.
- Produce: undeclared original anonymous types are not server-ready.
- Produce: declared original anonymous types retain current transport eligibility.
- Preserve: every existing public constructor signature.

- [x] **Step 1: Write failing boundary tests**

Replace the unconditional anonymous-shipping expectation with two explicit cases:

- `An_unregistered_anonymous_projection_stops_at_the_source`
- `A_registered_anonymous_projection_ships_with_its_shape`

Add a safe anonymous join-key case proving the database-executed rewrite remains server-ready without declaration.

- [x] **Step 2: Write failing transport matrix tests**

Add focused cases for neither-side, client-only, server-only, and both-side declarations. Record requests and server database commands.

Assert:

- neither-side reconstructs the final result on the client;
- client-only receives a catalog refusal, sends one request, executes zero commands, and makes no retry;
- server-only matches the neither-side client plan;
- both-side matches the direct provider result and retains the original client anonymous type.

- [x] **Step 3: Verify the new behavior tests fail correctly**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter "FullyQualifiedName~ServerBoundaryAnalyzerTest|FullyQualifiedName~QuerySplitterTest"
dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj -c Release --filter FullyQualifiedName~NorthwindOverHttpTest
```

Expected: undeclared projections still ship, so the new legacy-planning assertions fail.

- [x] **Step 4: Implement planning membership**

Give `ServerBoundaryAnalyzer` an additive init-only structural shape setting. Give `QuerySplitter` the matching additive setting and apply it before analysis. `QueryExecutor` reads the current context's shape set and supplies it to the splitter.

Apply the planning view during every rewrite pass and node verdict. Keep component checks for declared shapes. Undeclared original anonymous types retain only existing exact `AllowTypes` grants; otherwise use the released rewrite boundary. Refuse deliberate reconstruction nodes during final analysis.

- [x] **Step 5: Verify the focused behavior passes**

Run both commands from step 3.

Expected: all selected functional and transport tests pass.

- [x] **Step 6: Pin existing named-type compatibility**

Add characterization cases for a named record declared on neither side, only the client, and both sides. Verify local final reconstruction, strict server refusal, and backend execution respectively. Run them before and after any further refactoring.

Expected: these cases pass before production changes affecting named types and remain unchanged afterward.

### Task 3: Nested permission and provider-refusal boundaries

**Files:**

- Modify: `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeClientRegistrationTest.cs`
- Modify: `test/InfoCarrier.Core.FunctionalTests/Sqlite/ServerParameterizationTest.cs`
- Modify: `test/InfoCarrier.Core.TransportTests/NorthwindOverHttpTest.cs`

**Interfaces:**

- Consume: structural shape membership and planner behavior from tasks 1 and 2.
- Preserve: independent component permission and provider translation decisions.

- [x] **Step 1: Add focused boundary cases**

Add tests proving:

- declaring a wrapper does not admit its denied component;
- nested declarations restore each original client anonymous type;
- a declared construction refused by SQLite returns SQLite's refusal;
- that refusal makes one request and starts no client retry.

- [x] **Step 2: Prove test sensitivity**

Temporarily omit the nested descriptor or component permission in each test setup. Verify each test fails for its stated reason. Restore the intended setup before production edits.

- [x] **Step 3: Apply only required corrections**

If a focused test exposes a defect in descriptor recursion or permission ordering, make the smallest production correction. Do not widen `TypeAllowlist` and do not catch provider refusal.

- [x] **Step 4: Run focused tests**

Run:

```powershell
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj -c Release --filter "FullyQualifiedName~AnonymousShapeClientRegistrationTest|FullyQualifiedName~ServerParameterizationTest"
dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj -c Release --filter FullyQualifiedName~NorthwindOverHttpTest
```

Expected: all selected cases pass with no unexpected warnings.

### Task 4: Samples and user documentation

**Files:**

- Modify: `samples/Northwind.Console/Program.cs`
- Modify: `samples/Northwind.Client/Program.cs`
- Modify: `src/InfoCarrier.Core/PACKAGE.md`
- Modify if required: `docs/security-review.md`

**Interfaces:**

- Consume: `UseRegisteredAnonymousShapes` from task 1.
- Explain: client declaration controls planning; server registration controls admission.
- Refer: shared recommended configuration remains issue #221.

- [x] **Step 1: Update both sample clients**

Declare every anonymous structure their normal flows send. Keep the existing server registrations unchanged.

- [x] **Step 2: Update package documentation**

Use the humanizer skill before editing `PACKAGE.md`. Document the two-sided version 10 syntax, the four registration states, provider refusal, and no automatic retry. Do not describe the issue #221 API as available.

- [x] **Step 3: Validate samples**

Build and run the existing console and Blazor sample validation commands.

```powershell
dotnet build samples/Northwind.Demo/Northwind.Demo.csproj --configuration Release
dotnet build samples/Northwind.Client/Northwind.Client.csproj --configuration Release
bash eng/trim-ratchet.sh
```

Expected: normal flows succeed with matching declarations.

### Task 5: Full validation and review

**Files:**

- Modify only files required by failures caused by this change.

- [x] **Step 1: Run file hygiene**

Run `.claude/hooks/file-hygiene.py` against every changed file.

- [x] **Step 2: Run focused Release suites**

Run the full transport suite and the relevant projection, parameterization, protocol, and catalog families.

Expected: zero failures beyond existing skips.

- [x] **Step 3: Run the complete repository validation**

Run the repository's strict Release build, full functional suites, document-store suite, trimming comparison, package compatibility, IDE0290 check, and sample checks.

```powershell
dotnet build InfoCarrier.Core.slnx --configuration Release
dotnet pack InfoCarrier.Core.slnx --no-build --configuration Release --output artifacts/pack
dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --no-build --configuration Release
dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj --no-build --configuration Release
dotnet test test/InfoCarrier.Core.DocumentStoreTests/InfoCarrier.Core.DocumentStoreTests.csproj --no-build --configuration Release
dotnet format InfoCarrier.Core.slnx analyzers --no-restore --verify-no-changes --diagnostics IDE0290
bash eng/trim-ratchet.sh
dotnet build samples/Northwind.Demo/Northwind.Demo.csproj --configuration Release
```

Expected: zero failures or errors, existing skips only, and unchanged warning counts and names.

Also run the owner-requested slow live SQL comparisons for SQLite and Firebird, using
`INFOCARRIER_LIVE_COMPARE=1` with each provider's functional-test filter. Run these after
the regular measurement, using isolated Release output to preserve the running sample host.
Inspect every reported difference against the existing decisions. These comparisons are
investigation reports, not acceptance gates. Keep their output under ignored artifacts;
never commit captured SQL or change expected outcomes merely to clear a difference.

- [x] **Step 4: Review the public change**

Confirm XML documentation distinguishes planning knowledge from server permission. Confirm no existing API has an `[Obsolete]` attribute. Confirm package validation sees only additive public members.

- [x] **Step 5: Commit implementation**

Commit focused tests and production changes with no Codex co-author trailer.

## Validation evidence, 2026-10-11

Independent review found that result-only registration checks and a flat-scalar exception did
not preserve the approved all-expression compatibility rule. The corrected planner view applies
throughout the pipeline. The entity-wrapper regression failed before this correction; nested
array/list registration tests failed before container traversal was corrected.

- Required measurement `anon218-final`: functional 29,660 passed, 234 existing skips, 29,894 total;
  document store 235 passed, zero failures. Combined: 30,129 cases, zero failures. Failure names
  and reasons remain empty, as in `client-logging-primary`.
- Full transport suite: 140 passed, zero failures.
- The additional exact anonymous `AllowTypes` compatibility test passed separately after the
  measurement build. It is not included in that measurement's total.
- Strict isolated Release rebuild: zero errors and five existing Razor trimming warnings.
  Final incremental strict build: zero warnings and zero errors.
- Trimming: 107 product warnings, unchanged. Package compatibility and IDE0290 passed.
- Console sample: 14 successful round trips. Blazor build and trimmed publish passed.
- Strict documentation build, link checks, and word-budget checks passed.
- Slow Firebird comparison: 109 passed, one existing skip, zero failures.
- Full slow SQLite comparison: 27,544 passed, 60 failed, 167 skipped, 27,771 total. All 60
  failures came from the newly configured converter fixture adding InfoCarrier options to the
  plain-EF control. Restricting that configuration to the InfoCarrier side restored the control;
  rerunning the complete affected class with live comparison passed all 60 tests. No other class
  reported comparison failures. The full slow run was not repeated after this fixture correction.
- Final normal converter and exact-permission checks: 61 passed. The final strict test-project
  build and repeated IDE0290 check passed. File hygiene passed across all 24 branch files.

Logs remain under ignored `artifacts/`. No captured SQL is committed.
