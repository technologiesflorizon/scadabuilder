namespace ScadaBuilderV2.App;

/// <summary>
/// Pairs an <see cref="EffectKind"/> with its French label for UI display.
/// </summary>
internal sealed record EffectTypeItem(EffectKind Kind, string Label)
{
    /// <inheritdoc />
    public override string ToString() => Label;
}
