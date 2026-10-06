using System;
using AwesomeAssertions;
using PdfPinata.Text;
using TUnit.Core;

namespace PdfPinata.Test.Text;

/// <summary>
///   The character property tables the bidirectional algorithm and script itemisation are built
///   on. Generated from the Unicode Character Database by <c>tools/UnicodeTableGenerator</c>; these
///   are the tests that say the generation was right.
/// </summary>
public class UnicodePropertyTests
{
    [Test]
    public void TheTablesSayWhichUnicodeTheyCameFrom()
    {
        // Pinned rather than merely reported: the conformance suites in Assets/Unicode are from
        // the same version, and a table bumped without them would test one Unicode against
        // another's expectations.
        UnicodeProperties.UnicodeVersion.Should().Be("17.0.0");
    }

    // ----- Bidi_Class ----------------------------------------------------------------------------

    [Test]
    [Arguments(0x0041, BidiClass.L, "Latin capital A")]
    [Arguments(0x05D0, BidiClass.R, "Hebrew alef")]
    [Arguments(0x0627, BidiClass.AL, "Arabic alef")]
    [Arguments(0x0030, BidiClass.EN, "digit zero")]
    [Arguments(0x0660, BidiClass.AN, "Arabic-Indic digit zero")]
    [Arguments(0x0020, BidiClass.WS, "space")]
    [Arguments(0x0009, BidiClass.S, "tab is a segment separator")]
    [Arguments(0x000A, BidiClass.B, "line feed is a paragraph separator")]
    [Arguments(0x0301, BidiClass.NSM, "combining acute accent")]
    [Arguments(0x202B, BidiClass.RLE, "right-to-left embedding")]
    [Arguments(0x2066, BidiClass.LRI, "left-to-right isolate")]
    [Arguments(0x2069, BidiClass.PDI, "pop directional isolate")]
    [Arguments(0x061C, BidiClass.AL, "Arabic letter mark")]
    [Arguments(0x05BE, BidiClass.R, "Hebrew maqaf")]
    [Arguments(0x4E00, BidiClass.L, "the first CJK ideograph")]
    [Arguments(0x1F600, BidiClass.ON, "a grinning face is other neutral")]
    [Arguments(0xFFFF, BidiClass.BN, "a noncharacter is boundary neutral")]
    public void ACharacterHasTheBidiClassTheDatabaseGivesIt(int codePoint, BidiClass expected, string what)
    {
        UnicodeProperties.BidiClassOf(codePoint).Should().Be(expected, what);
    }

    [Test]
    [Arguments(0x05EB, BidiClass.R, "unassigned inside the Hebrew block")]
    [Arguments(0x08B5, BidiClass.AL, "inside the Arabic block")]
    [Arguments(0x20C0, BidiClass.ET, "unassigned inside the currency symbols block")]
    [Arguments(0xFDD0, BidiClass.BN, "a noncharacter in the Arabic Presentation Forms block")]
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

    [Test]
    [Arguments(0x0041, UnicodeScript.Latin)]
    [Arguments(0x05D0, UnicodeScript.Hebrew)]
    [Arguments(0x0627, UnicodeScript.Arabic)]
    [Arguments(0x0930, UnicodeScript.Devanagari)]
    [Arguments(0x4E00, UnicodeScript.Han)]
    [Arguments(0x0030, UnicodeScript.Common)]
    [Arguments(0x0301, UnicodeScript.Inherited)]
    [Arguments(0x05EB, UnicodeScript.Unknown)]
    public void ACharacterHasTheScriptTheDatabaseGivesIt(int codePoint, UnicodeScript expected)
    {
        UnicodeProperties.ScriptOf(codePoint).Should().Be(expected);
    }

    [Test]
    [Arguments(UnicodeScript.Arabic, "arab")]
    [Arguments(UnicodeScript.Latin, "latn")]
    [Arguments(UnicodeScript.Devanagari, "deva")]
    [Arguments(UnicodeScript.Hebrew, "hebr")]
    [Arguments(UnicodeScript.Han, "hani")]
    [Arguments(UnicodeScript.Common, "zyyy")]
    [Arguments(UnicodeScript.Inherited, "zinh")]
    [Arguments(UnicodeScript.Unknown, "zzzz")]
    public void AScriptKnowsTheFourLetterCodeAShaperIsToldItBy(UnicodeScript script, string code)
    {
        // Lowercased ISO 15924, which is what ITextShaper.Shape takes.
        UnicodeProperties.ScriptCode(script).Should().Be(code);
    }

    // ----- the shape of the tables ----------------------------------------------------------------

    [Test]
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

    [Test]
    [Arguments(-1)]
    [Arguments(0x110000)]
    public void SomethingThatIsNotACodePointIsRefused(int notACodePoint)
    {
        var bidi = () => UnicodeProperties.BidiClassOf(notACodePoint);
        var script = () => UnicodeProperties.ScriptOf(notACodePoint);

        bidi.Should().Throw<ArgumentOutOfRangeException>();
        script.Should().Throw<ArgumentOutOfRangeException>();
    }
}
