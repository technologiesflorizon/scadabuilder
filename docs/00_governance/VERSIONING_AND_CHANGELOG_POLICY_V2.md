# SCADA Builder V2 - Versioning And Changelog Policy

Date: 2026-06-16
Status: Active governance policy
Document version: `V2.1.5.0024`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-08-21 | `V2.1.5.0024` | `PENDING` | Boucle de fermeture des `PENDING` rendue obligatoire et outillee par `tools/docs/resolve-pending-commits.py`; exemption explicite des commits de bookkeeping et de `docs/10_generated/`. |
| 2026-07-15 | `V2.1.4.0034` | `b75f1d7` | Bump d'iteration pour l'implementation et la validation corrective de `DEC-0041`. |
| 2026-07-15 | `V2.1.4.0033` | `e811253` | Bump d'iteration pour l'approbation du contrat correctif Tableau/verrou. |
| 2026-07-15 | `V2.1.4.0032` | `ff21e33` | Bump d'iteration pour la specification et le plan de correction des regressions d'interaction Tableau/verrou. |
| 2026-07-15 | `V2.1.4.0031` | `e127190` | Bump d'iteration pour supprimer le clipping et la barre native du ruban secondaire. |
| 2026-07-15 | `V2.1.4.0030` | `5d762bb` | Bump d'iteration pour les corrections d'interaction verrou/Tableau et leur couverture. |
| 2026-07-15 | `V2.1.4.0029` | `bbca8fa` | Bump d'iteration pour la modernisation compacte du ruban de niveau 2 et sa regression. |
| 2026-07-15 | `V2.1.4.0028` | `c873744` | Bump d'itération pour corriger l'accessibilité et l'état partagé des surfaces Tableau/verrouillage déjà approuvées. |
| 2026-07-15 | `V2.1.4.0027` | `32a3ef6` | Bump d'itération pour la clôture automatisée des outils Tableau avancés et leur couverture. |
| 2026-06-16 | `V2.1.1.0039` | `2c5a0b4` | Creation de la politique de versioning documentaire et changelog pour la nouvelle architecture documentaire. |

## 1. Version Format

SCADA Builder V2 uses:

```text
V2.production.feature.iteration
```

Current version:

```text
V2.1.4.0033
```

## 2. Increment Rules

1. Iteration bump: documentation reorganization, small features, tests, UI polish, narrow bug fixes, and contract clarifications.
2. Feature bump: new module-level capability, new runtime contract family, or major workflow addition.
3. Production bump: production baseline change or breaking governance reset.

## 3. Changelog Rules

Every touched Markdown document must record:

1. Date.
2. Version.
3. Commit.
4. Change.

Use `PENDING` for uncommitted changes only. A placeholder whose introducing commit already exists is **stale debt**, not a valid entry.

Closing loop, mandatory after every commit that touched `docs/`:

```bash
python tools/docs/resolve-pending-commits.py --check   # liste la dette, sort 1 s'il en reste
python tools/docs/resolve-pending-commits.py --apply   # remplace par le hash court introducteur
```

Rules:

1. The resolver replaces a placeholder with the short hash of the commit that introduced the row, the decision metadata field or the report `Commit:` header.
2. Run it right after committing; ship the result in the next commit, or in a dedicated `docs: resolve pending commit placeholders` commit.
3. A bookkeeping commit that only resolves placeholders does **not** add changelog rows and does **not** bump `VERSION`; otherwise the debt regenerates itself at every pass.
4. `docs/10_generated/` is excluded: those files are rewritten by their generators, and editing them by hand makes `verify-docs` report a stale matrix.
5. `verify-docs.ps1` runs `--check` and reports every stale placeholder as an error. `-SkipPendingCheck` exists only for offline runs without Python.
6. Cross-repository hashes (TF100Web) stay manual: the resolver never invents a hash it cannot read from this repository's history.

## 4. Decision Linkage

Any changelog entry that modifies a contract should reference a decision id in the body of the document or in `DECISION_REGISTER_V2.md`.
