using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using PdfPinata.Text;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   How far right-to-left text gets up the layout engine, now that <c>DrawString</c> reorders.
/// </summary>
/// <remarks>
///   <para>
///     Reordering lives in the renderer rather than in any layout engine, so a formatter that hands
///     a whole line to <c>DrawString</c> gets it right without having been changed at all. Every
///     alignment but one does exactly that. Justifying cannot - each word has to be placed at an x
///     of its own for the extra room to go between them - so the order the words are placed in is
///     the formatter's own to get right, and that is most of what is tested here.
///   </para>
///   <para>
///     Hebrew rather than Arabic, because Liberation Sans draws both Hebrew and Latin where the
///     Arabic face draws no Latin at all. These tests read glyph identifiers back and compare them,
///     so a face answering <c>.notdef</c> for half the line would make a wrong order
///     indistinguishable from a right one. Nothing here registers a shaper: Hebrew does not join,
///     and reordering is not shaping.
///   </para>
/// </remarks>
public class BidirectionalLayoutTests
{
    // Three Hebrew words of two letters each. Escapes rather than literals, so that a source file
    // mixing right-to-left text with left-to-right code cannot be misread.
    const string First = "\u05D0\u05D1";
    const string Second = "\u05D2\u05D3";
    const string Third = "\u05D4\u05D5";

    static XFont Font() => new XFont("Arial", 12);

    static (PdfPage Page, XTextFormatter Formatter, XGraphics Graphics) Sheet()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PageSize.A6;
        var graphics = XGraphics.FromPdfPage(page);

        return (page, new XTextFormatter(graphics), graphics);
    }

    /// <summary>The glyph a single character draws as, for reading an order back.</summary>
    static int GlyphOf(char letter, XFont font)
        => DrawnText.Glyphs(DrawnText.Page(letter.ToString(), font)).Single();

    static int[] GlyphsOf(string word, XFont font)
        => word.Select(letter => GlyphOf(letter, font)).ToArray();

    /// <summary>The same word with its letters in the order they are drawn.</summary>
    static int[] Reversed(string word, XFont font)
        => GlyphsOf(word, font).Reverse().ToArray();

    /// <summary>
    ///   The runs of glyphs shown on the page, written out so that a failure says which word
    ///   landed where rather than that two arrays are not the same array.
    /// </summary>
    static string Placed(PdfPage page)
        => Written(DrawnText.GlyphRuns(page).ToArray());

    static string Written(params int[][] runs)
        => string.Join(" | ", runs.Select(run => string.Join(",", run)));

    // ----- what the formatter gets for free ---------------------------------------------------------

    [Fact]
    public void AFormatterLineOfRightToLeftTextComesOutInVisualOrder()
    {
        var font = Font();
        var (page, formatter, graphics) = Sheet();

        using (graphics)
            formatter.DrawString(First + " " + Second, font, XBrushes.Black,
                new XRect(12, 12, 200, 50));

        // The formatter joins a line back into one string and draws it in one go, so the whole line
        // goes through the bidirectional algorithm together: the word written first ends up
        // rightmost, and its letters turn round inside it. The space between two right-to-left
        // words is right-to-left too, so the line reverses whole.
        DrawnText.Glyphs(page).Should().Equal(Reversed(First + " " + Second, font),
            "the line is reordered across the words in it and not only inside each of them");
    }

    [Fact]
    public void OneRightToLeftWordIsDrawnLastLetterFirst()
    {
        var font = Font();
        var (page, formatter, graphics) = Sheet();

        using (graphics)
            formatter.DrawString(First, font, XBrushes.Black, new XRect(12, 12, 200, 50));

        DrawnText.Glyphs(page).Should().Equal(Reversed(First, font),
            "no shaper is registered, so nothing joins - but the letters are in the order they "
            + "are read, which is all reordering has to do");
    }

    // ----- and the one it does not ------------------------------------------------------------------

    /// <summary>
    ///   A rectangle wide enough for <paramref name="fits"/> and too narrow for one word more, so
    ///   that which words share the first line does not depend on how wide the face draws them.
    /// </summary>
    static XRect Room(XGraphics graphics, XFont font, string fits, string andOneMore)
    {
        double enough = graphics.MeasureString(fits, font).Width;
        double tooMuch = graphics.MeasureString(andOneMore, font).Width;

        return new XRect(12, 12, (enough + tooMuch) / 2, 100);
    }

    [Fact]
    public void AJustifiedLineIsLaidOutRightToLeftAcrossItsWordsToo()
    {
        var font = Font();
        var (page, formatter, graphics) = Sheet();

        const string line = First + " " + Second + " " + Third;
        formatter.Alignment = XParagraphAlignment.Justify;
        using (graphics)
            formatter.DrawString(line + " " + First, font, XBrushes.Black,
                Room(graphics, font, line, line + " " + First));

        DrawnText.GlyphRuns(page).Should().HaveCount(4,
            "three words justified, and one more on the line below");
        Placed(page).Should().StartWith(
            Written(Reversed(Third, font), Reversed(Second, font), Reversed(First, font)),
            "the word written first is the rightmost, so it is placed last");
    }

    [Fact]
    public void AnEnglishPhraseInsideAJustifiedRightToLeftLineKeepsItsOwnWordOrder()
    {
        // The case that says the words are really being ordered rather than the line being turned
        // round. Reversing a right-to-left line word by word is the obvious implementation and it
        // gets this wrong: two English words inside a Hebrew sentence run left to right between
        // themselves, and only their position in the sentence is right to left.
        var font = Font();
        var (page, formatter, graphics) = Sheet();

        const string line = First + " one two " + Second;
        formatter.Alignment = XParagraphAlignment.Justify;
        using (graphics)
            formatter.DrawString(line + " " + Third, font, XBrushes.Black,
                Room(graphics, font, line, line + " " + Third));

        Placed(page).Should().StartWith(Written(
            Reversed(Second, font), GlyphsOf("one", font), GlyphsOf("two", font),
            Reversed(First, font)));
    }

    [Fact]
    public void AJustifiedLeftToRightLineIsPlacedExactlyAsItAlwaysWas()
    {
        var font = Font();
        var (page, formatter, graphics) = Sheet();

        const string line = "one two three";
        formatter.Alignment = XParagraphAlignment.Justify;
        using (graphics)
            formatter.DrawString(line + " four", font, XBrushes.Black,
                Room(graphics, font, line, line + " four"));

        Placed(page).Should().StartWith(
            Written(GlyphsOf("one", font), GlyphsOf("two", font), GlyphsOf("three", font)),
            "a line with nothing right to left in it is not reordered at all");
    }

    // ----- saying which way it runs rather than leaving it to be guessed --------------------------

    [Fact]
    public void ADeclaredDirectionOverridesWhatTheFirstStrongCharacterSays()
    {
        // "one" is the first strong character of this line, so the algorithm left to itself makes
        // the whole line left to right and puts the Hebrew after it. Declaring the paragraph right
        // to left puts the Hebrew first, where a reader of Hebrew expects it.
        var font = Font();
        var (page, formatter, graphics) = Sheet();

        formatter.TextDirection = BidiParagraphDirection.RightToLeft;
        using (graphics)
            formatter.DrawString("one " + First, font, XBrushes.Black, new XRect(12, 12, 260, 50));

        DrawnText.Glyphs(page).Should().Equal(
            Reversed(First, font)
                .Concat(new[] { GlyphOf(' ', font) })
                .Concat(GlyphsOf("one", font)),
            "the Hebrew is drawn leftmost although it was written last");
    }

    [Fact]
    public void LeavingItToBeGuessedIsStillWhatHappensByDefault()
    {
        var font = Font();
        var (page, formatter, graphics) = Sheet();

        using (graphics)
            formatter.DrawString("one " + First, font, XBrushes.Black, new XRect(12, 12, 260, 50));

        DrawnText.Glyphs(page).Should().Equal(
            GlyphsOf("one ", font).Concat(Reversed(First, font)),
            "the first strong character is Latin, so the line reads left to right");
    }
}
