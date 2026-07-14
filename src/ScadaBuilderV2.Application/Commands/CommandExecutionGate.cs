namespace ScadaBuilderV2.Application.Commands;

/// <summary>Non-reentrant gate protecting one application workspace from overlapping commands.</summary>
public sealed class CommandExecutionGate
{
    private int _isBusy;

    /// <summary>Gets whether one command currently owns the gate.</summary>
    public bool IsBusy => Volatile.Read(ref _isBusy) != 0;

    /// <summary>Attempts to enter the gate and returns a lease that must be disposed.</summary>
    public bool TryEnter(out IDisposable? lease)
    {
        if (Interlocked.CompareExchange(ref _isBusy, 1, 0) != 0)
        {
            lease = null;
            return false;
        }

        lease = new Lease(this);
        return true;
    }

    private void Exit()
    {
        Volatile.Write(ref _isBusy, 0);
    }

    private sealed class Lease(CommandExecutionGate owner) : IDisposable
    {
        private CommandExecutionGate? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Exit();
        }
    }
}
