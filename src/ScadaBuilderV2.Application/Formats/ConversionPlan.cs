namespace ScadaBuilderV2.Application.Formats;

/// <summary>One artifact found to be behind the generation this binary understands.</summary>
public sealed record ArtifactToConvert(
    ArtifactModule Module,
    string FilePath,
    int FromVersion,
    int ToVersion);

/// <summary>What one artifact's conversion will do, as shown to the operator.</summary>
public sealed record ConversionPlanEntry(
    ArtifactModule Module,
    string FilePath,
    int FromVersion,
    int ToVersion,
    IReadOnlyList<string> StepDescriptions);

/// <summary>Everything a single conversion will touch.</summary>
public sealed record ConversionPlan(IReadOnlyList<ConversionPlanEntry> Entries)
{
    /// <summary>Gets whether there is nothing to convert.</summary>
    public bool IsEmpty => Entries.Count == 0;
}

/// <summary>The operator's answer. C5 allows two, and read-only consultation is not one of them.</summary>
public enum ConversionDecision
{
    /// <summary>Convert, back up first, and open.</summary>
    Convert,

    /// <summary>Do not convert, and do not open.</summary>
    Cancel
}
