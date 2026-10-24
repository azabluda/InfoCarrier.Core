# Configuration role API design

Date: 2026-10-23  
Issue: [#104](https://github.com/azabluda/InfoCarrier.Core/issues/104)  
Release: 11.0  
Status: implemented in pull request #253; parameterless attachment approved 2026-10-24

## Purpose

Version 11 replaces the remaining scattered role configuration with one unnamed client entry point
and one unnamed server entry point. This work builds on the existing shared deployment contract.
It does not replace that contract or duplicate its settings.

The first implementation supports one client and one server endpoint per service provider. Named
clients, named servers, multiple endpoints, typed endpoint mapping, and endpoint metric tags belong
to [issue #105](https://github.com/azabluda/InfoCarrier.Core/issues/105). The unnamed registrations
must leave additive paths for that work.

## Current surface

`InfoCarrierDeploymentBuilder` now owns the four facts shared by both roles:

- anonymous structures;
- extra allowed types;
- custom value mappers;
- arbitrary SQL permission.

The client applies those facts through the existing `UseInfoCarrier` callback. The server applies
them through `AddInfoCarrier`. Standard value mappers are automatic on both roles. Application
mapper instances participate in the Entity Framework service-provider identity, so different
mapper configurations do not share one internal provider.

Five obsolete methods duplicate the shared builder and can be removed directly:

- `InfoCarrierDbContextOptionsBuilder.AllowTypes`;
- `InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution()`;
- `AddInfoCarrierAllowedTypes`;
- `AddInfoCarrierArbitrarySqlExecution`;
- `AddInfoCarrierStandardValueMappers`.

The `IInfoCarrierValueMapper` type forwarder from `InfoCarrier.Core` can also be removed directly.
Version 11 prereleases do not preserve version 10 source or binary compatibility.

Role-specific configuration remains scattered. The server still needs explicit registrations for
the non-generic `DbContext`, `IInfoCarrierSerializer`, and `IInfoCarrierServer`. Payload limits remain
serializer constructor arguments. The endpoint remains a transport constructor argument and a
`MapInfoCarrier` route. Store behavior, transaction timeout, caller identity, and log forwarding use
separate role-specific registrations.

## Reconciled decisions

### Each role owns one shared-builder snapshot

The client and server options derive from `InfoCarrierDeploymentBuilder`. Each role callback therefore
configures shared and role-specific facts on one object. It produces one immutable snapshot owned by
that role registration.

`AllowedTypes`, `AllowArbitrarySqlExecution`, and `ValueMappers` are not redeclared on the role
options. The base builder remains their only state owner. Applications can continue to call one
application-owned extension method from both role callbacks.

The client-only non-relational planning switch and the server-only document repair remain separate
facts. A single `StoreKind` would hide the distinction that Entity Framework InMemory needs only the
first behavior. Version 11 gives the two facts clear role-option names but does not merge them.

### The first public role APIs use default registration descriptors

The server entry point is `AddInfoCarrierServer<TContext>`. It registers the default serializer and
the default in-process server. Its one callback configures the server options and inherited shared
deployment facts.

The client entry point is `AddInfoCarrierClient`. It registers one HTTP client, transport,
serializer, and `IInfoCarrierClient`. Its one callback configures the client options and inherited
shared deployment facts. `UseInfoCarrier` only attaches the configured client to Entity Framework.

Each entry point creates one immutable registration descriptor with an opaque internal identity.
The descriptor owns its options snapshot and factories. Services within one role are created from
that descriptor rather than assembled through a common string key.

The default registration uses the framework's default options name. A later public name maps to one
opaque identity and one descriptor. The initial public API does not expose names, generic endpoint
mapping, or multiple-server selection.

### Server construction uses the host container

`AddInfoCarrierServer<TContext>` stores a context factory in the default server registration. The
server resolves `TContext` from each request or transaction scope through that factory. It does not
add an unkeyed non-generic `DbContext` alias and does not create a child container.

The current non-generic server can receive an internal context resolver. An internal generic adapter
is also acceptable. The public low-level `InProcessInfoCarrierServer` does not need a generic public
replacement for this issue.

The entry point composes the existing host-owned logging registrations. Server log capture is now
an injected instance, and replay receives an injected logger factory. No process-wide logging state
remains for the role API to preserve.

The public low-level server, serializer, transport, and envelope types remain available. Advanced
applications can still assemble them directly. The new entry point supplies the recommended default
composition.

### Log forwarding stays an explicit grant

`InfoCarrierServerOptions.LogForwarding` expresses the existing minimum level and sensitive-data
grant. Enabling sensitive forwarding still requires forwarding to be enabled. The implementation
must preserve the current rule that a sensitive server forwards nothing without the second grant.

The role API does not add startup warnings for explicit security choices. Arbitrary SQL, unlimited
request size, information-level forwarding, and sensitive forwarding are deliberate configuration.
The API validates invalid combinations and values. It does not warn about accepted values.

### No agreement protocol belongs in this issue

Applications can use one shared configuration function for both role options. The server remains
the admission authority when callers configure the two roles differently.

Issue #104 does not add contract fingerprints, a connection handshake, response tokens, or retry
behavior. Such a check would change the wire protocol and needs a separate protocol design.
It also cannot compare mapper behavior from mapper object identity alone.

The endpoint path remains transport configuration. A wrong path remains an HTTP failure, with the
requested address available in the client exception. `MapInfoCarrier` validates that the default
server registration exists during endpoint setup. It cannot prove that a separately deployed client
uses the same path.

### Validation has one deadline

Direct client creation validates before returning. Dependency-injection registrations validate no
later than service-provider validation or endpoint mapping. No invalid configuration waits for the
first query.

Positive byte limits and transaction timeouts remain required. The endpoint must be absolute.
Sensitive forwarding requires a forwarding level. Repeated unnamed registrations compose the
default options in call order without adding duplicate default services.

### Names, identities, and resolution have separate jobs

A public name identifies application configuration. An opaque identity distinguishes registrations
inside InfoCarrier. A registration descriptor activates one complete role. These are separate
concepts and must not share one raw string key throughout the service graph.

Named options compose role configuration. Named HTTP clients configure handlers and connection
lifetime. InfoCarrier prefixes their internal HTTP names, so they cannot collide with application
HTTP clients. Keyed dependency injection is permitted at a final resolution boundary, but it is not
the storage model for role policy.

The server builder is also a registration handle. A later endpoint overload can accept that handle
directly, which prevents a spelling mismatch between registration and mapping. A name-based overload
remains useful when registration and endpoint mapping live in separate modules.

### The common case pays no naming cost

One client and one server use only the unnamed overloads. The application does not see a name,
identity, registry, descriptor, factory, or keyed service:

```csharp
builder.Services.AddInfoCarrierServer<ShopContext>(options =>
    options.ConfigureShared(ShopDeployment.ConfigureApplication));

app.MapInfoCarrier("api/data");
```

```csharp
builder.Services.AddInfoCarrierClient(options => options
    .WithEndpoint("https://api.example.com/api/data")
    .ConfigureShared(ShopDeployment.ConfigureApplication));

builder.Services.AddDbContextFactory<ShopContext>(options => options.UseInfoCarrier());
```

The returned builders expose existing optional composition, such as an HTTP message handler or
caller identity. Ignoring the return value is the normal path.

The Entity Framework provider attachment remains necessary. The descriptor model adds no syntax to
that existing line.

The parameterless attachment was approved on 2026-10-24 after comparison with Npgsql and an executable
prototype. It resolves the default client from the application provider already attached to the
current options builder, then delegates to `UseInfoCarrier(client)`. Resolution occurs before Entity
Framework builds internal services, preserving deployment snapshots and mapper identity. Standard
context, factory, pool, and pooled-factory registrations supply that provider automatically.

Client registration remains independent of context registration. Do not use `ConfigureDbContext<T>`
to attach a client to every registration of one context type. An application can use the same type
with independent remote InfoCarrier and local SQLite options. Custom factories either supply the
application provider before attachment or pass the client explicitly. Entity Framework's ordinary
`AddDbContextFactory` has no name parameter; keyed custom factories need independent options.

Issue #105 can add `UseInfoCarrier(clientName: "orders")` over the descriptor registry. The default
overload must not select an arbitrary named client. A missing provider or client fails during
options configuration with an actionable message; this does not imply eager activation by
`BuildServiceProvider(ValidateOnBuild = true)`.

Only an application with several clients or endpoints supplies a name or retains a registration
handle. The internal descriptor architecture must not add another callback, generic argument, or
resolution call to the unnamed syntax.

## Bounded public shape

### Shared configuration decision, revised 2026-10-24

Role properties remain mutable for configuration binding. Role-specific `With` extensions write
those properties and return the same options object. Shared operations are generic extensions
constrained to `InfoCarrierDeploymentBuilder`, so they preserve the concrete role type.

The revised decision supersedes the earlier recommendation for application-owned generic helpers.
Applications declare ordinary methods taking `InfoCarrierDeploymentBuilder` and returning `void`.
The library-owned `ConfigureShared<TBuilder>(Action<InfoCarrierDeploymentBuilder>)` adapter invokes
the method synchronously, exactly once, on the existing builder, then returns that exact builder.
Shared and role calls can appear in any order without application-side generic adaptation.

```csharp
public static void ConfigureSharedContract(InfoCarrierDeploymentBuilder builder)
    => builder.AddAllowedTypes(typeof(Formatting));

services.AddInfoCarrierClient(o => o
    .WithEndpoint(endpoint)
    .ConfigureShared(ShopDeployment.ConfigureSharedContract)
    .WithMaxResponseBytes(responseLimit));
```

Parameterized methods use `.ConfigureShared(b => ShopDeployment.ConfigureSharedContract(b, settings))`.
Shared methods can compose other ordinary shared methods. No configuration classes, interfaces,
discovery, or extra registration are required. Null receivers and callbacks fail immediately;
callback exceptions propagate. Execution stays at the adapter's position inside the role callback.
There is no queued phase, snapshot creation, or identity change inside the adapter. Existing
ordering applies: scalar assignments retain their existing semantics, and later mapper additions
do not generally override earlier matching mappers. Sharing a method does not prove agreement
between separately deployed applications.

`WithLogForwarding(LogLevel? minimumLevel, bool includeSensitiveData = false)` sets both forwarding
properties. A null level disables forwarding. Every call requires a fresh explicit sensitive-data
grant; an omitted argument clears an earlier grant. Existing snapshot validation rejects sensitive
forwarding without a level. No nested fluent builder or duplicate configuration state is added.

This syntax leaves the descriptor architecture unchanged. Named client and server overloads in
[issue #105](https://github.com/azabluda/InfoCarrier.Core/issues/105) can use the same option types
and ordinary shared methods. Names select registrations; they do not become part of shared configuration.

The implementation may refine names after compile-time API tests. Its boundary is:

```csharp
public sealed class InfoCarrierClientOptions : InfoCarrierDeploymentBuilder
{
    public Uri? Endpoint { get; set; }
    public bool ServerUsesRelationalQueryRules { get; set; }
    public int? MaxResponseBytes { get; set; }
}

public sealed class InfoCarrierServerOptions : InfoCarrierDeploymentBuilder
{
    public bool StoresOwnedDataInsideOwner { get; set; }
    public int? MaxRequestBytes { get; set; }
    public TimeSpan? TransactionIdleTimeout { get; set; }
    public InfoCarrierLogForwardingOptions LogForwarding { get; }
}

public static IHttpClientBuilder AddInfoCarrierClient(
    this IServiceCollection services,
    Action<InfoCarrierClientOptions> configure);

public static InfoCarrierServerBuilder AddInfoCarrierServer<TContext>(
    this IServiceCollection services,
    Action<InfoCarrierServerOptions> configure)
    where TContext : DbContext;
```

`InfoCarrierServerBuilder` is the composition point for caller identity and future protocol-specific
registrations. It carries the opaque registration identity and can become an endpoint handle in
issue #105. It does not expose a name before that issue. An overload may omit the callback when
defaults are sufficient.

The two store booleans above name independent behavior. Implementation review can select shorter
names, but it must not collapse them into one enum.

## Removals and retained extension points

Remove the five obsolete methods and the mapper type forwarder as part of the role API change.
No migration interval or compatibility shim is required on the version 11 prerelease line.

Remove configuration-only marker interfaces after their consumers read immutable role options.
Retain `IInfoCarrierServerCallerIdentity`, because a non-HTTP transport supplies behavior through
that interface. Retain `IInfoCarrierClient`, `IInfoCarrierServer`, `IInfoCarrierSerializer`, and
`IInfoCarrierTransport` as composition seams.

Keep `InfoCarrierOptionsExtension` public until a public-surface and construction audit proves that
external providers do not need it. Its removal is not required for the role APIs.

## Compatibility boundary for issue #105

The unnamed client and server each create one default descriptor. Issue #105 adds overloads that
map an application name to another descriptor. It does not replace the activation model.

Each client descriptor owns its endpoint, serializer limits, shared deployment snapshot, query-rule
behavior, and mapper instances. Each server descriptor owns its context factory, serializer limits,
shared deployment snapshot, document behavior, transaction settings, logging grants, and caller
identity.

Host infrastructure can remain shared when it contains no role policy. The logger provider and
HTTP client factory are examples. Role policy cannot live in an unkeyed mutable singleton or a loose
collection of keyed services.

`MapInfoCarrier` selects the default server descriptor. Issue #105 adds descriptor-handle and
name-based endpoint selection. It can also add typed convenience overloads without changing server
activation.

A typed role marker is optional syntax over the same descriptor registry. It must not introduce a
second registration system. A child service provider is not supported, because it would separate
the endpoint from host logging, configuration, disposal, and scoped services.

Client metrics and server metrics keep their current tags. Issue #105 adds client or endpoint
identity tags when multiple registrations make those identities meaningful.

## Main risks

1. Role options can redeclare inherited shared deployment facts. Compile-time surface tests must
   reject those duplicate members.
2. A role entry point can scatter policy across keyed services. Registration tests must prove one
   descriptor owns every role-specific option and factory.
3. Moving logging settings can weaken the two-grant sensitive-data rule. Focused transport tests
   must cover ordinary and sensitive contexts.
4. Replacing marker interfaces can change optional-feature defaults. Each removal needs a before-
   and-after behavior test.
5. Repeated registrations can duplicate logger providers or value mappers. Service-descriptor tests
   must check idempotence and ordering.
6. Early validation can construct scoped services from the root provider. Endpoint validation must
   inspect registrations without resolving `TContext` from the root scope.
7. A server descriptor can still resolve the wrong `DbContext` by accident. Two-context registration
   tests must prove each server factory resolves its declared context type.
8. Public names can collide with application HTTP client names. Tests must prove InfoCarrier derives
   private prefixed HTTP names from its opaque identities.

## Implementation order

1. Add role options that derive from the shared builder. Produce one immutable default descriptor
   per role. Preserve the two independent store facts.
2. Add the unnamed server entry point and endpoint registration validation. Resolve `TContext`
   without an unkeyed `DbContext` alias.
3. Add the unnamed client entry point and direct factory. Make `UseInfoCarrier` attachment-only.
4. Remove replaced methods, marker interfaces, and the mapper type forwarder during their owning
   role changes. Update repository callers in the same commits.
5. Update samples and configuration documentation after the APIs pass their focused tests.

Each implementation pull request stays within one numbered step. Issue #105 starts only after the
unnamed descriptors and their activation boundary are stable.

## Compatibility review after implementation (2026-10-24)

The review below distinguishes the implemented foundation from the public multi-registration work
reserved for [issue #105](https://github.com/azabluda/InfoCarrier.Core/issues/105).

| Requirement | Foundation in this change | Remaining issue #105 work |
|---|---|---|
| Ordinary registration stays concise | `AddInfoCarrierClient` plus registration-local `UseInfoCarrier()` | Add named attachment without changing the default overload |
| One context type can serve different destinations | Independent options and keyed custom factories work; no client attachment by context-type-wide configuration | Exercise two named clients of the same context type under concurrent reads and writes |
| Role identity is independent of context type | Client and server descriptors carry opaque identities; server builders retain their descriptor identity | Map public names and handles to those identities |
| Client policy remains isolated | Client descriptors own deployment snapshots, payload limits, store rules, and mapper instances | Named activation and separate private HTTP client configurations |
| Server admission remains isolated | Server descriptors retain distinct grants, anonymous shapes, and mapper policy | Activate and map several descriptors, including two policies over one context type |
| Context construction uses the host | Default server resolves its declared context from a host scope | Extend descriptor resolution for named or custom context factories when type alone is insufficient |
| Caller identity belongs to an endpoint | `AddHttpCallerIdentity` configures its server builder; the accessor remains shared | Endpoint mapping must activate the selected descriptor's policy |
| Runtime policy remains stable | Runtime descriptor store copies the registration catalog | Maintain that snapshot boundary during named registration and handle selection |
| Limits, timeouts, and log grants remain per role | Descriptor activation supplies limits, timeouts, and forwarding policy | Select the corresponding serializer and server together for each endpoint |
| Transactions remain endpoint-owned | Each server instance retains its own transaction registry and minting identity | Test token rejection across two mapped endpoints |
| Shared cache reuse preserves correctness | Parameterless attachment delegates to the existing configured-client path before internal service construction | Verify serializer, mapper, and compiled-query behavior across named clients with actual stored-data assertions |
| Endpoint selection is explicit when needed | Public mapping still selects the default role | Add handle/name selection, unambiguous typed convenience, and startup ambiguity errors |
| Metrics distinguish roles | Existing meter behavior is unchanged | Add client and endpoint tags |

`ImplicitClientRegistrationTest` exercises the four standard context registration methods through
HTTP, including pooled reuse. Its same-context test saves and reads remote data through custom
value mappers and independently saves and reads local SQLite data. `InfoCarrierRoleIsolationTest`
checks descriptor policy separation and runtime-store immutability. `HttpCallerIdentityTest` checks
that retained server builders configure separate caller policies.

This is an additive foundation for issue #105. It does not expose named clients, named servers,
multiple-endpoint mapping, or new metric tags in the issue #104 public surface.
