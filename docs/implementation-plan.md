# Implementation plan — M8 (productization)

Rolling checkbox detail for the **current** milestone only. M6's plan (Phases A–C) is in
[`archive/implementation-plan-m6-phase-c.md`](archive/implementation-plan-m6-phase-c.md) and is
never edited again.

Milestone-level scope lives in [`roadmap.md`](roadmap.md). Do not put scope here.

The suite stands at `Total tests: 22453, Passed: 22219, Failed: 13, Skipped: 221` (`c96`). All 13
are classified in C96 of the archived plan; none is a blocker for M8.

## Phase H — the HTTP transport (spec: `superpowers/specs/2026-08-11-blazor-wasm-sample-design.md` §10 phase 1)

Detailed steps are in
[`superpowers/plans/2026-08-11-northwind-http-transport.md`](superpowers/plans/2026-08-11-northwind-http-transport.md).
**That document is the "how"; this one is the record of what landed and what it measured.**

- [x] **M8-1. The spec measurement is scoped to one project, and the M8 plan is open.** `<this commit>`
- [x] **M8-2. The shared Northwind model, and a test project of its own.** `<this commit>`
- [x] **M8-3. `HttpInfoCarrierTransport`, the client half of the wire.** `<this commit>`
