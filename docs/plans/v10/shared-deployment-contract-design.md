# Shared deployment contract design

Date: 2026-10-12  
Issue: [#221](https://github.com/azabluda/InfoCarrier.Core/issues/221)  
Release: 10.3  
Status: approved

## Purpose

Version 10.3 adds one shared deployment contract for facts that must agree across the wire. An
application defines one configuration method and passes it to both role-specific entry points.
Existing version 10 APIs remain public, keep their signatures and behavior, and receive no
`[Obsolete]` warnings.

The first consumer is anonymous-shape planning. Matching client and server declarations select the
trusted catalog path. Missing client declaration preserves version 10.2 planning. Server admission
remains strict, and a refusal never triggers automatic client-evaluation retry.

## Scope

The shared contract contains four facts:

- anonymous structures;
- extra named types;
- custom value mappers;
- arbitrary SQL permission.

These facts need matching application intent on both roles. Store kind stays outside this contract
because the existing client and server switches describe different facts. Endpoints, serializers,
payload limits, transactions, logging, server construction, caller identity and store behavior also
remain role-specific.

Version 11 work remains in [issue #104](https://github.com/azabluda/InfoCarrier.Core/issues/104).
This design does not add role options, contract fingerprints, mismatch negotiation, new server entry
points, dependency-injection client construction, removals, renames or marker-interface changes.

## Public contract

`InfoCarrierDeploymentOptions` is a plain public sealed class in the `InfoCarrier.Core` namespace.
It exposes validated additive methods and one permission property:

```csharp
public sealed class InfoCarrierDeploymentOptions
{
    public void AddAnonymousShapes(params object[] shapes);
    public void AddAllowedTypes(params Type[] types);
    public void AddValueMappers(params IInfoCarrierValueMapper[] mappers);
    public bool AllowArbitrarySqlExecution { get; set; }
}
```

The client adds this method to `InfoCarrierDbContextOptionsBuilder`:

```csharp
public virtual InfoCarrierDbContextOptionsBuilder UseInfoCarrierDeployment(
    Action<InfoCarrierDeploymentOptions> configure);
```

The server adds this extension method:

```csharp
public static IServiceCollection AddInfoCarrierDeployment(
    this IServiceCollection services,
    Action<InfoCarrierDeploymentOptions> configure);
```

An application can keep one method beside its Entity Framework model configuration:

```csharp
static void ConfigureInfoCarrier(InfoCarrierDeploymentOptions options)
{
    options.AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null });
    options.AddAllowedTypes(typeof(ApplicationDbFunctions));
    options.AddValueMappers(new MoneyValueMapper());
    options.AllowArbitrarySqlExecution = true;
}
```

The application passes `ConfigureInfoCarrier` to both new entry points. Each role creates a separate
options instance and invokes the delegate once. The application therefore shares configuration code
without sharing mutable option collections.

## Composition and validation

Each additive method preserves insertion order. Calls on one options instance compose. Repeated role
entry-point calls also compose with earlier calls and with existing low-level registrations.

All delegates, arrays and array items reject `null` immediately. Anonymous structures use the
existing prototype validation. Prototype values are ignored, nested anonymous structures are
included, and named prototypes retain the existing error. Allowed types and mapper instances are
snapshotted before the role entry point returns.

Custom value mappers retain the current first-claim-wins order. A mapper instance is not cloned.
Applications that need separate mutable mapper state must create an instance during each delegate
invocation. The package's standard `IPAddress` and `Uri` mappers remain after application mappers.

## Client integration

`UseInfoCarrierDeployment` translates the shared options into the existing
`InfoCarrierOptionsExtension` state:

- anonymous structures use the same structural set as `UseRegisteredAnonymousShapes`;
- allowed types use the same list as `AllowTypes`;
- arbitrary SQL uses the same flag as `AllowArbitrarySqlExecution`;
- custom value mappers are registered in the Entity Framework internal service collection.

The options extension owns the mapper snapshot so `ApplyServices` can add the mapper instances when
Entity Framework builds the client provider. The extension's service-provider identity must include
configuration that changes service registrations. This prevents incompatible mapper sets from
sharing one internal provider.

Existing client methods continue to write the same internal settings. The shared path does not
change behavior when it is unused.

## Server integration

`AddInfoCarrierDeployment` translates the shared options into existing service registrations:

- anonymous structures use the existing trusted catalog registration;
- allowed types use the existing allowlist registration;
- arbitrary SQL uses the existing permission marker;
- custom value mappers use ordered singleton registrations;
- standard value mappers are registered idempotently.

Existing server methods remain independent and composable. The new method grants no permission that
the shared options do not request. The server remains the admission boundary for anonymous shapes,
named types and arbitrary SQL.

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

## Tests

Focused tests cover all four anonymous registration states through the shared entry points. The
client-only case records database commands and proves zero execution. It also proves one request and
no automatic retry. The absent-client cases pin version 10.2 planning.

Additional tests prove that:

- nested anonymous prototypes compose and retain existing validation;
- named types cross only when both roles use the shared declaration;
- arbitrary SQL crosses only when both roles use the shared permission;
- one application method installs a custom mapper on both roles;
- mapper precedence and repeated registrations preserve order;
- existing low-level APIs still compile and retain their behavior;
- the public API compatibility gate reports no removed or changed version 10 member.

Product changes run the focused tests first. Final verification runs the specification measurement,
transport tests, strict Release build, trimming gate, package validation and documentation gates.

## Documentation and migration

Package and website configuration examples recommend the shared method. Existing low-level methods
are described as legacy version 10 paths without compiler deprecation warnings. Documentation states
that matching declarations express application intent and do not weaken server admission.

After the additive surface is final, issue #104 receives the version 11 removal table. That update
records the actual version 10.3 names and does not start version 11 implementation.
