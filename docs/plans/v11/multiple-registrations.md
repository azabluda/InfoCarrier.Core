# Multiple clients and server endpoints

Date: 2026-10-25  
Issue: [#105](https://github.com/azabluda/InfoCarrier.Core/issues/105)  
Status: registration isolation implemented and validated; extension-point reconciliation remains open

## Delivery boundary

Use one feature branch and one pull request for this issue. The pull request starts with this
plan, then receives implementation, necessary obsolete-code cleanup, tests, and documentation.
It remains incomplete until the issue's acceptance criteria pass. Do not merge without the
owner's explicit instruction. Stop each pushed iteration when continuous integration is pending.

Keep the ordinary application unchanged: one unnamed client, standard Entity Framework context
registration with `UseInfoCarrier()`, one unnamed server, and `MapInfoCarrier()`.
Names and retained server builders are optional selection tools for applications that need them.

## Extension-point audit and reconciliation, 2026-10-25

The owner requested this audit after reviewing hardcoded activation dependencies. The existing
acceptance results establish isolation for the built-in implementations. They do not establish
consistent replacement through public interfaces. The earlier completion statements are superseded
by this open work. This section proposes the correction boundary; exact signatures and implementation
are not yet approved or implemented.

### Verified restrictions

Source references below use repository-relative paths and member names so they survive line changes.

| Public boundary | Current construction and replacement behavior | Required reconciliation |
|---|---|---|
| `IInfoCarrierSerializer` | `InfoCarrierRoleServiceCollectionExtensions.CreateClient` constructs `SystemTextJsonInfoCarrierSerializer`. `InfoCarrierServerActivation` does the same. The unnamed HTTP mapping resolves the public service, while named, typed, and handle mappings retain the activation's serializer. | One registration-specific replacement mechanism must apply consistently to all selection forms and both client/server configuration. |
| `IInfoCarrierTransport` | `CreateClient` constructs `HttpInfoCarrierTransport`. Manual `TransportInfoCarrierClient` construction accepts a custom transport, but `AddInfoCarrierClient` returns `IHttpClientBuilder` and always provisions HTTP. | Separate transport-independent registration from the HTTP convenience path. A custom transport must retain named selection and deployment configuration without creating an unused HTTP client. |
| `IInfoCarrierClient` | `UseInfoCarrier(client, configure)` supports explicit custom clients. The named catalog has no public custom-client factory path. | Decide the factory boundary for custom clients and decorators without losing descriptor policy, activation identity, or measurement ownership. |
| `IInfoCarrierServer` | `InfoCarrierServerActivation` constructs concrete `InProcessInfoCarrierServer`. Unnamed mapping permits a public-service replacement; selected mappings do not. The activation's `Server` property is concrete. | Define whether custom servers and decorators are supported by registration, then use that decision consistently across default, name, handle, and type selection. |
| `IExpressionSerializer` and `IDynamicValueMapper` | Client provider services register these through Entity Framework's internal provider. The server constructs `ExpressionSerializer.CreateForModel`; save execution casts its mapper to concrete `DynamicValueMapper`. | A host service override cannot replace both pipelines. Preserve model, admission, anonymous-shape, and mapper invariants while deciding the supported model-aware pipeline boundary. |
| Streaming and wire encoding | `TransportInfoCarrierClient` uses `QueryStreamFrameCodec`, concrete JSON row serialization, and a concrete serializer check for streamed response limits. HTTP mapping writes fixed JSON frames and uses a concrete server check for streaming. | Serializer substitution must not imply arbitrary wire-format replacement. Define envelope, row, and frame ownership separately; preserve limit enforcement and streaming for supported replacements. |
| Mappers and caller identity | Descriptor-local `AddValueMappers` and the server builder's caller factory already provide configuration boundaries. Server fallback lookups also exist for direct construction. | Retain first-claim-wins mapper order and caller isolation. Do not describe unrelated host registrations as per-registration replacements. |
| Shared infrastructure | Logger services, HTTP context access, host scopes, and the optional host clock remain ordinary services. | Keep these host-owned; replacement work does not require making every internal implementation independently selectable. |

Relevant files: `src/InfoCarrier.Core/InfoCarrierRoleServiceCollectionExtensions.cs`,
`InfoCarrierClientRegistrationCatalog.cs`, `InfoCarrierServerRegistrationCatalog.cs`,
`InfoCarrierServiceCollectionExtensions.cs`, `InfoCarrierOptionsExtension.cs`,
`InProcessInfoCarrierServer.cs`, `ExpressionSerializer.cs`, `TransportInfoCarrierClient.cs`, and
`src/InfoCarrier.Core.AspNetCore/InfoCarrierEndpointExtensions.cs`.

The wire serializer is distinct from expression translation and dynamic-value mapping. A custom
gRPC adapter can implement the existing transport interface, but it needs corresponding server
hosting. `MapInfoCarrier` remains an HTTP adapter. This feature need not implement gRPC itself;
it must let an adapter integrate without abandoning registration selection and policy ownership.

### Proposed replacement and ownership rules

- Keep complete immutable descriptors and opaque activation identities. Store supported factory
  choices in the descriptor; do not scatter serializer, transport, and policy under unrelated keys.
- Use the same activation path for unnamed, named, typed, and handle selection. A documented
  replacement must not be silently ignored by one selection form. Define how existing unkeyed
  overrides interact with the new path before changing their behavior.
- Factories receive the host provider and the immutable configuration they need. Long-lived
  activations must not capture scoped services. Operation-specific pipelines receive the actual
  context/model and operate within the existing host operation or transaction scope.
- Separate factories that create activation-owned objects from factories that return host-owned
  services. Define this distinction explicitly before selecting signatures. Activation-created
  disposable dependencies require exactly-once cleanup, including partial construction failure;
  objects resolved from the host must not be disposed again by an activation.
- Current client cleanup disposes only its HTTP wrapper. Current server cleanup disposes only its
  concrete server. Neither is a general ownership contract for custom disposable serializers,
  transports, clients, or decorators. Test aliasing, asynchronous cleanup, and constructor failure.
- Keep byte limits and admission policy effective for supported custom components. Move enforcement
  to a component-independent boundary where possible; otherwise reject unsupported combinations
  clearly. Do not silently drop limits because a serializer is not the built-in concrete type.
- Preserve metrics without double counting when replacing clients, transports, or decorators.
  Define which layer measures one operation, including streaming completion and cancellation.
- Shared ordinary configuration methods may choose matching components on both ends. Document
  declarations, registration, hosting, selection, and disposal together. They do not prove agreement
  between separately deployed applications; no handshake is added.
- Keep built-in HTTP/JSON defaults short. Advanced replacement must use additive APIs rather than
  forcing names, factories, or custom containers on the ordinary application.

### Bounded correction plan

1. Reconcile intended public contracts with the inventory above. Choose factory and ownership
   signatures, including whole-client/server decoration and the model-aware pipeline boundary.
   Compile representative ordinary, named, manual, same-host client/server, and adapter declarations
   before implementing new public overloads. Do not claim every public interface is fully replaceable.
2. Implement registration-local replacement through a single activation path. Retain the existing
   isolation, snapshot, cache, caller, and transaction rules. Keep transport-independent registration
   separate from HTTP setup. Review whether a dedicated frame/streaming contract is required.
3. Prove substitution with recording/decorating serializers and an in-process custom transport,
   rather than adding a gRPC dependency. Test both ends, two distinct named implementations, all
   selection forms, stored data, cancellation, limits, metrics, disposal, and construction failures.
   Test custom server streaming behavior explicitly instead of depending on a concrete type check.
4. Update user documentation alongside implementation, including complete advanced composition
   examples and explicit wire-format limits. Repeat the repository checks required by those changes.

This audit/plan iteration changes no public signatures or runtime behavior. Registration isolation
is retained. The pull request and issue remain incomplete until this reconciliation is resolved.

## Verified starting point

On 2026-10-25, after `git fetch --prune`, local `main` and `origin/main` both resolve to
`25e476c0046e8af59f13bfc7d52409ad70e4c838`.
[Pull request #253](https://github.com/azabluda/InfoCarrier.Core/pull/253) supplied default
configuration. [Pull request #255](https://github.com/azabluda/InfoCarrier.Core/pull/255)
subsequently removed the legacy configuration methods and marker-policy paths. Do not plan
those removals again. The current configuration design records that cleanup.

Roslyn CodeLens inspection confirms these boundaries:

| Source | Current behavior | Required extension |
|---|---|---|
| `InfoCarrierRoleServiceCollectionExtensions.AddInfoCarrierClient` | Replaces the default descriptor and client; uses one private HTTP name | Client catalog, named selection, private HTTP configuration per identity |
| `AddInfoCarrierServer<TContext>` | Retains descriptors and selects the latest unnamed descriptor as default | Named registrations and activation of every selected descriptor |
| `InfoCarrierServerRegistrationStore` | Copies the catalog dictionary at activation | Preserve snapshot isolation; permit named-only catalogs without an invented default |
| `InfoCarrierServerBuilder` | Retains its service collection, catalog, and registration identity | Validate handle ownership when mapping |
| `InfoCarrierEndpointExtensions.MapInfoCarrier` | Validates and resolves unkeyed default server and serializer, including streaming requests | Select and retain one matching server/serializer pair per mapped endpoint |
| `InfoCarrierDbContextOptionsBuilderExtensions.UseInfoCarrier` | Immediately resolves the application provider's default client, then attaches it explicitly | Named lookup through the same attachment path |
| `InfoCarrierRoleServiceCollectionExtensions.CreateClient` | Creates a client-local serializer and transport from the descriptor | Preserve this isolation during named activation |
| `InfoCarrierDatabase.CompileQuery` | Resolves client and serializer from the executing query context | Preserve execution-time resolution across cache reuse |

The current public surface contains no named registration, named attachment, or typed endpoint
mapping overloads. Existing descriptors are a foundation, not completed multi-registration support.
The historical sections of the configuration design are not a specification for restoring removed APIs.

## Simultaneous client and server participation

A host can accept incoming InfoCarrier requests and make outgoing InfoCarrier requests at the same
time. This is composition of the two existing kinds of registration, not a third role or a relay
registration API. Demonstrating that this follows naturally from the design is an acceptance goal.

For example, A calls B's incoming endpoint. B serves its local database and uses a separate outgoing
client to read or update C through application logic. B must not need a process-wide client/server
mode. One unnamed client and one unnamed server must coexist in either registration order.
The same label, such as `orders`, can independently name B's incoming server and outgoing client.

The source supports this direction: client construction owns its serializer and transport, while
server registration owns incoming policy. Client registration replaces `IInfoCarrierClient`;
server registration replaces `IInfoCarrierServer` and its incoming serializer. These are distinct
service types. This is source evidence for the composition boundary, not proof of every runtime path.

Trace both directions through activation, mapper configuration, cached delegates, scopes, disposal,
logging, caller identity, and metrics. Incoming limits and admission grants must not become outgoing
policy. Outgoing response limits must not configure incoming serialization. Shared ordinary methods
may configure both descriptors without joining their identities or mutable state.

Acceptance requires real incoming and outgoing operations in one host, in both registration orders,
with independent policy and stored-data assertions. Include concurrent execution. Identity and
sensitive-log grants do not propagate automatically. Transactions remain owned by the server that
minted them.

A transparent relay is stronger: B hosts an InfoCarrier-backed context that itself talks to C.
That needs separate evidence for query translation, saves, streaming, cancellation, and transaction
semantics. This issue does not promise transparent forwarding, arbitrary-length chains, automatic
identity propagation, token forwarding, or distributed transactions. Record any discovered relay
limitations explicitly. Successful coexistence alone must not be described as transparent relay support.

## Approved selection contract

The owner approved these rules on 2026-10-25, explicitly confirming that default selection means
the unnamed registration. Exact overload signatures still require compile-time checks during implementation.

| Decision | Approved behavior |
|---|---|
| Name comparison | Ordinal, case-sensitive; reject null, empty, or whitespace-only names on named overloads |
| Duplicate names | Reject during registration within the same client or server catalog; no hidden composition |
| Client/server namespaces | Independent; the same public label can exist once in each catalog |
| Named registration | Creates a fresh descriptor and identity; never changes the default |
| Unnamed client registration | Retain replacement behavior; each descriptor gets a private HTTP name so old handler callbacks cannot compose accidentally |
| Unnamed server registration | Retain descriptor history and latest default selection for compatibility with the merged foundation |
| Default client attachment | Uses only the explicit unnamed default; missing default fails even if exactly one named client exists |
| Unnamed endpoint mapping | Uses the explicit unnamed default; named-only configuration fails clearly and does not pick a registration by count |
| Multiple servers | An explicit unnamed default remains deterministic; named registrations cannot replace it. Type-only mapping requires exactly one matching descriptor |
| Missing names | Fail with the requested name and the required registration action; never fall back |
| Server handles | Must belong to the mapping host's catalog snapshot; reject foreign or missing identities during mapping |
| Configuration timing | Fresh options, synchronous callbacks, immutable descriptor snapshots; registration order before provider construction cannot change named selection |

The issue's earlier startup-ambiguity requirement needs this explicit reconciliation: selecting an
explicit default is not ambiguous. Mapping by type with multiple matches is ambiguous and must fail
during endpoint setup. Do not silently change the current latest-unnamed default rule.

### Coexistence evidence, 2026-10-25

`ClientServerCoexistenceTest` exercises the existing default APIs through real HTTP endpoint handlers.
The middle host registers a local SQLite context and incoming server, plus an outgoing default client
connected to a second host. The test runs in both registration orders. Incoming and outgoing operations
use the same context type and query shape, insert and update concurrently, assert save counts and
tracked entity state, and independently inspect both SQLite databases. Different incoming request
and outgoing response limits remain on their respective descriptors. No production changes were needed.

This proves basic default registration coexistence without a relay API. Mapper, caller identity,
logging, transaction, and metric isolation still require the later acceptance checks. It does not
claim transparent relay support or prove all named-registration paths.

Named `AddInfoCarrierClient` and `UseInfoCarrier(clientName: ...)` are implemented in P2, with
typed and untyped attachment checks. P3 adds `AddInfoCarrierServer<TContext>(string serverName, ...)`,
`MapInfoCarrier(string pattern, string serverName)`, `MapInfoCarrier(InfoCarrierServerBuilder server,
string pattern = "infocarrier")`, and `MapInfoCarrier<TContext>(string pattern = "infocarrier")`.
The existing single-string overload remains a route pattern. A named mapping requires both strings.

Do not add a client-handle API unless named selection and explicit `UseInfoCarrier(client)` prove
insufficient. Do not add named Entity Framework factory syntax. Two uses of the same context type
need independent options and distinguishable custom factories or keyed application registrations.
For servers, add a scoped context-resolver overload only where standard concrete-context resolution
cannot select the intended instance. Define context ownership before accepting such a factory.

## Activation and ownership

Maintain separate client and server catalogs. Names map to opaque identities. Each immutable
descriptor owns all policy and the factories needed for activation. Keyed dependency injection can
expose a final selection boundary; it must not scatter descriptor state across unrelated keys.

Allocate a private HTTP name from the client identity, including unnamed replacements. Return the
matching `IHttpClientBuilder` so application handlers configure only that registration. Avoid collisions
with application HTTP names. Do not add named options infrastructure merely to store descriptors.

Activate each server with its matching serializer, deployment snapshot, context resolver, request
limit, document behavior, transaction timeout, caller policy, and logging grants. Both buffered and
streaming endpoint paths must use that selected pair. Runtime catalogs must copy registration data.
Shared HTTP context access, logger infrastructure, and HTTP client factories remain host-owned.

### Initial ownership trace, 2026-10-25

| Carrier | Verified source boundary | Multi-registration follow-up |
|---|---|---|
| Outgoing serializer and transport | `CreateClient` constructs both from the client descriptor, not the host's incoming serializer | Retain one activation per selected client identity |
| Client mapper registration | `InfoCarrierOptionsExtension.ApplyServices` installs mapper instances into Entity Framework's internal provider | Preserve mapper-aware provider identity and test cache reuse with matching configurations |
| Incoming mapper and admission policy | Registration-backed `InProcessInfoCarrierServer` reads its deployment snapshot | Keep every activated endpoint on its own descriptor |
| Caller identity | `AddHttpCallerIdentity` stores a factory on the server builder; the factory uses the shared HTTP context accessor | Avoid a host-wide replacement policy; test different selectors on separate endpoints |
| Server transactions | Each server instance owns its transaction registry; `DisposeAsync` stops its timer and discards open transactions | Ensure host disposal reaches every activated server exactly once |
| Log capture | Server construction receives host-owned capture infrastructure; forwarding grants come from its registration | Test independent grants during concurrent incoming and outgoing operations |
| Metrics | `RoundTripCompleted` currently accepts operation, duration, and error type only | Carry descriptor labels explicitly; do not read an ambient current registration |

The existing server constructor may create a short scope to build an anonymous-shape catalog from
the registered context's model. Do not confuse that existing startup behavior with permission to
introduce child containers or new endpoint lifetime scopes. Custom context-factory ownership and
named activation disposal still need explicit implementation review.

P3 uses one keyed, host-owned server activation per opaque identity. It retains the matching
server and serializer. The unkeyed default server is a non-owning interface facade over that
activation, so dependency injection does not dispose one server through multiple owning aliases.
Default endpoint mapping unwraps this facade to preserve streaming; manually supplied server and
serializer services remain available through the low-level default mapping path.

The named custom-context overload takes the name, configuration callback, then a typed resolver.
Requiring the configuration argument avoids ambiguity between two single-argument callbacks.
The resolver selects a host-owned scoped context, usually a keyed scoped registration. InfoCarrier
disposes the existing operation or transaction scope, not a second context owner. Tests inspect
actual stores and context disposal counts, including host shutdown with transactions still open.

Transaction commit against a different endpoint fails. Existing rollback remains idempotent when
the token is absent from that endpoint; it does not end the owning endpoint's transaction.
This preserves existing transaction semantics rather than introducing forwarding or distributed work.

The P1 source review also confirms that `CompileQuery` still obtains client and serializer from the
executing query context. `ExtensionInfo.GetServiceProviderHashCode` and `ShouldUseSameServiceProvider`
retain ordered mapper-instance identity. Server query and save executors build expression serializers
from the selected server's mapper and admission snapshot. The new catalogs, activations, and default
facade introduce no process-wide client/server mode or ambient registration selection. Host logging
capture and HTTP context access remain shared infrastructure; caller selectors and forwarding grants
remain in each server descriptor. At P1, metrics still lacked registration tags; P5 completes them below.

No child service providers, endpoint containers, or hidden scopes are permitted. Preserve existing
request and transaction scopes. Verify each host-owned resource is disposed exactly once and that
explicitly supplied resources retain their documented ownership. Check active transaction cleanup.

Client and endpoint metrics use bounded registration identity labels: `infocarrier.client.name`
and `infocarrier.endpoint.name`, using configured labels. P5 uses an empty name for unnamed registrations
and adds `infocarrier.client.registration` / `infocarrier.endpoint.registration`, numbered from one
within each host's corresponding catalog. This distinguishes retained unnamed handles and named
registrations called `default` without reserving an application name. Direct clients and low-level
mappings without registration descriptors omit these labels. The numbers describe configuration
order; opaque service identities remain separate. Never use raw
request paths, caller identities, transaction tokens, or full URLs as tag values. Check existing meter
conventions before fixing names. Handles and typed mapping need stable labels without public names.

The strongest case against this machinery is additional activation and lifetime complexity when
explicit client instances already work. Keep the permanent addition limited to catalog lookup,
descriptor activation, mapping selection, and metric identity. Add no concept to ordinary application
configuration. Retain ordinary `ConfigureShared` methods and separate client/server store facts.

## Bounded implementation sequence

- [x] P0: Reconcile the merged baseline, record simultaneous participation, and propose selection rules.
- [x] P1: Finalize selection signatures and failure rules with compile-time usage examples. Trace all
  policy carriers and ownership paths. Add focused default coexistence tests in both registration orders.
  Selection rules are approved, default coexistence tests pass, and named/handle/type calls compile
  in P2/P3 tests. The source trace above records policy and ownership boundaries. The remaining
  behavior matrix, including adversarial mapper/cache and forwarding checks, stays in P4/P5.
- [x] P2: Implement client catalogs, isolated HTTP configurations, and named attachment. Document named
  clients with standard context registrations and honest same-context custom-factory examples.
  Implemented named overloads are `AddInfoCarrierClient(string clientName, Action<InfoCarrierClientOptions>)`
  and typed/untyped `UseInfoCarrier(string clientName)`. Named client tests compile these calls
  through all four standard context registration methods and independent keyed application factories.
  Client activations use opaque identity keys, snapshot name lookup, and unique private HTTP names.
  The host disposes each activated HTTP wrapper; the factory retains pooled handler ownership.
  Named checks, the full transport and specification suites, Release rebuild, package validation,
  clean trimming, documentation, and file hygiene passed on 2026-10-25. Specification failure names
  and reasons are unchanged from the coexistence measurement. Remaining mapper/cache, metric,
  and combined named-client/server acceptance checks stay in P4 and P5.
- [x] P3: Implement named server registration, descriptor activation, handle/name mapping, and typed
  ambiguity checks. Include custom scoped context selection where necessary. Update endpoint guidance.
  The overloads and documentation are implemented. Focused tests cover same-type stores, distinct SQL
  grants, independent request limits, startup selection errors, context disposal, and transaction ownership.
  Full specification comparison and transport tests, Release rebuild, package validation, clean
  trimming, documentation, and file hygiene passed on 2026-10-25. Specification failure names and
  reasons are unchanged from the named-client measurement.
- [x] P4: Verify policy and cache isolation through concurrent stored-data tests. Cover shared context
  types, pooling, compiled queries, mapper identity, streaming, limits, caller identity, SQL grants,
  document behavior, logging grants, and wrong-endpoint transaction rejection.
  P4a adds `NamedRegistrationCacheIsolationTest`: matching mapper instances must share the actual
  Entity Framework compiled-query cache, while different mapper instances must not. Scoped serializers
  remain distinct. The matrix covers pooled and ordinary factories, both client/server registration
  orders, explicitly compiled delegates, normal query-cache reuse, mapped predicates/projections,
  streaming enumeration, tracking, save counts, and independently read SQLite data. A middle host
  accepts incoming operations while its named clients call two upstream endpoints. Client and server
  both use the public label `orders` without joining their policies. The same workload also runs
  directly against SQLite as a control. Explicitly compiled delegates are shared only when models
  match, respecting Entity Framework's existing model restriction. Mapper counters must increase in
  every case; entity materialization alone does not establish that the mapper seam was exercised.
  Fixed mapper configurations are reused across theory cases to avoid creating unrelated internal
  providers. Explicit test pools are owned and disposed by the test factories.
  P4b adds `NamedEndpointPolicyIsolationTest`: concurrent transactions use different caller selectors
  on named endpoints, and buffered/streamed queries retain independent forwarding grants, including
  the sensitive-data grant. The positive streaming cases first failed because an async iterator's
  yield restored the caller's execution context and lost the active log-capture sink. Capture now
  surrounds each iterator advance and disposal, closes before yielding a frame, and forwards permitted
  events after enumeration. Rows remain streamed. The regression checks passed after this fix.
  On 2026-10-25, the focused checks, full transport suite, specification comparison, Release rebuild,
  clean trimming, documentation, and file hygiene passed. Specification failure names and reasons
  are unchanged from the server-selection measurement.
  P4c adds independent allowed-type and anonymous-shape admission, streamed request limits, and named
  response limits for both one large row and many small rows. These checks exposed missing limits
  on stream decoding and framed query responses. A per-decode, leave-open read wrapper now counts
  the complete incoming payload, including framing, and rejects the first read exceeding its limit.
  Exact-boundary tests verify equality is accepted, overflow fails, cancellation is preserved, and
  caller-owned streams remain open. A synchronized command test cancels one named request while a
  second endpoint continues, then verifies that the cancelled registration accepts its next query.
  `NamedDocumentPolicyTest` updates attached stubs concurrently through MongoDB and InMemory endpoints
  using the same context type and both registration orders. Independent store reads verify saved values
  and retained owned data. A materialization probe verifies that the InMemory endpoint does not perform
  document-repair reads; an independent full read proves the probe observes stored owned data.
  Both clients use non-relational query rules, while only MongoDB enables document repair.
  The final P4 checks passed on 2026-10-25: full specification comparison, transport suite, Release
  rebuild, clean trimming, documentation checks, and file hygiene. Specification failure names and
  reasons remain unchanged from P4b. No public signatures changed in this increment.
- [x] P5: Add bounded metric tags and lifetime/disposal coverage. Run final validation and reconcile
  documentation, obsolete code, issue acceptance, and the pull-request description.
  Review both buffered and streamed transport paths: named labels alone are insufficient if streamed
  operations bypass the existing round-trip instruments. Verify successful, failed, cancelled, and
  early-disposed enumeration, with one measurement per actual request and no registration-policy
  lookup through ambient state. Keep meter-listener tests isolated from parallel test traffic.
  Labels must also distinguish retained unnamed server registrations selected by handles, without
  colliding with valid public names such as `default`. Cardinality must follow configured registrations.
  P5a implements the labels above and separate `infocarrier.server.requests` and
  `infocarrier.server.request.duration` instruments. Client measurements cover buffered attempts and
  streamed responses through response disposal. Each stream shares one measured reader between the
  frame and aggregate APIs. An unstarted enumerator sends and measures nothing; early disposal records
  `incomplete`. Failures and cancellation retain exception-type tags. Both instrument families carry
  `infocarrier.outcome`, with bounded values `success`, `error`, `cancelled`, and `incomplete`.
  Endpoint measurements cover envelope rejection, protocol/operation refusal, returned faults,
  streamed faults, cancellation, and response writing. Unknown operation values use one `Unknown` tag.
  Labels travel in immutable descriptors and captured endpoint selections, never ambient state.
  `RegistrationMetricsTest` verifies same-host incoming/outgoing traffic in both registration orders,
  named/default/retained-unnamed selection, stored saves, synchronous/asynchronous faults, malformed
  and rejected requests, streamed failure boundaries, early disposal, and one matching counter/histogram
  pair per attempt. Meter tests remain in the collection that excludes parallel test traffic.
  The synchronized cancellation test also verifies one cancelled client/server pair and successful
  measurements for the unaffected endpoint and the cancelled registration's next query.
  Fault observation resolves only an already-loaded exception type, with a fixed fallback for unknown
  names. It does not reconstruct exceptions or execute their constructors. A custom-constructor probe
  failed with two calls before this correction and passes with one, the consumer's reconstruction.
  The lifetime review confirms one keyed activation owns each server, the default facade is non-owning,
  operation/transaction scopes own contexts, and server disposal closes outstanding transactions.
  The client lifetime test now resolves every named/default wrapper across scopes and checks host disposal.
  Final-source validation passed on 2026-10-25: the full specification comparison, full transport suite,
  slow SQLite and Firebird comparisons, Release rebuild, package validation, clean trimming, strict
  documentation, links, word budgets, file hygiene, and whitespace checks. Specification failure names
  and reasons remain unchanged from P4. The subsequent owner-requested audit is recorded below.
  The implementation and local acceptance matrix are complete. Keep the issue open and pull request
  draft for owner review and continuous integration; merging still requires explicit owner authorization.

Each step is a new commit on the same branch. Update its checkbox with the work. Cleanup is limited
to paths replaced by this feature; the legacy cleanup already merged is not another task.

## Post-implementation obsolete-code audit, 2026-10-25

Reviewed the pull request's runtime changes and adjacent configuration, activation, serializer,
query, mapping, and lifecycle dependencies. Compiler unused-symbol results were candidate lists,
checked against concrete and interface references, registration source, reflection, generated calls,
and the supported low-level API. Dependency-injection scan misses were checked against the actual
`EntityFrameworkServicesBuilder.TryAdd` chains and logger-provider registrations.

Removed confirmed leftovers:

- `InfoCarrierOptionsExtension.WithAllowedTypes`, `WithRegisteredAnonymousShapes`, and
  `WithArbitrarySqlExecution`: unused mutation routes superseded by the deployment snapshot path.
- `AnonymousShapeSet.ValidatePrototypes`: an unused forwarding adapter after validation moved to
  `InfoCarrierDeploymentBuilder`.
- The two-argument `ExpressionSerializer.CreateForModel` binary-compatibility shim: its documented
  purpose was version 10 compatibility, which is not required for version 11. The optional-argument
  factory and explicit catalog factory remain; two-argument source calls still bind normally.
- The unkeyed `InfoCarrierClientRegistration` service registration: runtime default selection now
  uses the catalog and keyed activation. Its only consumer was a test, now checking the actual
  activated client's descriptor.

Retained framework and supported entry points:

- Entity Framework database/query-context/transaction services, pool reset methods, conventions,
  and relational facade members are called through their framework interfaces.
- Keyed activation disposal, measurement disposal, logging providers, mapper implementations,
  serializer overloads, and synchronous client/server methods satisfy active lifecycle or interface contracts.
- `ClientResultMaterializer.Materialize` and `ClientPropertyReader.Read` are emitted into expression
  trees; `InfoCarrierTypeMapping.Default` is discovered by compiled-model generation.
- Design-time services are discovered by `DesignTimeProviderServicesAttribute` and implement
  `IDesignTimeServices`. The default server facade preserves the public default service contract
  without owning its activation.
- `ExpressionSerializer.UseAnonymousShapeCatalog`, the remaining model factories, and public
  analysis properties remain useful custom-composition or inspection APIs. The absence of internal
  callers does not establish that a supported public API is obsolete.
- The obsolete-marked document-mapping interface still has active convention/database consumers;
  its attribute does not make it dead code. Its replacement is outside this feature's cleanup.

Corrected stale serializer comments about a universally shared internal provider, the old allowed-type
API name, and host-wide mapper resolution. Mapper identity can partition providers; matching mapper
instances can still share them. Server expression serializers receive the selected policy snapshot.
This audit does not claim that every unrelated public API in the repository has external callers.

Cleanup validation exposed a streamed-limit fixture teardown race: the client could finish while
TestServer was still unwinding a request that held its SQLite file open. Host disposal did not join
that handler. A deterministic probe holds a real SQLite connection in a request until explicitly
released; the previous fixture reproduced the same file-lock failure. The fixture now stops the host,
joins tracked request completions, then disposes host services and deletes the files. It uses no
sleep, deletion retry, or swallowed file error. The new probe and limit/admission checks pass.

Cleanup validation passed: normal functional 29,692/0/29,926 and document store 237/0/237
(passed/failed/total), with unchanged specification failure names and reasons. Three consecutive
identical transport runs each passed 276/0/276. Compiled-model checks passed 4/0/4. Release rebuild,
package validation, clean trimming (115 InfoCarrier warnings), strict documentation, links, word
budgets, file hygiene, and whitespace checks passed. Slow SQLite and Firebird results above belong
to parent commit `0edd48f6`; they were not repeated for this deletion-only production cleanup.

## Validation and completion

Start with focused functional and transport checks. The same-context concurrency test must inspect
tracking, reported save results, and independently read stored data. Destination checks alone missed
the serializer capture fixed by [pull request #109](https://github.com/azabluda/InfoCarrier.Core/pull/109).
Exercise identical query shapes across clients with both matching and different mapper configurations,
so separate internal providers cannot conceal a shared-cache defect. Keep a direct-store control
where concurrency results need comparison. Preserve transport-host logging isolation from Windows Event Log.

After product changes, run the full specification measurement through `eng/measure.sh`, the separate
transport suite, a Release rebuild under warnings-as-errors, package validation for public signature
changes, and a clean trim check. Plan slow SQLite and Firebird comparisons for the completed feature;
investigate differences under the existing test policy rather than converting reports into snapshots.
Verify the samples after relevant configuration changes.

Run strict documentation checks, links, word budgets, changed-file hygiene, and `git diff --check`.
Apply the humanizer skill before editing README, package guidance, or website files. A plan-only
iteration needs documentation checks, not product suite reruns. Report exact run results and any
remaining limits. No issue completion claim or ready-for-review transition is justified by P0 alone.

Out of scope: agreement protocols, fingerprints, retries, distributed transactions, transparent relay
guarantees, and the separate documentation redesign in
[issue #254](https://github.com/azabluda/InfoCarrier.Core/issues/254).
