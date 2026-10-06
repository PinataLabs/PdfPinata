using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using SkiaSharp;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   A stream the writer deflates keeps the deflated form only when it is shorter than the plain
///   one, and says which it kept in <c>/Filter</c>.
/// </summary>
/// <remarks>
///   Deflating a few bytes makes them longer: a two byte zlib header and a four byte checksum are
///   paid before a single byte is saved, so an empty stream comes back as eight bytes of framing
///   around nothing, and Acrobat reports that particular stream as an error on a page. Page content
///   kept to that rule and nothing else did - the forms, the fonts, the <c>/ToUnicode</c> maps, the
///   images and their masks, and the object and cross-reference streams each deflated on their own
///   terms. They all go through <c>PdfStream.SetContent</c> now, and these tests say whether they
///   still do.
/// </remarks>
public class StreamCompressionTests
{
    [Test]
    [Arguments(PdfCrossReferenceFormat.Classic)]
    [Arguments(PdfCrossReferenceFormat.Stream)]
    public void NoStreamIsWrittenDeflatedWhereDeflatingMadeItLonger(PdfCrossReferenceFormat format)
    {
        var document = new PdfDocument();
        document.Options.CrossReferenceFormat = format;
        var page = document.AddPage();

        // A form with almost nothing in it, and an image of one pixel with a soft mask of one
        // pixel: the streams that used to come out longer for being deflated. The text brings a
        // font, its /ToUnicode map and a page's worth of content, which do shrink.
        var form = new XForm(document, new XSize(10, 10));
        using (var formGfx = XGraphics.FromForm(form))
            formGfx.DrawLine(XPens.Black, 0, 0, 1, 1);

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawString("Compression", new XFont("Arial", 20), XBrushes.Black, 20, 40);
            gfx.DrawImage(form, 20, 60);
            gfx.DrawImage(OnePixel(alpha: 128), 20, 100, 10, 10);
        }

        var streams = document.Reopened(PdfDocumentOpenMode.Import).Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Where(dictionary => dictionary.Stream != null)
            .ToList();

        var deflated = streams.Where(IsDeflated).ToList();
        deflated.Should().NotBeEmpty("the content, the font and its /ToUnicode map all shrink when deflated");
        foreach (var dictionary in deflated)
        {
            dictionary.Stream.Value.Length.Should().BeLessThan(dictionary.Stream.UnfilteredValue.Length,
                $"object {dictionary.Reference?.ObjectNumber} is deflated, so deflating has to have made it shorter");
        }

        streams.Should().Contain(dictionary => !IsDeflated(dictionary) && dictionary.Stream.Length > 0,
            "the one pixel image is three bytes, which deflating would have made fourteen");
    }

    [Test]
    public void AnImageTooSmallToShrinkIsWrittenPlainAndReadsBackTheSame()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(OnePixel(alpha: 128), 20, 20, 10, 10);

        var image = document.Reopened(PdfDocumentOpenMode.Import).Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Single(dictionary => dictionary.Elements.GetName("/Subtype") == "/Image" &&
                                  dictionary.Elements.ContainsKey("/SMask"));
        var softMask = (PdfDictionary)image.Elements.GetObject("/SMask");

        image.Elements.ContainsKey("/Filter").Should().BeFalse();
        image.Stream.Value.Should().Equal(0, 128, 255);
        softMask.Elements.ContainsKey("/Filter").Should().BeFalse();
        softMask.Stream.Value.Should().Equal(128);
    }

    [Test]
    public void ZippingDataThatDeflatingWouldLengthenLeavesItPlain()
    {
        var document = new PdfDocument();
        var dictionary = new PdfDictionary(document);
        document.Internals.AddObject(dictionary);
        dictionary.CreateStream([.."BT ET"u8]);

        dictionary.Stream.Zip();

        dictionary.Elements.ContainsKey(PdfDictionary.PdfStream.Keys.Filter).Should().BeFalse();
        dictionary.Stream.Value.Should().Equal([.."BT ET"u8]);
        dictionary.Elements.GetInteger(PdfDictionary.PdfStream.Keys.Length).Should().Be(5);
    }

    private static bool IsDeflated(PdfDictionary dictionary) =>
        dictionary.Elements.GetName(PdfDictionary.PdfStream.Keys.Filter) == "/FlateDecode";

    /// <summary>
    ///   A PNG of a single pixel at the alpha given; below 255 and above 0, it puts an /SMask of
    ///   one byte on the image PdfPinata writes for it.
    /// </summary>
    private static XImage OnePixel(byte alpha)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        bitmap.Erase(new SKColor(0, 128, 255, alpha));

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var png = encoded.ToArray();

        return XImage.FromStream(() => new MemoryStream(png));
    }
}
