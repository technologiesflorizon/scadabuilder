using System.Text.Json.Nodes;

namespace ScadaBuilderV2.Application.Formats;

/// <summary>An ordered, validated sequence of converters from one generation to another.</summary>
/// <param name="Module">Module the chain belongs to.</param>
/// <param name="FromVersion">Generation the artifact declares.</param>
/// <param name="ToVersion">Generation this binary understands.</param>
/// <param name="Steps">Converters to run, in ascending order.</param>
/// <param name="MissingFromVersion">Generation no converter accepts, or null when the chain is complete.</param>
public sealed record ConversionChain(
    ArtifactModule Module,
    int FromVersion,
    int ToVersion,
    IReadOnlyList<IArtifactConverter> Steps,
    int? MissingFromVersion)
{
    /// <summary>Gets whether every generation between the two ends is covered.</summary>
    public bool IsComplete => MissingFromVersion is null;

    /// <summary>Runs every step in order and returns the converted document.</summary>
    /// <exception cref="InvalidOperationException">The chain is incomplete.</exception>
    public JsonNode Apply(JsonNode document)
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException(
                $"La chaîne de conversion {Module} {FromVersion}->{ToVersion} est incomplète à {MissingFromVersion}.");
        }

        var current = document;
        foreach (var step in Steps)
        {
            current = step.Convert(current);
        }
        return current;
    }
}

/// <summary>Holds the converters of every module and composes their chains.</summary>
/// <remarks>
/// Two converters claiming the same starting generation is a registration error rather than a runtime one:
/// the ambiguity has no correct resolution, and discovering it at start-up is the only honest moment.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C3, C4.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ArtifactConverterRegistryTests.cs.
/// </remarks>
public sealed class ArtifactConverterRegistry
{
    private readonly Dictionary<(ArtifactModule Module, int From), IArtifactConverter> converters = [];

    /// <summary>Registers one converter.</summary>
    /// <exception cref="InvalidOperationException">Another converter already claims this starting generation.</exception>
    public void Register(IArtifactConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        if (converter.ToVersion <= converter.FromVersion)
        {
            throw new InvalidOperationException(
                $"Un convertisseur ne descend jamais: {converter.Module} {converter.FromVersion}->{converter.ToVersion}.");
        }

        var key = (converter.Module, converter.FromVersion);
        if (converters.TryGetValue(key, out var existing))
        {
            throw new InvalidOperationException(
                $"Deux convertisseurs revendiquent {converter.Module} depuis {converter.FromVersion}: "
                + $"{existing.StepDescription} et {converter.StepDescription}.");
        }
        converters[key] = converter;
    }

    /// <summary>Composes the chain between two generations, without running anything.</summary>
    public ConversionChain ResolveChain(ArtifactModule module, int fromVersion, int toVersion)
    {
        var steps = new List<IArtifactConverter>();
        var current = fromVersion;
        while (current < toVersion)
        {
            if (!converters.TryGetValue((module, current), out var step))
            {
                return new ConversionChain(module, fromVersion, toVersion, steps, current);
            }
            steps.Add(step);
            current = step.ToVersion;
        }
        if (current == toVersion)
        {
            return new ConversionChain(module, fromVersion, toVersion, steps, null);
        }
        return new ConversionChain(module, fromVersion, toVersion, steps, toVersion);
    }
}
