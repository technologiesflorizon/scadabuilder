using ScadaBuilderV2.Application.Projects;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Infrastructure.ModernProjects;

/// <summary>Validates and canonicalizes user-selected project workspace paths.</summary>
public static class ProjectWorkspacePathPolicy
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "con", "prn", "aux", "nul", "clock$",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
    };

    /// <summary>Validates creation input and returns its final canonical project root.</summary>
    public static (string? ProjectRoot, IReadOnlyList<ScadaBuildValidationIssue> Diagnostics) ValidateCreation(
        CreateProjectRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<ScadaBuildValidationIssue>();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            issues.Add(Error("project.name-required", "Le nom du projet est requis."));
        }

        var directoryName = request.ProjectDirectoryName?.Trim() ?? string.Empty;
        if (directoryName.Length == 0 ||
            directoryName is "." or ".." ||
            directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            ReservedNames.Contains(directoryName))
        {
            issues.Add(Error("project.directory-invalid", "Le nom du dossier projet est invalide ou réservé."));
        }

        var pageValidation = PageCodePolicy.Validate(request.InitialPageCode);
        foreach (var error in pageValidation.Errors)
        {
            issues.Add(Error("project.initial-page-invalid", error));
        }

        if (string.IsNullOrWhiteSpace(request.InitialPageTitle))
        {
            issues.Add(Error("project.initial-page-title-required", "Le titre de la première page est requis."));
        }

        if (request.CanvasSize.Width < 160 || request.CanvasSize.Height < 120 ||
            request.CanvasSize.Width > 16384 || request.CanvasSize.Height > 16384)
        {
            issues.Add(Error("project.canvas-invalid", "Les dimensions doivent être comprises entre 160 x 120 et 16384 x 16384."));
        }

        string? projectRoot = null;
        try
        {
            if (string.IsNullOrWhiteSpace(request.ParentDirectory) || !Path.IsPathRooted(request.ParentDirectory))
            {
                issues.Add(Error("project.parent-invalid", "Le dossier parent doit être un chemin absolu."));
            }
            else if (directoryName.Length > 0)
            {
                var parent = Path.GetFullPath(request.ParentDirectory);
                projectRoot = Path.GetFullPath(Path.Combine(parent, directoryName));
                var prefix = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!projectRoot.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Error("project.path-escape", "Le dossier projet doit rester sous le dossier parent choisi."));
                    projectRoot = null;
                }
                else if (Directory.Exists(projectRoot) || File.Exists(projectRoot))
                {
                    issues.Add(Error("project.target-exists", "Un fichier ou dossier existe déjà au chemin du projet."));
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issues.Add(Error("project.path-invalid", $"Le chemin du projet est invalide : {exception.Message}"));
        }

        return (projectRoot, issues);
    }

    /// <summary>Validates a selected manifest path and returns its exact project location.</summary>
    public static (ProjectWorkspaceLocation? Location, IReadOnlyList<ScadaBuildValidationIssue> Diagnostics) ValidateOpenPath(
        string projectFilePath)
    {
        var issues = new List<ScadaBuildValidationIssue>();
        try
        {
            if (string.IsNullOrWhiteSpace(projectFilePath) || !Path.IsPathRooted(projectFilePath))
            {
                issues.Add(Error("project.open-path-invalid", "Sélectionnez un chemin absolu vers project.json."));
                return (null, issues);
            }

            var fullPath = Path.GetFullPath(projectFilePath);
            if (!string.Equals(Path.GetFileName(fullPath), "project.json", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("project.manifest-name-invalid", "Le manifeste sélectionné doit être nommé project.json."));
            }
            else if (!File.Exists(fullPath))
            {
                issues.Add(Error("project.manifest-missing", "Le fichier project.json sélectionné est introuvable."));
            }

            var root = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                issues.Add(Error("project.root-invalid", "Le manifeste ne possède pas de dossier projet valide."));
            }

            return issues.Count == 0
                ? (new ProjectWorkspaceLocation(Path.GetFullPath(root!), fullPath), issues)
                : (null, issues);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            issues.Add(Error("project.open-path-invalid", $"Le chemin sélectionné est invalide : {exception.Message}"));
            return (null, issues);
        }
    }

    private static ScadaBuildValidationIssue Error(string code, string message) =>
        new(ScadaBuildValidationSeverity.Error, code, message);
}
