using System.ComponentModel;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.QuickWindows;

namespace ScadaBuilderV2.App.QuickWindows;

/// <summary>
/// Editor view-model of one parameter sheet: the text stays the single source of truth, diagnostics are
/// recomputed by <see cref="ParameterSheetParser"/> on every assignment, and <see cref="AppendRule"/> is the
/// only way the rule dialog ("+" of the editor header) grows the text.
/// </summary>
/// <remarks>
/// No WPF type: this view-model is `Compile Include`d into `tests/ScadaBuilderV2.Tests` so its behaviour is
/// pinned by a real unit test rather than a source-reading contract test.
///
/// Decisions: DEC-0052.
/// Contracts: docs/superpowers/specs/2026-09-30-quick-window-parameter-sheets-design.md §5.
/// Tests: tests/ScadaBuilderV2.Tests/QuickWindows/ParameterSheetEditorViewModelTests.cs.
/// </remarks>
public sealed class ParameterSheetEditorViewModel : INotifyPropertyChanged
{
    private readonly Guid sheetKey;
    private readonly string code;
    private readonly string displayName;
    private string text;
    private ParameterSheetParseResult parseResult;

    /// <summary>Creates the editor view-model of one sheet, parsing its current text immediately.</summary>
    public ParameterSheetEditorViewModel(ParameterSheet sheet, ScadaTagCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        sheetKey = sheet.SheetKey;
        code = sheet.Code;
        displayName = sheet.DisplayName;
        Catalog = catalog;
        text = sheet.Text ?? string.Empty;
        parseResult = ParameterSheetParser.Parse(text);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the project tag catalogue offered to a Tag rule, or null when none is available.</summary>
    public ScadaTagCatalog? Catalog { get; }

    /// <summary>Gets the display name of the edited sheet (read-only here; renamed from the panel).</summary>
    public string DisplayName => displayName;

    /// <summary>Gets or sets the full sheet text; every assignment refreshes <see cref="Diagnostics"/>.</summary>
    public string Text
    {
        get => text;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(normalized, text, StringComparison.Ordinal)) return;
            text = normalized;
            parseResult = ParameterSheetParser.Parse(text);
            OnChanged(nameof(Text));
            OnChanged(nameof(Diagnostics));
            OnChanged(nameof(NextFreeNumber));
        }
    }

    /// <summary>Gets the diagnostics of the current text, one per invalid or duplicate line.</summary>
    public IReadOnlyList<ParameterSheetDiagnostic> Diagnostics => parseResult.Diagnostics;

    /// <summary>Gets the smallest `#N` (1..999) not yet defined by the current text.</summary>
    public int NextFreeNumber
    {
        get
        {
            for (var number = 1; number <= 999; number++)
            {
                if (!parseResult.Entries.ContainsKey(number)) return number;
            }

            return 999;
        }
    }

    /// <summary>
    /// Appends one `#N := …` line to the text, built in the existing expression grammar: `{tag}` for a Tag,
    /// a quoted string for a Literal, the expression text as typed for an Expression. The existing grammar
    /// has no quote-escape inside a string, so a Literal containing `"` is refused rather than producing a
    /// line the parser could never read back.
    /// </summary>
    public void AppendRule(int number, ParameterValueKind kind, string value)
    {
        if (number < 1 || number > 999)
            throw new ArgumentOutOfRangeException(nameof(number), number, "Le numéro de paramètre doit être compris entre 1 et 999.");
        ArgumentNullException.ThrowIfNull(value);

        var rightHandSide = kind switch
        {
            ParameterValueKind.Tag => $"{{{value}}}",
            ParameterValueKind.Literal => FormatLiteral(value),
            ParameterValueKind.Expression => value,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Nature de règle inconnue.")
        };

        var line = $"#{number} := {rightHandSide}";
        var current = text;
        var needsNewline = current.Length > 0 && current[^1] != '\n';
        Text = current.Length == 0 ? line : $"{current}{(needsNewline ? "\n" : string.Empty)}{line}";
    }

    /// <summary>Returns the sheet with its original key and this view-model's current text.</summary>
    public ParameterSheet ToSheet() => new(sheetKey, code, displayName, text);

    private static string FormatLiteral(string value)
    {
        if (value.Contains('"'))
        {
            throw new ArgumentException(
                "Un littéral ne peut pas contenir de guillemet : la grammaire des feuilles n'a pas d'échappement.",
                nameof(value));
        }

        return $"\"{value}\"";
    }

    private void OnChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
