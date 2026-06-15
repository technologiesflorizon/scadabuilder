# SCADA Builder V2 - Reference Project

Date: 2026-06-15
Status: Legacy sample reference
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `PENDING` | Ajout du header documentaire obligatoire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.0.0.0000` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Reference Project

The current reference project is:

```text
F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER\AMR_SCADA\AMR_REF_SCADA
```

This project is a legacy sample and regression corpus for SCADA Builder V2.

It is not the official V2 project domain and must not define the target architecture.

It will be used to validate:

1. Project opening.
2. Scene loading.
3. Legacy Wonderware/ArchestrA migration workflows.
4. UI element inventory and classification.
5. Element selection and properties.
6. Responsive layout strategy.
7. Preview/build parity.
8. Future FT100 mapping integration.
9. Legacy Viewer comparison workflows.
10. Extraction of selected candidates into official V2 scenes.

## 2. Backup

A backup copy was created before using the project as the V2 reference:

```text
F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\references\backups\AMR_REF_SCADA_backup_20260529_114507
```

The original project must not be modified destructively without a new backup or explicit orchestrator approval.

## 3. Governance

1. Treat `AMR_REF_SCADA` as read-only legacy source material.
2. Use copies or generated working directories for destructive experiments.
3. Preserve legacy traceability.
4. Document every migration assumption learned from this project.
5. If the project structure changes, update this document and the relevant model/spec files.
6. Do not copy the AMR project structure into the official V2 domain without deliberate conversion.
7. Build/export pipelines must target the V2 model, not the AMR legacy HTML output.

## 4. Initial Priority

The first V2 implementation should prove the software can:

1. Load this project.
2. Read its project/page data.
3. Display a representative legacy scene in the Legacy Viewer.
4. Inspect legacy elements as extraction candidates.
5. Convert selected candidates into official V2 scene elements.
6. Compare the legacy scene with the V2 scene.
7. Preserve the relation between legacy source and modern UI elements.
