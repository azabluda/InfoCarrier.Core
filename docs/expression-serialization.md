# Expression Serialization — Research & Design

Status: **PRE-IMPLEMENTATION (research in progress)** · Decision frame: [ADR-001](decisions.md#adr-001-serialization-engine-greenfield-spec-only-locked-2026-07-19) (greenfield, spec-only — LOCKED), [ADR-008](decisions.md) (design direction — PROVISIONAL).

This document is the specification for how InfoCarrier.Core v2 serializes
`System.Linq.Expressions.Expression` trees for the wire. It records (a) what we learned
from third-party code, (b) the design direction that follows, and (c) the **open research
questions** that must be answered by further study before the design is locked.

---

## 1. Requirements (from `infocarrier-core-requirements.md` §2.3)

The engine must:

- Round-trip `Expression` trees **losslessly** across the wire.
- Handle `ConstantExpression` carrying arbitrary .NET objects (entities, collections,
  closures, captured variables).
- Remap `ParameterExpression` identity between client and server.
- Preserve type fidelity for `MemberExpression`, `MethodCallExpression`, `NewExpression`,
  `LambdaExpression`, `UnaryExpression`, `TypeBinaryExpression`, `ConditionalExpression`,
  `MemberInitExpression`, `ListInitExpression`.
- Survive proxy types, anonymous types, and compiler-generated closure types.
- Be transport- and format-agnostic (abstraction over the serializer; no hardcoded
  JSON/protobuf).

Non-functional (requirements §4): DI-first, async-first, AOT/trimming-friendly where
feasible, extensible (custom serializers, value converters, NetTopologySuite first-class).

## 2. What we studied

| Source | Role | What it taught us |
|---|---|---|
| `subrepos/rlinq` (Remote.Linq 7.3.3) | inspiration | Serializable node-DTO model, translators, partial-eval, constant wrapping, serializer seam |
| `subrepos/aqua` (aqua-core 5.5.1) | inspiration | `DynamicObject` value graphs, shape-based `TypeInfo`, ctor-param matching, reference maps, FormatterServices trap |
| `subrepos/infocarrier-v1` | inspiration | v1 lessons: Castle-proxy serialization bug, `ValueWrapper<T>` partial-eval bug, no-query-caching, >1 MB JSON stack hack |
| `subrepos/efcore` (EF Core 10) | authoritative | Query-pipeline internals the serializer must interoperate with (see §Open questions) |

### 2.1 Remote.Linq — findings

- **Own serializable DTO model** (`src/Remote.Linq/Expressions/`): ~22 `[DataContract]`
  node classes; reflection objects never cross the wire — Aqua `TypeInfo`/`MethodInfo`/
  `ConstructorInfo` metadata and Aqua `DynamicObject` values do.
- **Translation**: two visitors (`SystemToRemoteLinqTranslator`, `RemoteToSystemLinqTranslator`)
  behind small context interfaces; a `PartialEval` pass (MSDN SubtreeEvaluator) folds
  closures into constants *before* translation.
- **Constants**: non-primitive values → `DynamicObject` inside a `ConstantQueryArgument`
  DTO. This is how anonymous types / client DTOs cross.
- **Serializer seam**: STJ built-in (`JsonSerializerOptionsExtensions.ConfigureRemoteLinq`),
  Newtonsoft and protobuf-net as separate packages. **No wire versioning** (only
  `[DataMember(Order)]`).

### 2.2 Remote.Linq — anti-requirements (do NOT copy)

- `TypeResolver.Instance` **mutable static** (violates DI-first).
- **No compiled-query caching** — every call does `.Compile().DynamicInvoke()` fresh.
- **Trust-by-default server** — no method/node allowlists; resolves whatever arrives.
- **Unversioned wire** — client/server move in lockstep.
- `ResultWrapperExpression` visitor hack; int-cast enum ABI across the boundary.

### 2.3 Aqua — findings

- **`DynamicObject { TypeInfo, PropertySet }`** — the right *shape* for crossing the
  client/server type boundary; identity is assembly-free (FullName + generic args +
  MetadataToken-ordered property list; **no assembly/version** travels).
- **Ctor-param matching** rehydrates anonymous/record types client-side (param↔property by
  name, case-insensitive, assignable type; parameterless ctor wins).
- **Per-message reference maps** (`ToContext`/`FromContext`, `ReferenceEqualityComparer`-keyed)
  preserve identity and circular references — mandatory for EF identity resolution and
  circular navigation refs.

### 2.4 Aqua — anti-requirements (do NOT copy)

- **`FormatterServices` / `GetUninitializedObject`** — gated `< net8`, `[Obsolete(SYSLIB0050,
  true)]`; it is the mechanism of the **v1 Castle-proxy bug** (reads proxy fields instead of
  entity properties). Never use it.
- **IL-emit fallback** (`TypeEmitter`) as a default — not AOT/trim-safe; cannot emit circular
  type graphs.
- **`SilentlySkipUnassignableMembers = true`** default — data-corruption risk.
- **Name-only type resolution** for entities (provably merges same-shape types from
  different assemblies).
- Serializers without reference preservation (rules out plain protobuf-net, vanilla STJ
  without `ReferenceHandler`).

## 3. Design direction (PROVISIONAL — ADR-008)

rlinq-style node DTOs + aqua-style `TypeInfo`/`DynamicObject`, but:

1. **EF-metadata-driven mapper** — map entities via `IModel` metadata (entity types, shadow
   properties, keys, value converters), not blind public-reflection walks.
2. **Strict allowlists ON by default** — allowed node types, `MethodInfo`s (Queryable /
   Enumerable / `EF.Functions` / model-bound members), and deserializable types (model
   entities + registered projection types). Drop node types LINQ-to-EF never produces
   (Block/Loop/Try/Goto/Switch/Label).
3. **DI everywhere** — no statics; all components resolved from `IServiceProvider`.
4. **Versioned envelope** — protocol version in every message from day 1 (see
   [`wire-protocol.md`](wire-protocol.md)).
5. **Reference-preserving serializer** — circular nav refs must survive.
6. **Compiled-query cache** keyed by a structural hash ⇒ serialization must be **canonical
   and deterministic**.
7. **Explicit enum maps** — no int-cast across the System↔remote boundary.
8. **No `FormatterServices`, no IL-emit default** — instances created via EF materializer
   paths or matched constructors; values read through properties / EF `IProperty` accessors
   so lazy-loading proxies forward correctly.

## 4. Open research questions (to resolve BEFORE locking ADR-008)

| # | Question | Study source | Why it matters |
|---|---|---|---|
| Q1 | Which `ExpressionType`s can EF Core 10's query pipeline actually emit at our capture point? (determines the minimal node-DTO set) | `subrepos/efcore` query compiler + captured trees | We only need to support nodes EF can produce; over-supporting invites the rlinq general-purpose bloat |
| Q2 | How does EF Core 10 represent query parameters (`QueryParameter` / `ParameterExpression` funcletization) and how do we substitute values safely (the v1 `ValueWrapper<T>` trap)? | `subrepos/efcore` `QueryCompilationContext`, parameter processing | Parameter substitution is where v1 broke |
| Q3 | What is the exact shape of `QueryRootExpression` in EF Core 10, and what stub must replace it on the wire? | `subrepos/efcore` `QueryRootExpression`, provider query roots | Server must rebind stubs → real `DbSet<T>` → `QueryRootExpression` |
| Q4 | How are shared-type entities (`Dictionary<string,object>`) and owned types resolved in EF Core 10 (`model.FindEntityType(clrType)` + fallback scan)? | `subrepos/efcore` model APIs | Requirement §2.7; v1 failed here |
| Q5 | Can a compiled-query cache key be derived structurally from the serialized tree (canonical form), or must we hash the pre-serialization tree? | our design + rlinq comparison | Caching is a stated v2 advantage over rlinq |
| Q6 | How do `IAsyncEnumerable<T>` streaming results interact with materialization + identity resolution (per-row vs buffered)? | `subrepos/efcore` async query enumeration | Requirement §4.4 streaming |
| Q7 | NetTopologySuite: which spatial values appear in trees/results, and how do we serialize Z/M without loss (WKT vs configured GeoJSON)? | `subrepos/efcore` NTS plugin + v1 lesson | Requirement §2.8; v1 lost Z/M |
| Q8 | Where exactly is the client capture point (ADR-006 A/B), and which EF internals does it touch? | `subrepos/efcore` `Database.CompileQuery`, shaper construction | Locks ADR-006 |
| Q9 | Type-identity for entities across the wire: what do we add to aqua's shape-based `TypeInfo` to make collisions impossible (EF entity-type identity / assembly hint)? | aqua TypeSystem + our model | aqua provably merges same-shape types; entities must not merge |
| Q10 | Compiled vs interpreted server execution: cache compiled delegates, or translate to a server `IQueryable` and let EF compile? | `subrepos/efcore` + rlinq executor comparison | Server perf + correctness |

## 5. v1 pitfalls checklist (verified against sources)

- [ ] Castle dynamic proxies — read values through properties/EF accessors, never
      `FormatterServices` fields (aqua §2.4).
- [ ] `ValueWrapper<T>`-style generic structs as tree constants — do not wrap values in
      custom generic structs (rlinq §2.1 partial-eval).
- [ ] Shared-type entity CLR resolution — `model.FindEntityType(clrType)` + fallback scan
      (Q4).
- [ ] Spatial Z/M loss — WKT with 3D ordinates or correctly configured GeoJSON (Q7).
- [ ] M2M SaveChanges fixup — test from day 1 (see [`architecture.md`](architecture.md)).
- [ ] >1 MB payload deserialization stack depth — v1 used a 10 MB-stack thread; decide our
      approach (recursion-safe deserializer or depth limits).
