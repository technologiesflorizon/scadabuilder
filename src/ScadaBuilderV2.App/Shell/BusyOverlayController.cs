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
/// **This type is UI-thread-affine.** The lock is not a concurrency guarantee, and must not be read as one: it
/// keeps the counters and the pending stack from being corrupted by a call that arrives off the UI thread, and
/// that is all it does. The render callback deliberately runs *outside* the lock, because a renderer that goes
/// back through the dispatcher while the lock is held would deadlock - so two threads mutating concurrently can
/// still reach the shell in the opposite order to their mutations, leaving it showing a state that disagrees
/// with <see cref="State"/>. Ordering is not offered. Nothing in this shell calls these methods off the UI
/// thread today; a caller that wanted to would have to order its own updates, or this type would have to stamp
/// each state with a sequence and have the shell drop stale ones.
///
/// This type carries no WPF reference on purpose: the counting is the part that breaks silently, and the test
/// project cannot load a window. Marshalling the rendering back onto the dispatcher therefore belongs to the
/// shell, which owns the dispatcher, and buys safety - an off-thread render would otherwise throw on the first
/// element it touched - rather than ordering.
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
    private BusyOverlayState _state = BusyOverlayState.Hidden;
    private int _suspendDepth;

    /// <summary>Creates a controller that pushes every state change to <paramref name="render"/>.</summary>
    /// <param name="render">Applies one state to the shell. Called on every transition, including no-ops.</param>
    public BusyOverlayController(Action<BusyOverlayState> render)
    {
        _render = render ?? throw new ArgumentNullException(nameof(render));
    }

    /// <summary>Gets the state the shell was last told to render.</summary>
    /// <remarks>
    /// Read under the same lock as the two depths, for one story rather than three. It says what the shell was
    /// last *told*; with an off-thread caller it would not say what the shell is showing, because this type
    /// does not order the renders.
    /// </remarks>
    public BusyOverlayState State
    {
        get { lock (_gate) { return _state; } }
    }

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
            next = _state = Compute();
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
                _state = Compute();
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

            next = _state = Compute();
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
            next = _state = Compute();
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

                _state = Compute();
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

            next = _state = Compute();
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
