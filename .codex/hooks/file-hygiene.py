#!/usr/bin/env python
"""Protect subrepos before patches and check repository file hygiene afterward."""

import json
import re
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
BOM_MATTERS = {".yml", ".yaml", ".sh", ".json", ".py"}
CRLF_EXEMPT = {".cmd", ".bat"}
BINARY = {
    ".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip",
    ".nupkg", ".snupkg", ".db", ".dll", ".exe",
}
XML_FAMILY = {
    ".xml", ".csproj", ".props", ".targets", ".slnx",
    ".config", ".nuspec", ".resx",
}
PATCH_PATH = re.compile(r"^\*\*\* (?:Add|Update|Delete) File: (.+)$", re.MULTILINE)
MOVE_PATH = re.compile(r"^\*\*\* Move to: (.+)$", re.MULTILINE)


def paths_from_patch(command: object, cwd: Path) -> list[Path]:
    if not isinstance(command, str):
        return []
    raw_paths = PATCH_PATH.findall(command) + MOVE_PATH.findall(command)
    paths = []
    for raw in raw_paths:
        normalized = raw.strip().replace("\\", "/")
        path = Path(normalized)
        paths.append((cwd / path).resolve() if not path.is_absolute() else path.resolve())
    return paths


def inside_repository(path: Path) -> bool:
    try:
        path.relative_to(REPO_ROOT)
    except ValueError:
        return False
    return True


def inside_subrepos(path: Path) -> bool:
    try:
        path.relative_to(REPO_ROOT / "subrepos")
    except ValueError:
        return False
    return True


def problems_for(path: Path) -> list[str]:
    if not path.is_file() or not inside_repository(path):
        return []
    suffix = path.suffix.lower()
    if suffix in BINARY:
        return []
    try:
        data = path.read_bytes()
    except OSError:
        return []
    problems = []
    if suffix in BOM_MATTERS and data.startswith(b"\xef\xbb\xbf"):
        problems.append("has a UTF-8 BOM. Rewrite it without one.")
    if suffix not in CRLF_EXEMPT and b"\r\n" in data:
        problems.append("has CRLF line endings. This repository requires LF.")
    if suffix in XML_FAMILY:
        hits = [
            str(number)
            for number, line in enumerate(data.decode("utf-8", "replace").splitlines(), 1)
            if "<!--" in line
            and "-->" in line
            and "--" in line.split("<!--", 1)[1].rsplit("-->", 1)[0]
        ]
        if hits:
            problems.append(
                "has `--` inside an XML comment on line(s) "
                + ", ".join(hits)
                + ". Rewrite the XML comment."
            )
    return problems


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0
    raw_cwd = payload.get("cwd")
    if not isinstance(raw_cwd, str) or not raw_cwd:
        return 0
    cwd = Path(raw_cwd).resolve()
    if not inside_repository(cwd):
        return 0
    tool_input = payload.get("tool_input") or {}
    if not isinstance(tool_input, dict):
        return 0
    paths = paths_from_patch(tool_input.get("command"), cwd)
    if payload.get("hook_event_name") == "PreToolUse":
        if any(inside_subrepos(path) for path in paths):
            json.dump(
                {
                    "hookSpecificOutput": {
                        "hookEventName": "PreToolUse",
                        "permissionDecision": "deny",
                        "permissionDecisionReason": "Repository policy forbids edits under subrepos/**.",
                    }
                },
                sys.stdout,
            )
        return 0
    problems = [
        f"ERROR: {path} {problem}"
        for path in paths
        for problem in problems_for(path)
    ]
    if not problems:
        return 0
    print("\n".join(problems), file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main())
