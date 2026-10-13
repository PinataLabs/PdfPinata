using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   A label ending in a no-break space typed as text - "online:" and U+00A0 - written in one run
///   of formatting and the address it introduces in another, as a citation is.
/// </summary>
/// <remarks>
///   <para>
///     The no-break space sits at the edge of a leaf rather than in a leaf of its own, and a line
///     used to be broken at every leaf boundary, so the label was left at the end of one line and
///     its address set at the start of the next - the one break the no-break space was typed to
///     forbid. <c>SymbolAndDecimalTabTests.ALineIsNotBrokenAtANoBreakCharacterTypedAsText</c> pins
///     the join between three short words; these are the paragraphs it was reported in, with
///     punctuation in a run of its own, an address too long to fit after its label, a citation
///     repeating the label, and two no-break spaces in two runs one after the other.
///   </para>
///   <para>
///     Each test finds the runs it asks about by their glyphs - the label's, the address's - rather
///     than by counting, so a change in how the words before them are split cannot move the
///     question onto some other word.
///   </para>
/// </remarks>
public class NoBreakSpaceAcrossRunsTests
{
    private const string ShortAddress = "https://a.co";

    private const string LongAddress = "https://openparliament.ca/committees/industry/44-1/49/catherine-lovrics-2/";

    /// <summary>A paragraph in nine-point Arial, in a column <paramref name="width"/> points wide.</summary>
    private static Paragraph AParagraphInAColumn(double width, double margin, out Document document)
    {
        document = new Document();
        document.Styles.Normal.Font.Name = "Arial";
        document.Styles.Normal.Font.Size = 9;
        var section = document.AddSection();
        section.PageSetup.LeftMargin = Unit.FromPoint(margin);
        section.PageSetup.RightMargin = Unit.FromPoint(margin);
        section.PageSetup.PageWidth = Unit.FromPoint(width + 2 * margin);
        return section.AddParagraph();
    }

    /// <summary>
    ///   Some words, then "online" and a run of its own holding ":" and what <paramref name="space"/>
    ///   names, then the address, in one of the shapes the paragraph was reported in. Too wide for
    ///   one line, so that the line has to be broken somewhere after the words before the label.
    /// </summary>
    private static PdfPage ALabelAndItsAddress(string shape, char space = Chars.NonBreakableSpace)
    {
        var longAddress = shape == "a long link after a citation";
        var paragraph = AParagraphInAColumn(longAddress ? 468 : 120, longAddress ? 72 : 20, out var document);

        switch (shape)
        {
            case "a link":
            case "a run in bold":
                paragraph.AddText("Reference text ");
                break;

            case "a link after a run of punctuation":
                paragraph.AddText("Reference text");
                paragraph.AddFormattedText(", ").Font.Italic = true;
                break;

            case "a long link after a citation":
                paragraph.AddText("Committee on Industry and Technology, 5 December 2022, Catherine Lovrics, ");
                paragraph.AddFormattedText("Open Parliament").Font.Italic = true;
                paragraph.AddFormattedText(", ").Font.Italic = true;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "No such shape.");
        }

        paragraph.AddText("online");
        paragraph.AddFormattedText(":" + space);

        var address = longAddress ? LongAddress : ShortAddress;
        if (shape == "a run in bold")
            paragraph.AddFormattedText(address).Font.Bold = true;
        else
            paragraph.AddHyperlink(address, HyperlinkType.Url).AddText(address);

        return Rendered.FirstPageOf(document);
    }

    [Test]
    [Arguments("a link")]
    [Arguments("a link after a run of punctuation")]
    [Arguments("a long link after a citation")]
    [Arguments("a run in bold")]
    public void ALabelEndingInANoBreakSpaceGoesDownWithTheAddressAfterIt(string shape)
    {
        var placed = Glyphs.PlacedOn(ALabelAndItsAddress(shape));
        var label = IndexOfRunBeginning(placed, GlyphsOf("online"));
        var address = IndexOfRunBeginning(placed, GlyphsOf("https://", bold: shape == "a run in bold"), label);

        placed[label].Y.Should().BeLessThan(placed[0].Y, "the label goes down from the line the paragraph starts on");
        placed.Skip(label).Take(address - label + 1).Select(run => run.Y).Should()
            .AllSatisfy(y => y.Should().Be(placed[label].Y), "the label, its colon and the start of the address share a line");
    }

    [Test]
    public void ALabelEndingInAnOrdinarySpaceStaysOnTheLineTheAddressIsBrokenFrom()
    {
        // The control: with a space a line may be broken at, "online:" fits on the first line and
        // stays there, so the label moving down above is the no-break space and not the measure.
        var placed = Glyphs.PlacedOn(ALabelAndItsAddress("a link", ' '));
        var label = IndexOfRunBeginning(placed, GlyphsOf("online"));
        var address = IndexOfRunBeginning(placed, GlyphsOf("https://"), label);

        placed[label].Y.Should().Be(placed[0].Y, "\"Reference text online:\" fits on the first line");
        placed[address].Y.Should().BeLessThan(placed[label].Y);
    }

    [Test]
    public void ACitationOfThreeAddressesKeepsEachLabelWithItsAddressInFiveLines()
    {
        var paragraph = AParagraphInAColumn(468, 72, out var document);
        document.Styles.Normal.Font.Size = 8;
        paragraph.AddText("Committee on Industry and Technology, 5 December 2022, Catherine Lovrics, Open Parliament, ");
        for (var index = 0; index < 3; index++)
        {
            paragraph.AddFormattedText("online:" + Chars.NonBreakableSpace).Font.Italic = true;
            paragraph.AddFormattedText("https://openparliament.ca/committees/industry/44-1/");
            paragraph.AddFormattedText($"{49 + index}/catherine-lovrics-2/");
            paragraph.AddText("; Committee on Industry and Technology, 15 February 2023, Open Parliament, ");
        }
        var page = Rendered.FirstPageOf(document);
        var placed = Glyphs.PlacedOn(page);
        var online = GlyphsOf("online", italic: true);
        var https = GlyphsOf("https://");

        var labels = Enumerable.Range(0, placed.Count).Where(idx => Begins(placed[idx].Run, online)).ToList();
        labels.Should().HaveCount(3);
        foreach (var label in labels)
        {
            var address = IndexOfRunBeginning(placed, https, label);
            placed.Skip(label).Take(address - label + 1).Select(run => run.Y).Should()
                .AllSatisfy(y => y.Should().Be(placed[label].Y), "each label stays on the line its address starts on");
        }
        TextBaselines.LinesOf(page).Should().HaveCount(5, "upstream sets this citation in five lines");
    }

    /// <summary>
    ///   "x", then an italic "online:" and a no-break space, a bold address and a no-break space,
    ///   and "source" in the paragraph's own font, in a column wide enough for everything after
    ///   "x" but not for "x" as well.
    /// </summary>
    private static PdfPage AChainOfTwoNoBreakSpacesInTwoRuns(string last)
    {
        using var measure = XGraphics.CreateMeasureContext(new XSize(200, 200), XGraphicsUnit.Point, XPageDirection.Downwards);
        var chain = measure.MeasureString("online:" + Chars.NonBreakableSpace, new XFont("Arial", 9, XFontStyle.Italic)).Width
            + measure.MeasureString(ShortAddress + Chars.NonBreakableSpace, new XFont("Arial", 9, XFontStyle.Bold)).Width
            + measure.MeasureString("source", new XFont("Arial", 9)).Width;

        var paragraph = AParagraphInAColumn(chain + 2, 20, out var document);
        paragraph.AddText("x ");
        paragraph.AddFormattedText("online:" + Chars.NonBreakableSpace).Font.Italic = true;
        paragraph.AddFormattedText(ShortAddress + Chars.NonBreakableSpace).Font.Bold = true;
        paragraph.AddText(last);
        return Rendered.FirstPageOf(document);
    }

    [Test]
    [Arguments("source")]
    [Arguments("source later")]
    public void AChainOfTwoNoBreakSpacesInTwoRunsGoesDownWhole(string last)
    {
        var placed = Glyphs.PlacedOn(AChainOfTwoNoBreakSpacesInTwoRuns(last));
        var label = IndexOfRunBeginning(placed, GlyphsOf("online", italic: true));
        var source = IndexOfRunBeginning(placed, GlyphsOf("source"), label);

        label.Should().Be(1, "\"x\" is drawn first and nothing else before the label");
        placed[label].Y.Should().BeLessThan(placed[0].Y, "the chain goes down from the line \"x\" is on");
        placed.Skip(label).Take(source - label + 1).Select(run => run.Y).Should()
            .AllSatisfy(y => y.Should().Be(placed[label].Y), "the label, the address and \"source\" share a line");
    }

    /// <summary>The glyphs <paramref name="text"/> draws in Arial in the style given.</summary>
    private static IReadOnlyList<int> GlyphsOf(string text, bool bold = false, bool italic = false)
    {
        var document = new Document();
        document.Styles.Normal.Font.Name = "Arial";
        var formatted = document.AddSection().AddParagraph().AddFormattedText(text);
        formatted.Font.Bold = bold;
        formatted.Font.Italic = italic;
        return Glyphs.On(Rendered.FirstPageOf(document));
    }

    private static bool Begins(IReadOnlyList<int> run, IReadOnlyList<int> glyphs) =>
        run.Count >= glyphs.Count && run.Take(glyphs.Count).SequenceEqual(glyphs);

    /// <summary>The first run at or after <paramref name="from"/> that begins with the glyphs given.</summary>
    private static int IndexOfRunBeginning(
        IReadOnlyList<(double X, double Y, IReadOnlyList<int> Run)> placed, IReadOnlyList<int> glyphs, int from = 0)
    {
        for (var idx = from; idx < placed.Count; idx++)
        {
            if (Begins(placed[idx].Run, glyphs))
                return idx;
        }
        throw new InvalidOperationException("No run on the page begins with the glyphs asked for.");
    }
}
