using ScadaBuilderV2.Application.Pages;

namespace ScadaBuilderV2.Application.Commands.Pages;

/// <summary>Shared application command for native page creation.</summary>
public sealed class NewPageCommand(PageCommandCoordinator coordinator) : PageApplicationCommandBase<NewPageRequest>(coordinator)
{
    public override string Id => "page.new";
    public override string DisplayName => "Nouvelle page";
}
