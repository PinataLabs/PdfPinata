using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   What <see cref="TextOperators.ShownWithPositions"/> promises: every position it answers with is
///   the position the text was really drawn at, and a page it cannot place is refused rather than
///   answered for approximately.
/// </summary>
/// <remarks>
///   <para>
///     A test helper is not usually worth testing. This one is, because the promise is the whole of
///     what makes it useful and it is not visible from the call site. A helper that quietly returns
///     the position of the run before is one whose assertions pass and go on passing after the thing
///     they are about has moved — which is worse than no helper at all, and is exactly what it did
///     until it was made to count show-text operators rather than strings.
///   </para>
///   <para>
///     Both shapes below come out of the renderer rather than being written by hand, so a change to
///     how it emits text is caught here rather than silently changing what the helper means.
///   </para>
/// </remarks>
public class ShownPositionTests
{
    const double Left = 20;
    const double Top = 40;

    /// <summary>
    ///   Identity-H, whose two-byte codes <c>Tw</c> cannot reach — so a word spacing has to be paid
    ///   out as an adjustment inside a <c>TJ</c> array, which is the shape this is about.
    /// </summary>
    static XFont UnicodeFont => new XFont("Arial", 12, XFontStyle.Regular, XPdfFontOptions.UnicodeDefault);

    static PdfPage PageShowing(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);

        return page;
    }

    [Fact]
    public void ATJArrayIsOneRunAtOnePositionHoweverManyStringsItHolds()
    {
        var spaced = XStringFormats.Default;
        spaced.WordSpacing = 5;

        var page = PageShowing(gfx =>
            gfx.DrawString("one two", UnicodeFont, XBrushes.Black, Left, Top, spaced));

        // The arrangement is only worth asserting about if it really produced the shape: one
        // operator, more than one string in it, and a number between them that is not zero.
        TextOperators.ShowTextOperators(page).Should().Equal(OpCodeName.TJ);
        TextOperators.TJRunCounts(page).Should().Equal(2);
        TextOperators.TJAdjustments(page).Should().Contain(adjustment => adjustment != 0);

        // One entry, not one per string. The numbers inside a TJ array move glyphs within the run;
        // they do not place pieces of it separately, and the run began at the pen. Reporting each
        // string as though it were drawn at the operator's origin would have put the second one
        // several points to the left of where it is.
        var shown = TextOperators.ShownWithPositions(page);

        shown.Should().HaveCount(1);
        shown[0].X.Should().BeApproximately(Left, 1e-6);
        shown[0].Text.Should().HaveLength("one two".Length * 2,
            "the strings of the array are joined, and each glyph is two bytes");
    }

    [Fact]
    public void APageThatMovesThePenByShowingTextIsRefusedRatherThanGuessedAt()
    {
        // Two show-text operators with nothing repositioning the pen between them: the second one
        // starts wherever the first one ended, which is the width of the first — a sum of glyph
        // advances that lives in the font rather than in the content stream. The renderer writes
        // exactly this shape when one string is drawn from more than one face.
        //
        // Built by hand here rather than through font fallback, because what is being pinned is the
        // helper's answer to the shape, not the renderer's reason for writing it.
        var page = PageShowing(gfx =>
        {
            gfx.DrawString("first", UnicodeFont, XBrushes.Black, Left, Top, XStringFormats.Default);
            gfx.DrawString("second", UnicodeFont, XBrushes.Black, Left, Top * 2, XStringFormats.Default);
        });

        Repeated(page);

        var act = () => TextOperators.ShownWithPositions(page);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be worked out from the content stream alone*");
    }

    [Fact]
    public void TheOrderIsStillAnswerableForAPageThatCannotBePlaced()
    {
        // The refusal is on positions alone. Reading order survives, because two runs the pen ran
        // straight through are in the order they were written and that is the order they read in —
        // which is what ShownAcrossThePage sorts by once the positions tie.
        var page = PageShowing(gfx =>
        {
            gfx.DrawString("first", UnicodeFont, XBrushes.Black, Left, Top, XStringFormats.Default);
            gfx.DrawString("second", UnicodeFont, XBrushes.Black, Left, Top * 2, XStringFormats.Default);
        });

        Repeated(page);

        TextOperators.ShownStrings(page).Should().HaveCount(3);
        TextOperators.ShownAcrossThePage(page).Should().HaveCount(3);
    }

    /// <summary>
    ///   Appends a second show-text operator immediately after the last one on the page, with no
    ///   positioning operator between them — so the pen it starts from is wherever the first one
    ///   left it.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The renderer will not write this from two DrawString calls: each of those emits its own
    ///     <c>Td</c> first, on the same line as the show operator. It writes it inside a <em>single</em>
    ///     string drawn from more than one face, which needs the fallback seam and the font-shaping
    ///     collection to arrange. The content stream is edited directly instead, so that this stays
    ///     a test of the reader rather than of the conditions under which the renderer emits it.
    ///   </para>
    ///   <para>
    ///     Only the <c>&lt;…&gt; Tj</c> is copied, deliberately not the whole line — copying the line
    ///     would bring its <c>Td</c> along and reposition the pen, which is the very thing this is
    ///     arranging to be missing.
    ///   </para>
    /// </remarks>
    static void Repeated(PdfPage page)
    {
        var content = page.Contents.Elements.GetDictionary(0) as PdfDictionary;
        content.Should().NotBeNull();

        var stream = System.Text.Encoding.ASCII.GetString(content.Stream.UnfilteredValue);
        var end = stream.LastIndexOf(" Tj", StringComparison.Ordinal) + " Tj".Length;
        end.Should().BeGreaterThan(" Tj".Length, "the arrangement must have drawn something");

        var start = stream.LastIndexOf('<', end);
        start.Should().BeGreaterThan(0, "the run is written as a hex string");

        var edited = stream.Insert(end, "\n" + stream.Substring(start, end - start));
        content.Stream.Value = System.Text.Encoding.ASCII.GetBytes(edited);
        content.Elements.SetInteger("/Length", content.Stream.Length);
        content.Elements.Remove("/Filter");
    }
}
