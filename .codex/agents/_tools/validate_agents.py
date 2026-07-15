from __future__ import annotations

import sys
import tomllib
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
AGENTS_DIR = ROOT / ".codex" / "agents"
README = AGENTS_DIR / "README.md"
CONFIG = ROOT / ".codex" / "config.toml"

REQUIRED_FIELDS = ("name", "description", "developer_instructions")
REQUIRED_SECTIONS = (
    "Mission:",
    "Responsibilities:",
    "Authority:",
    "Limitations:",
    "Invoke when:",
    "Never invoke when:",
    "Expected inputs:",
    "Expected outputs:",
    "Verification checklist:",
    "Communication:",
)
ALLOWED_MODELS = {"gpt-5.6-luna", "gpt-5.6-terra", "gpt-5.6-sol"}
ALLOWED_SANDBOXES = {"read-only", "workspace-write", "danger-full-access"}


def main() -> int:
    errors: list[str] = []
    names: list[str] = []
    readme = README.read_text(encoding="utf-8")

    config = tomllib.loads(CONFIG.read_text(encoding="utf-8"))
    if config.get("agents", {}).get("max_depth") != 1:
        errors.append(".codex/config.toml: agents.max_depth must remain 1")

    files = sorted(AGENTS_DIR.glob("*.toml"))
    for path in files:
        try:
            data = tomllib.loads(path.read_text(encoding="utf-8"))
        except tomllib.TOMLDecodeError as exc:
            errors.append(f"{path.name}: invalid TOML: {exc}")
            continue

        for field in REQUIRED_FIELDS:
            if not data.get(field):
                errors.append(f"{path.name}: missing {field}")

        name = data.get("name", "")
        names.append(name)
        if name != path.stem:
            errors.append(f"{path.name}: name must match filename")
        if f"`{name}`" not in readme:
            errors.append(f"{path.name}: agent is missing from README roster")

        model = data.get("model")
        if model not in ALLOWED_MODELS:
            errors.append(f"{path.name}: unsupported project model {model!r}")
        sandbox = data.get("sandbox_mode")
        if sandbox is not None and sandbox not in ALLOWED_SANDBOXES:
            errors.append(f"{path.name}: unsupported sandbox_mode {sandbox!r}")

        instructions = data.get("developer_instructions", "")
        for section in REQUIRED_SECTIONS:
            if section not in instructions:
                errors.append(f"{path.name}: missing section {section}")

    duplicates = sorted({name for name in names if names.count(name) > 1})
    for name in duplicates:
        errors.append(f"duplicate agent name: {name}")

    if errors:
        print("Agent validation failed:")
        for error in errors:
            print(f"- {error}")
        return 1

    print(f"Validated {len(files)} Planora custom agents.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
