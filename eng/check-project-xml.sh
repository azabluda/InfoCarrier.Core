#!/usr/bin/env bash
# Parse every MSBuild project file as XML, and fail if any of them is malformed.
#
# WHY THIS EXISTS. Two of the three CI failures in one afternoon were a malformed project file, and
# neither said so. The second one reported:
#
#     error NETSDK1124: Trimming assemblies requires .NET Core 3.0 or higher
#
# which names trimming and has nothing to do with trimming: `Directory.Build.props` would not
# parse, so there was no TargetFramework for the check to be satisfied by, and that is what MSBuild
# says when it finds none. The same error had appeared once before from a completely different
# cause (a stale obj/Release), so its own history is misleading too.
#
# THE CAUSE BOTH TIMES WAS A DOUBLE HYPHEN INSIDE AN XML COMMENT. `--` is illegal there, and it is
# very easy to write while documenting a command:
#
#     <!-- CI=true dotnet build InfoCarrier.Core.slnx --configuration Release -->
#
# That is a comment explaining how to keep CI green, and it is what turned CI red. Use `-c Release`.
#
# This runs in about a second, before restore, so the failure names the file and the line instead
# of arriving later disguised as an SDK error.
set -euo pipefail

cd "$(dirname "$0")/.."

# Pick an interpreter that actually RUNS, rather than one that merely resolves. On Windows,
# `python3` resolves to a Microsoft Store stub that exists on PATH and then refuses to execute --
# so `command -v` is not enough, and trusting it made this script report all ten project files
# malformed on its first run. A gate that cannot be run locally is the problem it exists to fix.
PY=""
for candidate in python3 python py; do
  if command -v "$candidate" >/dev/null 2>&1 && "$candidate" -c "pass" >/dev/null 2>&1; then
    PY="$candidate"
    break
  fi
done

if [ -z "$PY" ]; then
  echo "check-project-xml: no working Python found (tried python3, python, py); skipping." >&2
  exit 0
fi

failed=0
count=0

# Everything MSBuild reads as XML. `obj/` and `bin/` hold generated copies; skip them.
while IFS= read -r file; do
  count=$((count + 1))
  if ! error=$("$PY" -c "
import sys, xml.etree.ElementTree as ET
try:
    ET.parse(sys.argv[1])
except Exception as e:
    print(e); sys.exit(1)
" "$file" 2>&1); then
    echo "::error file=$file::Malformed XML: $error"
    echo "  INVALID  $file"
    echo "           $error"
    failed=$((failed + 1))
  fi
done < <(find . \
  \( -path ./subrepos -o -path ./artifacts -o -name obj -o -name bin \) -prune -o \
  \( -name '*.csproj' -o -name '*.props' -o -name '*.targets' \) -print)

if [ "$failed" -gt 0 ]; then
  echo "check-project-xml: $failed of $count project files are malformed."
  exit 1
fi

echo "check-project-xml: OK ($count files parsed)."
