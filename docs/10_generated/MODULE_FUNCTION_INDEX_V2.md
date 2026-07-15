# SCADA Builder V2 - Module Function Index

Date: 2026-07-15
Status: Generated baseline; XML documentation gaps expected during migration
Document version: `V2.1.4.0027`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0027` | `88e865a` | Ajout de `TablePropertiesInspector` et des nouvelles requêtes de format/propriétés au suivi d'API `DEC-0040`. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Index manuel des API publiques de lock et d'authoring Tableau `DEC-0040`. |
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
| `ScadaBuilderV2.Domain` | `ScadaSceneElementLockOperations` | `ExpandSelectionClosure`, `ResolveEffectiveLock`, `ApplyRecursive` | Resolves and mutates persistent position-lock closures. |
| `ScadaBuilderV2.Domain` | `ScadaTableContentOperations` | `Convert`, `ConvertKind` | Applies the approved cell-content conversion matrix. |
| `ScadaBuilderV2.Domain` | `ScadaTableFormatOperations` | `ApplyFormat`, `ResetProperty`, `ResetScope` | Applies nullable format overrides by explicit scope. |
| `ScadaBuilderV2.Domain` | `ScadaTableBorderOperations` | `ApplyPreset`, `Validate` | Expands UI presets to physical border segments. |
| `ScadaBuilderV2.Domain` | `ScadaTableTrackOperations` | `Equalize*`, `Distribute*`, `ApplySizes` | Applies validated track dimensions and auto-fit results. |
| `ScadaBuilderV2.Application` | `TableAuthoringSession` | session transitions | Owns contextual Table UI state without scene elements. |
| `ScadaBuilderV2.Application` | `TablePropertiesInspector` | `Inspect` | Computes effective/local inherited, custom and mixed format state by scope. |
| `ScadaBuilderV2.Application` | `TableEditCoordinator` | `Apply` | Applies typed format reset, exact dimensions, track, header and border requests atomically. |
| `ScadaBuilderV2.Application` | `ElementTransformGuard` | `CanApply` | Rejects effective X/Y changes for locked closures. |
