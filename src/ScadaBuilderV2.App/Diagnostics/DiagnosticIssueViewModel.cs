using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.App.Diagnostics;

/// <summary>Presentation-safe diagnostic issue with internal page routing metadata.</summary>
public sealed class DiagnosticIssueViewModel
{
    /// <summary>Creates a diagnostic row without exposing its internal page key.</summary>
    public DiagnosticIssueViewModel(
        ScadaBuildValidationIssue issue,
        string pageLabel,
        string source,
        DateTimeOffset timestamp)
    {
        Severity = issue.Severity;
        Code = issue.Code;
        Message = issue.Message;
        PageLabel = pageLabel;
        ElementOrCommand = issue.ElementId ?? issue.CommandId ?? issue.PropertyPath ?? string.Empty;
        SuggestedFix = issue.SuggestedFix ?? string.Empty;
        Source = source;
        Timestamp = timestamp;
        PageRouteKey = issue.PageKey;
        ElementId = issue.ElementId;
        PropertyPath = issue.PropertyPath;
    }

    /// <summary>Gets the issue severity.</summary>
    public ScadaBuildValidationSeverity Severity { get; }

    /// <summary>Gets the stable diagnostic code.</summary>
    public string Code { get; }

    /// <summary>Gets the user-facing explanation.</summary>
    public string Message { get; }

    /// <summary>Gets the human page code or title.</summary>
    public string PageLabel { get; }

    /// <summary>Gets the affected element, command or property label.</summary>
    public string ElementOrCommand { get; }

    /// <summary>Gets the suggested correction.</summary>
    public string SuggestedFix { get; }

    /// <summary>Gets the producer of the diagnostic batch.</summary>
    public string Source { get; }

    /// <summary>Gets when the diagnostic batch was produced.</summary>
    public DateTimeOffset Timestamp { get; }

    internal Guid? PageRouteKey { get; }

    internal string? ElementId { get; }

    internal string? PropertyPath { get; }
}
