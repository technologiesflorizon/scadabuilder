using ScadaBuilderV2.Application.Pages;

namespace ScadaBuilderV2.Application.Commands.Pages;

/// <summary>Shared application command for complete page duplication.</summary>
public sealed class DuplicatePageCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<DuplicatePageRequest>(coordinator)
{
    public override string Id => "page.duplicate";
    public override string DisplayName => "Dupliquer la page";
}
