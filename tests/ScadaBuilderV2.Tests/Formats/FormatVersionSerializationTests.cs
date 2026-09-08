using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks the one property of the new field that matters for existing evidence: it is invisible until it is
/// set. Frozen fixtures, conformance packages and industrial proofs depend on byte-stable artifacts, and a
/// field written as `null` would change every one of them.
/// </summary>
[TestClass]
public sealed class FormatVersionSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [TestMethod]
    public void AnUnsetFormatVersionIsNotSerialised()
    {
        var json = JsonSerializer.Serialize(ScadaProject.CreateDefault("Projet"), Options);

        Assert.IsFalse(
            json.Contains("FormatVersion", StringComparison.OrdinalIgnoreCase),
            "an unset generation must leave the document byte-identical to what it was before this field existed.");
    }

    [TestMethod]
    public void ASetFormatVersionIsSerialisedAndReadBack()
    {
        var project = ScadaProject.CreateDefault("Projet") with { FormatVersion = 1 };

        var json = JsonSerializer.Serialize(project, Options);
        var round = JsonSerializer.Deserialize<ScadaProject>(json, Options);

        StringAssert.Contains(json, "\"FormatVersion\": 1");
        Assert.AreEqual(1, round!.FormatVersion);
    }

    [TestMethod]
    public void AnAbsentFormatVersionReadsBackAsGenerationZero()
    {
        var project = JsonSerializer.Deserialize<ScadaProject>(
            JsonSerializer.Serialize(ScadaProject.CreateDefault("Projet"), Options), Options);

        Assert.IsNull(project!.FormatVersion);
        Assert.AreEqual(0, project.EffectiveFormatVersion);
    }

    [TestMethod]
    public void TheSceneCarriesTheSameContract()
    {
        var scene = ScadaScene.CreateEmpty("win00001", "win00001", CanvasSize.DefaultDesktop);

        var json = JsonSerializer.Serialize(scene, Options);
        Assert.IsFalse(json.Contains("FormatVersion", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, scene.EffectiveFormatVersion);

        var versioned = JsonSerializer.Serialize(scene with { FormatVersion = 2 }, Options);
        StringAssert.Contains(versioned, "\"FormatVersion\": 2");
    }

    [TestMethod]
    public void TheTagCatalogCarriesTheSameContract()
    {
        var catalog = new ScadaTagCatalog("tf100web-scada-tags-v1", []);

        var json = JsonSerializer.Serialize(catalog, Options);
        Assert.IsFalse(json.Contains("FormatVersion", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, catalog.EffectiveFormatVersion);

        var versioned = JsonSerializer.Serialize(catalog with { FormatVersion = 1 }, Options);
        StringAssert.Contains(versioned, "\"FormatVersion\": 1");
    }
}
