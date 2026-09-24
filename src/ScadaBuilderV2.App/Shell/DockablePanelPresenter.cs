using System.Linq;
using AvalonDock;
using AvalonDock.Layout;

namespace ScadaBuilderV2.App.Shell;

/// <summary>Shows and hides a dockable panel without trusting the XAML field's own <see cref="LayoutAnchorable.Parent"/>.</summary>
/// <remarks>
/// AvalonDock 4.74.1: <c>DeserializeLayout</c> calls <c>XmlLayoutSerializer.Deserialize</c> with no
/// <c>LayoutSerializationCallback</c>, and that method **replaces <c>manager.Layout</c> wholesale** with a new
/// tree built from the XML, re-pairing each restored <c>Content</c> to a brand-new <c>LayoutAnchorable</c> by
/// <c>ContentId</c>. The original <c>LayoutAnchorable</c> objects that a XAML <c>x:Name</c> field points to -
/// <c>QuickWindowsAnchorable</c>, <c>ToolAnchorable</c>, etc. - are never in that new tree. They stay parented in
/// the old, dead one, and closing a live anchorable (<c>CanClose="True"</c> on all eleven) does the same thing:
/// it detaches into a dead subtree, it does not hide.
///
/// This is why a naive <c>if (anchorable.Parent is null)</c> guard never fires: a probe against the real
/// assembly (kept at
/// <c>C:\Users\mathi\AppData\Local\Temp\claude\F--Groupe-AMR-SCADA-AMR-GROUP\ec1a5845-88ba-4cc4-aef7-a67fb9a349f0\scratchpad\dockprobe</c>)
/// shows <c>Parent</c> non-null and <c>IsVisible</c> <c>true</c> on these ghosts both after a restore that omits
/// the panel and after <c>Close()</c>. <c>Show()</c> alone is then a no-op on a tree the <see
/// cref="DockingManager"/> no longer renders, and <c>AddToLayout</c> unconditionally would throw
/// (<c>IsVisible || IsHidden</c> is already true on the ghost).
///
/// The only field of a <see cref="LayoutAnchorable"/> that keeps its meaning across a full-tree replacement is
/// <see cref="LayoutAnchorable.ContentId"/>: <c>Deserialize</c> re-pairs by it, so it is how this presenter finds
/// the anchorable that is actually alive in <c>manager.Layout</c> right now. The XAML-declared object is used
/// only as a fallback when no live anchorable carries that <c>ContentId</c> - meaning the restored layout (or the
/// XAML default layout before any restore) never had this panel at all - in which case that declared object is
/// detached from whatever dead tree it is still parented in and attached to the live one.
///
/// Decisions: DEC-0050.
/// Contracts: docs/superpowers/specs/2026-08-04-parameterized-popup-management-architecture-design.md §9.1.
/// Tests: tests/ScadaBuilderV2.ShellTests/DockablePanelPresenterTests.cs.
/// </remarks>
public static class DockablePanelPresenter
{
    /// <summary>The default strategy used to re-attach a panel that is not in the live layout at all.</summary>
    public const AnchorableShowStrategy DefaultReattachStrategy = AnchorableShowStrategy.Most | AnchorableShowStrategy.Right;

    /// <summary>
    /// Shows the panel identified by <paramref name="declared"/>'s <see cref="LayoutAnchorable.ContentId"/>,
    /// activates it, and returns the anchorable that was actually shown.
    /// </summary>
    /// <param name="manager">The docking manager that owns the live layout, or <c>null</c> if the shell has not
    /// finished loading its docking surface yet.</param>
    /// <param name="declared">The <c>x:Name</c>-declared anchorable for this panel, used only as a fallback
    /// source when no live anchorable shares its <see cref="LayoutAnchorable.ContentId"/>.</param>
    /// <param name="strategy">The re-attach strategy used only when the panel has to be added back to the live
    /// layout from scratch.</param>
    /// <returns>
    /// The live anchorable that now carries this panel's content, or <c>null</c> when <paramref name="declared"/>
    /// or <paramref name="manager"/> (or its layout) is not available.
    /// </returns>
    /// <remarks>
    /// A panel that was absent from a restored layout takes the third branch below on its first open: the
    /// <c>declared</c> object itself is detached from its dead tree and <c>AddToLayout</c>-ed, so it *becomes*
    /// the live anchorable - <see cref="FindLive"/> and <c>declared</c> are the same object from then on. If the
    /// user then closes it, AvalonDock 4.74.1's <c>Close()</c> leaves a stale, dangling <see
    /// cref="LayoutAnchorable.Parent"/> (observed as a <c>LayoutPanel</c> it is no longer really a child of) and
    /// <see cref="LayoutAnchorable.IsVisible"/> stays <c>true</c>, while the anchorable is genuinely gone from
    /// <c>manager.Layout.Descendents()</c>. On the next <c>Show</c>, <see cref="FindLive"/> correctly finds
    /// nothing (it is not in the live tree), so the second branch runs: <c>Parent is ILayoutContainer</c> is
    /// true, but <c>RemoveChild</c> on that stale back-reference is a no-op - it does not clear <c>IsVisible</c>
    /// - and <c>AddToLayout</c> then throws on its <c>if (IsVisible || IsHidden) throw</c> guard. This shipped as
    /// a runtime <see cref="InvalidOperationException"/> ("l'opération a été interrompue") the first time a user
    /// actually closed a panel that had been opened this way.
    ///
    /// <see cref="LayoutAnchorable.Root"/> is what tells these two "not found live" cases apart, because it is
    /// unaffected by the stale <c>Parent</c>: a panel that was *never* restored or opened resolves to the dead
    /// tree's root (the one <c>Deserialize</c> discarded), while a panel that was live and then closed still
    /// resolves to <c>manager.Layout</c> itself - AvalonDock's <c>Close()</c> does not reparent the anchorable
    /// out of the live root, it only breaks the child link inside it. When <c>Root</c> matches the live layout,
    /// <c>Hide()</c> is called first: it normalizes the anchorable's bookkeeping into
    /// <c>LayoutRoot.Hidden</c> (clearing the state <c>AddToLayout</c>'s guard checks), and the following
    /// <c>Show()</c> then brings it back out of <c>Hidden</c> the way it is designed to - no <c>AddToLayout</c>
    /// call needed or possible here, since the anchorable is already rooted correctly. Calling <c>Hide()</c> on a
    /// never-restored ghost instead would be a no-op that just files it under the *dead* root's <c>Hidden</c>
    /// collection, so this branch must not be taken for that case - hence the <c>Root</c> check gating it.
    ///
    /// Verified against the real assembly by the probe kept at
    /// <c>C:\Users\mathi\AppData\Local\Temp\claude\F--Groupe-AMR-SCADA-AMR-GROUP\ec1a5845-88ba-4cc4-aef7-a67fb9a349f0\scratchpad\dockprobe</c>
    /// over three consecutive open/close cycles, content intact, no exception.
    /// </remarks>
    public static LayoutAnchorable? Show(
        DockingManager? manager,
        LayoutAnchorable? declared,
        AnchorableShowStrategy strategy = DefaultReattachStrategy)
    {
        if (declared is null || manager?.Layout is null)
        {
            return null;
        }

        var live = FindLive(manager, declared);
        if (live is not null)
        {
            live.Show();
            live.IsActive = true;
            return live;
        }

        if (ReferenceEquals(declared.Root, manager.Layout))
        {
            // Stale, but still rooted in the LIVE tree: a close left a dangling Parent, so IsVisible is
            // still true and AddToLayout would throw. Hide() normalises the anchorable into
            // LayoutRoot.Hidden, and Show() brings it back from there.
            declared.Hide();
            declared.Show();
            declared.IsActive = true;
            return declared;
        }

        if (declared.Parent is ILayoutContainer container)
        {
            container.RemoveChild(declared);
        }

        declared.AddToLayout(manager, strategy);
        declared.IsActive = true;
        return declared;
    }

    /// <summary>
    /// Activates (brings to front) the panel identified by <paramref name="declared"/>'s <see
    /// cref="LayoutAnchorable.ContentId"/>, if it is currently in the live layout. Unlike <see cref="Show"/>,
    /// this never re-attaches a panel that has been closed or was never restored - it only focuses one that is
    /// already visible.
    /// </summary>
    public static void Activate(DockingManager? manager, LayoutAnchorable? declared)
    {
        if (declared is null || manager?.Layout is null)
        {
            return;
        }

        var live = FindLive(manager, declared);
        if (live is not null)
        {
            live.IsActive = true;
        }
    }

    /// <summary>
    /// Hides the panel identified by <paramref name="declared"/>'s <see cref="LayoutAnchorable.ContentId"/>. A
    /// no-op when the panel is not currently in the live layout - there is nothing to hide.
    /// </summary>
    public static void Hide(DockingManager? manager, LayoutAnchorable? declared)
    {
        if (declared is null || manager?.Layout is null)
        {
            return;
        }

        FindLive(manager, declared)?.Hide();
    }

    /// <summary>Finds the anchorable actually present in the live layout tree with this <c>ContentId</c>.</summary>
    private static LayoutAnchorable? FindLive(DockingManager manager, LayoutAnchorable declared) =>
        manager.Layout?.Descendents().OfType<LayoutAnchorable>()
            .FirstOrDefault(a => a.ContentId == declared.ContentId);
}
