# Shared deployment contract design

Date: 2026-10-12  
Issue: [#221](https://github.com/azabluda/InfoCarrier.Core/issues/221)  
Release: 10.3  
Status: approved revision pending implementation

## Purpose

Version 10.3 adds one shared deployment contract for facts that must agree across the wire. An
application defines one configuration method and passes it to direct client and server entry points.
Shared application projects depend only on a small abstractions package. Existing released version
10 APIs remain public, keep their signatures and behavior, and receive no `[Obsolete]` warnings.

The first consumer is anonymous-shape planning. Matching client and server declarations select the
trusted catalog path. Missing client declaration preserves version 10.2 planning. Server admission
remains strict, and a refusal never triggers automatic client-evaluation retry.

## Package boundary

Version 10.3 adds `InfoCarrier.Core.Abstractions`. It contains only two public contracts:

- `InfoCarrierDeploymentOptions`;
- `IInfoCarrierValueMapper`.

The abstractions package uses only Base Class Library dependencies. It does not reference Entity
Framework Core or dependency injection. `InfoCarrier.Core` references the abstractions package, and
`InfoCarrier.Core.AspNetCore` continues to reference `InfoCarrier.Core`.

A shared application project can reference only `InfoCarrier.Core.Abstractions`. Client and server
applications reference their existing packages and receive abstractions transitively. No other
InfoCarrier interface moves into the abstractions package without a separate requirement.

`IInfoCarrierValueMapper` keeps its namespace and public signature. It moves to the abstractions
assembly. `InfoCarrier.Core` type-forwards it so binaries compiled against version 10.2 continue to
resolve the original type identity. Package validation and a compiled compatibility fixture verify
the forwarding behavior.

## Shared public contract

`InfoCarrierDeploymentOptions` is a plain public sealed class in the `InfoCarrier.Core` namespace:

```csharp
public sealed class InfoCarrierDeploymentOptions
{
    public void AddAnonymousShapes(params object[] shapes);
    public void AddAllowedTypes(params Type[] types);
    public void AddValueMappers(params IInfoCarrierValueMapper[] mappers);
    public bool AllowArbitrarySqlExecution { get; set; }
}
```

The contract contains four shared facts:

- anonymous structures;
- extra named types;
- custom value mappers;
- arbitrary SQL permission.

Store kind stays outside this contract because the existing client and server switches describe
different facts. Endpoints, serializers, payload limits, transactions, logging, server construction,
caller identity, and store behavior also remain role-specific.

Each additive method validates all input before changing state and preserves insertion order. Calls
on one options instance compose. Arrays are copied before the configuration delegate returns.
Anonymous prototype values are ignored. Named prototypes and null items fail during configuration.

The options object keeps private mutable lists. It produces one internal immutable snapshot for the
core package. The abstractions assembly grants `InternalsVisibleTo` only to `InfoCarrier.Core`. Test
assemblies receive no friendship and verify behavior through public entry points.

## Client entry point

The client adds generic and non-generic direct entry points:

```csharp
public static DbContextOptionsBuilder UseInfoCarrierDeployment(
    this DbContextOptionsBuilder optionsBuilder,
    IInfoCarrierClient client,
    Action<InfoCarrierDeploymentOptions> configure);

public static DbContextOptionsBuilder<TContext> UseInfoCarrierDeployment<TContext>(
    this DbContextOptionsBuilder<TContext> optionsBuilder,
    IInfoCarrierClient client,
    Action<InfoCarrierDeploymentOptions> configure);
```

The entry points create one options object, invoke the delegate once, apply its snapshot, and attach
the supplied client. Existing `UseInfoCarrier` overloads remain supported for lower-level and
client-specific configuration.

The nested `InfoCarrierDbContextOptionsBuilder.UseInfoCarrierDeployment` method introduced during
development is removed before release. It never becomes part of the version 10.3 public surface.

## Server entry point

The server entry point remains:

```csharp
public static IServiceCollection AddInfoCarrierDeployment(
    this IServiceCollection services,
    Action<InfoCarrierDeploymentOptions> configure);
```

It translates the snapshot into existing service registrations. Repeated calls compose. Application
value mappers retain first-claim-wins order. Standard value mappers are installed automatically and
idempotently after application mappers.

Applications do not call `AddInfoCarrierStandardValueMappers` separately when using the deployment
entry point. The existing method remains public for released low-level composition scenarios.

## Application usage

A shared application project owns one method:

```csharp
public static void ConfigureInfoCarrier(InfoCarrierDeploymentOptions options)
{
    options.AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null });
    options.AddAllowedTypes(typeof(ApplicationDbFunctions));
    options.AddValueMappers(new MoneyValueMapper());
    options.AllowArbitrarySqlExecution = true;
}
```

Each role passes the same method to its direct entry point:

```csharp
clientOptions.UseInfoCarrierDeployment(client, ConfigureInfoCarrier);
serverServices.AddInfoCarrierDeployment(ConfigureInfoCarrier);
```

Each role creates a separate options instance. The application shares configuration code without
sharing mutable option collections. Mapper instances are created during each delegate invocation.

The Northwind shared project references `InfoCarrier.Core.Abstractions`, not `InfoCarrier.Core`.
Client, server, and console applications already reference the core package directly.

## Mapper ownership and defaults

`IInfoCarrierValueMapper` belongs in abstractions because the shared options contract names it.
Nothing else moves merely because it is an interface.

The application mapper list starts empty. Standard `IPAddress` and `Uri` mappers do not appear in
that list. Core installs them after application mappers on both roles. This keeps application intent
separate from package defaults and preserves the existing zero client service-provider hash when no
application mapper exists.

A mapper instance is not cloned. Applications that need separate mutable mapper state create an
instance during each delegate invocation.

## Client service-provider isolation

The client options extension owns the application mapper snapshot. `ApplyServices` registers those
instances before standard mappers. The extension's service-provider identity includes the ordered
mapper object identities. Two configurations with different mapper instances cannot share one
Entity Framework internal provider.

Configurations without application mappers retain service-provider hash zero and existing provider
sharing. Allowed types, anonymous shapes, and arbitrary SQL remain per-context state and do not alter
service registrations.

## Anonymous-shape surface reduction

The anonymous-shape APIs introduced before version 10.3 are unreleased. Remove these public methods
rather than retaining internal versions with the same names:

```csharp
InfoCarrierDbContextOptionsBuilder.UseRegisteredAnonymousShapes(...)
InfoCarrierServiceCollectionExtensions.AddInfoCarrierAnonymousShapes(...)
```

The shared deployment contract becomes the only public anonymous-shape registration path. Core keeps
only smaller private or internal helpers needed for validation, planning, catalog construction, and
service registration. Tests migrate to the shared public entry points.

## Anonymous registration states

The four states retain the policy selected in
[issue #218](https://github.com/azabluda/InfoCarrier.Core/issues/218):

| Client declaration | Server declaration | Behavior |
|---|---|---|
| absent | absent | Preserve version 10.2 planning. |
| present | absent | Send the selected shape. The server refuses before database execution. |
| absent | present | Preserve version 10.2 client planning. The server declaration is unused. |
| present | present | Permit the trusted catalog path. The backing provider decides translation. |

Registration guides planning and admission. It does not promise provider translation. A server
refusal returns through the existing fault path and never causes an automatic retry.

## Version 10 compatibility policy

Released low-level APIs remain public and supported throughout version 10. They receive no obsolete
attributes. The new deployment API is a recommended facade over supported plumbing, not proof that
the plumbing is deprecated.

Removing a released member, reducing its accessibility, moving it without type forwarding, or adding
an obsolete warning can break consumers. Such reductions wait for version 11. Documentation can
recommend the facade while continuing to document supported low-level composition.

## Version 11 reduction inventory

[Issue #104](https://github.com/azabluda/InfoCarrier.Core/issues/104) records candidates for breaking
surface reduction in version 11:

- `InfoCarrierDbContextOptionsBuilder.AllowTypes`;
- `InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution`;
- `AddInfoCarrierAllowedTypes`;
- `AddInfoCarrierArbitrarySqlExecution`;
- `AddInfoCarrierStandardValueMappers`;
- manual client mapper registration through an Entity Framework internal service provider;
- configuration-only marker interfaces;
- public `InfoCarrierOptionsExtension`, if external construction is unnecessary;
- redundant role-specific overloads replaced by version 11 role options.

Each candidate must be classified before removal. Extension points and configuration plumbing have
different compatibility value. Transport, serializer, client, and server building blocks remain
public unless a separate design replaces their advanced composition scenarios.

## Tests and gates

Focused tests verify:

- shared projects compile with only the abstractions package;
- the abstractions package has no non-framework package dependencies;
- a version 10.2 mapper consumer runs through the type forwarder;
- all inputs validate before configuration changes state;
- mapper order, repeated calls, and client provider isolation remain correct;
- all four anonymous registration states preserve their selected behavior;
- client-only declarations cause one refusal, zero database commands, and no retry;
- removed anonymous methods are absent from the public API;
- every released low-level API remains unchanged.

Final verification runs package compatibility, the complete specification measurement, transport
tests, strict release builds, trimming, sample builds, and documentation gates.

## Documentation and migration

Package and website examples recommend the direct shared entry points. They state that matching
configuration guides planning but does not grant provider translation. Server admission remains
authoritative, and refusals are not retried.

The Northwind sample removes redundant standard mapper registration. Documentation keeps released
low-level paths available without deprecation warnings. Issue #104 receives the expanded version 11
reduction inventory without starting version 11 implementation.
