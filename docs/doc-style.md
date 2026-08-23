# Documentation style

The rules for every document written for someone outside this repository: the README, the two
package readmes under `src/`, the site under `website/`, the GitHub release bodies, and the package
metadata in `Directory.Build.props`.

`docs/` itself is exempt. A different audience reads it, and its long form is deliberate.

Read this before editing a user-facing document. It exists because the whole set was rewritten
once, in August 2026, after being measured against the projects below and found two to eight times
longer than any of them.

## The reference set

| Project | What was read |
|---|---|
| EF Core 10 | `subrepos/efcore`: 21 `PACKAGE.md` files and the repository README |
| Npgsql | `npgsql/efcore.pg`: README, `src/EFCore.PG/README.md`, the 10.0 release notes, three release bodies |
| Pomelo | `PomeloFoundation/Pomelo.EntityFrameworkCore.MySql`: README |
| MongoDB | `mongodb/mongo-efcore-provider`: README |
| Serilog | `serilog/serilog`: README |
| NuGet | `NuGet/docs.microsoft.com-nuget`: the published package-readme guidance and template |

The numbers below come from those files, not from memory. Re-measure before changing a budget.

## Budgets

| Document | Words | Reference |
|---|---|---|
| Package readme (`src/*/PACKAGE.md`) | 300 | EF Core providers: 106 to 160. Npgsql: 230. |
| Repository README | 450 | Npgsql: about 400. EF Core: about 700 for two products. |
| A site page | 620 | The default |
| A site page that points | 400 | `api-surface.md` only |
| The four deepest pages | 700 | `limitations`, `upgrading-from-3-1`, `release-notes`, `blazor-webassembly` |
| Whole site | 10,000 | 9,473 today across 19 pages, from 9,578 across 18 |

**`py eng/doc-words.py --all --budget` is the measurement, not `wc -w`.** `wc -w` counts fenced
code, mermaid diagrams and the URL inside every link, so a page can be well inside its budget in
prose and double it under `wc -w`. The script holds the same budgets as this table. Keep the two in
step.

**These numbers have been recalibrated twice, and the second time is the finding.** They started at
400, inferred from package readmes. Both times a page went over, the cause was the same: a reader
with no context wanted a fact the page did not have, and facts cost words. Shaving a sentence per
page to defend a number nobody chose on evidence is optimising the ruler.

The order is the part that survives, and it is not negotiable: **cut the padding a reader named,
then move the number. Never the reverse.** A budget yields to a missing fact; it never yields to a
paragraph that could have been shorter.

## Rules

1. **The first sentence is an identity sentence, and it uses `is`.** "`InfoCarrier.Core` is an
   Entity Framework Core provider for the client side of a multi-tier application." Not "serves
   as", not "represents", not "brings the power of".

2. **No em dashes or en dashes.** Use a comma, a colon, a period, or parentheses. EF Core's package
   readmes contain none. Npgsql's README contains none. This repository contained 157 before the
   rewrite. `grep -c "—"` on a file should return 0.

3. **Bold only where a reader loses data or time by missing the sentence.** Never for emphasis,
   never as a list item's mini-heading. EF Core's 21 package readmes contain no bold in prose at
   all.

4. **Admonition boxes (`!!!`) follow the same test, and at most one per page.** A box that repeats
   the paragraph above it in a coloured frame is decoration.

5. **No design rationale about this repository.** What a decision cost, what a previous attempt got
   wrong, which milestone something landed in, why a dependency was dropped: none of it belongs in
   a user-facing document. It goes in `docs/architecture.md` or `docs/decisions.md`.

   **A one-clause "why the API looks like this" is not that, and stays**, when a reader acts on it.
   Three sentences were checked against this rule and kept: why the ASP.NET Core endpoint is a
   separate package (it decides which package you reference), why opting out of a payload bound is
   spelled `null` (you will meet the API), and why no geometry mapper ships (you have to write one).
   The test is whether the reader does something differently for having read it.

6. **No unreleased feature is named as a plan.** No "still to come", no "coming soon", no roadmap.
   A capability a reader can build today may be described as a capability. A capability this
   project intends to ship may not be mentioned at all. Roadmaps in user documentation become
   promises, and they go stale between releases.

7. **"The client has no database" appears once per document**, in the opening paragraph. It is the
   premise of the product, not the pitch. Repeating it reads as a sales claim rather than a fact.

8. **The install version appears once per document, in the install command.** One plain sentence
   may explain that an unversioned install resolves to the older stable line. No box, no bold, no
   three-variant command list. EF Core covers exactly this situation in one sentence: "Use the
   `--version` option to specify a preview version to install."

9. **Headings in sentence case.**

10. **One sample model across every document.** `ShopContext`, with `Customer` and `Order`, matching
    the `Shop.Shared` project in the installation page. EF Core uses Blog and Post everywhere,
    Npgsql uses Blog, MongoDB uses Planet. A reader who moves between pages should not have to
    re-learn the entities.

11. **Every user-facing document ends with a way to report a problem.** A link to the issue
    tracker. Microsoft's package-readme guidance lists this among the things a readme should
    include, and EF Core, Npgsql and Pomelo all do it.

## The shape of a package readme

Five parts, in this order. It is what every EF Core provider ships and what Microsoft's template
describes.

1. One identity sentence.
2. `## Usage`, with one short code block showing the one call the reader needs.
3. `## Getting started`, a link.
4. `## Additional documentation`, a link.
5. `## Feedback`, a link to the issue tracker.

Microsoft's template also lists "Prerequisites", with the note "consider excluding this section if
your package works without any additional setup beyond simple package installation".

## The shape of a release

The narrative goes on the site, at `website/docs/release-notes/<version>.md`, where it is
versioned, linkable and maintained with the rest of the documentation.

**A release body says what changed in THIS release.** It is not a place to describe the product: a
reader on a release page either already knows what the package is or is one click from the
documentation. One clause of identity is the budget, and then the news.

The shape is one clause of identity, then **six lines at most, one per major change**, then the
install command and the links. About 150 words. No group headings and no bold lead-in on the list
items: with six lines there is nothing to group, and rule 3 applies here too.

**Major means a reader has to act on it or decide something.** Not compatible, the target framework,
a dropped dependency, something that now ships that did not, a new boundary, and how it is verified.
Everything else is the release-notes page, which is what the first link is for. A draft of this body
carried eighteen items in three groups and was correctly called too detailed: a release body is a
notice, not the changelog.

GitHub keeps no history of a release body, so the stub lives in `docs/release-bodies/<tag>.md` and
is applied with `gh release edit <tag> --notes-file docs/release-bodies/<tag>.md`. A body being
replaced is archived beside it as `<tag>.superseded-<date>.md` before the edit, because the copy on
GitHub is the only one there was.

**A release body must not link to a site page that has not been deployed yet.** The site publishes
from `main` (`.github/workflows/docs.yml`), so a link to a page added on a branch is a 404 until
that branch merges.

## Before committing

Run the file through the `humanizer` skill in file mode. Its patterns and this list overlap; the
ones this repository trips most are §7 (stock words), §14 (dashes), §15 (bold), §16 (bold list
headings), §29 (a heading restated in the first sentence) and §31 (dramatic fragments).

Then:

```bash
eng/docs-serve.sh --build              # mkdocs build --strict: a broken link fails the build
py eng/doc-words.py --all --budget     # prose words against the budgets above; exit 1 if over
grep -c "—" <file>                     # must be 0
```

Reading the page aloud is the check the measurements cannot make.
