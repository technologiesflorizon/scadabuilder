# SCADA Builder V2 - Responsive Model

Date: 2026-06-15
Status: Draft
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `72350e3` | Normalisation du header documentaire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.0.0.0002` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Objective

This document defines the responsive model for SCADA Builder V2.

The goal is to keep one logical SCADA scene while allowing controlled rendering on desktop, tablet and mobile targets.

The responsive model must support:

1. Fixed industrial panels.
2. Scale-to-fit deployments.
3. Adaptive layouts with editable device variants.
4. Preview presets for desktop, tablet and mobile.
5. Portrait and landscape validation.
6. CSS-first generated output.
7. Preview/build parity.

## 2. Core Principles

1. A scene is a logical functional unit.
2. Device variants are layout variants of the same scene, not duplicated pages.
3. Tags, bindings, scripts and actions belong to the logical scene.
4. Position, size, visibility, grouping and layout constraints can vary by device variant.
5. Responsive behavior should be generated primarily as CSS.
6. JavaScript must not be used for responsive layout when CSS can express the behavior.
7. The preview must consume the same scene model and responsive rules as the build.

## 3. Project-Level Responsive Settings

Each project defines a default responsive strategy.

Required fields:

1. `baseWidth`
2. `baseHeight`
3. `authoringMode`
4. `responsiveMode`
5. `defaultPreviewPreset`
6. `devicePresets`
7. `safeAreas`
8. `breakpoints`

Allowed `authoringMode` values:

1. `desktop-first`
2. `tablet-first`
3. `mobile-first`

Allowed `responsiveMode` values:

1. `fixed`
2. `scale-to-fit`
3. `adaptive-layout`

Default recommendation:

```text
authoringMode: desktop-first
responsiveMode: scale-to-fit
baseWidth: 1920
baseHeight: 1080
```

## 4. Mode: Fixed

`Fixed` mode targets a known industrial display or locked panel resolution.

Behavior:

1. One logical scene.
2. One layout variant.
3. Strict reference width and height.
4. No automatic layout reflow.
5. No element scaling unless explicitly configured on the element.
6. Overflow is explicit and visible in validation.

Generated CSS:

1. Root scene uses fixed dimensions.
2. Elements use deterministic positioning.
3. `box-sizing: border-box` applies globally.
4. The viewport wrapper may center the scene.
5. Optional background outside the scene area is allowed but must not affect scene dimensions.

Validation rules:

1. The configured target preset must match the base dimensions or be explicitly accepted as a mismatch.
2. Elements outside the scene bounds are warnings unless marked as intentionally off-canvas.
3. Text overflow is an error when it hides an alarm, label, value or operator command.
4. Interactive elements smaller than the configured minimum target size are warnings.

Use cases:

1. Dedicated HMI panel.
2. Control room display with known resolution.
3. Legacy migration where geometry fidelity is more important than adaptability.

## 5. Mode: Scale-to-Fit

`Scale-to-fit` mode keeps one layout and scales it to the available viewport.

Behavior:

1. One logical scene.
2. One layout variant.
3. The canvas keeps its aspect ratio.
4. The scene scales to fit inside the target viewport.
5. Letterboxing or pillarboxing is allowed.
6. The scale factor must be exposed in preview diagnostics.

Fit policies:

1. `contain`
   - Entire scene remains visible.
   - Empty margins may appear.
   - Default policy.
2. `cover`
   - Viewport is filled.
   - Cropping may occur.
   - Requires explicit project approval.
3. `width`
   - Scene scales to viewport width.
   - Vertical scroll may occur.
4. `height`
   - Scene scales to viewport height.
   - Horizontal overflow may occur.

Generated CSS:

1. The scene wrapper owns viewport fitting.
2. The scene surface keeps its logical dimensions.
3. Scaling is implemented with CSS transforms or equivalent deterministic CSS.
4. Transform origin must be explicit.
5. Element coordinates remain in the base coordinate system.

Validation rules:

1. Minimum readable text size after scaling must be checked per preset.
2. Minimum interactive target size after scaling must be checked per preset.
3. `cover`, `width` and `height` policies must report possible clipping or scrolling.
4. Safe area conflicts are warnings by default and errors for critical operator controls.

Use cases:

1. Desktop to tablet preview without redesign.
2. Legacy screen reuse.
3. Installations where visual proportions must stay identical.

## 6. Mode: Adaptive Layout

`Adaptive layout` mode gives one logical scene multiple editable layout variants.

Required variants:

1. `desktop`
2. `tablet`
3. `mobile`

Each variant can define:

1. Position.
2. Size.
3. Visibility.
4. Grouping.
5. Layout constraints.
6. Variant-specific CSS tokens.
7. Variant-specific safe area overrides.

Shared across variants:

1. Element identity.
2. Tags.
3. Bindings.
4. Actions.
5. Scripts.
6. Alarm semantics.
7. Navigation target identity.

Generated CSS:

1. Variants are selected with media queries and orientation queries.
2. Shared styles are emitted once.
3. Variant overrides are emitted in scoped responsive blocks.
4. Element IDs/classes remain stable across variants.
5. Hidden elements must remain addressable by bindings unless removed from runtime by an explicit build option.

Validation rules:

1. Every logical element must have a defined behavior for each required variant.
2. Critical controls cannot be hidden on a variant unless an alternate visible control is declared.
3. Bindings cannot diverge between variants unless explicitly versioned as a functional change.
4. Navigation must resolve to the same logical scene identity across variants.
5. Variant-specific layout changes must not create duplicate tag writes for one operator action.

Use cases:

1. SCADA screens designed for desktop, tablet and phone.
2. Mobile operator views with simplified layout.
3. Progressive modernization of legacy screens.

## 7. Device Presets

Device presets are stored in project configuration and used by preview, validation and build CSS generation.

Preset fields:

1. `id`
2. `label`
3. `category`
4. `width`
5. `height`
6. `orientation`
7. `pixelRatio`
8. `safeArea`
9. `defaultBreakpoint`

Allowed categories:

1. `desktop`
2. `tablet`
3. `mobile`
4. `custom`

Initial presets:

```text
desktop-1920x1080    Desktop 16:9 1920 x 1080
desktop-1600x900     Desktop 16:9 1600 x 900
desktop-1366x768     Desktop 16:9 1366 x 768
desktop-1280x720     Desktop 16:9 1280 x 720
desktop-1280x960     Desktop 4:3 1280 x 960
desktop-1024x768     Desktop 4:3 1024 x 768
tablet-1024x1366-p   Tablet portrait 1024 x 1366
tablet-1366x1024-l   Tablet landscape 1366 x 1024
tablet-800x1280-p    Android tablet portrait 800 x 1280
tablet-1280x800-l    Android tablet landscape 1280 x 800
mobile-390x844-p     Mobile portrait 390 x 844
mobile-844x390-l     Mobile landscape 844 x 390
mobile-412x915-p     Android phone portrait 412 x 915
mobile-915x412-l     Android phone landscape 915 x 412
custom               User-defined width, height and orientation
```

Rules:

1. Built-in presets must be read-only.
2. Custom presets must be stored in the project.
3. A custom preset must have positive integer width and height.
4. Pixel ratio is optional and used only for preview diagnostics unless a build target needs it.
5. Safe area values default to zero unless specified.

## 8. Rotation

Rotation is supported for tablet and mobile variants.

Supported orientations:

1. `portrait`
2. `landscape`

Rules:

1. Rotation changes the active preset dimensions and orientation.
2. Rotation must not create a new logical scene.
3. In `adaptive-layout`, portrait and landscape can have separate layout overrides inside the same device variant.
4. In `fixed`, rotation is a validation scenario only unless the target panel supports it.
5. In `scale-to-fit`, rotation recalculates the fit scale and reports clipping, margins and readable size.
6. Rotation preview must update safe areas.

CSS generation:

1. Use orientation media queries when variant overrides exist.
2. Keep shared variant CSS outside orientation-specific blocks.
3. Orientation-specific rules must be deterministic and ordered after device-category rules.

## 9. CSS-First Layout Contract

The responsive engine must prefer CSS output.

Allowed CSS mechanisms:

1. Media queries.
2. Orientation queries.
3. CSS custom properties.
4. Flex or grid where a component explicitly uses it.
5. Absolute positioning for imported or fixed legacy geometry.
6. `min-width`, `max-width`, `min-height`, `max-height`.
7. `clamp()` where values are validated and deterministic.
8. Transform-based scaling for scale-to-fit.

JavaScript is allowed only for:

1. Runtime data binding.
2. Operator actions.
3. Navigation.
4. Scripted behaviors generated from the project model.
5. Preview diagnostics that do not alter final layout.

JavaScript is not allowed for:

1. Choosing normal responsive breakpoints.
2. Moving elements between variants when CSS can select the layout.
3. Applying ad hoc viewport fixes that are not represented in the project model.

## 10. Validation

Responsive validation runs at project, scene, variant and preset level.

Project validation:

1. Base dimensions are valid.
2. Responsive mode is valid.
3. Required presets exist.
4. Breakpoints are ordered and non-overlapping.
5. Safe areas are valid numbers.

Scene validation:

1. Logical scene identity is stable.
2. Element IDs are unique.
3. Bindings resolve.
4. Actions resolve.
5. Required variants exist for adaptive layout.

Variant validation:

1. Elements have valid geometry.
2. Critical elements are visible or have declared alternatives.
3. Groups do not create impossible constraints.
4. Text containers have enough room for configured labels.
5. Z-index ordering is deterministic.

Preset validation:

1. No critical content is outside safe area.
2. Minimum readable text size is respected.
3. Minimum interactive target size is respected.
4. Rotation behavior is defined for tablet and mobile.
5. Scale-to-fit results remain usable at the chosen preset.

Severity levels:

1. `error`
   - Build must fail.
2. `warning`
   - Build can proceed with visible report.
3. `info`
   - Diagnostic only.

Default build blockers:

1. Invalid dimensions.
2. Missing required adaptive variant.
3. Unresolved binding on visible element.
4. Duplicate element ID.
5. Critical operator control hidden with no alternate path.
6. Generated CSS conflict that changes final geometry between preview and build.

## 11. Open Decisions

1. Confirm minimum text size thresholds for industrial operator use.
2. Confirm minimum touch target size for tablet and mobile.
3. Decide whether `cover` scale-to-fit is allowed in the first implementation.
4. Decide whether custom breakpoints are available in the first implementation or delayed.
