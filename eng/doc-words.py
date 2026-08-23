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
    "src/InfoCarrier.Core/PACKAGE.md": 300,
    "src/InfoCarrier.Core.AspNetCore/PACKAGE.md": 300,

    # api-surface.md points at things rather than teaching them, so it keeps the tighter figure
    # the package readmes came from.
    "website/docs/api-surface.md": 400,

    # The four pages that each cover a whole subject rather than one task: every failing scenario
    # in the spec suite, a generation of API change, three browser constraints plus a wiring
    # recipe, and the release itself.
    "website/docs/limitations.md": 700,
    "website/docs/getting-started/upgrading-from-3-1.md": 700,
    "website/docs/release-notes/10.0.md": 700,
    "website/docs/platforms/blazor-webassembly.md": 700,

    # Added to the 700 tier 2026-08-24, after a verification read. Each covers a whole subject
    # rather than one task: the security model, and the entire failure taxonomy. Both gained
    # facts that were verified in source and that a reader had asked for by name, and the padding
    # the same readers named was cut first.
    "website/docs/security.md": 700,
    "website/docs/guide/errors.md": 700,
}

# RECALIBRATED TWICE, 2026-08-23 and 2026-08-24, and the second time is the signal. These numbers
# started at 400, inferred from package readmes. Both times a page went over, the cause was the
# same: a cold reader wanted a fact the page did not have, and facts cost words. Shaving a sentence
# per page to defend a number I invented is optimising the ruler.
#
# So: 620 is what a page of this kind needs when it is written plainly AND carries what a reader
# needs to act. The order still holds and it is the part that matters -- cut the padding a reader
# named, THEN move the number, never the reverse.
DEFAULT_BUDGET = 620

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
