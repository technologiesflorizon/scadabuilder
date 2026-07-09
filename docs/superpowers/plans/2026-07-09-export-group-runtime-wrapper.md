# Export - wrapper DOM runtime pour les groupes - Plan d'implementation

Date: 2026-07-09
Status: Draft implementation plan - pending execution approval
Document version: `V2.1.5.0002`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-09 | `V2.1.5.0002` | `PENDING` | Correction du plan pour refleter la spec: retrait global de `data-scada-events`, `StateConfig.ReadVariable` dans HTML/manifest, gate TF100Web separe, tests sans reference directe a `ScadaBuilderV2.App`. |
| 2026-07-09 | `V2.1.5.0001` | `PENDING` | Plan initial du correctif de wrapper runtime des groupes. |

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Corriger l'export des groupes Element+ afin que les groupes portant du runtime moderne (`CommandConfig`, `StateConfig`, `StateConfig.ReadVariable`, fallback d'etat non defaut) aient un wrapper DOM exporte, sans utiliser l'ancien chemin `EventBindings` / `data-scada-events`.

**Architecture:** SCADA Builder V2 reste proprietaire du contrat de sortie `.sb2`: HTML, CSS, manifest et diagnostics d'export. TF100Web reste proprietaire du cycle de vie host apres injection de fragment. Le plan separe donc le correctif export SCADA Builder V2 du gate TF100Web qui rejoue les valeurs tags deja en cache pour rendre `StateConfig` immediatement observable.

**Tech Stack:** C# 12, .NET 8 WPF, MSTest, JavaScript TF100Web, Django tests.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md`.
- Aucune suppression physique de `ElementEventDialog`, `OpenElementEventDialog`, `AddElementEventFromDialog`, `WithObjectEvent` ou helpers `With*Event`.
- Ne pas convertir automatiquement `EventBindings` vers `CommandConfig` ou `StateConfig`.
- Ne pas reintroduire `data-scada-events` comme fallback runtime.
- `Data.ReadTagId` / `Data.WriteTagId` ne declenchent pas de wrapper groupe.
- Les lectures modernes passent par `StateConfig.ReadVariable`.
- Les ecritures modernes passent par `CommandConfig` avec `ScadaCommandKind.WriteTag`.
- Le manifest ne doit plus remplir `Events` depuis `element.EventBindings`.
- Les fonctions legacy restent presentes mais doivent etre traitees comme decommissionnees.
- Les commits mentionnes ci-dessous sont des bornes de plan; ne pas les faire sans autorisation explicite d'execution.

---

## Before You Start

- [ ] Confirmer l'autorisation d'implementation. Ce plan ne donne pas l'autorisation de modifier le code.
- [ ] Dans SCADA Builder V2:

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
git status --short --branch
```

Expected: relever les changements existants. Ne pas ecraser de modifications utilisateur.

- [ ] Lire les fichiers SCADA Builder V2 a modifier:

```powershell
Get-Content -Raw "src\ScadaBuilderV2.Rendering\Ft100SceneExporter.cs"
Get-Content -Raw "src\ScadaBuilderV2.Domain\Projects\ProjectModels.cs"
Get-Content -Raw "tests\ScadaBuilderV2.Tests\Ft100SceneExporterTests.cs"
```

- [ ] Si la phase TF100Web est executee, verifier le repo externe:

```powershell
Set-Location "F:\Projet\Git\TF100Web"
git status --short --branch
```

Expected: relever les changements existants. Toute modification TF100Web demande une autorisation explicite separee.

---

## Phase 1 - SCADA Builder V2 Export

### Task 1: Ajouter les tests de contrat export moderne

**Files:**
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs`

**Interfaces:**
- Consumes: `Ft100SceneExporter`, `ScadaScene`, `ScadaElement`, `ScadaElementCommandConfig`, `ScadaElementStateConfig`, `ScadaReadVariableRule`.
- Produces: couverture du contrat export groupe moderne et decommission `EventBindings`.

- [ ] **Step 1: Remplacer le test legacy**

Supprimer ou reecrire `ExportPreservesGroupClickNavigateEventAsRuntimeWrapper`. Il valide l'ancien contrat `data-scada-events`; il ne doit plus rester comme test positif.

- [ ] **Step 2: Ajouter les tests export groupe**

Ajouter des tests qui construisent des scenes minimales et verifient les sorties HTML/manifest/CSS:

- `Export_GroupWithOnlyCommandConfig_RendersWrapperWithCommandAttribute`
- `Export_GroupWithNavigateCommand_RendersWrapperWithCommandAttribute`
- `Export_GroupWithWriteTagCommand_RendersWrapperWithCommandAttribute`
- `Export_GroupWithOnlyStateConfig_RendersWrapperWithStateAttribute`
- `Export_GroupWithOnlyStateReadVariable_RendersWrapperWithStateAttribute`
- `Export_GroupWithNoRuntimeData_FlattensChildren`
- `Export_GroupWithOnlyLegacyDataValueBindings_DoesNotRequireRuntimeWrapper`
- `Export_GroupWithOnlyLegacyEventBindings_DoesNotExportRuntimeEvents`
- `Export_NonGroupWithLegacyEventBindings_DoesNotExportRuntimeEvents`
- `Export_GroupWithCommandConfigAndLegacyEventBindings_UsesCommandConfigOnly`
- `Export_Manifest_DoesNotSerializeLegacyEventBindingsAsActiveEvents`
- `Export_GroupRuntimeWrapper_DoesNotChangeChildGeometry`

Required assertions:

- HTML contains group wrapper id for `CommandConfig`, `StateConfig`, `StateConfig.ReadVariable`, and non-default fallback cases.
- HTML contains `data-scada-command-config` for command groups.
- HTML contains `data-scada-state-config` for state/readVariable groups.
- Decoded HTML state config contains `"readVariable"` and the expected `tagId` for read variable.
- HTML does not contain `data-scada-events`.
- Exported CSS does not contain `data-scada-events`.
- Manifest object `Events` is empty or absent for elements with legacy `EventBindings`.
- Manifest includes `StateConfig` when `ReadVariable` exists, even when `States.Count == 0`.
- A group with only `Data.ReadTagId` / `Data.WriteTagId` is flattened.
- Child geometry remains relative under a runtime wrapper and absolute when the group is flattened.

- [ ] **Step 3: Add authoring workflow static test without App reference**

Do not reference `ScadaBuilderV2.App` types from the test project. Instead, add a static file test that reads:

- `src/ScadaBuilderV2.App/MainWindow.xaml.cs`
- `src/ScadaBuilderV2.App/MainWindow.WebViewScript.cs`
- `src/ScadaBuilderV2.App/MainWindow.xaml`

Expected assertions:

- context menu descriptors do not expose an `events` command.
- `executeCommand` switch does not route `object.events` or `element-plus.events`.
- ribbon/XAML does not expose an active EventBindings authoring command.
- legacy handlers may still exist, but are not connected to active menu/ribbon/property commands.

- [ ] **Step 4: Run focused tests**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"
```

Expected before implementation: failures are acceptable only for the new contract tests.

---

### Task 2: Implementer le helper et les points d'appel groupe

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`

**Interfaces:**
- Consumes: `ScadaElement.EffectiveCommandConfig`, `ScadaElement.EffectiveStateConfig`, `HasNonDefaultFallback`.
- Produces: `GroupRequiresRuntimeWrapper`.

- [ ] **Step 1: Ajouter `GroupRequiresRuntimeWrapper`**

Add a private helper near `HasNonDefaultFallback`:

```csharp
private static bool GroupRequiresRuntimeWrapper(ScadaElement element)
{
    if (element.Kind != ScadaElementKind.Group)
    {
        return false;
    }

    var commandConfig = element.EffectiveCommandConfig;
    var stateConfig = element.EffectiveStateConfig;

    return commandConfig.Commands.Count > 0
        || stateConfig.States.Count > 0
        || stateConfig.ReadVariable is not null
        || HasNonDefaultFallback(stateConfig);
}
```

Do not mention an unregistered decision id such as `DEC-0040`; cite the spec path instead if a remark is added.

- [ ] **Step 2: Remplacer les 4 decisions groupe**

Replace `element.EventBindings.Count > 0` with `GroupRequiresRuntimeWrapper(element)` in:

- `BuildElementHtml`
- `AppendElementCss`
- `ShouldExportManifestObject`
- `FlattenExportedElementBounds`

Expected: only modern runtime data controls group materialization.

- [ ] **Step 3: Conserver la geometrie attendue**

For wrapped groups, children must still be rendered with parent offsets reset to `0,0`. For flattened groups, children must still inherit the accumulated absolute offset. Do not refactor unrelated geometry code.

---

### Task 3: Retirer `data-scada-events` de tout export moderne

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`

**Interfaces:**
- Consumes: legacy `EventBindings`.
- Produces: HTML/CSS/manifest without active legacy events.

- [ ] **Step 1: HTML**

Stop emitting `BuildEventAttribute(element)` for both groups and non-groups. Acceptable implementations:

- remove the calls and template interpolation from `BuildElementHtml`; or
- make `BuildEventAttribute` return an empty string and mark it decommissioned, while ensuring no exported HTML contains `data-scada-events`.

Expected: no element or group emits `data-scada-events`.

- [ ] **Step 2: CSS**

Remove `[data-scada-events]` from exported cursor CSS selectors. Keep button cursor behavior through `.ft100-element--Button`.

Expected: exported CSS does not contain `data-scada-events`.

- [ ] **Step 3: Manifest**

Replace `Events = element.EventBindings` with a legacy-safe empty value:

```csharp
Events = Array.Empty<ScadaObjectEventBinding>(),
```

If a later schema task removes the field entirely, that must be a separate explicit decision. This plan preserves shape while removing active legacy data.

---

### Task 4: Inclure `StateConfig.ReadVariable` dans la serialisation HTML et manifest

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs`

**Interfaces:**
- Consumes: `ScadaElementStateConfig.ReadVariable`.
- Produces: `data-scada-state-config` and manifest `StateConfig` when read variable is configured.

- [ ] **Step 1: HTML state attribute**

In `BuildStateCommandAttributes`, update the state predicate:

```csharp
var hasStateConfig = stateConfig.States.Count > 0
    || stateConfig.ReadVariable is not null
    || HasNonDefaultFallback(stateConfig);
```

Expected: a group or element with only `StateConfig.ReadVariable` receives `data-scada-state-config`.

- [ ] **Step 2: Manifest state config**

Use the same predicate when assigning manifest `StateConfig`.

Expected: manifest includes `StateConfig` when `ReadVariable` exists, even if no state rules exist.

- [ ] **Step 3: Validation**

```powershell
Set-Location "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2"
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"
```

Expected: all exporter contract tests pass.

---

### Task 5: Ajouter le diagnostic legacy EventBindings

**Files:**
- Modify: `src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs`
- Modify: `tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs` or a validator-focused test file if one exists.

**Interfaces:**
- Consumes: `ScadaProjectBuildValidator.Validate`.
- Produces: warning for every element still carrying `EventBindings`.

- [ ] **Step 1: Preserve public API compatibility**

Do not replace a public method with an inaccessible internal method. Recommended shape:

- add `public static void AuditLegacyEventBindings(...)`; and
- keep `public static void AuditOrphanedEventBindings(...)` as an obsolete wrapper if existing tests or callers use it, or update every direct caller intentionally.

- [ ] **Step 2: Broaden diagnostics**

Emit warning `event-bindings-decommissioned` for every element where `element.EventBindings.Count > 0`.

Diagnostic text must state:

- scene id;
- element id;
- count of bindings;
- `EventBindings` are decommissioned;
- expected authoring is `CommandConfig` or `StateConfig`;
- `EventBindings` are not exported as TF100Web runtime.

If the element has modern config, say the modern config is the exported source. If not, say the element may be inactive until migrated.

- [ ] **Step 3: Integrate validation**

Update `ScadaProjectBuildValidator.Validate(...)` to call the broadened diagnostic for scenes included in build.

- [ ] **Step 4: Tests**

Add/update tests for:

- EventBindings only => warning.
- EventBindings plus CommandConfig => warning still emitted.
- clean scene => no warning.
- `Validate(project, scenes)` includes this warning.

Run:

```powershell
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~EventBindings"
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaProjectBuildValidator"
```

Expected: legacy EventBindings are diagnosed consistently.

---

## Phase 2 - SCADA Builder V2 Validation

### Task 6: Validation complete SCADA Builder V2

**Files:**
- Inspect only unless failures require fixes.

**Interfaces:**
- Consumes: all SCADA Builder V2 changes from Phase 1.
- Produces: validated export contract.

- [ ] **Step 1: Search exported legacy contract references**

```powershell
rg -n "data-scada-events|Events = element.EventBindings|EventBindings.Count > 0" src\ScadaBuilderV2.Rendering tests\ScadaBuilderV2.Tests
```

Expected:

- no active exporter output path emits `data-scada-events`;
- no manifest projection assigns `Events = element.EventBindings`;
- remaining `EventBindings` references are diagnostics, domain legacy APIs, or tests explicitly covering decommission behavior.

- [ ] **Step 2: Build and tests**

```powershell
dotnet build ScadaBuilderV2.sln
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~Ft100SceneExporterTests"
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~ScadaProjectBuildValidator"
dotnet test ScadaBuilderV2.sln
```

Expected: same result as the fresh baseline captured in Before You Start; any new failure must be investigated before commit.

- [ ] **Step 3: Commit boundary**

Commit only after tests pass and after user approval to commit:

```bash
git add src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs
git add src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs
git add tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs
git commit -m "fix: export group runtime wrappers from modern state and command config"
```

---

## Phase 3 - TF100Web Host Lifecycle (Authorization Gate)

> **Authorization required before modifying `F:\Projet\Git\TF100Web`.**

This phase is required to close the full spec behavior for immediate `StateConfig` execution. If this phase is not authorized, the SCADA Builder V2 implementation must report a remaining external dependency: TF100Web must replay cached tag values after slot initialization.

### Task 7: Replay cached tag values after rendered slot initialization

**Files:**
- Modify: `F:\Projet\Git\TF100Web\static\asset\js\station\visualisation_import.js`
- Modify: `F:\Projet\Git\TF100Web\frontend\tests_scada_package.py` or another existing TF100Web test file with the same runtime contract coverage.

**Interfaces:**
- Consumes: `ScadaHost._initRenderedSlot`, `ScadaTagCache.values`, `window.ScadaRuntime.onTagValuesChanged`.
- Produces: immediate evaluation of `[data-scada-state-config]` and `stateConfig.readVariable` after fragment render.

- [ ] **Step 1: Add a host helper**

Add a small helper near `ScadaHost`:

```javascript
_replayCachedTagValues() {
  if (window.ScadaRuntime && typeof window.ScadaRuntime.onTagValuesChanged === 'function') {
    window.ScadaRuntime.onTagValuesChanged(Object.assign({}, ScadaTagCache.values));
  }
}
```

- [ ] **Step 2: Call it after `_initRenderedSlot`**

In `_renderPart`, after:

```javascript
await this._initRenderedSlot(slot, part.page_id);
```

call:

```javascript
this._replayCachedTagValues();
```

Equivalent placement is acceptable if the observable result is the same: a newly rendered slot with `data-scada-state-config` evaluates immediately from cached tag values.

- [ ] **Step 3: Tests**

Add TF100Web tests for:

- `_renderPart` / `_initRenderedSlot` path contains a replay call after initialization.
- `ScadaRuntime.onTagValuesChanged` receives a copy of `ScadaTagCache.values`.
- a fragment with `data-scada-state-config` + `readVariable` can be rendered without depending on `data-scada-events`.
- navigate and writeTag host paths still use `CommandConfig` / postMessage / mapping write endpoint.

Run the relevant TF100Web tests from `F:\Projet\Git\TF100Web`:

```powershell
python manage.py test frontend.tests_scada_package frontend.tests_scada_deploy
```

Expected: TF100Web SCADA package/runtime tests pass.

- [ ] **Step 4: Commit boundary**

Commit only after tests pass and after user approval to commit:

```bash
git add static/asset/js/station/visualisation_import.js
git add frontend/tests_scada_package.py
git commit -m "fix: replay SCADA tag cache after rendered slot initialization"
```

---

## Phase 4 - Documentation and Final Verification

### Task 8: Documentation validation

**Files:**
- Modify: `docs/superpowers/plans/2026-07-09-export-group-runtime-wrapper.md`
- Inspect: `docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md`

**Interfaces:**
- Consumes: implemented behavior and tests.
- Produces: plan/spec alignment.

- [ ] **Step 1: Verify plan still matches spec**

```powershell
rg -n "data-scada-events|ReadVariable|StateConfig|CommandConfig|TF100Web|EventBindings" docs\superpowers\specs\2026-07-09-export-group-runtime-wrapper.md docs\superpowers\plans\2026-07-09-export-group-runtime-wrapper.md
```

Expected: no contradiction between spec and plan.

- [ ] **Step 2: Documentation validation**

```powershell
powershell -ExecutionPolicy Bypass -File tools\docs\verify-docs.ps1
```

Expected: any failures must be classified as pre-existing or fixed before commit. The touched plan must have H1, Date, Status, Document version, and `## Historique des changements`.

- [ ] **Step 3: Final commit boundary**

Commit docs only after user approval:

```bash
git add docs/superpowers/plans/2026-07-09-export-group-runtime-wrapper.md
git add docs/superpowers/specs/2026-07-09-export-group-runtime-wrapper.md
git commit -m "docs: align group runtime wrapper plan with TF100Web contract"
```

---

## Validation Checklist

- [ ] SCADA Builder V2 export HTML contains no `data-scada-events`.
- [ ] SCADA Builder V2 export CSS contains no `[data-scada-events]` selector.
- [ ] Manifest does not serialize `element.EventBindings` as active events.
- [ ] A group with `CommandConfig.Navigate` has a wrapper with `data-scada-command-config`.
- [ ] A group with `CommandConfig.WriteTag` has a wrapper with `data-scada-command-config`.
- [ ] A group with `StateConfig` has a wrapper with `data-scada-state-config`.
- [ ] A group with only `StateConfig.ReadVariable` has a wrapper and serialized `readVariable`.
- [ ] A group with only `Data.ReadTagId` / `Data.WriteTagId` remains flattened.
- [ ] A group with only `EventBindings` remains flattened and emits a warning.
- [ ] Non-group elements with legacy `EventBindings` do not emit `data-scada-events`.
- [ ] Child geometry is unchanged by wrapper creation.
- [ ] TF100Web replays cached tag values after slot initialization, or this remains a documented external dependency if Phase 3 is not authorized.
- [ ] The old authoring flow remains present but unexposed by active human UI workflow.
