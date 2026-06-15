namespace ScadaBuilderV2.Application.Commands;

public sealed class ToggleSelectionLockCommand : IApplicationCommand
{
    public string Id => "selection.toggle-lock";

    public string DisplayName => "Lock selection";

    public bool CanExecute(ApplicationContext context)
    {
        return context.Selection.SelectedElementIds.Count > 0;
    }

    public CommandResult Execute(ApplicationContext context)
    {
        var next = !context.Selection.IsSelectionLocked;
        context.Selection.SetSelectionLocked(next);
        return CommandResult.Success(next ? "Selection locked" : "Selection unlocked");
    }
}
