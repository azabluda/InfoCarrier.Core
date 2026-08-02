---
name: experiment
description: Run one measured query-rewrite experiment against the spec suite — baseline, single change, full run, keep only on strict improvement.
---

# One experiment

Use this for any change to query translation, the projection split, or the carrier rewrites.
Not for docs, tests-only edits, or anything whose effect is not a failure count.

The whole point is that the verdict and the numbers backing it are inseparable. This repo has
twice reached a confident wrong conclusion from a run it did not finish reading, and both cost
more than the run would have.

## Steps

1. **Clean tree.** `git status --short`. If dirty, stop and ask — a measurement against
   uncommitted state cannot be attributed.

2. **Baseline.** `eng/measure.sh baseline`. Quote the printed line. If a `baseline` snapshot from
   this session already exists and HEAD has not moved, reuse it rather than re-running.

3. **One change.** Apply only the change described. No opportunistic cleanups, no second idea
   "while I'm here" — a run that measures two changes measures neither.

4. **Measure.** `eng/measure.sh <name> baseline`. This prints `FAILING`/`TOTAL` and the exact
   FIXED and BROKEN lists.

5. **Verdict, from the lists and not from the count.**
   - `FAILING` down and `BROKEN` empty → keep.
   - `BROKEN` non-empty → for each one, establish *why* before calling it a regression. Two
     specific traps, both of which have produced a wrong revert here:
     - **SQLite `ApplyNotSupported`** — grep `subrepos/efcore/test/EFCore.Sqlite.FunctionalTests`
       for the test name. If EF overrides it the same way, the query now reaches SQL and this is
       convergence with the reference provider, not a regression: adopt EF's override.
     - **A structural assertion** — read the actual expected/actual values. "The shape changed"
       is not the same as "the semantics broke".
   - `FAILING` unchanged → do **not** conclude the target does not exist. Establish that the
     code ran at all first: a matcher that never matched and a rewrite that did not help are
     indistinguishable from the count. Probe it (write to a file; xUnit swallows stdout) before
     drawing any conclusion.

6. **Keep or revert.** If it does not pay, revert the code — but commit the *finding* in the
   plan. A measured negative result is worth recording; dead neutral code is not.

7. **Commit** with the numbers in the message: `Passed: N, Failed: M, Skipped: S, Total: T`,
   read from actual output, never estimated. Tick the plan checkbox in the same commit.

## Report back

State the verdict in one line with `<before> → <after>`, then the FIXED and BROKEN lists in
full. Never summarise a BROKEN list as "a few unrelated failures".
