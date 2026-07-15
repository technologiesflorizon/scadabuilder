# SCADA Builder V2 - Project Model Contract

Date: 2026-07-14
Status: Active project model contract
Document version: `V2.1.4.0027`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0027` | `88e865a` | Validation end-to-end d'une table 16 x 10 avec contenus mixtes, deux en-têtes, fusion, styles par portée, pistes non uniformes, bordures physiques et `IsLocked`, sans modifier le schéma `.sb2`. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Extension Tableau par retour a la ligne, hauteur typographique et bordures physiques; ajout de `ScadaElement.IsLocked` comme metadata d'authoring. |
| 2026-07-14 | `V2.1.4.0016` | `10cfa72` | Ajout du contrat persistant `ScadaElementKind.Table`, pistes, cellules, fusions, contenus input et styles heritables. |
| 2026-07-14 | `V2.1.4.0012` | `PENDING` | Contrat `PageKey`/`PageCode`, provenance Wonderware, pages natives et sauvegarde atomique désormais implémenté. |
| 2026-07-14 | `V2.1.4.0011` | `4def659` | Ajout de la cible approuvée `PageKey`/`PageCode`, provenance importée et migration compatible, avant implémentation. |
| 2026-06-17 | `V2.1.2.0024` | `PENDING` | Clarification du role actif de `DisplayFormat` et de la deprecation authoring de `TagBinding`, `Decimals` et `Unit`. |
| 2026-06-17 | `V2.1.2.0010` | `PENDING` | Ajout du modele `ScadaActionCondition` pour actions objet conditionnelles. |
| 2026-06-17 | `V2.1.2.0009` | `PENDING` | Ajout des bindings Element+ `ReadTagId` et `WriteTagId` et des validations de build. |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Ajout du catalogue tags TF100Web importe au modele projet. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du contrat actif du modele projet et scene. |

## 1. Contract

The V2 project model is the source of truth for preview, save/reload, Studio exchange, and FT100/TF100Web export.

Legacy source paths and source ids are trace metadata unless explicitly converted or approved by a sanitized-source decision.

## 2. Ownership

1. Project owns identity, scene inventory, home page, and build composition.
2. Project owns the optional imported TF100Web tag catalog used by Element+ authoring surfaces.
3. Scene owns canvas, page type, background, elements, actions, composition references, and removed source ids.
4. Elements own identity, kind, bounds, data, event bindings, and optional read/write tag bindings.
5. Actions may own one optional tag condition when the runtime function supports deterministic conditional execution.
6. Runtime manifests are generated outputs, not editable source models.

### 2.1 Element+ Tableau

Un tableau est un seul `ScadaElement` de kind `Table` dont `Table` contient les colonnes, rangees, ancres de cellules, spans, contenus et styles. Les pistes sont limitees a 1..64 par axe; les dimensions minimales sont 24 px par colonne et 20 px par rangee. Les anciennes scenes sans champ `Table` restent lisibles.

Une cellule contient du texte statique, un `InputText` ou un `InputNumeric`. Ces inputs sont des valeurs locales simples : aucun `ValueBinding` cellule par cellule n'est cree. Les styles nullable signifient `Heriter`; la resolution se fait propriete par propriete selon `cellule > rangee explicite > bande > colonne > tableau > defaut`.

`ScadaTableFormat` porte aussi `TextWrap` et `LineHeight`. `BorderOverrides` persiste des segments horizontaux/verticaux unitaires; les presets UI ne sont jamais serialises. Plusieurs rangees initiales consecutives peuvent porter `IsHeader`.

`ScadaElement.IsLocked` est une metadata persistante de position. Une scene historique sans cle charge `false`. Cette metadata n'est ni une propriete runtime, ni une geometrie `.sb2`/`.sep`.

Element numeric data keeps compatibility fields for older projects, but active authoring uses:

1. `DisplayFormat` as the single display-format signal exported to TF100Web.
2. `Minimum` and `Maximum` only as clamp constraints for non-read-only numeric inputs.
3. `ReadTagId` and `WriteTagId` for runtime value bindings.

`TagBinding`, `Decimals`, and `Unit` are legacy model fields. They may be preserved by save/reload, but they are not active Element+ authoring controls.

## 3. Tag Catalog

Imported tags are stored as `ScadaProject.TagCatalog` with schema `tf100web-scada-tags-v1`. The catalog is project-level data, not scene-level geometry.

The catalog preserves:

1. Stable tag id.
2. Display label for authoring UI.
3. TF100Web source metadata such as keyword label, device, protocol, address, datatype, unit, enabled state, and writeable state.

All enabled tags are exposed for `Lire valeur` authoring. `Ecrire valeur` may target only writeable tags and only editable input Element+ objects. Build/export validation rejects missing tag references, write bindings on non-input or read-only Element+ objects, and write bindings to read-only tags.

## 4. Implemented Modern Page Model

`DEC-0038` is implemented. Every page owns an immutable internal `PageKey`, a visible mutable `PageCode`, optional import provenance, and canonical internal home/composition/action references by key. Existing id fields remain readable during idempotent migration. The `.sb2` boundary resolves keys back to human page codes and never emits GUIDs.

`ScadaProject.Scenes` remains authoritative for page inventory and metadata. Native pages do not require imported HTML; imported Wonderware projections remain optional provenance-backed inputs. A new `Default` page starts with `IncludeInBuild = false`.

## 5. Related Tests

1. `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
2. `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`
3. `tests/ScadaBuilderV2.Tests/PageIdentityTests.cs`
4. `tests/ScadaBuilderV2.Tests/ModernProjectAtomicSnapshotTests.cs`
5. `tests/ScadaBuilderV2.Tests/PageLifecycleIntegrationTests.cs`
