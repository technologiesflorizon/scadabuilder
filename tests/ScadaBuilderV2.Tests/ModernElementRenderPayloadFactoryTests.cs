using System.Text.Json;
using ScadaBuilderV2.App.EditorBridge;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ModernElementRenderPayloadFactoryTests
{
    [TestMethod]
    public void Create_ProjectsLockStateRecursivelyIntoSerializedEditorPayload()
    {
        var unlocked = ScadaElement.CreateText("unlocked", "Unlocked", 10, 20);
        var lockedChild = ScadaElement.CreateText("locked-child", "Locked child", 5, 6) with { IsLocked = true };
        var group = new ScadaElement(
            "group",
            "Group",
            ScadaElementKind.Group,
            new SceneBounds(100, 200, 300, 180),
            null,
            Children: [lockedChild],
            IsLocked: true);

        var payloads = new[]
        {
            ModernElementRenderPayloadFactory.Create(unlocked, new HashSet<string>(), 0),
            ModernElementRenderPayloadFactory.Create(group, new HashSet<string> { lockedChild.Id }, 1)
        };
        var json = JsonSerializer.Serialize(payloads);
        using var document = JsonDocument.Parse(json);

        Assert.IsFalse(document.RootElement[0].GetProperty("IsLocked").GetBoolean());
        Assert.IsTrue(document.RootElement[1].GetProperty("IsLocked").GetBoolean());
        Assert.IsTrue(document.RootElement[1].GetProperty("Children")[0].GetProperty("IsLocked").GetBoolean());
        Assert.IsTrue(document.RootElement[1].GetProperty("IsGroupContextSelected").GetBoolean());
    }
}
