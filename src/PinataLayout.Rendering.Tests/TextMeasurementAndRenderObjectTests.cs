using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Two public members that had never been executed: <see cref="TextMeasurement"/>, which measures
///   a string at design time without laying a document out, and
///   <see cref="DocumentRenderer.RenderObject"/>, which draws a single paragraph, table or shape
///   onto a graphics surface of the caller's choosing rather than onto a page of its own.
/// </summary>
/// <remarks>
///   Both need a real font, so they belong here rather than in the DOM suite - its
///   <c>NamedFontsOnly</c> resolver serves a font <em>name</em> and throws if asked for a face.
/// </remarks>
public class TextMeasurementAndRenderObjectTests
{
    static XGraphics OnAPage()
    {
        var document = new PdfDocument();
        return XGraphics.FromPdfPage(document.AddPage());
    }

    // ----- TextMeasurement.MeasureString ------------------------------------------------------------

    static XSize MeasuredIn(UnitType unit)
    {
        using var gfx = OnAPage();
        return new TextMeasurement(gfx, new Font("Arial", 12)).MeasureString("Measure me", unit);
    }

    [Fact]
    public void MeasuringInPointsReportsSomethingOfASensibleSize()
    {
        var size = MeasuredIn(UnitType.Point);

        size.Width.Should().BeGreaterThan(0);
        size.Height.Should().BeGreaterThan(0);
    }

    [Theory]
    // Each conversion is asserted against the measurement in points rather than against a number
    // typed in, so the test says what the unit means and stays true whatever the font measures.
    [InlineData(UnitType.Centimeter, 2.54 / 72)]
    [InlineData(UnitType.Inch, 1.0 / 72)]
    [InlineData(UnitType.Millimeter, 25.4 / 72)]
    [InlineData(UnitType.Pica, 1.0 / 12)]
    public void EveryUnitIsTheMeasurementInPointsConvertedIntoIt(UnitType unit, double factor)
    {
        var inPoints = MeasuredIn(UnitType.Point);

        var converted = MeasuredIn(unit);

        converted.Width.Should().BeApproximately(inPoints.Width * factor, inPoints.Width * factor * 0.001);
        converted.Height.Should().BeApproximately(inPoints.Height * factor, inPoints.Height * factor * 0.001);
    }

    [Fact]
    public void TheOverloadWithoutAUnitMeasuresInPoints()
    {
        using var gfx = OnAPage();
        var measurement = new TextMeasurement(gfx, new Font("Arial", 12));

        var implied = measurement.MeasureString("Measure me");
        var stated = measurement.MeasureString("Measure me", UnitType.Point);

        implied.Width.Should().Be(stated.Width);
        implied.Height.Should().Be(stated.Height);
    }

    [Fact]
    public void MeasuringNothingIsRefusedRatherThanAnsweredWithZero()
    {
        using var gfx = OnAPage();
        var measurement = new TextMeasurement(gfx, new Font("Arial", 12));

        var measuring = () => measurement.MeasureString(null, UnitType.Point);

        measuring.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AUnitThatIsNotOneOfTheUnitsIsRefused()
    {
        // The guard exists because the switch below it has an arm per unit and a Debug.Assert(false)
        // for anything else - which in a release build is a no-op that would return the measurement
        // in points under whatever name was asked for.
        using var gfx = OnAPage();
        var measurement = new TextMeasurement(gfx, new Font("Arial", 12));

        var measuring = () => measurement.MeasureString("Measure me", (UnitType)999);

        measuring.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangingTheFontChangesWhatIsMeasured()
    {
        // The cached XFont is dropped when the font is replaced, which is the only reason the second
        // measurement can differ from the first.
        using var gfx = OnAPage();
        var measurement = new TextMeasurement(gfx, new Font("Arial", 12));

        var small = measurement.MeasureString("Measure me", UnitType.Point);
        measurement.Font = new Font("Arial", 36);
        var large = measurement.MeasureString("Measure me", UnitType.Point);

        large.Width.Should().BeGreaterThan(small.Width);
    }

    // ----- DocumentRenderer.RenderObject ------------------------------------------------------------

    static DocumentRenderer RendererFor(Document document)
    {
        var renderer = new DocumentRenderer(document);
        renderer.PrepareDocument();
        return renderer;
    }

    /// <summary>
    ///   What the page shows, as text.
    /// </summary>
    /// <remarks>
    ///   Not <c>Glyphs</c>, which the rest of this suite uses. That reads glyph identifiers because
    ///   <c>PdfDocumentRenderer</c> embeds its fonts as Identity-H - but these tests draw onto an
    ///   <see cref="XGraphics"/> the caller made, which is the whole point of <c>RenderObject</c>,
    ///   and that path writes the string as characters. Reading it as glyph pairs is what the first
    ///   version of these tests did, and it reported "Framed" as {18034, 24941, 25956} - the
    ///   characters two at a time.
    /// </remarks>
    static string ShownOn(PdfPage page) => string.Concat(TextOperators.ShownStrings(page));

    [Fact]
    public void AParagraphCanBeDrawnOnItsOwnWithoutLayingTheDocumentOut()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("Standalone");
        var renderer = RendererFor(document);

        var pdf = new PdfDocument();
        var page = pdf.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            renderer.RenderObject(gfx, XUnit.FromPoint(20), XUnit.FromPoint(20), XUnit.FromPoint(300), paragraph);

        ShownOn(page).Should().Contain("Standalone");
    }

    [Fact]
    public void ATableCanBeDrawnOnItsOwn()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn(Unit.FromCentimeter(4));
        table.AddRow().Cells[0].AddParagraph("Celled");
        var renderer = RendererFor(document);

        var pdf = new PdfDocument();
        var page = pdf.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            renderer.RenderObject(gfx, XUnit.FromPoint(20), XUnit.FromPoint(20), XUnit.FromPoint(300), table);

        ShownOn(page).Should().Contain("Celled");
    }

    [Fact]
    public void AShapeCanBeDrawnOnItsOwn()
    {
        var document = new Document();
        var frame = document.AddSection().AddTextFrame();
        frame.Width = Unit.FromCentimeter(6);
        frame.Height = Unit.FromCentimeter(2);
        frame.AddParagraph("Framed");
        var renderer = RendererFor(document);

        var pdf = new PdfDocument();
        var page = pdf.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            renderer.RenderObject(gfx, XUnit.FromPoint(20), XUnit.FromPoint(20), XUnit.FromPoint(300), frame);

        ShownOn(page).Should().Contain("Framed");
    }

    [Fact]
    public void DrawingWithNoGraphicsToDrawOnIsRefused()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("Standalone");
        var renderer = RendererFor(document);

        var drawing = () => renderer.RenderObject(
            null, XUnit.FromPoint(20), XUnit.FromPoint(20), XUnit.FromPoint(300), paragraph);

        drawing.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DrawingNothingIsRefused()
    {
        var document = new Document();
        var renderer = RendererFor(document);

        using var gfx = OnAPage();
        var drawing = () => renderer.RenderObject(
            gfx, XUnit.FromPoint(20), XUnit.FromPoint(20), XUnit.FromPoint(300), null);

        drawing.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SomethingThatIsNotAParagraphTableOrShapeIsRefusedByName()
    {
        // Only three kinds of object have a renderer that can be given a rectangle and asked to fill
        // it. Anything else is refused up front rather than failing somewhere inside Renderer.Create.
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("Something");
        var renderer = RendererFor(document);

        using var gfx = OnAPage();
        var drawing = () => renderer.RenderObject(
            gfx, XUnit.FromPoint(20), XUnit.FromPoint(20), XUnit.FromPoint(300), section);

        drawing.Should().Throw<ArgumentException>();
    }
}
