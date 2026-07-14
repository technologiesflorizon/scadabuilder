namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class DiagnosticsSurfaceContractTests
{
    [TestMethod]
    public void DiagnosticsPanelExposesSharedSeverityTabsAndHumanLocationColumns()
    {
        var xaml = Read("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var viewModel = Read("src", "ScadaBuilderV2.App", "Diagnostics", "DiagnosticsPanelViewModel.cs");

        StringAssert.Contains(xaml, "x:Name=\"DiagnosticsAnchorable\"");
        StringAssert.Contains(xaml, "Header=\"Erreurs\"");
        StringAssert.Contains(xaml, "Header=\"Avertissements\"");
        StringAssert.Contains(xaml, "Header=\"Informations\"");
        StringAssert.Contains(xaml, "Binding=\"{Binding PageLabel}\"");
        StringAssert.Contains(xaml, "OnDiagnosticIssueMouseDoubleClick");
        StringAssert.Contains(viewModel, "ObservableCollection<DiagnosticIssueViewModel> Errors");
        StringAssert.Contains(viewModel, "ObservableCollection<DiagnosticIssueViewModel> Warnings");
        Assert.IsFalse(xaml.Contains("PageRouteKey", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("PageKey}", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DiagnosticsHeaderUsesOneWayBindingsForReadOnlyViewModelProperties()
    {
        var xaml = Read("src", "ScadaBuilderV2.App", "MainWindow.xaml");

        StringAssert.Contains(xaml, "{Binding DiagnosticsPanel.Source, Mode=OneWay}");
        StringAssert.Contains(xaml, "{Binding DiagnosticsPanel.ErrorCount, Mode=OneWay}");
        StringAssert.Contains(xaml, "{Binding DiagnosticsPanel.WarningCount, Mode=OneWay}");
        var dialog = Read("src", "ScadaBuilderV2.App", "Diagnostics", "CommandErrorDialog.xaml");
        StringAssert.Contains(dialog, "{Binding ErrorCount, Mode=OneWay}");
        StringAssert.Contains(dialog, "{Binding WarningCount, Mode=OneWay}");
        Assert.IsFalse(xaml.Contains("{Binding DiagnosticsPanel.Source}", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("{Binding DiagnosticsPanel.ErrorCount}", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("{Binding DiagnosticsPanel.WarningCount}", StringComparison.Ordinal));
        Assert.IsFalse(dialog.Contains("{Binding ErrorCount}", StringComparison.Ordinal));
        Assert.IsFalse(dialog.Contains("{Binding WarningCount}", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ModernErrorDialogCanOpenTheSharedDiagnosticsPanel()
    {
        var dialog = Read("src", "ScadaBuilderV2.App", "Diagnostics", "CommandErrorDialog.xaml");
        var dialogCode = Read("src", "ScadaBuilderV2.App", "Diagnostics", "CommandErrorDialog.xaml.cs");
        var controller = Read("src", "ScadaBuilderV2.App", "Pages", "PageCommandController.cs");
        var main = Read("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(dialog, "Afficher les erreurs");
        StringAssert.Contains(dialog, "Causes");
        StringAssert.Contains(dialogCode, "ShowDiagnosticsRequested");
        StringAssert.Contains(controller, "new CommandErrorDialog");
        StringAssert.Contains(main, "_diagnosticsPanel.Load(result.Diagnostics");
        StringAssert.Contains(main, "ShowDiagnosticsPanel");
        Assert.IsFalse(controller.Contains("MessageBox.Show(this, result.Message", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CancellationDoesNotOpenTheErrorDialog()
    {
        var controller = Read("src", "ScadaBuilderV2.App", "Pages", "PageCommandController.cs");
        StringAssert.Contains(controller, "CommandResultStatus.Cancelled) return");
    }

    private static string Read(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(path)) return File.ReadAllText(path);
            directory = directory.Parent;
        }
        Assert.Fail($"Unable to locate {Path.Combine(parts)}");
        return string.Empty;
    }
}
