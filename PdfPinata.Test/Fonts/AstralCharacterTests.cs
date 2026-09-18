using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;
using PdfPinata.Pdf.IO;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   Characters above the basic multilingual plane, which this library could not draw at all until
///   it learned to read a <c>cmap</c> format 12 subtable.
/// </summary>
/// <remarks>
///   <para>
///     An emoji was three failures at once, and they compounded. The unshaped path looked each
///     UTF-16 code unit up in a format 4 subtable, so a surrogate pair drew <c>.notdef</c>
///     <em>twice</em> - one character, two empty boxes. Coverage was asked the same way, so no face
///     could ever answer "yes" for an astral character. And because coverage could not answer,
///     font fallback could not be offered one either: there was no question to ask.
///   </para>
///   <para>
///     Fixing the reader fixes all three, because all three go through
///     <c>OpenTypeDescriptor.CharCodeToGlyphIndex</c>. What is pinned here is each of them
///     separately, so that a regression says which one broke.
///   </para>
///   <para>
///     The faces are the ones already checked in. Source Code Pro carries a format 12 subtable with
///     four real emoji in it, and Liberation Sans carries no format 12 subtable at all - which
///     makes the pair of them exactly the fallback case, with no new font asset needed.
///   </para>
/// </remarks>
[Collection(TextShapingCollection.Name)]
public class AstralCharacterTests
{
    // Emoji that Source Code Pro really has, confirmed against its own cmap rather than assumed:
    // U+1F512 LOCK, U+1F916 ROBOT FACE, U+1F3B5 MUSICAL NOTE.
    const int Lock = 0x1F512;
    const int Robot = 0x1F916;

    // MATHEMATICAL BOLD CAPITAL A. In neither face, so it is the "nobody can draw it" case.
    const int BoldA = 0x1D400;

    static string Text(int codePoint) => char.ConvertFromUtf32(codePoint);

    /// <summary>The face with a format 12 subtable.</summary>
    static XFont WithFormat12() => new XFont(PinnedFontResolver.CffFamilyName, 20);

    /// <summary>The face without one.</summary>
    static XFont WithoutFormat12() => new XFont("Arial", 20);

    sealed class Installed : IDisposable
    {
        internal Installed(IFontFallback fallback) => GlobalFontSettings.FontFallback = fallback;

        public void Dispose() => GlobalFontSettings.FontFallback = null;
    }

    /// <summary>A fallback offering one family, and only for the code points named.</summary>
    sealed class Only : IFontFallback
    {
        readonly HashSet<int> _mine;
        readonly string[] _families;

        internal Only(IEnumerable<int> codePoints, params string[] families)
        {
            _mine = new HashSet<int>(codePoints);
            _families = families;
        }

        public IEnumerable<string> FamiliesFor(int codePoint, bool isBold, bool isItalic)
            => _mine.Contains(codePoint) ? _families : Enumerable.Empty<string>();
    }

    // ----- one character, one glyph ----------------------------------------------------------------

    [Fact]
    public void AnAstralCharacterIsOneGlyphAndNotTwo()
    {
        // The heart of it. Two glyphs for one character was not merely wrong on the page - it made
        // the character undrawable in principle, because no cmap maps a lone surrogate.
        DrawnText.Glyphs(DrawnText.Page(Text(Lock), WithFormat12()))
            .Should().HaveCount(1, "a surrogate pair is one character");
    }

    [Fact]
    public void AFaceThatHasTheCharacterDrawsIt()
    {
        var glyphs = DrawnText.Glyphs(DrawnText.Page(Text(Lock), WithFormat12()));

        glyphs.Single().Should().NotBe(0,
            "Source Code Pro has U+1F512 in its format 12 subtable, so it is not .notdef");
    }

    [Fact]
    public void TwoDifferentAstralCharactersGetTwoDifferentGlyphs()
    {
        // Guards against a reader that answers plausibly but reads the wrong group - one constant
        // wrong answer would satisfy every test above this one.
        int drawnLock = DrawnText.Glyphs(DrawnText.Page(Text(Lock), WithFormat12())).Single();
        int drawnRobot = DrawnText.Glyphs(DrawnText.Page(Text(Robot), WithFormat12())).Single();

        drawnLock.Should().NotBe(drawnRobot);
    }

    [Fact]
    public void AFaceWithoutTheCharacterDrawsOneNotdefRatherThanTwo()
    {
        // Liberation Sans has no format 12 subtable. The answer is still .notdef - it always was -
        // but it is one .notdef for one character, where it used to be one per surrogate.
        DrawnText.Glyphs(DrawnText.Page(Text(Lock), WithoutFormat12()))
            .Should().Equal(new[] { 0 });
    }

    [Fact]
    public void ACharacterNoFaceHasIsStillOneNotdef()
    {
        DrawnText.Glyphs(DrawnText.Page(Text(BoldA), WithFormat12()))
            .Should().Equal(new[] { 0 });
    }

    // ----- measuring agrees with drawing -----------------------------------------------------------

    [Fact]
    public void AnAstralCharacterIsMeasuredAsOneCharacter()
    {
        // Measuring and drawing go through the same seam, so a width that still counted two
        // .notdef would put every following word in the wrong place.
        var font = WithFormat12();
        double one = DrawnText.MeasuredWidth(Text(Lock), font);

        one.Should().BeGreaterThan(0, "one character has one advance, and it is not a zero one");

        // The assertion that would have caught the old behaviour: two .notdef per character made a
        // surrogate pair measure as two glyphs, so this was never the width of one character.
        one.Should().BeApproximately(DrawnText.MeasuredWidth("A", font), 0.5 * one,
            "one astral character is about as wide as one ordinary one, not twice");

        DrawnText.MeasuredWidth(Text(Lock) + Text(Lock), font).Should().BeApproximately(2 * one, 1e-9,
            "two of the same character are twice one of them");
    }

    // ----- the character survives the trip out ------------------------------------------------------

    [Fact]
    public void TheSurrogatePairIsRecoveredWholeByToUnicode()
    {
        // The cluster of the one glyph is the index of the high surrogate and the run ends two code
        // units later, so what the glyph stands for is the pair rather than half of it. This is the
        // half of the change a reader sees: a destination of one code unit here would mean copying
        // the emoji out of the page yielded half a character.
        string cmap = ToUnicodeCMapOf(DrawnText.Page(Text(Lock), WithFormat12()));

        cmap.Should().NotBeNull("an embedded Identity-H font carries a /ToUnicode CMap");

        // A glyph standing for two code units cannot be written as a bfrange, whose destination is
        // a single code - so it has to be a bfchar, and its destination is the pair in UTF-16BE.
        cmap.Should().Contain("beginbfchar");
        cmap.Should().Contain("<D83DDD12>",
            "U+1F512 is D83D DD12 in UTF-16, and both code units have to be in the destination");
    }

    /// <summary>
    ///   The <c>/ToUnicode</c> CMap of the font on a page, read back out of the saved document.
    /// </summary>
    /// <remarks>
    ///   Saved and reopened rather than inspected in place, because the CMap is written at save
    ///   time - asking the object model before that finds nothing. The stream is found by its
    ///   content rather than by walking the font dictionary down to it: there is one CMap in these
    ///   one-font documents, and <c>begincmap</c> identifies it without depending on the shape of
    ///   the font dictionary this test is not about.
    /// </remarks>
    static string ToUnicodeCMapOf(PdfPage page)
    {
        using MemoryStream saved = new MemoryStream();
        page.Owner.Save(saved, false);
        saved.Position = 0;

        using PdfDocument reopened = Reader.Open(saved, PdfDocumentOpenMode.ReadOnly);
        foreach (PdfObject item in reopened.Internals.GetAllObjects())
        {
            if (item is PdfDictionary dictionary && dictionary.Stream != null)
            {
                string decoded = Encoding.ASCII.GetString(dictionary.Stream.UnfilteredValue);
                if (decoded.Contains("begincmap"))
                    return decoded;
            }
        }

        return null;
    }

    // ----- and the reason all this matters: fallback ------------------------------------------------

    [Fact]
    public void AnAstralCharacterCanNowBeOfferedForFallback()
    {
        // The third failure, and the one that could not even be attempted before: Liberation Sans
        // cannot draw the lock, Source Code Pro can, and until coverage could answer for an astral
        // character there was no way to find that out.
        using var _ = new Installed(new Only(new[] { Lock }, PinnedFontResolver.CffFamilyName));

        var glyphs = DrawnText.Glyphs(DrawnText.Page(Text(Lock), WithoutFormat12()));

        glyphs.Should().HaveCount(1);
        glyphs.Single().Should().NotBe(0,
            "the character was drawn by the fallback face, which really has it");
    }

    [Fact]
    public void TheFallbackFaceIsSelectedInTheContentStream()
    {
        // Not merely a non-zero glyph: a second font resource, selected with its own Tf. A glyph
        // number alone could come from the original face by accident.
        using var _ = new Installed(new Only(new[] { Lock }, PinnedFontResolver.CffFamilyName));

        string content = DrawnText.ContentOf(
            DrawnText.Page("A" + Text(Lock) + "B", WithoutFormat12()));

        Regex.Matches(content, @"/F\d+ [\d.]+ Tf")
            .Select(match => match.Value).Distinct()
            .Should().HaveCount(2, "the Latin and the emoji are drawn from different faces");
    }

    [Fact]
    public void AFallbackIsNotTakenWhenTheFaceCanAlreadyDrawIt()
    {
        // Coverage answering "yes" for an astral character has to stop the fallback, or a face
        // that was perfectly able to draw the character would be replaced anyway.
        using var _ = new Installed(new Only(new[] { Lock }, "Arial"));

        string content = DrawnText.ContentOf(DrawnText.Page(Text(Lock), WithFormat12()));

        Regex.Matches(content, @"/F\d+ [\d.]+ Tf")
            .Select(match => match.Value).Distinct()
            .Should().HaveCount(1, "Source Code Pro has the character, so nothing is fallen back to");
    }

    // ----- a face that lies about its own tables ------------------------------------------------------

    [Fact]
    public void AFormat12SubtableClaimingMoreGroupsThanItsOwnLengthIsRefused()
    {
        // The count is checked against the subtable's own declared length, which is the cheap half.
        var bytes = WithGroupCount(0x7FFFFFFF);

        Reading(bytes, "more groups than the declared length holds").Should().Throw<Exception>()
            .And.Should().NotBeOfType<OutOfMemoryException>();
    }

    [Fact]
    public void AFormat12SubtableWhoseLengthRunsPastTheFileIsRefused()
    {
        // The half the declared length cannot answer for, because the subtable declares that too: a
        // length of 256MB makes room for twenty million groups by its own arithmetic, and the file is
        // 280KB. The allocation is the danger rather than the parse - the largest a 32-bit length can
        // authorise is 4.3GB of groups, and the OutOfMemoryException that raises is one
        // Unrecoverable.Is deliberately refuses to wrap, so it would come out of the font reader as a
        // process-level failure rather than as "this font is broken".
        //
        // What this pins is that such a file is refused. It does not distinguish the available-bytes
        // check from the declared-length one, because a count big enough to be caught only by the
        // former is a count big enough to exhaust the machine if the guard is ever removed - which is
        // not a thing to do in a test run. Only AFormat12SubtableShorterThanItsOwnHeaderIsRefused
        // fails without the guard it was written for.
        var bytes = WithDeclaredLength(0x10000000);
        WriteFormat12GroupCount(bytes, 0x00FFFFFF);

        Reading(bytes, "a length running past the end of the file").Should().Throw<Exception>()
            .And.Should().NotBeOfType<OutOfMemoryException>();
    }

    static Action Reading(byte[] bytes, string why)
        => () => new XFont(RegisterCorrupt(bytes, why), 20).GetHeight();

    [Fact]
    public void AFormat12SubtableShorterThanItsOwnHeaderIsRefused()
    {
        // The unsigned subtraction that made this worth a test of its own: a declared length below
        // the 16-byte header used to wrap to something enormous rather than go negative, so the
        // bound computed from it let everything through.
        var bytes = WithDeclaredLength(4);

        Action reading = () => new XFont(RegisterCorrupt(bytes, "short length"), 20).GetHeight();

        reading.Should().Throw<Exception>()
            .And.Should().NotBeOfType<OutOfMemoryException>();
    }

    /// <summary>The Devanagari face with its format 12 group count overwritten.</summary>
    static byte[] WithGroupCount(uint groups) => WithFormat12Field(12, groups);

    /// <summary>Overwrites the group count of an already-corrupted copy, in place.</summary>
    static void WriteFormat12GroupCount(byte[] bytes, uint groups) => SetFormat12Field(bytes, 12, groups);

    /// <summary>The Devanagari face with its format 12 declared length overwritten.</summary>
    static byte[] WithDeclaredLength(uint length) => WithFormat12Field(4, length);

    /// <summary>
    ///   A real face with one 32-bit field of its format 12 subtable overwritten, at the given
    ///   offset from the start of that subtable.
    /// </summary>
    /// <remarks>
    ///   Built from a real font rather than from a hand-made one, so that everything the reader looks
    ///   at before it reaches this field is genuine and the test is about the field.
    /// </remarks>
    static byte[] WithFormat12Field(int offset, uint value)
    {
        var bytes = File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "Assets", "Fonts", "NotoSansDevanagari-Regular.ttf"));

        SetFormat12Field(bytes, offset, value);
        return bytes;
    }

    static void SetFormat12Field(byte[] bytes, int offset, uint value)
    {
        int cmap = TableOffset(bytes, "cmap");
        int subtables = (bytes[cmap + 2] << 8) | bytes[cmap + 3];

        for (int idx = 0; idx < subtables; idx++)
        {
            int record = cmap + 4 + idx * 8;
            int platform = (bytes[record] << 8) | bytes[record + 1];
            int encoding = (bytes[record + 2] << 8) | bytes[record + 3];
            if (platform != 3 || encoding != 10)
                continue;

            int at = cmap + (int)ReadUInt32(bytes, record + 4);
            WriteUInt32(bytes, at + offset, value);
            return;
        }

        throw new InvalidOperationException("The face has no format 12 subtable to corrupt.");
    }

    static int TableOffset(byte[] bytes, string tag)
    {
        int count = (bytes[4] << 8) | bytes[5];
        for (int idx = 0; idx < count; idx++)
        {
            int record = 12 + idx * 16;
            if (Encoding.ASCII.GetString(bytes, record, 4) == tag)
                return (int)ReadUInt32(bytes, record + 8);
        }

        throw new InvalidOperationException("No " + tag + " table.");
    }

    static uint ReadUInt32(byte[] bytes, int at)
        => ((uint)bytes[at] << 24) | ((uint)bytes[at + 1] << 16)
           | ((uint)bytes[at + 2] << 8) | bytes[at + 3];

    static void WriteUInt32(byte[] bytes, int at, uint value)
    {
        bytes[at] = (byte)(value >> 24);
        bytes[at + 1] = (byte)(value >> 16);
        bytes[at + 2] = (byte)(value >> 8);
        bytes[at + 3] = (byte)value;
    }

    /// <summary>Registers the given bytes under a family name of their own and returns it.</summary>
    static string RegisterCorrupt(byte[] bytes, string why)
    {
        var family = "Corrupt " + why;
        PinnedFontResolver.Register(family, bytes);
        return family;
    }

    // ----- what must not have changed ----------------------------------------------------------------

    [Fact]
    public void OrdinaryTextIsUntouched()
    {
        // The whole change is guarded by a code point above 0xFFFF, and this is the assertion that
        // says so: a face carrying a format 12 subtable still answers for ordinary text out of its
        // format 4 one, exactly as it did.
        var font = WithFormat12();

        DrawnText.Glyphs(DrawnText.Page("Hello", font)).Should().HaveCount(5);
        DrawnText.ContentOf(DrawnText.Page("Hello", font)).Should().Contain(" Tj");
    }

    [Fact]
    public void AnUnpairedSurrogateIsStillOneGlyphAndDrawsNothingReal()
    {
        // A lone high surrogate is not a character and cannot be completed. It must not be read as
        // the start of a pair that is not there, and it must not swallow the character after it.
        string text = "\uD83D" + "A";

        var glyphs = DrawnText.Glyphs(DrawnText.Page(text, WithFormat12()));

        glyphs.Should().HaveCount(2, "an unpaired surrogate is one unit and the A is another");
        glyphs[0].Should().Be(0, "a lone surrogate is not a character any face maps");
        glyphs[1].Should().NotBe(0, "the A after it is untouched");
    }
}
