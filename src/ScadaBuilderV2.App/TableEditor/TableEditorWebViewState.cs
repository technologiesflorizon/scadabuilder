using ScadaBuilderV2.Application.Tables;

namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Immutable editor-only Table interaction state sent to WebView2.</summary>
/// <remarks>
/// Decisions: DEC-0041. Contracts: docs/superpowers/specs/2026-07-15-table-lock-interaction-regression-correction-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/TableEditorWebViewStateTests.cs.
/// </remarks>
internal sealed record TableEditorWebViewState(
    string? TableElementId,
    TableInteractionMode Mode,
    bool ShowEditorGuides,
    bool EditorGuidesVisible);
