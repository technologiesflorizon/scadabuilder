using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Domain.Editor;

public enum EditorObjectKind
{
    SceneBackground,
    LegacyDetected,
    ElementPlus
}

public enum ElementPlusObjectType
{
    Text,
    TextInput,
    Numeric,
    Shape,
    Group
}

public abstract class EditorObject
{
    protected EditorObject(string runtimeId, string displayName, EditorObjectKind kind)
    {
        RuntimeId = string.IsNullOrWhiteSpace(runtimeId) ? throw new ArgumentException("Runtime id is required.", nameof(runtimeId)) : runtimeId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? runtimeId : displayName;
        Kind = kind;
    }

    public string RuntimeId { get; }

    public string DisplayName { get; protected set; }

    public EditorObjectKind Kind { get; }
}

public sealed record LegacyObjectStyle(
    string FontFamily,
    double FontSize,
    string Foreground,
    string Background);

public sealed class LegacyDetectedObject : EditorObject
{
    public LegacyDetectedObject(
        string runtimeId,
        string displayName,
        string legacyType,
        string text,
        bool isTextLike,
        SceneBounds bounds,
        LegacyObjectStyle style)
        : base(runtimeId, displayName, EditorObjectKind.LegacyDetected)
    {
        LegacyType = string.IsNullOrWhiteSpace(legacyType) ? "Legacy" : legacyType;
        Text = text;
        IsTextLike = isTextLike;
        Bounds = bounds;
        Style = style;
    }

    public string LegacyType { get; }

    public string Text { get; }

    public bool IsTextLike { get; }

    public SceneBounds Bounds { get; }

    public LegacyObjectStyle Style { get; }
}

public sealed class ElementPlusRuntimeObject : EditorObject
{
    public ElementPlusRuntimeObject(ScadaElement element, ElementPlusObjectType elementType)
        : base(element.Id, element.DisplayName, EditorObjectKind.ElementPlus)
    {
        Element = element;
        ElementType = elementType;
    }

    public ScadaElement Element { get; }

    public ElementPlusObjectType ElementType { get; }
}
