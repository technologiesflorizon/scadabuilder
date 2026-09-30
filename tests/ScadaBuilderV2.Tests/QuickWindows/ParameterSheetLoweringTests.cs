using System.Text.Json;
using System.Text.Json.Serialization;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Application.RuntimeContracts;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Lowering of quick-window pages, parameter sheets and open commands onto the DEC-0050 structures, and the
/// per-pair (sheet, page) validation that runs on the lowered project.
/// </summary>
/// <remarks>
/// Decisions: DEC-0050, DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §4, §6.
/// </remarks>
[TestClass]
public sealed class ParameterSheetLoweringTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Guid QuickWindowPageKey = Guid.Parse("5a000000-0000-4000-8000-0000000000a1");
    private static readonly Guid CallerPageKey = Guid.Parse("5a000000-0000-4000-8000-0000000000c1");
    private static readonly Guid SheetKey = Guid.Parse("5a000000-0000-4000-8000-0000000000f3");

    private const string QuickWindowPageCode = "popup_fan";
    private const string CallerPageCode = "synoptique";
    private const string SheetCode = "fan_03";
    private const string SheetName = "Ventilateur 3";
    private const string OpenElementId = "open-fan";
    private const string OpenCommandId = "open";

    private const string FullSheet =
        "// Ventilateur condenseur 3\n" +
        "#1 := {FAN_03_RUN}\n" +
        "#2 := {FAN_03_START}\n" +
        "#3 := {FAN_03_SPEED} * 0.1\n" +
        "#4 := \"Ventilateur 3\"\n";

    [TestMethod]
    public void AQuickWindowPageBecomesADefinitionKeyedByItsPageKeyWithOneMemberPerUsedPlaceholder()
    {
        var result = Lower(FullSheet, FanPageElements());

        var definition = result.Project.EffectiveQuickWindows.Single();
        Assert.AreEqual(QuickWindowPageKey, definition.DefinitionKey, "ruling R4: the definition key is the page key");
        Assert.AreEqual(QuickWindowPageCode, definition.Code);
        Assert.AreEqual("Popup ventilateur", definition.DisplayName);
        Assert.AreEqual(1, definition.InterfaceVersion);
        Assert.AreEqual(FanPageElements().Length, definition.EffectiveContent.EffectiveElements.Count);
        Assert.AreEqual(
            "Ventilateur #4",
            definition.EffectiveContent.EffectiveElements.Single(element => element.Id == "title").Data!.Text,
            "the content keeps its #N untouched");

        var members = definition.EffectiveInterfaceMembers;
        CollectionAssert.AreEqual(new[] { "#1", "#2", "#3", "#4" }, members.Select(member => member.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { 1, 2, 3, 4 }.Select(number => QuickWindowKeys.Member(QuickWindowPageKey, number)).ToArray(),
            members.Select(member => member.MemberKey).ToArray());

        var write = members.Single(member => member.Name == "#2");
        Assert.AreEqual(QuickWindowInterfaceFamily.WriteCommand, write.Family);
        Assert.AreEqual(QuickWindowMemberAccess.Write, write.Access);
        foreach (var read in members.Where(member => member.Name != "#2"))
        {
            Assert.AreEqual(QuickWindowInterfaceFamily.ReadState, read.Family, read.Name);
            Assert.AreEqual(QuickWindowMemberAccess.Read, read.Access, read.Name);
        }

        // spec §6 (fix round 1, Task 6): a member's DataType is deduced from its bindings' agreeing tag/literal
        // types (#1, #2: Tag-bound to a Boolean tag); #3 is Expression-bound (untyped, kept String); #4 is a
        // string Literal (String).
        Assert.AreEqual(QuickWindowDataType.Boolean, members.Single(member => member.Name == "#1").DataType);
        Assert.AreEqual(QuickWindowDataType.Boolean, members.Single(member => member.Name == "#2").DataType);
        Assert.AreEqual(QuickWindowDataType.String, members.Single(member => member.Name == "#3").DataType);
        Assert.AreEqual(QuickWindowDataType.String, members.Single(member => member.Name == "#4").DataType);
    }

    [TestMethod]
    public void ATagBoundBooleanPlaceholderInfersItsMemberDataType()
    {
        var result = Lower(FullSheet, FanPageElements());
        Assert.AreEqual(0, result.Diagnostics.Count, Describe(result));

        var member1 = result.Project.EffectiveQuickWindows.Single().EffectiveInterfaceMembers.Single(member => member.Name == "#1");
        Assert.AreEqual(QuickWindowDataType.Boolean, member1.DataType, "#1 is Tag-bound to FAN_03_RUN (Boolean) by the only invocation");

        var validation = ScadaProjectBuildValidator.Validate(result.Project, result.Scenes.Values.ToArray());
        Assert.IsFalse(
            validation.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error),
            string.Join(" | ", validation.Select(issue => $"{issue.Severity} {issue.Code}: {issue.Message}")));
    }

    [TestMethod]
    public void ATagBoundNumericPlaceholderInfersItsMemberDataType()
    {
        const string numericSheet =
            "#1 := {FAN_03_RUN}\n" +
            "#2 := {FAN_03_START}\n" +
            "#3 := {FAN_03_SPEED}\n" +
            "#4 := \"Ventilateur 3\"\n";
        var result = Lower(numericSheet, FanPageElements());
        Assert.AreEqual(0, result.Diagnostics.Count, Describe(result));

        var member3 = result.Project.EffectiveQuickWindows.Single().EffectiveInterfaceMembers.Single(member => member.Name == "#3");
        Assert.AreEqual(QuickWindowDataType.Decimal, member3.DataType, "#3 is Tag-bound to FAN_03_SPEED (Decimal) by the only invocation");

        var validation = ScadaProjectBuildValidator.Validate(result.Project, result.Scenes.Values.ToArray());
        Assert.IsFalse(
            validation.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error),
            string.Join(" | ", validation.Select(issue => $"{issue.Severity} {issue.Code}: {issue.Message}")));
    }

    [TestMethod]
    public void TwoSheetsDisagreeingOnAPlaceholderTypeMakeItStringWithoutErrorAndValidate()
    {
        var secondSheetKey = Guid.Parse("5a000000-0000-4000-8000-0000000000f4");
        const string secondSheetName = "Ventilateur 3 (bis)";
        const string sheetBText =
            "#1 := {FAN_03_SPEED}\n" +
            "#2 := {FAN_03_START}\n" +
            "#3 := {FAN_03_SPEED}\n" +
            "#4 := \"Ventilateur 3\"\n";

        var (quickWindowRef, quickWindowScene) = Page(QuickWindowPageKey, ScadaPageType.QuickWindow, QuickWindowPageCode, "Popup ventilateur", FanPageElements());
        var callerScene = ScadaScene.CreateEmpty(CallerPageCode, "Synoptique", new CanvasSize(430, 215)) with
        {
            PageKey = CallerPageKey,
            PageCode = CallerPageCode,
            PageType = ScadaPageType.Default,
            Elements =
            [
                OpenCommandElementWithIds("open-a", "open-a-cmd", SheetKey, QuickWindowPageKey),
                OpenCommandElementWithIds("open-b", "open-b-cmd", secondSheetKey, QuickWindowPageKey),
            ]
        };
        var callerRef = new ScadaSceneReference(
            CallerPageCode, "Synoptique", $"scenes/{CallerPageCode}.scene.json", ScadaPageType.Default,
            PageKey: CallerPageKey, PageCode: CallerPageCode);

        var project = ScadaProject.CreateDefault("ParameterSheetLoweringTypeConflict") with
        {
            ManifestVersion = "2.3",
            HomePageId = CallerPageCode,
            Scenes = [callerRef, quickWindowRef],
            TagCatalog = Catalog(),
            ParameterSheets =
            [
                new ParameterSheet(SheetKey, SheetCode, SheetName, FullSheet),
                new ParameterSheet(secondSheetKey, "fan_03_b", secondSheetName, sheetBText)
            ]
        };
        var scenes = new Dictionary<Guid, ScadaScene>
        {
            [CallerPageKey] = callerScene,
            [QuickWindowPageKey] = quickWindowScene
        };

        var result = ParameterSheetLowering.Lower(project, scenes);

        // Final review I5 (controller ruling, back to spec §6): a disagreement is not an error.
        Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Severity == ScadaBuildValidationSeverity.Error), Describe(result));
        var members = result.Project.EffectiveQuickWindows.Single().EffectiveInterfaceMembers;
        Assert.AreEqual(QuickWindowDataType.String, members.Single(member => member.Name == "#1").DataType,
            "#1 is a Boolean tag for one sheet and a Decimal tag for the other: the member is String");
        Assert.AreEqual(QuickWindowDataType.Decimal, members.Single(member => member.Name == "#3").DataType,
            "#3 is only typed by the second sheet's Decimal tag (the first binds an untyped expression): inference stays");

        // A Boolean tag and a Decimal tag bound to the same String #N member both validate.
        var validation = ScadaProjectBuildValidator.Validate(result.Project, result.Scenes.Values.ToArray());
        Assert.IsFalse(
            validation.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error),
            string.Join(" | ", validation.Select(issue => $"{issue.Severity} {issue.Code}: {issue.Message}")));
    }

    [TestMethod]
    public void TwoDistinctSheetsSharingADisplayNameStillLowerAndValidate()
    {
        // Same DisplayName ("Ventilateur 3") on two distinct sheets that disagree on #1's type: still two
        // invocations, a String #1 and no error (final review I5).
        var secondSheetKey = Guid.Parse("5a000000-0000-4000-8000-0000000000f5");
        const string sheetBText =
            "#1 := {FAN_03_SPEED}\n" +
            "#2 := {FAN_03_START}\n" +
            "#3 := {FAN_03_SPEED}\n" +
            "#4 := \"Ventilateur 3\"\n";

        var (quickWindowRef, quickWindowScene) = Page(QuickWindowPageKey, ScadaPageType.QuickWindow, QuickWindowPageCode, "Popup ventilateur", FanPageElements());
        var callerScene = ScadaScene.CreateEmpty(CallerPageCode, "Synoptique", new CanvasSize(430, 215)) with
        {
            PageKey = CallerPageKey,
            PageCode = CallerPageCode,
            PageType = ScadaPageType.Default,
            Elements =
            [
                OpenCommandElementWithIds("open-a", "open-a-cmd", SheetKey, QuickWindowPageKey),
                OpenCommandElementWithIds("open-b", "open-b-cmd", secondSheetKey, QuickWindowPageKey),
            ]
        };
        var callerRef = new ScadaSceneReference(
            CallerPageCode, "Synoptique", $"scenes/{CallerPageCode}.scene.json", ScadaPageType.Default,
            PageKey: CallerPageKey, PageCode: CallerPageCode);

        var project = ScadaProject.CreateDefault("ParameterSheetLoweringSameNameConflict") with
        {
            ManifestVersion = "2.3",
            HomePageId = CallerPageCode,
            Scenes = [callerRef, quickWindowRef],
            TagCatalog = Catalog(),
            ParameterSheets =
            [
                new ParameterSheet(SheetKey, SheetCode, SheetName, FullSheet),
                new ParameterSheet(secondSheetKey, "fan_03_same_name", SheetName, sheetBText) // same DisplayName, distinct SheetKey/Code
            ]
        };
        var scenes = new Dictionary<Guid, ScadaScene>
        {
            [CallerPageKey] = callerScene,
            [QuickWindowPageKey] = quickWindowScene
        };

        var result = ParameterSheetLowering.Lower(project, scenes);

        Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Severity == ScadaBuildValidationSeverity.Error), Describe(result));
        var member1 = result.Project.EffectiveQuickWindows.Single().EffectiveInterfaceMembers.Single(member => member.Name == "#1");
        Assert.AreEqual(QuickWindowDataType.String, member1.DataType);
        Assert.AreEqual(2, result.Project.EffectiveQuickWindowInvocations.Count, "each sheet still produces its own invocation");
        var validation = ScadaProjectBuildValidator.Validate(result.Project, result.Scenes.Values.ToArray());
        Assert.IsFalse(
            validation.Any(issue => issue.Severity == ScadaBuildValidationSeverity.Error),
            string.Join(" | ", validation.Select(issue => $"{issue.Severity} {issue.Code}: {issue.Message}")));
    }

    [TestMethod]
    public void AnOpenCommandBecomesAnInvocationWithOneBindingPerUsedPlaceholder()
    {
        var result = Lower(FullSheet, FanPageElements());

        Assert.AreEqual(0, result.Diagnostics.Count, Describe(result));
        var invocation = result.Project.EffectiveQuickWindowInvocations.Single();
        Assert.AreEqual(QuickWindowPageKey, invocation.DefinitionKey);
        Assert.AreEqual(1, invocation.InterfaceVersion);
        Assert.AreEqual(OpenElementId, invocation.OwnerElementId);
        Assert.AreEqual(OpenCommandId, invocation.OwnerCommandId);
        Assert.IsNull(invocation.OwnerPageKey, "package validation forbids *PageKey properties (KNOWN_GAPS entry 30)");
        CollectionAssert.AreEqual(
            new[] { 1, 2, 3, 4 }.Select(number => QuickWindowKeys.Member(QuickWindowPageKey, number)).ToArray(),
            invocation.Bindings.Select(binding => binding.MemberKey).ToArray());
    }

    [TestMethod]
    public void BindingKindsFollowTheSheetClassification()
    {
        var result = Lower(FullSheet, FanPageElements());
        var invocation = result.Project.EffectiveQuickWindowInvocations.Single();

        var run = invocation.FindBinding(QuickWindowKeys.Member(QuickWindowPageKey, 1))!;
        Assert.AreEqual(QuickWindowBindingSourceKind.Tag, run.SourceKind);
        Assert.AreEqual("tf100.mapping.101", run.TagId, "the tag name is resolved to its canonical id");

        var start = invocation.FindBinding(QuickWindowKeys.Member(QuickWindowPageKey, 2))!;
        Assert.AreEqual(QuickWindowBindingSourceKind.Tag, start.SourceKind);
        Assert.AreEqual("tf100.mapping.102", start.TagId);

        var speed = invocation.FindBinding(QuickWindowKeys.Member(QuickWindowPageKey, 3))!;
        Assert.AreEqual(QuickWindowBindingSourceKind.Expression, speed.SourceKind);
        Assert.AreEqual("{FAN_03_SPEED} * 0.1", speed.Expression);
        Assert.IsInstanceOfType(speed.ExpressionAst, typeof(ScadaExprBinary), "ruling R3: the AST travels with the source");
        Assert.IsNull(speed.TagId);
        var speedTagRef = (ScadaExprTagRef)((ScadaExprBinary)speed.ExpressionAst!).Left;
        Assert.AreEqual(
            "tf100.mapping.103", speedTagRef.TagName,
            "the tag reference INSIDE an Expression binding's AST must be canonicalized exactly like a " +
            "page state expression's (Ft100SceneExporter.NormalizeAstForExport) — the shared runtime's " +
            "TagBridge resolves every tagRef by this exact string, and the deployed mapping id is never " +
            "the human-readable tag name a parameter sheet was written with.");
        Assert.AreEqual(speedTagRef.TagName, speedTagRef.TagId, "a canonicalized tagRef carries the same value in both fields");

        var name = invocation.FindBinding(QuickWindowKeys.Member(QuickWindowPageKey, 4))!;
        Assert.AreEqual(QuickWindowBindingSourceKind.Literal, name.SourceKind);
        Assert.AreEqual("Ventilateur 3", name.LiteralValue, "the literal carries its value, without quotes");
        Assert.IsNull(name.ExpressionAst);
    }

    [TestMethod]
    public void TheCommandReceivesItsDeterministicInvocationKey()
    {
        var result = Lower(FullSheet, FanPageElements());

        var expected = QuickWindowKeys.Invocation(CallerPageKey, OpenElementId, OpenCommandId);
        Assert.AreEqual(expected, QuickWindowKeys.Invocation(CallerPageKey, OpenElementId, OpenCommandId), "pure derivation");
        Assert.AreNotEqual(expected, QuickWindowKeys.Invocation(CallerPageKey, OpenElementId, "other"));
        Assert.AreEqual(5, (expected.ToByteArray()[7] >> 4) & 0x0F, "version nibble of a name-based key");

        var command = result.Scenes[CallerPageKey].Elements
            .Single(element => element.Id == OpenElementId)
            .EffectiveCommandConfig.Commands.Single();
        Assert.AreEqual(expected, command.QuickWindowInvocationKey);
        Assert.AreEqual(SheetKey, command.ParameterSheetKey, "authoring keys are kept");
        Assert.AreEqual(QuickWindowPageKey, command.QuickWindowPageKey);
        Assert.AreEqual(expected, result.Project.EffectiveQuickWindowInvocations.Single().InvocationKey);
    }

    [TestMethod]
    public void LoweringTwiceIsByteIdentical()
    {
        var (project, scenes) = Arrange(FullSheet, FanPageElements());

        var first = ParameterSheetLowering.Lower(project, scenes);
        var second = ParameterSheetLowering.Lower(project, scenes);

        Assert.AreEqual(Serialize(first), Serialize(second));
        StringAssert.Contains(Serialize(first), QuickWindowKeys.Invocation(CallerPageKey, OpenElementId, OpenCommandId).ToString("D"));
    }

    [TestMethod]
    public void AQuickWindowPageIsRemovedFromExportedPages()
    {
        var (project, scenes) = Arrange(FullSheet, FanPageElements());
        Assert.IsTrue(
            ScadaRuntimeCapabilityAnalyzer.Analyze(project, scenes.Values.ToArray()).BlockedCapabilities
                .Any(capability => capability.Id == "page.quick-window"),
            "precondition: the unlowered project is refused by the capability gate");

        var result = ParameterSheetLowering.Lower(project, scenes);

        Assert.IsFalse(result.Scenes.ContainsKey(QuickWindowPageKey));
        Assert.IsTrue(result.Scenes.ContainsKey(CallerPageKey));
        CollectionAssert.AreEqual(new[] { CallerPageKey }, result.Project.Scenes.Select(page => page.PageKey).ToArray());

        // No ScadaPageType.QuickWindow reference may survive anywhere in what export consumes.
        Assert.IsFalse(result.Project.Scenes.Any(page => page.Type == ScadaPageType.QuickWindow));
        Assert.IsFalse(result.Scenes.Values.Any(scene => scene.PageType == ScadaPageType.QuickWindow));
        var json = Serialize(result);
        Assert.IsFalse(json.Contains("\"QuickWindow\"", StringComparison.Ordinal), "no page type QuickWindow in the lowered JSON");

        var analysis = ScadaRuntimeCapabilityAnalyzer.Analyze(result.Project, result.Scenes.Values.ToArray());
        Assert.IsFalse(
            analysis.BlockedCapabilities.Any(capability => capability.Id == "page.quick-window"),
            string.Join(", ", analysis.BlockedCapabilities.Select(capability => capability.Id)));
    }

    [TestMethod]
    public void APlaceholderUsedButUndefinedIsAnError()
    {
        var result = Lower("#1 := {FAN_03_RUN}\n#2 := {FAN_03_START}\n#3 := {FAN_03_SPEED}", FanPageElements());

        var issue = Single(result, "parameter-sheet.placeholder-undefined");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, $"Feuille '{SheetName}'");
        StringAssert.Contains(issue.Message, $"page '{QuickWindowPageCode}'");
        StringAssert.Contains(issue.Message, "#4");
        Assert.AreEqual($"Scenes[{CallerPageCode}].{OpenElementId}", issue.PropertyPath);
        Assert.AreEqual(OpenCommandId, issue.CommandId);
    }

    [TestMethod]
    public void AWriteToANonTagIsAnError()
    {
        var result = Lower(
            "#1 := {FAN_03_RUN}\n#2 := {FAN_03_START} + 1\n#3 := {FAN_03_SPEED}\n#4 := \"Ventilateur 3\"",
            FanPageElements());

        var issue = Single(result, "parameter-sheet.write-not-tag");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, "#2");
        StringAssert.Contains(issue.Message, "ligne 2");
        StringAssert.Contains(issue.Message, $"Feuille '{SheetName}'");
        Assert.IsNull(
            result.Project.EffectiveQuickWindowInvocations.Single().FindBinding(QuickWindowKeys.Member(QuickWindowPageKey, 2)),
            "an invalid pair produces no binding for that #N");
    }

    [TestMethod]
    public void EveryReadSiteBoundToANonTagIsAnError()
    {
        // Final review C1: the runtime substitutes a read site (data-scada-read-tag, readVariable.tagId, a
        // command's readTagId, a table cell read) only with a tag id and refuses the mount otherwise. The pair
        // must therefore fail validation, never pass it and then refuse to open (spec §7).
        foreach (var (site, element) in ReadSiteElements("#9"))
        {
            foreach (var rightHandSide in new[] { "{FAN_03_SPEED} * 0.1", "\"Ventilateur 3\"", "12" })
            {
                var result = Lower(FullSheet + $"#9 := {rightHandSide}\n", [.. FanPageElements(), element]);

                var issue = Single(result, "parameter-sheet.read-not-tag");
                Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity, $"{site} {rightHandSide}");
                StringAssert.Contains(issue.Message, $"Feuille '{SheetName}'", site);
                StringAssert.Contains(issue.Message, $"page '{QuickWindowPageCode}'", site);
                StringAssert.Contains(issue.Message, "#9", site);
                StringAssert.Contains(issue.Message, "ligne 6", site);
                Assert.IsNull(
                    result.Project.EffectiveQuickWindowInvocations.Single().FindBinding(QuickWindowKeys.Member(QuickWindowPageKey, 9)),
                    $"{site}: an invalid pair produces no binding for that #N");
            }
        }
    }

    [TestMethod]
    public void EveryReadSiteBoundToATagValidates()
    {
        foreach (var (site, element) in ReadSiteElements("#9"))
        {
            var result = Lower(FullSheet + "#9 := {FAN_03_SPEED}\n", [.. FanPageElements(), element]);

            Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Severity == ScadaBuildValidationSeverity.Error), $"{site}: {Describe(result)}");
        }
    }

    [TestMethod]
    public void APlaceholderInATextBoundToATagIsAnError()
    {
        var result = Lower(
            "#1 := {FAN_03_RUN}\n#2 := {FAN_03_START}\n#3 := {FAN_03_SPEED}\n#4 := {FAN_03_RUN}",
            FanPageElements());

        var issue = Single(result, "parameter-sheet.text-not-literal");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, "#4");
        StringAssert.Contains(issue.Message, "ligne 4");
        StringAssert.Contains(issue.Message, $"page '{QuickWindowPageCode}'");
    }

    [TestMethod]
    public void AnUnknownTagIsAnError()
    {
        var result = Lower(
            "#1 := {FAN_03_RUN}\n#2 := {FAN_03_START}\n\n#3 := {FAN_99_SPEED}\n#4 := \"Ventilateur 3\"",
            FanPageElements());

        var issue = Single(result, "parameter-sheet.tag-unknown");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, "ligne 4");
        StringAssert.Contains(issue.Message, "#3");
        StringAssert.Contains(issue.Message, "FAN_99_SPEED");
    }

    [TestMethod]
    public void AnUnknownTagInsideAnExpressionIsAnError()
    {
        // Fix round 1: an unknown tag REFERENCED FROM INSIDE an otherwise-valid expression (not a
        // lone tag reference — that is the ParameterValueKind.Tag case covered by AnUnknownTagIsAnError
        // above) must be reported exactly like the lone-reference case, not silently exported unresolved.
        var result = Lower(
            "#1 := {FAN_03_RUN}\n#2 := {FAN_03_START}\n\n#3 := {FAN_99_SPEED} * 0.1\n#4 := \"Ventilateur 3\"",
            FanPageElements());

        var issue = Single(result, "parameter-sheet.tag-unknown");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, "ligne 4");
        StringAssert.Contains(issue.Message, "#3");
        StringAssert.Contains(issue.Message, "FAN_99_SPEED");
        Assert.IsNull(
            result.Project.EffectiveQuickWindowInvocations.SingleOrDefault()?.FindBinding(QuickWindowKeys.Member(QuickWindowPageKey, 3)),
            "an Expression binding that fails canonicalization must never reach the exported invocation");
    }

    [TestMethod]
    public void AnAmbiguousTagInsideAnExpressionIsAnError()
    {
        var result = Lower(
            "#1 := {FAN_03_RUN}\n#2 := {FAN_03_START}\n#3 := {DUPLICATE} * 0.1\n#4 := \"Ventilateur 3\"",
            FanPageElements());

        var issue = Single(result, "parameter-sheet.tag-ambiguous");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, "ligne 3");
        StringAssert.Contains(issue.Message, "#3");
        StringAssert.Contains(issue.Message, "tf100.mapping.201");
        StringAssert.Contains(issue.Message, "DUPLICATE");
    }

    [TestMethod]
    public void AnAmbiguousTagIsAnError()
    {
        var result = Lower(
            "#1 := {FAN_03_RUN}\n#2 := {FAN_03_START}\n#3 := {DUPLICATE}\n#4 := \"Ventilateur 3\"",
            FanPageElements());

        var issue = Single(result, "parameter-sheet.tag-ambiguous");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, "ligne 3");
        StringAssert.Contains(issue.Message, "#3");
        StringAssert.Contains(issue.Message, "tf100.mapping.201");
    }

    [TestMethod]
    public void ASheetSyntaxErrorIsAnErrorCarryingItsLine()
    {
        var result = Lower(FullSheet + "#5 = 12\n", FanPageElements());

        var issue = Single(result, "parameter-sheet.syntax");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.StartsWith(issue.Message, $"Feuille '{SheetName}', ligne 6 : ");
        Assert.AreEqual($"ParameterSheets[{SheetCode}]", issue.PropertyPath);
        Assert.AreEqual(SheetKey, issue.TargetKey);
    }

    [TestMethod]
    public void ADefinedButUnusedPlaceholderIsOnlyAWarning()
    {
        var result = Lower(FullSheet + "#7 := 1\n", FanPageElements());

        var issue = Single(result, "parameter-sheet.placeholder-unused");
        Assert.AreEqual(ScadaBuildValidationSeverity.Warning, issue.Severity);
        StringAssert.Contains(issue.Message, "#7");
        StringAssert.Contains(issue.Message, "ligne 6");
        StringAssert.Contains(issue.Message, $"Feuille '{SheetName}'");
        Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Severity == ScadaBuildValidationSeverity.Error), Describe(result));
        Assert.AreEqual(4, result.Project.EffectiveQuickWindowInvocations.Single().Bindings.Count, "an unused #N is not bound");
    }

    [TestMethod]
    public void AnInvalidTokenInATextIsAnError()
    {
        var result = Lower(FullSheet, [.. FanPageElements(), TextElement("pump", "Pompe #0")]);

        var issue = Single(result, "parameter-sheet.token-invalid");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, "« #0 »");
        StringAssert.Contains(issue.Message, "'pump'");
        Assert.AreEqual($"Scenes[{QuickWindowPageCode}].pump", issue.PropertyPath);
    }

    [TestMethod]
    public void HexColoursAreNeverPlaceholders()
    {
        var coloured = TextElement("hex", "#1B7A1B") with
        {
            Style = ScadaElementStyle.DefaultText with { Foreground = "#0F2A30", Background = "#123456", BorderColor = "#FFFFFF" },
            LegacyPayload = new LegacyElementPayload("Text", "#0F2A30", true, "Segoe UI", 12, "#0F2A30", "#1B7A1B", null, null)
        };
        var scene = ScadaScene.CreateEmpty(QuickWindowPageCode, "Popup", new CanvasSize(400, 200)) with { Elements = [coloured] };

        var (usages, invalid) = QuickWindowPlaceholderUsageCollector.Collect(scene);

        Assert.AreEqual(0, usages.Count, string.Join(", ", usages));
        Assert.AreEqual(0, invalid.Count, string.Join(", ", invalid));

        var result = Lower(string.Empty, [coloured]);
        Assert.AreEqual(0, result.Project.EffectiveQuickWindows.Single().EffectiveInterfaceMembers.Count);
        Assert.IsFalse(
            result.Diagnostics.Any(diagnostic => diagnostic.Code == "parameter-sheet.token-invalid"),
            Describe(result));
    }

    [TestMethod]
    public void EveryDisplayedTextTheExporterRendersIsATextSite()
    {
        // Final review I2: a displayed text is exactly what the exporter emits as a text node, fallbacks included.
        var table = ScadaElement.CreateTable("table", "Tableau", 0, 0, rows: 1, columns: 2);
        table = table with
        {
            Table = table.Table! with
            {
                Cells =
                [
                    new ScadaTableCell(0, 0, Content: new ScadaTableCellContent(Text: "Débit #5")),
                    new ScadaTableCell(0, 1)
                ]
            }
        };
        var fallbackText = ScadaElement.CreateText("name-fallback", "Nom #6", 0, 0);
        fallbackText = fallbackText with { Data = fallbackText.Data! with { Text = null } };
        var placeholderButton = ScadaElement.CreateButton("button-fallback", "Bouton", 0, 0);
        placeholderButton = placeholderButton with { Data = placeholderButton.Data! with { Text = null, Placeholder = "Démarrer #7" } };
        var readOnlyNumeric = ScadaElement.CreateInputNumeric("numeric-fallback", "Valeur", 0, 0, isReadOnly: true);
        readOnlyNumeric = readOnlyNumeric with { Data = readOnlyNumeric.Data! with { Value = null, DisplayFormat = null, Placeholder = "#8" } };
        var scene = ScadaScene.CreateEmpty("qw", "qw", new CanvasSize(400, 200)) with
        {
            Elements = [table, fallbackText, placeholderButton, readOnlyNumeric]
        };

        var (usages, invalid) = QuickWindowPlaceholderUsageCollector.Collect(scene);

        Assert.AreEqual(0, invalid.Count);
        CollectionAssert.AreEquivalent(
            new[]
            {
                new PlaceholderUsage(5, PlaceholderUsageSite.Text, "table", null),
                new PlaceholderUsage(6, PlaceholderUsageSite.Text, "name-fallback", null),
                new PlaceholderUsage(7, PlaceholderUsageSite.Text, "button-fallback", null),
                new PlaceholderUsage(8, PlaceholderUsageSite.Text, "numeric-fallback", null),
            },
            usages.ToArray());

        // Validated and substituted as a text: a table cell #N bound to a tag is refused like any text.
        var result = Lower(FullSheet + "#5 := {FAN_03_SPEED}\n", [.. FanPageElements(), table]);
        var issue = Single(result, "parameter-sheet.text-not-literal");
        StringAssert.Contains(issue.Message, "#5");
    }

    [TestMethod]
    public void TheMarkupOfACustomElementIsNeverScanned()
    {
        var custom = ScadaElement.CreateText("svg", "svg", 0, 0) with { Kind = ScadaElementKind.Custom };
        custom = custom with { Data = custom.Data! with { Text = "<svg><style>.a{fill:#333}</style><text>#4 #0</text></svg>" } };
        var scene = ScadaScene.CreateEmpty("qw", "qw", new CanvasSize(400, 200)) with { Elements = [custom] };

        var (usages, invalid) = QuickWindowPlaceholderUsageCollector.Collect(scene);

        Assert.AreEqual(0, usages.Count, string.Join(", ", usages));
        Assert.AreEqual(0, invalid.Count, string.Join(", ", invalid));
    }

    [TestMethod]
    public void ATextCarriedByBothDataAndLegacyPayloadIsReportedOnce()
    {
        var legacy = TextElement("legacy", "Pompe #0") with
        {
            LegacyPayload = new LegacyElementPayload("Text", "Pompe #0", true, "Segoe UI", 12, "#000000", "Transparent", null, null)
        };
        var legacyValid = TextElement("legacy-valid", "Ventilateur #4") with
        {
            LegacyPayload = new LegacyElementPayload("Text", "Ventilateur #4", true, "Segoe UI", 12, "#000000", "Transparent", null, null)
        };
        var scene = ScadaScene.CreateEmpty("qw", "qw", new CanvasSize(400, 200)) with { Elements = [legacy, legacyValid] };

        var (usages, invalid) = QuickWindowPlaceholderUsageCollector.Collect(scene);
        Assert.AreEqual(1, invalid.Count, string.Join(", ", invalid));
        Assert.AreEqual(1, usages.Count, string.Join(", ", usages));

        var result = Lower(FullSheet, [.. FanPageElements(), legacy]);
        Single(result, "parameter-sheet.token-invalid");
    }

    [TestMethod]
    public void APlaceholderInAFieldTheRuntimeNeverSubstitutesIsAnError()
    {
        // Final review I2: no mount-time substitution exists for these fields, so a #N there would reach the
        // operator raw. Fail closed, naming the element and the field.
        var effectText = ScadaEffectBlock.Empty with { TextContent = "Ventilateur #4" };
        var cases = new (ScadaElement Element, string Field)[]
        {
            (StateElement("state-text", "{#1} == 1") with
            {
                StateConfig = StateElement("state-text", "{#1} == 1").StateConfig! with
                {
                    States = [new ScadaStateRule("run-state", "En marche", true, ScadaExpression.FromSource("{#1} == 1"), effectText)]
                }
            }, "StateConfig.States[run-state].Effect.TextContent"),
            (ScadaElement.CreateText("default-text", "t", 0, 0) with
            {
                StateConfig = ScadaElementStateConfig.Default with { DefaultEffect = effectText }
            }, "StateConfig.DefaultEffect.TextContent"),
            (ScadaElement.CreateText("fallback-text", "t", 0, 0) with
            {
                StateConfig = ScadaElementStateConfig.Default with { QualityFallback = ScadaEffectBlock.Empty with { TextContent = "{#4}" } }
            }, "StateConfig.QualityFallback.TextContent"),
            (ScadaElement.CreateText("format", "t", 0, 0) with
            {
                StateConfig = ScadaElementStateConfig.Default with { ReadVariable = new ScadaReadVariableRule("#1", "0.0 #4") }
            }, "StateConfig.ReadVariable.DisplayFormat"),
            (ScadaElement.CreateButton("confirm", "Marche", 0, 0) with
            {
                CommandConfig = new ScadaElementCommandConfig(
                [
                    new ScadaCommandBinding("confirm-cmd", "Marche", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
                        Confirmation: new ScadaConfirmation("Démarrer #4 ?"), WriteTagId: "#2", WriteMode: ScadaWriteMode.Momentary)
                ])
            }, "CommandConfig.Commands[confirm-cmd].Confirmation.Message"),
            (ScadaElement.CreateInputText("input", "Saisie", 0, 0) with
            {
                Data = ScadaElement.CreateInputText("input", "Saisie", 0, 0).Data! with { Text = "#4" }
            }, "Data.Text"),
        };

        foreach (var (element, field) in cases)
        {
            var result = Lower(FullSheet, [.. FanPageElements(), element]);

            var issue = Single(result, "parameter-sheet.placeholder-unsupported-site");
            Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity, field);
            Assert.AreEqual(element.Id, issue.ElementId, field);
            StringAssert.Contains(issue.Message, $"'{element.Id}'", field);
            StringAssert.Contains(issue.Message, field, field);
            StringAssert.Contains(issue.Message, $"Page '{QuickWindowPageCode}'", field);
        }
    }

    [TestMethod]
    public void AnEscapedHashOnAPageWithoutAnyPlaceholderIsUnescapedInTheDefinition()
    {
        // Final review M4: the runtime substitutes (and unescapes \#) only in a definition with #N members.
        var table = ScadaElement.CreateTable("table", "Tableau", 0, 0, rows: 1, columns: 1);
        table = table with
        {
            Table = table.Table! with { Cells = [new ScadaTableCell(0, 0, Content: new ScadaTableCellContent(Text: @"Ligne \#3"))] }
        };
        var result = Lower(string.Empty, [TextElement("pump", @"Pompe \#2"), table]);

        Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Severity == ScadaBuildValidationSeverity.Error), Describe(result));
        var content = result.Project.EffectiveQuickWindows.Single().EffectiveContent.EffectiveElements;
        Assert.AreEqual("Pompe #2", content.Single(element => element.Id == "pump").Data!.Text);
        Assert.AreEqual("Ligne #3", content.Single(element => element.Id == "table").Table!.EffectiveCells.Single().EffectiveContent.Text);

        var withPlaceholders = Lower(FullSheet, FanPageElements());
        Assert.AreEqual(
            @"Pompe \#2",
            withPlaceholders.Project.EffectiveQuickWindows.Single().EffectiveContent.EffectiveElements.Single(element => element.Id == "escaped").Data!.Text,
            "a page with #N keeps its escapes: the runtime unescapes them while substituting");
    }

    [TestMethod]
    public void AnImportedQuickWindowPageWhoseSourceLayerIsNotReplacedIsAnError()
    {
        // Final review I6: a definition keeps only the scene's elements; an imported page's source HTML layer,
        // legacy actions and text overrides would be lost silently.
        var legacyStatic = new ScadaElement(
            "legacy-pump", "Pompe", ScadaElementKind.LegacyStatic, new SceneBounds(0, 0, 50, 20),
            new LegacySourceTrace("Legacy", "popup_fan", "pump-1", "Pompe", "imports/popup_fan.html"));
        var cases = new (string Name, bool Materialized, ScadaElement[] Extra, bool Expected)[]
        {
            ("not materialized", false, [], true),
            ("materialized, one legacy element left unconverted", true, [legacyStatic], true),
            ("materialized and fully converted", true, [], false),
        };

        foreach (var (name, materialized, extra, expected) in cases)
        {
            var (project, scenes) = Arrange(FullSheet, [.. FanPageElements(), .. extra]);
            var importedScene = scenes[QuickWindowPageKey] with { Origin = PageOrigin.Imported, LegacyElementsMaterialized = materialized };
            var importedProject = project with
            {
                Scenes = project.Scenes
                    .Select(page => page.PageKey == QuickWindowPageKey ? page with { Origin = PageOrigin.Imported } : page)
                    .ToArray()
            };
            var result = ParameterSheetLowering.Lower(
                importedProject,
                new Dictionary<Guid, ScadaScene>(scenes) { [QuickWindowPageKey] = importedScene });

            var issues = result.Diagnostics.Where(diagnostic => diagnostic.Code == "parameter-sheet.page-not-materialized").ToArray();
            Assert.AreEqual(expected ? 1 : 0, issues.Length, $"{name}: {Describe(result)}");
            if (expected)
            {
                Assert.AreEqual(ScadaBuildValidationSeverity.Error, issues[0].Severity, name);
                StringAssert.Contains(issues[0].Message, $"'{QuickWindowPageCode}'", name);
            }
        }

        var native = Lower(FullSheet, FanPageElements());
        Assert.IsFalse(native.Diagnostics.Any(diagnostic => diagnostic.Code == "parameter-sheet.page-not-materialized"), Describe(native));
    }

    [TestMethod]
    public void AValueTheRuntimeWouldRejectAsAnInjectionIsASheetError()
    {
        // Final review M1: mirror of quick-window-runtime.js _isInjection — refused at validation, not at mount.
        // #4 is a displayed text (a literal), #3 a state expression (an expression).
        var cases = new (int Number, string RightHandSide)[]
        {
            (4, "\"<script>alert(1)</script>\""),
            (4, "\"javascript:void(0)\""),
            (4, "\"../etc\""),
            (4, "\"# Ventilateur\""),
            (4, "\"onclick=go()\""),
            (4, "\"Consommation = 5\""),
            (3, "{FAN_03_SPEED} > 0 && \"x\" == \"onload=1\""),
        };

        foreach (var (number, rightHandSide) in cases)
        {
            var lines = new Dictionary<int, string>
            {
                [1] = "{FAN_03_RUN}", [2] = "{FAN_03_START}", [3] = "{FAN_03_SPEED} * 0.1", [4] = "\"Ventilateur 3\"", [number] = rightHandSide
            };
            var sheet = string.Join("\n", lines.OrderBy(pair => pair.Key).Select(pair => $"#{pair.Key} := {pair.Value}")) + "\n";
            var result = Lower(sheet, FanPageElements());

            Assert.IsFalse(result.Diagnostics.Any(diagnostic => diagnostic.Code == "parameter-sheet.syntax"), $"{rightHandSide}: {Describe(result)}");
            var issue = Single(result, "parameter-sheet.literal-rejected-by-runtime");
            Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity, rightHandSide);
            StringAssert.Contains(issue.Message, $"#{number}", rightHandSide);
            StringAssert.Contains(issue.Message, $"ligne {number}", rightHandSide);
            Assert.IsNull(
                result.Project.EffectiveQuickWindowInvocations.Single().FindBinding(QuickWindowKeys.Member(QuickWindowPageKey, number)),
                $"{rightHandSide}: a rejected value never reaches the exported invocation");
        }

        var clean = Lower(FullSheet, FanPageElements());
        Assert.IsFalse(clean.Diagnostics.Any(diagnostic => diagnostic.Code == "parameter-sheet.literal-rejected-by-runtime"), Describe(clean));
    }

    [TestMethod]
    public void TheBuilderInjectionMirrorCarriesExactlyTheRuntimePatterns()
    {
        var runtime = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "ScadaBuilderV2.Rendering", "Runtime", "quick-window-runtime.js"));
        var start = runtime.IndexOf("var INJECTION_PATTERNS = [", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "INJECTION_PATTERNS not found in quick-window-runtime.js");
        var end = runtime.IndexOf("];", start, StringComparison.Ordinal);
        var patterns = runtime[(start + "var INJECTION_PATTERNS = [".Length)..end]
            .Split('\n')
            .Select(line => line.Trim().TrimEnd(','))
            .Where(line => line.Length > 0)
            .ToArray();

        CollectionAssert.AreEqual(patterns, QuickWindowRuntimeInjectionScreen.Patterns.ToArray());
    }

    [TestMethod]
    public void TheCollectorReadsEveryBindingSiteAndOnlyThem()
    {
        var table = ScadaElement.CreateTable("table", "Tableau", 0, 0, rows: 1, columns: 1);
        table = table with
        {
            Table = table.Table! with
            {
                Cells = [new ScadaTableCell(0, 0, ValueBindings: new ScadaTableCellValueBindings("#11", "#12"))]
            }
        };
        var group = ScadaElement.CreateText("group", "Groupe", 0, 0) with
        {
            Children =
            [
                ReadElement("child-read", "#13") with
                {
                    Data = ReadElement("child-read", "#13").Data! with { TagBinding = "#14" },
                    StateConfig = ScadaElementStateConfig.Default with { ReadVariable = new ScadaReadVariableRule("#15") }
                }
            ]
        };
        var commands = ScadaElement.CreateButton("toggle", "Bascule", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("toggle-cmd", "Bascule", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
                    WriteTagId: "#16", ReadTagId: "#17", WriteMode: ScadaWriteMode.Toggle)
            ])
        };
        var scene = ScadaScene.CreateEmpty("qw", "qw", new CanvasSize(400, 200)) with
        {
            Elements = [table, group, commands, StateElement("state", "{#18} == 1 && {FAN_03_RUN} == 1"), WriteElement("write", "#19")]
        };

        var (usages, invalid) = QuickWindowPlaceholderUsageCollector.Collect(scene);

        Assert.AreEqual(0, invalid.Count);
        CollectionAssert.AreEquivalent(
            new[]
            {
                new PlaceholderUsage(11, PlaceholderUsageSite.ReadTag, "table", null),
                new PlaceholderUsage(12, PlaceholderUsageSite.WriteTag, "table", null),
                new PlaceholderUsage(13, PlaceholderUsageSite.ReadTag, "child-read", null),
                new PlaceholderUsage(14, PlaceholderUsageSite.ReadTag, "child-read", null),
                new PlaceholderUsage(15, PlaceholderUsageSite.ReadVariable, "child-read", null),
                new PlaceholderUsage(16, PlaceholderUsageSite.CommandWriteTag, "toggle", "toggle-cmd"),
                new PlaceholderUsage(17, PlaceholderUsageSite.CommandReadTag, "toggle", "toggle-cmd"),
                new PlaceholderUsage(18, PlaceholderUsageSite.StateExpression, "state", null),
                new PlaceholderUsage(19, PlaceholderUsageSite.WriteTag, "write", null),
            },
            usages.ToArray());
    }

    [TestMethod]
    public void ACommandPointingAtADeletedSheetIsAnErrorNotAnException()
    {
        var deletedSheet = Guid.Parse("5a000000-0000-4000-8000-0000000000de");
        var (project, scenes) = Arrange(FullSheet, FanPageElements(), OpenCommandElement(deletedSheet, QuickWindowPageKey));

        var result = ParameterSheetLowering.Lower(project, scenes);

        var issue = Single(result, "parameter-sheet.sheet-missing");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, $"'{OpenElementId}'");
        StringAssert.Contains(issue.Message, deletedSheet.ToString("D"));
        Assert.AreEqual($"Scenes[{CallerPageCode}].{OpenElementId}", issue.PropertyPath);
        Assert.AreEqual(0, result.Project.EffectiveQuickWindowInvocations.Count);
        Assert.IsNull(result.Scenes[CallerPageKey].Elements.Single(element => element.Id == OpenElementId)
            .EffectiveCommandConfig.Commands.Single().QuickWindowInvocationKey);
    }

    [TestMethod]
    public void ACommandPointingAtAPageThatIsNoLongerAQuickWindowIsAnError()
    {
        var (project, scenes) = Arrange(FullSheet, FanPageElements(), OpenCommandElement(SheetKey, CallerPageKey));

        var result = ParameterSheetLowering.Lower(project, scenes);

        var issue = Single(result, "parameter-sheet.page-not-quick-window");
        Assert.AreEqual(ScadaBuildValidationSeverity.Error, issue.Severity);
        StringAssert.Contains(issue.Message, $"'{CallerPageCode}'");
        StringAssert.Contains(issue.Message, $"'{OpenElementId}'");
        Assert.AreEqual(0, result.Project.EffectiveQuickWindowInvocations.Count);
    }

    [TestMethod]
    public void TheFragmentPageOfTheReferenceProjectHasNoPlaceholder()
    {
        var path = Path.Combine(FindRepoRoot(), "tests", "ScadaBuilderV2.Tests", "TestData", "QuickWindows", "popup_fan_condenseur.scene.json");
        var json = File.ReadAllText(path);
        var scene = JsonSerializer.Deserialize<ScadaScene>(json, JsonOptions)!;
        Assert.AreEqual("Popup_Fan_Condenseur", scene.Title);
        Assert.AreEqual(ScadaPageType.Fragment, scene.PageType);
        Assert.AreEqual(76, scene.Elements.Count, "every element of the user's page is loaded");
        Assert.IsTrue(scene.Elements.All(element => element.LegacySource is not null));
        Assert.IsTrue(
            System.Text.RegularExpressions.Regex.Matches(json, "\"#[0-9A-Fa-f]{6}\"").Count > 100,
            "the page is dense in hex colours, which is exactly what must not be read as #N");

        var (usages, invalid) = QuickWindowPlaceholderUsageCollector.Collect(scene);

        Assert.AreEqual(0, usages.Count, string.Join(", ", usages));
        Assert.AreEqual(0, invalid.Count, string.Join(", ", invalid));
    }

    [TestMethod]
    public void ExistingDefinitionsAreKeptAsIsAndAPreExistingInvocationStillReferencedByALegacyCommandIsKept()
    {
        var existing = QuickWindowDefinition.CreateEmpty("legacy_qw", "Ancienne");
        var existingInvocation = QuickWindowInvocation.CreateEmpty(existing.DefinitionKey);
        var (project, scenes) = Arrange(FullSheet, FanPageElements());
        project = project with { QuickWindows = [existing], QuickWindowInvocations = [existingInvocation] };
        scenes = WithLegacyCaller(scenes, CallerPageKey, existingInvocation.InvocationKey);

        var result = ParameterSheetLowering.Lower(project, scenes);

        Assert.AreSame(existing, result.Project.EffectiveQuickWindows[0]);
        Assert.AreSame(existingInvocation, result.Project.EffectiveQuickWindowInvocations.Single(inv => inv.InvocationKey == existingInvocation.InvocationKey));
        Assert.AreEqual(2, result.Project.EffectiveQuickWindows.Count);
        Assert.AreEqual(2, result.Project.EffectiveQuickWindowInvocations.Count);
    }

    [TestMethod]
    public void APreExistingInvocationReferencedByNoLiveCommandIsDroppedByLowering()
    {
        // Controller fix round 1: a legacy OpenQuickWindow command that migrated to a ParameterSheetKey +
        // QuickWindowPageKey pair, whose old invocation was never retired at authoring time, must not reach
        // the lowered project - it would otherwise validate unconditionally and pin a build/export error to
        // a command that looks correctly configured.
        var existing = QuickWindowDefinition.CreateEmpty("legacy_qw", "Ancienne");
        var orphanInvocation = QuickWindowInvocation.CreateEmpty(existing.DefinitionKey);
        var (project, scenes) = Arrange(FullSheet, FanPageElements());
        project = project with { QuickWindows = [existing], QuickWindowInvocations = [orphanInvocation] };

        var result = ParameterSheetLowering.Lower(project, scenes);

        Assert.IsFalse(
            result.Project.EffectiveQuickWindowInvocations.Any(inv => inv.InvocationKey == orphanInvocation.InvocationKey),
            "an invocation referenced by no live command must be dropped from the lowered project");
        Assert.AreEqual(1, result.Project.EffectiveQuickWindowInvocations.Count, "only the lowered (sheet, page) invocation remains");
        Assert.IsFalse(
            result.Diagnostics.Any(diagnostic => diagnostic.TargetKey == orphanInvocation.InvocationKey),
            "no diagnostic should ever be raised about an invocation lowering itself already dropped");
    }

    [TestMethod]
    public void APreExistingInvocationReferencedOnlyFromInsideAQuickWindowPagesContentIsKept()
    {
        // Fix round 2: OpenQuickWindow/CloseQuickWindow are offered on quick-window pages too. A command
        // living inside a QuickWindow page's own content becomes part of a definition's content by this
        // very lowering, so the liveness scan must include it - scanning resultScenes alone would miss it,
        // since resultScenes deliberately excludes every QuickWindow page.
        var invocation = new QuickWindowInvocation(Guid.NewGuid(), Guid.NewGuid(), []);
        var (project, scenes) = Arrange(FullSheet, FanPageElements());
        project = project with { QuickWindowInvocations = [invocation] };
        scenes = WithLegacyCaller(scenes, QuickWindowPageKey, invocation.InvocationKey);

        var result = ParameterSheetLowering.Lower(project, scenes);

        Assert.IsTrue(
            result.Project.EffectiveQuickWindowInvocations.Any(inv => inv.InvocationKey == invocation.InvocationKey),
            "an invocation referenced only from inside a QuickWindow page's own content must be kept");
    }

    [TestMethod]
    public void APreExistingInvocationReferencedOnlyFromInsideAPreExistingDefinitionsContentIsKept()
    {
        // Fix round 2: the same applies to a pre-existing DEC-0050 definition's own content (never lowered,
        // carried forward as-is) - its commands must also feed the liveness scan.
        var definitionKey = Guid.NewGuid();
        var invocation = new QuickWindowInvocation(Guid.NewGuid(), definitionKey, []);
        var legacyCallerInsideDefinition = ScadaElement.CreateButton("inner-open", "Ouvrir (interne)", 5, 5) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("inner-cmd", "Ouvrir (interne)", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    QuickWindowInvocationKey: invocation.InvocationKey)
            ])
        };
        var existingDefinition = QuickWindowDefinition.CreateEmpty("legacy_qw", "Ancienne") with
        {
            DefinitionKey = definitionKey,
            Content = new VisualContent(CanvasSize.DefaultDesktop, Elements: [legacyCallerInsideDefinition])
        };
        var (project, scenes) = Arrange(FullSheet, FanPageElements());
        project = project with { QuickWindows = [existingDefinition], QuickWindowInvocations = [invocation] };

        var result = ParameterSheetLowering.Lower(project, scenes);

        Assert.IsTrue(
            result.Project.EffectiveQuickWindowInvocations.Any(inv => inv.InvocationKey == invocation.InvocationKey),
            "an invocation referenced only from inside a pre-existing definition's content must be kept");
    }

    private static IReadOnlyDictionary<Guid, ScadaScene> WithLegacyCaller(
        IReadOnlyDictionary<Guid, ScadaScene> scenes, Guid pageKey, Guid invocationKey)
    {
        var legacyCaller = ScadaElement.CreateButton("legacy-open", "Ouvrir (ancien)", 5, 5) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("legacy-cmd", "Ouvrir (ancien)", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    QuickWindowInvocationKey: invocationKey)
            ])
        };
        var updated = new Dictionary<Guid, ScadaScene>(scenes)
        {
            [pageKey] = scenes[pageKey].WithElement(legacyCaller)
        };
        return updated;
    }

    // ---- Arrangement ------------------------------------------------------------------------------------

    private static ParameterSheetLoweringResult Lower(string sheetText, ScadaElement[] quickWindowElements)
    {
        var (project, scenes) = Arrange(sheetText, quickWindowElements);
        return ParameterSheetLowering.Lower(project, scenes);
    }

    private static (ScadaProject Project, IReadOnlyDictionary<Guid, ScadaScene> Scenes) Arrange(
        string sheetText,
        ScadaElement[] quickWindowElements,
        ScadaElement? openElement = null)
    {
        var (quickWindowRef, quickWindowScene) = Page(QuickWindowPageKey, ScadaPageType.QuickWindow, QuickWindowPageCode, "Popup ventilateur", quickWindowElements);
        var (callerRef, callerScene) = Page(CallerPageKey, ScadaPageType.Default, CallerPageCode, "Synoptique",
            openElement ?? OpenCommandElement(SheetKey, QuickWindowPageKey));

        var project = ScadaProject.CreateDefault("ParameterSheetLowering") with
        {
            ManifestVersion = "2.3",
            HomePageId = CallerPageCode,
            Scenes = [callerRef, quickWindowRef],
            TagCatalog = Catalog(),
            ParameterSheets = [new ParameterSheet(SheetKey, SheetCode, SheetName, sheetText)]
        };
        var scenes = new Dictionary<Guid, ScadaScene>
        {
            [CallerPageKey] = callerScene,
            [QuickWindowPageKey] = quickWindowScene
        };
        return (project, scenes);
    }

    private static (ScadaSceneReference Reference, ScadaScene Scene) Page(
        Guid key, ScadaPageType type, string code, string title, params ScadaElement[] elements)
    {
        var reference = new ScadaSceneReference(code, title, $"scenes/{code}.scene.json", type, PageKey: key, PageCode: code);
        var scene = ScadaScene.CreateEmpty(code, title, new CanvasSize(430, 215)) with
        {
            PageKey = key,
            PageCode = code,
            PageType = type,
            Elements = elements
        };
        return (reference, scene);
    }

    /// <summary>
    /// The "Popup ventilateur" page: a title (#4), a state (#1), a speed threshold state (#3, an expression in
    /// <see cref="FullSheet"/>, so it may only be used inside a state expression, never on a read site) and a
    /// start command (#2).
    /// </summary>
    private static ScadaElement[] FanPageElements() =>
    [
        TextElement("title", "Ventilateur #4"),
        StateElement("run", "{#1} == 1"),
        StateElement("speed", "{#3} > 50"),
        WriteCommandElement("start", "#2"),
        TextElement("escaped", @"Pompe \#2"),
    ];

    private static ScadaElement TextElement(string id, string text)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with { Data = element.Data! with { Text = text } };
    }

    private static ScadaElement ReadElement(string id, string tagId)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with { Data = element.Data! with { Text = string.Empty, ReadTagId = tagId } };
    }

    /// <summary>One element per read site the runtime substitutes only with a tag id, all using <paramref name="placeholder"/>.</summary>
    private static IEnumerable<(string Site, ScadaElement Element)> ReadSiteElements(string placeholder)
    {
        yield return ("Data.ReadTagId", ReadElement("read-tag", placeholder));
        var tagBinding = ScadaElement.CreateText("tag-binding", "tag-binding", 0, 0);
        yield return ("Data.TagBinding", tagBinding with { Data = tagBinding.Data! with { Text = string.Empty, TagBinding = placeholder } });
        yield return ("ReadVariable", ScadaElement.CreateText("read-variable", "read-variable", 0, 0) with
        {
            StateConfig = ScadaElementStateConfig.Default with { ReadVariable = new ScadaReadVariableRule(placeholder) }
        });
        yield return ("CommandReadTag", ScadaElement.CreateButton("read-back", "Relire", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding("read-back-cmd", "Relire", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
                    WriteTagId: "#2", ReadTagId: placeholder, WriteMode: ScadaWriteMode.Momentary)
            ])
        });
        var table = ScadaElement.CreateTable("table-read", "Tableau", 0, 0, rows: 1, columns: 1);
        yield return ("TableCellRead", table with
        {
            Table = table.Table! with { Cells = [new ScadaTableCell(0, 0, ValueBindings: new ScadaTableCellValueBindings(placeholder, null))] }
        });
    }

    private static ScadaElement WriteElement(string id, string tagId)
    {
        var element = ScadaElement.CreateText(id, id, 0, 0);
        return element with { Data = element.Data! with { Text = string.Empty, WriteTagId = tagId } };
    }

    private static ScadaElement WriteCommandElement(string id, string writeTagId) =>
        ScadaElement.CreateButton(id, "Marche", 0, 0) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding($"{id}-cmd", "Marche", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.WriteTag,
                    WriteTagId: writeTagId, WriteMode: ScadaWriteMode.Momentary)
            ])
        };

    private static ScadaElement StateElement(string id, string expressionSource) =>
        ScadaElement.CreateShape(id, id, ScadaShapeKind.Rectangle, 0, 0) with
        {
            StateConfig = ScadaElementStateConfig.Default with
            {
                States = [new ScadaStateRule($"{id}-state", "En marche", true, ScadaExpression.FromSource(expressionSource), ScadaEffectBlock.Empty)]
            }
        };

    private static ScadaElement OpenCommandElement(Guid sheetKey, Guid pageKey) =>
        ScadaElement.CreateButton(OpenElementId, "Ventilateur 3", 20, 20) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding(OpenCommandId, "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    ParameterSheetKey: sheetKey, QuickWindowPageKey: pageKey)
            ])
        };

    /// <summary>Same shape as <see cref="OpenCommandElement"/>, with caller-chosen element/command ids so two
    /// open commands (two sheets opening the same quick-window page) can coexist on one caller page.</summary>
    private static ScadaElement OpenCommandElementWithIds(string elementId, string commandId, Guid sheetKey, Guid pageKey) =>
        ScadaElement.CreateButton(elementId, "Ouvrir", 20, 20) with
        {
            CommandConfig = new ScadaElementCommandConfig(
            [
                new ScadaCommandBinding(commandId, "Ouvrir", true, ScadaCommandTrigger.OnClick, ScadaCommandKind.OpenQuickWindow,
                    ParameterSheetKey: sheetKey, QuickWindowPageKey: pageKey)
            ])
        };

    private static ScadaTagCatalog Catalog() => new(
        "scada-v2-parameter-sheet-lowering-tests",
        [
            new ScadaTagDefinition("tf100.mapping.101", "FAN_03_RUN", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.102", "FAN_03_START", Datatype: "Boolean", Writeable: true),
            new ScadaTagDefinition("tf100.mapping.103", "FAN_03_SPEED", Datatype: "Decimal"),
            new ScadaTagDefinition("tf100.mapping.201", "DUPLICATE", Datatype: "Boolean"),
            new ScadaTagDefinition("tf100.mapping.202", "DUPLICATE", Datatype: "Boolean"),
        ],
        "generated-parameter-sheet-lowering-tags.json");

    private static ScadaBuildValidationIssue Single(ParameterSheetLoweringResult result, string code)
    {
        var matches = result.Diagnostics.Where(diagnostic => diagnostic.Code == code).ToArray();
        Assert.AreEqual(1, matches.Length, $"expected exactly one '{code}': {Describe(result)}");
        return matches[0];
    }

    private static string Describe(ParameterSheetLoweringResult result) =>
        string.Join(" | ", result.Diagnostics.Select(diagnostic => $"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}"));

    private static string Serialize(ParameterSheetLoweringResult result) =>
        JsonSerializer.Serialize(result.Project, JsonOptions) +
        JsonSerializer.Serialize(result.Scenes.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToArray(), JsonOptions) +
        JsonSerializer.Serialize(result.Diagnostics, JsonOptions);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ScadaBuilderV2.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("ScadaBuilderV2.sln not found.");
    }
}
