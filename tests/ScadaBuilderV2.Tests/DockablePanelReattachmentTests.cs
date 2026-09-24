using System.IO;
using System.Text.RegularExpressions;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Every dockable panel declared in <c>MainWindow.xaml</c> must be reachable from the "Fenetres" menu, and no
/// source file may touch a <c>...Anchorable</c> field's <c>Show()</c>/<c>Hide()</c>/<c>IsActive</c> directly
/// instead of going through the <c>DockablePanelPresenter</c>-backed helpers.
/// </summary>
/// <remarks>
/// This file previously also pinned the *shape* of the reattachment helper - that its body mentioned
/// <c>AddToLayout</c> and branched on <c>Parent</c>. That test passed on a helper that did not work: AvalonDock
/// 4.74.1's <c>XmlLayoutSerializer.Deserialize</c> (used with no <c>LayoutSerializationCallback</c>) replaces
/// <c>manager.Layout</c> wholesale, so the original <c>x:Name</c>-declared <c>LayoutAnchorable</c> objects are
/// never in the tree the <c>DockingManager</c> renders after a restore - and their <c>Parent</c> is never null
/// even so, because they are still parented in the old, dead tree. A guard on <c>Parent</c> therefore never
/// caught the "not in the live layout" case it was meant to catch, and the old "shape" test could not see that
/// because it never ran AvalonDock. A probe against the real assembly, kept at
/// <c>C:\Users\mathi\AppData\Local\Temp\claude\F--Groupe-AMR-SCADA-AMR-GROUP\ec1a5845-88ba-4cc4-aef7-a67fb9a349f0\scratchpad\dockprobe</c>,
/// demonstrates this. The *effect*-based coverage now lives in
/// <c>tests/ScadaBuilderV2.ShellTests/DockablePanelPresenterTests.cs</c>, which runs real AvalonDock on an STA
/// thread and asserts on <c>manager.Layout.Descendents()</c> - never on <c>Parent</c>.
///
/// What still holds and is still pinned here: every anchorable has a "Fenetres" menu entry, and no code path
/// bypasses the presenter-backed helpers to touch a declared anchorable field's <c>Show</c>/<c>Hide</c>/
/// <c>IsActive</c> directly - such a read or write would report the dead tree's state, not the live one's.
///
/// The test project does not reference <c>ScadaBuilderV2.App</c>, so no test here can instantiate
/// <c>MainWindow</c> or drive AvalonDock live. These checks read the source files as text and pin the
/// contract instead, in the style of <see cref="XamlStaticResourceForwardReferenceTests"/> and
/// <see cref="TransparentBackgroundTests"/>.
///
/// Tests: this file, tests/ScadaBuilderV2.ShellTests/DockablePanelPresenterTests.cs. Surface:
/// <c>MainWindow.xaml</c>, <c>MainWindow.xaml.cs</c>, <c>MainWindow.QuickWindows.cs</c>,
/// <c>Shell/DockablePanelPresenter.cs</c>.
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
    public void NoAnchorableIsTouchedDirectlyOutsideThePresenterBackedHelpers()
    {
        var offenders = new List<string>();

        // A bare `<Name>Anchorable.Show()/Hide()/Close()` or a direct `.IsActive =`/`.IsVisible`/`.IsSelected`/
        // `.Parent` access reports the state of the dead XAML-declared tree, not the live AvalonDock one, once
        // a saved layout has been restored - see the remarks on DockablePanelPresenter for why. The only
        // legitimate direct touch is inside DockablePanelPresenter itself (which resolves the live anchorable
        // first) and inside OnAnchorableClosing, whose `anchorable` local is the live object AvalonDock itself
        // raised the Closing event for, not a stale declared field.
        // `LayoutAnchorable` is the AvalonDock type name itself (e.g. in XML doc text or `is LayoutAnchorable`
        // patterns) and is excluded - only a declared field name ending in "Anchorable" is an offender.
        var offendingPattern = new Regex(@"\b(?!LayoutAnchorable\b)\w+Anchorable\.(Show\(\)|Hide\(\)|Close\(\)|IsActive\s*=|IsVisible\b|IsSelected\b|Parent\b)");

        foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, "DockablePanelPresenter.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (string.Equals(fileName, "MainWindow.xaml.cs", StringComparison.Ordinal)
                    && lines[i].Contains("anchorable.Hide()", StringComparison.Ordinal))
                {
                    // OnAnchorableClosing: `anchorable` here is the live object AvalonDock passed to the
                    // Closing event, not a stale `x:Name`-declared field - not an offender.
                    continue;
                }

                foreach (Match match in offendingPattern.Matches(lines[i]))
                {
                    offenders.Add($"{fileName}:{i + 1}: {match.Value}");
                }
            }
        }

        Assert.AreEqual(
            0,
            offenders.Count,
            "these touch a declared '...Anchorable' field directly instead of going through "
            + $"ShowAnchorable/HideAnchorable/ActivateAnchorable (DockablePanelPresenter): {string.Join(", ", offenders)}.");
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
