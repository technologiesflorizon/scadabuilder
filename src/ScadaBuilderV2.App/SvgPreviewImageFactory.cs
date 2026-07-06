using System.IO;
using System.Text;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace ScadaBuilderV2.App;

internal static class SvgPreviewImageFactory
{
    public static ImageSource? TryCreate(string? svgMarkup)
    {
        if (string.IsNullOrWhiteSpace(svgMarkup))
        {
            return null;
        }

        try
        {
            var settings = new WpfDrawingSettings
            {
                IncludeRuntime = false,
                TextAsGeometry = true
            };

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svgMarkup));
            var drawing = new FileSvgReader(settings).Read(stream);
            if (drawing is null)
            {
                return null;
            }

            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
