using System;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   That a wrap style survives being written to MDDDL and read back.
/// </summary>
/// <remarks>
///   The document object model's serialisation is partly generated and partly hand-written — see
///   <c>docs/specs/generated-serialization.md</c> — so a new enumeration value is not automatically
///   carried by it. The parser has a generic enum path driven by the value descriptor, which ought
///   to mean these work for free; this is the test that says so rather than the reading that
///   assumes it.
/// </remarks>
public class WrapStyleSerializationTests
{
    public static TheoryData<WrapStyle> EveryStyle
    {
        get
        {
            var data = new TheoryData<WrapStyle>();
            foreach (WrapStyle style in Enum.GetValues(typeof(WrapStyle)))
                data.Add(style);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EveryStyle))]
    public void AWrapStyleOnATextFrameRoundTrips(WrapStyle style)
    {
        var read = RoundTrip(document =>
        {
            var frame = document.AddSection().AddTextFrame();
            frame.Width = "4cm";
            frame.Height = "3cm";
            frame.WrapFormat.Style = style;
            frame.AddParagraph("Inside the frame.");
        });

        FrameIn(read).WrapFormat.Style.Should().Be(style);
    }

    [Theory]
    [MemberData(nameof(EveryStyle))]
    public void AWrapStyleIsWrittenByNameRatherThanByNumber(WrapStyle style)
    {
        var written = Written(document =>
        {
            var frame = document.AddSection().AddTextFrame();
            frame.WrapFormat.Style = style;
        });

        // By name, because a number in an MDDDL file would be re-pointed by any later insertion
        // into the enumeration - and because a file is meant to be readable.
        written.Should().Contain("Style = " + style);
        written.Should().NotContain("Style = " + (int)style);
    }

    [Fact]
    public void TheFourDistancesRoundTripAlongsideTheStyle()
    {
        var read = RoundTrip(document =>
        {
            var frame = document.AddSection().AddTextFrame();
            frame.WrapFormat.Style = WrapStyle.Left;
            frame.WrapFormat.DistanceLeft = "1cm";
            frame.WrapFormat.DistanceRight = "2cm";
            frame.WrapFormat.DistanceTop = "3cm";
            frame.WrapFormat.DistanceBottom = "4cm";
        });

        var wrap = FrameIn(read).WrapFormat;
        wrap.Style.Should().Be(WrapStyle.Left);
        wrap.DistanceLeft.Centimeter.Should().BeApproximately(1, 0.001);
        wrap.DistanceRight.Centimeter.Should().BeApproximately(2, 0.001);
        wrap.DistanceTop.Centimeter.Should().BeApproximately(3, 0.001);
        wrap.DistanceBottom.Centimeter.Should().BeApproximately(4, 0.001);
    }

    [Fact]
    public void TheStylesThatCameBeforeKeepTheNumbersTheyHad()
    {
        // The new values are appended, not inserted. A document written by an older version holds
        // these names, but anything that persisted the number would be silently re-pointed by an
        // insertion in the middle.
        ((int)WrapStyle.TopBottom).Should().Be(0);
        ((int)WrapStyle.None).Should().Be(1);
        ((int)WrapStyle.Through).Should().Be(2);
    }

    [Fact]
    public void TheNewStylesAreDistinctFromEachOtherAndFromTheOldOnes()
    {
        var values = Enum.GetValues(typeof(WrapStyle)).Cast<WrapStyle>().ToList();

        values.Should().OnlyHaveUniqueItems();
        values.Should().Contain(new[]
        {
            WrapStyle.TopBottom, WrapStyle.None, WrapStyle.Through,
            WrapStyle.Left, WrapStyle.Right, WrapStyle.Largest, WrapStyle.Both,
        });
    }

    [Fact]
    public void AnUnknownWrapStyleIsRefusedRatherThanTakenForTheDefault()
    {
        var mdddl = Written(document =>
        {
            var frame = document.AddSection().AddTextFrame();
            frame.WrapFormat.Style = WrapStyle.Left;
        }).Replace("Style = Left", "Style = Sideways");

        var read = () => DdlReader.DocumentFromString(mdddl);

        // A style the reader does not know is a document it cannot honour. Falling back to
        // TopBottom would lay the page out differently and say nothing.
        read.Should().Throw<Exception>();
    }

    // ----- writing and reading ---------------------------------------------------------------------

    static string Written(Action<Document> build)
    {
        var document = new Document();
        build(document);
        return DdlWriter.WriteToString(document);
    }

    static Document RoundTrip(Action<Document> build)
    {
        return DdlReader.DocumentFromString(Written(build));
    }

    static PinataLayout.DocumentObjectModel.Shapes.TextFrame FrameIn(Document document)
    {
        var section = document.Sections[0] as Section;
        return section.Elements
            .OfType<PinataLayout.DocumentObjectModel.Shapes.TextFrame>()
            .Single();
    }
}
