namespace ScadaBuilderV2.Domain.Versioning;

public enum VersionBumpKind
{
    Iteration,
    Feature,
    Production
}

public sealed record ScadaVersion(int Production, int Feature, int Iteration)
{
    public const string Generation = "V2";

    public static ScadaVersion Initial => new(0, 0, 1);

    public ScadaVersion Bump(VersionBumpKind kind)
    {
        return kind switch
        {
            VersionBumpKind.Iteration => this with { Iteration = Iteration + 1 },
            VersionBumpKind.Feature => this with { Feature = Feature + 1, Iteration = 0 },
            VersionBumpKind.Production => this with { Production = Production + 1, Feature = 0, Iteration = 0 },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public override string ToString()
    {
        return $"{Generation}.{Production}.{Feature}.{Iteration:0000}";
    }

    public static bool TryParse(string? raw, out ScadaVersion version)
    {
        version = Initial;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var parts = raw.Trim().Split('.');
        if (parts.Length != 4 || !string.Equals(parts[0], Generation, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var production) ||
            !int.TryParse(parts[2], out var feature) ||
            !int.TryParse(parts[3], out var iteration))
        {
            return false;
        }

        if (production < 0 || feature < 0 || iteration < 0 || iteration > 9999 || parts[3].Length != 4)
        {
            return false;
        }

        version = new ScadaVersion(production, feature, iteration);
        return true;
    }
}
