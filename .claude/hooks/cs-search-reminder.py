#!/usr/bin/env python
"""PreToolUse reminder: a text search is about to touch a `.cs` file.

CLAUDE.md forbids text search on `.cs` for any question about a symbol, and requires the
`roslyn-codelens` MCP server instead. The rule was broken three times in one session on
2026-09-01 -- always the same way, and never during deliberate navigation. Each was a single
`grep` in the middle of other work, on a file in `subrepos/efcore`, which the MCP server does not
load by default. The tool would have answered `notFound`, so the shortcut looked cheaper.

THIS HOOK DOES NOT DECIDE WHETHER THE SEARCH IS LEGAL, AND MUST NOT TRY. The rule turns on what
is being ASKED, not on what is being typed: `grep "TODO" x.cs` is permitted and `grep "Collate"
x.cs` is not, and the two are indistinguishable as strings. A hook that classified them would be
wrong in both directions -- blocking legal searches and passing illegal ones. So it blocks
nothing, judges nothing, and only says that the file is C#. The decision stays with the model,
where it belongs.

What it DOES decide is a fact rather than a judgement: does this invocation reach a `.cs` file.
Two ways in.

  * The invocation NAMES `.cs` -- a `Grep` whose `path`, `glob` or `type` says C#, or a `Bash`
    command that runs one of the search tools against a path ending in `.cs`.
  * A `Grep` restricted by nothing at all. It has no path, no glob and no type, so it searches the
    repository, and this repository is mostly C#. That is not an inference about intent.

Reading a whole `.cs` file with `cat`, `head` or a `sed` line range is EXPLICITLY PERMITTED and is
deliberately not matched -- it is the correct fallback when the MCP server cannot answer, and the
route this session used correctly after the three violations.

KNOWN OVER-FIRE, AND IT IS THE RIGHT TRADE. The Bash check looks at the command TEXT, so a command
that merely contains the characters `.cs` next to the name of a search tool fires even when it
searches nothing -- writing this file, or editing the rule in CLAUDE.md, did exactly that twice on
the day it was written. Narrowing that away means parsing the command, and a parser that is wrong
in the other direction misses a real search. An extra reminder while writing prose costs nothing;
a missed one costs a violation. Left as is, deliberately.

Exit 0 always. The reminder rides back on `additionalContext`, which reaches the model without
interrupting the user.
"""
import json
import re
import sys

# The search tools CLAUDE.md names. A fixed list -- identifying the TOOL is a fact, not the
# classification this hook refuses to make.
SEARCH_TOOLS = re.compile(r"(?:^|[|;&(`]|\s)(?:grep|egrep|fgrep|rg|ripgrep|findstr|Select-String)\b")

# `sed` is both routes at once, so it is matched separately and by SYNTAX. CLAUDE.md forbids
# `sed -n '/re/p'`, a regex address, and permits `sed -n '1,80p'`, a line range -- which is just a
# way of reading part of a file, and reading is permitted. The two are told apart by the shape of
# the script, not by what it is looking for, so this stays a fact rather than a judgement. Without
# it the hook fired on every `sed -n '1,80p' Foo.cs`, which is the most common LEGAL way this
# session read C# after the three violations. A reminder that fires on correct behaviour is noise,
# and noise is how a reminder stops being read.
SED_REGEX_ADDRESS = re.compile(r"(?:^|[|;&(`]|\s)sed\b[^|;&]*/[^/]*/\s*[a-zA-Z]")

REMINDER = (
    "REMINDER (project rule, not a block): this search touches C# source. CLAUDE.md forbids text "
    "search on a `.cs` file for any question about a SYMBOL -- a type, member, attribute, base "
    "class, override, constraint or reference -- and requires the `roslyn-codelens` MCP server "
    "instead. Text search on `.cs` is permitted for a non-symbol string (a comment, a literal) and "
    "for file-inventory questions; reading a whole file with `cat` or a `sed` line range is "
    "permitted too. If this is a symbol question, use the MCP tool. If the symbol lives outside the "
    "loaded solution -- `subrepos/efcore` is not loaded by default -- the fix is `load_solution`, "
    "never a text search. See `.claude/roslyn-codelens.md`."
)


def targets_csharp(tool_name: str, tool_input: dict) -> bool:
    if tool_name == "Grep":
        named = " ".join(
            str(tool_input.get(key) or "") for key in ("path", "glob", "type")
        ).lower()
        if ".cs" in named or named.strip() in {"csharp", "cs"}:
            return True

        # Restricted by nothing, so it reaches every file in the repository, C# included.
        return not any(tool_input.get(key) for key in ("path", "glob", "type"))

    if tool_name in {"Bash", "PowerShell"}:
        command = str(tool_input.get("command") or "")
        if ".cs" not in command:
            return False

        return bool(SEARCH_TOOLS.search(command)) or bool(SED_REGEX_ADDRESS.search(command))

    return False


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0  # Never fail a turn over an unreadable payload.

    tool_input = payload.get("tool_input") or {}
    if not isinstance(tool_input, dict):
        return 0

    if not targets_csharp(payload.get("tool_name") or "", tool_input):
        return 0

    json.dump(
        {
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "additionalContext": REMINDER,
            },
            "suppressOutput": True,
        },
        sys.stdout,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
