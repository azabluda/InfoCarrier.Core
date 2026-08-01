# Design Decisions (ADR log)

Record of design decisions for InfoCarrier.Core v2. Each entry states context, decision,
and rationale.

> **Status note.** The pre-implementation research phase closed 2026-07-22
> ([`research-findings.md`](research-findings.md)); the project is now in **implementation**
> (see [`roadmap.md`](roadmap.md)). Each entry is marked:
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
> [`implementation-plan.md`](implementation-plan.md) both cite it — but the entry was never
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

The *method* allowlist half of constraint 2 remains open: `ResolveMethod` binds any method on an
allowed declaring type. Narrower than before (the declaring type must now be allowed) but not
the "Queryable / Enumerable / `EF.Functions` / model-bound members" restriction this ADR calls
for. Still required before a network transport ships (roadmap M5).

> **What enabling it revealed.** Failures went 32 → 1,421 of 4,247. **1,197 are anonymous or
> other compiler-generated projection types, and ~108 more are client-only DTOs** — together
> ~31% of the suite, all of it the projection split (requirements §3, milestone M2). Those tests
> were passing only because the in-process transport shares an `AppDomain`, so an assembly scan
> found types no network server could ever have. The estimate before this landed was ~16 tests.
> This is the same class of self-deception recorded against G4e, at roughly eighty times the
> scale.

Constraint 6 (canonical form) is not yet exercised — no compiled-query cache exists.

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
