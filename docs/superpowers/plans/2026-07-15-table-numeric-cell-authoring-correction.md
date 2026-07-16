# Correction de l'authoring des cellules Tableau InputNumeric - Plan d'implementation

Date: 2026-07-15
Status: Draft implementation plan - pending execution approval
Document version: `V2.1.4.0040`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-15 | `V2.1.4.0040` | `PENDING` | Plan cree depuis la specification corrective approuvee `DEC-0043`. |

> **Pour les agents d'execution :** executer ce plan tache par tache. Chaque tache commence par des tests en echec, se termine par les validations indiquees et possede sa propre frontiere de commit.

**Goal:** Remplacer les trois commandes redondantes du ruban par une configuration unique et sure, afficher l'identite A1 de la cellule, refuser les selections perimees et initialiser automatiquement la lecture depuis l'ecriture lorsqu'aucune lecture n'est configuree.

**Architecture:** Application possede l'identite de selection, le format A1 et la politique de binding. App/WPF affiche le snapshot et route les intentions. WebView emet un message type pour le double-clic. Domain, Rendering, manifest 2.2 et TF100Web restent inchanges.

**Tech Stack:** C# 12, .NET 8, WPF/WebView2, JavaScript embarque, `System.Text.Json`, MSTest, PowerShell

## Global Constraints

- Specification : `docs/superpowers/specs/2026-07-15-table-numeric-cell-authoring-correction-design.md` (`DEC-0043`).
- Conserver le modele et le runtime de `DEC-0042`.
- Ne pas ajouter de champ persistant, de migration de scene ou de version manifest.
- Ne pas modifier TF100Web.
- Ne pas calculer l'adresse A1 ou le fallback de binding dans `MainWindow`.
- Les modifications de proprietes et bindings doivent rester un commit Tableau atomique et annulable.
- Une lecture distincte de l'ecriture doit rester possible.
- Les fichiers deja modifies sous `projects/AMR_REF_SCADA_V2` appartiennent a l'utilisateur et ne doivent jamais etre restaures, supprimes ou stages par ce plan.

---

## Before You Start

- [ ] Verifier la specification et la decision :

```powershell
rg -n "Status: Approved|DEC-0043" docs/superpowers/specs/2026-07-15-table-numeric-cell-authoring-correction-design.md docs/00_governance/DECISION_REGISTER_V2.md
```

- [ ] Verifier branche et worktree :

```powershell
git branch --show-current
git status --short
```

Attendu : la branche part de `codex/adding-table-cell-numeric-input`. Le worktree actuel contient des changements utilisateur sous `projects/AMR_REF_SCADA_V2`; avant l'implementation, les committer selon leur propre frontiere ou executer ce plan dans un worktree propre. Ne pas les inclure dans les commits ci-dessous.

- [ ] Capturer un baseline frais :

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu : consigner le resultat reel; toute nouvelle regression doit etre investiguee avant commit.

---

## Phase 1 - Selection fiable et adresse A1

### Task 1: Porter l'identite de cellule dans l'inspection Application

**Files:**
- Modify: `src/ScadaBuilderV2.Application/Tables/TableCellNumericInputInspector.cs`
- Create: `src/ScadaBuilderV2.Application/Tables/TableCellAddress.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableEditorController.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TablePropertiesViewModel.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs`
- Create: `tests/ScadaBuilderV2.Tests/TableCellAddressTests.cs`

**Interfaces:**
- Consumes: id Tableau courant, id associe a la selection, plage et cellules effectives.
- Produces: inspection avec `TableElementId`, ancre effective, `CellAddress` A1 et diagnostic de selection perimee.

- [ ] Ecrire les tests rouges de conversion A1 : A1, Z1, AA1, J7 et BL64.
- [ ] Etendre `TableCellNumericInputInspection` avec l'id Tableau et l'adresse A1 derives de l'ancre.
- [ ] Ajouter a `Inspect` l'id proprietaire de la selection et refuser l'inspection si cet id differe de `element.Id`.
- [ ] Faire passer `TableEditorController.ElementId` a l'inspecteur; ne plus inspecter une plage sans preuve de provenance.
- [ ] Ajouter une operation explicite de reset du contexte cellule lors d'une deselection de Tableau.
- [ ] Verifier les cellules fusionnees : l'adresse exposee est celle de l'ancre effective.
- [ ] Ajouter la documentation XML avec `DEC-0043`, la specification et les tests.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableCellAddressTests|FullyQualifiedName~TableEditCoordinatorTests"
```

Attendu : une selection issue d'un autre Tableau retourne `CanEditProperties == false`; toutes les adresses A1 attendues sont exactes.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.Application/Tables/TableCellNumericInputInspector.cs src/ScadaBuilderV2.Application/Tables/TableCellAddress.cs src/ScadaBuilderV2.App/TableEditor/TableEditorController.cs src/ScadaBuilderV2.App/TableEditor/TablePropertiesViewModel.cs tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs tests/ScadaBuilderV2.Tests/TableCellAddressTests.cs
git commit -m "fix: validate numeric table cell selection identity"
```

---

## Phase 2 - Politique Ecrire vers Lire

### Task 2: Initialiser explicitement la lecture depuis l'ecriture

**Files:**
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesViewModel.cs`
- Modify: `src/ScadaBuilderV2.Application/Tables/TableEditCoordinator.cs` only if a non-WPF orchestration hook is required
- Modify: `tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`

**Interfaces:**
- Consumes: draft `ReadTagId`, draft `WriteTagId`, bindings persistants et catalogue actif.
- Produces: intentions typees ou `ReadTagId = WriteTagId` seulement lorsque la lecture est vide.

- [ ] Ecrire d'abord la matrice de tests : rien/rien, write seul, read distinct + write, suppression write avec read, read-only.
- [ ] Centraliser la normalisation dans une methode Application/view-model testable, sans logique dans le dialogue code-behind.
- [ ] Lorsque write est renseigne et read vide, produire un `SetCellValueBinding` Read avant Write avec le meme tag id.
- [ ] Lorsque read est distinct, ne jamais le remplacer.
- [ ] Lorsque write est retire, conserver une lecture explicite existante.
- [ ] Conserver une seule action d'historique via `ApplyRequests`.
- [ ] Exposer un indicateur de draft permettant au dialogue d'afficher `Lecture automatiquement alignee sur Ecrire`.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableEditCoordinatorTests|Name=NumericCellDialogDelegatesValidationAndReturnsTypedIntentions"
```

Attendu : write seul produit deux bindings explicites; read distinct reste inchange; aucune logique de fallback n'apparait dans `MainWindow` ou Rendering.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesViewModel.cs src/ScadaBuilderV2.Application/Tables/TableEditCoordinator.cs tests/ScadaBuilderV2.Tests/TableEditCoordinatorTests.cs tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs
git commit -m "fix: default numeric cell reads to write mapping"
```

---

## Phase 3 - Surface unique et double-clic

### Task 3: Remplacer les trois commandes par Configurer et afficher la cible

**Files:**
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableRibbonViewModel.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesDialog.xaml`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesDialog.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.xaml`
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs`

**Interfaces:**
- Consumes: `TableCellNumericInputInspection` courant.
- Produces: un seul bouton `table.numeric.properties`, libelle `Configurer <A1>` et dialogue identifie par Tableau/cellule.

- [ ] Ecrire un test rouge exigeant exactement une commande dans le groupe `Input numerique`.
- [ ] Retirer les commandes visibles `table.binding.read` et `table.binding.write` ainsi que leurs branches de dispatch devenues inutiles.
- [ ] Garder `table.numeric.properties` comme id stable de la commande unique.
- [ ] Afficher `Configurer J7` lorsque l'inspection est valide et `Configurer` desactive avec diagnostic lorsqu'elle ne l'est pas.
- [ ] Ajouter dans le dialogue un bandeau `Tableau: <id> | Cellule: <A1>` alimente par le view model.
- [ ] Afficher la meme adresse A1 dans le panneau Propriete, sans recalcul dans `MainWindow`.
- [ ] Conserver les trois sections sur une page; ne pas introduire de `TabControl`.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableUiArchitectureTests|FullyQualifiedName~RibbonCommandCatalogTests"
```

Attendu : une seule commande de configuration est visible et toutes les surfaces affichent le meme snapshot d'identite.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor/TableRibbonViewModel.cs src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesDialog.xaml src/ScadaBuilderV2.App/TableEditor/TableNumericInputPropertiesDialog.xaml.cs src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs src/ScadaBuilderV2.App/MainWindow.xaml.cs src/ScadaBuilderV2.App/MainWindow.xaml tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs tests/ScadaBuilderV2.Tests/RibbonCommandCatalogTests.cs
git commit -m "fix: simplify numeric table cell configuration"
```

### Task 4: Ouvrir la configuration numerique au double-clic

**Files:**
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableWebViewMessages.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableWebViewMessageAdapter.cs`
- Modify: `src/ScadaBuilderV2.App/TableEditor/TableWebViewScript.cs`
- Modify: `src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableWebViewMessageAdapterTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs`

**Interfaces:**
- Consumes: double-clic sur cellule et `contentKind` rendu.
- Produces: message type d'ouverture pour une cellule `InputNumeric`; edition inline inchangee pour `Text`.

- [ ] Ajouter un schema type, par exemple `tableOpenNumericProperties`, avec id Tableau, row et column valides.
- [ ] Adapter le handler `dblclick` : selectionner l'ancre, emettre la selection, puis demander l'ouverture si `contentKind == InputNumeric`.
- [ ] Conserver l'edition inline actuelle pour `Text`; ne pas ouvrir la modale pour `InputText` dans cette tranche.
- [ ] Dans l'integration WPF, verifier une seconde fois l'id et les coordonnees avant d'ouvrir le dialogue.
- [ ] Ajouter les tests de message valide, id absent, coordonnee negative, type non supporte et non-regression texte.
- [ ] Executer :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableWebViewMessageAdapterTests|FullyQualifiedName~TableUiArchitectureTests"
```

Attendu : le double-clic numerique ouvre la cible emettrice et un message forge ne peut pas reutiliser une autre selection.

- [ ] Commit :

```powershell
git add src/ScadaBuilderV2.App/TableEditor/TableWebViewMessages.cs src/ScadaBuilderV2.App/TableEditor/TableWebViewMessageAdapter.cs src/ScadaBuilderV2.App/TableEditor/TableWebViewScript.cs src/ScadaBuilderV2.App/MainWindow.TableIntegration.cs tests/ScadaBuilderV2.Tests/TableWebViewMessageAdapterTests.cs tests/ScadaBuilderV2.Tests/TableUiArchitectureTests.cs
git commit -m "fix: open numeric table cell configuration on double click"
```

---

## Phase 4 - Persistance, export et cloture

### Task 5: Verifier la parite et synchroniser la documentation

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`
- Modify: `docs/00_governance/DECISION_REGISTER_V2.md`
- Modify: `docs/06_ui_ux/UI_SPECIFICATION_V2.md`
- Modify: `docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md`
- Modify: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`
- Modify: `docs/08_implementation_status/KNOWN_GAPS_V2.md` only if a gap remains
- Modify: `docs/README.md`
- Modify: `docs/superpowers/specs/2026-07-15-table-numeric-cell-authoring-correction-design.md`
- Modify: this plan
- Modify: `VERSION`

**Interfaces:**
- Consumes: implementation validee et scene avec write seul normalisee par l'authoring.
- Produces: preuve de round-trip, manifest 2.2 inchange et statut documentaire exact.

- [ ] Ajouter un round-trip prouvant que write seul en draft devient deux ids persistants identiques apres enregistrement.
- [ ] Ajouter un test export prouvant que le manifest contient explicitement les deux ids et conserve une paire distincte lorsqu'elle est configuree.
- [ ] Verifier qu'aucune adresse A1 editor-only n'apparait dans scene JSON, preview ou `.sb2`.
- [ ] Executer les tests cibles :

```powershell
dotnet test ScadaBuilderV2.sln --no-restore --filter "FullyQualifiedName~TableCellAddressTests|FullyQualifiedName~TableEditCoordinatorTests|FullyQualifiedName~TableUiArchitectureTests|FullyQualifiedName~TableWebViewMessageAdapterTests|FullyQualifiedName~ModernProjectStoreTests|FullyQualifiedName~Ft100SceneExporterTests"
```

- [ ] Executer build et suite complete :

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --no-restore
```

Attendu : meme resultat que le baseline frais; aucune nouvelle regression.

- [ ] Executer le smoke `win00012_modern_no_legacy` de la section 9 de la specification sur une copie de travail autorisee. Ne pas modifier ou stager les fichiers utilisateur actuels.
- [ ] Marquer `DEC-0043`, la specification et le plan `Implemented` seulement apres tests et smoke reussis.
- [ ] Synchroniser UI, fonctionnalites implementees, couverture et gaps sans modifier le contrat runtime `DEC-0042`.
- [ ] Calculer le bump d'iteration depuis la version presente au moment de la cloture; ne pas reutiliser aveuglement `V2.1.4.0040`.
- [ ] Executer :

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
git diff --check
git status --short
```

- [ ] Commit :

```powershell
git add VERSION docs/00_governance/DECISION_REGISTER_V2.md docs/06_ui_ux/UI_SPECIFICATION_V2.md docs/08_implementation_status/IMPLEMENTED_FEATURES_V2.md docs/08_implementation_status/REGRESSION_COVERAGE_V2.md docs/08_implementation_status/KNOWN_GAPS_V2.md docs/README.md docs/superpowers/specs/2026-07-15-table-numeric-cell-authoring-correction-design.md docs/superpowers/plans/2026-07-15-table-numeric-cell-authoring-correction.md tests/ScadaBuilderV2.Tests/ModernProjectStoreTests.cs tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git diff --cached --name-only
git commit -m "docs: record numeric table cell authoring correction"
```

---

## Validation Checklist

- [ ] Le groupe `Input numerique` expose un seul bouton `Configurer <A1>`.
- [ ] Le ruban, le panneau et le dialogue affichent la meme adresse et le meme id Tableau.
- [ ] Une selection provenant d'un autre Tableau est refusee.
- [ ] Le changement de Tableau desactive la configuration jusqu'au prochain clic cellule.
- [ ] Une cellule fusionnee utilise l'adresse de son ancre.
- [ ] Write seul initialise et persiste Read avec le meme tag.
- [ ] Une lecture distincte de l'ecriture est preservee.
- [ ] Le double-clic numerique ouvre la bonne cellule; le double-clic texte reste inline.
- [ ] Les mutations sont atomiques et couvertes par undo/redo.
- [ ] Sauvegarde/recharge et manifest 2.2 conservent les ids explicites.
- [ ] Aucun changement de schema, manifest ou TF100Web n'est introduit.
- [ ] Aucune adresse A1 editor-only n'est exportee.
- [ ] Build, tests cibles, suite complete, docs verifier et smoke interactif sont consignes.
- [ ] Aucun fichier utilisateur sous `projects/AMR_REF_SCADA_V2` n'est stage.
