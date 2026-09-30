namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Source-contract pin (controller fix round 1, Task 11): `RefreshQuickWindowPageSurfaces` must read the
/// active page's type from the active scene tab, not from `_modernProject.Scenes` — the latter observes
/// the pre-mutation project when a `page.set-type` result runs through `OnPageCommandCompleted`, since
/// `PageWorkspaceController.ReplaceProject` updates every open tab's `Page` (including the active one)
/// before `OnPageCommandCompleted` finishes. `OnPageCommandCompleted` must also call the refresh so any
/// page-property command (not only a fresh tab activation) is reflected immediately.
/// </summary>
/// <remarks>
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: this file.
/// </remarks>
[TestClass]
public sealed class QuickWindowPageSurfaceRefreshContractTests
{
    [TestMethod]
    public void RefreshQuickWindowPageSurfacesReadsTheActiveTabsPageTypeNotTheProjectScenes()
    {
        var source = ReadAppFile("MainWindow.QuickWindows.cs");
        var method = ExtractMethodBody(source, "RefreshQuickWindowPageSurfaces");

        StringAssert.Contains(method, "_activeSceneTab?.Page.Type == ScadaPageType.QuickWindow",
            "the refresh must read the active tab's own Page, the same source BuildQuickWindowAuthoringContext uses.");
        Assert.IsFalse(
            method.Contains("_modernProject?.Scenes", StringComparison.Ordinal) ||
            method.Contains("_modernProject.Scenes", StringComparison.Ordinal),
            "the refresh must never re-resolve the page type from _modernProject.Scenes: " +
            "that observes the pre-mutation project when OnPageCommandCompleted is still running.");
    }

    [TestMethod]
    public void OnPageCommandCompletedRefreshesTheQuickWindowPageSurfacesAfterReplacingTheProject()
    {
        var source = ReadAppFile("MainWindow.xaml.cs");
        var method = ExtractMethodBody(source, "OnPageCommandCompleted");

        var replaceIndex = method.IndexOf("_pageWorkspaceController.ReplaceProject", StringComparison.Ordinal);
        var refreshIndex = method.IndexOf("RefreshQuickWindowPageSurfaces()", StringComparison.Ordinal);
        Assert.IsTrue(replaceIndex >= 0, "OnPageCommandCompleted must call ReplaceProject.");
        Assert.IsTrue(refreshIndex >= 0, "OnPageCommandCompleted must call RefreshQuickWindowPageSurfaces so a page-property command (e.g. page.set-type) is reflected immediately.");
        Assert.IsTrue(refreshIndex > replaceIndex, "the refresh must run after ReplaceProject updated every open tab's Page.");
    }

    /// <summary>Extracts one method's brace-balanced body by name, naive but sufficient for a source pin.</summary>
    private static string ExtractMethodBody(string source, string methodName)
    {
        var nameIndex = source.IndexOf($" {methodName}(", StringComparison.Ordinal);
        Assert.IsTrue(nameIndex >= 0, $"Method '{methodName}' not found.");
        var openBrace = source.IndexOf('{', nameIndex);
        Assert.IsTrue(openBrace >= 0, $"Opening brace of '{methodName}' not found.");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0) return source[openBrace..(i + 1)];
            }
        }

        Assert.Fail($"Closing brace of '{methodName}' not found.");
        return string.Empty;
    }

    private static string ReadAppFile(string relativePath, string subdirectory = "")
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", subdirectory, relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{subdirectory}/{relativePath}.");
        return string.Empty;
    }
}
