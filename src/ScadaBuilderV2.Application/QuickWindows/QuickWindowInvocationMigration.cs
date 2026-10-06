using ScadaBuilderV2.Domain.ElementEvents.Command;
using ScadaBuilderV2.Domain.Projects;

namespace ScadaBuilderV2.Application.QuickWindows;

/// <summary>
/// Pure decision and project-mutation helpers for retiring a legacy `QuickWindowInvocation` when an
/// `OpenQuickWindow` command authored under DEC-0050 (a plain invocation key) is edited into the DEC-0052
/// sheet/page pairing (`ParameterSheetKey`/`QuickWindowPageKey`).
/// </summary>
/// <remarks>
/// Without this cleanup, the orphaned invocation stays in `project.QuickWindowInvocations`, still carrying
/// the migrated command's `OwnerPageKey`/`OwnerElementId`/`OwnerCommandId` (its `Id` is preserved by
/// migration): <c>ValidateQuickWindowInvocations</c> validates every invocation unconditionally, and if its
/// definition is later deleted or a bound tag drifts, build/export raises an error pinned to a command that
/// looks correctly configured, with no UI left to fix it (controller fix round 1 on Task 10).
///
/// Statut : DEC-0050 décommissionné comme modèle d'authoring (remplacé par DEC-0052) ; conservé pour compatibilité
/// des projets existants : InvocationKeyToRetire et WithoutInvocation migrent une commande OpenQuickWindow héritée
/// (QuickWindowInvocationKey) vers une paire feuille + page.
/// Decisions: DEC-0050 (décommissionné), DEC-0052.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowInvocationMigrationTests.cs.
/// </remarks>
public static class QuickWindowInvocationMigration
{
    /// <summary>
    /// Gets the legacy invocation key to retire when editing turns <paramref name="original"/> into
    /// <paramref name="migrated"/>, or null when this is not a sheet/page migration (nothing to retire).
    /// </summary>
    /// <remarks>
    /// A migration is: the original command was `OpenQuickWindow` and carried a `QuickWindowInvocationKey`;
    /// the migrated command is still `OpenQuickWindow`, now carries both `ParameterSheetKey` and
    /// `QuickWindowPageKey`, and no longer carries any `QuickWindowInvocationKey` of its own. Re-saving a
    /// legacy command unchanged (no sheet/page chosen) is not a migration and returns null.
    /// </remarks>
    public static Guid? InvocationKeyToRetire(ScadaCommandBinding original, ScadaCommandBinding migrated)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(migrated);

        if (original.Kind != ScadaCommandKind.OpenQuickWindow || migrated.Kind != ScadaCommandKind.OpenQuickWindow)
        {
            return null;
        }

        if (original.QuickWindowInvocationKey is not { } retiredKey)
        {
            return null;
        }

        if (migrated.ParameterSheetKey is null || migrated.QuickWindowPageKey is null)
        {
            return null;
        }

        if (migrated.QuickWindowInvocationKey is not null)
        {
            return null;
        }

        return retiredKey;
    }

    /// <summary>Removes one invocation from the project's effective quick-window invocations, if present.</summary>
    public static ScadaProject WithoutInvocation(ScadaProject project, Guid invocationKey)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.EffectiveQuickWindowInvocations.Any(invocation => invocation.InvocationKey == invocationKey))
        {
            return project;
        }

        return project with
        {
            QuickWindowInvocations = project.EffectiveQuickWindowInvocations
                .Where(invocation => invocation.InvocationKey != invocationKey)
                .ToArray()
        };
    }
}
