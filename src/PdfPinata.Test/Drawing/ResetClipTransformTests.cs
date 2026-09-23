using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   PDF cannot take a clip away, only restore the state saved before it — so the renderer's
///   <c>ResetClip</c> writes a <c>Q</c>, and that <c>Q</c> also throws away every <c>cm</c> written since
///   the clip was set. A transform applied after the clip still has to hold for what is drawn after
///   the reset.
/// </summary>
/// <remarks>
///   <see cref="XGraphics"/> offers no way to reset a clip, only to intersect one, so these reach the
///   renderer through the public <see cref="IXGraphicsRenderer"/> it implements. The field holding it
///   is private and this repository carries no <c>InternalsVisibleTo</c>, hence the reflection.
/// </remarks>
public class ResetClipTransformTests
{
    [Fact]
    public void ATransformDrawnWithInsideAClipOutlivesTheReset()
    {
        var page = new PdfDocument().AddPage();
        XMatrix reported;
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.IntersectClip(new XRect(0, 0, 300, 300));
            gfx.TranslateTransform(100, 50);
            gfx.DrawLine(XPens.Black, 0, 0, 10, 0);
            RendererOf(gfx).ResetClip();
            reported = gfx.Transform;
            gfx.DrawLine(XPens.Black, 0, 0, 10, 0);
        }

        // The clip rectangle is written as a path too; the two lines are the last two moves.
        var moves = PlacedOperators.Of(page).Where(op => op.Name == OpCodeName.m).TakeLast(2).ToList();

        var expected = reported.Transform(new XPoint(0, 0));
        foreach (var move in moves)
        {
            var at = move.At(0);
            at.X.Should().BeApproximately(expected.X, 0.01);
            at.Y.Should().BeApproximately(page.Height.Point - expected.Y, 0.01);
        }
    }

    [Fact]
    public void ATransformNotYetDrawnWithInsideAClipOutlivesTheReset()
    {
        var page = new PdfDocument().AddPage();
        XMatrix reported;
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.IntersectClip(new XRect(0, 0, 300, 300));
            gfx.DrawLine(XPens.Black, 0, 0, 10, 0);
            gfx.TranslateTransform(100, 50);
            RendererOf(gfx).ResetClip();
            reported = gfx.Transform;
            gfx.DrawLine(XPens.Black, 0, 0, 10, 0);
        }

        var last = PlacedOperators.Of(page).Last(op => op.Name == OpCodeName.m).At(0);
        var expected = reported.Transform(new XPoint(0, 0));
        last.X.Should().BeApproximately(expected.X, 0.01);
        last.Y.Should().BeApproximately(page.Height.Point - expected.Y, 0.01);
    }

    private static IXGraphicsRenderer RendererOf(XGraphics gfx)
    {
        var field = typeof(XGraphics).GetField("_renderer", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (IXGraphicsRenderer)field.GetValue(gfx)!;
    }
}
