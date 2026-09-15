#!/usr/bin/env bash
#
# The spec suite's numbers, read out of its TRX files: the counters the README badge shows, and the
# failing test names for the run's summary. It reports and decides nothing.
#
# THERE IS NO RATCHET SINCE 2026-09-15, AND THIS IS WHAT IS LEFT OF eng/ratchet.sh. The suite used
# to be red on purpose, so CI gated on the direction of the failure count against
# test/known-failures.txt. It is green now, the way EF Core's own provider suites are, and every
# override of a specification test says what the store does and where that is shown, which
# OverrideAudit checks inside the suite (docs/plans/v10/test-overhaul.md). So the gate is
# `dotnet test`'s own exit code: a red test fails the step, and so does a crashed test host, which
# was the other thing the ratchet's total guarded.
#
# The counters are SUMMED across the TRX files, one per test project, and that is the one arithmetic
# this repository allows on them. Each figure is read out of a run's own <Counters> element; `passed`
# is never derived from `total` and `failed`, which is the derivation that has cost three commits.
#
# THE BADGE SHOWS EF PARITY SINCE 2026-09-15, AND NOT THE COUNTERS. A green suite made "passed / total"
# a constant. eng/spec-parity.py joins every result with the reasons OverrideAudit wrote, and needs
# those files beside the TRX: the test steps set INFOCARRIER_OVERRIDE_REASONS to the TRX directory,
# and a run without them is an error rather than a badge that quietly stops meaning anything.
#
# Usage: eng/suite-summary.sh <results.trx> [more.trx ...]
#        Writes counters.env and failures.txt beside the first TRX, and reads every
#        *.override-reasons.tsv there.

set -euo pipefail

if [ $# -lt 1 ]; then
    echo "usage: suite-summary.sh <results.trx> [more.trx ...]" >&2
    exit 2
fi

trx_files=("$@")

for trx in "${trx_files[@]}"; do
    if [ ! -f "$trx" ]; then
        echo "suite-summary: no TRX at '$trx' — that test run produced no results at all." >&2
        exit 1
    fi
done

# Leading space + trailing '=' keeps 'passed' from matching 'passedButRunAborted'.
counter_in() { printf '%s' "$2" | grep -o " $1=\"[0-9]*\"" | grep -o '[0-9]\+' | head -n 1 || true; }

total=0
passed=0
failed=0

for trx in "${trx_files[@]}"; do
    counters=$(grep -o '<Counters[^>]*>' "$trx" | head -n 1)
    if [ -z "$counters" ]; then
        echo "suite-summary: '$trx' has no <Counters> element; cannot read that run's summary." >&2
        exit 1
    fi

    for name in total passed failed; do
        value=$(counter_in "$name" "$counters")
        if [ -z "$value" ]; then
            echo "suite-summary: could not read '$name' from '$trx'." >&2
            exit 1
        fi
        eval "$name=\$(( $name + value ))"
    done
done

echo "Passed: ${passed}, Failed: ${failed}, Total: ${total}"
echo "Read from ${#trx_files[@]} TRX: ${trx_files[*]}"

# build.yml's badge step reads these rather than parsing the TRX a second time: one parser.
results_dir=$(dirname "${trx_files[0]}")
{
    echo "total=${total}"
    echo "passed=${passed}"
    echo "failed=${failed}"
} > "$results_dir/counters.env"

# Candidates are tried rather than assumed: on Windows `python3` resolves to a Microsoft Store stub
# that prints a notice and exits non-zero, and on some Linux images `python` does not exist at all.
python=""
for candidate in python3 python py; do
    if command -v "$candidate" > /dev/null 2>&1 && "$candidate" -c "import sys" > /dev/null 2>&1; then
        python=$candidate
        break
    fi
done
if [ -z "$python" ]; then
    echo "suite-summary: no working Python interpreter found (tried python3, python, py)." >&2
    exit 1
fi

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
"$python" "$here/trx-failures.py" "${trx_files[@]}" > "$results_dir/failures.txt"

shopt -s nullglob
reason_files=("$results_dir"/*.override-reasons.tsv)
shopt -u nullglob
if [ ${#reason_files[@]} -eq 0 ]; then
    echo "suite-summary: no *.override-reasons.tsv in '$results_dir'. The test steps must set INFOCARRIER_OVERRIDE_REASONS to that directory." >&2
    exit 1
fi

"$python" "$here/spec-parity.py" "${reason_files[@]}" -- "${trx_files[@]}" \
    >> "$results_dir/counters.env" 2> "$results_dir/parity.md"
cat "$results_dir/parity.md"

if [ -s "$results_dir/failures.txt" ]; then
    echo
    echo "FAILED:"
    sed 's/^/  /' "$results_dir/failures.txt"
fi

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    {
        echo "## Spec suite"
        echo
        echo "| Passed | Failed | Total |"
        echo "|--:|--:|--:|"
        echo "| ${passed} | ${failed} | ${total} |"
        echo
        cat "$results_dir/parity.md"
        echo
        if [ -s "$results_dir/failures.txt" ]; then
            echo "<details open><summary><b>Failed</b> (${failed})</summary>"
            echo
            sed 's/^/- `/; s/$/`/' "$results_dir/failures.txt"
            echo
            echo "</details>"
        fi
    } >> "$GITHUB_STEP_SUMMARY"
fi
