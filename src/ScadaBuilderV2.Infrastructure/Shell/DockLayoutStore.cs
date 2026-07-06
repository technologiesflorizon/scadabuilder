namespace ScadaBuilderV2.Infrastructure.Shell;

/// <summary>
/// Reads and writes the AvalonDock layout XML used to persist the SCADA Builder V2
/// shell's side-panel arrangement across sessions.
/// </summary>
public sealed class DockLayoutStore
{
    /// <summary>
    /// Returns the default per-user path for the dock layout file:
    /// <c>%AppData%\ScadaBuilderV2\dock-layout.xml</c>.
    /// </summary>
    public string GetDefaultLayoutPath()
    {
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataRoot, "ScadaBuilderV2", "dock-layout.xml");
    }

    /// <summary>
    /// Reads the layout XML at <paramref name="path"/>. Returns <c>null</c> if the file
    /// does not exist or cannot be read.
    /// </summary>
    public async Task<string?> ReadLayoutXmlAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return await File.ReadAllTextAsync(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes <paramref name="layoutXml"/> to <paramref name="path"/>, creating the
    /// containing directory if it does not exist.
    /// </summary>
    public async Task WriteLayoutXmlAsync(string path, string layoutXml)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, layoutXml);
    }
}
