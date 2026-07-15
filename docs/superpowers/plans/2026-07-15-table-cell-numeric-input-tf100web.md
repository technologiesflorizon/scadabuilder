# Input numerique lie dans une cellule Tableau et TF100Web - Plan d'implementation

Date: 2026-07-15
Status: Draft implementation plan - pending execution approval
Document version: `V2.1.4.0038`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0038` | `PENDING` | Integration de la revue contre le code : valeurs `TableEditKind` et branches de dispatch rendues explicites; role runtime de `data-scada-step` distingue de l'attribut natif `step`. |
| 2026-07-15 | `V2.1.4.0037` | `PENDING` | Creation du plan executable cross-repo derive de la specification approuvee et de `DEC-0042`, avec TF100Web obligatoire avant le manifest `.sb2` 2.2. |

> **Pour les agents d'execution :** executer ce plan tache par tache. Ne pas deleguer a des sous-agents sans autorisation explicite de l'utilisateur. Chaque tache a sa propre frontiere de commit. Suspendre l'execution a tout gate explicite ou a toute divergence contractuelle non couverte par la specification.

**Goal:** Permettre a une cellule ancre de Tableau de type `InputNumeric` de porter des bindings lecture/ecriture, de conserver le comportement d'un InputNumeric Element+ standard dans TF100Web et de survivre correctement a la persistence, aux operations Tableau, au preview, au build et a l'export `.sb2`.

**Architecture:** SCADA Builder Domain porte le contenu, les bindings et les invariants structurels; Application porte les intentions, l'inspection, la securite et l'historique; App/WPF adapte le ruban, le panneau et le dialogue; Rendering produit le HTML et le manifest 2.2. TF100Web valide d'abord la version du package, extrait les cibles cellule dans un module Python dedie, puis reutilise le runtime numerique JavaScript commun sans moteur de polling ou d'ecriture propre aux Tableaux.

**Tech Stack:** C# 12, .NET 8, WPF/WebView2, `System.Text.Json`, MSTest, Python/Django, JavaScript navigateur, HTML/CSS, PowerShell et archives `.sb2`.

## Global Constraints

- Specification proprietaire : `docs/superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md`.
- Decision active : `DEC-0042`; `DEC-0039`, `DEC-0040` et `DEC-0041` demeurent actives pour le noyau Tableau et ses interactions.
- Depots : SCADA Builder V2 dans `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2` et TF100Web dans `F:\Projet\Git\TF100Web`.
- Branche commune obligatoire : `codex/adding-table-cell-numeric-input` dans les deux depots.
- Seul `InputNumeric` est inclus. Aucun binding `InputText`, aucune formule, aucune plage liee et aucun Element+ synthetique.
- `ValueBindings` appartient a la cellule ancre. L'identite runtime est `TableElementId + Row + Column`; aucun id de cellule persistant n'est ajoute.
- Copier/coller ne copie jamais les bindings. Les operations destructives les conservent, les bloquent ou demandent confirmation exactement selon la section 7 de la specification.
- Le manifest passe globalement a `2.2`, meme sans binding cellule. TF100Web compatible `2.2` doit etre livre avant tout exporteur SCADA Builder `2.2`.
- TF100Web accepte `2.1` et `2.2`, refuse explicitement une version absente ou inconnue, et ne degrade jamais silencieusement un package.
- Les attributs runtime ciblent le `<td>` page-scope; l'`<input type="number">` enfant est reutilise. `min` et `max` restent des attributs natifs de cet input.
- Preview, build et export consomment le meme modele. Les reperes A/1, selections, handles et autres artefacts editor-only restent exclus de `.sb2` et `.sep`.
- `MainWindow`, `frontend.views` et la commande de deploiement restent des orchestrateurs; aucune regle metier de binding cellule ne doit y etre implantee en ligne.
- Toute mutation persistante forme une seule action undo/redo et ne cree aucun dirty state lorsqu'elle est bloquee ou annulee.
- Toute API publique ajoutee recoit une documentation XML avec `Decisions: DEC-0042`, le contrat proprietaire et les tests dans `<remarks>`.
- Ne pas modifier, restaurer, supprimer ou stager `projects/AMR_REF_SCADA_V2`.
- Ne pas figer de nombre historique de tests. Comparer chaque execution a un baseline frais de sa branche.

---

## Before You Start

- [ ] Verifier branche et proprete des deux depots apres le commit documentaire.

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git branch --show-current
git status --short
Set-Location "F:\Projet\Git\TF100Web"
git branch --show-current
git status --short
```

Attendu : `codex/adding-table-cell-numeric-input` deux fois et aucun changement non commite. Si l'un des depots diverge, suspendre avant tout code.

- [ ] Confirmer l'approbation et les contrats de livraison.

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
rg -n "Status: Approved|DEC-0042|ManifestVersion.*2.2|TF100Web compatible 2.2" docs/superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md docs/00_governance/DECISION_REGISTER_V2.md
```

- [ ] Capturer les baselines frais avant modification de code.

```powershell
Set-Location "F:\Projet\Git\TF100Web"
.\.venv\Scripts\python.exe manage.py test frontend.tests_scada_deploy frontend.tests_scada_page_composition frontend.tests_scada_package -v 2

Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu : enregistrer les resultats comme baseline. Toute nouvelle regression doit etre corrigee avant le commit qui l'introduit.

- [ ] Capturer le baseline documentaire sans l'elargir.

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
```

Attendu au moment de la redaction : le validateur global signale 168 erreurs historiques de headers legacy. L'execution ne doit ajouter aucune erreur; leur correction globale reste hors scope.

---

## Phase 1 - TF100Web compatible manifest 2.2 en premier

### Task 1: Ajouter le gate de version du manifest au deploiement TF100Web

**Repository:** `F:\Projet\Git\TF100Web`

**Files:**
- Modify: `core/management/commands/deploy_scada_builder.py`
- Modify: `frontend/tests_scada_deploy.py`

**Interfaces:**
- Consumes: `manifest.json` extrait sous `SCADA_PACKAGE_DIR_NAME`.
- Produces/Changes: validation explicite `2.1`/`2.2` avant toute suppression ou copie dans `STATIC_ROOT/scada`.

- [ ] Ecrire d'abord les tests d'acceptation `2.1`, `2.2`, et de refus atomique pour version absente, invalide ou inconnue.
- [ ] Ajouter une fonction pure de lecture/validation de version, avec diagnostic contenant la version recue et les versions acceptees.
- [ ] Appeler le gate apres extraction et avant `deploy_package_to_static`; un refus ne doit pas effacer le package deja deploye.
- [ ] Mettre a jour `_build_test_package` pour annoncer explicitement sa version et verifier la preservation du comportement 2.1.
- [ ] Executer :

```powershell
.\.venv\Scripts\python.exe manage.py test frontend.tests_scada_deploy -v 2
```

- [ ] Commit TF100Web :

```powershell
git add core/management/commands/deploy_scada_builder.py frontend/tests_scada_deploy.py
git commit -m "feat: gate SCADA manifest versions at deployment"
```

### Task 2: Extraire et composer les cibles `TableCellBindings`

**Repository:** `F:\Projet\Git\TF100Web`

**Files:**
- Create: `frontend/scada_table_bindings.py`
- Modify: `frontend/views.py`
- Inspect/Modify if required by failing regression: `frontend/scada_builder_composition.py`
- Modify: `frontend/tests_scada_page_composition.py`
- Modify: `frontend/tests_scada_package.py`

**Interfaces:**
- Consumes: `Objects[].TableCellBindings[]` et le parseur existant `_binding_config_from_manifest_object`.
- Produces/Changes: `iter_table_cell_binding_targets(page)`, `target_dom_id(page_id, binding)`, `validate_table_cell_binding(binding)` et cibles page-scopees normalisees.

- [ ] Creer le module stateless qui valide `Row`, `Column`, `TargetId`, `Kind == InputNumeric`, `Data` et `ValueBindings`, sans importer `views.py`.
- [ ] Faire conserver integralement `TableCellBindings` par la composition header/body/footer. Si le code le fait deja, ajouter seulement la regression; modifier le composeur uniquement si le test echoue.
- [ ] Etendre `_manifest_scada_bindings` pour enumerer les bindings objets puis cellules, et reutiliser `_binding_config_from_manifest_object` pour resoudre lecture/ecriture.
- [ ] Ignorer une entree cellule invalide avec warning structure; ne jamais rabattre sa cible sur le wrapper Tableau.
- [ ] Injecter sur le `<td>` cible `data-scada-role`, ids de mapping, writeable/writable, format et step. Ne pas injecter `data-scada-min` ou `data-scada-max`.
- [ ] Conserver explicitement `data-scada-step` sur le `<td>` : le runtime commun lit `node.dataset.scadaStep` sur le noeud cible passe a `makeNumericControl`. L'attribut natif `step` deja present sur l'input enfant reste en parallele la contrainte navigateur; les deux ont des responsabilites distinctes.
- [ ] Tester page seule et composition header/body/footer, cible page-scopee, bindings lecture/ecriture distincts, lecture seule, warning invalide et absence de mutation du Tableau wrapper.
- [ ] Executer :

```powershell
.\.venv\Scripts\python.exe manage.py test frontend.tests_scada_page_composition frontend.tests_scada_package -v 2
```

- [ ] Commit TF100Web :

```powershell
git add frontend/scada_table_bindings.py frontend/views.py frontend/scada_builder_composition.py frontend/tests_scada_page_composition.py frontend/tests_scada_package.py
git commit -m "feat: intake numeric table cell bindings"
```

### Task 3: Reutiliser l'input numerique enfant dans le runtime commun

**Repository:** `F:\Projet\Git\TF100Web`

**Files:**
- Modify: `static/asset/js/station/visualisation_import.js`
- Modify: `frontend/tests_scada_package.py`

**Interfaces:**
- Consumes: noeud trouve par `initBindings()` via `[data-scada-role][data-scada-mapping-id]`.
- Produces/Changes: `resolveNumericInput(node)`, `configureNumericInput(node, input)` et `makeNumericControl(node)` non destructif pour un `<td>` contenant deja un input.

- [ ] Garder le selecteur `initBindings()` et faire appeler `makeNumericControl(td)` comme pour tout wrapper lie.
- [ ] Implementer `resolveNumericInput` avec `node.querySelector('input[type="number"]')` en premier; creer un input seulement lorsqu'aucun enfant compatible n'existe.
- [ ] Appeler `node.replaceChildren(input)` uniquement pour la creation d'un InputNumeric Element+ standard; ne jamais remplacer les enfants d'une cellule Tableau existante.
- [ ] Centraliser placeholder, step, readonly, handlers et feedback dans `configureNumericInput`; conserver les attributs natifs `min`/`max` deja rendus.
- [ ] Garder `commitNumeric`, `applyValue`, le polling, CSRF, permissions et mapping ecriture communs aux deux formes.
- [ ] Ajouter une preuve executable ou de contrat couvrant input standard cree, input enfant reutilise, valeur poll, Enter/blur, Escape, erreur, readonly et mapping ecriture distinct.
- [ ] Executer :

```powershell
.\.venv\Scripts\python.exe manage.py test frontend.tests_scada_package -v 2
```

- [ ] Commit TF100Web :

```powershell
git add static/asset/js/station/visualisation_import.js frontend/tests_scada_package.py
git commit -m "feat: reuse rendered numeric controls in SCADA runtime"
```

### Task 4: Fermer la tranche TF100Web et etablir le gate de livraison

**Repository:** `F:\Projet\Git\TF100Web`

**Files:**
- Modify only if required by test evidence: `frontend/tests_scada_deploy.py`
- Modify only if required by test evidence: `frontend/tests_scada_page_composition.py`
- Modify only if required by test evidence: `frontend/tests_scada_package.py`

- [ ] Construire un package de test 2.2 minimal avec un `<td><input type="number"></td>`, `TableCellBindings`, mappings lecture/ecriture differents et attributs `min`/`max` natifs.
- [ ] Tester deploiement, composition, injection d'attributs sur `<td>`, conservation de l'input enfant et compatibilite d'un package 2.1 existant.
- [ ] Executer les suites ciblees puis la suite `frontend` complete :

```powershell
.\.venv\Scripts\python.exe manage.py test frontend.tests_scada_deploy frontend.tests_scada_page_composition frontend.tests_scada_package -v 2
.\.venv\Scripts\python.exe manage.py test frontend -v 2
```

- [ ] Commit TF100Web seulement si des preuves supplementaires ont ete ajoutees :

```powershell
git add frontend/tests_scada_deploy.py frontend/tests_scada_page_composition.py frontend/tests_scada_package.py
git commit -m "test: verify table cell binding package intake"
```

### Gate A - Autorisation de commencer l'export SCADA manifest 2.2

- [ ] TF100Web accepte `2.1` et `2.2`, refuse les versions inconnues avant mutation du deploiement, et toutes les suites Phase 1 passent.
- [ ] Les commits TF100Web sont sur `codex/adding-table-cell-numeric-input` et disponibles pour revue/livraison.
- [ ] Ne pas activer `ManifestVersion = 2.2` dans SCADA Builder avant ce gate. Le deploiement sur un environnement partage ou de production exige une autorisation utilisateur distincte.

---

## Phase 2 - Modele, invariants et persistence SCADA Builder V2

### Task 5: Ajouter le modele persistant des inputs numeriques lies

**Repository:** `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableModels.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`

**Interfaces:**
- Produces/Changes: `ScadaTableCellContent.DisplayFormat`, `ScadaTableCellValueBindings`, `ScadaTableCell.ValueBindings`.

- [ ] Ajouter les nouveaux champs nullables comme derniers parametres des records afin de conserver la lecture JSON existante.
- [ ] Documenter les APIs publiques avec `DEC-0042` et les contrats/tests proprietaires.
- [ ] Tester ancien JSON sans champs, round-trip complet, omission/null, bindings lecture seule, ecriture seule et distincts.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~ScadaTableModelTests|FullyQualifiedName~ModernProjectStoreTests"
```

- [ ] Commit SCADA Builder :

```powershell
git add src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableModels.cs tests/ScadaBuilderV2.Tests/ScadaTableModelTests.cs tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs
git commit -m "feat: persist numeric table cell bindings"
```

### Task 6: Implementer les operations et protections de bindings cellule

**Repository:** `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`

**Files:**
- Create: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableCellBindingOperations.cs`
- Modify: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableContentOperations.cs`
- Modify: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableStructureOperations.cs`
- Modify: `src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableOperations.cs`
- Modify: `src/ScadaBuilderV2.Application/Tables/TableClipboard.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableContentOperationsTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableCellBindingOperationsTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/ScadaTableOperationsTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableClipboardTests.cs`

**Interfaces:**
- Produces/Changes: `SetBinding`, `RemoveBinding`, `GetBinding`, `EnumerateBindings`, `CountBindings`, `ValidateEditableNumericTarget` et invariants de la section 7.

- [ ] Implementer les operations immuables sur cellule ancre et la redirection d'une cellule fusionnee vers son ancre.
- [ ] Faire suivre contenu et bindings lors d'insertions; conserver le binding de l'ancre a la fusion/defusion; refuser une fusion absorbant une autre cellule liee.
- [ ] Preserver type et bindings lors de `ClearContent` d'une cellule liee et effacer seulement la valeur initiale.
- [ ] Garantir que copier/coller n'inclut jamais `ValueBindings`; un collage numerique ou vide conserve les bindings cibles et un collage non numerique est refuse atomiquement.
- [ ] Tester insert/delete, merge/unmerge, conversion, clear, plage fusionnee, copier/coller interne et TSV, ainsi que undo/redo via le coordinateur existant lorsque applicable.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableCellBindingOperationsTests|FullyQualifiedName~TableContentOperationsTests|FullyQualifiedName~ScadaTableOperationsTests|FullyQualifiedName~TableClipboardTests"
```

- [ ] Commit SCADA Builder :

```powershell
git add src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableCellBindingOperations.cs src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableContentOperations.cs src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableStructureOperations.cs src/ScadaBuilderV2.Domain/Scenes/Tables/ScadaTableOperations.cs src/ScadaBuilderV2.Application/Tables/TableClipboard.cs tests/ScadaBuilderV2.Tests/TableCellBindingOperationsTests.cs tests/ScadaBuilderV2.Tests/TableContentOperationsTests.cs tests/ScadaBuilderV2.Tests/ScadaTableOperationsTests.cs tests/ScadaBuilderV2.Tests/TableClipboardTests.cs
git commit -m "feat: protect table cell bindings across edits"
```

### Task 7: Ajouter inspection, securite, requetes typees et validation projet

**Repository:** `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`

**Files:**
- Create: `src/ScadaBuilderV2.Application/Tables/TableCellNumericInputInspector.cs`
- Create: `src/ScadaBuilderV2.Application/Tables/TableBindingSafetyPolicy.cs`
- Modify: `src/ScadaBuilderV2.Application/Tables/TableEditCoordinator.cs`
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs`

**Interfaces:**
- Produces/Changes: valeurs `TableEditKind.SetNumericInputProperties`, `TableEditKind.SetCellValueBinding`, `TableEditKind.RemoveCellValueBinding`; champs de requete `BindingKind`, `TagId`, `ConfirmedBindingRemoval`; inspection contextuelle et diagnostics `Elements[id].Table.Cells[row,column]`.

- [ ] Faire retourner par la politique le nombre de bindings touches et un resultat `Allowed`, `RequiresConfirmation` ou `Blocked`, sans dialogue ou callback UI.
- [ ] Ajouter les trois valeurs a `TableEditKind`, leurs branches exhaustives dans le switch prive `Apply(ScadaTableDefinition, TableEditRequest)` et leurs libelles dans `ResolveLabel`; aucune nouvelle intention ne doit tomber dans un chemin generique ou implicite.
- [ ] Etendre les requetes/resultats de facon additive; conserver l'element original et aucun dirty state si la confirmation manque ou si la politique bloque.
- [ ] Valider lecture sur tout tag actif, ecriture uniquement sur tag actif ecrivable, read-only sans binding ecriture, min/max/step/valeur initiale et `DisplayFormat` supporte.
- [ ] Faire deleguer `ValidateSceneValueBindings` aux validateurs objet et cellule; produire les chemins stables de la specification.
- [ ] Tester cellule unique, cellule fusionnee, plage non eligible au binding, conversion confirmee/annulee, suppression row/column confirmee/annulee, writeable/read-only et diagnostics exacts.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableEditCoordinatorTests|FullyQualifiedName~OfficialSceneDomainTests"
```

- [ ] Commit SCADA Builder :

```powershell
git add src/ScadaBuilderV2.Application/Tables/TableCellNumericInputInspector.cs src/ScadaBuilderV2.Application/Tables/TableBindingSafetyPolicy.cs src/ScadaBuilderV2.Application/Tables/TableEditCoordinator.cs src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs tests/ScadaBuilderV2.Tests/OfficialSceneDomainTests.cs
git commit -m "feat: coordinate numeric table cell binding edits"
```

---

## Phase 3 - Surfaces WPF et parite d'edition

### Task 8: Creer le view model et le dialogue Input numerique Tableau

**Repository:** `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`

**Files:**
- Create: `src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesViewModel.cs`
- Create: `src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesDialog.xaml`
- Create: `src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesDialog.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj` only if explicit XAML inclusion is required by the current project style
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`

**Interfaces:**
- Consumes: `TableCellNumericInputInspector`, catalogue `ScadaTagCatalog` et intentions du coordinateur.
- Produces/Changes: etat partage des proprietes numeriques, options lecture/ecriture filtrees, validation et commandes Ajouter/Modifier/Supprimer.

- [ ] Exposer valeur initiale, placeholder, min, max, step, format, read-only et resumes lecture/ecriture dans un view model sans reference a `MainWindow`.
- [ ] Reutiliser les conventions du picker de tags existant; filtrer le picker d'ecriture sur `Writeable == true`.
- [ ] Presenter les confirmations requises par la politique, puis renvoyer une intention confirmee au coordinateur sans muter le modele dans le code-behind.
- [ ] Tester par contrat d'architecture que le code-behind collecte seulement l'intention et qu'aucune regle de binding n'entre dans XAML.cs ou `MainWindow`.
- [ ] Executer :

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableUiArchitectureTests|FullyQualifiedName~TableEditCoordinatorTests"
```

- [ ] Commit SCADA Builder :

```powershell
git add src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesViewModel.cs src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesDialog.xaml src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesDialog.xaml.cs src/ScadaBuilderV2.App/ScadaBuilderV2.App.csproj tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs
git commit -m "feat: add numeric table cell properties dialog"
```

### Task 9: Brancher ruban, panneau Propriete et controller sans logique MainWindow

**Repository:** `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`

**Files:**
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableEditorController.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableRibbonViewModel.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TablePropertiesViewModel.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs` only for short delegated handlers or initialization
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TablePropertiesInspectorTests.cs`

**Interfaces:**
- Produces/Changes: `OpenNumericInputProperties`, `SetCellBinding`, `RemoveCellBinding` et groupe contextuel `Input numerique` actif pour une cellule ancre en mode Cellules.

- [ ] Injecter au controller un fournisseur du catalogue actif et le callback de commit Tableau existant.
- [ ] Synchroniser le meme etat inspecte dans ruban, panneau et dialogue; une plage peut convertir son type mais ne recoit aucun binding collectif.
- [ ] Desactiver les commandes si aucun Tableau, mode Objet, aucune cellule ancre unique, ou contenu non numerique tant que la conversion n'est pas choisie.
- [ ] Faire passer chaque mutation par le commit Tableau existant pour dirty state et historique atomiques.
- [ ] Garder dans `MainWindow.TableIntegration.cs` uniquement selection, delegation et refresh; aucun filtre de tag, validation, confirmation ou mutation de cellule en ligne.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableUiArchitectureTests|FullyQualifiedName~TablePropertiesInspectorTests|FullyQualifiedName~TableEditCoordinatorTests"
```

- [ ] Commit SCADA Builder :

```powershell
git add src/ScadaBuilderV2.App/TableEditor/TableEditorController.cs src/ScadaBuilderV2.App/TableEditor/TableRibbonViewModel.cs src/ScadaBuilderV2.App/TableEditor/TablePropertiesViewModel.cs src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs src/ScadaBuilderV2.App/MainWindow.xaml src/ScadaBuilderV2.App/MainWindow.xaml.cs tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs tests/ScadaBuilderV2.Tests/TablePropertiesInspectorTests.cs
git commit -m "feat: expose numeric bindings in table authoring surfaces"
```

### Task 10: Preserver l'etat numerique dans l'editeur WebView et le preview

**Repository:** `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`

**Files:**
- Inspect/Modify if required: `src/ScadaBuilderV2.App/TableEditor/TableWebViewScript.cs`
- Inspect/Modify if required: `src/ScadaBuilderV2.App/TableEditor/TableEditorWebViewStateFactory.cs`
- Modify: `src/ScadaBuilderV2.Rendering/ModernTableHtmlRenderer.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableEditorWebViewStateTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/PreviewDocumentTests.cs`

- [ ] Verifier que le payload editor preserve `DisplayFormat`, valeur, placeholder, min, max, step et readonly sans exposer les ids de mapping au DOM d'edition.
- [ ] Faire rendre par `ModernTableHtmlRenderer` un `<td>` stable contenant l'`<input type="number">` avec ses attributs natifs; `DisplayFormat` reste disponible pour le manifest/runtime sans remplacer le contenu.
- [ ] Tester parite save/reload/preview, ids cellule/input page-scopes, absence d'artefacts editor-only et dimensions/grille invariantes.
- [ ] Modifier les fichiers `TableWebView*` uniquement si une regression executable prouve que la serialisation automatique ne suffit pas.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableEditorWebViewStateTests|FullyQualifiedName~PreviewDocumentTests|FullyQualifiedName~ModernProjectStoreTests"
```

- [ ] Commit SCADA Builder :

```powershell
git add src/ScadaBuilderV2.App/TableEditor/TableWebViewScript.cs src/ScadaBuilderV2.App/TableEditor/TableEditorWebViewStateFactory.cs src/ScadaBuilderV2.Rendering/ModernTableHtmlRenderer.cs tests/ScadaBuilderV2.Tests/TableEditorWebViewStateTests.cs tests/ScadaBuilderV2.Tests/PreviewDocumentTests.cs
git commit -m "feat: preserve numeric table inputs through preview"
```

---

## Phase 4 - Export `.sb2` 2.2 apres Gate A

### Task 11: Construire `Objects[].TableCellBindings` et basculer le manifest a 2.2

**Repository:** `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`

**Files:**
- Create: `src/ScadaBuilderV2.Rendering/Ft100TableCellBindingManifestBuilder.cs`
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**
- Produces/Changes: builder stateless des seules cellules numeriques liees, `TargetId` non scope et `ManifestVersion = "2.2"`.

- [ ] Verifier Gate A avant ce changement.
- [ ] Faire construire au builder `Row`, `Column`, `TargetId`, `Kind`, `Data` et `ValueBindings` sans parsing de cellules dans `BuildManifestPage`.
- [ ] Omettre les cellules sans binding et ne jamais ajouter de faux objet pour une cellule.
- [ ] Conserver le `TargetId` non page-scope dans le manifest et le DOM reel `ft100-<page>__<TargetId>`.
- [ ] Basculer toutes les emissions de version hardcodees de `2.1` a `2.2`, y compris pour un package sans binding cellule.
- [ ] Tester lecture seule, lecture/ecriture distinctes, min/max/step/format, cellule fusionnee, plusieurs pages, absence de bindings et non-regression des `Objects[].ValueBindings` existants.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~Ft100SceneExporterTests|FullyQualifiedName~PreviewDocumentTests"
```

- [ ] Commit SCADA Builder :

```powershell
git add src/ScadaBuilderV2.Rendering/Ft100TableCellBindingManifestBuilder.cs src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "feat: export numeric table bindings in manifest 2.2"
```

### Task 12: Etendre la validation package et fermer l'integration `.sb2`

**Repository:** `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs`
- Create: `tests/ScadaBuilderV2.Tests/Ft100PackageValidatorTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

- [ ] Valider schema 2.2, type `InputNumeric`, coordonnees/ancre, `TargetId` unique, presence exacte du `<td>` page-scope et absence de binding runtime sur le wrapper Tableau.
- [ ] Bloquer tag inexistant/non ecrivable, readonly avec ecriture, format/contraintes invalides et collision DOM avec diagnostic de cellule stable.
- [ ] Exporter un `.sb2` de test, relire son manifest et son HTML depuis l'archive, et prouver la coherence `TableCellBindings`/DOM.
- [ ] Preserver les validations 2.1 historiques sur les fixtures existantes lorsque le validateur lit un ancien package; l'exporteur courant produit toujours 2.2.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~Ft100PackageValidatorTests|FullyQualifiedName~Ft100SceneExporterTests"
dotnet test ScadaBuilderV2.sln --no-restore
```

- [ ] Commit SCADA Builder :

```powershell
git add src/ScadaBuilderV2.Rendering/Ft100PackageValidation.cs tests/ScadaBuilderV2.Tests/Ft100PackageValidatorTests.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "test: validate table cell binding packages"
```

### Gate B - Preuve cross-repo avant livraison

- [ ] Exporter avec SCADA Builder un package 2.2 contenant au minimum une cellule lecture/ecriture, une lecture seule et une cellule numerique non liee.
- [ ] Deployer ce package dans une copie locale TF100Web au commit de Phase 1, jamais dans un environnement partage sans autorisation explicite.
- [ ] Verifier composition, affichage initial, polling, focus, Enter, blur, Escape, erreur, min/max natifs, mapping ecriture distinct et absence de reflow du Tableau.
- [ ] Verifier aussi un package 2.1 connu dans le meme TF100Web.
- [ ] Si aucun mapping reel et environnement autorise ne sont disponibles, marquer le smoke reel comme gate ouvert; ne pas declarer l'implementation complete.

---

## Phase 5 - Synchronisation documentaire et livraison ordonnee

### Task 13: Synchroniser les contrats apres preuves, puis preparer les merges

**Repository:** `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2`

**Files:**
- Modify: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md`
- Modify: `docs/03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md`
- Modify: `docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md`
- Modify: `docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md`
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md`
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md`
- Modify: `docs/superpowers/plans/2026-07-15-table-cell-numeric-input-tf100web.md`
- Modify: `docs/README.md`
- Modify: `VERSION`

- [ ] Documenter seulement les comportements prouves et conserver toute validation manuelle non executee comme gate explicite.
- [ ] Passer `DEC-0042`, la specification et le plan au statut implemente uniquement apres Gate B et suites completes.
- [ ] Appliquer l'increment de version SCADA Builder selon `V2.production.feature.iteration`, puis mettre a jour les historiques avec les commits reels.
- [ ] Executer :

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
git diff --check
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu : aucune nouvelle erreur documentaire au-dela du baseline capture; aucune regression build/test.

- [ ] Commit SCADA Builder :

```powershell
git add docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md docs/03_runtime_contracts/PROJECT_MODEL_CONTRACT_V2.md docs/03_runtime_contracts/PREVIEW_BUILD_EXPORT_CONTRACT_V2.md docs/04_editor/PROPERTIES_PANEL_CONTRACT_V2.md docs/04_editor/MENUS_AND_SURFACES_CONTRACT_V2.md docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md docs/08_implementation_status/REGRESSION_COVERAGE_V2.md docs/08_implementation_status/KNOWN_GAPS_V2.md docs/00_governance/DECISION_REGISTER_V2.md docs/superpowers/specs/2026-07-15-table-cell-numeric-input-tf100web-design.md docs/superpowers/plans/2026-07-15-table-cell-numeric-input-tf100web.md docs/README.md VERSION
git commit -m "docs: record numeric table cell runtime support"
```

### Gate C - Ordre de merge et de deploiement

1. [ ] Pousser et faire reviser la branche TF100Web.
2. [ ] Merger TF100Web sur `main` et deployer sa compatibilite manifest 2.1/2.2 avec autorisation utilisateur.
3. [ ] Confirmer le smoke TF100Web sur package 2.1 puis 2.2.
4. [ ] Pousser et faire reviser la branche SCADA Builder V2.
5. [ ] Merger SCADA Builder sur `master` seulement apres confirmation que les cibles TF100Web acceptent 2.2.
6. [ ] Ne jamais inverser cet ordre : un export SCADA Builder 2.2 est volontairement incompatible avec un TF100Web non mis a jour.

---

## Definition of Done

- [ ] Une cellule ancre `InputNumeric` configure lecture/ecriture avec les memes regles qu'un InputNumeric Element+ standard.
- [ ] Persistence, undo/redo, operations structurelles, clear et copier/coller respectent integralement les bindings.
- [ ] Ruban Tableau, panneau Propriete et dialogue partagent inspection et validation sans logique metier dans `MainWindow`.
- [ ] Le manifest 2.2 contient uniquement les `TableCellBindings` valides et le HTML conserve un input enfant natif dans un `<td>` page-scope.
- [ ] TF100Web accepte 2.1/2.2, refuse les versions inconnues atomiquement, injecte sur le `<td>` et reutilise l'input sans `replaceChildren` destructif.
- [ ] Les tests cibles et complets des deux depots passent par rapport aux baselines frais.
- [ ] Le smoke local cross-repo passe, puis le smoke mapping reel passe sur environnement explicitement autorise.
- [ ] TF100Web est livre avant SCADA Builder 2.2.
- [ ] Les contrats et statuts documentaires correspondent aux preuves reelles; aucune capacite `InputText` n'est revendiquee.
