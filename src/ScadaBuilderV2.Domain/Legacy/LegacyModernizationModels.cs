using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Domain.Legacy;

public enum LegacyExtractionState
{
    Candidate,
    Accepted,
    Rejected,
    Converted,
    Deferred
}

public enum LegacyViewerMode
{
    LegacyOnly,
    ModernOnly,
    SideBySide,
    Overlay
}

public sealed record LegacySourceDocument(
    string Id,
    string DisplayName,
    string SourceSystem,
    string SourcePath);

public sealed record LegacyExtractionCandidate(
    string Id,
    LegacySourceDocument SourceDocument,
    string SourceElementId,
    string SuggestedDisplayName,
    ScadaElementKind SuggestedKind,
    SceneBounds SourceBounds,
    LegacyExtractionState State);

public sealed record LegacyComparisonSession(
    string SceneId,
    string LegacyDocumentId,
    LegacyViewerMode ViewerMode);
