namespace ScadaBuilderV2.Application.Commands;

public sealed class ShortcutRegistry
{
    private readonly List<ApplicationShortcut> _shortcuts;

    public ShortcutRegistry()
    {
        _shortcuts =
        [
            new ApplicationShortcut("selection.select-all", ShortcutKey.A, ShortcutModifiers.Control),
            new ApplicationShortcut("clipboard.copy", ShortcutKey.C, ShortcutModifiers.Control),
            new ApplicationShortcut("clipboard.paste", ShortcutKey.V, ShortcutModifiers.Control),
            new ApplicationShortcut("clipboard.cut", ShortcutKey.X, ShortcutModifiers.Control),
            new ApplicationShortcut("history.undo", ShortcutKey.Z, ShortcutModifiers.Control),
            new ApplicationShortcut("history.redo", ShortcutKey.Y, ShortcutModifiers.Control)
        ];
    }

    public string? Resolve(ShortcutKey key, ShortcutModifiers modifiers)
    {
        return _shortcuts.FirstOrDefault(shortcut => shortcut.Key == key && shortcut.Modifiers == modifiers)?.CommandId;
    }
}
