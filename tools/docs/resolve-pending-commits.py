#!/usr/bin/env python3
"""Resolve `PENDING` commit placeholders in SCADA Builder V2 documentation.

Le standard documentaire (`docs/00_governance/VERSIONING_AND_CHANGELOG_POLICY_V2.md`)
autorise `PENDING` uniquement tant que le commit de fermeture n'existe pas. Une fois la
ligne de changelog ou la metadonnee de decision commitee, son hash est connu et doit
remplacer le placeholder dans un commit de bookkeeping.

Ce script trouve, pour chaque placeholder, le commit qui a introduit la ligne
(recherche pickaxe `git log -S` limitee au fichier), puis:

  --check   liste les placeholders resolvables (dette) et sort en code 1 s'il en reste;
  --apply   remplace les placeholders resolvables par le hash court correspondant.

Un placeholder introduit par un changement non encore commite reste `PENDING`: c'est
son usage legitime.

Decisions: DEC-0050.
Contracts: docs/00_governance/VERSIONING_AND_CHANGELOG_POLICY_V2.md,
docs/00_governance/DOCUMENTATION_STANDARD_V2.md.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DOCS_ROOT = REPO_ROOT / "docs"

# `docs/10_generated/` est reecrit par ses generateurs: toute edition manuelle y est
# signalee comme stale par verify-docs. Ses placeholders appartiennent aux generateurs.
EXCLUDED_DIRS = ("10_generated",)

# | 2026-08-13 | `V2.1.5.0022` | `PENDING` | Changement... |
CHANGELOG_ROW = re.compile(
    r"^\|\s*(?P<date>\d{4}-\d{2}-\d{2})\s*\|\s*(?P<version>`?V2\.[\d.]+`?)\s*\|"
    r"\s*(?P<commit>`?PENDING`?)\s*\|(?P<rest>.*)$"
)

# Created in commit: `PENDING`   /   Deprecated in commit: `PENDING`
DECISION_FIELD = re.compile(r"^(?P<label>(?:Created|Deprecated) in commit):\s*`PENDING`\s*$")

DECISION_HEADING = re.compile(r"^### (DEC-\d{4})")

# Commit: PENDING   (metadonnee d'en-tete de rapport)
HEADER_COMMIT_FIELD = re.compile(r"^Commit:\s*`?PENDING`?\s*$")


def git(*args: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(REPO_ROOT), *args],
        capture_output=True,
        check=False,
    )
    if result.returncode != 0:
        return ""
    return result.stdout.decode("utf-8", errors="replace")


def introducing_commit(needle: str, relative_path: str) -> str | None:
    """Return the short hash of the first commit that added `needle` to the file."""
    needle = needle.strip()
    if len(needle) < 12:
        return None
    out = git("log", "--reverse", "--format=%h", "-S", needle, "--", relative_path)
    hashes = [line.strip() for line in out.splitlines() if line.strip()]
    return hashes[0] if hashes else None


def first_commit_for_file(relative_path: str) -> str | None:
    """Return the short hash of the first commit that touched the file."""
    out = git("log", "--reverse", "--format=%h", "--", relative_path)
    hashes = [line.strip() for line in out.splitlines() if line.strip()]
    return hashes[0] if hashes else None


def anchor_for_row(match: re.Match[str]) -> str:
    """Pickaxe anchor for a changelog row: its description cell, trimmed."""
    rest = match.group("rest").strip().strip("|").strip()
    return rest[:120]


def scan_file(path: Path) -> list[tuple[int, str, str]]:
    """Return (line_index, resolved_hash, replacement_line) for resolvable placeholders."""
    relative = path.relative_to(REPO_ROOT).as_posix()
    lines = path.read_text(encoding="utf-8", newline="").splitlines()
    resolved: list[tuple[int, str, str]] = []
    current_decision: str | None = None

    for index, line in enumerate(lines):
        heading = DECISION_HEADING.match(line)
        if heading:
            current_decision = heading.group(1)

        row = CHANGELOG_ROW.match(line)
        if row:
            anchor = anchor_for_row(row)
            commit = introducing_commit(anchor, relative)
            if commit:
                replacement = line.replace(row.group("commit"), f"`{commit}`", 1)
                resolved.append((index, commit, replacement))
            continue

        if HEADER_COMMIT_FIELD.match(line):
            commit = first_commit_for_file(relative)
            if commit:
                resolved.append((index, commit, f"Commit: `{commit}`"))
            continue

        field = DECISION_FIELD.match(line)
        if field and current_decision:
            commit = introducing_commit(f"### {current_decision} ", relative)
            if commit:
                replacement = f"{field.group('label')}: `{commit}`"
                resolved.append((index, commit, replacement))

    return resolved


def iter_docs() -> list[Path]:
    return sorted(
        p
        for p in DOCS_ROOT.rglob("*.md")
        if p.is_file() and not any(part in EXCLUDED_DIRS for part in p.relative_to(DOCS_ROOT).parts)
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true", help="list stale placeholders, exit 1 if any")
    mode.add_argument("--apply", action="store_true", help="replace stale placeholders in place")
    parser.add_argument("--path", help="limit to one document, repo-relative")
    args = parser.parse_args()

    targets = [REPO_ROOT / args.path] if args.path else iter_docs()

    total = 0
    touched_files = 0
    for path in targets:
        resolved = scan_file(path)
        if not resolved:
            continue
        total += len(resolved)
        touched_files += 1
        relative = path.relative_to(REPO_ROOT).as_posix()

        if args.check:
            for index, commit, _ in resolved:
                print(f"{relative}:{index + 1}: PENDING resolvable -> {commit}")
            continue

        lines = path.read_text(encoding="utf-8", newline="").splitlines(keepends=True)
        for index, _, replacement in resolved:
            ending = "\r\n" if lines[index].endswith("\r\n") else "\n"
            lines[index] = replacement + ending
        path.write_text("".join(lines), encoding="utf-8", newline="")
        print(f"{relative}: {len(resolved)} placeholder(s) resolved")

    verb = "resolvable" if args.check else "resolved"
    print(f"{total} placeholder(s) {verb} across {touched_files} file(s).")

    if args.check and total:
        print("Stale `PENDING` placeholders remain; run with --apply in a bookkeeping commit.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
