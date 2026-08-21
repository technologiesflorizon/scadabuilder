#!/usr/bin/env python3
"""Backfill the mandatory documentation header on pre-standard documents.

`tools/docs/verify-docs.ps1` exige sur chaque document Markdown de `docs/`:
H1, `Date:`, `Status:`, `Document version:` et une section
`## Historique des changements` avec son tableau. Les plans et specs ecrits avant
l'adoption du standard ne les possedent pas et produisent une erreur permanente,
ce qui transforme le gate documentaire en bruit de fond.

Ce script retro-remplit ces metadonnees a partir de faits verifiables:

  Date               : prefixe `YYYY-MM-DD` du nom de fichier, sinon date du premier
                       commit qui a ajoute le document;
  Document version   : contenu de `VERSION` au commit qui a ajoute le document;
  Historique         : une ligne pointant sur ce meme commit;
  Status             : `Historical` explicite, sans revendiquer d'etat de livraison.

Rien n'est invente: aucune version, date ou hash n'est fabrique. Un document dont
le commit introducteur est introuvable est signale et laisse intact.

  --check   liste les documents non conformes et sort en code 1 s'il en reste;
  --apply   ecrit les en-tetes manquants.

Contracts: docs/00_governance/DOCUMENTATION_STANDARD_V2.md,
docs/00_governance/VERSIONING_AND_CHANGELOG_POLICY_V2.md.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DOCS_ROOT = REPO_ROOT / "docs"
EXCLUDED_DIRS = ("10_generated",)

FILENAME_DATE = re.compile(r"^(\d{4}-\d{2}-\d{2})-")
VERSION_VALUE = re.compile(r"`?(V2\.\d+\.\d+\.\d+)`?")
METADATA_LINE = re.compile(r"^[A-Za-zÀ-ÿ][^:\n]{1,40}:\s")
HISTORY_HEADING = "## Historique des changements"
HISTORY_TABLE = "| Date | Version | Commit | Changement |"

STATUS_BY_KIND = {
    "plans": "Historical - plan anterieur au standard documentaire; conserve pour tracabilite",
    "specs": "Historical - specification anterieure au standard documentaire; conservee pour tracabilite",
    "reports": "Historical - rapport anterieur au standard documentaire; conserve pour tracabilite",
}
DEFAULT_STATUS = "Historical - document anterieur au standard documentaire; conserve pour tracabilite"

BACKFILL_NOTE = (
    "Ajout initial du document, anterieur au standard documentaire; "
    "en-tete et historique retro-remplis depuis le commit d'origine."
)


def git(*args: str) -> str:
    result = subprocess.run(["git", "-C", str(REPO_ROOT), *args], capture_output=True, check=False)
    if result.returncode != 0:
        return ""
    return result.stdout.decode("utf-8", errors="replace")


def origin_commit(relative: str) -> tuple[str, str] | None:
    """Return (short hash, ISO date) of the first commit that added the document."""
    out = git("log", "--reverse", "--format=%h|%ad", "--date=short", "--", relative)
    for line in out.splitlines():
        if "|" in line:
            commit, date = line.strip().split("|", 1)
            return commit, date
    return None


def version_at(commit: str) -> str | None:
    text = git("show", f"{commit}:VERSION").strip()
    match = VERSION_VALUE.search(text)
    return match.group(1) if match else None


def kind_of(path: Path) -> str:
    parts = path.relative_to(DOCS_ROOT).parts
    return parts[1] if len(parts) > 2 and parts[0] == "superpowers" else parts[0]


def find_metadata_block(lines: list[str]) -> tuple[int, int]:
    """Return (start, end) indexes of the metadata block right after the H1."""
    index = 1
    while index < len(lines) and not lines[index].strip():
        index += 1
    start = index
    while index < len(lines) and METADATA_LINE.match(lines[index]):
        index += 1
    return start, index


def split_lines(text: str) -> tuple[list[str], list[str], str]:
    """Split into (content lines, line endings, dominant ending), tolerating mixed endings."""
    raw = text.splitlines(keepends=True)
    content: list[str] = []
    endings: list[str] = []
    for line in raw:
        stripped = line.rstrip("\r\n")
        content.append(stripped)
        endings.append(line[len(stripped):])
    dominant = "\r\n" if endings.count("\r\n") >= endings.count("\n") else "\n"
    return content, endings, dominant


def rewrite(path: Path) -> tuple[str, list[str]] | None:
    """Return (new text, applied fixes) or None when the document already conforms."""
    relative = path.relative_to(REPO_ROOT).as_posix()
    text = path.read_text(encoding="utf-8", newline="")
    lines, endings, newline = split_lines(text)
    fixes: list[str] = []

    if not lines or not lines[0].startswith("# "):
        return None

    def insert(index: int, values: list[str]) -> None:
        lines[index:index] = values
        endings[index:index] = [newline] * len(values)

    origin = origin_commit(relative)
    if origin is None:
        print(f"{relative}: no origin commit found, left untouched")
        return None
    commit, commit_date = origin

    # Normalise the French label used before the standard.
    for index, line in enumerate(lines):
        if line.startswith("Version du document:"):
            lines[index] = line.replace("Version du document:", "Document version:", 1)
            fixes.append("renamed `Version du document:` to `Document version:`")
            break

    def has(prefix: str) -> bool:
        return any(line.startswith(prefix) for line in lines)

    document_version = None
    for line in lines:
        if line.startswith("Document version:"):
            match = VERSION_VALUE.search(line)
            document_version = match.group(1) if match else None
            break

    additions: list[str] = []
    if not has("Date:"):
        match = FILENAME_DATE.match(path.name)
        additions.append(f"Date: {match.group(1) if match else commit_date}")
        fixes.append("added Date")
    if not has("Status:"):
        additions.append(f"Status: {STATUS_BY_KIND.get(kind_of(path), DEFAULT_STATUS)}")
        fixes.append("added Status")
    if document_version is None:
        document_version = version_at(commit)
        if document_version is None:
            print(f"{relative}: VERSION unreadable at {commit}, left untouched")
            return None
        additions.append(f"Document version: `{document_version}`")
        fixes.append("added Document version")

    if additions:
        _, end = find_metadata_block(lines)
        insert(end, additions)
        after = end + len(additions)
        if after < len(lines) and lines[after].strip():
            insert(after, [""])

    if HISTORY_HEADING not in lines or HISTORY_TABLE not in lines:
        _, end = find_metadata_block(lines)
        while end < len(lines) and not lines[end].strip():
            lines.pop(end)
            endings.pop(end)
        insert(end, [
            "",
            HISTORY_HEADING,
            "",
            HISTORY_TABLE,
            "| --- | --- | --- | --- |",
            f"| {commit_date} | `{document_version}` | `{commit}` | {BACKFILL_NOTE} |",
            "",
        ])
        fixes.append("added change history section")

    if not fixes:
        return None
    return "".join(line + ending for line, ending in zip(lines, endings)), fixes


def iter_docs() -> list[Path]:
    return sorted(
        p
        for p in DOCS_ROOT.rglob("*.md")
        if p.is_file() and not any(part in EXCLUDED_DIRS for part in p.relative_to(DOCS_ROOT).parts)
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true", help="list non conforming documents, exit 1")
    mode.add_argument("--apply", action="store_true", help="write the missing headers")
    parser.add_argument("--path", help="limit to one document, repo-relative")
    args = parser.parse_args()

    targets = [REPO_ROOT / args.path] if args.path else iter_docs()

    total = 0
    for path in targets:
        result = rewrite(path)
        if result is None:
            continue
        total += 1
        new_text, fixes = result
        relative = path.relative_to(REPO_ROOT).as_posix()
        if not args.check:
            path.write_text(new_text, encoding="utf-8", newline="")
        print(f"{relative}: {', '.join(fixes)}")

    verb = "non conforming" if args.check else "updated"
    print(f"{total} document(s) {verb}.")
    return 1 if (args.check and total) else 0


if __name__ == "__main__":
    sys.exit(main())
