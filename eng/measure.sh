#!/usr/bin/env bash
#
# One experiment, one number.
#
# Every rewrite in this repo is judged by the same question — did the failure count go down
# without breaking anything — and the answer has to come from a full run. Partial output has
# twice been enough to reach a confident wrong verdict here: phase X1 was reverted as useless
# because its matcher silently never fired, and phase X5 was reverted on a "regression" that was
# a SQLite store limitation plus a structural assertion nobody had read. Both cost more than the
# run they skipped.
#
# So this prints the count *and* keeps the failing test names, because the count alone cannot
# tell "fixed 4, broke 4" from "changed nothing".
#
# SEVERAL PROJECTS, ONE MEASUREMENT. The spec suite is split by backend store, one test project
# each, and every one of them is part of the same number. `projects` below is that list; the
# counters are summed and the failing names are merged into one sorted snapshot, exactly as
# eng/ratchet.sh aggregates the TRX files in CI. One snapshot and not one per project, for the
# reason ratchet.sh states at length: with a snapshot per project, a test that MOVES between
# projects reads as a fix in one and a break in the other.
#
# Usage:
#   eng/measure.sh <label>              run, snapshot as <label>, print counts
#   eng/measure.sh <label> <baseline>   also print what <label> fixed and broke vs <baseline>
#
# Snapshots live in artifacts/measure/ and are plain sorted test names, so they diff with comm.

set -euo pipefail

label=${1:?usage: measure.sh <label> [baseline-label]}
baseline=${2:-}

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
out="$root/artifacts/measure"
mkdir -p "$out"

log="$out/$label.log"
snapshot="$out/$label.txt"
reasons="$out/$label.reasons.txt"

# THE PROJECTS THAT MAKE UP THE SPEC SUITE, and nothing else. Each is measured on its own and the
# figures are added; a project missing from this list is silently missing from every measurement
# and from the baseline it is compared with, so add one here in the same commit that creates it.
#
# InfoCarrier.Core.TransportTests is deliberately ABSENT. It is not a spec project: it holds this
# repository's own HTTP-transport tests, it is expected to be green, and folding it in would
# inflate `total` past what test/known-failures.txt was written against. CLAUDE.md says the same
# thing about pointing a hand run at the .slnx.
projects=(
    "$root/test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj"
)

dotnet build "$root/InfoCarrier.Core.slnx" -v q --nologo > "$out/$label.build.log" 2>&1 || {
    echo "measure: build failed — see $out/$label.build.log" >&2
    grep -E " error " "$out/$label.build.log" | head -5 >&2
    exit 1
}

# THE PROJECTS, one `dotnet test` each, never the solution. The solution also holds
# InfoCarrier.Core.TransportTests, which is not part of this number; and the summary block is
# parsed per run, so one invocation covering several projects would report only the last one.
#
# `|| true`: a red suite is the normal state of this repo (ADR-004), so a non-zero exit from
# `dotnet test` is data, not an error. A run that never produced a summary line *is* an error,
# and is caught below, per project — a crashed host in one project must not be hidden by a clean
# summary from another.
#
# `-v n` is required for the reasons below — the per-failure `Error Message:` detail is simply
# absent at `-v q`. It also changes the summary from a `Failed! - Failed: N, Total: T` one-liner
# to a `Total tests: T` block, so that is what is parsed.
failed=0
total=0
: > "$log"

for project in "${projects[@]}"; do
    name=$(basename "$project" .csproj)
    projectLog="$out/$label.$name.log"

    dotnet test "$project" --no-build -v n --nologo > "$projectLog" 2>&1 || true

    summary=$(grep -E "^Total tests:" "$projectLog" | tail -n 1 || true)
    if [ -z "$summary" ]; then
        echo "measure: $name produced no summary line — the test host probably crashed." >&2
        tail -5 "$projectLog" >&2
        exit 1
    fi

    # Read out of THIS run's own summary block, then added to the running figure. Adding the same
    # figure across runs is the only arithmetic allowed on these counts; deriving one of them from
    # the others is what CLAUDE.md forbids and what has cost three commits here.
    projectFailed=$(sed -n 's/^ *Failed: *\([0-9]*\).*/\1/p' "$projectLog" | tail -n 1)
    projectTotal=$(sed -n 's/^Total tests: *\([0-9]*\).*/\1/p' <<< "$summary")

    # A fully green project prints no `Failed:` line at all, which is 0 and not a parse failure.
    failed=$(( failed + ${projectFailed:-0} ))
    total=$(( total + projectTotal ))

    if [ "${#projects[@]}" -gt 1 ]; then
        echo "  $name: failing ${projectFailed:-0} of $projectTotal"
    fi

    # One combined log, because the name and reason extraction below are line-based greps and a
    # concatenation of the per-project logs is exactly what they want. The per-project logs stay
    # on disk beside it, which is where to look when one project is the odd one out.
    cat "$projectLog" >> "$log"
done

sed -n 's/^\[xUnit\.net [^]]*\] *\(.*\) \[FAIL\]$/\1/p' "$log" | sort -u > "$snapshot"

# The *reasons*, tallied. A snapshot of test names alone cannot tell "this change did nothing"
# from "this change fixed what it aimed at and uncovered the next problem in the same tests" --
# both leave the name list byte-identical. That mistake was made twice in one session and once
# produced a wrong revert (plan L8), so the reasons are now recorded alongside the names.
#
# The `|| true`s matter: an empty reason list is what a green suite looks like, and under
# `set -euo pipefail` a grep that matches nothing would abort the run instead.
{ grep -A 3 "Error Message:" "$log" || true; } |
    { grep -E "^[[:space:]]+(System|Microsoft|Assert|InfoCarrier|Xunit)" || true; } |
    sed 's/^ *//' | cut -c1-120 | sort | uniq -c | sort -rn > "$reasons"

# The total is guarded for the same reason eng/ratchet.sh guards it: a crashed host reports
# fewer failures because fewer tests ran, which looks exactly like progress.
echo "FAILING: $failed  TOTAL: $total  ($label)"

if [ -z "$baseline" ]; then
    exit 0
fi

before="$out/$baseline.txt"
if [ ! -f "$before" ]; then
    echo "measure: no snapshot for baseline '$baseline' at $before" >&2
    exit 1
fi

echo
echo "FIXED  (in $baseline, not in $label):"
comm -23 "$before" "$snapshot" | sed 's/^/  /' || true
echo "BROKEN (in $label, not in $baseline):"
comm -13 "$before" "$snapshot" | sed 's/^/  /' || true

# Always shown, even when both lists above are empty -- that is exactly the case where the
# reasons are the only evidence that anything happened.
beforeReasons="$out/$baseline.reasons.txt"
if [ -f "$beforeReasons" ]; then
    echo
    if diff -q "$beforeReasons" "$reasons" > /dev/null; then
        echo "REASONS: unchanged."
    else
        echo "REASONS changed (-$baseline / +$label):"
        diff "$beforeReasons" "$reasons" | sed 's/^/  /' || true
    fi
fi
