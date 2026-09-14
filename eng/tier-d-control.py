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

IT CHECKS BOTH DIRECTIONS, AND THE SECOND ONE MATTERS MORE.

    1. The control fails a test Tier D passes. Either the control is misconfigured, or this
       provider genuinely answers what the raw store cannot -- a capability gain, named in
       test/tier-d-control-allowances.txt with its reason.

    2. The control PASSES a test Tier D overrides. That is a suppression of something the store can
       actually do, which is the worst failure this tier can have: a real defect wearing a citation.
       test/tier-d-overrides.txt is the manifest of what is suppressed and why.

The override manifest is deliberately NOT part of the allowance list. Allowances are claims that
this provider beats the store; overrides are claims that the store cannot do something. Keeping them
in one file would let the gate weaken itself every time something new was suppressed.

Usage:  eng/tier-d-control.py <results.trx> [allowances-file] [overrides-file]

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
    allowances_path = argv[2] if len(argv) > 2 else "test/tier-d-control-allowances.txt"
    overrides_path = argv[3] if len(argv) > 3 else "test/tier-d-overrides.txt"

    if not os.path.exists(trx):
        print(f"tier-d-control: no TRX at '{trx}'.", file=sys.stderr)
        return 1

    results = outcomes(trx)
    allowed = read_allowances(allowances_path)
    overridden = read_allowances(overrides_path)

    # Index by (class, method-with-args).
    indexed = {}
    for (cls, test_name), outcome in results.items():
        indexed[(cls, method_of(test_name))] = outcome

    violations = []
    suppressed_but_supported = []
    used_allowances = set()
    used_overrides = set()
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
            wire = indexed.get((wire_cls, method))
            if wire is None:
                continue
            paired += 1
            control = indexed[(control_cls, method)]
            key = f"{family}.{method}"

            if key in overridden:
                used_overrides.add(key)
                # DIRECTION 2: we assert the store cannot do this, and the control just did it.
                if control == "Passed":
                    suppressed_but_supported.append(key)
                continue

            # DIRECTION 1: the control is weaker than the wire it is supposed to underpin.
            if control == "Failed" and wire == "Passed":
                if key in allowed:
                    used_allowances.add(key)
                else:
                    violations.append(key)

    print(f"tier-d-control: {paired} paired tests across {len(FAMILIES)} families.")

    stale = sorted(set(allowed) - used_allowances)
    if stale:
        # A stale allowance is a claim that stopped being true, and leaving it costs the next
        # reader the same investigation. It is reported, not fatal: the direction that matters is
        # a control failing MORE than it should.
        print(f"tier-d-control: {len(stale)} allowance(s) no longer needed: {', '.join(stale)}")

    if used_allowances:
        print(f"tier-d-control: {len(used_allowances)} allowed capability gain(s): "
              f"{', '.join(sorted(used_allowances))}")

    stale_overrides = sorted(set(overridden) - used_overrides)
    if stale_overrides:
        print(f"tier-d-control: {len(stale_overrides)} override(s) in the manifest ran no test: "
              f"{', '.join(stale_overrides)}", file=sys.stderr)
        return 1

    if used_overrides:
        print(f"tier-d-control: {len(used_overrides)} override(s) checked against the control.")

    if suppressed_but_supported:
        print(file=sys.stderr)
        print(
            "tier-d-control: A TEST IS OVERRIDDEN AS UNSUPPORTED AND THE STORE ANSWERS IT. That is "
            "a real failure hidden behind a citation, and it is the worst outcome this tier has.",
            file=sys.stderr,
        )
        for key in suppressed_but_supported:
            print(f"  {key}", file=sys.stderr)
        print(file=sys.stderr)
        print("Delete the override and let the test run, or explain why the wire cannot do what "
              "the store demonstrably can.", file=sys.stderr)
        return 1

    if violations:
        print(file=sys.stderr)
        print(
            "tier-d-control: THE CONTROL FAILED TESTS THAT TIER D PASSES, which is the direction "
            "that biases every suppression built on it.",
            file=sys.stderr,
        )
        for key in violations:
            print(f"  {key}", file=sys.stderr)
        print(file=sys.stderr)
        print(
            "Either the control is misconfigured -- check that it is given the SAME model, store "
            "and options Tier D uses -- or this provider genuinely answers a query the raw store "
            "cannot, in which case add the test to "
            f"{allowances_path} with the reason.",
            file=sys.stderr,
        )
        return 1

    print("tier-d-control: the control fails nothing Tier D passes.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
