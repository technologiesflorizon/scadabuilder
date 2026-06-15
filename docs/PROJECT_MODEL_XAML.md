# SCADA Builder V2 - Project Model XAML

Date: 2026-06-11
Status: Draft specification
Document version: `V2.1.1.0022`
Previous planning version: `V2.0.1.0000`
Bump type: iteration
Reason: clarification that pages do not own actions and that object event bindings trigger runtime action definitions for Django/readable manifests.

## 1. Purpose

`project.xaml` is the main SCADA Builder V2 project contract.

It must describe the project identity, structure, responsive strategy, scenes/pages, assets, FT100 mapping placeholders, object-triggered action definitions, scripts, validation state, and build options without becoming a monolithic dump of every scene element.

Principles:

1. `project.xaml` is the root file opened by the editor.
2. The file is readable, versionable, and stable across normal editor operations.
3. Large scene content, generated output, logs, backups, and imported raw legacy data stay in separate files.
4. The preview and build pipeline must consume the same project model.
5. Legacy traceability is preserved, especially for `AMR_REF_SCADA`, but legacy material is not the official project model.
6. FT100 integration can start as placeholders and evolve without rewriting scene layouts.

## 2. Project Directory

Initial project structure:

```text
MyScadaProject/
  project.xaml
  scenes/
    win00008.scene.xaml
    win00008/
      desktop.layout.xaml
      tablet.layout.xaml
      mobile.layout.xaml
  assets/
    images/
    icons/
    symbols/
    modernized/
  libraries/
    components/
    styles/
  mappings/
    ft100.placeholders.xaml
    ft100.imported.xaml
  scripts/
    project/
    scenes/
  exports/
    ft100-web/
  backups/
  logs/
  references/
    legacy/
  modernization/
    candidates/
    comparisons/
```

Rules:

1. `project.xaml` owns project identity and indexes the files that belong to the project.
2. `scenes/*.scene.xaml` owns logical scene content: elements, groups, bindings, actions, scripts, and legacy references.
3. `scenes/<scene-id>/*.layout.xaml` owns responsive layout variants for the same logical scene.
4. `assets/` contains project-owned visual files that may be edited, copied, or exported.
5. `references/legacy/` contains read-only or copied legacy source material.
6. `modernization/` contains extraction candidates, comparison sessions, and audit material that help convert legacy sources into V2 scenes.
7. `exports/`, `logs/`, and `backups/` are generated or operational folders and are not source-of-truth model files.

## 3. Root XAML Shape

Recommended root shape:

```xml
<ScadaProject
    SchemaVersion="2.0"
    ProductVersion="V2.0.1.0000"
    Id="amr-ref-scada-v2"
    Name="Modernized AMR SCADA"
    CreatedUtc="2026-05-29T00:00:00Z"
    ModifiedUtc="2026-05-29T00:00:00Z">

  <Identity />
  <SourceReferences />
  <WorkspaceDefaults />
  <Responsive />
  <DevicePresets />
  <Assets />
  <Libraries />
  <Scenes />
  <Mappings />
  <Scripts />
  <Build />
  <Validation />
</ScadaProject>
```

Required attributes:

1. `SchemaVersion`: XAML schema version, independent from product version.
2. `ProductVersion`: SCADA Builder V2 version that last wrote the model.
3. `Id`: stable project id.
4. `Name`: user-facing project name.
5. `CreatedUtc` and `ModifiedUtc`: ISO 8601 UTC timestamps.

## 4. Identity

`Identity` stores user-facing metadata and must not depend on paths.

```xml
<Identity
    DisplayName="Modernized AMR SCADA"
    Customer="AMR"
    Site=""
    Description="Reference project migrated toward SCADA Builder V2" />
```

Rules:

1. `DisplayName` is the label shown in the Project panel.
2. Internal ids must not be shown as object names unless no user-facing label exists.
3. Empty fields are allowed during early migration.

## 5. Source References

`SourceReferences` records external origins without giving the editor permission to modify them.

```xml
<SourceReferences>
  <Reference
      Id="amr-ref-scada"
      Kind="LegacyModernizedProject"
      Role="LegacyExtractionSource"
      Path="F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER\AMR_SCADA\AMR_REF_SCADA"
      ReadOnly="true" />
  <Reference
      Id="amr-ref-scada-backup-20260529-114507"
      Kind="Backup"
      Role="SafetyCopy"
      Path="F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\references\backups\AMR_REF_SCADA_backup_20260529_114507"
      ReadOnly="true" />
</SourceReferences>
```

Rules:

1. The original `AMR_REF_SCADA` is a legacy sample, not an editable working target and not the V2 domain model.
2. Destructive experiments must use copies or generated V2 projects.
3. Each imported scene and asset must keep enough traceability to find the source legacy file.
4. FT100 build output must compile from V2 scene files, not from legacy HTML.

## 6. Workspace Defaults

```xml
<WorkspaceDefaults>
  <Canvas Width="1280" Height="873" Unit="px" />
  <Theme Id="amr_default" />
  <Preview DefaultDevicePreset="desktop-1280x873" />
  <Selection PreserveAcrossScenes="false" />
</WorkspaceDefaults>
```

Rules:

1. Canvas size is global by default.
2. Per-scene size is allowed for imported legacy scenes but should be normalized later.
3. Selection lock is editor state and should not be saved as normal project source unless explicitly persisted as workspace preference.

## 7. Responsive Model

The project supports three responsive modes:

1. `Fixed`
2. `ScaleToFit`
3. `AdaptiveLayout`

```xml
<Responsive Mode="AdaptiveLayout" AuthoringMode="DesktopFirst">
  <Breakpoints>
    <Breakpoint Id="desktop" MinWidth="1025" Variant="Desktop" />
    <Breakpoint Id="tablet" MinWidth="768" MaxWidth="1024" Variant="Tablet" />
    <Breakpoint Id="mobile" MaxWidth="767" Variant="Mobile" />
  </Breakpoints>
  <Generation PreferCss="true" RuntimeJavascript="Minimal" />
</Responsive>
```

Rules:

1. Responsive variants are variants of one logical scene, not independent pages.
2. Tags, bindings, actions, scripts, and logical element ids remain attached to the scene.
3. Layout variants may override position, size, visibility, transform, z-index, and grouping presentation.
4. Mobile and tablet variants support portrait and landscape presets.
5. The build should generate CSS media queries for responsive behavior whenever possible.
6. Runtime JavaScript must not become responsible for layout logic that CSS can handle deterministically.

## 8. Device Presets

```xml
<DevicePresets>
  <Preset Id="desktop-1920x1080" Family="Desktop" Width="1920" Height="1080" Orientation="Landscape" />
  <Preset Id="desktop-1280x873" Family="Desktop" Width="1280" Height="873" Orientation="Landscape" />
  <Preset Id="tablet-ipad-landscape" Family="Tablet" Width="1024" Height="768" Orientation="Landscape" />
  <Preset Id="tablet-ipad-portrait" Family="Tablet" Width="768" Height="1024" Orientation="Portrait" />
  <Preset Id="mobile-phone-portrait" Family="Mobile" Width="390" Height="844" Orientation="Portrait" />
  <Preset Id="mobile-phone-landscape" Family="Mobile" Width="844" Height="390" Orientation="Landscape" />
</DevicePresets>
```

Rules:

1. Presets are project data.
2. Users may add custom presets.
3. Safe areas may be added per preset.
4. Build output must be able to derive CSS media queries from the presets.

## 9. Assets

```xml
<Assets>
  <AssetRoot Id="project-assets" Path="assets" Role="ProjectOwned" />
  <AssetRoot Id="modernized-assets" Path="assets/modernized" Role="ModernizedLegacy" />
  <AssetRoot Id="legacy-html-assets" Path="references/legacy/html_pages/assets" Role="LegacyReference" ReadOnly="true" />

  <Asset Id="icon-pipe-horizontal-upper-v1"
         Kind="Image"
         Path="assets/modernized/icon_pipe_horizontal_upper_v1.svg"
         SourceReferenceId="amr-ref-scada"
         SourcePath="assets/modernized/icon_pipe_horizontal_upper_v1.svg" />
</Assets>
```

Rules:

1. Assets referenced by scenes must resolve through declared roots.
2. Imported legacy assets should be copied into project-owned locations before editing.
3. Legacy references may point to source paths but should remain read-only.
4. Generated export assets must not be treated as source unless explicitly imported back into the project.

## 10. Scene Index

`project.xaml` indexes scenes but does not inline heavy scene content.

```xml
<Scenes>
  <SceneRef
      Id="win00008"
      Title="win00008"
      Type="Default"
      File="scenes/win00008.scene.xaml"
      IncludeInBuild="true"
      IsHome="true"
      HeaderPageId="header-main"
      FooterPageId="footer-main"
      Width="1280"
      Height="873"
      DefaultVariant="Desktop"
      LegacySourceReferenceId="amr-ref-scada"
      LegacySourcePath="pages/win00008.json" />
</Scenes>
```

Rules:

1. Scene id is stable and machine-readable.
2. Title is user-facing and may later be renamed.
3. `SceneRef.File` is the model source for the logical scene.
4. Legacy source fields preserve traceability to imported `AMR_REF_SCADA` pages.
5. Opening a scene in the editor creates a workspace tab but does not duplicate the scene model.
6. Page type values are `Default`, `Fragment`, `Header`, and `Footer`.
7. Page width and height may override the workspace default when the page is intentionally resized.
8. Page records describe page structure and composition role; they do not own action triggers.
9. A project may declare one home page. If no explicit home is valid, build/runtime uses the first compiled `Default` page.
10. `IncludeInBuild` controls whether the page is exported to the FT100/TF100Web runtime package.
11. A compiled page may reference one compiled `Header` page and one compiled `Footer` page.
12. Build validation must fail when a compiled page references a missing, wrong-type, or non-compiled header/footer page.

## 11. Scene Model

Logical scene files should follow this shape:

```xml
<ScadaScene
    SchemaVersion="2.0"
    Id="win00008"
    Title="win00008">

  <BaseCanvas Width="1280" Height="873" Background="rgba(0,0,0,1)" />
  <LegacyTrace />
  <Composition IncludeInBuild="true" HeaderPageId="header-main" FooterPageId="footer-main" />
  <Elements />
  <Groups />
  <Bindings />
  <Actions />
  <Scripts />
  <LayoutVariants />
  <Validation />
</ScadaScene>
```

Scene content rules:

1. Elements represent logical SCADA objects, not only raw HTML tags.
2. Legacy element ids are stored in trace fields, not used as primary display labels.
3. Bindings and object event bindings are attached to logical element ids.
4. Layout variants override visual placement without duplicating bindings.
5. Legacy embeds are allowed as temporary migration elements.
6. Pages do not own events or action triggers.
7. Header/footer composition is page metadata and must not be represented as object-owned runtime action triggers.

## 12. Elements

Recommended element shape:

```xml
<Element
    Id="el-te-ext-value"
    Kind="Text"
    DisplayName="TE-EXT value"
    SourceElementId="784"
    SourceElementName="Text22">
  <Content Text="###.0" />
  <StyleRef Id="legacy-text-bold-value" />
  <Properties>
    <Property Name="Unit" Value="degC" />
  </Properties>
  <Events>
    <Event Trigger="click" ActionId="nav-to-win00009" />
  </Events>
</Element>
```

Rules:

1. `Id` is stable inside the project.
2. `Kind` maps to an editor element type such as `Text`, `Image`, `Button`, `Group`, `LegacyEmbed`, `Symbol`, or `NumericValue`.
3. `DisplayName` is user-facing.
4. `SourceElementId` and `SourceElementName` preserve legacy traceability.
5. CSS-like styling is stored as structured style properties, not as opaque raw HTML whenever possible.
6. Events are object-owned and reference action definitions by id.

## 13. Layout Variants

```xml
<LayoutVariants>
  <Variant Id="desktop" Family="Desktop" File="win00008/desktop.layout.xaml" />
  <Variant Id="tablet" Family="Tablet" File="win00008/tablet.layout.xaml" />
  <Variant Id="mobile" Family="Mobile" File="win00008/mobile.layout.xaml" />
</LayoutVariants>
```

Variant file example:

```xml
<SceneLayoutVariant SceneId="win00008" Variant="Desktop" Width="1280" Height="873">
  <Placement ElementId="el-te-ext-value" X="80" Y="57" Width="45" Height="24" ZIndex="10" Visible="true" />
  <Placement ElementId="el-condenseur-out-pipe" X="668" Y="185" Width="316" Height="217" ZIndex="3" Visible="true" />
</SceneLayoutVariant>
```

Rules:

1. A placement references an existing logical element.
2. Missing placement means the variant inherits from the base layout if one exists.
3. Visibility overrides are allowed per variant.
4. Rotations for tablet and mobile are layout metadata, not duplicated scenes.

## 14. FT100 Mapping Placeholders

FT100 integration is progressive. The model must support unresolved mappings from the start.

```xml
<Mappings>
  <MappingSource Id="ft100-placeholder" Kind="Placeholder" Status="Unresolved" File="mappings/ft100.placeholders.xaml" />
  <MappingSource Id="ft100-imported" Kind="FT100Import" Status="NotImported" File="mappings/ft100.imported.xaml" />
</Mappings>
```

Placeholder mapping file:

```xml
<Ft100Mappings SchemaVersion="2.0">
  <TagPlaceholder
      Id="tag-te-ext"
      Name="TE-EXT"
      DataType="Number"
      Unit="degC"
      Status="Unresolved"
      SourceHint="legacy text label TE-EXT in win00008" />

  <BindingPlaceholder
      Id="bind-te-ext-value"
      SceneId="win00008"
      ElementId="el-te-ext-value"
      Property="Text"
      TagPlaceholderId="tag-te-ext"
      Format="0.0"
      Status="Unresolved" />
</Ft100Mappings>
```

Rules:

1. A placeholder may be created before the real FT100 import exists.
2. Reimporting FT100 mappings must resolve placeholders by stable ids, names, aliases, or approved matching rules.
3. Missing or incompatible FT100 tags produce validation warnings, not silent deletion.
4. FT100 mappings stay in the mapping domain and are referenced by bindings; they are not embedded directly into UI widgets.

## 15. Bindings

```xml
<Bindings>
  <Binding
      Id="bind-te-ext-value"
      TargetElementId="el-te-ext-value"
      TargetProperty="Text"
      SourceKind="Ft100Tag"
      SourceId="tag-te-ext"
      Format="0.0"
      FallbackText="###.0" />
</Bindings>
```

Rules:

1. Bindings are model data and must be validated before preview/build.
2. Binding fallback values preserve readable SCADA screens when FT100 data is unavailable.
3. Binding targets must be type-compatible with the target element property.

## 16. Actions

```xml
<Actions>
  <Action Id="nav-to-win00009" Kind="Navigate" TargetSceneId="win00009" />
  <Action Id="show-pump-detail" Kind="SetVisibility" TargetElementId="grp-pump-detail" Value="true" />
</Actions>
```

Rules:

1. Actions are explicit runtime operation definitions in the model.
2. Object event bindings reference actions by id.
3. Pages may be action targets, but pages do not own action trigger lists.
4. Actions may use FT100 values only through validated bindings or script inputs.
5. Django-readable manifests must preserve this separation so Django can wire object events without inferring editor behavior.

## 17. Scripts

`project.xaml` indexes scripts. Script bodies may be inline for small logic or stored in `scripts/`.

```xml
<Scripts Language="ScadaBasic" TranspileTarget="JavaScript">
  <ScriptRef Id="script-condensing-pressure-color"
             Scope="Scene"
             SceneId="win00008"
             File="scripts/scenes/win00008/condensing_pressure_color.scadascript" />
</Scripts>
```

Rules:

1. Scripts must not bypass bindings, actions, or validation.
2. Scripts are validated by the C# domain layer.
3. Build transpiles scripts to generated JavaScript.
4. Generated JavaScript belongs in export output, not source project files.
5. Script inputs and outputs must be declared so dependencies remain inspectable.

## 18. Build Contract

```xml
<Build Target="FT100Web" OutputPath="exports/ft100-web" Minify="true" SourceMaps="false">
  <Runtime CssReset="ScadaV2" NormalizeHtmlControls="true" />
  <ResponsiveOutput PreferCssMediaQueries="true" />
  <Assets CopyMode="ReferencedOnly" />
</Build>
```

Rules:

1. Preview and build share the same runtime CSS contract.
2. Build output is generated and should be reproducible.
3. FT100 deployment settings are build options, not scene model data.
4. CSS reset and HTML control normalization are mandatory for preview/build parity.

## 19. Validation

```xml
<Validation>
  <RuleSet Id="default-v2" />
  <Warning Code="FT100_TAG_UNRESOLVED" Severity="Warning" />
  <Warning Code="LEGACY_EMBED_PRESENT" Severity="Info" />
</Validation>
```

Minimum validation:

1. All referenced files exist.
2. Scene ids are unique.
3. Element ids are unique inside each scene.
4. Layout placements reference existing elements.
5. Asset paths resolve through declared asset roots.
6. Bindings target compatible properties.
7. FT100 placeholders are flagged until resolved.
8. Scripts parse and declare allowed dependencies.
9. Preview and build use the same reset/runtime CSS version.

## 20. Migration From AMR_REF_SCADA

Mapping from current reference structure:

```text
AMR_REF_SCADA/project.json          -> modernization/source-index, then project.xaml only through explicit conversion
AMR_REF_SCADA/pages/*.json          -> modernization/candidates/*.xaml, then scenes/*.scene.xaml after acceptance
AMR_REF_SCADA/pages/win00008.json   -> modernization/candidates/win00008.*, then scenes/win00008.scene.xaml after conversion
AMR_REF_SCADA/assets                -> assets/ or references/legacy/assets
AMR_REF_SCADA/08_web_modernized     -> references/legacy/08_web_modernized
AMR_REF_SCADA/dist                  -> export example, not source of truth
AMR_REF_SCADA/runtime               -> reference runtime, not automatically V2 runtime
```

Migration rules:

1. Keep `AMR_REF_SCADA` read-only unless explicit approval is given.
2. Treat `pages/*.json` as source inventory for extraction candidates, not as V2 scene files.
3. Treat `dist/` as generated output for comparison, not as the authoritative model.
4. Keep `legacy.source_html`, inventory summaries, and element source ids as trace metadata.
5. Legacy embeds can be opened in the Legacy Viewer first, then progressively decomposed into V2 elements.
6. Accepted candidates become V2 elements with V2 ids; legacy ids remain trace metadata.

## 21. Open Decisions

1. Confirm whether V2 source files should use one XAML namespace for all project model types or separate namespaces by domain.
2. Confirm the exact first production target name: current reference uses `tf100-web`; docs use `FT100`.
3. Define the first FT100 import format and matching rules.
4. Decide whether scene element styles are stored inline, by style refs, or both.
5. Decide how much workspace UI state should be persisted in the project file.
