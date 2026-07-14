using ScadaBuilderV2.Application.Pages;

namespace ScadaBuilderV2.Application.Commands.Pages;

/// <summary>Changes the human-visible page code while preserving the stable page key.</summary>
public sealed class ChangePageCodeCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<ChangePageCodeRequest>(coordinator)
{
    public override string Id => "page.change-code";
    public override string DisplayName => "Modifier le code de page";
}

/// <summary>Routes the selected page to the editor workspace.</summary>
public sealed class OpenPageCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<OpenPageRequest>(coordinator)
{
    public override string Id => "page.open";
    public override string DisplayName => "Ouvrir la page";
}

/// <summary>Routes the selected page to the shared properties surface.</summary>
public sealed class ShowPagePropertiesCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<ShowPagePropertiesRequest>(coordinator)
{
    public override string Id => "page.properties";
    public override string DisplayName => "Propriétés de la page";
}

/// <summary>Changes whether a page participates in compilation and export.</summary>
public sealed class SetPageBuildInclusionCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<SetPageBuildInclusionRequest>(coordinator)
{
    public override string Id => "page.set-build-inclusion";
    public override string DisplayName => "Inclure la page dans le build";
}

/// <summary>Assigns the project home page.</summary>
public sealed class SetHomePageCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<SetHomePageRequest>(coordinator)
{
    public override string Id => "page.set-home";
    public override string DisplayName => "Définir comme page d'accueil";
}

/// <summary>Changes the semantic page type.</summary>
public sealed class SetPageTypeCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<SetPageTypeRequest>(coordinator)
{
    public override string Id => "page.set-type";
    public override string DisplayName => "Modifier le type de page";
}

/// <summary>Changes stable header and footer composition targets.</summary>
public sealed class SetPageCompositionCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<SetPageCompositionRequest>(coordinator)
{
    public override string Id => "page.set-composition";
    public override string DisplayName => "Modifier la composition de page";
}

/// <summary>Runs coherent page-workspace validation without changing the project.</summary>
public sealed class ValidatePagesCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<ValidatePagesRequest>(coordinator)
{
    public override string Id => "page.validate";
    public override string DisplayName => "Valider les pages";
}
