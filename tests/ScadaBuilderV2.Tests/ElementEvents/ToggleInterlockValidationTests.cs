using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.ElementEvents;

/// <summary>
/// Locks the build validation of a Toggle command interlock (DEC-0054): reserved to Toggle, an existing, enabled,
/// writeable, boolean tag, distinct from the write and read tags; a #N is exempt from catalog checks.
/// </summary>
[TestClass]
public sealed class ToggleInterlockValidationTests
{
    private const string PageId = "win00001";

    private static ScadaTagCatalog Catalog() => new("tf100web-scada-tags-v1", new[]
    {
        new ScadaTagDefinition("tag.start", "Depart", Datatype: "Booléen", Writeable: true),
        new ScadaTagDefinition("tag.stop", "Arret", Datatype: "Booléen", Writeable: true),
        new ScadaTagDefinition("tag.feedback", "Retour", Datatype: "Booléen", Writeable: false),
        new ScadaTagDefinition("tag.speed", "Vitesse", Datatype: "Float", Writeable: true),
        new ScadaTagDefinition("tag.disabled", "Inactif", Datatype: "Booléen", Writeable: true, Enabled: false),
    });

    private static ScadaCommandBinding Toggle(string? interlock, string write = "tag.start", string? read = null,
        ScadaWriteMode mode = ScadaWriteMode.Toggle) =>
        new("cmd", "Depart", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
            WriteTagId: write, ReadTagId: read, WriteMode: mode, FixedValue: mode == ScadaWriteMode.SetFixed ? "1" : null,
            InterlockTagId: interlock);

    private static IReadOnlyList<ScadaBuildValidationIssue> Validate(ScadaCommandBinding command, bool quickWindow = false)
    {
        var pageKey = Guid.Parse("11112222-3333-4444-5555-666677778888");
        var type = quickWindow ? ScadaPageType.QuickWindow : ScadaPageType.Default;
        var scene = (ScadaScene.CreateEmpty(PageId, "Page", new CanvasSize(400, 200)) with
            {
                PageKey = quickWindow ? pageKey : Guid.Empty,
                PageType = type
            })
            .WithElement(ScadaElement.CreateButton("btn", "Depart", 0, 0) with
            {
                CommandConfig = new ScadaElementCommandConfig(new[] { command })
            });
        var project = ScadaProject.CreateDefault("P") with
        {
            Scenes = new[] { new ScadaSceneReference(PageId, "Page", $"scenes/{PageId}.scene.json", type, PageKey: quickWindow ? pageKey : default, PageCode: PageId) },
            TagCatalog = Catalog()
        };
        return ScadaProjectBuildValidator.Validate(project, new[] { scene })
            .Where(issue => issue.Code.StartsWith("command.interlock-", StringComparison.Ordinal))
            .ToArray();
    }

    [TestMethod]
    public void AValidInterlockRaisesNoIssue()
    {
        Assert.AreEqual(0, Validate(Toggle("tag.stop")).Count);
    }

    [TestMethod]
    public void NoInterlockRaisesNoIssue()
    {
        Assert.AreEqual(0, Validate(Toggle(null)).Count);
    }

    [TestMethod]
    public void AnInterlockOutsideToggleIsAnError()
    {
        var issue = Validate(Toggle("tag.stop", mode: ScadaWriteMode.SetFixed)).Single();
        Assert.AreEqual("command.interlock-requires-toggle", issue.Code);
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        Assert.AreEqual("Scene.Elements[btn].CommandConfig.Commands[cmd].InterlockTagId", issue.PropertyPath);
    }

    [TestMethod]
    public void AnUnknownOrDisabledInterlockTagIsAnError()
    {
        Assert.AreEqual("command.interlock-tag-missing", Validate(Toggle("tag.unknown")).Single().Code);
        Assert.AreEqual("command.interlock-tag-missing", Validate(Toggle("tag.disabled")).Single().Code);
    }

    [TestMethod]
    public void AReadOnlyInterlockTagIsAnError()
    {
        Assert.AreEqual("command.interlock-tag-readonly", Validate(Toggle("tag.feedback")).Single().Code);
    }

    [TestMethod]
    public void ANonBooleanInterlockTagIsAnError()
    {
        Assert.AreEqual("command.interlock-tag-not-boolean", Validate(Toggle("tag.speed")).Single().Code);
    }

    [TestMethod]
    public void AnInterlockEqualToTheWriteOrReadTagIsAnError()
    {
        Assert.AreEqual("command.interlock-same-tag", Validate(Toggle("tag.start")).Single().Code);
        Assert.AreEqual("command.interlock-same-tag", Validate(Toggle("tag.stop", read: "tag.stop")).Single().Code);
    }

    [TestMethod]
    public void APlaceholderInterlockIsExemptFromCatalogChecksButNotFromTheSameTagRule()
    {
        Assert.AreEqual(0, Validate(Toggle("#4", write: "#3"), quickWindow: true).Count);
        Assert.AreEqual("command.interlock-same-tag", Validate(Toggle("#3", write: "#3"), quickWindow: true).Single().Code);
    }

    [TestMethod]
    public void APlaceholderInterlockOnADefaultPageIsAnError()
    {
        Assert.AreEqual("command.interlock-tag-missing", Validate(Toggle("#4", write: "#3")).Single().Code);
    }

    [TestMethod]
    public void AHumanLabelAsInterlockIsReportedLikeAWriteTag()
    {
        var issues = ScadaCommandBindingValidator.ValidateCommandBinding(Toggle("Arret"), Catalog());
        Assert.AreEqual(1, issues.Count);
        StringAssert.Contains(issues[0], "InterlockTagId");
        StringAssert.Contains(issues[0], "tag.stop");
    }
}
