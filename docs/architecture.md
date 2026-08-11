# Architecture — InfoCarrier.Core v2

Status: **PRE-IMPLEMENTATION (structure + strategy defined; internal seams provisional)**
· Decisions: [`decisions.md`](decisions.md) · Serializer: [`expression-serialization.md`](expression-serialization.md)
· Wire: [`wire-protocol.md`](wire-protocol.md)

High-level architecture of the EF Core 10 remote provider: how client, wire, and server fit
together; how entities materialize; and the test strategy. Authority: requirements in
[`infocarrier-core-requirements.md`](infocarrier-core-requirements.md) + build order in
[`decisions.md`](decisions.md) ADR-003.

---

## 1. System overview

```
Client App
   │  LINQ / SaveChanges (standard DbContext API)
   ▼
InfoCarrier Client Provider  ── CompileQuery / change-tracker capture
   │  serialize (expression-serialization) → wire envelope (wire-protocol)
   ▼
Transport (IInfoCarrierTransport)   ← HTTP / gRPC / in-process
   ▼
InfoCarrier Server  ── rebind stubs→DbSet<T>→QueryRootExpression, execute vs real provider
   │  (SQL Server / PostgreSQL / InMemory)
   ▼
serialize results → wire → client materialization (identity resolution + nav fixup)
```

**Key principle (requirements §1):** from the app's perspective the client `DbContext`
behaves identically to a local EF Core provider — LINQ, change tracking, `SaveChanges`,
navigation fixup, identity resolution all work transparently.

## 2. Components

| Component | Responsibility | Notes |
|---|---|---|
| `InfoCarrier.Core.Abstractions` | Public interfaces only (`IInfoCarrierClient`, `IInfoCarrierServer`, `IInfoCarrierSerializer`, `IInfoCarrierTransport`) | DI-first; testability seam |
| `InfoCarrier.Core/Common` | Shared DTOs: wire envelope, Query/SaveChanges request/result | See wire-protocol §4 |
| `InfoCarrier.Core/Client` | `IDatabase.CompileQuery` capture, expression→wire translation, result materialization, identity/nav fixup | Capture point per ADR-006 |
| `InfoCarrier.Core/Server` | Stub→`QueryRootExpression` rebinding, query execution, entity→wire mapping, `SaveChanges` replay, store-generated value return | Shared-type entity handling per requirements §2.7 |

All components registered via `IServiceCollection` / EF Core options pattern (requirements
§4.2). No statics (rlinq `TypeResolver.Instance` is the anti-pattern).

## 3. The client/server type boundary (requirements §3) — the central design problem

The server only knows **shared-assembly** types (entities, owned types, EF configuration).
It does **not** know anonymous types, client DTOs, value tuples, or ad-hoc types.

```csharp
// Server knows Order — works end-to-end:
var orders = ctx.Orders.Where(o => o.Price > 100).ToList();

// Server does NOT know ClientOrderDto — naive execution impossible:
var summaries = ctx.Orders
    .Select(o => new ClientOrderDto { Id = o.Id, Total = o.Price * o.Quantity })
    .ToList();
```

**Required split (requirements §3.2):**
1. **Server** executes the entity-typed portion and returns type-agnostic columnar data —
   only the columns needed (§3.3 minimizes transfer).
2. **Client** applies the final projection (`Select`, anonymous-type creation, DTO mapping)
   locally after materialization.

The expression tree is **partitioned at the boundary where server-unknown types appear**.
This partitioning must be transparent and must preserve correctness.

## 4. Entity materialization (requirements §2.5)

On receiving entity-typed results:

1. Deserialize row data into entity instances (via EF materializer paths / matched ctors —
   never `FormatterServices`).
2. **Identity resolution**: same key already tracked → reuse instance; else attach.
3. Populate scalar properties, applying configured value converters.
4. Wire navigation properties (reference + collection) from FK relationships.
5. Mark included navigations loaded (`entry.SetIsLoaded()`).

Reference identity is preserved per-message (see expression-serialization §2.3), so circular
nav refs and identity maps hold.

## 5. Test strategy (ADR-004 — LOCKED)

Mirror EF Core's official suite by inheriting `Microsoft.EntityFrameworkCore.Specification.Tests`
base classes through an InfoCarrier fixture — the v1 pattern, rebuilt for EF Core 10.

**v1 fixture architecture (ports cleanly):**
- Client `TestStore` wrapper (`InfoCarrierTestStore`) seen by spec tests.
- Backend `TestStore` (`InfoCarrierBackendTestStore`) doubling as `IInfoCarrierClient` — the
  in-process stand-in for the network.
- Two `IServiceProvider`s: client provider vs "server" provider.
- **`SimulateNetworkTransferJson`**: every request **and** result round-trips through real
  serialization in-process, so wire-serializability failures surface in tests exactly as
  over a network.
- Seeding flows through the spec's own `seed` delegate, forwarded to the backend store
  (`EnsureCreated` + seed against a server-side context).
- Compliance meta-test (`ComplianceTestBase`) verifies every spec base is implemented.

**Backends:** InMemory first, then SQL Server via **Docker container** (not LocalDB), fresh
DB per batch. See [`ci-cd.md`](ci-cd.md).

**Increment plan (v1 lesson: build coverage alongside the provider):**
1. Basic queries (`Northwind*QueryTestBase` family).
2. `SaveChanges`, identity, fixup — **M2M from day 1**.
3. Relationships, owned types, inheritance.
4. Spatial, advanced queries.

### 5.1 EF Core 3.1 → 10 port risks (from v1 study)

| Risk | Detail |
|---|---|
| **HIGH** | Query test base names reorganized (`SimpleQueryTestBase` → `Northwind*QueryTestBase` family); `FinalizeModel()` removed; `ToQuery()` defining queries removed |
| **MEDIUM** | `TestStore.Initialize` signature drift; `TestModelSource` internals; internal diagnostics/strings |
| **LOW** | `ITestStoreFactory` (unchanged, 4 members); `UseInternalServiceProvider`; `NonCapturingLazyInitializer` |

Concrete subclass mapping is an open research task (§6 Q-T1).

## 6. Open research questions (pre-implementation)

| # | Question | Study source | Blocks |
|---|---|---|---|
| A1 | ADR-006: capture point A (post-translation) vs B (raw capture)? | `subrepos/efcore` `Database.CompileQuery`, shaper construction | Client design |
| A2 | Projection-split boundary detection: how do we locate "server-unknown type" boundaries in a tree reliably? | `subrepos/efcore` + rlinq | §3 split |
| A3 | How does EF Core 10 build its shaper, and can we reuse its materialization for identity resolution? | `subrepos/efcore` shaper/materializer | §4 |
| T1 | Map v1's spec-test subclass list to EF Core 10 test base names (HIGH-risk item §5.1) | `subrepos/efcore/test/EFCore.Specification.Tests` | Test scaffolding |
| T2 | Does `ComplianceTestBase` still exist in EF Core 10 and what does it require? | `subrepos/efcore` | Test strategy |
| A4 | DI shape for the provider: which EF Core `IDatabaseProvider` services do we replace vs wrap? | `subrepos/efcore` provider-building docs/src | Component design |

## 6a. Open design questions (raised during implementation)

### D1 — `InfoCarrierEnvelope.Payload` is `byte[]`, and the outer envelope is JSON

**Raised 2026-08-11, during the M8 sample-app design. No decision, no action; recorded so it is
not lost.**

`InfoCarrierEnvelope.Payload` is declared `byte[]` so that the envelope stays independent of
whichever `IInfoCarrierSerializer` produced the body — the envelope can be JSON while the payload
is MessagePack, say. That independence is real and it was deliberate.

**What it costs today, when both are JSON.** The body is base64-encoded inside the outer JSON
document: roughly **33% larger**, and opaque to anyone reading the wire. For a provider whose
entire proposition is *"your LINQ expression ran over there"*, an unreadable blob is a poor
showing. It is not only cosmetic — the M8 sample's wire-inspector panel has to base64-decode
before it can display anything, which is the concrete symptom that raised this.

**Four candidate answers, none chosen:**

| # | Answer | Cost |
|---|---|---|
| a | Keep `byte[]` | Serializer-agnostic; base64 overhead and opacity stay. |
| b | Payload as raw JSON (`JsonElement` / `RawValue`) | Honest and readable, no base64; couples the envelope to JSON. |
| c | Generic `InfoCarrierEnvelope<TPayload>` | Type-safe; complicates a dispatcher that is deliberately one method. |
| d | Keep bytes, add a serializer/content-type id | A reader can decode without guessing; does not remove the base64 overhead. |

**Constraint on any answer:** `InfoCarrierPayloadLimits` bounds the *request* direction in bytes
(C37, and the asymmetry there is measured rather than stylistic). Whatever replaces `byte[]` must
keep the payload measurable in bytes before it is parsed, or the limit stops being a limit.

**Not urgent.** The wire is versioned (`ProtocolVersion`), so this can change behind a major
version bump. Revisit when the HTTP transport is promoted out of the sample.

## 7. Out of scope (initial release) — requirements §6

AuthN/authZ (protocol must not preclude); offline/disconnected caching; client-side query
composition beyond EF tracking; multi-tenant server-side context resolution (protocol must
not preclude).
