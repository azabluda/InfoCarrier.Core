"""Exercise artifact selection and comment safety without GitHub writes or coverage thresholds."""

import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile
from contextlib import redirect_stdout


spec = importlib.util.spec_from_file_location("review", Path(__file__).with_name("coverage-review.py"))
review = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review)

REPORT = b'''<coverage lines-covered="1" lines-valid="2" branches-covered="0" branches-valid="2">
<packages><package><classes><class filename="src/Example.cs"><lines>
<line number="3" hits="1"/><line number="4" hits="0" branch="true" condition-coverage="0% (0/2)"/>
</lines></class></classes></package></packages></coverage>'''


class ReviewTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        old = Path.cwd()
        os.chdir(self.temp.name)
        self.addCleanup(os.chdir, old)
        self.state = {"repo": "owner/repo", "number": 42, "base": "a" * 40,
                      "head": "b" * 40, "tested": "c" * 40,
                      "diff_base": "d" * 40,
                      "baseline_run": None, "run": "https://github.com/owner/repo/actions/runs/1",
                      "started": "2026-10-26T10:00:00+00:00",
                      "changed_status": "Changed-code coverage uses the pull-request diff."}

    def run_record(self, **overrides):
        return {"id": 7, "head_sha": self.state["base"], "head_branch": "main",
                "event": "push", "conclusion": "success", "html_url": "https://example.test/run/7",
                **overrides}

    def archive(self, report=REPORT, name="merged.cobertura.xml"):
        output = io.BytesIO()
        with zipfile.ZipFile(output, "w") as bundle:
            bundle.writestr(name, report)
        return output.getvalue()

    def test_exact_baseline_mirrors_current_report_pattern(self):
        artifact = {"id": 8, "name": "coverage-report", "expired": False}
        with patch.object(review, "api", side_effect=[
            {"workflow_runs": [self.run_record(head_sha="wrong"), self.run_record()]},
            {"artifacts": [artifact]},
        ]) as api, patch.object(review, "gh", return_value=self.archive()):
            self.assertEqual(review.reference("owner/repo", self.state["base"]), "https://example.test/run/7")
        self.assertIn("head_sha=" + self.state["base"], api.call_args_list[0].args[0])
        self.assertEqual((Path("reference-reports") / review.REPORT).read_bytes(), REPORT)

    def test_missing_or_expired_artifact_never_creates_reference_directory(self):
        for artifacts in ([], [{"id": 8, "name": "coverage-report", "expired": True}],
                          [{"id": 8, "name": "other-report", "expired": False}]):
            with self.subTest(artifacts=artifacts), patch.object(review, "api", side_effect=[
                {"workflow_runs": [self.run_record()]}, {"artifacts": artifacts},
            ]):
                self.assertIsNone(review.reference("owner/repo", self.state["base"]))
                self.assertFalse(Path("reference-reports").exists())

    def test_wrong_revision_failed_or_pull_request_run_is_rejected(self):
        runs = [self.run_record(head_sha="wrong"), self.run_record(conclusion="failure"),
                self.run_record(event="pull_request"), self.run_record(head_branch="feature")]
        with patch.object(review, "api", return_value={"workflow_runs": runs}):
            self.assertIsNone(review.reference("owner/repo", self.state["base"]))
        self.assertFalse(Path("reference-reports").exists())

    def test_malformed_or_missing_xml_never_becomes_baseline(self):
        for archive in (self.archive(b"<coverage/>"), self.archive(name="../../unexpected")):
            with self.subTest(archive=archive), patch.object(review, "api", side_effect=[
                {"workflow_runs": [self.run_record()]},
                {"artifacts": [{"id": 8, "name": "coverage-report", "expired": False}]},
            ]), patch.object(review, "gh", return_value=archive):
                with self.assertRaises((ValueError, KeyError)):
                    review.reference("owner/repo", self.state["base"])
                self.assertFalse(Path("reference-reports").exists())
                self.assertFalse(Path("unexpected").exists())

    def test_missing_baseline_is_explicit_and_never_a_zero_difference(self):
        text = review.context(self.state, None)
        self.assertIn("Baseline unavailable", text)
        self.assertIn("No baseline differences", text)
        self.assertNotIn("difference: 0", text)
        for sha in (self.state["head"], self.state["tested"], self.state["base"], self.state["diff_base"]):
            self.assertIn(sha, text)

    def test_totals_cannot_be_presented_as_existing_line_regressions(self):
        self.state["baseline_run"] = "https://example.test/run/7"
        text = review.context(self.state, "https://example.test/artifact/8")
        for evidence in ("added and removed code", "do not identify lost or gained coverage",
                         "unsupported", "framework dispatch", "does not establish dead code",
                         "https://example.test/artifact/8"):
            self.assertIn(evidence, text)

    def test_changed_code_uses_modified_lines_scope_not_modified_files(self):
        groups = review.configuration()["coverage"]
        tools = [tool for group in groups for tool in group["tools"]]
        self.assertTrue(all(len({t["scope"] for t in group["tools"]}) == 1 for group in groups))
        self.assertTrue(all(t["pattern"] == "**/artifacts/coverage/report/merged.cobertura.xml" for t in tools))
        self.assertEqual([(t["scope"], t["metric"]) for t in tools],
                         [("new", "line"), ("new", "branch"), ("project", "line"), ("project", "branch")])

    def test_nonproduction_and_deleted_code_do_not_enable_fallback_annotations(self):
        files = [{"filename": "src/Old.cs", "additions": 0},
                 {"filename": ".github/workflows/coverage.yml", "additions": 10}]
        with patch.object(review, "api", return_value=files):
            changed, status = review.changed_code("owner/repo", 42, 2)
        self.assertFalse(changed)
        self.assertIn("omitted", status)
        self.assertEqual(len(review.configuration(changed)["coverage"][0]["tools"]), 2)

    def test_new_or_moved_source_uses_added_lines_but_incomplete_patch_is_unknown(self):
        for patch_text, expected in (("@@ -0,0 +1,2 @@\n+one\n+two", True),
                                     ("@@ -0,0 +1,2 @@\n+one", False), ("", False)):
            with self.subTest(patch=patch_text), patch.object(review, "api", return_value=[
                {"filename": "src/Moved.cs", "additions": 2, "patch": patch_text},
            ]):
                self.assertEqual(review.changed_code("owner/repo", 42, 1)[0], expected)

    def test_truncated_file_list_is_not_an_empty_diff(self):
        with patch.object(review, "api", return_value=[]):
            changed, status = review.changed_code("owner/repo", 42, 10)
        self.assertFalse(changed)
        self.assertIn("unavailable", status)

    def prepare_files(self):
        review.REPORT.parent.mkdir(parents=True)
        review.REPORT.write_bytes(REPORT)
        event = {"pull_request": {"number": 42, "base": {"sha": self.state["base"]},
                                  "head": {"sha": self.state["head"]}}}
        Path("event.json").write_text(json.dumps(event))
        return {"GITHUB_EVENT_PATH": "event.json", "GITHUB_REPOSITORY": "owner/repo",
                "GITHUB_SHA": self.state["tested"], "GITHUB_RUN_ID": "1", "GITHUB_OUTPUT": "output.txt"}

    def test_prepare_reports_download_failure_without_enabling_a_zero_baseline(self):
        environment = self.prepare_files()
        current = {"base": {"sha": self.state["base"]}, "head": {"sha": self.state["head"]},
                   "changed_files": 0}
        with patch.dict(os.environ, environment), patch.object(subprocess, "check_output", side_effect=[
            self.state["tested"], self.state["base"] + " " + self.state["head"], self.state["diff_base"],
        ]), patch.object(review, "api", side_effect=[current, []]), patch.object(
            review, "reference", side_effect=subprocess.CalledProcessError(1, "gh"),
        ), redirect_stdout(io.StringIO()):
            review.prepare()
        state = json.loads(review.STATE.read_text())
        self.assertIsNone(state["baseline_run"])
        self.assertEqual(state["diff_base"], self.state["diff_base"])
        self.assertFalse(Path("reference-reports").exists())
        self.assertIn("skip-annotations=true", Path("output.txt").read_text())

    def test_wrong_merge_parents_stop_before_publishing_configuration(self):
        environment = self.prepare_files()
        with patch.dict(os.environ, environment), patch.object(subprocess, "check_output", side_effect=[
            self.state["tested"], "unexpected parents",
        ]), patch.object(review, "api") as api:
            with self.assertRaisesRegex(ValueError, "does not match"):
                review.prepare()
        api.assert_not_called()
        self.assertFalse(Path("output.txt").exists())

    def test_finish_updates_bot_comment_on_later_page_and_replaces_context(self):
        review.STATE.parent.mkdir(parents=True)
        review.STATE.write_text(json.dumps(self.state))
        bot = {"id": 77, "user": {"login": "github-actions[bot]"},
               "updated_at": "2026-10-26T10:01:00Z",
               "body": review.QUALITY_MARKER + "\nMetrics\n" + review.MARKER + "old context"}
        other = {"id": 1, "user": {"login": "someone"}, "body": review.QUALITY_MARKER}
        with patch.object(review, "api", side_effect=[
            {"base": {"sha": self.state["base"]}, "head": {"sha": self.state["head"]}},
            [other] * 100, [bot],
        ]), patch.object(subprocess, "run") as write:
            review.finish()
        self.assertIn("repos/owner/repo/issues/comments/77", write.call_args.args[0])
        body = json.loads(write.call_args.kwargs["input"])["body"]
        self.assertEqual(body.count(review.MARKER), 1)
        self.assertNotIn("old context", body)
        self.assertIn("Metrics", body)

    def test_old_metrics_are_not_relabelled_with_new_revisions(self):
        review.STATE.parent.mkdir(parents=True)
        review.STATE.write_text(json.dumps(self.state))
        with patch.object(review, "api", side_effect=[
            {"base": {"sha": self.state["base"]}, "head": {"sha": self.state["head"]}},
            [{"id": 77, "user": {"login": "github-actions[bot]"},
              "updated_at": "2026-10-25T10:00:00Z", "body": review.QUALITY_MARKER}],
        ]), patch.object(subprocess, "run") as write:
            with self.assertRaisesRegex(ValueError, "old metrics"):
                review.finish()
        write.assert_not_called()

    def test_stale_results_do_not_update_context(self):
        review.STATE.parent.mkdir(parents=True)
        review.STATE.write_text(json.dumps(self.state))
        with patch.object(review, "api", return_value={
            "base": {"sha": self.state["base"]}, "head": {"sha": "newer"},
        }), patch.object(subprocess, "run") as write, redirect_stdout(io.StringIO()):
            review.finish()
        write.assert_not_called()


if __name__ == "__main__":
    unittest.main()
