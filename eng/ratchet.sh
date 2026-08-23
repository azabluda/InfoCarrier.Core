#!/usr/bin/env bash
#
# Spec-suite failure ratchet (roadmap.md §CI strategy).
#
# The inherited EFCore.Specification.Tests suite is legitimately red during build-out and
# CLAUDE.md forbids skipping tests to force it green, so CI gates on the *direction* of the
# failure count rather than its value.
#
# It also guards the total. A run that crashes the test host reports fewer failures because
# fewer tests ran — that is how a stack overflow introduced in step K3c came within one
# measurement of looking like an improvement. A shrinking total is a regression, not progress.
#
# Usage: eng/ratchet.sh <results.trx> <baseline-file>

set -euo pipefail

trx=${1:?usage: ratchet.sh <results.trx> <baseline-file>}
baseline_file=${2:?usage: ratchet.sh <results.trx> <baseline-file>}

if [ ! -f "$trx" ]; then
    echo "ratchet: no TRX at '$trx' — the test run produced no results at all." >&2
    exit 1
fi

counters=$(grep -o '<Counters[^>]*>' "$trx" | head -n 1)
if [ -z "$counters" ]; then
    echo "ratchet: '$trx' has no <Counters> element; cannot read the run summary." >&2
    exit 1
fi

# Leading space + trailing '=' keeps 'passed' from matching 'passedButRunAborted'.
counter() { printf '%s' "$counters" | grep -o " $1=\"[0-9]*\"" | grep -o '[0-9]\+' | head -n 1 || true; }
baseline() { grep -E "^$1=" "$baseline_file" | head -n 1 | cut -d= -f2 | tr -d '[:space:]' || true; }

total=$(counter total)
passed=$(counter passed)
failed=$(counter failed)

baseline_failed=$(baseline failed)
baseline_total=$(baseline total)

for pair in "total:$total" "passed:$passed" "failed:$failed" \
            "baseline failed:$baseline_failed" "baseline total:$baseline_total"; do
    if [ -z "${pair#*:}" ]; then
        echo "ratchet: could not read '${pair%%:*}'. TRX: '$trx', baseline: '$baseline_file'." >&2
        exit 1
    fi
done

echo "Passed: ${passed}, Failed: ${failed}, Total: ${total}"
echo "Baseline: failed=${baseline_failed}, total=${baseline_total} (${baseline_file})"

# The spec-suite badge in README.md is built from these three numbers, and build.yml reads them
# from here rather than parsing the TRX a second time: one parser, one place to fix. Written
# before the gates below, because a run that fails the ratchet still has numbers worth publishing
# -- the badge is meant to go red with them.
counters_file="$(dirname "$trx")/counters.env"
{
    echo "total=${total}"
    echo "passed=${passed}"
    echo "failed=${failed}"
} > "$counters_file"

status=0

if [ "$total" -lt "$baseline_total" ]; then
    echo "::error::Total dropped ${baseline_total} -> ${total}. Tests stopped running rather than started passing — check for a crashed test host."
    status=1
fi

if [ "$failed" -gt "$baseline_failed" ]; then
    echo "::error::Failures rose ${baseline_failed} -> ${failed}. Fix the regression or state why the baseline moves."
    status=1
elif [ "$failed" -lt "$baseline_failed" ]; then
    echo "::notice::Failures fell ${baseline_failed} -> ${failed}. Lower the baseline in the same commit as the fix."
fi

exit $status
