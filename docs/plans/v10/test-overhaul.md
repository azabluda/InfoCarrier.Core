# Test overhaul: what a red test means

**Status: proposed, 2026-09-15.** Nothing below is implemented outside ADR-009 Tier D. When it is
agreed it becomes a dated amendment to ADR-004 and ADR-009, and this file is archived.

## The problem

Two rules are in tension, and the tension was invisible until Tier D adopted specification bases.

ADR-004 says a red test is information and must not be suppressed. That rule is about **this
provider**. If the backing store refuses a query, this provider cannot answer it either, so the red
restates the obvious and is noise in the failure list.

The opposite failure is worse and we committed it on 2026-09-14. An override asserted three rows
where five are correct, and the suite reported green over a wrong answer. MongoDB filed the same
defect against their own suite as `EF-367`.

So neither "always red" nor "always override" is right. The rule has to say **why** a test is green.

## The decision

**A red test means one thing: a defect in InfoCarrier.** Everything else is overridden, labelled and
counted.

| What failed | Label | Test state | Recorded in |
|---|---|---|---|
| A store design limit | `LIMIT` | green, overridden | the override comment |
| A store crash or wrong answer | `DEFECT` | green, overridden | `docs/upstream-defects.md` |
| A store bug with a ticket | `ISSUE <key>` | green, overridden | their tracker |
| **An InfoCarrier defect** | none | **RED** | `test/known-failures.txt` |

**Red is the default.** An override is an exception that needs evidence and a label. An unclassified
new failure stays red, and the ratchet reports that the count rose.

## Three rules for an override

1. **It asserts an exact value** — a row count, an exception type. Never "this failed".
   `Task.CompletedTask` and a bare `catch` are skips in a different spelling and are not permitted.
2. **It carries a label and a citation.** Without one, the claim "the store cannot do this" is our
   own assertion about somebody else's code.
3. **A crash or a wrong answer is `DEFECT`, never `LIMIT`.** A store that means "no" says so.

## Where the evidence comes from

**Read an upstream control if one exists. Build one only when none does.**

| Tier | Store | Control |
|---|---|---|
| A | InMemory | EF's InMemory functional tests — read |
| B | SQLite | `EFCore.Sqlite.FunctionalTests` — read |
| C | Firebird | the Firebird provider's own suite — read |
| D | MongoDB | **built**: the `Direct*` classes, because their suite never runs these bases |

A built control runs the same EF base with the wire removed, so both sides use EF's query rather
than a copy of it. Tier D's costs 4 seconds. **A control for every tier is rejected**: it would
double the suite to roughly 59000 tests, which is the expensive idea rejected weeks ago.

Where no control exists and building one is not worth it, the test stays **red**.

## Gates

- **`eng/tier-d-control.py`** — the built control is a conflict of interest, because a worse-wired
  control fails more and makes this provider look cleaner. The gate checks both directions and has
  already caught three real mistakes. It generalises to any future built control.
- **The override list is ratcheted like the failure list.** Its size must not rise without a reason
  in the commit message. That is what keeps a green suite honest about how much it suppresses.

## Order of work

1. **Tier D**, which is already half done. Convert its 54 failures to the label scheme.
2. **Re-triage every pre-Tier-D override**, honestly. Measured 2026-09-15: 210 files, and the
   weakest group is the **83 uses of `Task.CompletedTask`**, which assert nothing. Then the 116
   `ThrowsAsync` uses. The 268 `ApplyNotSupported` uses are cited and consistent, so they come last.
3. **Amend ADR-004 and ADR-009** with the agreed wording, and archive this file.

## What does not change

`test/known-failures.txt` and its names file stay, and so does the ratchet over them. The count
changes meaning: it becomes the number of InfoCarrier defects rather than the number of failing
tests. **Both baselines move together** and a commit that raises either states why.
