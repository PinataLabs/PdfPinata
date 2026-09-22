using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   The nine shapes a <see cref="PdfLineAnnotation"/> can put at either end of itself. Each is a
///   separate arm of one switch, and each draws into the annotation's own appearance stream — so
///   what pins them is what is drawn, not what the dictionary says.
///   <para>
///   <see cref="LineAnnotationTests"/> covers the dictionary, the rectangle and whether a reader
///   paints the line at all. These cover the arms of the ending switch, which that one reaches
///   only through <c>ClosedArrow</c>.
///   </para>
/// </summary>
public class LineEndingShapeTests
{
    static readonly XPoint From = new(100, 400);
    static readonly XPoint To = new(300, 400);

    static PdfLineAnnotation ALineEndedWith(PdfLineEnding start, PdfLineEnding end)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var line = new PdfLineAnnotation(document)
        {
            BorderWidth = 2,
            Interior = XColors.Red,
            StartEnding = start,
            EndEnding = end
        };
        line.SetLine(From, To);
        page.Annotations.Add(line);
        return line;
    }

    /// <summary>The operators of the annotation's appearance stream.</summary>
    static string AppearanceOf(PdfLineAnnotation line)
    {
        var normal = line.Elements.GetDictionary("/AP")?.Elements.GetDictionary("/N");
        return normal == null ? "" : normal.Stream.ToString();
    }

    [Theory]
    [InlineData(PdfLineEnding.Square)]
    [InlineData(PdfLineEnding.Circle)]
    [InlineData(PdfLineEnding.Diamond)]
    [InlineData(PdfLineEnding.OpenArrow)]
    [InlineData(PdfLineEnding.ClosedArrow)]
    [InlineData(PdfLineEnding.Butt)]
    [InlineData(PdfLineEnding.ROpenArrow)]
    [InlineData(PdfLineEnding.RClosedArrow)]
    [InlineData(PdfLineEnding.Slash)]
    public void EveryEndingDrawsSomethingBeyondTheLineItself(PdfLineEnding ending)
    {
        var plain = AppearanceOf(ALineEndedWith(PdfLineEnding.None, PdfLineEnding.None));
        var ended = AppearanceOf(ALineEndedWith(ending, ending));

        plain.Should().NotBeEmpty("even a bare line draws itself");
        ended.Length.Should().BeGreaterThan(plain.Length,
            "the ending is drawn on top of the line, so its operators are added to the stream");
    }

    [Theory]
    [InlineData(PdfLineEnding.Square)]
    [InlineData(PdfLineEnding.Circle)]
    [InlineData(PdfLineEnding.Diamond)]
    [InlineData(PdfLineEnding.ClosedArrow)]
    [InlineData(PdfLineEnding.RClosedArrow)]
    public void AFilledEndingPaintsItselfWithTheInteriorColour(PdfLineEnding ending)
    {
        var appearance = AppearanceOf(ALineEndedWith(ending, ending));

        // rg is the non-stroking colour, which is what a fill uses; a hollow ending never sets one.
        appearance.Should().Contain(" rg");
    }

    [Theory]
    [InlineData(PdfLineEnding.OpenArrow)]
    [InlineData(PdfLineEnding.ROpenArrow)]
    [InlineData(PdfLineEnding.Butt)]
    [InlineData(PdfLineEnding.Slash)]
    public void AHollowEndingIsStrokedRatherThanFilled(PdfLineEnding ending)
    {
        var appearance = AppearanceOf(ALineEndedWith(ending, ending));

        // A hollow ending is stroked rather than filled, so the stream never names a non-stroking
        // colour for it - the only one it sets is the stroking colour the line itself is drawn in.
        appearance.Should().NotContain(" rg");
    }

    /// <summary>
    ///   A reversed arrowhead is the same triangle turned round, so it draws the same operators in
    ///   a different place. Both are worth having because the reversal is one line of code that
    ///   nothing else reaches.
    /// </summary>
    [Fact]
    public void AReversedArrowheadIsTheSameShapeTheOtherWayRound()
    {
        var forwards = AppearanceOf(ALineEndedWith(PdfLineEnding.OpenArrow, PdfLineEnding.OpenArrow));
        var backwards = AppearanceOf(ALineEndedWith(PdfLineEnding.ROpenArrow, PdfLineEnding.ROpenArrow));

        forwards.Should().NotBeEmpty();
        backwards.Should().NotBe(forwards, "it points the other way");
        backwards.Length.Should().BeCloseTo(forwards.Length, 8,
            "and is otherwise the same drawing, give or take how the coordinates round");
    }

    [Fact]
    public void TheTwoEndsCanBeEndedDifferently()
    {
        var line = ALineEndedWith(PdfLineEnding.Square, PdfLineEnding.Circle);

        var endings = line.Elements.GetArray("/LE");

        endings!.Elements.GetName(0).Should().Be("/Square");
        endings.Elements.GetName(1).Should().Be("/Circle");
    }

    [Fact]
    public void ALineBuiltOnADocumentIsReadyToBeUsedWithoutBeingPlacedFirst()
    {
        var document = new PdfDocument();

        var line = new PdfLineAnnotation(document);

        line.Elements.GetName("/Subtype").Should().Be("/Line");
        line.BorderWidth.Should().Be(1);
        line.StartEnding.Should().Be(PdfLineEnding.None);
        line.EndEnding.Should().Be(PdfLineEnding.None);
    }

    /// <summary>
    ///   An endpoint is read back out of <c>/L</c> rather than kept in a field, so a document whose
    ///   <c>/L</c> is missing or too short answers the origin rather than throwing.
    /// </summary>
    [Fact]
    public void AnEndpointOfALineWithNoProperLineArrayIsTheOrigin()
    {
        var document = new PdfDocument();
        var line = new PdfLineAnnotation(document);
        line.Elements["/L"] = new PdfArray(document, new PdfReal(1), new PdfReal(2));

        line.Start.Should().Be(new XPoint());
        line.End.Should().Be(new XPoint());

        line.Elements.Remove("/L");

        line.Start.Should().Be(new XPoint());
    }
}
