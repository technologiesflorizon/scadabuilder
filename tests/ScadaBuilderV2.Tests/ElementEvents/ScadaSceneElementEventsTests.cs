using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaSceneElementEventsTests
{
    [TestMethod]
    public void ElementWithoutConfigsExposesDefaults()
    {
        var element = ScadaElement.CreateInputText("el-1", "El1", 0, 0);

        Assert.AreEqual(0, element.EffectiveStateConfig.States.Count);
        Assert.AreEqual(0, element.EffectiveCommandConfig.Commands.Count);
    }

    [TestMethod]
    public void WithElementStateConfigReplacesConfigOnMatchingElement()
    {
        var scene = ScadaScene.CreateEmpty("scene-1", "Main", new CanvasSize(800, 600));
        var element = ScadaElement.CreateInputText("el-1", "El1", 0, 0);
        scene = scene with { Elements = new[] { element } };

        var rule = new ScadaStateRule(
            "s1", "Alarme", true,
            ScadaExpression.FromSource("{Temp} > 80"),
            ScadaEffectBlock.Empty with { BackgroundColor = "#E53935" });
        var config = ScadaElementStateConfig.Default with { States = new[] { rule } };

        var updated = scene.WithElementStateConfig("el-1", config);
        var updatedElement = updated.FindElementRecursive("el-1");

        Assert.AreEqual(1, updatedElement!.EffectiveStateConfig.States.Count);
        Assert.AreEqual("Alarme", updatedElement.EffectiveStateConfig.States[0].Name);
    }

    [TestMethod]
    public void WithElementCommandConfigReplacesConfigOnMatchingElement()
    {
        var scene = ScadaScene.CreateEmpty("scene-1", "Main", new CanvasSize(800, 600));
        var element = ScadaElement.CreateInputText("el-1", "El1", 0, 0);
        scene = scene with { Elements = new[] { element } };

        var command = new ScadaCommandBinding("c1", "Demarrer", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag, WriteTagId: "tag-1", WriteMode: ScadaWriteMode.Toggle);
        var config = ScadaElementCommandConfig.Default with { Commands = new[] { command } };

        var updated = scene.WithElementCommandConfig("el-1", config);
        var updatedElement = updated.FindElementRecursive("el-1");

        Assert.AreEqual(1, updatedElement!.EffectiveCommandConfig.Commands.Count);
        Assert.AreEqual("Demarrer", updatedElement.EffectiveCommandConfig.Commands[0].Name);
    }

    [TestMethod]
    public void WithElementStateConfigIsNoOpWhenElementMissing()
    {
        var scene = ScadaScene.CreateEmpty("scene-1", "Main", new CanvasSize(800, 600));

        var updated = scene.WithElementStateConfig("does-not-exist", ScadaElementStateConfig.Default);

        Assert.AreEqual(scene, updated);
    }
}
