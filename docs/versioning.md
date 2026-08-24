# Versioning and releasing

How a version number is decided, where it is stored, and how each of the three feeds gets one.

## The short version

**The git tag is the version.** There is no version number in any file. `MinVer` reads the tags at
build time and hands the number to MSBuild.

```bash
git tag -a v10.0.1 -m "InfoCarrier.Core 10.0.1"
git push origin v10.0.1
```

That tag produces `InfoCarrier.Core 10.0.1` and
`InfoCarrier.Core.AspNetCore 10.0.1`, and nothing else has to be edited, remembered or
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
| `MAJOR.MINOR` | **Tracks Entity Framework Core.** `10.0.x` targets EF Core 10.0. Every EF provider follows this, and it is the fastest way for a reader to know what a package is for. `MinVerMinimumMajorMinor` holds the floor. |
| `PATCH` | Ours. Bump it for a fix that changes no contract. |
| `-preview.N`, `-rc.N` | **Keep the dot.** SemVer compares dot-separated identifiers, so `preview.10` sorts above `preview.9`. Written `preview10`, they compare as text and sort backwards. |

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
| **GitHub Packages** | every push to `main` that touches code | automatic, `packages.yml` |
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

## Releasing, start to finish

1. Land the work. `CI=true dotnet build InfoCarrier.Core.slnx --configuration Release` clean, both ratchets green.
2. Update `website/docs/limitations.md` if the failure set moved.
3. Tag: `git tag -a v10.0.1 -m "InfoCarrier.Core 10.0.1"`.
4. Push the tag: `git push origin v10.0.1`.
5. Watch `release.yml`. It runs both gates, packs, and creates the Release.
6. Approve `publish-nuget` when you mean it.
7. **Apply the release body, because the workflow does not.** `release.yml` creates the Release
   with GitHub's generated notes plus a paragraph about the reviewer gate, and that is not the
   body this repository wrote: `gh release edit <tag> --notes-file docs/release-bodies/<tag>.md`.
   Archive a body being replaced as `<tag>.superseded-<date>.md` first, because GitHub keeps no
   history of one. Skipping this is not visible from the repository, which is how the published
   `v10.0.0-preview.1` body drifted from its copy here.
8. Update any version a document names by hand: the `PackageReference` and Central Package
   Management examples on the site, and the counts on the limitations and release-notes pages.

The `dotnet add package` commands name no version, so they need no edit. They did until `10.0.0`,
because the newest stable was then `3.1.1` and an unversioned install silently resolved to it.
