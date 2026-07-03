using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScadaBuilderV2.ElementStudio.App;

public sealed class HtmlPreviewControl : UserControl
{
    private readonly WebBrowser browser = new();

    public static readonly DependencyProperty MarkupProperty =
        DependencyProperty.Register(
            nameof(Markup),
            typeof(string),
            typeof(HtmlPreviewControl),
            new PropertyMetadata(null, OnMarkupChanged));

    public string? Markup
    {
        get => (string?)GetValue(MarkupProperty);
        set => SetValue(MarkupProperty, value);
    }

    public HtmlPreviewControl()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
        browser.ObjectForScripting = new HtmlPreviewScriptBridge(this);
        Content = browser;
        Loaded += (_, _) => RenderMarkup();
    }

    private static void OnMarkupChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((HtmlPreviewControl)dependencyObject).RenderMarkup();
    }

    private void RenderMarkup()
    {
        if (!IsLoaded)
        {
            return;
        }

        var markup = Markup;
        if (string.IsNullOrWhiteSpace(markup))
        {
            browser.NavigateToString("<!doctype html><html><body oncontextmenu=\"window.external.RequestContextMenu(); return false;\"></body></html>");
            return;
        }

        browser.NavigateToString($$"""
<!doctype html>
<html>
<head>
  <meta http-equiv="X-UA-Compatible" content="IE=edge">
  <style>
    html, body {
      width: 100%;
      height: 100%;
      margin: 0;
      overflow: hidden;
      background: transparent;
    }
    body {
      display: flex;
      align-items: center;
      justify-content: center;
    }
    svg {
      display: block;
      width: 100%;
      height: 100%;
      max-width: 100%;
      max-height: 100%;
    }
    body > * {
      max-width: 100%;
      max-height: 100%;
      box-sizing: border-box;
    }
  </style>
</head>
<body oncontextmenu="window.external.RequestContextMenu(); return false;">
{{markup}}
</body>
</html>
""");
    }

    /// <summary>
    /// This control hosts a native WebBrowser (Internet Explorer) window layered over the
    /// WPF visual tree. Native child windows never route their input through WPF's hit-testing
    /// or routed-event system, so a right-click here would otherwise always show IE's own
    /// context menu instead of bubbling to the surrounding ListBox's WPF ContextMenu -
    /// IsHitTestVisible has no effect on this. The hosted document's oncontextmenu handler
    /// suppresses IE's native menu and calls back into .NET via ObjectForScripting, letting us
    /// open the correct WPF ContextMenu (and select the right-clicked item) ourselves.
    /// </summary>
    internal void OpenAncestorContextMenu()
    {
        ListBoxItem? listBoxItem = null;
        FrameworkElement? menuHost = null;

        DependencyObject? current = this;
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current);

            if (listBoxItem is null && current is ListBoxItem item)
            {
                listBoxItem = item;
            }

            if (menuHost is null && current is FrameworkElement { ContextMenu: not null } element)
            {
                menuHost = element;
            }
        }

        if (listBoxItem is not null)
        {
            listBoxItem.IsSelected = true;
        }

        if (menuHost?.ContextMenu is { } contextMenu)
        {
            contextMenu.PlacementTarget = menuHost;
            contextMenu.IsOpen = true;
        }
    }
}

[ComVisible(true)]
public sealed class HtmlPreviewScriptBridge
{
    private readonly HtmlPreviewControl owner;

    internal HtmlPreviewScriptBridge(HtmlPreviewControl owner)
    {
        this.owner = owner;
    }

    public void RequestContextMenu()
    {
        owner.Dispatcher.BeginInvoke(owner.OpenAncestorContextMenu);
    }
}
