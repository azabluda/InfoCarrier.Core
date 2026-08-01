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

- [x] **G4e.** Server query provider resolved from the query **root**, and single-result
      queries executed directly. ✅ **345 → 397 passing** (68 → 16 failures).

      Two defects in `ServerQueryExecutor`:
      1. `BuildQueryable` wrapped *every* tree in `EntityQueryable<T>`, but a single-result
         query (`Single`/`First`/`Count`) has the result as its expression type, not a
         sequence — invalid, since `EntityQueryable<T>` requires an `IQueryable<T>`-typed
         expression. Now routed through `IQueryProvider.Execute`.
      2. The provider was resolved from the query's **result** type, so any projection threw
         `Entity type '…' not found in the server model` *before EF ever saw the query*. The
         provider now comes from the query root (always a real entity) via `QueryRootFinder`.

      > ⚠️ **This did not solve the projection split (M2), and it hid it.** The 52 tests it
      > cleared pass because the in-process harness runs client and server in one AppDomain,
      > so the server can see anonymous types and client-only DTOs and materializes them
      > directly. Over a real transport the server would not have those types.
      > **Requirements §3 is untouched**, and the harness can no longer detect it. See
      > roadmap M2, re-scoped.
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

## Failure classification (the standing taxonomy)

Every failing spec test belongs to exactly one category. Only the first two ever earn an
override, and only with the reason stated at the override site.

| # | Category | Handling | Permanent? |
|---|---|---|---|
| **1** | **Conceptually inapplicable to InfoCarrier** — cannot hold for *any* remoting provider. | Override with reason, or `IgnoredTestBases` | Permanent |
| **2** | **Backing-store limitation** — the store can't do it; a *local* provider on the same store fails identically, and another store passes. Verify against EF's own test class for **both** providers before claiming this. | Override with reason, on the store-specific class only | Until Tier B/C |
| **3** | **Upstream EF Core limitation** — EF itself cannot translate it on any provider, usually with a tracking issue. Handling differs by store: InMemory returns nothing, relational throws, so the override differs per backend (`Task.CompletedTask` vs `AssertTranslationFailed`). | Override with reason + upstream issue number | Until EF fixes it |
| **4** | **Not yet implemented** — a real InfoCarrier gap with a roadmap home. | **Stays red**, tracked here | No |
| **5** | **Bug** — should already work. | Fix | No |

**Measured 2026-08-01 (2,600 tests): no category-1 failure has been found.** Two candidates
were considered and both fell through:

- *The client/server type boundary.* Requirements §3.2 resolves it by splitting the query,
  not by declaring it impossible.
- *SQL assertions.* Initially recorded (wrongly) as inapplicable because the **client**
  provider is non-relational. But the **server** is relational and the harness owns its
  service provider, so registering `TestSqlLoggerFactory` in
  `InfoCarrierBackendTestStore.AddServices` and exposing it from the fixture makes the
  inherited `AssertSql` assert on **server-generated** SQL. `AssertSql` is a fixture-level
  hook, so the test bodies need almost no rewriting. For InfoCarrier this is a *stronger*
  assertion than for a local provider: it proves a round-tripped tree yields the same SQL a
  local query would. **This unblocks the relational spec bases** — see M6.

**Correction (2026-08-01).** The 10 tests no-opped in G4c were labelled category 2. Only 2 of
them (`ElementAt_over_custom_projection*`) actually are: EF's `NorthwindWhereQuerySqliteTest`
does not override those, so they pass on a relational store. The other **8 are category 3** —
EF's SQLite class overrides the identical 8 with `AssertTranslationFailed`, citing
**EF Core issue #14672** (anonymous-type-to-constant comparison). SQLite will *not* fix them;
on Tier B their override must change shape rather than disappear.

## Phase I — Compliance scoreboard

- [x] **I1.** `InfoCarrierComplianceTest : ComplianceTestBase`. ✅ Red by design — reports
      **151 spec bases with no InfoCarrier subclass**. The inventory is now generated, not
      guessed.

- [x] **I1b.** Adopted the **20 remaining `Northwind*QueryTestBase` classes**, fixture generics
      mirroring EF's own `NorthwindQuery*InMemoryTest`. **413 → 2,600 tests; 1,073 passing.**
      Deliberately no overrides — every failure is information.

      **Triage of all 1,527 failures:**

      | Count | Cause | Category |
      |---|---|---|
      | **629** | `JsonException: A possible object cycle was detected` | **4** — result wire format |
      | **214** | `JsonException: The JSON value could not be converted to List<…>` | **4** — same root |
      | **260** | `ArgumentException: The type or method has N generic parameter(s), but N generic argument(s) were provided` | **4** — `MethodNode` generic-arity resolution |
      | 292 | `Assert.Equal` values differ | mixed, needs sub-triage |
      | 38 | `Assert.Throws` exception type mismatch | **3** — wire exception fidelity (W5, M5) |
      | 32 | `The LINQ expression 'DbSet<X>()' could not be translated` | needs investigation |
      | 8 | `NotImplementedException` | **3** — compiled queries / SaveChanges |
      | ~54 | long tail | mixed |

      **Two causes account for ~72% of all failures.**
      - **843 (55%)** are the result wire format. `ServerQueryExecutor.SerializeResult` still
        does `JsonSerializer.SerializeToUtf8Bytes(list, list.GetType())`; entity graphs with
        circular navigations (`Customer→Orders→Customer`) throw on the way out, and what does
        serialize will not deserialize back into `List<T>`. Wire-protocol §2.1 already
        specifies the fix — **identity-keyed rows with per-message reference preservation**,
        not raw object JSON. This is the single highest-value fix in the project.
      - **260 (17%)** are one generic-arity bug in method resolution.

- [ ] ~~**I2.** Scope `GetBaseTestClasses()` to the core assembly; relational bases are
      inapplicable to a non-relational client provider.~~ **Withdrawn 2026-08-01 — the premise
      was wrong.** The client emits no SQL, but the *server* is relational and the harness owns
      its service provider, so `AssertSql` can assert server-generated SQL (see the taxonomy
      above). Relational bases are adoptable, so the inventory is **not** scoped down. Replaced
      by I2′.

- [ ] **I2′.** Expose the server's `TestSqlLoggerFactory` through the fixture: register it in
      `InfoCarrierBackendTestStore.AddServices`, implement `ITestSqlLoggerFactory` on a
      relational InfoCarrier fixture, and have `ListLoggerFactory` resolve from the **server**
      provider. Unblocks `EFCore.Relational.Specification.Tests`. Requires Tier B (ADR-009), so
      it lands with M3; SQL baselines are per-backend, so those classes are backend-specific.

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

Totals jump below because Phase I adopted the 20 remaining `Northwind*QueryTestBase`
subclasses; the suite is a different, much larger population from here on.

| Date | Passed | Failed | Total | Note |
|---|---|---|---|---|
| 2026-08-01 | 3635 | 603 | 4247 | after result wire format + per-message reference scope |
| 2026-08-01 | 3692 | 546 | 4247 | after G5 (entity-in-projection routing) |
| 2026-08-01 | 3766 | 472 | 4247 | after G6a (server loaded-probe: no-tracking Includes) |
| 2026-08-01 | 3844 | 394 | 4247 | after G6b (per-query tracking behavior, untracked path) |
| 2026-08-01 | 3856 | 382 | 4247 | after G6c (loaded-probe key check, EF issue #23851) |
| 2026-08-01 | 3934 | 304 | 4247 | after G7 (null result rows are data, not absent rows) |
| 2026-08-01 | 3968 | 270 | 4247 | after G8 (server-side defining queries for keyless types) |
| 2026-08-01 | 3996 | 242 | 4247 | after G9 (declared collection types rebuilt, not widened to List) |
| 2026-08-01 | 4088 | 150 | 4247 | after G10 (mirror EF's own InMemory store-limitation overrides) |
| 2026-08-01 | 4134 | 104 | 4247 | after G11 (unwrap TargetInvocationException at the server boundary) |
| 2026-08-01 | 4166 | 72 | 4247 | after G12 (entity references carry their key to the server) |
| 2026-08-01 | 4171 | 67 | 4247 | after G13 (value-type ctors; a single result that is itself a sequence) |
| 2026-08-01 | 4193 | 45 | 4247 | after G14 (entities built by a projection are not identity-resolved) |
