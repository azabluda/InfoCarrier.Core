# Documentation style

The rules for every document written for someone outside this repository: the README, the three
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

## Length

The owner removed word-count checks and page budgets on November 10, 2026.
Use enough detail to complete the reader's task. Remove repetition and unnecessary explanation.
Review clarity, working examples, and links instead of enforcing a word limit.

## Rules

Owner direction, November 10, 2026: use plain language throughout the documentation overhaul.
Explain what the reader does and what happens. Define technical terms when they are needed.
Prefer concrete examples over compressed phrases such as "wire admission" or "registration-local
ownership". Keep exact method names, but explain them in ordinary words. This also applies to
the overhaul proposal and progress reports.

Owner review, November 10, 2026: organize explanations around the reader's task and prior knowledge.
For a task guide, establish a working normal case before optional details or unusual restrictions.
Introduce a setting or concept when the reader needs it to take the next step. Put deployment
concerns, tuning, and detailed contracts where their readers need them. Necessary security and
data-loss instructions must still precede the affected action. A working example and a passing
build do not establish that a page has a useful teaching order.

1. **Start with what the reader needs.** Product overviews and package readmes start with an
   identity sentence using `is`: "InfoCarrier.Core is an Entity Framework Core provider...".
   Task guides start with the task or expected result. Reference pages state what they cover.
   Do not force a product introduction into every guide.

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

   **"Today" means "in the newest published package", not "on `main`", and reading it the other
   way is what put a 10.2.0 API in front of 10.1.0 readers on 2026-09-09.** Describing an
   unreleased capability in the present tense is worse than naming it as a plan, because a plan
   only disappoints while a present-tense instruction does not compile. **The site now publishes
   from `release/10.1` rather than from `main`, so this is structural rather than a matter of
   care**, and prose about the next minor may sit on `main` unpublished until that branch is cut.
   What still needs care is the other half: a correction to what the SHIPPED release does belongs
   on the release branch, or nobody reading the site ever sees it.

7. **"The client has no database" appears once per document**, in the opening paragraph. It is the
   premise of the product, not the pitch. Repeating it reads as a sales claim rather than a fact.

8. **A `dotnet add package` command names no version.** The newest stable is the one a reader
   wants, and that is what an unversioned install resolves to. A `PackageReference` or a Central
   Package Management example must name one, because the syntax requires it. No box, no bold, no
   three-variant command list. This rule read the other way until `10.0.0`, when the newest
   stable was `3.1.1` and an unversioned install landed on the wrong line without warning.

9. **Headings in sentence case.**

10. **One sample model across every document.** `ShopContext`, with `Customer` and `Order`, matching
    the `Shop.Shared` project in the first-application tutorial. EF Core uses Blog and Post everywhere,
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
install command and the links. No group headings and no bold lead-in on the list
items: with six lines there is nothing to group, and rule 3 applies here too. Each line ends with the
pull requests it came from, as `(#115, #109)`, which GitHub links; an issue stands in where the
work reached `main` without one. The owner asked for them on 2026-09-28, for the 10.2 body. They
name where a change came from and do not turn the six lines into a changelog, which is what the
superseded `10.0.0` body was.

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
only when `.github/workflows/docs.yml` is dispatched on a `release/` branch, so the release-notes
page of a new version is a 404 until that version's line is published. `docs/versioning.md` applies
the body after the site for this reason. This said the site publishes from `main` until 2026-09-28,
which stopped being true on 2026-09-09.

## Before committing

Run the file through the `humanizer` skill in file mode. Its patterns and this list overlap; the
ones this repository trips most are §7 (stock words), §14 (dashes), §15 (bold), §16 (bold list
headings), §29 (a heading restated in the first sentence) and §31 (dramatic fragments).

Then:

```bash
dotnet run --file eng/DocsServe.cs -- --build              # mkdocs build --strict: a broken link fails the build
grep -c "—" <file>                     # must be 0
```

Reading the page aloud is the check the measurements cannot make.
