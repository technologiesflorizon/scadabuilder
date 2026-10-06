using System.Collections.ObjectModel;
using System.ComponentModel;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Domain.Scenes;
using ScadaBuilderV2.Rendering.QuickWindows;
// ScadaBuildValidationIssue lives in ScadaBuilderV2.Domain.Projects; ParameterSheet in ScadaBuilderV2.Domain.QuickWindows (both imported above).

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>One editor-only test row: a public port and the temporary value fed to the preview.</summary>
/// <remarks>
/// Test rows are never persisted, never validated as durable bindings and never exported: they exist to
/// exercise a preview instance while authoring.
///
/// Decisions: DEC-0050, FR-UI-21.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPreviewTests.cs.
/// </remarks>
public sealed class QuickWindowTestBenchRowViewModel : INotifyPropertyChanged
{
    private string temporaryValue = string.Empty;
    private string temporaryTagId = string.Empty;

    /// <summary>Creates one test row for a public interface member.</summary>
    public QuickWindowTestBenchRowViewModel(QuickWindowInterfaceMember member)
    {
        Member = member ?? throw new ArgumentNullException(nameof(member));
        temporaryValue = member.DefaultValue ?? string.Empty;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the previewed member.</summary>
    public QuickWindowInterfaceMember Member { get; }

    /// <summary>Gets the port name.</summary>
    public string Name => Member.Name;

    /// <summary>Gets the port family label.</summary>
    public string FamilyLabel => QuickWindowInterfaceLabels.For(Member.Family);

    /// <summary>Gets the port data type label.</summary>
    public string DataTypeLabel => QuickWindowInterfaceLabels.For(Member.DataType);

    /// <summary>Gets whether the port must be bound by a durable invocation.</summary>
    public bool Required => Member.Required;

    /// <summary>Gets or sets the temporary tag id fed to the shared tag bridge.</summary>
    public string TemporaryTagId
    {
        get => temporaryTagId;
        set { var candidate = value ?? string.Empty; if (temporaryTagId == candidate) return; temporaryTagId = candidate; Raise(nameof(TemporaryTagId)); }
    }

    /// <summary>Gets or sets the temporary value of this port.</summary>
    public string TemporaryValue
    {
        get => temporaryValue;
        set { var candidate = value ?? string.Empty; if (temporaryValue == candidate) return; temporaryValue = candidate; Raise(nameof(TemporaryValue)); }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Editor-only test bench of one quick-window definition: temporary values, temporary bindings and the
/// preview instance they feed (FR-UI-21).
/// </summary>
/// <remarks>
/// The bench never writes into the project: <see cref="ToTestBenchValues"/> produces a transient payload
/// consumed by the preview document only, and the guardrail is asserted by contract tests.
///
/// Statut : DEC-0050 décommissionné comme modèle d'authoring (remplacé par DEC-0052). LoadPage, SelectSheet,
/// SetLoweringDiagnostics et Clear servent le banc d'essai DEC-0052. Rows, Load(QuickWindowDefinition),
/// ToTestBenchValues et QuickWindowTestBenchRowViewModel sont des résidus DEC-0050 sans appelant hors tests
/// (KNOWN_GAPS_V2.md entrée 40).
/// Decisions: DEC-0050 (décommissionné), DEC-0052, FR-UI-21.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPreviewTests.cs.
/// </remarks>
public sealed class QuickWindowTestBenchViewModel : INotifyPropertyChanged
{
    private QuickWindowDefinition? definition;
    private string titleOverride = string.Empty;
    private string status = string.Empty;
    private string pageLabel = string.Empty;
    private Guid? selectedSheetKey;
    private IReadOnlyList<ScadaBuildValidationIssue> loweringDiagnostics = [];
    private Guid? loadedPageKey;
    private IReadOnlyList<(Guid SheetKey, string DisplayName)> loadedSheetIdentity = [];
    private bool isProgrammaticSelectionUpdate;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised exactly when the operator genuinely picks a sheet in the bench's sheet selector — never for
    /// a programmatic resync (<see cref="LoadPage"/> restoring a previously valid selection across a page
    /// reload, or <see cref="Clear"/>). This is what actually drives the lowering-and-preview request; the
    /// control forwards it unchanged as its own `SheetSelected` event.
    /// </summary>
    /// <remarks>
    /// Controller fix round 2: `OnPageCommandCompleted` now calls the refresh that reaches
    /// <see cref="LoadPage"/> on every page-property command, not only a real tab switch. Combined with the
    /// `SelectedValue` binding added in fix round 1, a page reload that only restores the same selection
    /// (or briefly clears it while the sheet `ItemsSource` is rebuilt) could reach the WPF control's
    /// `SelectionChanged` event and, before this fix, unconditionally re-triggered a full lowering and
    /// preview reload — visible flicker on every unrelated page command. The single source of truth is now
    /// this event: it fires only when <see cref="SelectedSheetKey"/> actually changes to a non-null value
    /// while <see cref="isProgrammaticSelectionUpdate"/> is false, regardless of how many times the WPF
    /// control's `SelectionChanged` re-forwards the same, unchanged key.
    /// </remarks>
    public event EventHandler<Guid>? SheetSelected;

    /// <summary>Gets the editable test rows, one per public port.</summary>
    public ObservableCollection<QuickWindowTestBenchRowViewModel> Rows { get; } = [];

    /// <summary>Gets the parameter sheets offered by the bench's sheet selector (DEC-0052).</summary>
    public ObservableCollection<ParameterSheet> Sheets { get; } = [];

    /// <summary>Gets the label of the active quick-window page previewed by the bench.</summary>
    public string PageLabel
    {
        get => pageLabel;
        private set { if (pageLabel == value) return; pageLabel = value; Raise(nameof(PageLabel)); }
    }

    /// <summary>
    /// Gets or sets the sheet key currently selected in the bench, or null. The setter is what the
    /// `SheetSelectorComboBox`'s `SelectedValue` binding writes to and reads back from, so a page reload
    /// that restores a previously picked sheet (<see cref="LoadPage"/>) keeps the combo box in sync.
    /// Setting it to a genuinely new value raises <see cref="SheetSelected"/> — unless
    /// <see cref="isProgrammaticSelectionUpdate"/> is set, which every internal caller
    /// (<see cref="LoadPage"/>, <see cref="Clear"/>) uses so a resync never previews on its own; only
    /// <see cref="SelectSheet"/>, the operator-pick entry point, leaves the guard open.
    /// </summary>
    public Guid? SelectedSheetKey
    {
        get => selectedSheetKey;
        set
        {
            if (selectedSheetKey == value) return;
            selectedSheetKey = value;
            Raise(nameof(SelectedSheetKey));
            if (!isProgrammaticSelectionUpdate && value is { } key)
            {
                SheetSelected?.Invoke(this, key);
            }
        }
    }

    /// <summary>Gets the lowering diagnostics of the last previewed (sheet, page) pair (DEC-0052, spec §6).</summary>
    public IReadOnlyList<ScadaBuildValidationIssue> LoweringDiagnostics => loweringDiagnostics;

    /// <summary>Gets whether the last lowering carried at least one diagnostic.</summary>
    public bool HasLoweringDiagnostics => loweringDiagnostics.Count > 0;

    /// <summary>Gets the definition currently exercised, or null.</summary>
    public Guid? DefinitionKey => definition?.DefinitionKey;

    /// <summary>Gets the definition label shown by the bench.</summary>
    public string DefinitionLabel => definition is null
        ? "Aucune fenêtre rapide active."
        : $"{definition.DisplayName} · interface v{definition.InterfaceVersion}";

    /// <summary>Gets whether a definition is loaded.</summary>
    public bool HasDefinition => definition is not null;

    /// <summary>Gets or sets the temporary title of the previewed instance.</summary>
    public string TitleOverride
    {
        get => titleOverride;
        set { var candidate = value ?? string.Empty; if (titleOverride == candidate) return; titleOverride = candidate; Raise(nameof(TitleOverride)); }
    }

    /// <summary>Gets the last bench status message.</summary>
    public string Status
    {
        get => status;
        set { var candidate = value ?? string.Empty; if (status == candidate) return; status = candidate; Raise(nameof(Status)); }
    }

    /// <summary>Loads the public ports of one definition, keeping previously typed values by member key.</summary>
    public void Load(QuickWindowDefinition value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var previous = Rows.ToDictionary(row => row.Member.MemberKey, row => (row.TemporaryTagId, row.TemporaryValue));
        definition = value;
        Rows.Clear();
        foreach (var member in value.EffectiveInterfaceMembers.Where(member => member.IsPublic))
        {
            var row = new QuickWindowTestBenchRowViewModel(member);
            if (previous.TryGetValue(member.MemberKey, out var kept))
            {
                row.TemporaryTagId = kept.TemporaryTagId;
                row.TemporaryValue = kept.TemporaryValue;
            }

            Rows.Add(row);
        }

        RaiseAll();
    }

    /// <summary>
    /// Loads the bench for the active quick-window page (DEC-0052, spec §5): offers every parameter
    /// sheet of the project, keeping the previously selected one when it is still offered.
    /// </summary>
    /// <remarks>
    /// Controller fix round 2: idempotent when <paramref name="page"/>'s key and the ordered (key,
    /// display name) set of <paramref name="sheets"/> are unchanged from the last call — nothing is
    /// touched, not even a `Sheets` collection-changed or a redundant `PropertyChanged`, since
    /// `OnPageCommandCompleted` now calls this on every page-property command, not only a real page
    /// switch. When something did change, the whole reload runs with <see cref="isProgrammaticSelectionUpdate"/>
    /// set, so restoring (or losing) a previously valid selection never raises <see cref="SheetSelected"/>.
    /// </remarks>
    public void LoadPage(ScadaScene page, IReadOnlyList<ParameterSheet> sheets)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(sheets);

        var ordered = sheets.OrderBy(sheet => sheet.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        var identity = ordered.Select(sheet => (sheet.SheetKey, sheet.DisplayName)).ToArray();
        if (loadedPageKey == page.PageKey && identity.SequenceEqual(loadedSheetIdentity))
        {
            return;
        }

        isProgrammaticSelectionUpdate = true;
        try
        {
            loadedPageKey = page.PageKey;
            loadedSheetIdentity = identity;
            var previous = selectedSheetKey;
            PageLabel = $"{page.Title} ({page.EffectivePageCode})";
            Sheets.Clear();
            foreach (var sheet in ordered) Sheets.Add(sheet);
            SelectedSheetKey = previous is { } key && Sheets.Any(sheet => sheet.SheetKey == key) ? key : null;
            loweringDiagnostics = [];
            Status = string.Empty;
            Raise(nameof(LoweringDiagnostics));
            Raise(nameof(HasLoweringDiagnostics));
        }
        finally
        {
            isProgrammaticSelectionUpdate = false;
        }
    }

    /// <summary>
    /// Records the sheet the operator genuinely picked in the bench's sheet selector, raising
    /// <see cref="SheetSelected"/> exactly once when this actually changes the selection. The control's
    /// code-behind calls this unconditionally from `SelectionChanged`, including for a resync the WPF
    /// `SelectedValue` binding itself triggered (e.g. the `Sheets` `ItemsSource` being rebuilt by
    /// <see cref="LoadPage"/>) — the equality check here, not the caller, is what makes that safe.
    /// </summary>
    public void SelectSheet(Guid sheetKey) => SelectedSheetKey = sheetKey;

    /// <summary>Shows the lowering diagnostics of the last previewed (sheet, page) pair in the bench.</summary>
    public void SetLoweringDiagnostics(IReadOnlyList<ScadaBuildValidationIssue> diagnostics)
    {
        loweringDiagnostics = diagnostics ?? [];
        Raise(nameof(LoweringDiagnostics));
        Raise(nameof(HasLoweringDiagnostics));
    }

    /// <summary>Clears the bench when no quick window is active.</summary>
    public void Clear()
    {
        isProgrammaticSelectionUpdate = true;
        try
        {
            definition = null;
            Rows.Clear();
            Sheets.Clear();
            loadedPageKey = null;
            loadedSheetIdentity = [];
            SelectedSheetKey = null;
            loweringDiagnostics = [];
            PageLabel = string.Empty;
            titleOverride = string.Empty;
            Status = string.Empty;
            RaiseAll();
            Raise(nameof(LoweringDiagnostics));
            Raise(nameof(HasLoweringDiagnostics));
        }
        finally
        {
            isProgrammaticSelectionUpdate = false;
        }
    }

    /// <summary>
    /// Produces the transient preview payload: temporary tag values and temporary literal bindings.
    /// Nothing here is persisted, validated as a durable binding or exported.
    /// </summary>
    public QuickWindowTestBenchValues ToTestBenchValues()
    {
        var tagValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var bindings = new List<QuickWindowBinding>();
        foreach (var row in Rows)
        {
            if (!string.IsNullOrWhiteSpace(row.TemporaryTagId))
            {
                tagValues[row.TemporaryTagId.Trim()] = row.TemporaryValue;
                bindings.Add(QuickWindowBinding.FromTag(row.Member.MemberKey, row.TemporaryTagId.Trim()));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(row.TemporaryValue))
            {
                bindings.Add(QuickWindowBinding.FromLiteral(row.Member.MemberKey, row.TemporaryValue));
            }
        }

        return new QuickWindowTestBenchValues(
            tagValues,
            bindings,
            string.IsNullOrWhiteSpace(titleOverride) ? null : titleOverride);
    }

    private void RaiseAll()
    {
        foreach (var name in new[] { nameof(DefinitionKey), nameof(DefinitionLabel), nameof(HasDefinition), nameof(TitleOverride) })
        {
            Raise(name);
        }
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
