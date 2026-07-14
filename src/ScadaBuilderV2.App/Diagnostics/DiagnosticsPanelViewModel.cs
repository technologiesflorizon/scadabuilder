using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.Diagnostics;

/// <summary>Retains and classifies the most recent structured diagnostics batch.</summary>
public sealed class DiagnosticsPanelViewModel : INotifyPropertyChanged
{
    private string source = "Aucune validation";
    private DateTimeOffset? timestamp;

    /// <summary>Gets all errors in the latest batch.</summary>
    public ObservableCollection<DiagnosticIssueViewModel> Errors { get; } = [];

    /// <summary>Gets all warnings in the latest batch.</summary>
    public ObservableCollection<DiagnosticIssueViewModel> Warnings { get; } = [];

    /// <summary>Gets informational rows in the latest batch.</summary>
    public ObservableCollection<DiagnosticIssueViewModel> Information { get; } = [];

    /// <summary>Gets the producer of the latest batch.</summary>
    public string Source
    {
        get => source;
        private set => SetField(ref source, value);
    }

    /// <summary>Gets the timestamp of the latest batch.</summary>
    public DateTimeOffset? Timestamp
    {
        get => timestamp;
        private set => SetField(ref timestamp, value);
    }

    /// <summary>Gets the number of errors.</summary>
    public int ErrorCount => Errors.Count;

    /// <summary>Gets the number of warnings.</summary>
    public int WarningCount => Warnings.Count;

    /// <summary>Gets the number of informational rows.</summary>
    public int InformationCount => Information.Count;

    /// <summary>Gets whether at least one diagnostic is retained.</summary>
    public bool HasIssues => ErrorCount + WarningCount + InformationCount > 0;

    /// <summary>Replaces the retained batch with structured issues from one operation.</summary>
    public void Load(
        IEnumerable<ScadaBuildValidationIssue> issues,
        ScadaProject? project,
        string batchSource)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var now = DateTimeOffset.Now;
        var pages = project?.Scenes ?? [];
        Errors.Clear();
        Warnings.Clear();
        Information.Clear();

        foreach (var issue in issues)
        {
            var page = issue.PageKey is { } key
                ? pages.FirstOrDefault(candidate => candidate.PageKey == key)
                : pages.FirstOrDefault(candidate => string.Equals(candidate.EffectivePageCode, issue.PageId, StringComparison.OrdinalIgnoreCase));
            var pageLabel = page?.EffectivePageCode ?? issue.PageId ?? string.Empty;
            var row = new DiagnosticIssueViewModel(issue, pageLabel, batchSource, now);
            if (issue.Severity == ScadaBuildValidationSeverity.Error) Errors.Add(row);
            else Warnings.Add(row);
        }

        Source = batchSource;
        Timestamp = now;
        RaiseCounts();
    }

    /// <summary>Clears retained diagnostics when the project closes or changes.</summary>
    public void Clear()
    {
        Errors.Clear();
        Warnings.Clear();
        Information.Clear();
        Source = "Aucune validation";
        Timestamp = null;
        RaiseCounts();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(InformationCount));
        OnPropertyChanged(nameof(HasIssues));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
