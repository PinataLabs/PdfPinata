using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   <see cref="PdfSquareAnnotation"/>: the dictionary it writes, and whether a reader paints it.
/// </summary>
/// <remarks>
///   The second half is the point. A <c>/Square</c> is drawn from its appearance stream and from
///   nothing else, so one carrying a rectangle, a colour and a border width is well formed and
///   rasterizes to nothing at all. This class exists to build that appearance, which means the
///   test that matters is one that counts pixels rather than keys.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class SquareAnnotationTests : IDisposable
{
    private const string OutDir = "Out/SquareAnnotations";

    private readonly List<MagickImageCollection> _rasterized = new();

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    static SquareAnnotationTests()
    {
        GhostscriptSetup.Configure();
    }

    private static readonly XRect Where = new(40, 40, 120, 80);

    [Fact]
    public void ASquareNamesItsSubtypeAndCarriesADefaultBorder()
    {
        var square = OnAPage();

        square.Elements.GetName("/Subtype").Should().Be("/Square");
        square.BorderWidth.Should().Be(1);

        // A square given nothing but a rectangle still has to appear, so the defaults draw
        // something rather than nothing.
        var border = square.Elements.GetDictionary("/BS");
        border.Elements.GetReal("/W").Should().Be(1);
    }

    [Fact]
    public void AnUnfilledSquareSaysSoWithAnEmptyArray()
    {
        var square = OnAPage();

        // The specification's way of saying "no interior colour" - and not the same as saying
        // nothing, which would leave a reader to guess.
        square.Interior.Should().Be(XColor.Empty);
        square.Elements.GetArray("/IC").Elements.Count.Should().Be(0);
    }

    [Fact]
    public void AFilledSquareWritesItsInteriorColour()
    {
        var square = OnAPage();

        square.Interior = XColors.RoyalBlue;

        var colour = square.Elements.GetArray("/IC");
        colour.Elements.Count.Should().Be(3);
        colour.Elements.GetReal(0).Should().BeApproximately(65 / 255.0, 0.01);
        colour.Elements.GetReal(2).Should().BeApproximately(225 / 255.0, 0.01);
    }

    [Fact]
    public void TheBorderIsDrawnInsideTheRectangleAndRecordedInRd()
    {
        var square = OnAPage();

        square.BorderWidth = 6;

        // Half the width on each side, because a stroke straddles the path it follows. Without
        // this the outer half of a wide border falls outside the annotation and is clipped.
        var differences = square.Elements.GetArray("/RD");
        differences.Elements.Count.Should().Be(4);
        foreach (var side in new[] { 0, 1, 2, 3 })
            differences.Elements.GetReal(side).Should().Be(3);
    }

    [Fact]
    public void ChangingTheInteriorOrTheBorderStampsTheModificationDate()
    {
        var square = OnAPage();

        // Rewound rather than read, so that the assertion does not turn on the clock ticking
        // between two statements.
        var before = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        square.Elements.SetDateTime("/M", before);
        square.Interior = XColors.RoyalBlue;
        square.Elements.GetDateTime("/M", DateTime.MinValue).Should().BeAfter(before);

        square.Elements.SetDateTime("/M", before);
        square.BorderWidth = 3;
        square.Elements.GetDateTime("/M", DateTime.MinValue).Should().BeAfter(before);
    }

    [Fact]
    public void ANegativeBorderIsRefused()
    {
        var square = OnAPage();

        Action act = () => square.BorderWidth = -1;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TheAppearanceIsBuiltWhenTheAnnotationReachesAPage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var square = new PdfSquareAnnotation();
        square.Interior = XColors.RoyalBlue;
        square.Rectangle = new PdfRectangle(Where);

        // Everything above was set with no document to build a form in. Adding it to the page is
        // what gives it one, and the appearance has to appear then rather than be lost.
        square.Elements.ContainsKey("/AP").Should().BeFalse();

        page.Annotations.Add(square);

        square.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [Fact]
    public void ChangingWhatItIsDrawnFromRebuildsTheAppearance()
    {
        var square = OnAPage();

        var first =
            (PdfDictionary)square.Elements.GetDictionary("/AP").Elements.GetObject("/N");
        var before = first.Stream.Value;

        square.Interior = XColors.Firebrick;

        var second =
            (PdfDictionary)square.Elements.GetDictionary("/AP").Elements.GetObject("/N");
        second.Stream.Value.Should().NotEqual(before);
    }

    [GoldenImageFact]
    public void AFilledSquareIsPainted()
    {
        var page = Rasterize("filled", square =>
        {
            square.Interior = XColors.RoyalBlue;
            square.BorderWidth = 0;
        });

        Count(page, IsBlue).Should().BeGreaterThan(1000);
    }

    [GoldenImageFact]
    public void AnUnfilledSquareIsAnOutlineAndNothingMore()
    {
        var page = Rasterize("outline", square =>
        {
            square.Color = XColors.Firebrick;
            square.BorderWidth = 4;
        });

        // The border is there.
        Count(page, IsRed).Should().BeGreaterThan(200);

        // And the middle of it is empty. Sampled rather than counted: how many pixels a 4pt
        // frame comes to depends on the rasterizing resolution, where the centre being white
        // does not, and "unfilled" is a statement about the middle.
        Centre(page, Where).Should().Match<IMagickColor<byte>>(
            c => c.R > 240 && c.G > 240 && c.B > 240);
    }

    [GoldenImageFact]
    public void ASquareWithNoBorderAndNoFillDrawsNothing()
    {
        var page = Rasterize("empty", square =>
        {
            square.BorderWidth = 0;
        });

        // Asked for nothing, draws nothing - rather than throwing, or writing an appearance
        // stream that paints an accidental black rectangle.
        Count(page, IsAnythingButWhite).Should().Be(0);
    }

    private IMagickImage<byte> Rasterize(string name, Action<PdfSquareAnnotation> arrange)
    {
        GlobalFontSettings.FontResolver ??= new PinnedFontResolver();

        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        var square = new PdfSquareAnnotation();
        page.Annotations.Add(square);
        square.Rectangle = new PdfRectangle(gfx.Transformer.WorldToDefaultPage(Where));

        arrange(square);

        var images = PdfHelper.Rasterize(document).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, name);
        return images[0];
    }

    private static PdfSquareAnnotation OnAPage()
    {
        var document = new PdfDocument();
        var square = new PdfSquareAnnotation();
        document.AddPage().Annotations.Add(square);

        // Without somewhere to be there is nothing to draw, so nothing derived from the geometry
        // - the appearance, and the /RD that records what the border took from it - is written.
        square.Rectangle = new PdfRectangle(Where);
        return square;
    }

    /// <summary>
    ///   The pixel at the middle of the rectangle, which for an unfilled square is inside the
    ///   frame and nowhere near it.
    /// </summary>
    private static IMagickColor<byte> Centre(IMagickImage<byte> image, XRect box)
    {
        // The page is A4 and the rectangle is placed in world coordinates from the top left,
        // which is the space the image is in too, so this scales straight across.
        var scale = image.Width / PageSizeConverter.ToSize(PageSize.A4).Width;
        var x = (int)((box.X + box.Width / 2) * scale);
        var y = (int)((box.Y + box.Height / 2) * scale);

        using var pixels = image.GetPixels();
        return pixels.GetPixel(x, y).ToColor();
    }

    private static bool IsBlue(IMagickColor<byte> c) => c.B > 150 && c.R < 120 && c.G < 150;

    private static bool IsRed(IMagickColor<byte> c) => c.R > 130 && c.G < 100 && c.B < 100;

    private static bool IsAnythingButWhite(IMagickColor<byte> c) => c.R < 240 || c.G < 240 || c.B < 240;

    private static int Count(IMagickImage<byte> image, Func<IMagickColor<byte>, bool> match)
    {
        using var pixels = image.GetPixels();
        return pixels.Count(p =>
        {
            var c = p.ToColor();
            return c != null && match(c);
        });
    }
}
