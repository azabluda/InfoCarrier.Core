# MEGA_PROMPT — InfoCarrier.Core

> **Purpose**: This document is a self-contained, executable prompt for an AI coding agent
> (Cline, OpenCode, or SpecKit) to build InfoCarrier.Core from scratch.
>
> **Target**: .NET 10, EF Core 10, greenfield implementation.
>
> **"SpecKit Planning Phase" sections** mark areas where the AI should pause, research, and
> propose a concrete plan before writing code.

---

## 0. What is InfoCarrier?

InfoCarrier is a custom **Entity Framework Core database provider** that does NOT connect to a
real database on the client side. Instead:

1. The **client** serializes LINQ expression trees and entity change-tracking state.
2. The **wire protocol** transports them to a server.
3. The **server** executes against a real EF Core context (SQL Server, PostgreSQL, InMemory).
4. Results flow back, are **materialized into tracked entities** on the client.
5. Entity identity, navigation fixup, and `SaveChanges` work exactly as if the database were local.

```
Client DbContext.SaveChanged() ──► SaveChangesRequest ──► Server DbContext.SaveChanged()
                                  (serialized entries)       (real SQL)

Client DbContext.Orders.Where() ──► QueryDataRequest ──► Server DbContext.Orders.Where()
    (serialized expression tree)                              (real SQL execution)
```

---

## 1. Lessons Learned from v1

The original InfoCarrier.Core v1 (EF Core 5, Remote.Linq v6.2.3, Aqua v4.5.3) works but has
pain points. **Do NOT repeat these mistakes:**

| Category | Problem | Root Cause | Mitigation |
|----------|---------|------------|---------------|
| **Proxy serialization** | Castle.Core dynamic proxies are `[Serializable]` but base types are not → `SerializationException` | Aqua `DynamicObjectMapper` with `UtilizeFormatterServices=true` (default) | Defer serialization engine choice; if using Aqua, set `UtilizeFormatterServices=false` from day 1 |
| **Expression partial eval** | `PartialEval` step tries to invoke `ValueWrapper<T>` constructor with wrong arg type | Generic struct type doesn't survive round-trip through DynamicObject | Avoid wrapping values in custom generic structs in expression trees |
| **Shared type entities** | `FindRuntimeEntityType(typeof(Dictionary<string,object>))` returns null | EF Core 5 model API doesn't resolve shared types by CLR type alone | must handle EF Core 10's shared type entity model correctly from the start |
| **GeoJSON Z/M loss** | `GeoJsonWriter` drops Z/M coordinates silently | Format limitation — not discovered until specific test ran | For spatial: prefer WKT with 3D ordinates, or ensure GeoJSON configured correctly |
| **M2M SaveChanges fixup** | Join-table row counts mismatch after update | Wire protocol doesn't correctly track M2M navigation changes through save | Thoroughly test M2M scenarios from day 1 |
| **Test suite ambition** | ~12,890 tests, 56 failures after months of work | Tried to retrofit a complex provider onto EF Core's full test suite | Build the test suite incrementally alongside the provider |

### Architecture Anti-Patterns from v1

- **Do NOT** use generic `ValueWrapper<T>` structs as expression tree constants — they break
  when the runtime type differs from the compile-time type.
- **Do NOT** assume `FindRuntimeEntityType()` works for all entity types — shared/owned types
  need special handling.
- **Do NOT** hardcode Newtonsoft.Json — use abstraction over serialization format.
- **Do NOT** put spatial serialization in a test-only value mapper — make it first-class.

---

## 2. Repository Structure

```
InfoCarrier.Core/
├── README.md
├── MEGA_PROMPT.md                    ← this file
├── .gitignore
├── .editorconfig
├── global.json                       ← pin .NET 10 SDK
├── Directory.Build.props             ← shared MSBuild properties
├── Directory.Build.targets
├── InfoCarrier.Core.sln
│
├── .github/
│   └── workflows/
│       ├── build.yml                 ← CI: build + test on push/PR
│       └── release.yml               ← CD: NuGet pack + publish
│
├── docs/
│   ├── architecture.md               ← high-level architecture decisions
│   ├── expression-serialization.md   ← research: options for LINQ expr serialization
│   ├── wire-protocol.md             ← message contracts
│   └── ci-cd.md                     ← CI/CD strategy
│
├── subrepos/                         ← 3rd-party source for reference (NOT git submodules)
│   ├── efcore/                       ← `git clone https://github.com/dotnet/efcore`
│   ├── rlinq/                        ← if Remote.Linq adopted
│   ├── aqua/                         ← if Aqua adopted
│   └── infocarrier-v1/               ← original InfoCarrier.Core v1 (non-authoritative, for inspiration only)
│
├── src/
│   ├── InfoCarrier.Core/             ← the provider library (netstandard2.1 or net10.0)
│   │   ├── InfoCarrier.Core.csproj
│   │   ├── Common/                   ← shared DTOs: QueryDataRequest/Result, SaveChangesRequest/Result
│   │   ├── Client/                   ← client-side: IDatabase, query compilation, result mapper
│   │   ├── Server/                   ← server-side: IInfoCarrierServer, query execution
│   │   └── Properties/
│   └── InfoCarrier.Core.Abstractions/ ← interfaces only (for testing)
│
├── test/
│   └── InfoCarrier.Core.FunctionalTests/
│       ├── InfoCarrier.Core.FunctionalTests.csproj
│       ├── TestUtilities/            ← test infrastructure, backend test store
│       ├── InMemory/                 ← InMemory provider functional tests
│       └── SqlServer/               ← SqlServer provider functional tests
│
└── samples/
    ├── BasicConsole/                 ← minimal client + server console app
    └── WebApi/                       ← ASP.NET Core server + client
```

> **subrepos/ convention**: These are plain cloned repositories (NOT git submodules). They
> must be listed in `.gitignore` so they never get committed. They exist purely for
> source-level reference when documentation is insufficient.

---

## 3. Phase 0: SpecKit Planning (EXECUTE FIRST)

Before ANY code is written, the AI MUST complete these planning steps:

### 3.0 Create the SpecKit Constitution

**Deliverable**: `docs/constitution.md`

Before any research or code, create the project constitution — the AI agent's rulebook
that governs how work is done. See §11 for the full list of items that belong here
(commit conventions, test skip policy, build discipline, etc.).

### 3.1 Expression Serialization Research

**Task**: Investigate options for serializing `System.Linq.Expressions.Expression` trees.

The expression tree is the heart of InfoCarrier — it's how the client communicates "what query
to run" to the server. The serialization engine must:

- Round-trip `System.Linq.Expressions.Expression` trees losslessly
- Handle `ConstantExpression` nodes containing arbitrary .NET objects (entities, collections, closures)
- Support `ParameterExpression` remapping (client parameters → server parameters)
- Handle `MemberExpression`, `MethodCallExpression`, `NewExpression` with type fidelity
- NOT fail on proxy types, anonymous types, or compiler-generated closures

**Options to evaluate:**

| Option | Pros | Cons |
|--------|------|------|
| **Remote.Linq vNext** | Battle-tested, purpose-built for this | Dependency on Aqua, opaque DynamicObject mapping |
| **Custom `ExpressionVisitor` + System.Text.Json** | Full control, no magic | Must solve all edge cases ourselves |
| **gRPC + Protobuf expression messages** | Type-safe, performant, streaming | Complex schema, expression trees are recursive |
| **Hybrid**: custom light mapper for expressions + System.Text.Json for payload | Balance of control and simplicity | Two serialization paths |

**Deliverable**: `docs/expression-serialization.md` with recommendation and rationale.

### 3.2 Wire Protocol Design

The wire protocol defines the contract between client and server. It must support:

- **Queries**: Client sends expression tree → Server returns materialized results
- **SaveChanges**: Client sends entity change entries → Server executes SQL → Returns updated values
- **Transactions**: Begin/Commit/Rollback
- **Async**: All operations must have async variants

**Deliverable**: `docs/wire-protocol.md` with message contracts (C# record types or Protobuf `.proto`).

### 3.3 Entity Materialization Strategy

How does the server serialize EF Core entities, and how does the client deserialize them into
tracked entities with proper identity resolution?

v1 used Aqua `DynamicObject` as an intermediate representation. must decide:

- Continue with DynamicObject? (if using Aqua)
- Use a custom DTO layer?
- Use Protobuf messages?

**Deliverable**: Section in `docs/architecture.md`.

### 3.4 Test Strategy

**must** mirror EF Core's official functional test suite (like v1 did). EF Core publishes
a shared test suite at `EFCore.Specification.Tests` that provider authors can reuse.

**Plan**:
1. Clone `subrepos/efcore/` at the EF Core 10 release tag
2. Reference `EFCore.Specification.Tests` NuGet package (not source)
3. Create InfoCarrier test classes that inherit from EF Core's test base classes
4. Start with InMemory backend (simpler), then add SqlServer
5. Build test coverage incrementally:
   - **Milestone 1**: Basic queries (NorthwindQueryTestBase)
   - **Milestone 2**: SaveChanges, identity, fixup
   - **Milestone 3**: Relationships, owned types, inheritance
   - **Milestone 4**: Spatial, many-to-many, advanced queries

**Deliverable**: Test plan section in `docs/architecture.md`.

---

## 4. Phase 1: Core Provider Skeleton

### 4.1 Project Setup

```
dotnet new sln -n InfoCarrier.Core
dotnet new classlib -n InfoCarrier.Core -f net10.0
dotnet new classlib -n InfoCarrier.Core.Abstractions -f net10.0
dotnet new xunit -n InfoCarrier.Core.FunctionalTests -f net10.0
```

### 4.2 Consumer-Facing Contracts (SpecKit Planning)

> **⚠️ SpecKit Planning Phase**: The interfaces and DTOs below are v1's contracts —
> provided as a reference point only. They are NOT authoritative for the new implementation.
>
> The SpecKit planning phase should design fresh, modern, DI-friendly contracts.
> Consider:
> - Generic `IInfoCarrierClient<TDbContext>` with typed context
> - `IInfoCarrierServer` as a hosted service with `IServiceProvider` integration
> - Streaming support (`IAsyncEnumerable<T>`) in the wire protocol
> - `System.Text.Json` serialization contexts for AOT compatibility
> - Strongly-typed message contracts (records or Protobuf)
>
> The v1 approach (simple interfaces + manual wire simulation) was pragmatic but
> amateurish. The new implementation should look like a professional .NET library
> with first-class DI, logging, and configuration.

### 4.3 High-Level Data Flow (Reference Only)

The provider skeleton implements the same conceptual pipeline as v1:

- **Client → Server**: Serialized expression tree + change entries
- **Server → Client**: Serialized query results + updated entry values
- **Server internally**: EF Core `IQueryProvider.Execute()` / `DbContext.SaveChanges()`

The exact types, serialization format, and wire protocol are determined in Phase 0.

---

## 5. Phase 2: Client Expression Serialization

> **⚠️ SpecKit Planning Phase**: The exact implementation depends on the serialization engine
> chosen in Phase 0. The steps below assume a custom `ExpressionVisitor` approach.

### 5.1 Expression Tree Conversion

The client must convert the EF Core expression tree into a serializable form:

1. **Parameter substitution**: Replace `QueryCompilationContext.QueryParameter` nodes
   with constant values from `QueryContext.ParameterValues`
2. **DbSet stub replacement**: Replace `QueryRootExpression` with stubs (like v1's
   `RemoteQueryableStub<T>`) that implement `IQueryable<T>` but serialize cleanly
3. **Partial evaluation**: Evaluate parts of the tree that can run locally (closures,
   captured variables) — but **carefully** avoid the v1 `ValueWrapper<T>` trap
4. **Serialization**: Convert the cleaned expression tree to the wire format

### 5.2 Client Result Materialization

After receiving `QueryDataResult` from the server:

1. **Deserialize** the result data
2. **Map to entities**: Match entity types from the model, look up existing tracked instances,
   create new ones
3. **Set navigation properties**: Wire up references and collections
4. **Mark as loaded**: Call `entry.SetIsLoaded()` for navigations that were included

---

## 6. Phase 3: Server Expression Rewriting

### 6.1 Resource Descriptor Replacement

The server receives an expression tree with "resource descriptors" (stubs representing
`DbSet<T>`). These must be replaced with the server's actual `DbSet<T>` instances.

### 6.2 DbSet Rewriting

After resource replacement, the expression has `DbSet<T>` constants. These must be
converted to `QueryRootExpression` nodes that EF Core's query pipeline understands.

**Critical**: Handle shared type entities (`Dictionary<string, object>`) correctly from day 1.
Use `model.FindEntityType(clrType)` + fallback scan of `model.GetEntityTypes()`.

### 6.3 Server Result Mapping

After executing the query, map entities to the wire format:

1. **Iterate** query results
2. **For each entity**: Extract scalar properties (with value converters), collect
   loaded navigations
3. **Serialize** to wire format using the chosen serialization engine

---

## 7. Test Infrastructure

### 7.1 Backend Test Store

Create a test `IInfoCarrierClient` implementation that:

- Creates an in-process `IInfoCarrierServer` backed by InMemory (or SqlServer)
- Simulates the wire protocol via JSON serialization (like v1's `SimulateNetworkTransferJson`)
- Runs on the same thread (no network, no HTTP)

**SqlServer backend**: Use a Docker SQL Server container, NOT LocalDB.
The test fixture should launch a pristine container (or reuse an existing one) and
create a fresh database per test batch. This works identically on Ubuntu and Windows.
Connection string template: `Server=localhost,1433;Database=InfoCarrierTest_{guid};User=sa;Password=InfoCarrier1!;TrustServerCertificate=true`
See [`docs/ci-cd.md`](docs/ci-cd.md) for the Docker run command.

### 7.2 Functional Test Classes

Each EF Core functional test base class gets an InfoCarrier subclass:

```csharp
public class NorthwindQueryInfoCarrierTest
    : NorthwindQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
{
    // Inherits all test methods from the base class
    // Override only to skip tests that are genuinely inapplicable
}
```

### 7.3 Skip Rules

Test skip policy, commit conventions, build discipline, and documentation standards
belong in `docs/constitution.md` — a SpecKit constitution file created during Phase 0.
The AI agent MUST produce this file before writing any code.

At minimum, the constitution should cover:
- Test skip policy (upstream InMemory parity, traceable `InfoCarrier#` identifiers, no silent skips)
- Commit frequency and message conventions
- Build-before-commit rule
- `MIGRATION_STATUS.md` maintenance

See §11 for additional items that should move into the constitution.

---

## 8. Sample Applications

### 8.1 BasicConsole

Minimal client + server in a console app:

```
samples/BasicConsole/
├── BasicConsole.sln
├── Server/Program.cs       ← ASP.NET Core Minimal API with InfoCarrier endpoint
├── Client/Program.cs       ← Console app using InfoCarrier DbContext
└── Shared/Models.cs        ← Entity types shared between client and server
```

### 8.2 WebApi

Full-stack sample:

```
samples/WebApi/
├── WebApi.sln
├── Server/
│   ├── Program.cs
│   ├── Controllers/InfoCarrierController.cs
│   └── appsettings.json
├── Client/
│   └── Program.cs          ← Could be Blazor WASM, console, or MAUI
└── Shared/
    └── Models.cs
```

---

## 9. CI/CD (GitHub Actions)

See [`docs/ci-cd.md`](docs/ci-cd.md) for the full CI/CD strategy.

Summary:
- `build.yml`: Restore → Build → Test on push/PR, ubuntu-latest with Docker SQL Server service container
- `release.yml`: `dotnet pack` + NuGet publish on tag push

---

## 10. Implementation Sequence (Ordered)

This is the execution order the AI agent MUST follow:

| Step | Phase | Description |
|------|-------|-------------|
| 1 | 0 | Clone `subrepos/efcore` (reference only, git-ignored) |
| 1.5 | 0 | Create `docs/constitution.md` (SpecKit rulebook — see §3.0, §7.3, §11) |
| 2 | 0 | Research expression serialization options → write decision doc |
| 3 | 0 | Design wire protocol → write contract types |
| 4 | 1 | Create solution + projects |
| 5 | 1 | Implement Common DTOs |
| 6 | 1 | Implement `IInfoCarrierClient` / `IInfoCarrierServer` interfaces |
| 7 | 1 | Implement test infrastructure (`InfoCarrierBackendTestStore`) |
| 8 | 1 | Implement server-side query execution |
| 9 | 1 | Implement client-side `IDatabase.CompileQuery` |
| 10 | 2 | Implement expression serialization (client → wire) |
| 11 | 2 | Implement result materialization (wire → client) |
| 12 | 3 | Implement expression deserialization (wire → server) |
| 13 | 3 | Implement entity-to-wire mapping (server → wire) |
| 14 | 4 | Implement SaveChanges pipeline |
| 15 | 5 | Add first functional tests (InMemory, basic queries) |
| 16 | 5 | Expand test coverage incrementally |
| 17 | 5 | Add SqlServer tests |
| 18 | 6 | Sample apps |
| 19 | 7 | CI/CD workflows |
| 20 | 8 | Performance profiling + optimization |

---

## 11. SpecKit Constitution

These meta-instructions, along with the skip rules from §7.3, should be formalized
into `docs/constitution.md` during Phase 0. The constitution is the AI agent's
rulebook — it governs how work is done, not what is built.

Move these items to the constitution:
- Commit frequency and conventions
- Push policy (never push unless asked)
- Build-before-test discipline
- Test naming conventions and skip policy
- When to pause and ask vs. when to proceed
- Reference subrepos usage (`efcore/`, `infocarrier-v1/` if cloned)
- `MIGRATION_STATUS.md` maintenance

The constitution file should be created as part of Phase 0 Step 1.5 (see §10).

---

## 12. Open Questions for SpecKit Planning Phase

These are deferred to the SpecKit planning phase (Phase 0). The AI agent should NOT implement
until these are resolved:

1. **Expression serialization engine**: Remote.Linq? Custom? gRPC? (Research → decide)
2. **Wire format**: System.Text.Json? Protobuf? MessagePack? (Depends on #1)
3. **Spatial handling**: Should NetTopologySuite be first-class or via value mapper?
4. **Async streaming**: Does the wire protocol support `IAsyncEnumerable<T>` for large result sets?
5. **Authentication/Authorization**: Out of scope for the initial release, but wire protocol should not preclude it.
6. **Caching**: Should the client cache compiled queries? Entity data? Metadata?
7. **Pagination / large result sets**: v1 materialized everything into `List<T>`. Should the new implementation support streaming for large queries? Affects wire protocol design.
8. **Client-side query composition**: Can the client compose further LINQ on returned entities?
   v1 disabled lazy loading during query execution — should do the same?
9. **Offline/disconnected scenarios**: Is purely online, or should there be a local cache?
10. **Multi-tenant server**: v1 used `CopyDbContextParameters` — might want first-class
    `DbContextFactory` pattern on the server side.

---

*This prompt is designed to be executed by an AI coding agent in iterative steps.
Each "SpecKit Planning Phase" marker is a checkpoint where the agent should pause
and present findings before proceeding to implementation.*
