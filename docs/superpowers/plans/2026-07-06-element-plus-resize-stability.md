# Fiabilisation du resize Element+ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Corriger les 5 defauts constates de l'outil de resize des objets Element+ dans SCADA Builder V2 : padding residuel sur les Shapes, absence de mise a l'echelle des enfants d'un groupe, absence de poignees a un seul axe, absence de conservation du ratio avec Shift, et instabilite pres des bords du canvas.

**Architecture:** Tout reste dans le pipeline existant du canvas WebView2 (`pointerdown`/`pointermove`/`pointerup` -> `modernDrag` -> message `chrome.webview.postMessage` -> `MainWindow.xaml.cs` -> `EditorHistoryService`). Aucun nouveau service, aucune nouvelle classe de haut niveau : on reutilise l'action d'historique generique `SceneSelectionMovedAction` (deja utilisee pour le deplacement multi-selection) pour rendre le resize de groupe atomique (groupe + enfants dans une seule entree Undo/Redo).

**Tech Stack:** .NET 8 / WPF (`ScadaBuilderV2.App`), WebView2 (JS embarque en chaine C# dans `MainWindow.WebViewScript.cs`), MSTest (`tests/ScadaBuilderV2.Tests`).

## Global Constraints

- Cible `net8.0-windows` ; build via `dotnet build ScadaBuilderV2.sln`, tests via `dotnet test ScadaBuilderV2.sln --no-restore`.
- Editor artifacts (poignees, cadres de selection, previsualisations de drag) ne doivent jamais fuiter dans la geometrie exportee (`.sep`/`.sb2`) — toutes les modifications de ce plan restent dans la couche editeur (canvas JS + `Bounds` du modele de scene), aucun changement a l'exporteur.
- Preview / build / export doivent rester en parite : ce plan ne touche pas `PreviewDocument.cs` ni `Ft100SceneExporter.cs`, seulement le modele de scene (`Bounds`) que ces deux consomment deja.
- Une seule action d'historique par resize (pas d'entree par element deplace/redimensionne) — deja garanti par `SceneSelectionMovedAction`, a respecter dans le nouveau handler C#.
- Avant de commencer, le worktree doit etre propre (commiter le travail en cours, hors fichiers de projet `.sep`/`.scene.json` non lies a ce plan).
- Apres chaque tache validee, faire un commit avant de passer a la tache suivante.
- Pas de suite de tests JS dans ce repo (le JS est embarque en chaine C#) — verification manuelle via `dotnet run --project src/ScadaBuilderV2.App` pour toute tache purement JS/CSS.

---

## Task 1: Corriger le padding residuel des wrappers Shape

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs` (fonction `renderModernElements`, branche `Kind === 'Shape'`)

**Interfaces:**
- Consomme : rien de nouveau.
- Produit : rien de nouveau — correctif visuel isole.

- [ ] **Step 1: Localiser la branche Shape actuelle**

Chercher dans `MainWindow.WebViewScript.cs` :

```js
      } else if (element.Kind === 'Shape') {
        wrapper.style.background = 'transparent';
        wrapper.style.border = '0';
        wrapper.appendChild(renderShapeElement(element, style));
      } else if (element.Kind === 'Text') {
```

- [ ] **Step 2: Ajouter la reinitialisation du padding**

Remplacer par :

```js
      } else if (element.Kind === 'Shape') {
        wrapper.style.background = 'transparent';
        wrapper.style.border = '0';
        wrapper.style.padding = '0';
        wrapper.appendChild(renderShapeElement(element, style));
      } else if (element.Kind === 'Text') {
```

- [ ] **Step 3: Verification manuelle**

Lancer l'app :

```bash
dotnet run --project src/ScadaBuilderV2.App
```

Ouvrir le projet `AMR_REF_SCADA_V2`, placer une forme (Shape) Element+ sur une scene, la selectionner, et l'agrandir depuis une poignee de coin. Attendu : le contour SVG de la forme touche exactement le bord interieur du cadre de selection (plus d'ecart de ~8px a gauche/droite).

- [ ] **Step 4: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs
git commit -m "fix: remove residual horizontal padding on Shape Element+ wrappers"
```

---

## Task 2: Ajouter les poignees de resize a un seul axe (N/S/E/W)

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs` (bloc CSS des poignees, et boucle de creation des poignees dans `renderModernElements`)

**Interfaces:**
- Consomme : rien de nouveau.
- Produit : des elements `.scada-modern-handle` avec `data-handle` valant `"n"`, `"s"`, `"e"` ou `"w"` (en plus de `"nw"`/`"ne"`/`"sw"`/`"se"` existants). La logique de calcul de geometrie du Task 3 doit gerer ces handles a une seule lettre.

- [ ] **Step 1: Ajouter le CSS de positionnement pour les nouvelles poignees**

Localiser :

```js
    .scada-modern-handle[data-handle="nw"] { left: -6px; top: -6px; cursor: nwse-resize; }
    .scada-modern-handle[data-handle="ne"] { right: -6px; top: -6px; cursor: nesw-resize; }
    .scada-modern-handle[data-handle="sw"] { left: -6px; bottom: -6px; cursor: nesw-resize; }
    .scada-modern-handle[data-handle="se"] { right: -6px; bottom: -6px; cursor: nwse-resize; }
```

Remplacer par :

```js
    .scada-modern-handle[data-handle="nw"] { left: -6px; top: -6px; cursor: nwse-resize; }
    .scada-modern-handle[data-handle="ne"] { right: -6px; top: -6px; cursor: nesw-resize; }
    .scada-modern-handle[data-handle="sw"] { left: -6px; bottom: -6px; cursor: nesw-resize; }
    .scada-modern-handle[data-handle="se"] { right: -6px; bottom: -6px; cursor: nwse-resize; }
    .scada-modern-handle[data-handle="n"] { left: 50%; top: -6px; transform: translateX(-50%); cursor: ns-resize; }
    .scada-modern-handle[data-handle="s"] { left: 50%; bottom: -6px; transform: translateX(-50%); cursor: ns-resize; }
    .scada-modern-handle[data-handle="e"] { right: -6px; top: 50%; transform: translateY(-50%); cursor: ew-resize; }
    .scada-modern-handle[data-handle="w"] { left: -6px; top: 50%; transform: translateY(-50%); cursor: ew-resize; }
```

- [ ] **Step 2: Ajouter les 4 nouvelles poignees a la liste generee**

Localiser :

```js
      ['nw', 'ne', 'sw', 'se'].forEach(handle => {
        const grip = document.createElement('span');
        grip.className = 'scada-modern-handle';
        grip.dataset.handle = handle;
        wrapper.appendChild(grip);
      });
```

Remplacer par :

```js
      ['nw', 'ne', 'sw', 'se', 'n', 's', 'e', 'w'].forEach(handle => {
        const grip = document.createElement('span');
        grip.className = 'scada-modern-handle';
        grip.dataset.handle = handle;
        wrapper.appendChild(grip);
      });
```

- [ ] **Step 3: Verification manuelle**

Lancer `dotnet run --project src/ScadaBuilderV2.App`, selectionner un element Element+, verifier que 8 poignees sont visibles (4 coins + 4 milieux de cote), et que tirer une poignee de bord (`e` par ex.) ne change que la largeur, pas la hauteur ni la position Y.

- [ ] **Step 4: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs
git commit -m "feat: add single-axis resize handles (N/S/E/W) to Element+ objects"
```

---

## Task 3: Corriger l'instabilite (anti-jump) du resize pres des bords du canvas

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs` (fonction `setWrapperGeometry` — ajout d'un helper — et le bloc `pointermove` mode `resize` de `modernDrag`)

**Interfaces:**
- Consomme : `modernDrag.handle`, `modernDrag.startX/startY/startWidth/startHeight` (deja existants).
- Produit : nouvelle fonction `clampNearAxis(startPos, startSize, delta)` retournant `{ pos, size }`, utilisee par Task 4.

- [ ] **Step 1: Ajouter le helper `clampNearAxis`**

Localiser `setWrapperGeometry` :

```js
  function setWrapperGeometry(wrapper, geometry) {
    wrapper.style.left = `${Math.max(0, geometry.x)}px`;
    wrapper.style.top = `${Math.max(0, geometry.y)}px`;
    wrapper.style.width = `${Math.max(8, geometry.width)}px`;
    wrapper.style.height = `${Math.max(8, geometry.height)}px`;
  }
```

Ajouter juste apres :

```js
  function setWrapperGeometry(wrapper, geometry) {
    wrapper.style.left = `${Math.max(0, geometry.x)}px`;
    wrapper.style.top = `${Math.max(0, geometry.y)}px`;
    wrapper.style.width = `${Math.max(8, geometry.width)}px`;
    wrapper.style.height = `${Math.max(8, geometry.height)}px`;
  }

  function clampNearAxis(startPos, startSize, delta) {
    const clampedDelta = Math.max(-startPos, Math.min(delta, startSize - 8));
    return { pos: startPos + clampedDelta, size: startSize - clampedDelta };
  }
```

- [ ] **Step 2: Remplacer le calcul de geometrie du mode resize**

Localiser dans le gestionnaire `pointermove` :

```js
      } else {
        if (modernDrag.handle.includes('e')) geometry.width = modernDrag.startWidth + dx;
        if (modernDrag.handle.includes('s')) geometry.height = modernDrag.startHeight + dy;
        if (modernDrag.handle.includes('w')) {
          geometry.x = modernDrag.startX + dx;
          geometry.width = modernDrag.startWidth - dx;
        }
        if (modernDrag.handle.includes('n')) {
          geometry.y = modernDrag.startY + dy;
          geometry.height = modernDrag.startHeight - dy;
        }
        setWrapperGeometry(modernDrag.wrapper, geometry);
      }
```

Remplacer par :

```js
      } else {
        if (modernDrag.handle.includes('w')) {
          const clampedX = clampNearAxis(modernDrag.startX, modernDrag.startWidth, dx);
          geometry.x = clampedX.pos;
          geometry.width = clampedX.size;
        } else if (modernDrag.handle.includes('e')) {
          geometry.width = Math.max(8, modernDrag.startWidth + dx);
        }
        if (modernDrag.handle.includes('n')) {
          const clampedY = clampNearAxis(modernDrag.startY, modernDrag.startHeight, dy);
          geometry.y = clampedY.pos;
          geometry.height = clampedY.size;
        } else if (modernDrag.handle.includes('s')) {
          geometry.height = Math.max(8, modernDrag.startHeight + dy);
        }
        setWrapperGeometry(modernDrag.wrapper, geometry);
      }
```

- [ ] **Step 3: Verification manuelle**

Lancer `dotnet run --project src/ScadaBuilderV2.App`, placer un element pres du bord gauche du canvas (X proche de 0), tirer la poignee `w` (ou `nw`) vers la droite au-dela du bord gauche puis revenir. Attendu : la boite suit le curseur sans sauter ni se desynchroniser, `x` ne descend jamais sous 0, `width` ne descend jamais sous 8.

- [ ] **Step 4: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs
git commit -m "fix: clamp resize delta instead of position to prevent handle desync near canvas edges"
```

---

## Task 4: Ajouter Shift = conservation du ratio (poignees de coin)

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs` (construction de `modernDrag` au `pointerdown`, et bloc `pointermove` mode `resize` modifie au Task 3)

**Interfaces:**
- Consomme : `clampNearAxis` (Task 3), `modernDrag.handle`.
- Produit : `modernDrag.aspectRatio` (nombre ou `null`), consomme par le meme bloc `pointermove`.

- [ ] **Step 1: Stocker le ratio au `pointerdown`**

Localiser :

```js
        const geometry = readWrapperGeometry(sceneMoveWrapper);
        const movingWrappers = event.target?.classList?.contains('scada-modern-handle')
          ? [sceneMoveWrapper]
          : Array.from(document.querySelectorAll('.scada-modern-element'))
              .filter(item => selectedModernIds.has(item.dataset.id))
              .map(item => getSceneMoveWrapper(item))
              .filter((item, index, items) => item && items.indexOf(item) === index);
        modernDrag = {
          id: sceneMoveId,
          wrapper: sceneMoveWrapper,
          mode: event.target?.classList?.contains('scada-modern-handle') ? 'resize' : 'move',
          handle: event.target?.dataset?.handle || '',
          startClientX: event.clientX,
          startClientY: event.clientY,
          startX: geometry.x,
          startY: geometry.y,
          startWidth: geometry.width,
          startHeight: geometry.height,
          items: movingWrappers.map(item => ({
            id: item.dataset.id,
            wrapper: item,
            geometry: readWrapperGeometry(item)
          }))
        };
```

Remplacer par :

```js
        const geometry = readWrapperGeometry(sceneMoveWrapper);
        const isResize = event.target?.classList?.contains('scada-modern-handle');
        const movingWrappers = isResize
          ? [sceneMoveWrapper]
          : Array.from(document.querySelectorAll('.scada-modern-element'))
              .filter(item => selectedModernIds.has(item.dataset.id))
              .map(item => getSceneMoveWrapper(item))
              .filter((item, index, items) => item && items.indexOf(item) === index);
        modernDrag = {
          id: sceneMoveId,
          wrapper: sceneMoveWrapper,
          mode: isResize ? 'resize' : 'move',
          handle: event.target?.dataset?.handle || '',
          startClientX: event.clientX,
          startClientY: event.clientY,
          startX: geometry.x,
          startY: geometry.y,
          startWidth: geometry.width,
          startHeight: geometry.height,
          aspectRatio: geometry.height > 0 ? geometry.width / geometry.height : null,
          items: movingWrappers.map(item => ({
            id: item.dataset.id,
            wrapper: item,
            geometry: readWrapperGeometry(item)
          }))
        };
```

- [ ] **Step 2: Appliquer le lock de ratio dans `pointermove`**

Localiser le bloc issu du Task 3 :

```js
      } else {
        if (modernDrag.handle.includes('w')) {
          const clampedX = clampNearAxis(modernDrag.startX, modernDrag.startWidth, dx);
          geometry.x = clampedX.pos;
          geometry.width = clampedX.size;
        } else if (modernDrag.handle.includes('e')) {
          geometry.width = Math.max(8, modernDrag.startWidth + dx);
        }
        if (modernDrag.handle.includes('n')) {
          const clampedY = clampNearAxis(modernDrag.startY, modernDrag.startHeight, dy);
          geometry.y = clampedY.pos;
          geometry.height = clampedY.size;
        } else if (modernDrag.handle.includes('s')) {
          geometry.height = Math.max(8, modernDrag.startHeight + dy);
        }
        setWrapperGeometry(modernDrag.wrapper, geometry);
      }
```

Remplacer par :

```js
      } else {
        if (modernDrag.handle.includes('w')) {
          const clampedX = clampNearAxis(modernDrag.startX, modernDrag.startWidth, dx);
          geometry.x = clampedX.pos;
          geometry.width = clampedX.size;
        } else if (modernDrag.handle.includes('e')) {
          geometry.width = Math.max(8, modernDrag.startWidth + dx);
        }
        if (modernDrag.handle.includes('n')) {
          const clampedY = clampNearAxis(modernDrag.startY, modernDrag.startHeight, dy);
          geometry.y = clampedY.pos;
          geometry.height = clampedY.size;
        } else if (modernDrag.handle.includes('s')) {
          geometry.height = Math.max(8, modernDrag.startHeight + dy);
        }

        if (event.shiftKey && modernDrag.handle.length === 2 && modernDrag.aspectRatio) {
          const widthRatioChange = Math.abs(geometry.width - modernDrag.startWidth) / modernDrag.startWidth;
          const heightRatioChange = Math.abs(geometry.height - modernDrag.startHeight) / modernDrag.startHeight;
          if (widthRatioChange >= heightRatioChange) {
            geometry.height = geometry.width / modernDrag.aspectRatio;
          } else {
            geometry.width = geometry.height * modernDrag.aspectRatio;
          }
          if (modernDrag.handle.includes('n')) {
            geometry.y = modernDrag.startY + (modernDrag.startHeight - geometry.height);
          }
          if (modernDrag.handle.includes('w')) {
            geometry.x = modernDrag.startX + (modernDrag.startWidth - geometry.width);
          }
        }

        setWrapperGeometry(modernDrag.wrapper, geometry);
      }
```

- [ ] **Step 3: Verification manuelle**

Lancer `dotnet run --project src/ScadaBuilderV2.App`, selectionner un element rectangulaire (ex. 200x100), tirer la poignee `se` en maintenant Shift. Attendu : le ratio 2:1 est conserve pendant tout le drag. Verifier aussi qu'une poignee de bord seul (`e`) ignore Shift (un seul axe change, comme avant).

- [ ] **Step 4: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs
git commit -m "feat: preserve aspect ratio when resizing from a corner handle with Shift held"
```

---

## Task 5: Test de regression — resize de groupe atomique (groupe + enfants)

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`

**Interfaces:**
- Consomme : `SceneSelectionMovedAction`, `MovedSceneElementBounds` (existants, `src/ScadaBuilderV2.Application/History/SceneSelectionMovedAction.cs`), les helpers prives `CreateShape`/`CreateGroup`/`CreateContext` deja presents dans ce fichier de test.
- Produit : preuve que `SceneSelectionMovedAction` restaure atomiquement les bounds d'un groupe **et** de son enfant — c'est le mecanisme reutilise par le handler C# du Task 8.

Ce test n'exige aucun nouveau code de production : `SceneSelectionMovedAction` est deja generique. Il caracterise et verrouille le comportement que le Task 8 va exploiter (au lieu d'inventer une nouvelle classe d'action pour le resize de groupe).

- [ ] **Step 1: Ecrire le test**

Ajouter dans `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`, apres la methode `SceneSelectionMovedActionUndoRedoRestoresMovedBounds` (avant la fermeture de la classe et les helpers prives) :

```csharp
    [TestMethod]
    public async Task SceneSelectionMovedActionUndoRedoRestoresGroupAndChildBoundsTogether()
    {
        var child = CreateShape("shape-001", 5, 6);
        var group = CreateGroup("group-001", 100, 200, [child]);
        var scene = ScadaScene.CreateEmpty("win00008", "win00008", new(1280, 873))
            .WithElement(group);
        var history = new EditorHistoryService();
        var context = CreateContext(scene, updated => scene = updated);

        var beforeGroupBounds = group.Bounds;
        var afterGroupBounds = new SceneBounds(100, 200, 160, 160);
        var beforeChildBounds = child.Bounds;
        var afterChildBounds = new SceneBounds(10, 12, 40, 40);

        scene = scene
            .WithReplacedElementRecursive(group with { Bounds = afterGroupBounds })
            .WithReplacedElementRecursive(child with { Bounds = afterChildBounds });

        history.Push(new SceneSelectionMovedAction(
            scene.Id,
            [
                new MovedSceneElementBounds(group.Id, beforeGroupBounds, afterGroupBounds),
                new MovedSceneElementBounds(child.Id, beforeChildBounds, afterChildBounds)
            ],
            "resize de groupe"));

        Assert.IsTrue(await history.UndoAsync(context));
        Assert.AreEqual(beforeGroupBounds.Width, scene.FindElementRecursive(group.Id)?.Bounds.Width);
        Assert.AreEqual(beforeChildBounds.Width, scene.FindElementRecursive(child.Id)?.Bounds.Width);

        Assert.IsTrue(await history.RedoAsync(context));
        Assert.AreEqual(afterGroupBounds.Width, scene.FindElementRecursive(group.Id)?.Bounds.Width);
        Assert.AreEqual(afterChildBounds.Width, scene.FindElementRecursive(child.Id)?.Bounds.Width);
    }
```

- [ ] **Step 2: Executer le test**

```bash
dotnet test ScadaBuilderV2.sln --no-restore --filter "Name=SceneSelectionMovedActionUndoRedoRestoresGroupAndChildBoundsTogether"
```

Expected: PASS (aucun code de production nouveau requis — ce test caracterise un comportement deja correct de `SceneSelectionMovedAction`).

- [ ] **Step 3: Commit**

```bash
git add tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs
git commit -m "test: lock in atomic undo/redo of group+child bounds via SceneSelectionMovedAction"
```

---

## Task 6: Etendre le DTO de message WebView pour le resize de groupe

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs`

**Interfaces:**
- Consomme : rien de nouveau.
- Produit : `LegacyViewerMessage.Children` (`List<LegacyViewerChildBoundsMessage>?`), nouvelle classe `LegacyViewerChildBoundsMessage` avec `Id`, `X`, `Y`, `Width`, `Height`, `BeforeX`, `BeforeY`, `BeforeWidth`, `BeforeHeight`. Consomme par Task 7 (JS, cote emission) et Task 8 (C#, cote lecture).

- [ ] **Step 1: Ajouter le champ `Children` sur `LegacyViewerMessage`**

Localiser (fin de la classe `LegacyViewerMessage`) :

```csharp
        public double FontSize { get; set; }

        public double BorderWidth { get; set; }
    }

    private sealed class LegacyViewerElementMessage
```

Remplacer par :

```csharp
        public double FontSize { get; set; }

        public double BorderWidth { get; set; }

        public List<LegacyViewerChildBoundsMessage>? Children { get; set; }
    }

    private sealed class LegacyViewerChildBoundsMessage
    {
        public string Id { get; set; } = "";

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double BeforeX { get; set; }

        public double BeforeY { get; set; }

        public double BeforeWidth { get; set; }

        public double BeforeHeight { get; set; }
    }

    private sealed class LegacyViewerElementMessage
```

- [ ] **Step 2: Build de verification**

```bash
dotnet build ScadaBuilderV2.sln
```

Expected: build reussi, 0 erreur (le champ `Children` sur `LegacyViewerMessage` n'est pas encore consomme, c'est attendu a ce stade).

- [ ] **Step 3: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs
git commit -m "feat: add group-children bounds payload to the WebView legacy message DTO"
```

---

## Task 7: Capturer, mettre a l'echelle en direct et envoyer les enfants d'un groupe redimensionne

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`

**Interfaces:**
- Consomme : `readWrapperGeometry`, `setWrapperGeometry`, `getSceneMoveWrapper`, `postModernGeometry` (existants), `modernDrag.aspectRatio` (Task 4).
- Produit : `modernDrag.groupChildren` (tableau `{ id, wrapper, geometry }`), nouvelle fonction `postModernGroupResize(id, before, after, children)` qui poste un message `type: 'resizeSceneGroupWithChildren'` conforme au DTO du Task 6. Consomme par Task 8 (cote C#).

- [ ] **Step 1: Capturer les descendants du groupe au `pointerdown`**

Localiser le bloc modifie au Task 4 :

```js
        const geometry = readWrapperGeometry(sceneMoveWrapper);
        const isResize = event.target?.classList?.contains('scada-modern-handle');
        const movingWrappers = isResize
```

Remplacer par :

```js
        const geometry = readWrapperGeometry(sceneMoveWrapper);
        const isResize = event.target?.classList?.contains('scada-modern-handle');
        const groupChildren = isResize && sceneMoveWrapper.classList.contains('scada-modern-group')
          ? Array.from(sceneMoveWrapper.querySelectorAll('.scada-modern-element')).map(child => ({
              id: child.dataset.id,
              wrapper: child,
              geometry: readWrapperGeometry(child)
            }))
          : [];
        const movingWrappers = isResize
```

Puis localiser (toujours dans le meme bloc `modernDrag = {...}`) :

```js
          aspectRatio: geometry.height > 0 ? geometry.width / geometry.height : null,
          items: movingWrappers.map(item => ({
```

Remplacer par :

```js
          aspectRatio: geometry.height > 0 ? geometry.width / geometry.height : null,
          groupChildren,
          items: movingWrappers.map(item => ({
```

- [ ] **Step 2: Mettre a l'echelle les enfants en direct pendant `pointermove`**

Localiser la fin du bloc `resize` de Task 4 :

```js
          if (modernDrag.handle.includes('w')) {
            geometry.x = modernDrag.startX + (modernDrag.startWidth - geometry.width);
          }
        }

        setWrapperGeometry(modernDrag.wrapper, geometry);
      }
```

Remplacer par :

```js
          if (modernDrag.handle.includes('w')) {
            geometry.x = modernDrag.startX + (modernDrag.startWidth - geometry.width);
          }
        }

        setWrapperGeometry(modernDrag.wrapper, geometry);

        if (modernDrag.groupChildren.length) {
          const scaleX = geometry.width / modernDrag.startWidth;
          const scaleY = geometry.height / modernDrag.startHeight;
          modernDrag.groupChildren.forEach(child => {
            setWrapperGeometry(child.wrapper, {
              x: child.geometry.x * scaleX,
              y: child.geometry.y * scaleY,
              width: child.geometry.width * scaleX,
              height: child.geometry.height * scaleY
            });
          });
        }
      }
```

- [ ] **Step 3: Ajouter `postModernGroupResize` a cote de `postModernGeometry`**

Localiser :

```js
  function postModernGeometry(id, before, after) {
    window.chrome?.webview?.postMessage({
      type: 'updateSceneObjectGeometry',
      id,
      beforeX: Math.max(0, Math.round(before.x)),
      beforeY: Math.max(0, Math.round(before.y)),
      beforeWidth: Math.max(8, Math.round(before.width)),
      beforeHeight: Math.max(8, Math.round(before.height)),
      x: Math.max(0, Math.round(after.x)),
      y: Math.max(0, Math.round(after.y)),
      width: Math.max(8, Math.round(after.width)),
      height: Math.max(8, Math.round(after.height))
    });
  }
```

Ajouter juste apres :

```js
  function postModernGeometry(id, before, after) {
    window.chrome?.webview?.postMessage({
      type: 'updateSceneObjectGeometry',
      id,
      beforeX: Math.max(0, Math.round(before.x)),
      beforeY: Math.max(0, Math.round(before.y)),
      beforeWidth: Math.max(8, Math.round(before.width)),
      beforeHeight: Math.max(8, Math.round(before.height)),
      x: Math.max(0, Math.round(after.x)),
      y: Math.max(0, Math.round(after.y)),
      width: Math.max(8, Math.round(after.width)),
      height: Math.max(8, Math.round(after.height))
    });
  }

  function postModernGroupResize(id, before, after, children) {
    window.chrome?.webview?.postMessage({
      type: 'resizeSceneGroupWithChildren',
      id,
      beforeX: Math.max(0, Math.round(before.x)),
      beforeY: Math.max(0, Math.round(before.y)),
      beforeWidth: Math.max(8, Math.round(before.width)),
      beforeHeight: Math.max(8, Math.round(before.height)),
      x: Math.max(0, Math.round(after.x)),
      y: Math.max(0, Math.round(after.y)),
      width: Math.max(8, Math.round(after.width)),
      height: Math.max(8, Math.round(after.height)),
      children: children.map(child => ({
        id: child.id,
        beforeX: Math.round(child.geometry.x),
        beforeY: Math.round(child.geometry.y),
        beforeWidth: Math.max(1, Math.round(child.geometry.width)),
        beforeHeight: Math.max(1, Math.round(child.geometry.height)),
        x: Math.round(child.after.x),
        y: Math.round(child.after.y),
        width: Math.max(1, Math.round(child.after.width)),
        height: Math.max(1, Math.round(child.after.height))
      }))
    });
  }
```

- [ ] **Step 4: Envoyer le message batch au `pointerup`**

Localiser :

```js
    if (modernDrag) {
      const geometry = readWrapperGeometry(modernDrag.wrapper);
      if (modernDrag.mode === 'move' && (modernDrag.items || []).length > 1) {
        postSelectionMove(
          'object',
          modernDrag.items.map(item => item.id).filter(Boolean),
          event.clientX - modernDrag.startClientX,
          event.clientY - modernDrag.startClientY);
      } else {
        postModernGeometry(
          modernDrag.id,
          {
            x: modernDrag.startX,
            y: modernDrag.startY,
            width: modernDrag.startWidth,
            height: modernDrag.startHeight
          },
          geometry);
      }
      modernDrag = null;
```

Remplacer par :

```js
    if (modernDrag) {
      const geometry = readWrapperGeometry(modernDrag.wrapper);
      if (modernDrag.mode === 'move' && (modernDrag.items || []).length > 1) {
        postSelectionMove(
          'object',
          modernDrag.items.map(item => item.id).filter(Boolean),
          event.clientX - modernDrag.startClientX,
          event.clientY - modernDrag.startClientY);
      } else if (modernDrag.mode === 'resize' && modernDrag.groupChildren.length) {
        postModernGroupResize(
          modernDrag.id,
          {
            x: modernDrag.startX,
            y: modernDrag.startY,
            width: modernDrag.startWidth,
            height: modernDrag.startHeight
          },
          geometry,
          modernDrag.groupChildren.map(child => ({
            id: child.id,
            geometry: child.geometry,
            after: readWrapperGeometry(child.wrapper)
          })));
      } else {
        postModernGeometry(
          modernDrag.id,
          {
            x: modernDrag.startX,
            y: modernDrag.startY,
            width: modernDrag.startWidth,
            height: modernDrag.startHeight
          },
          geometry);
      }
      modernDrag = null;
```

- [ ] **Step 5: Verification manuelle**

Lancer `dotnet run --project src/ScadaBuilderV2.App`, grouper 2 elements Element+, redimensionner le groupe depuis une poignee de coin. Attendu : les enfants grossissent/retrecissent proportionnellement en temps reel avec le cadre du groupe (plus d'ecart entre le cadre et le contenu).

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs
git commit -m "feat: scale group children live during resize and send batched bounds on release"
```

---

## Task 8: Gerer le message de resize de groupe cote C# et le rendre annulable

**Files:**
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`

**Interfaces:**
- Consomme : `LegacyViewerMessage.Children`/`LegacyViewerChildBoundsMessage` (Task 6), `SceneSelectionMovedAction`/`MovedSceneElementBounds` (existants, valides par Task 5), `_activeScene.FindElementRecursive`/`WithReplacedElementRecursive` (existants).
- Produit : nouvelle methode privee `UpdateModernGroupGeometryWithChildren(LegacyViewerMessage message)`, nouveau `case "resizeSceneGroupWithChildren"` dans le switch de `OnLegacyViewerMessageReceived`.

- [ ] **Step 1: Ajouter le `case` dans le switch de messages**

Localiser :

```csharp
                case "updateSceneObjectGeometry":
                case "updateModernElementGeometry":
                    UpdateModernElementGeometry(
                        message.Id,
                        message.X,
                        message.Y,
                        message.Width,
                        message.Height,
                        message.BeforeX,
                        message.BeforeY,
                        message.BeforeWidth,
                        message.BeforeHeight);
                    break;
```

Remplacer par :

```csharp
                case "updateSceneObjectGeometry":
                case "updateModernElementGeometry":
                    UpdateModernElementGeometry(
                        message.Id,
                        message.X,
                        message.Y,
                        message.Width,
                        message.Height,
                        message.BeforeX,
                        message.BeforeY,
                        message.BeforeWidth,
                        message.BeforeHeight);
                    break;
                case "resizeSceneGroupWithChildren":
                    UpdateModernGroupGeometryWithChildren(message);
                    break;
```

- [ ] **Step 2: Ajouter la methode `UpdateModernGroupGeometryWithChildren`**

Localiser la fin de `UpdateModernElementGeometry` (apres `SetStatus($"{updated.UserLabel}: position ...");` et sa fermeture `}`) :

```csharp
        MarkActiveSceneDirty();
        RefreshModernSceneUi();
        SetStatus($"{updated.UserLabel}: position {updated.Bounds.X:0},{updated.Bounds.Y:0}, taille {updated.Bounds.Width:0}x{updated.Bounds.Height:0}.");
    }

    private async Task MoveSelectionByAsync(LegacyViewerMessage message)
```

Remplacer par :

```csharp
        MarkActiveSceneDirty();
        RefreshModernSceneUi();
        SetStatus($"{updated.UserLabel}: position {updated.Bounds.X:0},{updated.Bounds.Y:0}, taille {updated.Bounds.Width:0}x{updated.Bounds.Height:0}.");
    }

    private void UpdateModernGroupGeometryWithChildren(LegacyViewerMessage message)
    {
        if (_activeScene is null || string.IsNullOrWhiteSpace(message.Id))
        {
            return;
        }

        var group = _activeScene.FindElementRecursive(message.Id);
        if (group is null)
        {
            return;
        }

        var beforeGroupBounds = new SceneBounds(
            Math.Max(0, Math.Round(message.BeforeX)),
            Math.Max(0, Math.Round(message.BeforeY)),
            Math.Max(8, Math.Round(message.BeforeWidth)),
            Math.Max(8, Math.Round(message.BeforeHeight)));
        var afterGroupBounds = new SceneBounds(
            Math.Max(0, Math.Round(message.X)),
            Math.Max(0, Math.Round(message.Y)),
            Math.Max(8, Math.Round(message.Width)),
            Math.Max(8, Math.Round(message.Height)));

        var elementBounds = new List<MovedSceneElementBounds>
        {
            new(group.Id, beforeGroupBounds, afterGroupBounds)
        };

        foreach (var child in message.Children ?? Enumerable.Empty<LegacyViewerChildBoundsMessage>())
        {
            if (string.IsNullOrWhiteSpace(child.Id))
            {
                continue;
            }

            var beforeChildBounds = new SceneBounds(
                Math.Round(child.BeforeX),
                Math.Round(child.BeforeY),
                Math.Max(1, Math.Round(child.BeforeWidth)),
                Math.Max(1, Math.Round(child.BeforeHeight)));
            var afterChildBounds = new SceneBounds(
                Math.Round(child.X),
                Math.Round(child.Y),
                Math.Max(1, Math.Round(child.Width)),
                Math.Max(1, Math.Round(child.Height)));

            elementBounds.Add(new MovedSceneElementBounds(child.Id, beforeChildBounds, afterChildBounds));
        }

        var updatedScene = _activeScene;
        foreach (var item in elementBounds)
        {
            var current = updatedScene.FindElementRecursive(item.ElementId);
            if (current is null)
            {
                continue;
            }

            updatedScene = updatedScene.WithReplacedElementRecursive(current with { Bounds = item.AfterBounds });
        }

        _activeScene = updatedScene;
        _selectedSceneObject = updatedScene.FindElementRecursive(group.Id);
        _selectedSceneObjectIds.Add(group.Id);

        _activeSceneTab?.History.Push(new SceneSelectionMovedAction(
            updatedScene.Id,
            elementBounds,
            "resize de groupe"));

        MarkActiveSceneDirty();
        RefreshModernSceneUi();
        SetStatus($"{group.UserLabel}: groupe redimensionne {afterGroupBounds.Width:0}x{afterGroupBounds.Height:0}.");
    }

    private async Task MoveSelectionByAsync(LegacyViewerMessage message)
```

- [ ] **Step 3: Build**

```bash
dotnet build ScadaBuilderV2.sln
```

Expected: build reussi, 0 erreur. (`MovedSceneElementBounds` et `SceneSelectionMovedAction` sont deja utilises plus bas dans ce meme fichier — le `using ScadaBuilderV2.Application.History;` est deja present.)

- [ ] **Step 4: Executer la suite de tests complete**

```bash
dotnet test ScadaBuilderV2.sln --no-restore
```

Expected: PASS, y compris le test du Task 5.

- [ ] **Step 5: Verification manuelle bout-en-bout**

Lancer `dotnet run --project src/ScadaBuilderV2.App`, grouper 2 elements, redimensionner le groupe, puis faire Ctrl+Z. Attendu : le groupe **et** ses enfants reviennent exactement a leur taille/position d'avant le resize, en une seule annulation. Ctrl+Y (ou Ctrl+Shift+Z selon le raccourci configure) doit reappliquer le resize sur le groupe et les enfants.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.App/MainWindow.xaml.cs
git commit -m "feat: apply and make undoable group resize with proportional child scaling"
```
