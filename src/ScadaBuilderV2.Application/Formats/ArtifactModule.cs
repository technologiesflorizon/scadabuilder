namespace ScadaBuilderV2.Application.Formats;

/// <summary>One independently versioned persisted artifact family.</summary>
/// <remarks>
/// Each module carries its own generation so one can move without forcing the others, and a converter only
/// ever knows its own module.
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C1.
/// </remarks>
public enum ArtifactModule
{
    /// <summary>`project.json`.</summary>
    Project,

    /// <summary>One persisted scene under `scenes/`.</summary>
    Scene,

    /// <summary>The imported tag catalog.</summary>
    TagCatalog,

    /// <summary>One `.sep` component package.</summary>
    Component
}
