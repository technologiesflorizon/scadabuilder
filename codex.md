# Codex repository guidance

## Protected TF100Web contract

SCADA Builder V2 exports `.sb2` packages consumed by the modern TF100Web composition path. The verified reference implementation is the local repository `F:\Projet\Git\TF100Web`.

- Package root: `scada-builder-v2-ft100-package/`.
- Required package root file: `manifest.json`.
- Required page layout: `<page-id>/<page-id>.html`, with page CSS/assets in the page folder.
- Deployment layout: `STATIC_ROOT/scada/manifest.json` and `STATIC_ROOT/scada/pages/<page-id>/<page-id>.html`.
- Composition entry point: `frontend.views.scada_package_page` → `frontend.scada_builder_composition.load_composed_page`.
- Composition resolves manifest fields `Pages`, `Id`, `IncludeInBuild`, `PageType`/`Type`, `HeaderPageId`, and `FooterPageId`, extracts `ft100-<page-id>`, and reads CSS hash/dimensions from HTML.
- Element+ HTML and CSS are opaque to the modern TF100Web composition path. It does not parse or reinterpret `ScadaElementStyle`, `data-scada-state-config`, or arbitrary CSS.
- `_inject_scada_element_attrs` may add runtime binding attributes from manifest metadata; it must not overwrite or semantically reinterpret Element+ style declarations.
- Manifest/project JSON follows the existing .NET/PascalCase contract. Runtime JSON embedded in HTML attributes follows Builder’s camelCase contract.
- New style properties must be fully emitted by Builder and remain intact through TF100Web deployment/composition. No TF100Web code change is assumed for CSS-only style additions.
- Exported scripts are not automatically considered executed by TF100Web fragment intake. Distinguish exporter output from host-executed behavior.

When changing exporter output, verify the matching TF100Web implementation/tests and update `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md` for any contract change.
