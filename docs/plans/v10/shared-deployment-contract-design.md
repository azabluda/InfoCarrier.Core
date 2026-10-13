# Shared deployment contract design

Date: 2026-10-12  
Issue: [#221](https://github.com/azabluda/InfoCarrier.Core/issues/221)  
Release: 10.3  
Status: approved for implementation

## Purpose

Version 10.3 adds one shared configuration builder for facts that must agree across the wire. An
application defines one extension method and applies it through the existing client entry point and
a concise server entry point. Existing shared model projects can own this method without referencing
the full provider package.

The first consumer is anonymous-shape planning. Matching client and server declarations select the
trusted catalog path. Missing client declaration preserves version 10.2 planning. Server admission
remains strict, and a refusal never triggers automatic client-evaluation retry.

## Abstractions package

Version 10.3 adds `InfoCarrier.Core.Abstractions`. It contains only two public contracts:

- `InfoCarrierDeploymentBuilder`;
- `IInfoCarrierValueMapper`.

The package uses only Base Class Library dependencies. It does not reference Entity Framework Core
or dependency injection. An application assembly may reference both Entity Framework Core and the
abstractions package when it owns the shared model and deployment configuration.

`InfoCarrier.Core` references the abstractions package. `InfoCarrier.Core.AspNetCore` continues to
reference `InfoCarrier.Core`. Client and server applications receive abstractions transitively. No
other InfoCarrier interface moves merely because it is an interface.

`IInfoCarrierValueMapper` keeps its namespace and public signature. It moves to the abstractions
assembly. `InfoCarrier.Core` type-forwards it so binaries compiled against version 10.2 resolve the
moved type without recompilation. Package validation and a compiled compatibility fixture verify the
forwarding behavior.

## Shared builder

`InfoCarrierDeploymentBuilder` is a public class in the `InfoCarrier.Core` namespace:

```csharp
public class InfoCarrierDeploymentBuilder
{
    public InfoCarrierDeploymentBuilder AddAnonymousShapes(params object[] shapes);
    public InfoCarrierDeploymentBuilder AddAllowedTypes(params Type[] types);
    public InfoCarrierDeploymentBuilder AddValueMappers(params IInfoCarrierValueMapper[] mappers);
    public InfoCarrierDeploymentBuilder AllowArbitrarySqlExecution(bool allowed);
}
```

The builder contains four shared facts:

- anonymous structures;
- extra named types;
- custom value mappers;
- arbitrary SQL permission.

Store kind stays outside this builder because the existing client and server switches describe
different facts. Endpoints, serializers, payload limits, transactions, logging, server construction,
caller identity, and store behavior also remain role-specific.

Each additive method validates all input before changing state and preserves insertion order. Calls
on one builder compose. Arrays are copied before the configuration callback returns. Anonymous
prototype values are ignored. Named prototypes and null items fail during configuration.

The builder keeps private mutable lists and produces one internal immutable snapshot. The
abstractions assembly grants `InternalsVisibleTo` only to `InfoCarrier.Core`. Test assemblies receive
no friendship and verify behavior through public entry points.

## Client builder hierarchy

The existing client builder derives from the shared builder:

```csharp
public class InfoCarrierDbContextOptionsBuilder
    : InfoCarrierDeploymentBuilder
{
    public InfoCarrierDbContextOptionsBuilder UseNonRelationalServerStore();
}
```

Released low-level client methods remain on the derived builder during version 10.3. The existing
`UseInfoCarrier` callbacks still receive `InfoCarrierDbContextOptionsBuilder`. No new client overload
is added, so existing overload resolution and source compatibility remain unchanged.

After the callback completes, `UseInfoCarrier` reads the inherited shared snapshot and applies it to
the client options extension. Existing client-only methods continue applying their role-specific
state.

## Server entry point

The server adds one concise entry point:

```csharp
public static IServiceCollection AddInfoCarrier(
    this IServiceCollection services,
    Action<InfoCarrierDeploymentBuilder> configure);
```

It creates the shared builder, invokes the callback once, and translates the snapshot into service
registrations. Repeated calls compose. Application value mappers retain first-claim-wins order.
Standard value mappers are installed automatically and idempotently after application mappers.

`AddInfoCarrier` configures shared deployment facts. Version 10.3 still requires explicit context,
serializer, server implementation, and endpoint setup. Documentation states this limited scope.

## Application-owned extension method

Applications define a non-generic extension method on the shared builder:

```csharp
public static InfoCarrierDeploymentBuilder ConfigureInfoCarrier(
    this InfoCarrierDeploymentBuilder builder)
    => builder
        .AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null })
        .AddAllowedTypes(typeof(ApplicationDbFunctions))
        .AddValueMappers(new MoneyValueMapper())
        .AllowArbitrarySqlExecution(true);
```

Client-specific calls precede the shared extension because it returns the base builder:

```csharp
clientOptions.UseInfoCarrier(
    client,
    builder => builder
        .UseNonRelationalServerStore()
        .ConfigureInfoCarrier());
```

The compiler rejects client-only calls placed after `ConfigureInfoCarrier`. Shared-only client
configuration is:

```csharp
clientOptions.UseInfoCarrier(
    client,
    builder => builder.ConfigureInfoCarrier());
```

The server uses the same application extension:

```csharp
serverServices.AddInfoCarrier(
    builder => builder.ConfigureInfoCarrier());
```

Each role creates a separate builder. The application shares configuration code without sharing
mutable collections. Mapper instances are created during each extension invocation.

A return-valued method group does not bind directly to `Action<T>`. The documented form therefore
uses a lambda for both roles.

## Application project layouts

Documentation recommends ownership rather than one required project structure.

### Reuse a shared model project

An existing project containing the `DbContext`, entities, and model configuration is usually the
simplest location. It adds a reference to `InfoCarrier.Core.Abstractions`, not the full provider.
Both roles already reference this project in common application layouts.

### Use a separate deployment project

Applications that keep the model unaware of InfoCarrier can add a small deployment project. It
references the model and abstractions packages. Both roles reference the deployment project. This
layout preserves the intentional model boundary at the cost of one project and assembly.

### Share source directly

Small repositories can link one configuration source file into both role projects. The code compiles
twice, so anonymous type identities differ by assembly. Structural anonymous matching supports this
case. Linked source remains an alternative, not the default recommendation.

The documentation states:

> Place `ConfigureInfoCarrier` in application code referenced by both roles. An existing shared
> model project is usually the simplest location. Use a separate deployment project when the model
> must remain unaware of InfoCarrier. A new project is not required.

## Mapper ownership and defaults

`IInfoCarrierValueMapper` belongs in abstractions because the shared builder names it. The application
mapper list starts empty. Standard `IPAddress` and `Uri` mappers do not appear in that list.

Core installs standard mappers after application mappers on both roles. This keeps application intent
separate from package defaults and preserves the existing zero client service-provider hash when no
application mapper exists.

A mapper instance is not cloned. Applications that need separate mutable mapper state create an
instance during each configuration invocation.

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

The shared builder becomes the only public anonymous-shape registration path. Core keeps only smaller
private or internal helpers needed for validation, planning, catalog construction, and service
registration. Tests migrate to the shared public entry points.

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

## Version 10.3 obsolete warnings

Version 10.3 marks confirmed version 11 removals obsolete. Warnings are not errors and each message
names its replacement. This provides one minor-version migration period before removal.

Confirmed warnings cover:

- `InfoCarrierDbContextOptionsBuilder.AllowTypes`;
- `InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution`;
- `AddInfoCarrierAllowedTypes`;
- `AddInfoCarrierArbitrarySqlExecution`;
- `AddInfoCarrierStandardValueMappers`.

Each warning receives a stable diagnostic identifier and documentation link. Adding warnings can
break consumer builds that treat warnings as errors. This source-compatibility cost is accepted for
the confirmed version 11 removals.

Uncertain reduction candidates receive no warning in version 10.3. The unreleased anonymous methods
are removed directly and receive no warning.

## Version 11 reduction inventory

[Issue #104](https://github.com/azabluda/InfoCarrier.Core/issues/104) records both confirmed removals
and candidates requiring more analysis.

Confirmed version 11 removals are the five methods warned in version 10.3. Further candidates are:

- manual client mapper registration through an Entity Framework internal service provider;
- configuration-only marker interfaces;
- public `InfoCarrierOptionsExtension`, if external construction is unnecessary;
- redundant role-specific overloads replaced by version 11 role options.

Each candidate must be classified before warning or removal. Extension points and configuration
plumbing have different compatibility value. Transport, serializer, client, and server building
blocks remain public unless a separate design replaces their advanced composition scenarios.

## Tests and gates

Focused tests verify:

- shared projects compile with only the abstractions package;
- the abstractions package has no non-framework package dependencies;
- a version 10.2 mapper consumer runs through the type forwarder;
- the existing client callback accepts the application extension without a new overload;
- client-only calls chain before shared configuration;
- all inputs validate before configuration changes state;
- mapper order, repeated calls, and client provider isolation remain correct;
- all four anonymous registration states preserve their selected behavior;
- client-only declarations cause one refusal, zero database commands, and no retry;
- removed anonymous methods are absent from the public API;
- released APIs change only by the five approved obsolete warnings.

Final verification runs package compatibility, the complete specification measurement, transport
tests, strict release builds, trimming, sample builds, and documentation gates.

## Documentation and migration

Package and website examples recommend the shared builder extension. They state that matching
configuration guides planning but does not grant provider translation. Server admission remains
authoritative, and refusals are not retried.

The Northwind sample removes redundant standard mapper registration. Migration guidance lists all
five obsolete methods and their replacements. Issue #104 receives the confirmed removals and the
remaining reduction inventory without starting version 11 implementation.
