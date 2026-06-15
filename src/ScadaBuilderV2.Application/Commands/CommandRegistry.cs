namespace ScadaBuilderV2.Application.Commands;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, IApplicationCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IApplicationCommand> Commands => _commands.Values;

    public void Register(IApplicationCommand command)
    {
        _commands[command.Id] = command;
    }

    public IApplicationCommand? Find(string id)
    {
        return _commands.TryGetValue(id, out var command) ? command : null;
    }
}
