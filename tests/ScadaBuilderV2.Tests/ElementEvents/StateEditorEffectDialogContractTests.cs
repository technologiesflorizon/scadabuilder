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
        var selectionHandlerIndex = source.IndexOf("private void OnTypeSelectionChanged", StringComparison.Ordinal);
        var defaultPopulateIndex = source.IndexOf("PopulateFields(ScadaEffectBlock.Empty)", selectionHandlerIndex, StringComparison.Ordinal);

        Assert.IsTrue(selectionHandlerIndex >= 0, "EffectEditorDialog must handle add-mode type changes.");
        Assert.IsTrue(defaultPopulateIndex > selectionHandlerIndex, "Changing the add-mode type must initialize defaults for the selected effect kind.");
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

    [TestMethod]
    public void ElementStateRuleDialog_BuildExpression_ComposesThroughTheSharedTagExpressionComposer()
    {
        // Fix round 2 (bug report): composing `{tag.DisplayName}` directly broke the moment a QuickWindow
        // page's #N catalog put the parameter sheet's source description in DisplayName (fix round 1) -
        // `{#1 — {BP_1A_AUTO}}` is unparsable. BuildExpressionFromVariable now delegates to
        // ScadaTagExpressionComposer.ComposeVariableExpression, which always composes from Id
        // (see tests/ScadaBuilderV2.Tests/QuickWindows/ScadaTagExpressionComposerTests.cs).
        var source = ReadAppFile("ElementStateRuleDialog.xaml.cs");
        Assert.IsFalse(
            source.Contains("$\"{{{tag.DisplayName}}}"),
            "BuildExpressionFromVariable must never compose `{tag.DisplayName}` directly again.");
        StringAssert.Contains(source, "ScadaTagExpressionComposer.ComposeVariableExpression(");
    }

    [TestMethod]
    public void ElementStateRuleDialog_SelectTagByName_PrimaryById()
    {
        var source = ReadAppFile("ElementStateRuleDialog.xaml.cs");
        var primaryById = source.IndexOf("string.Equals(item.TagId, tagName", StringComparison.Ordinal);
        Assert.IsTrue(primaryById >= 0,
            "SelectTagByName must match by tag Id first.");
    }

    [TestMethod]
    public void ElementStateRuleDialog_OnSave_InjectsTagIdIntoAst()
    {
        var source = ReadAppFile("ElementStateRuleDialog.xaml.cs");
        var usesFromAst = source.Contains("FromAst");
        Assert.IsTrue(usesFromAst,
            "OnSaveClick must use ScadaExpression.FromAst to inject TagId into the AST.");
    }

    [TestMethod]
    public void ElementStateRuleDialog_ResolveTagIds_UsesTryResolvePerRef()
    {
        var source = ReadAppFile("ElementStateRuleDialog.xaml.cs");
        var hasBlindInject = source.Contains("InjectTagIds(node, tag.Id)");
        Assert.IsFalse(hasBlindInject,
            "Must resolve each TagRef individually, not inject the same TagId everywhere.");
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
