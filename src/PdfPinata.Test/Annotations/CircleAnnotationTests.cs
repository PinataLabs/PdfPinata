using System;
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
///   <see cref="PdfCircleAnnotation"/>, which shares everything but its shape with
///   <see cref="PdfSquareAnnotation"/>.
/// </summary>
/// <remarks>
///   The tests that matter here are the ones a square would fail. Everything else - the interior
///   colour, the border, <c>/RD</c>, rebuilding the appearance when something changes - belongs to
///   <see cref="PdfSquareCircleAnnotation"/> and is covered by <see cref="SquareAnnotationTests"/>;
///   repeating it would only pin the same code twice.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class CircleAnnotationTests : IDisposable
{
    private const string OutDir = "Out/CircleAnnotations";

    private readonly Rasterizations _rasterized = new(OutDir);

    public void Dispose() => _rasterized.Dispose();

    static CircleAnnotationTests()
    {
        GhostscriptSetup.Configure();
    }

    /// <summary>A wide rectangle, so that "circle" is visibly an ellipse inscribed in it.</summary>
    private static readonly XRect Where = new(60, 60, 200, 100);

    [Fact]
    public void ACircleNamesItsSubtype()
    {
        var document = new PdfDocument();
        var circle = new PdfCircleAnnotation();
        document.AddPage().Annotations.Add(circle);

        circle.Elements.GetName("/Subtype").Should().Be("/Circle");
    }

    [Fact]
    public void ACircleCarriesTheSameInteriorAndBorderAsASquare()
    {
        var document = new PdfDocument();
        var circle = new PdfCircleAnnotation();
        document.AddPage().Annotations.Add(circle);
        circle.Rectangle = new PdfRectangle(Where);

        circle.Interior = XColors.SeaGreen;
        circle.BorderWidth = 8;

        // Written by the shared base, so this is a check that a circle really does inherit it
        // rather than a second test of the same code.
        circle.Elements.GetArray("/IC").Elements.Count.Should().Be(3);
        circle.Elements.GetArray("/RD").Elements.GetReal(0).Should().Be(4);
        circle.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ASubtypeIsRequiredOfWhateverDerivesFromTheSharedBase(string subtype)
    {
        // The constructors are protected on a public class, so a subtype can come from outside
        // this assembly. A dictionary with no /Subtype is ignored by readers, and the mistake
        // would otherwise surface far away as "the annotation does nothing".
        Action act = () => _ = new Nameless(subtype);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AskingForNothingClearsTheAppearanceStateAsWellAsTheAppearance()
    {
        var document = new PdfDocument();
        var circle = new PdfCircleAnnotation();
        document.AddPage().Annotations.Add(circle);
        circle.Rectangle = new PdfRectangle(Where);

        // A named appearance, which is what puts /AS on the annotation in the first place.
        circle.SetAppearance("/On", new XForm(document, Where.Size));
        circle.Elements.ContainsKey("/AS").Should().BeTrue();

        circle.BorderWidth = 0;
        circle.Interior = XColor.Empty;

        // /AS naming a state in an /AP that is no longer there is a dictionary a reader has to
        // guess at, so both go together.
        circle.Elements.ContainsKey("/AP").Should().BeFalse();
        circle.Elements.ContainsKey("/AS").Should().BeFalse();
    }

    /// <summary>A shape that names no subtype, which is the thing the base has to refuse.</summary>
    private sealed class Nameless : PdfSquareCircleAnnotation
    {
        public Nameless(string subtype)
            : base(subtype)
        { }

        protected override void DrawShape(XGraphics gfx, XPen pen, XBrush brush, XRect box)
        {
        }
    }

    [GoldenImageFact]
    public void AFilledCircleIsAnEllipseInscribedInTheRectangle()
    {
        var page = Rasterize("filled", circle =>
        {
            circle.Interior = XColors.SeaGreen;
            circle.BorderWidth = 0;
        });

        // The middle is inside the ellipse.
        IsGreen(At(page, Where.X + Where.Width / 2, Where.Y + Where.Height / 2)).Should().BeTrue();

        // And the corners of the rectangle are outside it, which is the whole of the difference
        // between this and PdfSquareAnnotation - a square would have painted all four.
        foreach ((var x, var y) in new[]
                 {
                     (Where.X + 4, Where.Y + 4),
                     (Where.Right - 4, Where.Y + 4),
                     (Where.X + 4, Where.Bottom - 4),
                     (Where.Right - 4, Where.Bottom - 4)
                 })
        {
            var corner = At(page, x, y);
            IsGreen(corner).Should().BeFalse("the corner at {0},{1} is outside the ellipse", x, y);
        }
    }

    [GoldenImageFact]
    public void AnUnfilledCircleIsAnOutlineWithAnEmptyMiddle()
    {
        var page = Rasterize("outline", circle =>
        {
            circle.Color = XColors.SeaGreen;
            circle.BorderWidth = 5;
        });

        Count(page, IsGreen).Should().BeGreaterThan(200);
        IsWhite(At(page, Where.X + Where.Width / 2, Where.Y + Where.Height / 2)).Should().BeTrue();
    }

    private IMagickImage<byte> Rasterize(string name, Action<PdfCircleAnnotation> arrange)
    {
        GlobalFontSettings.FontResolver ??= new PinnedFontResolver();

        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        var circle = new PdfCircleAnnotation();
        page.Annotations.Add(circle);
        circle.Rectangle = new PdfRectangle(gfx.Transformer.WorldToDefaultPage(Where));

        arrange(circle);

        return _rasterized.FirstPageOf(document, name);
    }

    /// <summary>
    ///   The pixel at a place on the page, given in the same world coordinates the drawing uses.
    /// </summary>
    private static IMagickColor<byte> At(IMagickImage<byte> image, double x, double y)
    {
        var scale = image.Width / PageSizeConverter.ToSize(PageSize.A4).Width;

        using var pixels = image.GetPixels();
        return pixels.GetPixel((int)(x * scale), (int)(y * scale)).ToColor();
    }

    private static bool IsGreen(IMagickColor<byte> c) => c.G > 90 && c.R < 120 && c.B < 140;

    private static bool IsWhite(IMagickColor<byte> c) => c.R > 240 && c.G > 240 && c.B > 240;

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
