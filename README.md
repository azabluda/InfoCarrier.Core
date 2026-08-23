# `badges`

Machine-written. One file, no history shared with `main`, no source code.

`spec-suite.json` is a [shields.io endpoint](https://shields.io/badges/endpoint-badge) response.
README.md's spec-suite badge reads it, and the *Publish spec-suite badge* step of
`.github/workflows/build.yml` rewrites it after every spec-ratchet run on `main`, from the
counters `eng/ratchet.sh` parses out of the TRX.

**Do not edit it by hand, and do not delete this branch.** The badge points here, and the workflow
step fails rather than recreating the branch — a badge branch that reappears by accident is a badge
that quietly starts showing nothing.

The alternative to a branch is a Gist plus a personal access token. This repository has no
long-lived credential — see the header of `.github/workflows/release.yml` — and this is why.
