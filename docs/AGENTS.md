# SCADA Builder V2 - Documentation Agent Rules

Date: 2026-06-16
Status: Active agent operating contract
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation des regles operationnelles pour humains et agents travaillant dans la documentation SCADA Builder V2. |

## 1. Required Workflow

For any change under `docs/`, or any code change that affects documented behavior:

1. Read `docs/README.md`.
2. Read `docs/00_governance/DECISION_REGISTER_V2.md`.
3. Identify the owner document for the touched behavior.
4. Update the owner document only; do not add active contracts to historical files.
5. Add or update a `DEC-xxxx` entry when a decision is created, changed, deprecated, or superseded.
6. Keep previous decisions present. Mark them `Deprecated` or `Superseded`; never erase them.
7. Update Mermaid diagrams when flow, ownership, command dispatch, state transitions, export, or Studio Element+ paths change.
8. Update generated documentation or run the verification script when public functions, commands, tests, or contracts change.
9. Run `tools/docs/verify-docs.ps1`.

## 2. Decision Rules

Decision changes require the decision register.

Required metadata:

1. Stable id: `DEC-0001`.
2. Status: `Active`, `Deprecated`, or `Superseded`.
3. Created datetime and commit.
4. Deprecated datetime and commit when applicable.
5. Superseding decision id when applicable.
6. Owner document.
7. Related tests when behavior is protected by tests.

Use `PENDING` only when the commit does not exist yet.

## 3. Code Documentation Rules

Every public method, public property with business meaning, public class, public record, public enum, and public interface must have XML documentation.

Every private method that touches selection, hit-testing, movement, grouping, properties, `.sep` export, preview, build, FT100/TF100Web export, actions, menus, state, undo/redo, or project persistence must have a concise intent comment or be covered by a nearby public XML documentation block.

XML documentation for contract-sensitive code should include:

```csharp
/// <remarks>
/// Decisions: DEC-0001, DEC-0004.
/// Contracts: docs/04_editor/SELECTION_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/WebViewContextMenuScriptTests.cs.
/// </remarks>
```

## 4. Studio Element+ Guardrail

For work touching Studio Element+ selection, hit-testing, movement, grouping, properties, `.sep` export, or regression tests, read:

```text
docs/05_studio_element_plus/STUDIO_ELEMENT_PLUS_SELECTION_CONTRACT_V2.md
```

Preserve:

1. `Shift + clic` adds to selection.
2. `Alt + clic` removes from selection.
3. Drag rectangle supports replace, add with `Shift`, and remove with `Alt`.
4. Selection overlays, handles, drag rectangles, workzone state, zoom, pan, and diagnostics are not exported into `.sep` component geometry.

## 5. Validation

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
```

Also run:

```powershell
dotnet test ScadaBuilderV2.sln --no-restore
```

when the change affects implemented behavior, tests, public contracts, or generated/runtime output.
