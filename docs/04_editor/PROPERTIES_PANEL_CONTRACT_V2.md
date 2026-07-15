# SCADA Builder V2 - Properties Panel Contract

Date: 2026-06-19
Status: Active properties panel contract
Document version: `V2.1.4.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0039` | `PENDING` | Ajout de l'inspecteur/dialogue Input numerique Tableau : valeur, placeholder, readonly, min/max/pas, format et tags lecture/ecriture. |
| 2026-07-15 | `V2.1.4.0030` | `5d762bb` | Origine du format Tableau explicitee et fusion/defusion remplacee par un bouton contextuel unique. |
| 2026-07-15 | `V2.1.4.0028` | `c873744` | La case explicite `Verrouiller la position` est visible dans Propriété > Général et partage l'état normal/mixte avec toutes les autres surfaces. |
| 2026-07-15 | `V2.1.4.0027` | `88e865a` | `TablePropertiesViewModel` partage les valeurs effectives/locales et les états Hérité/Personnalisé/Mixte; reset de propriété/portée, color picker et X/Y/W/H exacts passent par des requêtes typées et le guard de verrou. |
| 2026-07-15 | `V2.1.4.0026` | `0874416` | Onglet Tableau et dialogues etendus aux types/valeurs, portees, format complet, bordures et en-tetes; case de verrouillage partagee ajoutee aux proprietes generales. |
| 2026-07-14 | `V2.1.4.0016` | `10cfa72` | Ajout de l'onglet Tableau contextuel, du dialogue de proprietes Tableau, du format de cellule et des dimensions de pistes partageant le coordinateur type. |
| 2026-07-13 | `V2.1.4.0003` | `b954d46` | Ajout des propriétés typographiques Element+, Foreground authorable, styles de bordure avancés, BorderRadius et aperçu vivant. |
| 2026-06-19 | `V2.1.3.0002` | `PENDING` | Remplacement des couleurs arriere-plan/bordure Style et Bouton par le color picker modal aligne sur `CSS fond`. |
| 2026-06-19 | `V2.1.2.0038` | `6f76dc8` | Clarification de la parite metadata preview/export pour les wrappers de boutons Element+. |
| 2026-06-19 | `V2.1.2.0034` | `61eef34` | Ajout du style bouton appui/actif model-backed dans l'onglet Bouton. |
| 2026-06-18 | `V2.1.2.0032` | `d5ee1fd` | Ajout des proprietes Style avancees opacite et rotation pour les Element+. |
| 2026-06-18 | `V2.1.2.0030` | `cae57c9` | Ajout des presets de boutons HMI Element+ persistants via `ScadaButtonKind`. |
| 2026-06-17 | `V2.1.2.0024` | `PENDING` | Refactor de l'onglet Donnees Element+: retrait authoring de `Mapping / Tag`, `Decimales` et `Unite`; `Format affichage` devient le signal actif. |
| 2026-06-16 | `V2.1.2.0005` | `PENDING` | Ajout de la tab Bouton pour hover automatique, style de survol et etat desactive. |
| 2026-06-16 | `V2.1.2.0004` | `PENDING` | Ajout de l'entree Evenement pour l'edition des bindings runtime Element+. |
| 2026-06-16 | `V2.1.2.0000` | `PENDING` | Clarification du contrat Propriete pour les objets Element+ et du blocage explicite pour les sources non converties. |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du contrat actif du panneau proprietes. |

## 5. Advanced Element+ Style Contract

The Style tab exposes the following model-backed values on both `ElementPropertiesDialog` and the docked `MainWindow` surface:

1. `FontWeight`: `Normal`, `Bold`, `Bolder`, `Lighter`, or numeric CSS weights 100–900 by increments of 100.
2. `FontStyle`: `Normal`, `Italic`, or `Oblique`.
3. `TextDecoration`: combinable `Underline`, `LineThrough`, and `Overline` values.
4. `TextAlign`: `Left`, `Center`, `Right`, or `Justify`.
5. `TextTransform`: `None`, `Uppercase`, `Lowercase`, or `Capitalize`.
6. `LetterSpacing` and `LineHeight`: pixel values; `LineHeight = 0` means CSS `normal`.
7. `Foreground`: editable through the existing `ColorPickerField`.
8. `BorderStyle`: `None`, `Solid`, `Dashed`, `Dotted`, `Double`, `Groove`, `Ridge`, `Inset`, or `Outset`.
9. `BorderRadius`: four non-negative pixel corners; the UI may expose a uniform convenience mode.

Structured values are applied before `AdvancedCss`, which remains the final user override except for export geometry, namespace, and security invariants. All style mutations use the existing scene mutation and history path, preserve old-project defaults, and are consumed by both WebView preview and FT100 export.

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
14. The preview WebView must expose wrapper-level button metadata matching FT100 export (`data-scada-button-kind`, behavior metadata, disabled metadata, and Toggle initial state) while keeping generated buttons non-interactive for editor selection and property workflows.
15. The Element+ `Style` tab exposes model-backed `Opacity` from `0` to `1` and `Rotation` in degrees. Preview and FT100 export apply them through CSS `opacity` and `rotate(...)` before `AdvancedCss` so explicit advanced CSS remains the final override point.
16. Color-valued Element+ `Style` fields, including `Background` and `BorderColor`, and color-valued `Bouton` fields use the same color picker model as `CSS fond`: swatch preview, saturation/value area, hue slider, preset swatches, hex output, and RGB sliders in a modal picker with `Annuler` and `Enregistrer` before committing the model-backed value.
17. The Element+ `Donnees` tab exposes `Format affichage` as the active display-format field. `Decimales` and `Unite` are legacy model fields and must not be visible active authoring controls.
18. `Format affichage` may use hash masks such as `##.#`; the mask defines visible digit budget and decimal placement. Example: raw numeric value `999` with `##.#` displays as `99.9`, and the maximum visible value for the mask is `99.9`.
19. `Min` and `Max` are operator-entry clamp constraints only for numeric inputs that are not `Lecture seulement`; they are disabled for read-only displays.
20. `Propriété > Général` affiche toujours la case `Verrouiller la position` pour une sélection Element+; elle est cochée si tout est verrouillé, indéterminée pour une sélection mixte et décochée sinon.

## 3. Table Inspector Contract

1. `TablePropertiesInspector` calcule les valeurs effectives et locales par propriété pour les portées Tableau, en-têtes, alternance, rangées, colonnes, cellule et plage.
2. Le panneau et les dialogues expliquent l'origine du format depuis le meme `TablePropertiesViewModel`: `Herite` signifie aucune surcharge locale, `Personnalise` signifie une surcharge sur la portee et `Mixte` signifie plusieurs valeurs locales dans la selection.
3. Réinitialiser une propriété remet uniquement cette propriété à `null` sur chaque cible sans aplatir les autres surcharges hétérogènes.
4. Réinitialiser la portée retire toutes ses surcharges dans une seule mutation historique.
5. Le dialogue Tableau expose X/Y/W/H exacts; une variation X/Y est rejetée par le guard de position si l'Element+ est verrouillé, tandis qu'un resize sans translation demeure permis.
6. Le panneau expose un seul bouton Fusionner/Defusionner dont le libelle et l'etat suivent la presence de cellules fusionnees dans la plage active.
7. Une cellule ancre unique de type `InputNumeric` active la section `Input numerique`; le panneau resume son etat et ouvre le dialogue dedie pour valeur initiale, placeholder, lecture seule, minimum, maximum, pas, `DisplayFormat`, tag de lecture et tag d'ecriture.
8. Les tags proviennent du catalogue courant. Un tag d'ecriture doit etre ecrivable; readonly et binding d'ecriture sont incompatibles. Conversion, suppression de binding et operations structurelles destructives passent par le coordinateur type et ses confirmations, jamais par une mutation directe de `MainWindow`.

## 4. Related Tests

1. `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`
2. `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
3. `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`
4. `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
