using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.TableEditor;

internal sealed record TableCreationOptions(int Rows, int Columns, bool FirstRowIsHeader);

internal sealed class TableCreationDialog : Window
{
    private readonly TextBox rows = new() { Text = "6", MinWidth = 90 };
    private readonly TextBox columns = new() { Text = "8", MinWidth = 90 };
    private readonly CheckBox header = new() { Content = "Premiere rangee comme en-tete", IsChecked = true };
    public TableCreationOptions? Result { get; private set; }

    public TableCreationDialog()
    {
        Title = "Nouveau tableau"; Width = 360; Height = 250; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        Content = DialogLayout.Create(
            ("Rangees (1 a 64)", rows), ("Colonnes (1 a 64)", columns), ("", header),
            (Action)Save, (Action)(() => DialogResult = false));
    }

    private void Save()
    {
        if (!int.TryParse(rows.Text, out var rowCount) || rowCount is < 1 or > 64 ||
            !int.TryParse(columns.Text, out var columnCount) || columnCount is < 1 or > 64)
        {
            MessageBox.Show(this, "Les rangees et colonnes doivent etre comprises entre 1 et 64.", "Tableau", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Result = new(rowCount, columnCount, header.IsChecked == true);
        DialogResult = true;
    }
}

internal sealed class TrackSizeDialog : Window
{
    private readonly TextBox value = new() { MinWidth = 110 };
    private readonly double minimum;
    public double? Result { get; private set; }

    public TrackSizeDialog(string title, double current, double minimumValue)
    {
        Title = title; Width = 330; Height = 190; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        minimum = minimumValue; value.Text = current.ToString("0.###", CultureInfo.CurrentCulture);
        Content = DialogLayout.Create(($"Valeur minimale: {minimumValue:0.###} px", value), (Action)Save, (Action)(() => DialogResult = false));
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
    public ScadaTableFormat? Result { get; private set; }

    public CellFormatDialog(ScadaTableFormat current)
    {
        Title = "Format de cellule"; Width = 400; Height = 430; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        background.Text = current.Background ?? ""; foreground.Text = current.Foreground ?? ""; gridColor.Text = current.GridColor ?? "";
        gridWidth.Text = (current.GridWidth ?? 1).ToString("0.###", CultureInfo.CurrentCulture); horizontal.SelectedItem = current.HorizontalAlignment ?? ScadaTableHorizontalAlignment.Left;
        Content = DialogLayout.Create(("Couleur de fond", background), ("Couleur du texte", foreground), ("Couleur de grille", gridColor),
            ("Epaisseur de grille", gridWidth), ("Alignement horizontal", horizontal), (Action)Save, (Action)(() => DialogResult = false));
    }

    private void Save()
    {
        if (!double.TryParse(gridWidth.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var width) || width < 0)
        {
            MessageBox.Show(this, "L'epaisseur de grille doit etre un nombre positif.", Title, MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        Result = new(background.Text.Trim(), foreground.Text.Trim(), gridColor.Text.Trim(), width,
            HorizontalAlignment: horizontal.SelectedItem is ScadaTableHorizontalAlignment alignment ? alignment : ScadaTableHorizontalAlignment.Left);
        DialogResult = true;
    }
}

internal sealed class TablePropertiesDialog : Window
{
    private readonly TextBox baseBackground = new();
    private readonly TextBox headerBackground = new();
    private readonly TextBox alternateBackground = new();
    private readonly TextBlock dimensions = new();
    public ScadaTableStyle? Result { get; private set; }

    public TablePropertiesDialog(ScadaTableDefinition table)
    {
        Title = "Proprietes du tableau"; Width = 430; Height = 390; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        dimensions.Text = $"{table.EffectiveRows.Count} rangees x {table.EffectiveColumns.Count} colonnes — {table.Width:0.##} x {table.Height:0.##} px";
        baseBackground.Text = table.EffectiveStyle.Base?.Background ?? "";
        headerBackground.Text = table.EffectiveStyle.Header?.Background ?? "";
        alternateBackground.Text = table.EffectiveStyle.AlternatingRows?.Background ?? "";
        Content = DialogLayout.Create(("Dimensions", dimensions), ("Fond du tableau", baseBackground), ("Fond de l'en-tete", headerBackground),
            ("Fond des rangees alternees", alternateBackground), (Action)Save, (Action)(() => DialogResult = false));
    }

    private void Save()
    {
        Result = new(new ScadaTableFormat(Background: baseBackground.Text.Trim()),
            new ScadaTableFormat(Background: headerBackground.Text.Trim(), FontWeight: "Bold"),
            new ScadaTableFormat(Background: alternateBackground.Text.Trim()));
        DialogResult = true;
    }
}

internal static class DialogLayout
{
    public static FrameworkElement Create(params object[] entries)
    {
        var panel = new StackPanel { Margin = new Thickness(18) };
        foreach (var item in entries)
        {
            if (item is ValueTuple<string, Control> field)
            {
                if (!string.IsNullOrWhiteSpace(field.Item1)) panel.Children.Add(new TextBlock { Text = field.Item1, Margin = new Thickness(0, 3, 0, 3) });
                field.Item2.Margin = new Thickness(0, 0, 0, 9); panel.Children.Add(field.Item2);
            }
        }
        var save = entries.OfType<Action>().First();
        var cancel = entries.OfType<Action>().Skip(1).First();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancelButton = new Button { Content = "Annuler", MinWidth = 88, Margin = new Thickness(0, 0, 8, 0) };
        var saveButton = new Button { Content = "Enregistrer", MinWidth = 100, IsDefault = true };
        cancelButton.Click += (_, _) => cancel(); saveButton.Click += (_, _) => save(); buttons.Children.Add(cancelButton); buttons.Children.Add(saveButton); panel.Children.Add(buttons);
        return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
