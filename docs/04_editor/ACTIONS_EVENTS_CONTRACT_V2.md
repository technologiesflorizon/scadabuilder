# SCADA Builder V2 - Actions Events Contract

Date: 2026-06-17
Status: Active editor/runtime actions contract
Document version: `V2.1.2.0010`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-17 | `V2.1.2.0010` | `PENDING` | Implementation des actions objet `Afficher`, `Masquer`, `Basculer visibilite` avec condition tag deterministe. |
| 2026-06-17 | `V2.1.2.0009` | `PENDING` | Remplacement de l'action authorable `WriteTag` par les bindings Element+ `Lire valeur` et `Ecrire valeur`. |
| 2026-06-17 | `V2.1.2.0008` | `PENDING` | Implementation de l'import tags TF100Web et de l'authoring Element+ `WriteTag`. |
| 2026-06-16 | `V2.1.2.0007` | `PENDING` | Ajout du curseur runtime par defaut pour les cibles `Clic` exportees. |
| 2026-06-16 | `V2.1.2.0006` | `PENDING` | Clarification de l'export FT100 des events `Clic -> Changer de page` portes par des groupes Element+. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Ajout du registre contractuel Element+ events/actions et de la premiere modale Clic -> Changer de page. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du contrat actions/events separe des commandes et du statut d'implementation. |

## 1. Contract

Object events and runtime actions are model-owned behavior. UI controls may author them, but exported runtime behavior must come from scene actions and element event bindings.

## 2. Active Implemented Baseline

1. Object-owned click navigation action exists in the scene model and FT100 manifest output.
2. Page type, dimensions, background, actions, and event bindings persist through project save/reload.
3. The Element+ property/editor surface exposes an `Evenement` entry that opens a modal authoring flow.
4. The first event authoring slice creates `Clic -> Changer de page` by adding a scene action and an Element+ event binding.
5. One Element+ may hold several event bindings, including several `Clic` bindings.
6. `Clic -> Changer de page` bindings authored on Element+ groups are exported as transparent FT100 runtime wrappers so TF100Web can hit-test the group and execute the page navigation action.
7. FT100 export gives `Clic` targets a default pointer cursor in hover and active click states when they are buttons or carry exported `data-scada-events`.
8. The project can import a TF100Web `tf100web-scada-tags-v1` tag catalog. The Element+ event modal exposes enabled tags for value binding authoring.
9. `Lire valeur` and `Ecrire valeur` persist tag ids as Element+ data bindings, not triggered scene events. `Ecrire valeur` writes the operator-entered runtime value and never stores a literal design-time value.
10. `Afficher objet`, `Masquer objet`, and `Basculer visibilite` are authorable against Element+ targets and may use one deterministic tag condition.

## 3. Event Registry

Event trigger contracts are centralized in `ScadaEventRegistry`:

| Editor key | French label | Runtime trigger | Multiple bindings | Conditional contract |
| --- | --- | --- | --- | --- |
| `OnClick` | `Clic` | `click` | Yes | Planned |
| `OnRelease` | `Relachement` | `pointerup` | Yes | Planned |
| `OnHover` | `Survol` | `mouseenter` | Yes | Planned |
| `OnHoverEnter` | `Entree survol` | `mouseenter` | Yes | Planned |
| `OnHoverExit` | `Sortie survol` | `mouseleave` | Yes | Planned |

Runtime function contracts are centralized in `ScadaEventRegistry`:

| Function | French label | Persisted action kind | Required arguments | Status |
| --- | --- | --- | --- | --- |
| `ChangePage` | `Changer de page` | `Navigate` | `TargetPageId` | Implemented |
| `Show` | `Afficher objet` | `Show` | `TargetElementId`, optional `Condition` | Implemented |
| `Hide` | `Masquer objet` | `Hide` | `TargetElementId`, optional `Condition` | Implemented |
| `ToggleVisibility` | `Basculer visibilite` | `ToggleVisibility` | `TargetElementId`, optional `Condition` | Implemented |
| `ReadValue` | `Lire valeur` | `ReadValue` | `TagId` | Implemented |
| `WriteValue` | `Ecrire valeur` | `WriteValue` | `TagId` | Implemented |
| `WriteTag` | `Ecrire tag` | `WriteTag` | `TagId`, `Value` | Legacy compatibility, not authorable |

## 4. Authoring Flow

```mermaid
flowchart TD
  DoubleClick[Double-click Element+ or group] --> Editor[Element+ editor]
  Editor --> EventButton[Evenement button]
  PropertyPanel[Right Propriete panel] --> EventTab[Evenement tab]
  EventButton --> Modal[Event modal]
  EventTab --> Modal
  Modal --> Registry[ScadaEventRegistry]
  Modal --> Tags[Project tag catalog]
  Registry --> Scene[Scene action catalog]
  Registry --> Binding[Element+ event binding]
  Registry --> ValueBinding[Element+ value binding]
  Tags --> Scene
  Tags --> ValueBinding
  Scene --> History[Scene history]
  Binding --> History
  ValueBinding --> History
  History --> Save[Project save/reload]
  Save --> Export[FT100/TF100Web export]
  Export --> Runtime[Runtime JS action bridge]
```

## 5. Conditional Events Plan

Conditional execution is implemented for object visibility actions only.

Implemented condition fields:

1. Imported tag id.
2. Operator: `Vrai`, `Faux`, `=`, `<>`, `>`, `>=`, `<`, `<=`.
3. Comparison value for non-boolean operators.

Boolean `Vrai/Faux` operators are valid only for boolean tags. Missing target objects, missing condition tags, boolean operators on non-boolean tags, and missing comparison values are build/export errors.

## 6. Tag Authoring Boundary

The current implemented tag slice covers:

1. Importing the TF100Web tag export into the V2 project model.
2. Selecting any enabled tag in the Element+ event modal with labels formatted as `Nom du tag | datatype | Nom de l'appareil`.
3. Creating `Lire valeur` bindings with no trigger.
4. Creating `Ecrire valeur` bindings with no trigger and no design-time value field; the runtime operator input supplies the value.
5. Validating `Ecrire valeur` during build/export so read-only Element+ objects, non-input Element+ objects, read-only tags, and missing tags are rejected.
6. Exporting tags and per-element value binding metadata in the FT100/TF100Web package.

The current slice does not yet implement degraded state semantics, expression authoring, compound conditions, local tag creation, or project protocol import. Local tag creation requires a future protocol import revision.

## 7. Roadmap Boundary

The following are roadmap items until implemented and covered by tests:

1. `On click -> open popup`.
2. `mouse hover -> show element/group border`.
3. Degraded tag conditions and compound conditions.
4. Global scripts generating lifecycle events.
5. Visual effects such as blink, glow, pulse, alarm highlight, degraded treatment.

## 8. Event Flow

```mermaid
flowchart TD
  Inspector[Property or action UI] --> Scene[Scene action catalog]
  Inspector --> Element[Element event binding]
  Scene --> Save[Project save/reload]
  Element --> Save
  Scene --> Export[Manifest export]
  Element --> Export
  Export --> Runtime[Runtime JS action bridge]
```

## 9. Related Tests

1. `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
2. `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
3. `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`
4. `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
