# TF100Web — Composition header/body/footer du flux SCADA statique Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restaurer la composition header + body + footer pour le flux SCADA `STATIC_ROOT`-based (`frontend.views.scada_package_page` → JS `ScadaHost`), avec persistance du header/footer à travers la navigation (seul le body est toujours remplacé ; header/footer ne sont remplacés que si la page cible en référence un différent).

**Architecture:** Nouveau module pur et testable `frontend/scada_builder_composition.py` (résolution manifest + chargement de fragments, réutilise `_compiled_pages`/`_page_id`/`_page_type` de `scada_package.py` par import en lecture seule) appelé par un `views.scada_package_page` amaigri. Le contrat JSON devient uniforme : toujours une liste `parts` (1 à 3 entrées `role: header|body|footer`). Côté JS, `ScadaHost` traque l'id de page actuellement affiché par rôle et ne redessine que ce qui a changé.

**Tech Stack:** Python 3 / Django (TF100Web), vanilla JS (`visualisation_import.js`), Django test framework (`unittest.TestCase` pour la logique pure, `django.test.TestCase` pour les tests bout-en-bout). Task 0 touche un second repo : Node.js built-in test runner (`node:test`, `node:vm`) pour les modules runtime JS de SCADA Builder V2.

**Design doc:** `docs/superpowers/specs/2026-07-08-tf100web-scada-header-footer-composition-design.md`

## Global Constraints

- **Deux repos distincts.** Task 0 s'exécute dans `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2` (ce repo). Tasks 1-8 s'exécutent dans `F:\Projet\Git\TF100Web`, branche `feature/element-plus-state-command-events`. Toutes les commandes `pytest`/`python manage.py test`/`git` des Tasks 1-8 s'exécutent depuis ce dernier, pas depuis `SCADA_AMR_GROUP`.
- Ne pas modifier `frontend/scada_package.py` ni `frontend/scada_projects.py` (chemin legacy, hors scope). Import en lecture seule de `_compiled_pages`, `_page_id`, `_page_type` uniquement.
- KISS : composition à la volée par requête, pas de cache de fragments composés.
- Seules les pages de type `"default"` composent un header/footer. Les pages `"fragment"` (popups, rendues en overlay par `ScadaHost._createPopup`) ne composent jamais, même si leur entrée manifest contient un `HeaderPageId`/`FooterPageId` par erreur d'authoring.
- `HeaderPageId`/`FooterPageId` n'est honoré que si la page référencée existe et a le type exact `"header"`/`"footer"` — sinon ignoré silencieusement, jamais d'exception ni de 500.
- Contrat JSON uniforme : `parts` est toujours une liste avec exactement une entrée `role: "body"`, et 0 ou 1 entrée `role: "header"` / `role: "footer"`. Pas de format alternatif pour le cas non composé.
- `_createPopup()` ne gagne aucune capacité de composition — son seul changement est la lecture de `parts[0]` (toujours l'unique part `body` pour une page fragment) au lieu du format racine `data.html`/`data.css_hash`.
- Dimensions composées : largeur = max, hauteur = somme, calculées depuis les attributs HTML `data-scada-width`/`data-scada-height` de chaque part présente (jamais depuis les champs manifest `Width`/`Height`).

---

## Task 0: Fix `StateEngine.initPage` pour ne réinitialiser que son propre conteneur (repo `SCADA_BUILDER_V2`)

**Files:**
- Modify: `src/ScadaBuilderV2.Rendering/Runtime/state-engine.js:265-288`
- Test: `tests/runtime-js/state-engine.test.mjs`

**Repo :** `F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2` — distinct du repo TF100Web des Tasks 1-8. Toutes les commandes de cette tâche s'exécutent depuis ce repo.

**Contexte (découvert pendant le brainstorming de ce plan, voir design doc) :** `StateEngine.initPage(container, pageId)` réinitialise aujourd'hui `_paused` et `_stateCache` **globalement**, sans les restreindre à `container`. `_paused` n'est pas cosmétique : `InputEditGuard.lock()` (`input-edit-guard.js:96-98`) appelle `StateEngine.pauseElement(elementId)` au focus d'un champ éditable, précisément pour empêcher le moteur d'état d'écraser une valeur en cours de saisie. La Task 7 de ce plan appelle `ScadaRuntime.initPage()` sur le slot `body` à chaque navigation (comme aujourd'hui). Avec la persistance du header/footer introduite par ce plan, un header/footer inchangé **survit** désormais à une navigation de body — mais l'appel `initPage` du body continuerait de purger `_paused` pour **toute la page**, y compris pour un champ en cours d'édition dans le header/footer persistant, alors que ce dernier reste visuellement verrouillé (overlay, timer de 30s) sans plus être réellement protégé côté moteur d'état. Ce bug latent existait déjà dans le runtime mais était sans conséquence tant que toute navigation détruisait l'intégralité du DOM (l'élément en cours d'édition disparaissait de toute façon) ; la persistance le rend observable. Cette tâche corrige la cause racine dans le runtime partagé.

**Note pour la Task 8 :** un `.sb2` déjà exporté avant ce fix embarque l'ancien runtime (reset global). Ré-exporter le paquet de test utilisé pour la vérification manuelle de la Task 8 (Ft100SceneExporter regénère `state-engine.js` à chaque export) avant d'exécuter la Task 8 Step 6.

- [ ] **Step 1: Write the failing test**

In `tests/runtime-js/state-engine.test.mjs`, add this test (reusing the file's existing `makeFakeElement` helper defined at the top):

```javascript
test('initPage only resets pause/cache state for elements within its own container, not the whole page', () => {
  const window = loadRuntime(['tag-bridge.js', 'expression-evaluator.js', 'effect-applier.js', 'state-engine.js']);

  window.tf100webScadaBuilder = {
    getTagValue(tagId) {
      const mappingId = String(tagId).replace(/^tf100\.mapping\./, '');
      return { '42': 95 }[mappingId] ?? null;
    },
  };

  const stateConfig = {
    defaultEffect: {},
    states: [
      {
        id: 's1',
        name: 'Alarme',
        enabled: true,
        expression: {
          ast: {
            type: 'binary',
            op: 'GreaterThan',
            left: { type: 'tagRef', tagName: 'tf100.mapping.42' },
            right: { type: 'literalNumber', value: 80 },
          },
        },
        effect: { backgroundColor: '#E53935' },
      },
    ],
  };

  const applied = [];
  window.ScadaRuntime.EffectApplier.apply = (element, effect) => applied.push({ id: element.id, effect });

  const headerElement = makeFakeElement('header_el1', JSON.stringify(stateConfig));
  const headerContainer = { querySelectorAll: (sel) => (sel === '[data-scada-state-config]' ? [headerElement] : []) };
  const bodyContainer = { querySelectorAll: () => [] };

  // Header initializes once; its bound input gets locked for editing (pauseElement).
  window.ScadaRuntime.StateEngine.initPage(headerContainer, 'hdr01');
  window.ScadaRuntime.StateEngine.pauseElement('header_el1');

  // A body-only navigation re-initializes an unrelated container.
  window.ScadaRuntime.StateEngine.initPage(bodyContainer, 'win00009');

  // The header's element must still be paused: initPage on a different container
  // must not resume elements it doesn't own.
  window.ScadaRuntime.StateEngine.evaluate(headerElement, { '42': 95 });

  assert.equal(applied.length, 0,
    'header_el1 was edit-locked (paused) and must stay paused after an unrelated container re-initializes');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test state-engine.test.mjs`
Expected: FAIL — `applied.length` is `1`, not `0` (the second `initPage` call wiped `header_el1`'s pause).

- [ ] **Step 3: Fix `initPage`**

In `src/ScadaBuilderV2.Rendering/Runtime/state-engine.js`, replace:

```javascript
  /**
   * Resets caches and scans the container for elements with data-scada-state-config.
   * Does NOT evaluate — the host calls evaluate() per element after initPage.
   *
   * @param {Element} container  - The DOM container to scan (e.g. page root).
   * @param {string}  pageId     - Unique page identifier (for namespacing).
   */
  function initPage(container, pageId) {
    // Reset paused state and state cache
    _paused = {};
    _stateCache = {};

    if (!container) {
      return;
    }

    // Pre-cache all elements with data-scada-state-config
    var elements = container.querySelectorAll('[data-scada-state-config]');
    for (var i = 0; i < elements.length; i++) {
      _stateCache[elements[i].getAttribute('data-scada-element-id') || elements[i].id] = null;
    }
  }
```

with:

```javascript
  /**
   * Scans the container for elements with data-scada-state-config and resets
   * their pause/cache state only — never other containers' elements.
   *
   * Multiple containers (e.g. a composed page's header/body/footer slots) can
   * each call initPage independently without clobbering each other's edit-lock
   * (pauseElement) or state-cache entries. Does NOT evaluate — the host calls
   * evaluate() per element after initPage.
   *
   * @param {Element} container  - The DOM container to scan (e.g. a page root).
   * @param {string}  pageId     - Unique page identifier (for namespacing).
   */
  function initPage(container, pageId) {
    if (!container) {
      return;
    }

    var elements = container.querySelectorAll('[data-scada-state-config]');
    for (var i = 0; i < elements.length; i++) {
      var id = elements[i].getAttribute('data-scada-element-id') || elements[i].id;
      if (!id) {
        continue;
      }
      delete _paused[id];
      _stateCache[id] = null;
    }
  }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test state-engine.test.mjs`
Expected: all tests in the file PASS, including the new one.

- [ ] **Step 5: Run the full runtime-js suite to check for regressions in sibling modules**

Run: `cd "F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\tests\runtime-js" && node --test .`
Expected: all tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/ScadaBuilderV2.Rendering/Runtime/state-engine.js tests/runtime-js/state-engine.test.mjs
git commit -m "fix: scope StateEngine.initPage pause/cache reset to its own container"
```

---

## Task 1: Résolution manifest — quelles pages composent une page donnée

**Files:**
- Create: `frontend/scada_builder_composition.py`
- Test: `frontend/tests_scada_page_composition.py`

**Interfaces:**
- Produces: `_load_manifest(static_root: Path) -> dict`, `_resolve_composed_page_ids(manifest: dict, page_id: str) -> list[tuple[str, str]]` (liste ordonnée de `(role, page_id)` parmi `("header", x)`, `("body", page_id)`, `("footer", y)`).

- [ ] **Step 1: Write the failing tests**

Create `frontend/tests_scada_page_composition.py`:

```python
import json
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from .scada_builder_composition import _load_manifest, _resolve_composed_page_ids


class ScadaCompositionManifestTests(unittest.TestCase):
    def test_load_manifest_returns_dict_when_valid(self):
        with TemporaryDirectory() as temp_dir:
            static_root = Path(temp_dir)
            scada_dir = static_root / "scada"
            scada_dir.mkdir()
            (scada_dir / "manifest.json").write_text('{"HomePageId": "win00009"}', encoding="utf-8")

            manifest = _load_manifest(static_root)

        self.assertEqual(manifest["HomePageId"], "win00009")

    def test_load_manifest_returns_empty_dict_when_missing(self):
        with TemporaryDirectory() as temp_dir:
            manifest = _load_manifest(Path(temp_dir))

        self.assertEqual(manifest, {})

    def test_load_manifest_returns_empty_dict_when_invalid_json(self):
        with TemporaryDirectory() as temp_dir:
            static_root = Path(temp_dir)
            scada_dir = static_root / "scada"
            scada_dir.mkdir()
            (scada_dir / "manifest.json").write_text("{not json", encoding="utf-8")

            manifest = _load_manifest(static_root)

        self.assertEqual(manifest, {})


class ScadaCompositionResolutionTests(unittest.TestCase):
    def _manifest(self, pages):
        return {"Pages": pages}

    def test_default_page_without_header_footer_resolves_to_body_only(self):
        manifest = self._manifest([
            {"Id": "win00009", "Type": "default", "IncludeInBuild": True},
        ])

        resolved = _resolve_composed_page_ids(manifest, "win00009")

        self.assertEqual(resolved, [("body", "win00009")])

    def test_default_page_with_header_and_footer_resolves_in_order(self):
        manifest = self._manifest([
            {"Id": "hdr", "Type": "header", "IncludeInBuild": True},
            {
                "Id": "win00009", "Type": "default", "IncludeInBuild": True,
                "HeaderPageId": "hdr", "FooterPageId": "ftr",
            },
            {"Id": "ftr", "Type": "footer", "IncludeInBuild": True},
        ])

        resolved = _resolve_composed_page_ids(manifest, "win00009")

        self.assertEqual(resolved, [("header", "hdr"), ("body", "win00009"), ("footer", "ftr")])

    def test_header_only(self):
        manifest = self._manifest([
            {"Id": "hdr", "Type": "header", "IncludeInBuild": True},
            {"Id": "win00009", "Type": "default", "IncludeInBuild": True, "HeaderPageId": "hdr"},
        ])

        resolved = _resolve_composed_page_ids(manifest, "win00009")

        self.assertEqual(resolved, [("header", "hdr"), ("body", "win00009")])

    def test_footer_only(self):
        manifest = self._manifest([
            {"Id": "win00009", "Type": "default", "IncludeInBuild": True, "FooterPageId": "ftr"},
            {"Id": "ftr", "Type": "footer", "IncludeInBuild": True},
        ])

        resolved = _resolve_composed_page_ids(manifest, "win00009")

        self.assertEqual(resolved, [("body", "win00009"), ("footer", "ftr")])

    def test_invalid_header_reference_is_ignored(self):
        manifest = self._manifest([
            {"Id": "win00009", "Type": "default", "IncludeInBuild": True, "HeaderPageId": "missing"},
        ])

        resolved = _resolve_composed_page_ids(manifest, "win00009")

        self.assertEqual(resolved, [("body", "win00009")])

    def test_header_reference_of_wrong_type_is_ignored(self):
        manifest = self._manifest([
            {"Id": "other", "Type": "default", "IncludeInBuild": True},
            {"Id": "win00009", "Type": "default", "IncludeInBuild": True, "HeaderPageId": "other"},
        ])

        resolved = _resolve_composed_page_ids(manifest, "win00009")

        self.assertEqual(resolved, [("body", "win00009")])

    def test_fragment_page_never_composes_even_with_header_set(self):
        manifest = self._manifest([
            {"Id": "hdr", "Type": "header", "IncludeInBuild": True},
            {"Id": "popup1", "Type": "fragment", "IncludeInBuild": True, "HeaderPageId": "hdr"},
        ])

        resolved = _resolve_composed_page_ids(manifest, "popup1")

        self.assertEqual(resolved, [("body", "popup1")])

    def test_page_missing_from_manifest_resolves_to_body_only(self):
        manifest = self._manifest([
            {"Id": "other", "Type": "default", "IncludeInBuild": True},
        ])

        resolved = _resolve_composed_page_ids(manifest, "win00009")

        self.assertEqual(resolved, [("body", "win00009")])


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `python manage.py test frontend.tests_scada_page_composition -v 2`
Expected: `ModuleNotFoundError: No module named 'frontend.scada_builder_composition'`

- [ ] **Step 3: Write minimal implementation**

Create `frontend/scada_builder_composition.py`:

```python
"""STATIC_ROOT-based SCADA Builder V2 page composition (header/body/footer).

Reads the manifest and page fragments deployed by deploy_package_to_static
under STATIC_ROOT/scada/ (see core.management.commands.deploy_scada_builder).
Deliberately separate from the frozen legacy frontend.scada_package module,
which assumes a different on-disk layout (the extracted .sb2 package root,
not STATIC_ROOT) and is out of scope for this flow.
"""
import json
from pathlib import Path

from .scada_package import _compiled_pages, _page_id, _page_type


def _load_manifest(static_root: Path) -> dict:
    manifest_path = Path(static_root) / "scada" / "manifest.json"
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    return manifest if isinstance(manifest, dict) else {}


def _resolve_composed_page_ids(manifest: dict, page_id: str) -> list[tuple[str, str]]:
    """Resolve which pages compose `page_id`, in header -> body -> footer order.

    Only pages of type "default" ever compose a header/footer: fragment
    (popup) pages are rendered standalone by design, even if their manifest
    entry has a stray HeaderPageId/FooterPageId from an authoring mistake.
    A HeaderPageId/FooterPageId reference is only honored when it points to
    an existing page of the matching type ("header"/"footer"), mirroring
    Ft100PackageValidation.ValidatePageReference on the exporter side.
    """
    pages_by_id = {_page_id(page): page for page in _compiled_pages(manifest)}
    page = pages_by_id.get(page_id)
    if page is None or _page_type(page) != "default":
        return [("body", page_id)]

    resolved: list[tuple[str, str]] = []
    header_id = str(page.get("HeaderPageId") or "").strip()
    header_page = pages_by_id.get(header_id)
    if header_page is not None and _page_type(header_page) == "header":
        resolved.append(("header", header_id))

    resolved.append(("body", page_id))

    footer_id = str(page.get("FooterPageId") or "").strip()
    footer_page = pages_by_id.get(footer_id)
    if footer_page is not None and _page_type(footer_page) == "footer":
        resolved.append(("footer", footer_id))

    return resolved
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `python manage.py test frontend.tests_scada_page_composition -v 2`
Expected: all 11 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/scada_builder_composition.py frontend/tests_scada_page_composition.py
git commit -m "feat: resolve SCADA page header/body/footer composition from manifest"
```

---

## Task 2: Chargement des fragments composés (`load_composed_page`)

**Files:**
- Modify: `frontend/scada_builder_composition.py`
- Test: `frontend/tests_scada_page_composition.py`

**Interfaces:**
- Consumes: `_load_manifest`, `_resolve_composed_page_ids` (Task 1).
- Produces: `load_composed_page(static_root: Path, page_id: str) -> Optional[dict]`, retournant :
  ```python
  {
      "page_id": str,
      "parts": [{"role": "header"|"body"|"footer", "page_id": str, "html": str, "css_hash": str, "width": int|float, "height": int|float}, ...],
      "width": int|float, "height": int|float, "width_css": str, "height_css": str,
  }
  ```
  ou `None` si la page demandée (`role == "body"`) n'a pas de fichier HTML ou pas de fragment `ft100-<id>` valide.

- [ ] **Step 1: Write the failing tests**

Append to `frontend/tests_scada_page_composition.py` (add `import json` alongside existing imports at the top, and add `from .scada_builder_composition import load_composed_page` to the existing import line):

```python
from .scada_builder_composition import _load_manifest, _resolve_composed_page_ids, load_composed_page
```

Add this class at the end of the file, before `if __name__ == "__main__":`:

```python
class ScadaCompositionLoadTests(unittest.TestCase):
    def _write_static_page(self, static_root: Path, page_id: str, body: str, width: str = "", height: str = "") -> None:
        page_dir = static_root / "scada" / "pages" / page_id
        page_dir.mkdir(parents=True, exist_ok=True)
        attrs = ""
        if width:
            attrs += f' data-scada-width="{width}"'
        if height:
            attrs += f' data-scada-height="{height}"'
        (page_dir / f"{page_id}.html").write_text(
            f'<html><head><link rel="stylesheet" href="../../css/{page_id}.abc123ef.css"></head>'
            f'<body><div id="ft100-{page_id}"{attrs}>{body}</div></body></html>',
            encoding="utf-8",
        )

    def _write_manifest(self, static_root: Path, manifest: dict) -> None:
        scada_dir = static_root / "scada"
        scada_dir.mkdir(parents=True, exist_ok=True)
        (scada_dir / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")

    def test_body_only_page_returns_single_part(self):
        with TemporaryDirectory() as temp_dir:
            static_root = Path(temp_dir)
            self._write_manifest(static_root, {
                "Pages": [{"Id": "win00009", "Type": "default", "IncludeInBuild": True}],
            })
            self._write_static_page(static_root, "win00009", "Home", width="1920", height="900")

            result = load_composed_page(static_root, "win00009")

        self.assertIsNotNone(result)
        self.assertEqual([p["role"] for p in result["parts"]], ["body"])
        self.assertEqual(result["parts"][0]["css_hash"], "abc123ef")
        self.assertEqual(result["width"], 1920)
        self.assertEqual(result["height"], 900)
        self.assertEqual(result["width_css"], "1920px")
        self.assertEqual(result["height_css"], "900px")

    def test_composed_page_sums_height_and_maxes_width(self):
        with TemporaryDirectory() as temp_dir:
            static_root = Path(temp_dir)
            self._write_manifest(static_root, {
                "Pages": [
                    {"Id": "hdr", "Type": "header", "IncludeInBuild": True},
                    {
                        "Id": "win00009", "Type": "default", "IncludeInBuild": True,
                        "HeaderPageId": "hdr", "FooterPageId": "ftr",
                    },
                    {"Id": "ftr", "Type": "footer", "IncludeInBuild": True},
                ],
            })
            self._write_static_page(static_root, "hdr", "Header", width="1920", height="80")
            self._write_static_page(static_root, "win00009", "Home", width="1600", height="900")
            self._write_static_page(static_root, "ftr", "Footer", width="1920", height="40")

            result = load_composed_page(static_root, "win00009")

        self.assertEqual(result["parts"][0]["page_id"], "hdr")
        self.assertIn("Header", result["parts"][0]["html"])
        self.assertEqual(result["parts"][1]["page_id"], "win00009")
        self.assertEqual(result["parts"][2]["page_id"], "ftr")
        self.assertEqual(result["width"], 1920)
        self.assertEqual(result["height"], 1020)
        self.assertEqual(result["width_css"], "1920px")
        self.assertEqual(result["height_css"], "1020px")

    def test_missing_header_html_file_is_skipped_not_errored(self):
        with TemporaryDirectory() as temp_dir:
            static_root = Path(temp_dir)
            self._write_manifest(static_root, {
                "Pages": [
                    {"Id": "hdr", "Type": "header", "IncludeInBuild": True},
                    {"Id": "win00009", "Type": "default", "IncludeInBuild": True, "HeaderPageId": "hdr"},
                ],
            })
            self._write_static_page(static_root, "win00009", "Home", width="1920", height="900")
            # Note: no "hdr" page file written on disk (deploy mismatch).

            result = load_composed_page(static_root, "win00009")

        self.assertIsNotNone(result)
        self.assertEqual([p["role"] for p in result["parts"]], ["body"])

    def test_missing_body_html_file_returns_none(self):
        with TemporaryDirectory() as temp_dir:
            static_root = Path(temp_dir)
            self._write_manifest(static_root, {
                "Pages": [{"Id": "win00009", "Type": "default", "IncludeInBuild": True}],
            })

            result = load_composed_page(static_root, "win00009")

        self.assertIsNone(result)

    def test_missing_manifest_falls_back_to_single_body_part(self):
        with TemporaryDirectory() as temp_dir:
            static_root = Path(temp_dir)
            self._write_static_page(static_root, "win00009", "Home", width="1920", height="900")
            # Note: no manifest.json written at all.

            result = load_composed_page(static_root, "win00009")

        self.assertIsNotNone(result)
        self.assertEqual([p["role"] for p in result["parts"]], ["body"])

    def test_image_urls_are_rewritten_in_composed_fragment(self):
        with TemporaryDirectory() as temp_dir:
            static_root = Path(temp_dir)
            self._write_manifest(static_root, {
                "Pages": [{"Id": "win00009", "Type": "default", "IncludeInBuild": True}],
            })
            self._write_static_page(static_root, "win00009", '<img src="images/pump.png">')

            result = load_composed_page(static_root, "win00009")

        self.assertIn("/static/scada/images/pump.png", result["parts"][0]["html"])
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `python manage.py test frontend.tests_scada_page_composition.ScadaCompositionLoadTests -v 2`
Expected: `ImportError: cannot import name 'load_composed_page'`

- [ ] **Step 3: Write minimal implementation**

Add to `frontend/scada_builder_composition.py` (after the existing imports, add `import re` and `from typing import Optional` and `from django.templatetags.static import static`; append the following after `_resolve_composed_page_ids`):

```python
_SCADA_PAGE_IMAGE_SRC_RE = re.compile(r'(\bsrc=)(["\'])images/([^"\']+)\2', re.IGNORECASE)


def _extract_html_element_by_id(html: str, element_id: str) -> str:
    marker = f'id="{element_id}"'
    start_marker = html.find(marker)
    if start_marker < 0:
        marker = f"id='{element_id}'"
        start_marker = html.find(marker)
    if start_marker < 0:
        return ""

    start = html.rfind("<div", 0, start_marker)
    if start < 0:
        return ""

    tag_re = re.compile(r"</?div\b[^>]*>", re.IGNORECASE)
    depth = 0
    for match in tag_re.finditer(html, start):
        tag = match.group(0)
        if tag.startswith("</"):
            depth -= 1
            if depth == 0:
                return html[start:match.end()]
        else:
            depth += 1
    return ""


def _extract_css_hash_from_html(html: str) -> str:
    match = re.search(r'<link[^>]*href="[^"]*\.([a-zA-Z0-9_-]{8,64})\.css"', html)
    if match:
        return match.group(1)
    return ""


def _extract_page_dimension_from_html(html: str, attr: str) -> str:
    pattern = re.compile(rf'{re.escape(attr)}\s*=\s*"([^"]*)"')
    match = pattern.search(html)
    if match:
        return match.group(1)
    return ""


def _rewrite_scada_page_image_urls(fragment: str) -> str:
    """Rewrite the exporter's page-relative "images/<name>" src references.

    Ft100SceneExporter emits <img src="images/<name>"> expecting images/ to sit
    next to the page's own HTML (see its own docs). deploy_package_to_static
    flattens all pages' images into a single STATIC_ROOT/scada/images/, so the
    relative reference must be rewritten to the actual static URL before the
    fragment is injected into a #scada-host slot.
    """
    return _SCADA_PAGE_IMAGE_SRC_RE.sub(
        lambda m: f'{m.group(1)}{m.group(2)}{static(f"scada/images/{m.group(3)}")}{m.group(2)}',
        fragment,
    )


def _numeric_dimension(value) -> float:
    try:
        numeric = float(value)
    except (TypeError, ValueError):
        return 0.0
    return numeric if numeric > 0 else 0.0


def _int_or_float(value: float):
    return int(value) if float(value).is_integer() else value


def _css_px(value) -> str:
    if not value:
        return ""
    return f"{_int_or_float(value)}px"


def load_composed_page(static_root: Path, page_id: str) -> Optional[dict]:
    static_root = Path(static_root)
    manifest = _load_manifest(static_root)
    resolved = _resolve_composed_page_ids(manifest, page_id) if manifest else [("body", page_id)]

    parts = []
    for role, resolved_id in resolved:
        page_file = static_root / "scada" / "pages" / resolved_id / f"{resolved_id}.html"
        if not page_file.is_file():
            if role == "body":
                return None
            continue
        html = page_file.read_text(encoding="utf-8")
        fragment = _extract_html_element_by_id(html, f"ft100-{resolved_id}")
        if not fragment:
            if role == "body":
                return None
            continue
        fragment = _rewrite_scada_page_image_urls(fragment)
        width = _numeric_dimension(_extract_page_dimension_from_html(html, "data-scada-width"))
        height = _numeric_dimension(_extract_page_dimension_from_html(html, "data-scada-height"))
        parts.append({
            "role": role,
            "page_id": resolved_id,
            "html": fragment,
            "css_hash": _extract_css_hash_from_html(html),
            "width": _int_or_float(width),
            "height": _int_or_float(height),
        })

    if not parts:
        return None

    total_width = max((p["width"] for p in parts), default=0)
    total_height = sum(p["height"] for p in parts)
    return {
        "page_id": page_id,
        "parts": parts,
        "width": total_width,
        "height": total_height,
        "width_css": _css_px(total_width),
        "height_css": _css_px(total_height),
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `python manage.py test frontend.tests_scada_page_composition -v 2`
Expected: all 17 tests PASS (11 from Task 1 + 6 from this task).

- [ ] **Step 5: Commit**

```bash
git add frontend/scada_builder_composition.py frontend/tests_scada_page_composition.py
git commit -m "feat: load and compose SCADA header/body/footer fragments from STATIC_ROOT"
```

---

## Task 3: Brancher `views.scada_package_page` sur le nouveau module

**Files:**
- Modify: `frontend/views.py:208-266` (supprimer les 4 helpers relocalisés), `frontend/views.py:715-754` (`scada_package_page`)

**Interfaces:**
- Consumes: `load_composed_page(static_root, page_id)` (Task 2).

- [ ] **Step 1: Remove the relocated helpers from views.py**

In `frontend/views.py`, delete this entire block (currently lines 208-266, right after `scada_package_asset` and before `_scada_home_page_id`):

```python
def _extract_html_element_by_id(html: str, element_id: str) -> str:
    marker = f'id="{element_id}"'
    start_marker = html.find(marker)
    if start_marker < 0:
        marker = f"id='{element_id}'"
        start_marker = html.find(marker)
    if start_marker < 0:
        return ""

    start = html.rfind("<div", 0, start_marker)
    if start < 0:
        return ""

    tag_re = re.compile(r"</?div\b[^>]*>", re.IGNORECASE)
    depth = 0
    for match in tag_re.finditer(html, start):
        tag = match.group(0)
        if tag.startswith("</"):
            depth -= 1
            if depth == 0:
                return html[start:match.end()]
        else:
            depth += 1
    return ""


def _extract_css_hash_from_html(html):
    match = re.search(r'<link[^>]*href="[^"]*\.([a-zA-Z0-9_-]{8,64})\.css"', html)
    if match:
        return match.group(1)
    return ""


def _extract_page_dimension_from_html(html, attr):
    pattern = re.compile(rf'{re.escape(attr)}\s*=\s*"([^"]*)"')
    match = pattern.search(html)
    if match:
        return match.group(1)
    return ""


_SCADA_PAGE_IMAGE_SRC_RE = re.compile(r'(\bsrc=)(["\'])images/([^"\']+)\2', re.IGNORECASE)


def _rewrite_scada_page_image_urls(fragment: str) -> str:
    """Rewrite the exporter's page-relative "images/<name>" src references.

    Ft100SceneExporter emits <img src="images/<name>"> expecting images/ to sit
    next to the page's own HTML (see its own docs). deploy_package_to_static
    flattens all pages' images into a single STATIC_ROOT/scada/images/, so the
    relative reference must be rewritten to the actual static URL before the
    fragment is injected into #scada-host (otherwise it resolves against
    /visualisation/, not the image's real location).
    """
    return _SCADA_PAGE_IMAGE_SRC_RE.sub(
        lambda m: f'{m.group(1)}{m.group(2)}{static(f"scada/images/{m.group(3)}")}{m.group(2)}',
        fragment,
    )
```

- [ ] **Step 2: Add the import**

Near the top of `frontend/views.py`, in the block of local imports (right after the existing `from .scada_tags import build_scada_tag_export` line):

```python
from .scada_tags import build_scada_tag_export
from .scada_builder_composition import load_composed_page
```

- [ ] **Step 3: Replace `scada_package_page`**

Replace the current body (lines 715-754 before this plan's edits):

```python
@login_required
def scada_package_page(request, page_id: str):
    """Retourne le fragment HTML d'une page SCADA pour navigation AJAX.

    Lit les fichiers depuis STATIC_ROOT/scada/pages/ (deployes par deploy_scada_builder).
    Extrait le fragment racine ft100-<page_id> et les metadonnees CSS/dimensions.
    Aucun restart Gunicorn necessaire : les fichiers sont servis directement.
    """
    if not settings.TF100_INDUSTRIAL_DEPLOYMENT:
        raise Http404
    config = StationConfig.objects.filter(pk=1).first()
    if config is None or config.station_type != StationConfig.StationTypeChoices.SCADA_BUILDER_2:
        raise Http404

    static_root = Path(getattr(settings, "STATIC_ROOT", ""))
    page_file = static_root / "scada" / "pages" / page_id / f"{page_id}.html"
    if not page_file.is_file():
        raise Http404

    html = page_file.read_text(encoding="utf-8")
    fragment = _extract_html_element_by_id(html, f"ft100-{page_id}")
    fragment = _rewrite_scada_page_image_urls(fragment) if fragment else fragment
    css_hash = _extract_css_hash_from_html(html)
    width = _extract_page_dimension_from_html(html, "data-scada-width")
    height = _extract_page_dimension_from_html(html, "data-scada-height")
    # Feeds --ft100-scada-width/height (see visualisation_import.js
    # applyRuntimeDimensions), which actually sizes the .scada-host box -- the
    # separate data-scada-native-width/height only drives the zoom-fit ratio math.
    width_css = f"{width}px" if width else ""
    height_css = f"{height}px" if height else ""

    return JsonResponse({
        "page_id": page_id,
        "html": fragment or "",
        "css_hash": css_hash,
        "width": width,
        "height": height,
        "width_css": width_css,
        "height_css": height_css,
    })
```

with:

```python
@login_required
def scada_package_page(request, page_id: str):
    """Retourne les fragments HTML composes (header?/body/footer?) d'une page SCADA.

    Delegue a scada_builder_composition.load_composed_page, qui lit
    STATIC_ROOT/scada/ (deploye par deploy_scada_builder) et resout
    HeaderPageId/FooterPageId via le manifest. Aucun restart Gunicorn
    necessaire : les fichiers sont servis directement.
    """
    if not settings.TF100_INDUSTRIAL_DEPLOYMENT:
        raise Http404
    config = StationConfig.objects.filter(pk=1).first()
    if config is None or config.station_type != StationConfig.StationTypeChoices.SCADA_BUILDER_2:
        raise Http404

    static_root = Path(getattr(settings, "STATIC_ROOT", ""))
    result = load_composed_page(static_root, page_id)
    if result is None:
        raise Http404

    return JsonResponse(result)
```

- [ ] **Step 4: Run the full frontend test suite to see the expected breakage**

Run: `python manage.py test frontend -v 2`
Expected: `ScadaPackagePageServesNewEffectFieldsTests.test_page_with_read_variable_and_color_filter_serves_without_error` FAILS on `payload["css_hash"]` (KeyError — replaced by `parts` in Task 4), and the 3 obsolete tests in `ScadaBuilderPackagePageViewTests` FAIL or already failed before this change (confirm with `git stash` + rerun if you want to double check the "already broken" baseline — not required to proceed).

- [ ] **Step 5: Commit**

```bash
git add frontend/views.py
git commit -m "refactor: delegate scada_package_page to scada_builder_composition"
```

---

## Task 4: Mettre à jour les tests bout-en-bout existants pour le nouveau contrat

**Files:**
- Modify: `frontend/tests_scada_deploy.py`

- [ ] **Step 1: Update the failing assertion**

In `frontend/tests_scada_deploy.py`, in `ScadaPackagePageServesNewEffectFieldsTests.test_page_with_read_variable_and_color_filter_serves_without_error`, replace:

```python
            response = self.client.get("/visualisation/scada/page/win00009/")
            self.assertEqual(response.status_code, 200)
            payload = response.json()
            self.assertIn("readVariable", payload["html"])
            self.assertIn("colorFilterColor", payload["html"])
            self.assertEqual(payload["css_hash"], "abc12345")
```

with:

```python
            response = self.client.get("/visualisation/scada/page/win00009/")
            self.assertEqual(response.status_code, 200)
            payload = response.json()
            self.assertEqual([p["role"] for p in payload["parts"]], ["body"])
            body_part = payload["parts"][0]
            self.assertIn("readVariable", body_part["html"])
            self.assertIn("colorFilterColor", body_part["html"])
            self.assertEqual(body_part["css_hash"], "abc12345")
```

- [ ] **Step 2: Run this test to verify it passes**

Run: `python manage.py test frontend.tests_scada_deploy.ScadaPackagePageServesNewEffectFieldsTests -v 2`
Expected: PASS.

- [ ] **Step 3: Add an end-to-end composed-page test**

Append this test method to `ScadaPackagePageServesNewEffectFieldsTests` (same class, after the existing test method):

```python
    def test_page_with_header_and_footer_composes_all_three_parts(self):
        with TemporaryDirectory() as static_root, TemporaryDirectory() as pkg_dir:
            manifest = {
                "HomePageId": "win00009",
                "Pages": [
                    {"Id": "hdr01", "Type": "header", "IncludeInBuild": True},
                    {
                        "Id": "win00009", "Type": "default", "IncludeInBuild": True,
                        "HeaderPageId": "hdr01", "FooterPageId": "ftr01",
                    },
                    {"Id": "ftr01", "Type": "footer", "IncludeInBuild": True},
                ],
            }

            def _page_html(page_id, width, height, text):
                return (
                    '<!doctype html><html><body>'
                    f'<div id="ft100-{page_id}" data-scada-width="{width}" data-scada-height="{height}">{text}</div>'
                    f'<link rel="stylesheet" href="css/{page_id}.abc12345.css">'
                    '</body></html>'
                )

            package_path = Path(pkg_dir) / "export.sb2"
            with zipfile.ZipFile(package_path, "w") as zf:
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/manifest.json", json.dumps(manifest))
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/hdr01/hdr01.html", _page_html("hdr01", 1920, 80, "Header content"))
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/hdr01/css/hdr01.abc12345.css", "")
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/win00009/win00009.html", _page_html("win00009", 1600, 900, "Body content"))
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/win00009/css/win00009.abc12345.css", "")
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/ftr01/ftr01.html", _page_html("ftr01", 1920, 40, "Footer content"))
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/ftr01/css/ftr01.abc12345.css", "")
                zf.writestr(f"{SCADA_PACKAGE_DIR_NAME}/scada-runtime.deadbeef.js", "// runtime")

            with override_settings(STATIC_ROOT=static_root, TF100_INDUSTRIAL_DEPLOYMENT=True):
                call_command("deploy_scada_builder", str(package_path))

                from django.contrib.auth import get_user_model

                from .models import StationConfig

                User = get_user_model()
                user = User.objects.create_user(username="tester3", password="x")
                self.client.force_login(user)
                StationConfig.objects.update_or_create(
                    pk=1, defaults={"station_type": StationConfig.StationTypeChoices.SCADA_BUILDER_2}
                )

                response = self.client.get("/visualisation/scada/page/win00009/")
                self.assertEqual(response.status_code, 200)
                payload = response.json()
                self.assertEqual([p["role"] for p in payload["parts"]], ["header", "body", "footer"])
                self.assertIn("Header content", payload["parts"][0]["html"])
                self.assertIn("Body content", payload["parts"][1]["html"])
                self.assertIn("Footer content", payload["parts"][2]["html"])
                self.assertEqual(payload["width"], 1920)
                self.assertEqual(payload["height"], 1020)
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `python manage.py test frontend.tests_scada_deploy -v 2`
Expected: all tests in the file PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/tests_scada_deploy.py
git commit -m "test: cover composed header/body/footer response via scada_package_page endpoint"
```

---

## Task 5: Retirer les tests obsolètes de `tests_scada_package.py`

**Files:**
- Modify: `frontend/tests_scada_package.py`

Ces trois tests (`test_page_endpoint_returns_requested_page_fragment`, `test_page_endpoint_requires_scada_builder_station_type`, `test_page_endpoint_injects_manual_navigate_actions`) monkeypatchent `views.SCADA_IMPORT_ROOT` avec l'ancienne arborescence de paquet (`SCADA_PACKAGE_DIR_NAME/<id>/<id>.html`), mais `scada_package_page` lit `STATIC_ROOT/scada/pages/` — ils étaient déjà cassés avant ce plan (constat du design doc), et testent en partie un comportement (`title`, `actions`, injection d'actions manuelles) qui n'a jamais existé dans ce endpoint : il appartient à `_load_scada_scene` (le chemin `_load_scada_scene`, legacy, distinct — voir `views.py:679-712`). La couverture réelle de `scada_package_page` vit maintenant dans `frontend/tests_scada_deploy.py` (Task 4) et `frontend/tests_scada_page_composition.py` (Tasks 1-2).

- [ ] **Step 1: Remove the three obsolete tests**

In `frontend/tests_scada_package.py`, inside `class ScadaBuilderPackagePageViewTests(SimpleTestCase):`, delete these three methods in full:

```python
    def test_page_endpoint_returns_requested_page_fragment(self):
        with TemporaryDirectory() as temp_dir:
            original_root = views.SCADA_IMPORT_ROOT
            views.SCADA_IMPORT_ROOT = Path(temp_dir)
            self.addCleanup(setattr, views, "SCADA_IMPORT_ROOT", original_root)
            self._write_package(views.SCADA_IMPORT_ROOT)
            self._set_fake_station_config("SCADA_BUILDER_2")

            response = views.scada_package_page(self._request(), "page2")

        self.assertEqual(response.status_code, 200)
        payload = json.loads(response.content.decode("utf-8"))
        self.assertEqual(payload["page_id"], "page2")
        self.assertEqual(payload["title"], "Second")
        self.assertIn("Second", payload["html"])
        self.assertNotIn("Home", payload["html"])
        self.assertEqual(payload["width_css"], "")
        self.assertEqual(payload["height_css"], "")
        self.assertEqual(len(payload["css_urls"]), 1)
        self.assertTrue(payload["css_urls"][0].endswith("/visualisation/scada-package/page2/css/page2.css"))
        self.assertEqual(payload["actions"][0]["TargetPageId"], "page2")

    def test_page_endpoint_requires_scada_builder_station_type(self):
        with TemporaryDirectory() as temp_dir:
            original_root = views.SCADA_IMPORT_ROOT
            views.SCADA_IMPORT_ROOT = Path(temp_dir)
            self.addCleanup(setattr, views, "SCADA_IMPORT_ROOT", original_root)
            self._write_package(views.SCADA_IMPORT_ROOT)
            self._set_fake_station_config("SANITAIRE")

            with self.assertRaises(Exception) as raised:
                views.scada_package_page(self._request(), "page2")

        self.assertEqual(raised.exception.__class__.__name__, "Http404")

    def test_page_endpoint_injects_manual_navigate_actions(self):
        with TemporaryDirectory() as temp_dir:
            original_root = views.SCADA_IMPORT_ROOT
            views.SCADA_IMPORT_ROOT = Path(temp_dir)
            self.addCleanup(setattr, views, "SCADA_IMPORT_ROOT", original_root)
            self._write_package(views.SCADA_IMPORT_ROOT)
            self._set_fake_station_config("SCADA_BUILDER_2")
            self._set_manual_actions({
                "page2": {
                    "legacy_1": {
                        "trigger": "click",
                        "kind": "navigate",
                        "target_page_id": "home",
                    }
                }
            })

            response = views.scada_package_page(self._request(), "page2")

        payload = json.loads(response.content.decode("utf-8"))
        self.assertIn('id="legacy_1" data-scada-events=', payload["html"])
        self.assertIn("manual_nav_page2_legacy_1", payload["html"])
        manual_action = next(action for action in payload["actions"] if action["Id"] == "manual_nav_page2_legacy_1")
        self.assertEqual(manual_action["Kind"], "navigate")
        self.assertEqual(manual_action["TargetPageId"], "home")
```

Leave every other method in `ScadaBuilderPackagePageViewTests` untouched (they test `_configured_scada_bindings_for_page`, `_manifest_scada_bindings`, `_inject_scada_element_attrs`, and `scada_package_status`, which are unrelated to this plan).

- [ ] **Step 2: Run the file's test suite to verify nothing else broke**

Run: `python manage.py test frontend.tests_scada_package -v 2`
Expected: all remaining tests PASS (no reference to the removed methods anywhere else in the file).

- [ ] **Step 3: Commit**

```bash
git add frontend/tests_scada_package.py
git commit -m "test: remove scada_package_page tests obsoleted by the STATIC_ROOT rewrite"
```

---

## Task 6: Slots DOM header/body/footer dans le template

**Files:**
- Modify: `templates/frontend/station/visualisation.html:165-173`
- Modify: `static/asset/css/templates/frontend/station/_visualisation_import_style.css:194-196`

- [ ] **Step 1: Add the three child slots**

In `templates/frontend/station/visualisation.html`, replace:

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

with:

```html
            <div id="scada-host" class="scada-host"
                 data-scada-home-page="{{ home_page_id|default:'win00009' }}"
                 data-scada-page-url-template="{% url 'frontend_scada_package_page' page_id='__PAGE_ID__' %}"
                 data-scada-css-base="{% static 'scada/css/' %}"
                 data-mapping-snapshot-url="{% url 'frontend_station_mapping_snapshot' %}"
                 data-mapping-write-url="{% url 'frontend_station_mapping_write' %}"
                 data-live-refresh-ms="500"
                 data-can-write="1">
              <div id="scada-host-header"></div>
              <div id="scada-host-body"></div>
              <div id="scada-host-footer"></div>
            </div>
```

- [ ] **Step 2: Widen the scene-root display rule to a descendant selector**

In `static/asset/css/templates/frontend/station/_visualisation_import_style.css`, replace:

```css
.scada-host > .ft100-scada-scene{
  display:block;
}
```

with:

```css
.scada-host .ft100-scada-scene{
  display:block;
}
```

(The exported page root now sits one level deeper, inside `#scada-host-header`/`#scada-host-body`/`#scada-host-footer`, so the direct-child combinator would silently stop matching. `display:block` is a `<div>`'s default anyway, so this rule is redundant safety net either way — the change simply keeps it accurate.)

- [ ] **Step 3: No automated test for this step**

There is no existing test asserting the `#scada-host` template markup (confirmed: no test file references `scada-host-header` or renders this template directly). This step is verified visually in Task 7's end-to-end check and manually per Task 8.

- [ ] **Step 4: Commit**

```bash
git add templates/frontend/station/visualisation.html static/asset/css/templates/frontend/station/_visualisation_import_style.css
git commit -m "feat: add header/body/footer slots to the scada-host container"
```

---

## Task 7: `ScadaHost` — état par rôle, rendu différentiel, popup sur le nouveau contrat

**Files:**
- Modify: `static/asset/js/station/visualisation_import.js:50-233`
- Test: `frontend/tests_scada_package.py` (`ScadaBuilderRuntimeAssetTests`)

- [ ] **Step 1: Write the failing test**

In `frontend/tests_scada_package.py`, add this method to `class ScadaBuilderRuntimeAssetTests(SimpleTestCase):` (after `test_runtime_formats_scada_builder_hash_masks`):

```python
    def test_scada_host_tracks_and_diffs_composed_parts(self):
        script = self._repo_path("static", "asset", "js", "station", "visualisation_import.js").read_text(
            encoding="utf-8"
        )

        self.assertIn("currentHeaderPageId: null,", script)
        self.assertIn("currentBodyPageId: null,", script)
        self.assertIn("currentFooterPageId: null,", script)
        self.assertIn("_slotForRole(role) {", script)
        self.assertIn("return document.getElementById(`scada-host-${role}`);", script)
        self.assertIn("const bodyPart = parts.find((part) => part.role === 'body');", script)
        self.assertIn("if (role === 'body' || part.page_id !== this._currentPageIdForRole(role)) {", script)
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python manage.py test frontend.tests_scada_package.ScadaBuilderRuntimeAssetTests.test_scada_host_tracks_and_diffs_composed_parts -v 2`
Expected: FAIL (none of these literals exist in the current script yet).

- [ ] **Step 3: Replace the `ScadaHost` object**

In `static/asset/js/station/visualisation_import.js`, replace the entire `const ScadaHost = { ... };` block (from `const ScadaHost = {` through its closing `};`, i.e. the block currently spanning `currentPageId: null,` down to `_getPopupOverlay(pageId) { ... }` and the final `};`) with:

```javascript
const ScadaHost = {
  currentHeaderPageId: null,
  currentBodyPageId: null,
  currentFooterPageId: null,
  currentCssHashes: new Set(),
  runtimeLoaded: false,
  pendingEditLock: null,

  init(scadaHostEl) {
    const homePageId = scadaHostEl.dataset.scadaHomePage || 'win00009';
    ScadaTagCache.startPolling();
    this.loadPage(homePageId);
  },

  _slotForRole(role) {
    return document.getElementById(`scada-host-${role}`);
  },

  _currentPageIdForRole(role) {
    if (role === 'header') return this.currentHeaderPageId;
    if (role === 'footer') return this.currentFooterPageId;
    return this.currentBodyPageId;
  },

  _setCurrentPageIdForRole(role, pageId) {
    if (role === 'header') this.currentHeaderPageId = pageId;
    else if (role === 'footer') this.currentFooterPageId = pageId;
    else this.currentBodyPageId = pageId;
  },

  _injectCssIfNeeded(cssHash, partPageId) {
    if (!cssHash || this.currentCssHashes.has(cssHash)) return;
    const cssBase = scadaHostEl.dataset.scadaCssBase || '/static/scada/css/';
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = `${cssBase}${encodeURIComponent(partPageId)}.${cssHash}.css`;
    document.head.appendChild(link);
    this.currentCssHashes.add(cssHash);
  },

  _renderPart(role, part) {
    const slot = this._slotForRole(role);
    if (!slot) return;
    slot.innerHTML = part.html;
    this._injectCssIfNeeded(part.css_hash, part.page_id);
    if (window.ScadaRuntime && window.ScadaRuntime.initPage) {
      window.ScadaRuntime.initPage(slot, part.page_id);
    }
  },

  _clearSlot(role) {
    const slot = this._slotForRole(role);
    if (slot) slot.innerHTML = '';
  },

  async loadPage(pageId) {
    if (!pageId) return;
    this._showLoading(true);
    try {
      const pageUrl = (scadaHostEl.dataset.scadaPageUrlTemplate || '/visualisation/scada/page/__PAGE_ID__/')
        .replace('__PAGE_ID__', encodeURIComponent(pageId));
      const resp = await fetch(pageUrl);
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const data = await resp.json();
      const parts = Array.isArray(data.parts) ? data.parts : [];
      const bodyPart = parts.find((part) => part.role === 'body');
      if (!bodyPart) return;

      ['header', 'body', 'footer'].forEach((role) => {
        const part = parts.find((candidate) => candidate.role === role);
        if (!part) {
          if (this._currentPageIdForRole(role) !== null) {
            this._clearSlot(role);
            this._setCurrentPageIdForRole(role, null);
          }
          return;
        }
        // Body is always re-rendered (it's the actual navigation target); header/footer
        // are only re-rendered when they change, so live state (clocks, alarm banners,
        // tag-bound widgets) in a persisted header/footer isn't disrupted by a plain
        // body navigation.
        if (role === 'body' || part.page_id !== this._currentPageIdForRole(role)) {
          this._renderPart(role, part);
          this._setCurrentPageIdForRole(role, part.page_id);
        }
      });

      // Keep the shared zoom/fullscreen chrome (TF100Web-owned, not exported by SCADA
      // Builder V2) in sync with this page's composed size, same mechanism as the
      // legacy scene's AJAX navigation (see applyRuntimeDimensions).
      applyRuntimeDimensions(data.width_css || "", data.height_css || "", data.width || 0, data.height || 0);
    } catch (err) {
      console.error('scada: failed to load page', pageId, err);
    } finally {
      this._showLoading(false);
    }
  },

  _showLoading(show) {
    let backdrop = document.getElementById('scada-loading-backdrop');
    if (show) {
      if (!backdrop) {
        backdrop = document.createElement('div');
        backdrop.id = 'scada-loading-backdrop';
        backdrop.style.cssText = 'position:absolute;inset:0;z-index:9000;background:rgba(15,42,48,0.08);display:flex;align-items:center;justify-content:center;';
        const spinner = document.createElement('div');
        spinner.style.cssText = 'width:32px;height:32px;border:3px solid rgba(15,42,48,0.16);border-top-color:#0f2a30;border-radius:50%;animation:spin 0.8s linear infinite;';
        backdrop.appendChild(spinner);
        const host = document.getElementById('scada-host');
        if (host && host.parentElement) host.parentElement.appendChild(backdrop);
      }
    } else {
      if (backdrop) backdrop.remove();
    }
  },

  acquireEditLock(elementId, inputElement) {
    this.pendingEditLock = {
      elementId: elementId,
      since: Date.now(),
      inputElement: inputElement
    };

    const overlay = document.createElement('div');
    overlay.className = 'scada-input-edit-overlay';
    overlay.id = `scada-edit-overlay-${elementId}`;
    overlay.style.cssText =
      'position:absolute;inset:0;background:rgba(15,42,48,0.06);'
      + 'border:2px solid rgba(15,42,48,0.32);border-radius:4px;'
      + 'pointer-events:none;z-index:10;'
      + 'animation:scada-edit-pulse 2s ease-in-out infinite;';

    const parent = inputElement.parentElement;
    if (parent) {
      if (getComputedStyle(parent).position === 'static') {
        parent.style.position = 'relative';
      }
      parent.appendChild(overlay);
    }

    const timer = setTimeout(() => {
      ScadaHost.releaseEditLock();
      inputElement.blur();
    }, 30000);

    this.pendingEditLock.overlay = overlay;
    this.pendingEditLock.timer = timer;
  },

  releaseEditLock() {
    if (!this.pendingEditLock) return;
    if (this.pendingEditLock.timer) clearTimeout(this.pendingEditLock.timer);
    if (this.pendingEditLock.overlay && this.pendingEditLock.overlay.parentElement) {
      this.pendingEditLock.overlay.remove();
    }
    this.pendingEditLock = null;
  },

  _createPopup(pageId, options) {
    const pageUrl = (scadaHostEl.dataset.scadaPageUrlTemplate || '/visualisation/scada/page/__PAGE_ID__/')
      .replace('__PAGE_ID__', encodeURIComponent(pageId));
    fetch(pageUrl)
      .then(r => r.json())
      .then(data => {
        const parts = Array.isArray(data.parts) ? data.parts : [];
        const bodyPart = parts.find((part) => part.role === 'body');
        if (!bodyPart) return;

        // Fragment (popup) pages never compose a header/footer (see
        // scada_builder_composition._resolve_composed_page_ids), so `parts` here
        // always holds exactly the popup's own body content.
        // Same-document overlay, not an iframe: window.ScadaRuntime,
        // window.tf100webScadaBuilder, and ScadaTagCache's polling all live on this
        // window, and popup content needs all three (live state colors, commands).
        // Page CSS is already strictly scoped under #ft100-<pageId> (enforced by
        // Ft100PackageValidator, no global selectors permitted), so there's no
        // cross-page leakage risk from sharing the document.
        if (bodyPart.css_hash && !this.currentCssHashes.has(bodyPart.css_hash)) {
          const cssBase = scadaHostEl.dataset.scadaCssBase || '/static/scada/css/';
          const link = document.createElement('link');
          link.rel = 'stylesheet';
          link.href = `${cssBase}${encodeURIComponent(bodyPart.page_id)}.${bodyPart.css_hash}.css`;
          document.head.appendChild(link);
          this.currentCssHashes.add(bodyPart.css_hash);
        }

        const overlay = document.createElement('div');
        overlay.className = 'scada-popup-overlay';
        overlay.dataset.scadaPopupPageId = pageId;
        overlay.style.cssText = 'position:absolute;inset:0;z-index:10000;'
          + 'background:rgba(0,0,0,0.28);display:flex;align-items:center;justify-content:center;pointer-events:auto;';

        const panel = document.createElement('div');
        panel.style.cssText = 'position:relative;background:#fff;'
          + 'border:1px solid rgba(15,42,48,0.24);box-shadow:0 16px 42px rgba(15,42,48,0.28);'
          + 'width:80%;height:80%;max-width:960px;max-height:720px;overflow:auto;';

        const closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.textContent = '×';
        closeBtn.style.cssText = 'position:absolute;top:8px;right:8px;z-index:1;'
          + 'width:28px;height:28px;border:0;background:rgba(15,42,48,0.08);'
          + 'border-radius:50%;font-size:18px;cursor:pointer;color:#0f2a30;';
        closeBtn.onclick = function () { overlay.remove(); };
        panel.appendChild(closeBtn);

        const content = document.createElement('div');
        content.innerHTML = bodyPart.html;
        panel.appendChild(content);

        overlay.appendChild(panel);
        overlay.addEventListener('click', function (e) {
          if (e.target === overlay) overlay.remove();
        });
        document.getElementById('scada-host').appendChild(overlay);

        if (window.ScadaRuntime && window.ScadaRuntime.initPage) {
          window.ScadaRuntime.initPage(content, bodyPart.page_id);
        }
      });
  },

  _closePopup(pageId) {
    const existing = ScadaHost._getPopupOverlay(pageId);
    if (existing) existing.remove();
  },

  _getPopupOverlay(pageId) {
    const host = document.getElementById('scada-host');
    if (!host) return null;
    return host.querySelector(`[data-scada-popup-page-id="${pageId}"]`);
  }
};
```

- [ ] **Step 4: Run test to verify it passes**

Run: `python manage.py test frontend.tests_scada_package.ScadaBuilderRuntimeAssetTests -v 2`
Expected: all tests in this class PASS, including the new one.

- [ ] **Step 5: Run the full frontend suite**

Run: `python manage.py test frontend -v 2`
Expected: all tests PASS.

- [ ] **Step 6: Commit**

```bash
git add static/asset/js/station/visualisation_import.js frontend/tests_scada_package.py
git commit -m "feat: diff-render composed header/body/footer parts in ScadaHost"
```

---

## Task 8: Vérification bout-en-bout manuelle

**Files:** none (verification only)

- [ ] **Step 1: Deploy a package with header/footer configured**

From a SCADA Builder V2 export where at least one `Default` page has `HeaderPageId`/`FooterPageId` set (e.g. `win00009` with a real header/footer pair authored per Task 1 Step 1's rule — `Fragment` pages must NOT have them set), run:

```bash
python manage.py deploy_scada_builder path/to/export.sb2
```

- [ ] **Step 2: Load `/visualisation/` and confirm initial composition**

Open `/visualisation/` in a browser logged in as a user with a `StationConfig` of type `SCADA_BUILDER_2`. Confirm the header and footer render above/below the body on first load (this goes through `ScadaHost.init` → `loadPage(homePageId)`, same code path as any navigation).

- [ ] **Step 3: Navigate between two `Default` pages that share the same header/footer**

Trigger a `navigate` action to another `Default` page that references the **same** `HeaderPageId`/`FooterPageId`. Confirm via browser dev tools that `#scada-host-header` and `#scada-host-footer`'s DOM nodes are **not replaced** (e.g. add a temporary marker attribute to one of them before navigating and confirm it survives) while `#scada-host-body` changes.

- [ ] **Step 4: Navigate to a page with a different (or absent) header/footer**

Confirm the header/footer slot(s) do get replaced (or cleared) when the target page's `HeaderPageId`/`FooterPageId` differs from what's currently shown.

- [ ] **Step 5: Confirm a page without header/footer still works**

Navigate to a `Default` page with no `HeaderPageId`/`FooterPageId` set. Confirm the header/footer slots clear out and only the body renders, no console errors.

- [ ] **Step 6: Confirm read/write/state/command events still function, including inside a persisted header/footer**

These are not expected to regress (see rationale below), but must be verified directly rather than assumed, since `ScadaRuntime.initPage`'s internals are generated by SCADA Builder V2 and are not part of this repo/plan:

- **ReadValue**: confirm a numeric/state-bound display element updates live (e.g. wait for a polling tick, or trigger a tag change) both in the body and in a header/footer element, across a body-only navigation (Step 3's scenario) — the header/footer element must keep updating without needing to be re-navigated-to.
- **WriteValue**: click/edit a writable bound element in the body and confirm the write POST succeeds (dev tools Network tab, `data-mapping-write-url`). If any writable element exists in a header/footer, confirm it still writes correctly after surviving a body-only navigation (Step 3) — i.e. its listener wasn't lost.
- **ChangePage (navigate) / OpenPopup / TogglePopup command events**: if the header or footer contains a navigation button or popup trigger, click it once, then navigate the body away and back (Step 3's same-header/footer scenario), then click it again — confirm it still works the second time without a page reload. This is the concrete check for the "persisted header/footer is never re-initialized, so its listeners must survive" assumption in Task 7.
- **State events (conditional effects, e.g. `data-scada-state-config` color filters)**: confirm a state-driven visual effect in a persisted header/footer keeps reacting to tag changes after a body-only navigation, not just on first load.

- [ ] **Step 7: If any check fails**

Return to `superpowers:systematic-debugging` Phase 1 rather than patching ad hoc — do not skip straight to a fix.

---

## Self-Review Notes

- **Spec coverage:** Task 0 fixes a latent shared-runtime bug (`StateEngine.initPage`'s global pause/cache reset) discovered during design review, newly exposed by header/footer persistence — not in the original design doc, added after the design was approved; the design doc's Hors scope section should be read alongside this addition. Task 1-2 cover manifest resolution + fragment loading (design §Architecture). Task 3 covers the view wiring. Task 4-5 cover the design's Tests section (rewriting the obsolete test class, adding composed-page coverage, fixing the currently-passing test broken by the contract change). Task 6 covers the DOM slot + CSS selector change. Task 7 covers the JS diffing behavior and the `_createPopup` contract fix. Task 8 covers the design's "Hors scope" boundary by manually verifying popups are never touched by composition, and explicitly re-verifies read/write/navigate/state/command event parity (ScadaTagCache polling and tf100webScadaBuilder.writeTag are host-level and unaffected by slot structure; ChangePage/OpenPopup/TogglePopup route through the untouched postMessage listener; state/command events are initialized per-DOM-subtree via `ScadaRuntime.initPage`, the same scoping contract `_createPopup` already relies on today for its nested `content` div) — with particular attention to a persisted header/footer's listeners surviving a body-only navigation, since Task 7 deliberately skips re-initializing slots that didn't change.
- **Placeholder scan:** no TBD/TODO; every step has complete, runnable code.
- **Type consistency:** `load_composed_page` (Task 2) returns `parts: list[dict]` with keys `role`/`page_id`/`html`/`css_hash`/`width`/`height` — the same keys are read in Task 3's `views.py` (as opaque `result`), Task 4's test assertions, and Task 7's JS (`part.role`, `part.page_id`, `part.html`, `part.css_hash`). `_resolve_composed_page_ids`'s tuple shape `(role, page_id)` is only consumed internally by `load_composed_page` in the same module — no cross-task mismatch.
