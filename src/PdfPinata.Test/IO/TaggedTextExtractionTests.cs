using System;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Extraction;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Structure;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   <c>docs/specs/tagged-text-extraction.md</c>: the extractor now reads what the page says about
///   itself — <c>BDC</c>, <c>BMC</c>, <c>EMC</c> and <c>/ActualText</c> — rather than only the
///   <c>Tj</c>/<c>TJ</c> operators <c>TextExtractionTests</c> covers.
/// </summary>
/// <remarks>
///   <para>
///     <b>Built from raw operators, not from <see cref="XGraphics.BeginMarkedContent(PdfTag,string)"/>.</b>
///     That API writes a tag and an <c>/MCID</c> and nothing else — <c>/ActualText</c> never reaches
///     the content stream through any public drawing call. The one place this library writes it today
///     is the ligature path inside <c>XGraphicsPdfRenderer</c>, and the hyphenation the predecessor
///     spec's <c>ParagraphRenderer</c> writes puts <c>/ActualText</c> on the structure element the
///     sequence's <c>/MCID</c> points to, not inline in the sequence itself — reachable only by
///     resolving the parent tree, which <c>docs/specs/tagged-text-extraction.md</c> explicitly rules
///     the extractor out of doing. So these tests write the operators directly, the same way
///     <c>TextExtractionTests</c> reaches every operator <c>XGraphics</c> itself never emits.
///   </para>
/// </remarks>
public class TaggedTextExtractionTests
{
    [Fact]
    public void AWordDeclaredWholeAcrossTwoRunsExtractsAsOneWord()
    {
        // One sequence, opened once, spanning a line break - the shape a word broken at a hyphen
        // takes: two show-text operators, on two different baselines, both inside the one sequence
        // that says what the whole of them spells.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf /Span <</ActualText (conformance)>> BDC "
            + $"1 0 0 1 40 700 Tm <{shown}> Tj "
            + $"1 0 0 1 40 680 Tm <{shown}> Tj "
            + "EMC ET\n"));

        PdfTextExtractor.ExtractText(page).Should().Be("conformance");
    }

    [Fact]
    public void AGenuineHyphenSurvivesExtraction()
    {
        // No marked content at all - fixing the case above must not touch a real hyphen that was
        // never inside a sequence declaring anything.
        var page = Reopen(Draw(gfx => gfx.DrawString("well-formed", Font, XBrushes.Black, 40, 100)));

        PdfTextExtractor.ExtractText(page).Should().Be("well-formed");
    }

    [Fact]
    public void ARunningHeadIsAbsentFromTheJoinedTextButPresentAmongTheRuns()
    {
        // Both runs decode to the same word - WithContentReplaced can only reuse the glyph encoding
        // "Sample" was already drawn with - so what tells them apart is the baseline each sits on
        // and whether the joined text repeats it.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"/P <</MCID 0>> BDC BT {font} 12 Tf 1 0 0 1 40 700 Tm <{shown}> Tj ET EMC\n"
            + $"/Artifact BMC BT {font} 12 Tf 1 0 0 1 40 650 Tm <{shown}> Tj ET EMC\n"));

        var runs = PdfTextExtractor.ExtractRuns(page);

        runs.Should().HaveCount(2, "the lower-level method returns the furniture too");
        runs[0].Tag.Should().Be(PdfTag.P);
        runs[0].IsArtifact.Should().BeFalse();
        runs[1].Tag.Should().Be(PdfTag.Artifact);
        runs[1].IsArtifact.Should().BeTrue();
        runs[1].Text.Should().Be("Sample", "a run inside an artifact still reports its own glyph text");

        PdfTextExtractor.ExtractText(page).Should().Be("Sample",
            "the body run on the first baseline is kept and the artifact run on the second is left "
            + "out entirely, rather than joined onto it as a second line");
    }

    [Fact]
    public void AStructuralSequenceNestedInsideAnArtifactIsStillFurniture()
    {
        // Malformed for this library's own writer - StructureTagger never tags anything while an
        // artifact is open - but not for PDF in general, and a document from another producer is
        // free to nest one. The innermost tag is /P, which is exactly why checking Tag alone would
        // miss this: the artifact is an ancestor, not the nearest sequence.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"/Artifact BMC /P <</MCID 0>> BDC BT {font} 12 Tf 1 0 0 1 40 700 Tm <{shown}> Tj ET EMC EMC\n"));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        run.Tag.Should().Be(PdfTag.P, "the innermost tag is reported regardless");
        run.IsArtifact.Should().BeTrue("an ancestor sequence is an artifact, and that is not opt-out-able");
        PdfTextExtractor.ExtractText(page).Should().BeEmpty(
            "the run is furniture however specific a tag its own nearer sequence claims");
    }

    [Fact]
    public void ALigatureStyleSpanExtractsAsTheCharactersItDeclares()
    {
        // The shape the ligature path already writes: one sequence, one run, no /MCID at all.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf /Span <</ActualText (fi)>> BDC 1 0 0 1 40 700 Tm <{shown}> Tj EMC ET\n"));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        run.ActualText.Should().Be("fi");
        run.MarkedContentId.Should().BeNull("the ligature span carries no identifier");
        PdfTextExtractor.ExtractText(page).Should().Be("fi");
    }

    [Fact]
    public void ASequenceDeclaringTextOverSeveralRunsContributesItOnce()
    {
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf /Span <</ActualText (whole)>> BDC "
            + $"1 0 0 1 40 700 Tm <{shown}> Tj "
            + $"1 0 0 1 40 680 Tm <{shown}> Tj "
            + $"1 0 0 1 40 660 Tm <{shown}> Tj "
            + "EMC ET\n"));

        var runs = PdfTextExtractor.ExtractRuns(page);
        runs.Should().HaveCount(3, "the lower-level method still reports every run drawn");
        runs.Should().OnlyContain(run => run.ActualText == "whole");

        Occurrences(PdfTextExtractor.ExtractText(page), "whole").Should().Be(1,
            "the sequence says the word once, however many runs it spans");
    }

    [Fact]
    public void NestedSequencesReportTheInnermostTag()
    {
        var page = Reopen(WithContentReplaced((font, shown) =>
            "/Sect <</MCID 0>> BDC "
            + "/H2 <</MCID 1>> BDC "
            + $"BT {font} 12 Tf 1 0 0 1 40 700 Tm <{shown}> Tj ET "
            + "EMC EMC\n"));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        run.Tag.Should().Be(PdfTag.H2);
        run.MarkedContentId.Should().Be(1);
    }

    [Fact]
    public void APropertyListNamedThroughResourcesIsHonouredLikeAnInlineOne()
    {
        var bytes = WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf /Span /P1 BDC 1 0 0 1 40 700 Tm <{shown}> Tj EMC ET\n");

        using var reopened = new MemoryStream(bytes);
        var document = Reader.Open(reopened, PdfDocumentOpenMode.Modify);
        var page = document.Pages[0];

        var properties = new PdfDictionary(document);
        properties.Elements.SetInteger("/MCID", 3);
        properties.Elements.SetString("/ActualText", "conformance");

        var category = new PdfDictionary(document);
        category.Elements["/P1"] = properties;
        page.Elements.GetDictionary("/Resources").Elements["/Properties"] = category;

        using var output = new MemoryStream();
        document.Save(output, false);

        var run = PdfTextExtractor.ExtractRuns(Reopen(output.ToArray())).Single();

        run.Tag.Should().Be(PdfTag.Span);
        run.ActualText.Should().Be("conformance");
        run.MarkedContentId.Should().Be(3);
    }

    [Fact]
    public void ANameThatResolvesToNothingIsASequenceWithNoProperties()
    {
        // No /Properties category was ever added to the page's resources, so /Missing resolves to
        // nothing - a sequence with a tag and no properties, not a reason to abort extraction.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf /Span /Missing BDC 1 0 0 1 40 700 Tm <{shown}> Tj EMC ET\n"));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        run.Tag.Should().Be(PdfTag.Span);
        run.ActualText.Should().BeNull();
        run.MarkedContentId.Should().BeNull();
    }

    [Fact]
    public void ADocumentWithNoMarkedContentExtractsExactlyAsBefore()
    {
        var page = Reopen(Draw(gfx => gfx.DrawString("Plain text", Font, XBrushes.Black, 40, 100)));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        run.Tag.Should().BeNull();
        run.ActualText.Should().BeNull();
        run.MarkedContentId.Should().BeNull();
        PdfTextExtractor.ExtractText(page).Should().Be("Plain text");
    }

    [Fact]
    public void AnUnterminatedSequenceDoesNotAbortExtraction()
    {
        // The BDC is never closed - the content simply ends. The run inside it still comes back.
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"BT {font} 12 Tf /Span <</MCID 0>> BDC 1 0 0 1 40 700 Tm <{shown}> Tj ET\n"));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        run.Tag.Should().Be(PdfTag.Span);
    }

    [Fact]
    public void AnEndWithNoMatchingBeginIsIgnoredRatherThanThrowing()
    {
        var page = Reopen(WithContentReplaced((font, shown) =>
            $"EMC BT {font} 12 Tf 1 0 0 1 40 700 Tm <{shown}> Tj ET\n"));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        run.Tag.Should().BeNull("the stray EMC popped an empty stack rather than corrupting it");
    }

    [Fact]
    public void ATruncatedInlineDictionaryDoesNotAbortExtraction()
    {
        // The dictionary never sees its closing '>>' before the content ends. CLexer.ScanDictionary
        // gives up at the end of the content rather than running forever; extraction has to give up
        // just as gracefully.
        var page = Reopen(WithContentReplaced((font, _) =>
            $"BT {font} 12 Tf /Span <</MCID 0"));

        var extracting = () => PdfTextExtractor.ExtractRuns(page);

        extracting.Should().NotThrow();
    }

    [Fact]
    public void AnEndPastTheDepthCapClosesItsOwnSequenceRatherThanATrackedOne()
    {
        // A thousand plain sequences push the tracked stack to its cap; the next one past it is
        // counted rather than tracked. Its own EMC has to know that - popping the tracked stack
        // instead would take back the /Marker scope the Tj below is still inside, and every tag,
        // MCID and ActualText reported for the rest of the page would come from the wrong sequence.
        var filler = string.Concat(Enumerable.Repeat("/Filler BMC\n", 1023));
        var page = Reopen(WithContentReplaced((font, shown) =>
            filler
            + "/Marker <</MCID 9999>> BDC "
            + "/Extra <</MCID 1>> BDC "
            + "EMC "
            + $"BT {font} 12 Tf 1 0 0 1 40 700 Tm <{shown}> Tj ET\n"));

        var run = PdfTextExtractor.ExtractRuns(page).Single();

        run.Tag.Should().Be(new PdfTag("/Marker"),
            "the run is still inside the 1024th sequence, the deepest one that was tracked");
        run.MarkedContentId.Should().Be(9999);
    }

    private static XFont Font => new("Arial", 12);

    /// <summary>
    ///   A page whose content stream is replaced outright by <paramref name="build"/>, which is
    ///   handed the name of the font resource and the hexadecimal glyph string of a word already
    ///   drawn with it. The same technique <c>TextExtractionTests.WithContentReplaced</c> uses, for
    ///   the same reason: <c>BDC</c>, <c>BMC</c> and <c>EMC</c> are operators <c>XGraphics</c> itself
    ///   never emits with an inline dictionary, so writing the operator by hand is the only way in.
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

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var at = text.IndexOf(value, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(value, at + 1, StringComparison.Ordinal))
            count++;
        return count;
    }
}
