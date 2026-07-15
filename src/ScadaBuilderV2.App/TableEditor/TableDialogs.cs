using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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
    private readonly TextBox background = new();
    private readonly TextBox foreground = new();
    private readonly TextBox gridColor = new();
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
    public ScadaTableFormat? Result { get; private set; }

    public CellFormatDialog(ScadaTableFormat current)
    {
        Title = "Format de cellule"; Width = 430; Height = 720; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.CanResize;
        background.Text = current.Background ?? ""; foreground.Text = current.Foreground ?? ""; gridColor.Text = current.GridColor ?? "";
        gridWidth.Text = (current.GridWidth ?? 1).ToString("0.###", CultureInfo.CurrentCulture); horizontal.SelectedItem = current.HorizontalAlignment ?? ScadaTableHorizontalAlignment.Left;
        vertical.SelectedItem = current.VerticalAlignment ?? ScadaTableVerticalAlignment.Middle; gridStyle.SelectedItem = current.GridStyle ?? ScadaTableGridStyle.Solid;
        padding.Text = (current.Padding ?? 4).ToString("0.###", CultureInfo.CurrentCulture); fontFamily.Text = current.FontFamily ?? "Segoe UI";
        fontSize.Text = (current.FontSize ?? 14).ToString("0.###", CultureInfo.CurrentCulture); bold.IsChecked = string.Equals(current.FontWeight, "Bold", StringComparison.OrdinalIgnoreCase);
        italic.IsChecked = string.Equals(current.FontStyle, "Italic", StringComparison.OrdinalIgnoreCase); wrap.IsChecked = current.TextWrap == true;
        lineHeight.Text = current.LineHeight?.ToString("0.###", CultureInfo.CurrentCulture) ?? "";
        Content = DialogLayout.Create(
            Save,
            () => DialogResult = false,
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
            ("Hauteur de ligne (vide = heriter)", lineHeight));
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
        Result = new(NullWhenBlank(background.Text), NullWhenBlank(foreground.Text), NullWhenBlank(gridColor.Text), width,
            gridStyle.SelectedItem is ScadaTableGridStyle gs ? gs : ScadaTableGridStyle.Solid,
            horizontal.SelectedItem is ScadaTableHorizontalAlignment alignment ? alignment : ScadaTableHorizontalAlignment.Left,
            vertical.SelectedItem is ScadaTableVerticalAlignment va ? va : ScadaTableVerticalAlignment.Middle,
            pad, NullWhenBlank(fontFamily.Text), size, bold.IsChecked == true ? "Bold" : "Normal", italic.IsChecked == true ? "Italic" : "Normal", wrap.IsChecked == true, line);
        DialogResult = true;
    }

    private static string? NullWhenBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
    private readonly TextBox color = new() { Text = "#8AA0A6" }; private readonly TextBox width = new() { Text = "1" };
    public (ScadaTableBorderPreset Preset, ScadaTableBorder Border)? Result { get; private set; }
    public TableBorderDialog() { Title="Bordures"; Width=380; Height=360; WindowStartupLocation=WindowStartupLocation.CenterOwner; preset.SelectedItem=ScadaTableBorderPreset.All; style.SelectedItem=ScadaTableGridStyle.Solid; Content=DialogLayout.Create(Save,()=>DialogResult=false,("Appliquer",preset),("Style",style),("Couleur",color),("Epaisseur",width)); }
    private void Save() { if (!double.TryParse(width.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var w) || w<0 || string.IsNullOrWhiteSpace(color.Text)) { MessageBox.Show(this,"Bordure invalide.",Title); return; } Result=((ScadaTableBorderPreset)preset.SelectedItem,(new ScadaTableBorder((ScadaTableGridStyle)style.SelectedItem,color.Text.Trim(),w))); DialogResult=true; }
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
    private readonly TextBox baseBackground = new();
    private readonly TextBox headerBackground = new();
    private readonly TextBox alternateBackground = new();
    private readonly TextBlock dimensions = new();
    public ScadaTableStyle? Result { get; private set; }

    public TablePropertiesDialog(ScadaTableDefinition table)
    {
        currentStyle = table.EffectiveStyle;
        Title = "Proprietes du tableau"; Width = 430; Height = 390; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        dimensions.Text = $"{table.EffectiveRows.Count} rangees x {table.EffectiveColumns.Count} colonnes — {table.Width:0.##} x {table.Height:0.##} px";
        baseBackground.Text = table.EffectiveStyle.Base?.Background ?? "";
        headerBackground.Text = table.EffectiveStyle.Header?.Background ?? "";
        alternateBackground.Text = table.EffectiveStyle.AlternatingRows?.Background ?? "";
        Content = DialogLayout.Create(
            Save,
            () => DialogResult = false,
            ("Dimensions", dimensions),
            ("Fond du tableau", baseBackground),
            ("Fond de l'en-tete", headerBackground),
            ("Fond des rangees alternees", alternateBackground));
    }

    private void Save()
    {
        Result = new(
            (currentStyle.Base ?? new ScadaTableFormat()) with { Background = NullWhenBlank(baseBackground.Text) },
            (currentStyle.Header ?? new ScadaTableFormat()) with { Background = NullWhenBlank(headerBackground.Text) },
            (currentStyle.AlternatingRows ?? new ScadaTableFormat()) with { Background = NullWhenBlank(alternateBackground.Text) });
        DialogResult = true;
    }

    private static string? NullWhenBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

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
