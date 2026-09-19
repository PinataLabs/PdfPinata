using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   NInt marked "not set" by storing int.MinValue and NDouble by storing double.NaN, so those two
///   values could not be stored as data - assigning one silently meant "unset" instead, and the
///   value read back was zero.
///
///   int? and double? keep a separate bit for "has a value" and so hand the whole range of each
///   type back to the caller. These tests are the inverted form of the ones that pinned the defect,
///   and are what stops it coming back.
/// </summary>
public class SentinelCollisionTests
{
    static PageSetup APageSetup() => new Document().AddSection().PageSetup;

    static Image AnImage() => new();

    [Fact]
    public void AnIntegerSetToTheFormerSentinelIsKept()
    {
        var pageSetup = APageSetup();

        pageSetup.StartingNumber = int.MinValue;

        pageSetup.IsNull("StartingNumber").Should().BeFalse("int? tracks null separately from the value");
        pageSetup.StartingNumber.Should().Be(int.MinValue, "the assigned value is no longer lost");
        pageSetup.GetValue("StartingNumber", GV.GetNull).Should().Be(int.MinValue);
    }

    [Fact]
    public void AnIntegerSetToTheFormerSentinelSurvivesTheDdlRoundTrip()
    {
        var document = new Document();
        document.AddSection().PageSetup.StartingNumber = int.MinValue;

        var reread = DdlReader.DocumentFromString(DdlWriter.WriteToString(document));

        reread.LastSection.PageSetup.StartingNumber.Should().Be(int.MinValue);
    }

    [Fact]
    public void ADoubleSetToTheFormerSentinelIsKept()
    {
        var image = AnImage();

        image.ScaleWidth = double.NaN;

        image.IsNull("ScaleWidth").Should().BeFalse("double? tracks null separately from the value");
        image.ScaleWidth.Should().Be(double.NaN, "the assigned value is no longer lost");
        image.GetValue("ScaleWidth", GV.GetNull).Should().Be(double.NaN);
    }

    [Fact]
    public void EveryOtherExtremeOfTheRangeSurvives()
    {
        var pageSetup = APageSetup();
        var image = AnImage();

        pageSetup.StartingNumber = int.MinValue + 1;
        image.ScaleWidth = double.MinValue;

        pageSetup.StartingNumber.Should().Be(int.MinValue + 1);
        image.ScaleWidth.Should().Be(double.MinValue);
    }
}
