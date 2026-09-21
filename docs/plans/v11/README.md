# EF Core 11: notes before adoption

Adopting EF Core 11 is issue [#99](https://github.com/azabluda/InfoCarrier.Core/issues/99), and it
is `11.0.0` and nothing else can be (`docs/versioning.md`). **It has not started.** This folder
collects what the 10.x work found that the adoption must not forget, written down while the finding
is fresh rather than rediscovered when EF 11 ships.

When the adoption starts, this folder receives the generation's `roadmap.md` and
`implementation-plan.md`, as [`docs/plans/README.md`](../README.md) describes, and these notes feed
them.

| File | What it holds |
|---|---|
| [`adoption-checklist.md`](adoption-checklist.md) | What moves mechanically, what #99 says that is no longer true, and what it does not name |
| [`upstream-rechecks.md`](upstream-rechecks.md) | EF behaviour that 10.x pins, works around or cites, to re-read at the EF 11 tag |

## Adding a note

- **One subject per entry**, saying what to check, where, and why, with the date it was written.
- **Name what to re-read, not its current value**: a test, a file, an issue. A count is stale by the
  time the adoption starts.
- **A note that turns out wrong is corrected in place, with the date**, and not deleted. The next
  reader needs to know the reasoning changed.
