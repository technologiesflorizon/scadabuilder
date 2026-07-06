using ScadaBuilderV2.Infrastructure.Shell;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class DockLayoutStoreTests
{
    private string _tempDirectory = "";
    private string _tempPath = "";

    [TestInitialize]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"scada-builder-v2-dock-layout-test-{Guid.NewGuid():N}");
        _tempPath = Path.Combine(_tempDirectory, "dock-layout.xml");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ReadLayoutXmlAsyncReturnsNullWhenFileMissing()
    {
        var store = new DockLayoutStore();

        var result = await store.ReadLayoutXmlAsync(_tempPath);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task WriteThenReadRoundTripsLayoutXml()
    {
        var store = new DockLayoutStore();
        const string xml = "<LayoutRoot><RootPanel/></LayoutRoot>";

        await store.WriteLayoutXmlAsync(_tempPath, xml);
        var result = await store.ReadLayoutXmlAsync(_tempPath);

        Assert.AreEqual(xml, result);
    }

    [TestMethod]
    public async Task WriteLayoutXmlAsyncCreatesMissingDirectory()
    {
        var store = new DockLayoutStore();
        Assert.IsFalse(Directory.Exists(_tempDirectory));

        await store.WriteLayoutXmlAsync(_tempPath, "<LayoutRoot/>");

        Assert.IsTrue(File.Exists(_tempPath));
    }

    [TestMethod]
    public void GetDefaultLayoutPathEndsWithScadaBuilderV2DockLayoutXml()
    {
        var store = new DockLayoutStore();

        var path = store.GetDefaultLayoutPath();

        Assert.IsTrue(path.EndsWith(Path.Combine("ScadaBuilderV2", "dock-layout.xml"), StringComparison.Ordinal));
    }
}
