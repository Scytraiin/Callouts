#!/usr/bin/env python3

from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Update Callouts release metadata, rebuild via Docker, and refresh "
            "out/release artifacts."
        )
    )
    parser.add_argument("--workspace", required=True, help="Workspace root of the plugin repo.")
    parser.add_argument("--version", required=True, help="Release version, with or without leading v.")
    parser.add_argument(
        "--dalamud-dev-path",
        help="Path to the Dalamud Hooks/dev folder.",
    )
    return parser.parse_args()


def normalize_version(version: str) -> tuple[str, str, str]:
    clean_version = version.strip()
    if clean_version.startswith("v"):
        clean_version = clean_version[1:]

    if not re.fullmatch(r"\d+(?:\.\d+){1,3}(?:-[0-9A-Za-z.-]+)?", clean_version):
        raise ValueError("Version must look like 0.1.0, 0.1.0-alpha, or 1.0.0.0.")

    tag = f"v{clean_version}"
    numeric = clean_version.split("-", 1)[0]
    parts = numeric.split(".")
    while len(parts) < 4:
        parts.append("0")
    assembly_version = ".".join(parts[:4])
    return clean_version, tag, assembly_version


def replace_once(content: str, pattern: str, replacement: str, path: Path) -> str:
    updated, count = re.subn(pattern, replacement, content, count=1, flags=re.MULTILINE)
    if count != 1:
        raise RuntimeError(f"Expected exactly one replacement for {pattern!r} in {path}.")
    return updated


def update_csproj(path: Path, version: str, assembly_version: str) -> None:
    content = path.read_text(encoding="utf-8")
    content = replace_once(content, r"<Version>.*?</Version>", f"<Version>{version}</Version>", path)
    content = replace_once(
        content,
        r"<AssemblyVersion>.*?</AssemblyVersion>",
        f"<AssemblyVersion>{assembly_version}</AssemblyVersion>",
        path,
    )
    content = replace_once(
        content,
        r"<FileVersion>.*?</FileVersion>",
        f"<FileVersion>{assembly_version}</FileVersion>",
        path,
    )
    path.write_text(content, encoding="utf-8")


def update_repo_json(path: Path, version: str, tag: str, assembly_version: str) -> None:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list) or not data:
        raise RuntimeError(f"Unexpected repo JSON shape in {path}.")

    entry = data[0]
    entry["AssemblyVersion"] = assembly_version
    entry["TestingAssemblyVersion"] = assembly_version
    entry["LastUpdate"] = int(time.time())
    repo_url = entry["RepoUrl"].rstrip("/")
    url = f"{repo_url}/releases/download/{tag}/latest.zip"
    entry["DownloadLinkInstall"] = url
    entry["DownloadLinkTesting"] = url
    entry["DownloadLinkUpdate"] = url
    entry["Changelog"] = f"Callouts {version} release."
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def update_readme(path: Path, pattern: str, replacement: str) -> None:
    content = path.read_text(encoding="utf-8")
    updated = replace_once(content, pattern, replacement, path)
    path.write_text(updated, encoding="utf-8")


def run(cmd: list[str], cwd: Path) -> None:
    print(f"+ {' '.join(cmd)}")
    subprocess.run(cmd, cwd=cwd, check=True)


def verify_contains(path: Path, needle: str) -> None:
    content = path.read_text(encoding="utf-8")
    if needle not in content:
        raise RuntimeError(f"Expected {needle!r} in {path}.")


def main() -> int:
    args = parse_args()
    workspace = Path(args.workspace).expanduser().resolve()
    version, tag, assembly_version = normalize_version(args.version)

    dalamud_dev = Path(args.dalamud_dev_path).expanduser().resolve() if args.dalamud_dev_path else None

    if not workspace.is_dir():
        raise RuntimeError(f"Workspace does not exist: {workspace}")
    if dalamud_dev is None:
        raise RuntimeError("Provide --dalamud-dev-path with a valid Dalamud Hooks/dev folder.")
    if not dalamud_dev.is_dir():
        raise RuntimeError(f"Dalamud dev folder does not exist: {dalamud_dev}")

    csproj = workspace / "Callouts" / "Callouts.csproj"
    repo_json = workspace / "scyt.repo.json"
    root_readme = workspace / "README.md"

    update_csproj(csproj, version, assembly_version)
    update_repo_json(repo_json, version, tag, assembly_version)
    update_readme(
        root_readme,
        r"- the current release target is `[^`]+`",
        f"- the current release target is `{tag}`",
    )
    update_readme(
        root_readme,
        r"- Current plugin version: `[^`]+`",
        f"- Current plugin version: `{version}`",
    )

    run(["docker", "build", "-t", "callouts-ci", "."], workspace)

    out_dir = workspace / "out"
    out_dir.mkdir(parents=True, exist_ok=True)
    run(
        [
            "docker",
            "run",
            "--rm",
            "-v",
            f"{dalamud_dev}:/dalamud:ro",
            "-v",
            f"{out_dir}:/out",
            "callouts-ci",
        ],
        workspace,
    )

    release_dir = out_dir / "release"
    release_dir.mkdir(parents=True, exist_ok=True)

    built_zip = out_dir / "plugin" / "Callouts" / "latest.zip"
    if not built_zip.is_file():
        raise RuntimeError(f"Expected built zip at {built_zip}.")
    shutil.copy2(built_zip, release_dir / "latest.zip")
    shutil.copy2(repo_json, release_dir / "scyt.repo.json")

    verify_contains(out_dir / "plugin" / "Callouts.deps.json", f"Callouts/{version}")
    verify_contains(
        out_dir / "plugin" / "Callouts" / "Callouts.json",
        f'"AssemblyVersion": "{assembly_version}"',
    )
    verify_contains(release_dir / "scyt.repo.json", tag)

    print()
    print(f"Prepared release {tag}")
    print(f"Assembly version: {assembly_version}")
    print(f"Release zip: {release_dir / 'latest.zip'}")
    print(f"Release repo JSON: {release_dir / 'scyt.repo.json'}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:  # pragma: no cover
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
