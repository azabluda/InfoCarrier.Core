# SQL capture: every test's statements, against plain EF Core (#167)

**Status: design, agreed with the owner on 2026-09-26. Not implemented.** This document is the spec
for #167. Its decisions were taken one question at a time in a brainstorming session; each is
recorded in §2 with the alternative it beat, so that a later reader can tell a decision from a
default.

## 1. Purpose

**Goal 1: a reference for every test.** The server's SQL is compared with what plain EF Core runs
for the same test, on the same store, with the server's options. Until now the only reference was
the `AssertSql` text in EF's own provider suites, read by `eng/ef-sql-compare.sh`: it covered 785 of
8,873 Tier B test methods on 2026-09-24, it is a fragment because EF clears its log inside a test,
and it is paired with our tests through heuristics that needed repairs. The prototype on the
satellite branch `experiment/direct-baseline` ran Tier B twice, through InfoCarrier and without it,
and found in two days what #164, #165, #166, #168 and #169 fix. EF's `AssertSql` covered none of
those methods.

**Goal 2: delete what reading EF's `AssertSql` needed** (§11).

**Then: the captured statements are asserted in every normal run.** After the first round of
capture (§13, step 5), each test of Tiers B and C checks its server SQL against a committed file. A
later change in `src/` that changes a captured shape fails a test: it is a defect, or at least a
change that deserves attention. The slow mode that produces the files runs rarely (§3).

## 2. Decisions

| # | Question | Decision | The alternative it beat |
|---|---|---|---|
| 1 | What does the mode produce? | An investigation run on demand, whose output is then committed and asserted | A gate that compares with plain EF in every run |
| 2 | Where do captured statements live? | In data, one file per test class | Generated C# overrides, one per test method |
| 3 | What is recorded where InfoCarrier and plain EF differ? | Both, with a label | InfoCarrier's statements only |
| 4 | Which tiers? | B and C together from the start | B first, C later |
| 5 | Is a merge step needed? | No: each run commits its own raw files | One run writes, a merge step combines |
| 6 | Where do the files go? | Next to the test class's `.cs` file | A separate `Sql/` tree |
| 7 | Where do the labels go? | A typed attribute on the test method, in a pass-through override where the test is inherited | One central file; an attribute on the class |
| 8 | Row counts? | Recorded, never asserted, to be tried | Not recorded |
| 9 | Parallel slow-mode runs? | No: two ordinary runs, one after the other | Both runs at once from two output folders |
| 10 | The README badge? | One figure: a case counts only when its answer and its SQL both match plain EF | Two figures; a half weight for an SQL difference |

**On decision 1, and what it amends.** `docs/test-policy.md` decided on 2026-09-16 that "the
promises are ours" and that the comparison with EF's text is "an investigation rather than a gate",
after copying EF's `AssertSql` text into 580 overrides was tried and dropped the same day. That
rejection stands: the text asserted here is never EF's. It is captured from **our own runs**, it
covers **every** test rather than the ones upstream chose, and the reference beside it is plain EF on
**our** store. The amendment to `test-policy.md` records this with its date when step 4 lands.

**On decision 9.** The prototype ran its two halves at the same time and saved about 11 minutes of
about 26, and only for the rarely used `--direct`. Each run in this design is an ordinary parallel
xUnit run that already uses every core, so two at once would compete and save almost nothing.

## 3. When the slow mode runs

- EF Core changes version.
- A backing-store provider changes version: `Microsoft.Data.Sqlite`, the Firebird provider. They,
  not EF, write the SQL text.
- New specification bases are adopted.
- The test harness changes: a fix in it, or a change of the server's options (collection parameter
  mode, split-query default, compiled model).
- A product fix changes a shape on purpose, as #168 changed `SELECT 1` to `SELECT DISTINCT 1`. The
  assertion fails, as it should, and a capture with `--filter` on that class writes the new text.
  The PR diff shows the old and the new SQL.

## 4. Capture

### 4.1 Which test is running

The server executes a command deep inside EF, in an interceptor that the test class cannot reach, so
the test is carried there by an **async-local "current test"**. The in-process harness already
carries a value this way: the client side sets `InfoCarrierBackendTestStore.CurrentClientContext`
and the server side reads it when it creates its context (`InfoCarrierBackendTestStore.cs`, the
server context's creation). Step 0 proves that the new value reaches the recording interceptor in a
parallel run before anything builds on it.

**The value is the test case's display name**, the name the test explorer and the TRX show, for
example `A_compiled_query_indexing_a_list_matches_the_direct_query(mode: Parameter, list: True)`.

**xUnit v2 does not hand it to a `BeforeAfterTestAttribute`**: `Before(MethodInfo)` receives the
method and never the arguments, and v2 has no `TestContext`. EF decides the version:
`Microsoft.EntityFrameworkCore.Specification.Tests` 10.0.1 depends on `xunit.core` 2.9.3. So the
name comes from xUnit v2's supported extension point, `[assembly: TestFramework(…)]`: a subclass of
`XunitTestFramework` whose executor wraps each test case before it runs. The wrapper sets the
async-local and delegates every member, so discovery, EF's `ConditionalFact` and `ConditionalTheory`,
and the test explorer are unaffected.

**A theory whose data xUnit cannot serialize** is one test case whose rows run inside it, so the
wrapper sees only the method. For those, a `BeforeAfterTestAttribute` counts the rows, and the name
is `Method [row 3]`. Step 0 measures how many such theories Tiers B and C have.

**When EF's specification packages move to xUnit v3, `TestContext.Current.Test` replaces both the
wrapper and the row counter.** The doc comment of the wrapper says so, so that the cleanup is not
forgotten.

### 4.2 What is recorded

For each test case, every command the server runs while it is the current test, in order:

- **The command text, normalized** (§4.3). One command stays whole, because an EF batch can hold
  several SQL statements in one command. `SaveChanges`, split queries and `ExecuteUpdate` all give
  commands of their own.
- **A failure mark**, for a command that threw.
- **The number of rows**: the rows the server's EF read from a reader, counted by a wrapping
  `DbDataReader`, or the rows a non-query affected. Recorded in the file and **never asserted**
  (decision 8). A test that writes on a shared store could move a count, and a flaky assertion is
  worse than none. If a capture shows noise in them, they go.

**Not recorded:** parameter values, which vary from run to run and say nothing about the shape; the
rows themselves, which the test already asserts; and any command with no current test, such as a
fixture's seeding, which removes the prototype's "reads filed under the wrong test".

The store's `ServerSqlRecordingInterceptor` gains the tag. `ServerSqlRecorder` and the promises of
`ServerSqlTest` that assert through it stay as they are.

### 4.3 Normalization

One normalizer, in C#, in `InfoCarrier.Core.TestUtilities`. The writer and the assertion both use
it, so the file format has one reader and one writer.

- **Parameter names** become `@p0`, `@p1`, … in order of first appearance. The owner's rule:
  a name does not matter, and a parameter reaches the server inside a `ParameterBox<T>`, so EF names
  it `@Value` where the caller wrote `city`.
- **Table aliases and column aliases** get positional names the same way, so that the tuple's
  `Item1` against the anonymous type's `Title` does not differ.
- **Literals and structure stay.** A literal where EF has a parameter, or the reverse, is a defect
  (`docs/test-policy.md`, the owner's rule of 2026-09-15), and a structural change that can change
  the plan is what the comparison is for.

Tests of its own pin the normalizer. `ServerParameterizationTest.Normalize`, which does a smaller
part of this today, has none.

## 5. File format

One file per test class and per side, `<Class>.wire.sql` and `<Class>.direct.sql`:

```sql
-- Where_simple_closure(async: False)
-- Where_simple_closure(async: True)
-- #1 rows 6
SELECT "t0"."CustomerID", "t0"."Address", "t0"."City", …
FROM "Customers" AS "t0"
WHERE "t0"."City" = @p0

-- A_compiled_query_indexing_a_list_matches_the_direct_query(mode: Parameter, list: True)
-- #1 rows 1
…
```

- **Header lines name the test cases.** Test cases whose statements are exactly the same share one
  entry, which usually halves a file. The grouping follows the data and is not a rule: a test body
  that branches on `async`, a theory argument that changes the query, or a defect on one path only,
  as `ToQueryString()` was (#169), gives separate entries. When a change makes two grouped cases
  differ, the entry splits, and the diff shows it.
- **Two different cases that xUnit prints the same** (a string cut after 50 characters, two objects
  with one `ToString()`) get `#2`, `#3` in execution order, which xUnit keeps fixed within a class.
- **`-- #n rows k`** starts each command, and `-- #n failed` marks one that threw.
- **`-- direct run failed`** marks an entry in a `.direct.sql` file whose test failed in the plain-EF
  run: a test that asserts InfoCarrier's own behaviour (87 of them in the prototype). Its statements
  up to the failure are still written, and nobody takes them for a full reference.
- **Entries are sorted by test case**, so that a diff stays stable.

## 6. Where the files live

Next to the test class's `.cs` file (decision 6):

```
test/InfoCarrier.Core.FunctionalTests/
├── Sqlite/Query/
│   ├── NorthwindWhereQuerySqliteInfoCarrierTest.cs
│   ├── NorthwindWhereQuerySqliteInfoCarrierTest.wire.sql       asserted by normal runs
│   ├── NorthwindWhereQuerySqliteInfoCarrierTest.direct.sql     plain EF, the reference
│   ├── GearsOfWarQueryInfoCarrierTest.cs                 holds the TPT and TPC classes
│   ├── TPTGearsOfWarQueryInfoCarrierTest.wire.sql
│   └── TPTGearsOfWarQueryInfoCarrierTest.direct.sql
└── Firebird/…
```

- **Named by class, not by `.cs` file**, because one file can hold several test classes. A nested
  class keeps its outer class in the name.
- **The folder comes from the namespace below the project**, which in this project is the folder of
  the class's `.cs` file. A class whose namespace does not match gets its files where the namespace
  points, and the capture reports it so that it can be moved.
- **Found as `OverrideAudit.FindRepositoryFile` finds its files**, by walking up from the test
  binaries into the repository, so the files are read and written in the source tree and nothing is copied to
  `bin/`.
- **The IDE nests them under the `.cs` file** through a `DependentUpon` rule in the `.csproj`.
- **Nothing new is git-ignored.** The runs' TRX and console output go to `artifacts/sql-capture/`,
  which `artifacts/` already covers.
- **Only Tiers B and C get files.** Tier A runs no SQL, and Tier D is a project of its own whose
  `Direct*` classes already compare with plain EF by hand.

## 7. A normal run

With `INFOCARRIER_SQL_CAPTURE` unset, the `After` hook compares the test case's statements with its
entry in the class's `.wire.sql` file.

- **In order**, as EF's `AssertSql` compares, because order matters for a split query and for
  writes. A test whose order varies is a flaky test, and is treated at once (`CLAUDE.md`, "The test
  stores, and flakiness").
- **Adoption class by class.** A class with no `.wire.sql` file is not asserted. A class that has one
  must have an entry for every test case it runs, and a missing entry fails.
- **The failure message** shows the expected and the actual statements and names the command that
  captures that class again: `eng/sql-capture.sh --filter <Class>`.
- **The `.direct.sql` files take no part in the assertion.** They are the reference for people and
  for the compliance test (§9).

## 8. The slow mode

One variable, `INFOCARRIER_SQL_CAPTURE`, with two values:

| value | what the run does |
|---|---|
| `direct` | the client is plain EF Core on the server's store (§10), and each test writes its entry into `<Class>.direct.sql` |
| `wire` | the client is InfoCarrier, and each test writes its entry into `<Class>.wire.sql` |

The assertion of §7 is off in both. **`eng/sql-capture.sh [--filter X]`** runs the two, one after
the other, over Tiers B and C. They do not depend on each other, and either can run alone.

- **A test writes only its own entry**, in `After`. Only the tests of one class write that class's
  file, and xUnit runs them one after another, so no lock is needed.
- **Entries of tests that did not run stay**, so a capture with `--filter` rewrites only what it ran.
  An entry whose test no longer exists is caught by the compliance test (§9).
- **The report is the diff.** `git diff` shows every entry that changed, appeared or went away.
  `git diff --no-index` between a class's two files shows every difference from plain EF, with both
  sides' row counts.

## 9. Labels, and the compliance tests

A difference between a test case's `.wire.sql` and `.direct.sql` entries is labelled by a typed
attribute on the test method, the same system as the override reasons of `docs/test-policy.md`
(decision 7):

```csharp
[SqlDeviation(SqlLabel.OpenDefect, Reason = "the Distinct stays above the client-side rebuild")]
public override Task Select_DTO_distinct_translated_to_server(bool async)
    => base.Select_DTO_distinct_translated_to_server(async);
```

| label | meaning |
|---|---|
| `SqlLabel.OpenDefect` | InfoCarrier runs something worse than plain EF, and a fix is owed |
| `SqlLabel.AcceptedDeviation` | a difference that runs no dangerous SQL, with the reason it is accepted |
| no attribute | **unread**: a difference nobody has decided on yet |

- **A test inherited from EF's base gets a pass-through override** to carry the attribute, because
  an attribute needs the method declared in our class. A test already overridden for a behaviour
  reason gets the attribute beside that reason. `OverrideAudit` counts `SqlDeviation` as a reason of
  its own kind, so its rule "every override has a reason" holds with no exemption, and it lists every
  SQL label with the overrides.
- **`Case` narrows a label to some test cases**, as it does for the existing reasons:
  `Case = "async: True"`.
- **The compiler checks the method name**, and a pass-through override goes away when its difference
  does.
- **The capture never edits C#.** A new difference enters the repository unread, and not silently:
  the `.sql` diff in its PR shows it, the audit lists it, and the badge turns yellow (§12). A
  compliance setting can forbid unread differences once the first round is done.

Two compliance tests run in every normal run:

- **Every `SqlDeviation` names a real difference.** When a fix removes a difference, the test fails
  until its attribute, and with it a pass-through override, is deleted, so the labels cannot go
  stale.
- **Every entry names a method that exists on its class.** An entry left behind by a test that EF
  renamed or deleted fails it. It works like `InfoCarrierComplianceTest`.

With `INFOCARRIER_OVERRIDE_REASONS` set, the first of them also writes every test case whose two
entries differ, with its label or none, to `<assembly>.sql-differences.tsv` beside
`override-reasons.tsv`. That list is the overview of every open gap with plain EF, as
`test/known-failures.txt` once listed every failing test, and the badge reads it (§12). One C#
reader of the `.sql` format serves the assertion, the compliance tests and the badge.

## 12. The README badge

The badge keeps one figure, and it now counts statements as well as answers (decision 10).
`eng/spec-parity.py` scores each test case that ran through InfoCarrier:

- **1** when no `[InfoCarrierDesign]` or `[InfoCarrierDefect]` reason covers it **and**, in Tiers B
  and C, its `.wire.sql` entry is exactly its `.direct.sql` entry;
- **0** otherwise.

The figure is the mean, rounded down as today.

- **The label does not change the score.** An open defect, an accepted deviation and an unread
  difference all count 0. That is the rule `spec-parity.py` already states, "relabelling a defect as
  a design lowers it just the same", so no relabel can raise the figure.
- **The colour:** red when the suite fails; yellow while an `[InfoCarrierDefect]` override, an
  `SqlLabel.OpenDefect` or an unread difference exists; green otherwise.
- **Published only after the first round** (§13, step 6). Before it, the SQL half would cover only
  the adopted classes, and the figure would read better than the truth.
- **A rough estimate from the prototype:** about 400 differing cases of about 29,600 would take the
  figure from 99.86% to about 98.5%. Normalization will remove some text differences, so the real
  figure should be higher.

## 10. The plain-EF client

The prototype's plumbing comes to `main` from the satellite branch's first experiment commits
(`10b4873`, and the warning settings of `d6c2ffa`):

- `InfoCarrierBackendTestStore.AddDirectClientOptions` and a shared `DirectClientConnection`, so that
  one context can enlist in another's transaction, as EF's own relational test stores share one
  connection;
- `InfoCarrierTier.AddDirectClientServices`;
- `DirectClient.UseTestTransaction` in the overrides that enlist in a transaction, about 30 of them;
- EF's own SQLite warning settings, without which 2,676 tests of a plain-EF run failed on a warning
  before they ran a statement.

`INFOCARRIER_SQL_CAPTURE=direct` switches it on, and `INFOCARRIER_DIRECT_CLIENT` goes away.

**`FirebirdInfoCarrierBackendTestStore` gets the same overrides.** The plain-EF client uses the
server's options, and those include `FirebirdLateralQuerySqlGenerator`. Tier C's reference is
therefore plain EF plus the one correction that Firebird needs to parse the statement at all
(`CLAUDE.md`, "Two provider defects this repository works around").

## 11. What is deleted, and the documents

**Deleted:**

- `eng/ef-sql-compare.sh`, and `eng/ef-sql-diff.py` whole: the half that reads EF's source and pairs
  it with ours (`ef_expected`, `key`, `match_case`, `extras`, `mapping_control`, …), and the half that
  reads a log, which the C# capture replaces;
- `ServerSqlTestMarkerAttribute` and `ServerSqlLogAssemblyInfo.cs`;
- `ServerSqlLog`, `ServerSqlLogInterceptor`, `ServerSqlLogTest` and `INFOCARRIER_SERVER_SQL`. They are
  older than #111 and were a diagnostic, and a `wire` capture with `--filter` gives the same
  statements, already per test;
- on the satellite branch, `eng/direct-compare.py`, `eng/direct-remeasure.sh` and
  `eng/direct-baseline/`, and then the branch itself.

**`DeviationKind.SqlNotAsserted`** means "upstream also asserts the SQL here, and this override leaves
it out". 65 overrides carry it. When a class is adopted, its `.wire.sql` file asserts the SQL of
every test, so the flag goes from that class's overrides, and the enum member goes with the last
class. **No override exists for SQL today**: there are 480 (measured 2026-09-26, 434 in Tier B), each
for a behaviour reason. The labels of §9 add a pass-through override for each labelled test that is
inherited, and each goes away with its difference.

**Kept:** `ServerSqlTest` and `ServerParameterizationTest`, our named promises, and
`ServerSqlRecorder`, through which the first asserts.

**Documents**, in the commit that deletes the scripts:

- `docs/test-policy.md`: a dated amendment for decision 1, and the sections on `ef-sql-compare.sh`
  kept as a record of what the text comparison found;
- `CLAUDE.md`: the `eng/` table, and the paragraph "The SQL the server runs is asserted in our own
  words";
- a sweep for every comment that argues against golden text, per `CLAUDE.md`'s rule for a reversal:
  correct the reason, quote what it said with the date.

## 13. Order of work

0. **Spike.** Prove that the current test reaches `ServerSqlRecordingInterceptor` in a parallel run,
   through the test-framework wrapper. Count the theories of Tiers B and C whose data xUnit cannot
   serialize. Nothing builds on §4.1 before this step says it holds.
1. **Capture.** The wrapper and the row counter, the tagged recorder with row counts, the normalizer
   and its tests, the file reader and writer, the assertion in `After`, and both compliance tests.
   No class is adopted yet, so a normal run asserts nothing new.
2. **The plain-EF client on `main`**, for Tiers B and C (§10).
3. **`eng/sql-capture.sh`, and one class adopted from end to end**: `NorthwindWhereQuerySqliteInfoCarrierTest`.
4. **The deletions and the documents** (§11).
5. **The first round:** every class of Tiers B and C. The prototype's list says about 200 methods
   differ, so they enter unlabelled, which is unread, and each defect found then goes to its own PR,
   with its own differential test, as this session's did.
6. **The badge** (§12): `spec-parity.py` joins `sql-differences.tsv`, and the badge publishes the
   one figure.

## 14. Open points and risks

- **The async-local may not reach the interceptor** on some path, for example a transport that queues
  work on a thread of its own. Step 0 exists for this. The fallback is the prototype's serial run
  with a marker per test, which is known to work.
- **Theories with data xUnit cannot serialize** get `[row n]` names, which are stable but not
  readable. If step 0 finds many, a custom theory discoverer could split them.
- **Statements issued by a test class's constructor** run before the wrapper's value is set and are
  not recorded. EF's test classes seldom query there. If one does, the statement is missing from
  both sides alike.
- **`ServerSqlTest` and `ServerParameterizationTest` would be captured too**, and their SQL then
  asserted twice. Harmless, and it can be excluded later if it is only noise.
- **File size.** Most classes will be the same on both sides, and git stores an identical file once.
  The rest is a few MB of text, which compresses well.
