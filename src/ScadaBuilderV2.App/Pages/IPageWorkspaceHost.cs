using ScadaBuilderV2.App.Workspace;

namespace ScadaBuilderV2.App.Pages;

/// <summary>Visual callback boundary between the modern page workspace and the WPF/WebView shell.</summary>
public interface IPageWorkspaceHost
{
    Task ActivatePageAsync(SceneWorkspaceTab tab);
    Task<bool> ConfirmCloseDirtyPageAsync(SceneWorkspaceTab tab);
    void ClearActivePage(SceneWorkspaceTab closedTab);
    void ReportPageWorkspaceStatus(string message);
}
