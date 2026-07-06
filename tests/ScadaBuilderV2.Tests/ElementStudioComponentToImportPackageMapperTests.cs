using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementStudioComponentToImportPackageMapperTests
{
    [TestMethod]
    public void ToEditablePackagePreservesPartGeometryAndSetsTargetLibraryPath()
    {
        var sepPackage = CreateComponentPackage(
            CreatePart("part-1", "Condenseur_corps", ElementStudioComponentPartKind.Rectangle));

        var result = ElementStudioComponentToImportPackageMapper.ToEditablePackage(
            sepPackage,
            @"C:\projects\AMR_REF_SCADA_V2\library\elements\Condenseur.sep",
            "V2.1.3.0003");

        var item = result.Items.Single();
        Assert.AreEqual("part-1", item.SourceElementId);
        Assert.AreEqual("Condenseur_corps", item.SourceName);
        Assert.AreEqual("Rectangle", item.LegacyType);
        Assert.AreEqual(@"C:\projects\AMR_REF_SCADA_V2\library\elements", result.TargetLibraryPath);
    }

    [TestMethod]
    public void ToEditablePackageFlattensGroupPartsIntoTheirChildren()
    {
        var groupChild = CreatePart("child-1", "Aile1", ElementStudioComponentPartKind.Polygon);
        var group = CreatePart("group-1", "Ailettes", ElementStudioComponentPartKind.Group) with
        {
            Children = new[] { groupChild }
        };
        var sepPackage = CreateComponentPackage(group);

        var result = ElementStudioComponentToImportPackageMapper.ToEditablePackage(
            sepPackage,
            @"C:\lib\Condenseur.sep",
            "V2.1.3.0003");

        Assert.AreEqual(1, result.Items.Count);
        Assert.AreEqual("child-1", result.Items[0].SourceElementId);
    }

    [TestMethod]
    public void ToEditablePackageUsesSourceTraceWhenPresent()
    {
        var sourceTrace = new ElementStudioComponentSourceTrace(
            "AMR_REF_SCADA_V2",
            "win00008",
            "dist/pages/win00008.html",
            new[] { "793" });
        var sepPackage = CreateComponentPackage(
            CreatePart("part-1", "Condenseur_corps", ElementStudioComponentPartKind.Rectangle),
            sourceTrace);

        var result = ElementStudioComponentToImportPackageMapper.ToEditablePackage(
            sepPackage,
            @"C:\lib\Condenseur.sep",
            "V2.1.3.0003");

        Assert.AreEqual("AMR_REF_SCADA_V2", result.SourceProjectId);
        Assert.AreEqual("win00008", result.SourceSceneId);
        Assert.AreEqual("dist/pages/win00008.html", result.SourcePagePath);
    }

    [TestMethod]
    public void ToEditablePackageThrowsWhenComponentHasNoParts()
    {
        var sepPackage = CreateComponentPackage();

        Assert.ThrowsException<InvalidOperationException>(() =>
            ElementStudioComponentToImportPackageMapper.ToEditablePackage(
                sepPackage,
                @"C:\lib\Vide.sep",
                "V2.1.3.0003"));
    }

    private static ElementStudioComponentPackage CreateComponentPackage(
        params ElementStudioComponentPart[] parts)
    {
        return CreateComponentPackage(parts, sourceTrace: null);
    }

    private static ElementStudioComponentPackage CreateComponentPackage(
        ElementStudioComponentPart part,
        ElementStudioComponentSourceTrace sourceTrace)
    {
        return CreateComponentPackage(new[] { part }, sourceTrace);
    }

    private static ElementStudioComponentPackage CreateComponentPackage(
        ElementStudioComponentPart[] parts,
        ElementStudioComponentSourceTrace? sourceTrace)
    {
        var component = new ElementStudioComponent(
            "condenseur",
            "Condenseur",
            new SceneBounds(0, 0, 120, 80),
            new ElementStudioComponentVisual(ElementStudioComponentVisualKind.Svg, SvgMarkup: "<svg></svg>"),
            parts,
            Array.Empty<ElementStudioEmbeddedAsset>(),
            Array.Empty<ElementStudioComponentBinding>(),
            Array.Empty<ElementStudioComponentEvent>(),
            sourceTrace);
        return new ElementStudioComponentPackage(
            ElementStudioComponentMetadata.Current("V2.1.3.0003"),
            component);
    }

    private static ElementStudioComponentPart CreatePart(
        string partId,
        string name,
        ElementStudioComponentPartKind kind)
    {
        return new ElementStudioComponentPart(
            partId,
            name,
            kind,
            new SceneBounds(10, 20, 30, 40),
            ElementStudioStyleSnapshot.Default,
            Geometry: "M 10 20 L 40 20 L 40 60 Z");
    }
}
