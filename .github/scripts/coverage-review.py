#!/usr/bin/env python
"""Adapt this repository's artifacts to Quality Monitor; do not compute coverage metrics.

The action's built-in download targets another workflow. Only an exact, successful main
push is an acceptable reference here. Missing reports leave reference-reports absent,
which disables deltas rather than manufacturing a zero baseline.
"""

import io
import json
import os
from datetime import datetime, timezone
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile


REPORT = Path("artifacts/coverage/report/merged.cobertura.xml")
STATE = Path("artifacts/coverage/review.json")
MARKER = "<!-- coverage-review-context -->"
QUALITY_MARKER = "<!-- -[quality-monitor-comment]- -->"


def gh(path):
    return subprocess.check_output(["gh", "api", path])


def api(path):
    return json.loads(gh(path))


def validate_report(data):
    root = ET.fromstring(data)
    if root.tag != "coverage" or not root.findall(".//class/lines/line"):
        raise ValueError("Cobertura report has no instrumented lines")
    for key in ("lines-covered", "lines-valid", "branches-covered", "branches-valid"):
        if int(root.attrib[key]) < 0:
            raise ValueError(f"Invalid Cobertura counter: {key}")


def configuration(changed=True):
    # 'new' means modified lines. 'changed' means modified FILES in this action.
    return {"coverage": [{"name": name, "tools": [
        {"id": "cobertura", "metric": metric, "scope": scope, "sourcePath": ".",
         "pattern": "**/" + REPORT.as_posix()}
        for metric in ("line", "branch")
    ]} for scope, name in (([("new", "Changed production code")] if changed else [])
                          + [("project", "Whole project context")])]}


def changed_code(repo, number, expected_files):
    files = []
    for page in range(1, 31):
        batch = api(f"repos/{repo}/pulls/{number}/files?per_page=100&page={page}")
        files.extend(batch)
        if len(batch) < 100:
            break
    if len(files) != expected_files:
        return False, "Changed-code comparison unavailable: incomplete pull-request file list."
    candidates = [file for file in files if file["filename"].startswith("src/")
                  and file["filename"].endswith(".cs") and file["additions"] > 0]
    if not candidates:
        return False, "No added or replaced production C# lines in this diff; changed-code metrics are omitted."
    for file in candidates:
        # A missing or truncated API patch must not become an empty, apparently perfect diff.
        additions = sum(line.startswith("+") for line in file.get("patch", "").splitlines())
        if additions != file["additions"]:
            return False, "Changed-code comparison unavailable: missing or incomplete production source patch."
    return True, "Changed-code coverage uses the pull-request diff; deleted lines are excluded, and renames use the new path."


def reference(repo, base):
    # Never fall back to the latest successful run: it might describe different source.
    runs = api(f"repos/{repo}/actions/workflows/coverage.yml/runs"
               f"?branch=main&event=push&status=success&head_sha={base}&per_page=100")
    for run in runs["workflow_runs"]:
        if (run["head_sha"] != base or run["event"] != "push"
                or run["head_branch"] != "main" or run["conclusion"] != "success"):
            continue
        artifacts = api(f"repos/{repo}/actions/runs/{run['id']}/artifacts?per_page=100")
        for artifact in artifacts["artifacts"]:
            if artifact["name"] != "coverage-report" or artifact["expired"]:
                continue
            archive = gh(f"repos/{repo}/actions/artifacts/{artifact['id']}/zip")
            with zipfile.ZipFile(io.BytesIO(archive)) as bundle:
                # Upload-artifact strips the report directory. Never extract arbitrary paths.
                data = bundle.read("merged.cobertura.xml")
            validate_report(data)
            destination = Path("reference-reports") / REPORT
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
            return run["html_url"]
    return None


def prepare():
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8"))
    pr = event["pull_request"]
    repo = os.environ["GITHUB_REPOSITORY"]
    tested = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    parents = subprocess.check_output(["git", "show", "-s", "--format=%P", tested], text=True).split()
    base, head = pr["base"]["sha"], pr["head"]["sha"]
    if tested != os.environ["GITHUB_SHA"] or parents != [base, head]:
        raise ValueError("Checkout does not match the event's tested merge, base, and head")
    diff_base = subprocess.check_output(["git", "merge-base", base, head], text=True).strip()
    current = api(f"repos/{repo}/pulls/{pr['number']}")
    if current["head"]["sha"] != head or current["base"]["sha"] != base:
        raise ValueError("Pull-request revisions changed; refusing a stale coverage comment")
    validate_report(REPORT.read_bytes())
    if Path("reference-reports").exists():
        raise ValueError("Unexpected reference-reports directory; refusing an unverified baseline")
    state = {"repo": repo, "number": pr["number"], "base": base, "head": head,
             "tested": tested, "diff_base": diff_base, "baseline_run": None,
             "started": datetime.now(timezone.utc).replace(microsecond=0).isoformat(),
             "run": f"https://github.com/{repo}/actions/runs/{os.environ['GITHUB_RUN_ID']}"}
    changed, state["changed_status"] = changed_code(repo, pr["number"], current["changed_files"])
    try:
        state["baseline_run"] = reference(repo, base)
    except (subprocess.CalledProcessError, KeyError, ValueError, ET.ParseError,
            zipfile.BadZipFile) as error:
        # A download/parser failure is missing evidence, never a numerical comparison.
        print(f"::warning::Baseline unavailable: {type(error).__name__}")
    STATE.write_text(json.dumps(state), encoding="utf-8", newline="\n")
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8", newline="\n") as output:
        output.write("config=" + json.dumps(configuration(changed), separators=(",", ":")) + "\n")
        output.write("skip-annotations=" + ("" if changed else "true") + "\n")


def context(state, artifact_url):
    repo = state["repo"]

    def commit(sha):
        return f"[`{sha[:12]}`](https://github.com/{repo}/commit/{sha})"

    baseline = (f"Project-total differences compare main {commit(state['base'])} "
                f"([reference run]({state['baseline_run']})) with the tested merge."
                if state["baseline_run"] else
                f"Baseline unavailable for main {commit(state['base'])}. No baseline differences are reported.")
    return f"""{MARKER}

### Review evidence

Tested merge: {commit(state['tested'])}. Pull-request head: {commit(state['head'])}.
Diff base: {commit(state['diff_base'])} (merge base to head).
{baseline}

{state['changed_status']}
When changed-code evidence is available, open the GitHub Checks Result above for uncovered lines/branches (first 30 annotations).
Moved code can appear as added code. An empty instrumented diff can appear as 100%, without evidence of tested behavior.

Project-total differences include added and removed code. They do not identify lost or gained coverage on surviving code.
Line-mapped comparisons of existing code are unsupported. Branch totals can also change with instrumentation.

For each location, identify missing scenarios and add a focused test for supported behavior.
Before removal or design changes, inspect references, framework dispatch, registrations, generated calls, and ownership.
Zero coverage or no direct callers does not establish dead code. Investigate incomplete evidence.
See the [local review procedure](https://github.com/{repo}/blob/{state['head']}/docs/coverage-review.md).

[Detailed ReportGenerator report]({artifact_url or state['run']}) (download `coverage-report`, open `index.html`).
Coverage is informational; no percentage threshold or coverage merge gate is configured.
"""


def finish():
    state = json.loads(STATE.read_text(encoding="utf-8"))
    repo, number = state["repo"], state["number"]
    current = api(f"repos/{repo}/pulls/{number}")
    if current["head"]["sha"] != state["head"] or current["base"]["sha"] != state["base"]:
        print("::notice::Pull-request revisions changed; skipping stale coverage context")
        return
    # Paginate: a long review must still update its existing canonical comment.
    page = 1
    while True:
        comments = api(f"repos/{repo}/issues/{number}/comments?per_page=100&page={page}")
        for comment in comments:
            if comment["user"]["login"] != "github-actions[bot]" or QUALITY_MARKER not in comment["body"]:
                continue
            if datetime.fromisoformat(comment["updated_at"].replace("Z", "+00:00")) < datetime.fromisoformat(state["started"]):
                raise ValueError("Quality Monitor did not refresh its comment; refusing to relabel old metrics")
            body = comment["body"].split(MARKER)[0].rstrip()
            body += "\n\n" + context(state, os.environ.get("COVERAGE_ARTIFACT_URL"))
            subprocess.run(["gh", "api", "--method", "PATCH",
                            f"repos/{repo}/issues/comments/{comment['id']}", "--input", "-"],
                           input=json.dumps({"body": body}).encode(), check=True, stdout=subprocess.DEVNULL)
            return
        if len(comments) < 100:
            raise ValueError("Quality Monitor comment was not found")
        page += 1


if __name__ == "__main__":
    {"prepare": prepare, "finish": finish}[sys.argv[1]]()
