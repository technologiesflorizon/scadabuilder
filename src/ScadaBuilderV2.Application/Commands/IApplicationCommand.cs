namespace ScadaBuilderV2.Application.Commands;

public interface IApplicationCommand
{
    string Id { get; }

    string DisplayName { get; }

    bool CanExecute(ApplicationContext context);

    CommandResult Execute(ApplicationContext context);
}

public sealed record CommandResult(bool Changed, string Message)
{
    public static CommandResult NoChange(string message) => new(false, message);

    public static CommandResult Success(string message) => new(true, message);
}
