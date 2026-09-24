using System.IO;
using System.Text.RegularExpressions;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Every dockable panel declared in <c>MainWindow.xaml</c> must be reachable from the "Fenetres" menu, and
/// every menu item that reopens one must go through the reattachment helper rather than a bare
/// <c>LayoutAnchorable.Show()</c>.
/// </summary>
/// <remarks>
/// AvalonDock 4.74.1: all eleven <c>LayoutAnchorable</c>s in <c>MainWindow.xaml</c> are <c>CanClose="True"</c>,
/// and closing one detaches it from the layout tree instead of hiding it (a restored
/// <c>dock-layout.xml</c> that omits a panel produces the same detached state). <c>LayoutAnchorable.Show()</c>
/// only un-hides an anchorable that is still attached - called on a detached one it is a silent no-op - so a
/// closed panel, or one absent from the persisted layout, could never be reopened. Two panels
/// (<c>QuickWindowsAnchorable</c>, <c>DiagnosticsAnchorable</c>) additionally had no menu entry at all.
///
/// The test project does not reference <c>ScadaBuilderV2.App</c>, so no test can instantiate
/// <c>MainWindow</c> or drive AvalonDock live. These checks read the source files as text and pin the
/// contract instead, in the style of <see cref="XamlStaticResourceForwardReferenceTests"/> and
/// <see cref="TransparentBackgroundTests"/>.
///
/// Tests: this file. Surface: <c>MainWindow.xaml</c>, <c>MainWindow.xaml.cs</c>,
/// <c>MainWindow.QuickWindows.cs</c>.
/// </remarks>
[TestClass]
public sealed class DockablePanelReattachmentTests
{
    [TestMethod]
    public void EveryDockableAnchorableHasAMenuEntryUnderFenetres()
    {
        var xaml = ReadAppFile("MainWindow.xaml");

        var anchorableNames = Regex.Matches(xaml, @"<avalonDock:LayoutAnchorable\b(?<attrs>[^>]*)>")
            .Select(m => Regex.Match(m.Groups["attrs"].Value, @"x:Name=""(?<name>\w+)"""))
            .Where(m => m.Success)
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(anchorableNames.Count > 0, "expected to find at least one LayoutAnchorable in MainWindow.xaml.");

        var expectedHandlers = anchorableNames
            .Select(name => $"OnShow{name}Click")
            .ToHashSet(StringComparer.Ordinal);

        var menuBlockMatch = Regex.Match(xaml, @"<MenuItem Header=""Fenetres"">(?<body>[\s\S]*?)</MenuItem>\s*</Menu>");
        Assert.IsTrue(menuBlockMatch.Success, "could not locate the <MenuItem Header=\"Fenetres\"> block in MainWindow.xaml.");

        var actualHandlers = Regex.Matches(menuBlockMatch.Groups["body"].Value, @"Click=""(?<handler>OnShow\w+AnchorableClick)""")
            .Select(m => m.Groups["handler"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var missing = expectedHandlers.Except(actualHandlers).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.AreEqual(
            0,
            missing.Count,
            "these anchorables have no 'Fenetres' menu entry, so a user who closes them can never see them "
            + $"again: {string.Join(", ", missing)}.");

        var extra = actualHandlers.Except(expectedHandlers).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.AreEqual(
            0,
            extra.Count,
            $"these 'Fenetres' menu handlers do not name any declared anchorable: {string.Join(", ", extra)}.");
    }

    [TestMethod]
    public void NoAnchorableIsShownWithoutGoingThroughTheReattachmentHelper()
    {
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories))
        {
            var code = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(code, @"\b\w+Anchorable\.Show\(\)"))
            {
                offenders.Add($"{Path.GetFileName(file)}: {match.Value}");
            }
        }

        Assert.AreEqual(
            0,
            offenders.Count,
            "a bare '<Name>Anchorable.Show()' does nothing on a detached anchorable; every reopen must go "
            + $"through the reattachment helper instead: {string.Join(", ", offenders)}.");
    }

    [TestMethod]
    public void TheReattachmentHelperReattachesBeforeShowing()
    {
        var code = ReadAppFile("MainWindow.xaml.cs");

        var helperMatch = Regex.Match(
            code,
            @"private void ShowAnchorable\(LayoutAnchorable\? anchorable\)\s*\{(?<body>[\s\S]*?)\n    \}");
        Assert.IsTrue(helperMatch.Success, "expected a 'ShowAnchorable(LayoutAnchorable? anchorable)' helper in MainWindow.xaml.cs.");

        var body = helperMatch.Groups["body"].Value;
        StringAssert.Contains(body, "AddToLayout", "the helper must re-attach a detached anchorable via AddToLayout before showing it.");
        StringAssert.Contains(body, "Parent", "the helper must branch on whether the anchorable is still attached (Parent).");
        StringAssert.Contains(body, ".Show()", "the helper must still call Show() once the anchorable is attached.");
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

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        Assert.Fail("Unable to locate src/.");
        return string.Empty;
    }
}
