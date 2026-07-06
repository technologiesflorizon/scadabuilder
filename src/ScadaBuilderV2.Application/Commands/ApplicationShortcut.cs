namespace ScadaBuilderV2.Application.Commands;

public enum ShortcutKey
{
    A,
    C,
    V,
    X,
    Y,
    Z
}

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4
}

public sealed record ApplicationShortcut(string CommandId, ShortcutKey Key, ShortcutModifiers Modifiers);
