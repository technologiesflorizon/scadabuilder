# SCADA Builder V2 - Multi-Agent Operating Model

Date: 2026-06-15
Status: Draft
Document version: `V2.1.1.0030`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-15 | `V2.1.1.0030` | `PENDING` | Ajout du header documentaire obligatoire et rattachement a l'arbre documentaire stable. |
| 2026-06-15 | `V2.0.0.0000` | `2b59efb` | Baseline initiale du depot SCADA Builder V2. |

## 1. Principle

The user remains the product and technical orchestrator.

Agents are used as specialized contributors for bounded work. They must not make product direction decisions without orchestrator approval.

Development quality note:

SCADA Builder V2 must stop defaulting to the smallest possible implementation when the user asks for a usable tool. Minimal technical proof is acceptable only to unblock an unknown risk. Once the direction is clear, each iteration should deliver a coherent vertical slice with enough UI, state, validation, and workflow behavior to be evaluated in real use.

Practical rule:

1. Avoid placeholder-only features unless explicitly marked as temporary.
2. Prefer a usable first version over a bare button or isolated backend function.
3. Include expected controls, empty states, error states, and basic feedback.
4. When adding an editor tool, make the workflow discoverable from the UI.
5. When the request affects UX, validate the interaction path, not only compilation.
6. Document unfinished edges as known limitations, not hidden omissions.
7. Contextual menu boxes and floating inspectors must be draggable, closable with a visible control, and dismissible with `Escape`.

Default execution rule:

SCADA Builder V2 must not be developed as a sequence of isolated, single-agent tasks when the work touches more than one domain.

For every non-trivial request, the orchestrator must:

1. Identify the impacted domains.
2. Split the work by domain ownership.
3. Delegate bounded tasks to the relevant agents.
4. Keep the immediate critical-path work local only when needed.
5. Collect agent questions and risks.
6. Ask the user/orchestrator for decisions when product direction is needed.
7. Redistribute decisions back to the agents.
8. Integrate the results.
9. Apply one centralized version bump for the whole orchestrated iteration.

Single-agent/local work is acceptable only for:

1. Very small fixes.
2. Mechanical edits in one file.
3. Emergency build fixes.
4. User questions that require no code or document change.
5. Tasks explicitly requested as local-only by the user.

## 2. Orchestrator Role

The orchestrator:

1. Approves product direction.
2. Prioritizes modules.
3. Approves version bumps beyond normal iteration.
4. Reviews major architecture decisions.
5. Decides when a capability becomes production-preview.
6. Resolves conflicts between modules.

## 3. Agent Roles

## 3.1 Product / Functional Architecture Agent

Responsibilities:

1. Stabilize V2 decisions.
2. Maintain MVP priorities.
3. Track open product questions.
4. Clarify ribbon, panels, project navigation, locked selection, and responsive modes.

Expected artifacts:

1. Product decision notes.
2. `OPEN_DECISIONS.md`.
3. MVP scope notes.

## 3.2 Architecture Agent

Responsibilities:

1. Maintain modular architecture.
2. Prevent monolithic UI growth.
3. Define module boundaries.
4. Review dependency direction.
5. Maintain project structure.

Expected artifacts:

1. Architecture decision records.
2. Module boundary notes.
3. Dependency diagrams.

## 3.3 UI/UX Desktop Agent

Responsibilities:

1. Translate wireframes into UI specs.
2. Maintain Florizon visual continuity.
3. Define panel/ribbon/workspace behavior.
4. Ensure editor ergonomics.

Expected artifacts:

1. UI direction docs.
2. Component specs.
3. Interaction notes.

## 3.4 Project Model / XAML Agent

Responsibilities:

1. Define `project.xaml`.
2. Define scene model.
3. Define responsive variants.
4. Maintain versioned schema.

Expected artifacts:

1. XAML schema notes.
2. Example project files.
3. Migration notes.

## 3.5 Scene / Canvas / Selection Agent

Responsibilities:

1. Define workspace central behavior.
2. Define page tabs, close, reorder, dirty marker.
3. Define selection model.
4. Define locked selection.
5. Define object lock.
6. Define drag, zoom, and editing behavior.
7. Ensure UI uses explicit commands.

Expected artifacts:

1. Scene/canvas interaction notes.
2. Selection model docs.
3. Command/state specs.

## 3.6 Responsive / Preview Agent

Responsibilities:

1. Define fixed mode.
2. Define scale-to-fit mode.
3. Define adaptive layout mode.
4. Define device presets.
5. Define desktop/tablet/mobile variants.
6. Define safe areas, rotation and breakpoints.

Expected artifacts:

1. Responsive model docs.
2. Device preset specs.
3. Preview behavior notes.

## 3.7 Rendering/Build Agent

Responsibilities:

1. Maintain preview/build parity.
2. Define CSS generation.
3. Define responsive output.
4. Control CSS reset/normalization.

Expected artifacts:

1. Build pipeline notes.
2. Runtime CSS notes.
3. Preview validation procedures.

## 3.8 FT100 Integration Agent

Responsibilities:

1. Define mapping import.
2. Define binding model.
3. Validate FT100 relationships.
4. Plan progressive integration.

Expected artifacts:

1. FT100 mapping import notes.
2. Binding model specs.
3. Validation rules.

## 3.9 Scripting / Actions Agent

Responsibilities:

1. Define simple scripting language.
2. Define validation rules.
3. Define transpilation to JavaScript.
4. Keep runtime safe and deterministic.

Expected artifacts:

1. Script grammar notes.
2. Script examples.
3. JS generation rules.

## 3.10 QA/Validation Agent

Responsibilities:

1. Define non-regression tests.
2. Validate preview/build parity.
3. Validate UI workflow behavior.
4. Track known risks.

Expected artifacts:

1. Test plans.
2. Manual validation runbooks.
3. Regression reports.

## 4. Expected Documentation Set

Initial document set:

1. `ARCHITECTURE_V2.md`
   - Layers, domains, boundaries, command flow.
2. `PROJECT_MODEL_XAML.md`
   - `project.xaml`, scenes, assets, presets, build options.
3. `UI_SPEC_V2.md`
   - Layout, panels, ribbon, tools, states, selection, interactions.
4. `RESPONSIVE_MODEL_V2.md`
   - Fixed, scale-to-fit, adaptive layout, presets, variants.
5. `PREVIEW_BUILD_CONTRACT.md`
   - Preview/build equivalence, CSS reset, shared runtime CSS.
6. `FT100_IMPORT_SPEC.md`
   - Expected formats, parsing, validation, generated bindings.
7. `SCRIPTING_SPEC.md`
   - Syntax, validation, transpilation, runtime limits.
8. `COMMANDS_AND_STATE.md`
   - Application commands, selection, future undo/redo, project state.
9. `QA_TEST_STRATEGY.md`
   - Domain tests, critical cases, export validation.
10. `OPEN_DECISIONS.md`
   - Unresolved product and architecture decisions.

## 5. Coordination Rules

1. Each agent owns a bounded module or document set.
2. Agents must not rewrite unrelated modules.
3. Agents must report changed files.
4. Agents must state assumptions and risks.
5. Shared contracts require orchestrator approval.
6. Version bumps follow `VERSIONING_POLICY_V2.md`.
7. Major capability lines require feature version approval.
8. Agents must not duplicate work already assigned to another agent.
9. Agents must validate user inputs in their domain: CSS, dimensions, tags, scripts, paths, and mappings.
10. Shared command definitions must have a single source of truth.
11. The orchestrator must prefer domain delegation for work spanning UI, model, rendering, FT100, scripting, QA, or architecture.
12. The orchestrator must centralize all final integration and version changes.
13. Agents may propose version bumps, but only the orchestrator applies the official `VERSION` update.
14. Agents route questions to the orchestrator; the orchestrator asks the user and redistributes the answer.

## 6. Initial Workstreams

Recommended initial workstreams:

1. Architecture and project structure.
2. UI shell/ribbon/panels.
3. Project model and `project.xaml`.
4. Rendering preview with WebView2.
5. Responsive mode model.
6. FT100 mapping model placeholder.

## 7. Coordination Risks

1. Blurred boundaries between UI, project model, preview, and build.
2. Divergence between WebView2 preview and final browser/export runtime.
3. Responsive variants treated as independent duplicated pages instead of one logical scene.
4. Scripting becoming too permissive and bypassing bindings, validation, or model rules.
5. Properties panel exposing too much CSS without type-specific validation.
6. FT100 import coupled to UI instead of the binding domain.
7. Ribbon, left toolbar and tools menu becoming redundant without a shared command registry.
8. Locked selection confused with object lock.
9. `project.xaml` becoming monolithic if scenes, assets, and generated data are not separated.
10. Tests becoming full-application-only instead of domain-level and command-level.
