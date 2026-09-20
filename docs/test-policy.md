# Test policy: every override points to evidence, and every promise is ours

**This is the standing policy for this repository's tests.** It was `docs/plans/v10/test-overhaul.md`
until 2026-09-16, when the name stopped matching the file: an overhaul is an event, and this is the
rule the suite runs on. Nothing about the rule changed with the move.

**Status of the overhaul that produced it: done, 2026-09-15.** Every tier is converted, the suite is green (`FAILING: 0  TOTAL:
29792`), and the ratchet is gone: `eng/ratchet.sh` and both baseline files are deleted, and
`eng/suite-summary.sh` reports the counts in CI. ADR-004 carries the dated amendment, and the CI job
and the ruleset's required check are renamed from `Spec ratchet` to `Spec tests`.

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
  EF's own comment calls unfinished type-mapping inference: §1.12. Two since the same evening, and
  why is measured: see the next section.
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

## Two sides, EF parity, and a literal that was a defect

**Decided by the owner on 2026-09-15, after the conversion.**

- **An override can carry a reason from each side.** A store reason says what EF's own provider
  test does and links it; an InfoCarrier reason says why this test differs from that one. At most
  one store reason covers a case, and InfoCarrier reasons can be several, each naming a different
  decision or issue. `AnswerNotRefusal` and `RefusedEarlier` are legal only on an InfoCarrier
  reason, so that a difference of this provider's is never counted as the store's. Four overrides
  carry both: the two `Join_with_result_selector_returning_queryable_throws_validation_error`, whose
  one `DESIGN` reason had lost the link to EF's `ApplyNotSupported` override, and the two
  primitive-collection compiled queries below. Each of the three new rules was shown to fail on a
  deliberate mistake.
- **`StoreExceptionAsData` stays a mechanical deviation.** The store refuses at the same place with
  the same message, and only the exception's type differs. One decision covers every server error:
  `InfoCarrierFaultMapper` rebuilds a type only through an ordinary constructor, and
  `docs/security-review.md` §7 records why. Labelling it per test would count only the tests whose
  upstream assertion happens to name the exact type.
- **Remapping store exceptions to a type derived from `DbException` was considered and declined.**
  A caller could then catch `DbException` as with plain EF, but code written against the 10.1
  errors guide would stop matching, and no deviation would go, because EF's assertions name the
  exact driver type.
- **The README badge shows EF parity**, which `eng/spec-parity.py` computes from the TRX and the
  reasons `OverrideAudit` writes. The base is every test case that ran through InfoCarrier: a
  wire-free control and an xUnit skip are left out. A case counts against parity when an
  InfoCarrier reason covers it, and for it otherwise, so only a difference of this provider's lowers
  the figure, and relabelling a defect as a design lowers it just the same. It is rounded down. The
  colour is red when a test step fails, yellow while an `[InfoCarrierDefect]` override exists, and
  green otherwise. `passed / total passing` had become a constant when the suite went green. The
  first run on the older TRX read 99.81%, 54 of 29,445 cases.
- **SQL assertions (#111) are deferred.** The proposal for when they come: an `[UpstreamOverride]`
  marker for an override copied from EF without change, whose body the audit can compare with
  upstream's lines, and a `SqlDiffers` deviation on an InfoCarrier reason where the server's SQL
  differs from EF's. **The owner's rule for the comparison**: parameter names do not matter; a
  structural difference that can change the store's execution plan is a red flag; and a parameter
  replaced by its literal values is a defect. **And a literal replaced by a parameter is as bad**
  (the owner, later the same evening): both change the plan the caller's LINQ asked EF for, one by
  parsing again for every value and flooding the plan cache, the other by hiding the value from the
  optimizer. **A deviation from EF is still legitimate when no dangerous SQL runs on the server and
  what the caller sees is right**, which is the owner's answer for the parameterized-collection mode
  below.

**That last rule found a defect the same evening.** Measuring why the §1.12 overrides answer, with
the server's SQL log, showed `WHERE "p"."Ints" = '[1,10]'` for
`Column_collection_equality_inline_collection_with_parameters`: two query parameters, sent as one
literal. The cause was the one exception `Substitute` made to #59's rule, which kept a scalar inside
`new[] { i, j }` a plain constant so that this query could answer, and which sent
`new[] { i, j }.Contains(p.Id)` as `IN (2, 999)` where EF's own client sends `IN (@i, @j)`. The
exception is gone. The elements stay parameters, `IN (@Value, @Value0)`, and the equality query is
refused with EF's own message, so its override is deleted and EF's relational base holds.
`ServerParameterizationTest.An_inline_collection_of_parameters_matches_the_direct_query` pins it,
and fails on the old code with `IN (1, 3)`.

**The other two answer for a reason that is ADR-006 and not a defect.** The client sends a compiled
query's parameters as values, so the server's EF evaluates the expressions built on them before
translation: it ran `WHERE "p"."String" = @p` and `WHERE @p`, each value still a parameter. They
carry `[StoreDefect("1.12")]` and `[InfoCarrierDesign(6)]`.

**The audit after all of it**: 469 reasons, `LIMIT` 347, `DEFECT` 23, `ISSUE` 69, `DESIGN` 28,
`INFOCARRIER DEFECT` 2, and 4 overrides with a reason from each side.

## The server's SQL, compared with EF's test by test

**Measured 2026-09-15 on `main` at `f588004`.** Tier B ran serially with an xUnit
`BeforeAfterTest` attribute writing each test's name into `server-sql.log`, and a deterministic
script compared every test's statements with the `AssertSql` text of the same test in
`EFCore.Sqlite.FunctionalTests` at `v10.0.1`: whitespace, parameter names, aliases and alias
qualifiers ignored, literals and structure kept. **Both are committed since 2026-09-16** —
`ServerSqlTestMarkerAttribute` beside `ServerSqlLog`, and `eng/ef-sql-diff.py` — because they found
two lost updates in one run, and because #111's own progress is what they measure:

```
INFOCARRIER_SERVER_SQL=1 dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj \
    --filter "FullyQualifiedName~InfoCarrier.Core.FunctionalTests.Sqlite" -- xUnit.ParallelizeTestCollections=false
eng/ef-sql-diff.py test/InfoCarrier.Core.FunctionalTests/bin/Debug/net10.0/server-sql.log
``` 791 tests were paired, and 758 produced EF's statements
exactly. **No statement had a literal where EF has a parameter.** A first, shape-only pass over the
parallel run's log found the same, and was shown to catch the inline-collection defect by planting
it back into a copy of the log.

**Fixed on the branch that found them:**

- **An owned reference in its owner's table lost its concurrency tokens.** EF checks the tokens of
  every tracked entry that shares the row being written; the client sent only what changed, so the
  server's `UPDATE` checked the key alone and overwrote a row someone else had changed. `Expand`
  now sends the row-mates that carry a token, as `Unchanged`, on a relational store. EF's
  `Property_entry_original_value_is_set` statement now matches, and
  `ConcurrencyTokenTest.A_stale_write_is_refused_when_the_token_is_in_an_owned_reference` failed
  before the fix. The same test for a complex property passed before and after.
- **A write set every complex member and every JSON column.** `ModifiedProperties` names only
  `GetProperties()`, so the server left complex members modified: a change to `Name` wrote
  `SET "Stops", "Name", "Destination_City", "Destination_Street"`, and a concurrent change to those
  columns was overwritten. `ChangeEntry.ModifiedComplexProperties` now names the changed members, and
  a `null` from an older client keeps the old behaviour. All 19 `ComplexCollectionJsonUpdate`
  statements were this; `PartialUpdateTest` failed on both mappings before the fix. **The first
  version of the fix broke six tests, and the reason is a trap in EF's API**: the public
  `ComplexCollectionEntry.IsModified` setter recurses through every complex element of the entity,
  not only its own, and each element reports back to its own collection, so clearing `Employees`
  cleared `Contacts` and setting `Contacts` set `Employees`. The server now clears one collection
  through its own elements, before the scalar pass, which keeps EF from turning the entry unchanged.
  After the fix the per-test comparison of those two classes found all 19 paired tests identical to
  EF, the `Engines` concurrency statement included.

- **A terminal `First()` above a client-side projection ran without EF's `LIMIT`.** The operator
  consumes the rows the client reassembles, so it cannot ship, and nothing told the server how many
  rows were wanted: every matching row crossed the wire for one row of answer.
  `QuerySplitter.WithRowLimitForTerminalOperator` now sends the limit with the shipped query, as an
  inline constant so that it lands as `LIMIT 1` exactly as EF writes it rather than as a parameter.
  `Single` sends `LIMIT 2`, because one row cannot show that a second exists, and the client still
  raises EF's own exceptions. Only a reassembled projection was affected: a scalar projection, an
  entity query and a `Take` the caller wrote were already right, measured both ways. Three
  differential cases pin it, and the trim baseline rises by one for the `MakeGenericMethod` the limit
  needs.

**Red flags not yet fixed, which are design questions:**

- **An operator the type boundary leaves behind runs on the client over whole tables.**
  `NullSemantics.Join_uses_csharp_semantics_for_anon_objects` (a join on an anonymous key),
  `CustomConverters.Value_conversion_is_appropriately_used_for_left_join_condition` and
  `NorthwindGroupBy.Odata_groupby_empty_key` each read entire tables where EF runs one statement.
  `QuerySplitter.RejectClientEvaluation` lets these through on purpose, because the operator is
  translatable and only this provider's boundary stopped it.

**After the fixes, a fresh serial run compared 791 paired tests again: 777 identical**, up from 758.
Of the 14 that remain, five are refused queries the log cannot show, three are the client-side
operators above, two are the ADR-006 compiled queries, two are the mode deviation below, one is EF's
own `IsNullOrEmpty` quirk, and one is `HasFlag`, which the pushdown fixed after that run.

**A legitimate deviation**: `Check_inlined_constants_redacting` asks for
`ParameterTranslationMode.Constant`, which the client has no builder to carry, so the server sends
`IN (@Value1, @Value2, @Value3)` where EF sends `IN (1, 2, 3)`. No dangerous SQL, and the caller's
rows are right.

**Not findings**: five refused queries (the log records only commands that ran), EF's own
`IsNullOrEmpty` override calling `IsNullOrWhiteSpace`, `Where_subquery_expression` (a direct run
showed EF also sends two statements), and the ADR-006 evaluation of a compiled query's parameters,
which stay parameters. `Contains_over_concatenated_parameter_and_constant`, for which EF's suite
asserts no SQL, is now a case of `ServerParameterizationTest` and matches.

## Asserting the server's SQL (#111)

**The suite's own promises, and an investigation that finds new ones.** Two things, and the split
between them is the whole design (the owner, 2026-09-16).

### The promises are ours: `Sqlite/ServerSqlTest.cs`

Twenty-one tests, each named as a statement about this provider, each with our own model, our own
query and our own expected text:

- a filter, a projection, an aggregate and a grouping run on the server;
- a `First` or a `Single` above a projection this client reassembles still bounds the rows there;
- paging sends its limit and offset;
- an `Include` is one statement with a join, and a split query stays two;
- a captured collection reaches the store as parameters, not as its values;
- a query the server cannot run is refused and runs no statement at all;
- a write touches the columns the client changed, carries every concurrency token of the row, and
  leaves a JSON collection alone when nobody changed it;
- `ExecuteUpdate` and `ExecuteDelete` run one statement and read nothing first.

**Each was shown to fail before it was trusted.** Reverting the four fixes of 2026-09-15/16 one at a
time turns exactly the promises about them red: the pushdown fix (2 tests), the inline-collection fix
(1), the owned-row token fix (5) and the partial complex write fix (5).

**Why its own model rather than Northwind.** The expected text has to be readable, and a Northwind
row prints eleven columns before the interesting clause. Northwind is also upstream's schema, so a
change of theirs would rewrite what our tests expect, and it has no concurrency token, no complex
value and no JSON collection, which half the promises are about.

**Where a promise is a relation rather than a construct, it goes in `ServerParameterizationTest`**,
which runs the same query over the wire and directly against the server and compares the two. That
one needs no text at all and never churns.

### The investigation is `eng/ef-sql-compare.sh`

EF's specification suite is thousands of little users of this provider. They report a wrong answer
loudly and say nothing about a full table crossing the wire. The comparison makes that silent half
visible: one command, no checkout, nothing left behind. It reads the EF version from
`Directory.Packages.props`, fetches that tag if `subrepos/efcore` is not already at it, runs Tier B
serially with the server SQL log on, and prints the disagreements grouped by kind.

**It is not a gate and not a quality claim.** Each difference it reports is read once and ends as:

- **our defect** — fix it, and pin the promise in `ServerSqlTest`;
- **a deviation we accept** — pin that in `ServerSqlTest` too, with the reason beside it, because a
  decision recorded in a test is checked on every run and a decision recorded in a file is not;
- **an artifact of EF's own harness** — nothing to write.

**What the run of 2026-09-16 says**, on the whole tier: **791 tests paired, 786 identical, 5
differing** (PARAMETER 1, STRUCTURAL 5; one test is in both). It read 784 and 7 before the join-key
rewrite below. What is left is two groups and one upstream accident:

- **One test where an operator the type boundary leaves on the client reads a whole table**:
  `NorthwindGroupBy.Odata_groupby_empty_key` reads every order and groups here, because its group key
  is a class the caller declared, with its own `Equals`. Nothing can rewrite that, so the decision it
  needs is the owner's: refuse, or keep and document.
- **Two tests that used to be in this group are fixed.**
  `CustomConverters.Value_conversion_is_appropriately_used_for_left_join_condition` and
  `NullSemantics.Join_uses_csharp_semantics_for_anon_objects` each ran two full-table reads where EF
  runs one join, because a composite key is written `new { … }` and that type is the caller's.
  `JoinKeyRewriter` gives the key a `Tuple<…>` the server already accepts, and both now run EF's own
  statement (`docs/projection-split.md` §3.3a). `ServerSqlTest` pins it.
- **Two compiled queries over a parameter collection are FIXED (2026-09-17)**, and the reading that
  called them a deviation was wrong. ADR-006's capture substituted the execution's values, so the
  SERVER's funcletizer folded an operator EF had deliberately kept symbolic: the statement's shape
  then changed with the number of values, and a compiled query lost the one thing compiling buys.
  `SubstituteParametersExpressionVisitor` now marks such a collection with EF's own
  `EF.MultipleParameters`, which names EF's DEFAULT mode, so the server writes the statement EF's own
  client writes. **An ordinary query is untouched**, because EF folds such an operator itself before
  this client sees the tree, and the measurement that shows both is in `ServerSqlTest`.
- **One test where EF's `ParameterTranslationMode` does not cross the wire**
  (`AdHocMiscellaneous.Check_inlined_constants_redacting`): the caller asked for constants and got
  parameters. Accepted by the owner on 2026-09-15 as a legitimate deviation, because no dangerous
  SQL runs and the behaviour is right.
- **One is EF's own test bug**: `StringTranslationsSqliteTest.IsNullOrEmpty` calls
  `base.IsNullOrWhiteSpace()` and asserts that statement. Nothing of ours to write.

### The extras, read case by case (2026-09-20)

**The comparison drops most of what the server ran, and that remainder had been dismissed by
sampling.** EF clears its baseline inside the test, so what EF asserts is a fragment; our marker
covers the whole test. Matching EF's statements in order inside ours made the report readable
(#119), and it left every unmatched statement of ours unreported and, after that rewrite, uncounted.
The category was about 176 statements at the time and was classified by looking at a few.

`eng/ef-sql-diff.py --extras` reads it. The statements are grouped by SQL **shape**, so one shape is
read once however many tests run it, every extra belongs to a group, and the groups are ordered
unbounded reads first, because a table crossing the wire is what this instrument exists to find.
`--survey` is the same reading with nothing subtracted, for a tier upstream gives no baseline for.

**The whole remainder, on the run of 2026-09-20: 2683 statements in 280 shapes** — 4 unbounded
reads, 60 other reads, 216 writes (196 `INSERT`, 20 `UPDATE`, no `DELETE`). Read group by group:

- **No write is unbounded.** All 20 `UPDATE` shapes were printed and each carries a key predicate,
  simple or composite. There is no `DELETE` at all.
- **Three of the four unbounded reads are the test's own.** `Set<Kiwi>()` and `Set<Coke>()` are read
  by EF's `BulkUpdatesAsserter`, which runs the query once before the `ExecuteUpdate` and once after
  it, and the SQL carries no predicate because the test's query has none. The third is the
  projection `StringTranslations.IsNullOrEmpty` asks for.
- **The fourth is ours, and it is already known**: `NorthwindGroupBy.Odata_groupby_empty_key` reads
  every order, which is the deferred group-key case above. **The extras found it independently**,
  through a channel that does not use EF's assertion at all, which is the evidence that the channel
  works.
- **The 60 other reads are the same before-and-after reads carrying the test's own predicate**, plus
  `SELECT EXISTS (SELECT 1 FROM "T")`, which the log shows running BEFORE a fixture's seeding
  inserts, plus the two statements of the accepted `Check_inlined_constants_redacting` deviation.
- **The repeats are seeding.** 65 shapes run five or more times inside one test; 60 are the
  fixture's `INSERT`s and 5 are the `UPDATE`s that wire up its optional and self references
  afterwards, each keyed.
- **The writes that carry literals rather than parameters are EF's own**, emitted on the server by
  its seeding of `HasData` rows, one batch with `SELECT changes()` between the statements. Nothing
  of that crosses this wire.

**So the sampling verdict holds, and it is now a reading rather than a sample.** The remainder
contains one thing of ours, and it was already on the list. Re-run it with
`bash eng/ef-sql-compare.sh --keep` and then `--extras` on the log it names.

### Tier C has no upstream SQL, so it is surveyed instead (2026-09-20)

**The Firebird provider asserts no SQL anywhere.** At `EFCore-13.0.0.0`, the tag matching the
`FirebirdSql.EntityFrameworkCore.Firebird` version this repository runs, its functional test project
is 158 files and not one of them calls `AssertSql`. `TestSqlLoggerFactory` appears in 38 of them and
only to `Clear()` it or to point it at the test output; the overrides assert behaviour, with
`AssertTranslationFailed`; and the three files that contain a raw string block are two fixtures and
one ad-hoc query. **So the comparison that found four defects on Tier B cannot run here at all**,
and that is a fact about upstream rather than about this repository.

What the tier gets instead is `eng/ef-sql-diff.py --survey`, which groups every statement in a log
and needs no reference:

```bash
INFOCARRIER_SERVER_SQL=<file> dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj     --configuration Release --filter "FullyQualifiedName~InfoCarrier.Core.FunctionalTests.Firebird"     -- xUnit.ParallelizeTestCollections=false
python eng/ef-sql-diff.py <file> --survey --limit 100
```

**The run of 2026-09-20: 109 passed, 1 skipped, six seconds, and 106 statements in 57 shapes** — 10
unbounded reads, 45 other reads, 2 writes. Read group by group:

- **Every unbounded read is the test's own, and nine of the ten are one test's expected value.**
  `Udf_with_argument_being_comparison_of_nullable_columns` computes what it expects with
  `from a in context.Addresses.ToList() from r in context.Orders.ToList()`, which reads `Orders`
  once per address before the query under test runs. Eight addresses, eight reads, and EF calls
  `ClearLog()` immediately after them. The rest are queries a test asks for outright, three of them
  this repository's own smoke tests.
- **The 45 other reads are the capability this tier exists for, in SQL**: `JOIN LATERAL` over a
  table-valued function, `CROSS JOIN` with one, scalar functions in the projection, in a `WHERE`
  and nested inside each other, and `ROWS (n)` where a row limit is asked for. Nothing came back to
  be finished on the client.
- **Two writes, both parameterized**, from the smoke test. No statement changes rows without a
  predicate.
- **The one skip is EF's own.** `QF_Select_Direct_In_Anonymous_distinct` is skipped upstream and
  this repository declares no override of it, so nothing here asserts nothing.

**A survey is weaker evidence than a comparison and is not a substitute for one.** It says what the
server ran, not what a second provider would have run for the same query, so it can show a whole
table crossing the wire and cannot show a subtly worse plan. It is what this tier can have.

**And the first reading of that run was mostly the instrument, which is the lesson worth keeping.**
It reported 182 differing, and two defects of the tool accounted for 175 of them:

- **A statement that FAILED was invisible**, because the parser read only `Executed DbCommand`. Every
  `[StoreLimit]` test asserts a statement that fails, so each looked like a statement never run.
- **The window was wrong.** EF clears its baseline inside the test, so what EF asserts is a fragment;
  our marker covers the whole test, arrange and verify included. Matching EF's statements IN ORDER
  inside ours compares what can be compared and drops what cannot.

**A third defect was in the harness and the promises found it**: `DbContextOptionsBuilder.LogTo`
keeps ONE sink, so the log and the recorder could not both use it — switching the log on emptied the
recorder and every promise failed in that run alone. The recorder is an interceptor now, and
`AddInterceptors` appends.

**What this is NOT any more.** Copying EF's `AssertSql` text into overrides here was tried and
dropped on 2026-09-16, with 580 of them generated and green. Three reasons: the set was never
complete (580 of 798, and the 218 left out were precisely the tests where our SQL differs, which is
where the questions are), the scenario belongs to upstream so we would own the string and not the
query, and a wall of golden text argues quietly for conformance every time it goes red. The
`[UpstreamOverride]` label that supported it is deleted.

## Extending to the other tiers

- **Read upstream first**: EF's InMemory and SQLite functional tests, and the Firebird provider's
  suite. Pin each link to the package version this repository runs. **The Firebird suite was read on
  2026-09-20 and asserts no SQL at all**, so Tier C is surveyed rather than compared (above).
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
  does not need the answer. The proposal is `[UpstreamOverride]`, deferred with #111 (above).
- **Then remove the ratchet**: `eng/ratchet.sh`, both baseline files, and the direction gate in CI.
  Done 2026-09-15, and the job is `Spec tests` in the workflow and the `main` ruleset alike.

**This reverses a LOCKED guardrail.** CLAUDE.md said *"Never `[Skip]`, delete, or override a spec
test to make the suite green"*, and the rule behind ADR-004 was that a red test is information. ADR-004
carries the dated amendment since 2026-09-15, and the comments that argued for the ratchet were swept
in the commit that removed it.
