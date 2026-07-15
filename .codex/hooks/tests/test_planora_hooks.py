from __future__ import annotations

import importlib.util
import io
import json
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock


MODULE_PATH = Path(__file__).resolve().parents[1] / "planora_hooks.py"
SPEC = importlib.util.spec_from_file_location("planora_hooks", MODULE_PATH)
assert SPEC and SPEC.loader
hooks = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = hooks
SPEC.loader.exec_module(hooks)


class ClassificationTests(unittest.TestCase):
    def test_documentation_only_stays_in_tier_one(self) -> None:
        checks = hooks.selected_checks("completion", ["README.md"])
        self.assertEqual([check.id for check in checks], ["fast-audit"])

    def test_devex_only_avoids_dotnet_tier_three(self) -> None:
        checks = hooks.selected_checks(
            "completion", [".codex/hooks.json", ".codex/hooks/planora_hooks.py"]
        )
        ids = {check.id for check in checks}
        self.assertIn("hook-tests", ids)
        self.assertNotIn("release-build", ids)
        self.assertNotIn("full-tests", ids)

    def test_backend_and_frontend_are_targeted_at_commit(self) -> None:
        backend = {
            check.id
            for check in hooks.selected_checks(
                "commit", ["Planora.Api/Application/Services/AccountService.cs"]
            )
        }
        frontend = {
            check.id
            for check in hooks.selected_checks("commit", ["Planora.Web/Pages/Board.razor"])
        }
        self.assertIn("api-build", backend)
        self.assertNotIn("web-build", backend)
        self.assertIn("web-build", frontend)
        self.assertNotIn("api-build", frontend)

    def test_contract_build_replaces_duplicate_project_builds(self) -> None:
        ids = [
            check.id
            for check in hooks.selected_checks(
                "commit", ["Planora.Api/Controllers/BoardsController.cs"]
            )
        ]
        self.assertIn("contract-build", ids)
        self.assertNotIn("api-build", ids)

    def test_application_completion_selects_tier_three(self) -> None:
        checks = hooks.selected_checks(
            "completion", ["Planora.Api/Controllers/BoardsController.cs"]
        )
        ids = {check.id for check in checks}
        self.assertTrue({"restore", "release-build", "full-tests", "release-format"} <= ids)

    def test_gate_plan_reports_runtime_requirements(self) -> None:
        docs = hooks.gate_plan("pr", ["README.md"])
        backend = hooks.gate_plan(
            "pr", ["Planora.Api/Application/Services/AccountService.cs"]
        )
        self.assertEqual(docs["maxTier"], 1)
        self.assertFalse(docs["requiresPostgres"])
        self.assertEqual(backend["maxTier"], 3)
        self.assertTrue(backend["requiresPostgres"])

    def test_ci_only_completion_does_not_format_untouched_product_source(self) -> None:
        ids = {
            check.id
            for check in hooks.selected_checks("completion", [".github/workflows/ci.yml"])
        }
        self.assertIn("full-tests", ids)
        self.assertNotIn("release-format", ids)

    def test_specialist_categories_are_detected(self) -> None:
        result = hooks.classify(
            [
                "Planora.Api/Controllers/AuthController.cs",
                "Planora.Api/Migrations/20260715000000_AddThing.cs",
                "Planora.Api/Planora.Api.csproj",
                "Planora.Web/Pages/Board.razor",
            ]
        )
        self.assertTrue(
            {"authentication", "security", "migration", "database", "dependencies", "ui"}
            <= set(result)
        )

    def test_check_ids_are_unique(self) -> None:
        ids = [check.id for check in hooks.CHECKS]
        self.assertEqual(len(ids), len(set(ids)))


class CacheTests(unittest.TestCase):
    def test_runtime_uploads_do_not_invalidate_project_checks(self) -> None:
        original_root = hooks.ROOT
        try:
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                source = root / "Planora.Api" / "Controllers" / "ProbeController.cs"
                upload = root / "Planora.Api" / "wwwroot" / "uploads" / "boards" / "runtime.png"
                source.parent.mkdir(parents=True)
                upload.parent.mkdir(parents=True)
                source.write_text("sealed class ProbeController {}", encoding="utf-8")
                upload.write_bytes(b"runtime")
                hooks.ROOT = root
                check = next(item for item in hooks.CHECKS if item.id == "release-build")
                relevant = hooks.relevant_paths(check, [], {})
                self.assertIn("Planora.Api/Controllers/ProbeController.cs", relevant)
                self.assertNotIn("Planora.Api/wwwroot/uploads/boards/runtime.png", relevant)
        finally:
            hooks.ROOT = original_root

    def test_commands_resolve_for_the_current_platform(self) -> None:
        python_check = hooks.Check(
            "python-probe",
            2,
            10,
            ("python", "probe.py"),
            frozenset(),
            frozenset(),
            "probe",
        )
        powershell_check = hooks.Check(
            "powershell-probe",
            2,
            10,
            ("powershell", "-File", "probe.ps1"),
            frozenset(),
            frozenset(),
            "probe",
        )
        self.assertEqual(hooks.resolved_command(python_check)[0], sys.executable)
        with mock.patch.object(hooks.shutil, "which", side_effect=["/usr/bin/pwsh", None]):
            self.assertEqual(hooks.resolved_command(powershell_check)[0], "/usr/bin/pwsh")

    def test_format_command_includes_only_changed_source(self) -> None:
        check = next(item for item in hooks.CHECKS if item.id == "release-format")
        command = hooks.resolved_command(
            check,
            [
                "Planora.Shared/Constants/BoardLimits.cs",
                "README.md",
                "Planora.Api/Controllers/does-not-exist.cs",
            ],
        )
        self.assertIn("--include", command)
        self.assertIn("Planora.Shared/Constants/BoardLimits.cs", command)
        self.assertNotIn("README.md", command)
        self.assertNotIn("Planora.Api/Controllers/does-not-exist.cs", command)

    def test_content_change_invalidates_fingerprint(self) -> None:
        original_root = hooks.ROOT
        try:
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                subprocess.run(["git", "init", "-q"], cwd=root, check=True)
                subprocess.run(["git", "config", "user.email", "hooks@example.test"], cwd=root, check=True)
                subprocess.run(["git", "config", "user.name", "Hook Tests"], cwd=root, check=True)
                file = root / "sample.cs"
                file.write_text("first", encoding="utf-8")
                subprocess.run(["git", "add", "sample.cs"], cwd=root, check=True)
                subprocess.run(["git", "commit", "-qm", "seed"], cwd=root, check=True)
                hooks.ROOT = root
                before = hooks.file_digest(["sample.cs"])
                file.write_text("second", encoding="utf-8")
                after = hooks.file_digest(["sample.cs"])
                self.assertNotEqual(before, after)
        finally:
            hooks.ROOT = original_root

    def test_successful_check_is_reused_for_same_fingerprint(self) -> None:
        original_root = hooks.ROOT
        original_results = hooks.RESULTS_PATH
        try:
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                subprocess.run(["git", "init", "-q"], cwd=root, check=True)
                subprocess.run(["git", "config", "user.email", "hooks@example.test"], cwd=root, check=True)
                subprocess.run(["git", "config", "user.name", "Hook Tests"], cwd=root, check=True)
                file = root / "sample.txt"
                file.write_text("stable", encoding="utf-8")
                subprocess.run(["git", "add", "sample.txt"], cwd=root, check=True)
                subprocess.run(["git", "commit", "-qm", "seed"], cwd=root, check=True)
                hooks.ROOT = root
                hooks.RESULTS_PATH = root / "cache" / "results.json"
                check = hooks.Check(
                    "cache-probe",
                    2,
                    10,
                    (sys.executable, "-c", "raise SystemExit(0)"),
                    frozenset({"documentation"}),
                    frozenset({"completion"}),
                    "cache probe",
                )
                classification = {"documentation": ["sample.txt"]}
                with mock.patch.object(hooks, "live_dotnet_watch", return_value=False), redirect_stdout(io.StringIO()):
                    first = hooks.execute_check(check, ["sample.txt"], classification, False)
                    second = hooks.execute_check(check, ["sample.txt"], classification, False)
                self.assertTrue(first[0])
                self.assertFalse(first[1])
                self.assertTrue(second[0])
                self.assertTrue(second[1])
        finally:
            hooks.ROOT = original_root
            hooks.RESULTS_PATH = original_results


class GateFailureTests(unittest.TestCase):
    def test_gate_stops_after_first_failed_check(self) -> None:
        with mock.patch.object(hooks, "selected_checks", return_value=list(hooks.CHECKS[:2])), mock.patch.object(
            hooks, "classify", return_value={"devex": ["AGENTS.md"]}
        ), mock.patch.object(
            hooks, "execute_check", side_effect=[(False, False, 0.1), (True, False, 0.1)]
        ) as execute, mock.patch.object(hooks, "record_receipt"), redirect_stdout(io.StringIO()):
            result = hooks.run_gate("completion", ["AGENTS.md"], "worktree", False, False)
        self.assertEqual(result, 1)
        self.assertEqual(execute.call_count, 1)

    def test_pre_migration_rejects_empty_scope(self) -> None:
        with mock.patch.object(hooks, "record_receipt") as receipt, redirect_stdout(io.StringIO()):
            result = hooks.run_gate("pre-migration", ["README.md"], "worktree", False, False)
        self.assertEqual(result, 1)
        receipt.assert_called_once_with("pre-migration", ["README.md"], False)

    def test_native_hook_failure_blocks_pre_tool(self) -> None:
        output = io.StringIO()
        with mock.patch.object(hooks, "pre_tool", side_effect=RuntimeError("boom")), mock.patch.object(
            hooks, "hook_input", return_value={}
        ), redirect_stdout(output):
            result = hooks.handle_hook("pre-tool")
        self.assertEqual(result, 0)
        payload = json.loads(output.getvalue())
        self.assertEqual(payload["hookSpecificOutput"]["permissionDecision"], "deny")


class NativeHookContractTests(unittest.TestCase):
    def test_policy_inventory_has_required_metadata(self) -> None:
        policy = hooks.load_policy()
        required = {
            "id",
            "event",
            "native",
            "tier",
            "maxSeconds",
            "frequency",
            "scope",
            "blocking",
            "reuse",
        }
        for item in policy["hooks"]:
            self.assertFalse(required - set(item), item["id"])
            self.assertIn(item["tier"], (1, 2, 3))

    def test_codex_config_uses_only_supported_command_handlers(self) -> None:
        config_path = Path(__file__).resolve().parents[2] / "hooks.json"
        config = json.loads(config_path.read_text(encoding="utf-8"))
        supported = {
            "SessionStart",
            "UserPromptSubmit",
            "PreToolUse",
            "PostToolUse",
            "SubagentStart",
            "SubagentStop",
            "Stop",
        }
        self.assertEqual(set(config["hooks"]), supported)
        self.assertNotIn("Bash", config["hooks"]["PostToolUse"][0]["matcher"])
        for groups in config["hooks"].values():
            for group in groups:
                for handler in group["hooks"]:
                    self.assertEqual(handler["type"], "command")
                    self.assertLessEqual(handler["timeout"], 2)

    def test_pre_tool_blocks_destructive_command(self) -> None:
        output = io.StringIO()
        with redirect_stdout(output):
            hooks.pre_tool({"tool_input": {"command": "git reset --hard"}})
        payload = json.loads(output.getvalue())
        decision = payload["hookSpecificOutput"]
        self.assertEqual(decision["permissionDecision"], "deny")

    def test_pre_tool_blocks_commit_hook_bypass(self) -> None:
        output = io.StringIO()
        with redirect_stdout(output):
            hooks.pre_tool({"tool_input": {"command": "git commit --no-verify -m bypass"}})
        payload = json.loads(output.getvalue())
        self.assertEqual(payload["hookSpecificOutput"]["permissionDecision"], "deny")

    def test_subagent_stop_requires_handoff_envelope_once(self) -> None:
        output = io.StringIO()
        with redirect_stdout(output):
            hooks.subagent_stop({"last_assistant_message": "done", "stop_hook_active": False})
        self.assertEqual(json.loads(output.getvalue())["decision"], "block")

        output = io.StringIO()
        with redirect_stdout(output):
            hooks.subagent_stop({"last_assistant_message": "done", "stop_hook_active": True})
        self.assertEqual(json.loads(output.getvalue()), {})

    def test_stop_does_not_block_ordinary_progress_update(self) -> None:
        output = io.StringIO()
        with redirect_stdout(output):
            hooks.stop_hook({"last_assistant_message": "Still investigating the classifier."})
        self.assertEqual(json.loads(output.getvalue()), {})

    def test_prompt_secret_detection_blocks_clear_token(self) -> None:
        output = io.StringIO()
        with redirect_stdout(output), mock.patch.object(hooks, "remember_turn_baseline"):
            hooks.prompt_submit({"prompt": "use " + "sk-" + ("a" * 30)})
        self.assertEqual(json.loads(output.getvalue())["decision"], "block")


if __name__ == "__main__":
    unittest.main()
