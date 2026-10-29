#!/usr/bin/env python3
"""Check actual wire metadata against committed descriptions from protected Git revisions.

Only the candidate is built. --record updates its description after compatibility checks pass;
ordinary checks also verify that the saved candidate description matches its implementation.
CI selects the target from its event. Optional release references impose compatibility even
when a fresh wire version acknowledges a break against the development target.
"""

import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
INITIAL = "e877418b42b8991f0d3dc9be9cdfdea794fe3d81"
TOOL = Path("tools/InfoCarrier.WireContract")
LEDGER = "docs/wire-versions.json"
DESCRIPTION = "docs/wire-contracts/current.json"


def command(args, cwd=ROOT, capture=True):
    result = subprocess.run([str(arg) for arg in args], cwd=cwd, check=False,
                            text=True, encoding="utf-8", errors="replace",
                            stdout=subprocess.PIPE if capture else None,
                            stderr=subprocess.STDOUT if capture else None)
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {args}\n{result.stdout or ''}")
    return (result.stdout or "").strip()


def commit(reference):
    value = command(["git", "rev-parse", "--verify", "--end-of-options", f"{reference}^{{commit}}"])
    if len(value) != 40 or any(c not in "0123456789abcdef" for c in value):
        raise ValueError("Reference did not resolve to an exact Git commit.")
    return value


def ancestor(older, newer):
    result = subprocess.run(["git", "merge-base", "--is-ancestor", older, newer],
                            cwd=ROOT, capture_output=True)
    if result.returncode not in (0, 1):
        raise RuntimeError("Cannot establish baseline ancestry.")
    return result.returncode == 0


def select_reference(base_ref, event_name, event, head):
    if event_name:
        if base_ref:
            raise ValueError("CI baseline overrides are forbidden.")
        if event_name == "pull_request":
            request = event["pull_request"]
            target = commit(request["base"]["sha"])
            proposed = commit(request["head"]["sha"])
            parents = command(["git", "show", "-s", "--format=%P", head]).split()
            if parents != [target, proposed]:
                raise ValueError("Run the check on the current pull-request merge candidate, with matching base and head.")
        elif event_name == "merge_group":
            target = commit(event["merge_group"]["base_sha"])
            if commit(event["merge_group"]["head_sha"]) != head or not ancestor(target, head):
                raise ValueError("Merge-group identity or ancestry does not match the checked candidate.")
        elif event_name == "push":
            before = event.get("before", "")
            target = commit(before) if before and set(before) != {"0"} else commit("origin/main")
            if not ancestor(target, head):
                raise ValueError("Push rewrites protected history; baseline selection needs review.")
        else:
            raise ValueError(f"Unsupported CI event: {event_name}")
    else:
        target = commit(base_ref or "HEAD")
    if not ancestor(INITIAL, head):
        raise ValueError("The candidate lacks the initial description revision. Do not compare the earlier versioning implementation.")
    reference = target if ancestor(INITIAL, target) else INITIAL
    if not ancestor(reference, head):
        raise ValueError("Update the candidate to include the target branch before checking its contract.")
    return reference


def read_description(text):
    description = json.loads(text)
    if description.get("FormatVersion") != 1:
        raise ValueError("Unsupported description format. Extend coverage explicitly; do not regenerate the reference.")
    contract = description.get("Contract", {})
    if not all(contract.get(name) for name in ("Types", "Frames", "Inventory", "AllocatedVersions")):
        raise ValueError("Missing or empty contract inventory in committed description.")
    return description


def protected_description(reference):
    # Never use the working tree's copy as its own compatibility baseline.
    return read_description(command(["git", "show", f"{reference}:{DESCRIPTION}"]))


def difference(saved, actual, path="Contract"):
    if type(saved) is not type(actual):
        return path, saved, actual
    if isinstance(saved, dict):
        for key in sorted(saved.keys() | actual.keys()):
            if key not in saved or key not in actual:
                return path + "/" + key, saved.get(key, "<absent>"), actual.get(key, "<absent>")
            found = difference(saved[key], actual[key], path + "/" + key)
            if found:
                return found
    elif saved != actual:
        return path, saved, actual
    return None


def verify_saved(saved, actual):
    found = difference(saved["Contract"], actual["Contract"])
    if found:
        path, previous, current = found
        raise ValueError(f"Saved candidate description is stale at {path}: saved={previous!r}; actual={current!r}. "
                         "Run --record after resolving compatibility failures, then review the description diff.")


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n")


def inspect(source, ledger, output):
    sdk = command(["dotnet", "--version"], source)
    print(f"Inspecting {source}; SDK={sdk}", flush=True)
    project = source / TOOL / "InfoCarrier.WireContract.csproj"
    build = ["dotnet", "build", project, "--configuration", "Release"]
    command(build, source, capture=False)
    binaries = list((source / TOOL / "bin/Release").glob("*/InfoCarrier.WireContract.dll"))
    if len(binaries) != 1:
        raise ValueError("Expected exactly one built inspector.")
    executable = binaries[0]
    dependencies = json.loads(executable.with_suffix(".deps.json").read_text(encoding="utf-8"))
    print("Resolved inputs: " + ", ".join(sorted(dependencies["libraries"])), flush=True)
    command(["dotnet", executable, "inspect", output, ledger], source, capture=False)
    contract = json.loads(output.read_text(encoding="utf-8"))
    if not contract.get("Types") or not contract.get("Frames") or not contract.get("Inventory"):
        raise ValueError("Inspector returned an incomplete or empty contract inventory.")
    runtime = contract["Inventory"].pop("runtime:System.Text.Json")
    description = {"FormatVersion": 1, "Contract": contract,
                   "Inspection": {"Sdk": sdk, "SystemTextJson": runtime,
                                  "Dependencies": sorted(dependencies["libraries"])}}
    write_json(output, contract)
    return executable, description


def cleanup(path):
    resolved = path.resolve()
    if resolved.parent != (ROOT / "artifacts").resolve() or not resolved.name.startswith("wire-contract-"):
        raise ValueError("Refusing recursive cleanup outside the temporary comparison directory.")
    native = str(resolved)
    if os.name == "nt":
        native = "\\\\?\\UNC\\" + native[2:] if native.startswith("\\\\") else "\\\\?\\" + native
    shutil.rmtree(native)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-ref", help="Local target branch or exact revision; unavailable in CI.")
    parser.add_argument("--record", action="store_true", help="Record the candidate only after protected comparisons pass.")
    parser.add_argument("--release-ref", action="append", default=[], help="Also require compatibility with a stored release description; repeat for each release revision.")
    args = parser.parse_args()
    event_name = os.environ.get("GITHUB_EVENT_NAME", "")
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8")) if event_name else {}
    if event_name and args.record:
        raise ValueError("CI cannot record descriptions. Commit a reviewed local update.")
    reference = select_reference(args.base_ref, event_name, event, commit("HEAD"))
    print(f"Protected structural reference: {reference}", flush=True)
    (ROOT / "artifacts").mkdir(exist_ok=True)
    temporary = Path(tempfile.mkdtemp(prefix="wire-contract-", dir=ROOT / "artifacts")).resolve()
    try:
        baseline = temporary / "baseline.json"
        candidate = temporary / "candidate.json"
        references = [(reference, "compare")]
        references.extend((commit(ref), "compare-compatible") for ref in args.release_ref)
        # Resolve all protected inputs before any candidate build or description update.
        descriptions = [(ref, mode, protected_description(ref)) for ref, mode in references]
        executable, actual = inspect(ROOT, ROOT / LEDGER, candidate)
        for ref, mode, description in descriptions:
            print(f"Reference inspection inputs ({ref}): {json.dumps(description.get('Inspection', {}), sort_keys=True)}", flush=True)
            write_json(baseline, description["Contract"])
            command(["dotnet", executable, mode, baseline, candidate, ref], capture=False)
        if args.record:
            write_json(ROOT / DESCRIPTION, actual)
            print(f"Recorded {DESCRIPTION}. Review and commit its diff; protected comparisons passed.")
        else:
            verify_saved(read_description((ROOT / DESCRIPTION).read_text(encoding="utf-8")), actual)
            print("Saved candidate description matches the inspected implementation.")
    finally:
        cleanup(temporary)


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        print(f"Wire contract check failed: {error}", file=sys.stderr)
        sys.exit(1)
