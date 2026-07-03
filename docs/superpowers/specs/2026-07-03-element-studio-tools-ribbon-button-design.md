# Design: bouton Studio Element+ dans l'onglet Tools du ruban

Date: 2026-07-03
Statut: Approuve (brainstorming)

## Probleme

Studio Element+ ne peut etre ouvert que depuis la palette d'outils laterale
(`tool.element-studio` dans `RibbonCommandCatalog.CreateToolPalette()`), ou depuis
le menu contextuel sur un element legacy selectionne. Il n'existe aucun bouton
dans l'onglet **Tools** du ruban superieur pour l'ouvrir, alors que la palette
laterale est censee ne contenir que des outils d'edition canvas "a venir"
(actuellement tous desactives). La commande active `tool.element-studio` y est
donc hors de propos semantiquement, et le test `ToolPaletteUsesSemanticCommandCatalog`
echoue deja car il attend une palette sans cette entree.

## Solution retenue

Deplacer la commande `tool.element-studio` de la palette laterale vers le
groupe "Configuration" de l'onglet **Tools** du ruban superieur, aux cotes de
`tool.settings`. Aucune nouvelle logique de lancement n'est necessaire: le
handler existant `OpenElementStudioFromToolPaletteAsync()` (branche sur
`ExecuteRibbonCommand`, cas `"tool.element-studio"`) fonctionne deja
independamment de l'origine du clic (palette ou ruban).

### Composants touches

- **`RibbonCommandCatalog.cs`** (Application)
  - Retirer `Enabled("tool.element-studio", ...)` de `CreateToolPalette()`.
  - Ajouter la meme entree (activee) dans le groupe "Configuration" de
    `CreateDefault()["Tools"]`.
- **`Icons.xaml`** (App/Resources)
  - Ajouter `Icon.Tool.ElementStudio`, un `DrawingImage` suivant le style
    existant (geometrie vectorielle simple sur grille ~24x24,
    `Icon.OutlinePen` partage) — pictogramme evoquant un composant/bloc
    reutilisable (ex. piece de puzzle).
- **`MainWindow.xaml.cs`**
  - Aucun changement de logique de lancement. `ExecuteRibbonCommand` route
    deja `"tool.element-studio"` vers `OpenElementStudioFromToolPaletteAsync()`.
- **`RibbonCommandCatalogTests.cs`**
  - Mettre a jour `ToolPaletteUsesSemanticCommandCatalog` : la liste attendue
    de commandes de la palette ne doit plus contenir `tool.element-studio`.
  - Ajouter une assertion verifiant que le tab `Tools` contient
    `tool.element-studio`, active, avec `IconKey == "Icon.Tool.ElementStudio"`.
  - `DisabledCommandsExposeReason` doit continuer a passer (la commande
    deplacee reste activee, elle ne modifie pas le nombre de commandes
    desactivees necessaires par le test).

## Comportement du bouton

Toujours actif, sans condition de selection. Comportement identique a
l'existant :
- Si un ou plusieurs elements legacy sont selectionnes dans la scene, le clic
  ouvre Studio Element+ avec un package d'import genere depuis la selection
  (`OpenSelectedLegacyInElementStudioAsync`).
- Sinon, Studio Element+ s'ouvre sans package d'import.
- La resolution de lancement (executable package ou `dotnet run` du projet
  Element Studio), la gestion des echecs et le logging dans
  `element-studio-launch.log` sont inchanges.

## Flux utilisateur

Utilisateur ouvre l'onglet "Tools" du ruban -> groupe "Configuration" affiche
"Configurer" (desactive, a venir) et "Studio E+" (active, nouvelle icone) ->
clic -> `ExecuteRibbonCommand("tool.element-studio")` -> chemin de lancement
existant.

## Tests

- `RibbonCommandCatalogTests.ToolPaletteUsesSemanticCommandCatalog` : mise a
  jour de la liste d'ids attendue (retrait de `tool.element-studio`).
- Nouveau test ou assertion ajoutee verifiant la presence et l'etat de
  `tool.element-studio` dans le tab `Tools` de `CreateDefault()`.
- Suite complete `dotnet test ScadaBuilderV2.sln --no-restore` pour non-
  regression (`DisabledCommandsExposeReason`,
  `DefaultCatalogUsesStableUniqueCommandIds`,
  `DefaultCatalogRequiresSemanticIconKeys`).

## Hors perimetre

- Pas de nouvel onglet dedie.
- Pas de duplication de la commande dans la palette laterale et le ruban.
- Pas de changement au comportement de lancement, a la resolution de
  l'executable/projet, ni au format du log de lancement.
