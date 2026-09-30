import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
HOOKS = REPO_ROOT / ".codex" / "hooks"


def run_hook(name: str, payload: dict) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(HOOKS / name)],
        input=json.dumps(payload),
        text=True,
        capture_output=True,
        cwd=REPO_ROOT,
        check=False,
    )


class CSharpSearchReminderTests(unittest.TestCase):
    def test_search_command_targeting_csharp_adds_context(self) -> None:
        result = run_hook(
            "cs-search-reminder.py",
            {
                "cwd": str(REPO_ROOT),
                "tool_name": "Bash",
                "tool_input": {"command": "rg Collate src/Model.cs"},
            },
        )

        self.assertEqual(0, result.returncode)
        output = json.loads(result.stdout)
        self.assertEqual("PreToolUse", output["hookSpecificOutput"]["hookEventName"])
        self.assertIn("roslyn-codelens", output["hookSpecificOutput"]["additionalContext"])

    def test_non_search_command_produces_no_context(self) -> None:
        result = run_hook(
            "cs-search-reminder.py",
            {
                "cwd": str(REPO_ROOT),
                "tool_name": "Bash",
                "tool_input": {"command": "Get-Content src/Model.cs"},
            },
        )

        self.assertEqual(0, result.returncode)
        self.assertEqual("", result.stdout)

    def test_command_outside_repository_produces_no_context(self) -> None:
        result = run_hook(
            "cs-search-reminder.py",
            {
                "cwd": str(REPO_ROOT.parent),
                "tool_name": "Bash",
                "tool_input": {"command": "rg Collate src/Model.cs"},
            },
        )

        self.assertEqual(0, result.returncode)
        self.assertEqual("", result.stdout)


class FileHygieneTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(dir=REPO_ROOT)
        self.temp_path = Path(self.temp.name)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def run_hygiene(self, patch: str, cwd: Path = REPO_ROOT) -> subprocess.CompletedProcess[str]:
        return run_hook(
            "file-hygiene.py",
            {
                "cwd": str(cwd),
                "hook_event_name": "PostToolUse",
                "tool_name": "apply_patch",
                "tool_input": {"command": patch},
                "tool_response": {},
            },
        )

    def relative(self, path: Path) -> str:
        return path.relative_to(REPO_ROOT).as_posix()

    def test_clean_changed_file_passes(self) -> None:
        path = self.temp_path / "clean.json"
        path.write_bytes(b'{"ok": true}\n')

        result = self.run_hygiene(f"*** Update File: {self.relative(path)}\n")

        self.assertEqual(0, result.returncode)

    def test_pre_tool_use_denies_subrepo_edit(self) -> None:
        result = run_hook(
            "file-hygiene.py",
            {
                "cwd": str(REPO_ROOT),
                "hook_event_name": "PreToolUse",
                "tool_name": "apply_patch",
                "tool_input": {"command": "*** Update File: subrepos/efcore/example.txt\n"},
            },
        )

        self.assertEqual(0, result.returncode)
        output = json.loads(result.stdout)
        specific = output["hookSpecificOutput"]
        self.assertEqual("deny", specific["permissionDecision"])
        self.assertIn("subrepos", specific["permissionDecisionReason"])
        self.assertEqual("", result.stderr)

    def test_all_changed_files_are_checked(self) -> None:
        clean = self.temp_path / "clean.json"
        bad = self.temp_path / "bad.py"
        clean.write_bytes(b"{}\n")
        bad.write_bytes(b"print('bad')\r\n")
        patch = (
            f"*** Update File: {self.relative(clean)}\n"
            f"*** Update File: {self.relative(bad)}\n"
        )

        result = self.run_hygiene(patch)

        self.assertEqual(2, result.returncode)
        self.assertIn(str(bad), result.stderr)
        self.assertIn("CRLF", result.stderr)

    def test_windows_patch_path_is_resolved(self) -> None:
        bad = self.temp_path / "bad.yaml"
        bad.write_bytes(b"\xef\xbb\xbfkey: value\n")
        windows_path = self.relative(bad).replace("/", "\\")

        result = self.run_hygiene(f"*** Update File: {windows_path}\n")

        self.assertEqual(2, result.returncode)
        self.assertIn("UTF-8 BOM", result.stderr)

    def test_deleted_file_is_ignored(self) -> None:
        missing = self.temp_path / "missing.json"

        result = self.run_hygiene(f"*** Delete File: {self.relative(missing)}\n")

        self.assertEqual(0, result.returncode)

    def test_xml_comment_double_hyphen_fails(self) -> None:
        bad = self.temp_path / "bad.props"
        bad.write_bytes(b"<!-- bad -- comment -->\n")

        result = self.run_hygiene(f"*** Update File: {self.relative(bad)}\n")

        self.assertEqual(2, result.returncode)
        self.assertIn("XML comment", result.stderr)

    def test_patch_outside_repository_is_ignored(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            outside = Path(directory) / "bad.py"
            outside.write_bytes(b"print('bad')\r\n")

            result = self.run_hygiene(f"*** Update File: {outside}\n")

        self.assertEqual(0, result.returncode)

    def test_registered_windows_launcher_preserves_feedback_exit_code(self) -> None:
        bad = self.temp_path / "bad.py"
        bad.write_bytes(b"print('bad')\r\n")
        payload = {
            "cwd": str(REPO_ROOT),
            "hook_event_name": "PostToolUse",
            "tool_name": "apply_patch",
            "tool_input": {"command": f"*** Update File: {self.relative(bad)}\n"},
            "tool_response": {},
        }
        config = json.loads((REPO_ROOT / ".codex" / "hooks.json").read_text("utf-8"))
        command = config["hooks"]["PostToolUse"][0]["hooks"][0]["commandWindows"]

        result = subprocess.run(
            command,
            input=json.dumps(payload),
            text=True,
            capture_output=True,
            cwd=REPO_ROOT,
            shell=True,
            check=False,
        )

        self.assertEqual(2, result.returncode)
        self.assertIn("CRLF", result.stderr)


if __name__ == "__main__":
    unittest.main()
