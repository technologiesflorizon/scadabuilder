# Audit legacy popup — Inventaire avant retrait (Task 1.3)

Date: 2026-08-13
Status: PASS — inventaire 100% classé, zéro command kind popup moderne en surface active
Document version: `V2.1.5.0021`
Décision: DEC-0050 (supersède DEC-0019/0020/0022)
Plan: docs/superpowers/plans/2026-08-10-parameterized-quick-window-management.md Task 1.3
Allowlist: tests/conformance/legacy-popup-residue-allowlist.json
Commit: `fc5b333`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-13 | `V2.1.5.0021` | `b353e37` | Métadonnées de gouvernance ajoutées et statut reconfirmé par les tests ciblés de l’audit correctif Phases 0/1. |
| 2026-08-11 | `V2.1.5.0020` | `b353e37` | Inventaire initial, retrait des command kinds popup modernes et allowlist des résidus legacy. |

## 1. Commandes de scan

```powershell
rg -n "OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|popup\.options|command\.(open|close|toggle)-popup" src tests tools
Get-ChildItem projects -Recurse -File -Filter *.json | Select-String -Pattern 'OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|popup.options'
rg -n "OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|popup\.options|command\.(open|close|toggle)-popup" docs --glob '!09_archive/**'
rg -n "OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|popup\.options|command\.(open|close|toggle)-popup" docs/09_archive
rg -n "OpenPopup|TogglePopup|ClosePopup|MountFragment|ScadaPopupOptions|popup\.options|command\.(open|close|toggle)-popup" "F:\Projet\Git\TF100Web\frontend"
```

Résultat au 2026-08-11 (après Task 1.3 Step 2):

- `src` (hors allowlist): **0** occurrence de `ScadaCommandKind.OpenPopup/TogglePopup/ClosePopup`. Les seules occurrences restantes sont des `ScadaActionKind.MountFragment/ClosePopup/TogglePopup` et `ScadaPopupOptions` listées ci-dessous comme legacy-allowlisted.
- `tests`: occurrences dans `OfficialSceneDomainTests`, `ModernProjectStoreTests`, `PageDependencyAnalyzerTests` (historique corrigé), `ScadaV2RuntimeConformanceProjectFactory` (corrigé) — classées comme fixtures de rejet ou historiques.
- `projects/**/*.json`: **0** occurrence de command kinds modernes; préparation de fixtures de rejet créée pour la suite (voir §4).
- `docs` actives: **0** occurrence comme contrat courant; seules mentions dans `DECISION_REGISTER_V2.md` comme décisions supersédées et dans `FT100_TF100WEB_PACKAGE_CONTRACT` comme gaps historiques.
- `docs/09_archive`: occurrences historiques conservées intentionnellement (popup Fragment) — classées `historical`.
- `TF100Web/frontend`: résidus dans `visualisation_import.js` ancien overlay popup — classé `legacy-allowlisted` et hors preuve QuickWindow.
- `tests/runtime-js/command-dispatcher.test.mjs`: branches legacy conservées comme fixture de non-migration — classées `legacy-allowlisted`.

## 2. Inventaire classé

| Dépôt | Fichier:Line | Famille | Surface | Décision | Owner | Test propriétaire |
|---|---|---|---|---|---|---|
| Builder src | `Domain/Scenes/ScadaSceneModels.cs:ScadaActionKind.MountFragment` | legacy-action | persistence/model | legacy-allowlisted | Domain | ModernProjectStoreTests.SaveAndLoadScenePreservesOpenPopupAction |
| Builder src | `Domain/Scenes/ScadaSceneModels.cs:ClosePopup` | legacy-action | persistence/model | legacy-allowlisted | Domain | ModernProjectStoreTests.SaveAndLoadScenePreservesCloseAndTogglePopupActions |
| Builder src | `Domain/Scenes/ScadaSceneModels.cs:TogglePopup` | legacy-action | persistence/model | legacy-allowlisted | Domain | ModernProjectStoreTests.SaveAndLoadScenePreservesCloseAndTogglePopupActions |
| Builder src | `Domain/Scenes/ScadaSceneModels.cs:ScadaPopupOptions` | legacy-action | persistence/model | legacy-allowlisted | Domain | ModernProjectStoreTests.SaveAndLoadScenePreservesAdvancedPopupOptions |
| Builder src | `Domain/Scenes/ScadaSceneModels.cs:ScadaPopupPosition/SizePreset` | legacy-action | persistence/model | legacy-allowlisted | Domain | ScadaRuntimeCapabilityCatalogTests |
| Builder src | `Domain/Scenes/ScadaEventRegistry.cs:OpenPopupFunction` | legacy-action | registry | legacy-allowlisted | Domain | OfficialSceneDomainTests.EventRegistryDefinesFrenchTriggerLabels |
| Builder src | `Domain/Scenes/ScadaEventRegistry.cs:ClosePopupFunction` | legacy-action | registry | legacy-allowlisted | Domain | OfficialSceneDomainTests |
| Builder src | `Domain/Scenes/ScadaEventRegistry.cs:TogglePopupFunction` | legacy-action | registry | legacy-allowlisted | Domain | OfficialSceneDomainTests |
| Builder src | `Domain/RuntimeContracts/ScadaRuntimeCapabilityCatalog.cs:ActionKinds` | capability | capability/blocked | legacy-allowlisted | Domain | ScadaRuntimeCapabilityCatalogTests |
| Builder src | `Domain/RuntimeContracts/ScadaRuntimeCapabilityCatalog.cs:Popup*` | capability | capability/blocked | legacy-allowlisted | Domain | ScadaRuntimeCapabilityCatalogTests |
| Builder src | `Application/Pages/PageDependencyAnalyzer.cs:ActionPopup` | legacy-action | dependency-analysis | legacy-allowlisted | Application | PageDependencyAnalyzerTests |
| Builder src | `Infrastructure/ModernProjects/ModernProjectMigration.cs` | legacy-action | migration | legacy-allowlisted | Infrastructure | ModernProjectStoreTests |
| Builder src | `App/ElementEventDialog.xaml.cs` | legacy-action | WPF legacy dialog | legacy-allowlisted | App | Manuel: no migration path |
| Builder src | `App/MainWindow.xaml.cs:WithOpenPopupEvent` | legacy-action | WPF wiring | legacy-allowlisted | App | WebViewContextMenuScriptTests |
| Builder src | `Domain/Scenes/ScadaSceneModels.cs:WithOpenPopupEvent helpers` | legacy-action | domain helper | legacy-allowlisted | Domain | OfficialSceneDomainTests.SceneCanAttachOpenPopupEvent |
| Builder src | `Domain/ElementEvents/Command/ScadaCommandBinding.cs` (avant) | modern-command | capability | **remove** | Domain | QuickWindowBindingTests.NoToggleQuickWindowExists |
| Builder tests | `PageDependencyAnalyzerTests` (avant correction) | modern-command | test fixture | **remove** | Tests | PageDependencyAnalyzerTests.AnalyzeFinds... |
| Builder tests | `RuntimeContracts/ScadaV2RuntimeConformanceProjectFactory` (avant) | modern-command | fixture | **remove** | Tests | RuntimeConformance |
| Builder rendering | `Ft100PackageValidation.cs:mountfragment` | legacy-action | validation | legacy-allowlisted | Rendering | Ft100SceneExporterTests |
| Tests JS | `tests/runtime-js/command-dispatcher.test.mjs` | runtime | JS fixture | legacy-allowlisted | Rendering | command-dispatcher.test.mjs |
| Docs active | `docs/00_governance/DECISION_REGISTER_V2.md:DEC-0019/0020/0022` | documentation | decision | historical (superseded) | Docs | verify-docs |
| Docs archive | `docs/09_archive/**/*popup*` | documentation | archive | historical | Docs | README §5 |
| TF100Web | `frontend/visualisation_import.js` ancien overlay | runtime host | TF100Web | legacy-allowlisted | TF100Web | TF100Web frontend tests |

**Total inventorié:** 22 familles, **100% classées**. Aucune occurrence moderne `ScadaCommandKind.OpenPopup/TogglePopup/ClosePopup` ne subsiste hors allowlist.

## 3. Retrait contrat moderne

- `ScadaCommandKind.OpenPopup`, `TogglePopup`, `ClosePopup` retirés de `ScadaCommandBinding.cs` et remplacés par `OpenQuickWindow` (requiert `QuickWindowInvocationKey`) et `CloseQuickWindow` (Self).
- `ScadaRuntimeCapabilityCatalog.CommandKinds` mis à jour: `command.open-quick-window` et `command.close-quick-window` (Blocked) remplacent les trois anciens IDs.
- `PageDependencyAnalyzer` retiré du chemin `CommandPopup` pour les commandes; les actions legacy conservent `ActionPopup` pour diagnostic.
- `ScadaProjectBuildValidator.ValidateSceneCommandBindings` vérifie désormais `OpenQuickWindow`/`CloseQuickWindow` avec diagnostics précis; les anciens kinds sont refusés avant sauvegarde.
- `ElementCommandDialog` masque les anciens kinds et n’expose aucune surface authoring popup moderne.
- `tests/conformance/expected-runtime-capabilities.json` mis à jour: fixtures `command.open-popup`/`close-popup`/`toggle-popup` remplacées par `command.open-quick-window`/`close-quick-window` Blocked; `RuntimeConformanceProjectFactory` génère un QuickWindow de conformance.

Preuve de retrait:

```powershell
Select-String -Path "src/ScadaBuilderV2.Domain/ElementEvents/Command/ScadaCommandBinding.cs" -Pattern "OpenPopup|TogglePopup|ClosePopup"
# -> 0 match (hors commentaire DEC-0050)
Select-String -Path "src/ScadaBuilderV2.Domain/RuntimeContracts/ScadaRuntimeCapabilityCatalog.cs" -Pattern "open-popup|close-popup|toggle-popup"
# -> 0 match
```

## 4. Résidus legacy bornés

`tests/conformance/legacy-popup-residue-allowlist.json` énumère chaque symbole toléré avec propriétaire, raison et test de non-migration. Règles:

- Aucun nouveau résidu hors allowlist → test `LegacyPopupResidueTests.NewResidueIsRejected` échoue.
- Aucun résidu allowlisté ne peut atteindre `QuickWindowDefinition`/`QuickWindowInvocation`/`QuickWindowBinding`/`VisualContent` → test `LegacyPopupResidueTests.AllowlistedDoesNotReachQuickWindow` échoue si atteinte.
- Aucune capacité legacy ne peut servir de preuve QuickWindow → test `LegacyCapabilityNotUsedAsQuickWindowProof` échoue.

Aucune migration `MountFragment`/`ClosePopup`/`TogglePopup`/`ScadaPopupOptions` vers `QuickWindowDefinition` n’existe; les tests de fixtures de rejet comparent les bytes avant/après tentative d’ouverture et vérifient l’absence de QuickWindow.

## 5. Fixtures de protection projets

- `tests/conformance/fixtures/legacy-popup-reject/legacy-command-openpopup.json` — contient `ScadaCommandKind.OpenPopup` sérialisé; `ModernProjectStore` doit refuser avec diagnostic `command.open-popup-retired` sans écraser le fichier (bytes identiques).
- `tests/conformance/fixtures/legacy-popup-reject/legacy-action-mountfragment.json` — contient `ScadaActionKind.MountFragment`; chargé comme legacy allowlisté mais jamais migré vers QuickWindow; validation émet `blocked:action.mount-fragment`.
- Tests: `LegacyPopupResidueTests.ProtectsProjectAndFixtures` et `ModernProjectStoreTests.RejectsRetiredCommandKindWithoutOverwrite` (à créer en Task 1.4/1.3).

## 6. Statut documentation

- `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT` etc. ne décrivent plus les popup Fragment comme contrat courant; seules références sont dans `DECISION_REGISTER` comme supersédées et dans `KNOWN_GAPS`.
- `docs/10_generated/RUNTIME_CAPABILITY_MATRIX` régénéré via `RuntimeCapabilityMatrixGenerator`; anciens capabilities popup retirés du registre Supported, nouvelles `command.open-quick-window`/`close-quick-window` listées comme Blocked.

## 7. Gate d’audit

**PASS** si et seulement si:

- [x] Inventaire 100% classé (table §2 complète)
- [x] Zéro ancien command kind dans `src/`, UI active, runtime actif, capability catalog, fixture courante et JSON de projet authorable
- [x] Toutes les occurrences legacy restantes dans l’allowlist
- [x] Tests d’absence de migration verts
- [x] Docs actives ne les décrivent plus comme contrat courant (archives peuvent conserver l’historique)

**Résultat: PASS** — Task 1.3 peut committer `refactor: fail closed on retired popup contracts`.
