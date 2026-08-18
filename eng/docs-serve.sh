#!/usr/bin/env bash
# Preview the documentation site locally, with live reload.
#
#     eng/docs-serve.sh            http://127.0.0.1:8000, rebuilds as you save
#     eng/docs-serve.sh --build    build once into website/site and exit (what CI runs)
#
# WHY THIS EXISTS. `mkdocs.yml` has always carried the two commands in a comment, and they are
# still the whole of it -- but they need a virtual environment that is git-ignored, so the first
# thing a fresh clone does is fail. This creates it if it is missing and is then a no-op.
#
# `--strict` IS NOT OPTIONAL IN THE BUILD MODE. A link to a page that does not exist, or a page
# missing from the nav, must fail rather than warn: it is the only automated check a documentation
# site has. `serve` runs without it on purpose, so a half-written link does not stop the preview.
set -euo pipefail

cd "$(dirname "$0")/.."

# `python3` resolves to a Microsoft Store shim on this repository's Windows machines and then
# refuses to run, so a `command -v` test is not enough -- each candidate has to survive executing.
PY=""
for c in python3 python py; do
    if command -v "$c" >/dev/null 2>&1 && "$c" -c "pass" >/dev/null 2>&1; then PY="$c"; break; fi
done
[ -n "$PY" ] || { echo "docs-serve: no working python found (tried python3, python, py)" >&2; exit 1; }

# Windows and Linux lay a virtual environment out differently.
BIN=".venv/bin"
[ -d ".venv/Scripts" ] && BIN=".venv/Scripts"

if [ ! -x "$BIN/mkdocs" ] && [ ! -x "$BIN/mkdocs.exe" ]; then
    echo "docs-serve: creating .venv and installing website/requirements.txt…"
    "$PY" -m venv .venv
    BIN=".venv/bin"; [ -d ".venv/Scripts" ] && BIN=".venv/Scripts"
    "$BIN/pip" install --quiet --disable-pip-version-check -r website/requirements.txt
fi

if [ "${1:-}" = "--build" ]; then
    exec "$BIN/mkdocs" build --strict --config-file website/mkdocs.yml
fi

# No URL is printed here on purpose: mkdocs prints its own, and its own is the correct one. The
# site is served at http://127.0.0.1:8000/InfoCarrier.Core/ rather than at the root, because
# `site_url` in mkdocs.yml carries that path -- matching where GitHub Pages puts it. Requests to
# the root 302 there, so a browser is fine either way and a `curl` without `-L` is not.
exec "$BIN/mkdocs" serve --config-file website/mkdocs.yml "$@"
