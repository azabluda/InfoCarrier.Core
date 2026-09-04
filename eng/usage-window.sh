#!/usr/bin/env bash
#
# How much of the current Claude Code usage window is gone, and when it resets.
#
# There is no `claude usage` subcommand: `/usage` is a slash command, and the only way to reach it
# outside the interactive prompt is `claude -p`. That is what this does, and it prints the two lines
# worth reading plus a verdict.
#
# Usage:
#   eng/usage-window.sh            warn at 80% of the session window
#   eng/usage-window.sh 60         warn at 60%
#
# Exit 0 below the threshold, 1 at or above it. So it works in a condition:
#   eng/usage-window.sh 75 || echo "wrap up before starting anything long"
#
# THIS SCRIPT COSTS USAGE TO RUN, which is worth knowing before putting it in a loop. `claude -p`
# starts a session of its own. It is one short request, so the cost is small, but it is not zero and
# it counts against the very window being measured. Call it at boundaries -- after a commit, before
# starting a full suite run -- not on a timer.
#
# `MSYS_NO_PATHCONV=1` IS LOAD-BEARING ON WINDOWS AND IS NOT DECORATION. Git Bash rewrites an
# argument that starts with `/` into a Windows path, so `/usage` reached Claude Code as
# `C:/Program Files/Git/usage`, which is not a command. It was then treated as an ordinary prompt,
# which started a full agent session that read the repository and answered with advice -- an
# expensive way to learn nothing. Without this variable the script silently measures the wrong thing.

set -uo pipefail

threshold=${1:-80}

if ! command -v claude > /dev/null 2>&1; then
    echo "usage-window: the 'claude' CLI is not on PATH." >&2
    exit 2
fi

report=$(MSYS_NO_PATHCONV=1 claude -p "/usage" 2>&1)

# The two lines that carry a number. The session one is the 5-hour window; the week one is the
# longer limit, printed because a run that is fine on the first can still be near the second.
session=$(printf '%s\n' "$report" | grep -m1 'Current session:')
week=$(printf '%s\n' "$report" | grep -m1 'Current week')

if [ -z "$session" ]; then
    echo "usage-window: no 'Current session:' line in the output. Claude Code may have changed its" >&2
    echo "wording, or the CLI is signed out. The raw reply follows." >&2
    printf '%s\n' "$report" >&2
    exit 2
fi

printf '%s\n' "$session"
[ -n "$week" ] && printf '%s\n' "$week"

# `[0-9]+% used` rather than the first number on the line, because the reset date carries digits too.
percent=$(printf '%s\n' "$session" | grep -oE '[0-9]+% used' | grep -oE '^[0-9]+')

if [ -z "$percent" ]; then
    echo "usage-window: found the session line but no percentage in it." >&2
    exit 2
fi

if [ "$percent" -ge "$threshold" ]; then
    echo "usage-window: $percent% >= $threshold%. Finish or revert what is in flight, then wait for the reset above."
    exit 1
fi

echo "usage-window: OK ($percent% < $threshold%)."
