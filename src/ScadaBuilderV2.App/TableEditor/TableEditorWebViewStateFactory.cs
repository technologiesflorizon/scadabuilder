using System.Text.Json;
using ScadaBuilderV2.Application.Tables;

namespace ScadaBuilderV2.App.TableEditor;

/// <summary>Creates and serializes the atomic Table interaction snapshot for WebView2.</summary>
/// <remarks>
/// Decisions: DEC-0041. Contracts: docs/superpowers/specs/2026-07-15-table-lock-interaction-regression-correction-design.md.
/// Tests: tests/ScadaBuilderV2.Tests/TableEditorWebViewStateTests.cs.
/// </remarks>
internal static class TableEditorWebViewStateFactory
{
    /// <summary>Creates a WebView snapshot from Application-owned session state.</summary>
    public static TableEditorWebViewState Create(TableAuthoringSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new(session.TableElementId, session.Mode, session.ShowEditorGuides, session.EditorGuidesVisible);
    }

    /// <summary>Builds one JavaScript call that updates mode, guide preference and active Table id together.</summary>
    public static string BuildApplyScript(TableEditorWebViewState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var mode = JsonSerializer.Serialize(state.Mode == TableInteractionMode.Cells ? "cells" : "object");
        var guides = state.ShowEditorGuides ? "true" : "false";
        var tableId = JsonSerializer.Serialize(state.TableElementId);
        return $"window.scadaModernTable?.setEditorState({mode}, {guides}, {tableId});";
    }
}
