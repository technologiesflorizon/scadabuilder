using ScadaBuilderV2.Application.Commands;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class RibbonCommandCatalogTests
{
    [TestMethod]
    public void DefaultCatalogDefinesExpectedTopRibbonTabs()
    {
        var tabs = RibbonCommandCatalog.CreateDefault();

        CollectionAssert.AreEqual(
            new[] { "File", "Edit", "Pages", "Screen", "Selection", "Tools", "Insert" },
            tabs.Keys.ToArray());
    }

    [TestMethod]
    public void DefaultCatalogUsesStableUniqueCommandIds()
    {
        var commands = RibbonCommandCatalog.EnumerateCommands(RibbonCommandCatalog.CreateDefault()).ToArray();
        var duplicateIds = commands
            .GroupBy(command => command.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.AreEqual(0, duplicateIds.Length, $"Duplicate ribbon command ids: {string.Join(", ", duplicateIds)}");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "project.save");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "page.new");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "page.validate");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "export.ft100.sb2");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "insert.shape.circle");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "insert.shape.triangle");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "insert.shape.star");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "insert.button.emergency-stop");
        CollectionAssert.Contains(commands.Select(command => command.Id).ToArray(), "insert.table");
    }

    [TestMethod]
    public void InsertCatalogDefinesEightStableFamilies()
    {
        var families = RibbonCommandCatalog.CreateInsertFamilies();

        CollectionAssert.AreEqual(
            new[] { "text-values", "shapes", "process", "electrical", "commands", "data", "charts", "media" },
            families.Select(family => family.Id).ToArray());
        Assert.IsTrue(families.All(family => family.IconKey.StartsWith("Icon.InsertFamily.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void InsertCatalogExposesExecutableModernTableAndExplicitPlannedTools()
    {
        var tools = RibbonCommandCatalog.CreateInsertFamilies().SelectMany(family => family.Tools).ToArray();
        var table = tools.Single(tool => tool.Id == "insert.table");

        Assert.IsTrue(table.IsEnabled);
        Assert.AreEqual(ScadaBuilderV2.Domain.Scenes.ScadaElementKind.Table, table.ElementKind);
        Assert.AreEqual(InsertPlacementMode.ContextualSurface, table.PlacementMode);
        Assert.IsNull(table.DisabledReason);
        Assert.IsTrue(tools.Where(tool => !tool.IsEnabled).All(tool => !string.IsNullOrWhiteSpace(tool.DisabledReason)));
        Assert.AreEqual(tools.Length, tools.Select(tool => tool.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    public void DefaultCatalogRequiresSemanticIconKeys()
    {
        var commands = RibbonCommandCatalog.EnumerateCommands(RibbonCommandCatalog.CreateDefault()).ToArray();
        var invalidIconKeys = commands
            .Where(command => string.IsNullOrWhiteSpace(command.IconKey) || !command.IconKey.StartsWith("Icon.", StringComparison.Ordinal))
            .Select(command => $"{command.Id}:{command.IconKey}")
            .ToArray();

        Assert.AreEqual(0, invalidIconKeys.Length, $"Invalid ribbon icon keys: {string.Join(", ", invalidIconKeys)}");
    }

    [TestMethod]
    public void DisabledCommandsExposeReason()
    {
        var disabledCommands = RibbonCommandCatalog
            .EnumerateCommands(RibbonCommandCatalog.CreateDefault())
            .Concat(RibbonCommandCatalog.CreateToolPalette())
            .Where(command => !command.IsEnabled)
            .ToArray();

        Assert.IsTrue(disabledCommands.Length > 0, "The catalog should keep visible future commands explicit.");
        Assert.IsTrue(disabledCommands.All(command => !string.IsNullOrWhiteSpace(command.DisabledReason)));
    }

    [TestMethod]
    public void SelectionGroupCommandsAreExecutableFromRibbonCatalog()
    {
        var commands = RibbonCommandCatalog
            .EnumerateCommands(RibbonCommandCatalog.CreateDefault())
            .ToDictionary(command => command.Id, StringComparer.Ordinal);

        Assert.IsTrue(commands["object.group"].IsEnabled);
        Assert.IsTrue(commands["object.ungroup"].IsEnabled);
        Assert.IsNull(commands["object.group"].DisabledReason);
        Assert.IsNull(commands["object.ungroup"].DisabledReason);
    }

    [TestMethod]
    public void MainRibbonUsesOnlyDynamicCommandSurface()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(xaml, "x:Name=\"RibbonCommandSurface\"");
        StringAssert.Contains(xaml, "ItemTemplate=\"{StaticResource RibbonGroupTemplate}\"");
        Assert.IsFalse(xaml.Contains("x:Name=\"FileRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"EditRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"InsertRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"ScreenRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"SelectionRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("x:Name=\"ToolsRibbon\"", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("FileRibbon.Visibility", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("EditRibbon.Visibility", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("InsertRibbon.Visibility", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MainRibbonUsesClippingSafeOverflowHeight()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var ribbonSurfaceIndex = xaml.IndexOf("x:Name=\"RibbonCommandSurface\"", StringComparison.Ordinal);

        Assert.IsTrue(ribbonSurfaceIndex >= 0, "Main ribbon command surface was not found.");
        var ribbonSurface = xaml.Substring(ribbonSurfaceIndex, 1000);

        StringAssert.Contains(xaml, "Height=\"212\"");
        StringAssert.Contains(xaml, "<RowDefinition Height=\"172\"/>");
        StringAssert.Contains(xaml, "LargeRibbonCommandTemplate");
        StringAssert.Contains(xaml, "LargeCommandIconStyle");
        StringAssert.Contains(xaml, "<Setter Property=\"Width\" Value=\"26\"/>");
        StringAssert.Contains(xaml, "<Setter Property=\"Height\" Value=\"26\"/>");
        StringAssert.Contains(xaml, "<UniformGrid Columns=\"4\"/>");
        var shapeTemplateStart = xaml.IndexOf("<DataTemplate x:Key=\"LargeRibbonCommandTemplate\">", StringComparison.Ordinal);
        var shapeTemplateEnd = xaml.IndexOf("<DataTemplate x:Key=\"RibbonGroupTemplate\">", StringComparison.Ordinal);
        Assert.IsTrue(shapeTemplateStart >= 0 && shapeTemplateEnd > shapeTemplateStart, "The shape gallery template was not found.");
        var shapeTemplate = xaml.Substring(shapeTemplateStart, shapeTemplateEnd - shapeTemplateStart);
        StringAssert.Contains(shapeTemplate, "<Image Style=\"{StaticResource LargeCommandIconStyle}\" Source=\"{Binding Icon}\"/>");
        Assert.IsFalse(
            shapeTemplate.Contains("<TextBlock", StringComparison.Ordinal),
            "The shape gallery template should be icon-only; shape names remain available through tooltips.");
        StringAssert.Contains(ribbonSurface, "HorizontalScrollBarVisibility=\"Hidden\"");
        StringAssert.Contains(ribbonSurface, "<StackPanel Orientation=\"Horizontal\"/>");
        Assert.IsFalse(
            ribbonSurface.Contains("<WrapPanel/>", StringComparison.Ordinal),
            "The main ribbon command surface must scroll horizontally instead of wrapping into clipped rows.");
        StringAssert.Contains(xaml, "Style=\"{StaticResource RibbonOverflowButtonStyle}\"");
        StringAssert.Contains(xaml, "primitives:ScrollBar.PageLeftCommand");
        StringAssert.Contains(xaml, "primitives:ScrollBar.PageRightCommand");
    }

    [TestMethod]
    public void InsertFamilyRibbonKeepsFirstLevelCompact()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var styleStart = xaml.IndexOf("<Style x:Key=\"InsertFamilyButtonStyle\"", StringComparison.Ordinal);
        var styleEnd = xaml.IndexOf("</Style>", styleStart, StringComparison.Ordinal);
        var templateStart = xaml.IndexOf("<DataTemplate x:Key=\"InsertFamilyTemplate\">", StringComparison.Ordinal);
        var templateEnd = xaml.IndexOf("</DataTemplate>", templateStart, StringComparison.Ordinal);

        Assert.IsTrue(styleStart >= 0 && styleEnd > styleStart, "The compact Insert family style was not found.");
        Assert.IsTrue(templateStart >= 0 && templateEnd > templateStart, "The Insert family template was not found.");

        var style = xaml.Substring(styleStart, styleEnd - styleStart);
        var template = xaml.Substring(templateStart, templateEnd - templateStart);

        StringAssert.Contains(style, "<Setter Property=\"Height\" Value=\"26\"/>");
        StringAssert.Contains(style, "<Setter Property=\"MinWidth\" Value=\"86\"/>");
        StringAssert.Contains(style, "<Setter Property=\"Padding\" Value=\"6,1\"/>");
        StringAssert.Contains(template, "Style=\"{StaticResource InsertFamilyButtonStyle}\"");
        StringAssert.Contains(template, "Width=\"14\" Height=\"14\"");
        Assert.IsFalse(
            template.Contains("RibbonCommandButtonStyle", StringComparison.Ordinal),
            "The Insert family row must keep its dedicated compact presentation.");
    }

    [TestMethod]
    public void SecondLevelRibbonUsesCompactTwoRowCommands()
    {
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var styleStart = xaml.IndexOf("<Style x:Key=\"RibbonCommandButtonStyle\"", StringComparison.Ordinal);
        var styleEnd = xaml.IndexOf("</Style>", styleStart, StringComparison.Ordinal);
        var templateStart = xaml.IndexOf("<DataTemplate x:Key=\"RibbonCommandTemplate\">", StringComparison.Ordinal);
        var templateEnd = xaml.IndexOf("</DataTemplate>", templateStart, StringComparison.Ordinal);
        var groupStart = xaml.IndexOf("<DataTemplate x:Key=\"RibbonGroupTemplate\">", StringComparison.Ordinal);
        var groupEnd = xaml.IndexOf("</DataTemplate>", groupStart, StringComparison.Ordinal);

        Assert.IsTrue(styleStart >= 0 && styleEnd > styleStart, "The second-level ribbon command style was not found.");
        Assert.IsTrue(templateStart >= 0 && templateEnd > templateStart, "The second-level command template was not found.");
        Assert.IsTrue(groupStart >= 0 && groupEnd > groupStart, "The second-level ribbon group template was not found.");

        var style = xaml.Substring(styleStart, styleEnd - styleStart);
        var template = xaml.Substring(templateStart, templateEnd - templateStart);
        var group = xaml.Substring(groupStart, groupEnd - groupStart);

        StringAssert.Contains(style, "<Setter Property=\"Width\" Value=\"104\"/>");
        StringAssert.Contains(style, "<Setter Property=\"Height\" Value=\"28\"/>");
        StringAssert.Contains(style, "<Setter Property=\"Padding\" Value=\"5,2\"/>");
        StringAssert.Contains(template, "<StackPanel Orientation=\"Horizontal\">");
        StringAssert.Contains(template, "FontSize=\"10.5\"");
        StringAssert.Contains(template, "TextTrimming=\"CharacterEllipsis\"");
        StringAssert.Contains(group, "<UniformGrid Rows=\"2\"/>");
    }

    [TestMethod]
    public void ToolPaletteUsesSemanticCommandCatalog()
    {
        var commands = RibbonCommandCatalog.CreateToolPalette().ToArray();
        var ids = commands.Select(command => command.Id).ToArray();
        var xaml = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var code = ReadProjectFile("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        CollectionAssert.AreEqual(
            new[]
            {
                "tool.select",
                "tool.move",
                "tool.text",
                "tool.image",
                "tool.group",
                "tool.zoom"
            },
            ids);
        Assert.IsTrue(commands.All(command => command.IconKey.StartsWith("Icon.Tool.", StringComparison.Ordinal)));
        StringAssert.Contains(xaml, "ItemsSource=\"{Binding ToolPaletteCommands}\"");
        StringAssert.Contains(xaml, "ToolPaletteCommandTemplate");
        StringAssert.Contains(code, "RibbonCommandCatalog.CreateToolPalette()");
        Assert.IsFalse(xaml.Contains("Source=\"{StaticResource Icon.Tool.", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ToolsTabExposesElementStudioCommand()
    {
        var tabs = RibbonCommandCatalog.CreateDefault();
        var toolsCommands = tabs["Tools"].SelectMany(group => group.Commands).ToArray();
        var studioCommand = toolsCommands.SingleOrDefault(command => command.Id == "tool.element-studio");

        Assert.IsNotNull(studioCommand, "Tools tab should expose the tool.element-studio command.");
        Assert.IsTrue(studioCommand!.IsEnabled);
        Assert.IsNull(studioCommand.DisabledReason);
        Assert.AreEqual("Icon.Tool.ElementStudio", studioCommand.IconKey);

        var paletteIds = RibbonCommandCatalog.CreateToolPalette().Select(command => command.Id).ToArray();
        CollectionAssert.DoesNotContain(paletteIds, "tool.element-studio");
    }

    [TestMethod]
    public void ToolsTabExposesEnabledSettingsCommand()
    {
        var tabs = RibbonCommandCatalog.CreateDefault();
        var toolsCommands = tabs["Tools"].SelectMany(group => group.Commands).ToArray();
        var settingsCommand = toolsCommands.SingleOrDefault(command => command.Id == "tool.settings");

        Assert.IsNotNull(settingsCommand, "Tools tab should expose the tool.settings command.");
        Assert.IsTrue(settingsCommand!.IsEnabled);
        Assert.IsNull(settingsCommand.DisabledReason);
    }

    private static string ReadProjectFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Unable to locate project file: {Path.Combine(parts)}");
        return "";
    }
}
