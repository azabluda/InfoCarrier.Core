# Research Findings — Resolution of Open Questions

Status: **RESOLVED (2026-07-22)** · Closes the open-question tables in
[`architecture.md`](architecture.md) §6, [`expression-serialization.md`](expression-serialization.md) §4,
and [`wire-protocol.md`](wire-protocol.md) §5. Locks [ADR-006](decisions.md) and ADR-008.

Method: structural study of `subrepos/efcore` (EF Core 10, authoritative),
`subrepos/infocarrier-v1` (the proven client/server pattern), cross-checked against the
rlinq/aqua findings already recorded in repo memory. Each resolution cites its evidence.

---

## 1. ADR-006 — Capture point: **B (raw capture at `IDatabase.CompileQuery`)** — LOCKED

**Resolution.** The client intercepts the LINQ expression at `IDatabase.CompileQuery`
**before** EF Core's query-translation pipeline, replaces queryable roots with stubs,
substitutes parameters, and ships the tree. This is v1's proven pattern, rebuilt for EF Core 10.

**Evidence.**
- EF Core 10 `IDatabase.CompileQuery<TResult>(Expression query, bool async)`
  (`src/EFCore/Storage/IDatabase.cs:57`) is the provider's query entry point. Our provider
  implements `IDatabase` and receives the expression here.
- v1 `InfoCarrierDatabase.CompileQuery`
  (`subrepos/infocarrier-v1/src/InfoCarrier.Core/Client/Storage/Internal/InfoCarrierDatabase.cs:59`)
  does exactly this: `EntityQueryableStubVisitor.Replace(query)` swaps `EntityQueryable`
  constants for `RemoteQueryableStub<T>`; a `SubstituteParametersExpressionVisitor` resolves
  compiled-query parameters from `queryContext.ParameterValues`; then translates.
- **Why B over A (post-translation):** post-translation trees contain shaper delegates,
  provider-specific nodes, and compiled materialization closures that are *not* portable
  across the wire and *not* re-executable on a different provider (client InMemory-shim vs
  server SQL Server). Raw capture keeps the tree in a form the server can re-translate against
  its own provider. This is the same reason rlinq serializes the pre-translation tree.

**Consequence.** The server owns translation against the real provider (SQL Server /
PostgreSQL / InMemory). The client never sees SQL. The projection-split problem
(requirements §3) is handled by shipping the *whole* tree and letting the server execute the
entity-typed portion; see Q-split below.

## 2. Q3 — `QueryRootExpression` shape & stub — RESOLVED

EF Core 10 `QueryRootExpression` (`src/EFCore/Query/QueryRootExpression.cs`) is an abstract
`Expression` with `ExpressionType.Extension`, `QueryProvider`, `ElementType`, and abstract
`DetachQueryProvider()`. `EntityQueryRootExpression`
(`src/EFCore/Query/EntityQueryRootExpression.cs`) adds `IEntityType EntityType` and prints as
`DbSet<T>()` (or `DbSet<T>("Name")` for shared-type entities).

**Implication.** On the wire the query root is represented by a **stub carrying the entity
type identity** (name + CLR type). The server rebinds stub → `DbSet<T>` → the server's own
`EntityQueryRootExpression` via its model (`context.Set<T>()` / `Model.FindEntityType`).
This matches v1's `RemoteQueryableStub<T>` + server rebinding.

## 3. Q4 / T-shared — Shared-type entity resolution — RESOLVED

`IModel.FindEntityType` has overloads by CLR type **and by name** (including
`FindEntityType(string name, string definingNavigationName, ...)` for owned/shared types,
`src/EFCore/Metadata/Internal/Model.cs:1398`). Shared-type entities (`Dictionary<string,object>`
backing) are keyed by **name**, not CLR type.

**Rule (locks requirement §2.7).** Entity identity on the wire carries the EF **entity-type
name** (which for shared types is the distinguishing key) plus the CLR type for shared-assembly
resolution. Server resolves via `model.FindEntityType(clrType)` first, falling back to
name-based lookup (`FindEntityType(name)` + defining-navigation scan). This directly fixes v1's
shared-type failure.

## 4. T1 / T2 — Test infrastructure — RESOLVED

- **T2: `ComplianceTestBase` exists** in EF Core 10 at
  `test/EFCore.Specification.Tests/TestUtilities/ComplianceTestBase.cs`. We copy v1's
  `InfoCarrierComplianceTest` pattern to assert every spec-test base is implemented.
- **T1: base classes confirmed present** in the `Microsoft.EntityFrameworkCore.Specification.Tests`
  NuGet package: `TestStore` (`TestUtilities/TestStore.cs`), `ITestStoreFactory`,
  `InMemoryTestStore` (`test/EFCore.InMemory.FunctionalTests/TestUtilities/InMemoryTestStore.cs`).
  `TestStore.InitializeAsync` takes `(IServiceProvider, Func<DbContext>, seed, clean)` — the v1
  fixture seam ports. The EF Core 3.1→10 rename to the `Northwind*QueryTestBase` family is real;
  we target those bases (exact subclass map is an implementation-time detail, not a blocker).
- `InMemoryTestStore.AddProviderOptions` → `builder.UseInMemoryDatabase(Name)` confirms the
  backend store pattern. `InfoCarrierBackendTestStore` will double as `IInfoCarrierClient` and
  round-trip every request/result through JSON (v1 `SimulateNetworkTransferJson`).

## 5. Q1 — Node-DTO set — RESOLVED (minimal set)

Because we capture the **pre-translation** LINQ tree (ADR-006=B), the node set is what the C#
compiler + LINQ produce, not EF's post-translation extensions. Required node DTOs:
`Constant`, `Parameter`, `Member`, `MethodCall`, `Lambda`, `New`, `NewArray`, `MemberInit`,
`ListInit`, `Binary`, `Unary`, `TypeBinary`, `Conditional`, `Invocation`. **Excluded** (LINQ-to-EF
never produces): Block/Loop/Try/Goto/Switch/Label/DebugInfo/Throw/Dynamic. EF extension nodes
(`QueryRootExpression`) are represented by the stub (Q3), not serialized as extension nodes.

## 6. Q2 — Parameter substitution (the v1 `ValueWrapper<T>` trap) — RESOLVED

v1's bug: wrapping parameter values in a custom generic struct `ValueWrapper<T>` as a tree
constant broke translation. **Rule:** substitute compiled-query parameters as **plain
`ConstantExpression` of the runtime value** (typed to the parameter's type), resolved from
`queryContext.ParameterValues`. No wrapper types. Values that are entities/collections/closures
serialize as typed dynamic values (§7), never wrapped.

## 7. Q9 — Entity type identity (aqua collision fix) — RESOLVED

Aqua's shape-based `TypeInfo` provably merges same-shape types. **Rule:** entity-typed values on
the wire are identified by **EF entity-type name + key values** (resolved through the model), not
by CLR shape. Non-entity dynamic values (anonymous/DTO projections) use shape-based identity
(FullName + generic args + ordered property list) — collisions there are acceptable because they
are client-side projections materialized client-side (requirements §3). Entities must never merge;
projections may.

## 8. Q-split (A2) — Projection boundary — RESOLVED (deferred execution, not tree surgery)

Per ADR-006=B and requirements §3.2: the server receives the full tree. The server detects the
boundary where server-unknown types (anonymous/DTO/value-tuple) appear and executes only the
entity-typed prefix, returning identity-keyed row data; the client applies the final projection
locally after materialization + identity resolution. This avoids fragile tree partitioning:
the boundary is the last `Select` whose element type the server's model knows. Implementation
detail lives in the server query executor (Step 5/9), not the wire format.

## 9. W2 — SaveChanges store-generated value keying — RESOLVED

Change entries carry a client-assigned **correlation id** per entry (index in the submitted
list). Server replays in order against a real context, calls `SaveChanges`, then returns
store-generated values **keyed by the same correlation id**. Client maps them back onto its
tracked entries by position. Temp keys (Added with client temp key) are resolved server-side by
EF's normal fixup during replay; the correlation id bridges the client's temp key and the
server's real key.

## 10. Remaining Q/W — deferred to implementation (non-blocking)

Q5 (cache key from canonical serialization), Q6/W4 (streaming + identity), Q7 (NTS Z/M via WKT),
Q10 (server compiled-delegate cache), W1 (minimal column payload), W3 (transaction token),
W5 (exception fidelity), W6 (cancellation) are **implementation-phase** concerns that do not
block scaffolding or the first green test. They are tracked here and resolved in the relevant
steps. Defaults adopted now: WKT for spatial (Q7); server translates to `IQueryable` and lets EF
compile, with a compiled-query cache added once canonical serialization (Q5) lands; streaming
buffers for identity resolution initially, optimized later (W4).

---

## Locked consequences for implementation

1. **ADR-006 = B.** Client captures raw LINQ at `IDatabase.CompileQuery`; server translates.
2. **ADR-008 locked** per expression-serialization §3 with the node set of §5 above and the
   entity-identity rule of §7.
3. Build order per ADR-003 proceeds; Steps 1–4 unblock the vertical slice.
