using ScadaBuilderV2.Application.Conversion;
using ScadaBuilderV2.Domain.Editor;
using ScadaBuilderV2.Domain.Elements;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class ElementPlusLegacyConverterTests
{
    [TestMethod]
    public void Text27CanBeConvertedToReadOnlyNumericElement()
    {
        var source = new LegacyDetectedObject(
            "793",
            "Text27",
            "Text",
            "####",
            true,
            new SceneBounds(75, 179, 41, 24),
            new LegacyObjectStyle("Segoe UI", 12, "rgb(255, 255, 255)", "rgba(0, 0, 0, 0)"));

        var element = ElementPlusLegacyConverter.Convert(
            source,
            ElementPlusConversionTarget.NumericReadOnly,
            new ElementPlusConversionOptions(
                "elementplus_numeric_display_793",
                "Element+ Text27",
                "Wonderware/ArchestrA",
                "win00008",
                "dist/pages/win00008.html"));

        Assert.AreEqual(ScadaElementKind.InputNumeric, element.Kind);
        Assert.AreEqual(75, element.Bounds.X);
        Assert.AreEqual(179, element.Bounds.Y);
        Assert.AreEqual(41, element.Bounds.Width);
        Assert.AreEqual(24, element.Bounds.Height);
        Assert.IsTrue(element.Data?.IsReadOnly ?? false);
        Assert.AreEqual("####", element.Data?.DisplayFormat);
        Assert.AreEqual("793", element.LegacySource?.SourceElementId);
        Assert.AreEqual("Text27", element.LegacySource?.SourceElementName);
    }

    [TestMethod]
    public void NumericEditableUsesSameRuntimeKindWithReadOnlyFalse()
    {
        var source = new LegacyDetectedObject(
            "793",
            "Text27",
            "Text",
            "####",
            true,
            new SceneBounds(75, 179, 41, 24),
            new LegacyObjectStyle("", 0, "", ""));

        var display = ElementPlusLegacyConverter.Convert(
            source,
            ElementPlusConversionTarget.NumericReadOnly,
            new ElementPlusConversionOptions("numeric_display", "NumericDisplay", "Wonderware/ArchestrA", "win00008", null));
        var editable = ElementPlusLegacyConverter.Convert(
            source,
            ElementPlusConversionTarget.NumericEditable,
            new ElementPlusConversionOptions("numeric_editable", "NumericEditable", "Wonderware/ArchestrA", "win00008", null));

        Assert.AreEqual(display.Kind, editable.Kind);
        Assert.AreEqual(ScadaElementKind.InputNumeric, editable.Kind);
        Assert.IsTrue(display.Data?.IsReadOnly ?? false);
        Assert.IsFalse(editable.Data?.IsReadOnly ?? true);
    }

    [TestMethod]
    public void NumericConversionInstantiatesConcreteNumericInput()
    {
        var source = new LegacyDetectedObject(
            "793",
            "Text27",
            "Text",
            "####",
            true,
            new SceneBounds(75, 179, 41, 24),
            new LegacyObjectStyle("Segoe UI", 12, "rgb(255, 255, 255)", "rgba(0, 0, 0, 0)"));

        var element = ElementPlusLegacyConverter.ConvertToElement(
            source,
            ElementPlusConversionTarget.NumericReadOnly,
            new ElementPlusConversionOptions("numeric_text27", "Text27 Numeric", "Wonderware/ArchestrA", "win00008", null));

        Assert.IsInstanceOfType(element, typeof(NumericInput));
        var numeric = (NumericInput)element;
        Assert.IsTrue(numeric.IsReadOnly);
        Assert.AreEqual("####", numeric.DisplayFormat);
        Assert.AreEqual("####", numeric.DisplayText);
        StringAssert.Contains(numeric.HtmlCode, "scada-numeric-input");
        StringAssert.Contains(numeric.HtmlCode, "####");

        var adapter = numeric.ToScadaElement();
        Assert.AreEqual(ScadaElementKind.InputNumeric, adapter.Kind);
        Assert.AreEqual("####", adapter.Data?.DisplayFormat);
        Assert.IsTrue(adapter.Data?.IsReadOnly ?? false);
    }
}
