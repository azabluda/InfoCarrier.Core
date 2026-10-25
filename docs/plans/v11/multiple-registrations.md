# Multiple clients and server endpoints

Date: 2026-10-25  
Issue: [#105](https://github.com/azabluda/InfoCarrier.Core/issues/105)  
Status: selection rules approved by the owner on 2026-10-25; implementation in progress

## Delivery boundary

Use one feature branch and one pull request for this issue. The pull request starts with this
plan, then receives implementation, necessary obsolete-code cleanup, tests, and documentation.
It remains incomplete until the issue's acceptance criteria pass. Do not merge without the
owner's explicit instruction. Stop each pushed iteration when continuous integration is pending.

Keep the ordinary application unchanged: one unnamed client, standard Entity Framework context
registration with `UseInfoCarrier()`, one unnamed server, and `MapInfoCarrier()`.
Names and retained server builders are optional selection tools for applications that need them.

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

Proposed additions are named `AddInfoCarrierClient` and `UseInfoCarrier(clientName: ...)`, named
`AddInfoCarrierServer<TContext>`, server-handle and server-name endpoint mapping, and unambiguous
typed mapping. Preserve the existing route-pattern overload. Select exact parameter order and names
with compile-time examples so a route string cannot be mistaken for a registration name.

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

No child service providers, endpoint containers, or hidden scopes are permitted. Preserve existing
request and transaction scopes. Verify each host-owned resource is disposed exactly once and that
explicitly supplied resources retain their documented ownership. Check active transaction cleanup.

Client and endpoint metrics need bounded registration identity labels. Propose `infocarrier.client.name`
and `infocarrier.endpoint.name`, using configured labels and a stable default label. Never use raw
request paths, caller identities, transaction tokens, or full URLs as tag values. Check existing meter
conventions before fixing names. Handles and typed mapping need stable labels without public names.

The strongest case against this machinery is additional activation and lifetime complexity when
explicit client instances already work. Keep the permanent addition limited to catalog lookup,
descriptor activation, mapping selection, and metric identity. Add no concept to ordinary application
configuration. Retain ordinary `ConfigureShared` methods and separate client/server store facts.

## Bounded implementation sequence

- [x] P0: Reconcile the merged baseline, record simultaneous participation, and propose selection rules.
- [ ] P1: Finalize selection signatures and failure rules with compile-time usage examples. Trace all
  policy carriers and ownership paths. Add focused default coexistence tests in both registration orders.
  Selection rules are approved and default coexistence tests pass. Exact overload checks and the
  complete ownership audit remain open; do not mark the whole step complete yet.
- [ ] P2: Implement client catalogs, isolated HTTP configurations, and named attachment. Document named
  clients with standard context registrations and honest same-context custom-factory examples.
- [ ] P3: Implement named server registration, descriptor activation, handle/name mapping, and typed
  ambiguity checks. Include custom scoped context selection where necessary. Update endpoint guidance.
- [ ] P4: Verify policy and cache isolation through concurrent stored-data tests. Cover shared context
  types, pooling, compiled queries, mapper identity, streaming, limits, caller identity, SQL grants,
  document behavior, logging grants, and wrong-endpoint transaction rejection.
- [ ] P5: Add bounded metric tags and lifetime/disposal coverage. Run final validation and reconcile
  documentation, obsolete code, issue acceptance, and the pull-request description.

Each step is a new commit on the same branch. Update its checkbox with the work. Cleanup is limited
to paths replaced by this feature; the legacy cleanup already merged is not another task.

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
