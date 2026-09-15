#!/usr/bin/env python3
"""EF parity: the share of specification test cases on which InfoCarrier behaves as EF's own provider.

The README badge showed "passed / total passing" until 2026-09-15. The suite has been green since the
same day, so that badge could only ever say 100% and carried no information. EF Core's own README
shows passed, failed and skipped, which is the same figure; no other EF provider shows any. The
nearest model is a conformance percentage, as a JavaScript engine publishes against test262, and
this is that figure for a provider that means to make no difference to EF (the owner's decision,
2026-09-15, docs/plans/v10/test-overhaul.md).

WHAT IS COUNTED. A test case is one TRX result. The base is every case that ran through InfoCarrier:
a case of a wire-free control class runs without InfoCarrier and is left out, and so is a case xUnit
skipped. A case counts AGAINST parity when a reason from InfoCarrier's side covers it,
[InfoCarrierDesign] or [InfoCarrierDefect]. Everything else counts FOR it: EF's test unchanged, a
store reason, which mirrors what EF's own provider test does, or a skip copied from upstream. So
parity is lowered only by a difference of this provider's, and relabelling a defect as a design
lowers it just the same.

WHERE THE REASONS COME FROM. OverrideAudit writes <assembly>.override-reasons.tsv when
INFOCARRIER_OVERRIDE_REASONS names a directory, one line per reason under every concrete class that
runs it. A reason with a Case covers the cases whose theory arguments name that value.

THE FIGURE IS ROUNDED DOWN, so that one difference in thirty thousand cases never reads as 100%.

Usage: eng/spec-parity.py <reasons.tsv> [...] -- <results.trx> [...]
       Prints key=value lines for counters.env on stdout, and a Markdown table on stderr.
"""

import re
import sys
import xml.etree.ElementTree as ET

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"


def read_reasons(paths):
    controls = set()
    reasons = {}
    for path in paths:
        with open(path, encoding="utf-8") as tsv:
            for line in tsv:
                fields = line.rstrip("\n").split("\t")
                if fields[0] == "control" and len(fields) == 2:
                    controls.add(fields[1])
                elif fields[0] == "reason" and len(fields) == 6:
                    _, cls, method, case, side, label = fields
                    reasons.setdefault((cls, method), []).append((case or None, side, label))
                elif fields != [""]:
                    raise ValueError(f"{path}: not a reasons line: {line!r}")
    return controls, reasons


def read_results(paths):
    """(className, method, testName, outcome) for every result, joined to its definition by id."""
    for path in paths:
        outcomes = {}
        definitions = {}
        for _, element in ET.iterparse(path, events=("end",)):
            if element.tag == NS + "UnitTestResult":
                outcomes[element.get("testId")] = (element.get("testName") or "", element.get("outcome"))
                element.clear()
            elif element.tag == NS + "UnitTest":
                method = element.find(NS + "TestMethod")
                if method is not None:
                    definitions[element.get("id")] = (method.get("className"), method.get("name"))
                element.clear()
        for test_id, (name, outcome) in outcomes.items():
            if test_id not in definitions:
                raise ValueError(f"{path}: result {name!r} has no test definition")
            cls, method = definitions[test_id]
            yield cls, method, name, outcome


def covers(case, test_name):
    if case is None:
        return True
    arguments = test_name[test_name.find("("):] if "(" in test_name else ""
    return re.search(rf"\b{re.escape(case)}\b", arguments) is not None


def main(argv):
    sys.stdout.reconfigure(newline="\n")
    if "--" not in argv:
        print("usage: spec-parity.py <reasons.tsv> [...] -- <results.trx> [...]", file=sys.stderr)
        return 2
    split = argv.index("--")
    tsv_paths, trx_paths = argv[1:split], argv[split + 1:]
    if not tsv_paths or not trx_paths:
        print("usage: spec-parity.py <reasons.tsv> [...] -- <results.trx> [...]", file=sys.stderr)
        return 2

    try:
        controls, reasons = read_reasons(tsv_paths)
        control = skipped = ran = infocarrier = 0
        for cls, method, name, outcome in read_results(trx_paths):
            if cls in controls:
                control += 1
                continue
            if outcome == "NotExecuted":
                skipped += 1
                continue
            ran += 1
            if any(side == "infocarrier" and covers(case, name) for case, side, _ in reasons.get((cls, method), [])):
                infocarrier += 1
    except (OSError, ValueError, ET.ParseError) as error:
        print(f"spec-parity: {error}", file=sys.stderr)
        return 1

    if ran == 0:
        print("spec-parity: no test case ran through InfoCarrier; there is nothing to measure.", file=sys.stderr)
        return 1

    defects = sorted(
        {label.removeprefix("INFOCARRIER DEFECT ") for entries in reasons.values() for _, _, label in entries
         if label.startswith("INFOCARRIER DEFECT ")},
        key=lambda issue: int(issue.lstrip("#")))
    hundredths = (ran - infocarrier) * 10000 // ran
    parity = f"{hundredths // 100}.{hundredths % 100:02d}"

    print(f"parity={parity}")
    print(f"parity_ran={ran}")
    print(f"parity_infocarrier={infocarrier}")
    print(f"parity_skipped={skipped}")
    print(f"parity_control={control}")
    print(f"infocarrier_defects={len(defects)}")

    print("| EF parity | Ran through InfoCarrier | InfoCarrier differs | Skipped | Wire-free control | Open InfoCarrier defects |", file=sys.stderr)
    print("|--:|--:|--:|--:|--:|--|", file=sys.stderr)
    print(f"| {parity}% | {ran} | {infocarrier} | {skipped} | {control} | {', '.join(defects) or 'none'} |", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
