using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The alpha a colour is painted with is set by an ExtGState, written only when it differs from
///   the one written last. "Nothing written yet" used to be remembered as <see cref="XColor.Empty"/>,
///   whose alpha is 0, so a fully transparent colour painted first matched it, no ExtGState was
///   written, and the shape was painted at the default alpha of 1 - opaque (empira/PDFsharp#281).
/// </summary>
public class TransparentFirstColorTests
{
    private static readonly XColor Invisible = XColor.FromArgb(0, 255, 0, 0);

    /// <summary>
    ///   The value of <paramref name="key"/> in every ExtGState the page's content selects with gs
    ///   and that carries it, in the order the content selects them.
    /// </summary>
    private static List<double> AlphasSelected(PdfDocument document, string key)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        var page = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Import).Pages[0];

        var content = Encoding.ASCII.GetString(PageContent.Of(page));
        var states = page.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/ExtGState");

        var alphas = new List<double>();
        foreach (Match match in Regex.Matches(content, @"(/\S+)\s+gs\b"))
        {
            var state = states!.Elements.GetDictionary(match.Groups[1].Value)!;
            if (state.Elements.ContainsKey(key))
                alphas.Add(state.Elements.GetReal(key));
        }
        return alphas;
    }

    [Fact]
    public void AFullyTransparentFirstFillSelectsAFillAlphaOfZero()
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawRectangle(new XSolidBrush(Invisible), new XRect(10, 10, 100, 100));

        AlphasSelected(document, "/ca").Should().Equal([0.0],
            "without it the rectangle is filled at the default alpha of 1, in plain red");
    }

    [Fact]
    public void AFullyTransparentFirstStrokeSelectsAStrokeAlphaOfZero()
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawLine(new XPen(Invisible, 4), 10, 10, 200, 200);

        AlphasSelected(document, "/CA").Should().Equal([0.0],
            "without it the line is stroked at the default alpha of 1, in plain red");
    }

    [Fact]
    public void AFullyTransparentFillAfterAGradientSelectsAFillAlphaOfZero()
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
        {
            gfx.DrawRectangle(XBrushes.Blue, new XRect(10, 10, 100, 100));
            gfx.DrawRectangle(
                new XLinearGradientBrush(new XPoint(10, 120), new XPoint(110, 120), XColors.Red, XColors.Green),
                new XRect(10, 120, 100, 100));
            gfx.DrawRectangle(new XSolidBrush(Invisible), new XRect(10, 230, 100, 100));
        }

        // A gradient forgets the fill colour it replaced, and forgetting it used to mean alpha 0.
        AlphasSelected(document, "/ca").Last().Should().Be(0.0,
            "the fill alpha in force is otherwise still the 1 the blue rectangle selected");
    }

    [Fact]
    public void AnOpaqueFillSelectsItsAlphaOnceAndNoMore()
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
        {
            gfx.DrawRectangle(XBrushes.Blue, new XRect(10, 10, 100, 100));
            gfx.DrawRectangle(XBrushes.Red, new XRect(10, 120, 100, 100));
        }

        AlphasSelected(document, "/ca").Should().Equal(1.0);
    }
}
