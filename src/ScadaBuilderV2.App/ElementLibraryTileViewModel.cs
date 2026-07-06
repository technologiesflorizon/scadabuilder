using System.Windows.Media;
using ScadaBuilderV2.Application.ElementStudio;

namespace ScadaBuilderV2.App;

public sealed class ElementLibraryTileViewModel
{
    public ElementLibraryTileViewModel(ElementPlusLibraryItem item)
    {
        Item = item;
        PreviewImage = SvgPreviewImageFactory.TryCreate(item.PreviewMarkup);
    }

    public ElementPlusLibraryItem Item { get; }

    public ImageSource? PreviewImage { get; }
}
