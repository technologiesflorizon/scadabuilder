# SCADA Builder V2 - Versioning And Changelog Policy

Date: 2026-06-16
Status: Active governance policy
Document version: `V2.1.4.0027`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0027` | `32a3ef6` | Bump d'itération pour la clôture automatisée des outils Tableau avancés et leur couverture. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation de la politique de versioning documentaire et changelog pour la nouvelle architecture documentaire. |

## 1. Version Format

SCADA Builder V2 uses:

```text
V2.production.feature.iteration
```

Current version:

```text
V2.1.4.0027
```

## 2. Increment Rules

1. Iteration bump: documentation reorganization, small features, tests, UI polish, narrow bug fixes, and contract clarifications.
2. Feature bump: new module-level capability, new runtime contract family, or major workflow addition.
3. Production bump: production baseline change or breaking governance reset.

## 3. Changelog Rules

Every touched Markdown document must record:

1. Date.
2. Version.
3. Commit.
4. Change.

Use `PENDING` for uncommitted changes. Replace `PENDING` in a follow-up bookkeeping commit or release note.

## 4. Decision Linkage

Any changelog entry that modifies a contract should reference a decision id in the body of the document or in `DECISION_REGISTER_V2.md`.
