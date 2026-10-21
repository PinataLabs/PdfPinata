using System;
using System.Text;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using static PdfPinata.Test.Helpers.PageInk;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The stroke colour an <see cref="XForm"/> draws with. The graphics state remembers the stroke
///   colour it last wrote and skips "RG" for the same one again, and before anything is written it
///   takes the colour to be black, the default a page starts with. A form XObject starts with no
///   such default: it inherits the stroke colour in force at its <c>Do</c>. So a form whose first
///   stroke was black wrote no "RG", and its line came out in the caller's colour (#333).
/// </summary>
/// <remarks>
///   A page keeps the old rule, so its content stream writes the bytes it always did.
/// </remarks>
[Rasterizing]
public sealed class FormStrokeColourTests : IDisposable
{
    private readonly Rasterizations _rasterized = new("Out/FormStrokeColour");

    public void Dispose() => _rasterized.Dispose();

    /// <summary>Where the form strokes its horizontal line 8 points wide, in world space.</summary>
    private static readonly XRect FormLine = new(100, 321, 200, 8);

    /// <summary>
    ///   The caller strokes a red line, then draws a form at (100, 300) whose only stroke is a black
    ///   line 8 points wide across its middle.
    /// </summary>
    private static PdfDocument AFormStrokedInBlackDrawnAfterRed(XPen formPen)
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var form = new XForm(document, new XSize(200, 50));
        using (var formGfx = XGraphics.FromForm(form))
            formGfx.DrawLine(formPen, 0, 25, 200, 25);

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawLine(new XPen(XColors.Red, 8), 100, 100, 300, 100);
            gfx.DrawImage(form, 100, 300);
        }

        return document;
    }

    private static string FormContentOf(PdfPage page)
    {
        var xObjects = page.Resources.Elements.GetDictionary("/XObject")!;
        foreach (var key in xObjects.Elements.KeyNames)
        {
            var xObject = xObjects.Elements.GetDictionary(key.Value)!;
            if (xObject.Elements.GetName("/Subtype") == "/Form")
                return Encoding.Latin1.GetString(xObject.Stream.UnfilteredValue);
        }

        throw new InvalidOperationException("The page draws no form.");
    }

    private static string PageContentOf(PdfPage page) =>
        Encoding.Latin1.GetString(page.Contents.CreateSingleContent().Stream.UnfilteredValue);

    private static bool IsBlack(IMagickColor<byte> c) => c is { R: < 60, G: < 60, B: < 60 };

    [Test, GoldenImage]
    public void AFormStrokedInBlackAfterARedStrokeOnThePageDrawsItsLineInBlack()
    {
        var page = _rasterized.FirstPageOf(AFormStrokedInBlackDrawnAfterRed(new XPen(XColors.Black, 8)), "black_after_red");

        IsBlack(Within(page, FormLine)).Should().BeTrue("the form stroked its line in black, not in the caller's red");
    }

    [Test]
    public void AFormSetsItsStrokeColourBeforeItsFirstStroke()
    {
        var content = FormContentOf(AFormStrokedInBlackDrawnAfterRed(XPens.Black).Pages[0]);

        var strokeAt = content.IndexOf("S\n", StringComparison.Ordinal);
        strokeAt.Should().BePositive();
        content[..strokeAt].Should().Contain("0 0 0 RG\n", "a form inherits the stroke colour of whatever draws it");
    }

    [Test]
    public void APageStrokedInBlackFirstStillWritesNoStrokeColour()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawLine(XPens.Black, 0, 0, 100, 100);

        PageContentOf(page).Should().NotContain(" RG", "a page starts in black, and its bytes stay as they were");
    }

    [Test]
    public void AFormWritesItsStrokeColourOnceForTwoStrokesInIt()
    {
        var document = new PdfDocument();
        var form = new XForm(document, new XSize(200, 50));
        using (var formGfx = XGraphics.FromForm(form))
        {
            formGfx.DrawLine(XPens.Black, 0, 10, 200, 10);
            formGfx.DrawLine(XPens.Black, 0, 40, 200, 40);
        }

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(form, 0, 0);

        var content = FormContentOf(page);
        content.IndexOf(" RG\n", StringComparison.Ordinal).Should()
            .Be(content.LastIndexOf(" RG\n", StringComparison.Ordinal), "the colour is remembered once written");
    }
}
