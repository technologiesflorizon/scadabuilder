using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Application.Pages;

/// <summary>Base request passed from any page-management surface to a shared application command.</summary>
public abstract record PageCommandRequest;

/// <summary>Requests creation of a native page from an optional application template.</summary>
public sealed record NewPageRequest(string PageCode, string? Title = null, PageTemplate? Template = null) : PageCommandRequest;

/// <summary>Requests a descriptive title change without changing page identity.</summary>
public sealed record RenamePageRequest(Guid PageKey, string Title) : PageCommandRequest;

/// <summary>Requests a human-visible technical code change without changing the stable key.</summary>
public sealed record ChangePageCodeRequest(Guid PageKey, string PageCode) : PageCommandRequest;

/// <summary>Requests a complete page and scene duplication.</summary>
public sealed record DuplicatePageRequest(Guid PageKey, string PageCode, string? Title = null) : PageCommandRequest;

/// <summary>Requests deletion after dependency analysis.</summary>
public sealed record DeletePageRequest(Guid PageKey) : PageCommandRequest;

/// <summary>Requests explicit page opening or activation.</summary>
public sealed record OpenPageRequest(Guid PageKey) : PageCommandRequest;

/// <summary>Requests navigation to one page's properties.</summary>
public sealed record ShowPagePropertiesRequest(Guid PageKey) : PageCommandRequest;

/// <summary>Requests a build-inclusion state change.</summary>
public sealed record SetPageBuildInclusionRequest(Guid PageKey, bool IncludeInBuild) : PageCommandRequest;

/// <summary>Requests assignment of the compiled Default home page.</summary>
public sealed record SetHomePageRequest(Guid? PageKey) : PageCommandRequest;

/// <summary>Requests a page-type change.</summary>
public sealed record SetPageTypeRequest(Guid PageKey, ScadaPageType PageType) : PageCommandRequest;

/// <summary>Requests stable header and footer composition targets.</summary>
public sealed record SetPageCompositionRequest(Guid PageKey, Guid? HeaderPageKey, Guid? FooterPageKey) : PageCommandRequest;

/// <summary>Requests a page canvas-size change.</summary>
public sealed record SetPageCanvasRequest(Guid PageKey, CanvasSize CanvasSize) : PageCommandRequest;

/// <summary>Requests a page background-style change.</summary>
public sealed record SetPageBackgroundRequest(Guid PageKey, SceneBackgroundStyle Background) : PageCommandRequest;

/// <summary>Requests read-only validation of the complete page workspace.</summary>
public sealed record ValidatePagesRequest : PageCommandRequest;
