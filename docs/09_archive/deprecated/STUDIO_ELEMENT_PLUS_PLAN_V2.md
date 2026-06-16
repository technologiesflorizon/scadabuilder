# SCADA Builder V2 - Studio Element+ Plan

Date: 2026-06-15
Status: Approved direction; modernization roadmap added
Document version: `V2.1.1.0037`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0037` | `90c108b` | Ajout de la roadmap Studio Element+: modernisation d'elements, jeu CSS et effets visuels evenementiels. |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.1.0.0000` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Objective

Studio Element+ is a separate application dedicated to creating modern Element+ components from selected legacy geometry.

This is not a linear conversion path. Polygon, line, and composed graphic selections must be treated as source material for a new modern element, not as a direct one-to-one scene conversion.

SCADA Builder V2 remains the scene/project editor.

Studio Element+ becomes the component authoring application.

## 2. Product Boundary

Studio Element+ must be a real second application.

Final installer rule:

1. SCADA Builder V2 can be installed alone.
2. Studio Element+ can be installed as an optional companion tool.
3. Shared domain and application libraries may be reused.
4. UI, executable, and installer feature selection remain separate.

Target solution shape:

```text
src/
  ScadaBuilderV2.App/
  ScadaBuilderV2.ElementStudio.App/
  ScadaBuilderV2.Domain/
  ScadaBuilderV2.Application/
  ScadaBuilderV2.Infrastructure/
```

## 3. Launch Workflow

From SCADA Builder V2:

1. User selects legacy polygon, line, shape, text, or a mixed selection.
2. User opens the right-click context menu.
3. Context command appears when the selection is valid:

```text
Ouvrir dans Studio Element+
```

4. SCADA Builder V2 creates an import package.
5. Studio Element+ is launched as a separate application process.
6. Studio Element+ opens a workspace containing only the selected source elements.

SCADA Builder V2 must not hide, repaint, replace, or convert the selected legacy elements when launching Studio Element+.

## 4. Exchange Package

The bridge between SCADA Builder V2 and Studio Element+ is a file package, not an in-memory-only message.

Initial storage:

```text
projects/<project-id>/.studio/imports/
```

The package is temporary but versionable and inspectable for debugging.

Initial internal format may be JSON, but the file extension is the official product extension:

```text
*.ft1
```

Rationale:

1. `.ft1` is the SCADA Builder V2 export/transfer format used to open selected material in Studio Element+.
2. Internally it can contain clear JSON during development.
3. The content may be encoded or protected later without changing the SCADA Builder V2 -> Studio Element+ workflow.

Studio working format:

```text
*.sep
```

Rules:

1. `.sep` means **Studio Element Plus**.
2. `.sep` is the editable Studio project/component source file.
3. `.sep` must be distinguishable from temporary `.ft1` transfer packages.
4. A `.sep` file must package everything required to reopen and continue editing the Element+ out of the box.
5. A `.sep` file contains exactly one Element+ component.
6. A `.sep` file must include embedded image assets, cleaned SVG/HTML payloads, metadata, traceability, and future event placeholders.
7. The Studio workzone/canvas/viewport is an editor surface and must not be exported as part of the Element+ payload.
8. `.sep` is the editable Studio source of truth.
9. `.ft1` is the SCADA Builder V2 -> Studio Element+ transfer/export format, not the reusable library format.
10. Legacy imports are source material only and must not remain as permanent non-destructive legacy layers in the final `.sep`.

## 5. Package Model

Target transfer object:

```csharp
public sealed class ElementStudioImportPackage
{
    public string PackageId { get; init; }
    public string SourceProjectId { get; init; }
    public string SourceSceneId { get; init; }
    public string SourcePagePath { get; init; }
    public SceneBounds Bounds { get; init; }
    public IReadOnlyList<ElementStudioLegacyItem> Items { get; init; }
}
```

Target item:

```csharp
public sealed class ElementStudioLegacyItem
{
    public string SourceElementId { get; init; }
    public string SourceName { get; init; }
    public string LegacyType { get; init; }
    public SceneBounds BoundsAbsolute { get; init; }
    public SceneBounds BoundsRelativeToPackage { get; init; }
    public string? Geometry { get; init; }
    public string? Text { get; init; }
    public ElementStudioStyleSnapshot Style { get; init; }
    public int ZIndex { get; init; }
    public string? RawMetadataJson { get; init; }
}
```

The package must preserve enough source data to rebuild a modern element without depending on the active WebView DOM state after launch.

## 6. Studio UI Direction

Studio Element+ should use the same visual language as SCADA Builder V2.

The first Studio workspace must render the imported legacy source before conversion. This is a source review layer, not a modernized Element+ layer.

Initial layout:

1. Top ribbon: `Fichier`, `Edition`, `Vue`, `Element+`, `Exporter`.
2. Left panel: drawing and cleanup tools for creating SVG/image-based industrial icons.
3. Center: isolated component scene.
4. Right panel: `Sources`, `Structure`, `Proprietes`, and `Composant` tabs.
5. Bottom panel: diagnostics, conversion notes, and validation messages.

Initial operations:

1. Display imported legacy source elements.
2. Select and multi-select source items.
3. Group and ungroup.
4. Move source items in the Studio workspace.
5. Rename objects.
6. Create an Element+ component definition.
7. Save the result as `.sep`.

Initial rendering rule:

1. Use WebView2 for the legacy source layer.
2. Render `LegacyMarkup` from the `.ft1` package.
3. Keep WPF shapes as diagnostic overlays only.
4. Do not modernize geometry during import.

## 7. Component Output

Studio Element+ saves editable modern component sources into the shared Studio Element+ / SCADA Builder V2 library:

```text
projects/<project-id>/library/elements/
```

Output examples:

```text
pump_symbol_001.sep
valve_status_001.sep
```

The `.sep` output is the editable Element+ component source file. During development it can contain a JSON model equivalent to `.elementplus.json`, but the product-facing Studio source extension is `.sep`.

The `.ft1` extension remains the transfer package used when SCADA Builder V2 exports selected legacy material toward Studio Element+.

Element+ component outputs may be:

1. **Typed UI elements**: text, numeric display, input, button, indicator.
2. **Vector graphic components**: packaged SVG made from lines, polylines, polygons, paths, text, groups, and styles.
3. **Image-backed components**: raster image assets with metadata, bounds, events, and bindings.
4. **Composite industrial objects**: reusable symbols such as piping, valves, tanks, motors, pump assemblies, status panels, and other component groups.

The target model must not force every legacy polygon or line to become a separate scene object in SCADA Builder V2. A selected legacy piping assembly may become one reusable Element+ component whose internal renderer is SVG.

Initial Element+ component output shape:

```csharp
public sealed class ElementPlusComponent
{
    public string Id { get; init; }
    public string Name { get; set; }
    public string Category { get; set; }
    public SceneBounds Bounds { get; set; }
    public ElementPlusVisualModel Visual { get; set; }
    public IReadOnlyList<ElementPlusEventBinding> Events { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
}

public sealed class ElementPlusVisualModel
{
    public string VisualKind { get; init; } // Svg, Image, Html, Composite
    public string? SvgMarkup { get; set; }
    public string? ImageAssetPath { get; set; }
    public string? HtmlMarkup { get; set; }
    public string? CssCode { get; set; }
    public string? JsCode { get; set; }
}
```

SVG component rule:

1. Studio Element+ may preserve cleaned SVG as the canonical visual payload.
2. SVG payload must be normalized to component-local coordinates.
3. Legacy editor artifacts, selection rectangles, handles, and temporary outlines must be removed before save.
4. The SVG can remain internally composed of many primitives while the scene treats it as one Element+ object.
5. Initial events and bindings attach to the Element+ component as a whole.
6. Named internal parts are preserved for later editing and variants, not for first-slice event targeting.
7. Imported legacy ids may be preserved as source metadata, but Studio should create cleaner internal names during import or cleanup.

Image component rule:

1. Image-backed Element+ components must embed image data in the `.sep` package.
2. External image references are not acceptable for the portable component format.
3. The project library may cache extracted image previews, but the `.sep` remains self-contained.

## 8. Replacement Workflow

Short term:

1. Studio Element+ saves a reusable `.sep` component source file.
2. SCADA Builder V2 does not automatically replace the source selection.
3. User manually inserts the Element+ component later.

Medium term:

1. Studio Element+ exposes `Publier dans la librairie`.
2. SCADA Builder V2 reloads or watches the library.
3. User can insert the new Element+ component in the scene.

Later:

1. Studio Element+ exposes `Creer et remplacer dans la scene`.
2. SCADA Builder V2 inserts the new Element+ at the original package position.
3. SCADA Builder V2 hides/removes the source legacy items only after explicit replacement.
4. Undo restores the legacy source view during the session.

## 9. Guardrails

1. Opening Studio Element+ must not be treated as Conversion Element+.
2. Legacy source rendering must remain unchanged in SCADA Builder V2 until explicit replacement.
3. Direct polygon/line conversion in SCADA Builder V2 should remain blocked or routed to Studio Element+.
4. Studio Element+ owns composed graphic cleanup and modern component creation.
5. SCADA Builder V2 owns scene placement, page editing, FT100 context, and project persistence.

## 10. Anti-Regression Contract

The Studio Element+ slice is protected by tests that verify the product contract without requiring `src/**` changes in this documentation/testing pass.

Selection decisions are governed by `docs/STUDIO_ELEMENT_PLUS_SELECTION_DECISIONS_V2.md`. Studio Element+ selection work must preserve the canonical modifier contract: `Shift + clic` adds to the current selection and `Alt + clic` removes from the current selection.

Required contracts:

1. `.ft1` remains the SCADA Builder V2 -> Studio Element+ transfer package.
2. `.ft1` packages are written under `projects/<project-id>/.studio/imports/` and use the `scada-builder-v2.element-studio.import` schema.
3. `.sep` remains the shared Studio Element+ / SCADA Builder V2 library source format under `projects/<project-id>/library/elements/`.
4. One `.sep` file contains exactly one Element+ component.
5. The right context panel exposes the `Element` tab for imported source items.
6. Selecting imported items in the `Element` list updates the Studio workzone highlight state.
7. WebView source clicks can drive the same Studio selection model.
8. Drawing tools are declared as component-authoring tools, not overlay tools.
9. Drawing output must become real component primitives or embedded component assets in the Element+ model.
10. Workzone/editor state, zoom, pan, selection rectangles, handles, diagnostics, and UI overlays must not be exported as component geometry.
11. Legacy imports are source material only; they must not remain as permanent non-destructive legacy layers in the final `.sep`.

Associated regression coverage:

```text
tests/ScadaBuilderV2.Tests/ElementStudioImportPackageWriterTests.cs
tests/ScadaBuilderV2.Tests/ElementStudioSourceRenderingTests.cs
tests/ScadaBuilderV2.Tests/StudioElementPlusContractTests.cs
```

## 11. Roadmap - Element Modernization, CSS Properties, And Event Effects

This roadmap is future development, not implemented behavior in the current slice.

Studio Element+ must evolve from a source-isolation/editor tool into a modernization workspace that can improve selected material before publishing an Element+ component.

Modernization goals:

1. Rework selected source material into cleaner modern Element+ visuals rather than only wrapping the legacy HTML/SVG.
2. Support enhancement of images, SVG shapes, forms, and composed industrial symbols.
3. Preserve the promising preliminary SCADA Builder V1 experiments as product direction, while rebuilding the workflow on the V2 `.ft1`/`.sep` contracts.
4. Offer tools to clean geometry, simplify shapes, normalize fills/strokes, replace low-quality images, and compose reusable industrial objects.
5. Keep all generated output as real Element+ component primitives, embedded assets, or component CSS in the `.sep` model.
6. Never export workzone overlays, selection rectangles, handles, diagnostics, or temporary helper layers as component geometry.

CSS property roadmap shared with SCADA Builder V2:

1. Geometry and layout: `left`, `top`, `width`, `height`, `min/max`, `box-sizing`, overflow, clipping, and transform origin.
2. Fill, stroke, and border: background color/image, stroke color, stroke width, border, border radius, line style, and opacity.
3. Typography: font family, size, weight, alignment, line height, wrapping, and text color.
4. Visual depth and filters: shadow, glow-ready shadow tokens, blur/filter, brightness, contrast, saturation, and blend mode when supported.
5. Interaction/state properties: cursor, pointer behavior, visibility, disabled/locked visual state, and runtime state classes.
6. Effect tokens: blink, glow, pulse, alarm highlight, and degraded visual treatment.

Event visual effect roadmap:

1. Studio Element+ can author reusable visual effect presets on a component.
2. SCADA Builder V2 can bind those effects to object events, tag conditions, or generated page lifecycle events.
3. Exported effects must be page-namespaced and must not generate package-global selectors.
4. Effects are runtime/component behavior, not editor overlay state.
5. Effects must remain deterministic so preview, save/reload, and TF100 export use the same model.

Guardrails:

1. Selection, hit-testing, deletion, and movement remain governed by `STUDIO_ELEMENT_PLUS_SELECTION_DECISIONS_V2.md`.
2. Every present source or Element+ object remains selectable according to the polymorphic selection contract.
3. Modernization does not automatically delete source elements in SCADA Builder V2; replacement remains explicit and undoable.
4. Source deletion still goes through the global scene history and `RemovedSourceElementIds`, never through durable CSS masking.

## 12. Implementation Plan

Phase 1 - Contract and launcher:

1. Add `ElementStudioImportPackage` domain/application models.
2. Add `.ft1` package writer in infrastructure.
3. Add `Ouvrir dans Studio Element+` context command for eligible legacy selections.
4. Write selected legacy items to `projects/<project-id>/.studio/imports/<package-id>.ft1`.
5. Launch the Studio executable with the package path argument.
6. During development, verify that the launched Studio process remains alive; fall back to project launch when the executable exits immediately.
7. Keep normal selection/inventory messages lightweight; capture full legacy markup only for the Studio package export.
8. Write launch diagnostics under `.studio/logs` so failures in the second application are visible outside the transient WPF status bar.
9. The SCADA Builder V2 status-bar diagnostics button must expose the recent Studio launch log to the user.
10. A Studio launch is successful only when a visible `Studio Element+` WPF window is detected.
11. Read-only Studio property-panel fields must use explicit one-way bindings.
12. Legacy SVG geometry must be rendered in an SVG layer with a package-bounds `viewBox`; raw SVG elements must not be injected into HTML-only wrappers.
13. The Studio workzone must size from the imported package bounds, not a fixed full-scene canvas.
14. Source selection is owned by Studio and must not import SCADA Builder selection rectangles or editor-only outlines as component geometry.
15. The first `Creer SVG` action creates an in-memory SVG Element+ component draft before library persistence is implemented.

Phase 2 - Studio app skeleton:

1. Add `ScadaBuilderV2.ElementStudio.App`.
2. Parse the `.ft1` package argument.
3. Render an isolated scene with imported items.
4. Add imported element list and properties panel.
5. Add basic selection and multi-selection.
6. Make the Studio workspace a real component scene that can temporarily hold multiple imports while authoring one component.
7. Save and reopen exactly one Element+ component per `.sep` file.

Phase 3 - Component authoring:

1. Add group/ungroup inside Studio.
2. Add component metadata: name, category, description, tags.
3. Add `Creer composant SVG Element+` from selected source geometry.
4. Normalize selected SVG geometry into component-local coordinates.
5. Add save-as `.sep` component source output.
6. Add validation diagnostics.
7. Add optional image-backed component output.
8. Add event/binding placeholders on the component model.
9. Make drawing tools functional:
   - `Selection`: select, multi-select, move, inspect, and edit existing parts.
   - `Ligne`: create line primitives.
   - `Polyline`: create editable polyline primitives.
   - `Rectangle`: create editable rectangle primitives.
   - `Polygone`: create editable polygon primitives.
   - `Image`: import and embed raster images into the `.sep`.
10. Ensure created primitives become part of the Element+ component model, not editor-only overlays.
11. Ensure the `.sep` contains the complete packaged Element+ source and can be reopened without external files.
12. Ensure the workzone, zoom level, pan position, selection rectangle, and editor UI overlays are not exported as component geometry.

Phase 4 - SCADA Builder integration:

1. Add library refresh.
2. Add insert Element+ from library.
3. Add optional replace-source workflow.

## 13. Open Technical Questions

1. How much raw geometry can be extracted reliably from the current legacy HTML/SVG output?
2. Should the first Studio scene render imported items from extracted geometry or from a clipped legacy DOM snapshot?
3. Should `.sep` packages use plain JSON with a schema version first, or a zip/container layout immediately?
4. How should Studio auto-generate clean internal names during import while preserving source legacy traceability?
5. Should a visual-only Element+ component be allowed to contain no FT100 binding at all?
6. Which Studio Element+ modernization tools from the SCADA Builder V1 experiments should be rebuilt first in V2?
7. Which CSS properties are safe for the first shared property metadata set across SCADA Builder V2 and Studio Element+?
8. Which visual effects must be pure CSS classes and which require generated runtime JavaScript?

Default recommendation:

1. Start with plain JSON inside `.sep`.
2. Include schema/version metadata from day one.
3. Keep the package readable until the model stabilizes.
4. Start with SVG-backed components for legacy piping and composed graphics.
5. Add component-level events first; named internal SVG part events can follow once selection/naming tools exist in Studio.
6. Embed image assets in `.sep` to preserve component portability.
7. Enforce one Element+ component per `.sep` file.
