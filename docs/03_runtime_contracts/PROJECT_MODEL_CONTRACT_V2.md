# SCADA Builder V2 - Project Model Contract

Date: 2026-06-16
Status: Active project model contract
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du contrat actif du modele projet et scene. |

## 1. Contract

The V2 project model is the source of truth for preview, save/reload, Studio exchange, and FT100/TF100Web export.

Legacy source paths and source ids are trace metadata unless explicitly converted or approved by a sanitized-source decision.

## 2. Ownership

1. Project owns identity, scene inventory, home page, and build composition.
2. Scene owns canvas, page type, background, elements, actions, composition references, and removed source ids.
3. Elements own identity, kind, bounds, data, event bindings, and optional tag binding.
4. Runtime manifests are generated outputs, not editable source models.

## 3. Related Tests

1. `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
2. `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`
