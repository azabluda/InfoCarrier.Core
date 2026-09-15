# Test overhaul: every override points to evidence

**Status: proposed, 2026-09-15.** This replaces the first version of this file from the same day,
which kept the ratchet and classified overrides with the labels `LIMIT`, `DEFECT` and `ISSUE`. The
owner chose a stricter rule instead. It is tried on ADR-009 Tier D first and extended to the other
tiers after that.

## The decision

**The suite is green, and there is no ratchet.** That is Microsoft's approach for EF Core's own
providers.

**We are stricter than Microsoft in one way: an override needs a reference.** An override that
replaces a specification test's assertion is a skip, whatever it asserts. It is permitted only
with a reference to a place where the store does the same thing. There are three kinds of
reference and no other reason is accepted.

| Kind | When | The reference |
|---|---|---|
| Upstream | the store's own suite has the same test | repository, commit, file and line |
| Self-hosted | the store's suite has no such test | a test in this repository that shows what the store does |
| InfoCarrier defect | the failure is ours | a fix; if no fix is possible now, a GitHub issue in this repository |

**The commit is the release tag of the package version we run, never a branch.** This tier runs
`MongoDB.EntityFrameworkCore` 10.0.3, tagged `v10.0.3` at `de93261da6d989bd12db8815a3a672dcf28bafb1`.
Their `main` carries an unreleased rebuild of the query provider (`EF-322`), so it describes a
different product.

**A GitHub issue is filed only when the owner asks.** Tier D has no InfoCarrier defect today, so
the trial needs no issue.

## Why stricter than Microsoft

Measured on 2026-09-15 in EF's Cosmos `Associations` tests: seven files, two distinct issue numbers,
and one class with about 25 overrides of which two carry an issue. The rest carry a sentence, a
pasted error message, or nothing. A green suite built that way hides how much it suppresses, and
the reason for each suppression cannot be checked.

It also fails silently in the worst way. MongoDB's own `EF-367` is *"Include specification suites
mask wrong-data failures behind a bare-catch AssertTranslationFailed"*. This repository did the same
on 2026-09-14, when an override asserted three rows where five are correct.

## What "the store does the same" means

Proposed, and to be confirmed. All three conditions are required:

1. **The same operator.**
2. **The same kind of source**: the root set, a primitive collection, an owned reference, or an
   owned collection.
3. **The same outcome**: the same exception type, or the same result.

**Measured consequence: none of the seven upstream citations Tier D carries survives.**

- **SelectMany.** Their two tests, [`L48`](https://github.com/mongodb/mongo-efcore-provider/blob/de93261da6d989bd12db8815a3a672dcf28bafb1/tests/MongoDB.EntityFrameworkCore.FunctionalTests/Query/UnsupportedQueryTests.cs#L48)
  and [`L98`](https://github.com/mongodb/mongo-efcore-provider/blob/de93261da6d989bd12db8815a3a672dcf28bafb1/tests/MongoDB.EntityFrameworkCore.FunctionalTests/Query/UnsupportedQueryTests.cs#L98),
  select from `string[]`, which is a primitive collection. Ours selects from an owned entity
  collection. The source differs. `test/tier-d-overrides.txt` calls theirs "an EMBEDDED array,
  which is our shape", and that claim is wrong. Their `Planet` does declare an owned collection,
  `parkingCars`, and no test selects from it.
- **GroupBy.** Their test, [`L106`](https://github.com/mongodb/mongo-efcore-provider/blob/de93261da6d989bd12db8815a3a672dcf28bafb1/tests/MongoDB.EntityFrameworkCore.FunctionalTests/Query/UnsupportedQueryTests.cs#L106),
  groups the root set and asserts `InvalidOperationException`. Ours groups inside a document and
  raises `ExpressionNotSupportedException`. Both the source and the outcome differ.

So every Tier D reference is self-hosted today. That is a finding about the store rather than a
failure of the rule: MongoDB's suite has not tested queries over nested documents.

## Where the reference lives

Proposed: an attribute on each override, not a comment.

| Attribute | Kind |
|---|---|
| `[UpstreamTest("https://github.com/<owner>/<repo>/blob/<commit>/<path>#L<line>")]` | upstream |
| `[StoreControl(typeof(DirectProjectionTest), nameof(DirectProjectionTest.GroupBy))]` | self-hosted |
| `[InfoCarrierDefect("https://github.com/azabluda/InfoCarrier.Core/issues/<n>")]` | InfoCarrier defect |

**One reflection test enforces it.** It fails when an override of a specification test carries no
attribute, or carries an upstream link without a 40-character commit and a line anchor. That is
Microsoft's `Check_all_tests_overridden` made stricter: theirs proves somebody looked at a test,
ours proves why it is skipped.

**Open question: the form of a self-hosted reference.** The owner asked for one unified form. A
commit link to this repository has two defects: a commit cannot contain its own hash, so the
reference and the override would need separate commits, and the line number goes wrong the moment
the file changes. `typeof` plus `nameof` is checked by the compiler and cannot go wrong. The
proposal is a compiler reference for self-hosted evidence and a commit link for upstream evidence.

## The self-hosted control

It runs the **same** EF base with InfoCarrier removed, so both sides use EF's query rather than a
copy of it. In Tier D these are the `Direct*` classes, and they cost four seconds.

**It asserts what the store does and is therefore green.** It names the exception or the result,
never "this failed". When the store changes, the control goes red, and that is how a stale override
is found.

**It is built only where upstream has no test.** A control for every tier would double the suite to
about 59000 tests.

**It is a conflict of interest, and two things guard it.** A worse-wired control fails more and makes
InfoCarrier look cleaner. First, each assertion names an exact outcome, so a mis-wired control has
to produce the identical exception to pass. Second, the control classes that need no override —
Tier D's `Miscellaneous` and `PrimitiveCollection` — must stay green without one. They are the
canary for the control's wiring.

## Trial on Tier D

1. Add the three attributes and the reflection test.
2. Give each of the 15 control tests that still fail a real assertion. Each needs its query written
   out, because the failure happens inside the base's own assertion.
3. Override the 14 Tier D failures, each with a reference.
4. Replace every citation comment with an attribute, and check each against "the same". All seven
   upstream citations become self-hosted, as measured above.
5. Delete `test/tier-d-overrides.txt`, `test/tier-d-control-pending.txt` and
   `eng/tier-d-control.py`. Tier D stays in the ratchet with zero failures during the trial, which
   already makes any red fail CI.
6. Amend ADR-009, and correct CLAUDE.md where it describes the Tier D gate and the three evidence
   classes.

## Extending to the other tiers

- **Read upstream first**: EF's InMemory and SQLite functional tests, and the Firebird provider's
  suite. Pin each link to the package version this repository runs.
- **Build a control only where upstream has none.**
- **Every existing skip gets a reference or goes.** Measured 2026-09-15 in the spec project: 83 uses
  of `Task.CompletedTask`, 116 of `ThrowsAsync`, 113 of `AssertTranslationFailed`, 268 of
  `ApplyNotSupported`.
- **Decide first how the reflection test tells a skip from an override that calls the base and only
  adds assertions.** The second is not a suppression. Tier D has none, so the trial does not need
  the answer.
- **Then remove the ratchet**: `eng/ratchet.sh`, both baseline files, and the direction gate in CI.

**This reverses a LOCKED guardrail.** CLAUDE.md says *"Never `[Skip]`, delete, or override a spec
test to make the suite green"*, and ADR-004 says a red test is information. The reversal needs a
dated supersession of ADR-004, and the same commit must sweep every comment that argued for the
ratchet.
