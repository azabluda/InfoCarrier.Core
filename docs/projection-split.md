# Projection split — design spec (milestone M2)

Status: **design approved 2026-08-01**, implementation not started.
Authority for requirements §3. Recorded as [ADR-010](decisions.md#adr-010-projection-split-boundary-computed-on-the-client-locked-2026-08-01).

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

**One projection escaped that sentence until 2026-09-22**, and it is the one that needs no value at
all. A body reading nothing from the row yields no fragment, so the rewrite gave up and the plain
cut shipped the maximal `ServerOk` subtree, which is the query root: `Select(b => new { F = flag })`
read every column the entity has, where EF's own client writes `SELECT 1`. The carrier now holds a
single constant in that case and the reassembly reads none of it, so the sentence above is true of
a projection that needs no value too. Both answers were always right, which is why the suite could
not see it and only a comparison with EF's statement did.

**Amendment 2026-09-26 — a constant is a value too, when EF would translate the projection
whole.** EF binds a projection made only of constructions whose every value translates in one mode,
and puts each value in the statement, a constant or a captured value included:
`Select(c => new { c.CustomerID, ConstantTrue = true })` is `SELECT "c"."CustomerID", 1`, and
`Select(c => new { Ten = 10 })` is `SELECT 10`, not the stand-in `1`. A projection with client code
or a conditional in it is bound in EF's other mode, which keeps constants on the client, and so does
this rewrite. `ProjectionRewriter.TranslatableLeaves` makes the difference; #167's slow run found
it in nine methods. The stand-in `1` remains for a body with no value at all, `new OrderDto()`,
where EF projects nothing and a tuple has to hold something.

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

### 3.3a A composite join KEY is rewritten too (2026-09-16)

§3.3 is about the lambda that becomes the element. A join's **key** selector is not that lambda, and
it was left alone: a key written `new { a.X, a.Y }` has a type the caller's compiler generated, so
`ServerOk` was false for the `Join` node and §3.5 cut below it. The server then ran the two roots and
the client joined them. **The answer was right and both tables crossed the wire**, which no test
could see until the server's SQL was compared with EF's.

`JoinKeyRewriter` rewrites such a key to `Tuple<...>` before the analysis, and the join ships.
Three measurements decided the shape:

- EF does **not** translate a `ValueTuple` key — `Translation of method 'System.ValueTuple.Create'
  failed`, and a `new ValueTuple<…>(…)` tree fails the same way, with or without member bindings.
- EF **does** translate a `NewExpression` that carries its members over a named class.
  `Tuple<...>` is one, is already on the allowlist, and produces the identical SQL, including the
  null matching a C# anonymous key gets: `ON (a = b OR (a IS NULL AND b IS NULL)) AND ...`.
- The wire already carries `NewExpression.Members` (`NewNode.Members`), so nothing about the payload
  had to change.

**What is not rewritten**: a key of a type the caller declared, which has its own `Equals` and is not
data, and a key of more than seven members, which `Tuple` cannot hold without nesting. Both keep the
old behaviour rather than gaining a new one.

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

**Amendment 2026-09-22 — three shapes push down now, and performance was not the reason.** The
paragraph above still states the rule for an arbitrary downstream operator, and read alone it is
now too strong. What moved it was correctness rather than cost: an operator that stays on the
client makes this provider *answer* a query the server's EF would refuse, and the standing decision
for this family is to behave as plain EF does on the same server. Each of these three therefore
reaches the server, which gives EF's own answer:

- **A projection of a captured value.** Parameter substitution opens an anonymous object into the
  construction that built it, so `Select(c => new { f = flag }).OrderBy(e => (bool?)e.f)` reaches
  the server and is refused there. The message names `@p.Item1` where EF names `@p.f`.
- **`Distinct` over a rebuild.** `ProjectionRewriter.TryMoveDistinctBelowReassembly` moves it below
  a rebuild that constructs its result from the slots, each read once, onto the server's tuple.
  Until 2026-09-25 this said "a rebuild that copies each slot into one member", which only an
  anonymous type passed, so `Select(o => new OrderCountDTO(o.CustomerID)).Distinct()` kept its
  `Distinct` on the client and the server sent every duplicate row. A constructor, one with an
  initializer, a construction nested in another and one that reads no column now pass too, because
  EF removes duplicates by the columns a construction reads and never calls the type's `Equals`.
  Found by the second Tier B run with InfoCarrier removed: five `NorthwindMiscellaneous.Select_DTO_*`
  methods, two of which read the whole `Orders` table for a projected collection.
- **A `Select` over a rebuild.** `ProjectionRewriter.TryFuseSelectWithReassembly` fuses the two, so
  a projection over a client-typed projection reaches the server as EF writes it.

A fourth change of the same family is not pushdown and is recorded here beside them: an inner
projection's values are collected against the parameters of every *enclosing* lambda, so a
projected collection that reads its owner computes that read on the server.

**Amendment 2026-09-24 — paging under a terminal operator pushes down too.** `Skip` and `Take` at
the root already reached the server, because the re-carry puts the rebuild at the root. Under
`First`, `Single` and their siblings the rebuild stays below them, and
`Select(c => new { … }).Skip(1).First()` ran `Skip` on the client over every row the server sent.
`QuerySplitter.WithRowLimitForTerminalOperator` now moves each `Skip` and `Take` between the
operator and the rebuild onto the shipped query, in order, before the operator's own row limit.
It is sound for the reason that limit is: the rebuild is row for row and keeps the order. Found by
running Tier B a second time with InfoCarrier removed and comparing each test method's reads:
`Multi_level_includes_are_applied_with_skip` had read every order of every customer whose key
starts with "A".

**Amendment 2026-09-26 — the operators written above a rebuild move below it.** Until this date
the line after this paragraph read "The general case stays deferred, and §7 still holds it." #167's
slow run, which runs each Tier B test with plain EF Core and through this provider and compares the
two, showed what that cost: `Select(x => new Dto { Id = x.OrderID }).Where(d => ((IHaveId)d).Id ==
10252)` read all 831 orders where EF reads one, `Select(o => new { Id = CodeFormat(o.OrderID)
}).Count()` read every order to count them, and EF's `Take_with_single_select_many` read 75531 rows
of a cross join where EF reads two. `ProjectionRewriter.TryMoveBelowReassembly` and
`VisitOrderingChain` now move these onto the server's tuple:

- `Count`, `LongCount` and `Any` without a predicate drop the rebuild, because they read no value
  of it. EF drops the projection under them too, so client code in it is never called.
- `Skip` and `Take` move below, for the reason paging under a terminal operator does.
- `Where` and an ordering chain move below when their lambda, fused with the rebuild, is one the
  server can run. EF's `ReplacingExpressionVisitor` does the fusion and folds
  `new Dto { Id = row.Item1 }.Id` to `row.Item1`, through a cast to an interface too.
- A predicate given to a terminal operator is a `Where` under the operator, as EF normalizes it.
- `FirstOrDefault` and `SingleOrDefault` without a predicate, inside a projection, run on the
  server's tuple (`OneRowRebuilt`, the same day, H7). Their projection is carried in the
  reference-typed `Tuple` family, so that "no row" reads as `null`, and the client rebuilds the one
  row it gets. Before, the whole collection travelled in a slot and the client kept its first
  element: `Lift_projection_mapping_when_pushing_down_subquery` read 134 rows where EF reads 24.

**What stays on the client** is an operator whose lambda still needs the rebuild or client code,
and one whose fused lambda reads a slot that holds a sequence. The second is §6a's lesson: a slot
can hold a `GroupJoin`'s grouping, and navigating out of a projected tuple back into it is what no
provider translates. `QuerySplitter` judges what stays exactly as before.

The general case of §7, an arbitrary operator over any client-typed element, is still deferred.

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
| Operator pushdown past the boundary for any operator over any client-typed element (`Where`, orderings, paging, counting, `Distinct` and `Select` over a rebuild are done, §3.4) | Correctness first; needs the residual→tuple slot map to be invertible | performance backlog |
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

## 9. Exit criteria (from [`roadmap.md`](plans/v10/roadmap.md) M2)

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
