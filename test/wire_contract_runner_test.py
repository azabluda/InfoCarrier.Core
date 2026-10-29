"""Focused tests for trusted baseline selection; no builds or second repositories."""

import contextlib
import io
import json
import tempfile
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("wire_contract", Path(__file__).resolve().parents[1] / "eng/wire-contract.py")
GUARD = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GUARD)


class BaselineSelectionTest(unittest.TestCase):
    def test_local_default_uses_checked_out_commit_even_when_main_has_advanced(self):
        with patch.object(GUARD, "commit", return_value="historical-head") as resolve, \
             patch.object(GUARD, "ancestor", return_value=True):
            self.assertEqual("historical-head", GUARD.select_reference(None, "", {}, "historical-head"))
            resolve.assert_called_once_with("HEAD")

    def test_merge_candidate_uses_target_not_previous_feature_commit(self):
        event = {"pull_request": {"base": {"sha": "target"}, "head": {"sha": "feature"}}}
        with patch.object(GUARD, "commit", side_effect=lambda value: value), \
             patch.object(GUARD, "ancestor", return_value=True), \
             patch.object(GUARD, "command", return_value="target feature"):
            self.assertEqual("target", GUARD.select_reference(None, "pull_request", event, "merge"))

    def test_candidate_cannot_substitute_a_local_baseline_in_ci(self):
        with self.assertRaisesRegex(ValueError, "overrides"):
            GUARD.select_reference("HEAD", "pull_request", {}, "merge")

    def test_head_only_checkout_is_not_a_final_merge_candidate(self):
        event = {"pull_request": {"base": {"sha": "target"}, "head": {"sha": "feature"}}}
        with patch.object(GUARD, "commit", side_effect=lambda value: value), \
             patch.object(GUARD, "command", return_value="feature-parent"):
            with self.assertRaisesRegex(ValueError, "merge candidate"):
                GUARD.select_reference(None, "pull_request", event, "feature")

    def test_stale_merge_candidate_cannot_ignore_new_target_allocations(self):
        event = {"pull_request": {"base": {"sha": "new-target"}, "head": {"sha": "feature"}}}
        with patch.object(GUARD, "commit", side_effect=lambda value: value), \
             patch.object(GUARD, "command", return_value="old-target feature"):
            with self.assertRaisesRegex(ValueError, "matching base and head"):
                GUARD.select_reference(None, "pull_request", event, "stale-merge")

    def test_missing_target_is_an_error_not_an_empty_comparison(self):
        with patch.object(GUARD, "commit", side_effect=RuntimeError("missing ref")):
            with self.assertRaisesRegex(RuntimeError, "missing ref"):
                GUARD.select_reference(None, "", {}, "head")

    def test_initial_reference_applies_only_before_policy_ancestry(self):
        def ancestor(older, newer):
            return (older, newer) in {(GUARD.INITIAL, "head")}
        with patch.object(GUARD, "commit", return_value="old-main"), patch.object(GUARD, "ancestor", side_effect=ancestor):
            self.assertEqual(GUARD.INITIAL, GUARD.select_reference(None, "", {}, "head"))

    def test_candidate_must_include_target_changes(self):
        with patch.object(GUARD, "commit", return_value="target"), \
             patch.object(GUARD, "ancestor", side_effect=[True, True, False]):
            with self.assertRaisesRegex(ValueError, "include the target"):
                GUARD.select_reference(None, "", {}, "head")

    def test_push_uses_the_previous_protected_state_for_the_entire_push(self):
        with patch.object(GUARD, "commit", side_effect=lambda value: value), \
             patch.object(GUARD, "ancestor", return_value=True):
            self.assertEqual("before-push", GUARD.select_reference(None, "push", {"before": "before-push"}, "head"))

    def test_description_is_read_from_protected_git_not_candidate_file(self):
        description = self.description()
        with patch.object(GUARD, "command", return_value=json.dumps(description)) as read:
            self.assertEqual(description, GUARD.protected_description("protected-reference"))
            read.assert_called_once_with(["git", "show", "protected-reference:docs/wire-contracts/current.json"])

    def test_missing_initial_description_has_no_candidate_fallback(self):
        with patch.object(GUARD, "command", side_effect=RuntimeError("missing description")):
            with self.assertRaisesRegex(RuntimeError, "missing description"):
                GUARD.protected_description(GUARD.INITIAL)

    def test_unsupported_format_and_empty_inventory_fail(self):
        for description in ({"FormatVersion": 2}, {"FormatVersion": 1, "Contract": {}}):
            with self.assertRaises(ValueError):
                GUARD.read_description(json.dumps(description))

    def test_stale_description_identifies_the_changed_path(self):
        saved, actual = self.description(), self.description()
        actual["Contract"]["Types"]["quantity"] = "string"
        with self.assertRaisesRegex(ValueError, "Contract/Types/quantity"):
            GUARD.verify_saved(saved, actual)

    def test_toolchain_provenance_does_not_require_a_wire_change(self):
        saved, actual = self.description(), self.description()
        actual["Inspection"] = {"Sdk": "a newer SDK"}
        GUARD.verify_saved(saved, actual)

    def test_record_cannot_hide_breakage_even_if_candidate_snapshot_was_replaced(self):
        self.run_record(fail=True)

    def test_record_writes_only_after_protected_comparison_passes(self):
        self.run_record(fail=False)

    def test_ci_cannot_record_a_description(self):
        with patch.object(GUARD.sys, "argv", ["wire-contract.py", "--record"]), \
             patch.dict(GUARD.os.environ, {"GITHUB_EVENT_NAME": "push", "GITHUB_EVENT_PATH": "unused"}), \
             patch.object(Path, "read_text", return_value="{}"):
            with self.assertRaisesRegex(ValueError, "cannot record"):
                GUARD.main()

    @staticmethod
    def description():
        return {"FormatVersion": 1, "Contract": {"Version": 3, "AllocatedVersions": [1, 2, 3],
                "Types": {"quantity": "integer"}, "Frames": {"start": [0]}, "Inventory": {"context": "default"}},
                "Inspection": {"Sdk": "original"}}

    def run_record(self, fail):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            description_path = root / GUARD.DESCRIPTION
            description_path.parent.mkdir(parents=True)
            saved, actual = self.description(), self.description()
            actual["Contract"]["Types"]["quantity"] = "string"
            GUARD.write_json(description_path, actual if fail else saved)
            original_bytes = description_path.read_bytes()
            comparisons = []

            def compare(args, **kwargs):
                comparisons.append(json.loads(Path(args[3]).read_text()))
                self.assertEqual(original_bytes, description_path.read_bytes())
                if fail:
                    raise RuntimeError("incompatible protected contract")

            with patch.object(GUARD, "ROOT", root), \
                 patch.object(GUARD.sys, "argv", ["wire-contract.py", "--record"]), \
                 patch.dict(GUARD.os.environ, {"GITHUB_EVENT_NAME": ""}), \
                 patch.object(GUARD, "commit", return_value="head"), \
                 patch.object(GUARD, "select_reference", return_value="protected"), \
                 patch.object(GUARD, "protected_description", return_value=saved), \
                 patch.object(GUARD, "inspect", return_value=("inspector.dll", actual)), \
                 patch.object(GUARD, "command", side_effect=compare), contextlib.redirect_stdout(io.StringIO()):
                if fail:
                    with self.assertRaisesRegex(RuntimeError, "incompatible"):
                        GUARD.main()
                    self.assertEqual(original_bytes, description_path.read_bytes())
                else:
                    GUARD.main()
                    self.assertEqual(actual, json.loads(description_path.read_text()))
            self.assertEqual([saved["Contract"]], comparisons)

    def test_cleanup_rejects_paths_outside_its_temporary_directory(self):
        with self.assertRaisesRegex(ValueError, "cleanup"):
            GUARD.cleanup(GUARD.ROOT)


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(BaselineSelectionTest)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    failed = len(result.failures) + len(result.errors)
    print(f"Passed: {result.testsRun - failed}, Failed: {failed}, Total: {result.testsRun}")
    raise SystemExit(0 if result.wasSuccessful() else 1)
