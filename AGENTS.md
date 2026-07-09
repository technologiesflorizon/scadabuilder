# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Execution behaviour

When I give you a task, you must execute it without producing intermediate messages.

You must not:

explain what you are going to do;
show a plan unless I explicitly ask for one;
comment on your progress;
generate intermediate prompts;
ask for confirmation at every step;
interrupt execution for non-blocking details.

You must continue working silently until one of the following situations occurs:

The task is complete.
An error prevents you from continuing.
Essential information is missing.
A critical ambiguity prevents you from making a safe decision.
A user decision is absolutely required.
A technical, logical, or context limitation prevents the task from being completed.

In all other cases, continue executing without interrupting me.

Only at the end of the task, provide a concise final report containing:

what was done;
the files created or modified;
any problems encountered, if applicable;
what remains to be done, only if relevant.

Do not write unnecessary progress messages.
Do not ask me to validate normal steps.
Do not turn execution into a conversation.
Execute the task, then report the final result.

## What this is

SCADA Builder V2 is a Windows (WPF / .NET 8) desktop authoring tool for industrial SCADA/HMI screens. It imports legacy SCADA HTML pages, lets the user re-author them as "Element+" scene objects on a WebView2-hosted canvas, and exports normalized `.sb2` packages consumed by the **TF100Web** runtime. It explicitly competes with ScadaPlant — treat all UI/editor/export work as production-grade, not prototype (see `docs/AGENTS.md` §6).

## Build, test, run

Requires a Windows machine with the .NET 8 SDK (WPF projects target `net8.0-windows`).

```bash
dotnet build ScadaBuilderV2.sln                       # build all projects
dotnet test  ScadaBuilderV2.sln --no-restore          # run the full MSTest suite
dotnet run --project src/ScadaBuilderV2.App           # launch the main authoring app
dotnet run --project src/ScadaBuilderV2.ElementStudio.App   # launch the Element Studio component editor
```

Run a single test or a filtered set (MSTest):

```bash
dotnet test ScadaBuilderV2.sln --filter "FullyQualifiedName~WebViewContextMenuScriptTests"
dotnet test ScadaBuilderV2.sln --filter "Name=ExportAsync_BlocksDuplicateDomIds"
```

Documentation validation (PowerShell, run after any `docs/` change):

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
```

## Architecture

Clean-architecture layering; dependencies point inward (`App` → `Infrastructure`/`Rendering` → `Application` → `Domain`). `Domain` has no project references.

- **`ScadaBuilderV2.Domain`** — pure models/records, no I/O. Projects & scenes (`Projects/`, `Scenes/`), Element+ models and group operations (`Elements/`), the event registry (`Scenes/ScadaEventRegistry.cs`), legacy import models, and semantic versioning (`Versioning/ScadaVersion.cs`).
- **`ScadaBuilderV2.Application`** — orchestration with no UI. The command registry & ribbon command catalog (`Commands/`), undo/redo (`History/` — each editor mutation is an `*Action` reversible command), polymorphic selection state (`Selection/`), legacy→Element+ conversion (`Conversion/`), and Element Studio component/package models (`ElementStudio/`).
- **`ScadaBuilderV2.Infrastructure`** — file/system I/O: legacy HTML element detection (`LegacyExtraction/`), project persistence (`ModernProjects/ModernProjectStore.cs`), TF100Web tag-catalog import, `.sep`/import package read-write, and reference-project reading.
- **`ScadaBuilderV2.Rendering`** — preview document generation (`PreviewDocument.cs`) and the FT100/`.sb2` exporter (`Ft100SceneExporter.cs`) plus its package validation. Preview, build, and export all consume the **same** V2 project model.
- **`ScadaBuilderV2.App`** — the WPF shell. `MainWindow.xaml.cs` is the very large (~9k line) editor host that drives a WebView2 canvas; the editor↔canvas boundary is a JS message bridge. Dialogs: `ElementPropertiesDialog`, `ElementEventDialog`, `ColorPickerDialog`/`ColorPickerField`.
- **`ScadaBuilderV2.ElementStudio.App`** — a separate WPF app for authoring reusable Element+ library components (`.sep` packages).

Key runtime concepts:
- **Element+** objects are the canonical, exportable scene objects (own their SVG/DOM). Legacy/source nodes must be **converted** to Element+ before they can be grouped or fully authored.
- The current export artifact is **`.sb2`** — a ZIP whose top-level entry is `scada-builder-v2-ft100-package/`, containing root `manifest.json` + `<page-id>/<page-id>.html`. `index.html` is deprecated.
- Exported CSS, DOM ids, and runtime action targets are **page-namespaced** to avoid collisions in TF100Web composition.

Projects authored by the app live under `projects/<NAME>/` (`scenes/`, `library/elements/*.sep`, `imports/`, `exports/`, `.studio/`). `AMR_REF_SCADA_V2` is the working reference project.

## Non-negotiable guardrails

These are active decisions (full list: `docs/00_governance/DECISION_REGISTER_V2.md`, summarized in `docs/README.md` §4). The most load-bearing:

- **Editor artifacts must never leak into export geometry.** Selection overlays, handles, drag rectangles, placement previews, workzone state, zoom, and pan are editor-only and must not appear in `.sep`/`.sb2` output.
- Preview / build / export must stay in parity by consuming one project model.
- Exporter-emitted page scripts are **not** all executed by current TF100Web fragment intake — keep "what the exporter emits" separate from "what TF100Web runs". The event parity matrix lives in `docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md`; active gaps in `docs/08_implementation_status/KNOWN_GAPS_V2.md`. Do not document a gap as implemented behavior.
- Selection is polymorphic; durable source deletion uses scene state + `RemovedSourceElementIds`, never WebView masking.
- `win00009` is the known-good baseline; `win00008` is a known divergence/regression candidate.

## Working conventions (from docs/AGENTS.md)

- Before starting new implementation work, the worktree should be clean — commit existing work first unless told otherwise. After each validated implementation, create a commit before starting the next (unless the user specifies a different boundary).
- Additive changes (new features, UI surfaces, contracts, model fields, export behavior, dependencies) require a planning step first; iterative bug fixes on already-implemented behavior may skip it.
- Documentation is ownership-based: edit only the owner document for the touched area (`docs/README.md` is the index/router). Never add active contracts to files under `docs/09_archive/`. Record decision changes as `DEC-xxxx` entries; mark superseded decisions `Deprecated`/`Superseded` rather than deleting them.
- Public APIs require XML docs. Contract-sensitive code should cite `Decisions:`, `Contracts:`, and `Tests:` in `<remarks>`.
- Versioning: `VERSION` and `docs/` changelog tables use `V2.x.y.zzzz`; use `PENDING` for commit hashes that don't exist yet.

## Tests

MSTest (`tests/ScadaBuilderV2.Tests`). Notable suites map to guardrails: `Ft100SceneExporterTests` (export/validation), `WebViewContextMenuScriptTests` + `StudioElementPlusContractTests` (Studio selection/menu contracts), `EditorHistoryServiceTests` (undo/redo), `RibbonCommandCatalogTests` (command catalog), `Legacy*DetectorTests` (import). Regression coverage map: `docs/08_implementation_status/REGRESSION_COVERAGE_V2.md`.
