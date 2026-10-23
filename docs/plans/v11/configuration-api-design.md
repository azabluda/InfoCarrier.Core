# Configuration role API design

Date: 2026-10-23  
Issue: [#104](https://github.com/azabluda/InfoCarrier.Core/issues/104)  
Release: 11.0  
Status: reconciled design, before implementation

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

### Shared facts stay on the shared builder

The role options do not add `AllowedTypes`, `AllowArbitrarySqlExecution`, or `ValueMappers`.
Duplicating those members would create two sources for shared facts. Applications continue to place
one deployment delegate in code referenced by both roles.

The client-only non-relational planning switch and the server-only document repair remain separate
facts. A single `StoreKind` would hide the distinction that Entity Framework InMemory needs only the
first behavior. Version 11 gives the two facts clear role-option names but does not merge them.

### The first role APIs are unnamed

The server entry point is `AddInfoCarrierServer<TContext>`. It registers the default serializer,
the default in-process server, and the `DbContext` base mapping for `TContext`. It also applies the
existing shared deployment delegate and the server role options.

The client entry point is `AddInfoCarrierClient`. It registers one HTTP client, transport,
serializer, and `IInfoCarrierClient`. Its role options contain the endpoint, response limit, and
server query-rule behavior. `UseInfoCarrier` remains the Entity Framework provider attachment and
still receives the shared deployment delegate.

The unnamed registrations use internal keys that a later named overload can replace with an
application name. The initial public API does not expose names, generic endpoint mapping, or
multiple-server selection.

### Server construction uses the host container

`AddInfoCarrierServer<TContext>` resolves `TContext` through the host service provider. It maps that
instance to the non-generic `DbContext` expected by the current server implementation. The entry
point does not create a child container.

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

Version 10.3 already gives applications one shared configuration function. The server remains the
admission authority when callers configure the two roles differently.

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
Sensitive forwarding requires a forwarding level. Repeated unnamed registrations compose their
configuration in call order without adding duplicate default services.

## Bounded public shape

The implementation may refine names after compile-time API tests. Its boundary is:

```csharp
public sealed class InfoCarrierClientOptions
{
    public Uri? Endpoint { get; set; }
    public bool ServerUsesRelationalQueryRules { get; set; }
    public int? MaxResponseBytes { get; set; }
}

public sealed class InfoCarrierServerOptions
{
    public bool StoresOwnedDataInsideOwner { get; set; }
    public int? MaxRequestBytes { get; set; }
    public TimeSpan? TransactionIdleTimeout { get; set; }
    public InfoCarrierLogForwardingOptions LogForwarding { get; }
}

public static IHttpClientBuilder AddInfoCarrierClient(
    this IServiceCollection services,
    Action<InfoCarrierClientOptions> configure);

public static IServiceCollection AddInfoCarrierServer<TContext>(
    this IServiceCollection services,
    Action<InfoCarrierServerOptions> configure,
    Action<InfoCarrierDeploymentBuilder> configureDeployment)
    where TContext : DbContext;
```

The exact server return type can become an InfoCarrier server builder if caller-identity
registration needs that composition point. It must not expose a server name before issue #105.
An overload may omit either callback when defaults are sufficient.

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

The unnamed client becomes the default named client later. The unnamed server becomes the default
named server later. No version 11 implementation may use one unkeyed mutable singleton to hold role
options for all future registrations.

`AddInfoCarrierServer<TContext>` must resolve the context type without making the endpoint mapping
generic. Issue #105 can add `MapInfoCarrier<TContext>` or name-based mapping after it selects the
keying model. This issue does not choose between those designs.

Client metrics and server metrics keep their current tags. Issue #105 adds client or endpoint
identity tags when multiple registrations make those identities meaningful.

## Main risks

1. The client options can accidentally become a second home for shared deployment facts. Compile-
   time surface tests must reject those duplicate members.
2. A server entry point can register current services as unkeyed singletons that cannot later be
   named. Registration tests must prove an internal key boundary exists.
3. Moving logging settings can weaken the two-grant sensitive-data rule. Focused transport tests
   must cover ordinary and sensitive contexts.
4. Replacing marker interfaces can change optional-feature defaults. Each removal needs a before-
   and-after behavior test.
5. Repeated registrations can duplicate logger providers or value mappers. Service-descriptor tests
   must check idempotence and ordering.
6. Early validation can construct scoped services from the root provider. Endpoint validation must
   inspect registrations without resolving `TContext` from the root scope.

## Implementation order

1. Add immutable unnamed role options and validation. Preserve the two independent store facts.
2. Add the unnamed server entry point and endpoint registration validation. Keep the default
   composition replaceable through the existing low-level interfaces.
3. Add the unnamed client entry point and direct factory. Keep shared facts in `UseInfoCarrier`.
4. Remove replaced methods, marker interfaces, and the mapper type forwarder during their owning
   role changes. Update repository callers in the same commits.
5. Update samples and configuration documentation after the APIs pass their focused tests.

Each implementation pull request stays within one numbered step. Issue #105 starts only after the
unnamed registrations and their internal key boundary are stable.
