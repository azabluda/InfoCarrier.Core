# Implementation Plan — M1: Query pipeline correctness + working signal

Status: **IN PROGRESS** · Milestone [M1](roadmap.md#m1--query-pipeline-correctness--working-signal)

**Scope of this doc:** the current milestone only. Rewritten when M1 closes; the completed
query-pipeline plan is archived at
[`archive/2026-07-query-pipeline-plan.md`](archive/2026-07-query-pipeline-plan.md).
Milestone-level scope belongs in [`roadmap.md`](roadmap.md), not here.

Each checkbox is one minimal, logically-complete substep, committed individually with the
checkbox ticked **in the same commit** (CLAUDE.md).

**Baseline entering M1 (2026-08-01):** 141 passed / 272 failed / 413 total.

---

## Phase G — Unmask the real failure profile

The two mechanical causes account for ~214 of 272 failures. Until they clear, the remaining
failure profile is unknowable — anything underneath is masked.

- [x] **G1.** `QueryParameterExpression` substitution (**~138 failures**). ✅ **141 → 207
      passing** (272 → 206 failures). Fewer than 138 gained because many of those tests then
      hit G2's serialization failure — the two causes are layered, not disjoint.
      `QueryExecutor.SubstituteParametersExpressionVisitor` overrides only `VisitParameter`,
      matching `__`-prefixed `ParameterExpression`. EF Core 10's `ExpressionTreeFuncletizer`
      emits `Microsoft.EntityFrameworkCore.Query.QueryParameterExpression` instead — an
      *extension* node (`NodeType => ExpressionType.Extension`) carrying `Name` and `Type`.
      Add a `VisitExtension` override resolving `qp.Name` against `QueryContext.Parameters`
      and returning `Expression.Constant(value, qp.Type)`.
      **Constraint:** plain constants only — never a wrapper struct (research-findings §6,
      the v1 `ValueWrapper<T>` trap).
      **Keep** the existing `VisitParameter` path; both node forms can appear.

- [x] **G2.** STJ primitive registration (**~76 failures**). ✅ **207 → 289 passing**
      (206 → 124 failures). Registration alone was insufficient: enum constants carry their
      concrete enum runtime type, which can never be pre-registered, so they are now
      normalized to their underlying integral value at capture and rebuilt from the
      `TypeNode` on the far side. Explicit registration chosen over a reflection fallback,
      per the constraint below.
      `ConstantNode.PrimitiveValue` is `object?`, so serializing it through
      `ExpressionJsonContext` needs a `JsonTypeInfo` for each concrete runtime type; the
      context registers the 19 node types and no primitives. Observed missing: `Int32`,
      `UInt32`, `Double`, `DateTime`.
      Register the primitive set explicitly (`bool`, `byte`, `sbyte`, `short`, `ushort`,
      `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`, `char`, `string`,
      `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, `byte[]`,
      plus nullable variants).
      **Prefer** explicit registration over chaining a reflection-based fallback resolver —
      a fallback would silently defeat the AOT/trimming goal (requirements §4.5, ADR-008
      constraint 8). If a fallback proves unavoidable, record why in an ADR.

- [ ] **G3.** Re-run, re-triage, record. Full suite; update the baseline table below with
      measured numbers and re-group the surviving failures by root cause. **Do not fix
      anything in this substep** — the point is an accurate picture.

- [ ] **G4.** Tail failures (**~32**, exact set known only after G3). Currently visible:
      `JsonElement` → `String` coercion (6); `Method may only be called on a Type for which
      Type.IsGenericParameter is true` (8); `Assert.Equal` value mismatches (10);
      `Assert.Throws` exception-type mismatches (2) — the last of these likely needs wire
      exception fidelity (W5, deferred to M5) and may be legitimately deferred with a note.

> Failures traced to the **projection split** (anonymous types / DTOs / `System.String`
> "not found in the server model", ~26) are **out of scope for M1** — that is M2. Leave them
> red; do not patch around them.

## Phase H — CI that can gate

- [ ] **H1.** Fix `.github/workflows/build.yml` solution reference: `InfoCarrier.Core.sln` →
      `InfoCarrier.Core.slnx` (restore and build both).

- [ ] **H2.** Split into two jobs (roadmap §CI strategy):
      **fast gate** — build + `ExpressionRoundTripTest` + `InMemorySmokeTest`, must be green,
      blocks the build;
      **spec ratchet** — full suite, non-blocking on absolute count.
      Replace the `~InMemory` / `~SqlServer` filters, which match no current test class.

- [ ] **H3.** Failure-count ratchet. Commit `test/known-failures.txt` holding the current
      baseline count; CI parses the run summary and **fails only when failures exceed the
      baseline**. Nothing skipped, nothing hidden, progress monotonic.
      Lower the baseline in the same commit as any fix that reduces it.

- [ ] **H4.** Drop the SQL Server service container from the per-commit workflow (ADR-009 —
      Tier C is nightly, from M7). Removes container startup from every run.

## Phase I — Compliance scoreboard

- [ ] **I1.** `InfoCarrierComplianceTest : ComplianceTestBase` — override `TargetAssembly`
      only. **Expect it to fail**, listing every unimplemented spec base. That failure *is*
      the deliverable: the authoritative inventory, generated rather than guessed.

- [ ] **I2.** Scope `GetBaseTestClasses()` to the core `Microsoft.EntityFrameworkCore.Specification.Tests`
      assembly. Relational spec bases assert SQL and are inapplicable to a **non-relational
      client provider** — InfoCarrier's client emits no SQL and has no migrations. Bounds the
      inventory from "160+ bases, unbounded" to the core set.

- [ ] **I3.** Seed `IgnoredTestBases` with the clearly-inapplicable bases, **each with a
      one-line comment giving the reason**. Every base ends up implemented or explicitly
      classified — nothing silently forgotten. Bases that are merely *not yet* built stay
      out of the ignore list so the test keeps reporting them.

- [ ] **I4.** Record the resulting inventory in `roadmap.md` M6 as the coverage backlog.

## Phase J — Doc integrity

- [x] **J1.** ADR-008 recorded (was cited by three docs, never written). ✅ `<this commit>`
- [x] **J2.** ADR-009 — SQLite in-memory as Tier B relational backend. ✅ `<this commit>`
- [ ] **J3.** Pin subrepo revisions in `research-infrastructure.md` — all four cells are
      `_(record tag/SHA)_`, so ADR-005's reproducibility guarantee is currently void.
      Capture SHAs from the existing clones **before** anything re-clones them.
- [ ] **J4.** Reconcile `ci-cd.md` with ADR-009 (Docker demoted to Tier C) and with the
      two-job ratchet strategy.

---

## Exit criteria

M1 closes when all of:

1. G1–G4 done; failure count measured and recorded (expect ≈ 350/413 passing).
2. CI fast gate green; ratchet active and failing on regression.
3. Compliance test landed, inventory published to roadmap M6.
4. J3/J4 done.

Then rewrite this doc for **M2 — projection split**, which starts with a design session, not
code (roadmap M2).

## Baseline log

| Date | Passed | Failed | Total | Note |
|---|---|---|---|---|
| 2026-08-01 | 141 | 272 | 413 | M1 entry baseline; 1 of 21 Northwind bases |
