namespace ScadaBuilderV2.Domain.Projects;

/// <summary>Current persisted format generation of each module.</summary>
/// <remarks>
/// A generation is a plain incrementing integer. It is deliberately unrelated to <see cref="ScadaVersion"/>,
/// which is the product version that authored a file, and to `ManifestVersion`, which is the export profile
/// negotiated with TF100Web. A format number describes one thing only: the shape of the data.
///
/// The absence of the field in a file means generation zero, so every artifact written before this mechanism
/// existed is generation zero without being rewritten.
///
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C1.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/BackwardRefusalTests.cs.
/// </remarks>
public static class ScadaFormatGeneration
{
    /// <summary>Generation of `project.json` understood by this binary.</summary>
    public const int Project = 0;

    /// <summary>Generation of a persisted scene understood by this binary.</summary>
    public const int Scene = 0;

    /// <summary>Generation of the persisted tag catalog understood by this binary.</summary>
    public const int TagCatalog = 0;

    /// <summary>Generation of a `.sep` component understood by this binary.</summary>
    /// <remarks>
    /// The component module already shipped a version of its own before this mechanism existed:
    /// `ElementStudioComponentMetadata.SchemaVersion`. That field is the component's format version and is
    /// used as-is. Adding a second number to the same file would create two truths about it.
    /// </remarks>
    public const int Component = 1;
}
