using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   A shape is anything that can be placed rather than flowed - a text frame, an image, a chart
///   given a position. What it carries is where it goes and what it is drawn with, and
///   <see cref="LeftPosition"/> and <see cref="TopPosition"/> are the pair that say where.
///   <para>
///   Those two are value types with five implicit conversions each, so a caller writes
///   <c>frame.Left = "3cm"</c> or <c>frame.Left = ShapePosition.Center</c> and the type sorts out
///   which of the two things it is holding. <see cref="LeftAndTopPositionParityTests"/> covers the
///   parse the string route goes through; what is left is the conversions themselves, the two ways
///   a position writes itself out, and the shape properties around them.
///   </para>
/// </summary>
public class ShapePlacementTests
{
    private static TextFrame AFrame() => new Document().AddSection().AddTextFrame();

    private static string DdlOf(DocumentObject documentObject) => DdlWriter.WriteToString(documentObject);

    private static TextFrame FrameFrom(string attributes) =>
        (TextFrame)DdlReader
            .DocumentFromString("\\document{\\section{\\textframe[" + attributes + "]{framed}}}")
            .LastSection.Elements[0];

    // ----- where a shape goes -----------------------------------------------------------------

    /// <summary>
    ///   Five ways to say the same left edge, one per implicit conversion. A number with no unit is
    ///   points, which is what <see cref="Unit"/> means by a bare double.
    /// </summary>
    [Fact]
    public void ALeftEdgeCanBeGivenAsAUnitAStringANumberOrAName()
    {
        var frame = AFrame();

        frame.Left = Unit.FromCentimeter(3);
        frame.Left.Position.Centimeter.Should().BeApproximately(3, 1e-9);

        frame.Left = "2cm";
        frame.Left.Position.Centimeter.Should().BeApproximately(2, 1e-9);

        frame.Left = 12.5;
        frame.Left.Position.Point.Should().Be(12.5);

        frame.Left = 7;
        frame.Left.Position.Point.Should().Be(7);

        frame.Left = ShapePosition.Center;
        frame.Left.ShapePosition.Should().Be(ShapePosition.Center);
    }

    [Fact]
    public void ATopEdgeCanBeGivenTheSameFiveWays()
    {
        var frame = AFrame();

        frame.Top = Unit.FromCentimeter(3);
        frame.Top.Position.Centimeter.Should().BeApproximately(3, 1e-9);

        frame.Top = "2cm";
        frame.Top.Position.Centimeter.Should().BeApproximately(2, 1e-9);

        frame.Top = 12.5;
        frame.Top.Position.Point.Should().Be(12.5);

        frame.Top = 7;
        frame.Top.Position.Point.Should().Be(7);

        frame.Top = ShapePosition.Bottom;
        frame.Top.ShapePosition.Should().Be(ShapePosition.Bottom);
    }

    /// <summary>
    ///   A position writes itself as whichever of the two it is holding, under the same attribute
    ///   name. A reader takes both back, which is what makes the two representations one property.
    /// </summary>
    [Fact]
    public void APositionIsWrittenAsANumberOrAsANameAndReadBackEitherWay()
    {
        var byNumber = AFrame();
        byNumber.Left = Unit.FromCentimeter(3);
        byNumber.Top = Unit.FromCentimeter(4);
        DdlOf(byNumber).Should().Contain("Left = \"3cm\"").And.Contain("Top = \"4cm\"");

        var byName = AFrame();
        byName.Left = ShapePosition.Center;
        byName.Top = ShapePosition.Bottom;
        DdlOf(byName).Should().Contain("Left = Center").And.Contain("Top = Bottom");

        FrameFrom("Left = Center").Left.ShapePosition.Should().Be(ShapePosition.Center);
        FrameFrom("Top = \"4cm\"").Top.Position.Centimeter.Should().BeApproximately(4, 1e-9);
    }

    /// <summary>
    ///   The two share one <see cref="ShapePosition"/> enum and accept different members of it, so
    ///   each has to refuse what the other takes - a left edge at the bottom of the page means
    ///   nothing, and the alternative is a shape silently placed somewhere nobody asked for.
    /// </summary>
    [Fact]
    public void AnEdgeRefusesANameThatMeansNothingInItsDirection()
    {
        var frame = AFrame();

        var sideways = () => frame.Left = ShapePosition.Bottom;
        var upright = () => frame.Top = ShapePosition.Inside;

        sideways.Should().Throw<ArgumentException>();
        upright.Should().Throw<ArgumentException>();
    }

    // ----- what a shape is drawn with ---------------------------------------------------------

    [Fact]
    public void AShapeWritesEverythingItWasGivenAndNothingItWasNot()
    {
        var frame = AFrame();

        DdlOf(frame).Should().NotContain("RelativeHorizontal");

        frame.Width = Unit.FromCentimeter(5);
        frame.Height = Unit.FromCentimeter(2);
        frame.RelativeHorizontal = RelativeHorizontal.Page;
        frame.RelativeVertical = RelativeVertical.Page;
        frame.AlternativeText = "a box of words";
        frame.LineFormat = new LineFormat { Width = Unit.FromPoint(1) };
        frame.FillFormat = new FillFormat { Color = Colors.Azure };
        frame.WrapFormat = new WrapFormat { Style = WrapStyle.Through };
        frame.Left = Unit.FromCentimeter(1);
        frame.Top = Unit.FromCentimeter(1);

        frame.AlternativeText.Should().Be("a box of words");
        frame.LineFormat.Width.Point.Should().Be(1);
        frame.FillFormat.Color.Should().Be(Colors.Azure);
        frame.WrapFormat.Style.Should().Be(WrapStyle.Through);

        var ddl = DdlOf(frame);
        ddl.Should().Contain("RelativeHorizontal = Page")
            .And.Contain("RelativeVertical = Page")
            .And.Contain("AlternativeText = \"a box of words\"")
            .And.Contain("Width = \"5cm\"")
            .And.Contain("Height = \"2cm\"");
    }

    [Fact]
    public void AShapeCopiesItselfIntoItsOwnType()
    {
        var frame = AFrame();
        frame.AlternativeText = "a box of words";

        ((Shape)frame).Clone().AlternativeText.Should().Be("a box of words");
    }

    // ----- a text frame's own members ---------------------------------------------------------

    [Fact]
    public void ATextFrameTakesContentBuiltBeforeItAndBuildsItToo()
    {
        var frame = AFrame();

        frame.Add(new Paragraph());
        frame.Add(new Chart(ChartType.Line));
        frame.Add(new Table());
        frame.Add(new Image { Source = new NamedImage("picture.png") });

        frame.AddParagraph().Should().NotBeNull();
        frame.AddChart().Type.Should().Be(ChartType.Line);
        frame.AddChart(ChartType.Bar2D).Type.Should().Be(ChartType.Bar2D);
        frame.AddTable().Should().NotBeNull();
        frame.AddImage(new NamedImage("picture.png")).Should().NotBeNull();

        frame.Elements.Count.Should().Be(9);
    }

    [Fact]
    public void ATextFrameWritesItsFourMarginsAndItsOrientation()
    {
        var frame = AFrame();

        frame.MarginLeft = Unit.FromPoint(1);
        frame.MarginRight = Unit.FromPoint(2);
        frame.MarginTop = Unit.FromPoint(3);
        frame.MarginBottom = Unit.FromPoint(4);
        frame.Orientation = TextOrientation.Upward;
        frame.Elements = frame.Elements.Clone();

        frame.MarginBottom.Point.Should().Be(4);
        frame.Orientation.Should().Be(TextOrientation.Upward);

        var ddl = DdlOf(frame);
        ddl.Should().Contain("MarginLeft = 1")
            .And.Contain("MarginRight = 2")
            .And.Contain("MarginTop = 3")
            .And.Contain("MarginBottom = 4")
            .And.Contain("Orientation = Upward");
    }

    [Fact]
    public void ATextFrameCopiesItselfWithTheContentInIt()
    {
        var frame = AFrame();
        frame.AddParagraph("here");

        var copy = frame.Clone();

        copy.Should().NotBeSameAs(frame);
        copy.Elements.Count.Should().Be(1);
    }

    // ----- an image's own members -------------------------------------------------------------

    [Fact]
    public void AnImageWritesTheNameOfItsSourceAndEveryDialTurnedOnIt()
    {
        var image = new Document().AddSection().AddImage(new NamedImage("picture.png"));

        image.ScaleWidth = 0.5;
        image.ScaleHeight = 0.25;
        image.LockAspectRatio = true;
        image.Resolution = 150;
        image.PictureFormat.CropLeft = Unit.FromPoint(1);
        image.PictureFormat.CropRight = Unit.FromPoint(2);
        image.PictureFormat.CropTop = Unit.FromPoint(3);
        image.PictureFormat.CropBottom = Unit.FromPoint(4);

        image.IsNull().Should().BeFalse();
        image.Clone().Source.Name.Should().Be("picture.png");

        var ddl = DdlOf(image);
        ddl.Should().Contain("\\image(\"picture.png\")")
            .And.Contain("ScaleWidth = 0.5")
            .And.Contain("ScaleHeight = 0.25")
            .And.Contain("LockAspectRatio = true")
            .And.Contain("Resolution = 150")
            .And.Contain("CropLeft = 1")
            .And.Contain("CropBottom = 4");
    }

    /// <summary>
    ///   An image source that knows nothing but its name; see
    ///   <see cref="SectionAndHeaderFooterTests"/> for why a DOM test needs no more than that.
    /// </summary>
    private sealed class NamedImage : ImageSource.IImageSource
    {
        internal NamedImage(string name) => Name = name;

        public int Width => 1;

        public int Height => 1;

        public string Name { get; }

        public bool Transparent => false;

        public void SaveAsJpeg(MemoryStream ms) => throw new NotSupportedException();

        public PixelBuffer GetPixels() => throw new NotSupportedException();
    }
}
