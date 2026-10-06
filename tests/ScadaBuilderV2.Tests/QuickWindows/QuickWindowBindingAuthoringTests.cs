using ScadaBuilderV2.App.QuickWindows;
using ScadaBuilderV2.Application.Commands;
using ScadaBuilderV2.Application.Pages;
using ScadaBuilderV2.Application.QuickWindows;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.QuickWindows;

/// <summary>
/// Locks the quick-window caller authoring context: `OpenQuickWindow` with a definition target and no page
/// target, `CloseQuickWindow(Self)` only inside a quick-window content, and the absence of the `Liaisons`
/// tab. The DEC-0050 typed bindings editor (grid, source selectors, statuses, save/remove/delete-caller
/// chain) was decommissioned with its tests on 2026-10-06: DEC-0052 replaced it and nothing hosted it any more.
/// Decisions: DEC-0050 (décommissionné), DEC-0052. Plan: Task 3.3.
/// </summary>
[TestClass]
public sealed class QuickWindowBindingAuthoringTests
{
    private static readonly Guid DefinitionKey = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RunningKey = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SetpointKey = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid PrivateKey = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [TestMethod]
    public void APageOpensADefinitionAndNeverClosesAQuickWindow()
    {
        var page = new QuickWindowCommandAuthoringContext([Definition()], []);

        CollectionAssert.AreEqual(new[] { ScadaCommandKind.OpenQuickWindow }, page.AllowedCommandKinds().ToArray());
        Assert.IsTrue(page.CanOpenQuickWindow);
        Assert.IsFalse(page.CanCloseQuickWindow, "a page can never close a quick window");
        Assert.AreEqual(0, page.EffectiveParentPorts.Count, "a page has no parent port to forward");
        Assert.AreEqual(0, QuickWindowCommandAuthoringContext.Empty.AllowedCommandKinds().Count);
    }

    [TestMethod]
    public void AQuickWindowContentOffersCloseSelfWithoutAnyFreeTarget()
    {
        var content = new QuickWindowCommandAuthoringContext([Definition()], Definition().EffectiveInterfaceMembers, IsQuickWindowContent: true);

        CollectionAssert.AreEqual(
            new[] { ScadaCommandKind.OpenQuickWindow, ScadaCommandKind.CloseQuickWindow },
            content.AllowedCommandKinds().ToArray());
        Assert.AreEqual(2, content.EffectiveParentPorts.Count, "only public ports may be forwarded");
        Assert.IsFalse(content.EffectiveParentPorts.Any(port => port.MemberKey == PrivateKey));

        var xaml = ReadAppFile("ElementCommandDialog.xaml");
        StringAssert.Contains(xaml, "x:Name=\"CloseQuickWindowPanel\"");
        StringAssert.Contains(xaml, "x:Name=\"QuickWindowConfigureButton\"");

        var source = ReadAppFile("ElementCommandDialog.xaml.cs");
        StringAssert.Contains(source, "CloseQuickWindowPanel.Visibility = kind == ScadaCommandKind.CloseQuickWindow");
        StringAssert.Contains(
            source,
            "TargetPageId: kind == ScadaCommandKind.Navigate",
            "a quick-window command must never carry a page target");
        Assert.IsFalse(
            source.Contains("ToggleQuickWindow", StringComparison.Ordinal),
            "no Toggle quick-window kind exists");
    }

    [TestMethod]
    public void TheLiaisonsTabNoLongerExistsOnAuthoring()
    {
        // DEC-0052 replaces the `Liaisons` tab and its typed bindings grid with the "Configuration de la
        // fenêtre rapide" sub-dialog (sheet + page pair) opened from `ElementCommandDialog` itself; the tab
        // never appears in the properties surface any more.
        var xaml = ReadAppFile("ElementPropertiesDialog.xaml");
        Assert.IsFalse(xaml.Contains("Header=\"Liaisons\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"QuickWindowBindingsTab\"", StringComparison.Ordinal));

        var source = ReadAppFile("ElementPropertiesDialog.xaml.cs");
        Assert.IsFalse(source.Contains("QuickWindowBindingsEditorControl", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("EnsureQuickWindowInvocationAsync", StringComparison.Ordinal));
    }

    private static QuickWindowDefinition Definition() =>
        new(
            DefinitionKey,
            "motor",
            "Motor",
            1,
            new VisualContent(new CanvasSize(480, 320), Elements: []),
            [
                new QuickWindowInterfaceMember(RunningKey, "Running", QuickWindowInterfaceFamily.ReadState, QuickWindowDataType.Boolean, QuickWindowMemberAccess.Read, Required: true),
                new QuickWindowInterfaceMember(SetpointKey, "Setpoint", QuickWindowInterfaceFamily.PublicParameter, QuickWindowDataType.Decimal, QuickWindowMemberAccess.Read),
                new QuickWindowInterfaceMember(PrivateKey, "Counter", QuickWindowInterfaceFamily.PrivateVariable, QuickWindowDataType.Integer, QuickWindowMemberAccess.Internal)
            ],
            new QuickWindowPresentationDefaults());

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
