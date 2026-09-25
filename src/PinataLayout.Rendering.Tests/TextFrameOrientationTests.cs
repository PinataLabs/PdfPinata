using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   A text frame whose content is turned on its side.
/// </summary>
/// <remarks>
///   The frame saves the graphics state before it rotates its content and restores it afterwards.
///   The upward case saved it a second time over the first and restored only the second, so every
///   upward frame left a q open behind it, and everything drawn after the frame on that page ran one
///   level deeper than it should.
/// </remarks>
public class TextFrameOrientationTests
{
    [Theory]
    [InlineData(TextOrientation.Horizontal)]
    [InlineData(TextOrientation.Upward)]
    [InlineData(TextOrientation.Downward)]
    public void TheFrameRestoresEveryGraphicsStateItSaves(TextOrientation orientation)
    {
        var page = Rendered.FirstPageOf(Framed(orientation));

        var depths = TextObjectDepths(page);

        depths.Should().HaveCount(3, "one text object before the frame, one in it and one after");
        depths[2].Should().Be(depths[0],
            "content drawn after the frame is at the depth the content before it was");
        TextOperators.CountOf(page, OpCodeName.q).Should().Be(TextOperators.CountOf(page, OpCodeName.Q),
            "every q the page writes is matched by a Q");
    }

    private static Document Framed(TextOrientation orientation)
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("Before");

        var frame = section.AddTextFrame();
        frame.Width = Unit.FromCentimeter(4);
        frame.Height = Unit.FromCentimeter(4);
        frame.Orientation = orientation;
        frame.AddParagraph("Turned");

        section.AddParagraph("After");

        return document;
    }

    /// <summary>How many graphics states are saved at each BT on the page, in the order written.</summary>
    private static List<int> TextObjectDepths(PdfPage page)
    {
        var depths = new List<int>();
        var depth = 0;

        foreach (var op in ContentReader.ReadContent(PageContent.Of(page)).OfType<COperator>())
        {
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.q:
                    depth++;
                    break;

                case OpCodeName.Q:
                    depth--;
                    break;

                case OpCodeName.BT:
                    depths.Add(depth);
                    break;
            }
        }

        return depths;
    }
}
