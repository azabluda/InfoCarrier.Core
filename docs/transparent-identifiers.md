# Transparent identifiers — design spec

Status: **design**, implementation not started.
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

That is not merely slow. Two distinct failure modes come out of it:

- **Wrong answers.** A left join's `DefaultIfEmpty()` yields `null`; SQL propagates nulls through
  a projection and LINQ-to-Objects throws instead. `NullReferenceException`, 16 tests.
- **Refusals.** The client half then reads a navigation the server never sent, and the split
  refuses rather than answer `0`. 20 tests.

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

### 3.1 Flatten `GroupJoin` + `SelectMany` — mirror EF

Run the equivalent of `TryFlattenGroupJoinSelectMany` on the client, **before** the boundary
analysis. Same match, same substitution, same output: `Join` when there is no `DefaultIfEmpty`,
`LeftJoin` when there is.

This is deliberately a mirror, not an invention. The pattern is EF's, the emitted operators are
EF's, and the guard EF applies — flatten only when the collection selector is *uncorrelated* —
is EF's. Where EF gives up (it has a `TODO` for the correlated case) we give up identically and
leave the tree alone.

It also removes the specific thing that made the first attempt explode: the grouping. After
flattening there is no `IEnumerable<T>` to put in a carrier slot.

### 3.2 Re-carry the remaining identifiers — ours

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

**A tuple is structurally what the anonymous type was**, which is why navigating out of a slot
(`t.Item2.OrderDetails`) is expected to translate: it is the same shape EF already translates for
`Select(c => new { c, o }).SelectMany(x => x.o.OrderDetails)`.

## 4. The two guards, and why each exists

**No sequence in a slot.** A slot may hold a scalar or an entity, never an `IEnumerable`. This is
the constraint the first attempt violated: with `ValueTuple<Order, IEnumerable<Customer>>`,
`t.Item2.DefaultIfEmpty()` asks SQL to navigate out of a projected tuple back into a correlated
collection, and 107 `SelectMany`/`Join` translation failures followed. §3.1 removes the only
common source of such a slot; this guard catches the rest.

**Transform, then verify.** After rewriting, re-run the boundary analysis. Keep the rewrite only
if it **strictly increases** what ships — otherwise discard it and use the original tree. The
first attempt committed to its rewrite blindly and could only be assessed by running 4,000 tests.
A transformation that has to justify itself before it is kept cannot regress the split, only fail
to improve it.

Neither guard makes translation *certain* — server-ok is a type property, not a translatability
property. That gap is real and is why §6 keeps the phases separately measurable.

## 5. What this does not cover

- **Correlated collection selectors.** §3.1 declines them exactly where EF declines them.
- **A transparent identifier consumed by genuinely client-side code.** If the chain does not
  become server-ok, verification discards the rewrite and today's behaviour stands.
- **`ElementAt`/`First` over a client projection compared to `null`** — a different problem
  (`projection-split.md` §6a tail), unaffected by any of this.

## 6. Phases, each measured on its own

| Phase | Work | Target |
|---|---|---|
| **X1** | Mirror `TryFlattenGroupJoinSelectMany`, plus the member-access-over-`new` simplification it relies on | the 16 `NullReferenceException` failures |
| **X2** | Verification harness: rewrite, re-analyze, keep only on strict improvement | no test movement expected — it is the safety net for X3 |
| **X3** | `ValueTuple` re-carry for surviving identifiers, under both guards | the 20 navigation-read refusals |

X2 lands **between** the two rewrites, not after, so X3 is never measured without its guard.

## 7. How it will be known to work

- Each phase measured separately against the committed baseline; a phase that does not improve it
  is reverted rather than argued for.
- The two families are independently attributable — `NullReferenceException` for X1, navigation
  refusals for X3 — so a phase that fixes the wrong thing is visible.
- Both tiers must move together. Tier A alone would mean the fix depends on InMemory's client
  evaluation, which is the failure mode this project has already recorded twice.
