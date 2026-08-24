#!/usr/bin/env python
"""Validate every in-repository Markdown link, including its #anchor.

`mkdocs build --strict` checks that a linked PAGE exists and stops there. It does not check the
fragment, so renaming a heading silently breaks every inbound link to it and the build stays green.
That happened here: `client.md` pointed at `server.md#the-server-is-the-boundary` after that
heading became "Where the checks go", and three strict builds reported no warnings.

This checks both halves: the file resolves, and the anchor matches a heading in it. Anchors are
slugified the way Python-Markdown's `toc` extension does, which is what MkDocs uses.

Usage:  py eng/doc-links.py          every user-facing and internal Markdown file
        py eng/doc-links.py <file>   just these
Exit code 1 if any link is broken.
"""
import glob
import os
import re
import sys

LINK = re.compile(r"\[[^\]]*\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")
HEADING = re.compile(r"^#{1,6}\s+(.*?)\s*$", re.M)
FENCE = re.compile(r"^```.*?^```", re.S | re.M)
INLINE_CODE = re.compile(r"`([^`]*)`")
NOT_SLUG = re.compile(r"[^\w\- ]")


def slug(text):
    """Python-Markdown's toc slugify: strip markup, lowercase, spaces to hyphens."""
    text = INLINE_CODE.sub(r"\1", text)
    text = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", text)   # links keep their text
    text = re.sub(r"[*_]{1,3}", "", text)                   # bold and italics
    text = NOT_SLUG.sub("", text).strip().lower()
    return re.sub(r"[\s]+", "-", text)


def anchors(path):
    with open(path, encoding="utf-8") as handle:
        body = FENCE.sub("", handle.read())
    seen, out = {}, set()
    for raw in HEADING.findall(body):
        base = slug(raw)
        n = seen.get(base, 0)
        seen[base] = n + 1
        out.add(base if n == 0 else "%s_%d" % (base, n))     # toc's duplicate suffix
    return out


def check(files):
    bad = 0
    for path in files:
        here = os.path.dirname(path)
        with open(path, encoding="utf-8") as handle:
            body = FENCE.sub("", handle.read())
        for target in LINK.findall(body):
            if target.startswith(("http://", "https://", "mailto:", "//")):
                continue
            page, _, fragment = target.partition("#")
            resolved = path if not page else os.path.normpath(os.path.join(here, page))
            if page and not os.path.exists(resolved):
                print("  %s -> %s  (no such file)" % (path.replace(os.sep, "/"), target))
                bad += 1
                continue
            if fragment and resolved.endswith(".md") and fragment not in anchors(resolved):
                print("  %s -> %s  (no such heading)" % (path.replace(os.sep, "/"), target))
                bad += 1
    return bad


def main(argv):
    files = argv or (
        ["README.md", "CONTRIBUTING.md"]
        + sorted(glob.glob("src/**/*.md", recursive=True))
        + sorted(glob.glob("website/docs/**/*.md", recursive=True))
        + sorted(glob.glob("docs/**/*.md", recursive=True))
    )
    files = [f for f in files if os.path.exists(f)]
    bad = check(files)
    print("doc-links: %d link%s broken in %d files" % (bad, "" if bad == 1 else "s", len(files)))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
