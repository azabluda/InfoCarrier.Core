#!/usr/bin/env bash
# SATELLITE BRANCH ONLY. Measures, test method by test method, where the server reads more rows
# through InfoCarrier than plain EF Core reads for the same query, and prints which methods left or
# joined the list in eng/direct-baseline/suspects.tsv.
#
# WHY TWO RUNS AND WHY USUALLY ONE. Tier B runs once through InfoCarrier and once with InfoCarrier
# removed (INFOCARRIER_DIRECT_CLIENT, see test/InfoCarrier.Core.TestUtilities/DirectClient.cs). A fix
# in src/ cannot change the second run, which has no InfoCarrier in it, so its reads are kept in
# eng/direct-baseline/direct-reads.tsv and only the first run is repeated. Pass --direct when the
# tests or the EF version changed: that runs both halves at once, from two copies of the output
# folder, because the SQLite store files are created in the current directory and two runs must
# not share them.
#
# THE MEASURE IS A LIST AND NOT A COUNT. A fix that removes four suspects and adds four moves no
# count. The report prints every method that LEFT the list and every one that JOINED it.
#
# Usage: eng/direct-remeasure.sh [--direct] [--save] [--keep] [--limit N]
#   --direct  run the plain-EF half again and rewrite direct-reads.tsv
#   --save    rewrite suspects.tsv with this run's list (after a fix has landed)
#   --keep    keep the logs and say where they are
#   --limit   how many shapes to print per class (default 20)
set -euo pipefail

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
root=$(cd "$here/.." && pwd)
project="$root/test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj"
bin="$root/test/InfoCarrier.Core.FunctionalTests/bin/Release/net10.0"
baseline="$here/direct-baseline"
filter="FullyQualifiedName~InfoCarrier.Core.FunctionalTests.Sqlite"

direct=0
save=0
keep=0
limit=20
while [ $# -gt 0 ]; do
    case "$1" in
        --direct) direct=1; shift ;;
        --save) save=1; shift ;;
        --keep) keep=1; shift ;;
        --limit) limit=$2; shift 2 ;;
        *) echo "direct-remeasure: unknown argument '$1'" >&2; exit 2 ;;
    esac
done

# On Windows `python3` can be a Microsoft Store stub that exits non-zero, so candidates are tried.
python=""
for candidate in python3 python py; do
    if command -v "$candidate" > /dev/null 2>&1 && "$candidate" -c "import sys" > /dev/null 2>&1; then
        python=$candidate
        break
    fi
done
if [ -z "$python" ]; then
    echo "direct-remeasure: no working Python interpreter found (tried python3, python, py)." >&2
    exit 1
fi

work=$(mktemp -d)
cleanup() {
    if [ "$keep" = "1" ]; then
        echo "direct-remeasure: the logs are in $work"
    else
        rm -rf "$work"
    fi
}
trap cleanup EXIT

echo "direct-remeasure: building"
dotnet build "$project" --configuration Release > "$work/build.out" 2>&1 \
    || { tail -20 "$work/build.out"; echo "direct-remeasure: the build failed" >&2; exit 1; }

# One serial run of the tier from its own copy of the output folder. Serial, because parallel tests
# interleave their statements between two test markers.
run() {
    local name=$1
    shift
    cp -r "$bin" "$work/bin-$name"
    find "$work/bin-$name" -maxdepth 1 -name "*.db*" -delete
    (cd "$work/bin-$name" && env "$@" INFOCARRIER_SERVER_SQL="$work/$name.log" dotnet test \
        "$work/bin-$name/InfoCarrier.Core.FunctionalTests.dll" \
        --filter "$filter" \
        --results-directory "$work/trx-$name" --logger "trx;LogFileName=$name.trx" \
        -- xUnit.ParallelizeTestCollections=false > "$work/$name.out" 2>&1 || true)
    echo "direct-remeasure: $name: $(grep -E '^(Passed|Failed)!' "$work/$name.out" || echo 'no result line')"
}

echo "direct-remeasure: running Tier B serially through InfoCarrier (about 15 minutes)"
if [ "$direct" = "1" ]; then
    echo "direct-remeasure: and with InfoCarrier removed, at the same time"
    run direct INFOCARRIER_DIRECT_CLIENT=1 &
    run ic INFOCARRIER_DIRECT_CLIENT= &
    wait
    "$python" "$here/trx-failures.py" "$work/trx-direct/direct.trx" > "$work/direct-failures.txt"
    "$python" "$here/direct-compare.py" "$work/ic.log" "$work/direct.log" \
        --direct-failures "$work/direct-failures.txt" --limit 0 \
        --save-direct-reads "$baseline/direct-reads.tsv" > /dev/null
    echo "direct-remeasure: rewrote $baseline/direct-reads.tsv"
else
    run ic INFOCARRIER_DIRECT_CLIENT=
fi

save_args=()
if [ "$save" = "1" ]; then
    save_args=(--save-baseline "$baseline/suspects.new.tsv")
fi

"$python" "$here/direct-compare.py" "$work/ic.log" "$baseline/direct-reads.tsv" \
    --baseline "$baseline/suspects.tsv" --limit "$limit" --dump "$work/diff.tsv" "${save_args[@]+"${save_args[@]}"}"

if [ "$save" = "1" ]; then
    mv "$baseline/suspects.new.tsv" "$baseline/suspects.tsv"
    echo "direct-remeasure: rewrote $baseline/suspects.tsv"
fi
