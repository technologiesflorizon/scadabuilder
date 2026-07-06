using ScadaBuilderV2.Application.Commands;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ShortcutRegistryTests
{
    [TestMethod]
    public void ResolveReturnsExpectedCommandIdForEachDefaultBinding()
    {
        var registry = new ShortcutRegistry();

        Assert.AreEqual("selection.select-all", registry.Resolve(ShortcutKey.A, ShortcutModifiers.Control));
        Assert.AreEqual("clipboard.copy", registry.Resolve(ShortcutKey.C, ShortcutModifiers.Control));
        Assert.AreEqual("clipboard.paste", registry.Resolve(ShortcutKey.V, ShortcutModifiers.Control));
        Assert.AreEqual("clipboard.cut", registry.Resolve(ShortcutKey.X, ShortcutModifiers.Control));
        Assert.AreEqual("history.undo", registry.Resolve(ShortcutKey.Z, ShortcutModifiers.Control));
        Assert.AreEqual("history.redo", registry.Resolve(ShortcutKey.Y, ShortcutModifiers.Control));
    }

    [TestMethod]
    public void ResolveReturnsNullForUnknownCombination()
    {
        var registry = new ShortcutRegistry();

        Assert.IsNull(registry.Resolve(ShortcutKey.A, ShortcutModifiers.Control | ShortcutModifiers.Shift));
        Assert.IsNull(registry.Resolve(ShortcutKey.Z, ShortcutModifiers.None));
    }
}
