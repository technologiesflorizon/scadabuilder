# TF100Web — Composition header/body/footer du flux SCADA statique (design)

**Repo d'implémentation :** `F:\Projet\Git\TF100Web`, branche `feature/element-plus-state-command-events`.

## Contexte

Le flux SCADA `STATIC_ROOT`-based (`frontend.views.scada_package_page` → JS `ScadaHost.loadPage()`) a perdu la composition header + body + footer lorsque `scada_package_page` a été réécrite pour lire directement `STATIC_ROOT/scada/pages/<page_id>/` (commit `e86a450`). L'ancien flux manifest-parsing (`frontend/scada_package.py::load_scada_builder_package`, chemin legacy, gelé — voir Global Constraints) implémentait cette composition mais n'est pas réutilisable tel quel : il suppose une arborescence de paquet (`scada-builder-v2-ft100-package/<id>/<id>.html`, via `RelativePath`) différente de celle produite par `deploy_package_to_static` (`STATIC_ROOT/scada/pages/<id>/<id>.html`, toujours à plat).

Le document `2026-07-08-tf100web-scada-header-footer-composition.md` (constat initial, pas un plan) posait deux questions ouvertes. Elles sont tranchées ici avec preuves de code, **et corrigées par le produit** sur un point (les popups) :

1. **Popups et composition** — investigation initiale : aucun `ScadaPageType.Popup` n'existe (`ScadaBuilderV2.Domain/Projects/ProjectModels.cs` : `Default | Fragment | Header | Footer`), et l'UI d'authoring active les combobox Header/FooterPageId pour les pages `Default` **et** `Fragment` (`MainWindow.xaml.cs:3397`, `canCompose = PageType is not (Header or Footer)`). Le validateur d'export (`Ft100PackageValidation.cs`) ne restreint pas non plus `HeaderPageId`/`FooterPageId` aux pages `Default`.
   **Décision produit (prime sur le code observé) :** une page `Fragment` (= popup, rendu en overlay du body via `_createPopup`, jamais en navigation plein écran) **ne doit jamais composer** de header/footer, même si son entrée manifest en présente un par erreur d'authoring. Seules les pages `Default` composent. La logique de résolution doit donc vérifier explicitement `_page_type(page) == "default"` avant de résoudre `HeaderPageId`/`FooterPageId` — ne pas se fier uniquement à la présence des champs.
2. **Source de vérité des dimensions** — la vue `STATIC_ROOT`-based ne s'appuie déjà sur aucun champ manifest pour les dimensions (elle lit `data-scada-width`/`data-scada-height` depuis le HTML). Décision : conserver cette source unique pour rester cohérent avec le comportement actuel du cas non composé, plutôt que d'introduire les champs `Width`/`RequiredDisplayWidth` du manifest comme deuxième source.

**Comportement runtime attendu (reformulé avec le produit) :** la page d'accueil (ex. `win00008`) se charge via le même chemin `ScadaHost.loadPage()` que toute navigation (pas de rendu serveur séparé — `ScadaHost.init()` appelle déjà `loadPage(homePageId)`). Lors d'une navigation ultérieure entre pages `Default`, le **body est toujours remplacé**, mais le **header et le footer ne sont remplacés que si la page cible référence un header/footer différent (ou n'en référence plus)** — pas de recomposition systématique des trois parties à chaque appel. Objectif : ne pas interrompre un état vivant dans le header/footer (horloge, bandeau d'alarme, widgets liés à des tags) lors d'une simple navigation de corps de page — une exigence produit pour un SCADA "moderne effectif", même si aucun widget de ce type n'existe encore aujourd'hui.

## Architecture

### Nouveau module `frontend/scada_builder_composition.py`

Isolé du chemin legacy gelé (`scada_package.py`, non modifié) et du fichier `views.py` déjà volumineux (~3600 lignes). Contient uniquement du code lié au flux `STATIC_ROOT` du SCADA Builder.

- **Relocalisées depuis `views.py`** (utilisées uniquement par `scada_package_page` aujourd'hui, nulle part ailleurs) : `_extract_html_element_by_id`, `_extract_css_hash_from_html`, `_extract_page_dimension_from_html`, `_rewrite_scada_page_image_urls`.
- **Importées (lecture seule) depuis `scada_package.py`** : `_compiled_pages`, `_page_id`, `_page_type` — fonctions pures sur un dict manifest, réutilisables sans coupler davantage les deux modules.
- **`_load_manifest(static_root) -> dict`** — lit `STATIC_ROOT/scada/manifest.json`, `try/except (OSError, ValueError)` → `{}` en cas d'échec (même garantie que `_scada_home_page_id`).
- **`_resolve_composed_page_ids(manifest, page_id) -> list[tuple[str, str]]`** — retourne une liste ordonnée de `(role, page_id)` parmi `header?`, `body`, `footer?`.
  - Si `page_id` absent du manifest, ou `_page_type(page) != "default"` → retourne `[("body", page_id)]` uniquement (couvre Fragment/Header/Footer et "page absente du manifest" par le même chemin, sans branchement supplémentaire).
  - Sinon, résout `HeaderPageId`/`FooterPageId` : la référence n'est retenue que si la page cible existe **et** que son `_page_type()` est exactement `"header"`/`"footer"` (miroir du validateur d'export `Ft100PackageValidation.cs::ValidatePageReference`). Référence invalide ou de mauvais type → ignorée silencieusement, jamais d'erreur.
- **`load_composed_page(static_root: Path, page_id: str) -> Optional[dict]`** — point d'entrée unique appelé par `views.py`. Pour chaque `(role, id)` résolu : lit `STATIC_ROOT/scada/pages/<id>/<id>.html` (fichier manquant pour le body demandé → `None`, i.e. 404 ; manquant pour un header/footer résolu → cette part est simplement omise) ; extrait le fragment, réécrit les URLs d'images, extrait le hash CSS et les dimensions. Calcule `width = max(...)` et `height = sum(...)` sur les parts présentes. Retourne :

```json
{
  "page_id": "win00009",
  "parts": [
    {"role": "header", "page_id": "win00001", "html": "...", "css_hash": "...", "width": 1920, "height": 80},
    {"role": "body",   "page_id": "win00009", "html": "...", "css_hash": "...", "width": 1920, "height": 900},
    {"role": "footer", "page_id": "win00002", "html": "...", "css_hash": "...", "width": 1920, "height": 40}
  ],
  "width": 1920,
  "height": 1020,
  "width_css": "1920px",
  "height_css": "1020px"
}
```

Contrat **uniforme** : `parts` contient toujours exactement une entrée `role: "body"` (la page demandée, composée ou non, `Default` ou `Fragment`), et 0 ou 1 entrée `header`/`footer` chacune. Pas de format alternatif pour le cas non composé — un seul contrat à maintenir et tester.

### `frontend/views.py`

`scada_package_page` devient un wrapper mince : vérifications auth/feature-flag (inchangées) → `load_composed_page(static_root, page_id)` → 404 si `None` → `JsonResponse(result)`. Les 4 helpers relocalisés sont supprimés d'ici (importés depuis le nouveau module si encore nécessaires ailleurs — vérifié : non).

### Template `templates/frontend/station/visualisation.html`

`#scada-host` (actuellement un seul `<div>` vide, entièrement remplacé par `host.innerHTML = data.html`) reçoit trois slots enfants persistants, créés dans le template :

```html
<div id="scada-host" class="scada-host" data-scada-home-page="..." ...>
  <div id="scada-host-header"></div>
  <div id="scada-host-body"></div>
  <div id="scada-host-footer"></div>
</div>
```

### `static/asset/js/station/visualisation_import.js` — `ScadaHost`

- Nouvel état : `currentHeaderPageId`, `currentBodyPageId` (remplace `currentPageId`), `currentFooterPageId` — initialisés à `null`.
- `loadPage(pageId)` :
  1. `fetch` la même URL qu'aujourd'hui, récupère `{page_id, parts, width, height, width_css, height_css}`.
  2. Pour chaque rôle (`header`, `body`, `footer`) :
     - Le body est **toujours** remplacé.
     - Header/footer ne sont remplacés (DOM du slot + injection CSS si hash non déjà dans `currentCssHashes` + `ScadaRuntime.initPage(slotEl, part.page_id)`) que si la `part` présente a un `page_id` différent de `current<Role>PageId`.
     - Si la `part` du rôle est absente de la réponse alors qu'un id était précédemment affiché → vider le slot, remettre `current<Role>PageId` à `null`. Pas de nettoyage des `<link>` CSS déjà injectés (CSS strictement scopée à `#ft100-<pageId>`, un stylesheet orphelin est inoffensif — même convention que l'existant).
  3. `applyRuntimeDimensions(data.width_css, data.height_css, data.width, data.height)` — signature inchangée, alimentée par les totaux composés.
- `_createPopup(pageId, options)` : **aucun changement de comportement popup**. Seul changement mécanique : lit `data.parts[0]` (toujours l'unique entrée `role: "body"` pour une page `Fragment`) au lieu de `data.html`/`data.css_hash` au niveau racine, pour rester compatible avec le contrat de réponse désormais uniforme.

### Diagramme de flux (navigation)

```
loadPage(pageId)
  → GET /visualisation/scada/page/<pageId>/
  → views.scada_package_page → scada_builder_composition.load_composed_page
       → _load_manifest(STATIC_ROOT)
       → _resolve_composed_page_ids(manifest, pageId)
       → pour chaque (role, id): lire STATIC_ROOT/scada/pages/<id>/<id>.html, extraire fragment+css+dims
       → composer width/height (max/sum sur les parts présentes)
  ← JSON {page_id, parts[], width, height, width_css, height_css}
  → body: remplacé toujours
  → header/footer: remplacés seulement si part.page_id != current<Role>PageId, sinon inchangés
```

## Gestion des erreurs

| Cas | Comportement |
|---|---|
| `manifest.json` absent ou invalide | Pas de composition, fallback page seule (`parts = [{"role": "body", ...}]`), jamais de 500 |
| Page demandée absente du manifest | Idem (même chemin que "page non `default`") |
| Page demandée de type `fragment`/`header`/`footer` | Jamais composée, même si `HeaderPageId`/`FooterPageId` renseignés dans son entrée manifest |
| `HeaderPageId`/`FooterPageId` pointant vers une page inexistante | Référence ignorée silencieusement |
| `HeaderPageId`/`FooterPageId` pointant vers une page du mauvais type | Référence ignorée silencieusement (miroir du validateur d'export) |
| Fichier HTML de la page demandée (`body`) introuvable | 404 (comportement actuel inchangé) |
| Fichier HTML d'un header/footer résolu introuvable | Cette part omise de la réponse, pas d'erreur |

## Tests

- **`frontend/tests_scada_package.py`** — `test_page_endpoint_returns_requested_page_fragment` et les tests voisins (~lignes 332-391), déjà désynchronisés du comportement réel avant ce plan, sont réécrits pour le nouveau contrat `parts`-based.
- **Nouveau `frontend/tests_scada_page_composition.py`** — exerce `load_composed_page` sur une arborescence `STATIC_ROOT` de test :
  - page `default` sans header/footer → une seule part `body`.
  - page `default` avec header et footer valides → 3 parts, dimensions composées correctes (max largeur, somme hauteur).
  - page `default` avec un seul des deux (header seul, footer seul).
  - `HeaderPageId`/`FooterPageId` référençant une page inexistante → ignoré, pas d'exception.
  - `HeaderPageId` référençant une page qui n'est pas de type `header` → ignoré.
  - `manifest.json` absent → fallback page seule, pas de 500.
  - page de type `fragment` possédant (par erreur d'authoring) un `HeaderPageId` valide → toujours une seule part `body`, jamais composée.

## Hors scope (explicitement)

- **`_createPopup` ne gagne aucune capacité de composition.** Son seul changement est la lecture de `parts[0]` au lieu du format racine, imposée par l'unification du contrat de réponse — pas une décision produit sur les popups. L'implémentation des popups (positionnement à l'écran, attributs manquants, etc.) reste un chantier séparé et ultérieur.
- Ne touche pas `frontend/scada_package.py` ni `frontend/scada_projects.py` (chemin legacy, statut de dépréciation non tranché — voir `2026-07-08-tf100web-scada-deployment-bridge-fix.md` Task 0).
- Pas de cache de fragments composés : composition à la volée à chaque requête, cohérent avec le reste du flux `STATIC_ROOT` ("no restart required").
