# Transparent identifiers — design spec

Status: **X2 and X3 implemented; X1 tried and reverted.** Measured 111 → 101.
Extends [`projection-split.md`](projection-split.md) §6a. Recorded as [ADR-011](decisions.md#adr-011).

Measured 2026-08-02: **36 of 111 remaining failures** are this problem, across both test tiers.

---

## 1. What a transparent identifier is, and why it is ours to solve

`from c in cs from o in c.Orders where … select c` has no anonymous type in it. The C# compiler
puts one there: each `from` after the first produces a *transparent identifier* —
`SelectMany(cs, c => c.Orders, (c, o) => new { c, o })` — so the later clauses can still see `c`.
The same happens for `join … into … from … in …`, and for `let`.

EF never has to care. Its server pipeline handles anonymous types natively, so the identifier
just flows through translation. This provider cannot: an anonymous type is by definition absent
from the server's assembly, so [ADR-010](decisions.md#adr-010) treats it as a type boundary — and
every operator above the identifier falls to the client.

That is not merely slow. The client half then reads a navigation the server never sent, and the
split refuses rather than answer `0` — `Multiple_select_many_with_predicate`, `Navs_query` and
the rest of that family.

> ⚠️ **Corrected 2026-08-02, after X1 measured the residual directly.** This section originally
> also claimed a second failure mode — 16 `NullReferenceException`s from a left join's
> `DefaultIfEmpty()` propagating a null through a client-side projection — and made it X1's
> target. That attribution was inherited from `implementation-plan.md`, which had recorded it
> before phases A6 and E1 landed, and it is **no longer true of any failing test**. The surviving
> `NullReferenceException`s are `Select_GetValueOrDefault_on_DateTime_with_null_values`,
> `Reverse_in_join_inner`(`_with_skip`) and `Entity_equality_contains_with_list_of_null` — none of
> them a `join … into … from … DefaultIfEmpty` shape. The refusals below are the whole of this
> problem as it stands.

## 2. The finding that shapes the design

**EF already eliminates the `GroupJoin` form of this, and it does so at a point we never reach.**

`QueryableMethodNormalizingExpressionVisitor.TryFlattenGroupJoinSelectMany`
(`subrepos/efcore/src/EFCore/Query/Internal/QueryableMethodNormalizingExpressionVisitor.cs:566`)
matches `SelectMany` over `GroupJoin`, strips a `DefaultIfEmpty`, substitutes the group-join
result selector into the collection and result selectors, and emits a single `Join` or
`LeftJoin` with **no transparent identifier at all**.

It runs inside `CompileQuery` — on the client that is *after* ADR-006 capture, and on the server
it is after a deserialization that cannot happen. So the transformation exists, is proven, and is
positioned exactly where we cannot use it.

**Nothing equivalent exists for plain `SelectMany` chains**, because EF has no need of one. There
the identifier survives translation, and the relational visitor composes over it as a subquery.
For those we are on our own.

## 3. Two transformations, in order

### 3.1 Flatten `GroupJoin` + `SelectMany` — mirror EF · **tried, measured neutral, reverted**

The plan was to run the equivalent of `TryFlattenGroupJoinSelectMany` on the client, before the
boundary analysis: same match, same substitution, same output — `Join` without a
`DefaultIfEmpty`, `LeftJoin` with one — on the reasoning that a mirror of EF's own rewrite could
not be wrong.

**Built, measured `111 → 111`, reverted.** Two findings, both of which outrank the phase itself:

**EF's rewrite is not separable from EF's pipeline.** Substituting the group-join result
selector's body for the `SelectMany` parameter *reconstructs* the transparent identifier, grouping
member and all. So `from c … into g from o in g where … select o` produces a join whose result
selector body names `g` while the join binds only `c` and `o`. EF never notices: its projection
binding collapses that reconstructed anonymous type and drops the member nothing reads, long
before anything is compiled. This provider compiles the residual, so the same tree is a hard
`InvalidOperationException` — *"variable 'orders' … referenced from scope ''"* — and the first
run of X1 broke 10 previously-passing tests exactly there. Mirroring EF's **match** is not
mirroring its **pipeline**; the tail of that pipeline was what made the output legal.

Declining the rewrite whenever it leaves a parameter free repaired that, at the cost of declining
precisely the shapes that keep a grouping in the identifier — which is every case §3.1 claimed to
help. With the guard the phase is exactly neutral: nothing fixed, nothing broken.

**Its supporting role for §3.2 is void too.** §3.1 was also justified as removing the grouping
before it could reach a carrier slot. Under the guard it removes no groupings at all, so §3.2's
"no sequence in a slot" guard is now load-bearing on its own rather than a backstop.

Recorded here rather than retried. A future attempt would have to bring the dead-member
elimination with it — which is §3.2's work, not a preliminary to it.

### 3.2 Re-carry the remaining identifiers — ours · **done, 111 → 101**

For a transparent identifier that survives §3.1, **replace the anonymous type with a
`ValueTuple` and do not reassemble it on the client.** Member reads through it become tuple-slot
reads, and the chain stays server-side.

| | expression |
|---|---|
| original | `SelectMany(cs, c => c.Orders, (c, o) => new { c, o })` then `…ti.o.OrderDetails…` |
| rewritten | `SelectMany(cs, c => c.Orders, (c, o) => new ValueTuple<Customer, Order>(c, o))` then `…t.Item2.OrderDetails…` |

This is *not* the reassembly deferral that failed. That one pushed operators back below a
projection that already existed, and had to prove each move safe. This never creates the
client-side reassembly in the first place — the identifier is compiler plumbing that no caller
ever sees, so there is nothing to rebuild.

**A tuple is structurally what the anonymous type was** — but only if it is built the same way,
and that turned out to be the whole phase.

> ⚠️ **Measured.** A tuple built with `Expression.New(ctor, args)` is *not* what an anonymous type
> is. EF collapses `new { c, o }.c` back to `c` by looking the member up in
> `NewExpression.Members` (`ReplacingExpressionVisitor.VisitMember`); that is how an
> anonymous-type carrier survives navigation expansion at all. Without members, `t.Item1` is an
> opaque field read, the entity behind it is lost, and every query that navigates out of a slot
> stops translating — **214 extra failures, 111 → 323**. Supplying the `Item1…Item7`/`Rest` fields
> as members took it to 116 in one change. The claim above is true of the shape and false of the
> construction, which is not a distinction the design anticipated.

Two more corrections came out of measurement, each worth more than the line it changed:

- **The result type is not the only way a carrier escapes.** The rule "rewrite it only if it never
  reaches the query's result" is defeated by `.Cast<object>()`: the carrier vanishes from the
  signature while the value still reaches the caller, which turned
  `Take_with_single_select_many` into a boxed tuple where an anonymous type was asked for. The
  conversion has to be caught where it happens, not inferred from the declared result type.
- **The delegate type must be mapped, not re-inferred.** `SelectMany`'s collection selector is
  declared `Func<TSource, IEnumerable<TCollection>>` while its body — a collection navigation — is
  an `ICollection<TCollection>`. Letting `Expression.Lambda` infer from the body narrows the
  delegate, the rebuilt call no longer matches the operator, and the rewrite is discarded whole.
  This alone was the difference between 107 and 101, and it was silent: the target family simply
  did not move.

## 4. The two guards, and why each exists

**No sequence in a slot.** A slot may hold a scalar or an entity, never an `IEnumerable`. This is
the constraint the first attempt violated: with `ValueTuple<Order, IEnumerable<Customer>>`,
`t.Item2.DefaultIfEmpty()` asks SQL to navigate out of a projected tuple back into a correlated
collection, and 107 `SelectMany`/`Join` translation failures followed. §3.1 was to have removed
the common source of such a slot; since it did not survive measurement, this guard now carries
that weight alone.

**Transform, then verify.** After rewriting, re-run the boundary analysis. Keep the rewrite only
if it **strictly increases** what ships — otherwise discard it and use the original tree. The
first attempt committed to its rewrite blindly and could only be assessed by running 4,000 tests.
A transformation that has to justify itself before it is kept cannot regress the split, only fail
to improve it.

Implemented as `RewriteVerifier` (phase X2). The measure is **query operators left on the
client**, which is the quantity being moved and the one that stays comparable across a rewrite —
node counts do not, since swapping an anonymous type for a `ValueTuple` changes a tree's size
without moving anything. Three well-formedness refusals come before the measure: a changed root
type, a parameter nothing binds (X1's failure, kept as its regression test), and a shippable
subtree turned into a correlated fragment.

Neither guard makes translation *certain* — server-ok is a type property, not a translatability
property. That gap is real and is why §6 keeps the phases separately measurable.

## 5. What this does not cover

- **Correlated collection selectors.** §3.1 declines them exactly where EF declines them.
- **A transparent identifier consumed by genuinely client-side code.** If the chain does not
  become server-ok, verification discards the rewrite and today's behaviour stands.
- ~~**`ElementAt`/`First` over a client projection compared to `null`**~~ — recorded here as "a
  different problem, unaffected by any of this", and **wrong**: §3.2's rule covers it exactly
  (the carrier is built in a predicate and never reaches the result). All 34 were fixed in X4 by
  giving a null-compared carrier a reference-typed `Tuple<>` instead of a `ValueTuple`.

## 6. Phases, each measured on its own

| Phase | Work | Target | Outcome |
|---|---|---|---|
| **X1** | Mirror `TryFlattenGroupJoinSelectMany`, plus the member-access-over-`new` simplification it relies on | *(claimed)* 16 `NullReferenceException` failures | **reverted** — target misattributed, rewrite not separable from EF's pipeline (§3.1) |
| **X2** | Verification harness: rewrite, re-analyze, keep only on strict improvement | no test movement expected — it is the safety net for X3 | **done** — `RewriteVerifier`, 111 → 111 of 5222 |
| **X3** | `ValueTuple` re-carry for surviving identifiers, under both guards | the navigation-read refusals | **done** — 111 → 101 of 5227, nothing broken; both tiers moved |

X2 lands **before** the rewrite, so X3 is never measured without its guard. X1 is the argument
for that ordering rather than an exception to it: it had to grow an ad-hoc free-parameter check
mid-implementation, which is the verification step arriving late and in the wrong shape.

## 7. How it will be known to work

- Each phase measured separately against the committed baseline; a phase that does not improve it
  is reverted rather than argued for.
- The two families are independently attributable — `NullReferenceException` for X1, navigation
  refusals for X3 — so a phase that fixes the wrong thing is visible.
- Both tiers must move together. Tier A alone would mean the fix depends on InMemory's client
  evaluation, which is the failure mode this project has already recorded twice.
