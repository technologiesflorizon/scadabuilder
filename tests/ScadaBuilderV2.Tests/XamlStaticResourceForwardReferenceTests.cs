using System.IO;
using System.Text.RegularExpressions;

namespace ScadaBuilderV2.Tests;

/// <summary>
/// Guards the one XAML mistake no other test in this repository can see: a `StaticResource` used on the root
/// element's own attributes while the key it names is defined further down, inside that same file's
/// `Resources` block.
/// </summary>
/// <remarks>
/// XAML resolves `StaticResource` while parsing, scanning only backward through dictionaries it has already
/// built. The root element's attributes are set before its `Resources` child is parsed, so a key defined there
/// does not exist yet and `StaticResourceExtension` throws at load time - the window cannot open at all.
///
/// Two dialogs shipped with exactly this defect and nothing caught them: the test project does not reference
/// `ScadaBuilderV2.App`, so no test ever loads a window, and the source-contract tests that do read this markup
/// read it as *text*. `ConversionDialogContractTests` asserted which buttons the conversion dialog offers while
/// the dialog could not be shown. A test that reads markup as a string can pin what it says; only this check
/// pins that it loads.
///
/// This is a static check, not a substitute for instantiating a window. It proves the absence of one specific
/// forward reference, which is what broke, and nothing more.
///
/// Tests: this file guards every `.xaml` under `src/`.
/// </remarks>
[TestClass]
public sealed class XamlStaticResourceForwardReferenceTests
{
    [TestMethod]
    public void NoRootElementAttributeUsesAResourceDefinedLaterInItsOwnFile()
    {
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(SourceRoot(), "*.xaml", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(file);
            var resources = Regex.Match(markup, @"<(?:Window|UserControl|Application|Page)\.Resources>");
            if (!resources.Success) continue;

            // Everything before the Resources block is set on the root element first, at parse time.
            var rootAttributes = markup[..resources.Index];
            var definedLater = Regex.Matches(markup[resources.Index..], @"x:Key=""([^""]+)""")
                .Select(match => match.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (Match reference in Regex.Matches(rootAttributes, @"\{StaticResource\s+([^}]+?)\s*\}"))
            {
                var key = reference.Groups[1].Value;
                if (definedLater.Contains(key))
                {
                    offenders.Add($"{Path.GetFileName(file)}: root attribute uses '{key}', defined below in its own Resources");
                }
            }
        }

        Assert.AreEqual(
            0,
            offenders.Count,
            "a root attribute cannot use a key its own Resources block defines later - the window throws on "
            + "load. Move the brush onto the root's child element, or define it at application scope:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>Positive anchor: the scan must actually be reading files, not passing on an empty set.</summary>
    [TestMethod]
    public void TheScanReachesTheApplicationMarkup()
    {
        var files = Directory.EnumerateFiles(SourceRoot(), "*.xaml", SearchOption.AllDirectories).ToList();

        Assert.IsTrue(files.Count >= 20, $"expected the whole XAML surface, found {files.Count}.");
        Assert.IsTrue(
            files.Any(path => Path.GetFileName(path) == "ConversionPlanDialog.xaml"),
            "the conversion dialog is one of the two files this check exists for; it must be in scope.");
        Assert.IsTrue(
            files.Any(path => Path.GetFileName(path) == "UnsavedChangesDialog.xaml"),
            "the unsaved-changes dialog is the other; it must be in scope.");
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        Assert.Fail("Unable to locate src/.");
        return string.Empty;
    }
}
