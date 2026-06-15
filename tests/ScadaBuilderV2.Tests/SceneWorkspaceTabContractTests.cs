namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class SceneWorkspaceTabContractTests
{
    [TestMethod]
    public void MainWorkspaceSupportsMultipleClosableSceneTabsWithDirtyPrompt()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(xaml, "x:Name=\"SceneTabs\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnSceneTabSelectionChanged\"");
        StringAssert.Contains(xaml, "Click=\"OnCloseSceneTabClick\"");
        StringAssert.Contains(xaml, "ToolTip=\"Fermer la scene\"");

        StringAssert.Contains(code, "ObservableCollection<SceneWorkspaceTab>");
        StringAssert.Contains(code, "OpenSceneTabAsync");
        StringAssert.Contains(code, "ActivateSceneTabAsync");
        StringAssert.Contains(code, "CloseSceneTabAsync");
        StringAssert.Contains(code, "ConfirmSaveDirtyTabAsync");
        StringAssert.Contains(code, "MessageBoxButton.YesNoCancel");
        StringAssert.Contains(code, "SaveSceneTabAsync");
        StringAssert.Contains(code, "Closing += OnMainWindowClosing;");
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
