# Versioning and releasing

How a version number is decided, where it is stored, and how each of the three feeds gets one.

## The short version

**The git tag is the version.** There is no version number in any file. `MinVer` reads the tags at
build time and hands the number to MSBuild.

```bash
git tag -a v10.0.0-preview.2 -m "InfoCarrier.Core 10.0.0-preview.2"
git push origin v10.0.0-preview.2
```

That tag produces `InfoCarrier.Core 10.0.0-preview.2` and
`InfoCarrier.Core.AspNetCore 10.0.0-preview.2`, and nothing else has to be edited, remembered or
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

`-preview` stays until there is a gRPC binding and streaming results, because both may change
`IInfoCarrierTransport` and a stable `10.0.0` is a promise not to.

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
<dependency id="InfoCarrier.Core" version="10.0.0-preview.1" />
```

Lock-step is the right model here because the two packages share a wire protocol — a version pair
is a protocol pair. Independent versioning would buy nothing and cost a compatibility matrix.

(NuGet reads `version="10.0.0-preview.1"` as a *minimum*, not an exact match. That is standard and
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
and waits for a reviewer**. Approve it and it pushes `InfoCarrier.Core` first, then
`InfoCarrier.Core.AspNetCore`, then the symbol packages.

The order is not cosmetic: `InfoCarrier.Core.AspNetCore` declares a dependency on
`InfoCarrier.Core` at the same version, and nuget.org rejects a package whose dependency does not
resolve.

**M8-20's rule is intact — a person still decides, because a pushed version can be unlisted but
never withdrawn.** What changed is where that person stands. They used to run `dotnet nuget push`
from their own machine with their own key. They now approve a protected environment: same gate, but
the key is not on a laptop, the step is repeatable, and the push is recorded against the run that
made it.

**Setup, in two halves:**

| | Status |
|---|---|
| *Settings → Environments → `nuget-org`*, with **Required reviewers** | done |
| `NUGET_API_KEY`, scoped to that environment | not yet — first upload is by hand |

**Without the key the job fails on purpose**, at its first step, having pushed nothing. It prints
the four `dotnet nuget push` commands into the run summary, filled in with the version being
released, and the Release body carries the same list. The release itself is unaffected: `release`
has already packed, gated and published the GitHub Release before `publish-nuget` is even offered
for approval.

**A green "Publish to nuget.org" that pushed nothing would be worse than a red one.** That is the
false-clearance shape this repository has been bitten by before — a gate that passes for ever
because it silently does nothing. A red job that tells you exactly what to type is not that.

If nuget.org **Trusted Publishing** is available to this account, prefer it over adding the key —
it authenticates the workflow by OIDC and removes the long-lived secret entirely. Check its current
status before relying on it.

## Releasing, start to finish

1. Land the work. `CI=true dotnet build` clean, both ratchets green.
2. Update `docs/limitations.md` if the failure set moved.
3. Tag: `git tag -a v10.0.0-preview.2 -m "InfoCarrier.Core 10.0.0-preview.2"`.
4. Push the tag: `git push origin v10.0.0-preview.2`.
5. Watch `release.yml`. It runs both gates, packs, and creates the Release.
6. Approve `publish-nuget` when you mean it.
7. Update the version named in `README.md`, `docs/nuget-readme.md` and the site's installation page.

Step 7 is the one thing still done by hand, and it is deliberate: an install instruction naming a
version is worth more to a reader than one that says "latest".
