# SCADA Builder V2 - Actions Events Contract

Date: 2026-08-13
Status: Active editor/runtime actions contract
Document version: `V2.1.5.0021`

> **DEPRECATED (2026-07-07):** `SetClass`/`RemoveClass`/`ToggleClass`/`WriteTag` (legacy)
> action kinds and the border/visual-effect authoring described in §3, §8, §9 have been
> removed from the domain model. Element+ display-state and command authoring is now
> specified in `docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md`
> and `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md`. `Navigate`,
> `Show`/`Hide`/`ToggleVisibility` and `ReadValue`/`WriteValue` remain valid until fully
> absorbed by the new Etat/Commande tabs. `MountFragment`/`ClosePopup`/`TogglePopup`
> and `ScadaPopupOptions` are phased-out implementation residues superseded by
> `DEC-0050`; they are not the contract of the new Fenêtre rapide module.

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-13 | `V2.1.5.0021` | `b353e37` | Les commandes QuickWindow de Phase 1 restent inertes et sont exclues du dialogue de commande de page jusqu’à la surface dédiée de Phase 3. |
| 2026-08-10 | `V2.1.5.0017` | `fc5b333` | `DEC-0050` supersède le contrat popup Fragment de `DEC-0019`, `DEC-0020` et `DEC-0022`; les actions et options restantes deviennent des résidus de décommissionnement, sans migration vers les Fenêtres rapides. |
| 2026-07-16 | `V2.1.4.0053` | `bcec075` | `DEC-0047` : les 9 actions objet utilisent ActionDispatcher, conditions partagees, ordre/propagation et page scope. |
| 2026-07-16 | `V2.1.4.0043` | `8489dbd` | `DEC-0044` applique le modele Etat/Commande qui remplace les anciennes actions visuelles : 56 boutons Toggle, filtres PLC et texte dynamique via cible semantique partagee. |
| 2026-06-17 | `V2.1.2.0022` | `3b67c3a` | Clarification que `Lire valeur` et `Ecrire valeur` sont des events de binding runtime sans trigger utilisateur. |
| 2026-06-17 | `V2.1.2.0017` | `789a433` | Implementation des effets visuels runtime standards. |
| 2026-06-17 | `V2.1.2.0017` | `b465ba9` | Ajout du bridge lifecycle runtime global exporte. |
| 2026-06-17 | `V2.1.2.0017` | `1b5df61` | Implementation des groupes de conditions runtime `All/Any` et politique de tag manquant. |
| 2026-06-17 | `V2.1.2.0017` | `95af4bb` | Implementation des options runtime avancees pour popup Fragment. |
| 2026-06-17 | `V2.1.2.0016` | `32d9227` | Implementation des actions runtime `Afficher bordure`, `Masquer bordure` et `Basculer bordure`. |
| 2026-06-17 | `V2.1.2.0015` | `6ac2245` | Implementation de `Fermer popup` et `Basculer popup` vers fragments compiles. |
| 2026-06-17 | `V2.1.2.0014` | `06652c6` | Implementation de `Ouvrir popup` vers fragments compiles. |
| 2026-06-17 | `V2.1.2.0012` | `a73be05` | Clarification de l'application runtime des valeurs recues par `Lire valeur`. |
| 2026-06-17 | `V2.1.2.0010` | `5302022` | Implementation des actions objet `Afficher`, `Masquer`, `Basculer visibilite` avec condition tag deterministe. |
| 2026-06-17 | `V2.1.2.0009` | `7e3610c` | Remplacement de l'action authorable `WriteTag` par les bindings Element+ `Lire valeur` et `Ecrire valeur`. |
| 2026-06-17 | `V2.1.2.0008` | `f78e8cd` | Implementation de l'import tags TF100Web et de l'authoring Element+ `WriteTag`. |
| 2026-06-16 | `V2.1.2.0007` | `5c7d617` | Ajout du curseur runtime par defaut pour les cibles `Clic` exportees. |
| 2026-06-16 | `V2.1.2.0006` | `5c7d617` | Clarification de l'export FT100 des events `Clic -> Changer de page` portes par des groupes Element+. |
| 2026-06-16 | `V2.1.2.0004` | `5c7d617` | Ajout du registre contractuel Element+ events/actions et de la premiere modale Clic -> Changer de page. |
| 2026-06-16 | `V2.1.1.0039` | `2c5a0b4` | Creation du contrat actions/events separe des commandes et du statut d'implementation. |

## 1. Contract

Object events, binding events, and runtime actions are model-owned behavior. UI controls may author them, but exported runtime behavior must come from scene actions, Element+ event bindings, or Element+ value binding events.

SCADA Builder V2 treats every runtime function as an event family. Triggered events use browser triggers such as `click`; binding events such as `Lire valeur` and `Ecrire valeur` synchronize runtime tag values and do not require a user-trigger selector.

## 2. Active Implemented Baseline

Le baseline ci-dessous decrit le contrat historique encore valide pour ses familles non absorbees. Pour l'authoring courant d'etat et de commande, `ScadaElementStateConfig` et `ScadaElementCommandConfig` sont proprietaires selon `STATE_COMMAND_RUNTIME_CONTRACT_V1.md`.

1. Object-owned click navigation action exists in the scene model and FT100 manifest output.
2. Page type, dimensions, background, actions, and event bindings persist through project save/reload.
3. The Element+ property/editor surface exposes an `Evenement` entry that opens a modal authoring flow.
4. The first event authoring slice creates `Clic -> Changer de page` by adding a scene action and an Element+ event binding.
5. One Element+ may hold several event bindings, including several `Clic` bindings.
6. Object-action bindings authored on Element+ groups are exported as transparent FT100 runtime wrappers so the shared runtime can hit-test the group and execute the ordered action bindings.
7. FT100 export gives action targets a default pointer cursor in hover and active click states when they are buttons or carry `data-scada-action-bindings`. `data-scada-events` remains decommissioned.
8. The project can import a TF100Web `tf100web-scada-tags-v1` tag catalog. The Element+ event modal exposes enabled tags for value binding authoring.
9. `Lire valeur` and `Ecrire valeur` are binding events. They persist tag ids as Element+ data bindings instead of triggered scene actions. `Ecrire valeur` writes the operator-entered runtime value and never stores a literal design-time value.
10. The nine current object-action kinds may use one deterministic tag condition and/or one compound condition group; conditions are evaluated by the shared runtime before execution.
11. Exported runtime applies values pushed by TF100Web to every Element+ using the matching `Lire valeur` tag binding.
12. Le code historique peut encore contenir `MountFragment`, `ClosePopup`, `TogglePopup` et `ScadaPopupOptions`; `DEC-0050` les classe comme résidus phased-out à retirer explicitement. Ils ne constituent plus une surface produit approuvée, ne ciblent jamais une `QuickWindowDefinition` et ne prouvent aucune capacité Fenêtre rapide.
13. Legacy border/class actions are deprecated and removed from the active domain. Model-backed `StateConfig` owns visual effects.
15. Model-backed display states are evaluated continuously by the shared runtime and may combine color-filter effects with `TextContent`; generated text and button labels expose the same `[data-scada-text]` target.
16. Model-backed commands execute through the shared `CommandDispatcher`. Toggle reads `ReadTagId` (or `WriteTagId`) from the shared TF100Web snapshot and writes through the existing bridge; appearance follows the confirmed subsequent snapshot.
17. `OpenQuickWindow` et `CloseQuickWindow` existent comme contrats persistants de Phase 1, mais ne sont pas proposés par le dialogue de commande général. `CloseQuickWindow` est valide uniquement dans le contenu d’une définition et cible implicitement `Self`. La surface d’authoring dédiée appartient à la Phase 3 et toutes les capacités associées restent `Blocked`.

## 3. Event Registry

Event trigger contracts are centralized in `ScadaEventRegistry`:

| Editor key | French label | Runtime trigger | Multiple bindings | Conditional contract |
| --- | --- | --- | --- | --- |
| `OnClick` | `Clic` | `click` | Yes | Implemented |
| `OnRelease` | `Relachement` | `pointerup` | Yes | Implemented |
| `OnHover` | `Survol` | `mouseenter` | Yes | Implemented |
| `OnHoverEnter` | `Entree survol` | `mouseenter` | Yes | Implemented |
| `OnHoverExit` | `Sortie survol` | `mouseleave` | Yes | Implemented |

Runtime function contracts are centralized in `ScadaEventRegistry`:

| Function | French label | Persisted action kind | Required arguments | Status |
| --- | --- | --- | --- | --- |
| `ChangePage` | `Changer de page` | `Navigate` | `TargetPageId` | Implemented |
| `OpenPopup` | `Ouvrir popup` | `MountFragment` | `TargetPageId` fragment | Superseded by `DEC-0050`; removal pending |
| `ClosePopup` | `Fermer popup` | `ClosePopup` | `TargetPageId` fragment | Superseded by `DEC-0050`; removal pending |
| `TogglePopup` | `Basculer popup` | `TogglePopup` | `TargetPageId` fragment | Superseded by `DEC-0050`; removal pending |
| `Show` | `Afficher objet` | `Show` | `TargetElementId`, optional `Condition` | Implemented |
| `Hide` | `Masquer objet` | `Hide` | `TargetElementId`, optional `Condition` | Implemented |
| `ToggleVisibility` | `Basculer visibilite` | `ToggleVisibility` | `TargetElementId`, optional `Condition` | Implemented |
| `ShowBorder` / `HideBorder` / `ToggleBorder` | Bordure legacy | Removed kinds | N/A | Deprecated, not authorable |
| `Start*Effect` / `Stop*Effect` / `Toggle*Effect` | Effets legacy | Removed kinds | N/A | Deprecated; use `StateConfig` |
| `ReadValue` | `Lire valeur` | `ReadValue` binding event | `TagId` | Implemented |
| `WriteValue` | `Ecrire valeur` | `WriteValue` binding event | `TagId` | Implemented |
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

Conditional execution is implemented once for all current object-action kinds in `ActionDispatcher`.

Implemented condition fields:

1. Imported tag id.
2. Operator: `Vrai`, `Faux`, `=`, `<>`, `>`, `>=`, `<`, `<=`.
3. Comparison value for non-boolean operators.

Boolean `Vrai/Faux` operators are valid only for boolean tags. Missing target objects, missing condition tags, boolean operators on non-boolean tags, and missing comparison values are build/export errors.

Compound condition groups are implemented with:

1. `Mode`: `All` requires all conditions to be true; `Any` requires at least one true condition.
2. `MissingTagPolicy`: `BlockAction` prevents the action when a required runtime value is unavailable; `AllowAction` allows the action as an explicit fail-open degraded policy.
3. The same deterministic condition operators as single conditions.

## 6. Tag Authoring Boundary

The current implemented tag slice covers:

1. Importing the TF100Web tag export into the V2 project model.
2. Selecting any enabled tag in the Element+ event modal with labels formatted as `Nom du tag | datatype | Nom de l'appareil`.
3. Creating `Lire valeur` binding events with no user trigger.
4. Creating `Ecrire valeur` binding events with no user trigger and no design-time value field; the runtime operator input supplies the value.
5. Validating `Ecrire valeur` during build/export so read-only Element+ objects, non-input Element+ objects, read-only tags, and missing tags are rejected.
6. Exporting tags and per-element value binding metadata in the FT100/TF100Web package.
7. Applying pushed runtime values to read-bound Element+ objects through the TF100Web page bridge.

The current slice does not yet implement expression authoring, local tag creation, or project protocol import. Local tag creation requires a future protocol import revision.

## 7. Legacy Popup Decommissioning Boundary

Le modèle et le runtime historiques peuvent encore contenir :

1. `ScadaActionKind.MountFragment`, `ClosePopup` et `TogglePopup` avec `TargetPageId`;
2. `ScadaPopupOptions` avec `Position`, `SizePreset`, `AllowMultiple`, `ResetOnOpen` et `HostRegionId`;
3. leurs validations, intentions host et branches de montage de Fragment.

Ces éléments sont des résidus d’implémentation à décommissionner selon `DEC-0050`. Ils ne doivent recevoir aucune nouvelle option, correction fonctionnelle ou surface d’authoring, ne sont pas convertis en Fenêtres rapides et ne peuvent être comptés comme preuve de conformance. Le propriétaire du nouveau contrat est `docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md`. Leur retrait physique doit préserver le JSON original en cas de diagnostic et rester couvert par un chantier explicite du plan.

## 8. Visual Runtime Action Boundary

`SetClass`, `RemoveClass`, and `ToggleClass` are historical contracts removed from the active domain. They must not be reintroduced through `ActionDispatcher`. Current visual behavior is model-backed by `ScadaElementStateConfig`, evaluated by `StateEngine`, and applied reversibly by `EffectApplier`.

## 9. Roadmap Boundary

The following are roadmap items until implemented and covered by tests:

1. Expression/formula condition authoring.
2. Controlled custom script loading and script authoring.
3. Custom visual effect styling and local preview of runtime visual effects.

## 9.1 Canonical Runtime Adapter

1. The page root owns a camelCase `data-scada-action-registry`; each source object owns ordered `data-scada-action-bindings` with trigger, action id, `StopPropagation`, and `PreventDefault`.
2. `ScadaRuntime.initPage` binds the five trigger contracts idempotently; `disposePage` removes those listeners before page replacement.
3. `Navigate` reuse le transport host versionné de `CommandDispatcher`. Tant qu’ils ne sont pas physiquement retirés, `MountFragment`, `ClosePopup` et `TogglePopup` restent des branches legacy isolées et ne sont jamais assimilés aux commandes `OpenQuickWindow`/`CloseQuickWindow`. `Show`, `Hide`, `ToggleVisibility`, `ReadValue` et `WriteValue` restent portables.
4. Element targets are resolved exclusively by exact `data-scada-element-id` inside the initialized page root. A duplicate id in another composed root cannot be selected.
5. Read/write actions use `TagBridge`; condition comparisons use `ExpressionEvaluator`. A missing definition, target, single-condition tag, input value, unknown trigger/operator, disabled source, or unsupported kind fails closed.
6. Bindings execute in persisted order. `PreventDefault` and `StopPropagation` apply to their browser event without silently reordering or suppressing later bindings on the same source.
7. Strict manifest 2.3 export still rejects any action, condition, popup option, or policy marked `Blocked` in the capability registry. Local shared-runtime implementation alone does not promote a capability without TF100Web fixture evidence.

## 10. Event Flow

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

## 11. Related Tests

1. `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
2. `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
3. `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`
4. `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
