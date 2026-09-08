using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks chain composition. A converter is written one step at a time and the registry composes them, so an
/// incomplete or ambiguous chain must be a refusal before any write, never a partial conversion.
/// </summary>
[TestClass]
public sealed class ArtifactConverterRegistryTests
{
    private sealed class Step(ArtifactModule module, int from, int to) : IArtifactConverter
    {
        public ArtifactModule Module => module;
        public int FromVersion => from;
        public int ToVersion => to;
        public string StepDescription => $"{module} {from} vers {to}";
        public JsonNode Convert(JsonNode document)
        {
            document["Steps"] = (document["Steps"]?.GetValue<string>() ?? "") + $"[{from}->{to}]";
            return document;
        }
    }

    [TestMethod]
    public void ACompleteChainIsResolvedInOrder()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 1, 2));
        registry.Register(new Step(ArtifactModule.Project, 0, 1));

        var chain = registry.ResolveChain(ArtifactModule.Project, 0, 2);

        Assert.IsTrue(chain.IsComplete);
        CollectionAssert.AreEqual(
            new[] { 0, 1 },
            chain.Steps.Select(step => step.FromVersion).ToArray(),
            "the chain must run in ascending order regardless of registration order.");
    }

    [TestMethod]
    public void AChainWithAGapIsRefusedAndNamesTheMissingStep()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 1));
        // 1 -> 2 is missing.
        registry.Register(new Step(ArtifactModule.Project, 2, 3));

        var chain = registry.ResolveChain(ArtifactModule.Project, 0, 3);

        Assert.IsFalse(chain.IsComplete);
        Assert.AreEqual(1, chain.MissingFromVersion);
    }

    [TestMethod]
    public void AConverterMaySpanSeveralStepsWhenNoOtherClaimsThem()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 3));

        var chain = registry.ResolveChain(ArtifactModule.Project, 0, 3);

        Assert.IsTrue(chain.IsComplete);
        Assert.AreEqual(1, chain.Steps.Count);
    }

    /// <summary>Two converters claiming the same step is a registration error, not a runtime coin toss.</summary>
    [TestMethod]
    public void TwoConvertersClaimingTheSameStepIsRejectedAtRegistration()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 1));

        var thrown = Assert.ThrowsException<InvalidOperationException>(
            () => registry.Register(new Step(ArtifactModule.Project, 0, 2)));

        StringAssert.Contains(thrown.Message, "Project");
        StringAssert.Contains(thrown.Message, "0");
    }

    [TestMethod]
    public void ModulesDoNotInterfereWithEachOther()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 1));
        registry.Register(new Step(ArtifactModule.Scene, 0, 1));

        Assert.IsTrue(registry.ResolveChain(ArtifactModule.Scene, 0, 1).IsComplete);
        Assert.AreEqual(1, registry.ResolveChain(ArtifactModule.Scene, 0, 1).Steps.Count);
    }

    [TestMethod]
    public void AnArtifactAlreadyCurrentResolvesToAnEmptyCompleteChain()
    {
        var registry = new ArtifactConverterRegistry();

        var chain = registry.ResolveChain(ArtifactModule.Project, 2, 2);

        Assert.IsTrue(chain.IsComplete);
        Assert.AreEqual(0, chain.Steps.Count);
    }

    [TestMethod]
    public void ApplyingAChainRunsEveryStepInOrder()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 1));
        registry.Register(new Step(ArtifactModule.Project, 1, 2));

        var document = registry.ResolveChain(ArtifactModule.Project, 0, 2).Apply(new JsonObject());

        Assert.AreEqual("[0->1][1->2]", document["Steps"]!.GetValue<string>());
    }

    /// <summary>A converter with equal from and to versions is rejected: nothing happens, ever.</summary>
    [TestMethod]
    public void AConverterWithEqualFromAndToVersionsIsRejectedAtRegistration()
    {
        var registry = new ArtifactConverterRegistry();

        var thrown = Assert.ThrowsException<InvalidOperationException>(
            () => registry.Register(new Step(ArtifactModule.Project, 1, 1)));

        StringAssert.Contains(thrown.Message, "Project");
        StringAssert.Contains(thrown.Message, "1");
    }

    /// <summary>A converter with descending versions is rejected: the data cannot be recovered.</summary>
    [TestMethod]
    public void AConverterWithDescendingVersionsIsRejectedAtRegistration()
    {
        var registry = new ArtifactConverterRegistry();

        var thrown = Assert.ThrowsException<InvalidOperationException>(
            () => registry.Register(new Step(ArtifactModule.Scene, 3, 2)));

        StringAssert.Contains(thrown.Message, "Scene");
        StringAssert.Contains(thrown.Message, "3");
        StringAssert.Contains(thrown.Message, "2");
    }

    /// <summary>Applying an incomplete chain throws and does not mutate the document.</summary>
    [TestMethod]
    public void ApplyingAnIncompleteChainThrowsAndDoesNotMutateTheDocument()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 1));
        // 1 -> 2 is missing.
        registry.Register(new Step(ArtifactModule.Project, 2, 3));

        var chain = registry.ResolveChain(ArtifactModule.Project, 0, 3);
        var document = new JsonObject { ["Initial"] = "value" };

        var thrown = Assert.ThrowsException<InvalidOperationException>(
            () => chain.Apply(document));

        StringAssert.Contains(thrown.Message, "1");
        Assert.AreEqual("value", document["Initial"]!.GetValue<string>(), "the document must not be mutated by a failed apply.");
    }

    /// <summary>A converter spanning multiple steps that overshoots the requested target is incomplete.</summary>
    [TestMethod]
    public void AConverterOvershoottingTheTargetIsIncomplete()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(ArtifactModule.Project, 0, 3));

        var chain = registry.ResolveChain(ArtifactModule.Project, 0, 2);

        Assert.IsFalse(chain.IsComplete);
        Assert.AreEqual(2, chain.MissingFromVersion);
    }
}
