using System.Reflection;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests.Runtime;

/// <summary>
/// Verifies that runtime JS modules shipped as embedded resources in the Rendering
/// assembly are present and contain the expected public API symbols.
/// </summary>
[TestClass]
public sealed class RuntimeJsModulesTests
{
    private static readonly Assembly RenderingAssembly = typeof(ScadaBuilderV2.Rendering.Ft100SceneExporter).Assembly;

    /// <summary>
    /// The expression-evaluator.js module must be embedded in the Rendering assembly.
    /// </summary>
    [TestMethod]
    public void ExpressionEvaluator_IsEmbeddedResource()
    {
        var resourceNames = RenderingAssembly.GetManifestResourceNames();
        var match = resourceNames.FirstOrDefault(name =>
            name.EndsWith("expression-evaluator.js", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(match, "expression-evaluator.js must be an embedded resource in ScadaBuilderV2.Rendering");
    }

    /// <summary>
    /// The expression-evaluator.js module must expose window.ScadaRuntime.ExpressionEvaluator.
    /// </summary>
    [TestMethod]
    public void ExpressionEvaluator_ExposesScadaRuntimeExpressionEvaluator()
    {
        var source = ReadEmbeddedResource("expression-evaluator.js");
        StringAssert.Contains(source, "ScadaRuntime");
        StringAssert.Contains(source, "ExpressionEvaluator");
    }

    /// <summary>
    /// The expression-evaluator.js module must expose a walk function.
    /// </summary>
    [TestMethod]
    public void ExpressionEvaluator_ExposesWalkFunction()
    {
        var source = ReadEmbeddedResource("expression-evaluator.js");
        StringAssert.Contains(source, "walk");
    }

    /// <summary>
    /// The expression-evaluator.js module must handle the literalNumber AST node type.
    /// </summary>
    [TestMethod]
    public void ExpressionEvaluator_HandlesLiteralNumber()
    {
        var source = ReadEmbeddedResource("expression-evaluator.js");
        StringAssert.Contains(source, "literalNumber");
    }

    /// <summary>
    /// The expression-evaluator.js module must handle the tagRef AST node type.
    /// </summary>
    [TestMethod]
    public void ExpressionEvaluator_HandlesTagRef()
    {
        var source = ReadEmbeddedResource("expression-evaluator.js");
        StringAssert.Contains(source, "tagRef");
    }

    /// <summary>
    /// The expression-evaluator.js module must handle the func AST node type.
    /// </summary>
    [TestMethod]
    public void ExpressionEvaluator_HandlesFunc()
    {
        var source = ReadEmbeddedResource("expression-evaluator.js");
        StringAssert.Contains(source, "func");
    }

    /// <summary>
    /// The expression-evaluator.js module must handle the unary AST node type.
    /// </summary>
    [TestMethod]
    public void ExpressionEvaluator_HandlesUnary()
    {
        var source = ReadEmbeddedResource("expression-evaluator.js");
        StringAssert.Contains(source, "unary");
    }

    /// <summary>
    /// The expression-evaluator.js module must handle the binary AST node type.
    /// </summary>
    [TestMethod]
    public void ExpressionEvaluator_HandlesBinary()
    {
        var source = ReadEmbeddedResource("expression-evaluator.js");
        StringAssert.Contains(source, "binary");
    }

    /// <summary>
    /// The expression-evaluator.js module must handle the literalBool AST node type.
    /// </summary>
    [TestMethod]
    public void ExpressionEvaluator_HandlesLiteralBool()
    {
        var source = ReadEmbeddedResource("expression-evaluator.js");
        StringAssert.Contains(source, "literalBool");
    }

    /// <summary>
    /// The expression-evaluator.js module must handle the literalString AST node type.
    /// </summary>
    [TestMethod]
    public void ExpressionEvaluator_HandlesLiteralString()
    {
        var source = ReadEmbeddedResource("expression-evaluator.js");
        StringAssert.Contains(source, "literalString");
    }

    /// <summary>
    /// The effect-applier.js module must be embedded in the Rendering assembly.
    /// </summary>
    [TestMethod]
    public void EffectApplierModule_IsEmbeddedResource()
    {
        var resourceNames = RenderingAssembly.GetManifestResourceNames();
        var match = resourceNames.FirstOrDefault(name =>
            name.EndsWith("effect-applier.js", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(match, "effect-applier.js must be an embedded resource in ScadaBuilderV2.Rendering");
    }

    /// <summary>
    /// The state-engine.js module must be embedded in the Rendering assembly.
    /// </summary>
    [TestMethod]
    public void StateEngineModule_IsEmbeddedResource()
    {
        var resourceNames = RenderingAssembly.GetManifestResourceNames();
        var match = resourceNames.FirstOrDefault(name =>
            name.EndsWith("state-engine.js", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(match, "state-engine.js must be an embedded resource in ScadaBuilderV2.Rendering");
    }

    /// <summary>
    /// The animation-controller.js module must be embedded in the Rendering assembly.
    /// </summary>
    [TestMethod]
    public void AnimationControllerModule_IsEmbeddedResource()
    {
        var resourceNames = RenderingAssembly.GetManifestResourceNames();
        var match = resourceNames.FirstOrDefault(name =>
            name.EndsWith("animation-controller.js", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(match, "animation-controller.js must be an embedded resource in ScadaBuilderV2.Rendering");
    }

    /// <summary>
    /// The command-dispatcher.js module must be embedded and expose
    /// WriteTag, Navigate, Toggle, and confirmation-gate symbols.
    /// </summary>
    [TestMethod]
    public void CommandDispatcherModule_IsEmbeddedResource()
    {
        var resourceNames = RenderingAssembly.GetManifestResourceNames();
        var match = resourceNames.FirstOrDefault(name =>
            name.EndsWith("command-dispatcher.js", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(match, "command-dispatcher.js must be an embedded resource in ScadaBuilderV2.Rendering");

        var source = ReadEmbeddedResource("command-dispatcher.js");
        StringAssert.Contains(source, "WriteTag");
        StringAssert.Contains(source, "Navigate");
        StringAssert.Contains(source, "Toggle");
        StringAssert.Contains(source, "confirm");
    }

    /// <summary>
    /// The exporter serializes <c>ScadaCommandKind</c> / <c>ScadaWriteMode</c> as
    /// camelCase enum values (JsonStringEnumConverter(CamelCase); locked by
    /// Ft100SceneExporterTests asserting <c>"Kind": "navigate"</c>). command-dispatcher.js
    /// dispatches on <c>cmd.kind</c> / <c>cmd.writeMode</c>, so its switch case labels MUST
    /// use those exact camelCase values — otherwise no command (navigate, WriteTag, popups)
    /// ever executes at runtime.
    /// </summary>
    [TestMethod]
    public void CommandDispatcher_DispatchesCamelCaseKindsMatchingExporterSerialization()
    {
        var source = ReadEmbeddedResource("command-dispatcher.js");

        foreach (var kind in new[] { "writeTag", "navigate", "openPopup", "closePopup", "togglePopup", "openUrl", "back" })
        {
            StringAssert.Contains(source, $"case '{kind}':");
        }

        foreach (var mode in new[] { "momentary", "toggle", "setFixed", "setFromInput" })
        {
            StringAssert.Contains(source, $"case '{mode}':");
        }

        foreach (var stale in new[] { "case 'Navigate':", "case 'WriteTag':", "case 'OpenPopup':", "case 'Momentary':", "case 'SetFixed':" })
        {
            Assert.IsFalse(
                source.Contains(stale, StringComparison.Ordinal),
                $"PascalCase case label never matches the camelCase runtime payload: {stale}");
        }

        // TRIGGER_MAP keys are looked up by cmd.trigger, also serialized camelCase.
        // PascalCase keys make non-click triggers silently fall back to 'click'.
        foreach (var trigger in new[] { "onClick:", "onRelease:", "onHover:", "onHoverEnter:", "onHoverExit:" })
        {
            StringAssert.Contains(source, trigger);
        }
        Assert.IsFalse(
            source.Contains("OnClick:", StringComparison.Ordinal),
            "TRIGGER_MAP must key on camelCase trigger values matching the exporter serialization.");
    }

    /// <summary>
    /// The tag-bridge.js module must be embedded and expose
    /// getTagValue, writeTag, and reference window.tf100webScadaBuilder.
    /// </summary>
    [TestMethod]
    public void TagBridgeModule_IsEmbeddedResource()
    {
        var resourceNames = RenderingAssembly.GetManifestResourceNames();
        var match = resourceNames.FirstOrDefault(name =>
            name.EndsWith("tag-bridge.js", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(match, "tag-bridge.js must be an embedded resource in ScadaBuilderV2.Rendering");

        var source = ReadEmbeddedResource("tag-bridge.js");
        StringAssert.Contains(source, "getTagValue");
        StringAssert.Contains(source, "writeTag");
        StringAssert.Contains(source, "tf100webScadaBuilder");
    }

    /// <summary>
    /// The input-edit-guard.js module must be embedded and contain
    /// the EDIT_TIMEOUT constant (30000) and the scada-input-edit-overlay class name.
    /// </summary>
    [TestMethod]
    public void InputEditGuardModule_IsEmbeddedResource()
    {
        var resourceNames = RenderingAssembly.GetManifestResourceNames();
        var match = resourceNames.FirstOrDefault(name =>
            name.EndsWith("input-edit-guard.js", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(match, "input-edit-guard.js must be an embedded resource in ScadaBuilderV2.Rendering");

        var source = ReadEmbeddedResource("input-edit-guard.js");
        StringAssert.Contains(source, "30000");
        StringAssert.Contains(source, "scada-input-edit-overlay");
    }

    /// <summary>
    /// The confirmation-modal.js module must be embedded and expose
    /// the showConfirmation function on window.ScadaRuntime.
    /// </summary>
    [TestMethod]
    public void ConfirmationModalModule_IsEmbeddedResource()
    {
        var resourceNames = RenderingAssembly.GetManifestResourceNames();
        var match = resourceNames.FirstOrDefault(name =>
            name.EndsWith("confirmation-modal.js", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(match, "confirmation-modal.js must be an embedded resource in ScadaBuilderV2.Rendering");

        var source = ReadEmbeddedResource("confirmation-modal.js");
        StringAssert.Contains(source, "showConfirmation");
    }

    /// <summary>
    /// GetRuntimeScript must return a non-empty concatenated script that contains
    /// all module namespaces and the public API symbols.
    /// </summary>
    [TestMethod]
    public void GetRuntimeScript_ReturnsConcatenatedModules()
    {
        var script = Ft100SceneExporter.GetRuntimeScript();

        Assert.IsFalse(string.IsNullOrWhiteSpace(script), "GetRuntimeScript must return non-empty content");

        StringAssert.Contains(script, "ExpressionEvaluator");
        StringAssert.Contains(script, "EffectApplier");
        StringAssert.Contains(script, "StateEngine");
        StringAssert.Contains(script, "AnimationController");
        StringAssert.Contains(script, "CommandDispatcher");
        StringAssert.Contains(script, "TagBridge");
        StringAssert.Contains(script, "InputEditGuard");
        StringAssert.Contains(script, "showConfirmation");
        StringAssert.Contains(script, "initPage");
        StringAssert.Contains(script, "onTagValuesChanged");
    }

    /// <summary>
    /// Reads an embedded JS resource from the Rendering assembly.
    /// </summary>
    private static string ReadEmbeddedResource(string resourceFileName)
    {
        var resourceNames = RenderingAssembly.GetManifestResourceNames();
        var match = resourceNames.FirstOrDefault(name =>
            name.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(match, $"Embedded resource '{resourceFileName}' not found in {RenderingAssembly.FullName}");

        using var stream = RenderingAssembly.GetManifestResourceStream(match);
        Assert.IsNotNull(stream, $"Failed to open stream for '{match}'");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
