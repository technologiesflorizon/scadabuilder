namespace ScadaBuilderV2.App.Shell;

/// <summary>What the shell must render for the current busy state.</summary>
/// <param name="IsVisible">Whether the veil is up.</param>
/// <param name="Label">The gesture label, without its trailing ellipsis.</param>
/// <param name="Detail">The project name or path when the gesture knows it, otherwise <c>null</c>.</param>
public sealed record BusyOverlayState(bool IsVisible, string Label, string? Detail)
{
    /// <summary>The state of a shell with nothing running.</summary>
    public static BusyOverlayState Hidden { get; } = new(false, string.Empty, null);
}

/// <summary>Lets a collaborator take the busy veil down while it asks the operator a question.</summary>
/// <remarks>
/// A spinning ring behind a dialog reads as a frozen application: the operator is being asked something while
/// the shell claims to be working. This is the narrowest capability a dialog owner needs to step aside for the
/// length of its question, and it deliberately exposes nothing else about the shell.
///
/// Decisions: `DEC-0049` (cycle de vie autonome des projets V2) - the veil covers the gestures that decision
/// introduced.
/// Contracts: `docs/06_ui_ux/UI_ARCHITECTURE_V2.md` sections 1 and 2.
/// Tests: `tests/ScadaBuilderV2.Tests/BusyOverlayControllerTests.cs`.
/// </remarks>
public interface IBusyOverlaySuspender
{
    /// <summary>Takes the veil down until the returned scope is disposed.</summary>
    IDisposable Suspend();
}

/// <summary>Owns whether the shell's project busy veil is up, and what it says.</summary>
/// <remarks>
/// Two counters, never a flag. A gesture can begin while another is still unwinding, and a dialog suspends the
/// veil in the middle of one; a boolean would be cleared by whichever party finished first and leave the shell
/// either veiled with nothing running - dead - or bare while a project is still loading. The veil is up if and
/// only if <see cref="BusyDepth"/> is positive and <see cref="SuspendDepth"/> is zero.
///
/// The pending gestures are kept as a stack so that ending a nested gesture restores the label of the one that
/// is still running instead of blanking it.
///
/// Every mutation that the caller cannot undo is rolled back if the render callback throws. <see
/// cref="Suspend"/> is the dangerous one: its caller only learns of the suspension through the scope it
/// returns, so a throw between the increment and the return would suppress the veil for the rest of the
/// session with nothing left able to restore it.
///
/// The counters are guarded by a lock and the callback runs outside it. Today every call arrives on the UI
/// thread, but a single `ConfigureAwait(false)` anywhere in the open chain would change that silently, and the
/// failure mode is a permanently wrong veil rather than an exception. Marshalling the rendering itself back to
/// the dispatcher belongs to the shell, which owns the dispatcher; this type carries no WPF reference on
/// purpose, because the counting is the part that breaks silently and the test project cannot load a window.
///
/// Decisions: `DEC-0049` (cycle de vie autonome des projets V2).
/// Contracts: `docs/06_ui_ux/UI_ARCHITECTURE_V2.md` sections 1 and 2.
/// Tests: `tests/ScadaBuilderV2.Tests/BusyOverlayControllerTests.cs`,
/// `tests/ScadaBuilderV2.Tests/ProjectLoadingOverlayContractTests.cs`.
/// </remarks>
public sealed class BusyOverlayController : IBusyOverlaySuspender
{
    private readonly List<PendingGesture> _pending = [];
    private readonly Action<BusyOverlayState> _render;
    private readonly object _gate = new();
    private int _suspendDepth;

    /// <summary>Creates a controller that pushes every state change to <paramref name="render"/>.</summary>
    /// <param name="render">Applies one state to the shell. Called on every transition, including no-ops.</param>
    public BusyOverlayController(Action<BusyOverlayState> render)
    {
        _render = render ?? throw new ArgumentNullException(nameof(render));
    }

    /// <summary>Gets the state the shell was last told to render.</summary>
    public BusyOverlayState State { get; private set; } = BusyOverlayState.Hidden;

    /// <summary>Gets how many gestures are currently running.</summary>
    public int BusyDepth
    {
        get { lock (_gate) { return _pending.Count; } }
    }

    /// <summary>Gets how many suspension scopes are currently open.</summary>
    public int SuspendDepth
    {
        get { lock (_gate) { return _suspendDepth; } }
    }

    /// <summary>Declares that one gesture has started.</summary>
    /// <param name="label">The gesture label, shown as it is given plus an ellipsis.</param>
    /// <param name="detail">The project name or path, or <c>null</c> when the gesture does not know it yet.</param>
    public void BeginBusy(string label, string? detail = null)
    {
        var gesture = new PendingGesture(
            label ?? string.Empty,
            string.IsNullOrWhiteSpace(detail) ? null : detail);

        BusyOverlayState next;
        lock (_gate)
        {
            _pending.Add(gesture);
            next = State = Compute();
        }

        try
        {
            _render(next);
        }
        catch
        {
            // The shell was never told about this gesture, so there is nothing rendered to undo - but the
            // caller's `finally` will believe it raised the veil and lower it, popping somebody else's
            // gesture. Take it back off the stack instead.
            lock (_gate)
            {
                _pending.Remove(gesture);
                State = Compute();
            }

            throw;
        }
    }

    /// <summary>Declares that the most recently started gesture has finished, however it finished.</summary>
    public void EndBusy()
    {
        BusyOverlayState next;
        lock (_gate)
        {
            if (_pending.Count > 0)
            {
                _pending.RemoveAt(_pending.Count - 1);
            }

            next = State = Compute();
        }

        _render(next);
    }

    /// <inheritdoc />
    public IDisposable Suspend()
    {
        // Built before anything is published: the caller can only ever close this suspension through the
        // scope, so it must exist before the first thing that can throw.
        var scope = new SuspensionScope(this);

        BusyOverlayState next;
        lock (_gate)
        {
            _suspendDepth++;
            next = State = Compute();
        }

        try
        {
            _render(next);
        }
        catch
        {
            // The caller will never receive the scope, so nothing could ever close this suspension and the
            // veil would stay suppressed for the rest of the session. Roll it back. The shell was never told
            // about it, so the last state it successfully rendered is the one to return to.
            lock (_gate)
            {
                if (_suspendDepth > 0)
                {
                    _suspendDepth--;
                }

                State = Compute();
            }

            throw;
        }

        return scope;
    }

    private void Resume()
    {
        BusyOverlayState next;
        lock (_gate)
        {
            if (_suspendDepth > 0)
            {
                _suspendDepth--;
            }

            next = State = Compute();
        }

        _render(next);
    }

    private BusyOverlayState Compute()
    {
        var current = _pending.Count == 0 ? null : _pending[^1];
        return new BusyOverlayState(
            _pending.Count > 0 && _suspendDepth == 0,
            current?.Label ?? string.Empty,
            current?.Detail);
    }

    /// <remarks>A class, not a record: the rollback removes the exact instance it added, by reference.</remarks>
    private sealed class PendingGesture(string label, string? detail)
    {
        /// <summary>The gesture label.</summary>
        public string Label { get; } = label;

        /// <summary>The project name, or <c>null</c> when this gesture does not know one.</summary>
        public string? Detail { get; } = detail;
    }

    private sealed class SuspensionScope(BusyOverlayController owner) : IDisposable
    {
        private BusyOverlayController? _owner = owner;

        /// <inheritdoc />
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Resume();
    }
}
