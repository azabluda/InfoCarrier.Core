# Trusted anonymous-shape catalog implementation plan

> Execute inline in the existing checkout with `superpowers:executing-plans`.
> Preserve the existing uncommitted capacity investigation in
> `anonymous-shape-implementation.md`.

**Goal:** Prevent client descriptors from consuming shared anonymous-shape emission capacity.

**Architecture:** Only a finite, immutable catalog built from trusted, closed anonymous
types can request emission. Requests perform exact descriptor and runtime-component matching
against already constructed catalog entries. Component permissions remain a separate,
per-execution check. Client response restoration retains its exchange-local original bindings.

**Technology:** .NET 10, existing expression protocol, reflection emission, xUnit.

**Decision:** On 2026-10-08 the owner selected the trusted server catalog over worker isolation.
This explicitly accepts registration of previously automatic arbitrary client projections.

**Scope:** [Issue #212](https://github.com/azabluda/InfoCarrier.Core/issues/212), a prerequisite
of [issue #209](https://github.com/azabluda/InfoCarrier.Core/issues/209) and
[pull request #210](https://github.com/azabluda/InfoCarrier.Core/pull/210).
Ordinary descriptor complexity remains the separate, merged
[pull request #213](https://github.com/azabluda/InfoCarrier.Core/pull/213).

## Contract

- Configuration supplies actual closed anonymous `Type` objects, never received descriptors.
- A dedicated model-aware mapper derives full descriptors from those trusted types.
- Nested anonymous objects, arrays, and generic containers are closed recursively.
- Catalog membership compares token, member names and order, every component descriptor,
  array information, model names, and actual runtime component identities.
- A catalog is immutable after construction. Registrations do not grant component permissions.
- Validate the complete trusted graph and capacity requirements before emission. Reserve all
  missing entries under the factory lock; publish no partially built catalog.
- Keep the existing process emission limit of 4096 and exchange limit of 64.
  The process limit applies to trusted configuration, never new client identities.
- Unknown request shapes fail before emission, including shapes already present in another
  catalog's global cache. Sharing an emitted type does not share catalog membership.
- Request resolution cannot call the emitter. Cached entries still require current component
  permissions and exact runtime component identity.
- Server execution never accepts exchange-local mapper bindings as catalog registrations.
  Client restoration may use those bindings, but cannot emit a replacement without a catalog.
- Application startup should build and validate its catalog before serving requests. A custom
  server can pass the same immutable catalog into each serializer pipeline. The in-process
  server must consume trusted application services, not transport data, to obtain its catalog.
- The catalog is bound to the model used to construct its descriptors. Do not share a
  model-bound catalog across different model instances or derive model bindings from payloads.
- No automatic payload registration, trusted-caller token, process-counter reset, weak-cache
  workaround, worker host, general query execution budget, or release work.

## Review focus

1. A shape globally cached by another application remains refused without local registration.
2. Nested mutable input cannot alter a published catalog or emitted response descriptor.
3. A permitted component removed between executions remains refused through cached shapes.
4. Different runtime types with identical names cannot reuse the wrong registered entry.
5. An invalid or oversized trusted catalog cannot publish a partly usable catalog.

## Implementation

### C1: Immutable admission and trusted emission

Files: new `src/InfoCarrier.Core/Expressions/AnonymousShapeCatalog.cs`, existing
`AnonymousShapeTypes.cs`, `TypeNodeResolver.cs`, and owned projection-split tests.

- [x] Write failing owned tests for unregistered shape refusal, cross-catalog cache refusal,
  and thousands of forged identities across independent production resolvers.
- [x] Run the focused tests and record their expected failures before production changes.
- [x] Add `AnonymousShapeCatalog.Create(IModel? model, IEnumerable<Type> shapes)` and
  immutable internal descriptor/component/type entries. Reject open or non-anonymous roots.
- [x] Split trusted batch construction from request resolution. Only batch construction can
  invoke `Emit`; requests match local catalog entries and current component permissions.
- [x] Cover nested arrays/containers, mutation, equality, concurrent reuse, revoked permissions,
  model/runtime identity mismatches, invalid catalogs, and complete registration failure.
- [x] Run focused protocol, capacity, and deserialization tests.

### C2: Server configuration and parity fixtures

Files: `ExpressionSerializer.cs`, `InProcessInfoCarrierServer.cs`,
`InfoCarrierServiceCollectionExtensions.cs`, trusted test utilities, relevant owned query tests.

- [x] Add explicit catalog attachment to serializer creation without deleting shipped overloads.
- [x] Add server application-service registration of immutable catalogs. Preserve actual model
  binding and keep server request restoration separate from client original bindings.
- [x] Configure owned query tests with explicit closed trusted shapes before transport.
- [x] Give inherited specification fixtures a finite manifest derived from trusted compiled
  test code. Do not register descriptors observed in received bytes or dynamically append shapes.
- [x] Pin configured and unconfigured server behavior through the actual in-process transport.
- [x] Compare existing shape statement/result/refusal tests and retain only justified diagnostic
  attribute removals. Catalog registration does not imply translation parity.

### C3: Integration and delivery

- [x] Integrate merged ordinary-descriptor protection while preserving its whole-type allowlist
  semantics and the original uncommitted investigation notes.
- [x] Run regular specification measurement, transport tests, SQLite and Firebird slow tests,
  strict non-incremental Release build, trim ratchet, and Release package validation.
- [x] Document configuration, compatibility, and residual limits in the report and security review.
- [x] Obtain independent review, run file hygiene, commit with the matching rolling-plan step,
  and update the existing pull request with issue traceability and measured validation.
- [x] Stop when completed work awaits continuous integration.

## Evidence

The initial missing-registration test failed because the old resolver emitted an unregistered
shape. It passed after separating trusted catalog construction from request lookup.

The final Release protocol/catalog/capacity and ordinary descriptor run passed 48 tests. The capacity test uses a separate
process with the actual 4096-slot production limit: 8192 forged requests consume no slots; a later
legitimate registration succeeds; an oversized 4094-entry catalog fails with 4093 slots remaining;
four concurrent constructions of the same 4093-entry catalog fill the remaining capacity once;
cached registered callers remain usable at capacity. No counters are reset or lowered.

The independent source review found no confirmed defect. Its requested runtime identity mismatch
assertion was added: a component from another assembly load context cannot use the first catalog's
otherwise matching descriptor. The complete regular and slow suites remain the manifest-completeness
gate; source review alone does not establish that every inherited projection has registration.

HTTP transport passed 30 tests, including configured projection success and explicit missing-catalog
refusal. The strict non-incremental Release build passed with zero errors and the five existing
generated Razor warnings. Clean trim publish reports OURS 107, TOTAL 1140, within the existing 107
baseline. Both shipping packages passed Release compatibility validation without publication.

| Completed run | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Complete regular functional project | 29644 | 0 | 234 | 29878 |
| Complete regular document-store project | 235 | 0 | 0 | 235 |
| Final Release owned protocol/catalog/capacity and descriptor checks | 48 | 0 | 0 | 48 |
| HTTP transport | 30 | 0 | 0 | 30 |
| Complete SQLite live comparison | 27603 | 0 | 167 | 27770 |
| Complete Firebird live comparison | 109 | 0 | 1 | 110 |
| Corrected document-store suite | 235 | 0 | 0 | 235 |

Both slow comparisons used `INFOCARRIER_LIVE_COMPARE=1` and the complete namespace filters.
Neither reported a reason-without-difference failure. The SQLite snapshot preceded the manifest
helper's change from assembly-name selection to the concrete store's actual assembly. That correction
addresses the duplicate assembly-load-context test in the regular suite; it changes no SQLite query,
shape, production behavior, or expected result. Final Release owned checks and Firebird include it.

The first complete regular measurement passed all 29,878 functional cases (29,644 passed, 234
skipped), but three custom document-store tests lacked registration. Their independent fixture now
constructs a catalog from its trusted compiled assembly before accepting requests. All 235 document
cases then passed. The final complete measurement, `eng/measure.sh issue212-catalog-verified
fixture-config-owned-20261007`, reports **FAILING 0, TOTAL 30113**, with no newly fixed or broken
cases and unchanged failure reasons. The final run includes the stronger raw-name/reflection
component checks and every fixture correction.

Two incomplete regular runs identified test-fixture setup errors and were deliberately stopped.
The first omitted closed fixture classes when a context came from a specification assembly.
The second selected assemblies by name and became ambiguous after an assembly-load-context test.
The manifest now starts from the concrete backend store's actual assembly and is computed once
through `Lazy<Type[]>`. It never derives registration from received expressions or response bytes.

The corrected final Release rebuild also passed with zero errors and the same five Razor warnings.
One attempted rebuild while the SQLite slow process held Release files failed with Windows copy
locks. It was rerun after that process exited; this was an execution scheduling error, not a test
or source failure.

## Application configuration

Supply closed anonymous types from a trusted deployment artifact containing the actual query
templates. Shape membership includes the compiler template identity, not just matching member names.
A server-created anonymous literal in a different assembly is not interchangeable with the client
template. Rebuilding the template assembly can require updating the server's catalog. Open generic
templates and wildcards are deliberately refused.

Default assembly load contexts provide portable module/build identities. Independently created
non-default load contexts receive process-local scope tokens, so loading the same binary in separate
processes does not reproduce their template identity. The catalog does not add cross-process
load-context negotiation. The owned load-context tests verify isolation and runtime mismatch refusal,
not interoperability between independently created contexts.

An application with an already known model can call:

```csharp
var catalog = AnonymousShapeCatalog.Create(serverModel, SharedQueryManifest.ClosedAnonymousTypes);
services.AddInfoCarrierAnonymousShapes(catalog);
```

When the application obtains its model from dependency injection, it can construct the singleton
through the actual context and force construction before accepting requests:

```csharp
builder.Services.AddSingleton<AnonymousShapeCatalog>(provider =>
{
    using var scope = provider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<MyServerContext>();
    return AnonymousShapeCatalog.Create(context.Model, SharedQueryManifest.ClosedAnonymousTypes);
});
var app = builder.Build();
_ = app.Services.GetRequiredService<AnonymousShapeCatalog>();
```

`SharedQueryManifest` and `MyServerContext` represent application-owned declarations, not provider
APIs. The manifest is fixed trusted configuration, never an append operation driven by requests.
Use one complete catalog per actual model instance; combine trusted template sets before construction.
For a custom server, pass the catalog to the four-argument `ExpressionSerializer.CreateForModel`
overload or attach it with `UseAnonymousShapeCatalog`. Existing overloads remain available and refuse
anonymous shape reconstruction without registration. Model-independent primitive shapes may use a
null model. Catalog registration does not replace the existing client and server component-type
registrations.

## Limits and attribution

This closes client-driven generated-type capacity exhaustion. Trusted configuration can still fill
the finite process budget, and generated types remain strongly cached for stable provider identity.
Ordinary runtime type construction and evaluation of permitted application/framework methods retain
their separate resource risks. No worker isolation, general query budget, rate limit, release, or
additional method permission is introduced.

The existing shape-array descriptor carries rank but not the distinction between a vector and a
rank-one non-vector array. Rank-one non-vector arrays containing anonymous elements are not supported
by this protocol. Catalog runtime-component matching refuses a differently reconstructed array type;
this work does not claim support for every reflection-constructed CLR array form.

The catalog changes admission and availability, not backend translation. It removes no additional
`InfoCarrierXxx` attribution. Complete regular and slow comparisons retain the existing shape
implementation's single justified attribute removal with explicit fixture registration.

Local evidence remains ignored under `artifacts/`: `measure/issue212-catalog-verified.*`,
`issue212-owned-final.log`, `issue212-slow-sqlite.log`, `issue212-slow-firebird.log`,
`issue212-release-verified.log`, `issue212-trim.log`, and the two `issue212-pack-*.log` files.
The transport gate passed 30 tests. Independent source review found no confirmed defect.
