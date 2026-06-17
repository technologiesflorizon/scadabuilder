# SCADA Builder V2 - Module Function Index

Date: 2026-06-17
Status: Generated baseline; XML documentation gaps expected during migration
Document version: `V2.1.2.0019`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-17 | `V2.1.2.0019` | `bd6515e` | Ajout manuel des nouvelles API publiques d'export `.sb2` et validation FT100. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation de l'index initial des fonctions/modules pour la verification documentaire. |

## 1. Purpose

This file is the generated or verified index of public APIs and documentation coverage.

The current baseline is intentionally lightweight. `tools/docs/verify-docs.ps1` reports public C# members that still need XML documentation.

## 2. Required Future Fields

1. Module.
2. Type.
3. Member.
4. XML documentation present.
5. Related decision ids.
6. Related owner document.
7. Related tests.

## 3. Current Manual Additions

| Module | Type | Member | Contract |
| --- | --- | --- | --- |
| `ScadaBuilderV2.Rendering` | `Ft100SceneExporter` | `ExportProjectArchiveAsync` | Exports a validated `.sb2` archive for FT100 upload. |
| `ScadaBuilderV2.Rendering` | `Ft100PackageValidator` | `ValidatePackageDirectory` | Validates TF100Web intake shape and page namespace/collision rules before `.sb2` archive creation. |
