using System.Reflection;

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
