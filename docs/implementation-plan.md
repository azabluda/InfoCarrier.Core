# Implementation Plan — Steps 5–9 (Query Pipeline)

Status: **IN PROGRESS** · Build order per [ADR-003](decisions.md) · Design per
[ADR-006](decisions.md) (raw capture), [ADR-008](decisions.md), [`research-findings.md`](research-findings.md).

Each checkbox is one minimal, logically-complete substep, committed individually.
Not every substep is independently compilable — the milestone is the commit boundary.

---

## Phase A — Expression DTO model (wire representation of the tree)

The serializable node-DTO set. Minimal per research-findings §5: only nodes LINQ-to-EF
produces; no Block/Loop/Try/Goto/Switch/Label.

- [x] **A1.** `ExpressionNode` abstract base + `NodeKind` enum (explicit map, no int-cast ABI)
      + `TypeNode` (assembly-free type identity: FullName + generic args; entity-typed values
      carry EF entity-type name per research-findings §7). ✅ `196998e`
- [x] **A2.** Leaf nodes: `ConstantNode` (typed value payload), `ParameterNode` (name + type),
      `MemberNode` (declaring type + member name + kind), `MethodNode` (declaring type +
      name + generic args + signature hash). ✅ `85c82a8`
- [x] **A3.** Composite nodes: `MethodCallNode`, `LambdaNode`, `NewNode` (ctor + args),
      `NewArrayNode`, `BinaryNode`, `UnaryNode`. ✅ `96d1c07`
- [x] **A4.** Init/conditional nodes: `MemberInitNode` (+ bindings), `ListInitNode`,
      `ConditionalNode`, `TypeBinaryNode`, `InvocationNode`, `QueryRootStubNode`
      (research-findings §2 — entity-type identity, replaces `EntityQueryRootExpression`). ✅ `670172c`
- [x] **A5.** `DynamicValueNode` for non-primitive constants (aqua-style shape-based value
      graph: anonymous types, DTOs, collections, entities-by-key). Client-materialized, so
      shape collisions acceptable (research-findings §7). Serializer source-gen context for AOT. ✅ `f7f0de7`

## Phase B — Bidirectional translators (System.Linq.Expressions ↔ DTO)

Direct recursive translators (no rlinq `ResultWrapperExpression` hack). Partial-eval policy
explicit/configurable (not rlinq's heuristic).

- [x] **B1.** `IExpressionSerializer` seam: `ExpressionNode ToNode(Expression)` /
      `Expression ToExpression(ExpressionNode)`. DI-resolved, no statics. ✅ `456e428`
- [x] **B2.** `ExpressionToNodeTranslator` — leaf + composite nodes (A2/A3). ✅ `3d5209a`
- [x] **B3.** `ExpressionToNodeTranslator` — init/conditional + `QueryRootExpression`→
      `QueryRootStubNode` + constants → `DynamicValueNode` (A4/A5). Handles compiled-query
      parameters already substituted as plain constants (research-findings §6). ✅ `8ebf2f8`
- [x] **B4.** `NodeToExpressionTranslator` — full reverse; `QueryRootStubNode`→server
      `EntityQueryRootExpression` via `IModel` (research-findings §2/§3, shared-type by name). ✅ `76a742e`
- [x] **B5.** `DynamicValueMapper` — EF-metadata-driven (not blind reflection): entities via
      `IProperty` accessors; anonymous/records via ctor-param matching (aqua §2.3).
      Reference preservation per-message. ✅ `aa1087a`
- [x] **B6.** Round-trip unit tests: canonical tree shapes serialize→deserialize→identical. ✅ `8f6dc65`

## Phase C — Client provider (capture)

- [x] **C1.** `InfoCarrierOptionsExtension` + `UseInfoCarrier(...)` `DbContextOptionsBuilder`
      extension; `AddInfoCarrierClient` DI registration (DI-first, requirements §4.2). ✅ `53df56d`
- [x] **C2.** `InfoCarrierDatabase : IDatabase` — `CompileQuery` raw capture (ADR-006):
      substitute compiled-query params as plain constants; route through `IExpressionSerializer`;
      build `QueryDataRequest`; `SaveChanges`/`SaveChangesAsync` → Step 10 shell. ✅ `0bc999b`
- [x] **C3.** Client query executor — sends via `IInfoCarrierClient`, returns
      sync/async results (single vs sequence), respects `QueryTrackingBehavior`. ✅ `0bc999b`

## Phase D — Server execution

- [x] **D1.** `ServerQueryExecutor` — deserialize tree, rebind `QueryRootStubNode`→
      `context.Set`/real query roots (shared-type by name), execute against server context
      (`IQueryable` → EF compile), projection-boundary detection (research-findings §8). ✅ `d339cbb`
- [x] **D2.** Wire `InProcessInfoCarrierServer.QueryDataAsync` → executor; map entity results
      to identity-keyed rows / projections to columnar data; `IsEntityResult` routing. ✅ `f1b5732`

## Phase E — Materialization + first green

- [x] **E1.** Client materializer — entity identity resolution (reuse tracked / attach),
      populate scalars via value converters, nav fixup from FK, mark included loaded
      (requirements §2.5). ✅ `394a0bb`
- [x] **E2.** Projection application — non-entity results materialized client-side from
      columnar data (requirements §3.2). ✅ `b746571`
- [x] **E3.** First green InMemory Northwind functional test via spec-test fixture
      (architecture §5). Commit milestone. ✅ `e2911cf` — InMemory smoke test green (7/7);
      full Northwind spec-test fixture is S4.

---

## Later (Step 10+)

- [ ] **S1.** SaveChanges client capture (`InfoCarrierDatabase.SaveChanges(Async)`).
- [ ] **S2.** SaveChanges server replay + store-generated value return (M2M from day 1).
- [ ] **S3.** Transactions (begin/commit/rollback + token, wire W3).
- [ ] **S4.** Compliance meta-test (`ComplianceTestBase`) + expand spec coverage; SqlServer
      (Docker) backend.

---

## Spec-test fixture (ADR-004 — the REAL test coverage, not the 7-test smoke)

The 7-test smoke proves the vertical slice; the actual coverage goal is inheriting
`EFCore.Specification.Tests` bases via an InfoCarrier fixture (v1 pattern → EF Core 10).
Port map studied from v1 + EF Core 10 sources (see session research).

- [x] **F1.** `SharedTestStoreProperties` capture struct (ContextType, OnModelCreating,
      OnAddOptions, CopyDbContextParameters). ✅ `9170e30`
- [x] **F2.** `InfoCarrierBackendTestStore : TestStore, IInfoCarrierClient` — server provider
      in ctor, JSON round-trip (`SimulateNetworkTransferJson` via `IInfoCarrierSerializer`),
      abstract `AddServices`. EF Core 10 async shape (`InitializeAsync`/`CleanAsync`/`DisposeAsync`). ✅ `89285f7`
- [x] **F3.** `InMemoryInfoCarrierBackendTestStore` — InMemory backend (`AddEntityFrameworkInMemoryDatabase`). ✅ `89285f7`
- [x] **F4.** `InfoCarrierTestStore : TestStore` — client wrapper; `InitializeAsync` delegates to
      backend; `AddProviderOptions` → `UseInfoCarrier(backend)`. ✅ `d34f998`
- [x] **F5.** `InfoCarrierTestStoreFactory : ITestStoreFactory` (4 members) + `EnsureInitialized`
      lazy singleton; `AddProviderServices` → `AddEntityFrameworkInfoCarrier`. ✅ `a6a609b`
- [x] **F6.** `NorthwindQueryInfoCarrierFixture<TModelCustomizer> : NorthwindQueryFixtureBase<TModelCustomizer>`
      (constraint `ITestModelCustomizer`), override `TestStoreFactory`. ✅ `1468b02`
- [~] **F7.** First concrete Northwind test class (`NorthwindWhereQueryInfoCarrierTest`).
      **Fixture online; 413 inherited tests discovered.** `31dccb6` (class + fixture),
      `45f7000` (ILoggerFactory resolution). **Currently 141 passed / 272 failed** — see
      Failure triage below. Not complete until the mechanical causes are cleared.
- [ ] **F8.** `InfoCarrierComplianceTest : ComplianceTestBase` (`TargetAssembly` only).

### Failure triage (measured 2026-08-01, 141/413 passing)

The 272 failures reduce to a handful of root causes, not 272 problems:

| Count | Root cause | Fix |
|---|---|---|
| 138 | `NotSupportedException: Unsupported extension expression: QueryParameterExpression` | EF Core 10's funcletizer emits `QueryParameterExpression` (an *extension* node), but `QueryExecutor.SubstituteParametersExpressionVisitor` only overrides `VisitParameter`. Add a `VisitExtension` override resolving `qp.Name` against `QueryContext.Parameters`. |
| 76 | `NotSupportedException: JsonTypeInfo metadata for type 'System.Int32' … not provided` | `ConstantNode.PrimitiveValue` is `object?`; `ExpressionJsonContext` registers the 19 node types but no primitives. Register primitives, or chain a fallback resolver. |
| ~26 | `Entity type '<>f__AnonymousType…' / 'System.String' not found in the server model` | **Projection split (requirements §3) — unimplemented.** Real design work, not a bug. |
| ~32 | Tail: `JsonElement`→`String` coercion, `IsGenericParameter`, value mismatches | Investigate after the above. |
