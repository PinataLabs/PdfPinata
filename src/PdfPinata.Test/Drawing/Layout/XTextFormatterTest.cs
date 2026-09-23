using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Drawing.Layout.enums;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

[Collection(RasterizingCollection.Name)]
public class XTextFormatterTest
{
    private static readonly string OutDir = "TestResults/XTextFormatterTest";
    private static readonly string ExpectedImagesPath = Path.Combine("Drawing", "Layout");

    /// <summary>
    ///   How far a page may stand from its reference image before the comparison fails.
    ///   The font is shipped with the tests, so the text is laid out identically wherever they
    ///   run and only the drawing of the edges of the glyphs is left to the rasterizer of the
    ///   machine. That lands two orders of magnitude below this. Text that moved does not:
    ///   the defects these images caught while they were being remade measured between 0.05
    ///   and 0.16.
    /// </summary>
    private const double MaxDifference = 0.02;

    private readonly PdfDocument _document;
    private readonly PdfPage _page;
    private readonly XGraphics _renderer;
    private readonly XTextFormatter _textFormatter;

    // Run before each test
    public XTextFormatterTest()
    {
        _document = new PdfDocument();
        _page = _document.AddPage();
        _page.Size = PageSize.A6; // 295 x 417 pts
        _renderer = XGraphics.FromPdfPage(_page);
        _textFormatter = new XTextFormatter(_renderer);
    }
        
    [GoldenImageFact]
    public void DrawSingleLineString()
    {
        var layout = new XRect(12, 12, 200, 50);
        _textFormatter.DrawString("This is a simple single line test", new XFont("Arial", 12), XBrushes.Black, layout);

        var diffResult = DiffPage(_document, "DrawSingleLineString", 1);
            
        diffResult.DiffValue.Should().BeLessThan(MaxDifference);
    }
        
    [GoldenImageFact]
    public void DrawMultilineStringWithTruncate()
    {
        var layout = new XRect(12, 12, 200, 40);
        _renderer.DrawRectangle(XBrushes.LightGray, layout);
        _textFormatter.DrawString("This is text\nspanning 3 lines\nbut only space for 2", new XFont("Arial", 12), XBrushes.Black, layout);

        var diffResult = DiffPage(_document, "DrawMultilineStringWithTruncate", 1);
            
        diffResult.DiffValue.Should().BeLessThan(MaxDifference);
    }
        
    [GoldenImageFact]
    public void DrawMultiLineStringWithOverflow()
    {
        var layout = new XRect(12, 12, 200, 40);
        _renderer.DrawRectangle(XBrushes.LightGray, layout);
        _textFormatter.AllowVerticalOverflow = true;
        _textFormatter.DrawString("This is text\nspanning 3 lines\nand overflow shows all three", new XFont("Arial", 12), XBrushes.Black, layout);

        var diffResult = DiffPage(_document, "DrawMultiLineStringWithOverflow", 1);
            
        diffResult.DiffValue.Should().BeLessThan(MaxDifference);
    }
        
    [GoldenImageFact]
    public void DrawMultiLineStringsWithAlignment()
    {
        var layout1 = new XRect(12, 12, 200, 80);
        _renderer.DrawRectangle(XBrushes.LightGray, layout1);
        _textFormatter.DrawString("This is text\naligned to the top-left", new XFont("Arial", 12), XBrushes.Black, layout1);

        var layout2 = new XRect(12, 100, 200, 80);
        _renderer.DrawRectangle(XBrushes.LightGray, layout2);
        _textFormatter.SetAlignment(new TextFormatAlignment { Horizontal = XParagraphAlignment.Center, Vertical = XVerticalAlignment.Middle});
        _textFormatter.DrawString("This is text\naligned to the middle-center", new XFont("Arial", 12), XBrushes.Black, layout2);

        var layout3 = new XRect(12, 200, 200, 80);
        _renderer.DrawRectangle(XBrushes.LightGray, layout3);
        _textFormatter.SetAlignment(new TextFormatAlignment { Horizontal = XParagraphAlignment.Right, Vertical = XVerticalAlignment.Bottom});
        _textFormatter.DrawString("This is text\naligned to the bottom-right", new XFont("Arial", 12), XBrushes.Black, layout3);

        var diffResult = DiffPage(_document, "DrawMultiLineStringsWithAlignment", 1);
            
        diffResult.DiffValue.Should().BeLessThan(MaxDifference);
    }
        
    [GoldenImageFact]
    public void DrawMultiLineStringsWithLineHeight()
    {
        var font = new XFont("Arial", 12);
            
        var layout1 = new XRect(10, 10, 200, 80);
        _renderer.DrawRectangle(XBrushes.LightGray, layout1);
        _textFormatter.DrawString("This is text\naligned to the top-left\nand a custom line height", font, XBrushes.Black, layout1, 16);

        var layout2 = new XRect(10, 110, 200, 80);
        _renderer.DrawRectangle(XBrushes.LightGray, layout2);
        _textFormatter.SetAlignment(new TextFormatAlignment { Horizontal = XParagraphAlignment.Center, Vertical = XVerticalAlignment.Middle});
        _textFormatter.DrawString("This is text\naligned to the middle-center\nand a custom line height", font, XBrushes.Black, layout2, 16);

        var layout3 = new XRect(10, 210, 200, 80);
        _renderer.DrawRectangle(XBrushes.LightGray, layout3);
        _textFormatter.SetAlignment(new TextFormatAlignment { Horizontal = XParagraphAlignment.Right, Vertical = XVerticalAlignment.Bottom});
        _textFormatter.DrawString("This is text\naligned to the bottom-right\nand a custom line height", font, XBrushes.Black, layout3, 16);

        var layout4 = new XRect(10, 310, 200, 80);
        _renderer.DrawRectangle(XBrushes.LightGray, layout4);
        _textFormatter.SetAlignment(new TextFormatAlignment { Horizontal = XParagraphAlignment.Center, Vertical = XVerticalAlignment.Middle});
        _textFormatter.DrawString("This is text\nwith a very small\nline height", font, XBrushes.Black, layout4, 6);

        var diffResult = DiffPage(_document, "DrawMultiLineStringsWithLineHeight", 1);
            
        diffResult.DiffValue.Should().BeLessThan(MaxDifference);
    }

    [Theory]
    [InlineData("Line1\r\nLine2")] // Windows
    [InlineData("Line1\nLine2")]   // Unix
    [InlineData("Line1\rLine2")]   // classic Mac
    public void DrawStringPutsEachLineOfABreakedTextOnItsOwnLine(string text)
    {
        var font = new XFont("Arial", 12);
        var layout = new XRect(12, 12, 200, 50);

        _textFormatter.DrawString(text, font, XBrushes.Black, layout);

        var shownText = GetShownText();
        shownText.Should().HaveCount(2, "each line is drawn with a single show operation");
        shownText[0].Position.X.Should().Be(shownText[1].Position.X);
        LineDistance(shownText).Should().BeApproximately(font.GetHeight(), 0.001);
    }

    [Fact]
    public void DrawStringDoesNotDrawTextThatDoesNotFitTheLayoutRectangle()
    {
        var font = new XFont("Arial", 12);
        // Room for two lines and half of a third. The layout keeps a line while its top stays
        // above the height of the rectangle less one ascent and one descent, so a rectangle of
        // exactly two line heights decides on the difference between the height of a line and
        // the height of the characters on it, which is a fraction of a point and differs from
        // font to font. Half a line of room on either side of the answer keeps this about the
        // truncation rather than about the metrics of whichever font the machine resolves.
        var layout = new XRect(12, 12, 200, 2.5 * font.GetHeight());

        _textFormatter.DrawString("Line1\nLine2\nLine3", font, XBrushes.Black, layout);

        var shownText = GetShownText();
        shownText.Should().HaveCount(2);
        shownText.Select(t => t.Position.Y).Distinct().Should().HaveCount(2);
        // The line that does not fit is left out rather than drawn somewhere else. A block that
        // never got a place keeps the one it was born with, which puts it on the first line, so
        // check that both lines still show a single word.
        shownText.Select(t => t.Text.Length).Distinct().Should().HaveCount(1, "every line holds one word");
    }

    [Fact]
    public void DrawStringKeepsBlankLines()
    {
        var font = new XFont("Arial", 12);
        var layout = new XRect(12, 12, 200, 100);

        _textFormatter.DrawString("Line1\n\nLine3", font, XBrushes.Black, layout);

        var shownText = GetShownText();
        shownText.Should().HaveCount(2, "the blank line has nothing to show");
        LineDistance(shownText).Should().BeApproximately(2 * font.GetHeight(), 0.001);
    }

    [Fact]
    public void GetLayoutReportsOneLineHeightPerLineOfABreakedText()
    {
        var font = new XFont("Arial", 12);
        var layout = new XRect(12, 12, 200, 100);

        var required = _textFormatter.GetLayout("Line1\r\nLine2", font, XBrushes.Black, layout);

        required.Height.Should().BeApproximately(2 * font.GetHeight(), 0.001);
    }

    [Fact]
    public void DrawStringStretchesJustifiedLinesButNotTheLastOneOfTheText()
    {
        var font = new XFont("Arial", 12);
        var layout = new XRect(12, 12, 200, 100);

        _textFormatter.DrawString(
            "This is a long piece of text that needs more than a single line to fit, and ends short.",
            font, XBrushes.Black, layout, Justified);

        var lines = GetShownText().GroupBy(t => t.Position.Y).ToArray();
        lines.Length.Should().BeGreaterThan(2);
        lines.First().Should().HaveCountGreaterThan(1, "a stretched line is drawn word by word");
        lines.Last().Should().HaveCount(1, "the last line of the text is not stretched");
    }

    [Fact]
    public void DrawStringDoesNotStretchTheJustifiedLineBeforeALineBreak()
    {
        var font = new XFont("Arial", 12);
        var layout = new XRect(12, 12, 200, 100);

        _textFormatter.DrawString("Two words\nTwo words", font, XBrushes.Black, layout, Justified);

        var shownText = GetShownText();
        shownText.Should().HaveCount(2, "neither line is stretched, so each is drawn in one go");
        shownText[0].Position.X.Should().Be(shownText[1].Position.X);
        LineDistance(shownText).Should().BeApproximately(font.GetHeight(), 0.001);
    }

    private static TextFormatAlignment Justified =>
        new() { Horizontal = XParagraphAlignment.Justify, Vertical = XVerticalAlignment.Top };

    /// <summary>
    ///   Gets the distance between the first two shown lines. The Y axis of the PDF user space
    ///   points up, so a line further down the page has the lower Y coordinate.
    /// </summary>
    private static double LineDistance(IReadOnlyList<(string Text, XPoint Position)> shownText)
    {
        return shownText[0].Position.Y - shownText[1].Position.Y;
    }

    /// <summary>
    ///   Extracts the text-showing operations from the page, in the order they were written.
    ///   Td offsets are relative to the previous text position, so they are accumulated to
    ///   give the absolute position of each shown string.
    /// </summary>
    private IReadOnlyList<(string Text, XPoint Position)> GetShownText()
    {
        _renderer.Dispose(); // flush the content stream

        var result = new List<(string, XPoint)>();
        var position = new XPoint();
        foreach (var op in ContentReader.ReadContent(_page).OfType<COperator>())
        {
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.BT:
                    position = new XPoint();
                    break;
                case OpCodeName.Td:
                    position += new XVector(GetNumber(op.Operands[0]), GetNumber(op.Operands[1]));
                    break;
                case OpCodeName.Tj:
                    result.Add((((CString)op.Operands[0]).Value, position));
                    break;
            }
        }
        return result;
    }

    private static double GetNumber(CObject operand)
    {
        return operand switch
        {
            CReal real => real.Value,
            CInteger integer => integer.Value,
            _ => throw new InvalidOperationException($"Expected a number, got {operand.GetType().Name}.")
        };
    }

    private static DiffOutput DiffPage(PdfDocument document, string filePrefix, int pageNum)
    {
        using var rasterized = PdfHelper.Rasterize(document);
        var rasterizedFiles = PdfHelper.WriteImageCollection(rasterized.ImageCollection, OutDir, filePrefix);
        var expectedImagePath = PathHelper.GetInstance().GetAssetPath(ExpectedImagesPath, $"{filePrefix}_{pageNum}.png");
        return PdfHelper.Diff(rasterizedFiles[pageNum-1], expectedImagePath, OutDir, filePrefix);
    }
}
