# TF100Web - Initialisation deterministe du runtime SCADA Builder V2 - Design

Date: 2026-07-09
Status: Draft design - pret pour validation technique
Document version: `V2.1.3.0005`
Portee: TF100Web (runtime hote `ScadaHost`) + SCADA Builder V2 (runtime JS exporte, tests de contrat)
Dependance: `2026-07-07-scada-export-runtime-tf100web-integration-design.md`,
`2026-07-08-tf100web-scada-header-footer-composition-design.md`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-07-09 | `V2.1.3.0005` | `PENDING` | Creation de la spec d'initialisation deterministe du runtime TF100Web, avec nettoyage obligatoire des artefacts `EventBindings` legacy-only. |

## 1. Probleme

Une regression runtime a ete observee sur un package `.sb2` exporte par SCADA Builder V2
et deploye dans TF100Web : les commandes de navigation du footer `win00003` ne changent pas
de page au clic, meme lorsque le DOM contient bien `data-scada-command-config`.

Investigation confirmee :

1. Le package `.sb2` contient le runtime partage `scada-runtime.<hash>.js`.
2. Le DOM TF100Web contient `#scada-host` et les slots `#scada-host-header`,
   `#scada-host-body`, `#scada-host-footer`.
3. Les boutons `Page d'accueil` et `Compresseur` du footer contiennent bien
   `data-scada-command-config` avec un payload camelCase valide :
   `kind: "navigate"`, `trigger: "onClick"`, `targetPageId: "win00004"` ou
   `targetPageId: "win00059"`.
4. `window.postMessage({source:'scada-builder-v2', action:'navigate', pageId:'win00059'}, '*')`
   fonctionne cote TF100Web : le listener hote et `ScadaHost.loadPage()` sont corrects.
5. L'appel manuel suivant reactive les clics :

```javascript
window.ScadaRuntime.initPage(document.getElementById('scada-host-footer'), 'win00003')
```

Conclusion : le fragment est injecte avant que `window.ScadaRuntime.initPage` ait attache
les handlers runtime. Le bug n'est pas un probleme de casse `camelCase` / `PascalCase` pour
`Navigate`; c'est une condition de course entre le chargement du runtime exporte et
l'injection des fragments par `visualisation_import.js`.

Impact : la condition de course ne touche pas seulement `Navigate`. Tout comportement attache
par `ScadaRuntime.initPage()` peut etre absent du slot rendu :

1. commandes `[data-scada-command-config]` : navigate, popup, write command, URL, back ;
2. moteur d'etat `StateEngine.initPage(...)` ;
3. garde d'edition input `[data-scada-read-tag]`, `[data-scada-write-tag]` ;
4. evenement lifecycle `scada-builder-page-ready`.

## 2. Objectif

Rendre l'initialisation du runtime SCADA Builder V2 deterministe, rejouable et observable,
independamment de :

1. l'ordre effectif de chargement des scripts ;
2. la presence de `defer`, de cache navigateur, ou d'un chargement reseau plus lent ;
3. la composition header/body/footer ;
4. les popups ;
5. les futures reinitialisations de slots.

Le correctif durable ne doit pas dependre uniquement de la position d'une balise `<script>`.
Le host TF100Web doit posseder explicitement le cycle de vie : "rendre le fragment" puis
"initialiser ce fragment quand le runtime est pret".

## 3. Decisions verrouillees

| # | Decision |
|---|---|
| D1 | Le cycle de vie d'initialisation des fragments est une responsabilite TF100Web (`visualisation_import.js` / `ScadaHost`), pas une auto-reparation opaque dans le runtime exporte. |
| D2 | `_renderPart(role, part)` ne doit plus jamais ignorer silencieusement l'initialisation si `window.ScadaRuntime.initPage` est absent. |
| D3 | `ScadaHost.init()` attend explicitement que le runtime soit pret avant le premier `loadPage(homePageId)`, afin d'eviter une page d'accueil partiellement non initialisee. |
| D4 | Le meme chemin d'initialisation est utilise pour `header`, `body`, `footer` et les popups. |
| D5 | Le runtime SCADA Builder doit devenir idempotent pour permettre retries/replay sans double-binding de handlers. |
| D6 | Retirer `defer` du template peut masquer le symptome, mais ne constitue pas le correctif robuste retenu dans ce design. |
| D7 | Le runtime exporte ne doit pas connaitre les ids TF100Web `scada-host-header/body/footer`; ce couplage resterait dans le host. |
| D8 | Les anciens `data-scada-events` / `EventBindings` legacy-only sont des artefacts de donnees a decommissionner, pas un flux a maintenir ni a convertir automatiquement dans l'exporteur. |
| D9 | `ScadaHost.init()` peut rester appele en fire-and-forget, mais toute erreur async doit etre capturee, journalisee, et visible sur le host. Aucune rejection non geree ne doit rester dans la console. |
| D10 | Les `EventBindings` orphelins doivent etre nettoyes dans les donnees projet existantes. Le diagnostic d'export est un garde-fou, pas le mecanisme principal de correction des projets affectes. |

## 4. Architecture cible TF100Web

### 4.1 Runtime readiness barrier

Ajouter a `ScadaHost` un mecanisme unique d'attente du runtime :

```javascript
_waitForScadaRuntime(timeoutMs = 5000)
```

Contrat :

1. retourne immediatement si `window.ScadaRuntime && window.ScadaRuntime.initPage` existe ;
2. sinon attend via polling court, par exemple 25-50 ms ;
3. ecoute aussi `window.load` pour rechecker apres chargement complet ;
4. resolve des que `initPage` existe ;
5. reject apres timeout avec un message exploitable en console et dans l'etat runtime TF100Web.

Le timeout doit produire une erreur visible du type :

```text
Runtime SCADA Builder indisponible: impossible d'initialiser la page.
```

Pas de 500 serveur : c'est une erreur navigateur/runtime.

### 4.2 Initialisation centralisee des slots

Ajouter :

```javascript
async _initRenderedSlot(slot, pageId)
```

Contrat :

1. ignore si `slot` ou `pageId` est vide ;
2. attend `_waitForScadaRuntime()` ;
3. appelle `window.ScadaRuntime.initPage(slot, pageId)` ;
4. marque le slot :

```javascript
slot.dataset.scadaRuntimeInitialized = "1";
slot.dataset.scadaRuntimePageId = pageId;
```

5. en cas d'echec, marque :

```javascript
slot.dataset.scadaRuntimeInitialized = "error";
```

et affiche/logue l'erreur.

### 4.3 Flow `ScadaHost.init`

Avant :

```javascript
init(scadaHostEl) {
  const homePageId = scadaHostEl.dataset.scadaHomePage || 'win00009';
  ScadaTagCache.startPolling();
  this.loadPage(homePageId);
}
```

Apres :

```javascript
async init(scadaHostEl) {
  const homePageId = scadaHostEl.dataset.scadaHomePage || 'win00009';
  ScadaTagCache.startPolling();
  await this._waitForScadaRuntime();
  await this.loadPage(homePageId);
}
```

Si le runtime n'est pas disponible, `loadPage(homePageId)` ne doit pas rendre une page
non interactive sans diagnostic.

### 4.3.1 Call site `ScadaHost.init`

Le call site actuel est fire-and-forget :

```javascript
if (isNewScadaHost) {
  ScadaHost.init(scadaHostEl);
}
```

Ce mode peut rester valide : un runtime absent ne doit pas faire tomber toute la page station.
Mais l'erreur async doit etre geree explicitement. Le call site cible devient :

```javascript
if (isNewScadaHost) {
  ScadaHost.init(scadaHostEl).catch((error) => {
    console.error('scada: host initialization failed', error);
    ScadaHost._showRuntimeError('Runtime SCADA Builder indisponible.');
  });
}
```

`_showRuntimeError(message)` doit poser un diagnostic visible sur `#scada-host` ou son overlay
runtime, sans masquer le reste de l'interface TF100Web. Objectif : pas de rejection non geree,
pas de page silencieusement non interactive, pas de crash global de la visualisation.

### 4.4 Flow `_renderPart`

`_renderPart(role, part)` doit etre `async`. La relation avec `_initRenderedSlot` est un
contrat primaire du premier rendu, pas seulement un mecanisme de replay ou de rattrapage
apres chargement tardif du runtime.

Avant :

```javascript
slot.innerHTML = part.html;
this._injectCssIfNeeded(part.css_hash, part.page_id);
if (window.ScadaRuntime && window.ScadaRuntime.initPage) {
  window.ScadaRuntime.initPage(slot, part.page_id);
}
```

Apres :

```javascript
async _renderPart(role, part) {
  const slot = this._slotForRole(role);
  if (!slot || !part) return;

  slot.innerHTML = part.html;
  this._injectCssIfNeeded(part.css_hash, part.page_id);
  await this._initRenderedSlot(slot, part.page_id);
}
```

`loadPage(pageId)` doit attendre tous les slots rendus pour la page courante avant de
considerer le chargement termine :

```javascript
await Promise.all(parts.map((part) => this._renderPart(part.role, part)));
```

ou un equivalent sequentiel si l'ordre d'injection doit rester strict :

```javascript
for (const part of parts) {
  await this._renderPart(part.role, part);
}
```

Le point non negociable : le premier rendu de `header`, `body` et `footer` passe par la
barriere runtime et attend `ScadaRuntime.initPage`. Les marqueurs `data-scada-runtime-*`
sont un filet d'observabilite et de verification, pas le chemin principal d'initialisation.

Ancien pseudo-code interdit :

```javascript
slot.innerHTML = part.html;
this._injectCssIfNeeded(part.css_hash, part.page_id);
this._initRenderedSlot(slot, part.page_id); // fire-and-forget
```

L'absence temporaire de runtime ne doit plus etre un skip definitif, et ne doit pas produire
une page initiale marquee visuellement chargee mais non interactive.

### 4.5 Flow popup

Dans `_createPopup(pageId, options)`, apres injection de `bodyPart.html` dans le conteneur
popup, appeler exactement :

```javascript
await this._initRenderedSlot(content, bodyPart.page_id);
```

Ne pas creer un second chemin d'initialisation pour les popups.

## 5. Idempotence cote runtime SCADA Builder V2

Une fois que TF100Web peut rejouer l'initialisation, le runtime exporte doit etre robuste
aux appels multiples.

### 5.1 CommandDispatcher.bind

Probleme actuel : `CommandDispatcher.bind(element)` attache des handlers anonymes. Si
`initPage()` est appele deux fois sur le meme DOM, les handlers peuvent etre attaches deux
fois et une commande peut s'executer deux fois.

Correction cible :

```javascript
function bind(element) {
  var configRaw = element.getAttribute('data-scada-command-config');
  if (!configRaw) return;

  if (element.dataset.scadaCommandBoundConfig === configRaw) {
    return;
  }
  element.dataset.scadaCommandBoundConfig = configRaw;

  // parse + bind
}
```

Si une future version doit supporter le changement dynamique de `data-scada-command-config`
sur le meme element sans remplacement DOM, elle devra stocker les handlers et les retirer avant
rebind. Pour le flux actuel, les fragments sont remplaces par `innerHTML`; le fingerprint suffit.

### 5.2 StateEngine / InputEditGuard

Verifier et verrouiller par tests que :

1. `StateEngine.initPage(container, pageId)` ne cree pas plusieurs timers concurrents pour
   le meme container/page ;
2. `InputEditGuard.watch(element)` ne double-bind pas focus/blur/change/keydown.

Si ces modules ne sont pas idempotents, appliquer le meme principe :

```javascript
element.dataset.scadaInputGuardBound = "1";
```

ou une cle plus precise si le binding depend de la configuration.

## 6. Decommission des artefacts event-only legacy

Le fix d'initialisation rend fonctionnels les elements qui possedent deja
`data-scada-command-config`. Les elements qui n'ont que `data-scada-events` proviennent
de l'ancien flux d'evenements et ne representent plus un contrat runtime actif.

Observation sur `win00003` :

1. `group_001` et `group_002` possedent `data-scada-command-config` et doivent fonctionner
   apres correction de l'init runtime.
2. `group_003`, `group_004`, `group_008` possedent `data-scada-events` seulement et resteront
   non fonctionnels dans le nouveau `#scada-host`, car le runtime exporte lit
   `[data-scada-command-config]`, pas `[data-scada-events]`.

Decision produit : ces 5 actions mortes ne doivent pas etre sauvees par un fallback
`data-scada-events` ni par une synthese automatique dans l'exporteur. Elles sont des restes
non retires de l'ancien modele. Sur un nouveau projet cree avec le flux courant
Etat/Commande, ce cas ne doit plus apparaitre.

Plan de decommission :

1. Identifier dans les projets existants les `EventBindings` / `Actions` legacy-only qui
   n'ont pas de `CommandConfig` equivalent.
2. Nettoyer explicitement ces `EventBindings` orphelins dans les donnees projet existantes.
   Le nettoyage doit etre auditable : liste des elements touches, page, id element, action
   retiree, et confirmation que l'element ne possede pas de `CommandConfig` equivalent.
3. Conserver les commandes valides deja authorisees dans `CommandConfig`.
4. Ajouter un diagnostic d'audit projet/export : un element avec `EventBindings` legacy-only
   est signale comme artefact decommissionne, pas converti silencieusement. Ce diagnostic
   doit apparaitre avant le runtime, idealement a l'export `.sb2` ou dans un audit projet,
   afin que l'auteur ne decouvre pas des boutons morts seulement sur l'unite TF100Web. Il
   ne remplace pas le nettoyage des projets affectes.
5. Ajouter une regression : un nouveau projet ou un nouvel element commande ne doit pas
   produire de `EventBindings` Navigate legacy-only.

Regle d'architecture : le nouveau runtime TF100Web ne lit pas `data-scada-events`, et
l'exporteur ne doit pas reintroduire ce flux comme compatibilite implicite.

## 7. Tests

### 7.1 TF100Web

Tests statiques minimaux dans `frontend/tests_scada_package.py` ou module voisin :

1. `visualisation_import.js` contient `_waitForScadaRuntime`.
2. `visualisation_import.js` contient `_initRenderedSlot`.
3. `_renderPart` est `async` et appelle `await this._initRenderedSlot(slot, part.page_id)`.
4. `loadPage` attend les promesses de `_renderPart` pour les parts `header`, `body` et
   `footer` avant de terminer le chargement de page.
5. `_renderPart` ne contient plus le pattern
   `if (window.ScadaRuntime && window.ScadaRuntime.initPage) { window.ScadaRuntime.initPage(...) }`
   comme seul chemin.
6. `_createPopup` utilise `await this._initRenderedSlot(content, bodyPart.page_id)`.
7. Le call site `ScadaHost.init(scadaHostEl)` capture les erreurs avec `.catch(...)` ou un
   wrapper equivalent, et appelle un diagnostic visible type `_showRuntimeError(...)`.

Test JS/browser souhaite :

1. demarrer `ScadaHost.init()` avec `window.ScadaRuntime` absent ;
2. injecter `window.ScadaRuntime.initPage` apres un delai ;
3. verifier que le chargement initial attend l'initialisation des slots `header`, `body` et
   `footer`, puis que chaque slot rendu est initialise et marque
   `data-scada-runtime-initialized="1"`.

Test manuel production :

```javascript
document.querySelectorAll('#scada-host-footer [data-scada-command-config]').length
```

doit retourner `2` sur le cas observe, puis les clics `Page d'accueil` et `Compresseur`
doivent changer de page sans appel manuel a `initPage`.

### 7.2 SCADA Builder V2 runtime JS

Corriger les fixtures existantes de `tests/runtime-js/command-dispatcher.test.mjs` :

1. utiliser `kind: 'openPopup'`, `kind: 'closePopup'`, `kind: 'togglePopup'`,
   `kind: 'writeTag'` au lieu de PascalCase ;
2. ajouter un test direct :

```javascript
execute(element, { kind: 'navigate', trigger: 'onClick', targetPageId: 'win00059' })
```

doit publier :

```json
{"source":"scada-builder-v2","action":"navigate","pageId":"win00059"}
```

3. ajouter un test idempotence : appeler `CommandDispatcher.bind(element)` deux fois avec le
meme `data-scada-command-config`, cliquer une fois, verifier qu'un seul message est publie.

### 7.3 SCADA Builder V2 - decommission legacy event data

1. test d'audit : un projet contenant `EventBindings` sans `CommandConfig` equivalent est
   signale comme contenant des artefacts legacy decommissionnes ;
2. test de nettoyage projet : les `EventBindings` orphelins sont retires sans toucher aux
   `CommandConfig` valides, avec trace/audit des elements modifies ;
3. test nouveau flux : l'authoring courant d'une commande Navigate cree seulement
   `CommandConfig` et ne cree pas de `EventBindings` Navigate legacy-only.
4. test export/audit : une scene contenant des `EventBindings` Navigate legacy-only produit
   un warning explicite de decommission avant export ou pendant validation, sans synthese
   automatique de `CommandConfig`.

## 8. Fichiers touches (resume)

| Repo | Fichier | Action |
|---|---|---|
| TF100Web | `static/asset/js/station/visualisation_import.js` | Ajouter runtime readiness barrier, `_initRenderedSlot`, init slots/popups deterministe |
| TF100Web | `frontend/tests_scada_package.py` ou test voisin | Ajouter regressions statiques et/ou JS sur l'init runtime |
| SCADA Builder V2 | `src/ScadaBuilderV2.Rendering/Runtime/command-dispatcher.js` | Rendre `bind` idempotent |
| SCADA Builder V2 | `tests/runtime-js/command-dispatcher.test.mjs` | Corriger payloads camelCase, ajouter navigate et idempotence |
| SCADA Builder V2 | `src/ScadaBuilderV2.Rendering/Runtime/state-engine.js` | Verifier/ajouter idempotence si timer ou binding double possible |
| SCADA Builder V2 | `src/ScadaBuilderV2.Rendering/Runtime/input-edit-guard.js` | Verifier/ajouter idempotence du binding input |
| SCADA Builder V2 | Projet/data cleanup ou outil de migration a definir | Nettoyer obligatoirement les `EventBindings` orphelins dans les projets existants, avec audit |
| SCADA Builder V2 | Export/audit diagnostic a definir | Signaler les `EventBindings` legacy-only avant runtime, sans conversion implicite |
| SCADA Builder V2 | Tests domaine/projet/export a definir | Verrouiller que le nouveau flux Navigate authorise `CommandConfig` sans recreer `EventBindings` legacy-only |

## 9. Hors scope

1. Retirer `defer` du template TF100Web comme seule solution.
2. Mettre de la logique TF100Web (`scada-host-header/body/footer`) dans `scada-runtime.js`.
3. Reintroduire l'execution des scripts inline des pages exportees.
4. Revenir au vieux dispatcher `data-scada-events` dans le nouveau host comme chemin principal.
5. Nettoyage de `frontend/scada_package.py` / flux legacy.
6. Redesign de la composition header/body/footer, deja couvert par
   `2026-07-08-tf100web-scada-header-footer-composition-design.md`.

## 10. Checklist de validation

- [ ] Ouverture runtime TF100Web : aucun appel manuel a `window.ScadaRuntime.initPage(...)`
      n'est necessaire pour activer les boutons.
- [ ] `Page d'accueil` dans `win00003` navigue vers `win00004`.
- [ ] `Compresseur` dans `win00003` navigue vers `win00059`.
- [ ] Une page body contenant des `data-scada-command-config` est initialisee apres navigation.
- [ ] Un popup avec runtime content est initialise via le meme chemin.
- [ ] Une absence de runtime produit un diagnostic visible et aucune rejection async non geree.
- [ ] Reappeler `window.ScadaRuntime.initPage(slot, pageId)` ne double pas les executions de commande.
- [ ] Les tests JS SCADA Builder passent avec les payloads camelCase reels.
- [ ] Les `EventBindings` orphelins existants sont identifies, nettoyes dans les donnees projet, et signales explicitement par audit.
