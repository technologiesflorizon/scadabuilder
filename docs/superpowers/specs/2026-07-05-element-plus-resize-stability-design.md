# Design: fiabilisation de l'outil resize Element+

Date: 2026-07-05
Statut: Approuve (brainstorming)

## Probleme

L'outil de resize des objets Element+ dans le canvas WebView2 de SCADA
Builder V2 (`MainWindow.WebViewScript.cs`, mode `modernDrag`) presente
cinq defauts constates :

1. **Boite de resize plus grande que la forme rendue** : les wrappers
   `Kind === 'Shape'` heritent du padding de base
   (`.scada-modern-element { padding: 0 8px; }`) sans le remettre a `0`,
   contrairement a `Kind === 'Custom'` qui le fait deja. La forme SVG
   (rendue a `width:100%` de la zone de contenu) est donc inseree de 8px
   de chaque cote par rapport au cadre/aux poignees, horizontalement
   uniquement (le padding vertical est deja `0`).
2. **Boite de groupe qui deborde de son contenu a l'agrandissement** :
   `UpdateModernElementGeometry` (`MainWindow.xaml.cs`) ne met a jour que
   les `Bounds` de l'element redimensionne. Pour un `Group`, les enfants
   gardent leurs `X/Y/Width/Height` d'origine — rien ne les met a
   l'echelle avec le cadre du groupe.
3. **Resize a un seul axe impossible** : seules 4 poignees diagonales
   (`nw`, `ne`, `sw`, `se`) existent ; aucune poignee de bord (`n`, `s`,
   `e`, `w`) pour un resize horizontal ou vertical pur.
4. **Shift+resize ne conserve pas le ratio** : `event.shiftKey` n'est
   jamais lu pendant le drag de resize (`pointermove`, mode `resize`).
5. **Instabilite pres des bords du canvas** : dans `setWrapperGeometry`,
   `x`/`y` sont clampes a `Math.max(0, ...)` mais `width`/`height` sont
   calcules a partir d'un delta souris non clampe. Quand le clamp de
   position s'active (poignee `w`/`n` pres du bord), la largeur/hauteur
   continue de suivre le delta brut : la boite se desynchronise du
   curseur ("saute").

## Solution retenue

Tout reste dans le pipeline existant `pointerdown` / `pointermove` /
`pointerup` -> `modernDrag` -> `postModernGeometry` ->
`updateSceneObjectGeometry` -> `UpdateModernElementGeometry`. Aucun
changement d'architecture.

### 1. Padding Shape

Ajouter `wrapper.style.padding = '0';` dans la branche
`Kind === 'Shape'` de `renderModernElements`, au meme endroit ou
`background`/`border` sont deja reinitialises.

### 2. Poignees de bord (N/S/E/W)

Ajouter 4 elements `.scada-modern-handle` avec `data-handle="n"|"s"|"e"|"w"`,
positionnes au milieu de chaque cote (CSS `left: 50%; transform:
translateX(-50%)` pour `n`/`s`, equivalent en Y pour `e`/`w`), curseur
`ns-resize` / `ew-resize`.

La logique de calcul de geometrie (`modernDrag.handle.includes('e'|'s'|'w'|'n')`,
`MainWindow.WebViewScript.cs` ~ligne 2102) reste inchangee : un handle a
une seule lettre ne modifie deja qu'un seul axe. Aucune modification de
logique necessaire au-dela de l'ajout des poignees.

### 3. Shift = ratio conserve

Au `pointerdown`, stocker `aspectRatio = startWidth / startHeight` dans
`modernDrag` (si `startHeight === 0` ou non fini, ne pas stocker de
ratio — voir Erreurs).

Au `pointermove`, si `event.shiftKey` est actif **et** que
`modernDrag.handle.length === 2` (poignee de coin) :
- comparer `|dx|` relatif a `startWidth` et `|dy|` relatif a
  `startHeight` pour determiner l'axe pilote (le plus grand
  deplacement relatif).
- recalculer l'autre dimension via `aspectRatio` a partir de la
  dimension pilote.
- ajuster `x`/`y` pour les poignees contenant `n`/`w` afin que le coin
  oppose a la poignee tiree reste fixe.

Les poignees de bord seul (`handle.length === 1`, issues du point 2)
ignorent Shift — comportement standard (Figma / Illustrator /
PowerPoint) : un seul axe est deja explicitement demande par
l'utilisateur.

### 4. Anti-jump aux bords du canvas

Remplacer le clamp a posteriori (`Math.max(0, x)` dans
`setWrapperGeometry`) par un clamp du delta souris **avant** le calcul
de geometrie, dans le gestionnaire `pointermove` (mode `resize`) :

```js
// poignee 'w'
const effectiveDx = Math.max(-(modernDrag.startWidth - 8), Math.min(dx, modernDrag.startX));
geometry.x = modernDrag.startX + effectiveDx;
geometry.width = modernDrag.startWidth - effectiveDx;
```

Meme principe en Y pour les poignees contenant `n`. Ce clamp combine
garantit simultanement `x >= 0` et `width >= 8`, en gardant position et
taille toujours coherentes avec le curseur (plus de saut).

### 5. Groupe : mise a l'echelle des enfants

Au `pointerdown` sur une poignee d'un `Group`, capturer recursivement
la geometrie de tous les descendants (meme pattern que `items` en mode
`move`).

Au `pointermove`, appliquer un facteur d'echelle
(`scaleX = newWidth / startWidth`, `scaleY = newHeight / startHeight`,
ancre au coin oppose a la poignee tiree) a chaque wrapper descendant
pour un apercu live pendant le drag.

Au `pointerup`, envoyer un seul message batch (nouveau type
`resizeSceneGroupWithChildren`) contenant les bounds avant/apres du
groupe et de chaque descendant. Cote C#, un nouveau handler
(`UpdateModernGroupGeometryWithChildren`) applique les nouvelles
`Bounds` du groupe et de chaque descendant recursif, puis pousse **une
seule action d'historique composite** (nouvelle
`ModernGroupResizeAction`, analogue a `ModernElementBoundsChangedAction`
mais portant une liste de paires avant/apres) — un Ctrl+Z annule tout
le resize du groupe en une fois.

## Erreurs et cas limites

- **Ratio degenere** (`startHeight === 0` ou tres proche de 0) :
  `aspectRatio` non stocke / non fini -> le lock Shift est desactive
  pour ce drag plutot que de produire `NaN`/`Infinity`.
- **Groupe sans enfants** : la mise a l'echelle ne fait rien de
  particulier, seul le cadre change (comportement actuel inchange).
- **Groupes imbriques** : la capture de descendants est recursive au
  moment du `pointerdown` sur le groupe le plus proche redimensionne ;
  chaque wrapper n'est mis a l'echelle qu'une seule fois, par rapport a
  ce groupe-la (pas de double application en cascade).
- **Taille minimale (8px)** : regle existante conservee, mais appliquee
  desormais en clampant le delta plutot que la valeur finale (point 4),
  pour eliminer la desynchronisation deja presente.
- **Undo/Redo groupe** : doit rester atomique — une seule entree
  d'historique pour le cadre + tous les enfants, jamais une par
  element, pour eviter un etat incoherent apres un Ctrl+Z partiel.

## Tests

- Pas de suite de tests JS dans le repo pour ce fichier (le JS est
  embarque en string C#) : verification manuelle via `dotnet run
  --project src/ScadaBuilderV2.App` pour les poignees, le lock Shift et
  l'anti-jump aux bords.
- MSTest (`tests/ScadaBuilderV2.Tests`), extension du pattern
  `EditorHistoryServiceTests` :
  - Nouveau test verifiant que `UpdateModernGroupGeometryWithChildren`
    met a jour les bounds de tous les descendants proportionnellement
    et pousse une seule `ModernGroupResizeAction`.
  - Nouveau test Undo/Redo sur `ModernGroupResizeAction` (annulation
    restaure le cadre et tous les enfants en un seul appel).
