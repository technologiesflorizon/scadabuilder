using ScadaBuilderV2.Domain.ElementEvents.Command;

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
}
