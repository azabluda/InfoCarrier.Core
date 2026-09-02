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

### 5.2 Differential SQL testing, and reading the server's SQL

Added 2026-08-28, after #59 — a defect no test in the suite could see, because the suite compares
**answers** and the answers were right. A scalar query parameter was reaching the backing store as a
SQL *literal* where every other EF client sends a SQL parameter.

**The category this suite was missing is not "assert the SQL".** EF's relational bases pin generated
SQL against golden strings, and those are the backing store's business: they fail when SQLite changes
its dialect, which EF already tests, and they would fail here for a cosmetic reason as well, since a
parameter crosses inside `ParameterBox<T>` and EF names it after the box's property.

The category is **differential**:

> Run the query twice — once from the client over the wire, once directly against the server
> context — capture both statements, and assert they are the same after normalizing parameter names.

That asks the one question this project has and EF does not: *does the middleman change the
statement?* It needs no golden strings, so it survives an EF version bump, and it fails the moment
the wire turns a parameter into a literal, reorders a join, or drops an index-friendly predicate.
`ServerParameterizationTest` is the first of these.

**Reading the server's SQL at all.** `InfoCarrierTestStoreFactory` does build a
`TestSqlLoggerFactory`, but it belongs to the *client*, which has no database and emits none. Set
`INFOCARRIER_SERVER_SQL=1` and `InfoCarrierBackendTestStore` writes every server command to
`server-sql.log` in the test output directory. Off in every normal run, asserts nothing, and exists
so that a failing Tier B test can be re-run and read rather than reasoned about. `ServerSqlLog`
records why it is a switch and a file rather than output attached to a failing test.

**What is still missing** is the provenance the harness cannot have: whether a literal in the SQL
came from a constant the caller wrote or from a parameter this provider inlined. Only
`QueryExecutor.Substitute` knows, and issue #49 covers shipping that as a diagnostic event.

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

#### D3 amendment 2026-09-02 (R118) — the reference may come back, in a companion package, and D3 still stands

**Raised by the owner. Filed as [#97](https://github.com/azabluda/InfoCarrier.Core/issues/97), which
carries the full measurement. Nothing is decided and no design exists.**

**The idea.** A second shipped package, `InfoCarrier.Core.Relational`, referenced by **both halves**
when the client knows its backend is relational. `InfoCarrier.Core` still references nothing
relational, so **D3 as written is untouched** and a non-relational backend stays possible — which is
what D3 bought and what the other end of #96 asks for.

**Why it is worth measuring rather than dismissing.** J5 removed the reference and paid for it in
string literals. Counted on 2026-09-02:

| Paid today | Size |
|---|---|
| 9 magic `Relational:` annotation strings in 4 product files | pinned by a 268-line `DocumentMappingPinTest` |
| `RelationalQueryRootShape.cs`, two EF types resolved by name | 276 lines, **10 trim suppressions** |
| `InfoCarrierHierarchyMappingConvention.cs`, a narrower hand-written `EntityTypeHierarchyMappingConvention` | 131 lines |
| `AnnotationDocumentMapping` + `IInfoCarrierDocumentMapping` | 149 lines |

**Every one is a fact computed twice by two providers**, and the second computation is a string
literal. That is the same hazard `CLAUDE.md` opens with; J5 did not remove it, it changed its shape.

**The constraint that decides the design, read from EF's source rather than assumed.**
`EntityFrameworkRelationalServicesBuilder.TryAddCoreServices()` is **all-or-nothing and collides with
ADR-006**: it registers `IDatabase → RelationalDatabase`, and this provider captures the query at
`IDatabase.CompileQuery`. It also takes `IDbContextTransactionManager`, `IDatabaseCreator`,
`ITypeMappingSource`, `IAdHocMapper` and the whole SQL-generation stack. **So "make the client
relational" cannot mean calling that builder**, and EF publishes no seam that splits its metadata
half from its command half.

**Three levels, and only the third is risky.** Level 1 references the package and supplies today's
string answers behind seams — most of the deletion, almost no risk. Level 2 registers EF's own
relational conventions on the client model. **Level 3, a relational model on the client
(`GetRelationalModel`), needs an `IRelationalTypeMappingSource` the client cannot honestly have —
that is B4, and it is why D3 was drawn where it is.** Levels 1 and 2 do not depend on it.

**Testing needs no re-parenting, and that was checked.** 52 test files and 117 declaration sites
already sit on relational spec bases (ADR-013); EF's own relational bases derive from the core ones
and override, which is the layering this repository copied. The per-fixture switch also exists:
`relationalClientStore`, used by 8 fixtures. **Tier A must not get it** — InMemory is not relational,
and a relational client over it would recreate the disagreement the change exists to remove. Keeping
the flag per fixture makes the same bases run both ways, and the difference is the measurement.

**R114 already proved the registration shape.** An `IRelationalDatabaseFacadeDependencies` supplied
from outside `InfoCarrier.Core` satisfied EF's type test and took `failed` 143 → 141 with D3
untouched. That class is the first member this package would hold.

#### D3 amendment 2026-09-02 (R120) — level 1 is built, and the seam it needed has exactly one reader

**`src/InfoCarrier.Core.Relational` now exists and ships.** It holds
`InfoCarrierRelationalQueryRoots`, which names `FromSqlQueryRootExpression` and
`SqlQueryRootExpression` outright, and `InfoCarrierRelationalFacadeDependencies`, moved out of the
test project. `RelationalQueryRootShape.cs` is deleted: **276 lines and 10 trim suppressions**, and
the trim count fell with them. **D3 as written is still untouched** — `InfoCarrier.Core` references
nothing relational, and the seam it exposes is
`InfoCarrier.Core.Metadata.IInfoCarrierRelationalQueryRoots` with a no-op default.

**The finding is the constraint, and it cost a wrong answer to see.** The prototype gave the same
fact two carriers. `ServerBoundaryAnalyzer` read the seam from the **options**, which it must:
`ExtensionInfo.GetServiceProviderHashCode()` is `0` and `ShouldUseSameServiceProvider` is true for
every InfoCarrier options shape, so every client context in a process shares one internal service
provider and anything per-context has to travel on the options. `ExpressionToNodeTranslator` read it
from **DI**, because it is DI-scoped. A client that set the option but not the service therefore
**admitted** a raw-SQL root at the boundary and then **dropped its SQL** in the translator — the
whole table came back, which is the defect R75 closed.
`FromSql_arguments_cross_as_values_and_are_bound_rather_than_interpolated` answered **2 where 1 is
correct**, and a red test would have been the better outcome.

**The rule that generalises, and it is not about relational at all.** *A fact two components read
independently is a fact that can disagree with itself, and the disagreement is silent when one
component's answer only widens what the other is allowed to do.* Permission and knowledge were split
across two carriers here, and the boundary check held while the thing it guarded stopped being
carried.

**The shape of the fix.** `InfoCarrierOptionsExtension.RelationalQueryRootsFor(context)` is the one
reader. `QueryExecutor` calls it once per execution and hands the same object to `QuerySplitter`
(hence to the analyzer) and to `ExpressionSerializer.ToNode`. The translator takes it per
translation and scopes it exactly as it scopes parameter identity: set at depth 0, untouched in the
recursion. Nothing else resolves it.

**The server half is a separate provider and asks separately.** `InProcessInfoCarrierServer` resolves
it from the application's collection and passes it to `ServerQueryExecutor`, beside the value
mappers, the allowed types and the raw-SQL grant. **Not from the context**: the server context builds
its own internal service provider and never sees the application's collection, so a lookup through
the context answered "nothing is relational here" for a server that had registered a real
implementation.

**Three registration entry points, and that is not untidiness.**

| Call | Where | Why it is separate |
|---|---|---|
| `AddInfoCarrierRelational()` | both halves | the query roots, and nothing else |
| `AddInfoCarrierRelationalClient()` | **client only** | also replaces `IDatabaseFacadeDependencies`, which `Database.SqlQuery<T>` type-tests. A relational *server* already has EF's own, and that one owns a live connection |
| `InfoCarrierDbContextOptionsBuilder.UseRelationalQueryRoots(...)` | client, no DI | most clients never build an `IServiceCollection`; they only call `UseInfoCarrier`. `SqliteSmokeTest` is exactly that shape |

**One further cost, measured.** `EnablePackageValidation` is set for every packable project in
`Directory.Build.props`, and a brand-new package has no baseline: restore fails with `NU1101`
hunting an `InfoCarrier.Core.Relational 10.0.0` that cannot exist. The new csproj sets
`<EnablePackageValidation>false</EnablePackageValidation>` with a comment saying to turn it on after
the first release.

#### D3 amendment 2026-09-02 (R123) — level 2 is scoped, and it is blocked on ONE decision that is the owner's

**Nothing was built. This entry is the expensive half of level 2 done in advance, so the next
attempt does not re-derive it.** Level 2 is "register EF's own relational conventions on the client
model", so that `ToTable`, `ToJson`, TPT/TPC and `EntityTypeHierarchyMappingConvention` are EF's
rather than this repository's 131-line copy.

**Good news first: the convention itself runs on a client model, and that was read from EF's source
rather than assumed.**

| Question | Answer | How |
|---|---|---|
| Does `EntityTypeHierarchyMappingConvention` need relational *services*? | **No.** It takes `RelationalConventionSetBuilderDependencies` and never touches it in `ProcessModelFinalizing` | read `EFCore.Relational/Metadata/Conventions/EntityTypeHierarchyMappingConvention.cs` end to end |
| What is in that dependency object, then? | `IRelationalAnnotationProvider` and `IUpdateSqlGenerator`, neither of which the client has | read `RelationalConventionSetBuilderDependencies` |
| Does `GetTableName()` need one? | **No.** `GetDefaultTableName` reads model metadata and `Model.GetMaxIdentifierLength()`, and nothing else | read `RelationalEntityTypeExtensions` |

So the convention can be constructed from the relational package with a dependency object whose two
members throw, exactly as `InfoCarrierRelationalFacadeDependencies` already does for its three
relational members and for the reason ADR-013 records: **the callers that want them are not the
callers that want the rest**, and a throw is louder than a wrong answer if EF ever changes that.

**THE BLOCKER, and it is not the conventions.** It is *which client gets them*, and it is R120's
one-reader problem in its model-building form.

- **A convention set cannot read a context's options.** `ProviderConventionSetBuilderDependencies`
  exposes `ContextType` — a `Type` — and no `ICurrentDbContext`. So the seam cannot travel on the
  options the way `IInfoCarrierRelationalQueryRoots` does; it has to be registered in DI.
- **DI is one answer for every client context in the process**, because
  `ExtensionInfo.GetServiceProviderHashCode()` is `0`. That is the fact R120 was about.
- **And the model itself is shared.** This provider registers no `IModelCacheKeyFactory`, so EF's
  default is in force and it keys the model on the context CLR type. Two contexts of the same type,
  one configured relational and one not, get **one model** — so even a per-context answer could not
  be honoured without replacing that factory.

**Two ways forward, and choosing between them is a decision rather than a step.**

1. **Level 2 is a process-wide statement.** `AddInfoCarrierRelational()` on the client's service
   collection means "every InfoCarrier client in this process has a relational backing store". Cheap,
   honest for the overwhelming majority of applications, and it makes the relational conventions a
   property of the deployment rather than of the context. What it gives up is the mixed process.
2. **Replace `IModelCacheKeyFactory`** so the options participate in the key, and carry the seam on
   the options as level 1 does. Correct in the mixed process, and it costs a model per options shape
   plus a new public service this provider did not have.

**Do not start level 2 without answering that.** Starting with (1) and discovering (2) later is a
public-API change and a model-cache change at once.

**One more thing measured while scoping, because it changes the test plan.** The relational client
services are registered today only where a fixture asked for **raw SQL**
(`InfoCarrierTestStoreFactory.AddProviderServices`, gated on `ArbitrarySqlExecution`). Level 2 wants
them wherever the backing store is relational, which is every Tier B fixture. So the first
measurement of level 2 is not the conventions at all: it is **turning the relational client on for
the whole of Tier B**, which is a large blast radius and should be measured on its own before a
single convention moves.

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
of scope. It is restated in [`roadmap.md`](plans/v10/roadmap.md) as *"the axis is identified, decided and
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

### D7 — the client gets EF's core services, and nobody had listed what the relational set adds

**Raised 2026-09-01, after three defects turned out to be one shape.** `EF.Functions.Collate` over a
constant was executed on the client instead of translated (R80); a `HasDbFunction` call whose
arguments were all constants was evaluated the same way (R84); and a `FromSql` query filter still
fails while the **client's** model is built (open, below). All three are one sentence: **EF
registers a different set of services and conventions when a provider is relational, this client
gets the core set, and the difference had never been written down.** Each was found by a failing
test, which is the expensive way to find any of them.

**The inventory.** `EntityFrameworkRelationalServicesBuilder.TryAddCoreServices` makes **61**
`TryAdd` calls and registers **36** dependency objects.
`RelationalConventionSetBuilder.CreateConventionSet` **adds 20** conventions and **replaces 4**.

**The cut that makes the list short, and it is ADR-006.** This client captures the raw expression
tree at `IDatabase.CompileQuery` and never compiles past it. Everything EF registers in order to
turn a query tree into SQL, batch a `SaveChanges`, open a connection, run a migration or read a
`DbDataReader` is **downstream of the capture point and belongs to the server's provider** — not
missing here, not wanted here. That is **50 of the 61** in one stroke: the SQL generator and the
whole translating-visitor stack, the update pipeline and its row-value factories, migrations, the
connection and its transaction factory, the command and connection loggers, the three
`IInterceptorAggregator`s, and the liftable-constant machinery. Two of them the client already
replaces with services that **throw** and say why (`InfoCarrierQueryPipelineFactories`), and four
more it replaces with its own (`IDatabase`, `IDatabaseCreator`, `IDbContextTransactionManager`,
and `IQueryContextFactory` below).

**The eleven that run before the capture point, or outside a query altogether.** These are the
client's own business, and this is the list that did not exist.

| EF's relational service | What the relational one adds | Standing here |
|---|---|---|
| `IEvaluatableExpressionFilter` | Two clauses: `EF.Functions` hosts, and `model.FindDbFunction` | **Was the gap.** `InfoCarrierEvaluatableExpressionFilter` ports both (R80, R84). |
| `IModelCustomizer` | Nothing — `RelationalModelCustomizer` is an empty subclass | Nothing to lose. Verified by reading it. |
| `IExecutionStrategyFactory` | Nothing by default — both return `NonRetryingExecutionStrategy`; the relational one only adds the hook for `RelationalOptionsExtension.ExecutionStrategyFactory` | Nothing to lose; the client has no relational options. Verified by reading both. |
| `ICompiledQueryCacheKeyGenerator` | `UseRelationalNulls`, `QuerySplittingBehavior` and a buffering flag, all read off `RelationalOptionsExtension` | **Cannot apply**, and R82's rule says why: those three are the *server's* configuration. |
| `ITypeMappingSource` | The store's type mappings | Deliberate. `InfoCarrierTypeMappingSource` answers from the CLR type alone, which is CLAUDE.md's "computed twice by two providers" rule. |
| `IValueGeneratorSelector` | Store-generated key defaults | Deliberate; `InfoCarrierValueGeneratorSelector`, found neutral by D3's audit. |
| `IQueryContextFactory` | The connection on the query context | Deliberate; `InfoCarrierQueryContextFactory`. |
| `IModelValidator` | Store-mapping validation — shared tables, column clashes, TPT | Not wanted. The client replaces core's for the opposite reason: to *relax* a rule (a hierarchy needs no discriminator here). |
| `IModelRuntimeInitializer` | Builds the relational model — tables, columns, foreign keys | Not wanted; store layout, and the server builds its own. |
| `IStructuralTypeMaterializerSource` | One override: `ReadComplexTypeDirectly` is false for a JSON-mapped complex type, so the shaper handles it rather than the materializer | **Open, unverified.** It is D3's question again — *is this mapped to a document?* — and `IInfoCarrierDocumentMapping` is the seam that would answer it. No failure is attributed to it today. |
| `IAdHocMapper` | Builds an entity type for a CLR type a query names and the model does not map | **Open, unverified**, and bound up with `FromSql` (#60), which is out of scope without the owner. |

**The conventions, which are where the live defect is.** Of the 18 `RelationalConventionSetBuilder`
*adds*, fifteen decide **store layout** — table and column names and comments, check constraints,
sequences, stored procedures, table sharing and splitting, property overrides, discriminator
length, the JSON property-name attributes. None of them can change what this client puts on the
wire, because the wire carries an expression tree and a change-tracking graph, not a table. Three
are worth naming:

| Convention | Reading |
|---|---|
| `RelationalMapToJsonConvention` | Sets the container annotation `AnnotationDocumentMapping` reads. The client does not run it, and D3's pins are green — `JsonQuery` 393/0, `JsonOwnedCollectionUpdate` 5/5 — because a caller's own `ToJson()` sets the annotation directly. The *implicit* cases are what the convention adds, and none is covered. |
| `TableSharingConcurrencyTokenConvention` | Adds a **shadow concurrency-token property** where two entity types share a table. **CLOSED 2026-09-02 (R91), by running it.** It cannot fire on any store this suite has: `GetConcurrencyTokensMap` skips a token that is not also `ValueGenerated.OnUpdate`, which on a token means `rowversion`. Forced to fire, the divergence is real and costs nothing — the server applies its own model to entries the client sent by property *name*, and both halves of a split still save. |
| `RelationalDbFunctionAttributeConvention` | Puts a `[DbFunction]`-attributed method into the model, so the server's model maps it and the client's does not — asserted both ways. **CLOSED 2026-09-02 (R91), and the answer was not the prediction:** the call still crosses and the server translates it, because what decides that is the **allowlist**, and the context type was on it for an unrelated reason. Had the context declared no mapped function at all, the same call would have been refused. |

Of the 4 *replacements*: `KeyDiscoveryConvention` and `ValueGenerationConvention` the client already
makes (`InfoCarrierKeyDiscoveryConvention`, `InfoCarrierValueGenerationConvention`), and
`EntityTypeHierarchyMappingConvention` — an addition rather than a replacement — is mirrored by
`InfoCarrierHierarchyMappingConvention`. `RuntimeModelConvention` is **open and unverified**; it is
the compiled model, and `Scaffolding.CompiledModel` is green. The fourth is the defect:

**`QueryFilterRewritingConvention` is not replaced, and three tests fail because of it.**
`RelationalQueryFilterRewritingConvention` teaches the rewriter that a `FromSql*` call is a query
root and turns it into a `FromSqlQueryRootExpression`. The core one does not know `FromSql`, so it
rewrites the inner `Set<T>()` into an `IQueryable` and leaves it where a `DbSet<T>` parameter is
expected: *"Expression of type `IQueryable<Dictionary<string, object>>` cannot be used for parameter
of type `DbSet<Dictionary<string, object>>` of method `FromSqlRaw`"*, raised **while the client's
model is built, before any query runs**. Three of `SharedTypeQueryInfoCarrierTest`'s four reds are
this.

**CLOSED 2026-09-02 (R88).** `InfoCarrierQueryFilterRewritingConvention` leaves a
`FromSql*` call exactly as the caller wrote it, which is R82's rule — the server applies its
own model's filter, so the client's only has to be *representable*. Two of the three pass;
the third converged onto #60, reaching `FromSqlRaw` on a non-relational client and saying
so. **The other four rows above are still open and unverified.**

**A fourth defect, found 2026-09-02 (R89), and it is the same sentence turned inside out.**
`RelationalEvaluatableExpressionFilter`'s second clause is what R84 ported so a mapped function
survives parameter extraction; admitting the mapping's declaring type to the allowlist is what let
the call be *named*. For a function mapped as an **instance** method that declaring type is the
caller's own `DbContext`, and admitting it silently removed the refusal that had been standing in
for the missing capability: **38 `TranslationFailed` refusals vanished at R84** and became client
evaluations. R89 restores the refusal by making a constant holding a `DbContext` never server-ok.

**OPEN, and it is a security question rather than a rewrite.** Making an instance-mapped function
actually *cross* needs a wire node that resolves to the **server's** context, the way
`QueryRootStubNode` resolves to the server's model. That is a new capability handed to a payload,
so `security-review.md` §2's per-class conjunction has to be re-argued for it before any code is
written. Worth roughly ten of the residual `UdfDbFunction` reds; not started, and not to be started
without the owner.

**What three of these rows turned out to have in common, and it is not the convention set.**
R84, R89 and R91 were each decided by whether the **type allowlist** happened to admit a type — for
R84 the host of a mapped function, for R89 the caller's own `DbContext`, for R91 a context that was
on the list because of a *different* function. The allowlist is documented as a deserialization
control (ADR-008, `security-review.md` §2) and it is also, undocumented, the thing that decides
where the query boundary falls and whether an untranslatable call is refused or quietly run on the
client. **Nothing says so where it is declared**, and each of the three cost a measurement to find.

**The pattern all three defects share, and it is the part that transfers.** Every one is a service
EF replaces *for a reason that has nothing to do with SQL* — the filter protects a call from being
evaluated, the convention teaches a rewriter about a method — and this client needs the same
behaviour for the same reason while wanting none of the SQL. **A relational service is not
automatically a store service**, and reading the class name is not how to tell them apart: the
question is whether it runs before `IDatabase.CompileQuery`.

### D8 — `FromSql` (#60) is a milestone with a security precondition, priced 2026-09-02

**Raised by R92, which priced it rather than starting it.** The owner cleared #60 for work; this
entry is what that work would be, so the decision to start it is taken against a number rather than
an impression.

**What it is worth, counted out of `artifacts/measure/r91.log` rather than estimated.**

| | |
|---|---|
| **27 of the 157** current failures are raw-SQL shaped | 14 `JsonQuerySqlite`, 4 `NorthwindBulkUpdates`, 3 `TPHInheritanceQuery`, 2 `OwnedQuery`, 2 `QueryNoClientEval`, 1 `SharedTypeQuery`, 1 `NullSemanticsQuery` |
| **6 of the 10** unimplemented spec bases are raw-SQL bases | `FromSqlQueryTestBase`, `FromSqlSprocQueryTestBase`, `GearsOfWarFromSqlQueryTestBase`, `NorthwindSqlQueryTestBase`, `SqlQueryTestBase`, `SqlExecutorTestBase` |

**Corrected 2026-09-02, same day: the first count of this was 21 and it was low.** It matched on
the test *name* and so missed the ones that fail earlier, on a cast. **`R77`'s
`InvalidCastException` — `InfoCarrierTestStore` cannot be cast to `RelationalTestStore` — is not a
separate item; it is this one's first blocker**, and all 26 tests carrying it are raw-SQL tests.
**Reviving R77 on its own would therefore turn 26 failures from a cast into a translation failure
and buy no green test at all**, which settles the standing "do not revive it without a base that
demonstrably needs it": a base that needs it exists, and it needs #60 more.

The other four missing bases are unrelated: `JsonUpdateTestBase` (ADR-013 — the client is never
relational), `StoredProcedureUpdateTestBase`, `StoreValueGenerationTestBase`,
`AdHocQuerySplittingQueryTestBase`. **An earlier note said seven of ten wait on #60; the count is
six.**

**Three pieces of work, and the third is not code.**

1. **A wire node for `FromSqlQueryRootExpression`.** `ServerBoundaryAnalyzer.IsSerializableKind`
   refuses it today *by exact type match*, with a comment recording what happened when it did not:
   a `FromSqlRaw` with a `WHERE` came back as the whole table, silently. The type lives in
   `Microsoft.EntityFrameworkCore.Relational`, which this package deliberately does not reference
   (D3, M9 J5) — so the node has to carry `Sql` + `Argument` and be rebuilt on the server, the way
   `QueryRootStubNode` is rebuilt against the server's model.
2. **`SqlQuery<T>` and `Database.ExecuteSql` are separate entry points**, not query roots, and
   `SqlExecutorTestBase` does not go through the query pipeline at all.
3. **A `security-review.md` section, before either.** Every argument in that document is about what
   a payload may *name* — ADR-008's allowlist, the per-class conjunction of §2. **Raw SQL is a
   different axis: a payload that names nothing dangerous can still carry `DROP TABLE`.** Today the
   server executes only trees it rebuilt from a vocabulary it controls, and `FromSql` would be the
   first construct where the client hands the server a string to run. That is a change of posture,
   not an extension of the allowlist, and §2 cannot be stretched to cover it.

**Recommendation: item 3 first, and on its own.** The two code pieces are ordinary work whose shape
is already known; the security section decides whether they should exist, and in what form — an
opt-in server registration in the shape of R85's `AddInfoCarrierAllowedTypes` is the obvious
candidate, but that is the owner's call and a review's, not a step's.

#### D8 amendment 2026-09-02 — built, and the ordering was changed once for a measured reason

**The gate is `AddInfoCarrierArbitrarySqlExecution` (server) and `AllowArbitrarySqlExecution`
(client), and the name is the finding.** R94 put the two questions the security section rests on to
a test rather than to a reading, and both answers removed an option: **one `CommandText` executes
every statement it contains**, and **an uncomposed `FromSqlRaw` reaches the store unwrapped**. The
`FROM (…)` subquery that would have confined a caller to reading is an artefact of *composing* on
the query, and the caller decides whether to compose. So there is no read-only version of
`FromSql` to grant, the API cannot honestly be called "enable raw queries", and §2's per-class
conjunction gives no support — SQL text is not a naming question and there is no set to enumerate.
`security-review.md` §5a is the written form.

**Item 3 still came first in substance, and item 1 was built in the same step rather than the
next.** A registration that admits a node the wire cannot yet carry is a switch that turns on a
broken path, so R95 lands the gate and the `FromSqlQueryRootStubNode` together. The reflection that
buys — two `GetProperty` reads on the client and one `Activator.CreateInstance` on the server, all
in `RelationalQueryRootShape` — is the premise `eng/trim-baseline.txt` already describes, and every
call site carries a narrow `[UnconditionalSuppressMessage]` naming why the members survive.

**Item 2 is untouched and stays priced — but half of its stated reason was wrong.** `SqlQuery<T>`
and `Database.ExecuteSql` are separate entry points rather than query roots, and that half stands:
`RelationalDatabaseFacadeExtensions.GetFacadeDependencies` refuses a non-relational context before
any query is built, which is where four tests now stop. **The `DbParameter` half does not.** R98
adopted `FromSqlQueryTestBase` and 32 of its 54 first-run failures were
`Type 'Microsoft.Data.Sqlite.SqliteParameter' is not on the deserialization allowlist` — not a wire
limit at all. A `DbParameter` is an ordinary object with a parameterless constructor and settable
properties; the wire walks it and the server rebuilds it with no special handling, and admitting
the type through R85's seam turns all 32 green. **"The client cannot construct one" was never
tested and is false**: the test project references the store's provider, as any application whose
server it talks to would.

**Which is the fourth time in this issue that the type allowlist decided the behaviour** — R84,
R89, R91 and now R98 — and D7's note about it being load-bearing far beyond deserialization safety
is the general form.

#### D8 item 2 priced 2026-09-02 (R102) — it is blocked by D3, and by nothing smaller

**`Database.SqlQuery<T>` is not reachable from a client that does not reference
`EFCore.Relational`, and the obstacle is a type test rather than a capability.**
`RelationalDatabaseFacadeExtensions.SqlQueryRaw` opens with `GetFacadeDependencies`, which does

```csharp
dependencies is IRelationalDatabaseFacadeDependencies relationalDependencies
    ? relationalDependencies
    : throw new InvalidOperationException(RelationalStrings.RelationalNotInUse);
```

and that interface lives in `EFCore.Relational`. **A client can only satisfy it by referencing the
package**, which is exactly what M9's J5 removed and what D3 records. Nothing in this repository
gets past that line: it runs before any expression is built, so no wire node, no allowlist entry and
no boundary change can be reached.

**What it would need, in order, if D3 were reversed.**

1. `InfoCarrier.Core` references `Microsoft.EntityFrameworkCore.Relational` again.
2. An `IRelationalDatabaseFacadeDependencies` registered on the client whose **relational half
   throws** — `RelationalConnection`, `RawSqlCommandBuilder`, `CommandLogger` — and whose core half
   is real. That shape is already proven here: it is exactly what
   `RelationalInfoCarrierTestStore` does to `RelationalTestStore` (ADR-013's amendment), and the
   reason it works is the same, that the callers wanting the connection are not the callers wanting
   the rest.
3. `SqlQueryRaw` then builds one of two roots, and only the first is new work.
   `SqlQueryRootExpression` for a `TResult` the type-mapping source recognises — a scalar — is a
   direct sibling of R95's `FromSqlQueryRootStubNode` and costs about what that did.
   `FromSqlQueryRootExpression` for anything else already crosses.
4. **The second root has a further problem that is not about SQL.** It is built over
   `AdHocMapper.GetOrAddEntityType(typeof(TResult))`, an entity type created on the **client's**
   model at call time. The server resolves a query root through *its* model
   (`ServerQueryExecutor.RebindQueryRoot`), and an ad-hoc type is in neither the shared model nor
   the server's. So `SqlQuery<SomeDto>` needs the ad-hoc entity type to cross as well, which is a
   new capability rather than a new node.

**What it is worth.** Two missing bases, `NorthwindSqlQueryTestBase` and `SqlQueryTestBase`, both of
which EF ships SQLite classes for; and 6 of the current failures, 4 in `FromSqlQueryInfoCarrierTest`
and 2 in `SharedTypeQueryInfoCarrierTest`. **The 6 is a count by class and R111 corrected it to 3 by
cause**: `Multiple_occurrences_of_FromSql_with_db_parameter_adds_two_parameters` (sync and async)
and `Ad_hoc_query_for_shared_type_entity_type_works` are the only three whose stack trace reaches
`GetFacadeDependencies`. The other ten failures in those two classes have four other causes. The
bases are where item 2's value is; the current-failure figure never was. **Every test in both bases routes through
`Database.SqlQuery`** — 137 call sites across the two files — so there is no partial adoption to
take.

**Recommendation: not taken here.** Step 1 reverses a milestone exit criterion and `CLAUDE.md` is
explicit that such a reversal is a dated decision rather than a code change that quietly contradicts
one. The pricing is recorded so the decision can be made against a number.

#### D8 item 2 re-priced 2026-09-02 (R110) — D3 does not have to be reversed

**R102 read the blocker correctly and then assumed only one way past it.** Its step 1 —
`InfoCarrier.Core` references `EFCore.Relational` again — is not the only route, and it is not the
cheapest. Two others were put by the owner and both were checked here rather than argued.

**Naming the relational types by string does not work, and this is the one place that trick fails.**
Two sites in this repository already name relational things by string:
`QuerySplitter`'s `RelationalQueryableExtensionsFullName`, and `RelationalQueryRootShape`, which
reads a `FromSqlQueryRootExpression` by its shape. Both work because the question there is *what is
this node called*. Here the question is *does this object implement this interface*:

```csharp
dependencies is IRelationalDatabaseFacadeDependencies relationalDependencies
```

That is a CLR type test. It is answered by the runtime type's interface table, and no name, string
or shape can satisfy it. **Read, not measured** — but it is a language rule, not a judgement.

**An implementation registered from outside the package works, and it was measured.**
`DatabaseFacade` resolves its dependencies from the context's own service provider:

```csharp
private IDatabaseFacadeDependencies Dependencies
    => field ??= context.GetService<IDatabaseFacadeDependencies>();
```

So anything that can name `IRelationalDatabaseFacadeDependencies` can replace that registration, and
`InfoCarrier.Core` never has to be the thing that names it. A probe registered a class implementing
that interface through `DbContextOptionsBuilder.ReplaceService<IDatabaseFacadeDependencies, …>()`
on an ordinary InfoCarrier client, and the type test passed: the facade resolved the replacement and
`is IRelationalDatabaseFacadeDependencies` was true. **D3 stands as written, unamended.**

**The relational half throws and nothing on this path calls it.** The three members the relational
interface adds — `RelationalConnection`, `RawSqlCommandBuilder`, `CommandLogger` — have no meaning
on a client with no database, and the probe's implementation threw from all three. Every call still
completed, because `SqlQueryRaw` reads only `QueryProvider`, `TypeMappingSource` and `AdHocMapper`,
and all three are on the **core** interface. This is the shape ADR-013's amendment already records
for `RelationalInfoCarrierTestStore`.

**Two shapes for the registration, and the difference is who ships the class.**

| | Who names `EFCore.Relational` | Cost to the consumer |
|---|---|---|
| **D1** — the application registers it | the application | writes the class, or copies it; the same seam as R85's `AddInfoCarrierAllowedTypes` and R95's `AddInfoCarrierArbitrarySqlExecution` |
| **D2** — an optional companion package, e.g. `InfoCarrier.Core.Relational` | that package | one `PackageReference` and one call |

D1 is what the probe used and is what proves the mechanism. **D2 is the better product** and costs
nothing extra to build once D1 works: the class is the same class, and the reference becomes opt-in
at the NuGet level rather than at the DI level. Neither is started; this is the owner's call.

**Past the type test, the two roots behave differently, and both were measured.**

1. **The scalar root is exactly the new work R102 predicted.** `SqlQueryRaw<int>` built a
   `SqlQueryRootExpression` and the client refused it with *"`SqlQueryRootExpression` (Extension)
   has no wire representation"*. A direct sibling of R95's `FromSqlQueryRootStubNode`, as priced.
2. **The non-scalar root gets further than R102 expected, and dies somewhere else.**
   `SqlQueryRaw<TDto>` for a navigation-free DTO reached `AdHocMapper.GetOrAddEntityType` on the
   **client**, which built the entity type without complaint, produced a `FromSqlQueryRootExpression`
   — R95's node, already crossing — and was then refused by **this provider's own type allowlist**,
   naming the DTO. That refusal is ADR-008 constraint 2 working, and the answer to it is an ordinary
   application registration: R85's `AddInfoCarrierAllowedTypes` on the server and `AllowTypes` on
   the client. With the DTO admitted on both sides the query crossed the wire and the **server**
   raised *"Entity type 'BlogRow' not found in the server model"*.

**So R102's point 4 is confirmed, and it is now measured rather than reasoned.** The gap is not
"getting an ad-hoc entity type across" — the client builds one and the node already crosses. The gap
is that `ServerQueryExecutor.RebindQueryRoot` resolves through the server's model and the server has
never been asked to create the matching ad-hoc type. What would close it is the server calling its
own `AdHocMapper` when a `FromSqlQueryRootExpression` names a type its model does not hold, which is
a smaller thing than a new capability but is still a change to the rebind path and to what the
server will construct on a client's say-so. **It is the same security question the raw-SQL gate
asked and it should be answered the same way.**

**A type test that a DTO reached through `SqlQuery` is nothing like a type test a query root
reached through the model**, and that is the whole reason the allowlist refused first: nothing in
the model implies a `SqlQuery<T>` result type. Any work here starts from that, not from the SQL.

**Still not taken, and still the owner's decision.** What changed is that the decision no longer has
to be *"reverse D3 or drop two bases"*. The third option is real, was measured, and leaves the
milestone exit criterion intact.

#### D8 item 2 BUILT 2026-09-02 (R114, R115) — option D, and D3 untouched

**The owner chose option D. It is built, and the count moved.** `failed` 143 → 141 on the shim
alone, and `NorthwindSqlQueryTestBase` then adopted with **8 of 8 green on the first run**
(`total` 29384 → 29392). Compliance missing bases **2 → 1**.

**Two pieces, and only the second is product code.**

1. **`InfoCarrierRelationalFacadeDependencies`** (`test/`, R114) implements
   `IRelationalDatabaseFacadeDependencies` and is registered by the harness, which is an
   application. `RelationalConnection`, `RawSqlCommandBuilder` and the relational `CommandLogger`
   throw; nothing on this path calls them. **`InfoCarrier.Core` references nothing relational and
   D3 stands as written.** A shipped `InfoCarrier.Core.Relational` package would hold this same
   class — a packaging step, not a design one, costing a new `csproj`, `release.yml`'s package list
   and three user-facing pages. Not taken.
2. **`SqlQueryRootStubNode`** (`src/`, R115) carries EF's `SqlQueryRootExpression` across the wire.
   **A scalar root is the one query root with no entity type**, so
   `ServerQueryExecutor.RebindQueryRoot` answers it *before* it resolves one, rebuilding from the
   CLR element type the wire carried. Same grant as `FromSqlQueryRootStubNode`, and
   `RequireArbitrarySql` is now one method serving both because there is one grant.
   `security-review.md` §5a has the addendum.

**The trim ratchet caught the only mistake, and its lesson generalises.** Sharing one
`ResolveRootType(string fullName)` between the two roots turned a `const`-folded literal into a
**parameter**, which the trim analyzer cannot read: `IL2057`, 89 → 90, and the ratchet failed.
Splitting the two `Type.GetType` calls back apart returned it to 89. **That is the same lesson as
the `foreach`-not-`Select` note in the same file, from the other direction**: a suppression covers a
member's own body, and so does the analyzer's ability to see a constant. `src/` changed, so both
gates ran; `eng/measure.sh` and `eng/trim-ratchet.sh` are separate axes and this change failed one
while passing the other.

**What is still not built, and it is the other base.** `SqlQueryTestBase` (61 methods) projects into
`UnmappedProduct` and `UnmappedCustomer`, which take EF down the ad-hoc entity-type path.
Measured, not predicted: the **client's** `AdHocMapper` builds that entity type without complaint
and R95's node carries it; the type allowlist refuses the DTO until an application admits it
(R85's seam); and past that the **server** raises *"not found in the server model"*. Closing it
means the server calling its own `AdHocMapper` when a raw-SQL root names a type its model does not
hold — **the server constructing an entity type on a client's say-so**, which is a widening and
gets its own security reading before it is taken.

**`SqlExecutorTestBase` is not part of this and should never have been listed with it (R102).** Its
first three tests are `Executes_stored_procedure`, `Executes_stored_procedure_with_parameter` and
`Executes_stored_procedure_with_generated_parameter`: it is a stored-procedure base wearing a
general name. **EF's own `SqliteComplianceTest` ignores it**, alongside `FromSqlSprocQueryTestBase`
and `StoredProcedureUpdateTestBase`, and only SQL Server implements any of the three. So `ExecuteSql`
is not what item 2 needs, and D8's original sentence pairing `SqlQuery<T>` with `Database.ExecuteSql`
put a store limitation and a client limitation in one bucket.

**Where the numbers at the top of D8 came out (R100).** Of the **27** failures, **25 are green**;
the two that are not are `Delete_FromSql_converted_to_subquery`, whose cause is a harness mismatch
between `NorthwindRelationalContext`'s table names and the core model this tier builds its store
from (R97 fixed it, measured 236 other breakages, and reverted). Of the **6** missing bases,
**`FromSqlQueryTestBase` and `GearsOfWarFromSqlQueryTestBase` are adopted**. The remaining four are
one axis rather than four: `NorthwindSqlQueryTestBase`, `SqlQueryTestBase` and `SqlExecutorTestBase`
are all item 2, and `FromSqlSprocQueryTestBase` needs stored procedures, which SQLite has not and
for which EF ships no SQLite class.

## 7. Out of scope (initial release) — requirements §6

AuthN/authZ (protocol must not preclude); offline/disconnected caching; client-side query
composition beyond EF tracking; multi-tenant server-side context resolution (protocol must
not preclude).
