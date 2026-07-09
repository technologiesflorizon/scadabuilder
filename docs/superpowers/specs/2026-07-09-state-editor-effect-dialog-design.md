# Éditeur d'état — Dialogue de configuration d'effet & correctif ColorPicker (design)

Date: 2026-07-09
Status: Approved design — prêt pour planification d'implémentation
Document version: `V2.1.3.0000`
Portée: SCADA Builder V2 — `ElementStateRuleDialog` + `ColorPickerDialog`
Référence: `docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-09 | `V2.1.3.0000` | `c9f9b17` | Création de la spec approuvée pour le dialogue de configuration d'effet, l'extraction des types UI partagés et le correctif de teinte du ColorPicker. |

## 1. Problème

Trois défauts dans l'éditeur d'états actuel :

1. **Slider de teinte gelé.** Dans `ColorPickerDialog`, quand la couleur de départ est
   blanche/noire/grise (saturation = 0), bouger le slider de teinte n'a aucun effet
   visible : la couleur reconstruite a saturation 0, puis `ToHsv()` recalcule `hue = 0`
   pour les couleurs sans chroma, ce qui ramène le slider à 0.
2. **Le flux Ajouter n'ouvre aucun dialogue.** `OnAddActiveEffectClick` ajoute directement
   le type sélectionné à `_activeKinds` sans donner à l'utilisateur l'occasion de
   configurer les paramètres. Les paramètres sont ensuite saisis dans un panneau inline
   sous la liste — pas de validation, pas de distinction claire entre consultation et
   édition.
3. **Double-clic sur un effet ne fait qu'afficher l'éditeur inline.** Il n'ouvre pas de
   dialogue dédié, contrairement au double-clic sur une règle d'état qui ouvre bien
   `ElementStateRuleDialog`.

## 2. Objectif

Remplacer le panneau d'édition inline par un **dialogue modal `EffectEditorDialog`**
utilisé pour l'ajout ET la modification des effets. Corriger le bug de teinte dans
`ColorPickerDialog`. Le dialogue sera ouvert :

- Par le bouton **« + Ajouter »** (mode Ajout : type choisi dans le dialogue)
- Par **double-clic** sur un effet dans la liste (mode Édition)
- Par le bouton **« Éditer »** (mode Édition, alternative au double-clic)

## 3. Décisions

| # | Décision |
|---|---|
| D1 | **Supprimer complètement** `EffectEditorPanel` et tous ses sous-panneaux inline du XAML. |
| D2 | **Un seul dialogue** `EffectEditorDialog` pour Ajouter et Éditer (approche A). |
| D3 | Le choix du type se fait **dans le dialogue** en mode Ajout. Le bouton « + Ajouter » n'a pas de ComboBox préalable — le ComboBox `EffectTypeComboBox` actuel est supprimé. |
| D4 | Les valeurs d'effet sont stockées dans un `Dictionary<EffectKind, ScadaEffectBlock>` interne, plus via des contrôles UI éparpillés. |
| D5 | `BuildEffectFromUi` est adaptée pour fusionner les blocs partiels depuis le dictionnaire. |
| D6 | Le bug de teinte est corrigé en : (a) forçant `_saturation = 1.0` et `_value = 1.0` quand elles sont à 0 dans `OnHueSliderChanged` ; (b) ajoutant un paramètre `fallbackHue` à `ToHsv` (qui reste `static`) pour conserver la teinte quand `delta == 0`. |
| D7 | Bouton Éditer conservé pour accessibilité (alternative au double-clic). |

## 4. Architecture & fichiers

| Fichier | Action |
|---|---|
| `src/ScadaBuilderV2.App/EffectKind.cs` | **Nouveau** — `internal enum EffectKind` extrait de `ElementStateRuleDialog` |
| `src/ScadaBuilderV2.App/EffectTypeItem.cs` | **Nouveau** — `internal sealed record EffectTypeItem(EffectKind Kind, string Label)` extrait |
| `src/ScadaBuilderV2.App/ColorPickerDialog.xaml` | Conserver (pas de changement XAML nécessaire) |
| `src/ScadaBuilderV2.App/ColorPickerDialog.xaml.cs` | Modifier — `OnHueSliderChanged`, `ToHsv` (reste `static`, ajout paramètre `fallbackHue`) |
| `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml` | Modifier — supprimer `EffectEditorPanel` inline, supprimer `EffectTypeComboBox` |
| `src/ScadaBuilderV2.App/ElementStateRuleDialog.xaml.cs` | Modifier — supprimer les types privés extraits, remplacer `ShowEffectEditor`/`OnAddActiveEffectClick`/`OnEditActiveEffectClick`, adapter `BuildEffectFromUi` |
| `src/ScadaBuilderV2.App/EffectEditorDialog.xaml` | **Nouveau** — dialogue modal |
| `src/ScadaBuilderV2.App/EffectEditorDialog.xaml.cs` | **Nouveau** — code-behind |

**Extraction de `EffectKind` :** Actuellement `private enum` dans `ElementStateRuleDialog`.
Extrait en `internal enum EffectKind` dans son propre fichier pour être partagé entre
`ElementStateRuleDialog` et `EffectEditorDialog`. Le type `EffectTypeItem` suit la même
extraction. Ces types restent internes à l'assemblage `.App` (pas d'impact domaine).

Le dialogue réutilise `ColorPickerField` pour les champs couleur.

## 5. Flux d'interaction

### 5.1 Ajout d'un effet

1. L'utilisateur clique « + Ajouter »
2. `EffectEditorDialog` s'ouvre en mode Ajout (`existingEffect = null`, `effectKind = null`)
3. Le dialogue affiche la liste des types disponibles (pas encore dans `_activeKinds`)
4. L'utilisateur sélectionne un type → les champs de config spécifiques s'affichent
5. L'utilisateur paramètre, clique « Ajouter »
6. Le dialogue valide les champs obligatoires, ferme avec `DialogResult = true`
7. `ElementStateRuleDialog` récupère `ResultKind` et `ResultEffect`, les ajoute à
   `_activeKinds`, rafraîchit la liste, sélectionne le nouvel item, met à jour la preview

### 5.2 Édition d'un effet

1. L'utilisateur double-clique un effet dans `ActiveEffectsListBox` (ou le sélectionne + Éditer)
2. `EffectEditorDialog` s'ouvre en mode Édition (`existingEffect` = valeurs stockées,
   `effectKind` = type fixe)
3. Le type est affiché en lecture seule
4. L'utilisateur modifie les paramètres, clique « Enregistrer »
5. Retour → mise à jour du dictionnaire → rafraîchissement liste + preview

### 5.3 Suppression d'un effet

Inchangé : clic sur « Supprimer » retire le type de `_activeKinds` et du dictionnaire.

## 6. Interface du dialogue `EffectEditorDialog`

**Entrée (constructeur) :**

| Paramètre | Type | Description |
|---|---|---|
| `existingEffect` | `ScadaEffectBlock?` | Valeurs actuelles (null en mode Ajout) |
| `effectKind` | `EffectKind?` | Type fixe (null en mode Ajout, renseigné en mode Édition) |
| `availableKinds` | `IReadOnlySet<EffectKind>` | Types pas encore utilisés (mode Ajout seulement) |

**Sortie (propriétés publiques) :**

| Propriété | Type | Description |
|---|---|---|
| `ResultKind` | `EffectKind` | Le type configuré |
| `ResultEffect` | `ScadaEffectBlock` | Le bloc partiel avec les valeurs saisies |

### 6.1 Layout XAML

```
┌──────────────────────────────────────────┐
│ Configurer un effet                      │
│                                          │
│ Type                                     │
│ ┌──────────────────────────────────────┐ │
│ │ ○ Couleur de fond                    │ │  ← ListBox, visible seulement en mode Ajout
│ │ ○ Bordure                            │ │    (remplacé par label en mode Édition)
│ │ ○ Opacité                            │ │
│ │ ...                                  │ │
│ └──────────────────────────────────────┘ │
│                                          │
│ ── Paramètres ────────────────────────── │
│                                          │
│ [Panneau dynamique selon le type]        │
│                                          │
│ ──────────────────────────────────────── │
│                      [Annuler] [Ajouter] │
└──────────────────────────────────────────┘
```

### 6.2 Champs par type d'effet

| Type | Champs |
|---|---|
| Couleur de fond | `ColorPickerField` (couleur CSS) |
| Bordure | `ColorPickerField` (couleur) + `TextBox` (largeur px) |
| Texte | `TextBox` (contenu) + `ColorPickerField` (couleur) + `CheckBox` (visible) |
| Visibilité élément | `CheckBox` (élément visible) |
| Opacité | `Slider` 0–1 avec `TextBlock` affichant la valeur |
| Rotation | `TextBox` (degrés) |
| Animation | `ComboBox` (`None`/`Blink`/`Pulse`/`Halo`/`Spin`) |
| Filtre de couleur | `ColorPickerField` (couleur) + `Slider` (opacité 0-1) + `CheckBox` (halo) + `ColorPickerField` (couleur halo, visible si halo coché) |

### 6.3 Validation par type

| Type | Règle |
|---|---|
| Couleur de fond | Couleur CSS valide (hex, rgb, ou nom — via `ColorPickerDialog.TryParseCssColor`) |
| Bordure | Largeur > 0, couleur CSS valide |
| Texte | Aucun champ obligatoire |
| Visibilité élément | Aucun champ obligatoire |
| Opacité | Toujours valide (slider borné 0–1) |
| Rotation | Nombre valide (entier ou décimal) |
| Animation | Toujours valide (ComboBox) |
| Filtre de couleur | Couleur valide, opacité valide |

### 6.4 Taille

- Largeur : 420 px (même largeur que `ColorPickerDialog`)
- Hauteur : 440 px (le panneau Filtre de couleur est le plus long)
- Redimensionnable : non (`ResizeMode="NoResize"`)
- Centré sur le parent (`WindowStartupLocation="CenterOwner"`)

## 7. Correction du bug de teinte — `ColorPickerDialog`

### 7.1 `OnHueSliderChanged`

Forcer `_saturation = 1.0` et `_value = 1.0` avant d'appeler `FromHsv` pour que la
nouvelle teinte produise une couleur visible, même quand la saturation ou la valeur
précédentes étaient à 0 (blanc, noir, gris) :

```csharp
private void OnHueSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    if (_isUpdatingColorControls || !AreColorControlsReady())
        return;

    _hue = HueSlider.Value;
    if (_saturation <= 0) _saturation = 1.0;  // sortir du gris/blanc
    if (_value <= 0) _value = 1.0;            // sortir du noir
    var color = FromHsv(_hue, _saturation, _value);
    UpdateColorControls(color, updateText: true);
}
```

### 7.2 `ToHsv` — signature et logique

`ToHsv` est actuellement `static` donc ne peut pas lire le champ d'instance `_hue`.
Ajouter un paramètre `fallbackHue` pour que l'appelant puisse passer la teinte à
conserver quand `delta == 0` :

```csharp
// Signature actuelle :
private static (double Hue, double Saturation, double Value) ToHsv(Color color)

// Nouvelle signature :
private static (double Hue, double Saturation, double Value) ToHsv(Color color, double fallbackHue = 0)
```

Corps modifié :

```csharp
var hue = delta == 0
    ? fallbackHue               // conserve la teinte passée par l'appelant
    : max == red ? 60 * (((green - blue) / delta) % 6)
    : max == green ? 60 * (((blue - red) / delta) + 2)
    : 60 * (((red - green) / delta) + 4);
```

Et dans `UpdateColorControls`, l'appel devient :

```csharp
(var hue, var saturation, var value) = ToHsv(color, fallbackHue: _hue);
```

**Pourquoi `fallbackHue = 0` (défaut) :** garde la méthode utilisable sans état
d'instance pour les futurs appels simples. Dans le chemin actuel, les changements de
RGB, de texte, de swatch et de saturation/valeur passent par `UpdateColorControls`;
cet appel doit fournir `fallbackHue: _hue` afin que les couleurs achromatiques ne
réinitialisent pas la teinte sélectionnée. Le défaut garde un comportement prévisible
pour tout autre appel qui ne veut pas conserver une teinte existante.

## 8. Modifications de `ElementStateRuleDialog`

### 8.1 XAML — Suppressions

- `EffectEditorPanel` et tous ses sous-panneaux :
  `BackgroundColorEditor`, `BorderEditor`, `TextEditor`, `ElementVisibleEditor`,
  `OpacityEditor`, `RotationEditor`, `AnimationEditor`, `ColorFilterEditor`
- `Border` englobant (lignes 90–143 actuelles)
- `EffectTypeComboBox`

### 8.2 XAML — Nouveau layout zone effets

```xml
<TextBlock Text="Modifications d'apparence" FontWeight="SemiBold" .../>
<ListBox x:Name="ActiveEffectsListBox" Height="150" ... MouseDoubleClick="OnEditActiveEffectClick"/>
<StackPanel Orientation="Horizontal" Margin="0,4,0,8">
    <Button Content="+ Ajouter" Click="OnAddActiveEffectClick" Margin="0,0,6,0"/>
    <Button Content="Éditer" Click="OnEditActiveEffectClick" Margin="0,0,6,0"/>
    <Button Content="Supprimer" Click="OnRemoveActiveEffectClick"/>
</StackPanel>
```

### 8.3 Code-behind — Changements

| Méthode | Modification |
|---|---|
| `OnAddActiveEffectClick` | Ouvre `EffectEditorDialog` en mode Ajout. Si OK, ajoute `ResultKind` au dictionnaire et à `_activeKinds`, sélectionne l'item dans la liste |
| `OnEditActiveEffectClick` | Méthode unique pour double-clic ET bouton Éditer. Ouvre `EffectEditorDialog` en mode Édition avec les valeurs du dictionnaire |
| `ShowEffectEditor` | **Supprimée** |
| `BuildEffectSummary` | Conservée, lit depuis le dictionnaire |
| `BuildEffectFromUi` | Adaptée — fusionne les `ScadaEffectBlock` partiels du dictionnaire au lieu de lire des contrôles UI |
| `UpdatePreview` | Conservée |
| `LoadEffect` | Adaptée — peuple le dictionnaire au lieu de positionner des contrôles UI |

### 8.4 Stockage interne

```csharp
private readonly Dictionary<EffectKind, ScadaEffectBlock> _effectValues = new();
```

Chaque entrée stocke UN `ScadaEffectBlock` avec uniquement les propriétés pertinentes
pour ce type d'effet. `BuildEffectFromUi` les fusionne en un seul bloc :

```csharp
private ScadaEffectBlock BuildEffectFromUi()
{
    var merged = ScadaEffectBlock.Empty;
    foreach (var (_, block) in _effectValues)
    {
        merged = merged with
        {
            BackgroundColor = block.BackgroundColor ?? merged.BackgroundColor,
            BorderColor = block.BorderColor ?? merged.BorderColor,
            // ... chaque propriété
        };
    }
    return merged;
}
```

## 9. Garde-fou export — invariant du contrat

Ce changement est **strictement esthétique/UX** : il modifie la surface d'édition (comment
l'utilisateur saisit les paramètres d'effet), pas le modèle de données ni le contrat
d'export.

**Ce qui ne change pas :**

| Périmètre | Statut |
|---|---|
| `ScadaEffectBlock` | **Inchangé** — le record et ses propriétés restent identiques |
| `ScadaStateRule` | **Inchangé** — le record reste `Id + Name + Enabled + Expression + Effect` |
| `ScadaElementStateConfig` | **Inchangé** |
| `ElementStateRuleDialog.Result` | **Inchangé** — expose toujours un `ScadaStateRule?` avec un `ScadaEffectBlock` complet |
| `BuildEffectFromUi()` | **Refactorisée** — lit depuis le dictionnaire interne au lieu de contrôles UI. Produit le même `ScadaEffectBlock` cumulatif qu'avant |
| Export `.sb2` | **Aucun impact** — le `Ft100SceneExporter` lit `ScadaElementStateConfig` via le modèle domaine, qui est inchangé. Le dialogue `EffectEditorDialog` est un artefact UI WPF, absent du pipeline d'export |
| Export `.sep` | **Aucun impact** — même raison |

**Vérification à effectuer avant merge :**

1. Lancer `dotnet test --filter "FullyQualifiedName~Ft100SceneExporterTests"` —
   les tests d'export existants doivent rester verts.
2. Vérifier qu'un export `.sb2` d'une scène avec états produit le même `manifest.json`
   et les mêmes `ScadaElementStateConfig` sérialisés qu'avant le changement
   (test d'idempotence ou comparaison manuelle sur `win00009`).
3. Reconstruire `BuildEffectFromUi()` pour qu'elle produise un `ScadaEffectBlock`
   identique à l'ancienne version pour les mêmes entrées.

## 10. Hors scope

- Modification du modèle `ScadaEffectBlock` ou `ScadaStateRule` (le domaine est stable)
- Bouton Test (déjà dans la spec `2026-07-07-element-plus-state-command-events-design.md`,
  itération future)
- Refonte du panneau propriétés dans `MainWindow.xaml.cs`
- Changement du comportement de `ElementPropertiesDialog`

## 11. Contraintes projet

- Artefacts d'éditeur (aperçu, dialogue) ne doivent pas fuiter dans l'export `.sb2`/`.sep`
- APIs publiques : XML docs
- Tests : couvrir le dialogue `EffectEditorDialog` (ouverture Ajout/Édition, validation,
  retour des valeurs) et la correction de teinte (`ToHsv` avec delta=0). Si le projet de
  tests actuel ne dispose pas d'un harness STA/WPF fiable, extraire la logique de
  construction/validation d'effet dans des méthodes testables ou ajouter des tests
  textuels ciblés sur le XAML/code-behind en complément des tests d'export.
