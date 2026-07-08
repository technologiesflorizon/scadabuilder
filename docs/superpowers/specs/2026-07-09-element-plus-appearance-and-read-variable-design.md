# Element+ — Modification d'apparence en liste, Filtre de couleur, Lecture de variable — Design

Date: 2026-07-09
Status: Approved design — prêt pour planification d'implémentation
Portée: SCADA Builder V2 (Domain + App WPF + Rendering/exporteur + Runtime JS)
Dépendance: `2026-07-07-element-plus-state-command-events-design.md` (modèle État/Commande implémenté),
`2026-07-07-etat-condition-variable-expression-design.md` (mode Variable/Expression de la condition)

## 1. Problème

Trois lacunes trouvées en creusant une régression rapportée ("perte de la lecture de valeur vers
un mapping pour les Element+ Text") :

1. **Aucun moyen d'afficher en continu la valeur d'un tag** sur un Element+ Text. L'ancien
   mécanisme (`ReadValue`) a été explicitement "absorbé" dans le modèle État par le design du
   2026-07-07 (§7 : "lecture = moteur d'état, interpolation `{tag}`"), mais aucun chemin UI ne
   permet de le faire aujourd'hui — et même en le forçant via une règle d'état à condition
   toujours vraie, ça ne fonctionnerait pas (voir point 3).

2. **`Couleur de fond` ne fonctionne pas sur les composants `.sep` basés SVG.** L'effet
   `BackgroundColor` s'applique au `<div>` racine de l'Element+, mais le SVG interne le recouvre
   entièrement — le changement de couleur n'est jamais visible. Aucun mécanisme de substitution
   n'existe pour ces composants.

3. **`TextContent` ne fonctionne sur rien, actuellement.** Deux bugs cumulés :
   - `effect-applier.js` cherche un enfant `[data-scada-text]` pour y écrire le texte, mais
     l'exporteur n'a jamais émis ce marqueur sur aucun type d'Element+.
   - Même avec une cible, l'interpolation `{TagId}` documentée dans le design du 2026-07-07
     (`"Débit: {Flow}"`) n'a jamais été implémentée — le texte littéral `{Flow}` s'afficherait
     tel quel plutôt que la valeur du tag.

4. **L'éditeur d'état actuel** (`ElementStateRuleDialog`) affiche 7 cases à cocher toujours
   visibles (Couleur de fond, Bordure, Texte, Visibilité, Opacité, Rotation, Animation), ce qui
   encombre la fenêtre et ne permet pas d'ajouter facilement un futur type d'effet sans repasser
   par un réagencement complet du XAML.

## 2. Objectif

- Un champ **indépendant** "Lecture de variable" par Element+ (Tag + format), qui affiche en
  continu la valeur d'un tag sur l'élément **sans interférer** avec l'évaluation des règles
  d'état d'apparence (les deux se combinent : le texte affiche le tag, la couleur/bordure reflète
  l'état, simultanément).
- Un nouveau type d'effet **Filtre de couleur** (superposition translucide + halo optionnel) qui
  fonctionne sur tous les types d'Element+, y compris les `.sep` SVG.
- Une UI de modification d'apparence réorganisée en **liste cumulable** (dropdown de type +
  "+ Ajouter"), au lieu de cases à cocher fixes.
- Les deux bugs bloquants (`data-scada-text` absent, interpolation `{tag}` absente) corrigés,
  sans quoi rien de ce qui précède ne s'affiche réellement.
- Garde-fou : un seul `Kind` de commande par Element+ dans l'onglet Commande (ex. un seul
  `Navigate`), peu importe le déclencheur.
- Confirmation qu'aucun changement côté TF100Web (Django) n'est nécessaire — ce document inclut
  la vérification qui le prouve.

## 3. Décisions verrouillées

| # | Décision |
|---|---|
| D1 | "Lecture de variable" est un champ **singulier et indépendant** sur `ScadaElementStateConfig` (`ReadVariable: ScadaReadVariableRule?`), **pas** une entrée de la liste `States`. Étant un champ unique (pas une liste), l'unicité est garantie par le type — pas besoin de garde-fou UI pour ça. |
| D2 | Évaluation runtime en deux passes indépendantes par cycle : (1) `States` first-match-wins → effet d'apparence ; (2) si `ReadVariable` configuré → texte interpolé sur `[data-scada-text]`. Ordre d'application : `ReadVariable` d'abord (texte de base), puis l'effet de l'état matché par-dessus — un état actif dont l'effet définit un `TextContent` explicite (ex. `---` en erreur, D5 du design précédent) écrase le texte de `ReadVariable` pour ce cycle. |
| D3 | Un nouvel éditeur dédié `ElementReadVariableDialog` (Tag + Format), distinct de `ElementStateRuleDialog` — pas de mode combiné dans l'éditeur de règle d'état, les deux concepts ont des formes de données trop différentes (pas de Condition, pas de liste d'effets). |
| D4 | La section "Modification d'apparence" de `ElementStateRuleDialog` devient une liste cumulable : dropdown de type + bouton "+ Ajouter" ajoute une entrée configurable à une liste (éditable/supprimable). Le modèle `ScadaEffectBlock` reste inchangé dans son principe (propriétés optionnelles cumulables) — seule l'UI change. |
| D5 | Nouveau type d'effet **Filtre de couleur** : superposition translucide (`ColorFilterColor` + `ColorFilterOpacity`), pas un vrai recolorage — le rendu d'origine reste visible en transparence en dessous. Case **Halo** (`ColorFilterHalo: bool`) ; si cochée, un champ **Couleur du halo** (`ColorFilterHaloColor`, pré-rempli avec `ColorFilterColor`) apparaît. Le halo réutilise la classe CSS `.scada-anim-halo` déjà définie (glow pulsé), pas un glow statique. |
| D6 | Garde-fou Commande : un seul `Kind` par Element+ (peu importe le `Trigger`). Validation UI (dropdown Kind désactive les valeurs déjà utilisées ; message d'erreur si contournée). |
| D7 | Les deux surfaces (panneau docké `MainWindow.xaml` **et** fenêtre modale `ElementPropertiesDialog`) doivent recevoir tous les changements UI de ce design, dans le même lot — une régression précédente (`8674523`) a montré qu'oublier l'une des deux surfaces reproduit le bug initial. |
| D8 | Aucun changement côté TF100Web (Django/Python) : le HTML/CSS/JS exporté est opaque pour `scada_package_page` (extraction par regex du fragment/`css_hash`/dimensions, jamais de parsing du JSON `data-scada-state-config`). Un test bout-en-bout (export → `deploy_scada_builder` → page servie sans erreur) est ajouté au plan pour le prouver, pas seulement l'affirmer. |

## 4. Modèle de données (Domain)

```
ScadaEffectBlock                      // src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaEffectBlock.cs
├─ ... (champs existants inchangés : BackgroundColor, BorderColor, BorderWidth, TextColor,
│      TextContent, TextVisible, ElementVisible, Opacity, Rotation, Animation)
├─ ColorFilterColor    : string?      // nouveau — hex
├─ ColorFilterOpacity  : double?      // nouveau — 0.0-1.0
├─ ColorFilterHalo     : bool?        // nouveau
└─ ColorFilterHaloColor: string?      // nouveau — hex, pré-rempli avec ColorFilterColor à l'UI

ScadaElementStateConfig               // src/ScadaBuilderV2.Domain/ElementEvents/State/ScadaElementStateConfig.cs
├─ QualityFallback : ScadaEffectBlock              // inchangé
├─ DefaultEffect   : ScadaEffectBlock              // inchangé
├─ States          : List<ScadaStateRule>          // inchangé — appearance uniquement désormais
└─ ReadVariable    : ScadaReadVariableRule?         // nouveau — indépendant de States

ScadaReadVariableRule                 // nouveau fichier : State/ScadaReadVariableRule.cs
├─ TagId         : string             // obligatoire
└─ DisplayFormat : string?            // optionnel — ex. "Debit: {valeur} L/min" ; si vide, valeur brute
```

`DisplayFormat` utilise le jeton littéral `{valeur}` (remplacé par la valeur du tag résolue) —
distinct de la syntaxe `{TagId}` des expressions/effets existants, parce qu'ici le tag est déjà
fixé par `TagId` : pas besoin de répéter son nom dans le format. Si `DisplayFormat` est vide ou
ne contient pas `{valeur}`, le runtime affiche la valeur brute du tag.

`ScadaStateRule` et `ScadaCommandBinding` restent inchangés (aucun champ supplémentaire — le
combobox "Action" envisagé dans une itération précédente de ce design est abandonné : la lecture
de variable a son propre éditeur, `+ Ajouter` dans la liste État ne crée que des règles
d'apparence).

## 5. Sérialisation (manifest + HTML)

`data-scada-state-config` gagne une clé `readVariable` (objet ou absente si non configurée),
au même niveau que `qualityFallback`/`defaultEffect`/`states` :

```json
{
  "qualityFallback": { "opacity": 0.4, "borderColor": "#000000", "borderWidth": 2 },
  "defaultEffect": {},
  "readVariable": { "tagId": "tf100.mapping.42", "displayFormat": "Debit: {valeur} L/min" },
  "states": [
    { "id": "s1", "name": "Alarme haute", "enabled": true,
      "expression": { "source": "{Temp} > 80", "ast": { "...": "..." } },
      "effect": {
        "backgroundColor": "#E53935",
        "colorFilterColor": "#E53935", "colorFilterOpacity": 0.35,
        "colorFilterHalo": true, "colorFilterHaloColor": "#E53935"
      }
    }
  ]
}
```

Aucun `[JsonDerivedType]`/convertisseur supplémentaire nécessaire — `ColorFilter*` sont des
propriétés optionnelles plates sur `ScadaEffectBlock` (même traitement que `BackgroundColor`),
`ReadVariable` est un objet simple sans polymorphisme.

**Ordre de la liste `States`** : confirmé préservé de bout en bout aujourd'hui (`List<T>` →
`System.Text.Json` sérialise dans l'ordre d'énumération ; aucun `OrderBy`/`Sort` ne touche
`States`/`Commands` dans `Ft100SceneExporter.cs` — vérifié par grep, seuls 3 `OrderBy` existent
dans tout le fichier et concernent le tri des ids de page et des ids source supprimés, sans
rapport). Un test de régression verrouille cette garantie bout-en-bout (UI → JSON → HTML →
évaluation runtime JS), pour qu'un futur refactor ne l'introduise pas silencieusement.

## 6. Runtime JS (`src/ScadaBuilderV2.Rendering/Runtime/`)

### 6.1 `data-scada-text` — cible manquante (bug bloquant #1)

`Ft100SceneExporter.BuildElementContent` enveloppe désormais le contenu des éléments
`ScadaElementKind.Text` dans `<span data-scada-text>...</span>` (au lieu d'injecter le texte
brut directement dans le `<div>` racine). Portée volontairement limitée à `Text` — les `.sep`
(SVG) n'ont pas de zone de texte naturelle, `TextContent` n'a pas de sens dessus.

### 6.2 Interpolation `{tag}` — absente (bug bloquant #2)

`effect-applier.js` (`TextContent` et `ScadaReadVariableRule.DisplayFormat`) résout les tokens
avant assignation :
- `TextContent` (effet d'état) : token `{TagId}` (même syntaxe que les expressions), résolu via
  `window.ScadaRuntime.TagBridge.getTagValue(tagId)`.
- `DisplayFormat` (lecture de variable) : token `{valeur}` uniquement, résolu avec la valeur du
  `TagId` fixé sur la règle.
- Tag introuvable/valeur `null` → le token est remplacé par `---` (cohérent avec le badge
  d'erreur D5 du design précédent), pas de levée d'exception.

### 6.3 Filtre de couleur

`effect-applier.js` gère un enfant overlay créé/mis à jour paresseusement (une seule fois, puis
réutilisé) : `<div class="ft100-color-filter-overlay">` en position absolue, `inset: 0`,
`pointer-events: none`, avec `background-color: ColorFilterColor` et `opacity:
ColorFilterOpacity`. Si `ColorFilterHalo` est vrai, la classe `scada-anim-halo` (déjà définie par
page dans le CSS exporté) est ajoutée à l'overlay, avec `color: ColorFilterHaloColor` (le
`box-shadow` pulsé de `.scada-anim-halo` utilise `currentColor`). Si un état ne définit pas de
filtre de couleur, l'overlay existant (d'un état précédent) est retiré/masqué.

### 6.4 Lecture de variable — évaluation indépendante

`state-engine.js`, dans `evaluate(element, tagValues)` :
1. Nouvelle étape, avant la boucle `States` : si `data-scada-state-config` contient
   `readVariable`, résout `DisplayFormat` (ou la valeur brute) et écrit sur
   `[data-scada-text]` — indépendamment du résultat de la boucle `States`.
2. La boucle `States` s'exécute ensuite normalement (inchangée) ; si l'état matché définit un
   `TextContent` propre, il écrase le texte posé à l'étape 1 (cohérent avec D5 : l'erreur force
   `---`).

## 7. UI WPF

### 7.1 Onglet État — deux sections indépendantes

- **Règles d'état** (liste, inchangée dans sa structure) : `+ Ajouter` / Monter / Descendre /
  Éditer / Supprimer — ouvre `ElementStateRuleDialog`, qui ne gère plus que les règles
  d'apparence (Condition + liste d'effets cumulables, voir 7.2).
- **Lecture de variable** (nouvelle section, sous la liste) : si non configurée, bouton
  "+ Lecture de variable..." ; si configurée, résumé (`"Lecture: {tag} -> {format}"`) + boutons
  Modifier/Supprimer. Ouvre `ElementReadVariableDialog` (Tag ComboBox + TextBox Format).

### 7.2 `ElementStateRuleDialog` — liste cumulable d'effets

Remplace les 7 `CheckBox` + panneaux toujours présents par : un `ComboBox` de type (Couleur de
fond, Bordure, Texte, Visibilité, Opacité, Rotation, Animation, **Filtre de couleur**) + bouton
"+ Ajouter", qui ajoute une entrée à une `ListBox` en dessous (chaque entrée : résumé + Éditer/
Supprimer, édition inline via les mêmes contrôles que l'ancien panneau — juste affichés à la
demande au lieu d'en permanence). Un type déjà présent dans la liste est retiré du ComboBox
(pas de doublon "Couleur de fond" x2). Le filtre de couleur ouvre un mini-panneau : Couleur,
Opacité (slider 0-1), case Halo, et si cochée, Couleur du halo (pré-remplie avec la couleur du
filtre, éditable séparément).

### 7.3 Onglet Commande — garde-fou un-par-Kind

`ElementCommandDialog` : le `KindComboBox` désactive (grise, pas retire) les valeurs déjà
utilisées par une autre commande du même Element+ (sauf la commande en cours d'édition). Si
contourné (import externe, etc.), la sauvegarde de l'onglet Commande refuse le doublon et
affiche un message d'erreur dans le statut de l'app (pas d'exception, pas de sauvegarde
partielle).

### 7.4 Les deux surfaces

Chaque changement de 7.1-7.3 est appliqué à **la fois** dans `MainWindow.xaml`/`.xaml.cs`
(panneau docké) et `ElementPropertiesDialog.xaml`/`.xaml.cs` (fenêtre modale double-clic) —
même check-list de fichiers dans le plan d'implémentation, pas de "je le ferai plus tard sur
l'autre surface".

## 8. Vérification TF100Web (preuve, pas affirmation)

`frontend/views.py::scada_package_page` (TF100Web) fait uniquement : lecture du fichier HTML,
extraction du fragment par id (`_extract_html_element_by_id`), extraction du hash CSS par regex
sur le `<link>` (`_extract_css_hash_from_html`), extraction des dimensions par regex sur des
attributs `data-scada-width`/`data-scada-height`. Aucune de ces fonctions ne touche
`data-scada-state-config`/`data-scada-command-config` — le JSON qu'ils contiennent (avec les
nouveaux champs `readVariable`/`colorFilter*`) traverse Django sans jamais être parsé côté
serveur. Le plan d'implémentation inclut un test bout-en-bout : exporter un projet avec
`ReadVariable` + `Filtre de couleur` configurés, le déployer via `deploy_scada_builder`, vérifier
que `scada_package_page` sert la page sans erreur et que le fragment contient bien les attributs
attendus.

## 9. Fichiers touchés (résumé)

| Fichier | Action |
|---|---|
| `Domain/ElementEvents/State/ScadaEffectBlock.cs` | Ajouter 4 champs ColorFilter* |
| `Domain/ElementEvents/State/ScadaElementStateConfig.cs` | Ajouter champ `ReadVariable` |
| `Domain/ElementEvents/State/ScadaReadVariableRule.cs` | **Créer** |
| `Rendering/Ft100SceneExporter.cs` | `BuildElementContent` (span data-scada-text pour Text) ; `readVariable` se sérialise automatiquement (propriété optionnelle simple, même mécanisme que les autres champs de `ScadaElementStateConfig`, aucun convertisseur JSON supplémentaire) |
| `Rendering/Runtime/effect-applier.js` | Interpolation `{tag}`/`{valeur}`, overlay filtre de couleur |
| `Rendering/Runtime/state-engine.js` | Étape indépendante `readVariable` avant la boucle `States` |
| `App/ElementStateRuleDialog.xaml` + `.xaml.cs` | Liste cumulable dropdown+Ajouter, type Filtre de couleur |
| `App/ElementReadVariableDialog.xaml` + `.xaml.cs` | **Créer** |
| `App/ElementCommandDialog.xaml.cs` | Garde-fou un-par-Kind |
| `App/ElementPropertiesDialog.xaml` + `.xaml.cs` | Refléter 7.1-7.3 (surface modale) |
| `App/MainWindow.xaml` + `.xaml.cs` | Refléter 7.1-7.3 (surface dockée) |
| `tests/ScadaBuilderV2.Tests/...` | Tests domaine, export, ordre de sérialisation |
| `tests/runtime-js/...` | Tests interpolation, overlay filtre de couleur, lecture de variable indépendante |

## 10. Hors scope

- Édition de Repos/Qualité (confirmé non nécessaire — la règle "Lecture de variable" couvre le
  besoin qui motivait cette demande).
- Recolorage complet du SVG (option "silhouette unie" écartée au profit de la superposition
  translucide).
- Halo statique (non pulsé) pour le Filtre de couleur — réutilise l'animation pulsée existante.
- Changements côté TF100Web (Django) — confirmé non nécessaires (§8).
