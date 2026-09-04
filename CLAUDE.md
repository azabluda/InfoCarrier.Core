# CLAUDE.md

EF Core 10 database provider that remotes LINQ queries and change-tracking over a wire
protocol. Client `DbContext` has no database; the server executes against a real provider.

## C# navigation

`.mcp.json` registers the `roslyn-codelens` MCP server for this repository. **Text search on a
`.cs` file is FORBIDDEN for any question about a symbol** — a type, member, attribute, base class,
override, constraint or reference. **Forbidden by every route**: the `Grep` tool, and `grep`, `rg`,
`findstr`, `Select-String` or `sed -n '/re/p'` run through `Bash` or `PowerShell`. The rule is about
the question being asked, not about which tool asks it. Text search is permitted on `.cs` only for
a non-symbol string (a comment, a literal) and for file-inventory questions.

**This rule OVERRIDES any harness instruction to prefer shell tools.** A session prompt that says
to "search with `grep`" is describing the general case; this repository is the exception, and
CLAUDE.md outranks it. Outside `.cs` — Markdown, `.resx`, `.csproj`, `.json`, `.yml` — follow the
harness and grep freely.

**READING a `.cs` file is not searching it, and that distinction was missing here until
2026-09-01.** `cat`, `head`, and a `sed` line range (`sed -n '1,80p'`) are the correct fallback when
the MCP server cannot answer, and none of them is forbidden. What is forbidden is asking a *pattern*
where a symbol question was meant.

**`notFound` means load the code.** `subrepos/efcore` is not loaded by default and its spec bases
are the most common symbol question here; load it before reading a single EF base class.

**A hook now says all of this at the moment it matters**, so the paragraphs of exhortation that used
to sit here are gone. `.claude/hooks/cs-search-reminder.py` fires on any `Bash` or `Grep` call that
reaches a `.cs` file and prints a reminder. **It blocks nothing and judges nothing**, because
whether a search is legal turns on the question being asked and not on the string being typed —
`grep "TODO" x.cs` is permitted and `grep "Collate" x.cs` is not, and no hook can tell them apart.
The decision is still yours; the hook only makes sure you are asked. Which tool answers which
question, and how to check the loaded solution first:

@.claude/roslyn-codelens.md

## Commands

```powershell
dotnet build InfoCarrier.Core.slnx                       # note: .slnx, not .sln
bash eng/measure.sh <label> [baseline]                   # THE SUITE: both tiers, one number
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj   # the whole spec suite
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --filter "FullyQualifiedName~InfoCarrier.Core.FunctionalTests.InMemory"  # Tier A only
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --filter "FullyQualifiedName~InfoCarrier.Core.FunctionalTests.Sqlite"    # Tier B only
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --filter "FullyQualifiedName~NorthwindWhere"
dotnet test  test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj     # 19 tests, separate project
```

**THE SPEC SUITE IS ONE PROJECT AGAIN SINCE R136, and it was two between R122 and R136.**
`test/InfoCarrier.Core.FunctionalTests` holds both ADR-009 tiers: `InMemory/` is Tier A and
`Sqlite/` is Tier B, and `TestUtilities/` is the harness both share. It was
`test/InfoCarrier.Core.TestUtilities`, a project of its own, until R137 folded it in; by then it had
one consumer. The split existed to keep the `InfoCarrier.Core.Relational` package off Tier A's
compile line, and there is no such package any more. **The tiers are a namespace now, not a
project**, so a run of one tier is a `--filter` and the suite's number is one project's.

**EVERY CLIENT IS RELATIONAL SINCE R135, ON BOTH TIERS, AND THERE IS NO OPT-IN LEFT.**
`AddEntityFrameworkInfoCarrier` registers the relational half unconditionally: EF's relational
conventions on the client model, the relational facade dependencies, and the one
`IInfoCarrierRelationalQueryRoots` implementation. `AddInfoCarrierRelational()`,
`AddInfoCarrierRelationalClient()`, `UseRelationalQueryRoots()` and `NoRelationalQueryRoots` are
**deleted**, and so is R130's half-configuration warning, because a half-configured client cannot
exist without a switch. One package that ships the relational half cannot save a consumer anything
by withholding it — the payload carries it either way — so the opt-in bought nothing and could only
be got wrong. **The tiers are about the backing store and which spec bases it can host, and no
longer about whether the client is relational**; this file and `architecture.md` said the opposite
until R135, and `architecture.md` §6a carries the **D3 amendment 2026-09-03 (R135)** with the
measurement.

**Point test runs at each `.csproj`, never at the `.slnx`**, and prefer `eng/measure.sh`, which runs
both and adds the figures. The solution also holds `InfoCarrier.Core.TransportTests`, which is not a
spec project, so a solution-wide run inflates `Total` past what `test/known-failures.txt` was written
against. **A run of one tier alone is not comparable to the baseline either**, because the baseline
covers both.

**Report test results as `Passed: N, Failed: M, Total: T`, read out of the run's own output.** Never
estimate a count, and never derive one figure from the others.

`eng/` holds these and nothing else. No script does anything a comment inside it does not explain.

| Script | What it is for |
|---|---|
| `eng/measure.sh <label> [baseline]` | The way to measure a change. See below. **It runs every spec test project in its own `projects` list and adds the figures**, so a project missing from that list is missing from every measurement; add one there in the same commit that creates it. |
| `eng/trim-ratchet.sh [baseline]` | Publishes the Blazor sample trimmed and gates the direction of this product's `IL2xxx` count against `eng/trim-baseline.txt`. See below. |
| `eng/ratchet.sh <results.trx> [more.trx ...] <baseline-file>` | **CI only**, and wired: `.github/workflows/build.yml`'s *spec-ratchet* job invokes it against `test/known-failures.txt`. The suite is legitimately red during build-out and tests must not be skipped to force it green, so CI gates on the *direction* of the failure count, and on the **total** as well. **It reads its figures out of the TRX**, which counts the skips the console block's `passed` and `failed` do not. It also writes them to `counters.env` beside the TRX, which is where the README's spec-suite badge gets its numbers — one parser, not two. **It gates on the failing test NAMES as well**, read by `eng/trx-failures.py` and diffed against `test/known-failures.names.txt`: a change that fixes four tests and breaks four others leaves the count untouched. It publishes that delta to `$GITHUB_STEP_SUMMARY`, which the test report action cannot do because it does not know the baseline. **It takes several TRX and the LAST argument is the baseline**: counters are summed and names unioned into one list, gated against the one baseline pair. One baseline and not one per project, because a test that MOVES between projects would otherwise read as a fix in one and a break in the other. |
| `eng/trx-failures.py <results.trx> [more.trx ...]` | The failing test names across every TRX given, unioned and sorted, one per line. What `test/known-failures.names.txt` holds and what `ratchet.sh` diffs. Python and not grep because `>` is legal unescaped in an XML attribute value, so `[^>]*` truncates any test name containing one. |
| `eng/doc-links.py [file...]` | Validates every in-repo Markdown link **including its `#anchor`**. `mkdocs build --strict` checks only that the page exists, so renaming a heading silently breaks inbound links and the build stays green: three did, over a dead link on the security path. Exit 1 if any link is broken. |
| `eng/doc-words.py [--all] [--budget]` | Prose word count against the budgets in `docs/doc-style.md`. Not `wc -w`, which counts fenced code and link URLs. Exit 1 if a file is over. |
| `eng/docs-serve.sh [--build]` | Serves the documentation site locally with live reload; `--build` runs `mkdocs build --strict` instead. |
| `eng/usage-window.sh [threshold]` | How much of the current Claude Code usage window is gone, and when it resets. There is no `claude usage` subcommand, so it goes through `claude -p "/usage"`. **It costs usage to run**, because that starts a session of its own, so call it at boundaries rather than on a timer. Exit 1 at or above the threshold, which defaults to 80. **`MSYS_NO_PATHCONV=1` inside it is load-bearing**: Git Bash rewrites a leading `/` into a Windows path, and `/usage` then arrives as an ordinary prompt that starts a full agent session. |
| `eng/make-icons.py` | Regenerates every shipped icon — the 128px NuGet one and the site's favicon, apple-touch and manifest icons — from `docs/assets/icon-source.png`. Run it when the artwork changes; none of its outputs is ever edited by hand. |

## Measuring and gating

**`eng/measure.sh <label> [baseline]`** (or the `/experiment` skill, which wraps the whole loop)
prints the count, the exact list of tests fixed and broken, *and* a diff of the failure **reasons**.
Three levels, because each one hides a mistake the level below it catches:

- the count alone cannot tell "fixed 4, broke 4" from "changed nothing";
- the fixed/broken lists alone cannot tell "changed nothing" from "fixed what it aimed at and
  uncovered the next problem in the same tests" — both leave the name list byte-identical. That one
  produced a wrong revert (plan L8) after two runs were read as neutral.

**Never state a verdict from partial output.**

**Which gate to run before which commit.** `eng/measure.sh` says nothing about trimming and the trim
gate says nothing about behaviour. They are separate axes, and M9's J8 was committed green on one
while failing the other in CI. **And neither says anything about Native AOT**: trimming does not ask
whether code can be *generated*, so the 59 IL3050 diagnostics a `PublishAot` publish reports are
invisible to `trim-ratchet.sh`. Nothing gates that axis, because Native AOT is not supported.

| Change touches | Run |
|---|---|
| `src/` | **both** `eng/measure.sh` and `eng/trim-ratchet.sh` |
| `test/` only | `eng/measure.sh` |
| `docs/`, `eng/` text only | neither |
| **a public signature in `src/`** | **`dotnet pack InfoCarrier.Core.slnx --no-build --configuration Release`** as well |

**The pack gate now runs on the PR too, and it did not until R119.** Package validation compares
the assembly with the published `10.0.0` (`Directory.Build.props`). **Adding an optional parameter
to a public member is source-compatible and BINARY breaking** — the compiler emits one member and
the old arity leaves the assembly, which validation reports as `CP0002`. Six such breaks rode a
green PR into `main` and turned it red on merge, because the `Packages` workflow was then the only
job that packed and it runs on `main` alone. `build.yml`'s *fast-gate* job now packs as well, so
the break fails on the branch. Run the pack line locally anyway before pushing a public-signature
change; it takes seconds and needs no network beyond the baseline package.

The trim ratchet is a clean publish: ~41 s in CI, about a minute locally. That is cheap enough that
"product code changed" is the whole trigger — do not try to judge whether a change *looks*
reflective, because `WireGrouping` did not look like five warnings.

**And the build itself is a gate.** Warnings are errors when `CI=true`, so before any commit that
touches code: `CI=true dotnet build InfoCarrier.Core.slnx --configuration Release`.

**IT ONLY CHECKS WHAT IT RECOMPILES, AND THAT IS HOW R133 WENT RED ON THE PULL REQUEST.** MSBuild is
incremental: a project already built by an earlier non-`CI` command is *up to date*, so the
`CI=true` run skips it and reports `5 Warning(s), 0 Error(s)` without ever applying
warnings-as-errors to the file you just edited. R133 edited `src/InfoCarrier.Core`, built that
project alone without `CI=true` while iterating, then ran the gate — which recompiled nothing and
passed. CI recompiled everything and failed on one `EF1001`. **After editing `src/`, delete that
project's `obj` and `bin` before the gate**, or trust the gate only when its output shows the
project being rebuilt.
**`--configuration Release` is not optional, and leaving it off is how CI went red for ten commits
without anyone noticing (N12).** The Blazor sample turns the trim analyzer on in Release only, so a
Debug build cannot produce the diagnostic that fails the server. It takes seconds and it is what the
server runs. `docs/build-warnings.md` says what is suppressed and why.

Each of the following has already cost a wrong conclusion here, and each is cheap to avoid.

- **A count that did not move does not mean the target does not exist.** A matcher that never fired
  and a rewrite that did not help look identical from outside. Establish that the code *ran* first —
  with a probe that writes to a file, because xUnit swallows stdout.
- **A probe that prints nothing is evidence only once the build is known green. Check the error
  count, never the elapsed time.** `dotnet build … | Select-Object -Last 2` shows `Time Elapsed` and
  hides `1 Error(s)`, which is how three successive "nothing logged" results were each read as a
  clearance while every run used a stale binary.
- **"EF ships no InMemory test for this base" means move it to Tier B, not drop it.** ADR-009 has
  two tiers precisely because InMemory cannot host everything, and a base adopted on the wrong one
  produces failures that describe the *backing store* rather than this provider. Only "EF ships no
  test for it on any store we have" justifies leaving a base unadopted. The tell: **if adopting a
  base means writing a workaround for a store capability the base assumes, check the tier before
  writing the workaround.** And **a base belongs to exactly one tier** — running one on both is
  duplication, not coverage. When a base could go either way, the tier that *translates* is the one
  whose green means more.
- **Before moving a base to Tier B, grep it for `ExecuteWithStrategyInTransactionAsync` — and if it
  uses one, write the `UseTransaction` override in the same commit as the store switch.** That
  helper opens **one** transaction and then requires **every other context** to enlist in it. On
  Tier A the transaction is ignored and nothing shows; on Tier B it is real, and without the
  override the inner contexts stay outside it while the outer one holds the store's write lock —
  471 `SQLite Error 5: 'database is locked'` in a single run, each waiting out a 30-second timeout.
  **The tell is not in the skips and not in the fixture: it is the base's own transaction strategy.**
  The override calls `InfoCarrierDatabaseFacadeExtensions.UseInfoCarrierTransaction` and
  `InfoCarrierTransactionManager.UseTransaction(token)`, both shipped since M4. `architecture.md`
  §6a **D6** is the full reading, closed.
- **A newly-red SQLite test is not automatically a regression.** Grep
  `subrepos/efcore/test/EFCore.Sqlite.FunctionalTests` for the name first: if EF overrides it with
  `ApplyNotSupported`, the query now reaches SQL and this is convergence with the reference
  provider. Adopt EF's override. **Grep `EFCore.Relational.Specification.Tests` too** — a limit
  every relational provider has is overridden on the relational *base*, not in SQLite's own suite,
  and reading only the latter had `Reverse_without_explicit_ordering` classified as ours for two
  sessions. The reverse also happens: an override of ours that EF does *not* have is a workaround to
  delete once the limitation goes.

**What each of these cost, and how it was found, is in
[`docs/plans/v10/findings.md`](docs/plans/v10/findings.md).**

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
| `docs/decisions.md` **ADR-013** | The test project may reference `EFCore.Relational.Specification.Tests`. **Before adopting a relational spec base, check whether it assumes the *client* is relational** — a non-virtual `UseTransaction` calling `GetDbTransaction()` blocks a base only when every route runs through it (cost 142 tests to discover); a `protected virtual` caller above it, or only some tests routing through it, still adopts. See the ADR's 2026-08-30 amendment. |
| `docs/security-review.md` | **M5's review of the deserialization path** (C48). Read §2 before adding anything to `TypeAllowlist`: its safety is a conjunction across several clauses, and `Binder`/`MethodInfo`/`Activator` each break it alone. |
| `docs/build-warnings.md` | **Which warning codes are fatal, which are suppressed, where, and why.** Warnings are errors **in CI only** — `CI=true dotnet build InfoCarrier.Core.slnx --configuration Release` reproduces it — the configuration matters. **That command reports `5 Warning(s), 0 Error(s)`, and green is not zero here**: the five are `IL2110`/`IL2111` from the framework's own Razor output in `samples/Northwind.Client`, downgraded from error to warning on purpose so the trim ratchet can still count them. Debug reports `0 Warning(s)`, which is why "the build is clean" stood uncorrected for five milestones. Read before adding any `NoWarn`. |
| `docs/plans/v10/cold-read-findings.md` | **What seven readers with no context found in the user-facing docs**, and what is still open. §1 holds the `IgnoreQueryFilters` design question: the marker crosses the wire and the server honours it, so a global query filter is **not** an authorization boundary today. Read before touching the security or tenancy prose. |
| `docs/doc-style.md` | **The rules for every document a consumer reads** (README, `src/*/PACKAGE.md`, `website/`, the GitHub release bodies). Word budgets, the no-dash and no-rationale rules, and the reference set they were measured against. `docs/` itself is exempt. Read before editing any of those files. |

**Roadmap vs plan — do not mix them.** Milestone-level scope, ordering, and exit criteria go
in `roadmap.md`, which changes only when scope changes. Per-task checkboxes go in
`implementation-plan.md`, which is rewritten at each milestone boundary (previous ones land in
`docs/plans/v10/archive/`, never edited again). Putting task detail in the roadmap, or scope changes in
the plan, is what caused the drift these two docs replaced.

**Reversing a LOCKED ADR requires a dated supersession edit in `docs/decisions.md`** — not a
code change that quietly contradicts it. ADR-001 (greenfield serializer, no Remote.Linq/Aqua
dependency), ADR-004 (inherit `EFCore.Specification.Tests`), and ADR-006 (raw capture at
`IDatabase.CompileQuery`) are the ones most likely to be violated by accident.

## Guardrails

**Never edit anything under `subrepos/`.** Those are git-ignored reference clones of
`efcore`, `rlinq`, `aqua`, and `infocarrier-v1`, kept for source-level study. `efcore` is the
authoritative EF Core 10 reference — grep it to confirm API shapes rather than guessing.
Edits there are invisible to git and will be lost.

**Never `[Skip]`, delete, or override a spec test to make the suite green.** The inherited
`EFCore.Specification.Tests` classes *are* the coverage goal (ADR-004); a red test is
information. If a test targets genuinely unimplemented functionality, leave it failing and
note it in `docs/plans/v10/implementation-plan.md`. Silently suppressing tests was v1's stated failure
mode.

**Update the plan checkbox in the same commit as the work.** `docs/plans/v10/implementation-plan.md`
drifted out of sync with git once already (F1–F7 were committed while still shown unchecked).
One substep per commit, message prefixed `Step <id>:`.

**EF1001 usage is expected and allowed; the warning is suppressed per file, EF's own way.** This
provider legitimately depends on EF Core internals (`IStateManager`, `EntityQueryable<>`,
`InternalEntityEntry`). Do not refactor to avoid them — but do prefer public API where one exists.

The 19 files that use internals carry a **file-scoped** `#pragma warning disable EF1001` under a
two-line comment naming the reason. That is what EF Core's own providers do and it was checked
before it was copied: `subrepos/efcore` has **51 files** with `#pragma warning disable EF1001`
across eight projects (21 in `EFCore.Relational` alone), some file-scoped and some in narrow
pairs, and **no `NoWarn` for EF1001 anywhere in the repository**.

**Do not add `NoWarn=EF1001`** to a project or to `Directory.Build.props`. The pragma is per file
on purpose: a *new* file that reaches for an internal API still warns, which is the tripwire that
keeps "prefer public API where one exists" enforceable. A `NoWarn` would remove it silently.

**Do not add a NuGet dependency on Remote.Linq or Aqua** (ADR-001). They are specification
material only.

**Client-side work is allowed only where it is a projection reassembly, and everything else
throws.** `QuerySplitter.RejectClientEvaluation` raises EF's own `TranslationFailed` /
`TranslationFailedWithDetails`, so an untranslatable `Where` behaves here exactly as it does on
every other EF provider. **This was got wrong once by reading the design document and the analyzer
and stopping there**: `projection-split.md` §3.3 sends a client-typed `Where` to §3.5, §3.5 says
"ship the maximal `ServerOk` subtree containing a query root", and `ServerBoundaryAnalyzer` agrees
— which together read as "the cut lands below the `Where`, the server runs the root alone, and the
whole table crosses silently". **Both documents describe the frontier and neither mentions the
guard that runs between them.** `InMemorySmokeTest.A_filter_the_server_cannot_run_throws_rather_than_fetching_everything`
pins it. The rule that generalises: **a design document plus the code it describes can both be
right and still not tell you what happens, when the thing you need is a guard that sits between
them.**

**Anything the wire computes from a type mapping is computed twice, by two different providers,
and is only sound if the two agree.** The client's model is built by this provider and the
server's by the backing store, so `FindTypeMapping()` is not one answer but two. B4: a
`DateTime[]` was written by SQLite's JSON form (`2023-01-01 12:30:00`) and read by EF's core one
(ISO-8601), 106 failures in both directions. Scalars are safe because `PrimitiveCoercion`
short-circuits the wire primitives before any mapping is consulted; anything else must be derived
from the **CLR type alone**, through a service no provider replaces.

**A fact two components read independently can disagree with itself, and the disagreement is silent
when one component's answer only widens what the other is allowed to do.** R120's finding, and it
cost a wrong answer rather than a red test. `IInfoCarrierRelationalQueryRoots` says what EF's
relational raw-SQL query roots are. `ServerBoundaryAnalyzer` read it from the **options**, which it
must — `ExtensionInfo.GetServiceProviderHashCode()` is `0`, so every client context in a process
shares one internal service provider and anything per-context has to travel on the options. The
forward translator read it from **DI**, because it is DI-scoped. A client that set the option but
not the service then **admitted** a raw-SQL root at the boundary and **dropped its SQL** in the
translator: the whole table came back, silently, which is the defect R75 closed.
`InfoCarrierOptionsExtension.RelationalQueryRootsFor` is now the one reader, called once per
execution in `QueryExecutor` and handed to both. **When a permission and the knowledge it guards
live on different carriers, check that one reader answers for both.**

**There are TWO shipped packages since 2026-09-03, and `release.yml` names them one by one.**
`InfoCarrier.Core` and `InfoCarrier.Core.AspNetCore`. The push steps use exact filenames rather than
a glob, so **a third package would ship nothing until that workflow named it**. Every packable
project validates against `10.0.0`.

**`InfoCarrier.Core.Relational` was a third package and is not one any more.** D3 is superseded
(`architecture.md` §6a, 2026-09-03): the relational half lives at
`src/InfoCarrier.Core/Relational/`, keeps the `InfoCarrier.Core.Relational` **namespace**, and
`InfoCarrier.Core` carries the `Microsoft.EntityFrameworkCore.Relational` reference. It never
shipped a stable version, so nothing published had to change. **A future split is a folder move plus
one `PackageReference` line**, and that is deliberate — the supersession lists the three measurable
conditions that would call for it.

## Current state

**M8 is CLOSED (2026-08-24).** Every exit criterion has a resolution: three done (HTTP transport,
sample apps, packaging), two out of scope for v10 (gRPC and streaming; the compiled-query cache),
and requirements §4.5 answered in two halves (trimming verified, Native AOT not supported). Task
detail is archived in `docs/plans/v10/archive/implementation-plan-m8-phases-h-n.md` and is never
edited again. **`docs/plans/v10/implementation-plan.md` now holds M5's one remaining criterion, the
remote cancel signal (W6)**, and it is now the only work left in the whole roadmap.

**M7's SQL Server tier is DROPPED (2026-08-24, owner's decision), not deferred.** What is withdrawn
is a *third test tier* for this repository's suite, never support for the store: the server side is
an ordinary EF application and runs against whatever provider it references, so requirements §5 is
unaffected and ADR-009 keeps its two tiers. **The cost is smaller than it first looks and is stated
per feature in `roadmap.md`, because a first reading of it lumped four features together and was
too broad for three of them.** Computed columns, sequences and `rowversion` all reduce, on this
side of the wire, to mechanisms with direct green coverage: store-generated values
(`StoreGeneratedTestBase`, including `OnAddOrUpdate`) and concurrency tokens (67 pass). What is
untested about those three is the *store's* behaviour, which is an ordinary EF concern on the
server and never crosses this wire. **TPT/TPC is the one real gap**: it changes the model, and this
provider builds a model on the client too, and no TPT or TPC test class exists here at any tier.
**No user-facing document may claim any of the four works** — green coverage of a mechanism is not
a claim about a store this suite never runs. A non-relational backend tier
is recorded as future scope with nothing committed.

**M9 is CLOSED (2026-08-17).** The paragraph below was written while M8 was still open.
**M9 is CLOSED (2026-08-17). M8 is NOT.** M9 met its four exit criteria: the document-mapping seam
(so `InfoCarrier.Core` no longer references `EFCore.Relational`), the test project organised by
backend store, four bases moved to the tier that translates, and the capability axis identified,
decided and recorded rather than built (`architecture.md` §6a **D5**, answer (c)). Task detail is
archived in `docs/plans/v10/archive/implementation-plan-m9-phase-j.md` and is never edited again;
`docs/plans/v10/implementation-plan.md` holds M8's Phases H and I only.

**M6 is CLOSED (2026-08-11).** Every spec base EF ships that this provider can host is adopted, and
`All_test_bases_must_be_implemented` is what enforces it. **There are TWO of those tests since R122,
one per test project, and both must stay green** — `InfoCarrierComplianceTest` scans the core
specification assembly against Tier A, and `RelationalInfoCarrierComplianceTest` scans the relational
one against Tier B, with `GetBaseTestClasses()` overridden so the two do not both claim the core
bases. **BOTH MISSING LISTS ARE 0 SINCE R124** (2026-09-03), which closed the last one:
`SqlQueryTestBase` had no subclass anywhere and now has `Sqlite/Query/SqlQueryInfoCarrierTest`.
Those tests, not a list in this file, are the current answer to "which bases are in", and the answer
is now "all of them".

Query, projection split and SaveChanges work end-to-end. Lazy loading works: Phase L began at 505 of
505 failing and stands at **825 of 825**.

**`Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177`** (2026-08-18, `n8-minver`).
**All four figures come out of the run's own summary block, and none of them is arithmetic** — a
`c10b` entry once carried `Skipped` over from an earlier run and derived `Passed` from it. **A
falling `total` with no note explaining it is a crashed host**: `test/known-failures.txt` records
the one deliberate lowering, in C94, where two skipped theories turned 4 tests into 2.

**The baseline is two files and they move together.** `test/known-failures.txt` holds the counts
and the reasoning; `test/known-failures.names.txt` holds the failing test names and nothing else,
because `comm` cannot read a file with comments in it. A commit that lowers the count must copy
`artifacts/test-results/failures.txt` over the names file, and the ratchet says so in a `::notice::`
when it sees the count fall.

**All 9 failures are classified and not one is of unknown standing.** They sit in six classes,
three holding two and three holding one. The tables are in
`docs/plans/v10/archive/implementation-plan-m9-phase-j.md` — A54, A59, A61–A65, B3a–B16 and
C1–C96 — whose "The residual 13, examined properly" re-derives the whole tail **as it stood at
thirteen**; J20 and J21 lowered it after that, and `test/known-failures.txt` carries the dated
reading for each. **The archive is never edited, so its count is the count of the day it was
written and the baseline file is the current one.** `Query.Associations` is 336 of 336, and
`MaterializationInterception`, `OptimisticConcurrency` and `ComplexNavigations` are clear. Wrong
answers are down to **2**, both C64's `Correlated_collection_with_distinct_3_levels`, whose
assertion no correct answer can satisfy.

**The consumer-facing statement of what is missing is
[`website/docs/limitations.md`](website/docs/limitations.md)**, and that is the document to keep
true. It names one unsupported scenario, one query to treat with caution, two message-text
differences, and two queries this provider *answers* that other EF providers reject. It is not the
only consumer-facing document any more: the whole set (README, `src/*/PACKAGE.md`, `website/`, the
GitHub release bodies) is governed by **[`docs/doc-style.md`](docs/doc-style.md)**, which is the
file to read before editing any of them.

**What is not implemented.** Everything else that this section used to list has closed.

- **A gRPC binding and streaming results as `IAsyncEnumerable` are OUT OF SCOPE for v10**
  (2026-08-23, owner's decision; `roadmap.md`, M8 exit criteria). Neither ships in the 10.x line,
  and **neither may be named as a plan in any user-facing document** (`doc-style.md` rule 6).
  **No user-facing document may give a reason for a version suffix** — the version number carries
  it, as it does for every EF Core release. `10.0.0` carries no suffix.
- **The compiled-query cache keyed by canonical serialization (ADR-008 constraint 6, Q5) and the
  server delegate cache (Q10) are OUT OF SCOPE for v10** (2026-08-24, owner's decision;
  `roadmap.md`, M8 exit criteria). **It was never a correctness item**, which is why it could move:
  EF's own `ICompiledQueryCache` already caches what the client's `CompileQuery` returns, so what
  repeats per request is the serialize/translate work, and that gives the same answer every time.
  **The suite measures answers, so a missing cache is invisible to it** — do not read 22472 passing
  as evidence that this shipped. ADR-008 constraint 6 stays as written and stays unexercised.
- **Native AOT is not supported, and it was measured rather than assumed** (2026-08-24;
  `roadmap.md` M8 exit criteria, `findings.md`). A `PublishAot` publish of `samples/Northwind.Demo`
  reports **155 unique IL diagnostics, 153 ours, and 59 of those are IL3050** —
  `RequiresDynamicCode`, a code the trim analyzer never emits. **The trim ratchet therefore says
  nothing about AOT and never did.** The publish also failed at the native link step for a missing
  platform linker, so no native binary has been produced or run. `UseInfoCarrier` carries
  `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`, as EF Core's own `DbContext`
  constructors do; **that annotation did not lower the 88 and was not going to** — it exempts only
  the annotated method's own body. Trimming itself is verified and works.
- **`IgnoreQueryFilters` is not refused by the server, and v10 ships that way** (2026-08-24,
  owner's decision; `roadmap.md` §"`IgnoreQueryFilters`: why v10 ships with it open"). A global
  query filter is therefore not an authorization boundary against a hostile client, for reads and
  for `ExecuteUpdate`/`ExecuteDelete` alike. The documented control is a server-side query
  interceptor. **Read `docs/plans/v10/cold-read-findings.md` §1 before touching the security or
  tenancy prose**, and never let a user-facing page claim the filter is the boundary.
- Two `ComplexTypesTracking` parameterizations: a property-bag complex *collection* on an `Added`
  entity. J22 traced it to an upstream defect on a path only this provider takes, and the route
  around it has to reproduce constructor binding, so it is priced and not taken.
- The residual spec failures, every one of them classified.

**The long form is [`docs/plans/v10/findings.md`](docs/plans/v10/findings.md)**: how the HTTP
transport, the Blazor client, complex types, JSON-mapped owned collections, spatial, `GraphUpdates`,
the compiled model and the design-time services were made to work, what each cost, and the two
investigations that ran longest. The rules those findings produced are the part that transfers, and
they are these:

- **Read the reasons diff, not the count.** A count that did not move cannot tell "fixed four, broke
  four" from "changed nothing", and four `Scaffolding.CompiledModel` fixes each measured 26 to 26.
- **Establish that the code ran** before concluding anything from a count that did not move. A
  matcher that never fired and a rewrite that did not help look identical from outside.
- **Before pricing a gap, check whether a sibling of it already works.** Two bases were called
  permanently unreachable while the feature they needed had shipped five milestones earlier.
- **Before calling a family of failures a design question, check whether a sibling is green.**
- **A classification is not evidence, and age is not evidence.** Grep EF's own suites for the test
  name before calling a failure this provider's. Six standing classifications were found wrong that
  way in M9's closing session alone, one of which had read "SQLite-tier, a store limitation" for two
  milestones and was ours, one line (J19).
- **Ask what an assertion assumes about the topology** before treating it as a statement about the
  provider. This repository is two `DbContext` instances; `Assert.Same(context, …)` has no answer.
- **When a rule breaks a named family of tests, read the family.** It is usually stating the rule
  you actually wanted.
- **Two failures of the same shape are one defect until measured otherwise.**
- **An evidenced hypothesis can be right about the evidence and wrong about the mechanism.**

The Tier B store is **file-backed** (`<StoreName>.db` in the test output directory), as EF
Core's own `SqliteTestStore` is. Do not move it back to `Mode=Memory;Cache=Shared`: that makes
the database's lifetime a connection's, which makes test-class disposal order load-bearing and
has already produced a 698-test phantom failure. For the same reason **the store must not delete
its file on disposal, and must not release its `Created` entry either** — either one
reintroduces the coupling. The second half survived S3c-5 and produced a nine-test intermittent
failure once the suite passed ten thousand tests: a shared store's disposal re-armed the guard
and let a later class re-seed the file a live one was still using. `DisposeAsync` now releases
nothing. Stale files are swept once at startup instead.

**The runtime culture is pinned to invariant** by a `[ModuleInitializer]`, and that is a ratchet fix
rather than a test fix. On an `en-SE` machine nine spec tests fail on the decimal separator, none of
them this provider's, which made the suite total a property of the machine. Do not remove it.

**There is no known intermittent. Three have been closed, and by three different routes.** The
third was closed on 2026-09-04 **by reading the source it came from**, which is the cheapest route of
the three and the one to try first. A SQLite store failed once inside `SqliteConnection.Open()` with
`ObjectDisposedException: SQLitePCL.sqlite3`. EF's `SqliteDatabaseCreator.Delete` answers a
file-backed database with **`SqliteConnection.ClearAllPools()`, which is process-wide**, and every
SQLite store here calls `EnsureDeletedAsync` at initialization, so one store's delete disposes a
pooled native handle another store is opening. The test connection string now sets `Pooling=false`,
and with no pool the call has nothing to dispose. **That is proof by construction rather than by
repetition**, which matters because the failure appeared once in about ten full runs and three clean
runs would have shown nothing.

**The first two intermittents are closed**, and they were closed by opposite routes.
C38's was **instrumented into the open**: `ServerSaveChangesExecutor` rethrows an identity conflict
with the whole request appended, which turned a one-run-in-four failure into two dumps that arrived
already diagnosed. R76's **never reproduced under instrumentation at all** — five clean full runs —
and was closed by *reproducing its signature* instead: delete the shared `.db` in the window between
the two classes that share it and the same 18 failures come back to the reason. Its rule is worth
carrying: **a guard that records that work *started* is not evidence its result still exists.** Both
accounts are in [`docs/plans/v10/findings.md`](docs/plans/v10/findings.md).

**The suite is deterministic. Run it once.** Do not re-run to "confirm" a result — `measure.sh`
already ran it, and repeating that is minutes of wall clock buying nothing. Flakiness is not the
default assumption.

**If you do notice flakiness, it becomes the top priority — before whatever you were doing.** The
signal is a run that differs from the previous snapshot with **no code change between them**;
that is the only thing that justifies suspecting it. Stop, find the cause, fix it, and only then
go back to the work. A flake left in place poisons every measurement after it, which is how this
repo lost a day to a 698-test phantom failure and later to a nine-test intermittent one. Verify
the fix with three consecutive identical runs — *that* is what the three-run bar is for, not for
routine work.
