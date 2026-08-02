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

dotnet build "$root/InfoCarrier.Core.slnx" -v q --nologo > "$out/$label.build.log" 2>&1 || {
    echo "measure: build failed — see $out/$label.build.log" >&2
    grep -E " error " "$out/$label.build.log" | head -5 >&2
    exit 1
}

# `|| true`: a red suite is the normal state of this repo (ADR-004), so a non-zero exit from
# `dotnet test` is data, not an error. A run that never produced a summary line *is* an error,
# and is caught below.
dotnet test "$root/InfoCarrier.Core.slnx" --no-build -v q --nologo > "$log" 2>&1 || true

summary=$(grep -E "^(Failed!|Passed!)" "$log" | tail -n 1 || true)
if [ -z "$summary" ]; then
    echo "measure: the run produced no summary line — the test host probably crashed." >&2
    tail -5 "$log" >&2
    exit 1
fi

sed -n 's/^\[xUnit\.net [^]]*\] *\(.*\) \[FAIL\]$/\1/p' "$log" | sort -u > "$snapshot"

failed=$(sed -n 's/.*Failed: *\([0-9]*\).*/\1/p' <<< "$summary")
total=$(sed -n 's/.*Total: *\([0-9]*\).*/\1/p' <<< "$summary")

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
