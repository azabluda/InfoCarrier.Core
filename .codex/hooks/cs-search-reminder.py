#!/usr/bin/env python
"""Remind Codex when a shell search reaches C# source in this repository."""

import json
import re
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
SEARCH_TOOLS = re.compile(
    r"(?:^|[|;&(`]|\s)(?:grep|egrep|fgrep|rg|ripgrep|findstr|Select-String)\b",
    re.IGNORECASE,
)
SED_REGEX_ADDRESS = re.compile(
    r"(?:^|[|;&(`]|\s)sed\b[^|;&]*/[^/]*/\s*[a-zA-Z]",
    re.IGNORECASE,
)
REMINDER = (
    "REMINDER (project rule, not a block): this search touches C# source. "
    "AGENTS.md forbids text search on a `.cs` file for a symbol question. "
    "Use the `roslyn-codelens` MCP server instead. Text search remains permitted "
    "for comments, literals, and file inventory. Reading a file is also permitted. "
    "If a symbol is outside the loaded solution, use `load_solution`. See "
    "`.claude/roslyn-codelens.md`."
)


def is_inside_repository(raw_cwd: object) -> bool:
    if not isinstance(raw_cwd, str) or not raw_cwd:
        return False
    try:
        Path(raw_cwd).resolve().relative_to(REPO_ROOT)
    except (OSError, ValueError):
        return False
    return True


def targets_csharp(tool_name: str, tool_input: dict) -> bool:
    if tool_name != "Bash":
        return False
    command = str(tool_input.get("command") or "")
    if ".cs" not in command.lower():
        return False
    return bool(SEARCH_TOOLS.search(command)) or bool(SED_REGEX_ADDRESS.search(command))


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0
    if not is_inside_repository(payload.get("cwd")):
        return 0
    tool_input = payload.get("tool_input") or {}
    if not isinstance(tool_input, dict):
        return 0
    if not targets_csharp(str(payload.get("tool_name") or ""), tool_input):
        return 0
    json.dump(
        {
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "additionalContext": REMINDER,
            }
        },
        sys.stdout,
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
