using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Application.Commands;

/// <summary>Canonical asynchronous editor command contract.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/04_editor/COMMANDS_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/ApplicationCommandTests.cs.
/// </remarks>
public interface IApplicationCommand
{
    /// <summary>Gets the stable command id used by every UI surface.</summary>
    string Id { get; }

    /// <summary>Gets the user-facing command name.</summary>
    string DisplayName { get; }

    /// <summary>Returns whether the current application state supports execution.</summary>
    bool CanExecute(ApplicationContext context);

    /// <summary>Executes the command without blocking the UI thread.</summary>
    Task<CommandResult> ExecuteAsync(ApplicationContext context, CancellationToken cancellationToken = default);
}

/// <summary>Outcome category for an application command.</summary>
public enum CommandResultStatus
{
    /// <summary>The command completed normally, with or without a model change.</summary>
    Succeeded,

    /// <summary>The user or caller cancelled the command.</summary>
    Cancelled,

    /// <summary>Current state, authorization or the execution gate prevented execution.</summary>
    Blocked,

    /// <summary>An unexpected failure was converted into a structured result.</summary>
    Failed
}

/// <summary>Structured application command result consumed by editor surfaces.</summary>
/// <remarks>
/// Page keys remain internal routing data and must never be rendered directly to the user.
/// </remarks>
public sealed record CommandResult
{
    private CommandResult(
        CommandResultStatus status,
        bool changed,
        string message,
        IEnumerable<Guid>? affectedPageKeys = null,
        Guid? pageToSelectKey = null,
        Guid? pageToOpenKey = null,
        bool workspaceDirty = false,
        IReadOnlyList<ScadaBuildValidationIssue>? diagnostics = null,
        Exception? exception = null)
    {
        Status = status;
        Changed = changed;
        Message = message;
        AffectedPageKeys = (affectedPageKeys ?? Array.Empty<Guid>())
            .Where(key => key != Guid.Empty)
            .Distinct()
            .ToArray();
        PageToSelectKey = pageToSelectKey is { } selectKey && selectKey != Guid.Empty ? selectKey : null;
        PageToOpenKey = pageToOpenKey is { } openKey && openKey != Guid.Empty ? openKey : null;
        WorkspaceDirty = workspaceDirty;
        Diagnostics = diagnostics ?? Array.Empty<ScadaBuildValidationIssue>();
        Exception = exception;
    }

    /// <summary>Gets the outcome category.</summary>
    public CommandResultStatus Status { get; }

    /// <summary>Gets whether the command changed editor or project state.</summary>
    public bool Changed { get; }

    /// <summary>Gets the user-facing outcome message.</summary>
    public string Message { get; }

    /// <summary>Gets stable keys of pages affected by the command.</summary>
    public IReadOnlyList<Guid> AffectedPageKeys { get; }

    /// <summary>Gets the page the project tree should select.</summary>
    public Guid? PageToSelectKey { get; }

    /// <summary>Gets the page the editor should open or activate.</summary>
    public Guid? PageToOpenKey { get; }

    /// <summary>Gets whether the resulting workspace must be persisted.</summary>
    public bool WorkspaceDirty { get; }

    /// <summary>Gets structured diagnostics produced by the command.</summary>
    public IReadOnlyList<ScadaBuildValidationIssue> Diagnostics { get; }

    /// <summary>Gets the captured exception for logging; UI surfaces should display <see cref="Message"/>.</summary>
    public Exception? Exception { get; }

    /// <summary>Creates a successful result that changed state.</summary>
    public static CommandResult Success(
        string message,
        IEnumerable<Guid>? affectedPageKeys = null,
        Guid? pageToSelectKey = null,
        Guid? pageToOpenKey = null,
        bool workspaceDirty = false,
        IReadOnlyList<ScadaBuildValidationIssue>? diagnostics = null) =>
        new(
            CommandResultStatus.Succeeded,
            changed: true,
            message,
            affectedPageKeys,
            pageToSelectKey,
            pageToOpenKey,
            workspaceDirty,
            diagnostics);

    /// <summary>Creates a successful result that made no state change.</summary>
    public static CommandResult NoChange(
        string message,
        IReadOnlyList<ScadaBuildValidationIssue>? diagnostics = null) =>
        new(CommandResultStatus.Succeeded, changed: false, message, diagnostics: diagnostics);

    /// <summary>Creates a normal cancellation result.</summary>
    public static CommandResult Cancelled(string message = "Command cancelled.") =>
        new(CommandResultStatus.Cancelled, changed: false, message);

    /// <summary>Creates a result for a command that was not allowed to execute.</summary>
    public static CommandResult Blocked(string message) =>
        new(CommandResultStatus.Blocked, changed: false, message);

    /// <summary>Creates a controlled failure result.</summary>
    public static CommandResult Failed(string message, Exception? exception = null) =>
        new(CommandResultStatus.Failed, changed: false, message, exception: exception);
}
