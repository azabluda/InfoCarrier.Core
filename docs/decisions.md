# Design Decisions (ADR log)

Record of design decisions for InfoCarrier.Core v2. Each entry states context, decision,
and rationale.

> **Status note.** The pre-implementation research phase closed 2026-07-22
> ([`research-findings.md`](research-findings.md)); the project is now in **implementation**
> (see [`roadmap.md`](plans/v10/roadmap.md)). Each entry is marked:
> - **LOCKED** — decided and binding; reversing requires a dated supersession edit here.
> - **PROVISIONAL** — current best understanding, **subject to change** as research
>   continues. Provisional entries shape the specs but are not yet commitments; each links
>   to the open research questions that must be resolved to lock it.

Related: [`architecture.md`](architecture.md) · [`expression-serialization.md`](expression-serialization.md)
· [`wire-protocol.md`](wire-protocol.md) · [`research-infrastructure.md`](research-infrastructure.md)

---

## ADR-001 — Serialization engine: greenfield, spec-only — LOCKED (2026-07-19)

**Context.** The expression serializer is the heart of InfoCarrier. Remote.Linq + Aqua
are the canonical prior art and v1's engine. Research (see
[`expression-serialization.md`](expression-serialization.md)) shows they provide a proven
~80% (node DTOs, shape-based `TypeInfo`, `DynamicObject`, translators, partial-eval) but
with gaps that conflict directly with v2's stated goals.

**Decision.** Build the serialization engine **from scratch**, using Remote.Linq's and
Aqua's designs **as a written specification only**. No NuGet dependency on Remote.Linq or
Aqua; no source fork.

**Rationale.**
- v2 requires **AOT/trimming compatibility** — Aqua's unknown-type fallback is RuntimeIL
  emission (`TypeEmitter`), which is not AOT-safe, and its legacy path uses
  `FormatterServices` (obsolete SYSLIB0050 on .NET 8+).
- v2 is **DI-first** — Remote.Linq relies on a mutable static `TypeResolver.Instance`.
- v2 requires a **versioned wire envelope** and **strict server-side allowlists** — neither
  package has wire versioning or execution allowlists.
- Adopting the packages as-is would import all four conflicts; wrapping them leaves the AOT
  and DI goals unmet.

**Consequences.** We re-implement: serializable expression node DTOs, shape-based type
identity, dynamic value objects, bidirectional translators, partial evaluation. In return we
get: canonical deterministic serialization (enables a compiled-query cache), a versioned
envelope, allowlists on by default, DI-resolved components, and a clean AOT path.

## ADR-002 — No SpecKit / no constitution file — LOCKED (2026-07-19)

**Context.** The project's original implementation prompt (superseded by the specs in
`docs/`) mandated a SpecKit "constitution" rulebook (`docs/constitution.md`) before any
code.

**Decision.** Do **not** adopt SpecKit. Never create `docs/constitution.md` or any
constitution/rulebook file; drop all "SpecKit Planning Phase" framing. The constitution's
*content* (test-skip policy, build discipline, architecture guardrails) is preserved as
ordinary working agreements inside the relevant specs and `README.md` — not as a separate
governing artifact.

**Rationale.** User direction. Process overhead is kept proportional to a single-maintainer
greenfield project; rules live next to the work they govern.

## ADR-003 — Pre-implementation first, then fixed build order — LOCKED (2026-07-19)

**Context.** "Start implementation" was given, but extensive third-party code study must
precede writing our own code.

**Decision.** Two-stage execution:
1. **Pre-implementation (current phase)** — study `subrepos/*` (efcore authoritative;
   rlinq / aqua / infocarrier-v1 inspiration-only), author the spec docs, and resolve the
   open research questions tracked in each spec. **No product code is written in this
   phase.**
2. **Implementation** — begins only after the specs' open questions are resolved, then
   follows the fixed build order below (constitution step voided by ADR-002).

**Build order (implementation sequence):**

| Step | Deliverable |
|---|---|
| 1 | Solution + projects (`InfoCarrier.Core`, `InfoCarrier.Core.Abstractions`, functional tests) |
| 2 | Common DTOs (wire envelope, Query/SaveChanges request/result) |
| 3 | `IInfoCarrierClient` / `IInfoCarrierServer` contracts |
| 4 | Test infrastructure (`InfoCarrierBackendTestStore`, in-process JSON round-trip transport) |
| 5 | Server-side query execution |
| 6 | Client-side `IDatabase.CompileQuery` capture |
| 7 | Expression serialization (client → wire) |
| 8 | Result materialization (wire → client) |
| 9 | Server expression rewriting (stubs → `DbSet<T>` → `QueryRootExpression`) |
| 10 | SaveChanges pipeline (incl. M2M from day 1) |
| 11 | First green InMemory Northwind functional test |
| 12 | Expand coverage incrementally; add SqlServer (Docker) tests |
| 13 | Sample apps; CI/CD workflows; performance profiling |

**Rationale.** Locks the build order while acknowledging the specs are not yet final; the
sequence builds a vertical slice so harness and wire are validated before the hardest part
(expression-serialization fidelity) is hardened.

## ADR-004 — Test strategy: inherit `EFCore.Specification.Tests` — LOCKED (2026-07-19)

**Context.** v1's pain point was test-suite ambition: ~12,890 tests retrofitted late.

**Decision.** Consume the **`Microsoft.EntityFrameworkCore.Specification.Tests` NuGet
package** (not source), and inherit Microsoft's test base classes via an InfoCarrier
fixture — the v1 pattern, rebuilt against EF Core 10. Build coverage **incrementally**,
starting with the **InMemory** backend, then SQL Server (Docker, not LocalDB). Many-to-many
`SaveChanges` is tested from day one.

**Rationale.** Maximum, authoritative coverage with minimal hand-written tests; the v1
fixture architecture (client `TestStore` wrapper + backend store doubling as
`IInfoCarrierClient` + JSON round-trip simulation) ports cleanly (see
[`architecture.md`](architecture.md) §Test Strategy).

## ADR-005 — Research subrepos: ignored, no un-ignore exceptions — LOCKED (2026-07-19)

**Context.** `subrepos/` holds four cloned repositories for source-level reference.

**Decision.** The whole `subrepos/` tree is git-ignored with **no** un-ignore exceptions for
nested folders. Contents are external code plus machine-generated CodeGraph indexes
(`.codegraph/codegraph.db`), both non-portable and regenerable. Pinned revisions are recorded
outside the ignored tree in [`research-infrastructure.md`](research-infrastructure.md).

**Rationale.** Nothing inside `subrepos/` is ours to commit; notes about them belong in
`docs/`, not inside a clone.

## ADR-006 — Pipeline approach: raw capture at `IDatabase.CompileQuery` — LOCKED (2026-07-22)

**Context.** Two candidate capture points for the client query: **(A)** post-translation
(capture EF Core's already-processed query) vs **(B)** raw capture (intercept the LINQ
expression before EF's query pipeline).

**Decision.** **(B) raw capture.** The client intercepts the LINQ expression at
`IDatabase.CompileQuery` before EF's translation pipeline, replaces queryable roots with
stubs, substitutes compiled-query parameters as plain constants, and ships the tree. The
server owns translation against the real provider.

**Rationale.** Post-translation trees contain shaper delegates and provider-specific nodes
that are not portable across the wire and not re-executable on a different provider. Raw
capture keeps the tree re-translatable server-side — the same reason rlinq serializes the
pre-translation tree, and the pattern v1 proved. See
[`research-findings.md`](research-findings.md) §1.

**Supersedes.** PROVISIONAL entry of 2026-07-19 (evaluate both). Locked after EF Core 10
query-pipeline study.

## ADR-007 — CodeGraph research tooling: `@colbymchenry/codegraph` via npx only — LOCKED (2026-07-19)

**Context.** Large reference codebases (especially `subrepos/efcore`) need fast structural
queries.

**Decision.** Use **`@colbymchenry/codegraph`** as the only code-graph tool, invoked
**exclusively via `npx`** — never `npm install -g`, never the interactive installer, never
assuming it is on `PATH`. One MCP server entry in `.vscode/mcp.json`; subrepos are indexed
one-shot (no file watcher, `CODEGRAPH_NO_DAEMON=1`). Do not use the `codebase-memory` skill.

**Rationale.** Reproducible, install-free tooling that any contributor (or agent) can run
identically. See [`research-infrastructure.md`](research-infrastructure.md).

## ADR-008 — Serializer design: rlinq/aqua patterns, EF-metadata-driven — LOCKED (2026-07-22, recorded 2026-08-01)

> **Record note.** [`research-findings.md`](research-findings.md) §"Locked consequences"
> declared this ADR locked on 2026-07-22, and
> [`expression-serialization.md`](expression-serialization.md) §3 +
> [`implementation-plan.md`](plans/v10/implementation-plan.md) both cite it — but the entry was never
> written into this log. Recorded here retroactively from those sources. No decision changed;
> this closes a dangling reference.

**Context.** ADR-001 commits to a greenfield serializer using Remote.Linq and Aqua as written
specification. That leaves the actual design open: which patterns to adopt, and which of their
properties to deliberately reject.

**Decision.** rlinq-style node DTOs + aqua-style shape-based type identity, with eight binding
constraints (expression-serialization §3):

1. **EF-metadata-driven mapper** — entities map via `IModel` metadata (entity types, shadow
   properties, keys, value converters), never blind public-reflection walks.
2. **Strict allowlists ON by default** — allowed node kinds, allowed `MethodInfo`s (Queryable /
   Enumerable / `EF.Functions` / model-bound members), and allowed deserializable types (model
   entities + registered projection types).
3. **DI everywhere** — no statics (rlinq's `TypeResolver.Instance` is the anti-pattern).
4. **Versioned envelope** — protocol version in every message from day 1.
5. **Reference-preserving serializer** — circular navigation references must survive.
6. **Canonical, deterministic serialization** — enables a compiled-query cache keyed by
   structural hash.
7. **Explicit enum maps** — no int-casting across the System↔remote boundary.
8. **No `FormatterServices`, no IL-emit** — instances via EF materializer paths or matched
   constructors; values read through properties / EF `IProperty` accessors so lazy-loading
   proxies forward correctly.

Node set is the minimal one of research-findings §5 (no Block/Loop/Try/Goto/Switch/Label);
entity identity on the wire is EF entity-type name + key values per §7 — entities must never
merge by shape, projections may.

**Implementation status (2026-08-01, revised).** Constraints 1, 3, 4, 5, 7, 8 are implemented.

**Constraint 2 — type allowlist: implemented.** `TypeAllowlist` is on by default and derived
from the model (entity CLR types + mapped property types) plus a fixed set of framework types
and explicitly registered projection types. `TypeNodeResolver` still resolves a name before
checking it — the name has to be resolved to know what it denotes — but nothing is constructed
until it clears the list. This closes the remote-code-execution vector: the deserializer can no
longer be told to instantiate an arbitrary type by name.

**The *method* allowlist half of constraint 2 closed in C30 (2026-08-10), and this paragraph said
otherwise until 2026-08-24.** `NodeToExpressionTranslator.Admit` now requires two things at once:
the declaring type must clear `TypeAllowlist`, and the method must be **public**, or one of the two
non-public methods named in `AllowedNonPublicMethods` (`NotQuiteInclude`, `ExecuteUpdate`).

**That is not literally the list this ADR asked for**, and saying so is the point of recording it
here. The ADR wrote "Queryable / Enumerable / `EF.Functions` / model-bound members"; what shipped is
public-by-default on an allowed declaring type. The decision is unchanged and this is not a
supersession: constraint 2 asked for a default-deny method gate and there is one. What a future
reader needs to know is its exact shape, because it decides what is reachable. `IgnoreQueryFilters`
is public on an allowed type, so nothing today can refuse it by name, which is the mechanism behind
the open design question in
[`plans/v10/cold-read-findings.md`](plans/v10/cold-read-findings.md) §1.

The network transport shipped in M8 with this gate in place, which is what the old wording,
"still required before a network transport ships", was asking for.

> **What enabling it revealed.** Failures went 32 → 1,421 of 4,247. **1,197 are anonymous or
> other compiler-generated projection types, and ~108 more are client-only DTOs** — together
> ~31% of the suite, all of it the projection split (requirements §3, milestone M2). Those tests
> were passing only because the in-process transport shares an `AppDomain`, so an assembly scan
> found types no network server could ever have. The estimate before this landed was ~16 tests.
> This is the same class of self-deception recorded against G4e, at roughly eighty times the
> scale.

Constraint 6 (canonical form) is not exercised: no compiled-query cache exists, and
**building one is out of scope for v10** (2026-08-24, owner's decision; `plans/v10/roadmap.md`, M8
exit criteria). The constraint itself stands unchanged and nothing in the code contradicts it. It
was never a correctness item, which is why it could be deferred without touching this ADR's
substance: EF's own `ICompiledQueryCache` already caches what the client's `CompileQuery` returns,
so what repeats per request is serialization and translation, and that returns the same answer every
time.

## ADR-009 — Test backends: SQLite in-memory as the relational tier — LOCKED (2026-08-01)

**Context.** [`ci-cd.md`](ci-cd.md) mandated Docker SQL Server as *the* realistic backend and
explicitly rejected LocalDB. Meanwhile EF Core's InMemory provider — the only backend built —
**cannot test transactions at all**: it registers `TransactionIgnoredWarning` with
`WarningBehavior.Throw` (`src/EFCore.InMemory/Extensions/InMemoryDbContextOptionsExtensions.cs`).
Requirements §2.9 (transactions) and §2.2 (SaveChanges, FK enforcement, store-generated values)
are therefore untestable on the current backend, and the documented alternative costs a
container on every developer machine and CI run.

**Decision.** Three test backend tiers:

| Tier | Backend | Scope | Cadence |
|---|---|---|---|
| **A** | InMemory | Query semantics, fast iteration | Every run |
| **B** | **SQLite in-memory** | Transactions + savepoints, FK enforcement, constraint violations, store-generated keys, relational type mapping | Every run |
| **C** | SQL Server (Docker) | `rowversion` concurrency, computed columns, sequences, TPT/TPC, NTS spatial Z/M | Nightly / on-demand |

**Rationale.** A backend store in this architecture is ~30 lines
(`InMemoryInfoCarrierBackendTestStore`), so Tier B costs roughly 40 lines and runs in
milliseconds with no container. It unblocks SaveChanges and transaction coverage on every
commit and defers Docker to the cases that genuinely need SQL Server semantics. Tier C is not
cancelled — it is demoted from prerequisite to fidelity check.

**Consequences.** `SqliteInfoCarrierBackendTestStore` must hold one `SqliteConnection` open for
the store's lifetime (an in-memory SQLite database is destroyed when its last connection
closes). `ci-cd.md`'s "Docker SQL Server, NOT LocalDB" framing is superseded for Tiers A/B and
retained for Tier C.

**Supersedes.** The Docker-only backend strategy in [`ci-cd.md`](ci-cd.md).

### Amendment 2026-09-04 — Tier C is dropped as SQL Server and re-created as embedded Firebird

**The SQL Server tier above is dropped, not deferred** (2026-08-24, owner's decision;
[`plans/v10/roadmap.md`](plans/v10/roadmap.md), M7). What was withdrawn is a third *test tier* for
this repository, never support for the store: the server side is an ordinary EF application and
runs against whatever provider it references.

**Tier C now means embedded Firebird, and the letter is reused deliberately.** Do not read a
pre-2026-09-04 "Tier C" as this one; the row in the table above is the old meaning and is dead.

**Why a third tier exists again.** One capability, and it is the only one that justifies the cost.
SQLite has **no table-valued function and cannot be given one**: `Microsoft.Data.Sqlite` attaches
scalar delegates to a connection and exposes no `sqlite3_create_module`, so there are no virtual
tables and `SELECT ... FROM SomeFunction(...)` has no meaning. SQLite also has no `APPLY`. Between
them those two gaps are the whole of `UdfDbFunctionTestBase` that Tier B leaves red, and EF ships
that base for SQL Server only. Firebird has both: a *selectable stored procedure* is queried
exactly as a table-valued function is, and `LATERAL` has been in the engine since version 4.

**Why Firebird and not PostgreSQL.** PostgreSQL is the stronger store for this base on the
evidence — Npgsql adopts it and skips 2 tests, where Firebird's own provider skips 23 — but it is
a server process whose binaries are fetched at first run. **The constraint was no installation and
no container**, and Firebird meets it the way SQLite does: the engine arrives as a NuGet package of
native assets, one database is one file in the test output directory, and nothing is downloaded
during a run. The trade was measured, not assumed.

**Two things were measured before this was built, and both changed the decision.**

1. **The store does everything, including what Firebird's own EF provider marks unsupported.**
   A selectable procedure can be called with an argument from the outer table, both as
   `FROM a, proc(a.col)` and inside a real `LATERAL` derived table.
2. **The 14 "Not supported on Firebird" skips in that provider's suite are one SQL-generation
   defect**, not a store limit. `FbQuerySqlGenerator` wraps a plain table as
   `(SELECT * FROM "T") AS "t"` after `LATERAL`, because Firebird will not take a bare source
   there, and the same branch was never added for a function. `FirebirdLateralQuerySqlGenerator`
   in the test harness adds it, on the **server** half only. **Reported upstream as
   [FirebirdSQL/NETProvider#1277](https://github.com/FirebirdSQL/NETProvider/issues/1277) on
   2026-09-04**, with the repro and the suggested branch; delete the file when that lands.

**Scope, and it is narrow.** A base belongs to exactly one tier. Only a base that *needs* a
table-valued function or `APPLY` belongs here; everything else stays where its green already means
something. Running a base on two tiers is duplication, not coverage.

**Consequences.** The three community packages that carry the Firebird binaries are test-only and
must never appear in `src/`. `eng/measure.sh` needs no change, because the tiers are namespaces in
one project.

## ADR-010 — Projection split: boundary computed on the client — LOCKED (2026-08-01)

**Context.** Requirements §3: the server holds only the shared entity assembly, so it cannot
materialize anonymous types, client-only DTOs, or client-declared value tuples.
[`research-findings.md`](research-findings.md) §8 resolved this as *server-side* detection — the
server receives the whole tree, detects where server-unknown types appear, executes the
entity-typed portion. That was written before ADR-008 constraint 2 was implemented.

Step L1 (2026-08-01) turned the type allowlist on. Failures went **32 → 1,421 of 4,247**;
~1,305 of them are this milestone. The allowlist also changed what is *possible*: rejection now
happens inside `TypeNodeResolver.Resolve`, during deserialization.

**Decision.** The **client** computes the boundary, before serialization. It ships only
server-executable subtrees and evaluates the remainder locally against the materialized results.
`TypeAllowlist` is the shared definition of "server-known", so the two sides cannot disagree.
Design: [`projection-split.md`](projection-split.md).

**Rationale.** Server-side detection is not merely inconvenient under the allowlist, it is
excluded by it: a tree naming an anonymous type throws during deserialization, before the server
has an expression to analyze. Making the server tolerate unresolvable type names is exactly the
default-deny violation constraint 2 forbids, and would reopen the RCE surface L1 closed. The
client, meanwhile, already holds the tree as a live expression over its own `MemberInfo`s and can
simply evaluate the residual; the server could only describe the residual back, needing a
round-trip and a second wire vocabulary.

**Consequences.** The wire format does not change. `ServerQueryExecutor` does not change — if it
needs edits, the boundary was drawn in the wrong place. Projection lambdas are *rewritten* into a
`ValueTuple` carrier rather than cut, which is simultaneously the fix for correlated subqueries
and navigation reads (a cut answers `c.Orders.Count()` as `0`, silently) and the minimal-column
payload of requirements §3.3 (wire-protocol W1).

**Supersedes.** [`research-findings.md`](research-findings.md) §8, in placement only. Its
mechanism — execute the entity-typed portion, apply the projection locally, no tree surgery, no
new wire vocabulary — is retained.

## ADR-011 — Transparent identifiers are re-carried, not reassembled — LOCKED (2026-08-02)

**Context.** `from c in cs from o in c.Orders … select c` contains no anonymous type that the
caller wrote; the C# compiler inserts a *transparent identifier* so later clauses can still see
`c`. [ADR-010](#adr-010-projection-split-boundary-computed-on-the-client-locked-2026-08-01) treats an anonymous type as a type boundary, which is right for a
projection the caller asked for and wrong for compiler plumbing: every operator above the
identifier falls to the client, taking 36 of the 111 remaining failures with it — 16 of them
wrong answers rather than refusals, because a left join's `DefaultIfEmpty()` yields `null` and
LINQ-to-Objects throws where SQL propagates.

An earlier attempt to defer the client-side reassembly and push operators back below it measured
**91 → 383** and was reverted.

**Decision.** Two transformations on the client, before the boundary analysis
([`transparent-identifiers.md`](transparent-identifiers.md)):

1. **Mirror EF's `TryFlattenGroupJoinSelectMany`** — `SelectMany` over `GroupJoin` becomes a
   single `Join`/`LeftJoin` with no identifier at all, including EF's own guard against
   correlated collection selectors.
2. **Re-carry surviving identifiers in a `ValueTuple`, with no client reassembly.** The
   identifier is plumbing no caller observes, so there is nothing to rebuild; member reads
   become slot reads and the chain stays server-side.

Two guards are binding: **no slot may hold a sequence**, and a rewrite is **kept only if
re-analysis shows it strictly increases what ships**.

**Rationale.** The flattening is EF's own, at `QueryableMethodNormalizingExpressionVisitor.cs:566`
— proven, and positioned where we cannot reach it (inside `CompileQuery`, which on the server
follows a deserialization that cannot happen). Mirroring it beats inventing one. The sequence
guard is the precise cause of the reverted attempt's failure: a grouping in a slot makes
`t.Item2.DefaultIfEmpty()` ask SQL to navigate out of a projected tuple into a correlated
collection. The verification guard is what makes a wrong rewrite cost nothing.

**Consequences.** This refines ADR-010 rather than reversing it: an anonymous type the *caller*
wrote is still a boundary. Only compiler-generated identifiers are re-carried. Server-ok remains
a type property and not a translatability property, so the guards reduce risk without removing
it — which is why each phase is measured separately and reverted if it does not pay.

## ADR-012 — A value-mapper seam for CLR types the wire cannot walk — LOCKED (2026-08-09)

**Context.** The wire's default handling of a non-primitive, non-entity value is a reflective
walk of its public readable members. That is right for an anonymous type, a record or a DTO, and
it is *destructive* for a type whose members are computed. Two instances are already in the
suite, and neither is hypothetical:

- **`NetTopologySuite.Geometries.Geometry`** exposes `Boundary` and `Envelope`, both of which
  return geometries. C9 mapped geometries as scalars, let one travel, and the walk recursed until
  **the stack overflowed and the test host aborted** — the one outcome this repo holds to be
  worse than any number of red tests.
- **`System.Net.IPAddress`**, whose `ScopeId` throws `SocketException` for an IPv4 address. B23
  diagnosed `Comparison_with_value_converted_subclass` in full and found no narrow route: sending
  such a scalar through EF's core `ValueConverterSelector` inside `PrimitiveCoercion.Coerce` fixes
  it and costs **381**, because `Coerce` is on every scalar path.

ICC v1 solved the first with an `IInfoCarrierValueMapper` chain the result mapper walked before
its own reflective handling, and kept NetTopologySuite out of its product assembly entirely by
registering the geometry mapper test-side (C12). v2 had no equivalent: `IDynamicValueMapper` is
the whole mapper, not a chain, so there was nowhere to register one.

**Decision.** A public `InfoCarrier.Core.ValueMapping.IInfoCarrierValueMapper` with two methods,
`TryMapToWire` and `TryMapFromWire`. Both return `bool` and **both may decline**. The chain is
resolved from DI — EF's internal service provider on the client, the server's own on the server —
and `DynamicValueMapper` consults it in two places only:

- forward, in `MapToNode`, **after** the primitive branch and **before** the collection and
  object-shape branches;
- reverse, in `Materialize`, **before** the scalar branch.

A claimed value travels as **one wire primitive** — a `string` or a `byte[]`, the forms
`ExpressionJsonContext` already registers — under a `TypeNode` naming its **original** CLR type.

**Rationale.** Declining is what makes this safe to add: with no mapper registered the two hooks
are loops that do not run, so nothing that does not opt in can change. Carrying the original type
on the node is what makes the reverse side able to find the mapper at all, and it keeps
[ADR-008](#adr-008-serializer-design-rlinqaqua-patterns-ef-metadata-driven-locked-2026-07-22-recorded-2026-08-01) constraint 2 intact — the type named is a mapped property type, which
`TypeAllowlist.ForModel` already admits, so no allowlist widening is implied and none was made.
**No wire-format change**: this adds no message, no node kind and no field.

The contract is stated in terms of the **CLR type alone**, deliberately. A mapper runs on both
halves, and the two halves' models are built by different providers — the standing hazard behind
B4's 106 failures. A seam that let either side consult a type mapping to decide would reintroduce
exactly that, so `declaredType` is what a mapper matches on and nothing else is offered.

**Consequences.** Spatial support stays *out* of the product assembly, as it was in v1: a WKT
geometry mapper is ~30 lines and belongs to whoever already depends on NetTopologySuite.
> **Amended 2026-08-11 (plan C89) — the provider now ships two mappers and registers them by
> default.** `IPAddressValueMapper` and `UriValueMapper` are in
> `InfoCarrier.Core.ValueMapping`, and `AddEntityFrameworkInfoCarrier` calls
> `AddInfoCarrierStandardValueMappers()`. A server builds its own service collection, so it must
> call that method itself — the method is public for exactly that reason, and a value mapped on
> one side only is worse than one mapped on neither.
>
> **Why these two and not the third.** Both are **BCL** types whose members throw for perfectly
> ordinary instances — `IPAddress.ScopeId` for an IPv4 address, `Uri.AbsolutePath` for a relative
> URI. An application that stores one has opted into nothing and should not have to discover this
> seam to make it work; leaving them out kept this ADR's sentence literally true while a real
> application failed, which is the wrong side of that trade. A **geometry** is still not shipped:
> it would put a NetTopologySuite dependency in this package for a type most callers never use,
> and v1 kept NTS out of its product assembly for the same reason. An application that wants one
> registers its own mapper beside the standard two — that is the documented route, and
> `InfoCarrierNetTopologySuiteValueMapper` in the test project is a worked example of it.
>
> `TryAddEnumerable`, so a repeated call does not put a mapper in the chain twice; the chain is
> consumed as an `IEnumerable<T>`, where a duplicate is visible.

Registration is the application's on **both** halves, and a value mapped on one side only will
fail asymmetrically — that is inherent, and it is why the interface documents it rather than
guessing a default. This is new public API and so is `ApiConsistencyTestBase`'s business; it is
also the general answer to "a CLR type the wire cannot walk", which is the shape B23 left open.

---

**AMENDMENT, 2026-08-17 (M9, plan J9). A value converter declared in the model is not a type
mapping.**

ADR-012's "CLR type alone" clause bars consulting a **store type mapping**, which the two providers
compute independently. B23 measured the cost of ignoring that at **381**: it sent scalars through
EF's core `ValueConverterSelector` inside `PrimitiveCoercion.Coerce`, which is on every scalar path.

A **value converter declared in the model** is not one. It is shared configuration, identical on
both sides by construction, and it is the same fact B12/C80 and J5 already require both halves to
agree about — J5's document seam exists precisely because *"where a key shape is decided by the
caller's own model configuration rather than by the store, the client has to reach the same answer
as the server"*.

A mapper may therefore be derived from a model-declared converter, **provided it is registered on
both halves and applies only where the reflective walk would otherwise fail.**

`ValueMapping.ModelConverterValueMapper` is that mapper. Two things keep it inside the amendment
rather than beyond it:

- **Symmetry is structural, not a registration rule.** It is built inside `DynamicValueMapper` from
  whichever model that mapper was given, so each half derives it from its own. Unlike an
  application-registered mapper it *cannot* be present on one side only.
- **It is last in the chain**, so an application that registers its own mapper for a type keeps
  first refusal, and the narrowing clause — model type not a wire primitive, provider type is one —
  keeps it off every path `PrimitiveCoercion` already short-circuits, which is where B23's breadth
  came from.

Measured on adoption: **15 → 14, 0 broken** across 22,672 tests.

## ADR-013 — The test project may reference `EFCore.Relational.Specification.Tests` — LOCKED (2026-08-11)

**Context.** ADR-004 adopts `EFCore.Specification.Tests`, the *core* spec suite, and the test
project referenced only that. Several bases this provider needs live one package along, in
`Microsoft.EntityFrameworkCore.Relational.Specification.Tests`, and the standing reading was that
"a non-relational provider has no business referencing" it. That reading priced two adoptions out
of reach: B3d/C10 put `AdHocJsonQuery` at *"626 + 322 lines of relational mirror and seven
abstract seeds only EF's relational classes implement"*, and there was no route at all to any
coverage of **writing** a JSON-mapped collection.

The reading confused two different things. This provider's **client** is not relational — but its
**Tier B backing store is** (ADR-009), and a spec base that describes JSON mapping is describing
what that store does. Mirroring such a base by hand is transcription with a chance of error, and
it is transcription of the very file the package already contains.

**Decision.** The **test** project references
`Microsoft.EntityFrameworkCore.Relational.Specification.Tests`. The **product** does not change:
`src/InfoCarrier.Core` referenced `Microsoft.EntityFrameworkCore.Relational` before this and
still references nothing else.

A relational spec base is adopted on the same terms as any other (ADR-004, ADR-009 Tier B) with
one extra test applied first: **does the base assume the *client* is relational?** Two outcomes,
both measured in C81:

- `ComplexCollectionJsonUpdateTestBase` — `UseTransaction` is `protected virtual`, so the
  provider supplies its own. Adopted, **18 of 18**.
- `JsonUpdateTestBase` — `UseTransaction` is `public void`, not virtual, and calls
  `facade.UseTransaction(transaction.GetDbTransaction())`. A derived class cannot replace it, and
  the client holds an `InfoCarrierTransaction` with no `DbTransaction` behind it. **142 of 142**
  fail on *"Relational-specific methods can only be used when the context is using a relational
  database provider"* before reaching anything the base is about. **Not adopted** — 142 identical
  harness failures are not information about this provider, which is the A70/A77 mistake in
  another costume.

**Rationale.** The package is reference material for a store this provider genuinely has, and the
alternative is hand-copying it. The non-virtual `UseTransaction` is a real limit and it is a
property of the *base*, not of this provider — recording which bases have it is cheaper than
rediscovering it.

**Consequences.** `AdHocJsonQuery`'s price must be re-derived before it is quoted again: B3d/C10
priced the *absence of this package*, and `AdHocJsonQueryRelationalTestBase` contains no
`ExecuteSqlRaw`, no `GetDbTransaction` and no `UseTransaction`. Also,
`InfoCarrierTestStoreFactory.CreateListLoggerFactory` now returns a `TestSqlLoggerFactory` — which
derives from `ListLoggerFactory` — because relational fixtures expose it through a non-virtual
cast. On a client with no database it records no SQL and nothing in this suite asserts any.

**Amended 2026-08-30.** The gate above reads as a binary — `protected virtual UseTransaction`
adopts, non-virtual does not — and three later adoptions (R11, R14, R19 in
[`plans/v10/implementation-plan.md`](plans/v10/implementation-plan.md)) measured cases where a
non-virtual `UseTransaction` did **not** put the base out of reach. The disqualifying condition is
narrower than "the base has a non-virtual `UseTransaction`": it is that **every route to the base's
coverage passes through a relational-only member with no `protected virtual` hook above it**.
Applied as a three-way test:

1. **A `protected virtual` method sits between the test bodies and the relational call** —
   `UseTransaction` itself, or a caller such as `ExecuteWithStrategyInTransactionAsync`. Adopt,
   overriding that method. `ComplexCollectionJsonUpdateTestBase` (above) and
   `NonSharedModelUpdatesTestBase` (R19): the latter's `UseTransaction` is non-virtual and calls
   `GetDbTransaction()`, but `ExecuteWithStrategyInTransactionAsync` is `protected virtual`, so
   overriding it hands `TestHelpers` a different enlistment and the non-virtual member is never
   reached. `Principal_and_dependent_roundtrips_with_cycle_breaking` passes because of it.
2. **The relational call is inline in some test bodies with no hook between it and the test.**
   Adopt anyway; those specific tests are unreachable and cost N reds, each recorded in
   `test/known-failures.txt`. `TableSplittingTestBase` (R11) costs two tests;
   `EntitySplittingTestBase` (R19) has `Can_roundtrip` green and only
   `ExecuteDelete_throws_for_entity_splitting` unreachable.
3. **Every test routes through the non-virtual member.** Not adopted. `JsonUpdateTestBase` (above)
   is this case at 142/142 — identical harness failures that say nothing about this provider.

R14 states the principle: *"it is not that a non-virtual `UseTransaction` disqualifies a base, but
that a base failing wholesale yields nothing. One use costs a test; 136 costs the base."* The
`JsonUpdateTestBase` decision (not adopted) and the `ComplexCollectionJsonUpdateTestBase` decision
(adopted, 18 of 18) are unchanged.

**Amended 2026-09-02 (R96) — a fixture may opt into a relational client test store.** The gate above
asks whether a base assumes the *client* is relational, and until now the only answer was no. A
second answer exists for one narrow case: several relational fixtures declare
`public new RelationalTestStore TestStore => (RelationalTestStore)base.TestStore;`, and any test
touching that property threw `InvalidCastException` — **25 of them, inside six bases already
adopted**.

`RelationalTestStore` splits in two. The **string half** — `ConnectionString`,
`NormalizeDelimitersInRawString`, and delimiters that already default to SQLite's — is harmless, and
it is the half those tests actually want. The **connection half** — `ConnectionState`,
`CloseConnection`, `BeginTransaction` — would reach the database directly, past the wire, and a
green from that says nothing about this provider. All three are non-virtual and all three read
`Connection`, which is `protected virtual`, so **overriding that one member governs every one of
them**.

So `RelationalInfoCarrierTestStore` exists beside `InfoCarrierTestStore`, identical in behaviour and
different only in shape, with `Connection` throwing. A fixture opts in with
`relationalClientStore: true`; everything else keeps the plain shell. Two classes rather than one
because `RelationalTestStore` derives from `TestStore`, and **per fixture rather than globally**
because a global answer would claim every fixture in the suite has a relational client, which is not
true — the opt-in list is the record of which bases needed it. Per fixture is also the finest grain
available: xUnit builds one fixture per test class.

**Measured both ways, and they agree.** Making the whole suite relational and opting in for the six
give byte-identical results: 25 `InvalidCastException` to zero, `failed` 198 and `total` 29183
unchanged, FIXED none, BROKEN none. `Connection` was never reached in either run, so nothing in this
suite wants a live connection and refusing one costs nothing. What the 25 become is 24 of R75's own
`FromSql` refusals — accurate where the cast was noise — and **one real defect the cast had been
hiding**, `TPHInheritanceQueryInfoCarrierTest.Casting_to_base_type_joining_with_query_type_works`.

**Written and parked in R77, landed in R96, and the reason for the delay is the whole point.** The
figures above are R77's own, measured on 2026-09-01 against a baseline of 198: the change bought
**no green test**, because everything behind the cast was `FromSql` and `FromSql` was refused. It was
parked on that basis - accurate information is worth having, but not on its own schedule.

**What changed is that `FromSql` is now supported behind an opt-in** (#60, R95). The cast is the
*first* blocker on 26 raw-SQL tests and the gate is the second, so neither half buys anything
without the other, and R96 lands them together. The fixtures that opt into a relational client store
are very nearly the fixtures that opt into raw SQL, which is not a coincidence: a base casts the
store to `RelationalTestStore` precisely so it can normalize the delimiters of a SQL string it is
about to run. **They are still two flags**, because they answer two different questions and a
fixture may want the first without the second.

**One base it must not be pointed at.** `AdHocQuerySplittingQueryTestBase` calls `CloseConnection()`
on the cast store, so it needs a live connection. What it tests — a connection dropping mid
split-query — has no meaning across this wire, and a green there would be manufactured.

**Amended 2026-09-03 (R136) — the two spec projects are one again, and the grant is unconditional.**
`test/InfoCarrier.Core.FunctionalTests` holds both tiers, `InMemory/` and `Sqlite/`. The R122
amendment below is superseded in its enforcement and unchanged in its substance: what it protected
was a compile-line separation that mattered while `InfoCarrier.Core.Relational` was a package, and
D3's supersession removed the package. Since R135 every client registers the relational half
unconditionally, so the thing the boundary prevented is now the ordinary case and was measured to
change no answer. **The reference this ADR grants therefore belongs to the one spec project.** The
tiers are still real and still about the backing store; they are a namespace rather than an
assembly. `RelationalInfoCarrierComplianceTest` narrows its two assembly-wide scans to the Tier B
namespace, because a compliance test written against one store's assembly has to be told which half
of a merged one it answers for. **R137 folded `test/InfoCarrier.Core.TestUtilities` in as well**,
for the same reason at one remove: with one spec project it had one consumer, and "the harness is
neither tier's property" stops being a statement about projects. It is `TestUtilities/` now.

**Amended 2026-09-02 (R122) — "the test project" is now one of three, and only one of them may.**
The spec suite split by backing store, as EF Core's own does:
`test/InfoCarrier.Core.FunctionalTests` is Tier A over InMemory,
`test/InfoCarrier.Core.Relational.FunctionalTests` is Tier B over SQLite, and
`test/InfoCarrier.Core.TestUtilities` is the store-neutral harness they share. **The reference this
ADR grants belongs to the Tier B project alone.** Tier A references neither
`EFCore.Relational.Specification.Tests` nor the `InfoCarrier.Core.Relational` package, and neither
does the shared harness, because a reference is transitive.

**The decision is unchanged and so is every adoption. What changed is enforcement.** The flags this
ADR describes — `relationalClientStore`, and the raw-SQL grant beside it — were per fixture, which
asks politely: nothing stopped a Tier A fixture from setting one, and a relational client over an
InMemory backend is the disagreement the seam exists to prevent (`architecture.md` §6a **D3**). A
project boundary does not ask. The flags remain, because they still answer a per-fixture question
inside Tier B; what they can no longer do is cross the tier.

**Two things it cost, both measured rather than argued.**

- **Seven Tier A fixtures implemented `ITestSqlLoggerFactory` and none of them needed to.** The
  interface exists in the relational specification assembly and was implemented solely to satisfy
  `RelationalComplianceTestBase`'s second assertion. Tier A is now checked by the plain
  `ComplianceTestBase`, which does not ask, and what the property returned was the *client's* log —
  on a client with no database, empty. Tier B's fixtures still implement it, and its assertion is
  green.
- **`SpatialQueryInfoCarrierTest` gave up `SpatialQueryRelationalTestBase` for the core base, and
  lost no test doing it.** That base is fourteen lines and **declares no tests**: its whole
  contribution is a `RelationalQueryAsserter` that calls `TestSqlLoggerFactory.OutputSql()` when an
  assertion fails, on a client that emits no SQL. It is listed in Tier B's `IgnoredTestBases` with
  that measurement, because adopting it there would need SpatiaLite for nothing it declares.

**The compliance gate is now two tests and both must be green.** `InfoCarrierComplianceTest` scans
the core specification assembly against Tier A; `RelationalInfoCarrierComplianceTest` overrides
`GetBaseTestClasses()` to scan the relational one against Tier B, so the two do not both claim the
core bases. Tier A's list of ignored bases names the **108 core bases adopted on Tier B**, generated
from the test's own output rather than written by hand, and an entry there is a claim that a
subclass exists in the sibling assembly. Missing: **0 on Tier A, 1 on Tier B** (`SqlQueryTestBase`),
which is the same 1 the single test reported before the split.
