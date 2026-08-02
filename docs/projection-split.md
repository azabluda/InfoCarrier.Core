# Projection split — design spec (milestone M2)

Status: **design approved 2026-08-01**, implementation not started.
Authority for requirements §3. Recorded as [ADR-010](decisions.md#adr-010).

Companion docs: [`result-wire-format.md`](result-wire-format.md) (how rows travel),
[`expression-serialization.md`](expression-serialization.md) (how trees travel),
[`research-findings.md`](research-findings.md) §8 (superseded in part — see §2.3).

---

## 1. The problem, restated precisely

Requirements §3: the server has the shared entity assembly and nothing else. It cannot
materialize an anonymous type, a client-only DTO, or a value tuple declared in the client
application, because those types do not exist in its process.

Until 2026-08-01 this was invisible. The in-process test transport shares an `AppDomain`, so
`TypeNodeResolver` found client types by assembly scan and the server happily materialized them.
Step L1 turned the ADR-008 type allowlist on; the illusion collapsed and the suite went
**32 → 1,421 failures of 4,247**. **1,197 are compiler-generated projection types, ~108 are
client-only DTOs.** That ~31% is the true size of this milestone.

The allowlist is therefore not only a security control — it is the *specification* of the
boundary. A type is server-materializable exactly when `TypeAllowlist.IsAllowed` says so. The
split must be defined in those terms, so the two can never disagree.

---

## 2. Where the boundary is computed

### 2.1 Decision: on the client, before serialization

The client analyzes the captured tree, ships only server-executable parts, and evaluates the
remainder locally against the materialized results.

### 2.2 Why not on the server

[`research-findings.md`](research-findings.md) §8 concluded the opposite — "the server receives
the full tree; the server detects the boundary". That was written before the allowlist existed,
and the allowlist makes it **impossible**, not merely inconvenient:

- Rejection happens inside `TypeNodeResolver.Resolve`, during *deserialization*. A tree naming
  an anonymous type throws before the server has an expression to analyze. For the server to
  detect the boundary it would first have to deserialize past types it is required to refuse.
- Tolerating unresolvable type names — deferring them to opaque placeholders — is precisely the
  default-deny violation ADR-008 constraint 2 forbids. It would reintroduce the RCE surface L1
  closed.
- The client already holds the tree as a *live* expression, with real `ConstructorInfo`s and
  `MemberInfo`s for its own types. It can evaluate the residual by compiling it. The server
  could only describe the residual back to the client, which needs a round-trip and a second
  wire vocabulary for something the client already has.

The conclusion of §8 that survives is the important one: **no tree surgery on the server, and no
new wire vocabulary.** The wire format does not change in this milestone.

### 2.3 Consequence for §8

`research-findings.md` §8 is amended with a dated correction. Its *mechanism* (execute the
entity-typed portion, apply the projection locally) stands; its *placement* (server-side
detection) is reversed.

---

## 3. The analysis

### 3.1 `ServerOk` — a bottom-up predicate

For each node of the captured tree (after `QueryParameterExpression` substitution, which already
runs in `QueryExecutor`'s constructor):

```
ServerOk(node) ⇔ every type the serializer would emit for `node` is allowed
              ∧ ServerOk(child) for every child
```

"Every type the serializer would emit" is enumerated from the same sources
`ExpressionToNodeTranslator` writes `TypeNode`s from — `node.Type`, `Method.DeclaringType` and
its generic arguments, `Member.DeclaringType`, `NewExpression.Constructor`'s declaring type and
parameter types, `MemberInit` binding members, lambda parameter and return types, a constant's
*runtime* type, array element types, `TypeBinary.TypeOperand`.

**Drift is the risk here**, not complexity: if the collector and the translator disagree about
which types get written, the client will ship something the server refuses. Guard: after the
split is chosen, serialize the server query and check every `TypeNode` in the produced graph
against the allowlist. On failure, move the boundary one operator inward and retry (bounded).
This makes the analysis correct by construction even when the collector is incomplete, and a
test asserts the retry never fires on the spec suite.

### 3.2 Projection lambdas are rewritten, not cut

The naive reading of §8 — "cut the chain at the last `Select` whose element type is known" — is
wrong for the most common shapes in the suite:

```csharp
ctx.Customers.Select(c => new { c.City, Count = c.Orders.Count() })
```

A cut ships `Customer` entities and evaluates `c.Orders.Count()` on the client. `Orders` was
never loaded, so the answer is **0** — silently, with no error. Worse:

```csharp
ctx.Customers.Select(c => new { c.City, Any = ctx.Orders.Any(o => o.CustomerID == c.CustomerID) })
```

A cut cannot evaluate the correlated subquery client-side at all.

Both are solved by the same rewrite. Within a projection lambda whose body constructs a
client-only type, find the **maximal `ServerOk` subexpressions** of the body — its *fragments* —
and split the lambda in two:

| | expression |
|---|---|
| original | `c => new { c.City, Count = c.Orders.Count() }` |
| shipped | `c => new ValueTuple<string, int>(c.City, c.Orders.Count())` |
| residual | `t => new { City = t.Item1, Count = t.Item2 }` |

The fragments are translated by EF against the real store, where they belong. Navigations,
correlated subqueries and aggregates all work because the server evaluates them; the client only
reassembles values it was handed.

`ValueTuple<…>` is the carrier: it is already on the allowlist, EF translates its construction,
and arities above 7 nest in the usual way. A fragment whose value is an entity (`new { c, … }`)
occupies a slot as an entity and materializes with full identity, exactly as today.

**This is also wire-protocol W1.** The server returns only the values the projection needs; the
minimal-column payload of requirements §3.3 is not a later optimization but the same mechanism.

### 3.3 Which operators are rewritten

Rewriting applies to operators whose lambda *becomes* the element:

`Select` · `SelectMany` (result selector) · `Join` / `GroupJoin` (result selector) · `Zip` ·
`GroupBy` (element and result selectors)

For any other operator whose lambda mentions client-only types — a `Where` over a client type, an
`OrderBy` on a client-typed key — the operator's output type is its input type, so a tuple
rewrite would change the element type. These fall back to §3.5.

The rewrite is **recursive**: a nested projection inside a fragment
(`Select(c => new { Orders = c.Orders.Select(o => new { o.OrderID }) })`) is rewritten by the
same rule, and reassembled by the same rule on the client.

### 3.4 The residual chain

Everything downstream of the first rewritten projection runs on the client: its element type is a
client type the server cannot name. The residual is the original tree with each shipped subtree
replaced by `Expression.Constant(materialized.AsQueryable())` and each rewritten lambda replaced
by its reassembly form.

Execution is LINQ-to-Objects: `EnumerableQuery<T>`'s provider rewrites `Queryable.*` calls to
`Enumerable.*` itself, so no manual rewriting is needed. Two pre-conditions:

- **Marker calls must be stripped.** `AsNoTracking`, `AsTracking`, `Include`, `ThenInclude`,
  `AsSplitQuery` have no `Enumerable` counterpart and would fail the rewriter. Tracking markers
  are already consumed by `TrackingBehaviorFinder`; `Include` is consumed by the shipped query.
- **`EF.Property` cannot be evaluated locally.** A residual containing it is rejected with a
  clear message (§6).

**Deferred: operator pushdown.** After the rewrite the server holds `IQueryable<ValueTuple<…>>`,
so a downstream `OrderBy(x => x.City).Take(5)` *could* run server-side as
`OrderBy(t => t.Item1).Take(5)`. It does not, in M2: the client applies it after receiving every
row. Correct, potentially expensive, and tracked as a performance item (§7).

### 3.5 Frontier and fallback

Where a rewrite does not apply, the split is a plain cut. Take the **frontier**: the maximal
`ServerOk` subtrees. A frontier subtree is shipped when it contains an entity query root and has
no parameters bound outside it; otherwise it stays in the residual as ordinary local code.

A query may therefore produce **more than one shipped query** — a `Join` whose result selector
builds an anonymous type has two independent sources, each shipped and each materialized before
the residual joins them locally. `NorthwindJoinQueryTestBase` depends on this.

### 3.6 The escaped-entity hazard

One shape is not covered by §3.2, because the read happens after the projection:

```csharp
ctx.Customers.Select(c => new { c, c.City }).Where(x => x.c.Orders.Any())
```

`c` is shipped as an entity; `.Orders` is read downstream, on the client, where it is empty.

Detection is syntactic and conservative: scan every residual operator for member access naming a
navigation of a server-known entity type. For each such path, add the corresponding `Include` to
the shipped query that supplies that entity. Over-fetching is accepted; a wrong answer is not.

---

## 4. Tracking semantics

Over-`Include`ing (§3.6) would over-track, and a projection changes what EF tracks anyway: a
query returning `new { c.City, Count }` tracks nothing, even though it read entities.

Rule, matching EF: **boundary rows materialize with identity resolution but without tracking;
after the residual is evaluated, the entities present in its result are attached** according to
the query's `QueryTrackingBehavior`.

Identity resolution must therefore be independent of tracking in `ClientResultMaterializer` —
it already is, for `NoTrackingWithIdentityResolution`. The degenerate case (no boundary, residual
is the identity) reduces to today's behavior, so the existing fast path may be kept if unifying
proves invasive.

---

## 5. Component shape

New, in `src/InfoCarrier.Core/Query/`:

| Type | Responsibility |
|---|---|
| `WireTypeCollector` | Types a node would put on the wire (§3.1). Shared by analysis and verification. |
| `ServerBoundaryAnalyzer` | Bottom-up `ServerOk`; frontier; free-parameter check. |
| `ProjectionRewriter` | Fragment extraction, tuple carrier synthesis, reassembly lambda. |
| `QuerySplitter` | Orchestrates the above; returns a `SplitQuery`. |
| `SplitQuery` | `IReadOnlyList<Expression> ServerQueries` + residual `Expression` + slot bindings. |

Changed:

- `QueryExecutor<TElement>` — splits in the constructor; ships each server query; materializes
  each as its *boundary* element type (not `TElement`); executes the residual to produce
  `TElement`. Note `ReturnsSingleResult` must be recomputed **for the shipped query**, not the
  original: `…Select(c => new {…}).First()` ships a sequence and the residual takes the first.
- `InfoCarrierDatabase` — `QueryReturnsSingleResult` moves behind the splitter for the same
  reason.
- `ClientResultMaterializer` — materialize by runtime element type; no tracking at
  materialization time (§4).

The server is **unchanged**. That is the test of the design: if `ServerQueryExecutor` needs
edits, the boundary was drawn in the wrong place.

---

## 6. Diagnostics

Every rejection names the cause and the fix, in the style `TypeNodeResolver.BuildRejection`
already uses. No shape may fail *silently*:

- residual contains `EF.Property` → "cannot be evaluated on the client; move it into the
  server-side projection".
- a fragment has free parameters and no enclosing rewrite → name the subexpression.
- verification retry (§3.1) exhausted → name the type that could not be shipped.

---

## 6a. The transparent-identifier ceiling (diagnosed 2026-08-02)

The dominant remaining failure family on **both** tiers, and the thing worth understanding
before anyone tries the pushdown again.

`from o in os join c in cs on … into g from c in g.DefaultIfEmpty() select …` compiles to a
`GroupJoin` whose result selector builds a **transparent identifier** — `new { o, g }`. EF
handles those internally and normalises the whole shape into a LEFT JOIN. This provider must
treat the anonymous type as a boundary, so the `SelectMany` above it lands on the client, where
`DefaultIfEmpty`'s `null` is not SQL-propagated and the projection throws
`NullReferenceException` instead of yielding null.

**Why the obvious fix fails.** Deferring the reassembly — pushing the operator back below the
projection and rewriting `ti.o` into a tuple-slot read — was implemented and measured at
**91 → 383**. The reason is specific and worth writing down: a transparent identifier from a
`GroupJoin` holds a **grouping**, and a grouping is not a projectable value. Once the server
projects to `ValueTuple<Order, IEnumerable<Customer>>`, `t.Item2.DefaultIfEmpty()` asks SQL to
navigate out of a projected tuple back into a correlated collection, which no provider can
translate. 67 `SelectMany` and 40 `Join` translation failures, all of that shape.

**What would need to be true.** Either

- the carrier never holds a sequence — which fixes the translation failures but excludes exactly
  the `GroupJoin` cases that motivate the work; or
- the transparent identifier is *eliminated* rather than carried, so the server sees the
  `GroupJoin`/`SelectMany`/`DefaultIfEmpty` idiom in the shape
  `QueryableMethodNormalizingExpressionVisitor` recognises and converts to a LEFT JOIN itself.

The second is the real answer and is a design session, not a patch: it means reproducing enough
of EF's transparent-identifier handling to hand the server a tree its own normaliser accepts.

## 7. Non-goals

| Item | Why deferred | Where |
|---|---|---|
| Operator pushdown past the boundary (`Take`/`OrderBy` on tuples) | Correctness first; needs the residual→tuple slot map to be invertible | performance backlog |
| Streaming the residual | Residual evaluation buffers; `IAsyncEnumerable` results are M8/W4 | M8 |
| Compiled split cache | Depends on ADR-008 constraint 6 canonical form | M8 |
| `EF.Property` in a residual | Requires shipping shadow state per row | M6 |

---

## 8. Phases

Each phase is a commit, and each must leave the suite **no worse** than the previous one.

- **M2-A — analysis + cut.** `WireTypeCollector`, `ServerBoundaryAnalyzer`, `QuerySplitter`
  producing plain cuts, residual execution, §3.6 `Include` augmentation, §4 tracking. No tuple
  rewrite. Covers projections that read only scalar members and navigations reachable by
  `Include`.
- **M2-B — projection rewrite.** §3.2 fragments and tuple carrier at the top level. Removes the
  `Include` over-fetch for the shapes it covers and closes correlated subqueries. This is W1.
- **M2-C — recursion and multi-source.** Nested projections, `Join`/`GroupJoin`/`SelectMany`
  result selectors, `GroupBy` selectors, multiple shipped queries (§3.5).
- **M2-D — adopt `NorthwindSelectQueryTestBase`** (not currently adopted) and drive
  `NorthwindSelectQueryInfoCarrierTest` + `NorthwindJoinQueryInfoCarrierTest` green.
- **M2-E — re-check the ~84 store-limitation overrides** that now trip the type boundary before
  reaching the translation failure they assert. Each either returns to asserting its original
  failure or is deleted.

## 9. Exit criteria (from [`roadmap.md`](roadmap.md) M2)

- Boundary detection in the client; client applies the residual projection. *(relocated from the
  server per §2 — roadmap updated)*
- Minimal-column payload (W1) — satisfied by §3.2.
- `NorthwindSelectQueryTestBase` and `NorthwindJoinQueryTestBase` adopted and passing.
- Suite failures back below the pre-L1 baseline of 32, with the ~1,305 M2 failures cleared.

## 10. Test plan

- **Unit** — `ServerBoundaryAnalyzer` over hand-built trees: boundary placement for each shape in
  §3.2–§3.6, including the negative cases of §6.
- **Round-trip** — for every split the spec suite produces, serializing the shipped query must
  clear the server allowlist (§3.1 verification, asserted rather than merely relied on).
- **Behavioral** — the inherited `EFCore.Specification.Tests` bases are the coverage goal
  (ADR-004). No new behavioral tests are written for shapes a base already covers.
- **Regression guard** — a test asserting that `ctx.Customers.Select(c => new { c.City, Count =
  c.Orders.Count() })` returns non-zero counts. This is the silent-wrongness case of §3.2 and the
  one failure mode that would not announce itself.
