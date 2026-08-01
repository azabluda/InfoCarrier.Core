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

- [x] **G3.** Re-run, re-triage, record. ✅ Measured after G1/G2/G4a — **313 passed / 100
      failed / 413**.

      | Count | Root cause | Owner |
      |---|---|---|
      | **64** | `Entity type 'X' not found in the server model` — anonymous types, DTOs, `String`, `List<T>`, `T[]`, `IEnumerable<T>` | **M2 projection split** |
      | 16 | `Assert.Equal` values differ | G4b |
      | 8 | `Method may only be called on a Type for which Type.IsGenericParameter is true` | G4b |
      | 4 | `The LINQ expression 'DbSet<Order>()' could not be translated` | G4b |
      | 2 | `'c' could not be translated` | G4b |
      | 2 | `'NoNameParameter' could not be translated` | G4b |
      | 2 | `operands for operator 'Convert' do not match … op_Explicit` | G4b |
      | 2 | `Expression of type 'Customer' cannot be used for return type` | G4b |

      **Key finding: the projection split is far larger than the entry triage showed** — 64 of
      the 100 remaining failures, not the ~26 estimated on 2026-08-01. Clearing the mechanical
      causes revealed tests that were previously failing earlier in the pipeline. M2 is
      correspondingly more valuable and should follow directly after M1.

- [x] **G4a.** Primitives in the dynamic-value graph. ✅ **289 → 313 passing**
      (124 → 100 failures). `DynamicValueMapper.MapToNode` had no primitive branch — only
      entity → collection → object-shape — so a primitive appearing where a dynamic value is
      required (typically a collection element, e.g. `List<string>` in a `Contains` closure)
      fell through to object-shape. `string` mapped its `Length` property and then threw
      `MissingMethodException` on rehydration; **`int` mapped an empty property set and
      rehydrated silently as `0`** — wrong results with no exception, which is why part of the
      `Assert.Equal` group was really this bug. Added `DynamicValueNode.PrimitiveValue` (its
      doc already said "Empty for collection/**scalar** shapes" — the slot was anticipated but
      never added) plus matching branches on both sides.
      Extracted `PrimitiveCoercion` from `NodeToExpressionTranslator`: the return path also
      needed it, since `ReadValue` returned `JsonElement` raw (the
      `JsonElement cannot be converted to String` group).

- [x] **G4b.** `System.Type` constants carried as `TypeNode`. ✅ **313 → 321 passing.**
      `typeof(X)` reached the object-shape branch, which reflectively reads every public
      property; `Type.DeclaringMethod` throws unless `IsGenericParameter`. All four
      `GetType_on_non_hierarchy` tests.

- [x] **G4c.** InMemory store limitations no-opped. ✅ **321 → 341 passing.** Ten tests
      (structural anonymous/tuple equality, `ElementAt` over a custom projection) that EF
      Core's own `NorthwindWhereQueryInMemoryTest` no-ops identically. Store limitation, not
      an InfoCarrier gap — see the class doc for the rule.

- [x] **G4d.** Parameter identity across the wire (requirements §2.3). ✅ **341 → 345
      passing.** `ParameterNode` carried only `Name`, and both translators keyed their
      parameter maps by it. Unnamed parameters all collapsed onto key `""`, and same-named
      parameters in unrelated lambdas aliased each other — producing a tree whose body
      referenced a parameter its lambda never declared (`'NoNameParameter'` / `'c'` could not
      be translated). Added `ParameterNode.Id`, assigned from the reference identity of the
      source `ParameterExpression` and reset per message; both sides now key on it.

- [ ] **G4e.** Last tail (**4 tests**): `Decimal_cast_to_double_works` — `The operands for
      operator 'Convert' do not match the parameters of method 'op_Explicit'` (the `UnaryNode`
      operator-method round-trip picks a mismatched `op_Explicit` overload); and one pair
      failing `Expression of type 'Customer' cannot be used for return type`.
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
| 2026-08-01 | 207 | 206 | 413 | after G1 (`QueryParameterExpression`) |
| 2026-08-01 | 289 | 124 | 413 | after G2 (STJ primitives + enum normalization) |
| 2026-08-01 | 313 | 100 | 413 | after G4a (primitives in dynamic-value graph) |
| 2026-08-01 | 321 | 92 | 413 | after G4b (`Type` constants) |
| 2026-08-01 | 341 | 72 | 413 | after G4c (InMemory store limitations no-opped) |
| 2026-08-01 | 345 | 68 | 413 | after G4d (parameter identity) |

Of the 68 remaining, **64 are M2 (projection split)** and 4 are the G4e tail. M1 lands at
≈349/413 once G4e clears — the rest is M2 by construction.
