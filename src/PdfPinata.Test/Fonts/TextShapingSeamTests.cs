using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   The fourth static seam: what a registered <see cref="ITextShaper"/> gets to decide, and what
///   happens when there is none. Nothing here shapes anything for real - the shapers below are
///   stubs that answer a fixed script - because the question is whether the library asks the seam
///   and believes the answer, not whether HarfBuzz works.
/// </summary>
/// <remarks>
///   <para>
///     <see cref="GlobalFontSettings.TextShaper"/> is per application domain, and unlike the other
///     seams it is read by every path that measures or draws a character. A test that installed a
///     shaper answering every run would therefore corrupt whichever other test happened to be
///     drawing text beside it, xUnit running collections in parallel.
///   </para>
///   <para>
///     So every shaper here is <see cref="SelectiveShaper"/>: it answers for one sentinel string of
///     its own and returns null - "not mine" - for everything else, and a null answer is exactly
///     the path that falls back to the unshaped run. Concurrent tests measuring their own text get
///     the behaviour they would have got with no shaper at all. The tests are kept in one class so
///     that they do not install shapers over one another, xUnit running a class's tests in
///     sequence.
///   </para>
///   <para>
///     Hence the <c>seam-</c> on the front of every sentinel: it is what makes the string one no
///     other test in the suite could be drawing at the same moment. It reads as noise and it is
///     load-bearing. It is also deliberately <b>plain ASCII</b> - these sentinels were once made
///     unique by an invisible private-use character instead, which worked for exactly as long as
///     the whole string was shaped as one run. Itemisation gives a private-use character a run of
///     its own, script Unknown, and the sentinel then arrived at the shaper in two pieces that
///     matched nothing. A test fixture you cannot see is a test fixture you cannot debug.
///   </para>
/// </remarks>
[Collection(TextShapingCollection.Name)]
public class TextShapingSeamTests
{
    // ----- the stubs -----------------------------------------------------------------------------

    /// <summary>
    ///   A shaper that answers for one string and declines every other, so that installing it
    ///   cannot disturb anything else running at the same time.
    /// </summary>
    sealed class SelectiveShaper : ITextShaper
    {
        readonly string _mine;
        readonly Func<ShapingFont, IReadOnlyList<ShapedGlyph>> _glyphs;
        readonly XTextDirection _direction;

        internal SelectiveShaper(string mine, Func<ShapingFont, IReadOnlyList<ShapedGlyph>> glyphs,
            XTextDirection direction = XTextDirection.LeftToRight)
        {
            _mine = mine;
            _glyphs = glyphs;
            _direction = direction;
        }

        internal int Calls { get; private set; }

        internal ShapingFont LastFont { get; private set; }

        internal string LastScript { get; private set; }

        internal string LastLanguage { get; private set; }

        internal XTextDirection LastDirection { get; private set; }

        public ShapedRun Shape(ReadOnlySpan<char> text, ShapingFont font, XTextDirection direction,
            string script, string language)
        {
            if (!text.SequenceEqual(_mine.AsSpan()))
                return null;

            Calls++;
            LastFont = font;
            LastScript = script;
            LastLanguage = language;
            LastDirection = direction;

            return new ShapedRun(_glyphs(font), font.UnitsPerEm, _direction);
        }
    }

    static IDisposable Installed(ITextShaper shaper)
    {
        GlobalFontSettings.TextShaper = shaper;
        return new Uninstall();
    }

    sealed class Uninstall : IDisposable
    {
        public void Dispose() => GlobalFontSettings.TextShaper = null;
    }

    static XFont Font() => new XFont("Arial", 20);

    static IReadOnlyList<int[]> GlyphRuns(PdfPage page) => DrawnText.GlyphRuns(page);

    /// <summary>
    ///   Every glyph the page shows, whichever show-text operators they were written in.
    /// </summary>
    /// <remarks>
    ///   For the tests whose subject is <em>which</em> glyphs a shaper chose rather than how many
    ///   operators they arrived in. A glyph standing for several characters is now wrapped in a
    ///   marked-content sequence carrying its <c>/ActualText</c>, which cuts the run around it — and
    ///   the fixtures here hand back fewer glyphs than there are characters, so most of them contain
    ///   such a glyph by construction. Flattening keeps those tests about the one thing they name.
    /// </remarks>
    static int[] AllGlyphs(PdfPage page) => GlyphRuns(page).SelectMany(run => run).ToArray();

    static PdfPage Drawn(string text, XStringFormat format = null)
        => DrawnText.Page(text, Font(), format);

    static double MeasuredWidth(string text) => DrawnText.MeasuredWidth(text, Font());

    // ----- the unset seam ------------------------------------------------------------------------

    [Fact]
    public void NoShaperIsRegisteredUntilSomebodyRegistersOne()
    {
        // Unlike the other three seams, reading this one unset is not an error: there is a working
        // default behind it, and it is what this library has always done.
        GlobalFontSettings.TextShaper.Should().BeNull();
    }

    [Fact]
    public void AShaperCanBeTakenAwayAgain()
    {
        var shaper = new SelectiveShaper("seam-away", _ => Array.Empty<ShapedGlyph>());

        using (Installed(shaper))
            GlobalFontSettings.TextShaper.Should().BeSameAs(shaper);

        GlobalFontSettings.TextShaper.Should().BeNull(
            "nothing is cached against the shaper, so clearing it is allowed and puts the "
            + "unshaped behaviour back");
    }

    [Fact]
    public void IsTextShaperSetAnswersWhetherOneIsInstalledRightNow()
    {
        var shaper = new SelectiveShaper("seam-is-set", _ => Array.Empty<ShapedGlyph>());

        GlobalFontSettings.IsTextShaperSet.Should().BeFalse(
            "nothing is installed before this test installs one");

        using (Installed(shaper))
            GlobalFontSettings.IsTextShaperSet.Should().BeTrue();

        GlobalFontSettings.IsTextShaperSet.Should().BeFalse("and clearing it answers false again");
    }

    [Fact]
    public void TextShaperLifecycleSaysItMayBeSetAtAnyTime()
    {
        GlobalFontSettings.TextShaperLifecycle.Should().Be(SeamLifecycle.SetAnytime,
            "null is a working default and installing, replacing or clearing a shaper is always allowed");
    }

    // ----- what a shaper gets to decide ----------------------------------------------------------

    [Fact]
    public void AShaperDecidesWhichGlyphsAreDrawn()
    {
        const string text = "seam-glyphs";
        var unshaped = AllGlyphs(Drawn(text));

        using var _ = Installed(new SelectiveShaper(text, font => new[]
        {
            new ShapedGlyph(41, 0, 500),
            new ShapedGlyph(42, 1, 500)
        }));

        var shaped = AllGlyphs(Drawn(text));

        shaped.Should().Equal(new[] { 41, 42 }, "the seam says what glyphs a string draws as");
        shaped.Should().NotEqual(unshaped, "and it said something different from the cmap");
    }

    [Fact]
    public void AShaperDecidesHowWideTheTextMeasures()
    {
        const string text = "seam-width";
        var font = Font();

        // A run one em wide, whatever the characters would have measured on their own.
        using var _ = Installed(new SelectiveShaper(text,
            f => new[] { new ShapedGlyph(1, 0, f.UnitsPerEm) }));

        MeasuredWidth(text).Should().BeApproximately(font.Size, 1e-9,
            "the width is the sum of the shaped advances, scaled to the em size");
    }

    [Fact]
    public void MeasuringAndDrawingAskTheSameSeamAndSoAgreeWithOneAnother()
    {
        const string text = "seam-agree";

        // Two glyphs for six characters, and each of them half an em wide.
        using var _ = Installed(new SelectiveShaper(text, f => new[]
        {
            new ShapedGlyph(7, 0, f.UnitsPerEm / 2),
            new ShapedGlyph(8, 3, f.UnitsPerEm / 2)
        }));

        AllGlyphs(Drawn(text)).Should().HaveCount(2);
        MeasuredWidth(text).Should().BeApproximately(Font().Size, 1e-9);
    }

    [Fact]
    public void ALigatureIsOneGlyphForMoreThanOneCharacter()
    {
        const string text = "seam-fi";

        using var _ = Installed(new SelectiveShaper(text,
            f => new[] { new ShapedGlyph(300, 0, 900) }));

        GlyphRuns(Drawn(text)).Single().Should().Equal(new[] { 300 },
            "a whole string became one glyph, and nothing in the write path assumes otherwise");
    }

    [Fact]
    public void AShaperThatDeclinesARunLeavesItExactlyAsItWas()
    {
        const string text = "seam-declined";
        var before = GlyphRuns(Drawn(text)).Single();
        var width = MeasuredWidth(text);

        // Registered, but this is not its string.
        using var _ = Installed(new SelectiveShaper("seam-something else",
            f => new[] { new ShapedGlyph(1, 0, 1) }));

        GlyphRuns(Drawn(text)).Single().Should().Equal(before);
        MeasuredWidth(text).Should().Be(width);
    }

    [Fact]
    public void TheShaperIsHandedTheFontWhoseBytesItMustReadFrom()
    {
        const string text = "seam-bytes";
        var shaper = new SelectiveShaper(text, f => new[] { new ShapedGlyph(1, 0, 100) });

        using var _ = Installed(shaper);
        Drawn(text);

        shaper.Calls.Should().BeGreaterThan(0);
        shaper.LastFont.Should().NotBeNull();
        shaper.LastFont.Bytes.Length.Should().BeGreaterThan(0,
            "a shaper resolving a family for itself would one day disagree with the font resolver "
            + "about which face that family means, and the glyph numbers it returns are indices "
            + "into one particular file");
        shaper.LastFont.UnitsPerEm.Should().BeGreaterThan(0);
        shaper.LastFont.EmSize.Should().Be(Font().Size);
        shaper.LastFont.Key.Should().NotBeNullOrEmpty(
            "a shaper keeps its own parsed face between calls and needs something to key it on");
    }

    [Fact]
    public void TheSameFontIsTheSameFaceEveryTimeTheShaperSeesIt()
    {
        const string text = "stable";
        var shaper = new SelectiveShaper(text, f => new[] { new ShapedGlyph(1, 0, 100) });

        using var _ = Installed(shaper);
        Drawn(text);
        var first = shaper.LastFont;
        Drawn(text);

        shaper.LastFont.Key.Should().Be(first.Key);
        shaper.LastFont.FaceName.Should().Be(first.FaceName);
    }

    [Fact]
    public void ARunWithNothingSaidAboutItIsLeftToTheShaperToDecide()
    {
        const string text = "seam-defaults";
        var shaper = new SelectiveShaper(text, f => new[] { new ShapedGlyph(1, 0, 100) });

        using var _ = Installed(shaper);
        Drawn(text);

        shaper.LastDirection.Should().Be(XTextDirection.LeftToRight,
            "there is nothing right to left in it");
        shaper.LastScript.Should().BeNull(
            "text that can only be one run is not itemised at all, so nothing had to be said "
            + "about it - and this library never guesses");
        shaper.LastLanguage.Should().BeNull(
            "nor a language - guessing is how Turkish dotless i comes out wrong");
    }

    // ----- the places that assumed one glyph per character ---------------------------------------

    [Fact]
    public void WordSpacingIsPaidOutWhereTheSpaceReallyFellAndNotWhereItsCharacterIs()
    {
        // "ab cd" with "ab" ligated is four glyphs for five characters, so the space is glyph 1
        // and character 2. Splitting the run by character index would put the extra room inside
        // "cd" instead of after the space.
        const string text = "ab cd";
        var format = new XStringFormat { WordSpacing = 4 };

        using var _ = Installed(new SelectiveShaper(text, f => new[]
        {
            new ShapedGlyph(70, 0, 500),   // the ab ligature
            new ShapedGlyph(3, 2, 250),    // the space
            new ShapedGlyph(70, 3, 500),
            new ShapedGlyph(71, 4, 500)
        }));

        var runs = GlyphRuns(Drawn(text, format));

        // Three, not two: the ab ligature is one glyph standing for two characters, so it is shown
        // inside a sequence of its own carrying /ActualText (ab). What the test is about survives that
        // intact - the room still falls after the space rather than inside "cd", which is the whole
        // point, and it is the boundary between the last two runs that says so.
        runs.Should().HaveCount(3);
        runs[0].Should().Equal(new[] { 70 }, "the ligature, wrapped in what says it spells ab");
        runs[1].Should().Equal(new[] { 3 }, "up to and including the space");
        runs[2].Should().Equal(new[] { 70, 71 }, "and the rest after the room the space paid for");
    }

    [Fact]
    public void WordSpacingGoesAfterTheLastGlyphOfTheSpacesCluster()
    {
        const string text = "a b";
        var format = new XStringFormat { WordSpacing = 4 };

        // A space that shaped into two glyphs - odd, but the split must not land between them.
        using var _ = Installed(new SelectiveShaper(text, f => new[]
        {
            new ShapedGlyph(10, 0, 500),
            new ShapedGlyph(11, 1, 125),
            new ShapedGlyph(12, 1, 125),
            new ShapedGlyph(13, 2, 500)
        }));

        var runs = GlyphRuns(Drawn(text, format));

        runs.Should().HaveCount(2);
        runs[0].Should().Equal(10, 11, 12);
        runs[1].Should().Equal(13);
    }

    // ----- displacing a glyph off the pen position -----------------------------------------------

    /// <summary>The whole content stream of a page, as the renderer wrote it.</summary>
    static string Content(PdfPage page) => DrawnText.ContentOf(page);

    [Fact]
    public void AGlyphAShaperWantsMovedSidewaysIsMovedAndPutBack()
    {
        const string text = "sideways";

        // The second glyph is drawn a quarter of an em to the right of where the pen is, without
        // the run growing by it - which is how an attached mark is placed. A quarter rather than
        // some other fraction so that it comes to a round 250 thousandths of an em.
        using var _ = Installed(new SelectiveShaper(text, f => new[]
        {
            new ShapedGlyph(10, 0, f.UnitsPerEm / 2),
            new ShapedGlyph(11, 1, f.UnitsPerEm / 2, offsetX: f.UnitsPerEm / 4)
        }));

        var content = Content(Drawn(text));

        // The displacement and nothing about what precedes it. The second glyph stands for the rest of
        // this fixture's text, so it is wrapped in a sequence carrying its /ActualText and the first
        // glyph is shown separately - which is a fact about the fixture rather than about the
        // mechanism under test, and asserting the two glyphs were adjacent would be asserting it.
        content.Should().Contain("-250 <000B> 250",
            "a TJ number moves the pen back, so moving right is negative - and the displacement "
            + "is taken off again after the glyph, so that what follows is not carried with it");
    }

    [Fact]
    public void AGlyphAShaperWantsRaisedIsRaisedAndTheRiseIsPutBack()
    {
        const string text = "raised";

        using var _ = Installed(new SelectiveShaper(text, f => new[]
        {
            new ShapedGlyph(10, 0, f.UnitsPerEm / 2),
            new ShapedGlyph(11, 1, f.UnitsPerEm / 2, offsetY: f.UnitsPerEm / 4)
        }));

        var content = Content(Drawn(text));

        // A quarter of an em at 20 points is 5 points of text rise. There is no operand for this
        // the way there is for a sideways move, so the run is shown in one piece per height.
        content.Should().Contain("5 Ts", "the raised glyph is shown with the rise set");
        content.Should().Contain("0 Ts", "and the rise is put back, because it is text state and "
            + "would otherwise carry into everything drawn after it");
    }

    [Fact]
    public void AGlyphIsRaisedFromWhateverRiseTheTextIsAlreadyAt()
    {
        const string text = "seam-superscript";

        // The caller has asked for the whole string to sit three points above the baseline, and
        // the shaper wants one glyph a further five above that. The two compose: PdfGraphicsState
        // has already written the caller's rise and believes it is still there, so a glyph raised
        // from zero would be drawn low and the state would be left saying something untrue about
        // every string after it.
        using var _ = Installed(new SelectiveShaper(text, f => new[]
        {
            new ShapedGlyph(10, 0, f.UnitsPerEm / 2),
            new ShapedGlyph(11, 1, f.UnitsPerEm / 2, offsetY: f.UnitsPerEm / 4)
        }));

        var content = Content(Drawn(text, new XStringFormat { TextRise = 3 }));

        content.Should().Contain("8 Ts", "three points of text rise and five of glyph offset");
        content.Should().NotContain("0 Ts",
            "the rise is put back to what the caller asked for, not to nothing");
        content.TrimEnd().Should().Contain("3 Ts",
            "which is what the graphics state still thinks it is");
    }

    [Fact]
    public void ARunWithNothingToDisplaceIsStillTheSamePlainTj()
    {
        const string text = "plain";

        using var _ = Installed(new SelectiveShaper(text, f => new[]
        {
            new ShapedGlyph(10, 0, 500),
            new ShapedGlyph(11, 1, 500)
        }));

        var content = Content(Drawn(text));

        content.Should().Contain("Tj").And.NotContain("TJ",
            "a run that needs no displacement is written the way it always was, and a TJ array "
            + "would only be a longer way of saying the same thing");
        content.Should().NotContain("Ts");
    }

    // ----- the shaped run itself -----------------------------------------------------------------

    [Fact]
    public void ARunIsAsWideAsItsAdvancesAddUpTo()
    {
        var run = new ShapedRun(new[]
        {
            new ShapedGlyph(1, 0, 500),
            new ShapedGlyph(2, 1, 250)
        }, unitsPerEm: 1000);

        run.Width.Should().Be(750);
        run.WidthAt(12).Should().BeApproximately(9, 1e-9, "750/1000 of a 12 point em");
    }

    [Fact]
    public void AnEmptyRunIsARunAndNotANull()
    {
        var run = ShapedRun.Empty(2048);

        run.Glyphs.Should().BeEmpty();
        run.Width.Should().Be(0);
        run.UnitsPerEm.Should().Be(2048);
        run.Direction.Should().Be(XTextDirection.LeftToRight);
    }

    [Fact]
    public void ARunOfNoGlyphsAtAllIsRefused()
    {
        var act = () => new ShapedRun(null, 1000);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public void ARunMustSayWhatItsAdvancesAreMeasuredAgainst(int unitsPerEm)
    {
        // Advances are in design units, so a face of no em has no scale to read them at.
        var act = () => new ShapedRun(Array.Empty<ShapedGlyph>(), unitsPerEm);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AGlyphOnItsPenPositionSaysSo()
    {
        new ShapedGlyph(1, 0, 500).IsOnBaselineOrigin.Should().BeTrue();
        new ShapedGlyph(1, 0, 500, offsetX: 10).IsOnBaselineOrigin.Should().BeFalse();
        new ShapedGlyph(1, 0, 500, offsetY: -10).IsOnBaselineOrigin.Should().BeFalse();
    }

    [Fact]
    public void ARunRemembersWhichWayItWasWritten()
    {
        var run = new ShapedRun(new[] { new ShapedGlyph(1, 1, 500), new ShapedGlyph(2, 0, 500) },
            1000, XTextDirection.RightToLeft);

        run.Direction.Should().Be(XTextDirection.RightToLeft);
        run.Glyphs.Select(glyph => glyph.Cluster).Should().Equal(new[] { 1, 0 },
            "a right-to-left run is handed over already reversed, so its clusters descend");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AFaceWithoutAKeyIsRefused(string key)
    {
        // The key is what a shaper caches its parsed face under, and the type's own documentation
        // promises it is distinct per face. Nothing enforced that, so every keyless face shared one
        // cache entry: the second one shaped would come back with glyph identifiers read out of the
        // first one's file. Those identifiers index a font and nothing downstream checks which, so
        // the page would have drawn whatever that other face happened to have at those numbers -
        // wrong, silently, and only where the second font was used.
        var act = () => new ShapingFont("Family", "Face", key,
            isBold: false, isItalic: false, emSize: 12, unitsPerEm: 1000, bytes: Array.Empty<byte>());

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(key));
    }
}
