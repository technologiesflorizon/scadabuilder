using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Formats;

namespace ScadaBuilderV2.Tests.Formats;

/// <summary>
/// Locks the two outcomes C5 allows: convert, or do not open. There is no read-only consultation mode, so
/// there is no path by which an unconverted artifact becomes active.
/// </summary>
[TestClass]
public sealed class ConversionCoordinatorTests
{
    private sealed class Step(int from, int to) : IArtifactConverter
    {
        public ArtifactModule Module => ArtifactModule.Project;
        public int FromVersion => from;
        public int ToVersion => to;
        public string StepDescription => $"étape {from} vers {to}";
        public JsonNode Convert(JsonNode document)
        {
            document["Converted"] = to;
            return document;
        }
    }

    private sealed class Consent(ConversionDecision decision) : IConversionConsent
    {
        public int RequestCount { get; private set; }
        public ConversionPlan? SeenPlan { get; private set; }

        public Task<ConversionDecision> RequestAsync(ConversionPlan plan, CancellationToken cancellationToken)
        {
            RequestCount++;
            SeenPlan = plan;
            return Task.FromResult(decision);
        }
    }

    [TestMethod]
    public async Task AnArtifactAlreadyCurrentIsNotConvertedAndTheOperatorIsNotAsked()
    {
        var consent = new Consent(ConversionDecision.Convert);
        var coordinator = new ConversionCoordinator(new ArtifactConverterRegistry(), consent);

        var outcome = await coordinator.PrepareAsync(
            [new ArtifactToConvert(ArtifactModule.Project, "project.json", 0, 0)],
            CancellationToken.None);

        Assert.IsTrue(outcome.CanProceed);
        Assert.IsTrue(outcome.Plan.IsEmpty);
        Assert.AreEqual(0, consent.RequestCount, "there is nothing to consent to.");
    }

    [TestMethod]
    public async Task RefusingTheConversionStopsTheTransition()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(0, 1));
        var consent = new Consent(ConversionDecision.Cancel);
        var coordinator = new ConversionCoordinator(registry, consent);

        var outcome = await coordinator.PrepareAsync(
            [new ArtifactToConvert(ArtifactModule.Project, "project.json", 0, 1)],
            CancellationToken.None);

        Assert.IsFalse(outcome.CanProceed);
        Assert.AreEqual(1, consent.RequestCount);
        Assert.AreEqual("project.conversion-declined", outcome.Diagnostics.Single().Code);
    }

    [TestMethod]
    public async Task AcceptingTheConversionProducesAnExecutablePlan()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(0, 1));
        registry.Register(new Step(1, 2));
        var consent = new Consent(ConversionDecision.Convert);
        var coordinator = new ConversionCoordinator(registry, consent);

        var outcome = await coordinator.PrepareAsync(
            [new ArtifactToConvert(ArtifactModule.Project, "project.json", 0, 2)],
            CancellationToken.None);

        Assert.IsTrue(outcome.CanProceed);
        var entry = outcome.Plan.Entries.Single();
        Assert.AreEqual(0, entry.FromVersion);
        Assert.AreEqual(2, entry.ToVersion);
        CollectionAssert.AreEqual(
            new[] { "étape 0 vers 1", "étape 1 vers 2" },
            entry.StepDescriptions.ToArray(),
            "the operator is shown what each step changes, not just that something will.");
    }

    [TestMethod]
    public async Task AnIncompleteChainIsRefusedBeforeTheOperatorIsAsked()
    {
        var registry = new ArtifactConverterRegistry();
        registry.Register(new Step(0, 1));
        var consent = new Consent(ConversionDecision.Convert);
        var coordinator = new ConversionCoordinator(registry, consent);

        var outcome = await coordinator.PrepareAsync(
            [new ArtifactToConvert(ArtifactModule.Project, "project.json", 0, 3)],
            CancellationToken.None);

        Assert.IsFalse(outcome.CanProceed);
        Assert.AreEqual(0, consent.RequestCount, "there is no point asking to run a chain that cannot run.");
        var issue = outcome.Diagnostics.Single();
        Assert.AreEqual("project.conversion-chain-incomplete", issue.Code);
        StringAssert.Contains(issue.Message, "1");
    }
}
