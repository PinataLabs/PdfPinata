using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using PdfPinata.Fonts;
using PdfPinata.Utils;
using PinataLayout.DocumentObjectModel.Shapes;
using SixLabors.ImageSharp.PixelFormats;

namespace SampleBlazorWasm;

/// <summary>
/// Registers PdfPinata's font and imaging seams for the browser. Called once, from <c>Program.cs</c>.
/// </summary>
/// <remarks>
/// A browser has no font directory to scan, so the fonts are fetched from <c>wwwroot/fonts</c> and
/// handed to an <see cref="InMemoryFontResolver"/> as bytes. The images go through ImageSharp 4, from
/// <c>PdfPinata.ImageSharp4</c>, which is managed code: Skia would need its native library relinked
/// into the WebAssembly runtime. <c>PdfPinata.ImageSharp</c>, on ImageSharp 2.1, has the same types in
/// the same namespaces, so only the project reference says which of the two this is.
/// <para>
/// <c>ImageSource</c> lives in the PdfPinata assembly but in the
/// <c>PinataLayout.DocumentObjectModel.Shapes</c> namespace, hence that <c>using</c>.
/// </para>
/// </remarks>
internal static class Backends
{
    private static readonly string[] Faces =
    [
        "fonts/LiberationSans-Regular.ttf",
        "fonts/LiberationSans-Bold.ttf",
        "fonts/LiberationSans-Italic.ttf",
        "fonts/LiberationSans-BoldItalic.ttf",
    ];

    public static async Task RegisterAsync(HttpClient http)
    {
        var faces = await Task.WhenAll(Faces.Select(http.GetByteArrayAsync));

        var fonts = new InMemoryFontResolver();
        foreach (var face in faces)
            fonts.AddFont(face);

        // The first family added is the default already; an Arial asked for anywhere is served by
        // the face that carries its metrics.
        fonts.AddAlias("Arial", SampleDocument.FontFamily);

        GlobalFontSettings.FontResolver = fonts;
        ImageSource.ImageSourceImpl = new ImageSharpImageSource<Rgba32>();
        GlobalFontSettings.GlyphOutlineProvider = new ImageSharpGlyphOutlineProvider();
    }
}
