namespace ScadaBuilderV2.Application.ElementStudio;

/// <summary>
/// Decides whether an HTML document is too large for <c>CoreWebView2.NavigateToString</c>,
/// which rejects payloads beyond a documented ~2 MB (UTF-16) limit with an
/// <see cref="System.ArgumentException"/>. Callers that exceed the limit must navigate to a
/// temp file instead.
/// </summary>
/// <remarks>
/// Decisions: DEC-import-crash-navigate-fallback.
/// Contracts: Studio Element+ legacy source rendering.
/// Tests: ScadaBuilderV2.Tests.WebViewDocumentSizeGuardTests.
/// </remarks>
public static class WebViewDocumentSizeGuard
{
    /// <summary>
    /// Maximum document length (UTF-16 chars) considered safe for
    /// <c>NavigateToString</c>. Although Microsoft documents a ~2,097,152 ceiling,
    /// <c>NavigateToString</c> was empirically observed to throw
    /// <see cref="System.ArgumentException"/> at 1,921,198 characters
    /// (Studio Element+ / win00059, 152 legacy items). This threshold sits well below
    /// that observed failure point so oversized documents take the temp-file fallback.
    /// </summary>
    public const int NavigateToStringMaxCharacters = 1_500_000;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="document"/> is longer than
    /// <see cref="NavigateToStringMaxCharacters"/> and must use the temp-file navigation
    /// fallback. Returns <see langword="false"/> for <see langword="null"/>.
    /// </summary>
    public static bool ExceedsNavigateToStringLimit(string? document)
    {
        return document is not null && document.Length > NavigateToStringMaxCharacters;
    }
}
