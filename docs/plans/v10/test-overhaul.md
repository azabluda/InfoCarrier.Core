# Test overhaul: every override points to evidence

**Status: proposed, 2026-09-15.** This replaces the first version of this file from the same day.
That version kept the ratchet; this one drops it and adds a reference to every override. **The labels
`LIMIT`, `DEFECT` and `ISSUE` from the first version stay**, at the owner's request, because they
answer a different question from the reference. It is tried on ADR-009 Tier D first and extended to
the other tiers after that.

## The decision

**The suite is green, and there is no ratchet.** That is Microsoft's approach for EF Core's own
providers.

**We are stricter than Microsoft in one way: an override needs a reference.** An override
**changes the expected behaviour** of a specification test: the store refuses where EF expects an
answer, crashes, or answers where EF expects a refusal. It is permitted only with a reference to a
place where the store does the same thing. There are three kinds of reference and no other reason is
accepted.

**An override is not a skip, and a skip is not permitted.** The override states the new expected
behaviour exactly — the exception type, the row count, the value — so it goes red the day the store
behaves differently. An override that asserts nothing, `Task.CompletedTask` for example, or one that
catches any exception, watches nothing and would stay green over a wrong answer.

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

## Labels and references are two questions

**The label says WHAT the store does. The reference says WHERE that is shown.** Every override that
changes an expected behaviour to the store's behaviour carries exactly one of each.

| Label | What the store does | Also recorded in |
|---|---|---|
| `LIMIT` | refuses by design | nothing else |
| `DEFECT` | crashes, or answers wrongly | a section of `docs/upstream-defects.md` |
| `ISSUE <key>` | behaves in a way a tracker entry already covers | that tracker |

**A crash or a wrong answer is never `LIMIT`.** A store that means "no" says so. A
`NullReferenceException` is a `DEFECT` even when the store would refuse the same query on a better
day, because the label records what happened and not what was intended.

**`ISSUE` names any tracker, not only the store's.** Two Tier D tests fail because the store answers
a query that EF's base expects it to refuse, and the answer is correct. That is EF Core issue
`#36400`, so the label is `ISSUE dotnet/efcore#36400`.

**An InfoCarrier defect has no store label.** It is fixed, or it carries a GitHub issue of this
repository.

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

Proposed: an attribute on each override, not a comment. **The attribute's name is the label and its
arguments are the reference.** Each label attribute takes either form of reference.

```csharp
// self-hosted reference: the control test that shows it
[StoreLimit(typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_subquery_required_related_FirstOrDefault))]

// upstream reference: the store's own test at the tagged commit
[StoreLimit("https://github.com/<owner>/<repo>/blob/<commit>/<path>#L<line>")]

// a crash or a wrong answer, with its section in docs/upstream-defects.md
[StoreDefect("1.6", typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_optional_nested_on_optional_associate))]

// a tracker entry covers it
[StoreIssue("EF-250", typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_untranslatable_method_on_associate_scalar_property))]

// ours, when no fix is possible now
[InfoCarrierDefect("https://github.com/azabluda/InfoCarrier.Core/issues/<n>")]
```

**One reflection test enforces it.** It fails when an override of a specification test carries none
of these or more than one, when an upstream link lacks a 40-character commit and a line anchor, and
when a `StoreDefect` names a section that `docs/upstream-defects.md` does not have. That is
Microsoft's `Check_all_tests_overridden` made stricter: theirs proves somebody looked at a test,
ours proves what the store did and where that is shown.

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

1. Add the four attributes — `StoreLimit`, `StoreDefect`, `StoreIssue`, `InfoCarrierDefect` — and
   the reflection test.
2. Give each of the 15 control tests that still fail a real assertion. Each needs its query written
   out, because the failure happens inside the base's own assertion.
3. Override the 14 Tier D failures, each with a reference.
4. Replace every citation comment with an attribute, and check each against "the same". All seven
   upstream citations become self-hosted, as measured above. Choose each label again as well: the
   first classification called `GroupBy` an issue on the strength of `EF-149`, which is about
   grouping a root set and not a collection inside a document.
5. Delete `test/tier-d-overrides.txt`, `test/tier-d-control-pending.txt` and
   `eng/tier-d-control.py`. Tier D stays in the ratchet with zero failures during the trial, which
   already makes any red fail CI.
6. Amend ADR-009, and correct CLAUDE.md where it describes the Tier D gate and the three evidence
   classes.

## Extending to the other tiers

- **Read upstream first**: EF's InMemory and SQLite functional tests, and the Firebird provider's
  suite. Pin each link to the package version this repository runs.
- **Build a control only where upstream has none.**
- **Every existing override gets a label and a reference, or goes.** The 83 uses of
  `Task.CompletedTask` assert nothing, so each of those becomes an override with an exact expected
  behaviour, or the base test runs as it is. Measured 2026-09-15 in the spec project: 83 uses
  of `Task.CompletedTask`, 116 of `ThrowsAsync`, 113 of `AssertTranslationFailed`, 268 of
  `ApplyNotSupported`.
- **Decide first what an override that calls the base unchanged and only ADDS assertions needs.** It
  changes no expected behaviour, so it is not what a label describes. Tier D has none, so the trial
  does not need the answer.
- **Then remove the ratchet**: `eng/ratchet.sh`, both baseline files, and the direction gate in CI.

**This reverses a LOCKED guardrail.** CLAUDE.md says *"Never `[Skip]`, delete, or override a spec
test to make the suite green"*, and ADR-004 says a red test is information. The reversal needs a
dated supersession of ADR-004, and the same commit must sweep every comment that argued for the
ratchet.
