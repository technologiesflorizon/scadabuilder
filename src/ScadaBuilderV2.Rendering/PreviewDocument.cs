namespace ScadaBuilderV2.Rendering;

public sealed record PreviewDocument
{
    public PreviewDocument(string pageId, string title, string relativeHtmlSource)
    {
        PageId = RequireText(pageId, nameof(pageId));
        Title = RequireText(title, nameof(title));
        RelativeHtmlSource = RequireRelativePath(relativeHtmlSource, nameof(relativeHtmlSource));
    }

    public string PageId { get; }

    public string Title { get; }

    public string RelativeHtmlSource { get; }

    public string GetSourcePath(string previewRootPath)
    {
        var rootPath = RequireText(previewRootPath, nameof(previewRootPath));
        var fullRootPath = Path.GetFullPath(rootPath);
        var sourcePath = Path.GetFullPath(Path.Combine(fullRootPath, RelativeHtmlSource));

        if (!sourcePath.StartsWith(EnsureTrailingSeparator(fullRootPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Preview source path must stay inside the preview root path.");
        }

        return sourcePath;
    }

    public Uri GetSourceUri(string previewRootPath)
    {
        return new Uri(GetSourcePath(previewRootPath), UriKind.Absolute);
    }

    /// <summary>Materializes a native page document under the editor preview root.</summary>
    public static async Task<PreviewDocument> MaterializeNativeAsync(
        PageDocumentInput input,
        string previewRootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewRootPath);
        var document = NativePageDocumentFactory.Create(input);
        var relativeDirectory = Path.Combine("native", document.PageCode);
        var pageDirectory = Path.Combine(previewRootPath, relativeDirectory);
        var cssDirectory = Path.Combine(pageDirectory, "css");
        Directory.CreateDirectory(cssDirectory);
        var htmlRelativePath = Path.Combine(relativeDirectory, $"{document.PageCode}.html");
        await File.WriteAllTextAsync(
            Path.Combine(previewRootPath, htmlRelativePath),
            document.Html,
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(cssDirectory, $"{document.PageCode}.css"),
            document.Css,
            cancellationToken);
        return new PreviewDocument(document.PageCode, input.Page.Title, htmlRelativePath);
    }

    /// <summary>
    /// Materializes one editor-only quick-window instance preview under the preview root: document,
    /// stylesheet and the preview script bundle carrying the shared runtime and the quick-window host.
    /// </summary>
    /// <remarks>
    /// The materialized files live only under the editor preview root; they are never part of the project
    /// model and never reach a `.sb2` package.
    ///
    /// Decisions: DEC-0050, FR-019, FR-UI-21.
    /// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPreviewTests.cs.
    /// </remarks>
    public static async Task<PreviewDocument> MaterializeQuickWindowAsync(
        QuickWindows.QuickWindowPreviewInput input,
        string previewRootPath,
        long generation = 1,
        Guid? runtimeInstanceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewRootPath);
        var document = QuickWindows.QuickWindowPreviewDocumentFactory.Create(input, generation, runtimeInstanceId);
        var relativeDirectory = Path.Combine(QuickWindows.QuickWindowPreviewDocumentFactory.InstanceCodePrefix.TrimEnd('-'), document.InstanceCode);
        var instanceDirectory = Path.Combine(previewRootPath, relativeDirectory);
        var cssDirectory = Path.Combine(instanceDirectory, "css");
        Directory.CreateDirectory(cssDirectory);
        var htmlRelativePath = Path.Combine(relativeDirectory, $"{document.InstanceCode}.html");
        await File.WriteAllTextAsync(Path.Combine(previewRootPath, htmlRelativePath), document.Html, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(cssDirectory, $"{document.InstanceCode}.css"),
            document.Css,
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(instanceDirectory, QuickWindows.QuickWindowPreviewDocumentFactory.ScriptFileName),
            document.Script,
            cancellationToken);
        return new PreviewDocument(document.InstanceCode, input.Definition.EffectiveTitle, htmlRelativePath);
    }

    private static string RequireText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string RequireRelativePath(string value, string parameterName)
    {
        var relativePath = RequireText(value, parameterName);

        if (Path.IsPathRooted(relativePath) || Uri.TryCreate(relativePath, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Preview HTML source must be a relative path.", parameterName);
        }

        return relativePath;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
