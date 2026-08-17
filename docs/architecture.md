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
| ~~`InfoCarrier.Core.Abstractions`~~ | **Merged into `InfoCarrier.Core` 2026-08-17 (M8-22).** The interfaces are unchanged and so are their namespaces; only the assembly moved. The package earned nothing: `IInfoCarrierClient` takes a `DbContext`, so it referenced `Microsoft.EntityFrameworkCore` and was never the lightweight contracts package that would have justified the split. | |
| `InfoCarrier.Core` (root) | Public interfaces (`IInfoCarrierClient`, `IInfoCarrierServer`, `IInfoCarrierSerializer`, `IInfoCarrierTransport`) and `HttpInfoCarrierTransport` | DI-first; testability seam |
| `InfoCarrier.Core.AspNetCore` | `app.MapInfoCarrier()` — the server endpoint | **Separate package on purpose**: it needs a framework reference to `Microsoft.AspNetCore.App`, and a WPF/MAUI/WebAssembly client must not carry that |
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

**Candidate answers:**

| # | Answer | Cost |
|---|---|---|
| a | Keep it | Zero work; the smell and the WASM bytes stay. |
| b | Read the annotation by its **string** name and drop the package | Small; loses the compile-time constant, and an EF rename becomes a silent behaviour change rather than a build error. |
| **c** | **Define a provider-neutral seam — *"is this type mapped to a document?"* — that a backend answers** | The principled answer, and the one that also serves Cosmos. Largest. |
| d | Move the JSON clause into an optional `InfoCarrier.Core.Relational` package | Keeps the core clean; splits a convention across two packages, which is its own trap. |

**DECIDED 2026-08-16: (c), in two steps, and the package is the second step rather than the
first.** The audit that settled it is below, and its finding is that the reference is the least of
what is store-flavoured here.

**DONE 2026-08-16 (M9, plan J5). `InfoCarrier.Core` no longer references
`Microsoft.EntityFrameworkCore.Relational`.** `Metadata.IInfoCarrierDocumentMapping` asks the one
question, and `AnnotationDocumentMapping` answers it by reading the container annotation by its
**string** name — so (c) supplies the architecture and (b) supplies the default, which is the
combination neither row describes on its own. **Measured neutral**: `18 / 22456` against
`18 / 22453`, empty FIXED and BROKEN, REASONS unchanged, the three extra tests being the pin
itself. The D3 pins were verified *positively* rather than inferred from a stable count —
`JsonQuerySqlite` 393/0, `JsonOwnedCollectionUpdate` 5/0, `ComplexCollectionJsonUpdate` 18/0.

Three facts the write-up above did not anticipate, all of them found by running it:

- **`GetContainerColumnName()` is a walk, not an annotation read** — it falls back through the
  ownership chain, so reading the annotation on the type alone answers `null` for every nested
  type. That is B12 one level down, and it is what the pin test's third assertion exists for.
- **`SynthesizedOrdinalPropertyName` had to move onto the seam as well.** It is a `const`, so it is
  inlined at runtime, but naming its declaring type still needs the assembly at compile time. It
  belongs there anyway: Cosmos recognises the ordinal by the property's *shape*, not by this name.
- **EF will not let a provider register its own service through `EntityFrameworkServicesBuilder`.**
  Its `TryAdd` validates against EF's service contracts; routing this one through it put *"The
  database provider attempted to register an implementation of the 'IInfoCarrierDocumentMapping'
  service"* on 21,991 tests. It registers on the plain collection, as ADR-012's value mappers do.

**Step two — the boundary allowlist — is not done**, and it remains the larger half.

**(b) was rejected explicitly.** It removes the package and keeps every relational assumption
intact, so it buys the WebAssembly bytes and nothing else — while making an EF rename a silent
behaviour change instead of a build error. The reference is worth removing as a *consequence* of
answering the question properly, not as the goal.

**The audit, 2026-08-16.** The four call sites are as recorded above; nothing has moved. What is
new is the sweep of everything else, because "which parts only work if the store is relational?"
is the question the package reference stands in for:

| Component | Verdict |
|---|---|
| `InfoCarrierTypeMappingSource` | Neutral — CLR types only. |
| `InfoCarrierValueGeneratorSelector` | Neutral — numeric temporary keys, gated on `ValueGenerated`. |
| `ServerSaveChangesExecutor.IssuedAtSave` | Neutral **on purpose**: it asks the backend's own `IValueGeneratorSelector` whether the store issues at save, rather than testing for SQL. This is the shape the seam should copy. |
| `InProcessInfoCarrierServer` transactions | Neutral — it relays what the store says, refusals included. |
| `InfoCarrierDatabaseCreator` | Neutral (all no-ops). |
| `InfoCarrierKeyDiscoveryConvention` | **Relational-shaped** — the JSON ordinal key. Already known; CLAUDE.md records that Cosmos needs its own clause. |
| `InfoCarrierDatabase.Expand` | **Relational-shaped** — the JSON document owner scan. |
| `TypeAllowlist` + `ServerBoundaryAnalyzer` | **Store-blind, and this was not recorded anywhere.** |

**The third row is the finding.** The boundary between what the server runs and what the client
runs is decided from a **fixed list**, and the backend is never asked what it can translate. It is
invisible against SQLite because SQLite translates a great deal, so the fixed list is a decent
approximation of the truth. It stops being one against any store that translates less: the client
would ship a query the server cannot execute, and the failure would arrive from the store rather
than from the boundary that should have refused it. **Removing the package reference does nothing
about this**, which is why (c) is two steps and why the package is the second.

### D5 — the query boundary does not ask the backend what it can translate

**Raised 2026-08-16 in D3's audit; scoped properly 2026-08-17 (M9, J6).
DECIDED 2026-08-17: answer (c), and the deliverable is the written record rather than a
mechanism. See "THE DECISION" at the end of this entry.**

**First, a correction to how D3 and the M9 roadmap entry first stated this.** They said the remedy
was *"a backend-supplied query-capability set replacing the fixed boundary allowlist"*. **Replacing
is wrong and would be a security regression.** `TypeAllowlist` is not a capability list: it is
ADR-008 constraint 2, and its own summary says why — without it `TypeNodeResolver` resolved any
name a payload supplied by scanning every loaded assembly, *"a remote-code-execution vector the
moment a network transport exists"*. `security-review.md` §2 adds that its safety is a
**conjunction** across several clauses, so widening it is exactly how the conjunction collapses.
A backend must never be able to widen it by answering a question.

**What is actually missing is a second, independent axis.** Today `ServerBoundaryAnalyzer` cuts a
query where the wire cannot express it or a type is not permitted — *"can this be sent safely?"* It
never asks *"can the thing at the other end evaluate it?"* The two are unrelated: `Regex.IsMatch`
is refused by the allowlist (A46) while `string.StartsWith` is permitted and translated by SQLite
but not by every store. So a capability axis **narrows** what is shipped; the allowlist decides
what may be shipped at all, and neither substitutes for the other.

**Why it is invisible today.** Tier B is SQLite, which translates a great deal, so the allowlist is
a decent approximation of the truth. Against a store that translates less it stops being one, and
the failure arrives from the store rather than from the boundary that should have refused it.
`GroupBy_converted_enum` (plan J1/J2 residual) is a small preview: the first non-composed `GroupBy`
this provider ever shipped, previously screened by InMemory refusing the operator outright.

**Four candidate answers, none chosen:**

| # | Answer | Cost |
|---|---|---|
| a | Leave it. Document that the client assumes a capable backend. | Zero work. Honest only while every supported backend is relational. |
| b | A capability handshake: the client asks the server once and caches the answer | Automatic and always correct. Adds a wire operation, a cache, and a versioning question — and the answer has to be expressible, which is the hard part: EF providers do not enumerate what they translate. |
| c | The application declares the capability set on both sides, from one shared source | No wire change, and it is **D2's shape** — the same "one configuration, derived twice" this repo already needs for the model. Manual, and wrong if it drifts. |
| d | Ship optimistically, catch the translation failure, re-run client-side | What EF itself removed on purpose. Silent performance cliffs, and unsound the moment a query has side effects. |

**The honest difficulty, and it is (b)'s:** *"what can you translate?"* has no EF-shaped answer. A
provider exposes no capability manifest, and the real answer is a predicate over expression trees.
Anything cheap here will be a coarse approximation — operator families rather than a decision
procedure — which may be enough to move the failure to the right side of the wire, and is worth
saying out loud before anyone prices (b) as small.

**Not blocking anything today.** It becomes load-bearing the moment a third store is adopted, which
is why M9 lists Cosmos as explicitly out of scope until this exists.

**RECOMMENDATION, added 2026-08-17: (c), and start by writing down what is already known rather
than by building a mechanism.** Two of M9's own findings are evidence about the shape of the
answer, and both point away from (b):

- **J10.** EF's SQLite translates an anonymous join key and a `Tuple` one, and refuses a
  `ValueTuple` — with `NewExpression.Members` supplied either way. No capability *manifest* would
  ever have expressed that: it is not an operator or a type, it is a shape. A handshake asking
  "what can you translate?" would have answered "joins", and been wrong.
- **J8.** A non-composed `GroupBy` is refused by EF's InMemory provider and accepted by SQLite.
  That one *is* operator-shaped, and is the kind of thing (b) could express.

So the axis has at least two kinds of fact in it — coarse ones a backend could declare, and fine
ones that are properties of a *tree shape* and are only ever discovered by running the query.
**(b) cannot cover the second kind, and pricing it as if it could is the mistake to avoid.**
(c) — one shared declaration, derived twice, exactly as D2 proposes for the model — covers the
coarse facts honestly and leaves the fine ones where they already are: findings, written down,
fixed one at a time. It also needs no wire operation and no cache.

**What "start by writing it down" means concretely:** the fine-grained facts this milestone found
are already recorded next to the code that depends on them (J10's comment in
`TransparentIdentifierRewriter`). That is the useful artefact. A mechanism should follow a second
backend, not precede it.

## THE DECISION — (c), taken 2026-08-17

**Answer (c) is adopted: the application declares the capability set on both sides from one shared
source. No mechanism is built in M9, and that is part of the decision rather than a deferral of
it.**

**Why not (b), which is the one that sounds right.** A handshake is automatic and always current,
and it cannot express half the facts. J10 is the counter-example and it is decisive: EF's SQLite
translates an anonymous-type join key and a `Tuple` one and **refuses a `ValueTuple`**, with
`NewExpression.Members` supplied either way. That is not an operator and not a type — it is a
**tree shape**, and no manifest a provider could publish would carry it. A handshake asking "what
can you translate?" would answer "joins", and be wrong. Pricing (b) as if it covered everything is
the specific mistake this entry exists to prevent.

**Why not (a) or (d).** (a) is honest only while every supported backend is relational, which is
true today and is exactly the assumption M9 exists to stop making silently. (d) is what EF itself
removed on purpose: silent performance cliffs, and unsound the moment a query has side effects.

**Why (c) is the same shape as something this repo already needs.** D2 records that the model must
be one configuration derived twice, because the client's model and the server's are built by
different providers. A capability set has the identical shape and the identical failure mode — it
is wrong if it drifts — so it should be declared once and read by both halves, not negotiated.

**The known facts, which are the deliverable.** The axis holds two kinds, and separating them is
the useful part:

| Kind | Example | Can a declaration carry it? |
|---|---|---|
| **Coarse** — an operator a backend does not translate | A non-composed `GroupBy`: EF's InMemory refuses it, SQLite accepts it (J8) | **Yes.** This is what (c) declares. |
| **Coarse** — a BCL call a backend cannot map | `Regex.IsMatch`: SQLite emits `REGEXP` (J20); a store without regex cannot | **Yes.** |
| **Fine** — a property of the expression's *shape* | A `ValueTuple` join key refused where an anonymous type and a `Tuple` are accepted (J10) | **No.** Only found by running the query. |

**So the deliverable of J6 is this table plus the comments already sitting next to the code that
depends on each fact**, and the mechanism follows the second backend. Cosmos is the candidate and
is explicitly out of M9's scope.

**Consequence for M9's exit criterion, stated so the closure is not a fudge.** The criterion was
written as *"a query boundary that also asks what the backend can evaluate"*. Taken literally it
cannot be met inside M9: there is no second backend to ask, and adding one is the thing M9 puts out
of scope. It is restated in [`roadmap.md`](roadmap.md) as *"the axis is identified, decided and
recorded"*, which is what was actually achievable and what has been done.

**What would reopen this:** adopting a second store. At that point (c) needs a concrete shape — one
declaration, read by both halves — and the fine-grained facts stay where they are, as findings
beside the code.

### D6 — a second client context cannot join an open server transaction

**Raised 2026-08-17 by plan J3, which was reverted on it. No decision, and the security question
turns out to be already answered.**

`TestHelpers.ExecuteWithStrategyInTransactionAsync` opens one transaction and makes every other
context enlist through `UseTransaction`. Relational suites do that with
`transaction.GetDbTransaction()`; ADR-013 puts that permanently out of reach here. So
`ProxyGraphUpdates` cannot move to Tier B — 653 failures, 471 of them
`SQLite Error 5: 'database is locked'`, each waiting out a 30-second lock timeout.

**CORRECTED 2026-08-17, and the correction is the whole entry.** This said *"what is missing is one
client-side API"*. **Nothing was missing.** `InfoCarrierDatabaseFacadeExtensions.UseInfoCarrierTransaction`
and `InfoCarrierTransactionManager.UseTransaction(token)` have both shipped since M4, complete with
the non-owning semantics this question worried about — `InfoCarrierTransaction` takes
`owned: false`, and its `CommitAsync`/`RollbackAsync` check that flag before touching the server, so
a joining context detaches instead of ending someone else's transaction.

**What was actually missing was the test class's `UseTransaction` override**, which
`ConferencePlannerInfoCarrierTest` and `OptimisticConcurrencyInfoCarrierTest` have both carried for
some time — and the comment on the first of them names the exact symptom J3 produced: *"Without
enlisting, the second runs on its own SQLite connection and gets 'database is locked'."* The
evidence was in the repository the whole time, one grep away.

**The lesson is the one this repo keeps relearning, in a new place.** J3 read 653 failures of one
mechanism and concluded a product feature was absent, without checking whether the feature existed.
That is the same shape as C58 ("a remedy priced as expensive when the price was for the route") and
as B3d/C10's *"626 + 322 lines"* — **before pricing a gap, check whether a sibling of it already
works.** Two classes in the same suite already did this.

**The security objection does not survive checking either.** M8's roadmap note records that the
envelope carries no caller identity, so *"any caller who knows a token can join the transaction"*.
That is true — and it is true **today, and was true before this API existed**.
`InProcessInfoCarrierServer.Acquire` runs *any* request that names a token on that transaction's
context, and every request type carries the field. So the exposure is a property of the wire
protocol, not of `UseInfoCarrierTransaction`. Binding a token to its creator remains worth doing,
and it is M8's item, independent of this one.

**Both design questions this entry raised were already answered in the shipped code**, which is
consistent with the correction above: a joined transaction is non-owning, so it neither commits nor
rolls back on dispose, and two contexts hold two `InfoCarrierTransaction` objects over one token
with only the owner able to end it.

**Closed by plan J3 (2026-08-17).** Nothing was built for it.

### D4 — chained InfoCarrier: a server whose own `DbContext` is an InfoCarrier client

**Raised and measured 2026-08-16, during the D3 audit. Two defects recorded, neither scheduled.**

Chaining is the sharpest available test of provider-neutrality, because the "store" behind the
server is then this provider itself — an implementation that translates nothing, has no columns and
no connection. It was **executed rather than reasoned about**: three levels (outer client →
middle → middle → SQLite), the same assertions at depths 1, 2 and 3, 27 tests.

**23 of 27 pass.** Queries over entities, `Include`, scalar projections, the projection split
itself, transactions with savepoints and rollback, and `SaveChanges` insert and delete all survive
two chained hops unchanged. That is the headline: chaining nearly works, and nothing in the design
prevents it.

| Shape | 1 hop | 2 hops | 3 hops |
|---|---|---|---|
| Entities, `Include`, scalar projection, client-side projection | pass | pass | pass |
| Transaction + savepoint + rollback | pass | pass | pass |
| `SaveChanges` insert, `SaveChanges` delete | pass | pass | pass |
| **Anonymous projection** | pass | **fail** | **fail** |
| **`SaveChanges` update** | pass | pass | **fail** |

**Defect 1 — an anonymous projection fails at the first chained hop.** The middle node returns a
queryable of `TupleCarrier` tuples and the outer client refuses it:

```
Type 'System.Linq.EnumerableQuery`1[System.ValueTuple`2[System.String,System.Int32]]'
is not on the deserialization allowlist (ADR-008 constraint 2).
```

**Defect 2 — an update is silently lost at the second chained hop, and this is the serious one.**
The client reported `0` changes, no exception was raised, and the row in the store was unchanged —
the wrong-data-without-an-exception shape that A49, B4 and B12 each cost a session to.

**It was named by the payload, not by reading code.** A recording decorator on `IInfoCarrierClient`
at every hop printed each query result, and one field differs:

```
PROBE-Q hop0->store  … "isTracked":true   bytes=461
PROBE-Q hop1->hop0   … "isTracked":true   bytes=461
PROBE-Q hop2->hop1   … "isTracked":false  bytes=817
```

**A middle node reports its rows as untracked.** `ServerQueryExecutor`'s `IsTracked` answers from
its own state manager, and `ClientResultMaterializer` declines to track a row that arrives saying
`false` — so the outer client holds a `Detached` entity, `SaveChanges` finds nothing to send, and
the user's edit disappears. The payload grows at the same hop for the same reason: an untracked
entity serializes its navigations as a plain object graph.

**Why depth 3 and not depth 2.** One middle node replays and saves; that path is exercised by the
whole suite. It takes a *second* middle for a result that has already been materialized by a client
to be re-served as a server's answer, and that is the step that loses the flag.

**Not scheduled, and the probe is deliberately not in the suite** — it would add tests and failures
to a baseline whose number must keep meaning "inherited spec tests failing". Should chaining ever
become a supported topology, the probe is the specification for it, and the recording decorator is
the instrument that found this in one filtered run.

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
