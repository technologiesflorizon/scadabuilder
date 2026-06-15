using System.Text.Json;
using ScadaBuilderV2.Application.ElementStudio;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Infrastructure.ElementStudio;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementStudioImportPackageWriterTests
{
    [TestMethod]
    public async Task WriteToProjectCreatesFt1PackageUnderStudioImports()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var package = CreatePackage();
        var writer = new ElementStudioImportPackageWriter();

        try
        {
            var path = await writer.WriteToProjectAsync(package, root);

            Assert.AreEqual(
                Path.Combine(root, "AMR_REF_SCADA_V2", ".studio", "imports", "pkg_001.ft1"),
                path);
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual(".ft1", Path.GetExtension(path));

            var json = await File.ReadAllTextAsync(path);
            StringAssert.Contains(json, "\"Schema\": \"scada-builder-v2.element-studio.import\"");
            StringAssert.Contains(json, "\"SchemaVersion\": 1");
            StringAssert.Contains(json, "\"PackageId\": \"pkg_001\"");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task WriteToPathRejectsNonFt1Extension()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var package = CreatePackage();
        var writer = new ElementStudioImportPackageWriter();

        try
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                writer.WriteToPathAsync(package, Path.Combine(root, "pkg_001.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task WrittenFt1RoundTripsAsMinimalJsonImportPackage()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var package = CreatePackage();
        var writer = new ElementStudioImportPackageWriter();

        try
        {
            var path = await writer.WriteToProjectAsync(package, root);
            await using var read = File.OpenRead(path);
            var loaded = await JsonSerializer.DeserializeAsync<ElementStudioImportPackage>(
                read,
                ElementStudioImportPackageWriter.CreateJsonSerializerOptions());

            Assert.IsNotNull(loaded);
            Assert.AreEqual("pkg_001", loaded.PackageId);
            Assert.AreEqual(ElementStudioPackageMetadata.CurrentSchema, loaded.Metadata.Schema);
            Assert.AreEqual(ElementStudioPackageMetadata.CurrentSchemaVersion, loaded.Metadata.SchemaVersion);
            Assert.AreEqual("win00008", loaded.SourceSceneId);
            Assert.AreEqual(@"F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\projects\AMR_REF_SCADA_V2\library\elements", loaded.TargetLibraryPath);
            Assert.AreEqual(2, loaded.Items.Count);
            Assert.AreEqual("polygon", loaded.Items[0].LegacyType);
            Assert.AreEqual("M 10 20 L 20 20 Z", loaded.Items[0].Geometry);
            Assert.AreEqual("<polygon points=\"10,20 20,20\" />", loaded.Items[0].LegacyMarkup);
            Assert.AreEqual("Text27", loaded.Items[1].Text);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void FactoryCalculatesPackageBoundsAndRelativeItemBounds()
    {
        var package = CreatePackage();

        Assert.AreEqual(75, package.Bounds.X);
        Assert.AreEqual(179, package.Bounds.Y);
        Assert.AreEqual(105, package.Bounds.Width);
        Assert.AreEqual(51, package.Bounds.Height);
        Assert.AreEqual(0, package.Items[0].BoundsRelativeToPackage.X);
        Assert.AreEqual(0, package.Items[0].BoundsRelativeToPackage.Y);
        Assert.AreEqual(65, package.Items[1].BoundsRelativeToPackage.X);
        Assert.AreEqual(27, package.Items[1].BoundsRelativeToPackage.Y);
        Assert.AreEqual("V2.0.3.0001", package.Metadata.CreatedByVersion);
        Assert.AreEqual(@"F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\projects\AMR_REF_SCADA_V2\library\elements", package.TargetLibraryPath);
    }

    [TestMethod]
    public async Task WriteToProjectCopiesReferencedImageAssetsNextToFt1()
    {
        var root = Path.Combine(Path.GetTempPath(), "ScadaBuilderV2Tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source");
        var sourceAssets = Path.Combine(sourceRoot, "html_pages", "assets");
        Directory.CreateDirectory(sourceAssets);
        await File.WriteAllBytesAsync(Path.Combine(sourceAssets, "pump.png"), [0x89, 0x50, 0x4E, 0x47]);

        var package = ElementStudioImportPackageFactory.Create(
            "pkg_image",
            "AMR_REF_SCADA_V2",
            "win00008",
            Path.Combine(sourceRoot, "html_pages", "win00008.html"),
            new[]
            {
                new ElementStudioLegacyItem(
                    "97",
                    "Image4",
                    "Image",
                    new SceneBounds(709, 102, 199, 216),
                    new SceneBounds(0, 0, 0, 0),
                    null,
                    "<img src=\"assets/pump.png\" style=\"position:absolute; left:708px; top:101px; width:199px; height:216px;\" />",
                    null,
                    ElementStudioStyleSnapshot.Default,
                    42,
                    null)
            },
            ElementStudioPackageMetadata.Current("V2.0.3.0001"));
        var writer = new ElementStudioImportPackageWriter();

        try
        {
            var path = await writer.WriteToProjectAsync(package, Path.Combine(root, "projects"));
            var assetPath = Path.Combine(Path.GetDirectoryName(path)!, "assets", "pump.png");
            var json = await File.ReadAllTextAsync(path);

            Assert.IsTrue(File.Exists(assetPath));
            StringAssert.Contains(json, "\"ZIndex\": 42");
            StringAssert.Contains(json, "src=\\u0022assets/pump.png\\u0022");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ElementStudioImportPackage CreatePackage()
    {
        return ElementStudioImportPackageFactory.Create(
            "pkg_001",
            "AMR_REF_SCADA_V2",
            "win00008",
            "dist/pages/win00008.html",
            new[]
            {
                new ElementStudioLegacyItem(
                    "793",
                    "Polygon9",
                    "polygon",
                    new SceneBounds(75, 179, 41, 24),
                    new SceneBounds(0, 0, 0, 0),
                    "M 10 20 L 20 20 Z",
                    "<polygon points=\"10,20 20,20\" />",
                    null,
                    ElementStudioStyleSnapshot.Default with
                    {
                        Foreground = "rgb(255, 255, 255)",
                        Background = "transparent"
                    },
                    4,
                    "{\"source\":\"legacy-svg\"}"),
                new ElementStudioLegacyItem(
                    "794",
                    "Text27",
                    "text",
                    new SceneBounds(140, 206, 40, 24),
                    new SceneBounds(0, 0, 0, 0),
                    null,
                    "<span>Text27</span>",
                    "Text27",
                    ElementStudioStyleSnapshot.Default,
                    5,
                    null)
            },
            ElementStudioPackageMetadata.Current("V2.0.3.0001"),
            @"F:\Groupe AMR\SCADA_AMR_GROUP\SCADA_BUILDER_V2\projects\AMR_REF_SCADA_V2\library\elements");
    }
}
