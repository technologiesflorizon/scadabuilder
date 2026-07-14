using ScadaBuilderV2.Application.Pages;

namespace ScadaBuilderV2.Application.Commands.Pages;

/// <summary>Shared application command for changing a page title.</summary>
public sealed class RenamePageCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<RenamePageRequest>(coordinator)
{
    public override string Id => "page.rename";
    public override string DisplayName => "Renommer la page";
}
