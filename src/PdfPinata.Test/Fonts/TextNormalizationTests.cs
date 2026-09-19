using System;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   The one rule measuring and drawing now share: a tab becomes a single space, every other
///   character below 32 is dropped, and nothing at or above 32 is touched.
/// </summary>
/// <remarks>
///   TextNormalization is internal and this repository carries no InternalsVisibleTo, so it is
///   reached by reflection - the same way <see cref="PdfPinata.Test.IO.CharacterScanningTests"/>
///   reaches the scanner. Worth asking directly rather than only through a rendered page: it is
///   pure character-table logic with no font, no shaping and no rendering underneath it, and it
///   should be provable without any of the three.
/// </remarks>
public class TextNormalizationTests
{
    static readonly Type NormalizationType =
        typeof(XFont).Assembly.GetType("PdfPinata.Fonts.TextNormalization", throwOnError: true);

    // ----- TryNormalize: one character at a time ------------------------------------------------

    [Fact]
    public void TryNormalize_turnsATabIntoASingleSpace()
    {
        var (survives, normalized) = TryNormalize('\t');

        survives.Should().BeTrue();
        normalized.Should().Be(' ');
    }

    [Theory]
    [InlineData('\n')]
    [InlineData('\r')]
    [InlineData('\v')]
    [InlineData('\f')]
    [InlineData('\0')]
    [InlineData((char)27)]   // escape
    [InlineData((char)31)]   // the last one below the cut
    public void TryNormalize_dropsEveryOtherCharacterBelowThirtyTwo(char ch)
    {
        // A line feed is in this bucket on purpose. Nothing here splits lines, so the alternative
        // to dropping it is drawing the box the face keeps for a character it has no glyph for.
        TryNormalize(ch).Survives.Should().BeFalse();
    }

    [Theory]
    [InlineData(' ')]        // 32, the first one kept
    [InlineData('A')]
    [InlineData('~')]
    [InlineData(' ')]   // no-break space
    [InlineData('‍')]   // zero width joiner - a shaping control, and the shaper's business
    [InlineData('￿')]
    public void TryNormalize_leavesEverythingAtOrAboveThirtyTwoExactlyAsItIs(char ch)
    {
        var (survives, normalized) = TryNormalize(ch);

        survives.Should().BeTrue();
        normalized.Should().Be(ch);
    }

    // ----- NormalizeLine: a whole line ----------------------------------------------------------

    [Fact]
    public void NormalizeLine_answersTheSameReferenceWhenThereIsNothingToFilter()
    {
        // The common case, and the whole reason this looks before it copies: every string this
        // library already draws through XTextFormatter, PinataLayout or the charting renderers comes
        // back untouched and unallocated.
        const string text = "Handgloves and quartz";

        NormalizeLine(text).Should().BeSameAs(text);
    }

    [Fact]
    public void NormalizeLine_keepsAnEmptyStringAsItIs()
    {
        NormalizeLine(string.Empty).Should().BeSameAs(string.Empty);
    }

    [Fact]
    public void NormalizeLine_writesASpaceWhereEachTabWas()
    {
        NormalizeLine("a\tb\tc").Should().Be("a b c");
    }

    [Fact]
    public void NormalizeLine_dropsALineFeedRatherThanBreakingTheLineAtIt()
    {
        // The disagreement this candidate does not close: MeasureString reports two lines for
        // this string and DrawString draws one, with the newline gone rather than boxed.
        NormalizeLine("A newline\nbecomes").Should().Be("A newlinebecomes");
    }

    [Fact]
    public void NormalizeLine_dropsACarriageReturnToo()
    {
        // MeasureString has never special-cased \r either. Folding CR or CRLF into a line break
        // the way XTextFormatter does is a different, separate inconsistency.
        NormalizeLine("a\r\nb").Should().Be("ab");
    }

    [Fact]
    public void NormalizeLine_answersAnEmptyStringForALineOfNothingButControlCharacters()
    {
        NormalizeLine("\n\r\v\f").Should().BeEmpty();
    }

    [Fact]
    public void NormalizeLine_leavesTheCharactersBeforeTheFirstControlCharacterWhereTheyWere()
    {
        // The prefix is copied wholesale rather than filtered a character at a time, so it is
        // worth asserting that it arrives intact and in order.
        NormalizeLine("Handgloves\tand quartz\nagain").Should().Be("Handgloves and quartzagain");
    }

    // ----- reflection plumbing ------------------------------------------------------------------

    static (bool Survives, char Normalized) TryNormalize(char ch)
    {
        var method = NormalizationType.GetMethod("TryNormalize",
            BindingFlags.NonPublic | BindingFlags.Static);
        object[] args = { ch, '\0' };
        var survives = (bool)method.Invoke(null, args);
        return (survives, (char)args[1]);
    }

    static string NormalizeLine(string text)
    {
        var method = NormalizationType.GetMethod("NormalizeLine",
            BindingFlags.NonPublic | BindingFlags.Static);
        return (string)method.Invoke(null, new object[] { text });
    }
}
