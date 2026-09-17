using System.IO;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// A fully transparent background is how an operator builds an invisible zone that still carries events - a
/// click target over a diagram region, for instance. The style model always accepted `"Transparent"` (a group
/// is created with it), and the exporter always emitted it, but `ElementPropertiesDialog` could not express it:
/// the background always took a concrete colour from its picker, defaulting to `#FFFFFF`, while the border next
/// to it had a `Transparent` checkbox.
/// </summary>
/// <remarks>
/// The property that matters is not that the surface is invisible but that it is still **hit-testable**. In
/// CSS a transparent background still receives pointer events, and in SVG `fill="transparent"` does too -
/// unlike `fill="none"`, which does not. These tests pin that distinction, because a zone that is invisible
/// *and* unclickable would satisfy a naive "is it transparent" assertion while being useless for the purpose
/// the feature exists to serve.
///
/// Tests: this file. Surface: `ElementPropertiesDialog` (source contract below, as no unit test can drive a
/// WPF dialog).
/// </remarks>
[TestClass]
public sealed class TransparentBackgroundTests
{
    [TestMethod]
    public async Task ATransparentShapeStaysHitTestableInTheExportedPage()
    {
        var element = new ScadaElement(
            "zone_001",
            "Zone invisible",
            ScadaElementKind.Shape,
            new SceneBounds(10, 20, 200, 120),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText with { Background = "Transparent" },
            null);

        var (html, _) = await ExportSingleElementAsync(element, "transparent_shape");

        StringAssert.Contains(
            html,
            "fill=\"Transparent\"",
            "the shape's fill must carry the transparent value itself. `fill=\"none\"` would look identical "
            + "and receive no pointer events, which is the one thing an invisible click zone must not do.");
        Assert.IsFalse(
            html.Contains("fill=\"none\"", StringComparison.OrdinalIgnoreCase),
            "`none` is not a synonym for transparent here: it removes the element from hit testing.");
    }

    [TestMethod]
    public async Task ATransparentNonShapeKeepsItsBackgroundDeclarationRatherThanLosingIt()
    {
        var element = new ScadaElement(
            "zone_002",
            "Zone invisible",
            ScadaElementKind.Button,
            new SceneBounds(10, 20, 200, 120),
            null,
            ScadaElementLayout.Absolute,
            ScadaElementStyle.DefaultText with { Background = "Transparent" },
            null);

        var (_, css) = await ExportSingleElementAsync(element, "transparent_button");

        StringAssert.Contains(
            css,
            "background: Transparent;",
            "the declaration must survive to the page: an element whose background is simply dropped inherits "
            + "whatever is behind it, which is not the same thing and is not stable.");
    }

    /// <summary>
    /// Source contract: the dialog must be able to express a transparent background at all. No unit test can
    /// drive a WPF dialog, so this pins the wiring that carries the value from the checkbox to the style.
    /// </summary>
    [TestMethod]
    public void ThePropertiesDialogCanExpressATransparentBackground()
    {
        var xaml = ReadAppFile(Path.Combine("ElementPropertiesDialog.xaml"));
        var code = ReadAppFile(Path.Combine("ElementPropertiesDialog.xaml.cs"));

        StringAssert.Contains(xaml, "x:Name=\"BackgroundTransparentCheckBox\"");
        StringAssert.Contains(
            code,
            "Background: BackgroundTransparentCheckBox.IsChecked == true",
            "the checkbox must decide the persisted value - a checkbox wired only to the preview would look "
            + "correct on screen and save an opaque colour.");
        StringAssert.Contains(
            code,
            "BackgroundTransparentCheckBox.IsChecked = isBackgroundTransparent",
            "reopening an element that already carries a transparent background must show the box ticked, "
            + "otherwise saving twice silently turns it opaque.");
    }

    private static async Task<(string Html, string Css)> ExportSingleElementAsync(ScadaElement element, string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        Directory.CreateDirectory(sourceRoot);
        var sourceHtmlPath = Path.Combine(sourceRoot, $"{name}.html");
        await File.WriteAllTextAsync(sourceHtmlPath, "<!doctype html><html><body><div class=\"page\"></div></body></html>");

        var scene = ScadaScene.CreateEmpty("win00008", name, new(1280, 873)).WithElement(element);
        try
        {
            var result = await new Ft100SceneExporter().ExportAsync(scene, sourceHtmlPath, Path.Combine(root, "export"));
            return (await File.ReadAllTextAsync(result.HtmlPath), await File.ReadAllTextAsync(result.CssPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string ReadAppFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{relativePath}.");
        return string.Empty;
    }
}
