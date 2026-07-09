namespace ScadaBuilderV2.Tests.ElementEvents;

[TestClass]
public sealed class StateEditorEffectDialogContractTests
{
    [TestMethod]
    public void ColorPickerDialog_PreservesHueFallbackForAchromaticColors()
    {
        var source = ReadAppFile("ColorPickerDialog.xaml.cs");

        StringAssert.Contains(source, "ToHsv(Color color, double fallbackHue = 0)");
        StringAssert.Contains(source, "? fallbackHue");
        StringAssert.Contains(source, "ToHsv(color, fallbackHue: _hue)");
    }

    [TestMethod]
    public void ColorPickerDialog_HueSliderEscapesGrayWhiteAndBlack()
    {
        var source = ReadAppFile("ColorPickerDialog.xaml.cs");

        StringAssert.Contains(source, "if (_saturation <= 0) _saturation = 1.0;");
        StringAssert.Contains(source, "if (_value <= 0) _value = 1.0;");
        StringAssert.Contains(source, "FromHsv(_hue, _saturation, _value)");
    }

    [TestMethod]
    public void EffectEditorDialog_AddModeInitializesDefaultValues()
    {
        var source = ReadAppFile("EffectEditorDialog.xaml.cs");

        StringAssert.Contains(source, "PopulateFields(ScadaEffectBlock.Empty)");
        StringAssert.Contains(source, "OpacitySlider.Value = effect.Opacity ?? 1.0");
        StringAssert.Contains(source, "FilterOpacitySlider.Value = effect.ColorFilterOpacity ?? 1.0");
        StringAssert.Contains(source, "FilterColorPicker.SetColor(effect.ColorFilterColor ?? \"#E53935\")");
    }

    [TestMethod]
    public void ElementStateRuleDialog_RemovesInlineEffectEditorControls()
    {
        var xaml = ReadAppFile("ElementStateRuleDialog.xaml");
        var code = ReadAppFile("ElementStateRuleDialog.xaml.cs");

        Assert.IsFalse(xaml.Contains("EffectEditorPanel", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("EffectTypeComboBox", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("ShowEffectEditor", StringComparison.Ordinal));
        StringAssert.Contains(code, "Dictionary<EffectKind, ScadaEffectBlock>");
        StringAssert.Contains(code, "new EffectEditorDialog(");
    }

    [TestMethod]
    public void EffectEditorDialog_ExposesRequiredResultContract()
    {
        var source = ReadAppFile("EffectEditorDialog.xaml.cs");

        StringAssert.Contains(source, "public EffectKind ResultKind");
        StringAssert.Contains(source, "public ScadaEffectBlock ResultEffect");
        StringAssert.Contains(source, "ColorPickerDialog.TryParseCssColor");
    }

    private static string ReadAppFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ScadaBuilderV2.App", fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate src/ScadaBuilderV2.App/{fileName} from test output directory.");
        return string.Empty;
    }
}
