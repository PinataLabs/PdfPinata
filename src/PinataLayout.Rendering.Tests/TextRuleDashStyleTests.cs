using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   An underline or a strikethrough that changes its dash style from one run to the next.
/// </summary>
/// <remarks>
///   <para>
///     A rule is drawn as one line for as long as the pen it is drawn with stays the same, and the
///     renderer decides that by comparing the pen of each run with the pen of the rule in progress.
///     It compared the colour and the width and nothing else, so a dotted run followed by a dashed
///     one of the same colour and size was one continuous line, dotted all the way.
///   </para>
///   <para>
///     Read out of the content stream rather than rasterized: which dash pattern each stroke is
///     made under is exact there, where a dotted line and a dashed one at a sixteenth of the font
///     size are a few pixels apart.
///   </para>
/// </remarks>
public class TextRuleDashStyleTests
{
    // Two Hebrew words, for the reordering path. Escapes rather than literals, as in
    // BidirectionalParagraphTests.
    private const string Hebrew = "\u05D0\u05D1 \u05D2\u05D3";

    [Fact]
    public void AnUnderlineChangingFromDottedToDashedIsDrawnAsTwoRules()
    {
        var page = Rendered.FirstPageOf(Runs("dotted ", "dashed",
            text => text.Font.Underline = Underline.Dotted,
            text => text.Font.Underline = Underline.Dash));

        AssertTwoRulesInTwoPatterns(page);
    }

    [Fact]
    public void AStrikethroughChangingFromDottedToDashedIsDrawnAsTwoRules()
    {
        var page = Rendered.FirstPageOf(Runs("dotted ", "dashed",
            text => text.Font.Strikethrough = Strikethrough.Dotted,
            text => text.Font.Strikethrough = Strikethrough.Dash));

        AssertTwoRulesInTwoPatterns(page);
    }

    [Fact]
    public void AnUnchangedDashStyleIsStillOneRule()
    {
        // The other side of the comparison: two runs drawn with the same pen must not be broken
        // apart, or every run boundary would show as a seam in a dotted line.
        var page = Rendered.FirstPageOf(Runs("dotted ", "again",
            text => text.Font.Underline = Underline.Dotted,
            text => text.Font.Underline = Underline.Dotted));

        Strokes(page).Should().ContainSingle("both runs are underlined with the same pen");
    }

    [Fact]
    public void AReorderedUnderlineKeepsEachRunsDashStyle()
    {
        // A line that has to be reordered draws its rules leaf by leaf rather than in stretches,
        // which is the other path through the renderer. It has to honour the change as well.
        var page = Rendered.FirstPageOf(Runs(Hebrew + " ", Hebrew,
            text => text.Font.Underline = Underline.Dotted,
            text => text.Font.Underline = Underline.Dash));

        Strokes(page).Select(stroke => stroke.Dash).Distinct().Should().HaveCount(2,
            "the dotted run's leaves and the dashed run's leaves are each drawn in their own pattern");
    }

    [Fact]
    public void AReorderedStrikethroughKeepsEachRunsDashStyle()
    {
        var page = Rendered.FirstPageOf(Runs(Hebrew + " ", Hebrew,
            text => text.Font.Strikethrough = Strikethrough.Dotted,
            text => text.Font.Strikethrough = Strikethrough.Dash));

        Strokes(page).Select(stroke => stroke.Dash).Distinct().Should().HaveCount(2,
            "the dotted run's leaves and the dashed run's leaves are each drawn in their own pattern");
    }

    private static void AssertTwoRulesInTwoPatterns(PdfPage page)
    {
        var strokes = Strokes(page);

        strokes.Should().HaveCount(2, "the rule changes pen where the second run begins");
        strokes[0].Dash.Should().NotBe(strokes[1].Dash, "the first rule is dotted and the second dashed");
    }

    private static Document Runs(string first, string second,
        Action<FormattedText> styleFirst, Action<FormattedText> styleSecond)
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        styleFirst(paragraph.AddFormattedText(first));
        styleSecond(paragraph.AddFormattedText(second));

        return document;
    }

    /// <summary>
    ///   Every stroke on the page, with the dash pattern in force when it was made - following q
    ///   and Q, so that a pattern set inside a saved state stops applying after it.
    /// </summary>
    private static List<(string Dash, int Written)> Strokes(PdfPage page)
    {
        var strokes = new List<(string Dash, int Written)>();
        var dash = "[] 0";
        var saved = new Stack<string>();

        foreach (var op in ContentReader.ReadContent(PageContent.Of(page)).OfType<COperator>())
        {
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.q:
                    saved.Push(dash);
                    break;

                case OpCodeName.Q:
                    if (saved.Count > 0)
                        dash = saved.Pop();
                    break;

                case OpCodeName.d:
                    dash = Pattern(op.Operands);
                    break;

                case OpCodeName.S:
                case OpCodeName.s:
                    strokes.Add((dash, strokes.Count));
                    break;
            }
        }

        return strokes;
    }

    private static string Pattern(CSequence operands)
    {
        return string.Join(" ", operands.Select(operand => operand switch
        {
            CArray array => "[" + string.Join(" ", array.OfType<CNumber>().Select(Format)) + "]",
            CNumber number => Format(number),
            _ => operand.ToString()
        }));
    }

    private static string Format(CNumber number) => number switch
    {
        CInteger integer => integer.Value.ToString(CultureInfo.InvariantCulture),
        CReal real => real.Value.ToString("0.###", CultureInfo.InvariantCulture),
        _ => number.ToString()
    };
}
