using System.Windows;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.Diagnostics;

/// <summary>Modern blocking error summary backed by structured diagnostics.</summary>
public partial class CommandErrorDialog : Window
{
    /// <summary>Creates the dialog from a command result.</summary>
    public CommandErrorDialog(CommandResult result)
        : this(result.Message, result.Diagnostics)
    {
    }

    /// <summary>Creates the dialog from an operation summary and issue list.</summary>
    public CommandErrorDialog(string summary, IReadOnlyList<ScadaBuildValidationIssue> issues)
    {
        Summary = string.IsNullOrWhiteSpace(summary) ? "Une erreur empêche la poursuite de l'opération." : summary;
        ErrorCount = Math.Max(1, issues.Count(issue => issue.Severity == ScadaBuildValidationSeverity.Error));
        WarningCount = issues.Count(issue => issue.Severity == ScadaBuildValidationSeverity.Warning);
        Causes = issues.Take(6).Select(issue => issue.Message).DefaultIfEmpty(Summary).ToArray();
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>Gets the operation summary.</summary>
    public string Summary { get; }

    /// <summary>Gets the displayed error count.</summary>
    public int ErrorCount { get; }

    /// <summary>Gets the displayed warning count.</summary>
    public int WarningCount { get; }

    /// <summary>Gets the first actionable causes.</summary>
    public IReadOnlyList<string> Causes { get; }

    /// <summary>Gets whether the user requested the detailed diagnostics panel.</summary>
    public bool ShowDiagnosticsRequested { get; private set; }

    private void OnShowErrorsClick(object sender, RoutedEventArgs e)
    {
        ShowDiagnosticsRequested = true;
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
