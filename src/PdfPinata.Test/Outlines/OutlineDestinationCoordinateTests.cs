using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Outlines;

/// <summary>
///   The coordinates of an outline entry's <c>/Dest</c> array, written back out. Each was
///   formatted with <c>#.##</c>, which formats zero as nothing at all, and a <c>null</c> was read
///   as zero - so <c>[p /XYZ null 700 null]</c> came back as <c>[p /XYZ 700]</c>, with 700 now the
///   left coordinate. See #324.
/// </summary>
public class OutlineDestinationCoordinateTests
{
    [Test]
    // A null is the specification's "leave it as it is"; a zero is a coordinate.
    [Arguments("/XYZ null 700 null", new[] { "null", "700", "null" })]
    [Arguments("/XYZ 0 792 0", new[] { "0", "792", "0" })]
    [Arguments("/XYZ 0 0 0", new[] { "0", "0", "0" })]
    [Arguments("/FitH 0", new[] { "0" })]
    [Arguments("/FitH null", new[] { "null" })]
    [Arguments("/FitV 0", new[] { "0" })]
    [Arguments("/FitV null", new[] { "null" })]
    [Arguments("/FitR 0 0 300 null", new[] { "0", "0", "300", "null" })]
    [Arguments("/FitBH 0", new[] { "0" })]
    [Arguments("/FitBH null", new[] { "null" })]
    [Arguments("/FitBV 0", new[] { "0" })]
    [Arguments("/FitBV null", new[] { "null" })]
    public void ADestinationReadFromAFileKeepsEveryCoordinateWhenItIsSaved(string destination, string[] coordinates)
    {
        using var input = new MemoryStream(ImportedOutlineFixtures.WithDestinationArray("[4 0 R" + destination + "]"));
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        // Reading the outline is what makes the entry one this library writes from its properties.
        document.Outlines.Count.Should().Be(1);

        using var reopened = document.Reopened();

        var written = reopened.Outlines[0].Elements.GetArray("/Dest");
        written.Elements.Count.Should().Be(2 + coordinates.Length);
        for (var i = 0; i < coordinates.Length; i++)
            Describe(written.Elements[2 + i]).Should().Be(coordinates[i], "coordinate {0} of {1}", i, destination);
    }

    [Test]
    public void ANewEntryAtTheLeftEdgeIsWrittenWithALeftOfZero()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var outline = document.Outlines.Add("Top left", document.Pages[0]);
        outline.PageDestinationType = PdfPageDestinationType.Xyz;
        outline.Left = 0;
        outline.Top = 500;
        outline.Zoom = 0;

        using var reopened = document.Reopened();

        var written = reopened.Outlines[0].Elements.GetArray("/Dest");
        written.Elements.Count.Should().Be(5);
        Describe(written.Elements[2]).Should().Be("0");
        Describe(written.Elements[3]).Should().Be("500");
        Describe(written.Elements[4]).Should().Be("0");
        reopened.Outlines[0].Left.Should().Be(0);
        reopened.Outlines[0].Top.Should().Be(500);
    }

    [Test]
    public void ACoordinateThatIsNotAWholeNumberKeepsThreeDecimalPlaces()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var outline = document.Outlines.Add("Precise", document.Pages[0]);
        outline.PageDestinationType = PdfPageDestinationType.FitH;
        outline.Top = 123.4567;

        using var reopened = document.Reopened();

        reopened.Outlines[0].Top.Should().Be(123.457);
    }

    [Test]
    public void ANullCoordinateIsReadAsNoCoordinateAtAll()
    {
        using var input = new MemoryStream(ImportedOutlineFixtures.WithDestinationArray("[4 0 R/XYZ null 700 null]"));
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var outline = document.Outlines[0];
        outline.Left.Should().Be(double.NaN);
        outline.Top.Should().Be(700);
        outline.Zoom.Should().Be(double.NaN);
    }

    /// <summary>An array element as the text a reader would see: a number, or null.</summary>
    private static string Describe(PdfItem item)
    {
        return item switch
        {
            PdfNull or PdfNullObject => "null",
            PdfPinata.Pdf.PdfInteger integer => integer.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            PdfReal real => real.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => item?.GetType().Name ?? "(missing)"
        };
    }
}
