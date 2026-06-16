# SCADA Builder V2 - Documentation Standard

Date: 2026-06-16
Status: Active enterprise documentation standard
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation du standard documentaire modulaire, decisionnel, diagramme et verifiable. |

## 1. Objective

SCADA Builder V2 documentation must support multi-person and multi-agent development without losing decisions, duplicating contracts, or presenting future work as implemented behavior.

The standard is:

```text
Contracts humains + decisions historisees + documentation code + diagrammes Mermaid + validation automatisee.
```

## 2. Document Roles

Each document must have one primary role:

1. Governance: rules for documentation, decisions, versioning, and workflow.
2. Product: application objectives and user workflows.
3. Architecture: module boundaries and flows.
4. Runtime contract: output, project model, preview/build/export, and package behavior.
5. Editor contract: commands, states, actions, selection, menus, properties.
6. Studio Element+: Studio-specific architecture, selection, and `.sep`.
7. UI/UX: shell, visual, responsive, and icon decisions.
8. Legacy migration: source policy and modernization.
9. Implementation status: implemented features, known gaps, and regression map.
10. Generated: code maps and function indexes produced or verified by tooling.

A contract document must not be a roadmap. A roadmap must not claim behavior is implemented. A historical document must not be the active source of truth.

## 3. Mandatory Header

Every Markdown document under `docs/` must start with:

1. H1 title.
2. `Date`.
3. `Status`.
4. `Document version`.
5. `## Historique des changements`.
6. A table with `Date`, `Version`, `Commit`, and `Changement`.

Use `PENDING` only when the commit hash is not available yet.

## 4. Decision Governance

Architecture and behavioral decisions live in `DECISION_REGISTER_V2.md`.

A decision is not deleted when it changes. It is marked:

1. `Active` when it governs current work.
2. `Deprecated` when it is no longer valid and no direct replacement exists.
3. `Superseded` when replaced by another decision.

Deprecation requires datetime, commit, and reason.

## 5. Mermaid Diagrams

The following documents require Mermaid diagrams:

1. Global architecture: flowchart.
2. Application flow: flowchart.
3. Commands: sequence diagram.
4. State management: state diagram.
5. Actions/events: flowchart or sequence diagram.
6. Menus/surfaces: flowchart.
7. FT100/TF100Web package: flowchart.
8. Studio Element+: flowchart.

Diagram changes must happen in the owner document and in `docs/10_generated/*` when the generated map is affected.

## 6. Code Documentation

Public C# APIs must have XML documentation. Contract-sensitive private methods require intent comments or a nearby documented public entry point.

Required XML documentation for contract-sensitive APIs:

```csharp
/// <summary>
/// Explains the business role of the API.
/// </summary>
/// <remarks>
/// Decisions: DEC-0001.
/// Contracts: docs/03_runtime_contracts/FT100_TF100WEB_PACKAGE_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/Ft100SceneExporterTests.cs.
/// </remarks>
```

Comments explain the business reason and contract, not each line of code.

## 7. Validation

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
```

The verification script checks headers, decisions, Mermaid blocks, code documentation coverage, generated docs presence, and high-risk terms.
