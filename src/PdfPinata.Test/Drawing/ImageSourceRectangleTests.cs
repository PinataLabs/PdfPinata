using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XGraphics.DrawImage(XImage, XRect, XRect, XGraphicsUnit)"/> draws the part of an image its
///   source rectangle names. It used to ignore that rectangle and draw the whole image into the
///   destination, so a crop came out as the uncropped image squeezed to the cropped size. PDF has no
///   operator for part of an image, so the whole of it is drawn, scaled and moved so the part asked for
///   lands on the destination, and clipped there. These read the placement and the clip back out of the
///   content stream.
/// </summary>
public class ImageSourceRectangleTests
{
    static XImage AnImage() => XImage.FromFile(PathHelper.GetInstance().GetAssetPath("frog-and-toad.jpg"));

    [Fact]
    public void TheRightHalfOfAnImageFillsTheDestination()
    {
        var image = AnImage();
        var (w, h) = (image.PointWidth, image.PointHeight);
        var dest = new XRect(100, 100, 200, 200);

        var page = PageShowing(gfx => gfx.DrawImage(image, dest, new XRect(w / 2, 0, w / 2, h), XGraphicsUnit.Point));

        // Half the width is stretched over 200 points, so the whole image is 400 wide and starts 200
        // to the left of the destination; its height is the destination's.
        var placed = PlacedOperators.Of(page);
        var drawn = Box(placed.Single(op => op.Name == OpCodeName.Do).Ctm, 0, 0, 1, 1);
        ShouldBe(drawn, page, new XRect(-100, 100, 400, 200));
        ShouldBe(ClipBefore(placed), page, dest);
    }

    [Fact]
    public void TheSourceRectangleIsMeasuredInTheUnitItIsGiven()
    {
        var image = AnImage();
        var (w, h) = (image.PointWidth, image.PointHeight);
        var dest = new XRect(100, 100, 50, 50);
        var inInches = new XRect(0, 0, w / 2 / 72, h / 2 / 72);

        var page = PageShowing(gfx => gfx.DrawImage(image, dest, inInches, XGraphicsUnit.Inch));

        // The top left quarter: the whole image is twice the destination each way, from its corner.
        var drawn = Box(PlacedOperators.Of(page).Single(op => op.Name == OpCodeName.Do).Ctm, 0, 0, 1, 1);
        ShouldBe(drawn, page, new XRect(100, 100, 100, 100));
    }

    [Fact]
    public void AskedForTheWholeImageItDrawsExactlyWhatThePlainOverloadDoes()
    {
        var image = AnImage();
        var whole = new XRect(0, 0, image.PointWidth, image.PointHeight);

        var plain = PageShowing(gfx => gfx.DrawImage(image, 100, 100, 200, 150));
        var viaSource = PageShowing(gfx =>
            gfx.DrawImage(image, new XRect(100, 100, 200, 150), whole, XGraphicsUnit.Point));

        Encoding.ASCII.GetString(PageContent.Of(viaSource)).Should()
            .Be(Encoding.ASCII.GetString(PageContent.Of(plain)), "no clip is needed to draw all of it");
    }

    [Fact]
    public void PartOfAFormIsDrawnTheSameWay()
    {
        var page = PageShowing(gfx =>
        {
            var form = new XForm(gfx.PdfPage.Owner, 100, 50);
            using (var inside = XGraphics.FromForm(form))
                inside.DrawRectangle(XBrushes.Black, 0, 0, 100, 50);
            gfx.DrawImage(form, new XRect(200, 200, 100, 100), new XRect(50, 0, 50, 50), XGraphicsUnit.Point);
        });

        // The right half of a 100 by 50 form, doubled: the whole form is 200 by 100 and starts 100 to
        // the left of the destination.
        var placed = PlacedOperators.Of(page);
        var drawn = Box(placed.Single(op => op.Name == OpCodeName.Do).Ctm, 0, 0, 100, 50);
        ShouldBe(drawn, page, new XRect(100, 200, 200, 100));
        ShouldBe(ClipBefore(placed), page, new XRect(200, 200, 100, 100));
    }

    [Fact]
    public void NothingIsDrawnForAnEmptySourceRectangle()
    {
        var image = AnImage();

        var page = PageShowing(gfx =>
            gfx.DrawImage(image, new XRect(100, 100, 50, 50), new XRect(0, 0, 0, 10), XGraphicsUnit.Point));

        PlacedOperators.Of(page).Should().NotContain(op => op.Name == OpCodeName.Do);
    }

    [Fact]
    public void AnImageThatCannotBeDrawnLeavesNoClipBehindIt()
    {
        var image = AnImage();

        var page = PageShowing(gfx =>
        {
            gfx.DrawRectangle(XBrushes.Black, 10, 10, 20, 20);
            var draw = () => gfx.DrawImage(image, new XRect(double.NaN, 100, 50, 50),
                new XRect(0, 0, 10, 10), XGraphicsUnit.Point);
            draw.Should().Throw<Exception>();
            gfx.DrawRectangle(XBrushes.Black, 40, 10, 20, 20);
        });

        // Both rectangles are drawn at the same depth of saved states: the one after the failure is
        // not inside the state the image opened for its clip.
        var depths = new List<int>();
        var depth = 0;
        foreach (var op in PlacedOperators.Of(page))
        {
            if (op.Name == OpCodeName.q) depth++;
            else if (op.Name == OpCodeName.Q) depth--;
            else if (op.Name == OpCodeName.re) depths.Add(depth);
        }
        depths.Should().HaveCount(2).And.OnlyContain(d => d == depths[0]);
    }

    static PdfPage PageShowing(Action<XGraphics> draw)
    {
        var page = new PdfDocument().AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);
        return page;
    }

    /// <summary>The box in default user space a rectangle of the given space covers under a matrix.</summary>
    static XRect Box(XMatrix ctm, double x0, double y0, double x1, double y1)
    {
        var corners = new[]
        {
            ctm.Transform(new XPoint(x0, y0)), ctm.Transform(new XPoint(x1, y0)),
            ctm.Transform(new XPoint(x0, y1)), ctm.Transform(new XPoint(x1, y1))
        };
        return BoxAround(corners);
    }

    /// <summary>The box around the clipping path set last before the image, in default user space.</summary>
    static XRect ClipBefore(List<PlacedOperators.Placed> placed)
    {
        var clip = placed.FindIndex(op => op.Name is OpCodeName.W or OpCodeName.Wx);
        clip.Should().BePositive("the part of the image drawn is clipped");

        var start = placed.FindLastIndex(clip, op => op.Name is OpCodeName.m or OpCodeName.re);
        if (placed[start].Name == OpCodeName.re)
        {
            var re = placed[start];
            return Box(re.Ctm, re.Operands[0], re.Operands[1],
                re.Operands[0] + re.Operands[2], re.Operands[1] + re.Operands[3]);
        }

        var points = placed.Skip(start).Take(clip - start)
            .Where(op => op.Name is OpCodeName.m or OpCodeName.l)
            .Select(op => op.At(0));
        return BoxAround(points);
    }

    static XRect BoxAround(IEnumerable<XPoint> points)
    {
        var all = points.ToList();
        var left = all.Min(p => p.X);
        var bottom = all.Min(p => p.Y);
        return new XRect(left, bottom, all.Max(p => p.X) - left, all.Max(p => p.Y) - bottom);
    }

    /// <summary>Compares a box in default user space with one given in the downward world space drawn in.</summary>
    static void ShouldBe(XRect actual, PdfPage page, XRect world)
    {
        actual.X.Should().BeApproximately(world.X, 0.01);
        actual.Width.Should().BeApproximately(world.Width, 0.01);
        actual.Y.Should().BeApproximately(page.Height.Point - world.Bottom, 0.01);
        actual.Height.Should().BeApproximately(world.Height, 0.01);
    }
}
