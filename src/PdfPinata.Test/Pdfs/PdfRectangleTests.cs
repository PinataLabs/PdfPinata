using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.IO;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <see cref="PdfRectangle"/> is a simple type written as an array of four numbers: the x and y
///   of one corner, then the x and y of the diagonally opposite one. It compares by those four
///   coordinates, both through <see cref="PdfRectangle.Equals(object)"/> and through the
///   operators, and answers containment inclusively on every edge.
///
///   <para>
///   A page tree read from a file may state a box once on an intermediate node for every page
///   beneath it. Reading such a file copies the box down onto each page, and that is where a
///   rectangle is made from whatever item the node holds: an array, or a reference to one. A node
///   or a page whose entry is null, directly or by reference, is treated as having no entry.
///   </para>
/// </summary>
public class PdfRectangleTests
{
    [Fact]
    public void ARectangleFromALocationAndASizeReachesAcrossTheSize()
    {
        var rectangle = new PdfRectangle(new XPoint(10, 20), new XSize(30, 40));

        rectangle.X1.Should().Be(10);
        rectangle.Y1.Should().Be(20);
        rectangle.X2.Should().Be(40);
        rectangle.Y2.Should().Be(60);
        rectangle.Width.Should().Be(30);
        rectangle.Height.Should().Be(40);
        rectangle.Location.Should().Be(new XPoint(10, 20));
        rectangle.Size.Should().Be(new XSize(30, 40));
    }

    [Fact]
    public void ARectangleFromTwoCornersKeepsThemAsTheyWereGiven()
    {
        var rectangle = new PdfRectangle(new XPoint(5, 6), new XPoint(105, 206));

        rectangle.X1.Should().Be(5);
        rectangle.Y1.Should().Be(6);
        rectangle.X2.Should().Be(105);
        rectangle.Y2.Should().Be(206);
        rectangle.ToXRect().Should().Be(new XRect(5, 6, 100, 200));
    }

    [Fact]
    public void EveryWayOfMakingTheSameRectangleMakesAnEqualOne()
    {
        var fromCorners = new PdfRectangle(new XPoint(1, 2), new XPoint(4, 6));
        var fromSize = new PdfRectangle(new XPoint(1, 2), new XSize(3, 4));
        var fromRect = new PdfRectangle(new XRect(1, 2, 3, 4));

        fromSize.Should().Be(fromCorners);
        fromRect.Should().Be(fromCorners);
        fromSize.GetHashCode().Should().Be(fromCorners.GetHashCode());
        fromRect.GetHashCode().Should().Be(fromCorners.GetHashCode());
    }

    [Fact]
    public void TheEmptyRectangleHasEveryCoordinateZero()
    {
        PdfRectangle.Empty.IsEmpty.Should().BeTrue();
        PdfRectangle.Empty.Should().Be(new PdfRectangle());
        PdfRectangle.Empty.Width.Should().Be(0);
        PdfRectangle.Empty.Height.Should().Be(0);
        new PdfRectangle(new XPoint(0, 0), new XSize(0, 1)).IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void ARectangleIsNotEqualToSomethingThatIsNotARectangle()
    {
        var rectangle = new PdfRectangle(new XRect(0, 0, 10, 10));

        rectangle.Equals(null).Should().BeFalse();
        // ReSharper disable once SuspiciousTypeConversion.Global
        rectangle.Equals(new XRect(0, 0, 10, 10)).Should().BeFalse();
        // ReSharper disable once SuspiciousTypeConversion.Global
        rectangle.Equals(new PdfArray()).Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 0, 10, 10)]
    [InlineData(0, 1, 10, 10)]
    [InlineData(0, 0, 11, 10)]
    [InlineData(0, 0, 10, 11)]
    public void RectanglesDifferingInAnyOneCoordinateAreNotEqual(double x1, double y1, double x2, double y2)
    {
        var rectangle = new PdfRectangle(new XPoint(0, 0), new XPoint(10, 10));
        var other = new PdfRectangle(new XPoint(x1, y1), new XPoint(x2, y2));

        rectangle.Equals(other).Should().BeFalse();
        (rectangle == other).Should().BeFalse();
        (rectangle != other).Should().BeTrue();
        other.GetHashCode().Should().NotBe(rectangle.GetHashCode());
    }

    [Fact]
    public void TheEqualityOperatorComparesCoordinatesRatherThanReferences()
    {
        var rectangle = new PdfRectangle(new XRect(1, 2, 3, 4));
        var same = new PdfRectangle(new XRect(1, 2, 3, 4));

        ReferenceEquals(rectangle, same).Should().BeFalse();
        (rectangle == same).Should().BeTrue();
        (rectangle != same).Should().BeFalse();
    }

    [Fact]
    public void TheEqualityOperatorHandlesNullOnEitherSide()
    {
        var rectangle = new PdfRectangle(new XRect(1, 2, 3, 4));
        PdfRectangle none = null;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (rectangle == none).Should().BeFalse();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (none == rectangle).Should().BeFalse();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (none == null).Should().BeTrue();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (rectangle != none).Should().BeTrue();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        (none != rectangle).Should().BeTrue();
    }

    [Fact]
    public void AClonedRectangleIsAnEqualButSeparateInstance()
    {
        var rectangle = new PdfRectangle(new XRect(1, 2, 3, 4));

        var clone = rectangle.Clone();

        clone.Should().NotBeSameAs(rectangle);
        clone.Should().Be(rectangle);
        clone.X2.Should().Be(4);
        clone.Y2.Should().Be(6);
    }

    [Theory]
    [InlineData(10, 20, true)]   // a corner
    [InlineData(110, 220, true)] // the opposite corner
    [InlineData(60, 20, true)]   // on an edge
    [InlineData(60, 120, true)]  // inside
    [InlineData(9.99, 120, false)]
    [InlineData(110.01, 120, false)]
    [InlineData(60, 19.99, false)]
    [InlineData(60, 220.01, false)]
    public void APointIsContainedInclusiveOfEveryEdge(double x, double y, bool expected)
    {
        var rectangle = new PdfRectangle(new XPoint(10, 20), new XPoint(110, 220));

        rectangle.Contains(x, y).Should().Be(expected);
        rectangle.Contains(new XPoint(x, y)).Should().Be(expected);
    }

    [Theory]
    [InlineData(10, 20, 100, 200, true)] // exactly the same area
    [InlineData(20, 30, 10, 10, true)]
    [InlineData(5, 30, 10, 10, false)]   // starts left of it
    [InlineData(20, 15, 10, 10, false)]  // starts below it
    [InlineData(20, 30, 91, 10, false)]  // reaches past the right edge
    [InlineData(20, 30, 10, 191, false)] // reaches past the top edge
    public void ARegionIsContainedOnlyWhenItLiesEntirelyInside(double x, double y, double width, double height, bool expected)
    {
        var rectangle = new PdfRectangle(new XPoint(10, 20), new XPoint(110, 220));
        var region = new XRect(x, y, width, height);

        rectangle.Contains(region).Should().Be(expected);
        rectangle.Contains(new PdfRectangle(region)).Should().Be(expected);
    }

    [Fact]
    public void ARectangleIsSpelledAsTheArrayItIsWrittenAs()
    {
        new PdfRectangle(new XPoint(0, 0), new XPoint(595, 842)).ToString().Should().Be("[0 0 595 842]");
        new PdfRectangle(new XPoint(1.5, -2.25), new XPoint(3.14159, 4)).ToString().Should().Be("[1.5 -2.25 3.142 4]");
    }

    [Fact]
    public void ARectangleComesBackFromTheFileAsAnEqualRectangle()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var rectangle = new PdfRectangle(new XPoint(1.5, 2), new XPoint(300.25, 400));
        document.Internals.Catalog.Elements.SetRectangle("/TestBox", rectangle);

        using var output = new MemoryStream();
        document.Save(output, false);
        var saved = output.ToArray();
        Encoding.Latin1.GetString(saved).Should().Contain("/TestBox [1.5 2 300.25 400]");

        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Import);
        reread.Internals.Catalog.Elements.GetRectangle("/TestBox").Should().Be(rectangle);
    }

    [Fact]
    public void APageInheritsAMediaBoxStatedOnItsParent()
    {
        var document = OpenPageTree("/MediaBox[0 0 300 400]");

        var mediaBox = document.Pages[0].MediaBox;

        mediaBox.Should().Be(new PdfRectangle(new XPoint(0, 0), new XPoint(300, 400)));
        document.Pages[0].Width.Point.Should().Be(300);
        document.Pages[0].Height.Point.Should().Be(400);
    }

    [Fact]
    public void APageInheritsAMediaBoxItsParentHoldsByReference()
    {
        var document = OpenPageTree("/MediaBox 4 0 R", "[10 20 210 120]");

        document.Pages[0].MediaBox.Should().Be(new PdfRectangle(new XPoint(10, 20), new XPoint(210, 120)));
    }

    [Fact]
    public void APageInheritsACropBoxStatedOnItsParent()
    {
        var document = OpenPageTree("/MediaBox[0 0 300 400]/CropBox[5 5 295 395]");

        document.Pages[0].CropBox.Should().Be(new PdfRectangle(new XPoint(5, 5), new XPoint(295, 395)));
    }

    [Fact]
    public void AnInheritedMediaBoxThatIsNotAnArrayIsRefused()
    {
        FluentActions.Invoking(() => OpenPageTree("/MediaBox 42"))
            .Should().Throw<System.InvalidOperationException>();
    }

    [Theory]
    [InlineData("/CropBox null", null)]
    [InlineData("/CropBox 4 0 R", "null")]
    public void ANullBoxOnTheParentIsNotCopiedOntoThePage(string pagesEntries, string extraObject)
    {
        // ISO 32000-1 7.3.7: an entry whose value is null is the same as no entry at all. It used
        // to be read as the empty rectangle, copied down and written out as /CropBox [0 0 0 0],
        // which crops the page to nothing; held by reference, it could not be read at all.
        string[] extraObjects = extraObject == null ? [] : [extraObject];
        var document = OpenPageTree("/MediaBox[0 0 300 400]" + pagesEntries, extraObjects);

        document.Pages[0].Elements.ContainsKey("/CropBox").Should().BeFalse();

        using var output = new MemoryStream();
        document.Save(output, false);
        var saved = output.ToArray();
        Encoding.Latin1.GetString(saved).Should().NotContain("[0 0 0 0]");

        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Modify);
        reread.Pages[0].Elements.ContainsKey("/CropBox").Should().BeFalse();
        reread.Pages[0].MediaBox.Should().Be(new PdfRectangle(new XPoint(0, 0), new XPoint(300, 400)));
    }

    [Fact]
    public void ANullBoxPartWayDownTheTreeLeavesTheBoxFromFurtherUpInPlace()
    {
        // The inner node's null says nothing, so the box its own parent states still reaches the page.
        var document = Open([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/MediaBox[0 0 300 400]/CropBox[5 5 295 395]>>",
            "<</Type/Pages/Parent 2 0 R/Kids[4 0 R]/Count 1/CropBox null>>",
            "<</Type/Page/Parent 3 0 R>>"
        ]);

        document.Pages[0].CropBox.Should().Be(new PdfRectangle(new XPoint(5, 5), new XPoint(295, 395)));
    }

    [Fact]
    public void APageWhoseOwnBoxIsNullInheritsTheBoxItsParentStates()
    {
        var document = Open([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/MediaBox[0 0 300 400]/CropBox[5 5 295 395]>>",
            "<</Type/Page/Parent 2 0 R/CropBox null>>"
        ]);

        document.Pages[0].CropBox.Should().Be(new PdfRectangle(new XPoint(5, 5), new XPoint(295, 395)));
    }

    [Theory]
    [InlineData("/Rotate")]
    [InlineData("/Resources")]
    public void ANullForAnyOtherInheritableEntryIsAsIfItWereAbsent(string key)
    {
        // The same rule for the two inheritable entries that are not boxes. Neither could be read
        // at all: the null was cast to the integer or the dictionary the entry should hold.
        var document = OpenPageTree("/MediaBox[0 0 300 400]" + key + " null");

        document.Pages[0].Elements.ContainsKey(key).Should().BeFalse();
    }

    /// <summary>
    ///   One page under a page tree node carrying <paramref name="pagesEntries"/>, the page itself
    ///   stating no box at all. Anything in <paramref name="extraObjects"/> is numbered from four.
    /// </summary>
    private static PdfDocument OpenPageTree(string pagesEntries, params string[] extraObjects)
    {
        var objects = new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1" + pagesEntries + ">>",
            "<</Type/Page/Parent 2 0 R>>"
        };
        objects.AddRange(extraObjects);

        return Open(objects);
    }

    private static PdfDocument Open(List<string> objects)
    {
        return Pdf.IO.PdfReader.Open(new MemoryStream(RawPdf.Build(objects)), PdfDocumentOpenMode.Modify);
    }
}
