# SCADA Export Runtime & TF100Web Industrial Integration — Design

Date: 2026-07-07
Status: Approved design — prêt pour planification d'implémentation
Portée: SCADA Builder V2 (exporteur) + TF100Web (runtime hôte, déploiement industriel)
Dépendance: `2026-07-07-element-plus-state-command-events-design.md` (modèle domaine implémenté)

## 1. Problème

Le pipeline actuel Builder V2 → TF100Web a trois fractures structurelles :

1. **Le `<script>` runtime exporté par le Builder n'est jamais exécuté par TF100Web.**
   `scada_package.py` extrait uniquement le fragment `<div id="ft100-...">` du HTML
   exporté — le `<script>` après le `</div>` racine est ignoré. Résultat : popups,
   conditions, effets visuels, et tout le runtime state/command sont inactifs.

2. **Le modèle state/command (ElementEvents) est implémenté côté Builder mais absent
   de l'export.** `Ft100SceneExporter` ne référence aucun type de
   `ScadaBuilderV2.Domain.ElementEvents`. Les `StateConfig` et `CommandConfig` des
   Element+ ne sont pas sérialisés dans le manifest ni dans le HTML.

3. **Le pipeline de déploiement est fragile** : upload admin Django, extraction ZIP,
   parsing manifest, injection `data-scada-*` serveur, composition header/body/footer,
   rewrite d'assets. Trop de transformations entre la sortie Builder et l'exécution
   runtime.

## 2. Objectif

Un pipeline direct et maîtrisé où le Builder est la source unique de vérité :

```
Builder → export .sb2 → script sh TF100Web → fichiers dans templates/static
       → collectstatic → restart → pages servies → script runtime EXÉCUTÉ
```

- **Le `<script>` exporté par le Builder devient LA source unique du runtime.**
  Il tourne dans le navigateur TF100Web exactement comme il tournerait si on ouvrait
  le `.html` exporté directement.
- **StateConfig + CommandConfig** sont sérialisés dans le manifest ET en data
  attributes HTML → le runtime JS les lit et les exécute.
- **Navigation AJAX conservée** : `visualisation_import.js` orchestre le chargement
  des pages, l'injection des fragments, et fournit le bridge vers le backend TF100Web
  (`getTagValue` / `writeTag` / snapshot polling).

## 3. Architecture globale

```
┌─ SCADA Builder V2 ───────────────────────────────────────────────────┐
│                                                                       │
│  Éditeur (WebView2)            Export .sb2                           │
│  ├─ HTML legacy brut           ├─ manifest.json                      │
│  ├─ Bridge JS ↔ C#             ├─ scada-runtime.<hash>.js  ←★ nouveau│
│  └─ Injection DOM live         ├─ <page>/*.html    ← <script src>    │
│                                ├─ <page>/css/*.<hash>.css             │
│                                └─ <page>/images/*.<hash>.svg          │
│                                                                       │
│  ★ StateConfig + CommandConfig sérialisés dans:                       │
│     - manifest.json (Objects[].StateConfig, Objects[].CommandConfig)  │
│     - HTML data-scada-state-config / data-scada-command-config        │
│     - script runtime: évaluateur AST + effets + animations + commandes│
└──────────────────────────────────────────────────────────────────────┘
                                      │
                              .sb2 (ZIP)
                                      │
                                      ▼
┌─ TF100Web (déploiement industriel) ──────────────────────────────────┐
│                                                                       │
│  management command: deploy_scada_builder <path/to/package.sb2>       │
│  ├─ Décompresse le .sb2                                              │
│  ├─ Copie <page>/*.html  → templates/frontend/scada/pages/           │
│  ├─ Copie <page>/css/    → static/scada/css/                         │
│  ├─ Copie <page>/images/ → static/scada/images/                      │
│  ├─ Copie scada-runtime  → static/scada/js/                          │
│  └─ Lance collectstatic --noinput                                    │
│                                                                       │
│  Vue Django: scada_page(request, page_id)                            │
│  ├─ Rend le template HTML complet (script inclus → exécuté)          │
│  └─ Gated: TF100_INDUSTRIAL_DEPLOYMENT + SCADA_BUILDER_2             │
│                                                                       │
│  Navigation AJAX (gardée):                                            │
│  ├─ GET /scada/api/page/<page_id>/ → HTML complet + métadonnées      │
│  └─ visualisation_import.js: DOMParser → fragment → innerHTML        │
│     Le <script> est déjà chargé en amont, pas dans le fragment        │
└──────────────────────────────────────────────────────────────────────┘
```

**Principe clé :** le `<script>` runtime exporté par le Builder est la source unique de
vérité pour le runtime. Il tourne dans le navigateur TF100Web exactement comme si on
ouvrait le `.html` directement.

## 4. Stratégie de cache et assets

**Problème :** sur cible ARM Cortex-A72 / 4 GB RAM, recharger CSS + images + JS à
chaque navigation AJAX est inacceptable.

**Solution : content-hash + cache immutable.**

### 4.1 Structure du package .sb2

```
scada-builder-v2-ft100-package/
  manifest.json
  scada-runtime.a1b2c3d.js          ← runtime partagé (hash SHA-256, 8 premiers hex)
  win00008/
    win00008.html                    ← <script src="../scada-runtime.a1b2c3d.js">
    css/
      win00008.d4e5f6g.css          ← hashé
    images/
      pump.7h8i9j.svg               ← hashé
      motor.k1l2m3.svg              ← hashé
  win00009/
    win00009.html
    css/
      win00009.n4o5p6.css
    images/
      valve.q7r8s9.svg
```

### 4.2 Déploiement sur TF100Web

```
templates/frontend/scada/pages/     ← .html (servis par Django)
static/scada/
  js/scada-runtime.a1b2c3d.js      ← 1 an cache (immutable)
  css/win00008.d4e5f6g.css         ← 1 an cache
  images/pump.7h8i9j.svg           ← 1 an cache
```

### 4.3 Comportement de cache à la navigation

```
Page win00008 → Page win00009 (AJAX) :

1. fetch /scada/api/page/win00009/
2. DOMParser → extrait fragment + <link rel="stylesheet" href="...win00009.css">
3. Si <link> pas déjà dans le DOM → l'ajouter (sinon skip — déjà en cache)
4. innerHTML du fragment (images déjà en cache si vues avant)
5. Le <script src="scada-runtime.*.js"> est dans la page hôte, pas dans le fragment
   → chargé UNE SEULE FOIS à l'ouverture de la station
```

**Ce qui est chargé 1 fois (cache permanent) :**
- `scada-runtime.<hash>.js` — évaluateur, effets, animations, bridge tags
- Toutes les images/SVG déjà vues
- Tous les CSS déjà vus

**Ce qui est chargé à chaque navigation :**
- Le fragment HTML de la nouvelle page (~10-50 Ko)
- Le CSS de la nouvelle page si première visite (~5-20 Ko)
- Les images de la nouvelle page si première visite

**Content hash :** SHA-256 du contenu du fichier, 8 premiers caractères hex. Si le
fichier ne change pas entre deux exports, le hash ne change pas → le cache navigateur
tient.

## 5. Script runtime : évaluateur state/command

Le runtime est un fichier JS autonome (`scada-runtime.<hash>.js`, ~30-40 Ko minifié)
partagé entre toutes les pages d'un package. Il couvre l'ensemble du contrat
`docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md`.

### 5.1 Évaluateur d'expressions (AST walker)

L'AST est la source de vérité (pas le texte source). Sérialisé en JSON dans
`data-scada-state-config`.

```
Walk(AST node, tagValues):
  literalNumber → node.value
  literalBool   → node.value
  literalString → node.value
  tagRef        → tagValues[node.tagName]  // null si indisponible
  unary Not     → !Walk(node.operand)
  unary Negate  → -Walk(node.operand)
  binary Add/Sub/Mul/Div/Mod → Walk(left) op Walk(right)
  binary And/Or → court-circuit
  binary Equal/NotEqual/LessThan/... → comparaison
  func ABS(x)   → Math.abs(Walk(x))
  func MIN(a,b) → Math.min(Walk(a), Walk(b))
  func MAX(a,b) → Math.max(Walk(a), Walk(b))
  func BIT(tag,n)→ (Walk(tag) >> n) & 1  → booléen

Erreur div/0, tag null → signalé, ne bloque pas l'évaluation
```

### 5.2 Boucle d'évaluation d'état (first-match-wins)

```
ÉvaluerÉtats(element):
  1. Parcourir States[] (ordre = priorité, haut gagne)
     a. Pour chaque State:
        - Si un {tag} dans son expression est null → SKIP (qualité)
        - Sinon, évaluer l'expression:
          - true  → appliquer Effect, STOP (first-match-wins)
          - false → continuer
          - erreur → false + flag erreur, continuer
  2. Tous les états ont été skipés (qualité) → QualityFallback
  3. Aucun match → DefaultEffect (repos)
  4. Flag erreur → badge discret + TextContent = "---"
```

### 5.3 Application des effets

```
AppliquerEffect(element, effect):
  effect.BackgroundColor → element.style.backgroundColor
  effect.BorderColor     → element.style.borderColor
  effect.BorderWidth     → element.style.borderWidth
  effect.TextColor       → element.style.color
  effect.TextContent     → element.querySelector('[data-scada-text]').textContent
  effect.TextVisible     → toggle visibility du texte
  effect.ElementVisible  → element.hidden
  effect.Opacity         → element.style.opacity
  effect.Rotation        → element.style.transform = 'rotate(...deg)'
  effect.Animation       → appliquer classe CSS d'animation

Propriété null = ne pas toucher (laisse la valeur de conception)
```

### 5.4 Animations (CSS classes)

```css
.scada-anim-blink  → @keyframes opacity 0↔1, 0.6s step-end infinite
.scada-anim-pulse  → @keyframes scale 1↔1.05, 1s ease-in-out infinite
.scada-anim-halo   → @keyframes box-shadow pulse, 1.8s ease-in-out infinite
.scada-anim-spin   → @keyframes rotate 0→360deg, 1.2s linear infinite
```

Une seule animation active par état (spec D7). Les noms d'animation sont page-scopés
via le préfixe `ft100-<page>---` pour éviter les collisions entre pages.

### 5.5 Commandes

```
ExécuterCommande(element, command):
  Trigger: OnClick | OnRelease | OnHover | OnHoverEnter | OnHoverExit

  WriteTag:
    Momentary    → press=OnValue, release=OffValue
    Toggle       → lire ReadTagId, écrire l'inverse vers WriteTagId
    SetFixed     → écrire FixedValue
    SetFromInput → valeur saisie par l'opérateur

  Navigate   → postMessage('scada-navigate', {pageId})
  OpenPopup  → postMessage('openPopup', {targetPageId, options})
  ClosePopup → postMessage('closePopup', {targetPageId})
  TogglePopup→ postMessage('togglePopup', {targetPageId, options})
  OpenUrl    → window.open(url, newTab ? '_blank' : '_self')
  Back       → history.back()

  Confirmation → si présente, modale avant exécution
```

`Navigate`/`OpenPopup`/`ClosePopup`/`TogglePopup` délèguent à l'hôte
(`visualisation_import.js`) via `postMessage`. L'hôte gère le fetch de la page cible,
l'injection du fragment, et la création/suppression des popups iframe.

### 5.6 Bridge tags avec l'hôte TF100Web

```javascript
// Appels définis dans le contrat existant
window.tf100webScadaBuilder.getTagValue(tagId)  → valeur ou null
window.tf100webScadaBuilder.writeTag(tagId, value, payload)

// Si indisponible (page standalone dans un navigateur), fallback:
window.scadaBuilderTagValues[tagId]
```

### 5.7 Boucle de watch (polling)

```
Cycle d'évaluation (par page):
  1. Collecter tous les {tags} référencés dans les expressions d'état
  2. getTagValue() pour chaque tag
  3. Pour chaque Element+ avec StateConfig:
     → ÉvaluerÉtats(element)
     → AppliquerEffect si changé
  4. Intervalle: 500ms
  5. Optimisation: si aucune valeur de tag n'a changé → skip
```

### 5.8 Protection d'édition input

Quand l'opérateur clique un input pour éditer une valeur :

```
  1. Input reçoit le focus
  2. → WatchLoop passe en PAUSE pour cet élément
  3. → Overlay backshadow sur l'input
  4. → Timer 30s démarre

  Si opérateur tape Entrée / Tab / blur:
  → Valeur écrite via writeTag()
  → Overlay retiré
  → WatchLoop reprend immédiatement

  Si 30s sans action:
  → WatchLoop reprend
  → Valeur en cours abandonnée (pas d'écriture)
  → Input rafraîchi avec la dernière valeur tag
  → Overlay retiré
  → Focus retiré (blur forcé)
```

Backshadow visuel :

```css
.scada-input-edit-overlay {
  position: absolute; inset: 0;
  background: rgba(15, 42, 48, 0.06);
  border: 2px solid rgba(15, 42, 48, 0.32);
  border-radius: 4px;
  pointer-events: none;
  z-index: 10;
  animation: scada-edit-pulse 2s ease-in-out infinite;
}
@keyframes scada-edit-pulse {
  0%, 100% { border-color: rgba(15, 42, 48, 0.32); }
  50%      { border-color: rgba(15, 42, 48, 0.60); }
}
```

### 5.9 Structure du runtime JS

```
src/ScadaBuilderV2.Rendering/Runtime/
├─ scada-runtime.js              (point d'entrée, namespace, lifecycle)
├─ expression-evaluator.js       (AST walker)
├─ state-engine.js               (boucle first-match-wins)
├─ effect-applier.js             (application propriétés CSS)
├─ animation-controller.js       (classes CSS d'animation)
├─ command-dispatcher.js         (triggers + write/navigate/popup/url/back)
├─ tag-bridge.js                 (getTagValue / writeTag / setTagValue)
├─ input-edit-guard.js           (protection édition + backshadow)
└─ confirmation-modal.js         (modale de confirmation)
```

Pendant l'export, ces fichiers sont concaténés + minifiés → `scada-runtime.<hash>.js`.

## 6. Modifications exporteur Builder V2

### 6.1 Manifest : StateConfig / CommandConfig dans Objects[]

Dans `Ft100SceneExporter.BuildManifestPage`, après `ValueBindings`, ajouter :

```json
{
  "Objects": [{
    "Id": "pump_12",
    "StateConfig": {
      "qualityFallback": { "opacity": 0.4, "borderColor": "#000000", "borderWidth": 2 },
      "defaultEffect": {},
      "states": [{
        "id": "s1", "name": "Alarme haute", "enabled": true,
        "expression": {
          "source": "{Temp} > 80",
          "ast": { "type": "binary", "op": "GreaterThan",
            "left": { "type": "tagRef", "tagName": "Temp" },
            "right": { "type": "literalNumber", "value": 80 }
          }
        },
        "effect": { "backgroundColor": "#E53935", "animation": "Blink" }
      }]
    },
    "CommandConfig": {
      "commands": [{
        "id": "cmd1", "name": "Démarrer pompe", "enabled": true,
        "trigger": "OnClick", "kind": "WriteTag",
        "writeTagId": "tf100.mapping.42", "writeMode": "Toggle",
        "confirmation": { "message": "Démarrer la pompe ?" }
      }]
    }
  }]
}
```

Ajouter `[JsonDerivedType]` sur `ScadaExprNode` pour la sérialisation polymorphique
des nœuds AST :

```csharp
[JsonDerivedType(typeof(ScadaExprLiteralNumber), "literalNumber")]
[JsonDerivedType(typeof(ScadaExprLiteralBool), "literalBool")]
[JsonDerivedType(typeof(ScadaExprLiteralString), "literalString")]
[JsonDerivedType(typeof(ScadaExprTagRef), "tagRef")]
[JsonDerivedType(typeof(ScadaExprUnary), "unary")]
[JsonDerivedType(typeof(ScadaExprBinary), "binary")]
[JsonDerivedType(typeof(ScadaExprFunc), "func")]
public abstract record ScadaExprNode;
```

### 6.2 HTML : data attributes sur wrappers Element+

Dans `BuildElementHtml`, ajouter une méthode `BuildStateCommandAttributes` qui émet
deux attributs HTML optionnels :

- `data-scada-state-config="<json>"` — si l'élément a des states ou un fallback non-défaut
- `data-scada-command-config="<json>"` — si l'élément a au moins une commande

Le JSON est encodé pour HTML (HtmlEncoder). Le runtime JS lit ces attributs avec
`JSON.parse()`.

### 6.3 CSS : keyframes d'animation page-scopées

Dans `BuildCss`, ajouter les keyframes partagées une seule fois par page CSS :

```css
@keyframes ft100-<page>---scada-blink { 0%,100%{opacity:1} 50%{opacity:0.15} }
@keyframes ft100-<page>---scada-pulse { 0%,100%{transform:scale(1)} 50%{transform:scale(1.05)} }
@keyframes ft100-<page>---scada-halo  { 0%,100%{box-shadow:0 0 2px currentColor} 50%{box-shadow:0 0 14px currentColor} }
@keyframes ft100-<page>---scada-spin  { 0%{transform:rotate(0deg)} 100%{transform:rotate(360deg)} }

.scada-anim-blink { animation: ft100-<page>---scada-blink 0.6s step-end infinite; }
.scada-anim-pulse { animation: ft100-<page>---scada-pulse 1s ease-in-out infinite; }
.scada-anim-halo  { animation: ft100-<page>---scada-halo 1.8s ease-in-out infinite; }
.scada-anim-spin  { animation: ft100-<page>---scada-spin 1.2s linear infinite; }
```

### 6.4 Script runtime : externe au HTML

Le `BuildRuntimeScript` actuel (~2200 lignes inline C#) est retiré de la génération
HTML. À la place, chaque page HTML référence le runtime partagé :

```html
<script src="../scada-runtime.a1b2c3d.js" defer></script>
```

Le Builder exporte `scada-runtime.<hash>.js` à la racine du package `.sb2`.

### 6.5 Hash de contenu pour les assets

```csharp
private static string ContentHash(string filePath)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    var bytes = File.ReadAllBytes(filePath);
    var hash = sha.ComputeHash(bytes);
    return Convert.ToHexString(hash)[..8].ToLowerInvariant();
}
```

Appliqué lors de l'export à : CSS, images, runtime JS. Le hash est incrusté dans le
nom de fichier → cache-busting automatique.

### 6.6 Fichiers modifiés (Builder V2)

| Fichier | Action | Description |
|---------|--------|-------------|
| `ElementEvents/Expressions/ScadaExprNode.cs` | Modifier | `[JsonDerivedType]` polymorphisme AST |
| `Rendering/Ft100SceneExporter.cs` | Modifier | `BuildManifestPage` +StateConfig/+CommandConfig |
| | Modifier | `BuildElementHtml` +BuildStateCommandAttributes |
| | Modifier | `BuildHtml` `<script src>` au lieu de inline |
| | Modifier | `BuildCss` +keyframes animation |
| | Ajouter | `ExportSharedRuntime` — écrit `scada-runtime.<hash>.js` |
| | Ajouter | `ContentHash` — renommage avec hash |
| | Modifier | `CopyAndRewriteImageAssets` — renommage avec hash |
| `Rendering/Ft100PackageValidation.cs` | Modifier | Valider présence `scada-runtime.*.js` |
| `Rendering/Runtime/scada-runtime.js` | **Créer** | Runtime JS — point d'entrée |
| `Rendering/Runtime/expression-evaluator.js` | **Créer** | AST walker |
| `Rendering/Runtime/state-engine.js` | **Créer** | Boucle first-match-wins |
| `Rendering/Runtime/effect-applier.js` | **Créer** | Application propriétés CSS |
| `Rendering/Runtime/animation-controller.js` | **Créer** | Classes CSS d'animation |
| `Rendering/Runtime/command-dispatcher.js` | **Créer** | Déclencheurs + exécution |
| `Rendering/Runtime/tag-bridge.js` | **Créer** | Interface getTagValue/writeTag |
| `Rendering/Runtime/input-edit-guard.js` | **Créer** | Protection édition input + backshadow |
| `Rendering/Runtime/confirmation-modal.js` | **Créer** | Modale de confirmation commande |

## 7. Modifications TF100Web

### 7.1 Management command

```bash
python manage.py deploy_scada_builder /tmp/export-2026-07-07.sb2
```

```python
# core/management/commands/deploy_scada_builder.py

class Command(BaseCommand):
    help = "Déploie un package SCADA Builder V2 .sb2 dans templates/ et static/"

    def add_arguments(self, parser):
        parser.add_argument('package_path', type=str)

    def handle(self, *args, **options):
        sb2_path = Path(options['package_path'])

        with tempfile.TemporaryDirectory() as staging:
            shutil.unpack_archive(sb2_path, staging, 'zip')
            pkg_dir = Path(staging) / 'scada-builder-v2-ft100-package'

            # 1. Copier les .html → templates/frontend/scada/pages/
            for html in pkg_dir.glob('*/*.html'):
                dest = settings.TEMPLATES_DIR / 'frontend' / 'scada' / 'pages' / html.parent.name
                dest.mkdir(parents=True, exist_ok=True)
                shutil.copy2(html, dest / html.name)

            # 2. Copier scada-runtime.*.js → static/scada/js/
            for js in pkg_dir.glob('scada-runtime.*.js'):
                dest = settings.STATIC_ROOT / 'scada' / 'js'
                dest.mkdir(parents=True, exist_ok=True)
                shutil.copy2(js, dest / js.name)

            # 3. Copier CSS → static/scada/css/
            for css in pkg_dir.glob('*/css/*.css'):
                dest = settings.STATIC_ROOT / 'scada' / 'css'
                dest.mkdir(parents=True, exist_ok=True)
                shutil.copy2(css, dest / css.name)

            # 4. Copier images → static/scada/images/
            for img in pkg_dir.glob('*/images/*'):
                if img.is_file():
                    dest = settings.STATIC_ROOT / 'scada' / 'images'
                    dest.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(img, dest / img.name)

        # 5. collectstatic
        call_command('collectstatic', '--noinput', verbosity=0)

        self.stdout.write(self.style.SUCCESS(
            'Package SCADA déployé. Redémarre Gunicorn pour recharger les templates.'
        ))
```

### 7.2 Vues Django

```python
# frontend/views.py

@login_required
def scada_page_view(request, page_id: str):
    """Charge une page SCADA Builder complète (HTML standalone)."""
    if not settings.TF100_INDUSTRIAL_DEPLOYMENT:
        raise Http404
    config = StationConfig.objects.filter(pk=1).first()
    if config is None or config.station_type != StationConfig.StationTypeChoices.SCADA_BUILDER_2:
        raise Http404

    template_path = f'frontend/scada/pages/{page_id}/{page_id}.html'
    try:
        get_template(template_path)
    except TemplateDoesNotExist:
        raise Http404

    return render(request, template_path, {
        'page_id': page_id,
        'is_scada_page': True,
    })


@login_required
def scada_page_json(request, page_id: str):
    """Retourne le HTML complet de la page pour navigation AJAX."""
    if not settings.TF100_INDUSTRIAL_DEPLOYMENT:
        raise Http404

    template_path = f'frontend/scada/pages/{page_id}/{page_id}.html'
    try:
        template = get_template(template_path)
        html = template.render({}, request)
    except TemplateDoesNotExist:
        raise Http404

    fragment = _extract_html_element_by_id(html, f'ft100-{page_id}')

    return JsonResponse({
        'page_id': page_id,
        'fragment': fragment or '',
        'css_hash': _extract_css_hash(html),
        'width': _page_dimension(html, 'width'),
        'height': _page_dimension(html, 'height'),
    })
```

### 7.3 URLs

```python
# frontend/urls.py
path('scada/page/<str:page_id>/', scada_page_view, name='scada_page'),
path('scada/api/page/<str:page_id>/', scada_page_json, name='scada_page_json'),
```

### 7.4 Template hôte : visualisation.html

```django
{% if station.station_type == 'SCADA_BUILDER_2' and is_industrial_deployment %}
  <div id="scada-host"
       data-scada-home-page="{{ home_page_id }}"
       data-scada-runtime-src="{% static runtime_js_path %}"
       data-scada-poll-interval="500">
  </div>
  <script src="{% static runtime_js_path %}" defer></script>
{% endif %}
```

### 7.5 Fichiers modifiés (TF100Web)

| Fichier | Action | Description |
|---------|--------|-------------|
| `core/management/commands/deploy_scada_builder.py` | **Créer** | Commande déploiement .sb2 |
| `frontend/views.py` | Ajouter | `scada_page_view` + `scada_page_json` |
| `frontend/urls.py` | Ajouter | 2 nouvelles routes SCADA |
| `templates/frontend/station/visualisation.html` | Modifier | Mode SCADA_BUILDER_2 → host + runtime |
| `static/.../visualisation_import.js` | Modifier | ScadaHost, tag bridge, polling, edit lock, popups |

### 7.6 Code déprécié (à supprimer après validation)

| Fichier | Contenu à supprimer |
|---------|---------------------|
| `frontend/scada_package.py` | Tout (fragment extraction serveur, validation, asset rewrite) |
| `frontend/scada_projects.py` | Tout (upload admin, gestion projets) |
| `frontend/scada_tags.py` | À conserver (export tags vers Builder) |
| `frontend/views.py` | `_manifest_scada_bindings`, `_inject_scada_element_attrs`, `_configured_scada_bindings_for_page`, `_inject_scada_manual_action_attrs`, `_merge_scada_actions`, `_load_scada_scene`, `scada_package_status`, `ScadaBuilderAdminView`, `ScadaBuilderTagExportView`, `IndustrialScadaBuilderOnlyMixin` |
| `frontend/models.py` | `StationElementBinding`, `StationPumpBinding`, `StationFloatBinding` (si non utilisés ailleurs) |
| `frontend/urls.py` | Routes `/scada-builder/...` |
| `templates/frontend/scada_builder.html` | Tout |

**Règle :** ne rien supprimer tant que le nouveau flux n'est pas validé sur unité TF100
ARM physique. Les fichiers morts sont marqués `# DEPRECATED: removals scheduled for
next deployment cycle` en attendant.

## 8. visualisation_import.js : hôte runtime TF100Web

Le `visualisation_import.js` est le chef d'orchestre côté navigateur. Il charge les
pages, injecte le runtime une seule fois, et fournit le bridge vers le backend.

### 8.1 Cycle de vie

```
Ouverture station SCADA_BUILDER_2
  │
  ├─ 1. Charger le runtime scada-runtime.<hash>.js (1 seule fois)
  │     └─ window.ScadaRuntime exposé (StateEngine, CommandDispatcher, ...)
  │
  ├─ 2. Charger la page d'accueil (home page)
  │     ├─ fetch /scada/api/page/<home_id>/
  │     ├─ Injecter le fragment HTML dans #scada-host
  │     ├─ Injecter le <link> CSS si pas déjà présent (vérifié par hash)
  │     └─ window.ScadaRuntime.initPage(fragment)
  │
  ├─ 3. Démarrer le polling tags (500ms)
  │     └─ fetch /visualisation/mapping/snapshot/ → push vers ScadaTagCache
  │
  └─ 4. Écouter les événements de navigation
        └─ postMessage / custom events → changer de page
```

### 8.2 API exposée au runtime

```javascript
// window.tf100webScadaBuilder — bridge appelé par le runtime Builder

getTagValue(tagId)     → valeur actuelle depuis ScadaTagCache.values[mappingId]
writeTag(tagId, value) → POST /visualisation/mapping/write/
                          Bloqué si un input est en édition
```

### 8.3 Navigation entre pages

```javascript
// Navigation déclenchée par le runtime (commande Navigate)
window.addEventListener('scada-navigate', (e) => {
  const { pageId } = e.detail;
  history.pushState({ pageId }, '', `/scada/page/${pageId}/`);
  ScadaHost.loadPage(pageId);
});

// Support bouton back du navigateur
window.addEventListener('popstate', (e) => {
  if (e.state?.pageId) ScadaHost.loadPage(e.state.pageId);
});
```

### 8.4 Popups

```javascript
// Les commandes OpenPopup/TogglePopup/ClosePopup émettent postMessage
// ScadaHost.handlePopupMessage crée/supprime des iframes avec srcdoc
// → pas de requête réseau supplémentaire pour le contenu du popup

ScadaHost.createPopup(pageId, options) {
  fetch(`/scada/api/page/${pageId}/`)
    .then(r => r.json())
    .then(data => {
      const iframe = document.createElement('iframe');
      iframe.srcdoc = data.fragment;  // inline, pas de seconde requête
      // ... overlay + panel + close button
      document.getElementById('scada-host').appendChild(overlay);
    });
}
```

## 9. Plan de migration (3 étapes)

```
Étape 1 — Builder: exporter le runtime state/command
  ├─ Sérialisation StateConfig/CommandConfig dans le manifest
  ├─ Data attributes HTML
  ├─ Runtime JS autonome (fichiers .js dans Rendering/Runtime/)
  ├─ Content-hash pour CSS, images, runtime
  └─ Déployé → nouveau .sb2 avec runtime state/command complet

Étape 2 — TF100Web: nouveau flux de déploiement
  ├─ Management command deploy_scada_builder
  ├─ Vues scada_page_view + scada_page_json
  ├─ visualisation_import.js: ScadaHost + runtime injection + popups
  ├─ Dépréciation du code legacy (marqué, pas supprimé)
  └─ Validé sur unité TF100 ARM physique

Étape 3 — TF100Web: nettoyage post-validation
  ├─ Suppression code mort (scada_package.py, scada_projects.py, ...)
  ├─ Migration du package importé existant vers le nouveau format
  └─ Doc: mise à jour INFORMATION_TECHNIQUE / SCADA_BUILDER_SB2_RUNTIME.md
```

## 10. Checklist de validation

- [ ] Export `.sb2` contient `scada-runtime.<hash>.js` à la racine
- [ ] Manifest contient `StateConfig` et `CommandConfig` pour les objets concernés
- [ ] Ouverture d'un `.html` exporté dans un navigateur → script exécuté, page interactive
- [ ] Ouverture via TF100Web → page servie, script exécuté
- [ ] Navigation AJAX entre 2 pages → CSS chargé 1 fois, images en cache
- [ ] État d'un Element+ change quand la valeur du tag change (first-match-wins)
- [ ] QualityFallback quand tous les tags sont null
- [ ] DefaultEffect (repos) quand aucun état ne match
- [ ] Effet Blink/Pulse/Halo/Spin visible
- [ ] Commande WriteTag + Toggle fonctionnelle
- [ ] Commande WriteTag + Momentary (OnValue pressé, OffValue relâché)
- [ ] Commande Navigate change de page
- [ ] Confirmation modale avant exécution commande
- [ ] Input numérique : focus → backshadow visible, polling en pause
- [ ] Input numérique : 30s sans action → polling reprend, input rafraîchi
- [ ] Input numérique : Entrée → valeur écrite, polling reprend
- [ ] Popup ouvre/ferme/toggle avec iframe srcdoc
- [ ] Bouton Back navigateur → retour à la page précédente
- [ ] Performance: page chargée en < 1s, polling 500ms stable
- [ ] `deploy_scada_builder` fonctionnel sur ARM Debian

## 11. Hors scope

- Simulateur live dans le Builder (itération future)
- Évaluateur runtime C# côté Builder (l'éditeur utilise le bridge WebView2, pas le runtime JS)
- Stale-timeout de qualité (extension future, au-delà de null)
- Extensions de grammaire : fonctions supplémentaires
- Scripts custom/personnalisés par page
- Migration automatique des anciens packages importés

## 12. Références

- Design source state/command: `docs/superpowers/specs/2026-07-07-element-plus-state-command-events-design.md`
- Contrat runtime state/command: `docs/03_runtime_contracts/STATE_COMMAND_RUNTIME_CONTRACT_V1.md`
- Contrat package FT100/TF100Web: `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`
- Implémentation plan state/command: `docs/superpowers/plans/2026-07-07-element-plus-state-command-events.md`
- Known gaps: `docs/08_implementation_status/KNOWN_GAPS_V2.md`
