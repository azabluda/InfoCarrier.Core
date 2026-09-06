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
# Since it also reads the failing test NAMES, it gates on those too. The count alone cannot tell
# "fixed 4, broke 4" from "changed nothing" -- that is the whole reason eng/measure.sh keeps a name
# snapshot locally, and CI had only the count until this. The names live beside the counts, in
# <baseline-file> with `.names.txt` in place of `.txt`; the two are one baseline in two files
# because `comm` cannot read a file with comments in it and the counts file is mostly comments.
#
# SEVERAL TRX, ONE BASELINE. The spec suite is split across test projects, one per backend store,
# and each `dotnet test` writes its own TRX. Every TRX named on the command line is aggregated into
# a single set of counters and a single sorted name list, which are gated against the one baseline
# pair. **One baseline and not one per project**, deliberately: with a baseline per project, a test
# that MOVES between projects reads as a fix in one and a break in the other, and the name diff
# that makes "fixed 4, broke 4" fail the gate stops working across the boundary. Aggregating first
# keeps that gate whole.
#
# The counters are SUMMED, and that is the one arithmetic this repository allows on them. Each
# figure is still read out of a run's own <Counters> element; what is added is the same figure from
# a second run. `passed` is never derived from `total` and `failed`, which is the derivation that
# has cost three commits here.
#
# Usage: eng/ratchet.sh <results.trx> [more.trx ...] <baseline-file>
#        The LAST argument is the baseline; everything before it is a TRX. A single-TRX call is
#        unchanged from before this was written.

set -euo pipefail

if [ $# -lt 2 ]; then
    echo "usage: ratchet.sh <results.trx> [more.trx ...] <baseline-file>" >&2
    exit 2
fi

# The last argument, however many TRX precede it.
baseline_file=${!#}
trx_files=("${@:1:$#-1}")

for trx in "${trx_files[@]}"; do
    if [ ! -f "$trx" ]; then
        echo "ratchet: no TRX at '$trx' — that test run produced no results at all." >&2
        exit 1
    fi
done

if [ ! -f "$baseline_file" ]; then
    echo "ratchet: no baseline at '$baseline_file'. The last argument is the baseline file." >&2
    exit 1
fi

# Leading space + trailing '=' keeps 'passed' from matching 'passedButRunAborted'.
counter_in() { printf '%s' "$2" | grep -o " $1=\"[0-9]*\"" | grep -o '[0-9]\+' | head -n 1 || true; }
baseline() { grep -E "^$1=" "$baseline_file" | head -n 1 | cut -d= -f2 | tr -d '[:space:]' || true; }

total=0
passed=0
failed=0
executed=0
executed_known=1

# `executed`, and deliberately NOT a skip count. The console block ends `Skipped: 238`, but the
# TRX's <Counters> reports notExecuted="0" -- VSTest records each xUnit skip as its own
# outcome="NotExecuted" result and folds none of them into that aggregate. The only route from this
# element to 238 is `total - executed`, and deriving one figure from another is what this
# repository forbids and has paid for three times. So the report shows what the file actually says.
# Reported, never gated.
for trx in "${trx_files[@]}"; do
    counters=$(grep -o '<Counters[^>]*>' "$trx" | head -n 1)
    if [ -z "$counters" ]; then
        echo "ratchet: '$trx' has no <Counters> element; cannot read that run's summary." >&2
        exit 1
    fi

    trx_total=$(counter_in total "$counters")
    trx_passed=$(counter_in passed "$counters")
    trx_failed=$(counter_in failed "$counters")

    for pair in "total:$trx_total" "passed:$trx_passed" "failed:$trx_failed"; do
        if [ -z "${pair#*:}" ]; then
            echo "ratchet: could not read '${pair%%:*}' from '$trx'." >&2
            exit 1
        fi
    done

    total=$(( total + trx_total ))
    passed=$(( passed + trx_passed ))
    failed=$(( failed + trx_failed ))

    trx_executed=$(counter_in executed "$counters")
    if [ -z "$trx_executed" ]; then
        executed_known=0
    else
        executed=$(( executed + trx_executed ))
    fi
done

if [ "$executed_known" -eq 0 ]; then
    executed="?"
fi

baseline_failed=$(baseline failed)
baseline_total=$(baseline total)

for pair in "baseline failed:$baseline_failed" "baseline total:$baseline_total"; do
    if [ -z "${pair#*:}" ]; then
        echo "ratchet: could not read '${pair%%:*}' from baseline '$baseline_file'." >&2
        exit 1
    fi
done

echo "Passed: ${passed}, Failed: ${failed}, Total: ${total}"
echo "Read from ${#trx_files[@]} TRX: ${trx_files[*]}"
echo "Baseline: failed=${baseline_failed}, total=${baseline_total} (${baseline_file})"

# The spec-suite badge in README.md is built from these three numbers, and build.yml reads them
# from here rather than parsing the TRX a second time: one parser, one place to fix. Written
# before the gates below, because a run that fails the ratchet still has numbers worth publishing
# -- the badge is meant to go red with them.
#
# Beside the FIRST TRX named. CI writes every project's TRX into one results directory, so this is
# that directory whichever project ran first.
counters_file="$(dirname "${trx_files[0]}")/counters.env"
{
    echo "total=${total}"
    echo "passed=${passed}"
    echo "failed=${failed}"
} > "$counters_file"

# ---------------------------------------------------------------------------------------------
# The names. Level 2 of the three eng/measure.sh prints, which CI did not have.

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
names_baseline="${baseline_file%.txt}.names.txt"

# Candidates are tried rather than assumed: on Windows `python3` resolves to a Microsoft Store stub
# that prints a notice and exits non-zero, and on some Linux images `python` does not exist at all.
# Running `import sys` is what separates a real interpreter from the stub.
python=""
for candidate in python3 python py; do
    if command -v "$candidate" > /dev/null 2>&1 && "$candidate" -c "import sys" > /dev/null 2>&1; then
        python=$candidate
        break
    fi
done
if [ -z "$python" ]; then
    echo "ratchet: no working Python interpreter found (tried python3, python, py)." >&2
    exit 1
fi

if [ ! -f "$names_baseline" ]; then
    echo "ratchet: no name baseline at '$names_baseline'. It is generated by" >&2
    echo "         eng/trx-failures.py and committed beside '$baseline_file'." >&2
    exit 1
fi

# Beside the TRX, so it is uploaded with it: this file is exactly what the baseline becomes when a
# fix lowers the count, so updating the baseline is a copy rather than a transcription.
current_names="$(dirname "${trx_files[0]}")/failures.txt"
"$python" "$here/trx-failures.py" "${trx_files[@]}" > "$current_names"

fixed=$(comm -23 "$names_baseline" "$current_names")
broken=$(comm -13 "$names_baseline" "$current_names")
count_of() { printf '%s
' "$1" | wc -l | tr -d '[:space:]'; }

echo
if [ -n "$fixed" ]; then
    echo "FIXED (in the baseline, not in this run):"
    printf '%s
' "$fixed" | sed 's/^/  /'
else
    echo "FIXED: none."
fi
if [ -n "$broken" ]; then
    echo "BROKEN (in this run, not in the baseline):"
    printf '%s
' "$broken" | sed 's/^/  /'
else
    echo "BROKEN: none."
fi

# The run page and, through the checks tab, the pull request. The test report action cannot produce
# this section: it reads the TRX and knows nothing about the baseline, so the delta is ours to
# publish or nobody's.
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    summary_list() {
        if [ -z "$2" ]; then
            printf '**%s:** none.

' "$1"
            return
        fi
        printf '<details open><summary><b>%s</b> (%s)</summary>

' "$1" "$(count_of "$2")"
        printf '%s
' "$2" | sed 's/^/- `/; s/$/`/'
        printf '
</details>

'
    }

    {
        echo "## Spec suite"
        echo
        echo "| | Passed | Failed | Executed | Total |"
        echo "|---|--:|--:|--:|--:|"
        echo "| This run | ${passed} | ${failed} | ${executed} | ${total} |"
        echo "| Baseline | | ${baseline_failed} | | ${baseline_total} |"
        echo
        summary_list "Fixed since the baseline" "$fixed"
        summary_list "Broken since the baseline" "$broken"
        echo "Baseline: \`${baseline_file}\` and \`${names_baseline}\`. The suite is red on"
        echo "purpose (ADR-004); this gates the direction, not the value."
    } >> "$GITHUB_STEP_SUMMARY"
fi

# ---------------------------------------------------------------------------------------------
# The gates.

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

# The gate the count cannot be: a run that fixes four tests and breaks four others leaves `failed`
# untouched and passes everything above.
if [ -n "$broken" ]; then
    echo "::error::$(count_of "$broken") test(s) fail here and do not fail in the baseline. Fix them, or move them into ${names_baseline} with a reason in the commit message."
    status=1
fi

if [ -n "$fixed" ]; then
    echo "::notice::$(count_of "$fixed") test(s) now pass. Copy ${current_names} over ${names_baseline} in the same commit as the fix."
fi

exit $status
