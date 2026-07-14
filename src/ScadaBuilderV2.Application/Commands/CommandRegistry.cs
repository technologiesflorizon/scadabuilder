namespace ScadaBuilderV2.Application.Commands;

/// <summary>Registers and executes editor commands through one guarded asynchronous path.</summary>
/// <remarks>
/// Decisions: DEC-0038.
/// Contracts: docs/04_editor/COMMANDS_CONTRACT_V2.md.
/// Tests: tests/ScadaBuilderV2.Tests/ApplicationCommandTests.cs.
/// </remarks>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, IApplicationCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets registered commands.</summary>
    public IReadOnlyCollection<IApplicationCommand> Commands => _commands.Values;

    /// <summary>Registers or replaces a command with the same id.</summary>
    public void Register(IApplicationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Id);
        _commands[command.Id] = command;
    }

    /// <summary>Finds a command by id.</summary>
    public IApplicationCommand? Find(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _commands.TryGetValue(id, out var command) ? command : null;
    }

    /// <summary>Executes a command with state, authorization, cancellation and reentrancy checks.</summary>
    public async Task<CommandResult> ExecuteAsync(
        string id,
        ApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(context);

        if (!_commands.TryGetValue(id, out var command))
        {
            return CommandResult.Failed($"Command '{id}' is not registered.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return CommandResult.Cancelled();
        }

        try
        {
            if (!command.CanExecute(context))
            {
                return CommandResult.Blocked($"Command '{id}' is not available in the current context.");
            }

            if (!context.AuthorizationPolicy.IsAuthorized(command, context))
            {
                return CommandResult.Blocked($"Command '{id}' is not authorized.");
            }
        }
        catch (Exception exception)
        {
            return CommandResult.Failed($"Command '{id}' could not be evaluated: {exception.Message}", exception);
        }

        if (!context.ExecutionGate.TryEnter(out var lease))
        {
            return CommandResult.Blocked("Another application command is already running.");
        }

        using (lease)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await command.ExecuteAsync(context, cancellationToken).ConfigureAwait(false)
                    ?? CommandResult.Failed($"Command '{id}' returned no result.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return CommandResult.Cancelled();
            }
            catch (Exception exception)
            {
                return CommandResult.Failed($"Command '{id}' failed: {exception.Message}", exception);
            }
        }
    }
}
