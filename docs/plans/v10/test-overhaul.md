# Test overhaul: every override points to evidence

**Status: done, 2026-09-15.** Every tier is converted, the suite is green (`FAILING: 0  TOTAL:
29792`), and the ratchet is gone: `eng/ratchet.sh` and both baseline files are deleted, and
`eng/suite-summary.sh` reports the counts in CI. **ADR-004 is still to be amended by the owner.**

This replaces the first version of this file from the same day.
That version kept the ratchet; this one drops it and adds a reference to every override. **The labels
`LIMIT`, `DEFECT` and `ISSUE` from the first version stay**, at the owner's request, because they
answer a different question from the reference. It is tried on ADR-009 Tier D first and extended to
the other tiers after that.

## The decision

**The suite is green, and there is no ratchet.** That is Microsoft's approach for EF Core's own
providers.

**Two principles govern every override.**

1. **Deviate from upstream as little as possible.** Where the store's own suite overrides a test,
   this repository overrides it the same way: the same assertion, or the same skip.
2. **Make every override traceable and auditable.** Each one names its label, its reference, and
   upstream's own justification text.

**We are stricter than Microsoft in traceability, not in what an override may do.** An override
**changes the expected behaviour** of a specification test: the store refuses where EF expects an
answer, crashes, or answers where EF expects a refusal. It is permitted only with a reference to a
place where the store does the same thing. There are three kinds of reference and no other reason is
accepted.

**A skip is an override too, and it is permitted where upstream skips.** EF's own suites skip often.
Measured 2026-09-15: `Task.CompletedTask` 52 times in the SQLite functional tests, 100 in InMemory,
25 in Cosmos and 37 in the relational specification bases, plus explicit `Skip =` arguments. Their
reason is often in a comment, as at
[`OwnedJsonCollectionSqliteTest.cs#L13`](https://github.com/dotnet/efcore/blob/a6217e3438ca1fb430079f2626056c1a11581927/test/EFCore.Sqlite.FunctionalTests/Query/Associations/OwnedJson/OwnedJsonCollectionSqliteTest.cs#L13)
in `v10.0.1`: *"Base test expects "can't track owned entities" exception, but with SQLite we get
"no CROSS APPLY""*. Copying that skip, with that text, is the minimal deviation.

**A skip needs an UPSTREAM reference, never a self-hosted one.** A skip asserts nothing, so the only
thing that justifies it is that upstream made the same choice for the same store. Where upstream has
no test, as in Tier D, there is nothing to copy, and the override states the store's behaviour
exactly.

**The accepted risk, stated once.** A skip hides what the store does, including a wrong answer.
MongoDB's `EF-367` is that defect in their suite, and this repository's override of 2026-09-14 was
the same defect here. The risk is accepted only where upstream accepted it, and every skip is
declared so that the audit counts it.

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

**A fifth label, `DESIGN`, for where this provider differs from the store on purpose** (added
2026-09-15, when Tiers A, B and C were converted). A client that refuses a filter it cannot send, or
answers a projection EF's relational providers refuse, fails EF's test on the same store; the store
is not the reason and nothing is broken. `[InfoCarrierDesign(decision)]` names the decision that
causes it: an ADR number of `docs/decisions.md`, or a document and a heading anchor for a decision
recorded where it was made, such as the raw-SQL grant in `docs/security-review.md`. Its `Justification` says
which part of the decision applies, and a skip still needs an upstream reference.

**A skip still carries a label.** The label describes the store, and the skip describes the form of
the override. Upstream's *"with SQLite we get "no CROSS APPLY""* is a `LIMIT`, whether the override
asserts that or skips.

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
  collection. The source differs. The first classification called theirs "an EMBEDDED array,
  which is our shape", and that claim was wrong. Their `Planet` does declare an owned collection,
  `parkingCars`, and no test selects from it.
- **GroupBy.** Their test, [`L106`](https://github.com/mongodb/mongo-efcore-provider/blob/de93261da6d989bd12db8815a3a672dcf28bafb1/tests/MongoDB.EntityFrameworkCore.FunctionalTests/Query/UnsupportedQueryTests.cs#L106),
  groups the root set and asserts `InvalidOperationException`. Ours groups inside a document and
  raises `ExpressionNotSupportedException`. Both the source and the outcome differ.

So every Tier D reference is self-hosted today. That is a finding about the store rather than a
failure of the rule: MongoDB's suite has not tested queries over nested documents.

## Where the reference lives

**An attribute on each override, not a comment** (accepted by the owner, 2026-09-15). **The attribute's
name is the label and its arguments are the reference.** Each label attribute takes either form of
reference. The attributes and the audit live in `test/InfoCarrier.Core.TestUtilities`, so every tier
uses the same ones.

```csharp
// self-hosted reference: the control test that shows it
[StoreLimit(typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_subquery_required_related_FirstOrDefault))]

// upstream reference: repository, path and line range at the pinned commit, with upstream's words
[StoreLimit(
    UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/<path>.cs", <first>, <last>,
    Justification = "<upstream's comment, copied verbatim>")]

// a skip copied from upstream: Skip = true, and the upstream reference is mandatory
[StoreLimit(
    UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Associations/OwnedJson/OwnedJsonCollectionSqliteTest.cs", <first>, <last>,
    Justification = "Base test expects \"can't track owned entities\" exception, but with SQLite we get \"no CROSS APPLY\"",
    Skip = true)]

// a crash or a wrong answer, with its section in docs/upstream-defects.md
[StoreDefect("1.6", typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_optional_nested_on_optional_associate))]

// a tracker entry covers it: the tracker and the number
[StoreIssue(IssueTracker.MongoEfCore, 250, typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_untranslatable_method_on_associate_scalar_property))]

// ours, when no fix is possible now: the issue number in this repository
[InfoCarrierDefect(<n>)]

// ours, on purpose: the ADR number, or a document and heading, and which part of it applies
[InfoCarrierDesign(10, Justification = "A filter the server cannot run is refused ...")]
[InfoCarrierDesign(Decisions.SecurityReview, Decisions.RawSqlGrant, Justification = "Raw SQL is refused unless the server grants it ...")]

// a body that differs from upstream's: the kinds, and a note where a kind needs one
[StoreLimit(
    UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/<path>.cs", <first>, <last>,
    Justification = Upstream.GaveNoReason,
    Deviation = DeviationKind.StoreExceptionAsData | DeviationKind.UpstreamCallsAnotherTest,
    DeviationNote = "EF's override calls base.<other test>.")]

// two behaviours in one theory: each reason names its case
[StoreLimit(typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_required_associate_via_optional_navigation), Case = nameof(QueryTrackingBehavior.TrackAll))]
[StoreDefect("1.6", typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_required_associate_via_optional_navigation), Case = nameof(QueryTrackingBehavior.NoTracking))]

// inside a [WireFreeControl] class: the label, and no reference, because this IS the evidence
[StoreDefect("1.6", Case = nameof(QueryTrackingBehavior.NoTracking))]
```

**Five properties exist for the audit.**

| Property | Meaning |
|---|---|
| `Justification` | upstream's comment, copied verbatim. When upstream gives no reason, the constant `Upstream.GaveNoReason`, so the gap is visible rather than blank. |
| `Skip` | the override asserts nothing. Requires an upstream reference. |
| `Deviation` | how this override's body differs from upstream's, as `DeviationKind` flags. `None` means it is the same. |
| `DeviationNote` | what a kind cannot say. Required with `Other` and with `UpstreamCallsAnotherTest`. |
| `Case` | the theory case the reason covers, such as `TrackAll`. Empty means every case of the method. |

**Every value that recurs is typed rather than text** (the owner's decision, 2026-09-15): an upstream
reference is a repository, a path and a line range; an issue is a tracker and a number; a decision is
an ADR number; a deviation is a set of kinds. Text is left only where it is somebody's words. The
gain is that a typed value can be counted and checked: the audit reports the deviations by kind, and
it opens `subrepos/efcore` and checks that each reference's lines declare the overriding test, by
name, at the pinned commit. **A CI runner has no `subrepos/`**, so there the audit counts the
references it could not check rather than checking them; a local run checks all 446.

**`Case` exists because one test method can hold two store behaviours** (added during the Tier D
trial). `Select_required_associate_via_optional_navigation` refuses in its tracked arm and crashes
with `NullReferenceException` in its untracked arm, and a crash is never a `LIMIT`. One attribute per
method could not say both, so a method may carry several reasons when each names a distinct case.

**A control class is marked `[WireFreeControl]`** (added during the Tier D trial). Its overrides carry
a label and NO reference, because the control is the evidence. They may not skip, since a control
that skips documents nothing, and they may not carry `InfoCarrierDefect`, since there is no
InfoCarrier in them. A self-hosted reference on a wire override must name a method of such a class.

**One reflection test enforces it.** It fails when an override of a specification test carries no
reason, or several without distinct `Case` values; when an upstream reference's lines do not declare the
overriding test in the checkout it names; when an upstream reference has no `Justification`; when `Skip` is set without an
upstream reference; when a `StoreDefect` names a section that `docs/upstream-defects.md` does not
have; when a control override names a reference or skips; and when a wire override's control test
documents a different label, or a different section or key, for the same case; and when a `DESIGN`
names a decision heading that does not exist, or gives no `Justification`. **An override of an
abstract test is not audited**, because the base has no expectation to change. That is Microsoft's
`Check_all_tests_overridden` made stricter: theirs proves somebody looked at a test, ours proves what
the store did and where that is shown.

**The same test writes the audit.** Its output lists every override with its label, its reference,
whether it skips, its deviation, and every upstream reference that gave no reason. The counts are
the answer to "how much does this suite not check, and why".

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

**Done 2026-09-15.** Tier D is 234 tests and all of them pass. `OverrideAuditTest` lists 55
reasons: `LIMIT` 34, `DEFECT` 16, `ISSUE` 5, no InfoCarrier defect, no skip, five deviations. Each
of its rules was shown to fail on a deliberate mistake before it was trusted. The steps as they
were planned:

1. Add the four attributes — `StoreLimit`, `StoreDefect`, `StoreIssue`, `InfoCarrierDefect` — and
   the reflection test.
2. Give each of the 15 control tests that still fail a real assertion. No skip is available,
   because MongoDB's suite has no such test to copy. Where the base's own assertion fails, assert
   that failure together with the store's text inside its message, which pins the behaviour. Write
   the query out only where the message holds no store text, such as "No exception was thrown".
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

## Tiers A, B and C

**Converted 2026-09-15.** `OverrideAuditTest` in the spec project lists 453 reasons over 449
overrides: `LIMIT` 344, `DEFECT` 23, `ISSUE` 69, `DESIGN` 17, no InfoCarrier defect. 57 skip, 92
deviate, and upstream gave no reason for 310. **Those were the figures when the conversion was
committed; after the reds and the defect review the same day they are 466 reasons: `LIMIT` 345,
`DEFECT` 24, `ISSUE` 69, `DESIGN` 26, `INFOCARRIER DEFECT` 2.** Tier C has no override. The rules added for `DESIGN`
were shown to fail on a deliberate mistake before they were trusted.

**425 of them were mechanical.** A Roslyn parse of this repository and of `subrepos/efcore` at
`v10.0.1` matched each override to EF's override of the same test in the SQLite or InMemory
functional tests, and compared the bodies with comments and `AssertSql` removed. Where they differed
only by a helper that asserts the same thing, or by `!` and `var`, the body counts as the same. The
label came from EF's own words: an issue number in EF's comment is an `ISSUE`, a crash EF pins is a
`DEFECT`, and the rest are `LIMIT`. Two deviations recur and are constants: EF also asserts SQL,
which this client does not emit (tracked as #111), and a store exception crosses the wire as
`InfoCarrierServerException`.

**The other 24 were read one by one**, and four findings came out of them.

- **Twenty Tier A overrides pin crashes of EF's InMemory provider**, so they are `DEFECT` and not
  `LIMIT`: `docs/upstream-defects.md` §1.11.
- **Three primitive-collection overrides answer where EF's relational base asserts a refusal** that
  EF's own comment calls unfinished type-mapping inference: §1.12.
- **The client-evaluation guard was a decision recorded nowhere but CLAUDE.md.** ADR-010 now carries
  it as a dated amendment, because ten `DESIGN` references cite it.
- **Two overrides did not meet the rules and were changed.** `ConcurrencyDetectorEnabled.FromSql`
  skipped with nothing upstream to cite, and now asserts the refusal inside xUnit's failure.
  `Identifiers_are_generated_correctly` asserted a table name where EF asserts four names; EF's
  whole body passes, so it is EF's body.

**Two InfoCarrier defects came out of it, both measured and both tracked** (the owner's request,
2026-09-15, to confirm every suspected defect).

- **#52, the property-bag insert.** It was labelled `DESIGN` naming the limitations page, which
  described the page and not the failure; an issue already tracked it.
- **#113, a context leak.** `Inlined_dbcontext_is_not_leaking` expects EF's refusal of a client
  projection that calls a `DbContext` instance method. This client answers, correctly, and a context
  that ran that query stays reachable after disposal until EF's memory cache is compacted, while a
  context that ran a plain query is collected. The first probe could not see it because pooled
  contexts outlive every test; contexts built directly could. It had been labelled `DESIGN ADR-010`
  and skipped, with a justification the measurement disproved.

**Three suspected defects were measured and are not defects.** `AsSplitQuery` is honoured: the
server runs two statements where a single query runs one, so R47's "silently ignored" was wrong. A
complex collection not mapped to JSON is refused by the server's `SqliteModelValidator` with EF's
message. The table-splitting check that fails is an `IsInModel` mark while the client's model is
built, and a real client context inserts, updates and reads a split owned reference correctly. R138
was confirmed as described: the client ships an unmapped member and the server refuses it, which is
correct while client and server share one model.

## Extending to the other tiers

- **Read upstream first**: EF's InMemory and SQLite functional tests, and the Firebird provider's
  suite. Pin each link to the package version this repository runs.
- **Build a control only where upstream has none.**
- **Every existing override gets a label and a reference, or goes.** An existing
  `Task.CompletedTask` stays only if it copies an upstream skip, with the link and upstream's
  justification text. Otherwise it becomes an exact expectation, or the base test runs as it is.
  Tier B's `Distinct_projected` is the first candidate: it copies the SQLite skip above. Measured
  2026-09-15 in the spec project: 83 uses of `Task.CompletedTask`, 116 of `ThrowsAsync`, 113 of
  `AssertTranslationFailed`, 268 of `ApplyNotSupported`.
- **Pin each upstream link to the test package this repository runs.** The spec project uses EF's
  specification tests at 10.0.1, tag `v10.0.1`, commit `a6217e34`; Tier D uses them at 10.0.11.
- **Decide first what an override that calls the base unchanged and only ADDS assertions needs.** It
  changes no expected behaviour, so it is not what a label describes. Tier D has none, so the trial
  does not need the answer.
- **Then remove the ratchet**: `eng/ratchet.sh`, both baseline files, and the direction gate in CI.
  Done 2026-09-15. The CI job keeps the name `Spec ratchet` only because the `main` ruleset requires a
  check of that name.

**This reverses a LOCKED guardrail.** CLAUDE.md says *"Never `[Skip]`, delete, or override a spec
test to make the suite green"*, and ADR-004 says a red test is information. The reversal needs a
dated supersession of ADR-004, and the same commit must sweep every comment that argued for the
ratchet.
