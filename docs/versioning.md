# Versioning and releasing

How a version number is decided, where it is stored, and how each of the three feeds gets one.

## The short version

**The git tag is the version.** There is no version number in any file. `MinVer` reads the tags at
build time and hands the number to MSBuild.

```bash
git tag -a v10.1.0 -m "InfoCarrier.Core 10.1.0"
git push origin v10.1.0
```

That tag produces `InfoCarrier.Core 10.1.0` and
`InfoCarrier.Core.AspNetCore 10.1.0`, and nothing else has to be edited, remembered or
kept in step.

## Why not a property in `Directory.Build.props`

It was one, until N8. The number lived in `VersionPrefix`/`VersionSuffix` **and** in the tag, which
is two sources of truth for one fact — so `release.yml` carried a step whose only job was to check
that they agreed.

**That step was broken, and had been since M8-22.** It read:

```bash
pkg=$(ls artifacts/pack/InfoCarrier.Core.*.nupkg | grep -v Abstractions | sed -E '…')
```

`InfoCarrier.Core.Abstractions` was merged away in M8-22 and `InfoCarrier.Core.AspNetCore` arrived
in the same step. The glob matches both packages; the filter excludes a package that no longer
exists. So `pkg` held two lines and the comparison could never succeed — **every tagged release
would have stopped there.** It was found by packing both products and running the step's own shell
against them, not by reading it.

The lesson is not "fix the filter". It is that a gate exists only because two things can disagree.
Remove the disagreement and the gate has nothing to do.

## What the parts mean

`MAJOR.MINOR.PATCH[-prerelease]`, SemVer 2.0.

| Part | Rule |
|---|---|
| `MAJOR` | **Tracks Entity Framework Core.** `10.x.y` targets EF Core 10. Every EF provider follows this, and it is the fastest way for a reader to know what a package is for. `MinVerMinimumMajorMinor` holds the floor. |
| `MINOR` | Ours. Bump it when the public surface grows, or when the package gains a dependency. |
| `PATCH` | Ours. Bump it for a fix that changes no contract. |
| `-preview.N`, `-rc.N` | **Keep the dot.** SemVer compares dot-separated identifiers, so `preview.10` sorts above `preview.9`. Written `preview10`, they compare as text and sort backwards. |

**AMENDED 2026-09-09, and the amendment is the owner's.** This table read `MAJOR.MINOR` *tracks
Entity Framework Core* until then, with `PATCH` as the only part this repository owned. That leaves
nowhere to put a release that adds public API inside one EF Core minor, and `10.1.0` is exactly
that: it adds three client options, two server registrations and a dependency on
`Microsoft.EntityFrameworkCore.Relational`, on EF Core 10.0. Calling it a patch would make `PATCH`
mean two different things; calling it a major would claim an EF Core this package does not target.
So the minor is ours and the major is EF Core's, which is what SemVer says anyway.

**What a reader loses, stated rather than glossed.** `10.1` no longer implies EF Core 10.1. The
major still implies EF Core 10, and the package's own `Microsoft.EntityFrameworkCore` dependency
carries the exact floor, which is where a resolver looks.

## Where a breaking change goes, and why a bigger minor is not the place

**A minor may not remove a shipped API, however much it adds.** `10.0.1` to `10.1.0` is a large
release by content and that is beside the point: under SemVer a minor is a promise of
*compatibility*, not a measure of size, so the question "is this bump big enough to drop something"
has no version in which the answer is yes. It is also not a matter of judgement here.
`EnablePackageValidation` is on and `PackageValidationBaselineVersion` names the last stable, so
`dotnet pack` downloads it, compares assemblies and **fails the build** on a removed or re-arity'd
member. `ServerQueryExecutor` carries a two-argument constructor overload for exactly this reason,
with the reasoning in its own remarks.

**And the awkward part, which the amendment above created and should not hide: this package has no
slot for a break inside an EF Core generation.** The major is Entity Framework Core's, so `11.0.0`
means EF Core 11 and nothing else. There is no number that says "we broke something and EF did
not".

**So the route is `[Obsolete]`, and it is the only one.** Mark the member in a minor, keep it
working, say what to use instead, and remove it in the major that follows EF Core's own. That is
what EF Core does, and it is what the `10.0.0` compatibility overloads already practise.

The worked case is `IInfoCarrierDocumentMapping`, asked and answered on 2026-09-09. With the
relational reference back, `AnnotationDocumentMapping` could call `GetContainerColumnName()`
directly and the seam could go. It stays: both types shipped in `10.0.0` and
`InfoCarrierDatabase`'s public constructor takes the interface, so removal is a `CP0001`/`CP0002`
the gate refuses. Its own argument survives too, and that is the stronger half: the question is
store-shaped, and a document store recognises an ordinal key by the property's shape rather than by
this annotation.

`10.0.0` carries no suffix. A stable version is a promise not to break the public surface, and
that promise is made as of that release. Do not restate the reason for a suffix in a user-facing
document: `Directory.Build.props` records what the last attempt cost.

**The commit SHA is not part of the package version.** nuget.org does not carry SemVer build
metadata. It goes into `InformationalVersion` instead, which is where a diagnostic needs it:

```
10.0.0-preview.1+901bc81de00c14a899f3a3570d78d432ca58bb5d
```

## The three version fields, which are deliberately different

| Field | Value | Why |
|---|---|---|
| `AssemblyVersion` | `10.0.0.0` | **Pinned to the major, never derived.** A version that moves with every patch forces consumers to rebuild or carry a binding redirect, for a change that is compatible by definition. EF Core and ASP.NET Core both do this. |
| `FileVersion` | `10.0.0.0` | Follows the major too, so a file on disk is identifiable without being a compatibility statement. |
| `InformationalVersion` | `10.0.0-preview.1+sha` | The full truth, including the commit. |

Verified on the built assembly rather than assumed.

## Untagged builds

A commit with no tag on it gets a height, and **what the height is added to depends on the last
tag**. This is the part most likely to surprise, so it is measured rather than described:

| Last tag | Two commits later | Why |
|---|---|---|
| `v10.0.0-preview.1` (a prerelease) | `10.0.0-preview.1.2` | The height is appended to the existing prerelease identifiers. The patch does **not** move. |
| `v10.0.0` (stable) | `10.0.1-alpha.0.2` | Nothing can be appended to a stable version without making it look released, so the patch is bumped and a default prerelease is used. |
| none at all | `10.0.0-alpha.0.512` | The 10.0 line is held by `MinVerMinimumMajorMinor`; the height counts from the root commit. |

All three sort correctly, which is the property that matters for a feed:

```
10.0.0-alpha.0.512  <  10.0.0-preview.1  <  10.0.0-preview.1.2  <  10.0.0-preview.2  <  10.0.0
```

Unique, ordered and installable — exactly what the internal feed wants.

!!! danger "A tag that is not pushed makes CI disagree with your machine"

    The tag is the version, so a tag that exists only locally means local builds and CI builds are
    **different versions of the same commit**. Measured on this repository: with
    `v10.0.0-preview.1` local-only, `dotnet pack` here produced `10.0.0-preview.1.2` while a runner
    — which sees no `v*` tag — produced `10.0.0-alpha.0.512`.

    Neither build fails, and nothing warns. Push the tag when you create it.

!!! warning "MinVer needs the tags"

    Every workflow that builds checks out with `fetch-depth: 0`. A shallow clone has no tags, so
    MinVer falls back to a default **without failing** — the build succeeds and quietly produces
    the wrong number. That is why the setting is load-bearing rather than tidy.

## Two packages, one version

`InfoCarrier.Core` and `InfoCarrier.Core.AspNetCore` are versioned **in lock-step**: they always
ship together at the same number.

This needs no machinery. One version applies to every project, and the `ProjectReference` makes the
dependency come out at that version:

```xml
<dependency id="InfoCarrier.Core" version="10.0.0" />
```

Lock-step is the right model here because the two packages share a wire protocol — a version pair
is a protocol pair. Independent versioning would buy nothing and cost a compatibility matrix.

(NuGet reads `version="10.0.0"` as a *minimum*, not an exact match. That is standard and
deliberate: releasing both together means the newest of each always agree.)

## Where a build goes

| Feed | Trigger | Gate |
|---|---|---|
| **GitHub Packages** | every push to `main` or a release line that touches code | automatic, `packages.yml` |
| **GitHub Release** | a `v*` tag | automatic, `release.yml` |
| **nuget.org** | a `v*` tag | **a human approves the `nuget-org` environment** |

### GitHub Packages

`packages.yml` packs and pushes on every code push. Documentation-only changes are skipped — the
assembly would be identical.

It is an internal feed, and it cannot be anything else: consuming a NuGet package from GitHub
Packages requires a personal access token with `read:packages` **even when the package is public**.
The compensating advantage is that a version there can be deleted, which nuget.org never allows.

```bash
dotnet nuget add source https://nuget.pkg.github.com/azabluda/index.json \
  --name infocarrier-ci --username <you> --password <PAT> --store-password-in-clear-text

dotnet add package InfoCarrier.Core --prerelease
```

### nuget.org

Pushing a tag runs the gates, packs, and creates the Release. The `publish-nuget` job then **stops
and waits for a reviewer**. Approve it and it pushes `InfoCarrier.Core`, then
`InfoCarrier.Core.AspNetCore`.

The order is not cosmetic: `InfoCarrier.Core.AspNetCore` declares a dependency on
`InfoCarrier.Core` at the same version, and nuget.org rejects a package whose dependency does not
resolve.

**Symbols need no step of their own.** `dotnet nuget push` uploads the matching `.snupkg` whenever
it sits beside the `.nupkg`, so two commands produce four uploads.

### What the first release actually did

`10.0.0-preview.1`, 2026-08-18, and it is recorded because a mechanism that has never run is a
design rather than a fact:

| | |
|---|---|
| Gates before packing | `22658` tests, `9` failing — the baseline — and `trim-ratchet: OK (88 <= 88)` |
| Trusted Publishing | `Successfully exchanged OIDC token for NuGet API key` — worked on its first execution, no key anywhere |
| Uploads | four, from two `dotnet nuget push` commands |
| Result | both packages live; `InfoCarrier.Core` gained its first `10.x`, `InfoCarrier.Core.AspNetCore` its first version ever |

**One thing went wrong and it was ours.** A third step pushed the `.snupkg` files a second time and
took a `409` — *"another copy of this symbols package pending validation"*. It was
`continue-on-error`, so the release completed, but it printed a failure annotation on a successful
run. The step is gone.

**And the thing to keep watching:** `3.1.1` is still the latest *stable*, so
`dotnet add package InfoCarrier.Core` with no version continues to resolve to the EF Core 3.1 line.
That stays true until a stable `10.x` ships.

**M8-20's rule is intact — a person still decides, because a pushed version can be unlisted but
never withdrawn.** What changed is where that person stands. They used to run `dotnet nuget push`
from their own machine with their own key. They now approve a protected environment: same gate, but
the key is not on a laptop, the step is repeatable, and the push is recorded against the run that
made it.

### There is no publishing secret

Publishing uses nuget.org's **Trusted Publishing**. The job asks GitHub for an OIDC token,
nuget.org validates it against a policy naming this owner, repository, workflow file and
environment, and returns an API key valid for **one hour**. Nothing long-lived is stored, so
nothing long-lived can leak — and nuget.org's own guidance now calls API keys *"strongly
discouraged"* for automated publishing.

The exchange happens in the step immediately above the pushes, deliberately: each token buys
exactly one key, and requesting it early then pushing late is the documented way to have it expire
mid-release.

**Setup, in two halves. Both are required, and each fails closed on its own.**

| Where | What |
|---|---|
| GitHub | *Settings → Environments → `nuget-org`* → tick **Required reviewers**, name at least one, and **Save**. |
| nuget.org | *Your username → Trusted Publishing → Create*, with the four fields below. |

| Policy field | Value |
|---|---|
| Repository Owner | `azabluda` |
| Repository | `InfoCarrier.Core` |
| Workflow File | `release.yml` — **file name only**, no `.github/workflows/` prefix |
| Environment | `nuget-org` — optional, and worth setting: it pins the policy to the approval-gated job rather than to any job in this workflow |

The policy covers **every package owned by that account**, so neither package has to exist on
nuget.org first — which matters here, because neither does.

!!! warning "Tick *and* save the reviewer"

    Ticking **Required reviewers** without saving leaves the environment with
    `protection_rules: []`, and then a tag publishes with nobody in the loop. Checked over the API
    rather than in the UI: `gh api repos/azabluda/InfoCarrier.Core/environments/nuget-org` must
    show a non-empty `protection_rules`.

!!! note "A new policy on a private repository is *pending* for 7 days"

    It goes inactive if nothing is published in that window; the first successful publish makes it
    permanent. This repository is public, so the policy should be active immediately — but check
    the status in the nuget.org UI if a push is refused.

## Which branch does a change belong on?

One question, asked before the work rather than after it.

| The change | Branch | Reaches `main` by |
|---|---|---|
| A fix for the version people are running | `release/10.1` | merging up |
| Anything for the next minor | `main` | it is already there |
| A correction to what the SHIPPED docs say | `release/10.1` | merging up |
| Documentation for an unreleased feature | `main` | it is already there |

**Fixes originate on the release branch and are merged up**, the direction Symfony and Linux use,
chosen 2026-09-09 over .NET's fix-`main`-then-backport. The reason is that nothing can then be on a
release branch and forgotten: `git merge release/10.1` on `main` either fast-forwards or conflicts,
and neither outcome is silent. The cost is that a fix for both lines starts on the older one.

```
v10.1.0 (tag)
   |
   +-- release/10.1 ---- hotfix ---- v10.1.1 (tag) ----+
   |                                                    | merge up
main ------ 10.2 work --------------------------------- + ---- v10.2.0
```

`release/2.2` and `release/3.1` are the v1 line and predate all of this. Neither `build.yml` nor
`packages.yml` exists on them, and a workflow is read from the branch being pushed, so nothing here
touches them.

## Releasing, start to finish

Both procedures below end at the same place, so the shared tail is written once.

### A hotfix on a release line

1. `git checkout release/10.1 && git pull`.
2. Make the fix. **Confirm the pack baseline is the version you are patching**, not the one before
   it: `grep PackageValidationBaselineVersion Directory.Build.props` must read `10.1.0` on the
   `10.1` line. The trap is in `Directory.Build.props`'s own comment and in the list below.
3. Gates. `CI=true dotnet build InfoCarrier.Core.slnx --configuration Release` clean, both
   ratchets green, `dotnet pack` clean.
4. Push. **CI runs on release lines since 2026-09-09**, so the branch is gated exactly like the
   trunk, and `packages.yml` puts `10.1.1-alpha.0.N` on the internal feed. Install that and try it:
   it is the last point before a version becomes permanent.
5. Tag on **this branch**: `git tag -a v10.1.1 -m "InfoCarrier.Core 10.1.1"` then
   `git push origin v10.1.1`. `release.yml` triggers on `v*` from any branch and carries its own
   build, tests and both ratchets, so it does not depend on `build.yml` having run.
6. Continue at **After either**.

### A minor from `main`

1. Land the work on `main`, gates green.
2. Update `website/docs/limitations.md` if the failure set moved.
3. Tag on `main`: `git tag -a v10.2.0 -m "InfoCarrier.Core 10.2.0"`, `git push origin v10.2.0`.
4. Continue at **After either**, and then cut the new line: `git checkout -b release/10.2 v10.2.0`
   and push it. **Raise `PackageValidationBaselineVersion` to `10.2.0` on that branch too**, for
   the reason in the list below.
5. Publish the site from the new branch, and change the `deploy` job's `if` in `docs.yml` only if
   you narrow it; as written it accepts any `refs/heads/release/` ref, so nothing needs editing.

### After either

6. Watch `release.yml`. It runs the gates, packs, verifies the filenames against the tag, and
   creates the GitHub Release.
7. **Approve `publish-nuget` when you mean it.** It waits for a reviewer, because a pushed version
   can be unlisted but never withdrawn. It pushes `InfoCarrier.Core` first, then
   `InfoCarrier.Core.AspNetCore`, which depends on it at the same version.
8. **Apply the release body, because the workflow does not.** `gh release edit <tag> --notes-file
   docs/release-bodies/<tag>.md`. Archive a body being replaced as `<tag>.superseded-<date>.md`
   first, because GitHub keeps no history of one. Skipping this is invisible from the repository,
   which is how the published `v10.0.0-preview.1` body drifted from its copy here.
9. **Raise `PackageValidationBaselineVersion` to the version just published**, on every branch that
   will build against it. This is the LAST step and not the first: validation downloads the
   baseline package, so it cannot name one that is not on nuget.org yet.
10. **Publish the site, which no push does.**
    `gh workflow run Docs --ref release/10.1`, using the line you want readers to see. Confirm
    `Deploy: success` rather than `skipped` in `gh run list --workflow Docs --limit 1`.
11. Update any version a document names by hand: the `PackageReference` and Central Package
    Management examples on the site, and the counts on the limitations and release-notes pages.
12. If the fix was on a release line, merge it up: `git checkout main && git merge release/10.1`.
    **This is part of the release, not tidying afterwards.** Until it lands, `main` builds a
    version that NuGet orders BELOW the one you just shipped; the table above measures it.

The `dotnet add package` commands name no version, so they need no edit. They did until `10.0.0`,
because the newest stable was then `3.1.1` and an unversioned install silently resolved to it.

## What the numbers do around a hotfix

Measured on 2026-09-09 by rehearsing one end to end on `release/10.1`, tag included, and deleting
it afterwards. Read it when you are mid-hotfix and wondering what the next number will be.

| Point | Branch | Version |
|---|---|---|
| before the fix | `release/10.1` | `10.1.1-alpha.0.6` |
| the fix | `release/10.1` | `10.1.1-alpha.0.7` |
| **at tag `v10.1.1`** | `release/10.1` | **`10.1.1`** |
| next commit after shipping | `release/10.1` | `10.1.2-alpha.0.1` |
| **shipped, not yet merged up** | `main` | **`10.1.1-alpha.0.11`** |
| after merging up | `main` | `10.1.2-alpha.0.1` |
| next commit | `main` | `10.1.2-alpha.0.2` |
| at tag `v10.2.0` | `main` | `10.2.0` |

**`main` DOES change when a tagged hotfix is merged into it, and that is correct.** MinVer takes the
greatest tag reachable from `HEAD`, so `v10.1.1` becoming reachable moves `main` from `10.1.1-alpha`
to `10.1.2-alpha`. Nothing is lost: the numbers stay ordered, and tagging `v10.2.0` still produces
exactly `10.2.0`.

**A prerelease number never predicts the next release.** `MinVerAutoIncrement` is left at its
default, so `main` sits on `10.1.2-alpha.*` while heading for `10.2.0`. The tag decides the version;
the alpha stream only has to be ordered and unique.

**THE WINDOW BEFORE THE MERGE IS THE ONE TO KNOW ABOUT.** While the fix is shipped and not yet
merged, `main` builds `10.1.1-alpha.0.11`, and NuGet orders that BELOW the published `10.1.1`.
Anyone tracking `main` on the internal feed then sees builds that look older than the fix that just
shipped. Merging up resolves it immediately. That is why the merge is a numbered step of the
release and not tidying done afterwards, and it is worth saying because a rushed hotfix is exactly
when the merge gets postponed.

**TWO LINES COUNTING FROM ONE TAG COLLIDE, and the rehearsal found it by failing silently.**
MinVer numbers a prerelease by height above the nearest tag. `main` and `release/10.1` both count
from `v10.1.0`, so a height the release line reaches has usually been published by `main` already.
It recurs rather than happening once: after a hotfix ships and is merged up, both lines count from
the NEW tag and start colliding again from height 1.

Both release-line publishes during the rehearsal answered
`Conflict ... has already been pushed`, were downgraded to a warning by `--skip-duplicate`, and the
job went green. **Nothing was published and the tick said otherwise.** `--skip-duplicate` is gone
and each line now carries its own prerelease identifier, `alpha` for `main` and `hotfix` for a
release line, so the two cannot occupy the same number: the same commit reads `10.1.1-alpha.0.7` or
`10.1.1-hotfix.0.7` depending on where it is built, and both sort below `10.1.1`.

**A duplicate is now a red job**, which is the answer wanted for the case that remains: re-running
a workflow for a commit whose version is already on the feed. There is nothing to do about it and
something to know.

### What the rehearsal proved

Everything except the irreversible step, which stopped by itself. Budget about **twelve minutes**
from pushing the tag to the approval gate; the real `v10.1.0` release took eleven.

- CI on the release line: all four jobs green (docs gates, fast gate, spec suite, spec ratchet).
- `packages.yml` did NOT publish a candidate, and reported success anyway. That is the defect
  above, found only by reading the push step's own output rather than the job's conclusion.
- MinVer at the tag resolved to exactly `10.1.1`, and `release.yml`'s filename check found all four
  expected files.
- The GitHub Release was created with `isPrerelease: false` and was flagged **Latest**, which is
  right while the newest tag is also the newest line, and is the trap named above when it is not.
- `publish-nuget` stopped and waited for a reviewer. Rejecting it ended the run as a failure with
  nothing pushed to nuget.org.

### How to rehearse it again

Worth doing after any change to the release path. Everything below is reversible; the one
irreversible step stops and waits for a person.

1. **Record the anchors you will reset to.** `git rev-parse main release/10.1`.
2. **Commit a stand-in fix on the release line.** A comment in a `.cs` file is enough and risks
   nothing. It must not be documentation only, or `packages.yml` skips the push by design.
3. **Push, and read the PUSH STEP'S OUTPUT rather than the job's conclusion.** That is not
   pedantry: the conflict described above was invisible in the tick and plain in the log.
4. **Tag and push the tag.** `release.yml` runs the gates, packs, verifies the filenames against
   the tag, and creates the Release. Budget about twelve minutes to the gate.
5. **Reject `publish-nuget`.** The run ends as a failure, which is the correct outcome of a
   rehearsal, and nothing reaches nuget.org. Confirm on the package page if you want to see it.
6. **Measure whatever you came for.** Version questions are answerable locally and need no push:
   `dotnet msbuild src/InfoCarrier.Core/InfoCarrier.Core.csproj -t:MinVer -getProperty:MinVerVersion`,
   including on a throwaway merge, which is how the table above was produced without `main` ever
   being force-pushed. **`-t:MinVer` is required**: `-getProperty:Version` on its own answers
   `1.0.0`, because MinVer sets the version in a target and `-getProperty` evaluates before targets
   run.
7. **Clean up.** `gh release delete <tag> --yes --cleanup-tag` (which also removes the local tag),
   `git push origin :refs/tags/<tag>`, then reset the branch and `git push --force-with-lease`.
   Verify with `git log --all --oneline --grep=REHEARSAL` and a tag listing.

## What has bitten us

Each of these cost something real, and none is visible from the code.

- **A release branch cut from the tag inherits the PREVIOUS pack baseline**, because raising it is
  the last step of a release. `release/10.1` carried `10.0.1` until 2026-09-09. That is a hole and
  not a lag: a patch deleting an API the patched release introduced would pass validation, because
  the older baseline never had that API for `CP0002` to compare against. Green gate, shipped break.
- **Merging a release line up can silently discard the fix.** Where a page differs between branches
  because the versions genuinely differ, `git checkout --ours` keeps `main`'s wording and drops
  your correction with it. Resolve those by hand. `git diff origin/release/10.1 origin/main --
  <file>` before you start; empty output means the merge is clean.
- **Nothing publishes the site automatically.** A push to a release line builds the docs and
  deploys nothing, by design. Forgetting step 10 leaves readers on the previous content with no
  error anywhere.
- **The `github-pages` environment only allows branches it is told about.** Enabling Pages names
  the default branch and nothing else, so the first deploy from a release line is rejected before
  it runs a step. `release/*` was added on 2026-09-09; a new pattern is needed only if release
  branches are ever named differently.
- **The GitHub Release is marked latest by GitHub's own rule.** `release.yml` does not set
  `make_latest`, which is correct while the newest tag is also the newest line. **Patching an old
  line after a newer minor exists would advertise the patch as current**, so check the Release
  afterwards the first time that happens.
