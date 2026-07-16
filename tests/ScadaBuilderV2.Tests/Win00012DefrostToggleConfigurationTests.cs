using System.Text.Json;
using System.Text.RegularExpressions;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Infrastructure.ModernProjects;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class Win00012DefrostToggleConfigurationTests
{
    private static readonly Regex ToggleIdPattern = new(
        "^toggle_defrost_p(?<period>[1-4])_e(?<evaporator>[1-9]|1[0-4])$",
        RegexOptions.CultureInvariant);

    [TestMethod]
    public async Task ReferenceScene_ConfiguresAllDefrostTogglesFromTheirConfirmedCommandBit()
    {
        var root = FindRepositoryRoot();
        var scenePath = Path.Combine(
            root,
            "projects",
            "AMR_REF_SCADA_V2",
            "scenes",
            "win00012_modern_no_legacy.scene.json");
        var projectPath = Path.Combine(root, "projects", "AMR_REF_SCADA_V2", "project.json");

        using var document = JsonDocument.Parse(File.ReadAllText(scenePath));
        using var projectDocument = JsonDocument.Parse(File.ReadAllText(projectPath));
        var catalog = projectDocument.RootElement
            .GetProperty("TagCatalog")
            .GetProperty("Tags")
            .EnumerateArray()
            .ToDictionary(tag => tag.GetProperty("Id").GetString()!, StringComparer.Ordinal);
        var buttons = document.RootElement
            .GetProperty("Elements")
            .EnumerateArray()
            .Where(element => ToggleIdPattern.IsMatch(element.GetProperty("Id").GetString() ?? string.Empty))
            .ToArray();

        Assert.AreEqual(56, buttons.Length, "Expected four defrost periods for each of the fourteen evaporators.");

        var stateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var button in buttons)
        {
            var elementId = button.GetProperty("Id").GetString()!;
            var match = ToggleIdPattern.Match(elementId);
            var period = int.Parse(match.Groups["period"].Value);
            var evaporator = int.Parse(match.Groups["evaporator"].Value);

            var command = button.GetProperty("CommandConfig").GetProperty("Commands").EnumerateArray().Single();
            var readTagId = command.GetProperty("ReadTagId").GetString();
            Assert.AreEqual("WriteTag", command.GetProperty("Kind").GetString(), elementId);
            Assert.AreEqual("Toggle", command.GetProperty("WriteMode").GetString(), elementId);
            Assert.AreEqual(readTagId, command.GetProperty("WriteTagId").GetString(), elementId);
            Assert.IsTrue(catalog.TryGetValue(readTagId!, out var catalogTag), $"Missing catalog tag {readTagId} for {elementId}.");
            Assert.IsTrue(catalogTag.GetProperty("Enabled").GetBoolean(), elementId);
            Assert.IsTrue(catalogTag.GetProperty("Writeable").GetBoolean(), elementId);
            Assert.AreEqual("Booléen", catalogTag.GetProperty("Datatype").GetString(), elementId);

            var states = button.GetProperty("StateConfig").GetProperty("States").EnumerateArray().ToArray();
            Assert.AreEqual(2, states.Length, elementId);

            AssertState(
                states[0],
                $"state-defrost-e{evaporator:D2}-p{period}-active",
                "Actif",
                true,
                "#12B729",
                "ACTIF",
                readTagId!,
                stateIds,
                elementId);
            AssertState(
                states[1],
                $"state-defrost-e{evaporator:D2}-p{period}-stopped",
                "Arrêté",
                false,
                "#E53935",
                "ARRÊTÉ",
                readTagId!,
                stateIds,
                elementId);
        }

        Assert.AreEqual(112, stateIds.Count, "Every button state must have a deterministic unique id.");

        var loadedScene = await new ModernProjectStore().LoadOrCreateSceneAsync(
            Directory.GetParent(root)!.FullName,
            "win00012_modern_no_legacy",
            "Degivrage",
            CanvasSize.DefaultDesktop);
        var loadedButtons = loadedScene.Elements
            .Where(element => ToggleIdPattern.IsMatch(element.Id))
            .ToArray();
        Assert.AreEqual(56, loadedButtons.Length, "The durable scene must deserialize through the production store.");
        Assert.IsTrue(loadedButtons.All(element => element.EffectiveStateConfig.States.Count == 2));
    }

    private static void AssertState(
        JsonElement state,
        string expectedId,
        string expectedName,
        bool expectedValue,
        string expectedColor,
        string expectedText,
        string expectedTagId,
        ISet<string> stateIds,
        string elementId)
    {
        var stateId = state.GetProperty("Id").GetString()!;
        Assert.AreEqual(expectedId, stateId, elementId);
        Assert.IsTrue(stateIds.Add(stateId), $"Duplicate state id: {stateId}");
        Assert.AreEqual(expectedName, state.GetProperty("Name").GetString(), elementId);
        Assert.IsTrue(state.GetProperty("Enabled").GetBoolean(), elementId);

        var expression = state.GetProperty("Expression");
        var ast = expression.GetProperty("ast");
        Assert.AreEqual("Equal", ast.GetProperty("Op").GetString(), elementId);
        Assert.AreEqual(expectedTagId, ast.GetProperty("Left").GetProperty("TagId").GetString(), elementId);
        Assert.AreEqual(expectedValue, ast.GetProperty("Right").GetProperty("Value").GetBoolean(), elementId);
        Assert.AreEqual(expectedTagId, expression.GetProperty("referencedTags").EnumerateArray().Single().GetString(), elementId);

        var effect = state.GetProperty("Effect");
        Assert.AreEqual(expectedColor, effect.GetProperty("ColorFilterColor").GetString(), elementId);
        Assert.AreEqual(0.70, effect.GetProperty("ColorFilterOpacity").GetDouble(), 0.0001, elementId);
        Assert.AreEqual(expectedText, effect.GetProperty("TextContent").GetString(), elementId);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ScadaBuilderV2.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        Assert.Fail("Could not locate the SCADA Builder V2 repository root.");
        return string.Empty;
    }
}
