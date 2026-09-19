using System;
using AwesomeAssertions;
using PdfPinata.Text;
using Xunit;

namespace PdfPinata.Test.Text;

/// <summary>
///   The character property tables the bidirectional algorithm and script itemisation are built
///   on. Generated from the Unicode Character Database by <c>tools/UnicodeTableGenerator</c>; these
///   are the tests that say the generation was right.
/// </summary>
public class UnicodePropertyTests
{
    [Fact]
    public void TheTablesSayWhichUnicodeTheyCameFrom()
    {
        // Pinned rather than merely reported: the conformance suites in Assets/Unicode are from
        // the same version, and a table bumped without them would test one Unicode against
        // another's expectations.
        UnicodeProperties.UnicodeVersion.Should().Be("17.0.0");
    }

    // ----- Bidi_Class ----------------------------------------------------------------------------

    [Theory]
    [InlineData(0x0041, BidiClass.L, "Latin capital A")]
    [InlineData(0x05D0, BidiClass.R, "Hebrew alef")]
    [InlineData(0x0627, BidiClass.AL, "Arabic alef")]
    [InlineData(0x0030, BidiClass.EN, "digit zero")]
    [InlineData(0x0660, BidiClass.AN, "Arabic-Indic digit zero")]
    [InlineData(0x0020, BidiClass.WS, "space")]
    [InlineData(0x0009, BidiClass.S, "tab is a segment separator")]
    [InlineData(0x000A, BidiClass.B, "line feed is a paragraph separator")]
    [InlineData(0x0301, BidiClass.NSM, "combining acute accent")]
    [InlineData(0x202B, BidiClass.RLE, "right-to-left embedding")]
    [InlineData(0x2066, BidiClass.LRI, "left-to-right isolate")]
    [InlineData(0x2069, BidiClass.PDI, "pop directional isolate")]
    [InlineData(0x061C, BidiClass.AL, "Arabic letter mark")]
    [InlineData(0x05BE, BidiClass.R, "Hebrew maqaf")]
    [InlineData(0x4E00, BidiClass.L, "the first CJK ideograph")]
    [InlineData(0x1F600, BidiClass.ON, "a grinning face is other neutral")]
    [InlineData(0xFFFF, BidiClass.BN, "a noncharacter is boundary neutral")]
    public void ACharacterHasTheBidiClassTheDatabaseGivesIt(int codePoint, BidiClass expected, string what)
    {
        UnicodeProperties.BidiClassOf(codePoint).Should().Be(expected, what);
    }

    [Theory]
    [InlineData(0x05EB, BidiClass.R, "unassigned inside the Hebrew block")]
    [InlineData(0x08B5, BidiClass.AL, "inside the Arabic block")]
    [InlineData(0x20C0, BidiClass.ET, "unassigned inside the currency symbols block")]
    [InlineData(0xFDD0, BidiClass.BN, "a noncharacter in the Arabic Presentation Forms block")]
    public void AnUnassignedCodePointDefaultsByWhereItSitsAndNotToLeftToRight(
        int codePoint, BidiClass expected, string what)
    {
        // This is the part an implementation reading only the explicit ranges gets wrong, and it
        // gets it wrong for Hebrew and Arabic specifically - the scripts the algorithm exists for.
        // The generator materialises the database's @missing defaults into the table so that there
        // is nothing left to default at run time.
        UnicodeProperties.BidiClassOf(codePoint).Should().Be(expected, what);
    }

    // ----- Script --------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x0041, UnicodeScript.Latin)]
    [InlineData(0x05D0, UnicodeScript.Hebrew)]
    [InlineData(0x0627, UnicodeScript.Arabic)]
    [InlineData(0x0930, UnicodeScript.Devanagari)]
    [InlineData(0x4E00, UnicodeScript.Han)]
    [InlineData(0x0030, UnicodeScript.Common)]
    [InlineData(0x0301, UnicodeScript.Inherited)]
    [InlineData(0x05EB, UnicodeScript.Unknown)]
    public void ACharacterHasTheScriptTheDatabaseGivesIt(int codePoint, UnicodeScript expected)
    {
        UnicodeProperties.ScriptOf(codePoint).Should().Be(expected);
    }

    [Theory]
    [InlineData(UnicodeScript.Arabic, "arab")]
    [InlineData(UnicodeScript.Latin, "latn")]
    [InlineData(UnicodeScript.Devanagari, "deva")]
    [InlineData(UnicodeScript.Hebrew, "hebr")]
    [InlineData(UnicodeScript.Han, "hani")]
    [InlineData(UnicodeScript.Common, "zyyy")]
    [InlineData(UnicodeScript.Inherited, "zinh")]
    [InlineData(UnicodeScript.Unknown, "zzzz")]
    public void AScriptKnowsTheFourLetterCodeAShaperIsToldItBy(UnicodeScript script, string code)
    {
        // Lowercased ISO 15924, which is what ITextShaper.Shape takes.
        UnicodeProperties.ScriptCode(script).Should().Be(code);
    }

    // ----- the shape of the tables ----------------------------------------------------------------

    [Fact]
    public void EveryCodePointHasBothProperties()
    {
        // The tables are a complete partition of the code space, so there is no code point either
        // lookup can fail to answer for - and a binary search that walked off the end would be
        // found here rather than in the middle of laying out a page. What each answers has to be
        // one of the values its type names, too, or a table has a hole where a class should be.
        var undefined = 0;
        for (var codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            if (!Enum.IsDefined(UnicodeProperties.BidiClassOf(codePoint)))
                undefined++;
            if (!Enum.IsDefined(UnicodeProperties.ScriptOf(codePoint)))
                undefined++;
        }

        undefined.Should().Be(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0x110000)]
    public void SomethingThatIsNotACodePointIsRefused(int notACodePoint)
    {
        var bidi = () => UnicodeProperties.BidiClassOf(notACodePoint);
        var script = () => UnicodeProperties.ScriptOf(notACodePoint);

        bidi.Should().Throw<ArgumentOutOfRangeException>();
        script.Should().Throw<ArgumentOutOfRangeException>();
    }
}
