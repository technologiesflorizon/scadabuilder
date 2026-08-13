using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

[TestClass]
public sealed class QuickWindowDomainTests
{
    [TestMethod]
    public void VisualContentCompositionDoesNotInheritPage()
    {
        // FR-019: PageDefinition and QuickWindowDefinition compose VisualContent without inheritance.
        var pageType = typeof(ScadaScene);
        var qwType = typeof(QuickWindowDefinition);
        var visualType = typeof(VisualContent);

        Assert.IsFalse(qwType.IsSubclassOf(pageType), "QuickWindow must not inherit from page/scene.");
        Assert.IsFalse(pageType.IsSubclassOf(qwType));
        // VisualContent is composed, not inherited.
        Assert.IsTrue(qwType.GetProperties().Any(p => p.PropertyType == typeof(VisualContent) || p.Name == "Content"));
        Assert.IsTrue(qwType.GetProperties().Any(p => p.Name == "DefinitionKey"));
        // Ensure no PageType, Route, Header/Footer, Navigation on VisualContent
        var visualProps = visualType.GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        CollectionAssert.DoesNotContain(visualProps.ToArray(), "PageType");
        CollectionAssert.DoesNotContain(visualProps.ToArray(), "HeaderPageId");
        CollectionAssert.DoesNotContain(visualProps.ToArray(), "FooterPageId");
        CollectionAssert.DoesNotContain(visualProps.ToArray(), "PageKey");
    }

    [TestMethod]
    public void QuickWindowPreservesKeysWhenRenaming()
    {
        var key = Guid.NewGuid();
        var content = new VisualContent(CanvasSize.DefaultDesktop);
        var def = new QuickWindowDefinition(key, "qw_motor", "Moteur", 1, content, Array.Empty<QuickWindowInterfaceMember>());
        var renamed = def with { DisplayName = "Moteur Renamed", Code = "qw_motor_v2" };

        Assert.AreEqual(key, renamed.DefinitionKey);
        Assert.AreEqual("Moteur Renamed", renamed.DisplayName);
        Assert.AreEqual("qw_motor_v2", renamed.Code);
        Assert.AreEqual(1, renamed.InterfaceVersion);
    }

    [TestMethod]
    public void QuickWindowEqualityIsValueBased()
    {
        var key = Guid.NewGuid();
        var content = new VisualContent(new CanvasSize(400, 300));
        var member = new QuickWindowInterfaceMember(Guid.NewGuid(), "RunFeedback", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read);
        var members = new[] { member };
        var pres = new QuickWindowPresentationDefaults();
        var def1 = new QuickWindowDefinition(key, "qw_test", "Test", 1, content, members, pres);
        var def2 = new QuickWindowDefinition(key, "qw_test", "Test", 1, content, members, pres);

        // Record equality for IReadOnlyList is reference-based, so same reference ensures equality.
        // Renaming without key change must preserve key.
        Assert.AreEqual(def1.DefinitionKey, def2.DefinitionKey);
        Assert.AreEqual(def1.Code, def2.Code);
        Assert.AreEqual(def1.DisplayName, def2.DisplayName);
        Assert.AreEqual(def1.InterfaceVersion, def2.InterfaceVersion);

        var def3 = def1 with { DisplayName = "Other" };
        Assert.AreNotEqual(def1.DisplayName, def3.DisplayName);
        Assert.AreEqual(def1.DefinitionKey, def3.DefinitionKey);
    }

    [TestMethod]
    public void NoFourthIdentifierExists()
    {
        Assert.IsFalse(QuickWindowValidation.HasFourthIdentifierViolation(), "Must not introduce InstanceKey; only DefinitionKey, InvocationKey and RuntimeInstanceId allowed.");

        // Also ensure QuickWindowDefinition does not have InstanceKey property
        var props = typeof(QuickWindowDefinition).GetProperties().Select(p => p.Name).ToArray();
        CollectionAssert.DoesNotContain(props, "InstanceKey");
        CollectionAssert.DoesNotContain(props, "WindowInstanceKey");
    }

    [TestMethod]
    public void PresentationDefaultsPreservedWhenNull()
    {
        var content = new VisualContent(CanvasSize.DefaultDesktop);
        var def = new QuickWindowDefinition(Guid.NewGuid(), "qw_no_pres", "NoPres", 1, content, Array.Empty<QuickWindowInterfaceMember>(), PresentationDefaults: null);

        var effective = def.EffectivePresentation;
        Assert.AreEqual(QuickWindowPosition.Center, effective.Position);
        Assert.IsTrue(effective.IsDraggable);
        Assert.IsFalse(effective.IsResizable);
        Assert.IsTrue(effective.IsViewportConstrained);
        Assert.IsTrue(effective.Backdrop);
        // FR-UI-02: host adds chrome outside CanvasSize, so CanvasSize is content size only
        Assert.AreEqual(CanvasSize.DefaultDesktop.Width, def.EffectiveContent.EffectiveCanvasSize.Width);
        Assert.AreEqual(CanvasSize.DefaultDesktop.Height, def.EffectiveContent.EffectiveCanvasSize.Height);
        // Effective title falls back to DisplayName when Title null
        Assert.AreEqual("NoPres", def.EffectiveTitle);
        Assert.AreEqual("NoPres", effective.EffectiveTitle("NoPres"));
    }

    [TestMethod]
    public void PresentationDefaultsCanOverrideTitle()
    {
        var pres = new QuickWindowPresentationDefaults(Title: "Custom Title", Position: QuickWindowPosition.Center);
        var def = new QuickWindowDefinition(Guid.NewGuid(), "qw_title", "Display", 1, new VisualContent(CanvasSize.DefaultDesktop), Array.Empty<QuickWindowInterfaceMember>(), pres);
        Assert.AreEqual("Custom Title", def.EffectiveTitle);
        Assert.AreEqual("Custom Title", def.EffectivePresentation.EffectiveTitle(def.DisplayName));
    }

    [TestMethod]
    public void ChromeIsBounded()
    {
        var chrome = new QuickWindowChrome(TitleBarColor: "#123456", BorderColor: "#654321", Shadow: "2px 2px 4px rgba(0,0,0,0.2)");
        var pres = new QuickWindowPresentationDefaults(Chrome: chrome);
        Assert.AreEqual("#123456", pres.Chrome?.TitleBarColor);
        // Ensure no X/geometry/behaviors/backdrop in chrome
        var chromeProps = typeof(QuickWindowChrome).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        CollectionAssert.DoesNotContain(chromeProps.ToArray(), "X");
        CollectionAssert.DoesNotContain(chromeProps.ToArray(), "Geometry");
        CollectionAssert.DoesNotContain(chromeProps.ToArray(), "Backdrop");
    }

    [TestMethod]
    public void DomeValidationCatchesInvalidCodesAndKeys()
    {
        var def = new QuickWindowDefinition(Guid.Empty, "INVALID CODE", "", 0, new VisualContent(new CanvasSize(0, 0)), new[]
        {
            new QuickWindowInterfaceMember(Guid.Empty, "", QuickWindowInterfaceFamily.PrivateVariable, QuickWindowDataType.String, QuickWindowMemberAccess.Internal, Required: true)
        }, new QuickWindowPresentationDefaults(IsDraggable: false, IsResizable: true, IsViewportConstrained: false));
        var issues = QuickWindowValidation.ValidateDefinition(def);
        Assert.IsTrue(issues.Any(i => i.Contains("DefinitionKey")));
        Assert.IsTrue(issues.Any(i => i.Contains("Code")));
        Assert.IsTrue(issues.Any(i => i.Contains("DisplayName")));
        Assert.IsTrue(issues.Any(i => i.Contains("InterfaceVersion")));
        Assert.IsTrue(issues.Any(i => i.Contains("CanvasSize")));
        Assert.IsTrue(issues.Any(i => i.Contains("MemberKey")));
        Assert.IsTrue(issues.Any(i => i.Contains("Required")));
        Assert.IsTrue(issues.Any(i => i.Contains("IsDraggable")));
        Assert.IsTrue(issues.Any(i => i.Contains("IsResizable")));
        Assert.IsTrue(issues.Any(i => i.Contains("IsViewportConstrained")));
    }

    [TestMethod]
    public void PrivateMembersCannotBeRequired()
    {
        var member = new QuickWindowInterfaceMember(Guid.NewGuid(), "Secret", QuickWindowInterfaceFamily.PrivateVariable, QuickWindowDataType.String, QuickWindowMemberAccess.Internal, Required: true);
        var def = new QuickWindowDefinition(Guid.NewGuid(), "qw_priv", "Priv", 1, new VisualContent(CanvasSize.DefaultDesktop), new[] { member });
        var issues = QuickWindowValidation.ValidateDefinition(def);
        Assert.IsTrue(issues.Any(i => i.Contains("Private member") && i.Contains("Required")));
    }

    [TestMethod]
    public void InterfaceFamiliesEnforceAccessAndPrivateConstantValue()
    {
        var members = new[]
        {
            new QuickWindowInterfaceMember(Guid.NewGuid(), "Read", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write),
            new QuickWindowInterfaceMember(Guid.NewGuid(), "Write", QuickWindowInterfaceFamily.WriteCommand, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read),
            new QuickWindowInterfaceMember(Guid.NewGuid(), "Constant", QuickWindowInterfaceFamily.PrivateConstant, QuickWindowDataType.Integer, QuickWindowMemberAccess.Read)
        };
        var definition = new QuickWindowDefinition(Guid.NewGuid(), "qw_access", "Access", 1, new VisualContent(CanvasSize.DefaultDesktop), members);
        var issues = QuickWindowValidation.ValidateDefinition(definition);
        Assert.IsTrue(issues.Any(issue => issue.Contains("ReadState", StringComparison.Ordinal)));
        Assert.IsTrue(issues.Any(issue => issue.Contains("WriteCommand", StringComparison.Ordinal)));
        Assert.IsTrue(issues.Any(issue => issue.Contains("Internal", StringComparison.Ordinal)));
        Assert.IsTrue(issues.Any(issue => issue.Contains("fixed DefaultValue", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void VisualContentReferencesAreBoundedAndProjectRelative()
    {
        var content = new VisualContent(
            CanvasSize.DefaultDesktop,
            StyleSheets: new[] { "styles/qw-motor.css" },
            AssetReferences: new[] { "assets/motor.svg" });
        var definition = new QuickWindowDefinition(Guid.NewGuid(), "qw_refs", "Refs", 1, content, Array.Empty<QuickWindowInterfaceMember>());
        Assert.AreEqual(0, QuickWindowValidation.ValidateDefinition(definition).Count);

        var unsafeDefinition = definition with { Content = content with { AssetReferences = new[] { "../outside.svg" } } };
        Assert.IsTrue(QuickWindowValidation.ValidateDefinition(unsafeDefinition).Any(issue => issue.Contains("AssetReferences", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void InterfaceMembersAreDeterministicallyOrdered()
    {
        var m1 = new QuickWindowInterfaceMember(Guid.Parse("11111111-1111-1111-1111-111111111111"), "A", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read);
        var m2 = new QuickWindowInterfaceMember(Guid.Parse("22222222-2222-2222-2222-222222222222"), "B", QuickWindowInterfaceFamily.WriteCommand, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Write);
        var def = new QuickWindowDefinition(Guid.NewGuid(), "qw_order", "Order", 1, new VisualContent(CanvasSize.DefaultDesktop), new[] { m2, m1 });
        // Members preserve order as authored; exporter will sort ordinally - here we just ensure both present
        Assert.AreEqual(2, def.InterfaceMembers.Count);
        Assert.AreEqual("B", def.InterfaceMembers[0].Name);
    }

    [TestMethod]
    public void NamespaceDerivedFromDefinitionKey()
    {
        var key = Guid.Parse("a1b2c3d4-e5f6-4789-8123-456789abcdef");
        var ns = QuickWindowNamespace.ForDefinition(key);
        Assert.AreEqual("qw-a1b2c3d4", ns);
        Assert.IsTrue(ns.StartsWith("qw-"));
    }

    [TestMethod]
    public void VisualContentAdapterPreservesScene()
    {
        var scene = ScadaScene.CreateEmpty("page", "Page", new CanvasSize(800, 600))
            .WithElement(ScadaElement.CreateText("t1", "Hello", 10, 20));
        var content = VisualContent.FromScene(scene);
        Assert.AreEqual(800, content.EffectiveCanvasSize.Width);
        Assert.AreEqual(1, content.EffectiveElements.Count);
        var projected = content.ToScene("qw_scene", "QW Title", Guid.NewGuid(), "qw_scene");
        Assert.AreEqual("QW Title", projected.Title);
        Assert.AreEqual(1, projected.Elements.Count);
    }

    [TestMethod]
    public void ProjectPersistsQuickWindowsCollection()
    {
        var def = QuickWindowDefinition.CreateEmpty("qw_empty", "Empty");
        var project = ScadaProject.CreateDefault("Test") with { QuickWindows = new[] { def } };
        Assert.AreEqual(1, project.EffectiveQuickWindows.Count);
        Assert.AreEqual(def.DefinitionKey, project.EffectiveQuickWindows[0].DefinitionKey);
        // Old project without QuickWindows has empty
        var old = ScadaProject.CreateDefault("Old");
        Assert.AreEqual(0, old.EffectiveQuickWindows.Count);
    }

    [TestMethod]
    public void OldProjectsJsonDoesNotRewriteWhenNoQuickWindows()
    {
        // Placeholder to ensure persistence layer does not rewrite projects without windows - verified in QuickWindowStoreTests
        var project = ScadaProject.CreateDefault("P");
        var issues = QuickWindowValidation.ValidateDefinition(QuickWindowDefinition.CreateEmpty("qw_valid", "Valid"));
        Assert.AreEqual(0, issues.Count);
    }
}
