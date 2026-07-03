namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class HtmlPreviewControlContextMenuTests
{
    [TestMethod]
    public void AppHtmlPreviewControlSuppressesNativeContextMenuAndOpensWpfOne()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "HtmlPreviewControl.cs");

        StringAssert.Contains(code, "ObjectForScripting");
        StringAssert.Contains(code, "oncontextmenu");
        StringAssert.Contains(code, "RequestContextMenu");
        StringAssert.Contains(code, "contextMenu.IsOpen = true");
    }

    [TestMethod]
    public void StudioHtmlPreviewControlSuppressesNativeContextMenuAndOpensWpfOne()
    {
        var code = ReadProjectFile("src", "ScadaBuilderV2.ElementStudio.App", "HtmlPreviewControl.cs");

        StringAssert.Contains(code, "ObjectForScripting");
        StringAssert.Contains(code, "oncontextmenu");
        StringAssert.Contains(code, "RequestContextMenu");
        StringAssert.Contains(code, "contextMenu.IsOpen = true");
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate project file: {Path.Combine(parts)}");
        return "";
    }
}
