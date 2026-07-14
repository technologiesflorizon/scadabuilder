namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class SceneWorkspaceTabContractTests
{
    [TestMethod]
    public void MainWorkspaceSupportsMultipleClosableSceneTabsWithDirtyPrompt()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var shell = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");
        var controller = ReadProjectFile("src", "ScadaBuilderV2.App", "Pages", "PageWorkspaceController.cs");
        var tab = ReadProjectFile("src", "ScadaBuilderV2.App", "Workspace", "SceneWorkspaceTab.cs");

        StringAssert.Contains(xaml, "x:Name=\"SceneTabs\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnSceneTabSelectionChanged\"");
        StringAssert.Contains(xaml, "Click=\"OnCloseSceneTabClick\"");
        StringAssert.Contains(xaml, "ToolTip=\"Fermer la scene\"");

        StringAssert.Contains(controller, "ObservableCollection<SceneWorkspaceTab> OpenTabs");
        StringAssert.Contains(controller, "OpenAsync(Guid pageKey");
        StringAssert.Contains(controller, "ActivateAsync(SceneWorkspaceTab tab)");
        StringAssert.Contains(controller, "CloseAsync(SceneWorkspaceTab tab)");
        StringAssert.Contains(controller, "host.ConfirmCloseDirtyPageAsync(tab)");
        StringAssert.Contains(shell, "MessageBoxButton.YesNoCancel");
        StringAssert.Contains(shell, "SaveSceneTabAsync");
        StringAssert.Contains(shell, "Closing += OnMainWindowClosing;");
        StringAssert.Contains(tab, "PageWorkspaceEntry");
        Assert.IsFalse(tab.Contains("ReferenceScadaPage", StringComparison.Ordinal));
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate project file: {Path.Combine(parts)}");
        return "";
    }
}
