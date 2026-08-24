using System.Reflection;
using System.Text.RegularExpressions;
using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.ElementEvents.Expressions;
using ScadaBuilderV2.Domain.ElementEvents.State;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.RuntimeContracts;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests.RuntimeContracts;

[TestClass]
public sealed class ScadaRuntimeCapabilityCatalogTests
{
    [TestMethod]
    public void CatalogMapsEveryPersistentEnumValue()
    {
        AssertComplete<ScadaPageType>(ScadaRuntimeCapabilityCatalog.PageTypes.Keys);
        AssertComplete<ScadaElementKind>(ScadaRuntimeCapabilityCatalog.ElementKinds.Keys);
        AssertComplete<ScadaShapeKind>(ScadaRuntimeCapabilityCatalog.ShapeKinds.Keys);
        AssertComplete<ScadaButtonKind>(ScadaRuntimeCapabilityCatalog.ButtonKinds.Keys);
        AssertComplete<ScadaTableCellContentKind>(ScadaRuntimeCapabilityCatalog.TableCellKinds.Keys);
        AssertComplete<ScadaActionKind>(ScadaRuntimeCapabilityCatalog.ActionKinds.Keys);
        AssertComplete<ScadaConditionOperator>(ScadaRuntimeCapabilityCatalog.ConditionOperators.Keys);
        AssertComplete<ScadaConditionGroupMode>(ScadaRuntimeCapabilityCatalog.ConditionGroupModes.Keys);
        AssertComplete<ScadaMissingConditionPolicy>(ScadaRuntimeCapabilityCatalog.MissingConditionPolicies.Keys);
        AssertComplete<ScadaPopupPosition>(ScadaRuntimeCapabilityCatalog.PopupPositions.Keys);
        AssertComplete<ScadaPopupSizePreset>(ScadaRuntimeCapabilityCatalog.PopupSizes.Keys);
        AssertComplete<ScadaCommandTrigger>(ScadaRuntimeCapabilityCatalog.CommandTriggers.Keys);
        AssertComplete<ScadaCommandKind>(ScadaRuntimeCapabilityCatalog.CommandKinds.Keys);
        AssertComplete<ScadaWriteMode>(ScadaRuntimeCapabilityCatalog.WriteModes.Keys);
        AssertComplete<ScadaExprUnaryOp>(ScadaRuntimeCapabilityCatalog.UnaryOperators.Keys);
        AssertComplete<ScadaExprBinaryOp>(ScadaRuntimeCapabilityCatalog.BinaryOperators.Keys);
        AssertComplete<ScadaAnimation>(ScadaRuntimeCapabilityCatalog.Animations.Keys);
    }

    [TestMethod]
    public void RuntimeEnumSurfaceFingerprintForcesDeliberateContractUpdates()
    {
        var fingerprint = string.Join(";", new[]
        {
            Surface<ScadaShapeKind>(),
            Surface<ScadaConditionOperator>(),
            Surface<ScadaConditionGroupMode>(),
            Surface<ScadaMissingConditionPolicy>(),
            Surface<ScadaPopupPosition>(),
            Surface<ScadaPopupSizePreset>(),
            Surface<ScadaCommandTrigger>(),
            Surface<ScadaExprUnaryOp>(),
            Surface<ScadaExprBinaryOp>(),
            Surface<ScadaAnimation>()
        });

        Assert.AreEqual(
            "ScadaShapeKind=Rectangle,RoundedRectangle,Ellipse,Line,Arrow,Circle,Triangle,Star,IndicatorLamp,HorizontalBar,VerticalBar,Tank,PipeHorizontal,PipeVertical,Valve,Pump,Motor,Fan,Conveyor,Gauge,Switch,Breaker,Transformer,AlarmBeacon;" +
            "ScadaConditionOperator=Equals,NotEquals,GreaterThan,GreaterThanOrEqual,LessThan,LessThanOrEqual,True,False;" +
            "ScadaConditionGroupMode=All,Any;" +
            "ScadaMissingConditionPolicy=BlockAction,AllowAction;" +
            "ScadaPopupPosition=Center,TopLeft,TopRight,BottomLeft,BottomRight,DockLeft,DockRight,DockTop,DockBottom,HostRegion;" +
            "ScadaPopupSizePreset=Small,Medium,Large,Fullscreen;" +
            "ScadaCommandTrigger=OnClick,OnRelease,OnHover,OnHoverEnter,OnHoverExit;" +
            "ScadaExprUnaryOp=Not,Negate;" +
            "ScadaExprBinaryOp=Add,Subtract,Multiply,Divide,Modulo,Equal,NotEqual,LessThan,LessThanOrEqual,GreaterThan,GreaterThanOrEqual,And,Or;" +
            "ScadaAnimation=None,Blink,Pulse,Halo,Spin",
            fingerprint,
            "An exportable enum changed. Add/update its stable capability id and conformance fixture deliberately.");
    }

    [TestMethod]
    public void CatalogMapsEveryEffectFieldAndExpressionNodeType()
    {
        var effectProperties = typeof(ScadaEffectBlock)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var mappedEffectProperties = ScadaRuntimeCapabilityCatalog.EffectProperties.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(effectProperties, mappedEffectProperties);

        var nodeTypes = typeof(ScadaExprNode).Assembly.GetTypes()
            .Where(type => type.IsSealed && type.IsSubclassOf(typeof(ScadaExprNode)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        var mappedNodeTypes = ScadaRuntimeCapabilityCatalog.ExpressionNodeTypes.Keys
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(nodeTypes, mappedNodeTypes);
    }

    [TestMethod]
    public void CapabilityIdsAreUniqueStableAndOwnedOnce()
    {
        var all = ScadaRuntimeCapabilityCatalog.All;

        Assert.AreEqual(all.Count, all.Select(capability => capability.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.IsTrue(all.Count > 100, "The general contract should inventory the complete runtime surface, not a page-specific subset.");
        Assert.IsTrue(all.All(capability => Regex.IsMatch(capability.Id, "^[a-z0-9]+(?:[.-][a-z0-9]+)*$")));
        Assert.IsTrue(all.All(capability => capability.MinimumContractVersion == ScadaRuntimeCapabilityCatalog.ContractVersion));
        Assert.IsTrue(all.All(capability => capability.Artifacts.Count > 0));
        Assert.IsTrue(all.All(capability => capability.FixtureId ==
            $"{(capability.Status == ScadaRuntimeCapabilityStatus.Supported ? "conformance" : "blocked")}:{capability.Id}"));
    }

    [TestMethod]
    public void StrictSupportRequiresCompleteBuilderRuntimeAndTf100WebEvidence()
    {
        Assert.IsTrue(ScadaRuntimeCapabilityCatalog.All
            .Where(capability => capability.Status == ScadaRuntimeCapabilityStatus.Supported)
            .All(capability => capability.Evidence.IsComplete));

        Assert.AreEqual(ScadaRuntimeCapabilityStatus.Supported, ScadaRuntimeCapabilityCatalog.ActionKinds[ScadaActionKind.Navigate].Status);
        Assert.AreEqual(ScadaRuntimeCapabilityStatus.Blocked, ScadaRuntimeCapabilityCatalog.ActionKinds[ScadaActionKind.Show].Status);
        Assert.AreEqual(ScadaRuntimeCapabilityStatus.Blocked, ScadaRuntimeCapabilityCatalog.WriteModes[ScadaWriteMode.Momentary].Status);
        Assert.IsTrue(ScadaRuntimeCapabilityCatalog.Animations.Values.All(value => value.Status == ScadaRuntimeCapabilityStatus.Blocked));
        Assert.IsTrue(ScadaRuntimeCapabilityCatalog.All
            .Where(value => value.Status == ScadaRuntimeCapabilityStatus.Blocked)
            .All(value => !value.Evidence.IsComplete));
    }

    private static void AssertComplete<T>(IEnumerable<T> mapped) where T : struct, Enum
    {
        CollectionAssert.AreEquivalent(Enum.GetValues<T>(), mapped.ToArray(), $"Every {typeof(T).Name} value must have one capability mapping.");
    }

    private static string Surface<T>() where T : struct, Enum =>
        $"{typeof(T).Name}={string.Join(',', Enum.GetNames<T>())}";

    [TestMethod]
    public void EveryApprovedQuickWindowCapabilityIsRegisteredBlockedWithNoUmbrellaId()
    {
        string[] approved =
        [
            "command.close-quick-window",
            "command.open-quick-window",
            "quick-window.binding.parent-port",
            "quick-window.definition",
            "quick-window.dom.scoped-root",
            "quick-window.instance.single-per-definition",
            "quick-window.legacy-fragment-adapter",
            "quick-window.lifecycle.host-owned",
            "quick-window.local-interface.typed",
            "quick-window.nesting.depth-2",
            "quick-window.port-binding",
            "quick-window.port.required",
            "quick-window.presentation.backdrop"
        ];

        var registered = ScadaRuntimeCapabilityCatalog.All
            .Where(capability =>
                capability.Id.StartsWith("quick-window.", StringComparison.Ordinal) ||
                capability.Id.EndsWith("-quick-window", StringComparison.Ordinal))
            .ToArray();

        CollectionAssert.AreEqual(
            approved,
            registered.Select(capability => capability.Id).ToArray(),
            "The catalog must register exactly the capabilities approved by the specification, no more and no less.");
        Assert.IsTrue(
            registered.All(capability => capability.Status == ScadaRuntimeCapabilityStatus.Blocked),
            "Every quick-window capability starts Blocked until its own Phase 6 promotion.");
        Assert.IsTrue(
            registered.All(capability => capability.FixtureId == $"blocked:{capability.Id}"),
            "A blocked capability carries its own fixture id, never a shared one.");
    }

    [TestMethod]
    public void NoUmbrellaOrRetiredQuickWindowIdentifierExists()
    {
        var ids = ScadaRuntimeCapabilityCatalog.All.Select(capability => capability.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var forbidden in new[]
                 {
                     "quick-window.v1",
                     "quick-window",
                     "command.toggle-quick-window",
                     "quick-window.all"
                 })
        {
            Assert.IsFalse(ids.Contains(forbidden), $"'{forbidden}' must never exist as a capability id.");
        }
    }

    [TestMethod]
    public void QuickWindowCapabilitiesDeclareTheirOwnPackageArtifacts()
    {
        foreach (var capability in ScadaRuntimeCapabilityCatalog.All
                     .Where(item => item.Id.StartsWith("quick-window.", StringComparison.Ordinal)))
        {
            CollectionAssert.Contains(capability.Artifacts.ToArray(), "manifest.json:QuickWindows");
            CollectionAssert.Contains(capability.Artifacts.ToArray(), "manifest.json:QuickWindowInvocations");
            CollectionAssert.Contains(capability.Artifacts.ToArray(), "qw-<key8>/qw-<key8>.html");
        }
    }
}
