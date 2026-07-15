using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Application.Tables;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.TableEditor;

internal sealed class TrackSizeDialog : Window
{
    private readonly TextBox value = new() { MinWidth = 110 };
    private readonly double minimum;
    public double? Result { get; private set; }

    public TrackSizeDialog(string title, double current, double minimumValue)
    {
        Title = title; Width = 330; Height = 190; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        minimum = minimumValue; value.Text = current.ToString("0.###", CultureInfo.CurrentCulture);
        Content = DialogLayout.Create(
            Save,
            () => DialogResult = false,
            ($"Valeur minimale: {minimumValue:0.###} px", value));
    }

    private void Save()
    {
        if (!double.TryParse(value.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) || parsed < minimum)
        {
            MessageBox.Show(this, $"La valeur doit etre superieure ou egale a {minimum:0.###} px.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result = parsed; DialogResult = true;
    }
}

internal sealed class CellFormatDialog : Window
{
    private readonly ColorPickerField background = new();
    private readonly ColorPickerField foreground = new();
    private readonly ColorPickerField gridColor = new();
    private readonly TextBox gridWidth = new();
    private readonly ComboBox horizontal = new() { ItemsSource = Enum.GetValues<ScadaTableHorizontalAlignment>() };
    private readonly ComboBox vertical = new() { ItemsSource = Enum.GetValues<ScadaTableVerticalAlignment>() };
    private readonly ComboBox gridStyle = new() { ItemsSource = Enum.GetValues<ScadaTableGridStyle>() };
    private readonly TextBox padding = new();
    private readonly TextBox fontFamily = new();
    private readonly TextBox fontSize = new();
    private readonly CheckBox bold = new() { Content = "Gras" };
    private readonly CheckBox italic = new() { Content = "Italique" };
    private readonly CheckBox wrap = new() { Content = "Retour a la ligne" };
    private readonly TextBox lineHeight = new();
    private readonly ComboBox resetProperty = new() { ItemsSource = TableFormatPropertyChoices.All, DisplayMemberPath = nameof(TableFormatPropertyChoice.Label), SelectedValuePath = nameof(TableFormatPropertyChoice.PropertyName) };
    private readonly TextBlock state = new() { FontWeight = System.Windows.FontWeights.SemiBold };
    public TableFormatDialogResult? Result { get; private set; }

    public CellFormatDialog(TableFormatInspection inspection)
    {
        var current = inspection.EffectiveFormat;
        Title = "Format de cellule"; Width = 430; Height = 720; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResize;
        background.SetColor(current.Background ?? "#FFFFFF"); foreground.SetColor(current.Foreground ?? "#0F2A30"); gridColor.SetColor(current.GridColor ?? "#8AA0A6");
        gridWidth.Text = (current.GridWidth ?? 1).ToString("0.###", CultureInfo.CurrentCulture); horizontal.SelectedItem = current.HorizontalAlignment ?? ScadaTableHorizontalAlignment.Left;
        vertical.SelectedItem = current.VerticalAlignment ?? ScadaTableVerticalAlignment.Middle; gridStyle.SelectedItem = current.GridStyle ?? ScadaTableGridStyle.Solid;
        padding.Text = (current.Padding ?? 4).ToString("0.###", CultureInfo.CurrentCulture); fontFamily.Text = current.FontFamily ?? "Segoe UI";
        fontSize.Text = (current.FontSize ?? 14).ToString("0.###", CultureInfo.CurrentCulture); bold.IsChecked = string.Equals(current.FontWeight, "Bold", StringComparison.OrdinalIgnoreCase);
        italic.IsChecked = string.Equals(current.FontStyle, "Italic", StringComparison.OrdinalIgnoreCase); wrap.IsChecked = current.TextWrap == true;
        lineHeight.Text = current.LineHeight?.ToString("0.###", CultureInfo.CurrentCulture) ?? "";
        state.Text = inspection.State switch { TablePropertyValueState.Inherited => "Hérité", TablePropertyValueState.Custom => "Personnalisé", _ => "Mixte" };
        resetProperty.SelectedIndex = 0;
        Content = DialogLayout.Create(
            Save,
            () => DialogResult = false,
            ("État de la portée", state),
            ("Couleur de fond", background),
            ("Couleur du texte", foreground),
            ("Couleur de grille", gridColor),
            ("Epaisseur de grille", gridWidth),
            ("Style de grille", gridStyle),
            ("Alignement horizontal", horizontal),
            ("Alignement vertical", vertical),
            ("Padding", padding),
            ("Police", fontFamily),
            ("Taille", fontSize),
            ("", bold), ("", italic), ("", wrap),
            ("Hauteur de ligne (vide = heriter)", lineHeight),
            ("Propriété à remettre en héritage", resetProperty),
            ("", CreateResetButton()));
    }

    private void Save()
    {
        if (!double.TryParse(gridWidth.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var width) || width < 0)
        {
            MessageBox.Show(this, "L'epaisseur de grille doit etre un nombre positif.", Title, MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        if (!double.TryParse(padding.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var pad) || pad < 0 ||
            !double.TryParse(fontSize.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var size) || size <= 0 ||
            (!string.IsNullOrWhiteSpace(lineHeight.Text) && (!double.TryParse(lineHeight.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsedLine) || parsedLine <= 0)))
        { MessageBox.Show(this, "Padding, taille de police ou hauteur de ligne invalide.", Title, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        double? line = string.IsNullOrWhiteSpace(lineHeight.Text) ? null : double.Parse(lineHeight.Text, NumberStyles.Float, CultureInfo.CurrentCulture);
        Result = new(TableFormatDialogAction.Apply, new(background.Value, foreground.Value, gridColor.Value, width,
            gridStyle.SelectedItem is ScadaTableGridStyle gs ? gs : ScadaTableGridStyle.Solid,
            horizontal.SelectedItem is ScadaTableHorizontalAlignment alignment ? alignment : ScadaTableHorizontalAlignment.Left,
            vertical.SelectedItem is ScadaTableVerticalAlignment va ? va : ScadaTableVerticalAlignment.Middle,
            pad, NullWhenBlank(fontFamily.Text), size, bold.IsChecked == true ? "Bold" : "Normal", italic.IsChecked == true ? "Italic" : "Normal", wrap.IsChecked == true, line));
        DialogResult = true;
    }

    private Button CreateResetButton()
    {
        var button = new Button { Content = "Hériter / Réinitialiser la propriété", MinHeight = 30 };
        button.Click += (_, _) =>
        {
            if (resetProperty.SelectedValue is string property)
            {
                Result = new(TableFormatDialogAction.ResetProperty, PropertyName: property);
                DialogResult = true;
            }
        };
        return button;
    }

    private static string? NullWhenBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal enum TableFormatDialogAction { Apply, ResetProperty }
internal sealed record TableFormatDialogResult(TableFormatDialogAction Action, ScadaTableFormat? Format = null, string? PropertyName = null);
internal sealed record TableFormatPropertyChoice(string Label, string PropertyName);
internal static class TableFormatPropertyChoices
{
    public static IReadOnlyList<TableFormatPropertyChoice> All { get; } =
    [
        new("Couleur de fond", nameof(ScadaTableFormat.Background)), new("Couleur du texte", nameof(ScadaTableFormat.Foreground)),
        new("Couleur de grille", nameof(ScadaTableFormat.GridColor)), new("Épaisseur de grille", nameof(ScadaTableFormat.GridWidth)),
        new("Style de grille", nameof(ScadaTableFormat.GridStyle)), new("Alignement horizontal", nameof(ScadaTableFormat.HorizontalAlignment)),
        new("Alignement vertical", nameof(ScadaTableFormat.VerticalAlignment)), new("Padding", nameof(ScadaTableFormat.Padding)),
        new("Police", nameof(ScadaTableFormat.FontFamily)), new("Taille", nameof(ScadaTableFormat.FontSize)),
        new("Gras", nameof(ScadaTableFormat.FontWeight)), new("Italique", nameof(ScadaTableFormat.FontStyle)),
        new("Retour à la ligne", nameof(ScadaTableFormat.TextWrap)), new("Hauteur de ligne", nameof(ScadaTableFormat.LineHeight))
    ];
}

internal sealed class CellContentDialog : Window
{
    private readonly ComboBox kind = new() { ItemsSource = Enum.GetValues<ScadaTableCellContentKind>() };
    private readonly TextBox value = new(); private readonly TextBox placeholder = new();
    private readonly TextBox minimum = new(); private readonly TextBox maximum = new(); private readonly TextBox step = new();
    private readonly CheckBox readOnly = new() { Content = "Lecture seule" };
    public ScadaTableCellContent? Result { get; private set; }
    public CellContentDialog(ScadaTableCellContent current)
    {
        Title = "Type et contenu de cellule"; Width = 420; Height = 530; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        kind.SelectedItem = current.Kind; value.Text = current.Kind == ScadaTableCellContentKind.InputNumeric ? current.NumericValue?.ToString(CultureInfo.CurrentCulture) ?? "" : current.Text;
        placeholder.Text = current.Placeholder; minimum.Text = F(current.Minimum); maximum.Text = F(current.Maximum); step.Text = F(current.Step); readOnly.IsChecked = current.IsReadOnly;
        Content = DialogLayout.Create(Save, () => DialogResult = false, ("Type", kind), ("Valeur initiale", value), ("Placeholder", placeholder), ("Minimum", minimum), ("Maximum", maximum), ("Pas", step), ("", readOnly));
    }
    private void Save()
    {
        var selected = kind.SelectedItem is ScadaTableCellContentKind k ? k : ScadaTableCellContentKind.Text;
        if (!TryNullable(minimum.Text, out var min) || !TryNullable(maximum.Text, out var max) || !TryNullable(step.Text, out var increment) || (selected == ScadaTableCellContentKind.InputNumeric && !TryNullable(value.Text, out var numeric)))
        { MessageBox.Show(this, "Une valeur numerique est invalide.", Title, MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        double? number = null; if (selected == ScadaTableCellContentKind.InputNumeric) TryNullable(value.Text, out number);
        Result = selected switch { ScadaTableCellContentKind.Text => new(selected, Text:value.Text), ScadaTableCellContentKind.InputText => new(selected, Text:value.Text, Placeholder:placeholder.Text, IsReadOnly:readOnly.IsChecked==true), _ => new(selected, Placeholder:placeholder.Text, NumericValue:number, Minimum:min, Maximum:max, Step:increment, IsReadOnly:readOnly.IsChecked==true) };
        DialogResult = true;
    }
    private static string F(double? x) => x?.ToString(CultureInfo.CurrentCulture) ?? "";
    private static bool TryNullable(string text, out double? value) { value=null; if (string.IsNullOrWhiteSpace(text)) return true; if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var n)) return false; value=n; return true; }
}

internal sealed class TableBorderDialog : Window
{
    private readonly ComboBox preset = new() { ItemsSource = Enum.GetValues<ScadaTableBorderPreset>() };
    private readonly ComboBox style = new() { ItemsSource = Enum.GetValues<ScadaTableGridStyle>() };
    private readonly ColorPickerField color = new() { Value = "#8AA0A6" }; private readonly TextBox width = new() { Text = "1" };
    public (ScadaTableBorderPreset Preset, ScadaTableBorder Border)? Result { get; private set; }
    public TableBorderDialog() { Title="Bordures"; Width=380; Height=360; WindowStartupLocation=WindowStartupLocation.CenterOwner; preset.SelectedItem=ScadaTableBorderPreset.All; style.SelectedItem=ScadaTableGridStyle.Solid; Content=DialogLayout.Create(Save,()=>DialogResult=false,("Appliquer",preset),("Style",style),("Couleur",color),("Epaisseur",width)); }
    private void Save() { if (!double.TryParse(width.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var w) || w<0 || string.IsNullOrWhiteSpace(color.Value)) { MessageBox.Show(this,"Bordure invalide.",Title); return; } Result=((ScadaTableBorderPreset)preset.SelectedItem,(new ScadaTableBorder((ScadaTableGridStyle)style.SelectedItem,color.Value.Trim(),w))); DialogResult=true; }
}

internal sealed class HeaderRowsDialog : Window
{
    private readonly TextBox value = new(); private readonly int maximum; public int? Result { get; private set; }
    public HeaderRowsDialog(int current, int maximum) { this.maximum=maximum; value.Text=current.ToString(CultureInfo.CurrentCulture); Title="Rangees d'en-tete"; Width=350; Height=190; WindowStartupLocation=WindowStartupLocation.CenterOwner; Content=DialogLayout.Create(Save,()=>DialogResult=false,($"Nombre (0 a {maximum})",value)); }
    private void Save() { if (!int.TryParse(value.Text,out var n)||n<0||n>maximum) { MessageBox.Show(this,"Nombre de rangees invalide.",Title); return; } Result=n; DialogResult=true; }
}

internal sealed class TablePropertiesDialog : Window
{
    private readonly ScadaTableStyle currentStyle;
    private readonly ColorPickerField baseBackground = new();
    private readonly ColorPickerField headerBackground = new();
    private readonly ColorPickerField alternateBackground = new();
    private readonly TextBox x = new();
    private readonly TextBox y = new();
    private readonly TextBox width = new();
    private readonly TextBox height = new();
    private readonly TextBlock dimensions = new();
    private readonly int rowCount;
    private readonly int columnCount;
    public TablePropertiesDialogResult? Result { get; private set; }

    public TablePropertiesDialog(ScadaElement element)
    {
        var table = element.Table ?? throw new ArgumentException("A table Element+ is required.", nameof(element));
        currentStyle = table.EffectiveStyle;
        rowCount = table.EffectiveRows.Count;
        columnCount = table.EffectiveColumns.Count;
        Title = "Proprietes du tableau"; Width = 460; Height = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResize;
        dimensions.Text = $"{table.EffectiveRows.Count} rangees x {table.EffectiveColumns.Count} colonnes — {table.Width:0.##} x {table.Height:0.##} px";
        baseBackground.SetColor(table.EffectiveStyle.Base?.Background ?? "#FFFFFF");
        headerBackground.SetColor(table.EffectiveStyle.Header?.Background ?? "#EAF5F7");
        alternateBackground.SetColor(table.EffectiveStyle.AlternatingRows?.Background ?? "#F6FAFB");
        x.Text = element.Bounds.X.ToString("0.###", CultureInfo.CurrentCulture);
        y.Text = element.Bounds.Y.ToString("0.###", CultureInfo.CurrentCulture);
        width.Text = table.Width.ToString("0.###", CultureInfo.CurrentCulture);
        height.Text = table.Height.ToString("0.###", CultureInfo.CurrentCulture);
        Content = DialogLayout.Create(
            Save,
            () => DialogResult = false,
            ("Dimensions", dimensions),
            ("Position X", x),
            ("Position Y", y),
            ("Largeur exacte", width),
            ("Hauteur exacte", height),
            ("Fond du tableau", baseBackground),
            ("Fond de l'en-tete", headerBackground),
            ("Fond des rangees alternees", alternateBackground));
    }

    private void Save()
    {
        if (!TryNumber(x.Text, out var parsedX) || !TryNumber(y.Text, out var parsedY) ||
            !TryNumber(width.Text, out var parsedWidth) || !TryNumber(height.Text, out var parsedHeight) ||
            parsedX < 0 || parsedY < 0 ||
            parsedWidth < columnCount * ScadaTableDefinition.MinimumColumnWidth ||
            parsedHeight < rowCount * ScadaTableDefinition.MinimumRowHeight)
        {
            MessageBox.Show(this, "Position ou dimensions invalides pour les minimums des pistes.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new(parsedX, parsedY, parsedWidth, parsedHeight, new(
            (currentStyle.Base ?? new ScadaTableFormat()) with { Background = baseBackground.Value },
            (currentStyle.Header ?? new ScadaTableFormat()) with { Background = headerBackground.Value },
            (currentStyle.AlternatingRows ?? new ScadaTableFormat()) with { Background = alternateBackground.Value }));
        DialogResult = true;
    }

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && double.IsFinite(value);
}

internal sealed record TablePropertiesDialogResult(double X, double Y, double Width, double Height, ScadaTableStyle Style);

internal static class DialogLayout
{
    public static FrameworkElement Create(
        Action save,
        Action cancel,
        params (string Label, FrameworkElement Control)[] fields)
    {
        var panel = new StackPanel { Margin = new Thickness(18) };
        foreach (var field in fields)
        {
            if (!string.IsNullOrWhiteSpace(field.Label)) panel.Children.Add(new TextBlock { Text = field.Label, Margin = new Thickness(0, 3, 0, 3) });
            field.Control.Margin = new Thickness(0, 0, 0, 9); panel.Children.Add(field.Control);
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancelButton = new Button { Content = "Annuler", MinWidth = 88, Margin = new Thickness(0, 0, 8, 0) };
        var saveButton = new Button { Content = "Enregistrer", MinWidth = 100, IsDefault = true };
        cancelButton.Click += (_, _) => cancel(); saveButton.Click += (_, _) => save(); buttons.Children.Add(cancelButton); buttons.Children.Add(saveButton); panel.Children.Add(buttons);
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
