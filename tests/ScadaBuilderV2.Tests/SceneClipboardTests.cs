using ScadaBuilderV2.Application.Clipboard;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class SceneClipboardTests
{
    [TestMethod]
    public void IsEmptyUntilFirstCopy()
    {
        var clipboard = new SceneClipboard();

        Assert.IsFalse(clipboard.HasContent);
        Assert.IsNull(clipboard.Content);
    }

    [TestMethod]
    public void CopyStoresElementsAndOverwritesPreviousContent()
    {
        var clipboard = new SceneClipboard();
        var first = ScadaElement.CreateText("text-1", "First", 0, 0);
        var second = ScadaElement.CreateText("text-2", "Second", 10, 10);

        clipboard.Copy([first]);
        Assert.IsTrue(clipboard.HasContent);
        Assert.AreEqual(1, clipboard.Content!.Count);
        Assert.AreEqual("text-1", clipboard.Content[0].Id);

        clipboard.Copy([second]);
        Assert.AreEqual(1, clipboard.Content!.Count);
        Assert.AreEqual("text-2", clipboard.Content[0].Id);
    }
}
