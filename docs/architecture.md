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

**EVIDENCE, added 2026-08-12 (M8 phase 1, step M8-8). The opacity is not one layer deep, it is two,
and it made a test vacuous for four review rounds.**

`A_projection_crosses_as_columns_rather_than_as_entities` asserted that an excluded column never
crossed the wire, by searching the recorded HTTP response body for the excluded date:

```csharp
Assert.DoesNotContain("2026-01-05", allRecordedBodies);   // could never fail
```

**A tautology.** The payload is base64 inside the outer JSON, and the base64 alphabet contains no
`-`, so a hyphenated date can never appear in that body whatever the payload holds. The final
whole-branch review found it with a probe rather than by reading.

**And the first fix was still a tautology.** Decoding `InfoCarrierEnvelope.Payload` is not enough:
for a query, that deserializes to `QueryDataResult`, whose `SerializedResults` is **itself** a
`byte[]` and therefore base64 **again**. Both the reviewer's proposed fix and the controller's
stopped at one layer; only the implementer's own check caught it. The assertion is now genuinely
failable, proved by deliberately projecting the whole entity and observing
`Assert.DoesNotContain() Failure … Found: "2026-01-05"`.

**What this adds to the decision above.** Answer (a) — keep `byte[]` — now has a cost that is not
merely cosmetic: **two nested base64 layers make wire-level assertions hard to write correctly, and
easy to write in a form that cannot fail.** Anything asserting on payload content has to know the
nesting depth, and getting it wrong is silent. That argues for (b) or (d) more strongly than the
size overhead did. It also means the phase-2 wire inspector must decode two layers to show anything
useful, not one.

**The transferable lesson, which is about method rather than about this field:** of the several
mechanism assertions strengthened in this phase, exactly one was never deliberately broken to
confirm it could fail — and that was the vacuous one. **The assertion you did not watch fail is the
one to distrust.**

### D2 — shared `DbContext` configuration, derived and augmented by each side

**Raised 2026-08-11. A founding ICC idea, held since v1, never written down. Recorded now so it is
returned to systematically. No hurry, no immediate action.**

**The idea.** One shared model configuration that the backend and the frontend both *derive from*
and augment with their own specifics. Not two configurations kept in step by discipline — one
configuration, extended twice.

**Why it is load-bearing rather than tidy.** Two rules in `CLAUDE.md` make model agreement a
correctness property of this provider, not a convenience:

- the wire carries entity type **names**, so the client's model and the server's must agree about
  them (A49);
- **anything the wire computes from a type mapping is computed twice, by two different providers,
  and is only sound if the two agree.** B4 is the worked example: a `DateTime[]` written by
  SQLite's JSON form and read by EF's core one, 106 failures in both directions.

**Divergence is silent.** That is the whole danger. A property configured on one side only does not
throw — it returns a wrong answer. B12 was the same shape: every element of a JSON collection
shared one key, and the symptom was wrong data with no exception.

**What each side legitimately augments** — and the boundary is not obvious, which is why this needs
study rather than a quick answer:

| Belongs to | Examples |
|---|---|
| **Shared** | entity types and their names, keys, relationships, query filters, value converters, `ToJson()` (B12 proved the client needs it), complex types, ownership |
| **Server only** | column names and store types, indexes, sequences, computed columns, `rowversion`, anything a migration would emit |
| **Client only** | nothing store-related. Possibly nothing at all. |

**The expectation, which is worth testing rather than assuming:** the shared part should be *small*,
because EF's conventions already produce most of it. The M8 sample is the first worked example — one
`NorthwindContext` in a shared assembly, configured by provider at DI time — and it should be read
as evidence about the size of that part.

**What the sample does NOT yet show, and it is the half this question is named after.** Both halves
use that one `OnModelCreating` *verbatim*. Nothing is derived, and nothing is augmented: the
differences between the two sides are options-level (`UseSqlite` vs `UseInfoCarrier`,
`UseLazyLoadingProxies` on the server only), not model-level. So the sample is evidence for
"sharing works and is small", and no evidence at all for "each side augments it safely". A worked
example of the augmenting half is the obvious next step whenever this is picked up.

**A new instance of the silent-divergence danger, 2026-08-16 (M8-18), and it is the sharpest one
yet** — because the two models diverged *without anyone writing a second configuration*.
`dotnet ef dbcontext optimize` cannot load a Blazor WebAssembly project (no `deps.json`), so the
server had to be the `--startup-project` — and EF's tooling then takes its configuration from the
**startup application's own service provider**, silently ignoring the client's
`IDesignTimeDbContextFactory`. The compiled model handed to the browser was therefore the
*server's*: annotated `Relational:TableName`, `Relational:Schema` and `Proxies:LazyLoading = true`.
**The browser ran on it for two steps and looked completely healthy.** It only surfaced when
lazy-loading proxies were removed from the client and every page died on
`PropertyNotDefinedForType … ILazyLoader LazyLoader`, because the compiled model still declared a
service property the client could not bind.
Two lessons for D2. First, **tooling is a divergence source**, not just a hand-written second
configuration — any mechanism chosen here has to say which side generates artefacts and how that is
verified. Second, it is further evidence for the enforcement question below: a start-up check
comparing the two models would have caught this on the first page load instead of two steps later.

**Candidate mechanisms, none chosen:** one shared context class (what the sample does); a shared
assembly of `IEntityTypeConfiguration<T>`; a shared `IModelConfiguration` seam this provider
defines; or a convention set the provider ships so that the *default* is agreement.

**The question to answer when this is picked up:** is the shared configuration merely a *pattern*
this provider documents, or something it should **enforce** — for instance by refusing at start-up
when the two models disagree about a name the wire will carry? The suite already has the shape of
such a check: `JsonQuerySqliteInfoCarrierTest.The_two_models_agree_on_the_key_of_every_JSON_mapped_owned_collection`
compares the client model with the server model directly.

### D3 — why does `InfoCarrier.Core` reference `Microsoft.EntityFrameworkCore.Relational`?

**Raised 2026-08-11. Ideally the reference should not be there. Recorded with the facts so the
investigation starts from evidence rather than from the question. No action now.**

**It is used for exactly one question**, asked at four call sites in three files — *"is this entity
type mapped to a JSON column?"*:

| File | Call | Why |
|---|---|---|
| `InfoCarrierKeyDiscoveryConvention.cs:82, 95` | `GetContainerColumnName()` | B12/C80: the client must give a JSON-mapped owned collection the same synthesized-ordinal key its backing store gives it, or every element shares one key and EF's fixup gives each of them to all of them. |
| `InfoCarrierKeyDiscoveryConvention.cs:173` | `RelationalAnnotationNames.ContainerColumnName` | The annotation name the clause keys on. |
| `InfoCarrierDatabase.cs:356, 377` | `GetContainerColumnName()` | C87/C95: a JSON-mapped entry's owner, and the rest of that owner's document, must travel with it. |

Nothing else in the product touches a relational API. There is no `GetTableName`, no
`GetColumnName`, no `IRelationalConnection`, no `DbTransaction`.

**Why it is uncomfortable, and it is more than aesthetics.** The client is **never** a relational
context — that is ADR-013, and it is what makes `JsonUpdateTestBase` unreachable (142 tests). So the
package is referenced by a component that is, by construction, not relational. Two concrete costs:

- **Download size in a browser.** The M8 sample puts this provider in WebAssembly, where every
  referenced assembly is bytes over the network. That cost was theoretical before the sample and is
  not any more.
- **It is already known to be store-flavoured.** CLAUDE.md records the limit next to B12: *"A Cosmos
  backend would need its own clause — Cosmos recognises an ordinal key by the property's shape, not
  by this name."* So the current clause is not provider-neutral, only relational-shaped.

**Candidate answers, none chosen:**

| # | Answer | Cost |
|---|---|---|
| a | Keep it | Zero work; the smell and the WASM bytes stay. |
| b | Read the annotation by its **string** name and drop the package | Small; loses the compile-time constant, and an EF rename becomes a silent behaviour change rather than a build error. |
| c | Define a provider-neutral seam — *"is this type mapped to a document?"* — that a backend answers | The principled answer, and the one that also serves Cosmos. Largest. |
| d | Move the JSON clause into an optional `InfoCarrier.Core.Relational` package | Keeps the core clean; splits a convention across two packages, which is its own trap. |

**Related to D2**, and worth taking together: both are the same question at different levels —
*which part of a model is the store's business, and which is shared?* D2 asks it about the
application's configuration; this asks it about the provider's own.

**The regression pin already exists.** Any change here must keep `JsonQuery` at 0 failures and
`JsonOwnedCollectionUpdate` at 5 of 5, plus
`The_two_models_agree_on_the_key_of_every_JSON_mapped_owned_collection`. B12's own symptom was
**wrong data with no exception**, so a green build is not evidence here — the measurement is.

## 7. Out of scope (initial release) — requirements §6

AuthN/authZ (protocol must not preclude); offline/disconnected caching; client-side query
composition beyond EF tracking; multi-tenant server-side context resolution (protocol must
not preclude).
