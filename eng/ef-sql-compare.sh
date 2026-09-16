#!/usr/bin/env bash
# Compares the SQL this provider's server runs with the SQL EF's own SQLite suite expects, in one
# command, and leaves nothing behind.
#
# WHAT IT IS FOR. EF's specification suite is thousands of little users of this provider. They
# report a wrong answer loudly and say nothing at all about a full table crossing the wire. This run
# makes that silent half visible. It is an INVESTIGATION and never a gate: it always exits 0 once it
# has produced a report, nothing it prints is committed, and what we learn from it ends as a promise
# of ours in test/InfoCarrier.Core.FunctionalTests/Sqlite/ServerSqlTest.cs.
#
# WHAT IT DOES, AND THE TRAP EACH STEP REMOVES.
#   1. Reads the EF version from Directory.Packages.props, so nothing has to be kept in sync by hand.
#   2. Uses subrepos/efcore when it happens to be at that version's tag, and otherwise fetches the
#      tag into a temporary folder: one tag, no history, no blobs but the ones the sparse checkout
#      needs. Comparing against a DIFFERENT EF version invents differences that look exactly like
#      our defects, so the version is never guessed.
#   3. Runs Tier B SERIALLY with the log switched on. Parallel tests interleave between two markers,
#      which corrupts the comparison quietly rather than loudly.
#   4. Writes the log to a fresh temporary file. The log is appended to, so a stale one from an
#      earlier run reads exactly like this run's.
#   5. Runs eng/ef-sql-diff.py, which prints the disagreements grouped by kind, dangerous first.
#
# Usage: eng/ef-sql-compare.sh [--filter <xunit filter>] [--no-cache] [--keep] [-- <ef-sql-diff args>]
#   --filter     narrow the run, e.g. --filter NorthwindBulkUpdates (seconds instead of minutes)
#   --no-cache   fetch EF again even if a cached checkout of this version is there
#   --keep       keep the log and say where it is
set -euo pipefail

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
root=$(cd "$here/.." && pwd)
project="$root/test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj"

filter="FullyQualifiedName~InfoCarrier.Core.FunctionalTests.Sqlite"
no_cache=0
keep=0
diff_args=()
while [ $# -gt 0 ]; do
    case "$1" in
        --filter) filter="FullyQualifiedName~$2"; shift 2 ;;
        --no-cache) no_cache=1; shift ;;
        --keep) keep=1; shift ;;
        --) shift; diff_args=("$@"); break ;;
        *) echo "ef-sql-compare: unknown argument '$1'" >&2; exit 2 ;;
    esac
done

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
    echo "ef-sql-compare: no working Python interpreter found (tried python3, python, py)." >&2
    exit 1
fi

# 1. The version this repository runs. One source of truth, and it is the one the build uses.
version=$(sed -n 's/.*PackageVersion Include="Microsoft.EntityFrameworkCore" Version="\[\{0,1\}\([0-9][^,")]*\).*/\1/p' \
    "$root/Directory.Packages.props" | head -1)
if [ -z "$version" ]; then
    echo "ef-sql-compare: no Microsoft.EntityFrameworkCore version in Directory.Packages.props." >&2
    exit 1
fi
tag="v$version"
echo "ef-sql-compare: comparing against dotnet/efcore $tag"

# 2. The reference: the local clone when it is already at that tag, or a fetch of the tag alone.
efcore="$root/subrepos/efcore"
if [ -d "$efcore/.git" ] && [ "$(git -C "$efcore" describe --tags --exact-match HEAD 2> /dev/null || true)" = "$tag" ]; then
    echo "ef-sql-compare: using subrepos/efcore, which is at $tag"
else
    cache="${TMPDIR:-/tmp}/infocarrier-efcore-$version"
    if [ "$no_cache" = "1" ]; then rm -rf "$cache"; fi
    if [ ! -d "$cache/test/EFCore.Sqlite.FunctionalTests" ]; then
        echo "ef-sql-compare: fetching $tag into $cache (one tag, no history)"
        rm -rf "$cache"
        git init -q "$cache"
        git -C "$cache" remote add origin https://github.com/dotnet/efcore.git
        git -C "$cache" sparse-checkout init --cone
        git -C "$cache" sparse-checkout set test/EFCore.Sqlite.FunctionalTests
        git -C "$cache" fetch -q --depth 1 --filter=blob:none origin "refs/tags/$tag:refs/tags/$tag"
        git -C "$cache" checkout -q "$tag"
    else
        echo "ef-sql-compare: using the cached checkout in $cache (--no-cache to fetch again)"
    fi
    efcore="$cache"
fi

# 3 and 4. A serial run, writing to a log nothing else has written to.
work=$(mktemp -d)
log="$work/server-sql.log"
cleanup() {
    if [ "$keep" = "1" ]; then
        echo "ef-sql-compare: the log is at $log"
    else
        rm -rf "$work"
    fi
}
trap cleanup EXIT

echo "ef-sql-compare: running the tier serially with the server SQL log on (minutes, not seconds)"
INFOCARRIER_SERVER_SQL="$log" dotnet test "$project" \
    --configuration Release \
    --filter "$filter" \
    -- xUnit.ParallelizeTestCollections=false \
    || echo "ef-sql-compare: the run reported failures; the comparison below is still about what ran"

# 5. The report. It decides nothing, and its output is read by a person.
"$python" "$here/ef-sql-diff.py" "$log" --efcore "$efcore" "${diff_args[@]+"${diff_args[@]}"}"
