#!/usr/bin/env python
"""Gate the integrity of ADR-009 Tier D's wire-free control.

WHY THIS EXISTS: A CONFLICT OF INTEREST, NOT A SUSPICION.

Tier D suppresses a red when its control -- the same specification bases run against MongoDB with
InfoCarrier removed -- reproduces the failure without the wire. That is sound evidence, and it is
also evidence this repository generates about its own product. The bias runs one way:

    a worse-wired control fails MORE
      -> more of Tier D's reds are attributed to the store
        -> this provider looks cleaner.

Nobody has to intend it. The failure mode is silent, because a control that is subtly misconfigured
looks exactly like a store that is subtly less capable. The first version of the control had
precisely that bug, passing `null` for the model customization so that its own server context was
built from a different model than Tier D measures.

So the control is GATED rather than trusted, and the gate is pointed at the dangerous direction:

    THE CONTROL MAY NOT FAIL ANYTHING TIER D PASSES.

A control that is doing its job is at least as capable as the wire, because the wire runs on top of
it. Every exception to that rule has to be named in test/tier-d-control-allowances.txt with a
reason, and today there are two -- both places where the projection split lets this provider ANSWER
a query the raw store cannot, which is a capability gain rather than a control defect.

Sloppiness in the control now breaks the build instead of flattering the product.

THE CONTROL DOCUMENTS THE STORE, SO IT MUST BE GREEN.

Until 2026-09-15 the control simply ran and failed, and this script compared raw outcomes. The
control now OVERRIDES each failure to assert what MongoDB actually does, so it is green by design
and a raw comparison says nothing. The protection moved rather than disappeared:

    the control asserts "the store refuses this with exception X".
    If the store stops refusing, THAT ASSERTION FAILS and the control goes red.

So "we suppressed something the store can actually do" is now caught by the control test itself,
which is a stronger place for it than this script: the assertion names the exception, and a raw
outcome comparison never could.

    1. A control test that FAILS must be named in test/tier-d-control-pending.txt. That file is the
       list of control tests nobody has written a real assertion for yet. It must shrink and must
       never grow. Any other control failure means the store's behaviour changed, and every override
       that cites it has to be read again.

    2. Every override in test/tier-d-overrides.txt must have a control test present in the run. An
       override whose control is missing cites evidence that does not exist.

Usage:  eng/tier-d-control.py <results.trx> [overrides-file] [pending-file]

Exit 1 on either violation.
"""

import sys
import os
import re
import xml.etree.ElementTree as ET

NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"

# The control class <-> Tier D class pairing. Both run the same EF base; only the store wiring
# differs. Keyed by the EF family so a rename on one side cannot silently unpair them.
FAMILIES = {
    "Collection": ("DirectCollectionTest", "OwnedNavigationsCollectionInfoCarrierTest"),
    "Miscellaneous": ("DirectMiscellaneousTest", "OwnedNavigationsMiscellaneousInfoCarrierTest"),
    "PrimitiveCollection": ("DirectPrimitiveCollectionTest", "OwnedNavigationsPrimitiveCollectionInfoCarrierTest"),
    "Projection": ("DirectProjectionTest", "OwnedNavigationsProjectionInfoCarrierTest"),
    "SetOperations": ("DirectSetOperationsTest", "OwnedNavigationsSetOperationsInfoCarrierTest"),
    "StructuralEquality": ("DirectStructuralEqualityTest", "OwnedNavigationsStructuralEqualityInfoCarrierTest"),
}


def outcomes(trx_path):
    """{(class, testname): outcome} for every result in the TRX."""
    tree = ET.parse(trx_path)
    by_id = {}
    for d in tree.iter(NS + "UnitTest"):
        tm = d.find(NS + "TestMethod")
        if tm is not None:
            by_id[d.get("id")] = tm.get("className") or ""
    found = {}
    for r in tree.iter(NS + "UnitTestResult"):
        cls = by_id.get(r.get("testId"), "")
        cls = cls.split(",")[0].split(".")[-1]
        found[(cls, r.get("testName") or "")] = r.get("outcome")
    return found


def method_of(test_name):
    """`Ns.Class.Method(args)` -> `Method(args)`; the part that is shared across the pair."""
    name = test_name
    # Strip the namespace+class prefix, keeping any theory arguments.
    head, sep, args = name.partition("(")
    return head.split(".")[-1] + sep + args


def read_allowances(path):
    allowed = {}
    if not os.path.exists(path):
        return allowed
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            key, _, reason = line.partition("#")
            allowed[key.strip()] = reason.strip()
    return allowed


def main(argv):
    if not 2 <= len(argv) <= 4:
        print(__doc__)
        return 2

    trx = argv[1]
    overrides_path = argv[2] if len(argv) > 2 else "test/tier-d-overrides.txt"
    pending_path = argv[3] if len(argv) > 3 else "test/tier-d-control-pending.txt"

    if not os.path.exists(trx):
        print(f"tier-d-control: no TRX at '{trx}'.", file=sys.stderr)
        return 1

    results = outcomes(trx)
    overridden = read_allowances(overrides_path)
    pending = read_allowances(pending_path)

    # Index by (class, method-with-args).
    indexed = {}
    for (cls, test_name), outcome in results.items():
        indexed[(cls, method_of(test_name))] = outcome

    unexpected_control_failures = []
    missing_controls = []
    used_pending = set()
    paired = 0

    for family, (control_cls, wire_cls) in FAMILIES.items():
        methods = {m for (c, m) in indexed if c == control_cls}
        if not methods:
            print(
                f"tier-d-control: no results for control class '{control_cls}'. "
                "The control did not run, so nothing was verified.",
                file=sys.stderr,
            )
            return 1

        for method in sorted(methods):
            key = f"{family}.{method}"
            control = indexed[(control_cls, method)]

            # RULE 1: a control failure is only allowed while nobody has written its assertion.
            if control == "Failed":
                if key in pending:
                    used_pending.add(key)
                else:
                    unexpected_control_failures.append(key)

            if indexed.get((wire_cls, method)) is not None:
                paired += 1

    # RULE 2: an override must cite a control that exists.
    for key in sorted(overridden):
        family, _, method = key.partition(".")
        control_cls = FAMILIES.get(family, (None, None))[0]
        if control_cls is None or (control_cls, method) not in indexed:
            missing_controls.append(key)

    print(f"tier-d-control: {paired} paired tests across {len(FAMILIES)} families.")
    print(f"tier-d-control: {len(overridden)} override(s), each citing a control test.")

    stale_pending = sorted(set(pending) - used_pending)
    if stale_pending:
        print(
            f"tier-d-control: {len(stale_pending)} pending entr(y/ies) now pass. Remove them from "
            f"{pending_path}: {', '.join(stale_pending)}",
            file=sys.stderr,
        )
        return 1

    if used_pending:
        print(f"tier-d-control: {len(used_pending)} control test(s) still await a real assertion.")

    if missing_controls:
        print(file=sys.stderr)
        print(
            "tier-d-control: AN OVERRIDE CITES A CONTROL TEST THAT DID NOT RUN. The evidence for "
            "the suppression does not exist.",
            file=sys.stderr,
        )
        for key in missing_controls:
            print(f"  {key}", file=sys.stderr)
        return 1

    if unexpected_control_failures:
        print(file=sys.stderr)
        print(
            "tier-d-control: A CONTROL TEST FAILED THAT IS NOT PENDING. The control asserts what "
            "the store does, so a failure means the store now does something else. Every override "
            "citing it must be read again.",
            file=sys.stderr,
        )
        for key in unexpected_control_failures:
            print(f"  {key}", file=sys.stderr)
        return 1

    print("tier-d-control: the control documents the store, and every override cites it.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
