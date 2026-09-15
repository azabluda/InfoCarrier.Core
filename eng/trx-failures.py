#!/usr/bin/env python3
"""Print the names of the failing tests in one or more TRX files, one per line, sorted.

This is the second of the three levels eng/measure.sh prints locally, brought to CI, where
eng/suite-summary.sh lists the names in the run summary. Until 2026-09-15 it also fed
test/known-failures.names.txt and eng/ratchet.sh's name gate; both are gone with the ratchet.

SEVERAL TRX, ONE SORTED LIST. The spec suite is split across test projects, and each produces its
own TRX. The names are UNIONED and sorted together, so that eng/measure.sh can compare two snapshots
across the whole suite: with one list per project, a test that moves between projects would read as
a fix in one and a break in the other.

A name appearing in two TRX files is one entry. That is a set union and not a bug: a fully
qualified xUnit test name already carries its class, so two projects cannot legitimately produce
the same name unless the same test really ran twice.

Why Python and not grep. A TRX is XML, and '>' is legal *unescaped* inside an XML attribute value,
so `grep -o '<UnitTestResult[^>]*>'` truncates the element on any test whose name contains one.
Theory arguments are printed into `testName`, so that is not hypothetical.

Why iterparse. The suite is 29k tests and the TRX is tens of megabytes; iterparse clears each
element as it closes instead of holding the whole tree.

Sorting is by Unicode code point, which is what `LC_ALL=C sort` produces for UTF-8 as well, so the
output can be fed to `comm` against a file sorted either way.

Usage: eng/trx-failures.py <results.trx> [more.trx ...]
"""

import sys
import xml.etree.ElementTree as ET

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


def failing(paths):
    """Every distinct testName whose UnitTestResult reports outcome="Failed", across every TRX."""
    names = set()
    for path in paths:
        # Every UnitTestResult at any depth, not just the top-level ones: a data-driven test can
        # nest its cases in <InnerResults>, and a nested case is still a failure that must appear
        # in the diff.
        for _, element in ET.iterparse(path, events=("end",)):
            if element.tag != NS + "UnitTestResult":
                continue
            if element.get("outcome") == "Failed":
                name = element.get("testName")
                if name:
                    names.add(name)
            element.clear()
    return sorted(names)


def main(argv):
    # LF, on Windows too. Python's text stdout translates the newline to the platform ending, and
    # eng/measure.sh feeds snapshots to `comm`, as eng/ratchet.sh fed this output against its LF
    # baseline until 2026-09-15. A trailing carriage return makes every line differ, so `comm`
    # reports everything fixed and everything broken -- which is what it did before this line existed.
    sys.stdout.reconfigure(newline="\n")

    if len(argv) < 2:
        print("usage: trx-failures.py <results.trx> [more.trx ...]", file=sys.stderr)
        return 2

    try:
        names = failing(argv[1:])
    except OSError as error:
        print(f"trx-failures: cannot read a TRX: {error}", file=sys.stderr)
        return 1
    except ET.ParseError as error:
        print(f"trx-failures: a TRX is not well-formed XML: {error}", file=sys.stderr)
        return 1

    for name in names:
        print(name)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
