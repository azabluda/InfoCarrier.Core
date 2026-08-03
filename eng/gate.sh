#!/usr/bin/env bash
#
# A two-hour gate.
#
# Sleeps exactly 7200 seconds, then writes a sentinel line to artifacts/gate-open.txt.
# Launched detached; the exit of this script is the signal that work may begin.

set -euo pipefail

readonly SECONDS_TO_WAIT=7200

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
out="$root/artifacts"
sentinel="$out/gate-open.txt"

mkdir -p "$out"

started=$(date --iso-8601=seconds)
sleep "$SECONDS_TO_WAIT"
finished=$(date --iso-8601=seconds)

echo "gate opened: waited ${SECONDS_TO_WAIT}s, started $started, finished $finished" >> "$sentinel"
