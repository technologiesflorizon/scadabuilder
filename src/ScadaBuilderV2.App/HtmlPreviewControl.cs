using System.Windows;
using System.Windows.Controls;

namespace ScadaBuilderV2.App;

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
            browser.NavigateToString("<!doctype html><html><body></body></html>");
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
<body>
{{markup}}
</body>
</html>
""");
    }
}
