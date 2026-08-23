using System.Collections.ObjectModel;
using System.ComponentModel;
using ScadaBuilderV2.Domain.QuickWindows;
using ScadaBuilderV2.Rendering.QuickWindows;

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
/// Decisions: DEC-0050, FR-UI-21.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/QuickWindowPreviewTests.cs.
/// </remarks>
public sealed class QuickWindowTestBenchViewModel : INotifyPropertyChanged
{
    private QuickWindowDefinition? definition;
    private string titleOverride = string.Empty;
    private string status = string.Empty;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the editable test rows, one per public port.</summary>
    public ObservableCollection<QuickWindowTestBenchRowViewModel> Rows { get; } = [];

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

    /// <summary>Clears the bench when no quick window is active.</summary>
    public void Clear()
    {
        definition = null;
        Rows.Clear();
        titleOverride = string.Empty;
        Status = string.Empty;
        RaiseAll();
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
