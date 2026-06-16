# SCADA Builder V2 - Team Workflow

Date: 2026-06-16
Status: Active team workflow
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du workflow equipe code, documentation, decisions, tests et validation. |

## 1. Standard Work Cycle

Every meaningful change follows:

```text
Code or documentation change
  -> owner document identified
  -> DEC entry added or updated if a contract changes
  -> code XML documentation updated when APIs change
  -> Mermaid diagram updated when flow changes
  -> implementation status or known gap updated
  -> verify-docs.ps1
  -> dotnet test when implemented behavior changes
```

## 2. Review Gates

Reviewers should reject changes when:

1. Active behavior is documented in a historical file only.
2. A decision is replaced by deleting the previous decision.
3. A contract changes without a `DEC-xxxx` entry.
4. A diagram contradicts the owner document.
5. Public APIs are added without XML documentation.
6. Tests are cited but missing.
7. Future work is described as implemented behavior.

## 3. Team Ownership

Owners are documents, not people:

1. Runtime export behavior belongs to `03_runtime_contracts`.
2. Editor behavior belongs to `04_editor`.
3. Studio Element+ behavior belongs to `05_studio_element_plus`.
4. Legacy source selection belongs to `07_legacy_migration`.
5. Implemented status and test coverage belong to `08_implementation_status`.

## 4. Migration Policy

Legacy top-level documents are migration sources. During the transition, do not delete them unless a migration note, archive entry, and owner-document replacement already exist.
