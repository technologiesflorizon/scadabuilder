using System.ComponentModel;
using System.Runtime.CompilerServices;
using ScadaBuilderV2.Domain.Projects;
using ScadaBuilderV2.Domain.Scenes;

namespace ScadaBuilderV2.App.Pages;

/// <summary>Presentation model for the shared page properties surface.</summary>
public sealed class PagePropertiesViewModel : INotifyPropertyChanged
{
    private string pageCode = string.Empty;
    private string title = string.Empty;
    private ScadaPageType pageType;
    private bool includeInBuild;
    private bool isHome;
    private int width;
    private int height;
    private SceneBackgroundStyle background = SceneBackgroundStyle.Default;

    internal Guid? PageRouteKey { get; private set; }

    /// <summary>Gets or sets the user-visible technical page code.</summary>
    public string PageCode { get => pageCode; set => SetField(ref pageCode, value); }

    /// <summary>Gets or sets the descriptive page title.</summary>
    public string Title { get => title; set => SetField(ref title, value); }

    /// <summary>Gets or sets the semantic page type.</summary>
    public ScadaPageType PageType { get => pageType; set => SetField(ref pageType, value); }

    /// <summary>Gets or sets whether the page is included in build and export.</summary>
    public bool IncludeInBuild { get => includeInBuild; set => SetField(ref includeInBuild, value); }

    /// <summary>Gets or sets whether this page is the project home page.</summary>
    public bool IsHome { get => isHome; set => SetField(ref isHome, value); }

    /// <summary>Gets or sets the canvas width.</summary>
    public int Width { get => width; set => SetField(ref width, value); }

    /// <summary>Gets or sets the canvas height.</summary>
    public int Height { get => height; set => SetField(ref height, value); }

    /// <summary>Gets or sets the complete page background style.</summary>
    public SceneBackgroundStyle Background { get => background; set => SetField(ref background, value); }

    /// <summary>Loads one coherent page reference and scene snapshot.</summary>
    public void Load(ScadaSceneReference page, ScadaScene scene, Guid? homePageKey)
    {
        PageRouteKey = page.PageKey;
        PageCode = page.EffectivePageCode;
        Title = page.Title;
        PageType = page.Type;
        IncludeInBuild = page.IncludeInBuild;
        IsHome = homePageKey == page.PageKey;
        Width = scene.CanvasSize.Width;
        Height = scene.CanvasSize.Height;
        Background = scene.EffectiveBackground;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
