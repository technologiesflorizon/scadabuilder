using ScadaBuilderV2.Application.Commands;

namespace ScadaBuilderV2.Tests;

[TestClass]
public sealed class PageManagementSurfaceContractTests
{
    [TestMethod]
    public void RibbonDefinesPagesBetweenEditAndScreenWithSemanticIcons()
    {
        var tabs = RibbonCommandCatalog.CreateDefault();
        CollectionAssert.AreEqual(new[] { "File", "Edit", "Pages", "Screen", "Selection", "Tools", "Insert" }, tabs.Keys.ToArray());
        var commands = tabs["Pages"].SelectMany(group => group.Commands).ToDictionary(command => command.Id);

        CollectionAssert.AreEquivalent(
            new[] { "page.new", "page.rename", "page.duplicate", "page.delete", "page.properties", "page.validate" },
            commands.Keys.ToArray());
        Assert.IsTrue(commands.Values.All(command => command.IconKey.StartsWith("Icon.Page.", StringComparison.Ordinal)));
        Assert.IsTrue(commands.Values.All(command => command.IsEnabled));
    }

    [TestMethod]
    public void RibbonPanelAndContextMenuReuseTheSamePageCommandIds()
    {
        var xaml = Read("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var code = Read("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");
        var dialog = Read("src", "ScadaBuilderV2.App", "Pages", "PageEditorDialog.xaml");

        StringAssert.Contains(xaml, "x:Name=\"PagesMenuButton\"");
        StringAssert.Contains(xaml, "Tag=\"Pages\"");
        foreach (var id in new[] { "page.new", "page.rename", "page.duplicate", "page.delete", "page.open", "page.properties" })
        {
            StringAssert.Contains(xaml, $"Tag=\"{id}\"");
        }
        StringAssert.Contains(xaml, "PreviewMouseRightButtonDown=\"OnPagesListPreviewMouseRightButtonDown\"");
        StringAssert.Contains(xaml, "PreviewKeyDown=\"OnPagesListPreviewKeyDown\"");
        StringAssert.Contains(code, "ExecutePageSurfaceCommandAsync");
        StringAssert.Contains(code, "ContentOperations.GetParent(content)");
        StringAssert.Contains(code, "FrameworkContentElement");
        StringAssert.Contains(code, "_applicationCommandRegistry.Register(new NewPageCommand");
        StringAssert.Contains(dialog, "Code de page");
        StringAssert.Contains(dialog, "Modèle");
        Assert.IsFalse(xaml.Contains("PageKey}", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("Guid", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ProjectPanelUsesModernSearchFiltersBadgesAndQuickActions()
    {
        var xaml = Read("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var panel = Read("src", "ScadaBuilderV2.App", "Pages", "PagesPanelViewModel.cs");

        StringAssert.Contains(xaml, "x:Name=\"PagesSearchTextBox\"");
        StringAssert.Contains(xaml, "Text=\"Recherche\"");
        StringAssert.Contains(xaml, "x:Name=\"PagesTypeFilterComboBox\"");
        StringAssert.Contains(xaml, "x:Name=\"PagesBuildFilterComboBox\"");
        StringAssert.Contains(xaml, "SelectedItem=\"{Binding PagesPanel.TypeFilter, Mode=TwoWay}\"");
        StringAssert.Contains(xaml, "SelectedItem=\"{Binding PagesPanel.BuildFilter, Mode=TwoWay}\"");
        StringAssert.Contains(xaml, "Source=\"{StaticResource Icon.Page.New}\"");
        StringAssert.Contains(xaml, "Width=\"16\" Height=\"16\" Stretch=\"Uniform\"");
        StringAssert.Contains(xaml, "Width=\"30\" Height=\"28\" Padding=\"3\"");
        StringAssert.Contains(panel, "private string typeFilter = \"Default\";");
        StringAssert.Contains(panel, "private string buildFilter = \"Tous\";");
        StringAssert.Contains(xaml, "DiagnosticLabel");
        StringAssert.Contains(xaml, "{Binding PageCode, Mode=OneWay}");
        StringAssert.Contains(xaml, "{Binding TypeLabel, Mode=OneWay}");
        Assert.IsFalse(xaml.Contains("{Binding PageCode}", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("{Binding TypeLabel}", StringComparison.Ordinal));
        StringAssert.Contains(panel, "ICollectionView View");
        StringAssert.Contains(panel, "project.Scenes");
    }

    [TestMethod]
    public void PageTypeSelectorMapsByValueAndRetiresFragmentFromSelection()
    {
        var xaml = Read("src", "ScadaBuilderV2.App", "MainWindow.xaml");
        var code = Read("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(xaml, "x:Name=\"PageTypeComboBox\"");
        StringAssert.Contains(xaml, "DisplayMemberPath=\"Label\"");
        StringAssert.Contains(xaml, "SelectionChanged=\"OnPageTypeSelectionChanged\"");
        // No hard-coded ComboBoxItem: the selector is fed exclusively from PageTypeOptions.Selectable, by
        // value, never by list position (DEC-0052 — see PageTypeOptionsTests for the round-trip lock).
        Assert.IsFalse(xaml.Contains("<ComboBoxItem Content=\"Fragment\"", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("<ComboBoxItem Content=\"Defaut\"", StringComparison.Ordinal));

        StringAssert.Contains(code, "PageTypeComboBox.ItemsSource = PageTypeOptions.Selectable;");
        StringAssert.Contains(
            code,
            "private ScadaPageType GetSelectedPageType() =>\r\n        (PageTypeComboBox.SelectedItem as PageTypeOption)?.Type ?? ScadaPageType.Default;");
        StringAssert.Contains(
            code,
            "private void SelectPageType(ScadaPageType pageType) =>\r\n        PageTypeComboBox.SelectedItem = PageTypeOptions.Selectable.FirstOrDefault(option => option.Type == pageType);");
        StringAssert.Contains(code, "private static string GetPageTypeLabel(ScadaPageType pageType) => PageTypeOptions.Label(pageType);");
    }

    [TestMethod]
    public void PagesPanelFilterOffersQuickWindowLabelAndKeepsLegacyFragmentUnderTous()
    {
        var panel = Read("src", "ScadaBuilderV2.App", "Pages", "PagesPanelViewModel.cs");

        StringAssert.Contains(panel, "[\"Tous\", \"Default\", \"Fenêtre rapide\", \"Header\", \"Footer\"]");
        Assert.IsFalse(panel.Contains("\"Fragment\"", StringComparison.Ordinal), "Fragment must not be a selectable type filter (DEC-0052).");
        StringAssert.Contains(panel, "TypeFilterLabel");
        StringAssert.Contains(panel, "ScadaPageType.QuickWindow");
    }

    [TestMethod]
    public void PageContextTraversalSupportsInlineRunContentWithoutVisualTreeCrash()
    {
        var code = Read("src", "ScadaBuilderV2.App", "MainWindow.xaml.cs");

        StringAssert.Contains(code, "FrameworkContentElement content => ContentOperations.GetParent(content)");
        StringAssert.Contains(code, "Visual visual => VisualTreeHelper.GetParent(visual)");
        StringAssert.Contains(code, "if (source is ListBoxItem item)");
    }

    private static string Read(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(path)) return File.ReadAllText(path);
            directory = directory.Parent;
        }
        Assert.Fail($"Unable to locate {Path.Combine(parts)}");
        return string.Empty;
    }
}
