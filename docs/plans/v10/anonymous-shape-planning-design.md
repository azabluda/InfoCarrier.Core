# Anonymous-shape planning configuration design

Issue: [#218](https://github.com/azabluda/InfoCarrier.Core/issues/218)  
Target: 10.3.0  
Evidence: [anonymous-shape-comparison-audit.md](anonymous-shape-comparison-audit.md)

## Goal

Version 10.3 restores the version 10.2 planning path when a client has no anonymous-shape declaration. An explicit client declaration permits the planner to send that structure. The server still requires its independent trusted-catalog declaration.

This change keeps every released version 10 public API. It adds a focused client setting for issue #218. The recommended shared deployment contract remains separate additive work in [#221](https://github.com/azabluda/InfoCarrier.Core/issues/221). Version 11 migration and removal work remains in [#104](https://github.com/azabluda/InfoCarrier.Core/issues/104).

## Version 10 user surface

The existing server registration remains unchanged:

```csharp
services.AddInfoCarrierAnonymousShapes(
    new { Id = 0, CustomerId = (string?)null });
```

The client receives one low-level planning declaration on `InfoCarrierDbContextOptionsBuilder`:

```csharp
options.UseInfoCarrier(
    client,
    infoCarrier => infoCarrier.UseRegisteredAnonymousShapes(
        new { Id = 0, CustomerId = (string?)null }));
```

`UseRegisteredAnonymousShapes` states client knowledge. It does not grant server permission or promise provider translation. Calls compose, snapshot their input, include nested anonymous structures, and reject null or named prototypes with the server API's existing rules.

The name deliberately exposes the low-level fact: the caller asserts that the same structural shapes are registered on the server. Documentation recommends the shared configuration from issue #221 after that API exists. Version 10 does not mark this method or an existing method with `[Obsolete]`; compiler warnings can break builds that treat warnings as errors.

## Behavior

| Client declaration | Server declaration | Result |
|---|---|---|
| absent | absent | Preserve version 10.2 planning |
| present | absent | Send the selected shape; the server refuses before database execution |
| absent | present | Preserve version 10.2 client planning; the server declaration is unused |
| present | present | Permit transport; the backing provider decides translation |

An unregistered anonymous final projection uses existing client reconstruction when that position is eligible. Registration permits the original construction to reach the backend. Filtering, ordering, grouping, joins, distinct, and equality retain their existing position-specific planning rules.

Safe internal carrier rewrites remain supported. A rewrite is safe here when the database still executes the operator and the original anonymous carrier is not observable. Registration must not disable such rewrites merely to ship an original anonymous type.

Server admission remains strict. A missing catalog entry, denied component type, unavailable named type, or provider refusal is returned to the client. No refusal starts a second request or automatic client-evaluation retry.

## Internal design

`InfoCarrierOptionsExtension` stores immutable client anonymous-shape descriptors beside its allowed types and other planning settings. The descriptors use ordered member names and component types, not compiler-generated runtime type identity. `QueryExecutor` reads them from the current context for every execution. They do not enter the service-provider hash because the extension deliberately shares one internal provider across client contexts.

The new builder method validates prototypes through one shared internal validator. The existing server extension uses the same validator, preventing different client and server structural rules.

`QueryExecutor` passes the configured descriptors into `QuerySplitter`. Existing public `QuerySplitter` constructors remain binary compatible. A new overload can accept the descriptor set, while every shipped constructor delegates with an empty set.

`ServerBoundaryAnalyzer` distinguishes general type admission from anonymous-shape planning knowledge. `TypeAllowlist` continues enforcing component permissions. An original anonymous type may cross the client boundary only when its descriptor is configured or an earlier safe rewrite removed it from the shipped tree.

The server continues resolving anonymous descriptors through `AnonymousShapeCatalog.Require`. Client configuration never changes the server catalog and never authorizes a component type.

## Compatibility

- No released public type or member is removed, renamed, moved, or changed.
- Existing constructor signatures remain in the assembly.
- Existing server registration signatures remain unchanged.
- Applications with no new client declaration keep the version 10.2 path.
- The anonymous catalog behavior currently on unreleased `main` can change when the client declaration is absent.
- Package compatibility validation must pass against the configured version 10 baseline.
- No version 10 API receives an `[Obsolete]` warning.

## Permanent coverage

Focused tests cover these boundaries:

1. An unregistered anonymous final projection reconstructs on the client and retains its original runtime result type.
2. Matching client and server declarations permit the trusted catalog path and match the direct provider result.
3. A client-only declaration produces the catalog refusal, executes no database command, and makes no retry request.
4. A server-only declaration preserves the same client plan as neither-side registration.
5. Nested anonymous structures require nested registration and preserve returned client identities.
6. A denied component remains denied even when its enclosing shape is declared.
7. A safe anonymous join-key rewrite remains database-executed without registration.
8. A registered shape that the provider cannot translate returns the provider refusal without retry.
9. Repeated builder calls compose and snapshot prototypes.
10. Existing public server methods and all shipped `QuerySplitter` constructors remain compatible.

The null-component group-join difference remains evidence for execution-location risk. It does not become a version 10 behavior change. The captured named-value refusal remains a separate investigation candidate because it does not depend on anonymous catalog membership.

## Exclusions

This change does not add the shared contract from issue #221. It does not tighten implicit client evaluation; that version 11 policy is [#220](https://github.com/azabluda/InfoCarrier.Core/issues/220). It does not add contract fingerprints, capability negotiation, mismatch handshakes, automatic retries, or new server permissions.

## Completion criteria

The focused tests pass with all four registration states. Existing functional, transport, package-compatibility, trimming, and sample checks pass. The samples declare required anonymous shapes on both sides using the version 10 syntax. Documentation identifies the client method as a planning declaration and the server method as admission.
