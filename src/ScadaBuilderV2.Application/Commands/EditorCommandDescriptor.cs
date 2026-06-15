namespace ScadaBuilderV2.Application.Commands;

public enum CommandSurface
{
    Ribbon,
    TopMenu,
    ContextMenu,
    ElementList,
    PropertyPanel,
    FloatingEditor,
    Keyboard
}

public sealed record EditorCommandDescriptor(
    string Id,
    string Label,
    string Category,
    bool IsEnabled = true,
    string? DisabledReason = null,
    string? IconKey = null,
    IReadOnlyList<EditorCommandDescriptor>? Children = null);
