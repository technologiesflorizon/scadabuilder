using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Clicking `Rouvrir &lt;projet&gt;` or `Ouvrir` in the recent list gave no sign that anything was happening,
/// and nothing stopped a second gesture being launched on top of the first. A veil now covers the whole window
/// for the length of a project gesture: it says what is running, it swallows the clicks and it swallows the
/// keys.
/// </summary>
/// <remarks>
/// The property that matters is not that the calls exist but *where* they sit, so every assertion here is made
/// against a brace-matched block rather than against the position of a word. `IndexOf("try")` would match
/// `retry`, a comment, or a string; it would also happily accept a `BeginBusy` sitting after the block it is
/// supposed to open. The source is stripped of comments and string bodies first, so nothing written in prose
/// can satisfy a structural assertion.
///
/// The veil must go up inside the `try` and come down in the `finally`, because a gesture that throws is
/// exactly the case the veil would otherwise turn into a dead window. The `finally` alone is not enough: a
/// `catch` runs before it and raises a modal box, which would sit in front of a still-spinning ring, so each
/// `catch` lowers the veil first.
///
/// This reads the sources as text on purpose. The test project does not reference `ScadaBuilderV2.App`, so no
/// test in this repository can instantiate a window or a `Border`; two dialogs have already shipped broken for
/// exactly that reason. The counting behind the veil is testable, and is tested for real in
/// `BusyOverlayControllerTests`.
///
/// Decisions: `DEC-0049`.
/// Contracts: `docs/06_ui_ux/UI_ARCHITECTURE_V2.md` sections 1 and 2.
/// Tests: this file. Surface: `MainWindow.xaml`, `MainWindow.xaml.cs`, `Projects/WpfConversionConsent.cs`.
/// </remarks>
[TestClass]
public sealed class ProjectLoadingOverlayContractTests
{
    [TestMethod]
    public void TheGestureBoundaryRaisesTheVeilInsideTheTryAndLowersItInTheFinally()
    {
        var body = GestureBoundaryBody();

        var tryBlock = ReadBlock(body, "try");
        var beginIndex = tryBlock.IndexOf("BeginBusy(", StringComparison.Ordinal);
        var actionIndex = tryBlock.IndexOf("await action()", StringComparison.Ordinal);

        Assert.IsTrue(
            beginIndex >= 0,
            "the veil must go up inside the `try` block itself. Raised before it, a throw from the raise "
            + "escapes the boundary that exists to keep these `async void` handlers from killing the "
            + "application.");
        Assert.IsTrue(
            actionIndex > beginIndex,
            "the veil must be up before the gesture is awaited, otherwise nothing is shown while it runs.");
        Assert.AreEqual(
            1,
            Occurrences(body, "BeginBusy("),
            "the veil is raised in exactly one place; a second raise would need a second lowering to match.");

        var finallyBlock = ReadBlock(body, "finally");
        StringAssert.Contains(
            finallyBlock,
            "LowerVeilOnce()",
            "the `finally` is the guarantee: lowered anywhere else alone, a gesture that throws leaves the "
            + "window veiled and inert.");
        Assert.AreEqual(
            1,
            Occurrences(body, "EndBusy()"),
            "the counter must be decremented from exactly one place, so that the `catch` path and the "
            + "`finally` path cannot pop the same gesture twice.");
    }

    [TestMethod]
    public void EveryCatchLowersTheVeilBeforeItPresentsAnything()
    {
        var body = GestureBoundaryBody();
        var catches = ReadAllBlocks(body, "catch");

        Assert.AreEqual(
            3,
            catches.Count,
            "the boundary still catches the activation failure, the cancellation and the rest.");

        foreach (var block in catches)
        {
            var lowered = block.IndexOf("LowerVeilOnce()", StringComparison.Ordinal);
            Assert.IsTrue(
                lowered >= 0,
                "every `catch` must lower the veil itself. `catch` runs before `finally`, so leaving it to "
                + $"the `finally` shows the message with the ring still spinning behind it. Block: {block}");

            var presented = FirstIndexOfAny(block, "PresentProjectFailure(", "SetStatus(");
            Assert.IsTrue(
                presented > lowered,
                "the veil must be down *before* the message is presented: `MessageBox.Show` is modal and "
                + $"blocks until the operator dismisses it. Block: {block}");
        }
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
            "IsHitTestVisible=\"True\"",
            "freezing the other commands is half the request; the veil has to be the thing that receives the "
            + "click.");

        var background = Regex.Match(tag, "Background=\"([^\"]*)\"");
        Assert.IsTrue(
            background.Success,
            "the veil must carry a `Background`. A `Border` whose brush is null is not hit-tested at all, so "
            + "every click would pass straight through to the ribbon it is meant to freeze.");

        var brush = background.Groups[1].Value.Trim();
        Assert.AreNotEqual(
            string.Empty,
            brush,
            "`Background=\"\"` is not a brush: it leaves the property null and the veil transparent to the "
            + "pointer. A fully transparent *colour* would be fine - WPF hit-tests those - but nothing is not "
            + "a colour.");
        Assert.IsFalse(
            brush.Equals("{x:Null}", StringComparison.OrdinalIgnoreCase),
            "`{x:Null}` is the explicit spelling of the same defect: no brush, no hit testing, no freeze.");

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
    public void TheVeilStepsAsideForEveryQuestionAskedInsideAGesture()
    {
        var mainWindow = ReadAppFile("MainWindow.xaml.cs");
        var consent = ReadAppFile(Path.Combine("Projects", "WpfConversionConsent.cs"));

        AssertSuspendedBefore(
            MethodBody(mainWindow, "private async Task OpenProjectInteractiveAsync("),
            "ShowDialog(this)",
            "the file picker");
        AssertSuspendedBefore(
            MethodBody(mainWindow, "private async Task CreateProjectInteractiveAsync("),
            "ShowDialog()",
            "the creation dialog");
        AssertSuspendedBefore(
            MethodBody(mainWindow, "IProjectLifecycleHost.RequestCloseDecisionAsync("),
            "ShowDialog()",
            "the unsaved-changes dialog");
        AssertSuspendedBefore(
            MethodBody(consent, "public Task<ConversionDecision> RequestAsync("),
            "dialog.ShowDialog()",
            "the conversion consent dialog");

        StringAssert.Contains(
            consent,
            "IBusyOverlaySuspender",
            "the conversion dialog receives the narrow suspension capability, not the whole window.");
    }

    [TestMethod]
    public void NoFailureIsEverShownWithTheVeilStillUp()
    {
        var mainWindow = ReadAppFile("MainWindow.xaml.cs");

        AssertSuspendedBefore(
            MethodBody(mainWindow, "private void PresentProjectFailure("),
            "MessageBox.Show(",
            "the gesture failure box");
        AssertSuspendedBefore(
            MethodBody(mainWindow, "private void PresentProjectRepositoryFailure("),
            "MessageBox.Show(",
            "the repository diagnostic box");
    }

    [TestMethod]
    public void TheVeilFreezesTheKeyboardAndNotOnlyThePointer()
    {
        var xaml = ReadAppFile("MainWindow.xaml");
        var mainWindow = ReadAppFile("MainWindow.xaml.cs");

        StringAssert.Contains(
            xaml,
            "PreviewKeyDown=\"OnWindowPreviewKeyDown\"",
            "the window itself must see every key first. A welcome button that already had focus re-fires on "
            + "Enter or Space, which is two project gestures at once - the thing the freeze exists to stop.");

        var handler = MethodBody(mainWindow, "private void OnWindowPreviewKeyDown(");
        StringAssert.Contains(handler, "_busyOverlay.State.IsVisible", "the freeze follows the veil, nothing else.");
        StringAssert.Contains(
            handler,
            "e.Handled = true",
            "the key must be marked handled, or the input binding and the focused element still receive it.");

        Assert.AreEqual(
            2,
            Occurrences(xaml, "CanExecute=\"OnSceneHistoryCommandCanExecute\""),
            "`Ctrl+Z` and `Ctrl+Y` are declared on the window and reach the editor without the pointer; both "
            + "command bindings must refuse to execute while a project gesture holds the veil up.");
        StringAssert.Contains(
            MethodBody(mainWindow, "private void OnSceneHistoryCommandCanExecute("),
            "e.CanExecute = !_busyOverlay.State.IsVisible",
            "undo and redo are unavailable for exactly as long as the veil is up, and no longer.");
    }

    private static string GestureBoundaryBody() =>
        MethodBody(ReadAppFile("MainWindow.xaml.cs"), "private async Task RunProjectGestureAsync(");

    /// <summary>
    /// Returns one method body, comments and string bodies blanked. The extraction runs on the raw file so
    /// that the blanking never has to survive a raw string literal, of which this shell has several.
    /// </summary>
    private static string MethodBody(string source, string signatureStart) =>
        Sanitise(ReadMethodBody(source, signatureStart));

    private static void AssertSuspendedBefore(string body, string call, string what)
    {
        var suspended = body.IndexOf("Suspend()", StringComparison.Ordinal);
        Assert.IsTrue(
            suspended >= 0,
            $"{what} is a question, not loading: a ring spinning behind it reads as a frozen application, so "
            + $"the veil must be suspended around it. Body: {body}");

        var shown = body.IndexOf(call, StringComparison.Ordinal);
        Assert.IsTrue(
            shown > suspended,
            $"the veil has to be down before {what} is shown, not after it returns. Body: {body}");
    }

    private static int FirstIndexOfAny(string source, params string[] needles)
    {
        var best = -1;
        foreach (var needle in needles)
        {
            var index = source.IndexOf(needle, StringComparison.Ordinal);
            if (index >= 0 && (best < 0 || index < best)) best = index;
        }

        Assert.IsTrue(best >= 0, $"none of [{string.Join(", ", needles)}] was found in: {source}");
        return best;
    }

    private static int Occurrences(string source, string needle)
    {
        var count = 0;
        for (var index = source.IndexOf(needle, StringComparison.Ordinal);
             index >= 0;
             index = source.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>Returns the body of the first `keyword ... { }` block, braces balanced.</summary>
    private static string ReadBlock(string source, string keyword, int from = 0)
    {
        var blocks = ReadAllBlocks(source, keyword, from, stopAfterFirst: true);
        Assert.AreEqual(1, blocks.Count, $"no `{keyword}` block was found in: {source}");
        return blocks[0];
    }

    private static IReadOnlyList<string> ReadAllBlocks(
        string source,
        string keyword,
        int from = 0,
        bool stopAfterFirst = false)
    {
        var blocks = new List<string>();
        var pattern = new Regex(@"(?<![A-Za-z0-9_])" + Regex.Escape(keyword) + @"(?![A-Za-z0-9_])[^{;}]*\{");
        var cursor = from;

        while (cursor < source.Length)
        {
            var match = pattern.Match(source, cursor);
            if (!match.Success) break;

            var open = match.Index + match.Length - 1;
            var end = MatchingBrace(source, open);
            blocks.Add(source[(open + 1)..end]);
            if (stopAfterFirst) break;
            cursor = end + 1;
        }

        return blocks;
    }

    private static int MatchingBrace(string source, int open)
    {
        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return index;
        }

        Assert.Fail("unbalanced braces in the source under test.");
        return -1;
    }

    private static string ReadMethodBody(string source, string signatureStart)
    {
        var start = source.IndexOf(signatureStart, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"`{signatureStart}` was not found in the source under test.");

        var open = source.IndexOf('{', start);
        Assert.IsTrue(open >= 0, $"`{signatureStart}` has no body.");

        return source[(open + 1)..MatchingBrace(source, open)];
    }

    /// <summary>
    /// Blanks comments and string bodies, keeping every offset intact, so that a structural assertion cannot
    /// be satisfied by prose and a brace inside a literal cannot unbalance the scan.
    /// </summary>
    private static string Sanitise(string source)
    {
        var output = new StringBuilder(source);
        var index = 0;

        while (index < source.Length)
        {
            var current = source[index];

            if (current == '/' && index + 1 < source.Length && source[index + 1] == '/')
            {
                while (index < source.Length && source[index] != '\n') output[index++] = ' ';
                continue;
            }

            if (current == '/' && index + 1 < source.Length && source[index + 1] == '*')
            {
                while (index < source.Length && !(source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/'))
                {
                    if (source[index] != '\n') output[index] = ' ';
                    index++;
                }

                continue;
            }

            if (current == '"')
            {
                var verbatim = index > 0 && source[index - 1] == '@';
                index++;
                while (index < source.Length && source[index] != '"')
                {
                    if (!verbatim && source[index] == '\\') { output[index] = ' '; index++; }
                    if (index < source.Length && source[index] != '\n') output[index] = ' ';
                    index++;
                }

                index++;
                continue;
            }

            if (current == '\'')
            {
                index++;
                while (index < source.Length && source[index] != '\'')
                {
                    if (source[index] == '\\') { output[index] = ' '; index++; }
                    if (index < source.Length) output[index] = ' ';
                    index++;
                }

                index++;
                continue;
            }

            index++;
        }

        return output.ToString();
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
