using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.RuntimeContracts;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// `ManifestVersion` served two roles: the export profile negotiated with TF100Web, and a declaration of what
/// a project was allowed to contain. The second is removed. The capability catalog is already the authority
/// on allowed content - it carries status, three-layer evidence and the fail-closed gate - and a string
/// comparison was never going to say the same thing.
/// </summary>
[TestClass]
public sealed class ManifestVersionResponsibilityTests
{
    [TestMethod]
    public void QuickWindowDefinitionCapabilityIsCatalogedAsSupported()
    {
        // This only names the catalog fact the authorisation path reads - it passes whether or not
        // ValidateQuickWindows actually consults the catalog, so it proves a precondition, not the
        // authorisation itself. The end-to-end proof - a project on a non-"2.3" ManifestVersion, carrying
        // real quick-window content, cleared by ScadaProjectBuildValidator.Validate because this capability
        // is Supported - is
        // QuickWindowBuildValidationTests.ContentIsAuthorisedByCapabilityStatusEvenWhenManifestVersionNeverBecame23.
        Assert.AreEqual(
            ScadaRuntimeCapabilityStatus.Supported,
            ScadaRuntimeCapabilityCatalog.QuickWindowDefinition.Status,
            "this is what authorises quick-window content, not the string \"2.3\".");
    }

    [TestMethod]
    public void ContentValidationNoLongerReadsTheManifestVersionString()
    {
        var source = ReadSource("src/ScadaBuilderV2.Domain/Projects/ProjectModels.cs");

        // Anchored on the declaration, not the call site: `ValidateQuickWindows(` first appears as the call
        // at ProjectModels.cs:309, and a start anchor there would extract the caller's own body (lines
        // 309-427) instead of the method under test - a span containing neither `ManifestVersion` nor
        // `quick-window.profile-unsupported` regardless of any change, so the assertion below would pass
        // vacuously forever. `private static void ValidateQuickWindows(` is unique to the declaration.
        var validation = Between(source, "private static void ValidateQuickWindows(", "private static void Validate");

        // Proves the extracted span is the right method body, not an empty or wrong region: this error code
        // is only ever added inside ValidateQuickWindows, nowhere else in the file.
        Assert.IsTrue(
            validation.Contains("quick-window.duplicate-key", StringComparison.Ordinal),
            "extracted span does not look like the ValidateQuickWindows body - the anchors found the wrong method.");

        Assert.IsFalse(
            validation.Contains("ManifestVersion", StringComparison.Ordinal),
            "content validation belongs to the capability catalog.");
    }

    private static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        Assert.IsTrue(from >= 0, $"'{start}' not found.");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        return to > from ? source[from..to] : source[from..];
    }

    private static string ReadSource(string relativePath)
    {
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, relativePath);
            if (System.IO.File.Exists(candidate)) return System.IO.File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        Assert.Fail($"Unable to locate {relativePath}.");
        return string.Empty;
    }
}
