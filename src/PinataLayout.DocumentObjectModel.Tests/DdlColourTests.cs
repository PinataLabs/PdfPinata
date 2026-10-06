using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using TUnit.Core;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The five ways a colour can be written in MDDDL. A bare number and a colour name are read by
///   the value model; <c>RGB(…)</c>, <c>CMYK(…)</c> and <c>GRAY(…)</c> are read by the parser,
///   each with its own arm that checks the count of its arguments, the kind of each and the range
///   of each.
///   <para>
///   What those checks do when they fail is the surprise, and it is the reason the tests here read
///   the error list rather than expecting a throw. Every attribute assignment in the parser is
///   wrapped in <c>catch (Exception ex) { ReportParserException(ex, InvalidAssignment, …) }</c>, so
///   a colour the parser cannot read is written to the error list, the property is left as it was,
///   and the document is returned as though nothing happened. A caller passing no error list is
///   told nothing at all.
///   </para>
/// </summary>
public class DdlColourTests
{
    private static string DocumentWith(string colourLiteral) =>
        "\\document{\\section{\\paragraph{\\font[Color = " + colourLiteral + "]{x}}}}";

    /// <summary>The colour of a run of formatted text, which is the shortest route to one.</summary>
    private static Color ColourOf(string colourLiteral)
    {
        var errors = new DdlReaderErrors();
        var document = (Document)DdlReader.ObjectFromString(DocumentWith(colourLiteral), errors);

        Complaints(errors).Should().BeEmpty("this colour is a valid one");

        // ReSharper disable PossibleNullReferenceException
        return (document.LastSection.Elements[0] as Paragraph)
            .Elements.OfType<FormattedText>().Single().Font.Color;
        // ReSharper restore PossibleNullReferenceException
    }

    /// <summary>What the reader had to say about a document, as text an assertion can match.</summary>
    private static IReadOnlyList<string> Complaints(DdlReaderErrors errors) =>
        ReaderDiagnostics.Reported(errors);

    /// <summary>
    ///   The complaints a colour that cannot be read produces. Reading is deliberately allowed to
    ///   fail in either of the two ways it can: quietly, with the error list the only sign, or
    ///   fatally once the token stream has lost its place.
    /// </summary>
    private static IReadOnlyList<string> ComplaintsAbout(string colourLiteral) =>
        ReaderDiagnostics.ComplaintsAbout(DocumentWith(colourLiteral));

    // ----- RGB ------------------------------------------------------------------------------------

    [Test]
    public void AnRgbColourIsItsThreeChannelsAndIsOpaque()
    {
        var colour = ColourOf("RGB(12, 34, 56)");

        colour.R.Should().Be(12);
        colour.G.Should().Be(34);
        colour.B.Should().Be(56);
        colour.A.Should().Be(255, "RGB states no alpha, so the colour is fully opaque");
        colour.IsCmyk.Should().BeFalse();
    }

    [Test]
    [Arguments("RGB(0, 0, 0)", 0u, 0u, 0u)]
    [Arguments("RGB(255, 255, 255)", 255u, 255u, 255u)]
    [Arguments("RGB(0xFF, 0x00, 0x80)", 255u, 0u, 128u)]
    public void TheEndsOfEachChannelAndHexDigitsAreAllRead(string ddl, uint r, uint g, uint b)
    {
        var colour = ColourOf(ddl);

        (colour.R, colour.G, colour.B).Should().Be((r, g, b));
    }

    [Test]
    [Arguments("RGB(256, 0, 0)")]
    [Arguments("RGB(0, 256, 0)")]
    [Arguments("RGB(0, 0, 256)")]
    [Arguments("RGB(1.5, 0, 0)")]
    [Arguments("RGB(\"red\", 0, 0)")]
    [Arguments("RGB 1, 2, 3)")]
    [Arguments("RGB(1, 2, 3")]
    [Arguments("RGB(1 2, 3)")]
    [Arguments("RGB(1, 2)")]
    public void AnRgbColourThatCannotBeReadIsComplainedAbout(string ddl)
    {
        ComplaintsAbout(ddl).Should().NotBeEmpty();
    }

    [Test]
    public void AChannelPastTheEndOfItsRangeSaysWhatTheRangeIs()
    {
        ComplaintsAbout("RGB(256, 0, 0)").Should().Contain(complaint => complaint.Contains("0 - 255"));
    }

    // ----- CMYK -----------------------------------------------------------------------------------

    [Test]
    public void ACmykColourWithFourValuesIsOpaque()
    {
        var colour = ColourOf("CMYK(10, 20, 30, 40)");

        colour.IsCmyk.Should().BeTrue();
        colour.C.Should().BeApproximately(10, 1e-4);
        colour.M.Should().BeApproximately(20, 1e-4);
        colour.Y.Should().BeApproximately(30, 1e-4);
        colour.K.Should().BeApproximately(40, 1e-4);
        colour.Alpha.Should().BeApproximately(100, 1e-4, "four values state no alpha");
    }

    [Test]
    public void ACmykColourWithFiveValuesTakesTheFirstAsItsAlpha()
    {
        // The one arm of the method that is not a repetition of the one above it: a fifth value
        // shifts the meaning of all the others along by one.
        var colour = ColourOf("CMYK(50, 10, 20, 30, 40)");

        colour.Alpha.Should().BeApproximately(50, 1e-4);
        colour.C.Should().BeApproximately(10, 1e-4);
        colour.M.Should().BeApproximately(20, 1e-4);
        colour.Y.Should().BeApproximately(30, 1e-4);
        colour.K.Should().BeApproximately(40, 1e-4);
    }

    [Test]
    public void ACmykValueCanBeFractional()
    {
        // Unlike RGB, whose channels are whole numbers, these are percentages and real.
        ColourOf("CMYK(12.5, 0, 0, 0)").C.Should().BeApproximately(12.5, 1e-4);
    }

    [Test]
    [Arguments("CMYK(0, 0, 0, 0)", 0.0)]
    [Arguments("CMYK(100, 100, 100, 100)", 100.0)]
    public void TheEndsOfTheCmykRangeAreAllowed(string ddl, double expected)
    {
        ColourOf(ddl).C.Should().BeApproximately(expected, 1e-4);
    }

    [Test]
    [Arguments("CMYK(101, 0, 0, 0)")]
    [Arguments("CMYK(0, 101, 0, 0)")]
    [Arguments("CMYK(0, 0, 101, 0)")]
    [Arguments("CMYK(0, 0, 0, 101)")]
    [Arguments("CMYK(101, 0, 0, 0, 0)")]
    [Arguments("CMYK(0, 0, 0, 0, 101)")]
    [Arguments("CMYK(0, 0, 0)")]
    [Arguments("CMYK(\"a\", 0, 0, 0)")]
    public void ACmykColourThatCannotBeReadIsComplainedAbout(string ddl)
    {
        ComplaintsAbout(ddl).Should().NotBeEmpty();
    }

    [Test]
    public void ACmykValuePastTheEndOfItsRangeSaysWhatTheRangeIs()
    {
        ComplaintsAbout("CMYK(101, 0, 0, 0)")
            .Should().Contain(complaint => complaint.Contains("0.0 - 100.0"));
    }

    // ----- the other two ways ---------------------------------------------------------------------

    [Test]
    public void AGrayColourIsTheSameValueInAllThreeChannels()
    {
        var colour = ColourOf("GRAY(100)");

        colour.R.Should().Be(colour.G).And.Be(colour.B);
    }

    [Test]
    public void AColourCanBeNamed()
    {
        ColourOf("Red").Should().Be(Colors.Red);
    }

    [Test]
    public void AColourCanBeWrittenAsOneNumber()
    {
        ColourOf("0xFF804020").Argb.Should().Be(0xFF804020);
    }

    [Test]
    public void AColourNameThatIsNotOneIsNamedInTheComplaint()
    {
        ComplaintsAbout("Puce").Should().Contain(complaint => complaint.Contains("Puce"));
    }

    /// <summary>
    ///   Two of the five colour spaces are not written yet, and the arms that would read them
    ///   throw NotImplementedException. Because every attribute assignment catches Exception
    ///   rather than the parser's own type, that lands in the error list like any other complaint
    ///   and the document reads on without a colour. Pinned so that implementing either is a
    ///   visible change.
    /// </summary>
    [Test]
    [Arguments("HSB(1, 2, 3)")]
    [Arguments("Lab(1, 2, 3)")]
    public void TheTwoColourSpacesThatWereNeverFinishedAreComplainedAboutRatherThanThrown(string ddl)
    {
        ComplaintsAbout(ddl).Should().NotBeEmpty();
    }

    // ----- and what the caller is told ---------------------------------------------------------------

    /// <summary>
    ///   The consequence of the blanket catch, stated on its own because it is the part that bites.
    ///   A colour that cannot be read leaves the property at its default and the document is
    ///   returned as if it had been read. A caller that passes no error list - which is what
    ///   <c>DdlReader.DocumentFromString(string)</c> does - has no way to find out.
    /// </summary>
    [Test]
    public void AColourThatCannotBeReadLeavesThePropertyAloneAndTheDocumentReadable()
    {
        var document = DdlReader.DocumentFromString(DocumentWith("Puce"));

        // ReSharper disable PossibleNullReferenceException
        var formatted = (document.LastSection.Elements[0] as Paragraph)
            .Elements.OfType<FormattedText>().Single();
        // ReSharper restore PossibleNullReferenceException

        formatted.Font.Color.Should().Be(Color.Empty, "the assignment never happened");
    }
}
