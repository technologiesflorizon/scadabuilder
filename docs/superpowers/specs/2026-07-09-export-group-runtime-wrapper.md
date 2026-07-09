# Export : critère de wrapper DOM pour les groupes (design)

Date: 2026-07-09
Status: Draft design
Document version: `V2.1.5.0000`
Portée: SCADA Builder V2 — `Ft100SceneExporter`

## 1. Problème

L'exporteur `Ft100SceneExporter` utilise `element.EventBindings.Count > 0` comme
critère unique pour décider si un `ScadaElement` de kind `Group` doit être
matérialisé en wrapper `<div>` dans le HTML exporté.

Ce critère est obsolète depuis l'introduction du système CommandConfig/StateConfig
(DEC-0036). Un groupe peut désormais porter des données runtime modernes SANS avoir
d'EventBindings legacy :

- `CommandConfig` : commandes Navigate, WriteTag, OpenPopup, etc.
- `StateConfig` : règles d'état avec expressions et effets
- `Data.ReadTagId` / `Data.WriteTagId` : value bindings

Quand un tel groupe est aplati par l'exporteur :
- Ses enfants sont rendus directement dans le parent supérieur
- Les attributs `data-scada-command-config`, `data-scada-state-config`,
  `data-scada-read-tag`, `data-scada-write-tag` sont **perdus**
- TF100Web ne peut pas initialiser les commandes/états/value bindings sur ce groupe

### 1.1 Cas concret (win00003)

Les groupes `group_001` et `group_002` ont des restes EventBindings ET CommandConfig,
donc ils sont wrappés correctement. Mais d'autres groupes ont uniquement CommandConfig
comme vraie source — sans EventBindings — et sont aplatis à l'export. Résultat :
leurs commandes ne sont pas bindées par le runtime TF100Web.

## 2. Objectif

Faire en sorte que tout groupe portant au moins une donnée runtime (legacy ou moderne)
soit matérialisé en wrapper DOM dans le HTML, le CSS et le manifest exportés.

## 3. Décisions

| # | Décision |
|---|---|
| D1 | Remplacer le critère `EventBindings.Count > 0` par un helper `GroupRequiresRuntimeWrapper` qui vérifie la présence de TOUTE donnée runtime. |
| D2 | Le helper vérifie : `EventBindings.Count > 0` OU `CommandConfig.Commands.Count > 0` OU `StateConfig.States.Count > 0` OU `HasNonDefaultFallback(StateConfig)` OU `ReadTagId` non vide OU `WriteTagId` non vide. |
| D3 | Le helper est utilisé aux 4 endroits où la décision groupe-wrapper est prise : `BuildElementHtml`, `AppendElementCss`, `ShouldExportManifestObject`, `FlattenExportedElementBounds`. |
| D4 | Un groupe sans AUCUNE donnée runtime continue d'être aplati (comportement existant préservé). |
| D5 | Aucun changement de contrat TF100Web : le format des attributs `data-scada-*` et du manifest reste inchangé. |

## 4. Contrat

### 4.1 Helper signature

```csharp
// Dans Ft100SceneExporter.cs

/// <summary>
/// Determines whether a Group element requires a runtime DOM wrapper in the
/// exported output. Returns <c>true</c> when the group carries at least one
/// piece of runtime data that needs a DOM node to be reachable by the
/// TF100Web runtime.
/// </summary>
private static bool GroupRequiresRuntimeWrapper(ScadaElement element)
{
    if (element.Kind != ScadaElementKind.Group)
        return false;

    return element.EventBindings.Count > 0
        || element.EffectiveCommandConfig.Commands.Count > 0
        || element.EffectiveStateConfig.States.Count > 0
        || HasNonDefaultFallback(element.EffectiveStateConfig)
        || !string.IsNullOrWhiteSpace(element.Data?.ReadTagId)
        || !string.IsNullOrWhiteSpace(element.Data?.WriteTagId);
}
```

### 4.2 Points d'appel

| Méthode | Ligne (avant) | Changement |
|---|---|---|
| `BuildElementHtml` | 1025 | `element.EventBindings.Count > 0` → `GroupRequiresRuntimeWrapper(element)` |
| `AppendElementCss` | 1659 | `element.EventBindings.Count > 0` → `GroupRequiresRuntimeWrapper(element)` |
| `ShouldExportManifestObject` | 1907 | `element.EventBindings.Count > 0` → `GroupRequiresRuntimeWrapper(element)` |
| `FlattenExportedElementBounds` | 1937 | `element.EventBindings.Count > 0` → `GroupRequiresRuntimeWrapper(element)` |

### 4.3 Format HTML groupe wrapper (inchangé)

```html
<div id="ft100-{pageId}__{elementId}"
     class="ft100-element ft100-element--Group"
     data-scada-element-id="{elementId}"
     data-name="{displayName}"
     style="..."
     data-scada-command-config="..."
     data-scada-state-config="..."
     data-scada-read-tag="..."
     data-scada-write-tag="...">
  <!-- enfants -->
</div>
```

### 4.4 Format manifest groupe (inchangé)

```json
{
  "id": "group_003",
  "displayName": "Navigation Group",
  "kind": "Group",
  "events": [],
  "valueBindings": { "readTagId": null, "writeTagId": null },
  "stateConfig": null,
  "commandConfig": { "commands": [...] }
}
```

## 5. Tests

| Test | Scénario |
|---|---|
| `Export_GroupWithOnlyCommandConfig_RendersWrapperWithCommandAttribute` | Groupe avec CommandConfig, sans EventBindings → wrapper + data-scada-command-config |
| `Export_GroupWithOnlyStateConfig_RendersWrapperWithStateAttribute` | Groupe avec StateConfig, sans EventBindings → wrapper + data-scada-state-config |
| `Export_GroupWithNoRuntimeData_FlattensChildren` | Groupe vide → aplati (pas de wrapper) |
| `Export_GroupWithOnlyValueBindings_RendersWrapperWithTagAttributes` | Groupe avec ReadTagId/WriteTagId → wrapper + data-scada-read-tag/write-tag |
| `ExportPreservesGroupClickNavigateEventAsRuntimeWrapper` (existant) | Groupe avec EventBindings → continue de fonctionner |
| `ExportProjectArchive_ProducesCompleteSb2WithStateCommandRuntime` (existant) | Élément non-groupe avec StateConfig+CommandConfig → inchangé |
