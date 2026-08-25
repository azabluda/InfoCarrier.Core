#!/usr/bin/env python3
"""Print the names of the failing tests in a TRX, one per line, sorted.

This is the second of the three levels eng/measure.sh prints locally, brought to CI. The count
alone cannot tell "fixed 4, broke 4" from "changed nothing", and eng/ratchet.sh gated on the count
alone until this existed. Its output is what test/known-failures.names.txt holds and what
eng/ratchet.sh diffs against.

Why Python and not grep. A TRX is XML, and '>' is legal *unescaped* inside an XML attribute value,
so `grep -o '<UnitTestResult[^>]*>'` truncates the element on any test whose name contains one.
Theory arguments are printed into `testName`, so that is not hypothetical.

Why iterparse. The suite is 22k tests and the TRX is tens of megabytes; iterparse clears each
element as it closes instead of holding the whole tree.

Sorting is by Unicode code point, which is what `LC_ALL=C sort` produces for UTF-8 as well, so the
output can be fed to `comm` against a file sorted either way.

Usage: eng/trx-failures.py <results.trx>
"""

import sys
import xml.etree.ElementTree as ET

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


def failing(path):
    """Every distinct testName whose UnitTestResult reports outcome="Failed"."""
    names = set()
    # Every UnitTestResult at any depth, not just the top-level ones: a data-driven test can nest
    # its cases in <InnerResults>, and a nested case is still a failure that must appear in the diff.
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
    # eng/ratchet.sh feeds this output to `comm` against a baseline that .gitattributes keeps at LF
    # on every platform. A trailing carriage return makes every line differ, so `comm` reports the
    # whole baseline fixed and the whole run broken -- which is what it did before this line existed.
    sys.stdout.reconfigure(newline="\n")

    if len(argv) != 2:
        print("usage: trx-failures.py <results.trx>", file=sys.stderr)
        return 2

    try:
        names = failing(argv[1])
    except OSError as error:
        print(f"trx-failures: cannot read '{argv[1]}': {error}", file=sys.stderr)
        return 1
    except ET.ParseError as error:
        print(f"trx-failures: '{argv[1]}' is not well-formed XML: {error}", file=sys.stderr)
        return 1

    for name in names:
        print(name)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
