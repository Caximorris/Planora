from __future__ import annotations

import argparse
import fnmatch
import functools
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence


SCRIPT = Path(__file__).resolve()
ROOT = SCRIPT.parents[2]
POLICY_PATH = SCRIPT.with_name("policy.json")
CACHE_DIR = ROOT / ".codex" / "cache"
RESULTS_PATH = CACHE_DIR / "results.json"
TURN_STATE_PATH = CACHE_DIR / "turn-state.json"

SOURCE_CATEGORIES = {
    "backend",
    "frontend",
    "shared_contract",
    "tests",
    "api_contract",
    "authentication",
    "authorization",
    "database",
    "migration",
    "dependencies",
    "configuration",
    "ui",
    "ci",
    "container",
    "security",
}

TIER3_CATEGORIES = SOURCE_CATEGORIES - {"configuration"}

COMPLETION_WORDS = re.compile(
    r"\b(complete[ds]?|implemented|finished|fixed|resolved|done|ready)\b", re.IGNORECASE
)

CLEAR_SECRET_PATTERNS = (
    ("private key", re.compile(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----")),
    ("OpenAI-style API key", re.compile(r"\bsk-[A-Za-z0-9_-]{20,}\b")),
    ("GitHub token", re.compile(r"\b(?:ghp|github_pat)_[A-Za-z0-9_]{20,}\b")),
    ("Azure storage account key", re.compile(r"\bAccountKey=[A-Za-z0-9+/=]{20,}")),
    ("Resend API key", re.compile(r"\bre_[A-Za-z0-9_-]{20,}\b")),
)

FORBIDDEN_CHANGED_PATHS = (
    re.compile(r"(^|/)appsettings\.Development\.json$", re.IGNORECASE),
    re.compile(r"(^|/)\.env(?:\.|$)", re.IGNORECASE),
    re.compile(r"(^|/)\.git/", re.IGNORECASE),
    re.compile(r"(^|/)(?:bin|obj)/", re.IGNORECASE),
    re.compile(r"\.(?:pfx|p12|pem|key)$", re.IGNORECASE),
)

FORMAT_CHECK_IDS = {"format", "release-format"}
FORMAT_SUFFIXES = {".cs", ".razor"}


@dataclass(frozen=True)
class Check:
    id: str
    tier: int
    max_seconds: int
    command: tuple[str, ...] | None
    categories: frozenset[str]
    gates: frozenset[str]
    purpose: str


CHECKS = (
    Check("fast-audit", 1, 2, None, frozenset(), frozenset({"commit", "pre-migration", "pr", "completion"}), "classify files, detect secrets, inspect whitespace and architecture"),
    Check("agent-library", 2, 10, ("python", ".codex/agents/_tools/validate_agents.py"), frozenset({"devex"}), frozenset({"commit", "pr", "completion"}), "validate custom agent profiles"),
    Check("skill-library", 2, 15, ("powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".agents/skills/_tools/validate-library.ps1"), frozenset({"devex"}), frozenset({"commit", "pr", "completion"}), "validate repository skill packages"),
    Check("hook-tests", 2, 15, ("python", "-m", "unittest", "discover", "-s", ".codex/hooks/tests", "-p", "test_*.py"), frozenset({"devex"}), frozenset({"commit", "pr", "completion"}), "exercise classifier, routing, cache, and hook contracts"),
    Check("api-build", 2, 30, ("dotnet", "build", "Planora.Api/Planora.Api.csproj", "--no-restore"), frozenset({"backend", "database", "authentication", "authorization", "container"}), frozenset({"commit", "pre-migration"}), "compile affected backend code"),
    Check("web-build", 2, 30, ("dotnet", "build", "Planora.Web/Planora.Web.csproj", "--no-restore"), frozenset({"frontend", "ui"}), frozenset({"commit"}), "compile affected frontend code"),
    Check("contract-build", 2, 30, ("dotnet", "build", "Planora.slnx", "--no-restore"), frozenset({"shared_contract", "api_contract"}), frozenset({"commit"}), "compile both Shared consumers"),
    Check("format", 2, 30, ("dotnet", "format", "Planora.slnx", "--verify-no-changes", "--no-restore"), frozenset({"backend", "frontend", "shared_contract", "tests", "api_contract", "authentication", "authorization", "database", "migration", "ui"}), frozenset({"commit"}), "enforce the repository formatter gate"),
    Check("restore", 3, 180, ("dotnet", "restore", "Planora.slnx"), SOURCE_CATEGORIES, frozenset({"pr", "completion"}), "restore the exact dependency graph"),
    Check("release-build", 3, 300, ("dotnet", "build", "Planora.slnx", "--configuration", "Release", "--no-restore"), SOURCE_CATEGORIES, frozenset({"pr", "completion"}), "compile the complete release solution"),
    Check("full-tests", 3, 900, ("dotnet", "test", "Planora.slnx", "--configuration", "Release", "--no-build", "--verbosity", "normal"), SOURCE_CATEGORIES, frozenset({"pr", "completion"}), "run the PostgreSQL-backed full suite"),
    Check("release-format", 3, 180, ("dotnet", "format", "Planora.slnx", "--verify-no-changes", "--no-restore"), SOURCE_CATEGORIES, frozenset({"pr", "completion"}), "enforce formatting after release restore"),
    Check("dependency-audit", 3, 300, ("dotnet", "list", "Planora.slnx", "package", "--vulnerable", "--include-transitive"), frozenset({"dependencies"}), frozenset({"pr", "completion"}), "query transitive NuGet vulnerability data"),
)


def load_policy() -> dict[str, Any]:
    return json.loads(POLICY_PATH.read_text(encoding="utf-8"))


def run_git(args: Sequence[str], *, timeout: float = 1.5) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args], cwd=ROOT, text=True, capture_output=True, timeout=timeout, check=False
    )


def normalize_path(value: str) -> str:
    value = value.strip().strip('"\'').replace("\\", "/")
    root = ROOT.as_posix().rstrip("/") + "/"
    if value.lower().startswith(root.lower()):
        value = value[len(root) :]
    while value.startswith("./"):
        value = value[2:]
    return value


def unique(values: Iterable[str]) -> list[str]:
    return list(dict.fromkeys(value for value in values if value))


def changed_files(mode: str = "worktree") -> list[str]:
    commands: list[list[str]] = []
    if mode == "staged":
        commands.append(["diff", "--cached", "--name-only", "--diff-filter=ACMRD"])
    elif mode == "pr":
        base = run_git(["merge-base", "HEAD", "origin/main"])
        if base.returncode == 0 and base.stdout.strip():
            commands.append(["diff", "--name-only", "--diff-filter=ACMRD", f"{base.stdout.strip()}..HEAD"])
        else:
            parent = run_git(["rev-parse", "HEAD~1"])
            if parent.returncode == 0 and parent.stdout.strip():
                commands.append(["diff", "--name-only", "--diff-filter=ACMRD", f"{parent.stdout.strip()}..HEAD"])
            else:
                # A repository with no merge base/parent is safer to validate in full than to skip.
                commands.append(["ls-files"])
        commands.extend(
            [
                ["diff", "--name-only", "--diff-filter=ACMRD"],
                ["diff", "--cached", "--name-only", "--diff-filter=ACMRD"],
                ["ls-files", "--others", "--exclude-standard"],
            ]
        )
    else:
        commands.extend(
            [
                ["diff", "--name-only", "--diff-filter=ACMRD"],
                ["diff", "--cached", "--name-only", "--diff-filter=ACMRD"],
                ["ls-files", "--others", "--exclude-standard"],
            ]
        )

    paths: list[str] = []
    for command in commands:
        result = run_git(command)
        if result.returncode != 0:
            raise RuntimeError(f"git changed-file detection failed: {' '.join(command)}")
        paths.extend(normalize_path(line) for line in result.stdout.splitlines())
    return sorted(unique(paths))


def classify(paths: Iterable[str], policy: dict[str, Any] | None = None) -> dict[str, list[str]]:
    policy = policy or load_policy()
    result: dict[str, list[str]] = {}
    for raw_path in paths:
        path = normalize_path(raw_path)
        for category, patterns in policy["categories"].items():
            if any(fnmatch.fnmatchcase(path.lower(), pattern.lower()) for pattern in patterns):
                result.setdefault(category, []).append(path)
    return {category: sorted(unique(items)) for category, items in sorted(result.items())}


def categories_for(paths: Iterable[str]) -> set[str]:
    return set(classify(paths))


def routing_context(categories: Iterable[str]) -> tuple[list[str], list[str]]:
    policy = load_policy()
    skills = ["planora-workflow"]
    agents: list[str] = []
    for category in sorted(set(categories)):
        route = policy.get("routing", {}).get(category, {})
        skills.extend(route.get("skills", []))
        agents.extend(route.get("agents", []))
    return unique(skills), unique(agents)


def clear_secrets(text: str) -> list[str]:
    return [label for label, pattern in CLEAR_SECRET_PATTERNS if pattern.search(text)]


def inspect_files_for_secrets(paths: Iterable[str]) -> list[str]:
    findings: list[str] = []
    for relative in paths:
        path = ROOT / relative
        if not path.is_file() or path.stat().st_size > 1_000_000:
            continue
        try:
            content = path.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue
        for label in clear_secrets(content):
            findings.append(f"{relative}: possible {label}")
    return findings


def forbidden_paths(paths: Iterable[str]) -> list[str]:
    return [path for path in paths if any(pattern.search(path) for pattern in FORBIDDEN_CHANGED_PATHS)]


def whitespace_findings(paths: Iterable[str]) -> list[str]:
    findings: list[str] = []
    for relative in paths:
        path = ROOT / relative
        if not path.is_file() or path.stat().st_size > 1_000_000:
            continue
        try:
            lines = path.read_text(encoding="utf-8").splitlines()
        except (UnicodeDecodeError, OSError):
            continue
        for number, line in enumerate(lines, 1):
            if line.endswith((" ", "\t")):
                findings.append(f"{relative}:{number}: trailing whitespace")
                if len(findings) >= 20:
                    return findings
    return findings


def architecture_findings(paths: Iterable[str]) -> list[str]:
    findings: list[str] = []
    for relative in paths:
        path = ROOT / relative
        if not path.is_file() or path.suffix.lower() not in {".cs", ".razor"}:
            continue
        try:
            content = path.read_text(encoding="utf-8")
        except OSError:
            continue
        if relative.startswith("Planora.Web/") and re.search(
            r"\b(Microsoft\.EntityFrameworkCore|Npgsql|ApplicationDbContext|UserManager<)", content
        ):
            findings.append(f"{relative}: Web must not depend on persistence or Identity stores")
        if relative.startswith("Planora.Shared/") and re.search(
            r"\b(Microsoft\.EntityFrameworkCore|Npgsql|Planora\.Api|Azure\.Storage)", content
        ):
            findings.append(f"{relative}: Shared must remain infrastructure-free")
        if relative.startswith("Planora.Api/Controllers/") and re.search(
            r"(?:ActionResult|Task<ActionResult)<\s*(?:IEnumerable<)?\s*(?:Planora\.Api\.)?Domain\.Entities", content
        ):
            findings.append(f"{relative}: public endpoints must not expose EF entities")
    return findings


def migration_edit_findings(paths: Iterable[str], command: str = "") -> list[str]:
    if "dotnet ef migrations add" in command.lower():
        return []
    findings: list[str] = []
    for relative in paths:
        if not relative.startswith("Planora.Api/Migrations/"):
            continue
        name = Path(relative).name
        if name == "ApplicationDbContextModelSnapshot.cs" or re.match(r"\d{14}_.*\.cs$", name):
            findings.append(f"{relative}: generate migration artifacts with dotnet ef; do not edit them directly")
    return findings


def fast_audit(paths: Iterable[str]) -> tuple[list[str], list[str]]:
    path_list = list(paths)
    blockers = forbidden_paths(path_list) + inspect_files_for_secrets(path_list)
    warnings = whitespace_findings(path_list) + architecture_findings(path_list)
    for arguments in (["diff", "--check"], ["diff", "--cached", "--check"]):
        result = run_git(arguments)
        if result.returncode != 0:
            blockers.extend(line for line in result.stdout.splitlines() if line.strip())
    return unique(blockers), unique(warnings)


def file_digest(paths: Iterable[str]) -> str:
    digest = hashlib.sha256()
    digest.update(POLICY_PATH.read_bytes())
    digest.update(SCRIPT.read_bytes())
    for relative in sorted(unique(paths)):
        digest.update(relative.encode())
        path = ROOT / relative
        if path.is_file():
            try:
                digest.update(path.read_bytes())
            except OSError:
                digest.update(b"unreadable")
        else:
            digest.update(b"deleted")
    return digest.hexdigest()


@functools.lru_cache(maxsize=1)
def dotnet_version() -> str:
    try:
        result = subprocess.run(
            ["dotnet", "--version"], cwd=ROOT, text=True, capture_output=True, timeout=2, check=False
        )
        return result.stdout.strip() if result.returncode == 0 else "dotnet-unavailable"
    except (OSError, subprocess.TimeoutExpired):
        return "dotnet-unavailable"


def load_json(path: Path, default: Any) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return default


def save_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, sort_keys=True), encoding="utf-8")
    temporary.replace(path)


def check_fingerprint(check: Check, paths: Iterable[str]) -> str:
    path_list = list(paths)
    digest = hashlib.sha256()
    digest.update(file_digest(path_list).encode())
    digest.update(dotnet_version().encode())
    digest.update(check.id.encode())
    digest.update(json.dumps(resolved_command(check, path_list)).encode())
    return digest.hexdigest()


def format_paths(paths: Iterable[str]) -> list[str]:
    return sorted(
        unique(
            normalize_path(path)
            for path in paths
            if Path(normalize_path(path)).suffix.lower() in FORMAT_SUFFIXES
            and (ROOT / normalize_path(path)).is_file()
        )
    )


def resolved_command(check: Check, paths: Iterable[str] = ()) -> tuple[str, ...] | None:
    if check.command is None:
        return None
    command = list(check.command)
    if command[0] == "python":
        command[0] = sys.executable
    elif command[0] == "powershell":
        executable = shutil.which("pwsh") or shutil.which("powershell")
        if executable:
            command[0] = executable
    if check.id in FORMAT_CHECK_IDS:
        included = format_paths(paths)
        if included:
            command.extend(["--include", *included])
    return tuple(command)


def receipt_is_current(gate: str, paths: Iterable[str]) -> bool:
    results = load_json(RESULTS_PATH, {})
    receipt = results.get("receipts", {}).get(gate)
    return bool(receipt and receipt.get("fingerprint") == file_digest(paths) and receipt.get("success"))


def record_receipt(gate: str, paths: Iterable[str], success: bool) -> None:
    results = load_json(RESULTS_PATH, {"checks": {}, "receipts": {}})
    results.setdefault("receipts", {})[gate] = {
        "fingerprint": file_digest(paths),
        "success": success,
        "timestamp": int(time.time()),
    }
    save_json(RESULTS_PATH, results)


def relevant_paths(check: Check, paths: Sequence[str], classification: dict[str, list[str]]) -> list[str]:
    if check.id in FORMAT_CHECK_IDS:
        changed_source = [
            path for path in paths if Path(normalize_path(path)).suffix.lower() in FORMAT_SUFFIXES
        ]
        return sorted(
            unique(
                [
                    *changed_source,
                    "Planora.slnx",
                    "Planora.Api/Planora.Api.csproj",
                    "Planora.Web/Planora.Web.csproj",
                    "Planora.Shared/Planora.Shared.csproj",
                    "Planora.Tests/Planora.Tests.csproj",
                ]
            )
        )
    scopes = {
        "agent-library": (".codex/agents", ".codex/config.toml"),
        "skill-library": (".agents/skills",),
        "hook-tests": (".codex/hooks", ".codex/hooks.json", ".githooks", "scripts"),
        "api-build": ("Planora.Api", "Planora.Shared"),
        "web-build": ("Planora.Web", "Planora.Shared"),
        "contract-build": ("Planora.Api", "Planora.Web", "Planora.Shared", "Planora.Tests"),
        "restore": ("Planora.Api", "Planora.Web", "Planora.Shared", "Planora.Tests"),
        "release-build": ("Planora.Api", "Planora.Web", "Planora.Shared", "Planora.Tests"),
        "full-tests": ("Planora.Api", "Planora.Web", "Planora.Shared", "Planora.Tests"),
    }
    if check.id in scopes:
        selected: list[str] = []
        for scope in scopes[check.id]:
            candidate = ROOT / scope
            if candidate.is_file():
                selected.append(scope)
            elif candidate.is_dir():
                selected.extend(
                    path.relative_to(ROOT).as_posix()
                    for path in candidate.rglob("*")
                    if path.is_file()
                    and path.suffix != ".pyc"
                    and not {
                        "bin",
                        "obj",
                        ".git",
                        "__pycache__",
                        "cache",
                        "run-logs",
                        "uploads",
                    }
                    & set(path.parts)
                )
        selected.extend(
            [
                "Planora.slnx",
                "Planora.Api/Planora.Api.csproj",
                "Planora.Web/Planora.Web.csproj",
                "Planora.Shared/Planora.Shared.csproj",
                "Planora.Tests/Planora.Tests.csproj",
            ]
        )
        return sorted(unique(selected))
    if not check.categories:
        return list(paths)
    selected: list[str] = []
    for category in check.categories:
        selected.extend(classification.get(category, []))
    # HEAD guards reuse when an unchanged dependency file affects an affected project.
    selected.extend(["Planora.slnx", "Planora.Api/Planora.Api.csproj", "Planora.Web/Planora.Web.csproj", "Planora.Shared/Planora.Shared.csproj", "Planora.Tests/Planora.Tests.csproj"])
    return sorted(unique(selected))


def selected_checks(gate: str, paths: Sequence[str]) -> list[Check]:
    classification = classify(paths)
    categories = set(classification)
    runtime_configuration_changed = any(
        not path.startswith((".codex/", ".agents/", ".githooks/", "scripts/"))
        for path in classification.get("configuration", [])
    )
    application_changed = bool(categories & TIER3_CATEGORIES) or runtime_configuration_changed
    selected: list[Check] = []
    for check in CHECKS:
        if gate not in check.gates:
            continue
        if check.tier == 1:
            selected.append(check)
        elif check.categories & categories:
            if check.tier < 3 or application_changed:
                selected.append(check)
    selected_ids = {check.id for check in selected}
    if "contract-build" in selected_ids:
        selected = [check for check in selected if check.id not in {"api-build", "web-build"}]
    if not format_paths(paths):
        selected = [check for check in selected if check.id not in FORMAT_CHECK_IDS]
    return selected


def gate_plan(gate: str, paths: Sequence[str]) -> dict[str, Any]:
    classification = classify(paths)
    checks = selected_checks(gate, paths)
    return {
        "gate": gate,
        "files": list(paths),
        "categories": sorted(classification),
        "checks": [
            {
                "id": check.id,
                "tier": check.tier,
                "maxSeconds": check.max_seconds,
            }
            for check in checks
        ],
        "maxTier": max((check.tier for check in checks), default=0),
        "requiresPostgres": any(check.id == "full-tests" for check in checks),
    }


def live_dotnet_watch() -> bool:
    commands = (
        ["powershell", "-NoProfile", "-Command", "@(Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\" | Where-Object CommandLine -Match 'watch').Count"],
        ["pgrep", "-af", "dotnet.*watch"],
    )
    for command in commands:
        try:
            result = subprocess.run(command, text=True, capture_output=True, timeout=1.2, check=False)
        except (OSError, subprocess.TimeoutExpired):
            continue
        if command[0] == "powershell":
            return result.returncode == 0 and result.stdout.strip() not in {"", "0"}
        return result.returncode == 0 and bool(result.stdout.strip())
    return False


def execute_check(check: Check, paths: Sequence[str], classification: dict[str, list[str]], force: bool) -> tuple[bool, bool, float]:
    if check.id == "fast-audit":
        started = time.perf_counter()
        blockers, warnings = fast_audit(paths)
        for warning in warnings:
            print(f"WARN: {warning}")
        for blocker in blockers:
            print(f"ERROR: {blocker}")
        return not blockers, False, time.perf_counter() - started

    relevant = relevant_paths(check, paths, classification)
    fingerprint = check_fingerprint(check, relevant)
    results = load_json(RESULTS_PATH, {"checks": {}, "receipts": {}})
    previous = results.get("checks", {}).get(check.id)
    if not force and previous and previous.get("fingerprint") == fingerprint and previous.get("success"):
        print(f"REUSED T{check.tier} {check.id} ({previous.get('durationSeconds', 0):.2f}s previous run)")
        return True, True, 0.0

    command = resolved_command(check, relevant)
    assert command is not None
    if command[0] == "dotnet" and live_dotnet_watch():
        print(f"ERROR: {check.id} blocked because a dotnet watch process is live.")
        return False, False, 0.0

    print(f"RUN T{check.tier} {check.id}: {' '.join(command)}")
    started = time.perf_counter()
    try:
        completed = subprocess.run(command, cwd=ROOT, timeout=check.max_seconds, check=False)
        success = completed.returncode == 0
    except subprocess.TimeoutExpired:
        print(f"ERROR: {check.id} exceeded {check.max_seconds}s")
        success = False
    except OSError as exc:
        print(f"ERROR: {check.id} could not start: {exc}")
        success = False
    duration = time.perf_counter() - started
    results = load_json(RESULTS_PATH, {"checks": {}, "receipts": {}})
    results.setdefault("checks", {})[check.id] = {
        "fingerprint": fingerprint,
        "success": success,
        "durationSeconds": round(duration, 3),
        "tier": check.tier,
        "timestamp": int(time.time()),
    }
    save_json(RESULTS_PATH, results)
    print(f"{'PASS' if success else 'FAIL'} T{check.tier} {check.id} ({duration:.2f}s)")
    return success, False, duration


def run_gate(gate: str, paths: Sequence[str] | None, mode: str, force: bool, dry_run: bool) -> int:
    path_list = list(paths) if paths is not None else changed_files(mode)
    classification = classify(path_list)
    if gate == "pre-migration" and not ({"backend", "database"} & set(classification)):
        print("ERROR: pre-migration requires a changed backend/database model input; refusing an empty migration receipt.")
        if not dry_run:
            record_receipt(gate, path_list, False)
        return 1
    checks = selected_checks(gate, path_list)
    print(f"Gate: {gate} | Files: {len(path_list)} | Categories: {', '.join(classification) or 'none'}")
    print("Checks: " + (", ".join(f"T{check.tier}:{check.id}" for check in checks) or "none"))
    if dry_run:
        return 0

    success = True
    for check in checks:
        passed, _, _ = execute_check(check, path_list, classification, force)
        if not passed:
            success = False
            break
    record_receipt(gate, path_list, success)
    if success:
        skills, agents = routing_context(classification)
        print(f"Required Skills: {', '.join(skills)}")
        if agents:
            print(f"Risk-selected agents (invoke only for independent specialist review): {', '.join(agents)}")
    return 0 if success else 1


def hook_input() -> dict[str, Any]:
    try:
        raw = sys.stdin.read()
        return json.loads(raw) if raw.strip() else {}
    except json.JSONDecodeError:
        return {}


def hook_output(event: str, context: str = "", **extra: Any) -> None:
    output: dict[str, Any] = {}
    hook_specific = {"hookEventName": event}
    for key, value in extra.items():
        if key in {"permissionDecision", "permissionDecisionReason", "updatedInput", "additionalContext"}:
            hook_specific[key] = value
        else:
            output[key] = value
    if context:
        hook_specific["additionalContext"] = context
    if len(hook_specific) > 1:
        output["hookSpecificOutput"] = hook_specific
    print(json.dumps(output))


def command_paths(command: str) -> list[str]:
    patterns = (
        r"^\*\*\* (?:Update|Add|Delete) File:\s*(.+)$",
        r"^(?:\+\+\+|---)\s+(?:[ab]/)?(.+)$",
    )
    paths: list[str] = []
    for line in command.splitlines():
        for pattern in patterns:
            match = re.match(pattern, line.strip())
            if match:
                candidate = normalize_path(match.group(1))
                if candidate != "/dev/null":
                    paths.append(candidate)
    return unique(paths)


def pre_tool(data: dict[str, Any]) -> None:
    tool_input = data.get("tool_input") if isinstance(data.get("tool_input"), dict) else {}
    command = str(tool_input.get("command", ""))
    lowered = command.lower()
    paths = command_paths(command)
    secrets = clear_secrets(command)
    if secrets:
        hook_output(
            "PreToolUse",
            permissionDecision="deny",
            permissionDecisionReason=f"Tool input appears to contain a {', '.join(secrets)}. Redact it first.",
        )
        return
    destructive = (
        "git reset --hard",
        "git checkout --",
        "drop database",
        "dotnet ef database drop",
    )
    if any(token in lowered for token in destructive):
        hook_output(
            "PreToolUse",
            permissionDecision="deny",
            permissionDecisionReason="Planora policy blocks destructive history/database cleanup.",
        )
        return
    if re.search(r"\bgit\s+commit\b", lowered) and "--no-verify" in lowered:
        hook_output(
            "PreToolUse",
            permissionDecision="deny",
            permissionDecisionReason="Planora policy forbids bypassing the blocking pre-commit gate.",
        )
        return
    migration_findings = migration_edit_findings(paths, command)
    if migration_findings:
        hook_output(
            "PreToolUse",
            permissionDecision="deny",
            permissionDecisionReason="; ".join(migration_findings),
        )
        return
    if "dotnet ef migrations add" in lowered and not receipt_is_current("pre-migration", changed_files()):
        hook_output(
            "PreToolUse",
            permissionDecision="deny",
            permissionDecisionReason="Run scripts/Invoke-PlanoraQuality.ps1 -Gate pre-migration first.",
        )
        return
    if re.search(r"\bgh\s+pr\s+create\b", lowered) and not receipt_is_current("pr", changed_files("pr")):
        hook_output(
            "PreToolUse",
            permissionDecision="deny",
            permissionDecisionReason="Run scripts/Invoke-PlanoraQuality.ps1 -Gate pr before creating the PR.",
        )
        return
    if re.search(r"\bdotnet\s+(?:build|test|format)\b", lowered) and live_dotnet_watch():
        hook_output(
            "PreToolUse",
            permissionDecision="deny",
            permissionDecisionReason="Stop live dotnet watch processes before build/test/format.",
        )
        return

    categories = categories_for(paths)
    skills, agents = routing_context(categories)
    warnings: list[str] = []
    if any(path.startswith(".github/workflows/") for path in paths):
        warnings.append("CI/CD edits require explicit human approval before modification.")
    if any(token in lowered for token in ("git push", "gh pr merge", "az containerapp", "docker push")):
        warnings.append("External push/merge/deploy actions require explicit human approval.")
    if categories:
        warnings.append(f"Changed scope: {', '.join(sorted(categories))}.")
        warnings.append(f"Use Skills: {', '.join(skills)}.")
        if agents:
            warnings.append(f"Use risk-selected agents only when independent review helps: {', '.join(agents)}.")
    if warnings:
        hook_output("PreToolUse", " ".join(warnings))
    else:
        print("{}")


def post_tool(data: dict[str, Any]) -> None:
    paths = changed_files()
    blockers, warnings = fast_audit(paths)
    classification = classify(paths)
    skills, agents = routing_context(classification)
    messages = warnings[:8]
    if classification:
        messages.append(f"Current change categories: {', '.join(classification)}.")
        messages.append(f"Next use Skills: {', '.join(skills)}.")
        if agents:
            messages.append(f"Independent review candidates: {', '.join(agents)}.")
    if blockers:
        reason = "Fast audit found blocking issues: " + "; ".join(blockers[:8])
        print(json.dumps({"decision": "block", "reason": reason, "hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": reason}}))
    elif messages:
        hook_output("PostToolUse", " ".join(messages))
    else:
        print("{}")


def remember_turn_baseline(data: dict[str, Any]) -> None:
    turn_id = str(data.get("turn_id") or "session")
    state = load_json(TURN_STATE_PATH, {})
    state[turn_id] = {"fingerprint": file_digest(changed_files()), "timestamp": int(time.time())}
    # Bound local state without introducing a cleanup hook.
    newest = sorted(state.items(), key=lambda item: item[1].get("timestamp", 0), reverse=True)[:50]
    save_json(TURN_STATE_PATH, dict(newest))


def session_start(data: dict[str, Any]) -> None:
    remember_turn_baseline(data)
    paths = changed_files()
    categories = classify(paths)
    context = (
        "Planora hooks active. Apply planora-workflow, classify changed files before editing, "
        "and run scripts/Invoke-PlanoraQuality.ps1 -Gate completion before claiming completion. "
        f"Current pre-existing categories: {', '.join(categories) or 'none'}; preserve unrelated changes."
    )
    hook_output("SessionStart", context)


def prompt_submit(data: dict[str, Any]) -> None:
    prompt = str(data.get("prompt", ""))
    secrets = clear_secrets(prompt)
    if secrets:
        print(json.dumps({"decision": "block", "reason": f"Prompt appears to contain a {', '.join(secrets)}. Remove or redact it before submitting."}))
        return
    remember_turn_baseline(data)
    keyword_paths: list[str] = []
    keyword_map = {
        "migration": "Planora.Api/Migrations/pending.cs",
        "database": "Planora.Api/Infrastructure/Data/pending.cs",
        "auth": "Planora.Api/Controllers/AuthController.cs",
        "permission": "Planora.Api/Application/Services/WorkspaceAccessService.cs",
        "api": "Planora.Api/Controllers/pending.cs",
        "frontend": "Planora.Web/Pages/pending.razor",
        "blazor": "Planora.Web/Pages/pending.razor",
        "ui": "Planora.Web/Pages/pending.razor",
        "dependency": "Planora.Api/Planora.Api.csproj",
        "ci": ".github/workflows/ci.yml",
    }
    lowered = prompt.lower()
    for keyword, synthetic_path in keyword_map.items():
        if keyword in lowered:
            keyword_paths.append(synthetic_path)
    categories = classify(keyword_paths)
    skills, agents = routing_context(categories)
    context = "Use planora-workflow and inspect before editing."
    if categories:
        context += f" Prompt risk: {', '.join(categories)}. Applicable Skills: {', '.join(skills)}."
        if agents:
            context += f" Candidate specialist agents: {', '.join(agents)}; invoke only for bounded independent work."
    context += " Before final completion, run scripts/Invoke-PlanoraQuality.ps1 -Gate completion."
    hook_output("UserPromptSubmit", context)


def subagent_start(data: dict[str, Any]) -> None:
    agent_type = str(data.get("agent_type") or "unknown")
    hook_output(
        "SubagentStart",
        f"Planora subagent {agent_type}: read AGENTS.md and .codex/agents/PROTOCOL.md; use planora-workflow plus the narrow owning Skill; do not broaden scope, spawn recursively, push, deploy, or cross approval gates.",
    )


def subagent_stop(data: dict[str, Any]) -> None:
    if data.get("stop_hook_active"):
        print("{}")
        return
    message = str(data.get("last_assistant_message") or "")
    required = ("STATUS:", "SCOPE:", "EVIDENCE:", "OUTPUT:", "RISKS:", "VALIDATION:", "HANDOFFS:")
    missing = [field for field in required if field not in message]
    if missing:
        print(json.dumps({"decision": "block", "reason": f"Return the required Planora handoff envelope; missing: {', '.join(missing)}"}))
    else:
        print("{}")


def stop_hook(data: dict[str, Any]) -> None:
    if data.get("stop_hook_active"):
        print("{}")
        return
    message = str(data.get("last_assistant_message") or "")
    if not COMPLETION_WORDS.search(message):
        print("{}")
        return
    turn_id = str(data.get("turn_id") or "session")
    paths = changed_files()
    if not paths:
        print("{}")
        return
    current = file_digest(paths)
    state = load_json(TURN_STATE_PATH, {})
    baseline = state.get(turn_id, {}).get("fingerprint")
    if baseline == current:
        print("{}")
        return
    if not receipt_is_current("completion", paths):
        print(json.dumps({"decision": "block", "reason": "Completion was claimed after repository changes without a current completion receipt. Run scripts/Invoke-PlanoraQuality.ps1 -Gate completion, inspect git diff/status, then report exact results."}))
        return
    print("{}")


def handle_hook(name: str) -> int:
    data = hook_input()
    handlers = {
        "session-start": session_start,
        "prompt-submit": prompt_submit,
        "pre-tool": pre_tool,
        "post-tool": post_tool,
        "subagent-start": subagent_start,
        "subagent-stop": subagent_stop,
        "stop": stop_hook,
    }
    try:
        handlers[name](data)
    except Exception:
        reason = "Planora hook could not evaluate repository policy. Stop and run the quality gate manually; do not continue through an unknown state."
        if name == "pre-tool":
            hook_output("PreToolUse", permissionDecision="deny", permissionDecisionReason=reason)
        elif name in {"prompt-submit", "post-tool", "subagent-stop", "stop"}:
            if data.get("stop_hook_active"):
                print("{}")
            else:
                print(json.dumps({"decision": "block", "reason": reason}))
        elif name == "session-start":
            hook_output("SessionStart", reason)
        else:
            hook_output("SubagentStart", reason)
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Planora Codex hook and quality orchestrator")
    subparsers = parser.add_subparsers(dest="command", required=True)

    hook_parser = subparsers.add_parser("hook")
    hook_parser.add_argument("name", choices=("session-start", "prompt-submit", "pre-tool", "post-tool", "subagent-start", "subagent-stop", "stop"))

    classify_parser = subparsers.add_parser("classify")
    classify_parser.add_argument("files", nargs="*")
    classify_parser.add_argument("--mode", choices=("worktree", "staged", "pr"), default="worktree")
    classify_parser.add_argument("--json", action="store_true")

    plan_parser = subparsers.add_parser("plan")
    plan_parser.add_argument("--gate", choices=("commit", "pre-migration", "pr", "completion"), required=True)
    plan_parser.add_argument("--json", action="store_true")
    plan_parser.add_argument("files", nargs="*")

    run_parser = subparsers.add_parser("run")
    run_parser.add_argument("--gate", choices=("commit", "pre-migration", "pr", "completion"), required=True)
    run_parser.add_argument("--mode", choices=("worktree", "staged", "pr"))
    run_parser.add_argument("--force", action="store_true")

    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.command == "hook":
        return handle_hook(args.name)
    if args.command == "classify":
        paths = args.files or changed_files(args.mode)
        result = classify(paths)
        if args.json:
            print(json.dumps(result, indent=2))
        else:
            for category, files in result.items():
                print(f"{category}: {', '.join(files)}")
        return 0
    if args.command == "plan":
        mode = "staged" if args.gate == "commit" else "pr" if args.gate == "pr" else "worktree"
        paths = args.files or changed_files(mode)
        if args.json:
            print(json.dumps(gate_plan(args.gate, paths), indent=2))
            return 0
        return run_gate(args.gate, paths, mode, False, True)
    mode = args.mode or ("staged" if args.gate == "commit" else "pr" if args.gate == "pr" else "worktree")
    return run_gate(args.gate, None, mode, args.force, False)


if __name__ == "__main__":
    sys.exit(main())
