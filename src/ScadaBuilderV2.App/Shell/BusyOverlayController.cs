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
/// This type carries no WPF reference on purpose: the counting is the part that can break silently, and the
/// test project cannot load a window. Rendering is the callback's business.
///
/// Tests: `tests/ScadaBuilderV2.Tests/BusyOverlayControllerTests.cs`,
/// `tests/ScadaBuilderV2.Tests/ProjectLoadingOverlayContractTests.cs`.
/// </remarks>
public sealed class BusyOverlayController : IBusyOverlaySuspender
{
    private readonly List<PendingGesture> _pending = [];
    private readonly Action<BusyOverlayState> _render;
    private int _suspendDepth;

    /// <summary>Creates a controller that pushes every state change to <paramref name="render"/>.</summary>
    /// <param name="render">Applies one state to the shell. Called on every transition, including no-ops.</param>
    public BusyOverlayController(Action<BusyOverlayState> render)
    {
        _render = render ?? throw new ArgumentNullException(nameof(render));
    }

    /// <summary>Gets the state last pushed to the renderer.</summary>
    public BusyOverlayState State { get; private set; } = BusyOverlayState.Hidden;

    /// <summary>Gets how many gestures are currently running.</summary>
    public int BusyDepth => _pending.Count;

    /// <summary>Gets how many suspension scopes are currently open.</summary>
    public int SuspendDepth => _suspendDepth;

    /// <summary>Declares that one gesture has started.</summary>
    /// <param name="label">The gesture label, shown as it is given plus an ellipsis.</param>
    /// <param name="detail">The project name or path, or <c>null</c> when the gesture does not know it yet.</param>
    public void BeginBusy(string label, string? detail = null)
    {
        _pending.Add(new PendingGesture(
            label ?? string.Empty,
            string.IsNullOrWhiteSpace(detail) ? null : detail));
        Publish();
    }

    /// <summary>Declares that the most recently started gesture has finished, however it finished.</summary>
    public void EndBusy()
    {
        if (_pending.Count > 0)
        {
            _pending.RemoveAt(_pending.Count - 1);
        }

        Publish();
    }

    /// <inheritdoc />
    public IDisposable Suspend()
    {
        _suspendDepth++;
        Publish();
        return new SuspensionScope(this);
    }

    private void Resume()
    {
        if (_suspendDepth > 0)
        {
            _suspendDepth--;
        }

        Publish();
    }

    private void Publish()
    {
        var current = _pending.Count == 0 ? null : _pending[^1];
        State = new BusyOverlayState(
            _pending.Count > 0 && _suspendDepth == 0,
            current?.Label ?? string.Empty,
            current?.Detail);
        _render(State);
    }

    private sealed record PendingGesture(string Label, string? Detail);

    private sealed class SuspensionScope(BusyOverlayController owner) : IDisposable
    {
        private BusyOverlayController? _owner = owner;

        /// <inheritdoc />
        public void Dispose()
        {
            var target = _owner;
            _owner = null;
            target?.Resume();
        }
    }
}
