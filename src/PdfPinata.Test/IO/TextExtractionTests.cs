using System;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Extraction;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   The content-stream parser has been here all along — <c>CParser</c>, <c>ContentReader</c> and a
///   full operator table — and nothing turned what it produced into text. So a library that wrote
///   PDFs could not read back a word of its own output, and anyone needing that reached for a second
///   library.
///
///   These tests round-trip: draw text, save, reopen, extract. That is the strongest check available
///   here, because a font embedded as Identity-H writes glyph indices rather than characters, and
///   getting the text back means the <c>/ToUnicode</c> map this library writes was read correctly by
///   the map reader — two independent pieces agreeing.
/// </summary>
public class TextExtractionTests
{
    [Fact]
    public void TextDrawnOnAPageIsReadBack()
    {
        var page = Reopen(Draw(gfx => gfx.DrawString("Hello world", Font, XBrushes.Black, 40, 100)));

        PdfTextExtractor.ExtractText(page).Should().Be("Hello world");
    }

    [Fact]
    public void EachRunSaysWhereItsBaselineStarts()
    {
        var page = Reopen(Draw(gfx => gfx.DrawString("Positioned", Font, XBrushes.Black, 72, 200)));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        run.Origin.X.Should().BeApproximately(72, 1);

        // User space has its origin at the bottom-left and Y growing upwards, while DrawString was
        // given a Y measured down from the top. On an A4 page 200 down is 842 - 200 up.
        run.Origin.Y.Should().BeApproximately(842 - 200, 1);
    }

    [Fact]
    public void ARunKnowsTheSizeItWasDrawnAt()
    {
        var page = Reopen(Draw(gfx => gfx.DrawString("Large", new XFont("Arial", 24), XBrushes.Black, 40, 100)));

        PdfTextExtractor.ExtractRuns(page).Single().FontSize.Should().BeApproximately(24, 0.5);
    }

    [Fact]
    public void ARunIsAsWideAsTheTextItHolds()
    {
        var page = Reopen(Draw(gfx => gfx.DrawString("Wide enough to measure", Font, XBrushes.Black, 40, 100)));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        // Compared against what the library itself says the string measures, so this asserts the
        // extractor's width arithmetic against the writer's rather than against a number typed in.
        var expected = Measure("Wide enough to measure");
        run.Width.Should().BeApproximately(expected, expected * 0.05);
    }

    [Fact]
    public void TextOnSeparateBaselinesComesBackOnSeparateLines()
    {
        var page = Reopen(Draw(gfx =>
        {
            gfx.DrawString("First line", Font, XBrushes.Black, 40, 100);
            gfx.DrawString("Second line", Font, XBrushes.Black, 40, 130);
        }));

        PdfTextExtractor.ExtractText(page).Should().Be("First line\nSecond line");
    }

    [Fact]
    public void TwoRunsOnOneBaselineAreOneLine()
    {
        var page = Reopen(Draw(gfx =>
        {
            gfx.DrawString("Left", Font, XBrushes.Black, 40, 100);
            gfx.DrawString("Right", Font, XBrushes.Black, 300, 100);
        }));

        var text = PdfTextExtractor.ExtractText(page);

        text.Should().NotContain("\n");
        text.Should().StartWith("Left").And.EndWith("Right");
        text.Should().Contain(" ", "a gap the pen jumped is a space, even though none was drawn");
    }

    [Fact]
    public void AccentedTextSurvivesTheRoundTrip()
    {
        // The point of reading /ToUnicode rather than guessing: these are not code points the glyph
        // indices bear any relation to.
        var page = Reopen(Draw(gfx => gfx.DrawString("Ångström café", Font, XBrushes.Black, 40, 100)));

        PdfTextExtractor.ExtractText(page).Should().Be("Ångström café");
    }

    [Fact]
    public void TextIsReadBackFromEveryPage()
    {
        var document = new PdfDocument();
        foreach (var word in new[] { "One", "Two", "Three" })
        {
            var gfx = XGraphics.FromPdfPage(document.AddPage());
            gfx.DrawString(word, Font, XBrushes.Black, 40, 100);
            gfx.Dispose();
        }

        var reopened = Reopen(Save(document));

        reopened.Owner.Pages.Cast<PdfPage>().Select(PdfTextExtractor.ExtractText)
            .Should().Equal("One", "Two", "Three");
    }

    [Fact]
    public void APageWithNoTextYieldsNoRuns()
    {
        var page = Reopen(Draw(gfx => gfx.DrawRectangle(XBrushes.LightGray, 10, 10, 100, 50)));

        PdfTextExtractor.ExtractRuns(page).Should().BeEmpty();
    }

    [Fact]
    public void TextDrawnUnderATransformIsReportedWhereItLanded()
    {
        // The extractor has to keep the current transformation matrix as well as the text matrix,
        // or everything drawn inside a translated container is reported at the wrong place.
        var page = Reopen(Draw(gfx =>
        {
            gfx.TranslateTransform(100, 50);
            gfx.DrawString("Shifted", Font, XBrushes.Black, 0, 100);
        }));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        run.Origin.X.Should().BeApproximately(100, 1);
        run.Origin.Y.Should().BeApproximately(842 - 150, 1);
    }

    [Fact]
    public void ARunNamesTheFontResourceItWasDrawnWith()
    {
        var page = Reopen(Draw(gfx => gfx.DrawString("Named", Font, XBrushes.Black, 40, 100)));

        PdfTextExtractor.ExtractRuns(page).Single().FontName.Should().StartWith("/F");
    }

    [Fact]
    public void RunsComeBackInTheOrderTheyWereDrawn()
    {
        var page = Reopen(Draw(gfx =>
        {
            gfx.DrawString("Third", Font, XBrushes.Black, 40, 300);
            gfx.DrawString("First", Font, XBrushes.Black, 40, 100);
            gfx.DrawString("Second", Font, XBrushes.Black, 40, 200);
        }));

        // Drawing order, not reading order. Saying so is the honest contract: sorting these into
        // the order a person would read them is layout analysis and is not what this does.
        PdfTextExtractor.ExtractRuns(page).Select(run => run.Text)
            .Should().Equal("Third", "First", "Second");
    }

    [Fact]
    public void ARunUnderAScaledTransformIsAsWideAsItLooks()
    {
        // The width and the size have to be measured through the same matrix. Measuring the width
        // through the text matrix alone leaves the run reported in text space while its size is
        // reported in user space, and a test that only translates cannot tell, because translating
        // scales by one.
        var page = Reopen(Draw(gfx =>
        {
            gfx.ScaleTransform(2);
            gfx.DrawString("Wide", Font, XBrushes.Black, 20, 50);
        }));

        var run = PdfTextExtractor.ExtractRuns(page).Single();
        var unscaled = Measure("Wide");

        run.FontSize.Should().BeApproximately(24, 0.5);
        run.Width.Should().BeApproximately(unscaled * 2, unscaled * 0.1);
    }

    [Fact]
    public void ADestinationOfMoreThanOneCharacterDoesNotAbortExtraction()
    {
        // A /ToUnicode destination is a string of UTF-16 code units, not a scalar. Reading it as one
        // number and converting threw for anything longer than a single unit — a ligature such as
        // <00660069> for "fi" reads as 6684777 — and the exception came out of extraction rather
        // than out of the map, taking the whole page with it.
        var page = Reopen(WithToUnicode(Draw(gfx => gfx.DrawString("Hello", Font, XBrushes.Black, 40, 100)),
            cmap => cmap + "\n1 beginbfrange\n<F000> <F001> <00660069>\nendbfrange\n"));

        PdfTextExtractor.ExtractText(page).Should().Be("Hello");
    }

    [Fact]
    public void AnArrayOfDestinationsDoesNotShiftTheEntriesAfterIt()
    {
        // The array form — <lo> <hi> [<d1> <d2> …] — was documented as not read. It was not skipped
        // either: collecting every hexadecimal string in the block and stepping through them three
        // at a time swallows the array's elements into the same stream and shifts everything after
        // it, so the real entries below map the wrong codes to the wrong text.
        var page = Reopen(WithToUnicode(Draw(gfx => gfx.DrawString("Hello", Font, XBrushes.Black, 40, 100)),
            // Matched without a line break, because the CMap is written with StreamWriter.WriteLine
            // and so carries whatever newline the machine that wrote it uses. Matching "\n" made
            // this pass by doing nothing on Windows.
            cmap => cmap.Replace("beginbfrange", "beginbfrange\n<F000> <F002> [<0041> <0042> <0043>]")));

        PdfTextExtractor.ExtractText(page).Should().Be("Hello");
    }

    [Fact]
    public void TheSpacingOperandsOfAQuotedShowAreNotReadAsKerning()
    {
        // " takes aw ac string. Showing from operand zero reads the two spacing numbers as though
        // they were the kerning adjustments of a TJ array, so they are applied twice — once as the
        // spacing they are, and once as a displacement they are not.
        var page = Reopen(WithTwoShows());

        var runs = PdfTextExtractor.ExtractRuns(page);

        // The same string, the same spacing, shown two ways. Whatever the number is, it is the same
        // number.
        runs[^1].Width.Should().BeApproximately(runs[^2].Width, 0.01);
    }

    [Fact]
    public void ExtractingFromNothingIsRefusedRatherThanReturningNothing()
    {
        var extracting = () => PdfTextExtractor.ExtractRuns(null);

        extracting.Should().Throw<ArgumentNullException>();
    }

    // ---------------------------------------------------------------------------------------------
    // The text-state and positioning operators. XGraphics emits only a few of these - Tf, Tm, Tj and
    // TJ - so a page it drew leaves most of the walker's switch untouched. Every test below replaces
    // the content stream outright and writes the operator itself, which is the only way to reach
    // them, and is what a page from another producer would contain.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TextDrawnInvisiblyIsNotExtracted()
    {
        // Render mode 3 is how the OCR layer under a scanned page is drawn. Reporting it would give
        // a caller the page's text twice, so it is skipped - a decision this class makes rather than
        // exposes, and one worth pinning because it is silent.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf 1 0 0 1 40 700 Tm <{shown}> Tj ET\n" +
            $"BT {font} 12 Tf 3 Tr 1 0 0 1 40 650 Tm <{shown}> Tj ET\n"));

        var runs = PdfTextExtractor.ExtractRuns(page);

        runs.Should().ContainSingle("the run in render mode 3 is not reported");
        runs[0].Origin.Y.Should().BeApproximately(700, 0.5, "the visible one is the one that survives");
    }

    [Fact]
    public void TextAfterAnInvisibleRunIsExtractedAgain()
    {
        // The other side of it: render mode is text state and persists past ET, so the mode has to
        // be put back rather than assumed to lapse. If it were sticky the page would extract to
        // nothing at all.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf 3 Tr 1 0 0 1 40 700 Tm <{shown}> Tj ET\n" +
            $"BT {font} 12 Tf 0 Tr 1 0 0 1 40 650 Tm <{shown}> Tj ET\n"));

        var runs = PdfTextExtractor.ExtractRuns(page);

        runs.Should().ContainSingle();
        runs[0].Origin.Y.Should().BeApproximately(650, 0.5, "the second run is the visible one");
    }

    [Fact]
    public void TheNextLineOperatorMovesDownByTheLeading()
    {
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf 14 TL 1 0 0 1 40 700 Tm <{shown}> Tj T* <{shown}> Tj ET\n"));

        var runs = PdfTextExtractor.ExtractRuns(page);

        runs.Should().HaveCount(2);
        runs[0].Origin.Y.Should().BeApproximately(700, 0.5);
        runs[1].Origin.Y.Should().BeApproximately(686, 0.5, "T* steps down by the leading TL set");
        runs[1].Origin.X.Should().BeApproximately(40, 0.5,
            "T* returns to the start of the line rather than to where the last run ended");
    }

    [Fact]
    public void AQuotedShowMovesToTheNextLineBeforeShowingIt()
    {
        // ' is T* and Tj in one operator.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf 14 TL 1 0 0 1 40 700 Tm <{shown}> Tj <{shown}> ' ET\n"));

        var runs = PdfTextExtractor.ExtractRuns(page);

        runs.Should().HaveCount(2);
        runs[1].Origin.Y.Should().BeApproximately(686, 0.5);
        runs[1].Origin.X.Should().BeApproximately(40, 0.5);
    }

    [Fact]
    public void MovingTheLineWithTdAlsoSetsTheLeadingForWhatFollows()
    {
        // TD is Td with a side effect: it sets the leading to the negation of its ty, so a following
        // T* repeats the same step without being told the distance again. Asserting the third run is
        // what makes this a test of the side effect rather than of the movement.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf 1 0 0 1 40 700 Tm <{shown}> Tj 0 -20 TD <{shown}> Tj T* <{shown}> Tj ET\n"));

        var runs = PdfTextExtractor.ExtractRuns(page);

        runs.Should().HaveCount(3);
        runs[0].Origin.Y.Should().BeApproximately(700, 0.5);
        runs[1].Origin.Y.Should().BeApproximately(680, 0.5, "TD moved the line down by 20");
        runs[2].Origin.Y.Should().BeApproximately(660, 0.5, "and left the leading at 20 for T*");
    }

    [Fact]
    public void HorizontalScalingNarrowsTheRunItIsSetFor()
    {
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf 1 0 0 1 40 700 Tm <{shown}> Tj ET\n" +
            $"BT {font} 12 Tf 50 Tz 1 0 0 1 40 650 Tm <{shown}> Tj ET\n"));

        var runs = PdfTextExtractor.ExtractRuns(page);

        runs.Should().HaveCount(2);
        runs[1].Width.Should().BeApproximately(runs[0].Width / 2, runs[0].Width * 0.02,
            "Tz is a percentage, so 50 is half as wide");
    }

    [Fact]
    public void ARiseLiftsTheBaselineOfWhatFollowsIt()
    {
        // Ts is what a superscript is drawn with.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf 1 0 0 1 40 700 Tm <{shown}> Tj 6 Ts <{shown}> Tj ET\n"));

        var runs = PdfTextExtractor.ExtractRuns(page);

        runs.Should().HaveCount(2);
        runs[0].Origin.Y.Should().BeApproximately(700, 0.5);
        runs[1].Origin.Y.Should().BeApproximately(706, 0.5, "the rise is added to the baseline");
    }

    [Fact]
    public void RestoringTheGraphicsStateUndoesTheTransformUnderIt()
    {
        // q, cm and Q. Both blocks name the same text position, so the only thing that can separate
        // them is whether Q put the transformation matrix back.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"q 1 0 0 1 0 100 cm BT {font} 12 Tf 1 0 0 1 40 600 Tm <{shown}> Tj ET Q\n" +
            $"BT {font} 12 Tf 1 0 0 1 40 600 Tm <{shown}> Tj ET\n"));

        var runs = PdfTextExtractor.ExtractRuns(page);

        runs.Should().HaveCount(2);
        runs[0].Origin.Y.Should().BeApproximately(700, 0.5, "the cm inside q lifted it by 100");
        runs[1].Origin.Y.Should().BeApproximately(600, 0.5, "and Q put the matrix back for the rest");
    }

    [Fact]
    public void AnUnbalancedRestoreIsIgnoredRatherThanThrowing()
    {
        // A Q with no q under it is malformed, and a content stream from anywhere can be. The stack
        // is checked before it is popped, so the page still reads.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"Q BT {font} 12 Tf 1 0 0 1 40 700 Tm <{shown}> Tj ET\n"));

        PdfTextExtractor.ExtractRuns(page).Should().ContainSingle();
    }

    private static XFont Font => new("Arial", 12);

    /// <summary>
    ///   A page whose content stream is replaced outright by <paramref name="build"/>, which is
    ///   handed the name of the font resource and the hexadecimal glyph string of a word already
    ///   drawn with it. Everything the page needs to be a page - the resources, the font, its
    ///   <c>/ToUnicode</c> map - comes from having drawn that word first.
    /// </summary>
    private static byte[] WithContentReplaced(Func<string, string, string> build)
    {
        var document = Reader.Open(new MemoryStream(
            Draw(gfx => gfx.DrawString("Sample", Font, XBrushes.Black, 40, 100))), PdfDocumentOpenMode.Modify);

        var page = document.Pages[0];
        var content = Encoding.Latin1.GetString(PageContent.Of(page));

        var font = page.Elements.GetDictionary("/Resources").Elements.GetDictionary("/Font")
            .Elements.KeyNames[0].Value;
        var shown = content.Substring(content.IndexOf('<') + 1,
            content.IndexOf('>') - content.IndexOf('<') - 1);

        var bytes = Encoding.Latin1.GetBytes(build(font, shown));
        var stream = (PdfDictionary)page.Elements.GetValue("/Contents");
        stream.Stream.Value = bytes;
        stream.Elements.Remove("/Filter");
        stream.Elements.SetInteger("/Length", bytes.Length);

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static double Measure(string text)
    {
        var document = new PdfDocument();
        var gfx = XGraphics.FromPdfPage(document.AddPage());
        return gfx.MeasureString(text, Font).Width;
    }

    private static byte[] Draw(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var gfx = XGraphics.FromPdfPage(document.AddPage());
        draw(gfx);
        gfx.Dispose();
        return Save(document);
    }

    private static byte[] Save(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static PdfPage Reopen(byte[] bytes)
    {
        var saved = new MemoryStream(bytes);
        return Reader.Open(saved, PdfDocumentOpenMode.Modify).Pages[0];
    }

    /// <summary>
    ///   Rewrites the font's <c>/ToUnicode</c> map, so that a CMap this library would never write
    ///   can still be put in front of the reader that has to cope with one.
    /// </summary>
    private static byte[] WithToUnicode(byte[] bytes, Func<string, string> rewrite)
    {
        var document = Reader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
        var map = FontOf(document.Pages[0]).Elements.GetDictionary("/ToUnicode");

        map.Stream.TryUnfilter();
        var original = Encoding.Latin1.GetString(map.Stream.Value);
        var replaced = Encoding.Latin1.GetBytes(rewrite(original));

        // So that a rewrite matching nothing fails here rather than leaving a test that passes
        // because it tested nothing.
        Encoding.Latin1.GetString(replaced).Should().NotBe(original,
            "the rewrite has to have changed the map for the test to be testing anything");

        map.Stream.Value = replaced;
        map.Elements.Remove("/Filter");
        map.Elements.SetInteger("/Length", replaced.Length);

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    /// <summary>
    ///   A page showing the same string twice with the same spacing, once through <c>Tj</c> with the
    ///   spacing set by <c>Tw</c> and <c>Tc</c>, and once through <c>"</c>, which carries it.
    /// </summary>
    private static byte[] WithTwoShows()
    {
        var document = Reader.Open(new MemoryStream(
            Draw(gfx => gfx.DrawString("Quoted", Font, XBrushes.Black, 40, 100))), PdfDocumentOpenMode.Modify);

        var page = document.Pages[0];
        var content = Encoding.Latin1.GetString(PageContent.Of(page));

        var font = page.Elements.GetDictionary("/Resources").Elements.GetDictionary("/Font")
            .Elements.KeyNames[0].Value;
        var shown = content.Substring(content.IndexOf('<') + 1,
            content.IndexOf('>') - content.IndexOf('<') - 1);

        // Spacing large enough that reading it twice cannot be mistaken for rounding.
        var added = content
            + "\nBT " + font + " 12 Tf 0 Tw -3000 Tc 1 0 0 1 40 700 Tm <" + shown + "> Tj ET\n"
            + "BT " + font + " 12 Tf 1 0 0 1 40 650 Tm 0 -3000 <" + shown + "> \" ET\n";

        var bytes = Encoding.Latin1.GetBytes(added);
        var stream = (PdfDictionary)page.Elements.GetValue("/Contents");
        stream.Stream.Value = bytes;
        stream.Elements.Remove("/Filter");
        stream.Elements.SetInteger("/Length", bytes.Length);

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static PdfDictionary FontOf(PdfPage page)
    {
        var fonts = page.Elements.GetDictionary("/Resources").Elements.GetDictionary("/Font");
        return fonts.Elements.GetDictionary(fonts.Elements.KeyNames[0].Value);
    }
}
