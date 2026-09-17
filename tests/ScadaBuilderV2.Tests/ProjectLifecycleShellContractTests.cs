using System.IO;
using ScadaBuilderV2.Application.Projects;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Locks the WPF surface of the project lifecycle: the error boundary that keeps a failure from closing the
/// application, the all-or-nothing activation, the welcome screen's own contract and the three explicit verbs
/// D9 requires. These are source contracts because the surfaces they guard are XAML and `async void` handlers,
/// which no unit test can exercise; each one encodes a defect that was found in the audit of 2026-09-08.
///
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md D8 to D12.
/// </summary>
[TestClass]
public sealed class ProjectLifecycleShellContractTests
{
    /// <summary>Nothing observes an `async void` handler, so the dispatcher needs a boundary of its own.</summary>
    /// <remarks>
    /// Without it an ordinary failure of this flow - a full disk, a revoked permission, a recent entry whose
    /// project moved - reached the dispatcher and closed the editor with no message and no chance to save.
    /// </remarks>
    [TestMethod]
    public void TheApplicationInstallsALastResortErrorBoundary()
    {
        var app = ReadAppFile("App.xaml.cs");

        StringAssert.Contains(app, "DispatcherUnhandledException +=");
        StringAssert.Contains(app, "e.Handled = true;");
    }

    [TestMethod]
    public void EveryLifecycleGestureRunsBehindTheErrorBoundary()
    {
        var shell = ReadAppFile("MainWindow.xaml.cs");

        foreach (var handler in new[]
                 {
                     "OnWelcomeNewProjectClick",
                     "OnWelcomeOpenProjectClick",
                     "OnWelcomeReopenLastClick",
                     "OnRecentProjectOpenClick",
                     "OnRecentProjectRemoveClick",
                     "OnDiscoveredProjectOpenClick"
                 })
        {
            var body = HandlerBody(shell, handler);
            StringAssert.Contains(
                body,
                "RunProjectGestureAsync",
                $"{handler} must not reach the dispatcher with an unobserved exception.");
        }
    }

    /// <summary>A failed activation lands in a defined state, never a half-built session.</summary>
    [TestMethod]
    public void AFailedActivationFallsBackToTheWelcomeStateAndNamesTheProject()
    {
        var shell = ReadAppFile("MainWindow.xaml.cs");
        var activation = Between(
            shell,
            "async Task IProjectLifecycleHost.ActivateProjectAsync(",
            "private async Task ActivateProjectCoreAsync(");

        StringAssert.Contains(activation, "catch (Exception exception)");
        StringAssert.Contains(activation, "await CloseActiveProjectCoreAsync();");
        StringAssert.Contains(activation, "ShowProjectWelcome();");
        StringAssert.Contains(activation, "throw new ProjectActivationException(");
    }

    /// <summary>D10 step 6 replaces the WebView by the welcome state; hiding it is not replacing it.</summary>
    [TestMethod]
    public void ClosingAProjectReleasesItsPreviewDocument()
    {
        var shell = ReadAppFile("MainWindow.xaml.cs");
        var teardown = Between(shell, "private Task CloseActiveProjectCoreAsync()", "private async Task RefreshRecentProjectsAsync()");

        StringAssert.Contains(teardown, "ReleasePreviewDocument();");
        StringAssert.Contains(shell, "PreviewWebView.CoreWebView2?.Navigate(\"about:blank\")");
    }

    /// <summary>D12 rule 2 admits a recent entry only after a successful creation or opening.</summary>
    /// <remarks>
    /// Discovery used to write straight into the recents, so `Rouvrir le dernier` could target a project the
    /// operator had never opened. Discovered projects are now their own list on the welcome screen.
    /// </remarks>
    [TestMethod]
    public void DiscoveryNeverWritesIntoTheRecentProjects()
    {
        var shell = ReadAppFile("MainWindow.xaml.cs");
        var discovery = Between(shell, "private async Task RefreshDiscoveredProjectsAsync()", "private void ShowProjectWelcome()");

        Assert.IsFalse(
            discovery.Contains("_recentProjectStore.RecordAsync", StringComparison.Ordinal),
            "a discovered project is not an opened project.");
        StringAssert.Contains(discovery, "DiscoveredProjects.Add(");
    }

    /// <summary>D9 names three answers; `Oui`/`Non` made the destructive one the shortest word on screen.</summary>
    [TestMethod]
    public void TheUnsavedChangesDialogSpellsOutAllThreeAnswers()
    {
        var dialog = ReadAppFile(Path.Combine("Projects", "UnsavedChangesDialog.xaml"));

        StringAssert.Contains(dialog, "Content=\"Enregistrer\"");
        StringAssert.Contains(dialog, "Content=\"Ne pas enregistrer\"");
        StringAssert.Contains(dialog, "Content=\"Annuler\"");

        var shell = ReadAppFile("MainWindow.xaml.cs");
        var request = Between(
            shell,
            "async Task<ProjectCloseDecision> IProjectLifecycleHost.RequestCloseDecisionAsync(",
            "async Task<bool> IProjectLifecycleHost.SaveActiveProjectAsync(");
        StringAssert.Contains(request, "new UnsavedChangesDialog(");
        Assert.IsFalse(
            request.Contains("MessageBoxButton.YesNoCancel", StringComparison.Ordinal),
            "a system MessageBox cannot say what its buttons do.");
    }

    [TestMethod]
    public void TheUnsavedChangesDialogMapsEachButtonToItsDecision()
    {
        var code = ReadAppFile(Path.Combine("Projects", "UnsavedChangesDialog.xaml.cs"));

        StringAssert.Contains(code, "OnSaveClick(object sender, RoutedEventArgs e) => Complete(ProjectCloseDecision.Save)");
        StringAssert.Contains(code, "OnDiscardClick(object sender, RoutedEventArgs e) => Complete(ProjectCloseDecision.Discard)");
        StringAssert.Contains(code, "OnCancelClick(object sender, RoutedEventArgs e) => Complete(ProjectCloseDecision.Cancel)");
        StringAssert.Contains(
            code,
            "ProjectCloseDecision Decision { get; private set; } = ProjectCloseDecision.Cancel;",
            "closing the dialog by any other route must not discard work.");
    }

    /// <summary>The welcome screen carries the whole of D11 and D12, not a subset of it.</summary>
    [TestMethod]
    public void TheWelcomeScreenShowsWhatD11AndD12Require()
    {
        var xaml = ReadAppFile("MainWindow.xaml");
        var welcome = Between(xaml, "<Border x:Name=\"WelcomePanel\"", "</avalonDock:LayoutDocument>");

        StringAssert.Contains(welcome, "x:Name=\"ReopenLastProjectText\"", "D11 names the project in the button.");
        StringAssert.Contains(welcome, "x:Name=\"NoRecentProjectsText\"", "the heading must not stand over nothing.");
        StringAssert.Contains(welcome, "LastOpenedUtc", "a recents list without dates cannot be sorted by eye.");
        StringAssert.Contains(welcome, "Dossier introuvable", "an unavailable entry must say why.");
        StringAssert.Contains(welcome, "Icon.Project.New");
        StringAssert.Contains(welcome, "Icon.Project.Open");
    }

    /// <summary>Colours reach the welcome screen through the versioned art direction, not literals.</summary>
    [TestMethod]
    public void TheWelcomeScreenCarriesNoHardCodedColour()
    {
        var xaml = ReadAppFile("MainWindow.xaml");
        var welcome = Between(xaml, "<Border x:Name=\"WelcomePanel\"", "</avalonDock:LayoutDocument>");

        foreach (var literal in new[] { "\"#F4F8F6\"", "\"White\"", "\"#FFFFFF\"" })
        {
            Assert.IsFalse(
                welcome.Contains(literal, StringComparison.OrdinalIgnoreCase),
                $"{literal} bypasses DEC-0048; use a named brush.");
        }

        StringAssert.Contains(welcome, "{StaticResource SurfaceBrush}");
        StringAssert.Contains(welcome, "{StaticResource PanelBrush}");
    }

    [TestMethod]
    public void TheCreationDialogUsesANamedBrushForItsValidationText()
    {
        var xaml = ReadAppFile(Path.Combine("Projects", "CreateProjectDialog.xaml"));

        Assert.IsFalse(xaml.Contains("#6A4300\"", StringComparison.OrdinalIgnoreCase) &&
                       !xaml.Contains("x:Key=\"WarningBrush\"", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "Foreground=\"{StaticResource WarningBrush}\"");
    }

    private static string HandlerBody(string source, string handler)
    {
        var index = source.IndexOf($"void {handler}(", StringComparison.Ordinal);
        Assert.IsTrue(index >= 0, $"{handler} was not found in the shell.");
        var length = Math.Min(700, source.Length - index);
        return source.Substring(index, length);
    }

    private static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        Assert.IsTrue(from >= 0, $"'{start}' was not found.");
        var to = source.IndexOf(end, from, StringComparison.Ordinal);
        Assert.IsTrue(to > from, $"'{end}' was not found after '{start}'.");
        return source[from..to];
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
