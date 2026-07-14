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
