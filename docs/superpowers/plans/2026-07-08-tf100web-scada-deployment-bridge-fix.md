# TF100Web — Correction du pont déploiement admin → STATIC_ROOT

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Raccorder le flux d'import admin SCADA Builder (`ScadaBuilderAdminView` → `import_project_from_zip`) au déploiement `STATIC_ROOT/scada/` attendu par `visualisation.html` et `visualisation_import.js`, corriger les URLs hardcodées sans préfixe `/app`, mettre à jour le test obsolète, et corriger une dépréciation erronée qui menace de supprimer le chemin d'upload admin lui-même. Cible : un `.sb2` déployable **soit** en CLI (`manage.py deploy_scada_builder`) **soit** via `/scada-builder/` en admin, avec un rendu 100% fonctionnel dans les deux cas — c'est la seule voie de déploiement disponible sur un poste d'usine en test sans accès shell.

**Architecture:** Extraction de la logique de déploiement de `deploy_scada_builder.py` en une fonction réutilisable `deploy_package_to_static()`, appelée depuis la vue admin après un import réussi. Le template `visualisation.html` expose le `STATIC_URL` et le préfixe d'URL Django via des attributs `data-*` sur `#scada-host`, lus par `visualisation_import.js`. Aucun changement architecture côté Builder V2.

**Contexte découvert (2026-07-08, branche `feature/element-plus-state-command-events` — branche active, contient tout le travail SCADA récent) :** le commit `51e7153` (« chore: deprecate legacy SCADA intake code ») a marqué `# DEPRECATED` en bloc sur :
1. Le chemin d'upload admin lui-même, **par erreur** : le module entier `frontend/scada_projects.py` (contient `import_project_from_zip`, `repository_root` — utilisés par `ScadaBuilderAdminView`), le module entier `frontend/scada_package.py` (contient `SCADA_PACKAGE_DIR_NAME`, `validate_scada_builder_package` — importés par `deploy_scada_builder.py`, le remplaçant supposé), et les classes `IndustrialScadaBuilderOnlyMixin`, `ScadaBuilderAdminView`, `ScadaBuilderTagExportView`.
2. La chaîne de rendu legacy (`_load_scada_scene`, `_inject_scada_element_attrs`, `_manifest_scada_bindings`, `_configured_scada_bindings_for_page`, `_merge_scada_actions`, `_build_manual_scada_event`, `_inject_scada_manual_action_attrs`, `scada_package_status`) en supposant qu'elle est toujours inatteignable — **prémisse déjà invalidée** par le commit `eefed1d` (« test: drop ScadaSceneDeadCodeTests — audit premise was incomplete ») : `scada_scene` est lu dans `visualisation.html` en dehors de la seule branche conditionnelle qui semblait la rendre toujours `None`, et `_load_scada_scene()` peut légitimement retourner une valeur non-`None` pour une station ayant encore des données SCADA legacy. Le commentaire dit « removals scheduled for next deployment cycle » : si ce nettoyage a lieu tel quel (sur l'un ou l'autre groupe), il supprime du code encore atteignable. Task 0 retire tous les marqueurs `# DEPRECATED` posés par `51e7153` (les deux groupes) plutôt que de re-trancher soi-même quel sous-ensemble serait « vraiment » mort — cette question nécessite une vérification runtime (traçage de requêtes réelles), pas une relecture de template, et sort du périmètre de ce plan.

**Tech Stack:** Python 3 / Django (TF100Web), vanilla JS (visualisation_import.js), Django test framework.

## Global Constraints

- **Repo :** `F:\Projet\Git\TF100Web` uniquement. Aucun fichier Builder V2 touché.
- Ne pas modifier `scada_projects.py` (marqué DEPRECATED).
- Ne pas toucher aux fichiers sous `docs/09_archive/`.
- `FORCE_SCRIPT_NAME = "/app"`, `STATIC_URL = "/app/static/"` en production.
- KISS : pas de nouveau module, pas de nouvelle dépendance JS.
- Commit après chaque tâche.

---

## File Structure

- **Modify:** `frontend/scada_projects.py` — retirer le marqueur `# DEPRECATED` (module toujours consommé par `ScadaBuilderAdminView`)
- **Modify:** `frontend/scada_package.py` — retirer le marqueur `# DEPRECATED` (module toujours consommé par `deploy_scada_builder.py` et `scada_projects.py`)
- **Modify:** `frontend/views.py:721-797` — retirer `# DEPRECATED` sur `IndustrialScadaBuilderOnlyMixin`, `ScadaBuilderAdminView`, `ScadaBuilderTagExportView` ; garder le marqueur sur `scada_package_status` (vraiment mort, non touché)
- **Modify:** `core/management/commands/deploy_scada_builder.py` — extraire `deploy_package_to_static()` comme fonction module-level
- **Modify:** `frontend/views.py:760-788` — appeler `deploy_package_to_static()` après `import_project_from_zip()`
- **Modify:** `templates/frontend/station/visualisation.html:161-169` — ajouter `data-scada-page-url-template` et `data-scada-static-base` sur `#scada-host`
- **Modify:** `static/asset/js/station/visualisation_import.js:54,63,146,160,289` — remplacer les URLs hardcodées par les data attributes
- **Modify:** `frontend/tests_scada_package.py:778-779` — corriger le chemin attendu du test `test_deploys_html_js_css_images`

---

## Task 0: Corriger la dépréciation erronée du chemin d'upload admin

**Files:**
- Modify: `frontend/scada_projects.py`
- Modify: `frontend/scada_package.py`
- Modify: `frontend/views.py` (tous les marqueurs `# DEPRECATED: removals scheduled for next deployment cycle.` posés par le commit `51e7153` : `_inject_scada_element_attrs`, `_manifest_scada_bindings`, `_configured_scada_bindings_for_page`, `_merge_scada_actions`, `_inject_scada_manual_action_attrs`, `_load_scada_scene`, `scada_package_status`, `IndustrialScadaBuilderOnlyMixin`, `ScadaBuilderAdminView`, `ScadaBuilderTagExportView`)

**Interfaces:** aucun changement de comportement — uniquement des commentaires/docstrings. Aucune fonction n'est supprimée : l'atteignabilité réelle de la chaîne `_load_scada_scene` n'est pas tranchée ici (cf. Contexte découvert) et une future décision de suppression doit passer par une vérification runtime dédiée, pas par ce plan.

- [ ] **Step 1: Retirer le marqueur du module `scada_projects.py`**

Dans `frontend/scada_projects.py`, supprimer les 2 lignes en tête de fichier :
```python
# DEPRECATED: removals scheduled for next deployment cycle.
# Replaced by deploy_scada_builder management command.
```
Remplacer par :
```python
# Backbone of the admin upload flow (ScadaBuilderAdminView, frontend_scada_builder route).
# Complementary to deploy_scada_builder (CLI) — not replaced by it. Do not delete.
```

- [ ] **Step 2: Retirer le marqueur du module `scada_package.py`**

Dans `frontend/scada_package.py`, supprimer les 4 lignes en tête de fichier :
```python
# DEPRECATED: removals scheduled for next deployment cycle.
# Replaced by deploy_scada_builder management command and direct static file serving.
# See SCADA_BUILDER_SB2_RUNTIME.md — new deployment flow.
# This module is retained only for backward compatibility during validation.
```
Remplacer par :
```python
# SCADA_PACKAGE_DIR_NAME and validate_scada_builder_package are imported by
# deploy_scada_builder.py (CLI) and scada_projects.py (admin upload) — both live
# deployment paths depend on this module. Do not delete.
```

- [ ] **Step 3: Retirer tous les marqueurs `# DEPRECATED` posés par `51e7153` dans `views.py`**

Supprimer la ligne `# DEPRECATED: removals scheduled for next deployment cycle.` juste avant chacune de ces 10 fonctions/classes (repérer avec `grep -n "# DEPRECATED" frontend/views.py`) :
`_inject_scada_element_attrs`, `_manifest_scada_bindings`, `_configured_scada_bindings_for_page`, `_merge_scada_actions`, `_inject_scada_manual_action_attrs`, `_load_scada_scene`, `scada_package_status`, `IndustrialScadaBuilderOnlyMixin`, `ScadaBuilderAdminView`, `ScadaBuilderTagExportView`.

Remplacer par un commentaire court et honnête sur l'état réel, par ex. au-dessus de `ScadaBuilderAdminView` :
```python
# Live admin upload path — the only deployment route on factory stations without
# shell access. Complementary to `manage.py deploy_scada_builder` (CLI), not replaced
# by it. Both must call deploy_package_to_static() (see Task 1).
```

et au-dessus de `_load_scada_scene` :
```python
# Legacy per-request scene rendering (scada_projects import root, not STATIC_ROOT).
# Reachability vs the new scada-host/AJAX flow (STATIC_ROOT-based) is NOT settled —
# commit 51e7153 marked this dead based on a premise later disproved (eefed1d).
# Do not remove without a dedicated runtime-reachability investigation.
```
(un seul de ces deux commentaires par fonction/classe suffit selon le cas — l'important est de ne plus affirmer une suppression programmée qui n'est pas décidée.)

- [ ] **Step 4: Vérifier qu'aucun marqueur `# DEPRECATED` de `51e7153` ne subsiste**

Run: `cd "F:\Projet\Git\TF100Web" && grep -rn "removals scheduled for next deployment cycle" frontend/ core/`
Expected: aucun résultat.

- [ ] **Step 5: Commit**

```bash
cd "F:\Projet\Git\TF100Web"
git add frontend/scada_projects.py frontend/scada_package.py frontend/views.py
git commit -m "fix: revert unsettled deprecation markers from the SCADA intake cleanup

Commit 51e7153 marked two groups of code DEPRECATED with 'removals scheduled
for next deployment cycle': (1) the live admin upload path
(scada_projects.py, scada_package.py, ScadaBuilderAdminView,
IndustrialScadaBuilderOnlyMixin, ScadaBuilderTagExportView) — wrong, these
are still imported by deploy_scada_builder.py and are the only deployment
route on factory stations without shell access; (2) the legacy scene
rendering chain (_load_scada_scene and its helpers, scada_package_status) —
based on a reachability premise already disproved by eefed1d. Neither group
is deleted here; reachability of group 2 needs a dedicated runtime
investigation, not a template re-read.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 1: Extraire `deploy_package_to_static()` et l'appeler depuis l'import admin

**Files:**
- Modify: `core/management/commands/deploy_scada_builder.py`
- Modify: `frontend/views.py:760-788`

**Interfaces:**
- Produces: `deploy_package_to_static(package_dir: Path, static_root: Path, stderr=None) -> dict` — déploie un package extrait vers `STATIC_ROOT/scada/`, retourne `{"html": int, "runtime_js": int, "css": int, "images": int, "manifest": int}`
- Consumes: `SCADA_PACKAGE_DIR_NAME` (from `frontend.scada_package`), `STATIC_ROOT` (from `django.conf.settings`)
- Modified: `ScadaBuilderAdminView.post()` calls `deploy_package_to_static()` after successful `import_project_from_zip()`

- [ ] **Step 1: Extraire la fonction `deploy_package_to_static`**

Dans `core/management/commands/deploy_scada_builder.py`, déplacer la logique de déploiement (lignes 63-115) dans une fonction module-level :

```python
import re
import shutil
from pathlib import Path
from tempfile import TemporaryDirectory

from django.conf import settings
from django.core.management import BaseCommand, call_command

from frontend.scada_package import SCADA_PACKAGE_DIR_NAME


def deploy_package_to_static(package_dir, static_root, _stderr=None):
    """Deploy an extracted SCADA Builder package to STATIC_ROOT/scada/.

    Args:
        package_dir: Path to the extracted package directory
                      (contains manifest.json, scada-runtime.*.js, <page>/*.html, etc.)
        static_root: STATIC_ROOT directory

    Returns:
        dict with counts: html, runtime_js, css, images, manifest
    """
    package_dir = Path(package_dir)
    static_root = Path(static_root)

    html_count = 0
    runtime_js_count = 0
    css_count = 0
    image_count = 0
    manifest_count = 0

    # Wipe previous deployment
    scada_root = static_root / "scada"
    if scada_root.exists():
        shutil.rmtree(scada_root)

    for file_path in package_dir.rglob("*"):
        if not file_path.is_file():
            continue

        relative = file_path.relative_to(package_dir)
        parts = relative.parts
        rel_name = relative.name

        # 1. */css/*.css  -->  static/scada/css/<name>.css
        if relative.suffix == ".css" and len(parts) >= 3 and parts[-2] == "css":
            dest = static_root / "scada" / "css" / rel_name
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(str(file_path), str(dest))
            css_count += 1
            continue

        # 2. scada-runtime.*.js  -->  static/scada/js/scada-runtime.js AND static/scada/js/<hash>.js
        if re.match(r"^scada-runtime\..*\.js$", rel_name):
            dest_hashed = static_root / "scada" / "js" / rel_name
            dest_hashed.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(str(file_path), str(dest_hashed))
            dest_stable = static_root / "scada" / "js" / "scada-runtime.js"
            shutil.copy2(str(file_path), str(dest_stable))
            runtime_js_count += 1
            continue

        # 3. */images/*  -->  static/scada/images/<name>
        if len(parts) >= 3 and parts[-2] == "images":
            dest = static_root / "scada" / "images" / rel_name
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(str(file_path), str(dest))
            image_count += 1
            continue

        # 4. */*.html  -->  static/scada/pages/<page_dir>/<name>.html
        if relative.suffix == ".html" and len(parts) >= 2:
            page_dir = parts[0]
            dest = static_root / "scada" / "pages" / page_dir / rel_name
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(str(file_path), str(dest))
            html_count += 1
            continue

        # 5. manifest.json (package root)  -->  static/scada/manifest.json
        if rel_name == "manifest.json" and len(parts) == 1:
            dest = static_root / "scada" / "manifest.json"
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(str(file_path), str(dest))
            manifest_count += 1
            continue

    call_command("collectstatic", "--noinput", verbosity=0)

    return {
        "html": html_count,
        "runtime_js": runtime_js_count,
        "css": css_count,
        "images": image_count,
        "manifest": manifest_count,
    }


class Command(BaseCommand):
    help = "Deploy a SCADA Builder .sb2 package: extract, copy to static/, run collectstatic."

    def add_arguments(self, parser):
        parser.add_argument("package_path", help="Path to the .sb2 package file")

    def handle(self, *args, **options):
        package_path = Path(options["package_path"])

        if not package_path.is_file():
            self.stderr.write(self.style.ERROR(f"File not found: {package_path}"))
            return

        static_root = Path(settings.STATIC_ROOT)

        with TemporaryDirectory() as staging_dir:
            staging = Path(staging_dir)

            try:
                shutil.unpack_archive(str(package_path), str(staging), "zip")
            except Exception as exc:
                self.stderr.write(self.style.ERROR(f"Failed to extract package: {exc}"))
                return

            package_dir = staging / SCADA_PACKAGE_DIR_NAME
            if not package_dir.is_dir():
                self.stderr.write(
                    self.style.ERROR(
                        f"Package directory '{SCADA_PACKAGE_DIR_NAME}' not found in the archive"
                    )
                )
                return

            counts = deploy_package_to_static(package_dir, static_root)

        self.stdout.write(f"Deployed {counts['html']} page HTML file(s)")
        self.stdout.write(f"Deployed {counts['runtime_js']} runtime JS file(s)")
        self.stdout.write(f"Deployed {counts['css']} CSS file(s)")
        self.stdout.write(f"Deployed {counts['images']} image(s)")
        self.stdout.write(f"Deployed {counts['manifest']} manifest.json file(s)")
        self.stdout.write(self.style.SUCCESS(
            "SCADA Builder package deployed. Pages served from static/ — no restart required."
        ))
```

- [ ] **Step 2: Vérifier que la commande existante fonctionne encore**

Run: `cd "F:\Projet\Git\TF100Web" && .venv\Scripts\python manage.py test frontend.tests_scada_deploy.DeployScadaBuilderManifestTests -v 2`

Expected: PASS (la refactorisation est transparente pour les tests existants)

- [ ] **Step 3: Appeler `deploy_package_to_static` depuis `ScadaBuilderAdminView.post()`**

Dans `frontend/views.py`, ajouter l'import et l'appel après `import_project_from_zip()`:

```python
# En haut du fichier, ajouter l'import :
from core.management.commands.deploy_scada_builder import deploy_package_to_static

# Dans ScadaBuilderAdminView.post(), remplacer le bloc "if action == 'upload':"
# (lignes 763-773) par :

            if action == "upload":
                uploaded = request.FILES.get("package")
                if uploaded is None:
                    messages.error(request, "Aucun package ZIP fourni.")
                else:
                    project = import_project_from_zip(
                        uploaded,
                        name=request.POST.get("name", ""),
                        activate=request.POST.get("activate", "1") == "1",
                    )
                    # Deploy to STATIC_ROOT so visualisation.html can serve
                    # the runtime JS, pages, CSS, images, and manifest.
                    package_dir = project.path / SCADA_PACKAGE_DIR_NAME
                    counts = deploy_package_to_static(
                        package_dir,
                        Path(settings.STATIC_ROOT),
                    )
                    messages.success(
                        request,
                        f"Projet SCADA Builder charge et deploye : {project.name} "
                        f"({counts['html']} pages, {counts['runtime_js']} runtime JS, "
                        f"{counts['css']} CSS, {counts['images']} images)."
                    )
```

Ajouter les imports manquants en haut de `views.py`:
```python
from pathlib import Path
```

Note : `SCADA_PACKAGE_DIR_NAME` est déjà importé dans `views.py` (vérifier, sinon l'ajouter depuis `frontend.scada_package`).

- [ ] **Step 4: Vérifier que l'import SCADA_PACKAGE_DIR_NAME existe dans views.py**

Run: `cd "F:\Projet\Git\TF100Web" && grep -n "SCADA_PACKAGE_DIR_NAME" frontend/views.py`

Si absent, ajouter l'import :
```python
from .scada_package import SCADA_PACKAGE_DIR_NAME
```

- [ ] **Step 5: Commit**

```bash
cd "F:\Projet\Git\TF100Web"
git add core/management/commands/deploy_scada_builder.py frontend/views.py
git commit -m "fix: bridge admin SCADA import to STATIC_ROOT deployment

Extract deploy_package_to_static() from deploy_scada_builder so the admin
upload flow (ScadaBuilderAdminView) also deploys runtime JS, pages, CSS,
images, and manifest.json to STATIC_ROOT/scada/. Fixes 404 on
scada-runtime.js and manifest.json when importing via the admin UI.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: Corriger les URLs hardcodées dans `visualisation_import.js`

**Files:**
- Modify: `templates/frontend/station/visualisation.html:161-169`
- Modify: `static/asset/js/station/visualisation_import.js:54,63,146,160,289`

**Interfaces:**
- Produces: `data-scada-page-url-template` et `data-scada-css-base` sur `#scada-host`
- Consumes: `visualisation_import.js` lit ces attributs pour construire les URLs

- [ ] **Step 1: Ajouter les data attributes dans `visualisation.html`**

Remplacer le bloc `#scada-host` (lignes 162-168) :

```html
        <div id="scada-host" class="scada-host"
             data-scada-home-page="{{ home_page_id|default:'win00009' }}"
             data-scada-page-url-template="{% url 'frontend_scada_package_page' page_id='__PAGE_ID__' %}"
             data-scada-css-base="{% static 'scada/css/' %}"
             data-mapping-snapshot-url="{% url 'frontend_station_mapping_snapshot' %}"
             data-mapping-write-url="{% url 'frontend_station_mapping_write' %}"
             data-live-refresh-ms="500"
             data-can-write="1">
        </div>
```

Note : `{% url 'frontend_scada_package_page' page_id='__PAGE_ID__' %}` produit `/app/visualisation/scada/page/__PAGE_ID__/` en production et `/visualisation/scada/page/__PAGE_ID__/` en dev.

- [ ] **Step 2: Mettre à jour `visualisation_import.js` pour utiliser les data attributes**

Remplacer les URLs hardcodées dans `visualisation_import.js` :

**Ligne 54** — `ScadaHost.loadPage` :
```javascript
// AVANT :
const resp = await fetch(`/visualisation/scada/page/${encodeURIComponent(pageId)}/`);

// APRES :
const pageUrl = (scadaHostEl.dataset.scadaPageUrlTemplate || '/visualisation/scada/page/__PAGE_ID__/')
    .replace('__PAGE_ID__', encodeURIComponent(pageId));
const resp = await fetch(pageUrl);
```

**Ligne 63** — `ScadaHost.loadPage` CSS :
```javascript
// AVANT :
link.href = `/static/scada/css/${encodeURIComponent(pageId)}.${data.css_hash}.css`;

// APRES :
const cssBase = scadaHostEl.dataset.scadaCssBase || '/static/scada/css/';
link.href = `${cssBase}${encodeURIComponent(pageId)}.${data.css_hash}.css`;
```

**Ligne 146** — `ScadaHost._createPopup` :
```javascript
// AVANT :
fetch(`/visualisation/scada/page/${encodeURIComponent(pageId)}/`)

// APRES :
const pageUrl = (document.getElementById('scada-host')?.dataset.scadaPageUrlTemplate || '/visualisation/scada/page/__PAGE_ID__/')
    .replace('__PAGE_ID__', encodeURIComponent(pageId));
fetch(pageUrl)
```

**Ligne 160** — `ScadaHost._createPopup` CSS :
```javascript
// AVANT :
link.href = `/static/scada/css/${encodeURIComponent(pageId)}.${data.css_hash}.css`;

// APRES :
const cssBase = document.getElementById('scada-host')?.dataset.scadaCssBase || '/static/scada/css/';
link.href = `${cssBase}${encodeURIComponent(pageId)}.${data.css_hash}.css`;
```

**Ligne 289** — `window.addEventListener('message', ...)` navigate :
```javascript
// AVANT :
history.pushState({ pageId: msg.pageId }, '', `/visualisation/scada/page/${encodeURIComponent(msg.pageId)}/`);

// APRES :
const pageUrl = (document.getElementById('scada-host')?.dataset.scadaPageUrlTemplate || '/visualisation/scada/page/__PAGE_ID__/')
    .replace('__PAGE_ID__', encodeURIComponent(msg.pageId));
history.pushState({ pageId: msg.pageId }, '', pageUrl);
```

- [ ] **Step 2b (discovered during implementation, 2026-07-08): fix the top-of-file guard that blocked the entire new runtime**

`visualisation_import.js` opened with:
```javascript
const root = document.getElementById("diagramRoot");
if (!root || !root.classList.contains("scada-host")) return;
```
`#diagramRoot` only exists in the legacy `scada_scene` branch — the new `#scada-host` branch never renders it. This `return` therefore aborted the *entire* script (including `ScadaHost.init()`, the `postMessage` listener, `ScadaTagCache`) whenever the new branch was active, regardless of Task 1's STATIC_ROOT fix. Separately, zoom/fullscreen/scale-frame chrome (`#scadaViewport`/`#scadaScaleFrame`/`#scadaZoom*Btn`) is TF100Web-owned viewport tooling (not exported by SCADA Builder V2) and was only rendered in the legacy branch — per explicit user direction this must keep working for the new branch too, not be dropped.

Applied fix:
- `visualisation_import.js`: replaced the hard `return` with `isLegacyScadaDiagram`/`isNewScadaHost` flags and a `scaleTarget = root || scadaHostEl` reference; `nativeDimensions()`/`applyRuntimeDimensions()`/`setRuntimeCssVariable()` now operate on `scaleTarget` instead of `root`; `initFullscreenButton()`/`initZoomControls()` now run unconditionally; legacy-only wiring (`initBindings()`, `setActions()`, `window.TF100ScadaRuntime`) is gated behind `isLegacyScadaDiagram`; the `popstate` handler and the `postMessage` listener route to the correct loader (`ScadaHost.loadPage` vs legacy `loadPage`) based on which host is active; `ScadaHost.loadPage()` now calls `applyRuntimeDimensions(data.width_css, data.height_css, data.width, data.height)` after injecting a page, reusing the exact mechanism the legacy AJAX navigation already used (same `scada_package_page` JSON shape for both flows).
- `visualisation.html`: extracted the zoom/fullscreen toolbar markup into `templates/frontend/station/_scada_viewport_toolbar.html` (single source, included from both branches) and wrapped the new `#scada-host` div in the same `#scadaViewport`/`#scadaScaleFrame` shell the legacy branch uses.

- [ ] **Step 3: Commit**

```bash
cd "F:\Projet\Git\TF100Web"
git add templates/frontend/station/visualisation.html templates/frontend/station/_scada_viewport_toolbar.html static/asset/js/station/visualisation_import.js
git commit -m "fix: use data attributes for SCADA URLs, unblock the new runtime guard

Replace hardcoded /visualisation/scada/page/ and /static/scada/css/ URLs
with values from data-scada-page-url-template and data-scada-css-base on
#scada-host. Fixes broken URLs when APP_BASE_PATH is /app.

Also fix a top-of-file guard in visualisation_import.js that returned early
whenever #diagramRoot (legacy-only) was absent, which silently skipped
ScadaHost.init() and the postMessage listener for every SCADA_BUILDER_2
industrial render. Zoom/fullscreen/scale-frame chrome (TF100Web-owned, not
exported by SCADA Builder V2) now works for both the legacy scene and the
new #scada-host flow, sharing one toolbar partial
(_scada_viewport_toolbar.html) instead of duplicating the markup.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: Corriger le test obsolète `test_deploys_html_js_css_images`

**Files:**
- Modify: `frontend/tests_scada_package.py:778-801`

**Interfaces:**
- Consumes: `deploy_scada_builder` déploie maintenant les HTML dans `STATIC_ROOT/scada/pages/`

- [ ] **Step 1: Mettre à jour le chemin attendu dans le test**

Dans `frontend/tests_scada_package.py`, remplacer les assertions du test `test_deploys_html_js_css_images` (lignes 778-801) :

```python
# AVANT (ligne 778-780) :
                # HTML in templates
                html_dest = td / "templates" / "frontend" / "scada" / "pages" / "home" / "home.html"
                self.assertTrue(html_dest.is_file())
                self.assertIn("Home content", html_dest.read_text(encoding="utf-8"))

# APRES :
                # HTML in STATIC_ROOT/scada/pages/
                html_dest = td / "static_root" / "scada" / "pages" / "home" / "home.html"
                self.assertTrue(html_dest.is_file())
                self.assertIn("Home content", html_dest.read_text(encoding="utf-8"))
```

- [ ] **Step 2: Vérifier que le test passe**

Run: `cd "F:\Projet\Git\TF100Web" && .venv\Scripts\python manage.py test frontend.tests_scada_package.DeployScadaBuilderCommandTests.test_deploys_html_js_css_images -v 2`

Expected: PASS

- [ ] **Step 3: Lancer la suite complète des tests SCADA**

Run: `cd "F:\Projet\Git\TF100Web" && .venv\Scripts\python manage.py test frontend.tests_scada_package frontend.tests_scada_deploy -v 2`

Expected: all PASS

- [ ] **Step 4: Commit**

```bash
cd "F:\Projet\Git\TF100Web"
git add frontend/tests_scada_package.py
git commit -m "fix: update DeployScadaBuilderCommandTests for STATIC_ROOT HTML path

test_deploys_html_js_css_images now expects HTML in STATIC_ROOT/scada/pages/
instead of templates/frontend/scada/pages/, matching the current
deploy_scada_builder behavior.

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Task 4: Vérification bout-en-bout des deux chemins de déploiement

**Files:** none (verification only).

**But :** confirmer que CLI (`manage.py deploy_scada_builder`) et admin (`/scada-builder/`) produisent tous deux un `STATIC_ROOT/scada/` complet et un rendu 100% fonctionnel — condition posée par l'utilisateur, poste d'usine sans accès shell garanti à long terme.

**Prérequis à vérifier avant les tests (config/ops, aucun code ne peut les garantir) :**
- `StationConfig.station_type == SCADA_BUILDER_2` pour la station de test (sinon `visualisation.html` ne rend jamais la branche `#scada-host`, quel que soit l'état de `STATIC_ROOT`).
- `TF100_DEPLOYMENT_PROFILE=industrial` dans l'environnement du serveur (sinon `is_industrial_deployment()` est faux et la même branche ne se déclenche jamais).
- Le worker Django (gunicorn) a les droits d'écriture sur `/var/www/florizon/ft100_rtu/static/scada/` — Task 1 fait désormais écrire ce dossier depuis une requête web (admin upload), alors qu'avant seul un script CLI lancé manuellement (souvent avec des droits différents) y écrivait. Un `chmod`/`chown` peut être nécessaire sur le poste d'usine si le déploiement admin échoue avec une `PermissionError`.

- [ ] **Step 1: Déploiement via CLI**

Run: `cd "F:\Projet\Git\TF100Web" && python manage.py deploy_scada_builder <chemin_vers_export.sb2>`
Expected: `STATIC_ROOT/scada/{manifest.json, js/scada-runtime.js, css/, pages/, images/}` tous présents.

- [ ] **Step 2: Effacer STATIC_ROOT/scada, redéployer via l'admin**

1. Supprimer `STATIC_ROOT/scada/` (ou repartir d'un environnement où il est absent, comme le poste d'usine actuel).
2. Se connecter à `/scada-builder/`, uploader le même `.sb2`, action "upload".
3. Vérifier le message de succès affiche les compteurs (`counts['html']`, etc. — Task 1 Step 3).

Expected: `STATIC_ROOT/scada/` contient exactement la même arborescence qu'à l'Étape 1.

- [ ] **Step 3: Vérification navigateur**

Ouvrir `/visualisation/` : la page SCADA s'affiche (pas de 404 sur `scada-runtime.js` ni `manifest.json`), la home page correspond au `HomePageId` du manifest, et la navigation entre pages fonctionne (fetch AJAX, pas de rechargement complet — cf. Task 7 Step 4 du plan `2026-07-08-tf100web-scada-runtime-integration-fixes.md` pour la checklist complète si ce plan est exécuté en parallèle).

- [ ] **Step 4: Si un check échoue, retour à Systematic Debugging (Phase 1) plutôt qu'un patch ad hoc.**

---

## Self-Review

### 1. Spec coverage (user analysis points)

| Point | Couvert par |
|-------|-------------|
| Route admin dépréciée par erreur, upload jamais déployé | Task 0 — marqueurs DEPRECATED erronés/non tranchés retirés (chemin admin + chaîne legacy) |
| scada-runtime.js 404 car absent de STATIC_ROOT | Task 1 — `deploy_package_to_static()` appelé depuis l'admin |
| manifest.json absent de STATIC_ROOT → home_page_id retombe sur win00009 | Task 1 — manifest.json déployé dans STATIC_ROOT |
| Deux flux incohérents (admin import vs deploy_scada_builder) | Task 1 — pont créé, les deux flux passent par la même fonction |
| URLs absolues sans préfixe /app dans visualisation_import.js | Task 2 — data attributes lus par le JS |
| Test obsolète test_deploys_html_js_css_images | Task 3 — chemin corrigé |
| CLI et admin doivent tous deux produire un SCADA fonctionnel | Task 4 — vérification bout-en-bout des deux chemins |

### 2. Placeholder scan

Aucun placeholder TBD/TODO. Toutes les étapes ont le code exact.

### 3. Type consistency

- `deploy_package_to_static(package_dir: Path, static_root: Path, _stderr=None) -> dict` — cohérent entre Task 1 Step 1 (définition) et Task 1 Step 3 (appel)
- `data-scada-page-url-template` et `data-scada-css-base` — cohérents entre Task 2 Step 1 (template) et Task 2 Step 2 (JS)
