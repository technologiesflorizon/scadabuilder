# Export - critere de wrapper DOM runtime pour les groupes

Date: 2026-07-09
Status: Draft design - gate export TF100Web et EventBindings decommissionnes
Document version: `V2.1.5.0001`
Portee: SCADA Builder V2 - `Ft100SceneExporter`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-09 | `V2.1.5.0001` | `PENDING` | Verrouille le contrat de sortie TF100Web moderne, retire `EventBindings` et les anciens `Data.ReadTagId`/`Data.WriteTagId` des criteres runtime, precise le gate lifecycle TF100Web pour `StateConfig`, et garde l'ancien flow comme code decommissionne temporaire. |
| 2026-07-09 | `V2.1.5.0000` | `PENDING` | Design initial du critere de wrapper DOM pour les groupes portant des donnees runtime. |

## 1. Probleme

L'exporteur `Ft100SceneExporter` utilise encore `element.EventBindings.Count > 0` comme
critere pour decider si un `ScadaElement` de kind `Group` doit etre materialise en wrapper
`<div>` dans le HTML exporte.

Ce critere est faux pour le contrat runtime actuel. Le flux `EventBindings` est
decommissionne et ne doit plus etre considere comme une source de verite runtime. Depuis
l'introduction de `CommandConfig` et `StateConfig`, un groupe peut porter des donnees runtime
modernes sans avoir d'`EventBindings` :

1. `CommandConfig` : commandes Navigate, WriteTag, OpenPopup, ClosePopup, TogglePopup, etc.
2. `StateConfig` : regles d'etat avec expressions et effets.

Quand un tel groupe est aplati par l'exporteur :

1. ses enfants sont rendus directement dans le parent superieur ;
2. les attributs `data-scada-command-config` et `data-scada-state-config` ne peuvent pas
   etre portes par le groupe ;
3. TF100Web ne peut pas initialiser les commandes ou etats du groupe.

### 1.1 Cas concret

Dans les scenes touchees, certains groupes fonctionnent parce qu'ils ont encore des
artefacts `EventBindings` en plus de leur `CommandConfig`. D'autres groupes n'ont que la
configuration moderne et sont aplatis, donc leurs attributs runtime modernes disparaissent.

La correction ne doit pas sauver le vieux chemin `data-scada-events`. Elle doit exporter le
contrat moderne attendu par TF100Web.

### 1.2 Workflow humain actuel

Audit UI confirme :

1. le chemin utilisateur actif passe par les proprietes Element+ ;
2. les commandes/navigation sont authorisees dans `CommandConfig` ;
3. les evenements d'etat sont authorises dans `StateConfig` ;
4. aucun menu/ruban/proprietes courant ne doit exposer l'ancien `ElementEventDialog`.

Les anciens champs `Data.ReadTagId` / `Data.WriteTagId` ne sont plus un chemin d'authoring
humain actif. Dans le workflow courant, la lecture de variable passe par
`StateConfig.ReadVariable`, et l'ecriture de tag passe par `CommandConfig` avec une commande
`WriteTag`. Ces deux cas sont donc deja couverts par les criteres `StateConfig` et
`CommandConfig`.

L'ancien code (`ElementEventDialog`, `OpenElementEventDialog`, `AddElementEventFromDialog`,
`WithObjectEvent`, `WithChangePageEvent`, helpers popup/visibilite) reste present pour le
moment, mais il est decommissionne. Il ne doit pas etre utilise par le nouveau flux ni par
l'export moderne. Sa suppression physique sera faite dans une tranche separee lorsque le
risque migration/support sera ferme.

## 2. Objectif

Corriger le probleme de wrapper des groupes sans introduire de nouveau contrat runtime.

L'export `.sb2` doit respecter le contrat TF100Web attendu :

1. commandes : `data-scada-command-config` ;
2. etats : `data-scada-state-config` ;
3. manifest coherent avec ces memes donnees modernes.

Objectifs explicites :

1. un groupe avec `CommandConfig` ou `StateConfig` doit avoir un wrapper DOM runtime ;
2. un groupe sans donnees runtime modernes continue d'etre aplati pour preserver le rendu
   existant ;
3. `EventBindings` ne doit plus declencher de wrapper runtime ;
4. l'exporteur ne doit plus emettre `data-scada-events` comme chemin runtime actif ;
5. `Data.ReadTagId` / `Data.WriteTagId` ne doivent pas declencher de wrapper groupe dans cette
   spec, car ce chemin est legacy/semi-fonctionnel et n'est plus authorisable par le workflow
   humain courant ;
6. les `EventBindings` restants doivent etre traites comme artefacts legacy a auditer, pas
   comme compatibilite implicite.

Limite importante : cette correction garantit que les attributs runtime modernes sont presents
sur un noeud DOM exporte. Elle ne remplace pas le cycle de vie TF100Web qui doit initialiser
et reevaluer ce noeud apres injection du fragment. Pour `CommandConfig.Navigate`, le chemin
TF100Web est deja present. Pour `CommandConfig.WriteTag`, le chemin existe sous conditions
mapping/permission/driver. Pour `StateConfig`, TF100Web doit aussi rejouer les valeurs de tags
deja en cache apres `_initRenderedSlot(...)`, sinon un etat ou une `readVariable` peut rester
non evalue jusqu'au prochain changement de valeur.

## 3. Decisions

| # | Decision |
| --- | --- |
| D1 | Remplacer les decisions `element.EventBindings.Count > 0` par un helper `GroupRequiresRuntimeWrapper`. |
| D2 | `GroupRequiresRuntimeWrapper` regarde seulement les donnees runtime modernes supportees : `CommandConfig`, `StateConfig`, fallback d'etat non defaut et `StateConfig.ReadVariable`. |
| D3 | `GroupRequiresRuntimeWrapper` exclut explicitement `EventBindings`. |
| D4 | Le helper est utilise dans `BuildElementHtml`, `AppendElementCss`, `ShouldExportManifestObject` et `FlattenExportedElementBounds`. |
| D5 | Un groupe avec seulement des `EventBindings` legacy reste traite comme sans runtime moderne : pas de wrapper runtime dedie, pas de `data-scada-events`. |
| D6 | Un groupe avec `CommandConfig` et des artefacts `EventBindings` est exporte via `CommandConfig` seulement. Les artefacts n'ajoutent aucun attribut ni manifest event actif. |
| D7 | Le manifest exporte ne doit plus remplir `Events` depuis `element.EventBindings`. Si le schema garde un champ `events`, il doit etre vide ou legacy-safe, sans contrat runtime. |
| D8 | L'ancien flow reste dans le code comme decommissionne temporaire, sans suppression dans cette tranche. |
| D9 | Le gate export/audit doit signaler les `EventBindings` legacy restants, afin d'eviter des boutons morts silencieux dans TF100Web. |
| D10 | `Data.ReadTagId` / `Data.WriteTagId` est un ancien chemin de binding valeur. Il ne doit pas etre utilise pour justifier un wrapper groupe dans cette spec. Les lectures/ecritures modernes passent par `StateConfig.ReadVariable` et `CommandConfig.WriteTag`. |
| D11 | Le plan d'implementation doit distinguer le correctif export SCADA Builder V2 du gate lifecycle TF100Web. Le wrapper DOM est necessaire mais insuffisant pour prouver l'execution immediate de `StateConfig`. |
| D12 | TF100Web doit initialiser les fragments rendus puis rejouer les valeurs tag courantes vers `ScadaRuntime.onTagValuesChanged(...)` ou un mecanisme equivalent apres `_initRenderedSlot(...)`. |

## 4. Contrat de sortie TF100Web

Cette spec ne change pas le contrat attendu cote TF100Web. Elle aligne SCADA Builder V2 sur
ce contrat.

### 4.1 Attributs autorises pour le runtime moderne

Un element ou groupe runtime peut porter :

```html
data-scada-command-config="..."
data-scada-state-config="..."
```

Ces attributs sont ceux consommes par le runtime SCADA Builder V2 initialise dans TF100Web.

### 4.2 Attribut decommissionne

Interdit comme sortie runtime active :

```html
data-scada-events="..."
```

`data-scada-events` appartient a l'ancien flow `EventBindings`. Le nouvel export ne doit pas
le reintroduire comme fallback, meme si des donnees legacy existent encore dans le projet.

### 4.3 Manifest

Le manifest doit refleter les donnees modernes :

```json
{
  "id": "group_003",
  "displayName": "Navigation Group",
  "kind": "Group",
  "events": [],
  "stateConfig": null,
  "commandConfig": { "commands": [] }
}
```

Regles :

1. `commandConfig` transporte les commandes modernes.
2. `stateConfig` transporte les etats modernes.
3. une lecture de variable moderne est portee par `stateConfig.readVariable`.
4. une ecriture de tag moderne est portee par une commande `commandConfig.commands[].writeTagId`.
5. `valueBindings` / `Data.ReadTagId` / `Data.WriteTagId` ne sont pas retenus comme contrat
   fonctionnel pour cette correction.
6. `events` ne doit pas etre alimente depuis `element.EventBindings`.
7. Si le champ `events` peut etre retire sans casser le schema consomme, le plan
   d'implementation peut proposer son retrait. Sinon il reste vide pour compatibilite de
   forme seulement.

### 4.4 Contrat lifecycle TF100Web

Le contrat de donnees ne change pas, mais le plan doit inclure ou referencer le gate
lifecycle cote TF100Web :

1. apres injection HTML d'un slot, TF100Web appelle `window.ScadaRuntime.initPage(slot, pageId)` ;
2. apres cet appel, TF100Web doit rejouer les valeurs deja connues dans `ScadaTagCache.values`
   vers le runtime SCADA Builder V2 ;
3. ce replay doit evaluer immediatement les elements `[data-scada-state-config]`, incluant
   `stateConfig.readVariable` ;
4. le prochain polling de tags reste utile, mais ne doit pas etre le seul mecanisme qui rend
   un fragment nouvellement injecte fonctionnel ;
5. l'absence de runtime ou de bridge tag doit rester diagnostiquee visiblement, sans retour a
   l'ancien `data-scada-events`.

Pseudo-contrat attendu cote host :

```javascript
await this._initRenderedSlot(slot, pageId);
if (window.ScadaRuntime && window.ScadaRuntime.onTagValuesChanged) {
  window.ScadaRuntime.onTagValuesChanged(Object.assign({}, ScadaTagCache.values));
}
```

L'implementation exacte peut differer, mais le resultat observable est obligatoire : un
`StateConfig` ou `readVariable` deja resolvable par le cache TF100Web est applique
immediatement apres rendu du slot.

## 5. Architecture cible

### 5.1 Helper de wrapper

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

`EventBindings` et `Data.ReadTagId` / `Data.WriteTagId` sont volontairement absents du helper.

### 5.2 Points d'appel

| Methode | Changement attendu |
| --- | --- |
| `BuildElementHtml` | Remplacer le test `EventBindings.Count > 0` par `GroupRequiresRuntimeWrapper(element)` et ne plus emettre `BuildEventAttribute(element)` pour `data-scada-events`. |
| `AppendElementCss` | Remplacer le test `EventBindings.Count > 0` par `GroupRequiresRuntimeWrapper(element)`. |
| `ShouldExportManifestObject` | Remplacer le test `EventBindings.Count > 0` par `GroupRequiresRuntimeWrapper(element)`. |
| `FlattenExportedElementBounds` | Remplacer le test `EventBindings.Count > 0` par `GroupRequiresRuntimeWrapper(element)`. |
| Projection manifest | Ne plus utiliser `Events = element.EventBindings` comme donnees runtime. |
| Diagnostic export/audit | Ajouter ou reutiliser un warning pour les `EventBindings` restants. |

### 5.3 Rendu groupe avec runtime moderne

```html
<div id="ft100-{pageId}__{elementId}"
     class="ft100-element ft100-element--Group"
     data-scada-element-id="{elementId}"
     data-name="{displayName}"
     style="..."
     data-scada-command-config="..."
     data-scada-state-config="...">
  <!-- enfants -->
</div>
```

Tous les enfants doivent conserver leur position exportee actuelle. Le wrapper ne doit pas
introduire de decalage, de double offset, de perte de bounds ou de changement d'ordre visuel.

### 5.4 Rendu groupe sans runtime moderne

Un groupe sans `CommandConfig`, sans `StateConfig`, sans `StateConfig.ReadVariable` et sans
fallback d'etat non defaut reste aplati. Cela inclut les groupes qui ne possedent que des
`EventBindings` legacy ou seulement des anciens `Data.ReadTagId` / `Data.WriteTagId`.

Ce comportement evite d'introduire un nouveau noeud DOM inutile ou un ancien contrat runtime
non consomme par TF100Web.

## 6. Gate et diagnostics

Le correctif doit inclure un gate d'audit/export contre les artefacts legacy.

### 6.1 Warning attendu

Un element contenant `EventBindings` doit produire un diagnostic clair du type :

```text
EventBindings decommissionnes detectes: page=<pageId>, element=<elementId>.
Authoring attendu: CommandConfig ou StateConfig. Les EventBindings ne sont pas exportes
comme runtime TF100Web.
```

Le diagnostic ne doit pas convertir automatiquement les `EventBindings` en `CommandConfig`.
Une conversion silencieuse risquerait d'inventer un contrat ou de masquer une donnee legacy
incorrecte.

### 6.2 Politique de blocage

Pour cette tranche, le comportement recommande est :

1. warning non bloquant si l'element possede aussi une configuration moderne equivalente ;
2. warning visible et action de nettoyage requise si l'element ne possede que des
   `EventBindings` ;
3. pas de fallback `data-scada-events` dans les deux cas.

Si le validateur export possede deja une severite bloquante pour les artefacts runtime
incompatibles, le plan d'implementation devra choisir explicitement entre warning et erreur.
La decision ne doit pas etre implicite dans le code.

## 7. Separation des plans

Le plan d'implementation doit separer deux surfaces :

| Surface | Responsabilite |
| --- | --- |
| SCADA Builder V2 export | Wrapper DOM pour les groupes avec `CommandConfig` / `StateConfig`, retrait de `EventBindings` du chemin export, manifest moderne, diagnostics legacy. |
| TF100Web host | Initialisation deterministe des slots et replay des valeurs tag deja en cache apres rendu, pour garantir l'execution immediate de `StateConfig` / `readVariable`. |

Si le plan est limite a SCADA Builder V2, il doit explicitement marquer le replay TF100Web
comme dependance externe non fermee. Si le plan couvre les deux repos, les tests TF100Web
ci-dessous deviennent obligatoires.

## 8. Tests

| Test | Scenario |
| --- | --- |
| `Export_GroupWithOnlyCommandConfig_RendersWrapperWithCommandAttribute` | Groupe avec `CommandConfig`, sans `EventBindings` -> wrapper + `data-scada-command-config`. |
| `Export_GroupWithOnlyStateConfig_RendersWrapperWithStateAttribute` | Groupe avec `StateConfig`, sans `EventBindings` -> wrapper + `data-scada-state-config`. |
| `Export_GroupWithOnlyStateReadVariable_RendersWrapperWithStateAttribute` | Groupe avec `StateConfig.ReadVariable` -> wrapper + `data-scada-state-config` contenant `readVariable`. |
| `Export_GroupWithWriteTagCommand_RendersWrapperWithCommandAttribute` | Groupe avec `CommandConfig.WriteTag` -> wrapper + commande `writeTagId` serialisee, sans `data-scada-events`. |
| `Export_GroupWithNavigateCommand_RendersWrapperWithCommandAttribute` | Groupe avec `CommandConfig.Navigate` -> wrapper + commande `targetPageId` serialisee, sans `data-scada-events`. |
| `Export_GroupWithNoRuntimeData_FlattensChildren` | Groupe vide -> aplati, pas de wrapper. |
| `Export_GroupWithOnlyLegacyDataValueBindings_DoesNotRequireRuntimeWrapper` | Groupe avec seulement `Data.ReadTagId` / `Data.WriteTagId` -> pas de wrapper justifie par ce chemin legacy. |
| `Export_GroupWithOnlyLegacyEventBindings_DoesNotExportRuntimeEvents` | Groupe avec seulement `EventBindings` -> pas de `data-scada-events`, pas de manifest events actifs, diagnostic decommission. |
| `Export_GroupWithCommandConfigAndLegacyEventBindings_UsesCommandConfigOnly` | Groupe avec `CommandConfig` + artefacts `EventBindings` -> wrapper via `CommandConfig`, aucun `data-scada-events`. |
| `Export_Manifest_DoesNotSerializeLegacyEventBindingsAsActiveEvents` | Manifest exporte -> `Events` vide/legacy-safe, jamais rempli depuis `element.EventBindings`. |
| `Export_GroupRuntimeWrapper_DoesNotChangeChildGeometry` | Groupe moderne wrapper -> positions, bounds et ordre visuel des enfants restent coherents avec l'export attendu. |
| `AuthoringWorkflow_DoesNotExposeLegacyElementEventDialog` | Verification statique/UI -> aucun menu/ruban/proprietes ne permet d'ouvrir l'ancien `ElementEventDialog`; StateConfig/CommandConfig restent les chemins actifs. |

### 8.1 Tests TF100Web obligatoires si le plan couvre le host

| Test | Scenario |
| --- | --- |
| `ScadaHost_InitRenderedSlot_ReplaysCachedTagValues` | Apres `_initRenderedSlot`, TF100Web appelle `ScadaRuntime.onTagValuesChanged(...)` avec `ScadaTagCache.values`. |
| `ScadaHost_StateConfigReadVariable_AppliesImmediatelyAfterRender` | Un fragment nouvellement rendu avec `data-scada-state-config` + `readVariable` affiche la valeur deja en cache sans attendre un nouveau polling. |
| `ScadaHost_CommandConfigNavigate_GroupWrapperNavigates` | Un groupe wrappé avec `data-scada-command-config` navigate declenche `ScadaHost.loadPage(targetPageId)`. |
| `ScadaHost_CommandConfigWriteTag_PostsMappingWrite` | Une commande `writeTag` avec `writeTagId = tf100.mapping.X` poste vers `/visualisation/mapping/write/` avec `mapping_id = X`. |
| `ScadaHost_DoesNotDependOnDataScadaEvents` | Le host moderne n'a pas besoin de `data-scada-events` pour Navigate/WriteTag/StateConfig. |

## 9. Hors scope

1. Supprimer physiquement `ElementEventDialog` et les helpers `With*Event`.
2. Convertir automatiquement les `EventBindings` en `CommandConfig` ou `StateConfig`.
3. Reintroduire `data-scada-events` dans TF100Web.
4. Changer le runtime TF100Web pour lire l'ancien flow.
5. Reactiver l'ancien chemin `Data.ReadTagId` / `Data.WriteTagId` comme contrat groupe.
6. Redesign du manifest au-dela du retrait logique des events actifs legacy.

## 10. Checklist de validation

- [ ] Aucun export `.sb2` moderne ne contient `data-scada-events`.
- [ ] Un groupe avec `CommandConfig` sans `EventBindings` navigue dans TF100Web apres init runtime.
- [ ] Un groupe avec `StateConfig` sans `EventBindings` expose `data-scada-state-config`.
- [ ] Un groupe avec `StateConfig.ReadVariable` sans `EventBindings` expose `data-scada-state-config`.
- [ ] Un groupe avec seulement `Data.ReadTagId` / `Data.WriteTagId` ne devient pas wrapper runtime par ce seul critere.
- [ ] TF100Web rejoue les valeurs tag deja en cache apres rendu d'un slot contenant `data-scada-state-config`.
- [ ] `StateConfig.ReadVariable` s'affiche immediatement apres chargement si sa valeur est deja dans `ScadaTagCache.values`.
- [ ] `CommandConfig.WriteTag` poste vers le endpoint mapping write avec l'id canonique attendu.
- [ ] Un groupe sans runtime moderne reste aplati.
- [ ] Les artefacts `EventBindings` restants sont diagnostiques.
- [ ] Le manifest ne serialise pas `element.EventBindings` comme events actifs.
- [ ] Les fonctions legacy restent presentes mais marquees decommissionnees jusqu'a retrait approuve.
