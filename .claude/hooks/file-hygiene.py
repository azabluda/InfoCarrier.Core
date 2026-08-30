#!/usr/bin/env python
"""PostToolUse guard: catch the three file-hygiene faults that have each cost a debugging round-trip
here. Reads the hook's JSON payload on stdin, checks the one file the tool just wrote, and exits 2
with a message on stderr so Claude is told and can fix it in the same turn.

Python and not a shell one-liner for two reasons: there is no `jq` on this machine, and the payload
carries Windows paths as JSON-escaped backslashes, which only a real JSON parser unescapes correctly.

Each rule below is scoped to the files where the fault actually breaks something. The scoping is not
caution, it is measured: on 2026-08-30 a blanket BOM rule would have fired on 17 tracked .cs files
that carry a BOM harmlessly, turning the guard into noise and inviting gratuitous re-encoding of
source files. The narrow rules below have a clean baseline -- zero tracked files violate them.
"""
import json
import sys
from pathlib import Path

# A BOM is only a defect where a parser or an interpreter chokes on it. That is what happened to the
# generated release.yml. It is NOT a defect in .cs or .md, where Visual Studio writes one routinely.
BOM_MATTERS = {".yml", ".yaml", ".sh", ".json", ".py"}

# .gitattributes settles this: `* text=auto eol=lf`, with .cmd/.bat the stated exception because
# cmd.exe can mis-parse an LF-only batch file. A CRLF eng/*.sh fails in CI as `$'\r': command not
# found`, which is the fault this catches.
CRLF_EXEMPT = {".cmd", ".bat"}

# Binary per .gitattributes. Never inspect the bytes of these.
BINARY = {".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip",
          ".nupkg", ".snupkg", ".db", ".dll", ".exe"}

# `--` is illegal inside an XML comment per the XML spec, and the error it produces names the wrong
# line. Line-based, so it catches the single-line case, which is the one that occurs.
XML_FAMILY = {".xml", ".csproj", ".props", ".targets", ".slnx",
              ".config", ".nuspec", ".resx"}


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0  # Not our business to fail a turn over an unreadable payload.

    tool_input = payload.get("tool_input") or {}
    tool_response = payload.get("tool_response") or {}
    raw = tool_response.get("filePath") or tool_input.get("file_path")
    if not raw:
        return 0

    path = Path(raw)
    if not path.is_file():
        return 0  # Deleted, or never written.

    # Only police this repository. Scratchpad and temp files are not ours to normalise.
    repo_root = Path(__file__).resolve().parents[2]
    try:
        path.resolve().relative_to(repo_root)
    except ValueError:
        return 0

    suffix = path.suffix.lower()
    if suffix in BINARY:
        return 0

    try:
        data = path.read_bytes()
    except OSError:
        return 0

    problems = []

    if suffix in BOM_MATTERS and data.startswith(b"\xef\xbb\xbf"):
        problems.append("has a UTF-8 BOM. Rewrite it without one -- a BOM breaks the shebang in a "
                        "shell script and strict parsers in YAML and JSON.")

    if suffix not in CRLF_EXEMPT and b"\r\n" in data:
        problems.append("has CRLF line endings. This repository is LF everywhere, in the working "
                        "tree as well as in git (.gitattributes: `* text=auto eol=lf`). A CRLF "
                        r"shell script fails in CI as `$'\r': command not found`.")

    if suffix in XML_FAMILY:
        hits = [str(n) for n, line in enumerate(data.decode("utf-8", "replace").splitlines(), 1)
                if "<!--" in line and "-->" in line
                and "--" in line.split("<!--", 1)[1].rsplit("-->", 1)[0]]
        if hits:
            problems.append("has `--` inside an XML comment on line(s) " + ", ".join(hits) +
                            ". That is illegal per the XML spec and the parser error names the "
                            "wrong line. Rewrite the comment text.")

    if not problems:
        return 0

    for problem in problems:
        print(f"ERROR: {path} {problem}", file=sys.stderr)
    return 2  # Exit 2 feeds stderr back to Claude, which is the point of the guard.


if __name__ == "__main__":
    sys.exit(main())
