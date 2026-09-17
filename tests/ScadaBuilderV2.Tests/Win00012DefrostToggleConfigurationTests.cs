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

    private static readonly Regex UpperToggleIdPattern = new(
        "^toggle_defrost_p(?<period>[1-4])_e(?<evaporator>1[5-9]|2[0-9]|30)$",
        RegexOptions.CultureInvariant);

    /// <summary>Maps each "Heure depart" table row to the suffix of the setpoint register it writes.</summary>
    private static readonly IReadOnlyDictionary<int, string> StartHourRows = new Dictionary<int, string>
    {
        [3] = "",
        [5] = "2",
        [7] = "3",
        [9] = "4",
    };

    private static readonly Regex ManualDepartureIdPattern = new(
        "^manual_defrost_e(?<evaporator>[1-9]|1[0-4])$",
        RegexOptions.CultureInvariant);

    private static readonly Regex DefrostStatusIdPattern = new(
        "^defrost_status_e(?<evaporator>[1-9]|1[0-4])$",
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

    [TestMethod]
    public async Task ReferenceScene_ConfiguresManualDepartureTogglesAndDefrostStatusRows()
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
        var elements = document.RootElement.GetProperty("Elements").EnumerateArray().ToArray();
        var table = elements.Single(element => element.GetProperty("Id").GetString() == "table_defrost_upper");
        Assert.AreEqual(566d, table.GetProperty("Bounds").GetProperty("Height").GetDouble(), 0.0001);

        var rows = table.GetProperty("Table").GetProperty("Rows").EnumerateArray().ToArray();
        Assert.AreEqual(18, rows.Length);
        Assert.AreEqual(8d, rows[15].GetProperty("Height").GetDouble(), 0.0001, "The spacer row must remain intentionally compact.");
        Assert.AreEqual(40d, rows[16].GetProperty("Height").GetDouble(), 0.0001);
        Assert.AreEqual(36.2978723404256d, rows[17].GetProperty("Height").GetDouble(), 0.0001);

        var addedCells = table.GetProperty("Table").GetProperty("Cells").EnumerateArray()
            .Where(cell => cell.GetProperty("Row").GetInt32() >= 16)
            .ToArray();
        Assert.AreEqual(30, addedCells.Length, "Each added row must have one label cell and fourteen evaporator cells.");
        Assert.AreEqual(
            "Départ Manuel",
            addedCells.Single(cell => cell.GetProperty("Row").GetInt32() == 16 && cell.GetProperty("Column").GetInt32() == 0)
                .GetProperty("Content").GetProperty("Text").GetString());
        Assert.AreEqual(
            "État du dégivrage",
            addedCells.Single(cell => cell.GetProperty("Row").GetInt32() == 17 && cell.GetProperty("Column").GetInt32() == 0)
                .GetProperty("Content").GetProperty("Text").GetString());

        var manualButtons = elements
            .Where(element => ManualDepartureIdPattern.IsMatch(element.GetProperty("Id").GetString() ?? string.Empty))
            .OrderBy(element => int.Parse(ManualDepartureIdPattern.Match(element.GetProperty("Id").GetString()!).Groups["evaporator"].Value))
            .ToArray();
        var statusIndicators = elements
            .Where(element => DefrostStatusIdPattern.IsMatch(element.GetProperty("Id").GetString() ?? string.Empty))
            .OrderBy(element => int.Parse(DefrostStatusIdPattern.Match(element.GetProperty("Id").GetString()!).Groups["evaporator"].Value))
            .ToArray();
        Assert.AreEqual(14, manualButtons.Length);
        Assert.AreEqual(14, statusIndicators.Length);

        for (var index = 0; index < 14; index++)
        {
            var expectedX = 259d + (72.42857142857143d * index);
            var button = manualButtons[index];
            Assert.AreEqual("Button", button.GetProperty("Kind").GetString());
            Assert.AreEqual("Command", button.GetProperty("ButtonKind").GetString());
            Assert.AreEqual("DÉPART", button.GetProperty("Data").GetProperty("Text").GetString());
            Assert.AreEqual(JsonValueKind.Null, button.GetProperty("Data").GetProperty("ReadTagId").ValueKind);
            Assert.AreEqual(JsonValueKind.Null, button.GetProperty("Data").GetProperty("WriteTagId").ValueKind);
            var command = button.GetProperty("CommandConfig").GetProperty("Commands").EnumerateArray().Single();
            var expectedCommandTagId = $"tf100.mapping.{629 + index}";
            Assert.AreEqual("WriteTag", command.GetProperty("Kind").GetString(), button.GetProperty("Id").GetString());
            Assert.AreEqual("Toggle", command.GetProperty("WriteMode").GetString(), button.GetProperty("Id").GetString());
            Assert.AreEqual(expectedCommandTagId, command.GetProperty("ReadTagId").GetString(), button.GetProperty("Id").GetString());
            Assert.AreEqual(expectedCommandTagId, command.GetProperty("WriteTagId").GetString(), button.GetProperty("Id").GetString());
            Assert.IsTrue(catalog.TryGetValue(expectedCommandTagId, out var commandTag), button.GetProperty("Id").GetString());
            Assert.IsTrue(commandTag.GetProperty("Writeable").GetBoolean(), button.GetProperty("Id").GetString());
            Assert.AreEqual(expectedX, button.GetProperty("Bounds").GetProperty("X").GetDouble(), 0.0001);
            Assert.AreEqual(605d, button.GetProperty("Bounds").GetProperty("Y").GetDouble(), 0.0001);

            var indicator = statusIndicators[index];
            var indicatorId = indicator.GetProperty("Id").GetString()!;
            Assert.AreEqual("Shape", indicator.GetProperty("Kind").GetString());
            Assert.AreEqual("Rectangle", indicator.GetProperty("ShapeKind").GetString());
            var indicatorData = indicator.GetProperty("Data");
            Assert.IsTrue(
                indicatorData.ValueKind is JsonValueKind.Null or JsonValueKind.Object,
                $"{indicatorId}: optional default Data may be omitted or normalized as an object.");
            if (indicatorData.ValueKind == JsonValueKind.Object)
            {
                Assert.AreEqual(JsonValueKind.Null, indicatorData.GetProperty("ReadTagId").ValueKind, indicatorId);
                Assert.AreEqual(JsonValueKind.Null, indicatorData.GetProperty("WriteTagId").ValueKind, indicatorId);
            }
            Assert.AreEqual(JsonValueKind.Null, indicator.GetProperty("CommandConfig").ValueKind);
            var expectedStatusTagId = $"tf100.mapping.{615 + index}";
            Assert.IsTrue(catalog.TryGetValue(expectedStatusTagId, out var statusTag), indicatorId);
            Assert.IsFalse(statusTag.GetProperty("Writeable").GetBoolean(), indicatorId);
            var statusStates = indicator.GetProperty("StateConfig").GetProperty("States").EnumerateArray().ToArray();
            Assert.AreEqual(2, statusStates.Length, indicatorId);
            AssertStatusState(statusStates[0], expectedStatusTagId, true, "#12B729", indicatorId);
            AssertStatusState(statusStates[1], expectedStatusTagId, false, "#E53935", indicatorId);
            Assert.AreEqual(expectedX, indicator.GetProperty("Bounds").GetProperty("X").GetDouble(), 0.0001);
            Assert.AreEqual(647d, indicator.GetProperty("Bounds").GetProperty("Y").GetDouble(), 0.0001);
            Assert.AreEqual(20d, indicator.GetProperty("Bounds").GetProperty("Height").GetDouble(), 0.0001);
        }

        var loadedScene = await new ModernProjectStore().LoadOrCreateSceneAsync(
            Directory.GetParent(root)!.FullName,
            "win00012_modern_no_legacy",
            "Degivrage",
            CanvasSize.DefaultDesktop);
        Assert.AreEqual(14, loadedScene.Elements.Count(element => ManualDepartureIdPattern.IsMatch(element.Id)));
        Assert.AreEqual(14, loadedScene.Elements.Count(element => DefrostStatusIdPattern.IsMatch(element.Id)));
        var loadedTable = loadedScene.Elements.Single(element => element.Id == "table_defrost_upper").Table;
        Assert.IsNotNull(loadedTable);
        Assert.AreEqual(18, loadedTable!.EffectiveRows.Count);
    }

    /// <summary>
    /// Pins the E-15..E-30 defrost page against the same contract as its E-1..E-14 sibling.
    /// </summary>
    /// <remarks>
    /// The page ships the four period toggles for sixteen more evaporators. Its manual-departure buttons and
    /// status indicators stay unconfigured on purpose: the imported catalogue carries no `BP_E{n}_Deg` and no
    /// `Evap{n}_defrostActif` beyond E-14, so there is no bit to write or read. This test deliberately asserts
    /// nothing about those elements rather than freezing the gap as if it were a decision.
    ///
    /// Each toggle is checked against the tag that belongs to *its own* evaporator and period. Asserting only
    /// that some catalogue tag exists would pass with all sixty-four buttons wired to one bit.
    /// </remarks>
    [TestMethod]
    public async Task UpperEvaporatorScene_ConfiguresEveryPeriodToggleFromItsOwnCommandBit()
    {
        var root = FindRepositoryRoot();
        var scenePath = Path.Combine(
            root,
            "projects",
            "AMR_REF_SCADA_V2",
            "scenes",
            "win00012_modern_no_legacy_e15_e30.scene.json");
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
            .Where(element => UpperToggleIdPattern.IsMatch(element.GetProperty("Id").GetString() ?? string.Empty))
            .ToArray();

        Assert.AreEqual(64, buttons.Length, "Expected four defrost periods for each of the sixteen upper evaporators.");

        var stateIds = new HashSet<string>(StringComparer.Ordinal);
        var commandTagIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var button in buttons)
        {
            var elementId = button.GetProperty("Id").GetString()!;
            var match = UpperToggleIdPattern.Match(elementId);
            var period = int.Parse(match.Groups["period"].Value);
            var evaporator = int.Parse(match.Groups["evaporator"].Value);

            var command = button.GetProperty("CommandConfig").GetProperty("Commands").EnumerateArray().Single();
            var readTagId = command.GetProperty("ReadTagId").GetString();
            Assert.AreEqual("WriteTag", command.GetProperty("Kind").GetString(), elementId);
            Assert.AreEqual("Toggle", command.GetProperty("WriteMode").GetString(), elementId);
            Assert.AreEqual("OnClick", command.GetProperty("Trigger").GetString(), elementId);
            Assert.AreEqual(readTagId, command.GetProperty("WriteTagId").GetString(), elementId);
            Assert.IsTrue(catalog.TryGetValue(readTagId!, out var catalogTag), $"Missing catalog tag {readTagId} for {elementId}.");
            Assert.AreEqual(
                $"YL{evaporator}_E{period}_HDEG",
                catalogTag.GetProperty("DisplayName").GetString(),
                $"{elementId} must carry the command bit of its own evaporator and period.");
            Assert.IsTrue(catalogTag.GetProperty("Enabled").GetBoolean(), elementId);
            Assert.IsTrue(catalogTag.GetProperty("Writeable").GetBoolean(), elementId);
            Assert.AreEqual("Booléen", catalogTag.GetProperty("Datatype").GetString(), elementId);
            Assert.IsTrue(commandTagIds.Add(readTagId!), $"Duplicate command bit {readTagId} on {elementId}.");

            Assert.AreEqual(
                "ON/OFF",
                button.GetProperty("Data").GetProperty("Text").GetString(),
                $"{elementId} must carry the idle label; the states only replace it once the bit is readable.");

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

        Assert.AreEqual(128, stateIds.Count, "Every button state must have a deterministic unique id.");
        Assert.AreEqual(64, commandTagIds.Count, "Each toggle drives a distinct command bit.");

        var loadedScene = await new ModernProjectStore().LoadOrCreateSceneAsync(
            Directory.GetParent(root)!.FullName,
            "win00012_modern_no_legacy_e15_e30",
            "Degivrage",
            CanvasSize.DefaultDesktop);
        var loadedButtons = loadedScene.Elements
            .Where(element => UpperToggleIdPattern.IsMatch(element.Id))
            .ToArray();
        Assert.AreEqual(64, loadedButtons.Length, "The durable scene must deserialize through the production store.");
        Assert.IsTrue(loadedButtons.All(element => element.EffectiveStateConfig.States.Count == 2));
    }

    /// <summary>
    /// Pins the defrost start-hour cells of the E-15..E-30 table to their own setpoint registers.
    /// </summary>
    /// <remarks>
    /// The four "Heure depart" rows hold one HHMM register per evaporator and period, written from the same
    /// cell the operator reads. The table lays evaporator k out at column 1 + 2k, so a binding that drifts by
    /// one column would set the neighbour's schedule - the assertion below resolves each cell's tag by name
    /// rather than only checking that some catalogue entry exists.
    ///
    /// One cell carries no binding: `KIC_E22_HDEG4` is absent from the imported catalogue, which stops at
    /// `KIC_E22_HDEG3` while every other evaporator has all four. The test states that gap as a fact about the
    /// catalogue rather than as an intention, so importing the missing register makes it fail and ask for the
    /// binding.
    /// </remarks>
    [TestMethod]
    public async Task UpperEvaporatorTable_WritesEachDefrostStartHourToItsOwnSetpointRegister()
    {
        var root = FindRepositoryRoot();
        var scenePath = Path.Combine(
            root,
            "projects",
            "AMR_REF_SCADA_V2",
            "scenes",
            "win00012_modern_no_legacy_e15_e30.scene.json");
        var projectPath = Path.Combine(root, "projects", "AMR_REF_SCADA_V2", "project.json");

        using var document = JsonDocument.Parse(File.ReadAllText(scenePath));
        using var projectDocument = JsonDocument.Parse(File.ReadAllText(projectPath));
        var tags = projectDocument.RootElement
            .GetProperty("TagCatalog")
            .GetProperty("Tags")
            .EnumerateArray()
            .ToArray();
        var byId = tags.ToDictionary(tag => tag.GetProperty("Id").GetString()!, StringComparer.Ordinal);
        var byName = tags.ToDictionary(tag => tag.GetProperty("DisplayName").GetString()!, StringComparer.Ordinal);

        var cells = document.RootElement
            .GetProperty("Elements")
            .EnumerateArray()
            .Single(element => element.GetProperty("Id").GetString() == "table_defrost_upper")
            .GetProperty("Table")
            .GetProperty("Cells")
            .EnumerateArray()
            .Where(cell => StartHourRows.ContainsKey(cell.GetProperty("Row").GetInt32()))
            .Where(cell => cell.GetProperty("Content").GetProperty("Kind").GetString() == "InputNumeric")
            .ToArray();

        Assert.AreEqual(64, cells.Length, "Four start hours for each of the sixteen upper evaporators.");

        var boundTagIds = new HashSet<string>(StringComparer.Ordinal);
        var uncovered = new List<string>();
        foreach (var cell in cells)
        {
            var row = cell.GetProperty("Row").GetInt32();
            var column = cell.GetProperty("Column").GetInt32();
            var evaporator = 15 + ((column - 1) / 2);
            var expectedTagName = $"KIC_E{evaporator}_HDEG{StartHourRows[row]}";
            var where = $"row {row}, column {column} (E-{evaporator})";
            var bindings = cell.GetProperty("ValueBindings");

            if (!byName.TryGetValue(expectedTagName, out var catalogTag))
            {
                Assert.AreEqual(
                    JsonValueKind.Null,
                    bindings.ValueKind,
                    $"{where}: {expectedTagName} is not in the catalogue, so the cell must stay unbound rather "
                    + "than write a neighbouring register.");
                uncovered.Add(expectedTagName);
                continue;
            }

            Assert.AreEqual(JsonValueKind.Object, bindings.ValueKind, $"{where}: expected a binding to {expectedTagName}.");
            var readTagId = bindings.GetProperty("ReadTagId").GetString();
            Assert.AreEqual(readTagId, bindings.GetProperty("WriteTagId").GetString(), $"{where}: the operator must write back the register they read.");
            Assert.AreEqual(
                expectedTagName,
                byId[readTagId!].GetProperty("DisplayName").GetString(),
                $"{where}: bound to the wrong evaporator or period.");
            Assert.AreEqual("UInt16", catalogTag.GetProperty("Datatype").GetString(), where);
            Assert.IsTrue(catalogTag.GetProperty("Writeable").GetBoolean(), where);
            Assert.IsTrue(catalogTag.GetProperty("Enabled").GetBoolean(), where);
            Assert.IsFalse(cell.GetProperty("Content").GetProperty("IsReadOnly").GetBoolean(), $"{where}: a read-only cell cannot carry a write binding.");
            Assert.AreEqual(0d, cell.GetProperty("Content").GetProperty("Minimum").GetDouble(), 0.0001, where);
            Assert.AreEqual(2359d, cell.GetProperty("Content").GetProperty("Maximum").GetDouble(), 0.0001, where);
            Assert.IsTrue(boundTagIds.Add(readTagId!), $"{where}: {readTagId} is already used by another cell.");
        }

        CollectionAssert.AreEqual(
            new[] { "KIC_E22_HDEG4" },
            uncovered,
            "Only E-22 period 4 lacks a setpoint register in the imported catalogue. If this list changed, the "
            + "catalogue changed: import the register and bind the cell, or record the new gap deliberately.");
        Assert.AreEqual(63, boundTagIds.Count);

        var loadedScene = await new ModernProjectStore().LoadOrCreateSceneAsync(
            Directory.GetParent(root)!.FullName,
            "win00012_modern_no_legacy_e15_e30",
            "Degivrage",
            CanvasSize.DefaultDesktop);
        var loadedTable = loadedScene.Elements.Single(element => element.Id == "table_defrost_upper").Table;
        Assert.IsNotNull(loadedTable);
        Assert.AreEqual(
            63,
            loadedTable!.EffectiveCells.Count(cell => StartHourRows.ContainsKey(cell.Row) && cell.ValueBindings is not null),
            "The bindings must survive the production store.");

        var project = await new ModernProjectStore().LoadProjectAsync(Directory.GetParent(root)!.FullName);
        Assert.IsNotNull(project);
        var cellIssues = ScadaProjectBuildValidator.Validate(project!, [loadedScene])
            .Where(issue => issue.Code.StartsWith("table-cell.", StringComparison.Ordinal))
            .Select(issue => $"{issue.Code}: {issue.Message}")
            .ToArray();
        Assert.AreEqual(
            0,
            cellIssues.Length,
            "The page must still build clean; a binding the catalogue cannot honour is reported here:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, cellIssues));
    }

    private static void AssertStatusState(
        JsonElement state,
        string expectedTagId,
        bool expectedValue,
        string expectedBackground,
        string elementId)
    {
        var expression = state.GetProperty("Expression");
        Assert.AreEqual(expectedTagId, expression.GetProperty("referencedTags").EnumerateArray().Single().GetString(), elementId);
        var ast = expression.GetProperty("ast");
        Assert.AreEqual("Equal", ast.GetProperty("Op").GetString(), elementId);
        Assert.AreEqual(expectedTagId, ast.GetProperty("Left").GetProperty("TagId").GetString(), elementId);
        Assert.AreEqual(expectedValue, ast.GetProperty("Right").GetProperty("Value").GetBoolean(), elementId);
        Assert.AreEqual(expectedBackground, state.GetProperty("Effect").GetProperty("BackgroundColor").GetString(), elementId);
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
