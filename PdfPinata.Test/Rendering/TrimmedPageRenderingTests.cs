using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   Rendering a MigraDoc document onto a page the caller made and gave a trim margin.
/// </summary>
/// <remarks>
///   MigraDoc's own <c>PdfDocumentRenderer</c> creates every page itself and never sets a trim
///   margin, and <c>PageSetup</c> has no bleed to set, so this composition is the route that
///   exists: make the page, set the margin, open an <see cref="XGraphics"/> on it and render into
///   that. It works, nothing said so, and nothing tested it - which is the whole reason these
///   tests are here rather than a new property on the document object model.
/// </remarks>
public class TrimmedPageRenderingTests
{
    static readonly XUnit Bleed = PdfPinata.Drawing.XUnit.FromMillimeter(3);
    static readonly XUnit Marks = PdfPinata.Drawing.XUnit.FromMillimeter(5);

    /// <summary>From the corner of the sheet to the corner of the trimmed page.</summary>
    static double Inset => Bleed.Point + Marks.Point;

    const double A5Width = 420;
    const double A5Height = 595;

    [Fact]
    public void ADocumentIsLaidOutToTheTrimmedPageRatherThanToTheSheet()
    {
        var onTrimmed = BaselinesOnTheSheet(trimmed: true);
        var onPlain = BaselinesOnTheSheet(trimmed: false);

        onTrimmed.Should().HaveCount(onPlain.Count, "the same text lays out on the same number of lines");

        // Measured from the trim corner - which on the untrimmed page is the corner of the sheet -
        // every line is in the same place. The margins came from the page the caller asked for,
        // not from the larger sheet that will be cut down to it.
        for (var line = 0; line < onPlain.Count; line++)
            (onTrimmed[line] - Inset).Should().BeApproximately(onPlain[line], 0.01);
    }

    [Fact]
    public void TheTextIsHeldOffTheSheetEdgeByTheBleedAsWellAsByTheMargin()
    {
        var first = BaselinesOnTheSheet(trimmed: true).First();

        // A 2.5cm top margin is MigraDoc's default, and the sheet's own margins are on top of it.
        // Asserted as a range because the first baseline sits a line's ascent below the margin,
        // not on it.
        first.Should().BeGreaterThan(Inset + PdfPinata.Drawing.XUnit.FromCentimeter(2.5).Point);
        first.Should().BeLessThan(Inset + PdfPinata.Drawing.XUnit.FromCentimeter(3.5).Point);
    }

    [Fact]
    public void TheRenderedPageIsSavedWithTheBoxesOfATrimmedPage()
    {
        var saved = Render(trimmed: true);

        foreach (var key in new[] { "/MediaBox", "/CropBox", "/BleedBox", "/TrimBox", "/ArtBox" })
            saved.Elements[key].Should().NotBeNull(key + " is written for a trimmed page");

        var mediaBox = saved.Elements.GetRectangle("/MediaBox");
        mediaBox.Width.Should().BeApproximately(A5Width + 2 * Inset, 0.01);
        mediaBox.Height.Should().BeApproximately(A5Height + 2 * Inset, 0.01);
    }

    [Fact]
    public void ACallerCanDrawIntoTheBleedAroundTheRenderedDocument()
    {
        var saved = Render(trimmed: true, alongside: gfx =>
            gfx.DrawRectangle(XBrushes.Black, new XRect(-Bleed.Point, -Bleed.Point, 60, 60)));

        var content = Encoding.ASCII.GetString(PageContent.Of(saved));

        // The band's corner in the content is the drawing origin less one bleed on each axis,
        // which is the corner of the bleed. MigraDoc laid out inside it and knew nothing about it.
        content.Should().MatchRegex(@"-8\.504 \d+\.?\d* 60 60 re");
    }

    // ----- rendering, and reading back what was drawn --------------------------------------------

    static Document ADocument()
    {
        var document = new Document();
        var section = document.AddSection();

        // Explicit rather than inherited, so that the two pages under comparison cannot differ
        // through anything but the trim margin.
        section.PageSetup.PageWidth = Unit.FromPoint(A5Width);
        section.PageSetup.PageHeight = Unit.FromPoint(A5Height);

        for (var paragraph = 1; paragraph <= 4; paragraph++)
            section.AddParagraph("Paragraph " + paragraph + " of a document laid out on a page that will be trimmed.");

        return document;
    }

    static PdfPage Render(bool trimmed, Action<XGraphics> alongside = null)
    {
        var pdf = new PdfDocument();
        var page = pdf.AddPage();
        page.Width = PdfPinata.Drawing.XUnit.FromPoint(A5Width);
        page.Height = PdfPinata.Drawing.XUnit.FromPoint(A5Height);
        if (trimmed)
            page.TrimMargins.All = Bleed;

        var renderer = new DocumentRenderer(ADocument());
        renderer.PrepareDocument();

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            alongside?.Invoke(gfx);
            renderer.RenderPage(gfx, 1);
        }

        using var stream = new MemoryStream();
        pdf.Save(stream, false);
        stream.Position = 0;
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0];
    }

    /// <summary>
    ///   Where each line of text sits on the sheet, in points down from the sheet's top edge.
    /// </summary>
    /// <remarks>
    ///   The renderer writes one <c>cm</c> for the whole page and everything after it is measured
    ///   in the space that sets up, so the text matrix alone says nothing about where a line
    ///   landed. Applying the translation is what turns it into a position a reader would agree
    ///   with, and it is the only part of the matrix a page like this has.
    /// </remarks>
    static IReadOnlyList<double> BaselinesOnTheSheet(bool trimmed)
    {
        var page = Render(trimmed);
        var content = Encoding.ASCII.GetString(PageContent.Of(page));

        var offsetY = 0.0;
        var cm = Regex.Match(content, @"1 0 0 1 (-?[\d.]+) (-?[\d.]+) cm");
        if (cm.Success)
            offsetY = double.Parse(cm.Groups[2].Value, CultureInfo.InvariantCulture);

        var sheetTop = page.Elements.GetRectangle("/MediaBox").Y2;

        return TextBaselines.LinesOf(page)
            .Select(baseline => sheetTop - (baseline + offsetY))
            .ToList();
    }
}
