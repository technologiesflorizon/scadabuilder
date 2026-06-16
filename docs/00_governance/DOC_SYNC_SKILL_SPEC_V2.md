# SCADA Builder V2 - Doc Sync Skill Specification

Date: 2026-06-16
Status: Active skill specification
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Specification du skill `scada-v2-doc-sync` et de la validation documentaire automatisee. |

## 1. Purpose

The `scada-v2-doc-sync` skill verifies and synchronizes documentation with code, tests, decisions, and Mermaid diagrams.

It must be used when:

1. A document under `docs/` is created, moved, restructured, or deprecated.
2. A decision or contract changes.
3. Public APIs or contract-sensitive functions change.
4. Command, state, action, menu, selection, export, Studio Element+, or `.sep` behavior changes.
5. Generated documentation under `docs/10_generated` must be refreshed.

## 2. Required Checks

The skill must:

1. Read `docs/README.md`.
2. Read `docs/AGENTS.md`.
3. Read `docs/00_governance/DECISION_REGISTER_V2.md`.
4. Run or recommend `tools/docs/verify-docs.ps1`.
5. Verify mandatory Markdown headers.
6. Verify decision references and decision statuses.
7. Verify Mermaid blocks in required owner documents.
8. Verify public C# XML documentation coverage.
9. Verify generated documentation freshness signals.
10. Report known gaps instead of silently rewriting them as complete.

## 3. Output

The skill should report:

1. Updated files.
2. Decisions created, deprecated, or superseded.
3. Version bump.
4. Validation command results.
5. Code documentation gaps.
6. Open implementation gaps.

## 4. Local Skill Location

The local Codex skill is installed at:

```text
C:\Users\mathi\.codex\skills\scada-v2-doc-sync
```
