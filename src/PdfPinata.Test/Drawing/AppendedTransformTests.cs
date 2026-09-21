using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   Every transform on <see cref="XGraphics"/> takes an <see cref="XMatrixOrder"/>, and the PDF
///   <c>cm</c> operator can only prepend. <see cref="XGraphics.Transform"/> always honoured an
///   appended transform, but the page was handed it as a prepend, so the two disagreed and the
///   ink landed somewhere <see cref="XGraphics.Transform"/> said it would not. These tests read
///   the <c>cm</c> operators back out of the content stream and follow a point through them.
/// </summary>
public class AppendedTransformTests
{
    [Fact]
    public void AScaleAppendedAfterATranslationScalesTheTranslationToo()
    {
        var (page, gfx) = OnAPage();
        using (gfx)
        {
            gfx.TranslateTransform(100, 50);
            gfx.ScaleTransform(2, 2, XMatrixOrder.Append);
            gfx.DrawLine(XPens.Black, 0, 0, 10, 0);
        }

        // World (0, 0) is translated to (100, 50) and then scaled to (200, 100); a prepended
        // scale would have left it at (100, 50). The line is 10 long before the scale.
        var (start, end) = LineOn(page);
        start.X.Should().BeApproximately(200, 0.01);
        start.Y.Should().BeApproximately(page.Height.Point - 100, 0.01);
        end.X.Should().BeApproximately(220, 0.01);
        end.Y.Should().BeApproximately(page.Height.Point - 100, 0.01);
    }

    [Fact]
    public void ARotationAppendedAfterATranslationTurnsAboutThePageOrigin()
    {
        var (page, gfx) = OnAPage();
        using (gfx)
        {
            gfx.TranslateTransform(100, 0);
            gfx.RotateTransform(90, XMatrixOrder.Append);
            gfx.DrawLine(XPens.Black, 0, 0, 10, 0);
        }

        // A quarter turn clockwise on a downward page carries (100, 0) to (0, 100) and the line
        // from pointing right to pointing down. Prepended, the line would start at (100, 0).
        var (start, end) = LineOn(page);
        start.X.Should().BeApproximately(0, 0.01);
        start.Y.Should().BeApproximately(page.Height.Point - 100, 0.01);
        end.X.Should().BeApproximately(0, 0.01);
        end.Y.Should().BeApproximately(page.Height.Point - 110, 0.01);
    }

    [Theory]
    [InlineData(XMatrixOrder.Prepend)]
    [InlineData(XMatrixOrder.Append)]
    public void ThePageAgreesWithTheTransformTheSurfaceReports(XMatrixOrder order)
    {
        var (page, gfx) = OnAPage();
        XMatrix reported;
        using (gfx)
        {
            gfx.TranslateTransform(40, 30);
            gfx.RotateTransform(30, order);
            gfx.ScaleTransform(1.5, 0.5, order);
            gfx.TranslateTransform(-10, 20, order);
            reported = gfx.Transform;
            gfx.DrawLine(XPens.Black, 0, 0, 10, 0);
        }

        var (start, end) = LineOn(page);
        var expectedStart = reported.Transform(new XPoint(0, 0));
        var expectedEnd = reported.Transform(new XPoint(10, 0));
        start.X.Should().BeApproximately(expectedStart.X, 0.01);
        start.Y.Should().BeApproximately(page.Height.Point - expectedStart.Y, 0.01);
        end.X.Should().BeApproximately(expectedEnd.X, 0.01);
        end.Y.Should().BeApproximately(page.Height.Point - expectedEnd.Y, 0.01);
    }

    [Fact]
    public void AppendingToAMatrixWithNoInverseIsRefusedAndChangesNothing()
    {
        var (_, gfx) = OnAPage();
        using (gfx)
        {
            gfx.ScaleTransform(0, 1);
            var before = gfx.Transform;

            var append = () => gfx.TranslateTransform(10, 10, XMatrixOrder.Append);

            append.Should().Throw<InvalidOperationException>();
            gfx.Transform.Should().Be(before);
        }
    }

    static (PdfPage, XGraphics) OnAPage()
    {
        var page = new PdfDocument().AddPage();
        return (page, XGraphics.FromPdfPage(page));
    }

    /// <summary>
    ///   Both ends of the one line drawn on the page, in default user space: the <c>m</c> and
    ///   <c>l</c> points carried through every <c>cm</c> in force where they were written.
    /// </summary>
    static (XPoint Start, XPoint End) LineOn(PdfPage page)
    {
        var ctm = new XMatrix();
        var saved = new Stack<XMatrix>();
        XPoint? start = null;

        foreach (var op in ContentReader.ReadContent(PageContent.Of(page)).OfType<COperator>())
        {
            var operands = op.Operands.Select(Number).ToArray();
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.q:
                    saved.Push(ctm);
                    break;

                case OpCodeName.Q:
                    ctm = saved.Pop();
                    break;

                case OpCodeName.cm:
                    ctm.Prepend(new XMatrix(operands[0], operands[1], operands[2], operands[3],
                        operands[4], operands[5]));
                    break;

                case OpCodeName.m:
                    start = ctm.Transform(new XPoint(operands[0], operands[1]));
                    break;

                case OpCodeName.l:
                    return (start!.Value, ctm.Transform(new XPoint(operands[0], operands[1])));
            }
        }

        throw new InvalidOperationException("The page draws no line.");
    }

    static double Number(CObject operand)
    {
        return operand switch
        {
            CInteger integer => integer.Value,
            CReal real => real.Value,
            _ => 0.0
        };
    }
}
