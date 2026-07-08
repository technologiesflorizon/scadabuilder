# TF100Web — Restaurer la composition header/body/footer du flux SCADA statique

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restaurer la composition header + body + footer pour le flux SCADA `STATIC_ROOT`-based (`frontend.views.scada_package_page` → `ScadaHost.loadPage()`), qui existait dans l'ancien flux manifest-parsing (`frontend/scada_package.py:load_scada_builder_package`) mais a été perdue quand `scada_package_page` a été réécrite pour lire directement `STATIC_ROOT/scada/pages/<page_id>/` (commit `e86a450`, « feat: serve SCADA pages from static files »).

**Contexte découvert (2026-07-08, session de test sur le poste d'usine, branche `feature/element-plus-state-command-events`) :** après le fix de déploiement (voir `2026-07-08-tf100web-scada-deployment-bridge-fix.md`), l'utilisateur a signalé que le header et le footer de la page SCADA exportée (`win00009`) n'apparaissent plus — uniquement le corps de la page s'affiche. Diagnostic confirmé par lecture de code (pas de reproduction navigateur nécessaire) :

- `frontend/scada_package.py:load_scada_builder_package()` (lignes 323-393) implémente déjà la composition : pour la page sélectionnée, elle résout `HeaderPageId`/`FooterPageId` (des champs par-page dans `manifest.json`) via `pages_by_id`, charge les trois fragments (`_load_page_fragment`), les concatène (`"\n".join(fragments)`), fusionne leurs URLs CSS, et calcule des dimensions composées (`_composed_dimensions` — largeur max, hauteur sommée). C'est le flux original : « header + body + footer, et au changement de page le fragment du body était rechargé en async » (citation utilisateur) — en réalité chaque navigation recompose header+body+footer en un seul appel, pas de persistance DOM séparée du header/footer.
- `frontend.views.scada_package_page` (la vue actuelle, branchée sur `STATIC_ROOT`, ajoutée par `e86a450` puis étendue par cette session) ne fait **aucun** appel à cette logique : elle lit directement `STATIC_ROOT/scada/pages/<page_id>/<page_id>.html`, extrait le fragment `ft100-<page_id>` de **cette seule page**, et retourne `{page_id, html, css_hash, width, height, width_css, height_css}` — un seul hash CSS, un seul fragment, sans jamais lire `manifest.json` pour trouver `HeaderPageId`/`FooterPageId`.
- Corollaire : `frontend/tests_scada_package.py` a une classe de test (`test_page_endpoint_returns_requested_page_fragment` et voisines, ~lignes 332-391) qui appelle encore `views.scada_package_page(...)` en attendant l'**ancienne** forme de réponse (`title`, `css_urls` pluriel, `actions`, `width_css`/`height_css` toujours vides dans le test) — ce test est déjà cassé/obsolète indépendamment de ce plan (constaté mais non corrigé pendant la session du 2026-07-08 bridge-fix, hors scope à ce moment-là). Ce plan doit le réconcilier : soit le mettre à jour pour la nouvelle forme composée, soit le remplacer.

**Architecture :** Réutiliser les fonctions pures déjà existantes dans `scada_package.py` (`_compiled_pages`, `_page_id`, `_page_type`, `_composed_dimensions`) plutôt que de les réécrire pour le flux `STATIC_ROOT`. Étendre `scada_package_page` pour :
1. Lire `STATIC_ROOT/scada/manifest.json` (déjà déployé par `deploy_package_to_static`).
2. Résoudre `HeaderPageId`/`FooterPageId` de la page demandée via le manifest.
3. Pour chaque page composée (header?, body, footer?), lire son fichier `STATIC_ROOT/scada/pages/<id>/<id>.html`, extraire son fragment et réécrire ses URLs d'image (réutiliser `_extract_html_element_by_id` et `_rewrite_scada_page_image_urls`, déjà en place).
4. Concaténer les fragments, collecter les hash CSS de chacun (pluriel), composer les dimensions (max largeur, somme hauteur — même règle que `_composed_dimensions`, mais à partir des attributs HTML `data-scada-width`/`data-scada-height` déjà lus par `_extract_page_dimension_from_html`, pas des champs manifest `Width`/`RequiredDisplayWidth` que `scada_package.py` utilise — cohérence à trancher, voir Task 1 Step 2).
5. Adapter `ScadaHost.loadPage()` (JS) pour injecter plusieurs `<link>` CSS au lieu d'un seul.

**Tech Stack:** Python 3 / Django (TF100Web), vanilla JS (`visualisation_import.js`), Django test framework.

## Global Constraints

- **Repo :** `F:\Projet\Git\TF100Web`, branche `feature/element-plus-state-command-events`.
- Ne pas toucher à `frontend/scada_package.py` ni `frontend/scada_projects.py` (chemin legacy, hors scope — cf. `2026-07-08-tf100web-scada-deployment-bridge-fix.md` Task 0 : leur statut de dépréciation est déjà non tranché, ne pas ajouter de couplage supplémentaire dessus). Réutiliser leurs fonctions **pures** (`_compiled_pages`, `_page_type`, etc.) par import si elles s'appliquent telles quelles à un dict manifest ; sinon dupliquer une version minimale côté `views.py` plutôt que de coupler les deux modules davantage.
- KISS : la composition doit rester une opération à la volée par requête (pas de cache de fragments composés), cohérente avec le reste du flux `STATIC_ROOT` (« no restart required »).
- Ne pas changer le comportement pour les pages **sans** `HeaderPageId`/`FooterPageId` (la majorité probable) : `scada_package_page` doit rester équivalente à aujourd'hui dans ce cas (un seul fragment, un seul hash CSS) — pas de régression de format de réponse pour ce cas courant si possible, ou alors migration de format assumée et documentée avec le fix JS correspondant.
- Question ouverte à trancher en Task 1 : les popups (`ScadaHost._createPopup`, `OpenPopup`/`TogglePopup`) doivent-ils aussi composer header/footer, ou seulement la navigation plein-écran (`loadPage`) ? Vérifier si l'exporteur permet un `HeaderPageId`/`FooterPageId` sur une page de type popup avant de décider.

## File Structure

- **Modify:** `frontend/views.py` — `scada_package_page()` étendu pour lire le manifest et composer header/body/footer ; nouvelle fonction utilitaire (nom à définir, ex. `_resolve_composed_pages(manifest, page_id)`).
- **Modify:** `static/asset/js/station/visualisation_import.js` — `ScadaHost.loadPage()` (et potentiellement `_createPopup()` selon la décision Task 1) pour injecter plusieurs `<link>` CSS.
- **Modify or Replace:** `frontend/tests_scada_package.py` — réconcilier `test_page_endpoint_returns_requested_page_fragment` et les tests voisins (~lignes 332-391) avec la forme de réponse réellement produite aujourd'hui (déjà désynchronisée avant ce plan) et avec la nouvelle composition.
- **Create:** tests couvrant la composition header+body+footer dans `frontend/tests_scada_deploy.py` ou un nouveau fichier `frontend/tests_scada_page_composition.py`.

---

## Task 1: Décider et implémenter la résolution header/body/footer côté serveur

**Files:**
- Modify: `frontend/views.py`

- [ ] **Step 1: Vérifier si les popups peuvent avoir un `HeaderPageId`/`FooterPageId`**

Inspecter `SCADA_BUILDER_V2/src/ScadaBuilderV2.Rendering/Ft100SceneExporter.cs` (génération du manifest, champ `HeaderPageId`/`FooterPageId` par page) et `docs/03_runtime_contracts/` pour confirmer si un popup peut légitimement en avoir. Si oui, la composition doit s'appliquer uniformément (même fonction utilisée par navigation et popup). Si non/rare, restreindre la composition à `ScadaHost.loadPage()` (navigation plein-écran) et laisser `_createPopup()` inchangé (page unique).

- [ ] **Step 2: Choisir la source de vérité pour les dimensions composées**

Décider entre (a) lire `Width`/`Height` depuis `manifest.json` par page (comme `scada_package.py::_composed_dimensions`), ou (b) sommer les attributs `data-scada-width`/`data-scada-height` déjà extraits de chaque fragment HTML composé (cohérent avec le reste de la vue `STATIC_ROOT`-based, qui ne s'appuie sur aucun champ manifest pour les dimensions aujourd'hui). Recommandation : (b), pour ne pas introduire une deuxième source de vérité dans le même flux.

- [ ] **Step 3: Implémenter la résolution des pages composées**

Dans `frontend/views.py`, ajouter une fonction qui lit `STATIC_ROOT/scada/manifest.json` (mêmes garanties d'erreur que `_scada_home_page_id` : `try/except (OSError, ValueError)`, fallback sur page seule si le manifest est absent/invalide), trouve la page demandée dans `manifest["Pages"]`, résout `HeaderPageId`/`FooterPageId` s'ils existent et pointent vers des pages valides du manifest, et retourne la liste ordonnée `[header?, body, footer?]` (ids de page).

- [ ] **Step 4: Étendre `scada_package_page` pour composer les fragments**

Pour chaque id de page composée : lire `STATIC_ROOT/scada/pages/<id>/<id>.html`, extraire le fragment (`_extract_html_element_by_id`), réécrire les images (`_rewrite_scada_page_image_urls`), extraire son hash CSS (`_extract_css_hash_from_html`) et ses dimensions (`_extract_page_dimension_from_html`). Concaténer les fragments dans l'ordre header→body→footer. Décider du format de réponse JSON (liste de hash CSS vs un seul si non composé — éviter de casser le cas non composé si possible, cf. Global Constraints).

- [ ] **Step 5: Tests unitaires de la composition**

Couvrir : page sans Header/FooterPageId (réponse inchangée par rapport à aujourd'hui), page avec les deux, page avec un seul des deux, référence `HeaderPageId`/`FooterPageId` invalide (page inexistante — ne doit pas planter, juste ignorer), manifest absent (fallback gracieux, pas de 500).

---

## Task 2: Adapter `ScadaHost.loadPage()` pour plusieurs feuilles de style

**Files:**
- Modify: `static/asset/js/station/visualisation_import.js`

- [ ] **Step 1: Gérer un tableau de hash CSS au lieu d'un seul**

Adapter la boucle d'injection de `<link>` (actuellement un seul `if (data.css_hash && !this.currentCssHashes.has(data.css_hash))`) pour itérer sur plusieurs hash si le format de réponse choisi en Task 1 Step 4 est une liste. Garder `currentCssHashes` comme `Set` pour éviter les doublons entre pages composées qui partageraient une même feuille.

- [ ] **Step 2: Vérifier `applyRuntimeDimensions` avec les dimensions composées**

Confirmer que les valeurs `width`/`height`/`width_css`/`height_css` composées (Task 1 Step 2) donnent un rendu correct de `--ft100-scada-width`/`--ft100-scada-height` pour le conteneur `#scada-host` empilant header+body+footer verticalement (ou horizontalement selon la convention de `scada_package.py::_composed_dimensions` — largeur max, hauteur sommée : à vérifier que c'est bien un empilement vertical dans le canevas SCADA Builder V2, pas une supposition).

---

## Task 3: Réconcilier les tests obsolètes

**Files:**
- Modify: `frontend/tests_scada_package.py`

- [ ] **Step 1: Mettre à jour ou remplacer `test_page_endpoint_returns_requested_page_fragment` et les tests voisins**

Ces tests (lignes ~332-391 au 2026-07-08) attendent une forme de réponse (`title`, `css_urls` pluriel, `actions`, `SCADA_IMPORT_ROOT`) qui ne correspond ni à l'ancienne vue `STATIC_ROOT`-based (avant ce plan) ni à aucune version actuellement testée avec succès — déjà cassés avant ce plan. Les réécrire pour couvrir la vue réellement en place après Task 1/2.

---

## Task 4: Vérification bout-en-bout

- [ ] **Step 1:** Déployer (CLI ou admin) un `.sb2` dont au moins une page a `HeaderPageId` et `FooterPageId` renseignés dans son manifest.
- [ ] **Step 2:** Ouvrir `/visualisation/`, confirmer que le header et le footer de la scène s'affichent au chargement initial et après une navigation entre pages (`ScadaHost.loadPage`).
- [ ] **Step 3:** Confirmer qu'une page **sans** header/footer continue de fonctionner exactement comme avant ce plan (pas de régression sur le cas courant).
- [ ] **Step 4:** Si un check échoue, retour à Systematic Debugging (Phase 1) plutôt qu'un patch ad hoc.
