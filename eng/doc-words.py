#!/usr/bin/env python
"""Prose word count for a user-facing Markdown file.

`wc -w` counts fenced code, mermaid diagrams and the URL inside every link, none of which is prose
and none of which a reader experiences as length. A page can be well under its budget in prose and
double it under `wc -w`, which makes `wc -w` useless as a gate. This counts what is left after
dropping fenced blocks, link targets (the link TEXT is kept), inline code, raw HTML and Material's
`:icon-name:` shortcodes.

The budgets are the ones in docs/doc-style.md. Keep the two in step.

Usage:  py eng/doc-words.py <file>...        one line per file
        py eng/doc-words.py --budget <file>  exit 1 if any file is over its budget
        py eng/doc-words.py --all            every user-facing file, with a total
"""
import glob
import re
import sys

BUDGET = {
    "README.md": 450,
    "src/InfoCarrier.Core/PACKAGE.md": 250,
    "src/InfoCarrier.Core.AspNetCore/PACKAGE.md": 250,

    # api-surface.md points at things rather than teaching them, so it is held to the tighter
    # figure the package readmes came from.
    "website/docs/api-surface.md": 400,

    # The home page carries four navigation cards and the install note, which no other page has.
    # Raised 500 -> 600 on 2026-08-23 after a cold read: it was missing facts a reader needs to
    # decide anything, and every one of them costs words. The .NET 10 gate, the second package's
    # name, the price of lazy loading, the price of client-side residual evaluation, and what the
    # 177 skips are. The redundancy the same read found was cut first, so this is the cost of the
    # facts and not of padding.
    "website/docs/index.md": 600,

    # The four deepest pages. Each covers a whole subject rather than one task: every failing
    # scenario in the spec suite, a generation of API change, a working client and server end to
    # end, and three independent browser constraints plus a wiring recipe.
    "website/docs/limitations.md": 600,
    "website/docs/getting-started/upgrading-from-3-1.md": 600,
    "website/docs/getting-started/first-app.md": 600,
    # Raised 600 -> 700 on 2026-08-23, same reason: it never said the release is a preview, and a
    # one-line diff row was carrying the sync-over-async question for every WPF and WinForms
    # caller. Both now have the sentences they needed.
    "website/docs/release-notes/10.0.md": 700,
    "website/docs/platforms/blazor-webassembly.md": 600,
}

# A page that teaches a topic with worked examples. The 400 figure below was inferred from package
# readmes, which POINT at documentation, and it is too tight for a page that teaches: several pages
# sat just over it after every padding cut had been made, which is a wrong ruler rather than long
# pages. 400 is kept only for api-surface.md, which points.
DEFAULT_BUDGET = 550

FENCED = re.compile(r"^```.*?^```", re.S | re.M)
INLINE_CODE = re.compile(r"`[^`]*`")
LINK_TARGET = re.compile(r"\]\([^)]*\)")
BARE_URL = re.compile(r"<?https?://[^\s>)]+>?")
HTML_TAG = re.compile(r"<[^>]+>")
ICON = re.compile(r":[a-z0-9-]+:")
PUNCT = re.compile(r"[|*#>!\[\]]")


def prose(text):
    text = FENCED.sub(" ", text)
    text = INLINE_CODE.sub(" ", text)
    text = LINK_TARGET.sub("] ", text)
    text = BARE_URL.sub(" ", text)
    text = HTML_TAG.sub(" ", text)
    text = ICON.sub(" ", text)
    text = PUNCT.sub(" ", text)
    return len(text.split())


def user_facing():
    files = ["README.md", "src/InfoCarrier.Core/PACKAGE.md",
             "src/InfoCarrier.Core.AspNetCore/PACKAGE.md"]
    files += sorted(p.replace("\\", "/") for p in glob.glob("website/docs/**/*.md", recursive=True))
    return files


def main(argv):
    check = "--budget" in argv
    files = [a for a in argv if not a.startswith("--")]
    if "--all" in argv or not files:
        files = user_facing()

    over = 0
    total = 0
    for path in files:
        key = path.replace("\\", "/")
        with open(path, encoding="utf-8") as handle:
            count = prose(handle.read())
        total += count
        budget = BUDGET.get(key, DEFAULT_BUDGET)
        flag = ""
        if count > budget:
            flag = "  OVER by %d (budget %d)" % (count - budget, budget)
            over += 1
        print("%-52s %4d%s" % (key, count, flag))

    if len(files) > 1:
        print("%-52s %4d  in %d files, %d over budget" % ("TOTAL", total, len(files), over))
    return 1 if (check and over) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
