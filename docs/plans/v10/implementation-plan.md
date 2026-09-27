# Implementation plan — no phase open

Milestone-level scope lives in [`roadmap.md`](roadmap.md). Do not put scope here.

**No phase is open (2026-09-28).** Every milestone closed on 2026-08-24, and the issue-driven phases
that followed, Q to H, were done when H4 merged. Their plan is archived with the
milestones' and never edited again. The next phase starts here, as a section that names the GitHub
issue it serves, with one checkbox per step, ticked in the commit that does the work. Which release
it lands in is decided on the issue, not here.

| Plan | Archive |
|---|---|
| M5 — wire hardening (Phase P) | [`archive/implementation-plan-m5-phase-p.md`](archive/implementation-plan-m5-phase-p.md) |
| M6 — spec-base adoption (Phases A–C) | [`archive/implementation-plan-m6-phase-c.md`](archive/implementation-plan-m6-phase-c.md) |
| M8 — productization (Phases H–N) | [`archive/implementation-plan-m8-phases-h-n.md`](archive/implementation-plan-m8-phases-h-n.md) |
| M9 — provider neutrality (Phase J) | [`archive/implementation-plan-m9-phase-j.md`](archive/implementation-plan-m9-phase-j.md) |
| Post-10.0, issue-driven (Phases Q, R, V, S, T, U, Y, Z, X and H) | [`archive/implementation-plan-post-10.0.md`](archive/implementation-plan-post-10.0.md) |

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
