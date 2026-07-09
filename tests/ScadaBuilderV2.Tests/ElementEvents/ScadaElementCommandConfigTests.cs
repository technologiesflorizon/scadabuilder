using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class ScadaElementCommandConfigTests
{
    [TestMethod]
    public void DefaultConfigHasNoCommands()
    {
        var config = ScadaElementCommandConfig.Default;

        Assert.AreEqual(0, config.Commands.Count);
    }

    [TestMethod]
    public void ToggleCommandFallsBackToWriteTagIdWhenReadTagIdMissing()
    {
        var command = new ScadaCommandBinding(
            "cmd-1", "Demarrer pompe", Enabled: true,
            Trigger: ScadaCommandTrigger.OnClick,
            Kind: ScadaCommandKind.WriteTag,
            WriteTagId: "tag-cmd-start",
            WriteMode: ScadaWriteMode.Toggle);

        Assert.AreEqual("tag-cmd-start", command.EffectiveReadTagId);
    }

    [TestMethod]
    public void ToggleCommandUsesExplicitReadTagIdWhenProvided()
    {
        var command = new ScadaCommandBinding(
            "cmd-1", "Demarrer pompe", Enabled: true,
            Trigger: ScadaCommandTrigger.OnClick,
            Kind: ScadaCommandKind.WriteTag,
            WriteTagId: "tag-cmd-start",
            ReadTagId: "tag-status-running",
            WriteMode: ScadaWriteMode.Toggle);

        Assert.AreEqual("tag-status-running", command.EffectiveReadTagId);
    }

    [TestMethod]
    public void ConfirmationIsOptional()
    {
        var withConfirmation = new ScadaCommandBinding(
            "cmd-1", "Reset", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
            Confirmation: new ScadaConfirmation("Confirmer le reset ?"));

        Assert.IsNotNull(withConfirmation.Confirmation);
        Assert.AreEqual("Confirmer le reset ?", withConfirmation.Confirmation!.Message);
    }

    [TestMethod]
    public void ValidateCommandBinding_CanonicalId_Passes()
    {
        var catalog = new ScadaTagCatalog("v1", new[]
        {
            new ScadaTagDefinition("tf100.mapping.196", "MC_120C", Datatype: "bool"),
        });
        var cmd = new ScadaCommandBinding("cmd1", "Write", true,
            ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
            WriteTagId: "tf100.mapping.196", WriteMode: ScadaWriteMode.SetFixed,
            FixedValue: "true");

        var issues = ScadaCommandBindingValidator.ValidateCommandBinding(cmd, catalog);
        Assert.AreEqual(0, issues.Count);
    }

    [TestMethod]
    public void ValidateCommandBinding_DisplayNameAsWriteTagId_ReturnsIssue()
    {
        var catalog = new ScadaTagCatalog("v1", new[]
        {
            new ScadaTagDefinition("tf100.mapping.196", "MC_120C", Datatype: "bool"),
        });
        var cmd = new ScadaCommandBinding("cmd1", "Write", true,
            ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
            WriteTagId: "MC_120C",
            WriteMode: ScadaWriteMode.SetFixed, FixedValue: "true");

        var issues = ScadaCommandBindingValidator.ValidateCommandBinding(cmd, catalog);
        Assert.IsTrue(issues.Count > 0);
        Assert.IsTrue(issues[0].Contains("MC_120C"));
        Assert.IsTrue(issues[0].Contains("tf100.mapping.196"));
    }

    [TestMethod]
    public void ValidateCommandBinding_NullCatalog_Skips()
    {
        var cmd = new ScadaCommandBinding("cmd1", "Write", true,
            ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
            WriteTagId: "nimporte", WriteMode: ScadaWriteMode.SetFixed,
            FixedValue: "true");

        var issues = ScadaCommandBindingValidator.ValidateCommandBinding(cmd, null);
        Assert.AreEqual(0, issues.Count,
            "Null catalog must skip validation (no false positives).");
    }
}
