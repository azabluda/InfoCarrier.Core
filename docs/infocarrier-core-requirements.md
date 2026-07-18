# InfoCarrier.Core — Requirements Specification

> Pure requirements and essential ideas. No v1 implementation references.

---

## 1. Core Concept

InfoCarrier.Core is an **Entity Framework Core database provider** that proxies all database
operations to a remote server. The client has no direct database connection.

### Data Flow

```
Client App → InfoCarrier DbContext → [serialize] → wire → [deserialize] → Server DbContext → Real DB
                                      (SQL Server / PostgreSQL / InMemory)
```

### Key Principle

From the client application's perspective, the InfoCarrier `DbContext` behaves identically to a
local EF Core provider. LINQ queries, change tracking, `SaveChanges`, navigation fixup, and
identity resolution all work transparently.

---

## 2. Functional Requirements

### 2.1 Query Execution

- Client compiles LINQ queries into a serializable representation of the expression tree.
- Server receives the expression, binds it to a real `DbContext`, executes it against the
  database, and returns results.
- Client materializes results into tracked entities with correct identity resolution and
  navigation property fixup.
- All EF Core query capabilities must be supported: projections, filtering, ordering, grouping,
  joins, includes (`ThenInclude`), split queries.

### 2.2 Change Tracking & SaveChanges

- Client tracks entity state changes (Added, Modified, Deleted) using standard EF Core change
  tracking.
- On `SaveChanges`, the client serializes the change tracker state and sends it to the server.
- Server replays the changes against a real `DbContext` and calls `SaveChanges`.
- Store-generated values (identity keys, computed columns, concurrency tokens, default values)
  flow back to the client and update the tracked entities.
- Many-to-many relationships, owned types, and table splitting must be handled correctly.

### 2.3 Expression Tree Serialization

The serialization engine must handle all expression node types that EF Core can produce:

- `ConstantExpression` — including closures, captured variables, entity instances
- `MemberExpression`, `MethodCallExpression`, `NewExpression`, `BinaryExpression`
- `ParameterExpression` — with remapping between client and server parameter identities
- `LambdaExpression`, `UnaryExpression`, `TypeBinaryExpression`
- `ConditionalExpression`, `MemberInitExpression`, `ListInitExpression`
- Compiler-generated closure types, anonymous types

The serialized form must survive a round-trip through the wire protocol without type fidelity
loss.

### 2.4 Wire Protocol

The protocol between client and server must support these operations:

| Operation | Client → Server | Server → Client |
|-----------|---------------------|----------------------|
| **Query** | Serialized expression tree + query parameters | Materialized result rows |
| **SaveChanges** | Serialized entity change entries | Updated property values (store-generated) |
| **Transaction** | Begin / Commit / Rollback | Acknowledgement |

All operations must have asynchronous variants. The protocol must be transport-agnostic
(works over HTTP, gRPC, in-process, or any other transport).

### 2.5 Entity Materialization

When the client receives query results from the server for entity types:

1. Deserialize row data into entity instances.
2. Resolve identity: if an entity with the same key is already tracked, reuse it; otherwise
   attach the new instance.
3. Populate scalar properties, applying any configured value converters.
4. Wire up navigation properties (reference and collection) based on foreign key relationships.
5. Mark included navigations as loaded.

For queries that project to non-entity types (anonymous types, DTOs, value tuples), the server returns the necessary entity data (or raw column values) and the client applies the projection locally after materialization.

### 2.6 Server-Side Query Binding

When the server receives a query:

1. Replace abstract resource descriptors in the expression tree with the server's actual
   `DbSet<T>` instances.
2. Rewrite `DbSet<T>` references into `QueryRootExpression` nodes that EF Core's query
   pipeline can consume.
3. Handle shared-type entities (entities backed by `Dictionary<string, object>`) correctly.
4. Execute the query and map results into the wire format.

### 2.7 Shared / Owned Entity Types

The provider must correctly handle:
- **Owned types** (value objects mapped to the same table or a separate table)
- **Shared-type entities** (`Dictionary<string, object>` backing with CLR type resolution
  that differs from standard entity types)
- **Table splitting** and **table-per-hierarchy (TPH)** inheritance

> **Shared assembly approach**: Entity types and their configurations are defined in a shared assembly referenced by both client and server projects. This enables the server to understand all entity types used in projections, while acknowledging limitations for anonymous projections and ad-hoc DTOs.

### 2.8 Spatial Data

If `NetTopologySuite` is configured, spatial types (`Point`, `LineString`, `Polygon`, etc.)
must serialize and deserialize without data loss, including Z/M ordinates.

### 2.9 Transactions

- `DbContext.Database.BeginTransaction()` must propagate to the server.
- Nested transactions (savepoints) must be supported if the underlying provider supports them.
- Transaction disposal/rollback on the client must clean up on the server.

---

## 3. Fundamental Constraint: Client/Server Type Boundary

### 3.1 The Problem

The server only has access to types from the **shared assembly** — entity types, owned types,
and their EF Core configuration. It does **not** have access to:

- Anonymous types produced by `.Select(...)` projections
- Client-local DTO classes
- Value tuples and other ad-hoc types
- Any type defined solely in the client application

This means the server **cannot materialize arbitrary LINQ projections**. It can only materialize
types it knows about.

```csharp
// This works: the server knows about Order
var orders = ctx.Orders.Where(o => o.Price > 100).ToList();

// This does NOT work naively: the server has no ClientOrderDto
var summaries = ctx.Orders
    .Select(o => new ClientOrderDto { Id = o.Id, Total = o.Price * o.Quantity })
    .ToList();
```

### 3.2 Implication

The query pipeline must split responsibility:

1. **Server-side**: Execute the database portion of the query using only shared entity types.
   Return results in a type-agnostic format (columnar data / raw values).
2. **Client-side**: Apply the final projection (`Select`, anonymous type creation, DTO
   mapping) locally on the received data.

This split must be transparent to the application developer — the same LINQ expression must
produce the same result as if the database were local.

### 3.3 Design Requirement

The expression tree must be analyzed and partitioned at the boundary where server-unknown
types appear. The server executes the portion it can; the client applies the remainder.
This partitioning must preserve query correctness while minimizing data transfer (the server
should only return the columns needed for the client-side projection, not full entities).

---

## 4. Non-Functional Requirements

### 4.1 Serialization Abstraction

The serialization format must be abstracted behind an interface. The library must not hardcode
a specific serializer (JSON, Protobuf, MessagePack, etc.).

### 4.2 Dependency Injection

All components must be registered and resolved via standard `IServiceCollection` / DI.
The library must integrate naturally with `AddDbContext<T>` and the EF Core options pattern.

### 4.3 Async-First

All I/O operations (queries, SaveChanges, transactions) must be async. Sync overloads may
exist but must never block on async code.

### 4.4 Streaming

The wire protocol should support streaming large result sets via `IAsyncEnumerable<T>` rather
than buffering everything into a `List<T>`.

### 4.5 AOT / Trimming Compatibility

Where feasible, the library should be compatible with .NET trimming and Native AOT. This means
avoiding reflection-heavy patterns and supporting `System.Text.Json` source generators.

### 4.6 Extensibility

The provider must allow:
- Custom serialization backends
- Custom transport implementations
- Hooks for value conversion and type mapping
- First-class support for `NetTopologySuite` and other EF Core extensions

---

## 5. Target Platform

- **.NET 10**, **EF Core 10**
- Provider library: `net10.0`
- Server backends: SQL Server, PostgreSQL, InMemory (minimum)
- Transport: transport-agnostic (HTTP, gRPC, in-process)

---

## 6. Out of Scope (Initial Release)

- Authentication / authorization (protocol must not preclude adding it later)
- Offline / disconnected mode with local caching
- Client-side query composition beyond what EF Core tracking allows
- Multi-tenant server-side `DbContext` resolution (protocol must not preclude it)
