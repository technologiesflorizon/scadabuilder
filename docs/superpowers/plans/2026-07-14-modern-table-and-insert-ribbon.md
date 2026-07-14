# Tableau moderne et ruban Inserer hierarchique - Plan d'implementation

Date: 2026-07-14
Status: Draft implementation plan - pending execution approval
Document version: `V2.1.4.0015`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-14 | `V2.1.4.0015` | `95a57ac` | Creation du plan executable derive de la specification approuvee et de `DEC-0039`. |

> **Pour les agents d'execution :** utiliser un workflow d'execution de plan tache par tache. Ne pas deleguer a des sous-agents sans autorisation explicite de l'utilisateur. Les cases `- [ ]` constituent le suivi d'execution.

**Goal:** Livrer un Element+ `Table` moderne comparable aux besoins visibles dans `win00012`, avec edition type tableur, proprietes contextualisees, historique, persistance et export `.sb2`, puis remplacer le ruban Inserer plat par deux niveaux extensibles sans ajouter de regles metier ni de coordination detaillee dans `MainWindow`.

**Architecture:** Domain porte le contrat persistant, les invariants, les operations de grille et la resolution de styles. Application porte les requetes typees, commandes, diagnostics, presse-papiers et catalogues d'insertion. Rendering produit le HTML/CSS page-scope. App porte des controles, view models, dialogues, un coordinateur et un script WebView dedies. `MainWindow` ne fait que fournir le workspace actif, heberger les controles et deleguer les messages.

**Tech Stack:** C# 12, .NET 8, WPF/AvalonDock/WebView2, JavaScript embarque, `System.Text.Json`, MSTest, PowerShell, HTML/CSS et archive `.sb2`

## Global Constraints

- Spec proprietaire : `docs/superpowers/specs/2026-07-14-modern-table-and-insert-ribbon-design.md`.
- Decision : `DEC-0039` dans `docs/00_governance/DECISION_REGISTER_V2.md`.
- Le tableau est un `ScadaElementKind.Table` unique; les inputs de cellule ne sont pas des Element+ superposes.
- Aucun `ValueBindings`, `ReadTagId` ou `WriteTagId` cellule par cellule dans cette tranche.
- Capacite : 1 a 64 rangees et 1 a 64 colonnes; preset 6 x 8; minimum 24 px par colonne et 20 px par rangee.
- Precedence par propriete : cellule, rangee explicite, bande de rangee, colonne, tableau, defaut systeme; `null` signifie `Heriter`.
- Toute mutation persistante est atomique, validee et annulable/retablissable.
- Le presse-papiers interne conserve contenu, style de cellule et fusions; TSV assure l'interoperabilite texte. Il ne copie pas les dimensions ni les styles de pistes.
- Preview native et export partagent `Ft100SceneExporter`; le canevas WebView reste une surface d'authoring distincte qui consomme le meme modele.
- Les gouttieres, selections, separateurs, caret, menus et overlays sont editor-only et ne doivent jamais entrer dans `.sb2` ou `.sep`.
- Le contrat `.sb2` reste racine `manifest.json` plus `<page-id>/<page-id>.html`, avec ids et CSS page-scopes.
- Les ids d'insertion executables existants restent stables. Les outils futurs visibles sont desactives avec raison explicite.
- Aucun calcul de grille, fusion, remappage, presse-papiers, precedence, validation ou construction de menu Tableau ne doit etre ajoute a `MainWindow`.
- Les ajouts autorises a `MainWindow` sont limites a l'hebergement, l'activation de surface, la resolution du workspace actif et la delegation.
- Toute API publique ajoutee recoit une documentation XML avec `Decisions: DEC-0039`, contrats et tests dans `<remarks>`.
- Ne pas modifier `projects/AMR_REF_SCADA_V2` pendant l'implementation ou les tests automatises.

---

## Before You Start

- [ ] Verifier la branche et l'etat de travail.

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git status --short --branch
```

Attendu : branche `codex/table-tool-insert-ribbon`. Les modifications actuelles sous `projects/AMR_REF_SCADA_V2` appartiennent a l'utilisateur. Avant toute modification de code, les isoler par un commit utilisateur distinct, une sauvegarde approuvee ou un worktree propre. Ne jamais les stager, les restaurer ni les supprimer depuis ce plan.

- [ ] Capturer le baseline dans un worktree propre.

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu : conserver le resultat frais comme baseline; toute nouvelle regression doit etre expliquee avant un commit.

- [ ] Confirmer que les documents de planification sont deja committes et qu'aucun changement de code n'est melange a leur commit.

---

## Phase 1 - Modele persistant et invariants Domain

### Task 1: Ajouter le contrat Element+ Tableau retrocompatible

**Files:**
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableModels.cs`
- Modify: `src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs`
- Create: `tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs`

**Interfaces:**
- Consumes: `ScadaElement`, `ScadaElementKind`, `System.Text.Json` conventions.
- Produces: `ScadaTableDefinition`, pistes, cellules, contenus, styles, formats effectifs et `ScadaElement.Table`.

- [ ] Ajouter `ScadaElementKind.Table` et une propriete optionnelle `ScadaTableDefinition? Table` sans changer les valeurs ou defaults existants.
- [ ] Implementer les records approuves, les enums de grille/alignement, `ScadaTableStyle.Default`, les collections effectives `[JsonIgnore]` et `CreateDefault(rows, columns)`.
- [ ] Normaliser la creation a 1..64, 96 x 32 px, premiere rangee en-tete par defaut et contenu `Text` vide.
- [ ] Ajouter une factory `ScadaElement.CreateTable(...)` et verifier que `Bounds` derive de la somme des pistes.
- [ ] Tester defaults, limites, rejet des dimensions invalides, JSON ancien sans `Table`, round-trip complet et 64 x 64.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaTableModelTests|FullyQualifiedName~ScadaSceneModelsTests"
```

Attendu : tests cibles verts et aucun changement au rendu des anciens Element+.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableModels.cs src/ScadaBuilderV2.Domain/Scenes/ScadaSceneModels.cs tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs tests/ScadaBuilderV2.Tests/ScadaSceneModelsTests.cs
git commit -m "feat: add persistent modern table model"
```

### Task 2: Implementer les operations pures de grille et la precedence de style

**Files:**
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableOperations.cs`
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableStyleResolver.cs`
- Create: `tests/ScadaBuilderV2.Tests/ScadaTableOperationsTests.cs`

**Interfaces:**
- Consumes: modele de la tache 1 et plages rectangulaires.
- Produces: nouvelles definitions immuables ou diagnostics sans mutation partielle.

- [ ] Definir une plage canonique et des resultats explicites pour selection, fusion, defusion, contenu, format, dimension, insertion, suppression et distribution.
- [ ] Implementer les validations de spans, chevauchements, limites, piste finale et minimums de dimensions.
- [ ] Implementer insertion/suppression avec remappage des ancres et extension/reduction deterministe des fusions.
- [ ] Implementer redimensionnement externe proportionnel et redimensionnement de separateur avec correction d'arrondi sur la derniere piste.
- [ ] Implementer le resolver propriete par propriete approuve par `DEC-0039`, incluant `Heriter`, en-tete et alternance.
- [ ] Tester toutes les operations, les refus sans mutation, les fusions aux limites, l'insertion dans une fusion et l'exemple de precedence R3C2 de la spec.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaTableOperationsTests"
```

Attendu : toutes les operations Domain sont deterministes et sans dependance WPF/WebView.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Domain/Scenes/Tables tests/ScadaBuilderV2.Tests/ScadaTableOperationsTests.cs
git commit -m "feat: add immutable table grid operations"
```

---

## Phase 2 - Commandes Application, historique et presse-papiers

### Task 3: Ajouter les requetes typees et le coordinateur d'edition Tableau

**Files:**
- Create: `src/ScadaBuilderV2.Application/Tables/TableEditRequests.cs`
- Create: `src/ScadaBuilderV2.Application/Tables/TableEditCoordinator.cs`
- Create: `src/ScadaBuilderV2.Application/Tables/TableEditDiagnostics.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`

**Interfaces:**
- Consumes: operations Domain, scene active, selection interne et `ModernElementChangedAction`.
- Produces: ids `table.*`, enablement, diagnostic structure, Element+ avant/apres et description d'historique.

- [ ] Definir les requetes `table.merge`, `unmerge`, `set-content`, `set-format`, `set-track-size`, `resize-track`, `resize-proportional`, `insert-row`, `insert-column`, `delete-row`, `delete-column`, `clear-content` et `distribute`.
- [ ] Centraliser validation contextuelle et disabled reason; aucune commande ne doit dependre de controles WPF.
- [ ] Retourner un seul snapshot avant/apres par commande et pousser une seule action d'historique par geste termine.
- [ ] Tester enablement, diagnostics, absence d'effet sur erreur et undo/redo exact pour chaque famille de mutation.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableEditCoordinatorTests|FullyQualifiedName~EditorHistoryServiceTests"
```

Attendu : toute mutation passe par le coordinateur et l'historique restaure exactement cellules couvertes et contenus de fusion.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Application/Tables tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs
git commit -m "feat: add table editing command coordinator"
```

### Task 4: Ajouter le presse-papiers rectangulaire interne et TSV

**Files:**
- Create: `src/ScadaBuilderV2.Application/Tables/TableClipboard.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableClipboardTests.cs`

**Interfaces:**
- Consumes: plage de cellules et format texte du presse-papiers systeme fourni par l'adaptateur App.
- Produces: payload interne serialisable en memoire, TSV et requete de collage validee.

- [ ] Copier contenu, format explicite et fusions entierement incluses sans dimensions ni styles de pistes.
- [ ] Produire/consommer TSV avec tabulations, CRLF/LF, cellules vides et texte echappe de facon deterministe.
- [ ] Coller a l'ancre active sans extension implicite; refuser depassement ou intersection partielle d'une fusion.
- [ ] Verifier que `Effacer le contenu` conserve format, pistes et spans.
- [ ] Tester copie simple, plage, fusion, TSV externe, overflow et undo via le coordinateur.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableClipboardTests|FullyQualifiedName~TableEditCoordinatorTests"
```

Attendu : presse-papiers conforme a la spec, sans dependance directe a `System.Windows.Clipboard`.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Application/Tables/TableClipboard.cs tests/ScadaBuilderV2.Tests/TableClipboardTests.cs tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs
git commit -m "feat: add rectangular table clipboard"
```

---

## Phase 3 - Catalogue d'insertion et ruban hierarchique

### Task 5: Creer le catalogue de familles et les descripteurs generiques

**Files:**
- Create: `src/ScadaBuilderV2.Application/Commands/InsertToolCatalog.cs`
- Modify: `src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs`

**Interfaces:**
- Consumes: ids actuels `insert.*`, kinds, shapes, boutons et modes de placement.
- Produces: `RibbonFamilyDefinition`, `InsertToolDescriptor` et familles Texte/valeurs, Formes, Process, Electrique, Commandes, Donnees, Graphiques et Media.

- [ ] Conserver tous les ids executables existants et ajouter `insert.table` dans `Donnees`.
- [ ] Decrire creation, preset et placement dans les descriptors plutot que dans un `switch` WPF par id.
- [ ] Ajouter les outils modernes planifies de la spec comme disabled avec icone et raison non vide.
- [ ] Adapter `RibbonCommandCatalog` pour exposer le niveau 1 puis les groupes/outils du niveau 2, sans dupliquer le catalogue.
- [ ] Tester unicite, stabilite des ids, huit familles, `insert.table` executable et raisons disabled.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~RibbonCommandCatalogTests"
```

Attendu : le catalogue Application est la seule source canonique des insertions et familles.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Application/Commands/InsertToolCatalog.cs src/ScadaBuilderV2.Application/Commands/RibbonCommandCatalog.cs tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs
git commit -m "refactor: add hierarchical insert tool catalog"
```

---

## Phase 4 - Rendu HTML/CSS et contrat `.sb2`

### Task 6: Rendre le tableau dans le pipeline FT100 partage

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/NativePageDocumentTests.cs`

**Interfaces:**
- Consumes: definition et styles effectifs Domain.
- Produces: `<table>`, `<colgroup>`, `<tr>`, `<th>/<td>`, `rowspan/colspan`, inputs natifs et CSS page-scope.

- [ ] Ajouter un renderer Tableau dedie appele par `Ft100SceneExporter`; ne pas dupliquer une seconde implementation dans `PreviewDocument`.
- [ ] Emettre dimensions explicites, `table-layout: fixed`, contenus encodes et ids `ft100-<page>__<table>__r<row>-c<column>`.
- [ ] Emettre `<input type="text">` et `<input type="number">` avec valeur initiale, placeholder, min, max, step et readonly, sans binding ni script.
- [ ] Appliquer les formats effectifs et garder les invariants namespace/securite selon les contrats existants.
- [ ] Tester fusion, styles, inputs, absence de `ValueBindings`, namespace, anciennes pages et package `.sb2` valide sans artefacts editeur.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~Ft100SceneExporterTests|FullyQualifiedName~NativePageDocumentTests"
```

Attendu : preview native et `.sb2` produisent la meme structure de tableau sous la racine page-scopee.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs tests/ScadaBuilderV2.Tests/NativePageDocumentTests.cs
git commit -m "feat: render modern tables in ft100 output"
```

---

## Phase 5 - Editeur WebView dedie

### Task 7: Ajouter le rendu interactif et le bridge Tableau hors `MainWindow`

**Files:**
- Create: `src/ScadaBuilderV2.App/TableEditor/TableWebViewScript.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/TableWebViewMessages.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/TableWebViewBridgeAdapter.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableWebViewScriptTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: JSON du tableau, selection externe Element+ et coordinateur Application.
- Produces: DOM interactif, selection interne temporaire et messages types de commit.

- [ ] Isoler tout HTML/CSS/JS Tableau dans `TableWebViewScript`; `MainWindow.WebViewScript.cs` ne conserve qu'un point de composition de haut niveau.
- [ ] Rendre cellules, inputs, spans, bandeau de colonnes, gouttiere de rangees, separateurs et selection rectangulaire.
- [ ] Implementer clic, double-clic, Shift, Ctrl facultatif, glissement, F2, Enter, Tab, Shift+Tab et Escape selon la spec.
- [ ] Poster uniquement des requetes typees; le script ne modifie jamais durablement le modele et le bridge ne contient pas de regle Domain.
- [ ] Coalescer un glissement de separateur en feedback temporaire puis une seule commande au pointer-up.
- [ ] Laisser dans `MainWindow.xaml.cs` seulement l'enregistrement du bridge et la delegation au coordinateur Tableau.
- [ ] Tester presence des interactions, absence des artefacts dans le payload de rendu et absence des regles interdites dans les fichiers `MainWindow*`.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableWebViewScriptTests|FullyQualifiedName~WebViewContextMenuScriptTests"
```

Attendu : canevas interactif et bridge fonctionnels, sans etat durable uniquement DOM.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/TableWebViewScriptTests.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs
git commit -m "feat: add dedicated webview table editor"
```

---

## Phase 6 - Surfaces WPF Tableau

### Task 8: Ajouter creation, panneau Propriete et dialogues dedies

**Files:**
- Create: `src/ScadaBuilderV2.App/TableEditor/CreateTableDialog.xaml`
- Create: `src/ScadaBuilderV2.App/TableEditor/CreateTableDialog.xaml.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/TablePropertiesViewModel.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/TablePropertiesPane.xaml`
- Create: `src/ScadaBuilderV2.App/TableEditor/TablePropertiesPane.xaml.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/TablePropertiesDialog.xaml`
- Create: `src/ScadaBuilderV2.App/TableEditor/TablePropertiesDialog.xaml.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/CellFormatDialog.xaml`
- Create: `src/ScadaBuilderV2.App/TableEditor/CellFormatDialog.xaml.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/TrackSizeDialog.xaml`
- Create: `src/ScadaBuilderV2.App/TableEditor/TrackSizeDialog.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableEditorSurfaceContractTests.cs`

**Interfaces:**
- Consumes: coordinateur Application, selection interne et color picker existant.
- Produces: creation 6 x 8, inspection contextuelle et edits valides sans duplication de modele.

- [ ] Faire du pane un controle autonome heberge par un seul point de haut niveau dans l'anchorable `Propriete`.
- [ ] Afficher Tableau/Rangee/Colonne/Cellule/Plage, valeurs mixtes et action `Heriter` par propriete.
- [ ] Partager view model, validateurs et controles d'edition entre pane et dialogues.
- [ ] Le dialogue Tableau stage les changements et commit une seule action a `Enregistrer`; `Annuler` ne mute rien.
- [ ] Le dialogue de taille est parametre pour hauteur de ligne ou largeur de colonne et applique les minimums Domain.
- [ ] Le dialogue de creation valide 1..64, preset 6 x 8 et en-tete initial avant placement.
- [ ] Tester structure XAML, partage du view model, validation, mixed state et limitation des ajouts `MainWindow` a l'hebergement/delegation.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableEditorSurfaceContractTests|FullyQualifiedName~EditorHistoryServiceTests"
```

Attendu : deux surfaces synchronisees, commandes communes et aucun handler de regle Tableau dans `MainWindow`.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/TableEditorSurfaceContractTests.cs
git commit -m "feat: add modern table property surfaces"
```

### Task 9: Ajouter le menu contextuel, dimensions et presse-papiers WPF

**Files:**
- Create: `src/ScadaBuilderV2.App/TableEditor/TableContextMenuProvider.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/WpfTableClipboardAdapter.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableWebViewBridgeAdapter.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableEditorSurfaceContractTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableWebViewScriptTests.cs`

**Interfaces:**
- Consumes: contexte cell/plage/rangee/colonne et enablement Application.
- Produces: menu adapte, appel des dialogues et adaptateur `System.Windows.Clipboard`.

- [ ] Construire les items depuis une definition dediee et les ids `table.*`; ne jamais construire le menu dans `MainWindow` ou le JS.
- [ ] Preserver la selection avant clic droit et afficher uniquement les commandes pertinentes a la cible.
- [ ] Brancher copier/coller interne et TSV, effacement, insertion/suppression, fusion/defusion, format et dimensions.
- [ ] Afficher les diagnostics de refus sans modifier la scene ni l'historique.
- [ ] Tester labels, contexte, disabled reason, preservation de selection, clipboard systeme simule et une action d'historique par commande.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableEditorSurfaceContractTests|FullyQualifiedName~TableWebViewScriptTests|FullyQualifiedName~TableClipboardTests"
```

Attendu : menu complet conforme a la section 5.7, sans duplication de regles.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor tests/ScadaBuilderV2.Tests/TableEditorSurfaceContractTests.cs tests/ScadaBuilderV2.Tests/TableWebViewScriptTests.cs
git commit -m "feat: add contextual table editing commands"
```

---

## Phase 7 - Ruban WPF et extraction hors `MainWindow`

### Task 10: Rendre les deux niveaux du ruban et genericiser l'insertion

**Files:**
- Create: `src/ScadaBuilderV2.App/Insertion/InsertRibbonViewModels.cs`
- Create: `src/ScadaBuilderV2.App/Insertion/InsertRibbonControl.xaml`
- Create: `src/ScadaBuilderV2.App/Insertion/InsertRibbonControl.xaml.cs`
- Create: `src/ScadaBuilderV2.App/Insertion/ModernElementFactory.cs`
- Create: `src/ScadaBuilderV2.App/Insertion/InsertPlacementCoordinator.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Delete after migration: `src/ScadaBuilderV2.App/MainWindow.ElementFactory.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs`
- Modify: `src/ScadaBuilderV2.App/Resources/Icons.xaml`
- Modify: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs`

**Interfaces:**
- Consumes: `InsertToolCatalog` et descriptors Application.
- Produces: famille niveau 1, outils niveau 2, famille session, et placement generique incluant Tableau.

- [ ] Extraire factory, etat de placement et view models hors des partials `MainWindow`.
- [ ] Remplacer les cases `insert.*` individuelles du grand switch par une resolution generique de descriptor et une delegation au `InsertPlacementCoordinator`.
- [ ] Rendre les familles en niveau 1, les groupes/outils en niveau 2 et conserver la famille active pendant la session.
- [ ] Ajouter les icones vectorielles semantiques manquantes, incluant Tableau et familles, sans glyphes texte temporaires.
- [ ] Conserver la galerie Formes, les placements deux-points et les ids actuels sans regression.
- [ ] Supprimer les handlers `OnInsert*Click` non references et les types de ruban migres de `MainWindow.NestedTypes.cs`.
- [ ] Ajouter un test d'architecture qui echoue si le dispatch `MainWindow` contient un case individuel `insert.shape.*`, `insert.hmi.*`, `insert.button.*` ou une regle Tableau interdite.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~RibbonCommandCatalogTests|FullyQualifiedName~WebViewContextMenuScriptTests|FullyQualifiedName~TableEditorSurfaceContractTests"
```

Attendu : ruban hierarchique extensible et `MainWindow` limite au branchement de haut niveau.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/Insertion src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.App/MainWindow.NestedTypes.cs src/ScadaBuilderV2.App/Resources/Icons.xaml tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs tests/ScadaBuilderV2.Tests/TableEditorSurfaceContractTests.cs
git rm src/ScadaBuilderV2.App/MainWindow.ElementFactory.cs
git commit -m "refactor: add hierarchical insert ribbon outside main window"
```

---

## Phase 8 - Persistance, integration et validation produit

### Task 11: Verifier round-trip, performance et parcours complet

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/EditorHistoryServiceTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/NativePageDocumentTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/ModernTableIntegrationTests.cs`

**Interfaces:**
- Consumes: toutes les tranches precedentes.
- Produces: preuve creation -> edition -> sauvegarde -> reload -> preview -> `.sb2`.

- [ ] Construire un scenario 16 x 10 representatif de `win00012`, avec en-tete, alternance, colonne coloree, fusion et inputs.
- [ ] Verifier creation, resize externe, separateurs, format par portee, copie/collage, insertion/suppression, effacement, merge/unmerge et undo/redo.
- [ ] Verifier round-trip JSON et archive `.sb2` sans toucher au projet de reference.
- [ ] Mesurer creation, rendu et sauvegarde 64 x 64; documenter un seuil reproductible avant de figer un gate, sans virtualisation hors scope.
- [ ] Executer build, tests cibles puis suite complete :

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaTable|FullyQualifiedName~TableEdit|FullyQualifiedName~TableClipboard|FullyQualifiedName~TableWebView|FullyQualifiedName~TableEditorSurface|FullyQualifiedName~ModernTableIntegration|FullyQualifiedName~RibbonCommandCatalogTests|FullyQualifiedName~Ft100SceneExporterTests|FullyQualifiedName~NativePageDocumentTests|FullyQualifiedName~ModernProjectStoreTests|FullyQualifiedName~EditorHistoryServiceTests"
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu : tests cibles verts; suite complete egale ou meilleure que le baseline frais, sans nouvelle regression.

- [ ] Effectuer la verification manuelle sur une copie isolee d'un projet : creation 6 x 8 et 16 x 10, edition pane/dialog, menus, drag, couleurs, reload et inspection `.sb2`. Ne pas ouvrir ni sauvegarder `projects/AMR_REF_SCADA_V2`.
- [ ] Commit :

```powershell
git add tests/ScadaBuilderV2.Tests
git commit -m "test: cover modern table end to end"
```

### Task 12: Synchroniser contrats, decisions, cartes et version

**Files:**
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/02_architecture/MODULE_BOUNDARIES_V2.md`
- Modify: `docs/03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`
- Modify: `docs/04_editor/COMMANDS_CONTRACT_V2.md`
- Modify: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`
- Modify: `docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md`
- Modify: `docs/06_ui_ux/UI_ARCHITECTURE_V2.md`
- Modify: `docs/06_ui_ux/UI_SPECIFICATION_V2.md`
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md` only if a gap remains
- Modify: `docs/10_generated/CODE_MAP_V2.md`
- Modify: `docs/10_generated/COMMAND_FLOW_DIAGRAM_V2.md`
- Modify: `docs/10_generated/EXPORT_FLOW_DIAGRAM_V2.md`
- Modify: `docs/README.md`
- Modify: `docs/superpowers/specs/2026-07-14-modern-table-and-insert-ribbon-design.md`
- Modify: this plan
- Modify: `VERSION` only according to the approved implementation version decision

**Interfaces:**
- Consumes: commits et preuves des taches 1 a 11.
- Produces: documentation distinguant clairement decision, implementation, tests, gaps et version.

- [ ] Marquer `DEC-0039`, la spec et le plan comme implementes uniquement lorsque les preuves automatisees et manuelles applicables existent.
- [ ] Synchroniser les contrats proprietaires, diagrammes Mermaid, cartes generees, XML docs et couverture de regression.
- [ ] Appliquer `scada-builder-v2-versioning`: une implementation de cette capacite majeure peut justifier un feature bump; ne pas l'appliquer avant la tranche fonctionnelle complete et l'approbation du niveau de version.
- [ ] Executer :

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
rg -n "index\.html|08_web_modernized|source_html|Open[ ]Decisions|Document version|Historique des changements|PENDING" docs
```

Attendu : aucun nouvel ecart documentaire attribuable a la tranche; les erreurs historiques preexistantes sont consignees separement.

- [ ] Commit :

```powershell
git add VERSION docs
git commit -m "docs: record modern table implementation"
```

---

## Validation Checklist

- [ ] Spec approuvee et `DEC-0039` respectee sans decision produit inventee dans le plan.
- [ ] Le tableau est un seul Element+ persistant et round-trip.
- [ ] La capacite 64 x 64 et le scenario 16 x 10 sont valides.
- [ ] Largeurs et hauteurs sont modifiables par drag et dialogue numerique.
- [ ] Merge/unmerge, insertion/suppression, clear, format et clipboard sont undoable.
- [ ] Panneau droit, dialogue Tableau et dialogue Format utilisent les memes commandes/validateurs.
- [ ] Precedence par propriete et `Heriter` correspondent a la section 6.3.
- [ ] Inputs texte/numerique exportes sans `ValueBindings` et non persistants au runtime.
- [ ] Canevas WebView, preview native et `.sb2` consomment le meme modele.
- [ ] Aucun artefact editor-only ne se trouve dans le HTML/CSS exporte.
- [ ] Ruban Inserer a deux niveaux, ids existants stables, futurs outils clairement disabled.
- [ ] Aucun dispatch individuel ou regle Tableau detaillee ne demeure dans `MainWindow`.
- [ ] Aucun fichier de `projects/AMR_REF_SCADA_V2` n'est modifie ou stage par l'implementation.
- [ ] Build, tests cibles, suite complete, validation documentaire et verification manuelle isolee sont consignes.
