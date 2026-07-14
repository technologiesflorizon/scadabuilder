using ScadaBuilderV2.Application.Pages;

namespace ScadaBuilderV2.Application.Commands.Pages;

/// <summary>Shared application command for dependency-safe page deletion.</summary>
public sealed class DeletePageCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<DeletePageRequest>(coordinator)
{
    public override string Id => "page.delete";
    public override string DisplayName => "Supprimer la page";
}
