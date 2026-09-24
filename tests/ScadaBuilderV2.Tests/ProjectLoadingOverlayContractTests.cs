using System.IO;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Clicking `Rouvrir &lt;projet&gt;` or `Ouvrir` in the recent list gave no sign that anything was happening,
/// and nothing stopped a second gesture being launched on top of the first. A veil now covers the whole window
/// for the length of a project gesture: it says what is running and it swallows the clicks.
/// </summary>
/// <remarks>
/// The property that matters is not that the two calls exist but *where* they sit. The veil must go up inside
/// the `try` and come down in the `finally`, because a gesture that throws is exactly the case the veil would
/// otherwise turn into a dead window - and the failure box must not be raised behind it. A test that only
/// looked for `BeginBusy` and `EndBusy` somewhere in the method would pass on a version that lowers the veil
/// in the happy path alone, which is the broken version.
///
/// This reads the sources as text on purpose. The test project does not reference `ScadaBuilderV2.App`, so no
/// test in this repository can instantiate a window or a `Border`; two dialogs have already shipped broken for
/// exactly that reason. The counting behind the veil is testable, and is tested for real in
/// `BusyOverlayControllerTests`.
///
/// Tests: this file. Surface: `MainWindow.xaml`, `MainWindow.xaml.cs`, `Projects/WpfConversionConsent.cs`.
/// </remarks>
[TestClass]
public sealed class ProjectLoadingOverlayContractTests
{
    [TestMethod]
    public void TheGestureBoundaryRaisesTheVeilInsideTheTryAndLowersItInTheFinally()
    {
        var body = ExtractMethodBody(
            ReadAppFile("MainWindow.xaml.cs"),
            "private async Task RunProjectGestureAsync(");

        var tryIndex = body.IndexOf("try", StringComparison.Ordinal);
        var beginIndex = body.IndexOf("BeginBusy(", StringComparison.Ordinal);
        var actionIndex = body.IndexOf("await action()", StringComparison.Ordinal);
        var finallyIndex = body.IndexOf("finally", StringComparison.Ordinal);
        var endIndex = body.IndexOf("EndBusy()", StringComparison.Ordinal);

        Assert.IsTrue(tryIndex >= 0, "the error boundary must still open with a `try`.");
        Assert.IsTrue(beginIndex >= 0, "`RunProjectGestureAsync` must raise the busy veil.");
        Assert.IsTrue(actionIndex >= 0, "the gesture itself must still be awaited here.");
        Assert.IsTrue(finallyIndex >= 0, "`RunProjectGestureAsync` must carry a `finally`.");
        Assert.IsTrue(endIndex >= 0, "`RunProjectGestureAsync` must lower the busy veil.");

        Assert.IsTrue(
            beginIndex > tryIndex,
            "the veil must go up inside the `try`. Raised before it, a throw from the raise itself escapes the "
            + "boundary that exists to keep these handlers from killing the application.");
        Assert.IsTrue(
            actionIndex > beginIndex,
            "the veil must be up before the gesture is awaited, otherwise nothing is shown while it runs.");
        Assert.IsTrue(
            finallyIndex > actionIndex,
            "the `finally` must close the boundary, after the awaited gesture and its `catch` clauses.");
        Assert.IsTrue(
            endIndex > finallyIndex,
            "the veil must come down in the `finally`. Lowered anywhere else, a gesture that throws leaves the "
            + "window veiled and inert, and the failure box appears behind it.");
        Assert.AreEqual(
            endIndex,
            body.LastIndexOf("EndBusy()", StringComparison.Ordinal),
            "the veil must come down in exactly one place; a second lowering would unbalance the counter.");
    }

    [TestMethod]
    public void TheVeilIsCollapsedByDefaultAndSwallowsClicksWhenItIsUp()
    {
        var xaml = ReadAppFile("MainWindow.xaml");

        var overlayIndex = xaml.IndexOf("<Border x:Name=\"ProjectBusyOverlay\"", StringComparison.Ordinal);
        Assert.IsTrue(overlayIndex >= 0, "`MainWindow.xaml` must declare the `ProjectBusyOverlay` element.");

        var tag = xaml[overlayIndex..xaml.IndexOf('>', overlayIndex)];

        StringAssert.Contains(
            tag,
            "Visibility=\"Collapsed\"",
            "the veil must start folded away. Shipped visible, it hides the editor from the first frame.");
        StringAssert.Contains(
            tag,
            "Background=\"",
            "the veil must carry a background, however transparent. A `Border` without one is not hit-tested "
            + "at all, so every click would pass straight through to the ribbon it is meant to freeze.");
        StringAssert.Contains(
            tag,
            "IsHitTestVisible=\"True\"",
            "freezing the other commands is half the request; the veil has to be the thing that receives the "
            + "click.");

        Assert.IsTrue(
            xaml.LastIndexOf("</DockPanel>", StringComparison.Ordinal) < overlayIndex,
            "the veil must be a sibling of the shell root `DockPanel`, declared after it, so that it covers "
            + "the ribbon, the side panels and the canvas at once rather than the canvas alone.");
    }

    [TestMethod]
    public void TheRingSpinsFromMarkupAloneWithoutAnyNewDependency()
    {
        var xaml = ReadAppFile("MainWindow.xaml");
        var overlayIndex = xaml.IndexOf("<Border x:Name=\"ProjectBusyOverlay\"", StringComparison.Ordinal);
        Assert.IsTrue(overlayIndex >= 0, "`MainWindow.xaml` must declare the `ProjectBusyOverlay` element.");

        var veil = xaml[overlayIndex..];

        StringAssert.Contains(veil, "<RotateTransform", "the ring turns by a rotation, not by a GIF or an image.");
        StringAssert.Contains(veil, "<Storyboard>", "the rotation is animated by a storyboard declared in markup.");
        StringAssert.Contains(veil, "Duration=\"0:0:1\"", "one second per turn.");
        StringAssert.Contains(
            veil,
            "RepeatBehavior=\"Forever\"",
            "the ring must keep turning for as long as the veil is up.");
        StringAssert.Contains(
            veil,
            "x:Name=\"ProjectBusyDetailTextBlock\"",
            "the second line carries the project name when the gesture knows it.");
        StringAssert.Contains(
            xaml[xaml.IndexOf("x:Name=\"ProjectBusyDetailTextBlock\"", StringComparison.Ordinal)..],
            "Visibility=\"Collapsed\"",
            "with no project name to show, the second line is hidden rather than left blank.");
    }

    [TestMethod]
    public void TheVeilStepsAsideForTheTwoQuestionsAskedInsideAGesture()
    {
        var mainWindow = ReadAppFile("MainWindow.xaml.cs");
        var consent = ReadAppFile(Path.Combine("Projects", "WpfConversionConsent.cs"));

        var picker = ExtractMethodBody(mainWindow, "private async Task OpenProjectInteractiveAsync(");
        StringAssert.Contains(
            picker,
            "Suspend()",
            "the file picker is a question, not loading: a ring spinning behind it reads as a frozen "
            + "application.");
        Assert.IsTrue(
            picker.IndexOf("Suspend()", StringComparison.Ordinal)
                < picker.IndexOf("ShowDialog(this)", StringComparison.Ordinal),
            "the veil has to be down before the picker is shown, not after it returns.");

        StringAssert.Contains(
            consent,
            "IBusyOverlaySuspender",
            "the conversion dialog receives the narrow suspension capability, not the whole window.");
        Assert.IsTrue(
            consent.IndexOf("Suspend()", StringComparison.Ordinal)
                < consent.IndexOf("dialog.ShowDialog()", StringComparison.Ordinal),
            "the consent dialog must be asked with the veil already down.");
    }

    private static string ExtractMethodBody(string source, string signatureStart)
    {
        var start = source.IndexOf(signatureStart, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"`{signatureStart}` was not found in the source under test.");

        var open = source.IndexOf('{', start);
        Assert.IsTrue(open >= 0, $"`{signatureStart}` has no body.");

        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return source[(open + 1)..index];
            }
        }

        Assert.Fail($"`{signatureStart}` has an unbalanced body.");
        return string.Empty;
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
