# Element+ — Capacités de style avancé — Spécification de conception

Date: 2026-07-13
Status: Draft design — validation TF100Web et approbation utilisateur requises
Portée: SCADA Builder V2 — Domain + App WPF + WebView preview + Rendering/exporteur
Dépendances: `docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md`, `docs/06_ui_ux/UI_SPECIFICATION_V2.md`, `docs/06_ui_ux/ICON_STRATEGY_V2.md`
Version du document: `V2.1.3.0009`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-13 | `V2.1.3.0009` | `PENDING` | Statut repassé en Draft, aperçu vivant rendu obligatoire, et contrat réel de persistance/export/TF100Web distingué et vérifié. |
| 2026-07-13 | `V2.1.3.0008` | `PENDING` | Réécriture complète : hypothèses vérifiées contre le code, questions ouvertes résolues (D19-D20), mapping CSS précis, spécification du `DialogResult` étendu et du contrat de sérialisation. |
| 2026-07-13 | `V2.1.3.0007` | `PENDING` | Ajout de la direction UI inspirée de Microsoft Word et clarification du statut des icônes `Icon.Property.*`. |
| 2026-07-13 | `V2.1.3.0006` | `PENDING` | Refonte de la spécification : contrat de modèle, flux UI, sérialisation, rendu, migration et critères d'acceptation. |
| 2026-07-13 | `V2.1.3.0005` | `PENDING` | Première ébauche issue de l'audit des lacunes Style. |

---

## 1. Problème

### 1.1 État actuel (vérifié contre le code)

Le record `ScadaElementStyle` (`src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs:198-211`) expose 13 propriétés :

```
FontFamily, FontSize, Foreground, Background,
BorderColor, BorderWidth, BorderStyle, ShadowPreset,
AdvancedCss, Opacity, Rotation,
FlipHorizontally, FlipVertically
```

L'onglet Style du dialogue modal (`ElementPropertiesDialog.xaml:98-156`) et du panneau docké (`MainWindow.xaml:910-970`) exposent des contrôles pour : famille de police, taille, fond, couleur/style/largeur de bordure, ombre, opacité, rotation et CSS avancé.

Le rendu WebView (`MainWindow.WebViewScript.cs:1884-1897`) et l'exporteur FT100 (`Ft100SceneExporter.cs:1741-1753`) consomment les mêmes 13 propriétés et produisent un CSS équivalent.

### 1.2 Lacunes confirmées

L'audit du 2026-07-13, vérifié par inspection du code, confirme six lacunes :

1. **Propriétés typographiques absentes du modèle.** `ScadaElementStyle` n'a pas de champs pour `font-weight`, `font-style`, `text-decoration`, `text-align`, `text-transform`, `letter-spacing` ou `line-height`. Le WebView les liste dans son filtre de propriétés préservées (ligne 556-557), mais aucune valeur n'est jamais peuplée.

2. **`Foreground` (couleur du texte) non authorable.** Présent dans `ScadaElementStyle` et correctement consommé par le WebView (`wrapper.style.color`) et l'exporteur (`color: {Foreground}`), mais aucun contrôle UI ne permet de le modifier — ni dans le dialogue modal, ni dans le panneau docké. Le `ElementPropertiesDialogResult` (ligne 451) n'a d'ailleurs pas de champ `Foreground`.

3. **Styles de bordure limités à 4 valeurs.** Le `ComboBox` XAML (lignes 124-129) propose `Solid`, `Dashed`, `Dotted`, `None`. Les styles CSS `Double`, `Groove`, `Ridge`, `Inset`, `Outset` sont absents. La méthode `NormalizeBorderStyle` de l'exporteur (ligne 2061) fait déjà un `ToLowerInvariant()` générique — elle accepterait n'importe quelle chaîne sans modification.

4. **Aucun `border-radius` persisté.** Le modèle n'a pas de champ `BorderRadius`. Les rayons visibles sur certaines formes (`RoundedRectangle`, `Motor`, `Breaker`) sont des constantes codées en dur dans le renderer SVG (`renderShapeElement`, lignes 1281-1516) et dans l'exporteur (méthodes `Render*Svg`), pas des propriétés utilisateur.

5. **`AdvancedCss` comme seul recours.** Sans propriétés structurées, l'utilisateur doit écrire du CSS brut pour des besoins élémentaires (mettre en gras, souligner, centrer). Ce CSS n'est ni validé, ni prévisualisé, ni cohérent entre les deux surfaces.

6. **Risque de régression de parité.** Une modification d'une seule surface (dialogue modal ou panneau docké) créerait une divergence — problème déjà rencontré sur des fonctionnalités Element+ antérieures. Les deux surfaces et les deux chemins de rendu doivent évoluer ensemble.

### 1.3 Incohérence exporteur identifiée

La méthode `AppendElementCss` (bloc `<style>` CSS) omet `opacity`, `transform-origin` et `transform` (rotation + flips), alors que `BuildElementInlineStyle` (attribut `style` inline) les émet correctement. Cette incohérence est documentée comme un bug existant hors scope — elle sera corrigée dans le cadre de ce chantier puisque les nouvelles propriétés doivent être émises dans les deux chemins.

---

## 2. Objectif

Ajouter des propriétés de style model-backed couvrant les besoins CSS de présentation les plus fréquents en ingénierie SCADA, tout en maintenant la parité sur les quatre surfaces :

```text
WPF modal / panneau docké
            ↓
ElementPropertiesDialogResult / mutation de scène
            ↓
ScadaElementStyle persisté
       ↙                    ↘
WebView preview          FT100 .sb2 export
```

Le résultat attendu est un éditeur de propriétés utilisable quotidiennement par un intégrateur SCADA : compact, lisible, rapide à parcourir, accessible au clavier, visuellement cohérent avec le registre d'icônes existant, et suffisamment explicite pour ne pas exiger `AdvancedCss` dans les cas courants.

---

## 3. Décisions

### 3.1 Décisions d'architecture

| # | Décision |
|---|---|
| D1 | Les nouvelles propriétés sont des champs persistants du modèle. Leur fonctionnement ne dépend pas d'une concaténation CSS WPF-only. |
| D2 | Les champs sont ajoutés avec des valeurs par défaut rétrocompatibles. Un ancien projet sans ces champs doit produire le même rendu qu'avant. |
| D3 | Les deux surfaces UI (`ElementPropertiesDialog` et panneau docké `MainWindow`) sont modifiées dans le même lot et utilisent la même nomenclature, les mêmes valeurs et les mêmes règles de validation. |
| D4 | `FontWeight` est limité aux valeurs CSS valides : `Normal`, `Bold`, `Bolder`, `Lighter` et les poids numériques 100–900 par pas de 100. Le défaut est `"Normal"`. |
| D5 | `FontStyle` accepte `Normal`, `Italic` et `Oblique`. Le défaut est `"Normal"`. |
| D6 | `TextDecoration` est un tableau d'enums combinable : `Underline`, `LineThrough`, `Overline`. Un tableau vide ou `null` signifie aucune décoration. |
| D7 | `TextAlign` accepte `Left`, `Center`, `Right` et `Justify`. Le défaut est `"Left"`. |
| D8 | `TextTransform` accepte `None`, `Uppercase`, `Lowercase` et `Capitalize`. Le défaut est `"None"`. |
| D9 | `LetterSpacing` est un `double` exprimé en pixels. Le défaut est `0`. Les valeurs négatives sont permises dans la limite [-10, 50]. |
| D10 | `LineHeight` est un `double` exprimé en pixels. La valeur `0` signifie `normal` (comportement CSS par défaut). Le défaut est `0`. Les valeurs négatives sont refusées. |
| D11 | `Foreground` (couleur du texte) devient authorable avec le même `ColorPickerField` que les autres couleurs Element+. |
| D12 | Les styles de bordure authorables sont `None`, `Solid`, `Dashed`, `Dotted`, `Double`, `Groove`, `Ridge`, `Inset` et `Outset`. |
| D13 | `BorderRadius` est un record `ScadaBorderRadius` avec quatre coins (`TopLeft`, `TopRight`, `BottomRight`, `BottomLeft`), chaque coin étant un `double` en pixels (défaut `0`). Les valeurs négatives sont normalisées à zéro. |
| D14 | Les valeurs structurées sont appliquées avant `AdvancedCss`. `AdvancedCss` reste le dernier override utilisateur, sauf pour les invariants de sécurité, de namespace et de géométrie d'export qui ne peuvent pas être écrasés. |
| D15 | `Icon.Property.*` est une nouvelle famille de clés à créer dans `Resources/Icons.xaml`. Ces icônes n'existent pas aujourd'hui et doivent être créées comme ressources vectorielles internes. |
| D16 | Les nouvelles icônes sont des ressources vectorielles WPF internes, cohérentes avec les familles existantes (`Icon.Tool.*`, `Icon.Shape.*`, `Icon.Hmi.*`, etc.). Aucune dépendance tierce. |
| D17 | Le comportement UI suit une direction d'interaction inspirée de Microsoft Word : groupes lisibles, boutons de formatage à état actif, contrôles contextuels, tooltips, aperçu et réinitialisation locale. C'est une référence d'interaction, pas une copie visuelle. |
| D18 | Toute modification de style passe par la mutation de scène existante et participe à undo/redo, sauvegarde/recharge, preview et export. |

### 3.2 Décisions de format de données

| # | Décision |
|---|---|
| D19 | `TextDecoration` est sérialisé en JSON comme un tableau d'enums : `"textDecoration": ["Underline", "LineThrough"]`. Un tableau vide ou absent signifie aucune décoration. Ce format permet l'édition individuelle des décorations (toggle) sans parsing de chaîne CSS, et reste cohérent avec l'orientation enum du modèle Domain. |
| D20 | `LineHeight` est sérialisé comme un `double` en pixels. La valeur `0` est la sentinelle pour `normal`. Format JSON : `"lineHeight": 0` (normal) ou `"lineHeight": 24` (24px). Ce choix est cohérent avec `BorderWidth: 0` qui signifie déjà "pas de bordure". Si le support du multiplicateur sans unité devient nécessaire, une migration vers un record dédié reste possible sans casser la rétrocompatibilité. |

---

## 4. Contrat du modèle de données

### 4.1 Extension de `ScadaElementStyle`

Le record existant conserve tous ses champs. Les nouvelles propriétés sont ajoutées avec des valeurs par défaut rétrocompatibles :

```csharp
public sealed record ScadaElementStyle(
    // Champs existants — inchangés
    string FontFamily,
    double FontSize,
    string Foreground,
    string Background,
    string BorderColor,
    double BorderWidth,
    string BorderStyle,
    string ShadowPreset,
    string? AdvancedCss,
    double Opacity = 1,
    double Rotation = 0,
    bool FlipHorizontally = false,
    bool FlipVertically = false,
    // Nouvelles propriétés — typographie
    string FontWeight = "Normal",
    string FontStyle = "Normal",
    IReadOnlyList<string>? TextDecoration = null,
    string TextAlign = "Left",
    string TextTransform = "None",
    double LetterSpacing = 0,
    double LineHeight = 0,
    // Nouvelles propriétés — bordure
    ScadaBorderRadius? BorderRadius = null)
```

Les constructeurs existants dans les tests et les convertisseurs ne sont pas cassés puisque toutes les nouvelles propriétés ont des valeurs par défaut.

### 4.2 `ScadaBorderRadius`

```csharp
public sealed record ScadaBorderRadius(
    double TopLeft = 0,
    double TopRight = 0,
    double BottomRight = 0,
    double BottomLeft = 0)
{
    /// <summary>True quand les quatre coins sont égaux (mode uniforme).</summary>
    [JsonIgnore]
    public bool IsUniform =>
        Math.Abs(TopLeft - TopRight) < 0.01 &&
        Math.Abs(TopRight - BottomRight) < 0.01 &&
        Math.Abs(BottomRight - BottomLeft) < 0.01;

    /// <summary>Normalise les valeurs négatives à zéro.</summary>
    public ScadaBorderRadius Normalized() => new(
        Math.Max(0, TopLeft),
        Math.Max(0, TopRight),
        Math.Max(0, BottomRight),
        Math.Max(0, BottomLeft));
}
```

Règles :
- Toutes les valeurs sont en pixels CSS.
- Une valeur négative est normalisée à zéro avant mutation.
- Le mode uniforme est une commodité UI (un seul `TextBox`), pas une seconde représentation persistée. Quand l'utilisateur passe du mode uniforme au mode par coin, les quatre valeurs sont initialisées à la valeur uniforme courante.
- Quatre valeurs égales doivent produire un rendu identique au mode uniforme.
- Les rayons supérieurs aux dimensions de l'élément sont laissés au moteur CSS ; l'UI doit les accepter mais peut suggérer une validation visuelle (tooltip, aperçu).

### 4.3 Compatibilité JSON

Les anciens JSON n'ont aucun des nouveaux champs. L'inspection de `ModernProjectStore.JsonOptions` confirme que la persistance projet utilise `PropertyNameCaseInsensitive = true`, `WriteIndented = true` et `JsonStringEnumConverter`, sans `JsonSerializerDefaults.Web` ni `DefaultIgnoreCondition`. Les propriétés de projet sont donc actuellement écrites avec la convention .NET par défaut (PascalCase), et les nouveaux champs manquants reçoivent leurs défauts du record à la désérialisation. Aucune migration destructive n'est exécutée.

Le contrat comporte trois niveaux distincts :

1. **Persistance projet** (`ModernProjectStore`) : JSON .NET de projet, noms de propriétés par défaut et lecture insensible à la casse.
2. **Manifest `.sb2`** (`Ft100SceneExporter.ManifestJsonOptions`) : JSON de manifest avec noms .NET par défaut ; TF100Web lit notamment `Pages`, `Id`, `IncludeInBuild`, `PageType`/`Type`, `HeaderPageId`, `FooterPageId` et `Objects`.
3. **Attributs runtime HTML** (`StateCommandJsonOptions`) : JSON camelCase destiné au JavaScript, notamment `data-scada-state-config` et `data-scada-command-config`.

Les nouvelles propriétés de `ScadaElementStyle` doivent respecter le niveau où elles sont consommées. Elles ne doivent pas être documentées comme un unique contrat camelCase global.

Exemple de style complet après extension :

```json
{
  "fontFamily": "Segoe UI",
  "fontSize": 16,
  "foreground": "#0F2A30",
  "background": "Transparent",
  "borderColor": "Transparent",
  "borderWidth": 0,
  "borderStyle": "None",
  "shadowPreset": "None",
  "opacity": 1,
  "rotation": 0,
  "flipHorizontally": false,
  "flipVertically": false,
  "fontWeight": "Bold",
  "fontStyle": "Italic",
  "textDecoration": ["Underline"],
  "textAlign": "Center",
  "textTransform": "None",
  "letterSpacing": 0,
  "lineHeight": 0,
  "borderRadius": {
    "topLeft": 8,
    "topRight": 8,
    "bottomRight": 2,
    "bottomLeft": 2
  }
}
```

Exemple de JSON rétrocompatible (ancien projet) :

```json
{
  "fontFamily": "Segoe UI",
  "fontSize": 16,
  "foreground": "#0F2A30",
  "background": "Transparent",
  "borderColor": "Transparent",
  "borderWidth": 0,
  "borderStyle": "None",
  "shadowPreset": "None"
}
```

L'exemple camelCase ci-dessus représente la forme runtime/illustrative. Le test de persistance doit utiliser la forme réellement produite par `ModernProjectStore`, tandis que le test d'export doit vérifier la forme réellement produite par `Ft100SceneExporter`.

---

## 5. Sémantique de rendu

### 5.1 Mapping propriété → CSS

Chaque propriété du modèle se traduit en CSS selon le tableau suivant :

| Propriété modèle | Propriété CSS | Format |
|---|---|---|
| `FontFamily` | `font-family` | Valeur directe |
| `FontSize` | `font-size` | `{value}px` |
| `Foreground` | `color` | Valeur directe |
| `Background` | `background` | Valeur directe (ou `transparent` pour les formes) |
| `BorderColor` | `border-color` | Valeur directe |
| `BorderWidth` | `border-width` | `{value}px` |
| `BorderStyle` | `border-style` | Valeur en minuscules (ou `none` pour `"None"`) |
| `ShadowPreset` | `box-shadow` | Mapping : voir §5.6 |
| `Opacity` | `opacity` | Valeur directe (clampée 0–1) |
| `Rotation` | `transform: rotate()` | `rotate({value}deg)` |
| `FlipHorizontally` | `transform: scaleX()` | `scaleX(-1)` si true |
| `FlipVertically` | `transform: scaleY()` | `scaleY(-1)` si true |
| `FontWeight` | `font-weight` | Valeur directe |
| `FontStyle` | `font-style` | Valeur en minuscules |
| `TextDecoration` | `text-decoration` | Jointure des valeurs en minuscules : `"underline line-through"` |
| `TextAlign` | `text-align` | Valeur en minuscules |
| `TextTransform` | `text-transform` | Valeur en minuscules |
| `LetterSpacing` | `letter-spacing` | `{value}px` (omis si `0`) |
| `LineHeight` | `line-height` | `normal` si `0`, sinon `{value}px` |
| `BorderRadius` | `border-radius` | `TL TR BR BL` en px |
| `AdvancedCss` | — | Appended after all structured properties |

### 5.2 Ordre d'application CSS

Pour un Element+ (hors legacy static) :

1. Géométrie et position : `left`, `top`, `width`, `height`, `box-sizing`, `overflow`
2. Couleurs et fond : `color`, `background`
3. Typographie structurée : `font-family`, `font-size`, `font-weight`, `font-style`, `text-align`, `text-transform`, `letter-spacing`, `line-height`
4. Décoration : `text-decoration`
5. Bordure et rayon : `border`, `border-radius`
6. Ombre, opacité, transformation : `box-shadow`, `opacity`, `transform`
7. `AdvancedCss` comme override final
8. Invariants d'export et de namespace (non écrasables par AdvancedCss)

Le WebView (`MainWindow.WebViewScript.cs`) et l'exporteur (`Ft100SceneExporter.cs`) doivent produire les mêmes propriétés dans le même ordre. Les deux chemins d'export (`BuildElementInlineStyle` et `AppendElementCss`) doivent émettre les mêmes propriétés.

### 5.3 Formes SVG et border-radius

Le wrapper HTML d'une forme SVG peut recevoir `border-radius`, mais cela n'arrondit que le conteneur, pas la géométrie interne. Comportement par type :

- **`RoundedRectangle`** : possède déjà un `rx` codé en dur. Après cette feature, si `BorderRadius` est non-nul, le `rx` du SVG est dérivé de `BorderRadius.TopLeft` (ou de la valeur uniforme). Sinon, le `rx` par défaut actuel est conservé.
- **`Rectangle` et autres formes sans rayon natif** : le `border-radius` s'applique au wrapper HTML uniquement. Le SVG interne n'est pas modifié.
- **Formes à géométrie courbe** (`Ellipse`, `Circle`, `IndicatorLamp`, `Gauge`) : `border-radius` sur le wrapper peut produire un effet de clip — c'est un comportement CSS standard, documenté mais non restreint.
- **Composants `.sep`** : le rayon s'applique au wrapper. Les éléments internes du `.sep` ne sont pas modifiés.

### 5.4 Formes SVG et styles typographiques

Les propriétés typographiques (`FontWeight`, `FontStyle`, `TextDecoration`, `TextAlign`, `TextTransform`, `LetterSpacing`, `LineHeight`) s'appliquent au wrapper HTML. Pour les formes SVG qui contiennent du texte (comme `Motor`, `Breaker`, `Gauge`, `Valve`), ces propriétés ne modifient pas les labels SVG internes. C'est cohérent avec le comportement actuel où `FontFamily` et `FontSize` du wrapper ne modifient pas les labels SVG.

### 5.5 TextDecoration — sérialisation et rendu

Stockage : `IReadOnlyList<string>?` avec valeurs `"Underline"`, `"LineThrough"`, `"Overline"`.

Rendu CSS : les valeurs sont mises en minuscules et jointes par des espaces :
- `["Underline", "LineThrough"]` → `text-decoration: underline line-through`
- `[]` ou `null` → propriété CSS omise (pas de `text-decoration: none` forcé, ce qui laisse l'héritage CSS fonctionner)

### 5.6 ShadowPreset — mapping existant (inchangé)

| Preset | CSS |
|---|---|
| `"None"` | `none` |
| `"Soft"` | `0 8px 18px rgba(15,42,48,.16)` |
| `"Raised"` | `0 12px 26px rgba(15,42,48,.24)` |
| `"Inset"` | `inset 0 2px 8px rgba(15,42,48,.22)` |

### 5.7 Contrat TF100Web — vérification contre le code réel

Le dépôt local TF100Web vérifié est `F:\Projet\Git\TF100Web`. Le chemin moderne `frontend.views.scada_package_page` appelle `frontend.scada_builder_composition.load_composed_page` puis `_inject_scada_element_attrs`.

Le contrat réel est le suivant :

- `deploy_scada_builder` extrait le `.sb2`, copie le manifest racine vers `STATIC_ROOT/scada/manifest.json`, les pages vers `STATIC_ROOT/scada/pages/<page-id>/<page-id>.html`, les CSS vers `STATIC_ROOT/scada/pages/<page-id>/css/` et les images vers `STATIC_ROOT/scada/images/`.
- `load_composed_page` lit les champs manifest `Pages`, `Id`, `IncludeInBuild`, `PageType`/`Type`, `HeaderPageId`, `FooterPageId`, extrait le fragment `id="ft100-<page-id>"`, détecte le hash CSS et les dimensions par expressions régulières, puis retourne le HTML/CSS metadata.
- Le HTML et le CSS Element+ sont traités comme contenu statique opaque par ce chemin. Le code ne parse pas `ScadaElementStyle`, `data-scada-state-config` ou les déclarations CSS structurées.
- `scada_package_page` peut ajouter les attributs runtime de binding issus du manifest via `_inject_scada_element_attrs`, mais cette injection ne remplace pas le style inline/CSS et ne dépend pas des nouveaux champs de style.
- Par conséquent, aucun changement TF100Web n’est requis pour `FontWeight`, `FontStyle`, `TextDecoration`, `Foreground`, `BorderStyle` ou `BorderRadius` : ces propriétés doivent être émises correctement dans le HTML/CSS Builder, puis traversent le déploiement et la composition sans parsing serveur.

La preuve attendue dans le plan est un test d’intégration TF100Web qui déploie un fragment contenant les nouveaux styles, appelle `scada_package_page` et vérifie que le HTML retourné conserve les déclarations/attributs. Le test ne doit pas supposer que TF100Web comprend sémantiquement ces propriétés ; il doit vérifier leur opacité et leur conservation.

---

## 6. Conception de l'UI WPF

### 6.1 Direction d'interaction

L'interface adopte les conventions d'un inspecteur de mise en forme professionnel, avec les comportements suivants :

- **Boutons à état** : `Bold`, `Italic`, `Underline`, `Strikethrough` et les boutons d'alignement sont des `ToggleButton` ou `RadioButton` qui reflètent l'état actif.
- **Actions indisponibles** : désactivées avec tooltip explicatif (ex: l'alignement `Justify` est sans effet sur un `<input>`).
- **Regroupement par tâche** : sections `Typographie`, `Couleurs`, `Bordure`, `Ombre et effets`, `CSS avancé` — plutôt qu'une liste uniforme.
- **Options avancées contextuelles** : le mode par coin du rayon apparaît via un toggle à côté du champ de rayon uniforme.
- **Aperçu local** : un `TextBlock` ou `Border` dans le dialogue reflète les modifications en temps réel.
- **Tooltips** : chaque contrôle a un tooltip avec le nom de la propriété et sa valeur CSS équivalente.
- **Reset par section** : un bouton de réinitialisation par section restaure les défauts de cette section uniquement.
- **Accessibilité clavier** : tab order logique, les boutons de formatage sont activables au clavier.
- **Double indication d'état** : la couleur et l'état visuel ne sont jamais l'unique indication (un texte ou icône accompagne).

### 6.2 Structure commune des deux surfaces

L'onglet Style est organisé en sections, dans cet ordre :

1. **Typographie**
   - Ligne de formatage rapide : `ToggleButton` Bold, Italic, Underline, Strikethrough
   - Alignement : `RadioButton` Left, Center, Right, Justify
   - `ComboBox` : famille de police
   - `TextBox` : taille de police
   - Ligne avancée : transformation (`None`/`Uppercase`/`Lowercase`/`Capitalize`), espacement des lettres, interligne

2. **Couleurs**
   - `ColorPickerField` : couleur du texte (`Foreground`) — **nouveau**
   - `ColorPickerField` : couleur de fond (`Background`)

3. **Bordure**
   - `ColorPickerField` : couleur de bordure (avec case à cocher "Transparent" dans le dialogue)
   - `ComboBox` : style de bordure (9 valeurs)
   - `TextBox` : largeur de bordure (px)
   - `TextBox` : rayon uniforme (px)
   - `ToggleButton` : activer le mode par coin → quatre `TextBox` : TopLeft, TopRight, BottomRight, BottomLeft

4. **Ombre et effets**
   - `RadioButton` × 4 : Aucun, Soft, Lift, Inset (conservés tels quels)
   - `TextBox` : opacité (0–1)
   - `TextBox` : rotation (degrés)
   - Réservation pour évolution future (filtres, backdrop)

5. **CSS avancé**
   - `TextBox` multiligne conservé
   - Aide textuelle expliquant que ce champ surcharge les valeurs structurées

Chaque section possède une icône (`Icon.Property.*`), un en-tête, une disposition stable, un état disabled cohérent, et un bouton de reset local.

### 6.3 Icônes

Créer les clés suivantes dans `src/ScadaBuilderV2.App/Resources/Icons.xaml` :

```text
Icon.Property.Typography     — section Typographie
Icon.Property.FontFamily     — famille de police
Icon.Property.FontWeight     — graisse (Bold)
Icon.Property.FontStyle      — style (Italic)
Icon.Property.TextDecoration — décorations (Underline, barré)
Icon.Property.TextAlign      — alignement
Icon.Property.Colors         — section Couleurs
Icon.Property.Border         — section Bordure
Icon.Property.BorderRadius   — rayon de bordure
Icon.Property.Shadow         — section Ombre et effets
Icon.Property.Transform      — transformation (opacité, rotation)
Icon.Property.AdvancedCss    — CSS avancé
Icon.Property.Reset          — reset de section
```

Chaque ressource :
- Utilise le style vectoriel interne existant (primitives `GeometryDrawing` avec `Icon.OutlinePen`)
- Est référencée par une clé sémantique (pas de nom de fichier temporaire)
- Possède un tooltip en français
- Est lisible à 16–20 px (taille d'icône de propriété)
- Ne repose pas uniquement sur la couleur pour distinguer son état

### 6.4 Flux d'édition

#### Ouverture (dialogue modal et panneau docké)

1. Lire `currentElement.Style` ou le défaut du type d'élément (`DefaultText` / `DefaultInput`).
2. Normaliser les valeurs (ex: `TextDecoration` null → `[]`, `BorderRadius` null → `ScadaBorderRadius` zéro).
3. Peupler les contrôles structurés avec les valeurs normalisées.
4. Si `BorderRadius.IsUniform`, activer le mode uniforme ; sinon activer le mode par coin et peupler les quatre champs.
5. Les boutons de formatage reflètent leur état actif/inactif.
6. L'aperçu local reçoit une copie du style.
7. Le panneau docké ignore l'élément si `_isUpdatingElementProperties` est true (garde existante).

#### Modification

1. Chaque contrôle met à jour l'aperçu local sans écrire dans le projet.
2. Les valeurs invalides (ex: `LetterSpacing` hors [-10, 50], `FontSize` < 6) affichent un message dans `ValidationText` et bloquent l'application.
3. Les champs de couleur utilisent `ColorPickerField` avec son flux `Annuler`/`Enregistrer` existant.
4. Le reset de section remet uniquement cette section à ses valeurs par défaut, sans toucher aux autres sections ni aux autres onglets.

#### Enregistrement

1. **Dialogue modal** : `OnApplyClick` construit `ElementPropertiesDialogResult` (étendu avec les nouveaux champs), ferme avec `DialogResult = true`. `MainWindow` reconstruit `ScadaElementStyle` depuis le résultat puis appelle `CommitModernElementProperties`.
2. **Panneau docké** : `OnElementPropertyChanged` reconstruit `ScadaElementStyle` avec `style with { ... }` puis appelle `CommitModernElementProperties`.
3. `CommitModernElementProperties` exécute une mutation de scène unique, pousse l'undo, marque le dirty state, rafraîchit la preview et l'export.
4. Les deux chemins produisent le même `ScadaElementStyle` final.

### 6.5 Extension de `ElementPropertiesDialogResult`

Le record existant (ligne 451 de `ElementPropertiesDialog.xaml.cs`) reçoit les champs suivants avec valeurs par défaut :

```csharp
public sealed record ElementPropertiesDialogResult(
    // ... champs existants inchangés ...
    // Nouveaux champs Style
    string Foreground = "#0F2A30",
    string FontWeight = "Normal",
    string FontStyle = "Normal",
    IReadOnlyList<string>? TextDecoration = null,
    string TextAlign = "Left",
    string TextTransform = "None",
    double LetterSpacing = 0,
    double LineHeight = 0,
    ScadaBorderRadius? BorderRadius = null)
```

L'ajout de `Foreground` corrige une lacune existante où la couleur du texte n'était pas éditable.

---

## 7. Validation et tests

### 7.1 Tests domaine/persistance

| Test | Fichier |
|---|---|
| Ancien JSON sans nouveaux champs → défauts appliqués | `ScadaSceneModelsTests.cs` |
| Défauts identiques au rendu historique (bold off, italic off, etc.) | `ScadaSceneModelsTests.cs` |
| Sauvegarde/recharge de toutes les nouvelles propriétés | `ModernProjectStoreTests.cs` |
| Validation des bornes : `FontWeight` 100–900 par pas de 100 | `ScadaSceneModelsTests.cs` |
| Validation des bornes : `LetterSpacing` [-10, 50] | `ScadaSceneModelsTests.cs` |
| Validation des bornes : `LineHeight` ≥ 0 | `ScadaSceneModelsTests.cs` |
| `BorderRadius.Normalized()` écrête les négatifs | `ScadaSceneModelsTests.cs` |
| `BorderRadius.IsUniform` : 4 valeurs égales = true | `ScadaSceneModelsTests.cs` |
| `TextDecoration` : round-trip `["Underline", "LineThrough"]` | `ScadaSceneModelsTests.cs` |

### 7.2 Tests UI/contrat

| Test | Fichier |
|---|---|
| Contrôles présents dans les deux XAML (modal + docké) | `WebViewContextMenuScriptTests.cs` |
| `ElementPropertiesDialogResult` et panneau docké écrivent les mêmes champs | `WebViewContextMenuScriptTests.cs` |
| Bold, Italic, Underline et combinaisons | `WebViewContextMenuScriptTests.cs` |
| `Inset` et tous les styles de bordure déclarés | `WebViewContextMenuScriptTests.cs` |
| Rayon uniforme et asymétrique | `WebViewContextMenuScriptTests.cs` |
| Reset, focus, tooltip et état disabled | `WebViewContextMenuScriptTests.cs` |
| Résolution de chaque `Icon.Property.*` dans `Icons.xaml` | Nouveau test ou extension existant |

### 7.3 Tests preview/export

| Test | Fichier |
|---|---|
| Mêmes styles CSS dans WebView et FT100 (les deux chemins) | `Ft100SceneExporterTests.cs` |
| `AdvancedCss` reste le dernier override après les nouvelles propriétés | `Ft100SceneExporterTests.cs` |
| `Inset` est présent dans le HTML/CSS exporté | `Ft100SceneExporterTests.cs` |
| `font-weight`, `font-style`, `text-decoration` dans le CSS exporté | `Ft100SceneExporterTests.cs` |
| Les quatre rayons sont présents avec les bonnes unités | `Ft100SceneExporterTests.cs` |
| Les formes SVG conservent leur géométrie sauf `RoundedRectangle` | `Ft100SceneExporterTests.cs` |
| Aucun élément d'aperçu (overlay éditeur) n'est exporté | `Ft100SceneExporterTests.cs` |
| `opacity`, `transform` émis dans `AppendElementCss` (bug corrigé) | `Ft100SceneExporterTests.cs` |

### 7.4 Tests historique

| Test | Fichier |
|---|---|
| Undo/redo d'un changement de `FontWeight` | `EditorHistoryServiceTests.cs` |
| Undo/redo d'un changement de `BorderStyle` vers `Inset` | `EditorHistoryServiceTests.cs` |
| Undo/redo d'un changement de `BorderRadius` uniforme et par coin | `EditorHistoryServiceTests.cs` |
| Undo/redo d'une combinaison de décorations | `EditorHistoryServiceTests.cs` |

---

## 8. Fichiers et responsabilités

| Surface | Fichier | Modification |
|---|---|---|
| **Domain** | `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs` | Ajout des champs à `ScadaElementStyle`, création de `ScadaBorderRadius` |
| **Dialog XAML** | `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml` | Refonte de l'onglet Style (sections, nouveaux contrôles) |
| **Dialog code** | `src/ScadaBuilderV2.App/ElementPropertiesDialog.xaml.cs` | Extension de `ElementPropertiesDialogResult`, lecture/validation des nouveaux champs |
| **Dock XAML** | `src/ScadaBuilderV2.App/MainWindow.xaml` | Refonte de l'onglet Style (identique au dialogue) |
| **Dock code** | `src/ScadaBuilderV2.App/MainWindow.xaml.cs` | Extension de `OnElementPropertyChanged` pour les nouveaux champs |
| **Preview** | `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs` | Émission des nouvelles propriétés CSS dans `renderModernElements` |
| **Export** | `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs` | Émission des nouvelles propriétés dans `BuildElementInlineStyle` et `AppendElementCss` |
| **Icônes** | `src/ScadaBuilderV2.App/Resources/Icons.xaml` | Ajout des 13 clés `Icon.Property.*` |
| **Contrat** | `docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md` | Ajout des règles pour les nouvelles propriétés |
| **Contrat TF100Web** | `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` | Confirmation du transport opaque HTML/CSS des nouveaux styles et des conventions de casse |
| **Guidance agents** | `CLAUDE.md`, `codex.md` | Protection du contrat d’intake TF100Web contre les régressions |
| **Tests modèle** | `tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs` | Tests de défauts, validation, bornes, sérialisation |
| **Tests UI** | `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs` | Tests de contrat UI |
| **Tests export** | `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs` | Tests de parité preview/export |
| **Tests historique** | `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs` | Tests undo/redo |
| **Test intégration TF100Web** | `F:\Projet\Git\TF100Web\frontend\tests_scada_deploy.py` | Vérification du déploiement et de la conservation HTML/CSS via `scada_package_page` |

---

## 9. Migration et rétrocompatibilité

- Aucune migration destructive. Les anciens styles restent valides.
- `AdvancedCss` n'est pas supprimé ni modifié.
- Les nouveaux champs ne sont pas garantis absents lorsqu'ils valent leur défaut : `ModernProjectStore` ne configure pas `DefaultIgnoreCondition.WhenWritingDefault`. Le comportement exact de présence/absence doit être couvert par le test de sérialisation ; la compatibilité repose sur l'ignorance des champs inconnus et les defaults de désérialisation.
- Les rayons codés en dur des formes SVG ne sont pas automatiquement convertis en `BorderRadius` utilisateur. Une conversion silencieuse est interdite.
- Les projets ouverts avec une ancienne version de SCADA Builder V2 ignoreront les nouveaux champs JSON inconnus au niveau du lecteur, sous réserve d’un test explicite avec la version de sérialiseur effectivement utilisée.

---

## 10. Hors scope

- Éditeur CSS libre complet (type DevTools)
- Import de propriétés CSS arbitraires depuis le legacy HTML
- Bibliothèque d'icônes tierce (Lucide, Fluent UI, etc.)
- Refonte globale de l'application hors des deux surfaces de propriétés
- Modification du runtime TF100Web/Django
- Modification du contrat de sélection, des overlays, ou du format `.sep` (hors prise en charge des nouveaux champs de style en lecture/écriture)
- Gestion du `border-radius` sur les primitives SVG internes des formes (hors `RoundedRectangle`)
- Support du multiplicateur `line-height` sans unité

---

## 11. Critères d'acceptation

Le design est implémentable lorsque :

1. Un texte peut être configuré en **bold**, *italic*, <u>underline</u>, ~~barré~~ et toute combinaison de décorations, sans `AdvancedCss`.
2. La couleur du texte (`Foreground`) est éditable dans les deux surfaces.
3. Les 9 styles de bordure (`Solid`, `Dashed`, `Dotted`, `Double`, `Groove`, `Ridge`, `Inset`, `Outset`, `None`) sont sélectionnables et conservés après rechargement.
4. `Inset` est correctement rendu dans la preview et émis dans l'export.
5. Un rayon uniforme et quatre rayons distincts sont persistés, prévisualisés et exportés.
6. Le dialogue modal et le panneau docké produisent le même `ScadaElementStyle` pour les mêmes entrées.
7. La preview WebView et l'export FT100 consomment les mêmes valeurs et produisent un CSS équivalent.
8. Les deux chemins d'export (`BuildElementInlineStyle` et `AppendElementCss`) émettent les mêmes propriétés (bug d'incohérence `opacity`/`transform` corrigé).
9. Les anciens projets (sans les nouveaux champs JSON) s'ouvrent et se rendent comme avant.
10. Les icônes `Icon.Property.*` sont des ressources vectorielles résolues, sémantiques et cohérentes avec les familles existantes.
11. Les tests couvrent : modèle (défauts, bornes, sérialisation), UI (contrôles, contrat, icônes), rendu (WebView + export), historique (undo/redo), et rétrocompatibilité.
12. Le reset par section restaure les défauts sans affecter les autres sections ni les autres onglets.

---

## 12. Décision finale sur l'aperçu vivant

**Q1 résolue — l'aperçu vivant est obligatoire dans la première itération.**

L'aperçu local WPF fait partie de la direction UI D17 et des critères d'acceptation. Il doit refléter les valeurs en cours d'édition avant l'enregistrement, notamment :

- famille, taille, graisse, style et décorations ;
- alignement, transformation, espacement et interligne ;
- couleur du texte et du fond ;
- type, largeur, couleur et rayon de bordure ;
- ombre, opacité et rotation lorsque ces propriétés sont visibles dans l'aperçu.

L'aperçu est une projection temporaire, non une mutation de scène. Il ne doit pas réutiliser le DOM runtime, ne doit pas être ajouté à `HtmlCode`/`CssCode` et ne doit jamais être exporté dans `.sep` ou `.sb2`. Cette décision augmente la portée UI de la première itération, mais elle est nécessaire pour atteindre la qualité d'interaction inspirée de Microsoft Word demandée par le produit.
