# Implementation plan — the rest of #167's triage

Milestone-level scope lives in [`roadmap.md`](roadmap.md). Do not put scope here.

**Every milestone closed on 2026-08-24, and the issue-driven phases that followed, Q to H, were done
when H4 merged.** Their plan is archived with the milestones' and never edited again. What is open
is the rest of #167's triage, below. A new phase starts here too, as a section that names the GitHub
issue it serves, with one checkbox per step, ticked in the commit that does the work. Which release
it lands in is decided on the issue, not here.

| Plan | Archive |
|---|---|
| M5 — wire hardening (Phase P) | [`archive/implementation-plan-m5-phase-p.md`](archive/implementation-plan-m5-phase-p.md) |
| M6 — spec-base adoption (Phases A–C) | [`archive/implementation-plan-m6-phase-c.md`](archive/implementation-plan-m6-phase-c.md) |
| M8 — productization (Phases H–N) | [`archive/implementation-plan-m8-phases-h-n.md`](archive/implementation-plan-m8-phases-h-n.md) |
| M9 — provider neutrality (Phase J) | [`archive/implementation-plan-m9-phase-j.md`](archive/implementation-plan-m9-phase-j.md) |
| Post-10.0, issue-driven (Phases Q, R, V, S, T, U, Y, Z, X and H) | [`archive/implementation-plan-post-10.0.md`](archive/implementation-plan-post-10.0.md) |

## Phase H, continued — the reds #167's slow run still shows

H0 to H4 are in the archive. After H29 the slow run of Tiers B and C was red in 21 methods, each a
decision for the owner and deferred: 8 where this provider answers a query plain EF refuses, and 13
others. A step here takes one of them through the same loop as H5 to H33: a promise of our own, seen
red first, the fix in the product, and the slow run again.

- [x] **H34. A filter on a local list of anonymous objects is refused, as EF refuses it.** On the
      branch `live-comparison-anonymous-contains`, on top of H4. The owner, 2026-09-28, choosing it
      out of the 8 for 10.2.0: "lets include in 10.2.0 and we have good (yet slow :)) tool to
      highlight it and a clear guideline how to pin it before fixing". It was the one of the 8 that
      breaks a documented promise: `Where(o => ids.Contains(new { o.OrderID, o.ProductID }))` over
      a captured list of anonymous objects read all 2156 rows of `"Order Details"` and filtered
      here, where plain EF raises `TranslationFailed` and the guide says a filter the server cannot
      run throws. The other seven are projections that run client code, and each answers right
      from the same rows.

      1. **A promise in `ServerParameterizationTest`, seen red first**:
         `A_filter_on_a_local_list_of_anonymous_objects_is_refused_as_EF_refuses_it`. Before the
         fix it failed with "InfoCarrier answered: SELECT "_"."Id", "_"."Title"", a read of the
         whole table, while plain EF refused.
      2. **The fix**: `QuerySplitter`'s guard against client evaluation refuses a client-side
         operator whose row-deciding lambda calls `Contains` on a local collection of a type the
         wire cannot carry. The anonymous type had passed as the type boundary, the exemption
         that keeps a composite `GroupBy` or join key legal, so the `Where` stayed here. The test
         is the collection, not the construction, and a registered element type is left for the
         server's EF to decide. Relational only, as the guard's ordering-key rule is.
      3. **EF's own SQLite override is adopted** for
         `NorthwindAggregateOperatorsQueryInfoCarrierTest.Contains_with_local_anonymous_type_array_closure`,
         with its line range. Slow run of the class and the promise: **Passed: 423, Failed: 0**,
         and the method is off the red list: 20 left.

      Normal run, `eng/measure.sh anonymous-contains h4-cleanup`: **FAILING 0, TOTAL 29958**, the
      one new promise, FIXED none, BROKEN none, REASONS unchanged. `trim-ratchet.sh` OK at
      106 <= 106. `InfoCarrier.Core.TransportTests` **Passed: 28, Failed: 0, Total: 28**. `CI=true`
      Release build after deleting the product's `obj` and `bin`: 0 errors, once a nullability error
      in the new promise was fixed (`Blog.Title` is `string?`, so the local list declares it too).

## Branches that outlive their pull request

**A branch that survives a merge is a branch nobody records, and until 2026-09-20 none of these
was named in any document.** Work branches are deleted when their pull request merges, by the
repository setting rather than by hand, so only two prefixes live here: `park/` for work that may
resume, `archive/` for work that will not. A machine branch keeps whatever name its workflow types.

| Branch | What it holds | What would revive it |
|---|---|---|
| `park/r77-relational-client-store` | A fixture may opt into a relational client test store (#56). Six adopted bases re-declare `TestStore` as `RelationalTestStore` and 25 tests threw `InvalidCastException`; the branch splits that type into its string half, which is harmless, and its connection half, which would reach the database past the wire | Adopting a base whose tests need the string half. The connection half never ships: a green that reaches the store says nothing about this provider |
| `park/d7-streaming-half` | Streaming results over the HTTP transport, complete and measured green, parked 2026-08-17 because its cost is countable and none of its headline benefits was ever measured | A measurement: peak memory, payload size, time to first byte or throughput. Not an argument from reading the code, which is the error its commit message exists to correct. Renamed from `streaming/d7-half-a-parked` on 2026-09-20 |
| `archive/v5` | The v5 line, last touched 2026-06-28. Kept for history only | Nothing. Renamed from `v5/abandoned` on 2026-09-20 |
| `badges` | Written by `build.yml`, which clones it by name and pushes the EF parity figure that shields.io reads for the README | Not a work branch. Do not rename it: the workflow names it in four places |

**The branches of #167 were deleted on 2026-09-28, at the owner's request.** The 26 step branches of
Phase H were merged into `main` with every commit, so nothing of theirs is lost. Three were never
merged, and their last commits are named here so that a clone still holding them can restore them:
`experiment/direct-baseline` at `7b628da`, the prototype #167 was opened on;
`experiment/live-comparison` at `514a691`, the spike ADR-014's amendment of 2026-09-26 measured; and
`sql-capture` at `9f0ef88`, the withdrawn capture files, which PR #170 also keeps.
