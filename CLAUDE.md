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
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --filter "FullyQualifiedName~InfoCarrier.Core.FunctionalTests.Firebird"  # Tier C only
dotnet test  test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj --filter "FullyQualifiedName~NorthwindWhere"
dotnet test  test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj     # 26 tests, separate project, NOT in measure.sh
```

**THE SPEC SUITE IS ONE PROJECT AGAIN SINCE R136, and it was two between R122 and R136.**
`test/InfoCarrier.Core.FunctionalTests` holds every ADR-009 tier: `InMemory/` is Tier A,
`Sqlite/` is Tier B, `Firebird/` is Tier C since R153. **The tiers are a namespace now, not a
project**, so a run of one tier is a `--filter` and the suite's number is one project's.

**THE HARNESS IS A PROJECT AGAIN SINCE 2026-09-13, AND ONLY THE STORE-NEUTRAL HALF OF IT.**
`test/InfoCarrier.Core.TestUtilities` holds the client and server shells, the store factory, the
fixture properties, the in-process transport, the geometry mapper, the server SQL log and the
culture pin. R137 had folded these into the spec project because by then they had one consumer;
**Tier D is the second, and it cannot be in the spec project because it runs on a newer EF Core
patch** — one project has one package graph. **The harness sits at the repository's 10.0.1 floor
and carries no `VersionOverride`**, because .NET binds an assembly by name and a higher version
satisfies a lower reference: compile-low/run-high works and the reverse does not. `src/` already
worked this way for Tier D. **Anything that names a store stayed in
`FunctionalTests/TestUtilities/`** — the three tier classes, their backend stores, the Northwind
contexts, the relational client shell — because moving one would put its provider package on every
consumer's compile line. ADR-009's 2026-09-13 amendment is the reading.

**TIER C IS EMBEDDED FIREBIRD SINCE R153, AND IT EXISTS FOR ONE CAPABILITY.** SQLite has no
table-valued function and cannot be given one, and no `APPLY`; together those are the whole of
`UdfDbFunctionTestBase` that Tier B has to leave red. Firebird has both, and it meets the
no-installation and no-container bar the way SQLite does: the engine arrives as a NuGet package of
native assets and one database is one `.fdb` file in the test output. **Only a base that NEEDS a
table-valued function or `APPLY` belongs here.** The dated amendment to ADR-009 carries the whole
reading, including why PostgreSQL is the better store on the evidence and was still not chosen.

**`FirebirdLateralQuerySqlGenerator` corrects somebody else's bug and is deleted when they fix
it.** The Firebird provider emits a bare function after `LATERAL`, which the store will not parse;
it already wraps a plain *table* as `(SELECT * FROM "T") AS "t"` and simply never added the branch
for a function. That one defect is what its own suite records as fourteen "Not supported on
Firebird" skips. The correction lives on the **server** half of the harness, because the server is
an ordinary EF application and the SQL is its provider's; nothing in `src/` knows about it.

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

**TIER D IS AN EMBEDDED MONGODB SINCE 2026-09-10, IN A PROJECT OF ITS OWN
(`test/InfoCarrier.Core.DocumentStoreTests`), AND IT ADOPTS SPEC BASES SINCE 2026-09-14.** This
paragraph read *"AND IT ADOPTS NO SPEC BASES"* until then, and it adopts the whole
`OwnedNavigations` family. **The tier is 234 tests in two halves, and all of them pass since
2026-09-15**: the six bases over the wire, and the SAME bases with InfoCarrier REMOVED — the
`Direct*` classes, plain EF Core on the same embedded MongoDB. **The control documents the store**:
each of its overrides asserts exactly what MongoDB does, and each wire override names the control
test that shows it. **The reason that
sentence was written is unchanged and is what still governs which bases come here**: this tier
exists to prove a negative that no relational tier can, that this provider is not relational-only
(#51), and a compliance test demanding every base be adopted against a document store would misread
it. What changed is that "no compliance test" never meant "no bases" — a base whose tests mostly RUN
on the store earns its place, and one that is mostly overridden into "not supported" teaches nothing
about the wire. ADR-009's 2026-09-13 amendment carries that selection rule and its 2026-09-14 one
carries the adoption.

**IT IS MEASURED LIKE EVERY OTHER TIER SINCE 2026-09-14, AND IT WAS RATCHETED LIKE THEM UNTIL THE
RATCHET WENT ON 2026-09-15.** This paragraph read *"gated beside the transport suite rather than by
the spec ratchet"* before 2026-09-14, which was right while the tier adopted no bases. Its project is
in `measure.sh`'s list, and CI runs it in the `Spec tests` job, a required check that was named
`Spec ratchet` until the same day. ADR-004 carries the amendment for the whole change.

**THE WHOLE SUITE IS GREEN AND THERE IS NO RATCHET SINCE 2026-09-15** (`docs/plans/v10/test-overhaul.md`).
This is Microsoft's approach for EF Core's own providers, made stricter in traceability: **every
override of a specification test says what the store does and where that is shown.** The override
carries an attribute whose TYPE is the label and whose ARGUMENTS are the reference, all typed:

- `[StoreLimit]` for a refusal by design; `[StoreDefect("1.6")]` for a crash or a wrong answer, with
  its section in `docs/upstream-defects.md`; `[StoreIssue(IssueTracker.EfCore, 36400)]` for a
  tracker entry. The reference is an upstream test, as `UpstreamRepository.EfCore, "path", first,
  last` at the pinned release commit, or a `typeof`/`nameof` naming a wire-free control test.
- `[InfoCarrierDesign(10)]` where this provider differs on purpose, naming the ADR or a document
  heading (`Decisions.*`) that records the decision.
- `[InfoCarrierDefect(n)]` is the last resort for a defect of ours that cannot be fixed yet, and
  **only the owner files its issue.**

**A test can carry a store reason and InfoCarrier reasons together** (the owner, 2026-09-15): the
store one says what EF's own provider test does, the InfoCarrier ones why this test differs from it.
`AnswerNotRefusal` and `RefusedEarlier` are legal only on an InfoCarrier reason, and
`StoreExceptionAsData` is mechanical, because one decision covers every server error.

**A skip is permitted only with an upstream reference**, because only upstream's own choice
justifies asserting nothing, and it copies upstream's justification text. **A crash or a wrong
answer is never `LIMIT`**: a store that means "no" says so. `Justification` is upstream's words or
`Upstream.GaveNoReason`; a body that differs from upstream's says how in `Deviation`
(`DeviationKind` flags) and `DeviationNote`.

**`OverrideAudit` (in the shared harness) enforces it, and `OverrideAuditTest` runs it in both
projects.** It fails on an override with no reason, on two reasons without distinct `Case` values,
on an upstream reference whose lines in `subrepos/efcore` do not declare the overriding test at the
pinned commit, on a skip without an upstream reference, on a `DEFECT` naming a missing section, on a
`DESIGN` naming a missing heading, and on a wire override whose control test documents a different
label. **An abstract test is not audited**, because it has no expectation to change. Its output is
the audit: every override with its label, reference, skip and deviation, counted by kind. Each rule
was shown to fail on a deliberate mistake before it was trusted. **A CI runner has no `subrepos/`**,
so there the line check counts what it could not check; a local run checks all of them.

**EVERY TIER D REFERENCE IS SELF-HOSTED, AND THAT IS A FINDING.** MongoDB's own `MongoComplianceTest`
lists all six `OwnedNavigations` bases in `IgnoredTestBases` under *"Test bases added in EF10+"*,
which means "we have not run this base", NOT "the store cannot do this". The one upstream test that
looked close, `UnsupportedQueryTests.cs` at `v10.0.3`, selects from `string[]`, a primitive
collection, and this family selects from owned entities. **The control is a conflict of interest**: a
worse-wired control fails more and makes InfoCarrier look cleaner. Every control assertion names an
exact outcome, and the control classes that need no override must stay green without one.
`eng/tier-d-control.py` and its two text manifests gated this until 2026-09-15 and are deleted. Its own
project exists because
`MongoDB.EntityFrameworkCore` needs EF Core >= 10.0.11 while `src/` compiles against a 10.0.1 floor,
which also makes it the one place the product runs on a NEWER EF Core than it was built with.
**It found #100 on its first run and the same change fixes it**: updating an entity that owns
nested documents lost them over the wire, and a scalar update lost them silently. The fix widens
the owner-expansion in `InfoCarrierDatabase.Expand` that C86/C87/C95 built for JSON columns, which
was bounded by `GetContainerColumnName()` and therefore blind to a store with no columns, and adds
the direction that case never needed: a root being written pulls its own document along. **Gated on
`UseNonRelationalServerStore()`, so a relational deployment's payload is unchanged.**

**AND THAT CLIENT GATE IS NOT THE WHOLE FIX, WHICH TOOK A SECOND CHANGE TO SEE (#102).** `Expand`
can only send what the client's change tracker holds, so a client that ATTACHED A STUB sends a bare
root even with the switch on, and a deployment that never set the switch sends one always. The
server half, `AddInfoCarrierServerDocumentStore()`, completes an incomplete change set with a keyed
query before the write; EF's identity resolution makes that a repair rather than a clobber, because
a tracked entity is never overwritten by a query. **The server is TOLD rather than sniffing**:
`IsRelational()` is false for InMemory too and that store nests nothing, and MongoDB's own model
marks a root with `Mongo:CollectionName` while marking an owned type with nothing. It reads only
where the change set omits an owned navigation the model declares, and never for an insert or a
delete. ADR-009's 2026-09-10 and 2026-09-11 amendments are the reading.

**Point test runs at each `.csproj`, never at the `.slnx`**, and prefer `eng/measure.sh`, which runs
every project in its own `projects` list and adds the figures. **That list holds TWO projects since
2026-09-14 — the spec project and ADR-009 Tier D — and it held the spec project alone before that.**
`InfoCarrier.Core.TransportTests` is deliberately absent: it is this repository's own HTTP-transport
suite, not a spec project, and the spec-suite badge counts spec tests. **So a `src/` change needs the
transport line above as well as `measure.sh`**; the script says so in its own comment.

**Report test results as `Passed: N, Failed: M, Total: T`, read out of the run's own output.** Never
estimate a count, and never derive one figure from the others.

`eng/` holds these and nothing else. No script does anything a comment inside it does not explain.

| Script | What it is for |
|---|---|
| `eng/measure.sh <label> [baseline]` | The way to measure a change. See below. **It runs every spec test project in its own `projects` list and adds the figures**, so a project missing from that list is missing from every measurement; add one there in the same commit that creates it. |
| `eng/trim-ratchet.sh [baseline]` | Publishes the Blazor sample trimmed and gates the direction of this product's `IL2xxx` count against `eng/trim-baseline.txt`. See below. |
| `eng/suite-summary.sh <results.trx> [more.trx ...]` | **CI only**: reads the spec suite's TRX files, sums their counters into `counters.env`, lists the failing names in the run summary, and runs `eng/spec-parity.py` for the README badge. **It decides nothing**; `dotnet test`'s own exit code is the gate since the ratchet went on 2026-09-15. It replaced `eng/ratchet.sh`, which gated the direction of the failure count against `test/known-failures.txt`; both files are deleted, and git history keeps them. |
| `eng/spec-parity.py <reasons.tsv ...> -- <results.trx ...>` | **EF parity, the README badge since 2026-09-15**: the share of test cases that ran through InfoCarrier on which no `[InfoCarrierDesign]` or `[InfoCarrierDefect]` reason applies. It joins each TRX result with the `*.override-reasons.tsv` that `OverrideAudit` writes when `INFOCARRIER_OVERRIDE_REASONS` names a directory, which CI sets for both test steps. The badge showed `passed / total` until the suite went green and made that a constant. |
| `eng/trx-failures.py <results.trx> [more.trx ...]` | The failing test names across every TRX given, unioned and sorted, one per line. Python and not grep because `>` is legal unescaped in an XML attribute value, so `[^>]*` truncates any test name containing one. |
| `eng/doc-links.py [file...]` | Validates every in-repo Markdown link **including its `#anchor`**. `mkdocs build --strict` checks only that the page exists, so renaming a heading silently breaks inbound links and the build stays green: three did, over a dead link on the security path. Exit 1 if any link is broken. |
| `eng/doc-words.py [--all] [--budget]` | Prose word count against the budgets in `docs/doc-style.md`. Not `wc -w`, which counts fenced code and link URLs. Exit 1 if a file is over. |
| `eng/docs-serve.sh [--build]` | Serves the documentation site locally with live reload; `--build` runs `mkdocs build --strict` instead. |
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
  three tiers precisely because InMemory cannot host everything, and a base adopted on the wrong one
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
- **A newly-red SQLite test is not automatically a regression.** Look the name up in
  `subrepos/efcore/test/EFCore.Sqlite.FunctionalTests` first: if EF overrides it with
  `ApplyNotSupported`, the query now reaches SQL and this is convergence with the reference
  provider. Adopt EF's override, with the attribute that names it. **Grep `EFCore.Relational.Specification.Tests` too** — a limit
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
| `docs/versioning.md` | **How a version is decided and how a release is shipped**, including the hotfix path off a release line. Two procedures with a shared tail, and a "what has bitten us" list: the pack baseline a branch cut from a tag inherits, the merge resolution that silently drops a fix, the site that no push republishes, and the `github-pages` environment refusing a branch it was not told about. **Read it before tagging**, and before cutting a release branch. |
| `docs/upstream-defects.md` | **Defects in somebody else's code, and which ones have been reported.** §1 is what nobody has sent; §2 is what an issue number already covers, so a comment naming a number can be checked against what it says. **Read it before citing an issue number**: one citation here named a Backlog feature request rather than the defect it was attached to, for two milestones. **Since 2026-09-14 it also carries defects in `MongoDB.EntityFrameworkCore`** (§1.6-1.10), found by ADR-009 Tier D and reproduced by its wire-free control, and it records that their bugs go to the **Jira `EF` project** rather than GitHub, whose issues are disabled. Each entry says what it blocks, because a defect that blocks nothing needs a report and not a workaround. |

**Roadmap vs plan — do not mix them.** Milestone-level scope, ordering, and exit criteria go
in `roadmap.md`, which changes only when scope changes. Per-task checkboxes go in
`implementation-plan.md`, which is rewritten at each milestone boundary (previous ones land in
`docs/plans/v10/archive/`, never edited again). Putting task detail in the roadmap, or scope changes in
the plan, is what caused the drift these two docs replaced.

**Reversing a LOCKED ADR requires a dated supersession edit in `docs/decisions.md`** — not a
code change that quietly contradicts it. ADR-001 (greenfield serializer, no Remote.Linq/Aqua
dependency), ADR-004 (inherit `EFCore.Specification.Tests`), and ADR-006 (raw capture at
`IDatabase.CompileQuery`) are the ones most likely to be violated by accident.

**AND A REVERSAL IS THE MOMENT TO SWEEP FOR THE COMMENTS THAT ARGUED FOR IT, IN THE SAME COMMIT.**
A comment recording a decision goes stale exactly when that decision is reversed, which is the one
moment nobody re-reads it: the work is in the code, and the prose that justified the old shape sits
somewhere else entirely. **Measured on this repository, and the number is why this rule exists.**
R135 and the D3 supersession reversed one premise — that `InfoCarrier.Core` does not reference
`EFCore.Relational` — and left **twenty-four** comments asserting it. Six were found in Y5 and two
in Y11, both times by accident while doing something else; a deliberate sweep in Y12 found the other
fourteen, six days later and one release too late. **XML doc comments ship in the package and show
in IntelliSense**, so a consumer reads them.

So: grep for the reversed API's name, the deleted type's name and the claim's own words, across
`src/` **and** `test/`, before the reversal commits. Two rules make the result worth having.
**Correct the reason, do not delete the paragraph** — most of these decisions are still right for a
different reason, and the new reason is the valuable half. And **quote what it used to say with the
date**, because the next reader needs to know the reasoning changed rather than that it was always
this.

## Guardrails

**Never edit anything under `subrepos/`.** Those are git-ignored reference clones of
`efcore`, `rlinq`, `aqua`, and `infocarrier-v1`, kept for source-level study. `efcore` is the
authoritative EF Core 10 reference — grep it to confirm API shapes rather than guessing.
Edits there are invisible to git and will be lost.

**Never override a spec test without saying what the store does and where that is shown.** The
inherited `EFCore.Specification.Tests` classes *are* the coverage goal (ADR-004), and silently
suppressing tests was v1's stated failure mode. **This rule read *"Never `[Skip]`, delete, or override
a spec test to make the suite green … a red test is information"* until 2026-09-15.** The suite is
green now and every override carries a typed reason that `OverrideAudit` checks; see the Tier D
paragraphs above and `docs/plans/v10/test-overhaul.md`. A skip needs an upstream skip to copy, a
defect of ours is fixed first, and nothing is filed anywhere unless the owner asks.

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
translator: the whole table came back, silently, which is the defect R75 closed. **When a permission
and the knowledge it guards live on different carriers, check that one reader answers for both.**

**That original instance is closed by construction and the fix it once named is GONE.**
`RelationalQueryRootsFor` does not exist: R135 deleted the option, so there is one implementation
and nothing to reconcile, and `QueryExecutor` holds
`Relational.InfoCarrierRelationalQueryRoots.Instance` directly.

**The rule outlived it, and its live instance is a different pair.** What may be **SENT** is decided
by `InfoCarrierOptionsExtension.AllowedTypesFor`, read per execution in `QueryExecutor`; what may be
**READ BACK** is decided by the DI-scoped `TypeNodeResolver`, whose own allowlist knows only the
model. Those two disagreed silently until a declared projection type came back over the wire:
`Database.SqlQuery<UnmappedCustomer>` cleared the boundary and then failed to materialize its own
rows. `QueryExecutor` now hands the same list to both directions. **A `DbParameter` is sent and
never returned, which is why the older registered type showed nothing** — a one-way fact cannot
expose a two-reader disagreement.

**There are TWO shipped packages since 2026-09-03, and `release.yml` names them one by one.**
`InfoCarrier.Core` and `InfoCarrier.Core.AspNetCore`. The push steps use exact filenames rather than
a glob, so **a third package would ship nothing until that workflow named it**. Every packable
project validates against `10.0.0`.

**PUBLISHING THE SITE IS AN EXPLICIT ACT: `gh workflow run Docs --ref release/10.1`.** The
`deploy` job requires `workflow_dispatch` and a `refs/heads/release/` ref, so no push publishes
anything and `main` cannot publish at all. A push still BUILDS, which keeps `--strict` gating every
commit and every pull request. **The branch dispatched from is the choice of what to publish**, so
a new minor needs no edit to the workflow: cut `release/10.2` and dispatch on it. The site can
therefore go stale, and that is the accepted trade.

**THE SITE PUBLISHES FROM `release/10.1`, NOT FROM `main`, SINCE 2026-09-09.** `main` runs ahead
of nuget.org, and an unversioned site built from it tells every reader to call APIs their package
does not contain. That is not hypothetical: Z1 put a 10.2.0 server timeout in front of 10.1.0
readers and deleted a sentence that was true of their version for being false on `main`.

**So the branch model is maintenance branches, and fixes ORIGINATE ON THE RELEASE BRANCH and are
MERGED UP** (the Symfony and Linux direction, chosen 2026-09-09 over .NET's fix-main-then-backport).
A correction to what the shipped release does is a commit on `release/10.1`, merged into `main`;
`main` is for the next minor. `release/2.2` and `release/3.1` are the v1 line and predate this.

**`build.yml` and `packages.yml` RUN ON `release/**` SINCE 2026-09-09**, so a release line is
gated exactly like the trunk and publishes a candidate to the internal feed before any tag makes a
version permanent. They were `main`-only for part of that day, as a deliberate economy while the
branch carried documentation; the economy was dropped on the argument that a hotfix is urgent by
definition, so the first exercise of an unexercised path would happen under pressure. **The
spec-suite badge is still `main`-only**, because a badge is a claim about the trunk.

**`docs/versioning.md` IS THE RELEASE PROCEDURE**, in two variants sharing a tail, plus the traps
that are invisible from the code. Read it before tagging anything.

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
edited again. M5's last criterion, the remote cancel signal (W6), landed the same day.

**Every milestone is closed, so `docs/plans/v10/implementation-plan.md` is ISSUE-DRIVEN and not
milestone-driven** (corrected 2026-09-09; it read "now holds M5's one remaining criterion" long
after that criterion landed). It holds Phases Q, R, S, T, U, V, Y and Z, each naming the GitHub
issue it serves, and which release a phase lands in is decided on the issue rather than in the plan.
**Phase Y is the 10.1 release preparation.** The letter is Y and not W because **W1 to W6 are M5's
requirement labels**, used throughout `roadmap.md` and the archives; a Phase W would have collided
in the one document where both are read. **Phase Z is #54's idle timeout for a server-held
transaction**, the first of that issue's three separable properties and the first work after the
10.1 release; X was free too and Z was taken because it reads as following Y.

**M7's SQL Server tier is DROPPED (2026-08-24, owner's decision), not deferred.** What is withdrawn
is a *third test tier* for this repository's suite, never support for the store: the server side is
an ordinary EF application and runs against whatever provider it references, so requirements §5 is
unaffected. **The letter C was reused on 2026-09-04 for embedded Firebird** and does not mean SQL
Server any more (ADR-009's dated amendment); nothing about the drop above changed. **The cost is smaller than it first looks and is stated
per feature in `roadmap.md`, because a first reading of it lumped four features together and was
too broad for three of them.** Computed columns, sequences and `rowversion` all reduce, on this
side of the wire, to mechanisms with direct green coverage: store-generated values
(`StoreGeneratedTestBase`, including `OnAddOrUpdate`) and concurrency tokens (67 pass). What is
untested about those three is the *store's* behaviour, which is an ordinary EF concern on the
server and never crosses this wire.

**TPT/TPC WAS "the one real gap" AND IS NOT ONE ANY MORE (corrected 2026-09-09).** This paragraph
read *"no TPT or TPC test class exists here at any tier"* until then, and by then Tier B had four:
`TPTInheritanceQueryInfoCarrierTest`, `TPCInheritanceQueryInfoCarrierTest`,
`TPHInheritanceQueryInfoCarrierTest` and `TPTTableSplittingInfoCarrierTest`, adopted across R5-R12,
plus TPT and TPC variants of Gears of War, bulk updates, relationships and many-to-many. The reason
the gap was real is also closed: it changes the *model*, and since R135 the client carries the
server's mapping strategy rather than core EF's guess (`Relational:MappingStrategy` on every
hierarchy root, in the compiled-model baselines).

**So the rule below is NARROWED rather than broken, and the narrowing is deliberate.** *A
user-facing document may say the three inheritance mappings round trip*, because Tier B is a real
relational store and the coverage is direct rather than a mechanism proxy. **It still may not claim
anything about SQL Server**, which this suite does not run, and that is unchanged for computed
columns, sequences and `rowversion`, whose coverage really is by mechanism. A non-relational backend
tier is recorded as future scope with nothing committed.

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

**`FAILING: 0  TOTAL: 29793`** (2026-09-15, `parity-sides`), across the two projects `measure.sh`
runs: **0 of 29559** in the spec project (`Passed: 29321, Skipped: 238`) and **0 of 234** in ADR-009
Tier D. The one new test is `ServerParameterizationTest`'s inline collection of parameters. It read
`FAILING: 19  TOTAL: 29792` earlier the same day, and the nineteen went by the override rule
above, each with an exact assertion and its reason, not by a skip. **Every figure comes out of the
run's own summary block, and none of them is arithmetic** — a `c10b` entry once carried `Skipped`
over from an earlier run and derived `Passed` from it.

**THERE IS NO BASELINE ANY MORE.** `test/known-failures.txt` and `test/known-failures.names.txt`
held the counts, the failing names and a dated reading of every class of red until 2026-09-15, and
are deleted with the ratchet; git history keeps them, and so does
`docs/plans/v10/archive/implementation-plan-m9-phase-j.md` for the tail as it stood at thirteen.
**The audit is the current answer to "what does this suite not check, and why"**: `OverrideAuditTest`
writes every override with its label, reference, skip and deviation on every run.

**The consumer-facing statement of what is missing is
[`website/docs/limitations.md`](website/docs/limitations.md)**, and that is the document to keep
true. It names one unsupported scenario, message-text differences on a refused
query, and queries this provider *answers* that other EF providers reject. **It no longer claims a
count of any of them**: a count was wrong twice, once because the suite grew under it and once
because the honest number included a family the owner had decided not to name. It is not the
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
  around it has to reproduce constructor binding, so it is priced and not taken. Its override
  asserts the defect's exception and carries `[InfoCarrierDefect(52)]`.

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
- **A relational service on the client needs its companions, and the companions are what this
  client refuses.** Measured twice on 2026-09-07 with two different services, `EntitySplittingConvention`
  (30 -> 149) and `RelationalModelValidator` (30 -> 4037). Neither failed on its own merits; each
  failed on the absence of a neighbour in EF's own list — a key discovery this client cannot vacate,
  and a shared-table convention that decides column names. **Read the list a service sits in before
  adding it alone**; the missing neighbour is named nowhere in the service itself.

The Tier B store is **file-backed** (`<StoreName>.db` in the test output directory), as EF
Core's own `SqliteTestStore` is. Do not move it back to `Mode=Memory;Cache=Shared`: that makes
the database's lifetime a connection's, which makes test-class disposal order load-bearing and
has already produced a 698-test phantom failure. For the same reason **the store must not delete
its file on disposal, and must not release its `Created` entry either** — either one
reintroduces the coupling. The second half survived S3c-5 and produced a nine-test intermittent
failure once the suite passed ten thousand tests: a shared store's disposal re-armed the guard
and let a later class re-seed the file a live one was still using. `DisposeAsync` now releases
nothing. Stale files are swept once at startup instead.

**The runtime culture is pinned to invariant** by a `[ModuleInitializer]`, and that is an instrument
fix rather than a test fix (it was written as a ratchet fix, before the ratchet went). On an `en-SE` machine nine spec tests fail on the decimal separator, none of
them this provider's, which made the suite total a property of the machine. Do not remove it.

**There is no known intermittent. FIVE have been closed, and the FIFTH IS THE ONE THAT HID BEHIND
THE FOURTH** (2026-09-14). Fixing the product defect below did NOT make a whole-tier Tier D run
stable: it still failed 5, then 15, then 9, and the reading "it failed before because of that bug"
was wrong. **Two causes wore one symptom.** The second is a harness defect and the evidence
separates them cleanly: every failure said `An existing connection was forcibly closed by the remote
host` and classes untouched by the work in hand were among the casualties, so the store's PROCESS
was dying rather than the wire lying. **`EmbeddedMongo` was extracted from `DocumentStoreFixture`
and the original was left in place, so the tier had two `StartGate` semaphores.** Each family
serialized against itself and not against the other; the before-and-after process diff that names
"my `mongod`" then picked up a neighbour's server, and teardown killed one whose tests were running.
**A semaphore serializes the callers that share it, and a gate spelt twice is not a gate.** The rule
that transfers is broader than the semaphore: **when an extraction says a duplicate is dangerous,
check in the same commit that the duplicate is gone** — that type's own comment read "two copies of
process bookkeeping is two places to get the reaping wrong" while the second copy compiled beside it.
Four consecutive whole-tier runs of 53 of 53 closed it, and the run time fell from 2m32s to 6s
because the old duration was almost entirely 30-second connection timeouts.

**The FOURTH was a PRODUCT DEFECT rather than a test one** (2026-09-13). Tier D failed about one run in ten, always on the same customer, and
it was not Tier D's: **`InfoCarrierDatabase.CompileQuery` captured `_expressionSerializer` into the
delegate EF caches in `ICompiledQueryCache`**, a singleton shared by every context with the same
options shape. Every context has a serializer of its own — measured, 600 contexts and 600 distinct
instances — and the cached delegate handed all of them the one belonging to whoever compiled first.
Its `ExpressionToNodeTranslator` keeps a translation's state in instance fields, so two contexts
running one query shape trampled each other: `Translation produced no node.` when one cleared
`_result` under another, and a change tracker holding the wrong entities — sometimes NONE, so the
save sent nothing and reported success. **The same mistake had already been fixed once for the
client**, which is what `ClientFor(queryContext)` is; the serializer was left behind by that fix and
`SerializerFor(queryContext)` is its twin.

**Four things about the hunt are worth carrying.** It presented as a document-store problem and cost
hours there; **the tell that it was not was a control** — 600 iterations of plain EF Core against the
same MongoDB store, zero corruption. **Run counting proved nothing at a one-in-ten rate**: three
separate ten-and-fifteen-run batches came back clean and two of them were arms of experiments that
turned out to be irrelevant, which is a one-in-five to one-in-six coincidence each. What closed it
was **reproducing the signature** — `ConcurrentContextTrackingTest`, three workers on SQLite, fails
in two seconds without the fix and passes with it, with its control in the same file. And **the
existing C38 replay diagnostic named the defect the first time it fired**; it was missed for a while
because a `grep` truncated its output to six lines.

**The third was closed on 2026-09-04 by reading the source it came from**, which is the cheapest route of
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
