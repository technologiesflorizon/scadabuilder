using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Application.Clipboard;

public sealed class SceneClipboard
{
    public IReadOnlyList<ScadaElement>? Content { get; private set; }

    public bool HasContent => Content is { Count: > 0 };

    public void Copy(IReadOnlyList<ScadaElement> elements)
    {
        Content = elements;
    }
}
