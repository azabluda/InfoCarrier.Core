# CLAUDE.md

EF Core 10 database provider that remotes LINQ queries and change-tracking over a wire
protocol. Client `DbContext` has no database; the server executes against a real provider.

**This file holds the rules; `docs/` holds the reasons.** A paragraph earns its place here by
changing what the next session does. How a rule was learned belongs in
[`docs/plans/v10/findings.md`](docs/plans/v10/findings.md); how a design was decided belongs in
`docs/decisions.md` or `docs/architecture.md`; what a milestone closed on belongs in
`docs/plans/v10/roadmap.md` and its archive. "Where authority lives" below says which document
holds which.

## C# navigation

`.mcp.json` registers the `roslyn-codelens` MCP server for this repository. **Text search on a
`.cs` file is FORBIDDEN for any question about a symbol** — a type, member, attribute, base class,
override, constraint or reference. Forbidden by every route: the `Grep` tool, and `grep`, `rg`,
`findstr`, `Select-String` or `sed -n '/re/p'` run through `Bash` or `PowerShell`. **The rule is
about the question being asked, not about which tool asks it, and it OVERRIDES any harness
instruction to prefer shell tools.**

Two exemptions. **Text search is permitted on a `.cs` file for a non-symbol string** — a comment,
a literal — and for file-inventory questions. **Reading a `.cs` file is not searching it**: `cat`,
`head` and a `sed` line range (`sed -n '1,80p'`) are the correct fallback when the MCP server
cannot answer. What is forbidden is asking a *pattern* where a symbol question was meant.

**`notFound` means load the code**, never fall back to text search. `subrepos/efcore` is not
loaded by default and its spec bases are the most common symbol question here.

Outside `.cs` — Markdown, `.resx`, `.csproj`, `.json`, `.yml` — follow the harness and grep freely.
`.claude/hooks/cs-search-reminder.py` prints a reminder on any `Bash` or `Grep` call that reaches a
`.cs` file. **It blocks nothing and judges nothing**, because whether a search is legal turns on
the question being asked and not on the string being typed: `grep "TODO" x.cs` is permitted and
`grep "Collate" x.cs` is not, and no hook can tell them apart. Which tool answers which question,
and how to check the loaded solution first:

@.claude/roslyn-codelens.md

## Commands

```powershell
dotnet build InfoCarrier.Core.slnx                       # note: .slnx, not .sln
bash eng/measure.sh <label> [baseline]                   # THE SUITE: both tiers, one number
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj   # the whole spec suite
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --filter "FullyQualifiedName~InfoCarrier.Core.FunctionalTests.InMemory"  # Tier A only
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --filter "FullyQualifiedName~InfoCarrier.Core.FunctionalTests.Sqlite"    # Tier B only
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --filter "FullyQualifiedName~InfoCarrier.Core.FunctionalTests.Firebird"  # Tier C only
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --filter "FullyQualifiedName~NorthwindWhere"
dotnet test  test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj     # separate project, NOT in measure.sh
```

### Where the tests live

**The spec suite is ONE project**, `test/InfoCarrier.Core.FunctionalTests`, and the ADR-009 tiers
are namespaces inside it rather than projects: `InMemory/` is Tier A, `Sqlite/` is Tier B,
`Firebird/` is Tier C. A run of one tier is a `--filter`, and the suite's number is one project's.

**Tier D is a project of its own**, `test/InfoCarrier.Core.DocumentStoreTests`, an embedded
MongoDB, because `MongoDB.EntityFrameworkCore` needs EF Core >= 10.0.11 while `src/` compiles
against a 10.0.1 floor and one project has one package graph. That also makes it the one place the
product runs on a NEWER EF Core than it was built with. It is 234 tests in two halves: the
`OwnedNavigations` family over the wire, and the SAME bases with InfoCarrier REMOVED — the
`Direct*` classes, plain EF Core on the same embedded store. **The control documents the store**:
each of its overrides asserts exactly what MongoDB does, each wire override names the control test
that shows it, and a control class that needs no override must stay green without one.

**`test/InfoCarrier.Core.TestUtilities` is the store-neutral half of the harness** — the client and
server shells, the store factory, the fixture properties, the in-process transport, the geometry
mapper, the server SQL log, the culture pin and `OverrideAudit`. It sits at the repository's 10.0.1
floor and carries no `VersionOverride`, because .NET binds an assembly by name and a higher version
satisfies a lower reference: compile-low/run-high works and the reverse does not. **Anything that
names a store stays in `FunctionalTests/TestUtilities/`** — the tier classes, their backend stores,
the Northwind contexts, the relational client shell — because moving one would put its provider
package on every consumer's compile line.

### Which tier a base belongs to, and it belongs to exactly one

Running a base on two tiers is duplication, not coverage. When a base could go either way, the tier
that *translates* is the one whose green means more.

- **Tier A** hosts what InMemory can. **"EF ships no InMemory test for this base" means move it to
  Tier B, not drop it.** Only "EF ships no test for it on any store we have" justifies leaving a
  base unadopted.
- **Tier B** is the relational tier, a real file-backed SQLite store.
- **Tier C exists for one capability.** SQLite has no table-valued function and cannot be given
  one, and no `APPLY`; together those are the whole of `UdfDbFunctionTestBase` that Tier B has to
  leave red. Firebird has both and meets the no-installation, no-container bar: the engine is a
  NuGet package of native assets and one database is one `.fdb` file in the test output. **Only a
  base that NEEDS a table-valued function or `APPLY` belongs here.**
- **Tier D exists to prove a negative that no relational tier can** — that this provider is not
  relational-only (#51). **A base whose tests mostly RUN on the store earns its place; one that is
  mostly overridden into "not supported" teaches nothing about the wire.** A compliance test
  demanding every base be adopted against a document store would misread the tier.

ADR-009 and its dated amendments carry the reasoning for all four, including why PostgreSQL was the
better store for Tier C on the evidence and was still not chosen.

**The tell that you are on the wrong tier: if adopting a base means writing a workaround for a
store capability the base assumes, check the tier before writing the workaround.** A base adopted
on the wrong tier produces failures that describe the *backing store* rather than this provider.

**Before moving a base to Tier B, check it for `ExecuteWithStrategyInTransactionAsync` — and if it
uses one, write the `UseTransaction` override in the same commit as the store switch.** That helper
opens **one** transaction and then requires **every other context** to enlist in it. On Tier A the
transaction is ignored and nothing shows; on Tier B it is real, and without the override the inner
contexts stay outside it while the outer one holds the store's write lock — 471 `SQLite Error 5:
'database is locked'` in a single run, each waiting out a 30-second timeout. **The tell is not in
the skips and not in the fixture: it is the base's own transaction strategy.** The override calls
`InfoCarrierDatabaseFacadeExtensions.UseInfoCarrierTransaction` and
`InfoCarrierTransactionManager.UseTransaction(token)`, both shipped since M4. `architecture.md` §6a
**D6** is the full reading, closed.

### Every client is relational, and there is no opt-in

`AddEntityFrameworkInfoCarrier` registers the relational half unconditionally: EF's relational
conventions on the client model, the relational facade dependencies, and the one
`IInfoCarrierRelationalQueryRoots` implementation. `AddInfoCarrierRelational()`,
`AddInfoCarrierRelationalClient()`, `UseRelationalQueryRoots()` and `NoRelationalQueryRoots` are
**deleted**. One package that ships the relational half cannot save a consumer anything by
withholding it, so the opt-in bought nothing and could only be got wrong. **The tiers are about the
backing store and which spec bases it can host, and never about whether the client is relational.**
`architecture.md` §6a carries the **D3 amendment 2026-09-03 (R135)** with the measurement.

### Two provider defects this repository works around

**`FirebirdLateralQuerySqlGenerator` corrects somebody else's bug and is deleted when they fix
it.** The Firebird provider emits a bare function after `LATERAL`, which the store will not parse;
it already wraps a plain *table* as `(SELECT * FROM "T") AS "t"` and never added the branch for a
function. The correction lives on the **server** half of the harness, because the server is an
ordinary EF application and the SQL is its provider's; nothing in `src/` knows about it.

**The document-store write path has two gates and needs both (#100, #102).**
`InfoCarrierDatabase.Expand` pulls an owner's nested documents along when a root is written — the
owner-expansion C86/C87/C95 built for JSON columns was bounded by `GetContainerColumnName()` and
therefore blind to a store with no columns. It is gated on `UseNonRelationalServerStore()`, so a
relational deployment's payload is unchanged. **That client gate is not the whole fix**, because
`Expand` can only send what the client's change tracker holds: a client that ATTACHED A STUB sends
a bare root even with the switch on. `AddInfoCarrierServerDocumentStore()` completes an incomplete
change set with a keyed query before the write, and EF's identity resolution makes that a repair
rather than a clobber. It reads only where the change set omits an owned navigation the model
declares, and never for an insert or a delete. **The server is TOLD rather than sniffing**:
`IsRelational()` is false for InMemory too. ADR-009's 2026-09-10 and 2026-09-11 amendments are the
reading.

### What a test of this suite may assert

**The whole suite is green and there is no ratchet.** Read
[`docs/test-policy.md`](docs/test-policy.md) before overriding a specification test or adding a
golden SQL string. This is Microsoft's approach for EF Core's own providers, made stricter in
traceability: **every override of a specification test says what the store does and where that is
shown.** The override carries an attribute whose TYPE is the label and whose ARGUMENTS are the
reference, all typed:

- `[StoreLimit]` for a refusal by design; `[StoreDefect("1.6")]` for a crash or a wrong answer,
  with its section in `docs/upstream-defects.md`; `[StoreIssue(IssueTracker.EfCore, 36400)]` for a
  tracker entry. The reference is an upstream test, as `UpstreamRepository.EfCore, "path", first,
  last` at the pinned release commit, or a `typeof`/`nameof` naming a wire-free control test.
- `[InfoCarrierDesign(10)]` where this provider differs on purpose, naming the ADR or a document
  heading (`Decisions.*`) that records the decision.
- `[InfoCarrierDefect(n)]` is the last resort for a defect of ours that cannot be fixed yet, and
  **only the owner files its issue.**

**A test can carry a store reason and InfoCarrier reasons together**: the store one says what EF's
own provider test does, the InfoCarrier ones why this test differs from it. `AnswerNotRefusal` and
`RefusedEarlier` are legal only on an InfoCarrier reason, and `StoreExceptionAsData` is mechanical,
because one decision covers every server error.

**A skip is permitted only with an upstream reference**, because only upstream's own choice
justifies asserting nothing, and it copies upstream's justification text. **A crash or a wrong
answer is never `LIMIT`**: a store that means "no" says so. `Justification` is upstream's words or
`Upstream.GaveNoReason`; a body that differs from upstream's says how in `Deviation`
(`DeviationKind` flags) and `DeviationNote`.

**`OverrideAudit` enforces it, and `OverrideAuditTest` runs it in both projects.** It fails on an
override with no reason, on two reasons without distinct `Case` values, on an upstream reference
whose lines in `subrepos/efcore` do not declare the overriding test at the pinned commit, on a skip
without an upstream reference, on a `DEFECT` naming a missing section, on a `DESIGN` naming a
missing heading, and on a wire override whose control test documents a different label. **An
abstract test is not audited**, because it has no expectation to change. Its output is the audit:
every override with its label, reference, skip and deviation, counted by kind. **A CI runner has no
`subrepos/`**, so there the line check counts what it could not check; a local run checks all.

**Every Tier D reference is self-hosted, and that is a finding.** MongoDB's own
`MongoComplianceTest` lists all six `OwnedNavigations` bases in `IgnoredTestBases` under *"Test
bases added in EF10+"*, which means "we have not run this base", NOT "the store cannot do this".
**The control is a conflict of interest** — a worse-wired control fails more and makes InfoCarrier
look cleaner — so every control assertion names an exact outcome.

**The SQL the server runs is asserted in our own words, in `Sqlite/ServerSqlTest.cs`.** Each promise
has our own model, our own query and our own expected text, and **each was shown to fail before it
was trusted**, by reverting the fix it is about. **Copying EF's `AssertSql` text into
overrides was tried and dropped the same day**, with 580 generated and green: the set could not be
complete, the scenario belongs to upstream, and golden text argues for conformance where the
owner's rule allows a deviation that runs no dangerous SQL. `eng/ef-sql-compare.sh` is the
investigation that finds new promises, and it is a report, never a gate. **A slow run's output is
never committed and never asserted, and that includes text we captured ourselves** (ADR-014):
committing each test's statements beside its class was built, reviewed and withdrawn on 2026-09-26.
#167 replaces this script with a slow mode that runs each test with plain EF and through InfoCarrier
and compares the two in memory, and every InfoCarrier reason that claims a runtime difference
(`SqlDiffers`, `AnswerNotRefusal` or `RefusedEarlier`) must then agree with that comparison. This
said "every InfoCarrier reason" until ADR-014's amendment of 2026-09-26: the spike's six reasons with
no difference were skips, rewritten test bodies and a compliance test, and none claimed one.

### Running and reporting

**Point test runs at each `.csproj`, never at the `.slnx`**, and prefer `eng/measure.sh`, which runs
every project in its own `projects` list and adds the figures. **That list holds TWO projects — the
spec project and ADR-009 Tier D — so a project missing from it is missing from every measurement;
add one there in the same commit that creates it.** `InfoCarrier.Core.TransportTests` is
deliberately absent: it is this repository's own HTTP-transport suite, not a spec project, and the
spec-suite badge counts spec tests. **So a `src/` change needs the transport line above as well as
`measure.sh`.**

**Report test results as `Passed: N, Failed: M, Total: T`, read out of the run's own output.** Never
estimate a count, and never derive one figure from the others.

`eng/` holds the scripts below and nothing else. No script does anything a comment inside it does
not explain.

| Script | What it is for |
|---|---|
| `eng/measure.sh <label> [baseline]` | The way to measure a change. See below. Runs every spec test project in its own `projects` list and adds the figures. |
| `eng/trim-ratchet.sh [baseline]` | Publishes the Blazor sample trimmed and gates the direction of this product's `IL2xxx` count against `eng/trim-baseline.txt`. See below. |
| `eng/suite-summary.sh <results.trx> [more.trx ...]` | **CI only**: sums the spec suite's TRX counters into `counters.env`, lists failing names in the run summary, and runs `eng/spec-parity.py` for the README badge. **It decides nothing**; `dotnet test`'s own exit code is the gate. |
| `eng/spec-parity.py <reasons.tsv ...> -- <results.trx ...>` | **EF parity, the README badge**: the share of test cases that ran through InfoCarrier on which no `[InfoCarrierDesign]` or `[InfoCarrierDefect]` reason applies. It joins each TRX with the `*.override-reasons.tsv` that `OverrideAudit` writes when `INFOCARRIER_OVERRIDE_REASONS` names a directory. |
| `eng/ef-sql-compare.sh [--filter X] [--no-cache] [--keep]` | **The #111 investigation, in one command, local only.** Runs Tier B **serially** with `INFOCARRIER_SERVER_SQL` naming a fresh log and prints the disagreements with EF's own `AssertSql` text, grouped by kind. **Not a gate; always exits 0.** Each difference ends as a fix plus a promise in `Sqlite/ServerSqlTest.cs`, as a promise pinning a deviation we accept, or as nothing when it is an artifact of EF's harness. `--filter` narrows it to one class, which takes seconds. |
| `eng/ef-sql-diff.py <server-sql.log> [--efcore <path>] [--limit N] [--extras] [--survey]` | The comparing half of the script above, on a log that already exists. Reports LITERAL, PARAMETER, VALUE and STRUCTURAL, counted per test. **`--extras` reads what EF does not assert**, grouped by shape, unbounded reads first. **`--survey` reads EVERY statement** and needs no reference, which is the mode for a tier upstream gives nothing to compare with. |
| `eng/trx-failures.py <results.trx> [more.trx ...]` | The failing test names across every TRX given, unioned and sorted, one per line. Python and not grep because `>` is legal unescaped in an XML attribute value, so `[^>]*` truncates any test name containing one. |
| `eng/doc-links.py [file...]` | Validates every in-repo Markdown link **including its `#anchor`**. `mkdocs build --strict` checks only that the page exists, so a renamed heading breaks inbound links silently. Exit 1 if any is broken. |
| `eng/doc-words.py [--all] [--budget]` | Prose word count against the budgets in `docs/doc-style.md`. Not `wc -w`, which counts fenced code and link URLs. Exit 1 if a file is over. |
| `eng/docs-serve.sh [--build]` | Serves the documentation site locally with live reload; `--build` runs `mkdocs build --strict` instead. |
| `eng/make-icons.py` | Regenerates every shipped icon from `docs/assets/icon-source.png`. Run it when the artwork changes; none of its outputs is ever edited by hand. |

## Measuring and gating

**`eng/measure.sh <label> [baseline]`** (or the `/experiment` skill, which wraps the whole loop)
prints the count, the exact list of tests fixed and broken, *and* a diff of the failure **reasons**.
Three levels, because each one hides a mistake the level below it catches:

- the count alone cannot tell "fixed 4, broke 4" from "changed nothing";
- the fixed/broken lists alone cannot tell "changed nothing" from "fixed what it aimed at and
  uncovered the next problem in the same tests" — both leave the name list byte-identical. That one
  produced a wrong revert after two runs were read as neutral.

**Never state a verdict from partial output**, and **read the reasons diff, not the count**.

**Which gate to run before which commit.** `eng/measure.sh` says nothing about trimming and the trim
gate says nothing about behaviour. They are separate axes, and one change was committed green on one
while failing the other in CI. **And neither says anything about Native AOT**: trimming does not ask
whether code can be *generated*, so the 59 IL3050 diagnostics a `PublishAot` publish reports are
invisible to `trim-ratchet.sh`. Nothing gates that axis, because Native AOT is not supported.

| Change touches | Run |
|---|---|
| `src/` | **both** `eng/measure.sh` and `eng/trim-ratchet.sh` |
| `test/` only | `eng/measure.sh` |
| `docs/`, `eng/` text only | neither |
| **a public signature in `src/`** | **`dotnet pack InfoCarrier.Core.slnx --no-build --configuration Release`** as well |

**The pack gate runs on the PR too.** Package validation compares the assembly with the last
published stable, which `PackageValidationBaselineVersion` in `Directory.Build.props` names, and
`docs/versioning.md` says when it moves. This said "the published `10.0.0`" until 2026-09-22, after
the value had moved to `10.1.0` and then `10.1.1` with those releases. **Adding an optional parameter to a public member is
source-compatible and BINARY breaking** — the compiler emits one member and the old arity leaves the
assembly, which validation reports as `CP0002`. Six such breaks once rode a green PR into `main` and
turned it red on merge, because only the `main`-only `Packages` workflow packed. `build.yml`'s
*fast-gate* job now packs as well. Run the pack line locally anyway before pushing a
public-signature change; it takes seconds and needs no network beyond the baseline package.

The trim ratchet is a clean publish: ~41 s in CI, about a minute locally. That is cheap enough that
"product code changed" is the whole trigger — do not try to judge whether a change *looks*
reflective, because `WireGrouping` did not look like five warnings.

**And the build itself is a gate.** Warnings are errors when `CI=true`, so before any commit that
touches code: `CI=true dotnet build InfoCarrier.Core.slnx --configuration Release`.

**IT ONLY CHECKS WHAT IT RECOMPILES.** MSBuild is incremental: a project already built by an earlier
non-`CI` command is *up to date*, so the `CI=true` run skips it and reports `5 Warning(s), 0
Error(s)` without ever applying warnings-as-errors to the file you just edited. That is exactly how
one branch passed locally and failed CI on an `EF1001`. **After editing `src/`, delete that
project's `obj` and `bin` before the gate**, or trust the gate only when its output shows the
project being rebuilt. **`--configuration Release` is not optional**: the Blazor sample turns the
trim analyzer on in Release only, so a Debug build cannot produce the diagnostic that fails the
server, and leaving it off kept CI red for ten commits. `docs/build-warnings.md` says what is
suppressed and why.

### Rules that have each cost a wrong conclusion here

Each is cheap to avoid, and the account of what it cost is in
[`docs/plans/v10/findings.md`](docs/plans/v10/findings.md).

- **A count that did not move does not mean the target does not exist.** A matcher that never fired
  and a rewrite that did not help look identical from outside. **Establish that the code *ran*
  first** — with a probe that writes to a file, because xUnit swallows stdout.
- **A probe that prints nothing is evidence only once the build is known green. Check the error
  count, never the elapsed time.** `dotnet build … | Select-Object -Last 2` shows `Time Elapsed` and
  hides `1 Error(s)`, which is how three successive "nothing logged" results were each read as a
  clearance while every run used a stale binary.
- **A newly-red SQLite test is not automatically a regression.** Look the name up in
  `subrepos/efcore/test/EFCore.Sqlite.FunctionalTests` first: if EF overrides it with
  `ApplyNotSupported`, the query now reaches SQL and this is convergence with the reference
  provider. Adopt EF's override, with the attribute that names it. **Check
  `EFCore.Relational.Specification.Tests` too** — a limit every relational provider has is overridden
  on the relational *base*, not in SQLite's own suite, and reading only the latter had
  `Reverse_without_explicit_ordering` classified as ours for two sessions. The reverse also happens:
  an override of ours that EF does *not* have is a workaround to delete once the limitation goes.
- **A classification is not evidence, and age is not evidence.** Check EF's own suites for the test
  name before calling a failure this provider's. Six standing classifications were found wrong that
  way in one session, one of which had read "SQLite-tier, a store limitation" for two milestones and
  was ours, one line.
- **Before pricing a gap, check whether a sibling of it already works**, and **before calling a
  family of failures a design question, check whether a sibling is green.** Two bases were called
  permanently unreachable while the feature they needed had shipped five milestones earlier.
- **Ask what an assertion assumes about the topology** before treating it as a statement about the
  provider. This repository is two `DbContext` instances; `Assert.Same(context, …)` has no answer.
- **When a rule breaks a named family of tests, read the family.** It is usually stating the rule you
  actually wanted.
- **Two failures of the same shape are one defect until measured otherwise.**
- **An evidenced hypothesis can be right about the evidence and wrong about the mechanism.**
- **A relational service on the client needs its companions, and the companions are what this client
  refuses.** Measured twice with two different services, `EntitySplittingConvention` (30 → 149) and
  `RelationalModelValidator` (30 → 4037). Neither failed on its own merits; each failed on the
  absence of a neighbour in EF's own list. **Read the list a service sits in before adding it
  alone**; the missing neighbour is named nowhere in the service itself.

## Where authority lives

`docs/` is the source of truth. Read before changing design, and keep it current:

| Doc | Role |
|---|---|
| `docs/decisions.md` | **ADR log.** LOCKED entries are binding. |
| `docs/infocarrier-core-requirements.md` | Authoritative requirements spec |
| `docs/plans/v10/roadmap.md` | **Stable** milestone plan for the whole project |
| `docs/plans/v10/implementation-plan.md` | **Rolling** checkbox detail for the *current* milestone only |
| `docs/architecture.md` | Components, test strategy, open questions |
| `docs/research-findings.md` | EF Core 10 pipeline findings backing the ADRs |
| `docs/decisions.md` **ADR-014** | **A slow run's output is never committed, and #167 compares with plain EF live.** The committed form of a finding is our own minimal repro, seen red and made green. Read it before designing anything that generates text. |
| `docs/decisions.md` **ADR-013** | The test project may reference `EFCore.Relational.Specification.Tests`. **Before adopting a relational spec base, check whether it assumes the *client* is relational** — a non-virtual `UseTransaction` calling `GetDbTransaction()` blocks a base only when every route runs through it (cost 142 tests to discover); a `protected virtual` caller above it, or only some tests routing through it, still adopts. See the ADR's 2026-08-30 amendment. |
| `docs/security-review.md` | **M5's review of the deserialization path** (C48). Read §2 before adding anything to `TypeAllowlist`: its safety is a conjunction across several clauses, and `Binder`/`MethodInfo`/`Activator` each break it alone. |
| `docs/build-warnings.md` | **Which warning codes are fatal, which are suppressed, where, and why. Read before adding any `NoWarn`.** **Green is not zero here**: the gate build reports `5 Warning(s), 0 Error(s)`, the five being `IL2110`/`IL2111` from the framework's own Razor output in `samples/Northwind.Client`, downgraded from error on purpose so the trim ratchet can still count them. Debug reports `0 Warning(s)`, which is why "the build is clean" stood uncorrected for five milestones. |
| `docs/plans/v10/cold-read-findings.md` | **What seven readers with no context found in the user-facing docs**, and what is still open. §1 holds the `IgnoreQueryFilters` design question: the marker crosses the wire and the server honours it, so a global query filter is **not** an authorization boundary today. Read before touching the security or tenancy prose. |
| `docs/doc-style.md` | **The rules for every document a consumer reads** (README, `src/*/PACKAGE.md`, `website/`, the GitHub release bodies). Word budgets, the no-dash and no-rationale rules, and the reference set they were measured against. `docs/` itself is exempt. Read before editing any of those files. |
| `docs/versioning.md` | **How a version is decided and how a release is shipped**, including the hotfix path off a release line, and a "what has bitten us" list of traps invisible from the code. **Read it before tagging**, and before cutting a release branch. |
| `docs/test-policy.md` | **What a test of this suite may assert, and why.** The override rule and its typed reasons, the promises this provider makes about the SQL its server runs (`Sqlite/ServerSqlTest.cs`), and the investigation that finds new ones (`eng/ef-sql-compare.sh`). **Read it before overriding a specification test or adding a golden SQL string.** |
| `docs/upstream-defects.md` | **Defects in somebody else's code, and which ones have been reported.** §1 is what nobody has sent; §2 is what an issue number already covers. **Read it before citing an issue number**: one citation here named a Backlog feature request rather than the defect it was attached to, for two milestones. §1.6-1.10 are `MongoDB.EntityFrameworkCore` defects, whose bugs go to the **Jira `EF` project** rather than GitHub. Each entry says what it blocks, because a defect that blocks nothing needs a report and not a workaround. |
| `docs/plans/v10/findings.md` | **How every rule in this file was learned, and what each cost.** The long form of the HTTP transport, the Blazor client, complex types, JSON-mapped owned collections, spatial, `GraphUpdates`, the compiled model, the design-time services, and the five closed intermittents. |

**Roadmap vs plan — do not mix them.** Milestone-level scope, ordering, and exit criteria go in
`roadmap.md`, which changes only when scope changes. Per-task checkboxes go in
`implementation-plan.md`, which is rewritten at each milestone boundary (previous ones land in
`docs/plans/v10/archive/`, never edited again). Putting task detail in the roadmap, or scope changes
in the plan, is what caused the drift these two docs replaced.

**Reversing a LOCKED ADR requires a dated supersession edit in `docs/decisions.md`** — not a code
change that quietly contradicts it. ADR-001 (greenfield serializer, no Remote.Linq/Aqua dependency),
ADR-004 (inherit `EFCore.Specification.Tests`), and ADR-006 (raw capture at `IDatabase.CompileQuery`)
are the ones most likely to be violated by accident.

**AND A REVERSAL IS THE MOMENT TO SWEEP FOR THE COMMENTS THAT ARGUED FOR IT, IN THE SAME COMMIT.**
A comment recording a decision goes stale exactly when that decision is reversed, which is the one
moment nobody re-reads it. **Measured here: the R135 reversal of "`InfoCarrier.Core` does not
reference `EFCore.Relational`" left twenty-four comments asserting it**, and the last fourteen were
found six days later by a deliberate sweep, one release too late. **XML doc comments ship in the
package and show in IntelliSense**, so a consumer reads them.

So: search for the reversed API's name, the deleted type's name and the claim's own words, across
`src/` **and** `test/`, before the reversal commits. Two rules make the result worth having.
**Correct the reason, do not delete the paragraph** — most of these decisions are still right for a
different reason, and the new reason is the valuable half. And **quote what it used to say with the
date**, because the next reader needs to know the reasoning changed rather than that it was always
this.

## Guardrails

**A new permanent mechanism is argued against before it is built.** #167's committed SQL captures
were specified, planned, built and reviewed before anyone tested them against the reasons the
owner had given, ten days earlier, for rejecting the same idea (ADR-014). Four rules:

1. **Before a design amends a recorded decision** (an ADR, `docs/test-policy.md`, a rule in this
   file), list each reason of that decision and say whether it still holds. If one holds, stop and
   ask the owner. Writing "that rejection stands" is not the check.
2. **Name what a red means for every artifact that is committed**: what does a person do when it
   goes red? If the answer is "run a tool and commit its output", it is golden text, and the answer
   is no.
3. **Prove on a satellite branch first**, and move to `main` only the smallest part that the next
   finding needs.
4. **A plan of more than three pull requests, or one that adds a concept to every normal run, shows
   its whole permanent footprint and gets the owner's yes before its first step.** Before agreeing
   to such a mechanism, state the strongest case against it, citing the recorded decisions it
   touches.

**Never edit anything under `subrepos/`.** Those are git-ignored reference clones of `efcore`,
`rlinq`, `aqua`, `infocarrier-v1`, and `firebird` (`FirebirdSQL/NETProvider` at `EFCore-13.0.0.0`,
the tag matching the package Tier C runs), kept for source-level study. Each is checked out at the
tag this repository references, and `firebird` loads in `roslyn-codelens` from
`subrepos/firebird/src/NETProvider.slnx` with `rootProjects` naming its two EF Core projects.
`efcore` is the authoritative EF Core 10 reference — read it to confirm API shapes rather than
guessing. Edits there are invisible to git and will be lost.

**Never override a spec test without saying what the store does and where that is shown.** The
inherited `EFCore.Specification.Tests` classes *are* the coverage goal (ADR-004), and silently
suppressing tests was v1's stated failure mode. The typed reasons and the audit that checks them are
under "What a test of this suite may assert" above. A skip needs an upstream skip to copy, a defect
of ours is fixed first, and nothing is filed anywhere unless the owner asks.

**Update the plan checkbox in the same commit as the work.** `docs/plans/v10/implementation-plan.md`
drifted out of sync with git once already. One substep per commit, message prefixed `Step <id>:`.

**EF1001 usage is expected and allowed; the warning is suppressed per file, EF's own way.** This
provider legitimately depends on EF Core internals (`IStateManager`, `EntityQueryable<>`,
`InternalEntityEntry`). Do not refactor to avoid them — but do prefer public API where one exists.
The 19 files that use internals carry a **file-scoped** `#pragma warning disable EF1001` under a
two-line comment naming the reason; `subrepos/efcore` has 51 such files across eight projects and no
`NoWarn` for EF1001 anywhere. **Do not add `NoWarn=EF1001`** to a project or to
`Directory.Build.props`. The pragma is per file on purpose: a *new* file that reaches for an internal
API still warns, which is the tripwire that keeps "prefer public API where one exists" enforceable.

**Do not add a NuGet dependency on Remote.Linq or Aqua** (ADR-001). They are specification material
only.

**Client-side work is allowed only where it is a projection reassembly, and everything else throws.**
`QuerySplitter.RejectClientEvaluation` raises EF's own `TranslationFailed` /
`TranslationFailedWithDetails`, so an untranslatable `Where` behaves here exactly as it does on every
other EF provider. **This was got wrong once by reading the design document and the analyzer and
stopping there**: `projection-split.md` §3.3 sends a client-typed `Where` to §3.5, §3.5 says "ship
the maximal `ServerOk` subtree containing a query root", and `ServerBoundaryAnalyzer` agrees — which
together read as "the whole table crosses silently". **Both documents describe the frontier and
neither mentions the guard that runs between them.**
`InMemorySmokeTest.A_filter_the_server_cannot_run_throws_rather_than_fetching_everything` pins it.
The rule that generalises: **a design document plus the code it describes can both be right and still
not tell you what happens, when the thing you need is a guard that sits between them.**

**A composite join key is rewritten so the join ships.** `new { a.X, a.Y }` is a type the caller's
compiler generates, so the boundary could not ship it and cut below the join. `JoinKeyRewriter`
gives the key a `Tuple<...>`, which EF translates to the same SQL an anonymous key gets, null
matching included. **`ValueTuple` does not work and was measured**: EF refuses to translate it. A key
of a type the caller declared still runs on the client, because a class with its own `Equals` is not
data. `docs/projection-split.md` §3.3a is the reading.

**A compiled query's collection parameter stays a parameter.** A compiled query's parameters cross
this wire as values, so the server's funcletizer folded an operator EF had kept symbolic and the
statement's shape changed with the number of values. `SubstituteParametersExpressionVisitor` marks
such a collection with EF's own `EF.MultipleParameters`, and **the server replaces that mark with
EF's marker for its own collection mode** (`CollectionParameterMark`), so it writes EF's own
statement in every mode. This said "EF's DEFAULT mode, so the server writes EF's own statement" until
2026-09-21, which held only on a server in that mode. A marker the CALLER wrote keeps the caller's
mode: the client sends its argument as a constant, and the server rewrites only a boxed one.
**Only an operator the server could fold is marked.** One that reads the row,
`ids.Where(y => y == x.Id)`, reaches this client in an ordinary query too, and a mark there replaced
the server's own collection mode with the default. This said "an ordinary query is untouched" until
2026-09-21; `ServerParameterizationTest` now measures the server's three modes. **An operator that
CONSUMES the list is marked too, since 2026-09-22**, so a compiled `ids.Count()` runs EF's
`json_array_length(@ids)` and, in `Constant` and `MultipleParameters` mode, **fails with EF Core
10's own `UnreachableException`** (dotnet/efcore#37370, fixed for EF 11 only). That failure is
EF's, parked by the owner until EF 11: do not "fix" it here. **An index into the list, `ids[1]`,
is kept one parameter too, since 2026-09-22, and with `EF.Parameter` rather than the mode-named
mark**: EF translates an index over the list as one parameter in every mode (`@ids ->> 1`), and the
mode-named mark made a server in `Constant` mode inline the list.

**Anything the wire computes from a type mapping is computed twice, by two different providers, and
is only sound if the two agree.** The client's model is built by this provider and the server's by
the backing store, so `FindTypeMapping()` is not one answer but two: a `DateTime[]` was once written
by SQLite's JSON form (`2023-01-01 12:30:00`) and read by EF's core one (ISO-8601), 106 failures in
both directions. Scalars are safe because `PrimitiveCoercion` short-circuits the wire primitives
before any mapping is consulted; anything else must be derived from the **CLR type alone**, through a
service no provider replaces.

**A fact two components read independently can disagree with itself, and the disagreement is silent
when one component's answer only widens what the other is allowed to do.** The live instance: what
may be **SENT** is decided by `InfoCarrierOptionsExtension.AllowedTypesFor`, read per execution in
`QueryExecutor`; what may be **READ BACK** is decided by the DI-scoped `TypeNodeResolver`, whose own
allowlist knows only the model. Those two disagreed silently until a declared projection type came
back over the wire: `Database.SqlQuery<UnmappedCustomer>` cleared the boundary and then failed to
materialize its own rows. `QueryExecutor` now hands the same list to both directions. **A
`DbParameter` is sent and never returned, which is why the older registered type showed nothing** — a
one-way fact cannot expose a two-reader disagreement. **When a permission and the knowledge it guards
live on different carriers, check that one reader answers for both.**

### Packaging, the site, and the branch model

**There are TWO shipped packages, and `release.yml` names them one by one**: `InfoCarrier.Core` and
`InfoCarrier.Core.AspNetCore`. The push steps use exact filenames rather than a glob, so **a third
package would ship nothing until that workflow named it**. Every packable project validates against
the last published stable, which `PackageValidationBaselineVersion` names.

**`InfoCarrier.Core.Relational` was a third package and is not one any more.** D3 is superseded
(`architecture.md` §6a, 2026-09-03): the relational half lives at `src/InfoCarrier.Core/Relational/`,
keeps the `InfoCarrier.Core.Relational` **namespace**, and `InfoCarrier.Core` carries the
`Microsoft.EntityFrameworkCore.Relational` reference. It never shipped a stable version. **A future
split is a folder move plus one `PackageReference` line**, and the supersession lists the three
measurable conditions that would call for it.

**Publishing the site is an explicit act: `gh workflow run Docs --ref release/10.1`.** The `deploy`
job requires `workflow_dispatch` and a `refs/heads/release/` ref, so no push publishes anything and
`main` cannot publish at all. A push still BUILDS, which keeps `--strict` gating every commit and
every pull request. **The branch dispatched from is the choice of what to publish**, so a new minor
needs no edit to the workflow: cut `release/10.2` and dispatch on it. The site can therefore go
stale, and that is the accepted trade.

**The site publishes from `release/10.1`, not from `main`.** `main` runs ahead of nuget.org, and an
unversioned site built from it tells every reader to call APIs their package does not contain — which
has happened: a 10.2.0 server timeout was put in front of 10.1.0 readers, and a sentence true of
their version was deleted for being false on `main`.

**So the branch model is maintenance branches, and fixes ORIGINATE ON THE RELEASE BRANCH and are
MERGED UP** (the Symfony and Linux direction, chosen over .NET's fix-main-then-backport). A
correction to what the shipped release does is a commit on `release/10.1`, merged into `main`; `main`
is for the next minor. `release/2.2` and `release/3.1` are the v1 line and predate this.
**`build.yml` and `packages.yml` run on `release/**`**, so a release line is gated exactly like the
trunk and publishes a candidate to the internal feed before any tag makes a version permanent. **The
spec-suite badge is still `main`-only**, because a badge is a claim about the trunk.
**`docs/versioning.md` is the release procedure** — read it before tagging anything.

## Current state

**The suite is green, and CI fails on any failing test**: `dotnet test`'s own exit code is the gate,
on every push and every pull request. **No document keeps the current count.** `eng/measure.sh`
prints it, a commit message or a PR body records it with its date, and the published site gives it
once per release (`docs/versioning.md`, "A minor from `main`"). This paragraph gave the count until
2026-09-21, when six hand-kept copies were found stale within days, two of them wrong and the two
website pages disagreeing with each other, so **do not add one back**. **Every figure a report
gives comes out of the run's own summary block, and none of them is arithmetic** — one entry once
carried `Skipped` over from an earlier run and derived `Passed` from it.

**There is no baseline any more.** `test/known-failures.txt` and `test/known-failures.names.txt` were
deleted with the ratchet on 2026-09-15; git history keeps them. **The audit is the current answer to
"what does this suite not check, and why"**: `OverrideAuditTest` writes every override with its
label, reference, skip and deviation on every run.

**Every spec base EF ships that this provider can host is adopted**, and two compliance tests enforce
it: `InfoCarrierComplianceTest` scans the core specification assembly against Tier A, and
`RelationalInfoCarrierComplianceTest` scans the relational one against Tier B, with
`GetBaseTestClasses()` overridden so the two do not both claim the core bases. **Both missing lists
are 0, and both tests must stay green.** Those tests, not a list in this file, are the answer to
"which bases are in".

**Every milestone is closed, so `docs/plans/v10/implementation-plan.md` is ISSUE-DRIVEN**: it holds
Phases Q, R, S, T, U, V, X, Y, Z and H, each naming the GitHub issue it serves, and which release a
phase lands in is decided on the issue rather than in the plan. **Phase Y is the 10.1 release
preparation** — the letter is Y and not W because W1 to W6 are M5's requirement labels, used
throughout `roadmap.md`. **Phase Z is #54's idle timeout for a server-held transaction**, the first
of that issue's three separable properties. **Phase H is #167's live comparison with plain EF
(ADR-014), PROPOSED on 2026-09-26: no step of it starts before the owner's yes.** Query, projection
split, `SaveChanges` and lazy loading all work end-to-end.

### What may and may not be claimed

**The consumer-facing statement of what is missing is
[`website/docs/limitations.md`](website/docs/limitations.md)**, and that is the document to keep
true. It names one unsupported scenario, message-text differences on a refused query, and queries
this provider *answers* that other EF providers reject. **It no longer claims a count of any of
them**: a count was wrong twice, once because the suite grew under it and once because the honest
number included a family the owner had decided not to name. The whole consumer-facing set (README,
`src/*/PACKAGE.md`, `website/`, the GitHub release bodies) is governed by
[`docs/doc-style.md`](docs/doc-style.md), which is the file to read before editing any of them.

**A user-facing document may say the three inheritance mappings round trip.** Tier B has
`TPTInheritanceQueryInfoCarrierTest`, `TPCInheritanceQueryInfoCarrierTest`,
`TPHInheritanceQueryInfoCarrierTest` and `TPTTableSplittingInfoCarrierTest`, plus TPT and TPC
variants of Gears of War, bulk updates, relationships and many-to-many. That is a real relational
store and direct coverage, not a mechanism proxy. Since R135 the client also carries the server's
mapping strategy rather than core EF's guess (`Relational:MappingStrategy` on every hierarchy root,
in the compiled-model baselines).

**It still may not claim anything about SQL Server**, which this suite does not run. M7's SQL Server
tier was dropped on 2026-08-24 by the owner, and what was withdrawn is a *test tier*, never support
for the store: the server side is an ordinary EF application and runs against whatever provider it
references, so requirements §5 is unaffected. **The letter C was reused for embedded Firebird and
does not mean SQL Server any more.** Computed columns, sequences and `rowversion` are covered here
only *by mechanism* — store-generated values (`StoreGeneratedTestBase`, including `OnAddOrUpdate`)
and concurrency tokens — and what is untested about them is the *store's* behaviour, which never
crosses this wire. A non-relational backend tier is recorded as future scope with nothing committed.

### What is not implemented

- **A gRPC binding and streaming results as `IAsyncEnumerable` are OUT OF SCOPE for v10**
  (2026-08-23, owner's decision; `roadmap.md`, M8 exit criteria). Neither ships in the 10.x line, and
  **neither may be named as a plan in any user-facing document** (`doc-style.md` rule 6). **No
  user-facing document may give a reason for a version suffix** — the version number carries it, as
  it does for every EF Core release. `10.0.0` carries no suffix.
- **The compiled-query cache keyed by canonical serialization (ADR-008 constraint 6) and the server
  delegate cache are OUT OF SCOPE for v10** (2026-08-24, owner's decision). **It was never a
  correctness item**: EF's own `ICompiledQueryCache` already caches what the client's `CompileQuery`
  returns, so what repeats per request is the serialize/translate work, which gives the same answer
  every time. **The suite measures answers, so a missing cache is invisible to it.** ADR-008
  constraint 6 stays as written and stays unexercised.
- **Native AOT is not supported, and it was measured rather than assumed** (2026-08-24). A
  `PublishAot` publish of `samples/Northwind.Demo` reports **155 unique IL diagnostics, 153 ours, and
  59 of those are IL3050** — `RequiresDynamicCode`, a code the trim analyzer never emits. **The trim
  ratchet therefore says nothing about AOT and never did.** The publish also failed at the native
  link step for a missing platform linker, so no native binary has been produced or run.
  `UseInfoCarrier` carries `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, as EF Core's own
  `DbContext` constructors do; **that annotation did not lower the count and was not going to** — it
  exempts only the annotated method's own body. Trimming itself is verified and works.
- **`IgnoreQueryFilters` is not refused by the server, and v10 ships that way** (2026-08-24, owner's
  decision). A global query filter is therefore not an authorization boundary against a hostile
  client, for reads and for `ExecuteUpdate`/`ExecuteDelete` alike. The documented control is a
  server-side query interceptor. **Read `docs/plans/v10/cold-read-findings.md` §1 before touching the
  security or tenancy prose**, and never let a user-facing page claim the filter is the boundary.
- **Two `ComplexTypesTracking` parameterizations**: a property-bag complex *collection* on an `Added`
  entity. It is an upstream defect on a path only this provider takes, and the route around it has to
  reproduce constructor binding, so it is priced and not taken. Its override asserts the defect's
  exception and carries `[InfoCarrierDefect(52)]`.

### The test stores, and flakiness

The Tier B store is **file-backed** (`<StoreName>.db` in the test output directory), as EF Core's own
`SqliteTestStore` is. **Do not move it back to `Mode=Memory;Cache=Shared`**: that makes the
database's lifetime a connection's, which makes test-class disposal order load-bearing and has
already produced a 698-test phantom failure. For the same reason **the store must not delete its file
on disposal, and must not release its `Created` entry either** — either one reintroduces the
coupling, and the second half produced a nine-test intermittent once the suite passed ten thousand
tests. `DisposeAsync` now releases nothing; stale files are swept once at startup instead.

**The runtime culture is pinned to invariant** by a `[ModuleInitializer]`. On an `en-SE` machine nine
spec tests fail on the decimal separator, none of them this provider's, which made the suite total a
property of the machine. **Do not remove it.**

**There is no known intermittent. Five have been closed**, and their accounts are in
[`docs/plans/v10/findings.md`](docs/plans/v10/findings.md). Three rules came out of them:

- **A semaphore serializes the callers that share it, and a gate spelt twice is not a gate.** More
  broadly: **when an extraction says a duplicate is dangerous, check in the same commit that the
  duplicate is gone.**
- **A guard that records that work *started* is not evidence its result still exists.**
- **Run counting proves nothing at a one-in-ten rate.** Close an intermittent by *reproducing its
  signature* in a fast deterministic test, or by *reading the source it comes from*, or by
  *instrumenting it to diagnose itself* — not by repetition.

**The suite is deterministic. Run it once.** Do not re-run to "confirm" a result — `measure.sh`
already ran it, and repeating that is minutes of wall clock buying nothing. Flakiness is not the
default assumption.

**If you do notice flakiness, it becomes the top priority — before whatever you were doing.** The
signal is a run that differs from the previous snapshot with **no code change between them**; that is
the only thing that justifies suspecting it. Stop, find the cause, fix it, and only then go back to
the work. A flake left in place poisons every measurement after it. Verify the fix with three
consecutive identical runs — *that* is what the three-run bar is for, not for routine work.
