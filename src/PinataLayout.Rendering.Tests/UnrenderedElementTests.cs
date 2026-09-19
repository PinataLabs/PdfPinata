using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.Rendering.Tests.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Two elements the document object model can hold and this assembly cannot draw.
/// </summary>
/// <remarks>
///   <para>
///     Both were dropped silently until these tests were written. A <c>Footnote</c> fell through the
///     <c>default:</c> of <c>ParagraphRenderer.FormatElement</c>; a <c>Barcode</c> produced a null
///     renderer from <c>Renderer.Create</c>, which the callers treat as "nothing to draw" because
///     that is the right answer for a legend and for a bookmark. Either way the caller built an
///     element, read its properties back exactly as they were set, and got a page without it.
///   </para>
///   <para>
///     These pin the refusal rather than the feature. Neither element is rendered now and neither is
///     going to be by this spec - laying footnotes out is a feature with its own design - so what is
///     worth protecting is that the gap stays audible, and that making it audible did not cost the
///     elements beside them that do render.
///   </para>
/// </remarks>
public class UnrenderedElementTests
{
    // ----- the footnote, where it cannot go -----
    //
    // A footnote in a paragraph of a section renders now; see FootnoteTests. What is still refused
    // is a note somewhere that owns no page to put it at the foot of.

    [Fact]
    public void AFootnoteInATableCellIsRefusedRatherThanDropped()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn(Unit.FromCentimeter(8));
        table.AddRow().Cells[0].AddParagraph("A claim in a cell").AddFootnote("The support.");

        Action render = () => Rendered.Of(document);

        render.Should().Throw<NotSupportedException>()
            .WithMessage("*laid out on a page*");
    }

    [Fact]
    public void AFootnoteInAHeaderIsRefusedRatherThanDropped()
    {
        // A header is formatted once per position and repeated on every page it applies to, so a
        // note in one has no single page to belong to.
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("Body");
        section.Headers.Primary.AddParagraph("Running head").AddFootnote("The support.");

        Action render = () => Rendered.Of(document);

        render.Should().Throw<NotSupportedException>()
            .WithMessage("*laid out on a page*");
    }

    [Fact]
    public void TheRefusalNamesTheThingToDoInstead()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn(Unit.FromCentimeter(8));
        table.AddRow().Cells[0].AddParagraph("Text").AddFootnote("Note");

        Action render = () => Rendered.Of(document);

        // The value of the message is the way out of it.
        render.Should().Throw<NotSupportedException>()
            .WithMessage("*Move the footnote*");
    }

    [Fact]
    public void AParagraphWithNoFootnoteInItStillRenders()
    {
        // The guard on the cases above. FormatElement switches on the element's type name, and a
        // switch with a new case in it is one typo away from taking the wrong one.
        var document = new Document();
        document.AddSection().AddParagraph("Nothing unusual here.");

        Rendered.Of(document).PageCount.Should().Be(1);
    }

    // ----- the bar code -----

    [Fact]
    public void ABarcodeInASectionIsRefusedRatherThanDropped()
    {
        var document = new Document();
        var barcode = document.AddSection().Elements.AddBarcode();
        barcode.Code = "PDFSHARPCORE";
        barcode.Type = BarcodeType.Barcode39;

        Action render = () => Rendered.Of(document);

        render.Should().Throw<NotSupportedException>()
            .WithMessage("*no renderer for the Barcode shape*");
    }

    [Fact]
    public void TheBarcodeRefusalNamesTheRouteThatDraws()
    {
        var document = new Document();
        document.AddSection().Elements.AddBarcode().Code = "PDFSHARPCORE";

        Action render = () => Rendered.Of(document);

        // PdfSharp draws bar codes and always has. The demonstration app's Barcodes demo is that
        // route; this message is how somebody who started in MigraDoc finds it.
        render.Should().Throw<NotSupportedException>()
            .WithMessage("*XGraphics.DrawBarCode*");
    }

    [Fact]
    public void AChartInASectionStillRenders()
    {
        // The guard on the case above, and on the duplicated 'is Chart' branch deleted beside it.
        // Renderer.Create dispatches on a chain of type tests; a chart added to that chain in the
        // wrong place would be answered by the barcode branch or by nothing.
        var document = new Document();
        var chart = document.AddSection().AddChart(ChartType.Column2D);
        chart.Width = Unit.FromCentimeter(10);
        chart.Height = Unit.FromCentimeter(6);
        var series = chart.SeriesCollection.AddSeries();
        series.Add(1.0, 4.0, 2.0);

        Rendered.Of(document).PageCount.Should().Be(1);
    }
}
