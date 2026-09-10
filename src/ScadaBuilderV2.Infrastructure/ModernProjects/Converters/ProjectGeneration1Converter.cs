using System.IO;
using System.Text.Json.Nodes;
using ScadaBuilderV2.Application.Formats;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Infrastructure.ModernProjects.Converters;

/// <summary>Converts `project.json` from generation 0 to 1 by settling page identity once, in the file.</summary>
/// <remarks>
/// `ModernProjectMigration` remains: it is the store's identity normalizer, called from 11 sites including two
/// save paths and one in-memory construction with no file involved (Ruling 16), so a once-at-open file
/// converter cannot replace it. What this converter actually does is stamp the format generation and settle
/// each page's key using the same deterministic derivation the rest of the product already uses
/// (<see cref="PageKeyFactory.CreateDeterministic"/>), so that after conversion the file no longer depends on
/// re-derivation for the pages it already covers. That is what makes a future, narrower deletion of the
/// overlapping rule provable rather than merely hoped for.
///
/// The key is derived from the project name and page code rather than randomly generated, so converting the
/// same project twice or on two machines produces identical bytes, and so a converted page carries the exact
/// identity <see cref="PageKeyFactory.CreateDeterministic"/> would have produced for it anywhere else in the
/// product (Ruling 17).
///
/// Contracts: docs/superpowers/specs/2026-09-08-project-format-versioning-and-converters-design.md C3, C9.
/// Tests: tests/ScadaBuilderV2.Tests/Formats/ProjectGeneration1ConverterTests.cs.
/// </remarks>
public sealed class ProjectGeneration1Converter : IArtifactConverter
{
    /// <inheritdoc />
    public ArtifactModule Module => ArtifactModule.Project;

    /// <inheritdoc />
    public int FromVersion => 0;

    /// <inheritdoc />
    public int ToVersion => 1;

    /// <inheritdoc />
    public string StepDescription =>
        "Fixe l'identité durable de chaque page, jusqu'ici recalculée à chaque ouverture.";

    /// <inheritdoc />
    public JsonNode Convert(JsonNode document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document["Scenes"] is JsonArray scenes)
        {
            var projectName = document["Name"]?.GetValue<string>() ?? "";

            // Same reasoning as the per-page code guard below: PageKeyFactory.CreateDeterministic throws an
            // unfiltered ArgumentException on a blank name, which is a caller-bug signal everywhere else it is
            // called but is invalid data here — a truncated or hand-edited project.json with no Name is
            // exactly the input the backward-refusal gate exists to handle gracefully. Checked once, before
            // the scene loop, because it is a property of the document, not of any one page.
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new InvalidDataException(
                    "Conversion projet impossible : le manifeste projet ne porte aucun nom (\"Name\").");
            }

            var index = 0;
            foreach (var scene in scenes.OfType<JsonObject>())
            {
                var position = index++;
                var existing = scene["PageKey"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(existing)
                    && Guid.TryParse(existing, out var parsed)
                    && parsed != Guid.Empty)
                {
                    continue;
                }

                var code = scene["PageCode"]?.GetValue<string>()
                    ?? scene["Id"]?.GetValue<string>()
                    ?? "";

                // PageKeyFactory.CreateDeterministic throws ArgumentException on a blank project name or page
                // code, which would signal a caller bug anywhere else it is called. Here the blank value comes
                // from the document being converted, not from a caller mistake: it is invalid data. Reject it
                // as such, with InvalidDataException, so OpenAsync's existing catch filter
                // (IOException/UnauthorizedAccessException/InvalidDataException/InvalidOperationException/
                // JsonException) turns it into a `project.open-failed` diagnostic instead of letting an
                // unfiltered ArgumentException propagate unhandled to the UI.
                if (string.IsNullOrWhiteSpace(code))
                {
                    var title = scene["Title"]?.GetValue<string>();
                    var identifier = scene["Id"]?.GetValue<string>();
                    var descriptor = !string.IsNullOrWhiteSpace(identifier)
                        ? $"Id='{identifier}'"
                        : !string.IsNullOrWhiteSpace(title)
                            ? $"Title='{title}'"
                            : "aucun champ identifiant";
                    throw new InvalidDataException(
                        $"Conversion projet impossible : la page à l'index {position} ({descriptor}) ne porte aucun code de page.");
                }

                scene["PageKey"] = PageKeyFactory.CreateDeterministic(projectName, code).ToString("D");
            }
        }

        document["FormatVersion"] = ToVersion;
        return document;
    }
}
