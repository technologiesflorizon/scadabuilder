using System.Windows;
using ScadaBuilderV2.Application.Projects;

namespace ScadaBuilderV2.App.Projects;

/// <summary>Asks what to do with unsaved work before a project is replaced or closed.</summary>
/// <remarks>
/// D9 names three answers: `Enregistrer`, `Ne pas enregistrer`, `Annuler`. A system `MessageBox` can only
/// offer `Oui`/`Non`/`Annuler`, which makes the destructive answer the shortest word on screen and forces the
/// operator to reconstruct what "Non" refers to from the question above it. This dialog puts the consequence
/// in the button, which is the only place it is read under time pressure.
///
/// Decisions: DEC-0049.
/// Contracts: docs/superpowers/specs/2026-07-29-project-lifecycle-design.md D9.
/// Tests: tests/ScadaBuilderV2.Tests/ProjectLifecycleShellContractTests.cs.
/// </remarks>
public partial class UnsavedChangesDialog : Window
{
    /// <summary>Gets the answer chosen by the operator; `Cancel` unless a button says otherwise.</summary>
    public ProjectCloseDecision Decision { get; private set; } = ProjectCloseDecision.Cancel;

    /// <summary>Creates the dialog for one named project and the transition it is blocking.</summary>
    /// <param name="projectName">Project carrying the unsaved changes.</param>
    /// <param name="pendingAction">What the operator asked for, used to say what `Annuler` keeps.</param>
    public UnsavedChangesDialog(string projectName, string pendingAction)
    {
        InitializeComponent();
        var name = string.IsNullOrWhiteSpace(projectName) ? "Le projet actif" : $"« {projectName} »";
        HeadlineText.Text = $"{name} contient des modifications non enregistrées.";
        DetailText.Text =
            $"« Enregistrer » écrit les modifications puis {pendingAction}. " +
            $"« Ne pas enregistrer » les abandonne définitivement. " +
            $"« Annuler » laisse le projet ouvert exactement dans son état actuel.";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => Complete(ProjectCloseDecision.Save);

    private void OnDiscardClick(object sender, RoutedEventArgs e) => Complete(ProjectCloseDecision.Discard);

    private void OnCancelClick(object sender, RoutedEventArgs e) => Complete(ProjectCloseDecision.Cancel);

    private void Complete(ProjectCloseDecision decision)
    {
        Decision = decision;
        DialogResult = decision != ProjectCloseDecision.Cancel;
        Close();
    }
}
