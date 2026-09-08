using System.Windows;
using System.Windows.Threading;

namespace ScadaBuilderV2.App;

public partial class App : System.Windows.Application
{
    /// <summary>Installs the last-resort error boundary before any window is shown.</summary>
    /// <remarks>
    /// The editor is built on `async void` event handlers, where nothing observes the returned task: without
    /// this boundary an ordinary failure - a full disk, a revoked permission, a project file changed under
    /// the editor - reaches the dispatcher and closes the application with no message and no chance to save.
    /// Reporting and continuing is the correct trade here: the alternative is not a safer shutdown, it is a
    /// silent one.
    ///
    /// Decisions: DEC-0049.
    /// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md D8.
    /// Tests: tests/ScadaBuilderV2.Tests/ProjectLifecycleShellContractTests.cs.
    /// </remarks>
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        MessageBox.Show(
            MainWindow,
            $"Une erreur inattendue est survenue et l'opération a été interrompue.{Environment.NewLine}{Environment.NewLine}" +
            $"{e.Exception.GetType().Name}: {e.Exception.Message}{Environment.NewLine}{Environment.NewLine}" +
            "L'application reste ouverte. Enregistrez votre travail avant de poursuivre.",
            "SCADA Builder V2",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
