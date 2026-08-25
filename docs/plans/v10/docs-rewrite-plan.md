# Documentation rewrite plan

Scope: every document written for someone outside this repository. The repository README, the two
NuGet package readmes, the 21 pages of the documentation site, the GitHub release description, and
the package metadata that shows on nuget.org.

Not in scope: anything under `docs/` other than the package readme and this file. Those are
internal and a different audience reads them.

---

## 1. What the reference set does

Measured, not remembered. Sources: `subrepos/efcore` (EF Core 10 itself, 21 package readmes),
`npgsql/efcore.pg`, `PomeloFoundation/Pomelo.EntityFrameworkCore.MySql`,
`mongodb/mongo-efcore-provider`, `serilog/serilog`, and Microsoft's own package-readme guidance at
`NuGet/docs.microsoft.com-nuget`.

### Length

| Document | Reference | Here today |
|---|---|---|
| Provider package readme | EF Core providers: 106 to 160 words. Npgsql: 230. | 843, shared by both packages |
| Core library package readme | EF Core: 405 words | n/a |
| Repository README | EF Core: ~700 words covering two products. Npgsql: ~400. | 537 |
| A single documentation page | Npgsql, MongoDB: one page per topic, a few hundred words | 498 to 1048, average 700, 21 pages |

The site is about 13,000 words. Npgsql documents a full PostgreSQL provider, arrays, ranges, JSON,
NodaTime and spatial, in less.

### Shape of a package readme

Every EF Core provider package ships `PACKAGE.md` with the same five parts:

1. One identity sentence. "`Microsoft.EntityFrameworkCore.Sqlite` is the EF Core database provider
   package for SQLite."
2. `## Usage`, with one short code block showing the one call the reader needs.
3. `## Getting started with EF Core`, a link.
4. `## Additional documentation`, a link.
5. `## Feedback`, a link to the issue tracker.

Microsoft's published template names the same parts and adds "Prerequisites", with the note
"consider excluding this section if your package works without any additional setup".

### Shape of a release

Npgsql's GitHub release body for 10.0.0 is three lines: a link to the release-notes page on the
documentation site, a link to the closed milestone, and the auto-generated "What's Changed" list.
The narrative lives at `npgsql.org/efcore/release-notes/10.0.html`, versioned, on the site.

Ours is 1,400 words of narrative inside the GitHub release body, where nothing links to it and
nothing keeps it current.

### English style

What the reference set does, consistently:

- Plain declarative sentences that start with the subject and use `is`, `has`, `supports`.
- Second person for instructions. "Call the `UseSqlite` method to choose the SQLite database
  provider for your `DbContext`."
- Almost no bold. EF Core's 21 package readmes contain no bold in prose at all.
- No em dashes. EF Core's package readmes: zero. Npgsql's README: zero. This repository: 157
  across the site and the two readmes.
- No admonition boxes. The prerelease problem, which is the same problem this repository has, gets
  exactly one plain sentence in EF Core's README: "Use the `--version` option to specify a preview
  version to install."
- No design rationale. Why a package is split, why a dependency was dropped, what a decision cost:
  none of it appears in a user-facing document. It lives in the repository's own `docs/`.
- No roadmap. No "still to come", no "coming soon", no unreleased feature named as a promise.
- The code sample is deliberately boring and reuses one model throughout: Blog and Post for EF
  Core, Blog for Npgsql, Planet for MongoDB.

---

## 2. The house style that follows

These become `docs/doc-style.md`, so a later session has something to check against.

**Budgets.** Package readme: 250 words maximum. Site page: 400 words, except `limitations.md`,
`upgrading-from-3-1.md` and `first-app.md`, which may reach 600. Repository README: 450 words.
Site total target: 6,000 words.

**Rules.**

1. The first sentence of any document is an identity sentence using `is`.
2. No em dashes or en dashes. Use a comma, a colon, a period, or parentheses.
3. Bold only where a reader loses data or time by missing the sentence. Never as emphasis, never as
   a list mini-heading.
4. Admonition boxes (`!!!`) only for the same case. At most one per page.
5. No design rationale in `website/` or in a package readme. Move it to `docs/` or delete it.
6. No unreleased feature is named anywhere as a plan.
7. "The client has no database" appears once per document, in the opening paragraph. It is the
   premise, not the pitch. Today it appears three times in the README, three times in the NuGet
   readme, and three times on the site index.
8. The install version appears once per document, in the install command. One plain sentence may
   explain the prerelease resolution, in the style of EF Core's README. No box, no bold, no
   before-and-after list of three commands.
9. Headings in sentence case.
10. One sample model across every document. `ShopContext` with `Customer` and `Order`, matching
    `Shop.Shared` in the installation page. Today the README uses `NorthwindContext`, the site
    index uses `ShopContext`, and the limitations page invents `Product`, `Dashboard` and `User`.
11. Every user-facing document ends with a way to report a problem, per Microsoft's guidance.
    Today none of them does.

**Process.** Run each rewritten file through the `humanizer` skill in file mode before committing
it. The skill's §7 word list, §14 dash rule, §15 bold rule, §16 list rule, §29 heading-repeat rule
and §31 dramatic-fragment rule are the ones this repository trips most.

---

## 3. Steps

One substep per commit, message prefixed `Docs <id>:`.

**Gates.** Steps that touch only `docs/` or `website/` need no gate. Step D4 edits two `.csproj`
files, so it is subject to the `src/` rule in CLAUDE.md: `CI=true dotnet build
InfoCarrier.Core.slnx --configuration Release`, then `eng/measure.sh` and `eng/trim-ratchet.sh`.
Every step that touches `website/` runs `eng/docs-serve.sh --build`, which is `mkdocs build
--strict` and fails on a broken internal link or a page missing from the nav.

### D1. Write the style guide

Create `docs/doc-style.md` from section 2 above, with the measurements from section 1 as its
evidence. Link it from `CLAUDE.md`'s "Where authority lives" table.

Nothing else in this plan is checkable without it.

### D2. Remove the two over-stressed themes and the roadmap promises

A mechanical pass across all 23 user-facing files, before any rewriting, so the later steps are not
re-litigating it.

- Six sites name gRPC or `IAsyncEnumerable` as something still to come:
  `docs/nuget-readme.md:113`, `website/docs/index.md:107`,
  `website/docs/getting-started/installation.md:110`, `website/docs/limitations.md:196`,
  `website/docs/limitations.md:201-204`, and the GitHub release body. All are deleted.
  Mentions of gRPC as a transport a reader can write today, at `README.md:35`,
  `docs/nuget-readme.md:56`, `website/docs/configuration/transports.md:94` and
  `website/docs/index.md:57`, are statements of present capability and stay.
- The limitations page keeps one true present-tense entry in place of the streaming promise: a
  query result arrives in one response, so page large result sets. It is a limitation, not a plan.
- "No database" drops to one occurrence per document.
- The version-pinning material drops to one sentence per document. `installation.md` loses its
  `!!! danger` box, which is 14 lines and three variants of one command.
- `Directory.Build.props` line 96 gives gRPC and streaming as the reason `-preview` stays. It is an
  internal comment, but it is the seed the user-facing text grew from, so it is corrected in the
  same commit.

### D3. Rewrite the repository README

Target 450 words, on the Npgsql shape: banner, badges, one identity paragraph, one code sample,
links, credits, license. What goes:

- The "What works" and "What does not" lists move to their homes on the site. The README links.
- The three-block getting-started section (shared model, client, server) is a compressed version of
  `first-app.md`. Keep one block, the client, and link.
- The credits paragraph stays. It is specific, it is history, and it is the kind of detail a
  generic README does not have.

Add a "Getting support" section pointing at the issue tracker, which EF Core, Npgsql and Pomelo all
have and this README does not.

### D4. Split the NuGet readme into two package readmes

Create `src/InfoCarrier.Core/PACKAGE.md` and `src/InfoCarrier.Core.AspNetCore/PACKAGE.md`, each on
the five-part EF Core shape, each 250 words or fewer. Delete `docs/nuget-readme.md`.

Update both `.csproj` files to pack their own `PACKAGE.md`, and update the comment in
`Directory.Build.props` that explains why the package readme is not the repository README. The
reason it gives is still correct.

`InfoCarrier.Core.AspNetCore`'s readme is the short one: it is one method, `app.MapInfoCarrier()`,
and everything else is a link to the main package.

Neither readme explains the package split. That is design rationale, and it moves to
`docs/architecture.md` if it is not there already.

Run the `src/` gates.

### D5. Rewrite the site, page by page

Nav unchanged, so no URL breaks. Order, worst first by the measurements in section 1:

| Order | Page | Today | Target |
|---|---|---|---|
| 1 | `index.md` | 755 words, 14 bold, 9 dashes, 1 box | 400 |
| 2 | `limitations.md` | 1025 words, 16 bold, 12 dashes | 600 |
| 3 | `getting-started/installation.md` | 587 words, 7 bold, 8 dashes, 1 box | 350 |
| 4 | `getting-started/upgrading-from-3-1.md` | 1048 words, 19 dashes | 600 |
| 5 | `getting-started/first-app.md` | 819 words | 600 |
| 6 | `guide/querying.md` | 914 words, 12 dashes | 450 |
| 7 | `platforms/blazor-webassembly.md` | 824 words | 400 |
| 8 | `configuration/server.md` | 734 words, 11 dashes | 400 |
| 9 | `guide/errors.md` | 736 words | 400 |
| 10 | `configuration/value-mappers.md` | 699 words | 400 |
| 11 | `security.md` | 677 words, 12 bold | 400 |
| 12 | `configuration/client.md` | 638 words | 400 |
| 13 | `configuration/transports.md` | 615 words | 400 |
| 14 | `getting-started/samples.md` | 605 words | 300 |
| 15 | `guide/loading-related-data.md` | 585 words | 350 |
| 16 | `saving-changes.md`, `transactions.md`, `api-surface.md` | 566, 551, 498 | 350 each |

Commit in groups of three or four pages, not one per page. Run `eng/docs-serve.sh --build` per
commit.

The site index is the page that carries the most rhetoric and the least information. It is the
first one and it sets the tone for the rest.

### D6. Add a release-notes page and shrink the GitHub release

Create `website/docs/release-notes/10.0.md`, on the Npgsql shape: what is new, what changed since
3.1, and the requirements table. This is the only place in the user-facing set where writing about
the previous version is correct, per the humanizer's §30.

Add it to the nav. Move into it the material the README, the package readmes and the GitHub release
body are each carrying a copy of today.

Then replace the GitHub release body with a stub: one identity line, a link to the release-notes
page, a link to the limitations page, and the install command. About 80 words, from 1,400.

The release body is edited with `gh release edit v10.0.0-preview.1 --notes-file <file>`. The file
is `website/docs/release-notes/10.0.md`'s stub, kept in the repository so the next release copies
it rather than reinventing it.

### D7. Package metadata

`Directory.Build.props` `<Description>` is one sentence and is fine. `InfoCarrier.Core.AspNetCore`'s
`<Description>` in its `.csproj` is 3 lines and explains the package split, which nuget.org truncates
and which is rationale. Cut it to one sentence.

Check `<PackageTags>` against what EF providers use.

Runs the `src/` gates, so it can be folded into D4 if the plan is executed in one sitting.

### D8. Verify

- `eng/docs-serve.sh --build` passes with `--strict`.
- Word counts against the budgets in section 2.
- Re-run the tell scan: em dashes to zero, bold spans under 20 across the whole set, admonition
  boxes under 5.
- `dotnet pack` and confirm both packages carry their own readme.
- Read the site index, the two package readmes and the release body aloud. That is the check the
  measurements cannot make.

---

## 4. Order and why

D1 first, because every later step needs a target to be checkable against. D2 second, because it is
mechanical and because leaving it until the rewrites means arguing about the same sentences twice.
D3 and D4 next, because the README and the package readmes are the two documents most readers see,
and they are 1,380 words of the 14,400. D5 is the bulk of the work and the least urgent. D6 and D7
are small. D8 closes it.


---

## 5. Outcome, 2026-08-23

All eight steps executed on branch `docs/rewrite`.

### Measured, in prose words (`py eng/doc-words.py`)

| | Before | After |
|---|---|---|
| GitHub release body | 1,019 | 116 |
| Package readme | one shared file, 648, in both packages | two files, 247 and 200, one per package |
| Repository README | 340 | 380 |
| The site | 9,578 across 18 pages | 8,878 across 19 |
| The same 18 site pages | 9,578 | 8,325 |
| Em dashes, everything user-facing | 157 | 0 |
| Bold spans in prose | about 140 | 4 |
| Admonition boxes | 13 | 5, at most one per page |
| gRPC or streaming named as a plan | 6 places | 0 |

**A correction to the D5d commit message.** It reads "13,000 prose words to 9,695". Those are two
different rulers: 13,000 was the `wc -w` figure for the site before the rewrite, and 9,695 was the
prose figure for the whole user-facing set after it. The honest pair is the table above. In `wc -w`
terms the site went from 12,876 to 12,100, and most of that total is code blocks either way, which
is why `eng/doc-words.py` exists.

### What the numbers do not show

The site fell 13% in prose on the same pages, which is smaller than the release body's 89% or the
package readme's split. That is the right shape: the site pages were long because they were dense,
and the release body and the package readme were long because they were repeating themselves and
arguing their own design decisions. The style changes carry more of the improvement than the count
does: no em dashes, no bold-as-emphasis, no design rationale, no roadmap, and no boxed warning where
a sentence does the work.

### Deviations from the plan as written

- **D2 shrank.** It was a mechanical pass over 23 files, but D3 to D6 rewrite every one of those
  files anyway, so doing it separately meant editing each twice. D2 became the two internal records
  that produced the user-facing copies (`roadmap.md`'s M8 exit criteria and the `Directory.Build.props`
  comment), and the text removals happened inside each rewrite. Verified by grep in D8.
- **D6 moved earlier.** `upgrading-from-3-1.md` links to the release-notes page, so `mkdocs --strict`
  failed until that page existed. It landed in D5b.
- **The default page budget moved from 400 to 550.** Recorded in `doc-style.md` as a correction: 400
  came from package readmes, which point rather than teach.
- **`eng/doc-words.py` is new**, and was not in the plan. `wc -w` counts fenced code and link URLs,
  which made it useless as a gate.
- **The humanizer skill was not run in file mode, page by page, as section 2 says it should be.** It
  was invoked once, which loaded its full rule set into the session, and the rules were applied while
  each file was written, then checked afterwards by grepping every user-facing file for its patterns:
  the §7 word list, "not just X but Y", false ranges, title-case headings, emoji, curly quotes, §23
  filler, and the §27 and §28 openers. That audit is real and it came back clean. It is not the same
  as running the skill over each file, and the rule in `doc-style.md` still asks for the latter.

### Left open

- Mentions of gRPC remain, in `README.md`, `index.md`, `transports.md` and the release-notes page.
  Each says the transport interface has one method and a gRPC binding is a class a consumer can
  write, which is a present capability rather than a plan. **Kept deliberately, 2026-08-23, by the
  owner's decision**, when the sentence carrying them was rewritten: naming gRPC, a message bus and
  an in-process call shows a reader what the seam is for.
- `docs/infocarrier-core-requirements.md` §4.4 and `docs/wire-protocol.md` W4 still describe
  streaming as something the wire protocol should support. Those are internal design documents
  rather than promises to a consumer, and amending the authoritative requirements spec is a larger
  decision than this plan covers.
- The published release body links to `/release-notes/10.0/`, which 404s until this branch reaches
  `main`, because `.github/workflows/docs.yml` publishes the site from `main`.


### Follow-up, same day

The sentence "`IInfoCarrierTransport` is a single method, so gRPC, a message bus or an in-process
call remains a small class of your own" was rewritten in simpler English. It existed in six places
in three different wordings, and all six now read the same way: HTTP is included; to use gRPC, a
message bus or an in-process call, write one small class; `IInfoCarrierTransport` has one method.
Short sentences, one idea each, and the examples kept.

**Corrected 2026-08-26. Both figures above are wrong, and the claim of uniformity was too.** The
family is nine statements in eight source files, not six: `README.md`, `website/docs/index.md`,
`src/InfoCarrier.Core/PACKAGE.md`, `website/docs/release-notes/10.0.md`,
`website/docs/configuration/transports.md` (twice), `website/docs/getting-started/first-app.md`,
and both files in `docs/release-bodies/`. They did not all read the same way: `README.md` still
said "HTTP is included. To use gRPC, WCF or a message bus" until today. Only two of the nine do
the same job and should match word for word, the README and the site front page, and those two do
now. The other seven differ correctly, because a reference page, an install page and a release
body are not a pitch. The original paragraph is left as written, because it records what was
believed that day.

`docs/release-bodies/v10.0.0-preview.1.superseded-2026-08-23.md` was deleted at the owner's request.
The GitHub release body it archived is gone for good, and everything a reader needed from it is on
the release-notes page.
