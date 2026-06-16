# SCADA Builder V2 - Versioning Policy

Date: 2026-06-15
Status: Active policy
Document version: `V2.1.1.0036`
Current version: `V2.1.1.0036`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0036` | `PENDING` | Bump iteration pour namespace CSS/DOM/runtime generalise dans l'export FT100/TF100Web. |
| 2026-06-15 | `V2.1.1.0035` | `PENDING` | Bump iteration pour correction scoping CSS FT100/TF100Web. |
| 2026-06-15 | `V2.1.1.0034` | `PENDING` | Bump iteration pour contrat selection polymorphe et correction suppression globale source/objet. |
| 2026-06-15 | `V2.1.1.0033` | `PENDING` | Bump iteration pour correction selection SVG source et durcissement export footer FT100. |
| 2026-06-15 | `V2.1.1.0032` | `PENDING` | Bump iteration pour geometrie inline source legacy dans l'export FT100. |
| 2026-06-15 | `V2.1.1.0031` | `PENDING` | Bump iteration pour durcissement composition header/footer FT100Web et regression d'export. |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Bump iteration pour gouvernance documentaire et headers obligatoires. |
| 2026-06-15 | `V2.1.1.0029` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Format

SCADA Builder V2 uses:

```text
V2.<production>.<feature>.<iteration>
```

Example:

```text
V2.0.0.0001
```

## 2. Fields

1. `V2`
   - Product generation.
   - Fixed for SCADA Builder V2.

2. `<production>`
   - Production readiness line.
   - Starts at `0`.
   - Increment when a production-preview, pilot, release candidate, or production line is declared.

3. `<feature>`
   - Major functional capability line.
   - Increment when a major feature is deployed and will need many follow-up iterations.
   - Examples: adaptive layout engine, FT100 mapping import, scripting/transpilation, module host.

4. `<iteration>`
   - Four-digit normal iteration counter.
   - Increment for normal development, small features, bug fixes, documentation, tests, UI polish, and minor refactors.

## 3. Increment Rules

## 3.1 Iteration

Use for:

1. Small functionality.
2. Debugging.
3. Documentation.
4. UI polish.
5. Tests.
6. Minor non-breaking changes.

Example:

```text
V2.0.0.0001 -> V2.0.0.0002
```

## 3.2 Feature

Use when a major functional capability is introduced.

Example:

```text
V2.0.0.0042 -> V2.0.1.0000
```

## 3.3 Production

Use when a production-level preview or release line is approved.

Example:

```text
V2.0.8.0141 -> V2.1.0.0000
```

## 4. Skill

The Codex skill `scada-builder-v2-versioning` was created to apply this policy consistently.

Location:

```text
C:\Users\mathi\.codex\skills\scada-builder-v2-versioning
```

Helper script:

```powershell
python C:\Users\mathi\.codex\skills\scada-builder-v2-versioning\scripts\bump_scada_v2_version.py V2.0.0.0001 iteration
```

## 5. Governance

1. Normal work defaults to an iteration bump.
2. Feature bump requires an explicit major capability decision.
3. Production bump requires orchestrator approval.
4. Every release note must state previous version, new version, bump type, reason, and affected modules.
