# SCADA Builder V2 - Properties Panel Contract

Date: 2026-06-19
Status: Active properties panel contract
Document version: `V2.1.2.0034`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-19 | `V2.1.2.0034` | `61eef34` | Ajout du style bouton appui/actif model-backed dans l'onglet Bouton. |
| 2026-06-18 | `V2.1.2.0032` | `d5ee1fd` | Ajout des proprietes Style avancees opacite et rotation pour les Element+. |
| 2026-06-18 | `V2.1.2.0030` | `cae57c9` | Ajout des presets de boutons HMI Element+ persistants via `ScadaButtonKind`. |
| 2026-06-17 | `V2.1.2.0024` | `PENDING` | Refactor de l'onglet Donnees Element+: retrait authoring de `Mapping / Tag`, `Decimales` et `Unite`; `Format affichage` devient le signal actif. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Ajout de la tab Bouton pour hover automatique, style de survol et etat desactive. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Ajout de l'entree Evenement pour l'edition des bindings runtime Element+. |
| 2026-06-16 | `V2.1.2.0000` | `PENDING` | Clarification du contrat Propriete pour les objets Element+ et du blocage explicite pour les sources non converties. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du contrat actif du panneau proprietes. |

## 1. Contract

The properties panel edits model-backed properties through commands or application services. It must not write durable behavior through ad hoc WebView state.

## 2. Rules

1. Common geometry fields reflect current selection.
2. Mixed values are blank or explicitly represented as mixed.
3. Invalid values must be blocked or warning-only; they must not silently export invalid runtime output.
4. CSS/runtime effect properties require metadata, validation, serialization, preview, and export rules before becoming active.
5. Raw `Mapping / Tag` authoring is deprecated. Runtime value bindings are authored through `Evenement` as `Lire valeur` / `Ecrire valeur`.
6. Context-menu `Propriete` edits only converted Element+ scene objects; source legacy objects must be converted before the property panel can edit durable model-backed properties.
7. Button Element+ text is a model-backed `Data.Text` property and must render in preview and FT100 export.
8. Element+ runtime events are edited through the `Evenement` entry and must write scene actions plus Element+ event bindings, not WPF-only state.
9. Element+ buttons have model-backed hover metadata by default unless disabled or explicitly disabled in the `Bouton` tab.
10. The `Bouton` tab appears between `Style` and `Evenement` for buttons and owns disabled state, hover enablement, hover background, hover foreground, and hover border color.
11. SCADA Builder V2 authors and persists button hover metadata; FT100 export may generate scoped CSS from it, and FT100Web owns the deployed runtime interpretation.
12. SCADA Builder V2 authors and persists button pressed/active metadata; FT100 export may generate scoped `:active` and active toggle-state CSS from it, while preview preserves the metadata without simulating runtime press state.
13. Inserted Element+ buttons persist `ScadaButtonKind` for `Command`, `Toggle`, `Navigation`, `AlarmAcknowledge`, and `EmergencyStop`; the preset supplies initial size, text, and style only. Hover, disabled state, pressed/active state, events, and later property edits remain independently model-backed.
14. The Element+ `Style` tab exposes model-backed `Opacity` from `0` to `1` and `Rotation` in degrees. Preview and FT100 export apply them through CSS `opacity` and `rotate(...)` before `AdvancedCss` so explicit advanced CSS remains the final override point.
15. The Element+ `Donnees` tab exposes `Format affichage` as the active display-format field. `Decimales` and `Unite` are legacy model fields and must not be visible active authoring controls.
16. `Format affichage` may use hash masks such as `##.#`; the mask defines visible digit budget and decimal placement. Example: raw numeric value `999` with `##.#` displays as `99.9`, and the maximum visible value for the mask is `99.9`.
17. `Min` and `Max` are operator-entry clamp constraints only for numeric inputs that are not `Lecture seulement`; they are disabled for read-only displays.

## 3. Related Tests

1. `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
2. `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
3. `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`
4. `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
