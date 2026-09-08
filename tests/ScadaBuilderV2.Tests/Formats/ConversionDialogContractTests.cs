using System.IO;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Source contract for a surface no unit test can drive. Each assertion encodes a decision of the spec that
/// would otherwise be silently lost in a later edit.
/// </summary>
[TestClass]
public sealed class ConversionDialogContractTests
{
    [TestMethod]
    public void TheDialogOffersExactlyTwoOutcomes()
    {
        var xaml = ReadAppFile(Path.Combine("Projects", "ConversionPlanDialog.xaml"));

        StringAssert.Contains(xaml, "Content=\"Convertir\"");
        StringAssert.Contains(xaml, "Content=\"Ne pas ouvrir\"");
        Assert.IsFalse(
            xaml.Contains("sans convertir", StringComparison.OrdinalIgnoreCase),
            "C5 removed the read-only consultation mode: the generations diverge too much for a "
            + "half-migrated session to be faithful.");
    }

    [TestMethod]
    public void TheDialogSaysTheOperationDoesNotUndo()
    {
        var xaml = ReadAppFile(Path.Combine("Projects", "ConversionPlanDialog.xaml"));

        StringAssert.Contains(xaml, "ne se défait pas");
        StringAssert.Contains(xaml, "sauvegarde");
    }

    [TestMethod]
    public void CancelIsTheDefaultDecision()
    {
        var code = ReadAppFile(Path.Combine("Projects", "ConversionPlanDialog.xaml.cs"));

        StringAssert.Contains(
            code,
            "ConversionDecision Decision { get; private set; } = ConversionDecision.Cancel;",
            "closing the dialog by any other route must not convert.");
    }

    [TestMethod]
    public void ThePlanIsShownStepByStep()
    {
        var xaml = ReadAppFile(Path.Combine("Projects", "ConversionPlanDialog.xaml"));

        StringAssert.Contains(xaml, "StepDescriptions");
        StringAssert.Contains(xaml, "FilePath");

        // C5 also requires the backup path to be shown. `ConversionPlanEntry` carries no such field - the
        // dialog derives it from `FilePath` the same way `ArtifactBackupWriter` does, and says plainly that a
        // numbered suffix is used when a backup already exists, because the exact name depends on what is on
        // disk at write time and showing a name we cannot guarantee would be a lie.
        //
        // A source-contract test is only as strong as the string it searches for. Checking for a topic word
        // ("{0}.bak", "suffixe numéroté") is satisfied by any sentence that mentions the topic, including one
        // that says the opposite of what the design requires - rebinding the format string to the wrong
        // property, or writing "aucun suffixe numéroté n'est utilisé", would still pass. Both assertions below
        // instead pin the whole claim as it must appear in the markup.
        StringAssert.Contains(
            xaml,
            "{Binding FilePath, StringFormat={}{0}.bak}",
            "the shown backup path must be bound to FilePath itself - binding the same format string to a "
            + "different property (Module, ToVersion, ...) must fail this assertion.");
        StringAssert.Contains(
            xaml,
            "un suffixe numéroté est utilisé si une sauvegarde existe déjà",
            "must state the claim itself, not just mention the topic - reversing it to \"aucun suffixe "
            + "numéroté n'est utilisé\" would be a lie the dialog would tell the operator, and must not "
            + "satisfy this assertion.");
    }

    private static string ReadAppFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }
        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{relativePath}.");
        return string.Empty;
    }
}
