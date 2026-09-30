---
name: experiment
description: Run one measured query-rewrite experiment against the spec suite - baseline, single change, full run, keep only on strict improvement.
---

# One experiment

Use this for any change to query translation, the projection split, or the carrier rewrites.
Not for docs, tests-only edits, or anything whose effect is not a failure count.

The verdict and its supporting numbers are inseparable. This repository has reached wrong
conclusions from incomplete runs, and those conclusions cost more than completing the run.

## Steps

1. **Clean tree.** Run `git status --short`. If dirty, stop and ask. A measurement against
   uncommitted state cannot be attributed.

2. **Baseline.** Run `eng/measure.sh baseline`. Quote the printed line. If a `baseline` snapshot
   from this session already exists and HEAD has not moved, reuse it.

3. **One change.** Apply only the described change. Do not include unrelated cleanups or another
   experiment.

4. **Measure.** Run `eng/measure.sh <name> baseline`. Read `FAILING`, `TOTAL`, the complete
   `FIXED` and `BROKEN` lists, and the failure-reason difference.

5. **Verdict.** Use the lists and reasons, not only the count.
   - `FAILING` decreases and `BROKEN` is empty: keep the change.
   - `BROKEN` is not empty: establish why each test broke before deciding.
   - `FAILING` is unchanged: establish that the changed code ran before deciding.

   For SQLite `ApplyNotSupported`, load the Entity Framework Core solution with
   `roslyn-codelens`. Search its SQLite and relational functional tests for the test name. An
   equivalent upstream override means the query now reaches SQL and may show convergence.

   For a structural assertion, read the expected and actual values. A changed shape does not
   prove changed semantics.

   When the count is unchanged, use a file-writing probe. xUnit can swallow standard output.

6. **Keep or revert.** Revert code that does not improve the measured result. Record a useful
   negative finding in the plan.

7. **Commit only when requested.** Use counts from the actual output. Update the matching plan
   checkbox in the same commit.

## Report back

State the verdict in one line with `<before> -> <after>`. Then give the complete `FIXED` and
`BROKEN` lists. Never summarize a `BROKEN` list as unrelated failures.
